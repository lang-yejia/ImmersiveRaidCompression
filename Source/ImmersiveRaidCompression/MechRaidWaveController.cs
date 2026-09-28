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
                || pawns.Count <= settings.mechWaveSplitCountThreshold
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

            MechWavePartition partition = MechWavePlanner.Build(
                pawns,
                settings.mechanoidSoftPawnCap,
                settings.mechWaveSplitCountThreshold,
                settings.mechWaveMinimumPoints,
                settings.mechWaveBudgetFraction,
                parms.points);
            if (partition == null)
            {
                return false;
            }

            List<Pawn> firstWave = partition.Waves[0];
            List<List<Pawn>> laterWaves = partition.Waves.Skip(1).ToList();
            List<Pawn> deferred = laterWaves.SelectMany(wave => wave).ToList();
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
                laterWaves,
                partition.MinimumWavePoints,
                settings.enableTacticalMechDrops,
                settings.mechWaveTriggerFraction,
                Find.TickManager.TicksGame + settings.mechWaveMinimumDelayTicks);
            component.AddPlan(plan);
            CompressionTelemetry.AttachWavePlan(
                plan.TelemetryId,
                partition.Waves.Count,
                firstWave.Count,
                partition.WavePoints[0],
                deferred.Count,
                partition.MinimumWavePoints,
                parms.raidArrivalMode.defName,
                parms.spawnCenter);
            if (settings.verboseLogging)
            {
                Log.Message(
                    "[Immersive Raid Compression] staged homogeneous mech raid: "
                    + partition.Waves.Count + " waves, first wave " + firstWave.Count
                    + " pawns / " + partition.WavePoints[0].ToString("F0")
                    + " points, minimum " + partition.MinimumWavePoints.ToString("F0")
                    + " points, deferred " + deferred.Count + ", arrival "
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

    }

    public sealed class MechWavePartition
    {
        public List<List<Pawn>> Waves { get; }
        public List<float> WavePoints { get; }
        public float MinimumWavePoints { get; }

        public MechWavePartition(List<List<Pawn>> waves, float minimumWavePoints)
        {
            Waves = waves;
            WavePoints = waves.Select(MechWavePlanner.CombatPower).ToList();
            MinimumWavePoints = minimumWavePoints;
        }
    }

    public static class MechWavePlanner
    {
        private const float PointEpsilon = 0.01f;

        public static MechWavePartition Build(
            List<Pawn> pawns,
            int targetWavePawnCount,
            int splitCountThreshold,
            float absoluteMinimumPoints,
            float budgetFraction,
            float adjustedRaidPoints)
        {
            if (pawns == null || pawns.Count <= Math.Max(1, splitCountThreshold))
            {
                return null;
            }

            float minimumPoints = Math.Max(0f, Math.Max(absoluteMinimumPoints, adjustedRaidPoints * budgetFraction));
            float totalPoints = CombatPower(pawns);
            // Long reinforcement chains feel like delay rather than pressure. An
            // eligible swarm therefore becomes two or three substantial waves only.
            int desiredWaveCount = Math.Min(
                3,
                Math.Max(2, (int)Math.Ceiling(pawns.Count / (double)Math.Max(1, targetWavePawnCount))));
            int maximumQualifiedWaves = minimumPoints <= PointEpsilon
                ? desiredWaveCount
                : (int)Math.Floor((totalPoints + PointEpsilon) / minimumPoints);
            int waveCount = Math.Min(desiredWaveCount, maximumQualifiedWaves);

            // A too-small tail is not emitted. Re-plan with one fewer bucket until
            // every wave clears the dynamic point floor; this is the smart merge.
            while (waveCount >= 2)
            {
                List<List<Pawn>> waves = DistributeByPower(pawns, waveCount);
                if (waves.All(wave => CombatPower(wave) + PointEpsilon >= minimumPoints))
                {
                    return new MechWavePartition(waves, minimumPoints);
                }
                waveCount--;
            }

            return null;
        }

        public static float CombatPower(IEnumerable<Pawn> pawns)
        {
            return pawns.Where(pawn => pawn?.kindDef != null).Sum(pawn => pawn.kindDef.combatPower);
        }

        private static List<List<Pawn>> DistributeByPower(List<Pawn> pawns, int waveCount)
        {
            List<List<Pawn>> waves = Enumerable.Range(0, waveCount)
                .Select(_ => new List<Pawn>())
                .ToList();
            float[] powers = new float[waveCount];
            foreach (Pawn pawn in pawns
                .OrderByDescending(candidate => candidate.kindDef.combatPower)
                .ThenBy(candidate => candidate.kindDef.defName)
                .ThenBy(candidate => candidate.thingIDNumber))
            {
                int target = Enumerable.Range(0, waveCount)
                    .OrderBy(index => powers[index])
                    .ThenBy(index => waves[index].Count)
                    .ThenBy(index => index)
                    .First();
                waves[target].Add(pawn);
                powers[target] += pawn.kindDef.combatPower;
            }
            return waves;
        }
    }

    public static class TacticalMechDropPlanner
    {
        private const int MinimumAllyDistance = 6;
        private const int MaximumAllyDistance = 18;
        private const int MinimumPlayerPawnDistance = 25;
        private const int MinimumPlayerBuildingDistance = 18;
        private const int PodOpenDelayTicks = 110;

        public static bool TryDropNearSurvivingAttackers(
            List<Pawn> pawns,
            Map map,
            IEnumerable<Pawn> originalRaidPawns,
            out IntVec3 anchor,
            out List<IntVec3> dropCells)
        {
            anchor = IntVec3.Invalid;
            dropCells = new List<IntVec3>();
            if (pawns == null || pawns.Count == 0 || map == null)
            {
                return false;
            }

            List<Pawn> survivors = originalRaidPawns
                .Where(pawn => pawn != null
                    && !pawn.Dead
                    && !pawn.Downed
                    && pawn.Spawned
                    && pawn.Map == map)
                .OrderBy(pawn => pawn.thingIDNumber)
                .ToList();
            List<Pawn> playerPawns = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.Faction == Faction.OfPlayer && !pawn.Dead)
                .ToList();
            List<Building> playerBuildings = map.listerBuildings.allBuildingsColonist;

            foreach (Pawn survivor in survivors)
            {
                List<IntVec3> safeCells = GenRadial.RadialCellsAround(
                        survivor.Position,
                        MaximumAllyDistance,
                        true)
                    .Where(cell => cell.DistanceToSquared(survivor.Position)
                        >= MinimumAllyDistance * MinimumAllyDistance)
                    .Where(cell => IsSafeDropCell(cell, map, playerPawns, playerBuildings))
                    .OrderBy(cell => cell.DistanceToSquared(survivor.Position))
                    .ThenBy(cell => cell.x)
                    .ThenBy(cell => cell.z)
                    .Take(pawns.Count)
                    .ToList();
                if (safeCells.Count < pawns.Count)
                {
                    continue;
                }

                for (int index = 0; index < pawns.Count; index++)
                {
                    Pawn pawn = pawns[index];
                    pawn.SetForbidden(true, false);
                    ActiveTransporterInfo info = new ActiveTransporterInfo
                    {
                        openDelay = PodOpenDelayTicks,
                        leaveSlag = true
                    };
                    info.innerContainer.TryAdd(pawn, true);
                    DropPodUtility.MakeDropPodAt(safeCells[index], map, info);
                }
                anchor = survivor.Position;
                dropCells = safeCells;
                return true;
            }

            return false;
        }

        public static bool IsSafeDropCell(
            IntVec3 cell,
            Map map,
            IReadOnlyList<Pawn> playerPawns,
            IReadOnlyList<Building> playerBuildings)
        {
            if (!cell.InBounds(map)
                || !cell.Standable(map)
                || cell.Fogged(map)
                || cell.Roofed(map)
                || map.areaManager.Home[cell]
                || cell.GetFirstPawn(map) != null)
            {
                return false;
            }

            int pawnDistanceSquared = MinimumPlayerPawnDistance * MinimumPlayerPawnDistance;
            if (playerPawns.Any(pawn => pawn.Spawned
                && pawn.Position.DistanceToSquared(cell) < pawnDistanceSquared))
            {
                return false;
            }

            int buildingDistanceSquared = MinimumPlayerBuildingDistance * MinimumPlayerBuildingDistance;
            return !playerBuildings.Any(building => building.Spawned
                && building.Position.DistanceToSquared(cell) < buildingDistanceSquared);
        }
    }

    public sealed class MechRaidReinforcementComponent : GameComponent
    {
        private List<MechRaidWavePlan> plans = new List<MechRaidWavePlan>();

        public int PendingPlanCount => plans.Count;
        public int PendingPawnCount => plans.Sum(plan => plan.DeferredCount);
        public int PendingWaveCount => plans.Sum(plan => plan.RemainingWaveCount);
        public int TotalPlannedWaveCount => plans.Sum(plan => plan.RemainingWaveCount + 1);
        public bool AllPlannedWavesMeetMinimum => plans.All(plan => plan.AllPlannedWavesMeetMinimum);
        public float MinimumPlannedWavePoints => plans.Count == 0 ? 0f : plans.Min(plan => plan.MinimumWavePoints);
        public bool LastReleaseUsedTacticalDrop => plans.Any(plan => plan.LastReleaseUsedTacticalDrop);
        public IReadOnlyList<IntVec3> LastTacticalDropCells => plans
            .SelectMany(plan => plan.LastTacticalDropCells)
            .ToList();
        public IntVec3 LastTacticalDropAnchor => plans
            .Where(plan => plan.LastReleaseUsedTacticalDrop)
            .Select(plan => plan.LastTacticalDropAnchor)
            .FirstOrDefault();

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
        private List<int> remainingWaveSizes = new List<int>();
        private List<float> plannedWavePoints = new List<float>();
        private float minimumWavePoints;
        private bool tacticalDropReinforcements;
        private bool lastReleaseUsedTacticalDrop;
        private List<IntVec3> lastTacticalDropCells = new List<IntVec3>();
        private IntVec3 lastTacticalDropAnchor = IntVec3.Invalid;
        // Kept only for migrating v0.6 saves.
        private int waveSize;
        private float triggerFraction;
        private float initialWavePower;
        private int nextReleaseTick;
        private int releasedWaveCount;
        private string telemetryId;

        public int DeferredCount => deferredPawns?.Count ?? 0;
        public int RemainingWaveCount => remainingWaveSizes?.Count ?? 0;
        public float MinimumWavePoints => minimumWavePoints;
        public bool AllPlannedWavesMeetMinimum => plannedWavePoints != null
            && plannedWavePoints.All(points => points + 0.01f >= minimumWavePoints);
        public bool LastReleaseUsedTacticalDrop => lastReleaseUsedTacticalDrop;
        public IReadOnlyList<IntVec3> LastTacticalDropCells => lastTacticalDropCells;
        public IntVec3 LastTacticalDropAnchor => lastTacticalDropAnchor;
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
            List<List<Pawn>> laterWaves,
            float minimumWavePoints,
            bool tacticalDropReinforcements,
            float triggerFraction,
            int nextReleaseTick)
        {
            this.map = map;
            this.faction = faction;
            this.arrivalMode = arrivalMode;
            this.raidStrategy = raidStrategy;
            this.spawnCenter = spawnCenter;
            releasedPawns = new List<Pawn>(firstWave);
            deferredPawns = laterWaves.SelectMany(wave => wave).ToList();
            remainingWaveSizes = laterWaves.Select(wave => wave.Count).ToList();
            plannedWavePoints = new[] { MechWavePlanner.CombatPower(firstWave) }
                .Concat(laterWaves.Select(MechWavePlanner.CombatPower))
                .ToList();
            this.minimumWavePoints = minimumWavePoints;
            this.tacticalDropReinforcements = tacticalDropReinforcements;
            this.triggerFraction = triggerFraction;
            this.nextReleaseTick = nextReleaseTick;
            initialWavePower = MechWavePlanner.CombatPower(firstWave);
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

            bool usedTacticalDrop = false;
            IntVec3 releaseAnchor = spawnCenter;
            List<IntVec3> tacticalDropCells = new List<IntVec3>();
            try
            {
                if (tacticalDropReinforcements)
                {
                    usedTacticalDrop = TacticalMechDropPlanner.TryDropNearSurvivingAttackers(
                        batch,
                        map,
                        releasedPawns,
                        out releaseAnchor,
                        out tacticalDropCells);
                }
                if (!usedTacticalDrop)
                {
                    MechRaidWaveController.ArriveDeferredWave(batch, parms);
                }
                AttachBatchToOriginalLord(batch);
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
            lastReleaseUsedTacticalDrop = usedTacticalDrop;
            lastTacticalDropCells = tacticalDropCells;
            lastTacticalDropAnchor = usedTacticalDrop ? releaseAnchor : IntVec3.Invalid;
            remainingWaveSizes.RemoveAt(0);
            releasedWaveCount++;
            nextReleaseTick = Find.TickManager.TicksGame
                + (CompressionMod.Settings?.mechWaveMinimumDelayTicks ?? 180);
            CompressionTelemetry.RecordWaveRelease(
                telemetryId,
                releasedWaveCount,
                batch.Count,
                MechWavePlanner.CombatPower(batch),
                deferredPawns.Count,
                RemainingWaveCount,
                usedTacticalDrop
                    ? "IRC_TacticalDropArrival".Translate()
                    : arrivalMode.defName,
                releaseAnchor);
            if (CompressionMod.Settings?.verboseLogging == true)
            {
                Log.Message(
                    "[Immersive Raid Compression] released mech reinforcement wave "
                    + releasedWaveCount + ": " + batch.Count + " pawns, "
                    + deferredPawns.Count + " deferred, arrival "
                    + (usedTacticalDrop ? "tactical vanilla drop pods" : arrivalMode.defName)
                    + ", anchor " + releaseAnchor + ".");
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
            remainingWaveSizes.Clear();
        }

        private void AttachBatchToOriginalLord(List<Pawn> batch)
        {
            if (lord == null)
            {
                throw new InvalidOperationException("The original raid Lord is unavailable.");
            }

            foreach (Pawn pawn in batch)
            {
                Lord currentLord = LordUtility.GetLord(pawn);
                if (currentLord == lord)
                {
                    continue;
                }
                if (currentLord != null || !lord.CanAddPawn(pawn))
                {
                    throw new InvalidOperationException(
                        "A reinforcement pawn could not join the original raid Lord.");
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
            Scribe_References.Look(ref lord, "lord");
            Scribe_Collections.Look(ref releasedPawns, "releasedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref deferredPawns, "deferredPawns", LookMode.Reference);
            Scribe_Collections.Look(ref remainingWaveSizes, "remainingWaveSizes", LookMode.Value);
            Scribe_Collections.Look(ref plannedWavePoints, "plannedWavePoints", LookMode.Value);
            Scribe_Values.Look(ref minimumWavePoints, "minimumWavePoints");
            Scribe_Values.Look(ref tacticalDropReinforcements, "tacticalDropReinforcements", false);
            Scribe_Values.Look(ref lastReleaseUsedTacticalDrop, "lastReleaseUsedTacticalDrop", false);
            Scribe_Collections.Look(ref lastTacticalDropCells, "lastTacticalDropCells", LookMode.Value);
            Scribe_Values.Look(ref lastTacticalDropAnchor, "lastTacticalDropAnchor", IntVec3.Invalid);
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
                remainingWaveSizes ??= new List<int>();
                plannedWavePoints ??= new List<float>();
                lastTacticalDropCells ??= new List<IntVec3>();
                if (deferredPawns.Count > 0 && remainingWaveSizes.Count == 0)
                {
                    int legacySize = Math.Max(1, waveSize);
                    for (int remaining = deferredPawns.Count; remaining > 0; remaining -= legacySize)
                    {
                        remainingWaveSizes.Add(Math.Min(legacySize, remaining));
                    }
                }
                if (plannedWavePoints.Count == 0)
                {
                    plannedWavePoints.Add(initialWavePower);
                    int offset = 0;
                    foreach (int size in remainingWaveSizes)
                    {
                        plannedWavePoints.Add(MechWavePlanner.CombatPower(deferredPawns.Skip(offset).Take(size)));
                        offset += size;
                    }
                }
            }
        }
    }
}
