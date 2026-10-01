using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ImmersiveRaidCompression
{
    [HarmonyPatch]
    public static class ShamblerSwarmGenerationPatch
    {
        [ThreadStatic] private static List<Pawn> letterPawns;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(IncidentWorker_ShamblerSwarm), "GenerateEntities");
            yield return AccessTools.Method(typeof(IncidentWorker_ShamblerSwarmAnimals), "GenerateEntities");
        }

        public static void Postfix(
            IncidentWorker_ShamblerSwarm __instance,
            IncidentParms parms,
            ref List<Pawn> __result)
        {
            bool animalSwarm = __instance.GetType() == typeof(IncidentWorker_ShamblerSwarmAnimals);
            if ((!animalSwarm && __instance.GetType() != typeof(IncidentWorker_ShamblerSwarm))
                || !ShamblerWaveController.TryStageSwarm(
                    __result,
                    parms,
                    animalSwarm,
                    out List<Pawn> completeSwarm))
            {
                return;
            }

            letterPawns = completeSwarm;
        }

        public static bool TryUseCompleteLetterPawns(ref List<Pawn> pawns)
        {
            if (letterPawns == null)
            {
                return false;
            }

            pawns = letterPawns;
            letterPawns = null;
            return true;
        }

        public static void ClearLetterPawns()
        {
            letterPawns = null;
        }
    }

    [HarmonyPatch]
    public static class ShamblerSwarmLetterPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(IncidentWorker_ShamblerSwarm), "SendLetter");
            yield return AccessTools.Method(typeof(IncidentWorker_ShamblerSwarmAnimals), "SendLetter");
        }

        public static void Prefix(IncidentWorker_ShamblerSwarm __instance, ref List<Pawn> pawns)
        {
            if (__instance.GetType() == typeof(IncidentWorker_ShamblerSwarm)
                || __instance.GetType() == typeof(IncidentWorker_ShamblerSwarmAnimals))
            {
                ShamblerSwarmGenerationPatch.TryUseCompleteLetterPawns(ref pawns);
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_EntitySwarm), "TryExecuteWorker")]
    public static class ShamblerSwarmContextCleanupPatch
    {
        public static Exception Finalizer(Exception __exception, IncidentWorker_EntitySwarm __instance)
        {
            if (__instance.GetType() == typeof(IncidentWorker_ShamblerSwarm)
                || __instance.GetType() == typeof(IncidentWorker_ShamblerSwarmAnimals))
            {
                ShamblerSwarmGenerationPatch.ClearLetterPawns();
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(PawnsArrivalModeWorker_EdgeWalkInDistributedGroups), nameof(PawnsArrivalModeWorker.Arrive))]
    public static class ShamblerAssaultArrivalPatch
    {
        public static void Prefix(List<Pawn> pawns, IncidentParms parms)
        {
            if (!ShamblerWaveController.ReleasingAssaultWave)
            {
                ShamblerWaveController.TryStageAssault(pawns, parms);
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_ShamblerAssault), "GetLetterText")]
    public static class ShamblerAssaultLetterPatch
    {
        public static void Prefix(ref List<Pawn> pawns)
        {
            ShamblerWaveController.TryUseCompleteAssaultForLetter(ref pawns);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_ShamblerAssault), "TryExecuteWorker")]
    public static class ShamblerAssaultContextCleanupPatch
    {
        public static Exception Finalizer(Exception __exception)
        {
            ShamblerWaveController.ClearCompleteAssaultForLetter();
            return __exception;
        }
    }

    public static class ShamblerWaveController
    {
        private static readonly FieldInfo AssaultLifespanRangeField = AccessTools.Field(
            typeof(IncidentWorker_ShamblerAssault),
            "ShamblerLifespanTicksRange");

        [ThreadStatic] private static List<Pawn> completeAssaultForLetter;
        internal static bool ReleasingAssaultWave { get; private set; }

        public static bool TryStageSwarm(
            List<Pawn> pawns,
            IncidentParms parms,
            bool animalSwarm,
            out List<Pawn> completeSwarm)
        {
            completeSwarm = null;
            if (!CanStage(pawns, parms, requireAssault: false))
            {
                return false;
            }

            ShamblerWavePartition partition = ShamblerWavePlanner.Build(pawns, parms.points);
            if (partition == null)
            {
                return false;
            }

            completeSwarm = new List<Pawn>(pawns);
            return Stage(
                pawns,
                parms,
                partition,
                ShamblerArrivalKind.EdgeSwarm,
                animalSwarm ? "shambler animal swarm" : "shambler swarm",
                animalSwarm
                    ? "IRC_ShamblerAnimalSwarmClassificationSummary".Translate()
                    : "IRC_ShamblerSwarmClassificationSummary".Translate(),
                "IRC_ShamblerEdgeSwarmArrival".Translate());
        }

        public static bool TryStageAssault(List<Pawn> pawns, IncidentParms parms)
        {
            if (!CanStage(pawns, parms, requireAssault: true))
            {
                return false;
            }

            ShamblerWavePartition partition = ShamblerWavePlanner.Build(pawns, parms.points);
            if (partition == null)
            {
                return false;
            }

            completeAssaultForLetter = new List<Pawn>(pawns);
            List<Pawn> deferred = partition.Waves.Skip(1).SelectMany(wave => wave).ToList();
            InitializeDeferredAssaultLifespans(deferred);
            if (!Stage(
                    pawns,
                    parms,
                    partition,
                    ShamblerArrivalKind.DistributedAssault,
                    "shambler assault",
                    "IRC_ShamblerAssaultClassificationSummary".Translate(),
                    parms.raidArrivalMode.defName))
            {
                completeAssaultForLetter = null;
                return false;
            }
            return true;
        }

        public static bool TryUseCompleteAssaultForLetter(ref List<Pawn> pawns)
        {
            if (completeAssaultForLetter == null)
            {
                return false;
            }

            pawns = completeAssaultForLetter;
            return true;
        }

        public static void ClearCompleteAssaultForLetter()
        {
            completeAssaultForLetter = null;
        }

        internal static void ArriveDeferredAssault(List<Pawn> pawns, IncidentParms parms)
        {
            ReleasingAssaultWave = true;
            try
            {
                parms.raidArrivalMode.Worker.Arrive(pawns, parms);
            }
            finally
            {
                ReleasingAssaultWave = false;
            }
        }

        private static bool CanStage(List<Pawn> pawns, IncidentParms parms, bool requireAssault)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (settings == null
                || !settings.enableAnomalyMassThreats
                || !settings.enablePhasedShamblerWaves
                || !ModsConfig.AnomalyActive
                || pawns == null
                || parms?.target is not Map
                || parms.points < settings.minimumAnomalyThreatPoints
                || pawns.Count <= settings.shamblerWaveSplitCountThreshold)
            {
                return false;
            }

            if (!pawns.All(IsShambler))
            {
                return false;
            }

            if (!requireAssault)
            {
                return (parms.faction == null || parms.faction == Faction.OfEntities)
                    && parms.raidStrategy == null
                    && parms.raidArrivalMode == null;
            }

            return parms.faction == Faction.OfEntities
                && parms.pawnGroupKind == PawnGroupKindDefOf.Shamblers
                && parms.raidStrategy == RaidStrategyDefOf.ShamblerAssault
                && parms.raidArrivalMode?.Worker is PawnsArrivalModeWorker_EdgeWalkInDistributedGroups;
        }

        private static bool Stage(
            List<Pawn> pawns,
            IncidentParms parms,
            ShamblerWavePartition partition,
            ShamblerArrivalKind arrivalKind,
            string threatType,
            string classificationSummary,
            string arrivalLabel)
        {
            Map map = (Map)parms.target;
            List<Pawn> complete = new List<Pawn>(pawns);
            List<Pawn> firstWave = partition.Waves[0];
            List<List<Pawn>> laterWaves = partition.Waves.Skip(1).ToList();
            List<Pawn> deferred = laterWaves.SelectMany(wave => wave).ToList();
            pawns.Clear();
            pawns.AddRange(firstWave);
            foreach (Pawn pawn in deferred)
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            ShamblerReinforcementComponent component = Current.Game?.GetComponent<ShamblerReinforcementComponent>();
            if (component == null)
            {
                foreach (Pawn pawn in deferred)
                {
                    Find.WorldPawns.RemovePawn(pawn);
                    pawns.Add(pawn);
                }
                return false;
            }

            CompressionSettings settings = CompressionMod.Settings;
            ShamblerWavePlan plan = new ShamblerWavePlan(
                map,
                parms.faction ?? Faction.OfEntities,
                parms.raidArrivalMode,
                parms.raidStrategy,
                parms.spawnCenter,
                parms.spawnRotation,
                parms.questTag,
                firstWave,
                laterWaves,
                partition.MinimumWavePoints,
                settings.shamblerWaveTriggerFraction,
                Find.TickManager.TicksGame + settings.shamblerWaveMinimumDelayTicks,
                settings.shamblerWaveMinimumDelayTicks,
                threatType,
                arrivalKind);
            component.AddPlan(plan);

            float totalPower = MechWavePlanner.CombatPower(complete);
            string composition = DescribeComposition(complete);
            CompressionTelemetry.Record(
                threatType,
                (parms.faction ?? Faction.OfEntities).Name,
                complete.Count,
                complete.Count,
                totalPower,
                totalPower,
                parms.points,
                0,
                0,
                composition,
                composition,
                "IRC_IdentityShamblerWavesPreserved".Translate(),
                null,
                null,
                classificationSummary);
            CompressionTelemetry.AttachWavePlan(
                plan.TelemetryId,
                threatType,
                partition.Waves.Count,
                firstWave.Count,
                partition.WavePoints[0],
                deferred.Count,
                partition.MinimumWavePoints,
                arrivalLabel,
                parms.spawnCenter);

            if (settings.verboseLogging)
            {
                Log.Message(
                    "[Immersive Raid Compression] staged " + threatType + ": "
                    + partition.Waves.Count + " waves, first wave " + firstWave.Count
                    + " pawns / " + partition.WavePoints[0].ToString("F0")
                    + " points, minimum " + partition.MinimumWavePoints.ToString("F0")
                    + " points, deferred " + deferred.Count + ".");
            }
            return true;
        }

        private static void InitializeDeferredAssaultLifespans(IEnumerable<Pawn> pawns)
        {
            if (AssaultLifespanRangeField?.GetValue(null) is not IntRange range)
            {
                return;
            }

            foreach (Pawn pawn in pawns)
            {
                HediffComp_DisappearsAndKills comp = pawn.health?.hediffSet
                    ?.GetFirstHediffOfDef(HediffDefOf.Shambler)
                    ?.TryGetComp<HediffComp_DisappearsAndKills>();
                if (comp == null)
                {
                    Log.ErrorOnce(
                        "[Immersive Raid Compression] deferred shambler assault pawn has no lifespan hediff.",
                        91267411);
                    continue;
                }
                comp.disabled = false;
                comp.ticksToDisappear = range.RandomInRange;
            }
        }

        private static bool IsShambler(Pawn pawn)
        {
            return pawn?.health?.hediffSet
                ?.GetFirstHediffOfDef(HediffDefOf.Shambler) != null;
        }

        private static string DescribeComposition(IEnumerable<Pawn> pawns)
        {
            return string.Join(", ", pawns
                .GroupBy(pawn => pawn.kindDef)
                .OrderBy(group => group.Key.label)
                .Select(group => group.Key.LabelCap + " ×" + group.Count()));
        }
    }

    public sealed class ShamblerWavePartition
    {
        public List<List<Pawn>> Waves { get; }
        public List<float> WavePoints { get; }
        public float MinimumWavePoints { get; }

        public ShamblerWavePartition(List<List<Pawn>> waves, float minimumWavePoints)
        {
            Waves = waves;
            WavePoints = waves.Select(MechWavePlanner.CombatPower).ToList();
            MinimumWavePoints = minimumWavePoints;
        }
    }

    public static class ShamblerWavePlanner
    {
        private const float PointEpsilon = 0.01f;

        public static ShamblerWavePartition Build(List<Pawn> pawns, float incidentPoints)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (settings == null
                || pawns == null
                || pawns.Count <= Math.Max(1, settings.shamblerWaveSplitCountThreshold))
            {
                return null;
            }

            float minimumPoints = Math.Max(
                settings.shamblerWaveMinimumPoints,
                incidentPoints * settings.shamblerWaveBudgetFraction);
            float totalPoints = MechWavePlanner.CombatPower(pawns);
            int desiredWaveCount = Math.Min(
                3,
                Math.Max(2, (int)Math.Ceiling(
                    pawns.Count / (double)Math.Max(1, settings.shamblerWaveSplitCountThreshold))));
            int maximumQualifiedWaves = minimumPoints <= PointEpsilon
                ? desiredWaveCount
                : (int)Math.Floor((totalPoints + PointEpsilon) / minimumPoints);
            int waveCount = Math.Min(desiredWaveCount, maximumQualifiedWaves);

            while (waveCount >= 2)
            {
                List<List<Pawn>> waves = DistributeByKindAndPower(pawns, waveCount);
                if (waves.All(wave => MechWavePlanner.CombatPower(wave) + PointEpsilon >= minimumPoints))
                {
                    return new ShamblerWavePartition(waves, minimumPoints);
                }
                waveCount--;
            }
            return null;
        }

        private static List<List<Pawn>> DistributeByKindAndPower(List<Pawn> pawns, int waveCount)
        {
            List<List<Pawn>> waves = Enumerable.Range(0, waveCount)
                .Select(_ => new List<Pawn>())
                .ToList();
            float[] powers = new float[waveCount];
            Dictionary<PawnKindDef, int>[] kindCounts = Enumerable.Range(0, waveCount)
                .Select(_ => new Dictionary<PawnKindDef, int>())
                .ToArray();

            foreach (IGrouping<PawnKindDef, Pawn> kindGroup in pawns
                .GroupBy(pawn => pawn.kindDef)
                .OrderBy(group => group.Key.defName))
            {
                foreach (Pawn pawn in kindGroup
                    .OrderByDescending(candidate => candidate.kindDef.combatPower)
                    .ThenBy(candidate => candidate.thingIDNumber))
                {
                    PawnKindDef kind = pawn.kindDef;
                    int target = Enumerable.Range(0, waveCount)
                        .OrderBy(index => kindCounts[index].TryGetValue(kind, out int count) ? count : 0)
                        .ThenBy(index => powers[index])
                        .ThenBy(index => waves[index].Count)
                        .ThenBy(index => index)
                        .First();
                    waves[target].Add(pawn);
                    powers[target] += pawn.kindDef.combatPower;
                    kindCounts[target][kind] = kindCounts[target].TryGetValue(kind, out int count)
                        ? count + 1
                        : 1;
                }
            }
            return waves;
        }
    }

    public enum ShamblerArrivalKind
    {
        EdgeSwarm,
        DistributedAssault
    }

    public sealed class ShamblerReinforcementComponent : GameComponent
    {
        private List<ShamblerWavePlan> plans = new List<ShamblerWavePlan>();

        public ShamblerReinforcementComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0)
            {
                return;
            }

            for (int index = plans.Count - 1; index >= 0; index--)
            {
                ShamblerWavePlan plan = plans[index];
                if (!plan.MapStillAvailable())
                {
                    plan.DiscardDeferredPawns();
                    plans.RemoveAt(index);
                    continue;
                }

                plan.TryAttachLord();
                if (Find.TickManager.TicksGame >= plan.NextReleaseTick
                    && plan.ActiveCombatPower <= plan.ReleaseThreshold)
                {
                    plan.ReleaseNextWave();
                }

                if (plan.DeferredCount == 0)
                {
                    plans.RemoveAt(index);
                }
            }
        }

        public void AddPlan(ShamblerWavePlan plan)
        {
            plans.Add(plan);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref plans, "immersiveRaidCompressionShamblerWavePlans", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                plans ??= new List<ShamblerWavePlan>();
                plans.RemoveAll(plan => plan == null);
            }
        }
    }

    public sealed class ShamblerWavePlan : IExposable
    {
        private Map map;
        private Faction faction;
        private PawnsArrivalModeDef arrivalMode;
        private RaidStrategyDef raidStrategy;
        private IntVec3 spawnCenter;
        private Rot4 spawnRotation;
        private string questTag;
        private Lord lord;
        private List<Pawn> releasedPawns = new List<Pawn>();
        private List<Pawn> deferredPawns = new List<Pawn>();
        private List<int> remainingWaveSizes = new List<int>();
        private List<float> plannedWavePoints = new List<float>();
        private float minimumWavePoints;
        private float triggerFraction;
        private float initialWavePower;
        private int nextReleaseTick;
        private int minimumDelayTicks;
        private int releasedWaveCount;
        private string telemetryId;
        private string threatType;
        private ShamblerArrivalKind arrivalKind;

        public int DeferredCount => deferredPawns?.Count ?? 0;
        public int NextReleaseTick => nextReleaseTick;
        public string TelemetryId => telemetryId;
        public float ActiveCombatPower => releasedPawns
            .Where(pawn => pawn != null
                && !pawn.Dead
                && !pawn.Downed
                && pawn.SpawnedOrAnyParentSpawned
                && pawn.MapHeld == map)
            .Sum(pawn => pawn.kindDef.combatPower);
        public float ReleaseThreshold => initialWavePower * triggerFraction;

        public ShamblerWavePlan()
        {
        }

        public ShamblerWavePlan(
            Map map,
            Faction faction,
            PawnsArrivalModeDef arrivalMode,
            RaidStrategyDef raidStrategy,
            IntVec3 spawnCenter,
            Rot4 spawnRotation,
            string questTag,
            List<Pawn> firstWave,
            List<List<Pawn>> laterWaves,
            float minimumWavePoints,
            float triggerFraction,
            int nextReleaseTick,
            int minimumDelayTicks,
            string threatType,
            ShamblerArrivalKind arrivalKind)
        {
            this.map = map;
            this.faction = faction;
            this.arrivalMode = arrivalMode;
            this.raidStrategy = raidStrategy;
            this.spawnCenter = spawnCenter;
            this.spawnRotation = spawnRotation;
            this.questTag = questTag;
            releasedPawns = new List<Pawn>(firstWave);
            deferredPawns = laterWaves.SelectMany(wave => wave).ToList();
            remainingWaveSizes = laterWaves.Select(wave => wave.Count).ToList();
            plannedWavePoints = new[] { MechWavePlanner.CombatPower(firstWave) }
                .Concat(laterWaves.Select(MechWavePlanner.CombatPower))
                .ToList();
            this.minimumWavePoints = minimumWavePoints;
            this.triggerFraction = triggerFraction;
            initialWavePower = MechWavePlanner.CombatPower(firstWave);
            this.nextReleaseTick = nextReleaseTick;
            this.minimumDelayTicks = minimumDelayTicks;
            this.threatType = threatType;
            this.arrivalKind = arrivalKind;
            telemetryId = Guid.NewGuid().ToString("N");
        }

        public bool MapStillAvailable()
        {
            return map != null && Find.Maps.Contains(map);
        }

        public void TryAttachLord()
        {
            if (lord != null)
            {
                return;
            }
            lord = releasedPawns
                .Where(pawn => pawn != null)
                .Select(LordUtility.GetLord)
                .FirstOrDefault(candidate => candidate != null);
        }

        public bool ReleaseNextWave()
        {
            if (deferredPawns == null
                || deferredPawns.Count == 0
                || remainingWaveSizes == null
                || remainingWaveSizes.Count == 0
                || !MapStillAvailable()
                || lord == null
                || ActiveCombatPower > ReleaseThreshold)
            {
                return false;
            }

            int batchSize = Math.Min(deferredPawns.Count, remainingWaveSizes[0]);
            List<Pawn> selected = deferredPawns.Take(batchSize).ToList();
            List<Pawn> batch = selected
                .Where(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed)
                .ToList();
            foreach (Pawn pawn in selected)
            {
                deferredPawns.Remove(pawn);
                if (batch.Contains(pawn))
                {
                    Find.WorldPawns.RemovePawn(pawn);
                }
                else if (pawn != null)
                {
                    Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
                }
            }
            if (batch.Count == 0)
            {
                remainingWaveSizes.RemoveAt(0);
                nextReleaseTick = Find.TickManager.TicksGame + 1;
                return true;
            }

            try
            {
                if (arrivalKind == ShamblerArrivalKind.DistributedAssault)
                {
                    IncidentParms parms = new IncidentParms
                    {
                        target = map,
                        faction = faction,
                        forced = true,
                        sendLetter = false,
                        points = MechWavePlanner.CombatPower(batch),
                        raidStrategy = raidStrategy,
                        raidArrivalMode = arrivalMode,
                        spawnCenter = spawnCenter,
                        spawnRotation = spawnRotation,
                        lord = lord,
                        pawnGroupKind = PawnGroupKindDefOf.Shamblers
                    };
                    ShamblerWaveController.ArriveDeferredAssault(batch, parms);
                }
                else
                {
                    SpawnEdgeSwarm(batch);
                }
                AttachBatchToOriginalLord(batch);
            }
            catch (Exception exception)
            {
                Log.Error("[Immersive Raid Compression] Could not release a deferred "
                    + threatType + " wave: " + exception);
                foreach (Pawn pawn in batch.Where(pawn => !pawn.SpawnedOrAnyParentSpawned))
                {
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                    deferredPawns.Add(pawn);
                }
                return false;
            }

            releasedPawns.AddRange(batch);
            remainingWaveSizes.RemoveAt(0);
            releasedWaveCount++;
            nextReleaseTick = Find.TickManager.TicksGame + Math.Max(1, minimumDelayTicks);
            CompressionTelemetry.RecordWaveRelease(
                telemetryId,
                releasedWaveCount,
                batch.Count,
                MechWavePlanner.CombatPower(batch),
                deferredPawns.Count,
                remainingWaveSizes.Count,
                arrivalKind == ShamblerArrivalKind.DistributedAssault
                    ? arrivalMode.defName
                    : "IRC_ShamblerEdgeSwarmArrival".Translate(),
                spawnCenter);
            return true;
        }

        public void DiscardDeferredPawns()
        {
            foreach (Pawn pawn in deferredPawns.Where(pawn => pawn != null).ToList())
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
            }
            deferredPawns.Clear();
            remainingWaveSizes.Clear();
        }

        private void SpawnEdgeSwarm(IEnumerable<Pawn> batch)
        {
            Rot4 inward = Rot4.FromAngleFlat((map.Center - spawnCenter).AngleFlat);
            foreach (Pawn pawn in batch)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(spawnCenter, map, 10);
                GenSpawn.Spawn(pawn, cell, map, inward);
                QuestUtility.AddQuestTag(pawn, questTag);
            }
        }

        private void AttachBatchToOriginalLord(IEnumerable<Pawn> batch)
        {
            foreach (Pawn pawn in batch)
            {
                Lord currentLord = LordUtility.GetLord(pawn);
                if (currentLord == lord)
                {
                    continue;
                }
                if (currentLord != null || !lord.CanAddPawn(pawn))
                {
                    throw new InvalidOperationException("A shambler reinforcement could not join its original Lord.");
                }
                lord.AddPawn(pawn);
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Defs.Look(ref arrivalMode, "arrivalMode");
            Scribe_Defs.Look(ref raidStrategy, "raidStrategy");
            Scribe_Values.Look(ref spawnCenter, "spawnCenter");
            Scribe_Values.Look(ref spawnRotation, "spawnRotation");
            Scribe_Values.Look(ref questTag, "questTag");
            Scribe_References.Look(ref lord, "lord");
            Scribe_Collections.Look(ref releasedPawns, "releasedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref deferredPawns, "deferredPawns", LookMode.Reference);
            Scribe_Collections.Look(ref remainingWaveSizes, "remainingWaveSizes", LookMode.Value);
            Scribe_Collections.Look(ref plannedWavePoints, "plannedWavePoints", LookMode.Value);
            Scribe_Values.Look(ref minimumWavePoints, "minimumWavePoints");
            Scribe_Values.Look(ref triggerFraction, "triggerFraction", 0.65f);
            Scribe_Values.Look(ref initialWavePower, "initialWavePower");
            Scribe_Values.Look(ref nextReleaseTick, "nextReleaseTick");
            Scribe_Values.Look(ref minimumDelayTicks, "minimumDelayTicks", 180);
            Scribe_Values.Look(ref releasedWaveCount, "releasedWaveCount");
            Scribe_Values.Look(ref telemetryId, "telemetryId");
            Scribe_Values.Look(ref threatType, "threatType", "shambler swarm");
            Scribe_Values.Look(ref arrivalKind, "arrivalKind", ShamblerArrivalKind.EdgeSwarm);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                releasedPawns ??= new List<Pawn>();
                deferredPawns ??= new List<Pawn>();
                remainingWaveSizes ??= new List<int>();
                plannedWavePoints ??= new List<float>();
            }
        }
    }
}
