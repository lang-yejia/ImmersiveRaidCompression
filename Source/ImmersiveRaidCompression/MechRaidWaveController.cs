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
    public static class MechRaidPhasedArrivalPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PawnsArrivalModeWorker_EdgeWalkIn), nameof(PawnsArrivalModeWorker.Arrive));
            yield return AccessTools.Method(typeof(PawnsArrivalModeWorker_EdgeDrop), nameof(PawnsArrivalModeWorker.Arrive));
        }

        public static void Prefix(List<Pawn> pawns, IncidentParms parms)
        {
            if (!MechRaidWaveController.ReleasingDeferredWave)
            {
                MechRaidWaveController.TryStageLaterWaves(pawns, parms);
            }
        }
    }

    public static class MechRaidWaveController
    {
        private const float PureRoleFraction = 0.999f;
        internal static bool ReleasingDeferredWave { get; private set; }

        public static bool TryStageLaterWaves(List<Pawn> pawns, IncidentParms parms)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (settings == null
                || !settings.enableMechanoidRaids
                || !settings.enablePhasedMechanoidWaves
                || pawns == null
                || parms?.target is not Map map
                || parms.faction?.def != FactionDefOf.Mechanoid
                || parms.points < settings.minimumRaidPoints
                || pawns.Count <= settings.mechanoidSoftPawnCap
                || !ArrivalModeSupported(parms.raidArrivalMode))
            {
                return false;
            }

            if (!CanPhase(
                    pawns.Select(pawn => pawn.kindDef),
                    parms.raidStrategy,
                    parms.raidArrivalMode))
            {
                return false;
            }

            int firstWaveSize = Math.Max(1, settings.mechanoidSoftPawnCap);
            List<Pawn> firstWave = TakeBalancedFirstWave(pawns, firstWaveSize);
            if (firstWave.Count >= pawns.Count)
            {
                return false;
            }

            List<Pawn> deferred = pawns.Except(firstWave).ToList();
            pawns.Clear();
            pawns.AddRange(firstWave);
            foreach (Pawn pawn in deferred)
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            MechRaidReinforcementComponent component = Current.Game?.GetComponent<MechRaidReinforcementComponent>();
            if (component == null)
            {
                foreach (Pawn pawn in deferred)
                {
                    Find.WorldPawns.RemovePawn(pawn);
                    pawns.Add(pawn);
                }
                return false;
            }

            MechRaidWavePlan plan = new MechRaidWavePlan(
                map,
                parms.faction,
                parms.raidArrivalMode,
                parms.raidStrategy,
                parms.spawnCenter,
                firstWave,
                deferred,
                settings.mechanoidSoftPawnCap,
                settings.mechWaveTriggerFraction,
                Find.TickManager.TicksGame + settings.mechWaveMinimumDelayTicks);
            component.AddPlan(plan);
            CompressionTelemetry.AttachWavePlan(
                plan.TelemetryId,
                firstWave.Count,
                deferred.Count,
                parms.raidArrivalMode.defName,
                parms.spawnCenter);
            if (settings.verboseLogging)
            {
                Log.Message(
                    "[Immersive Raid Compression] staged homogeneous mech raid: first wave "
                    + firstWave.Count + ", deferred " + deferred.Count + ", arrival "
                    + parms.raidArrivalMode.defName + ", edge anchor " + parms.spawnCenter + ".");
            }
            return true;
        }

        internal static void ArriveDeferredWave(List<Pawn> pawns, IncidentParms parms)
        {
            ReleasingDeferredWave = true;
            try
            {
                parms.raidArrivalMode.Worker.Arrive(pawns, parms);
            }
            finally
            {
                ReleasingDeferredWave = false;
            }
        }

        public static bool CanPhase(
            IEnumerable<PawnKindDef> composition,
            RaidStrategyDef strategy,
            PawnsArrivalModeDef arrivalMode)
        {
            List<PawnKindDef> kinds = composition.Where(kind => kind != null).ToList();
            MechRaidClassification classification = MechRaidClassifier.Analyze(kinds, strategy);
            return ArrivalModeSupported(arrivalMode)
                && classification.Treatment == MechRaidTreatment.PhasedReinforcementCandidate
                && classification.DominantFraction >= PureRoleFraction
                && !kinds.Any(kind => kind.isBoss
                    || MechanoidRaidCompressionPolicy.Instance.IsProtectedKind(kind));
        }

        private static bool ArrivalModeSupported(PawnsArrivalModeDef arrivalMode)
        {
            return arrivalMode == PawnsArrivalModeDefOf.EdgeWalkIn
                || arrivalMode == PawnsArrivalModeDefOf.EdgeDrop;
        }

        private static List<Pawn> TakeBalancedFirstWave(List<Pawn> pawns, int count)
        {
            List<IGrouping<PawnKindDef, Pawn>> groups = pawns
                .GroupBy(pawn => pawn.kindDef)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key.defName)
                .ToList();
            Dictionary<PawnKindDef, Queue<Pawn>> remaining = groups.ToDictionary(
                group => group.Key,
                group => new Queue<Pawn>(group));
            List<Pawn> result = new List<Pawn>();
            while (result.Count < count && remaining.Count > 0)
            {
                PawnKindDef nextKind = remaining
                    .OrderByDescending(pair => pair.Value.Count)
                    .ThenBy(pair => pair.Key.defName)
                    .First().Key;
                result.Add(remaining[nextKind].Dequeue());
                if (remaining[nextKind].Count == 0)
                {
                    remaining.Remove(nextKind);
                }
            }
            return result;
        }
    }

    public sealed class MechRaidReinforcementComponent : GameComponent
    {
        private List<MechRaidWavePlan> plans = new List<MechRaidWavePlan>();

        public int PendingPlanCount => plans.Count;
        public int PendingPawnCount => plans.Sum(plan => plan.DeferredCount);

        public MechRaidReinforcementComponent(Game game)
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
                MechRaidWavePlan plan = plans[index];
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

        public void AddPlan(MechRaidWavePlan plan)
        {
            plans.Add(plan);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref plans, "immersiveRaidCompressionMechWavePlans", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                plans ??= new List<MechRaidWavePlan>();
                plans.RemoveAll(plan => plan == null);
            }
        }
    }

    public sealed class MechRaidWavePlan : IExposable
    {
        private Map map;
        private Faction faction;
        private PawnsArrivalModeDef arrivalMode;
        private RaidStrategyDef raidStrategy;
        private IntVec3 spawnCenter;
        private Lord lord;
        private List<Pawn> releasedPawns = new List<Pawn>();
        private List<Pawn> deferredPawns = new List<Pawn>();
        private int waveSize;
        private float triggerFraction;
        private float initialWavePower;
        private int nextReleaseTick;
        private int releasedWaveCount;
        private string telemetryId;

        public int DeferredCount => deferredPawns?.Count ?? 0;
        public string TelemetryId => telemetryId;
        public int NextReleaseTick => nextReleaseTick;
        public float ActiveCombatPower => releasedPawns
            .Where(pawn => pawn != null
                && !pawn.Dead
                && !pawn.Downed
                && pawn.SpawnedOrAnyParentSpawned
                && pawn.MapHeld == map)
            .Sum(pawn => pawn.kindDef.combatPower);
        public float ReleaseThreshold => initialWavePower * triggerFraction;

        public MechRaidWavePlan()
        {
        }

        public MechRaidWavePlan(
            Map map,
            Faction faction,
            PawnsArrivalModeDef arrivalMode,
            RaidStrategyDef raidStrategy,
            IntVec3 spawnCenter,
            List<Pawn> firstWave,
            List<Pawn> deferredPawns,
            int waveSize,
            float triggerFraction,
            int nextReleaseTick)
        {
            this.map = map;
            this.faction = faction;
            this.arrivalMode = arrivalMode;
            this.raidStrategy = raidStrategy;
            this.spawnCenter = spawnCenter;
            releasedPawns = new List<Pawn>(firstWave);
            this.deferredPawns = new List<Pawn>(deferredPawns);
            this.waveSize = waveSize;
            this.triggerFraction = triggerFraction;
            this.nextReleaseTick = nextReleaseTick;
            initialWavePower = firstWave.Sum(pawn => pawn.kindDef.combatPower);
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
                || !MapStillAvailable()
                || ActiveCombatPower > ReleaseThreshold)
            {
                return false;
            }

            int activeCount = releasedPawns.Count(pawn => pawn != null
                && !pawn.Dead
                && !pawn.Downed
                && pawn.SpawnedOrAnyParentSpawned
                && pawn.MapHeld == map);
            int batchSize = Math.Min(deferredPawns.Count, Math.Max(1, waveSize - activeCount));
            List<Pawn> batch = deferredPawns.Take(batchSize).ToList();
            foreach (Pawn pawn in batch)
            {
                Find.WorldPawns.RemovePawn(pawn);
                deferredPawns.Remove(pawn);
            }

            IncidentParms parms = new IncidentParms
            {
                target = map,
                faction = faction,
                forced = true,
                sendLetter = false,
                points = batch.Sum(pawn => pawn.kindDef.combatPower),
                raidStrategy = raidStrategy,
                raidArrivalMode = arrivalMode,
                spawnCenter = spawnCenter,
                lord = lord,
                pawnGroupKind = PawnGroupKindDefOf.Combat
            };

            try
            {
                MechRaidWaveController.ArriveDeferredWave(batch, parms);
                AttachBatchToLord(batch, parms);
            }
            catch (Exception exception)
            {
                Log.Error("[Immersive Raid Compression] Could not release a deferred mech wave: " + exception);
                foreach (Pawn pawn in batch.Where(pawn => !pawn.SpawnedOrAnyParentSpawned))
                {
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                    deferredPawns.Add(pawn);
                }
                return false;
            }

            releasedPawns.AddRange(batch);
            releasedWaveCount++;
            nextReleaseTick = Find.TickManager.TicksGame
                + (CompressionMod.Settings?.mechWaveMinimumDelayTicks ?? 600);
            CompressionTelemetry.RecordWaveRelease(
                telemetryId,
                releasedWaveCount,
                batch.Count,
                deferredPawns.Count,
                arrivalMode.defName,
                spawnCenter);
            if (CompressionMod.Settings?.verboseLogging == true)
            {
                Log.Message(
                    "[Immersive Raid Compression] released mech reinforcement wave "
                    + releasedWaveCount + ": " + batch.Count + " pawns, "
                    + deferredPawns.Count + " deferred, arrival " + arrivalMode.defName
                    + ", edge anchor " + spawnCenter + ".");
            }
            return true;
        }

        public void DiscardDeferredPawns()
        {
            foreach (Pawn pawn in deferredPawns.Where(pawn => pawn != null).ToList())
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
            }
            deferredPawns.Clear();
        }

        private void AttachBatchToLord(List<Pawn> batch, IncidentParms parms)
        {
            TryAttachLord();
            if (lord != null && batch.All(lord.CanAddPawn))
            {
                lord.AddPawns(batch);
                return;
            }

            raidStrategy.Worker.MakeLords(parms, batch);
            lord = batch.Select(LordUtility.GetLord).FirstOrDefault(candidate => candidate != null);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Defs.Look(ref arrivalMode, "arrivalMode");
            Scribe_Defs.Look(ref raidStrategy, "raidStrategy");
            Scribe_Values.Look(ref spawnCenter, "spawnCenter");
            Scribe_References.Look(ref lord, "lord");
            Scribe_Collections.Look(ref releasedPawns, "releasedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref deferredPawns, "deferredPawns", LookMode.Reference);
            Scribe_Values.Look(ref waveSize, "waveSize", 24);
            Scribe_Values.Look(ref triggerFraction, "triggerFraction", 0.45f);
            Scribe_Values.Look(ref initialWavePower, "initialWavePower");
            Scribe_Values.Look(ref nextReleaseTick, "nextReleaseTick");
            Scribe_Values.Look(ref releasedWaveCount, "releasedWaveCount");
            Scribe_Values.Look(ref telemetryId, "telemetryId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                releasedPawns ??= new List<Pawn>();
                deferredPawns ??= new List<Pawn>();
            }
        }
    }
}
