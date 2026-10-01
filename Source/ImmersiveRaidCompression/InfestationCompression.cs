using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ImmersiveRaidCompression
{
    [HarmonyPatch(typeof(IncidentWorker_Infestation), "TryExecuteWorker")]
    public static class OrdinaryInfestationContextPatch
    {
        public struct ContextState
        {
            public bool Active;
            public float Points;
        }

        [ThreadStatic] internal static bool Active;
        [ThreadStatic] internal static float Points;

        public static void Prefix(IncidentParms parms, out ContextState __state)
        {
            __state = new ContextState { Active = Active, Points = Points };
            Active = true;
            Points = parms?.points ?? 0f;
        }

        public static Exception Finalizer(Exception __exception, ContextState __state)
        {
            Active = __state.Active;
            Points = __state.Points;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(InfestationUtility), nameof(InfestationUtility.SpawnTunnels))]
    public static class OrdinaryInfestationTunnelPatch
    {
        public static void Prefix(Map map, out HashSet<int> __state)
        {
            __state = OrdinaryInfestationContextPatch.Active && map != null
                ? map.listerThings.AllThings
                    .OfType<TunnelHiveSpawner>()
                    .Select(spawner => spawner.thingIDNumber)
                    .ToHashSet()
                : null;
        }

        public static void Postfix(Map map, HashSet<int> __state)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (__state == null
                || map == null
                || settings == null
                || !settings.enableInfestations
                || OrdinaryInfestationContextPatch.Points < settings.minimumInfestationPoints)
            {
                return;
            }

            List<TunnelHiveSpawner> spawners = map.listerThings.AllThings
                .OfType<TunnelHiveSpawner>()
                .Where(spawner => !__state.Contains(spawner.thingIDNumber))
                .OrderBy(spawner => spawner.thingIDNumber)
                .ToList();
            if (spawners.Count == 0 || Hive.spawnablePawnKinds.NullOrEmpty())
            {
                return;
            }

            if (settings.verboseLogging)
            {
                Log.Message(
                    "[Immersive Raid Compression] ordinary infestation eligibility: tunnels="
                    + spawners.Count + ", incidentPoints="
                    + OrdinaryInfestationContextPatch.Points.ToString("F0") + ", cap="
                    + settings.infestationSoftPawnCap + ".");
            }
            if (spawners.Count <= settings.infestationSoftPawnCap)
            {
                return;
            }

            Current.Game?.GetComponent<InfestationCompressionComponent>()?.Register(
                map,
                spawners,
                OrdinaryInfestationContextPatch.Points);
        }
    }

    [HarmonyPatch(typeof(TunnelHiveSpawner), "Spawn")]
    public static class OrdinaryInfestationSpawnPatch
    {
        public static bool Prefix(TunnelHiveSpawner __instance, Map map, IntVec3 loc)
        {
            InfestationCompressionComponent component = Current.Game?
                .GetComponent<InfestationCompressionComponent>();
            if (component == null
                || !component.TryGetPlan(
                    __instance.thingIDNumber,
                    out string eventId,
                    out List<PawnKindDef> original,
                    out List<PawnKindDef> compressed))
            {
                return true;
            }

            SpawnFromComposition(__instance, map, loc, compressed);
            component.RecordSpawn(
                eventId,
                __instance.thingIDNumber);
            if (CompressionMod.Settings?.verboseLogging == true)
            {
                Log.Message(
                    "[Immersive Raid Compression] ordinary infestation tunnel compressed: "
                    + original.Count + " -> " + compressed.Count + " insects.");
            }
            return false;
        }

        private static void SpawnFromComposition(
            TunnelHiveSpawner spawner,
            Map map,
            IntVec3 loc,
            List<PawnKindDef> composition)
        {
            if (spawner.spawnHive)
            {
                Hive hive = HiveUtility.SpawnHive(
                    loc,
                    map,
                    WipeMode.FullRefund,
                    false,
                    true,
                    true,
                    false,
                    true,
                    true,
                    false);
                hive.questTags = spawner.questTags;
            }

            List<Pawn> pawns = new List<Pawn>();
            foreach (PawnKindDef kind in composition)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfInsects);
                GenSpawn.Spawn(
                    pawn,
                    CellFinder.RandomClosewalkCellNear(loc, map, 2),
                    map,
                    WipeMode.Vanish);
                pawn.mindState.spawnedByInfestationThingComp = spawner.spawnedByInfestationThingComp;
                pawns.Add(pawn);
                if (ModsConfig.BiotechActive)
                {
                    PollutionUtility.Notify_TunnelHiveSpawnedInsect(pawn);
                }
            }

            if (pawns.Count > 0)
            {
                LordMaker.MakeNewLord(
                    Faction.OfInsects,
                    new LordJob_AssaultColony(
                        Faction.OfInsects,
                        true,
                        false,
                        false,
                        false,
                        true,
                        false,
                        false),
                    map,
                    pawns);
            }
        }
    }

    public static class InsectCompositionPlanner
    {
        private const float MinimumRetainedFraction = 0.95f;
        private const float MaximumRetainedFraction = 1.05f;

        public static List<PawnKindDef> GenerateVanillaComposition(float insectPoints)
        {
            List<PawnKindDef> result = new List<PawnKindDef>();
            if (Hive.spawnablePawnKinds.NullOrEmpty())
            {
                return result;
            }

            float pointsLeft = Mathf.Max(
                insectPoints,
                Hive.spawnablePawnKinds.Min(kind => kind.combatPower));
            for (int attempts = 0; pointsLeft > 0f && attempts < 1000; attempts++)
            {
                if (!Hive.spawnablePawnKinds
                    .Where(kind => kind.combatPower <= pointsLeft)
                    .TryRandomElement(out PawnKindDef kind))
                {
                    break;
                }
                result.Add(kind);
                pointsLeft -= kind.combatPower;
            }
            return result;
        }

        public static List<PawnKindDef> TryCompress(IReadOnlyList<PawnKindDef> original)
        {
            if (original == null || original.Count < 2 || Hive.spawnablePawnKinds.NullOrEmpty())
            {
                return original?.ToList() ?? new List<PawnKindDef>();
            }

            List<PawnKindDef> candidates = Hive.spawnablePawnKinds
                .Where(kind => kind != null && kind.combatPower > 0f)
                .Distinct()
                .OrderByDescending(kind => kind.combatPower)
                .ThenBy(kind => kind.defName)
                .ToList();
            Dictionary<PawnKindDef, int> minimumCounts = original
                .GroupBy(kind => kind)
                .ToDictionary(group => group.Key, _ => 1);
            if (minimumCounts.Keys.Any(kind => !candidates.Contains(kind)))
            {
                return original.ToList();
            }

            float originalPower = original.Sum(kind => kind.combatPower);
            float minimumPower = originalPower * MinimumRetainedFraction;
            float maximumPower = originalPower * MaximumRetainedFraction;
            List<PawnKindDef> best = null;
            float bestDifference = float.MaxValue;
            int minimumCount = minimumCounts.Count;
            for (int targetCount = minimumCount; targetCount < original.Count; targetCount++)
            {
                int[] counts = new int[candidates.Count];
                SearchCompositions(
                    candidates,
                    minimumCounts,
                    counts,
                    0,
                    targetCount,
                    minimumPower,
                    maximumPower,
                    originalPower,
                    ref best,
                    ref bestDifference);
                if (best != null)
                {
                    return best;
                }
            }

            return original.ToList();
        }

        private static void SearchCompositions(
            IReadOnlyList<PawnKindDef> candidates,
            IReadOnlyDictionary<PawnKindDef, int> minimumCounts,
            int[] counts,
            int index,
            int remainingCount,
            float minimumPower,
            float maximumPower,
            float originalPower,
            ref List<PawnKindDef> best,
            ref float bestDifference)
        {
            if (index == candidates.Count - 1)
            {
                int minimum = minimumCounts.TryGetValue(candidates[index], out int finalRequired)
                    ? finalRequired
                    : 0;
                if (remainingCount < minimum)
                {
                    return;
                }
                counts[index] = remainingCount;
                Evaluate(
                    candidates,
                    counts,
                    minimumPower,
                    maximumPower,
                    originalPower,
                    ref best,
                    ref bestDifference);
                return;
            }

            int requiredHere = minimumCounts.TryGetValue(candidates[index], out int required) ? required : 0;
            int requiredLater = candidates
                .Skip(index + 1)
                .Sum(kind => minimumCounts.TryGetValue(kind, out int count) ? count : 0);
            int maximumHere = remainingCount - requiredLater;
            for (int count = requiredHere; count <= maximumHere; count++)
            {
                counts[index] = count;
                SearchCompositions(
                    candidates,
                    minimumCounts,
                    counts,
                    index + 1,
                    remainingCount - count,
                    minimumPower,
                    maximumPower,
                    originalPower,
                    ref best,
                    ref bestDifference);
            }
        }

        private static void Evaluate(
            IReadOnlyList<PawnKindDef> candidates,
            IReadOnlyList<int> counts,
            float minimumPower,
            float maximumPower,
            float originalPower,
            ref List<PawnKindDef> best,
            ref float bestDifference)
        {
            float power = 0f;
            for (int index = 0; index < candidates.Count; index++)
            {
                power += candidates[index].combatPower * counts[index];
            }
            if (power < minimumPower || power > maximumPower)
            {
                return;
            }

            float difference = Mathf.Abs(power - originalPower);
            if (difference + 0.01f >= bestDifference)
            {
                return;
            }

            bestDifference = difference;
            best = new List<PawnKindDef>();
            for (int index = 0; index < candidates.Count; index++)
            {
                for (int count = 0; count < counts[index]; count++)
                {
                    best.Add(candidates[index]);
                }
            }
        }
    }

    public sealed class InfestationCompressionComponent : GameComponent
    {
        private List<InfestationCompressionEvent> events = new List<InfestationCompressionEvent>();

        public InfestationCompressionComponent(Game game)
        {
        }

        public void Register(Map map, IEnumerable<TunnelHiveSpawner> spawners, float incidentPoints)
        {
            List<TunnelHiveSpawner> ordered = spawners
                .Where(spawner => spawner != null)
                .GroupBy(spawner => spawner.thingIDNumber)
                .Select(group => group.First())
                .OrderBy(spawner => spawner.thingIDNumber)
                .ToList();
            if (ordered.Count == 0)
            {
                return;
            }

            InfestationCompressionEvent plan = new InfestationCompressionEvent(
                map,
                ordered,
                incidentPoints);
            if (plan.FinalKinds.Count >= plan.OriginalKinds.Count)
            {
                CompressionTelemetry.RecordSkipped(
                    "infestation",
                    "IRC_OrdinaryInfestation".Translate(),
                    plan.OriginalKinds.Count,
                    plan.OriginalKinds.Sum(kind => kind.combatPower),
                    plan.IncidentPoints,
                    Describe(plan.OriginalKinds),
                    "IRC_ReasonNoInsectMerge".Translate(),
                    "IRC_InfestationClassificationSummary".Translate());
                return;
            }
            events.Add(plan);
        }

        public bool TryGetPlan(
            int spawnerId,
            out string eventId,
            out List<PawnKindDef> original,
            out List<PawnKindDef> compressed)
        {
            foreach (InfestationCompressionEvent infestationEvent in events)
            {
                InfestationTunnelPlan plan = infestationEvent.TunnelPlans.FirstOrDefault(candidate =>
                    candidate.SpawnerId == spawnerId);
                if (plan != null)
                {
                    eventId = infestationEvent.EventId;
                    original = plan.OriginalKinds.ToList();
                    compressed = plan.FinalKinds.ToList();
                    return true;
                }
            }

            eventId = null;
            original = null;
            compressed = null;
            return false;
        }

        public void RecordSpawn(
            string eventId,
            int spawnerId)
        {
            InfestationCompressionEvent plan = events.FirstOrDefault(candidate =>
                candidate.EventId == eventId);
            if (plan == null)
            {
                return;
            }

            plan.Record(spawnerId);
            if (plan.RemainingSpawnerIds.Count > 0)
            {
                return;
            }

            string originalComposition = Describe(plan.OriginalKinds);
            string finalComposition = Describe(plan.FinalKinds);
            if (plan.FinalKinds.Count < plan.OriginalKinds.Count)
            {
                CompressionTelemetry.Record(
                    "infestation",
                    "IRC_OrdinaryInfestation".Translate(),
                    plan.OriginalKinds.Count,
                    plan.FinalKinds.Count,
                    plan.OriginalKinds.Sum(kind => kind.combatPower),
                    plan.FinalKinds.Sum(kind => kind.combatPower),
                    plan.IncidentPoints,
                    0,
                    0,
                    originalComposition,
                    finalComposition,
                    "IRC_IdentityInfestationPreserved".Translate(),
                    null,
                    null,
                    "IRC_InfestationClassificationSummary".Translate());
            }
            events.Remove(plan);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref events, "immersiveRaidCompressionInfestationEvents", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                events ??= new List<InfestationCompressionEvent>();
                events.RemoveAll(plan => plan == null);
            }
        }

        private static string Describe(IEnumerable<PawnKindDef> kinds)
        {
            return string.Join(", ", kinds
                .GroupBy(kind => kind)
                .OrderBy(group => group.Key.label)
                .Select(group => group.Key.LabelCap + " ×" + group.Count()));
        }
    }

    public sealed class InfestationCompressionEvent : IExposable
    {
        private Map map;
        private string eventId;
        private List<int> remainingSpawnerIds = new List<int>();
        private float incidentPoints;
        private List<PawnKindDef> originalKinds = new List<PawnKindDef>();
        private List<PawnKindDef> finalKinds = new List<PawnKindDef>();
        private List<InfestationTunnelPlan> tunnelPlans = new List<InfestationTunnelPlan>();

        public string EventId => eventId;
        public List<int> RemainingSpawnerIds => remainingSpawnerIds;
        public float IncidentPoints => incidentPoints;
        public List<PawnKindDef> OriginalKinds => originalKinds;
        public List<PawnKindDef> FinalKinds => finalKinds;
        public List<InfestationTunnelPlan> TunnelPlans => tunnelPlans;

        public InfestationCompressionEvent()
        {
        }

        public InfestationCompressionEvent(
            Map map,
            List<TunnelHiveSpawner> spawners,
            float incidentPoints)
        {
            this.map = map;
            this.incidentPoints = incidentPoints;
            eventId = Guid.NewGuid().ToString("N");

            foreach (TunnelHiveSpawner spawner in spawners)
            {
                List<PawnKindDef> original;
                Rand.PushState(Gen.HashCombineInt(map.uniqueID, spawner.thingIDNumber));
                try
                {
                    original = InsectCompositionPlanner.GenerateVanillaComposition(
                        spawner.insectsPoints);
                }
                finally
                {
                    Rand.PopState();
                }
                tunnelPlans.Add(new InfestationTunnelPlan(
                    spawner.thingIDNumber,
                    original));
                originalKinds.AddRange(original);
            }

            finalKinds = InsectCompositionPlanner.TryCompress(originalKinds);
            remainingSpawnerIds = tunnelPlans.Select(plan => plan.SpawnerId).ToList();
            for (int kindIndex = 0; kindIndex < finalKinds.Count; kindIndex++)
            {
                int planIndex = Mathf.Clamp(
                    Mathf.RoundToInt(
                        (kindIndex + 0.5f) * tunnelPlans.Count / finalKinds.Count - 0.5f),
                    0,
                    tunnelPlans.Count - 1);
                tunnelPlans[planIndex].FinalKinds.Add(finalKinds[kindIndex]);
            }
        }

        public void Record(int spawnerId)
        {
            remainingSpawnerIds.Remove(spawnerId);
            tunnelPlans.RemoveAll(plan => plan.SpawnerId == spawnerId);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref eventId, "eventId");
            Scribe_Collections.Look(ref remainingSpawnerIds, "remainingSpawnerIds", LookMode.Value);
            Scribe_Values.Look(ref incidentPoints, "incidentPoints");
            Scribe_Collections.Look(ref originalKinds, "originalKinds", LookMode.Def);
            Scribe_Collections.Look(ref finalKinds, "finalKinds", LookMode.Def);
            Scribe_Collections.Look(ref tunnelPlans, "tunnelPlans", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                remainingSpawnerIds ??= new List<int>();
                originalKinds ??= new List<PawnKindDef>();
                finalKinds ??= new List<PawnKindDef>();
                tunnelPlans ??= new List<InfestationTunnelPlan>();
                eventId ??= Guid.NewGuid().ToString("N");
            }
        }
    }

    public sealed class InfestationTunnelPlan : IExposable
    {
        private int spawnerId;
        private List<PawnKindDef> originalKinds = new List<PawnKindDef>();
        private List<PawnKindDef> finalKinds = new List<PawnKindDef>();

        public int SpawnerId => spawnerId;
        public List<PawnKindDef> OriginalKinds => originalKinds;
        public List<PawnKindDef> FinalKinds => finalKinds;

        public InfestationTunnelPlan()
        {
        }

        public InfestationTunnelPlan(int spawnerId, List<PawnKindDef> originalKinds)
        {
            this.spawnerId = spawnerId;
            this.originalKinds = originalKinds ?? new List<PawnKindDef>();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref spawnerId, "spawnerId");
            Scribe_Collections.Look(ref originalKinds, "originalKinds", LookMode.Def);
            Scribe_Collections.Look(ref finalKinds, "finalKinds", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                originalKinds ??= new List<PawnKindDef>();
                finalKinds ??= new List<PawnKindDef>();
            }
        }
    }
}
