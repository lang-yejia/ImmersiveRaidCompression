using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ImmersiveRaidCompression
{
    public static class PirateRaidWaveController
    {
        internal static bool ReleasingDeferredWave { get; private set; }

        public static bool TryStageLaterWaves(List<Pawn> pawns, IncidentParms parms)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (settings == null
                || !settings.enableHumanRaids
                || !settings.enablePhasedPirateWaves
                || pawns == null
                || parms?.target is not Map map
                || !IsPirateFaction(parms.faction?.def)
                || parms.points < settings.minimumRaidPoints
                || pawns.Count <= settings.pirateWaveSplitCountThreshold
                || !CanPhase(pawns.Select(pawn => pawn.kindDef), parms.raidStrategy, parms.raidArrivalMode))
            {
                return false;
            }

            MechWavePartition partition = PirateWavePlanner.Build(
                pawns,
                settings.humanSoftPawnCap,
                settings.pirateWaveSplitCountThreshold,
                settings.pirateWaveMinimumPoints,
                settings.pirateWaveBudgetFraction,
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
                settings.enableTacticalPirateDrops,
                settings.pirateWaveTriggerFraction,
                Find.TickManager.TicksGame + settings.pirateWaveMinimumDelayTicks,
                "human raid",
                settings.pirateWaveMinimumDelayTicks);
            component.AddPlan(plan);
            CompressionTelemetry.AttachWavePlan(
                plan.TelemetryId,
                "human raid",
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
                    "[Immersive Raid Compression] staged pirate assault: "
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
            HumanRaidClassification classification = HumanRaidClassifier.Analyze(
                composition,
                strategy,
                arrivalMode);
            return classification.Archetype == HumanRaidArchetype.DirectAssault
                && classification.Treatment == HumanRaidTreatment.VanillaPromotion
                && (arrivalMode == PawnsArrivalModeDefOf.EdgeWalkIn
                    || arrivalMode == PawnsArrivalModeDefOf.EdgeDrop);
        }

        public static bool IsPirateFaction(FactionDef factionDef)
        {
            return factionDef?.humanlikeFaction == true
                && factionDef.defName.StartsWith("Pirate", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class PirateWavePlanner
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
            float totalPoints = MechWavePlanner.CombatPower(pawns);
            int desiredWaveCount = Math.Min(
                3,
                Math.Max(2, (int)Math.Ceiling(pawns.Count / (double)Math.Max(1, targetWavePawnCount))));
            int maximumQualifiedWaves = minimumPoints <= PointEpsilon
                ? desiredWaveCount
                : (int)Math.Floor((totalPoints + PointEpsilon) / minimumPoints);
            int waveCount = Math.Min(desiredWaveCount, maximumQualifiedWaves);

            while (waveCount >= 2)
            {
                List<List<Pawn>> waves = DistributeByRoleAndPower(pawns, waveCount);
                if (waves.All(wave => MechWavePlanner.CombatPower(wave) + PointEpsilon >= minimumPoints))
                {
                    return new MechWavePartition(waves, minimumPoints);
                }
                waveCount--;
            }

            return null;
        }

        public static string RoleFor(Pawn pawn)
        {
            PawnGenOption option = new PawnGenOption { kind = pawn.kindDef };
            PawnGenOptionWithXenotype withXenotype = new PawnGenOptionWithXenotype(
                option,
                pawn.genes?.Xenotype,
                1f);
            return HumanRaidCompressionPolicy.Instance.RoleFor(withXenotype);
        }

        private static List<List<Pawn>> DistributeByRoleAndPower(List<Pawn> pawns, int waveCount)
        {
            List<List<Pawn>> waves = Enumerable.Range(0, waveCount)
                .Select(_ => new List<Pawn>())
                .ToList();
            float[] powers = new float[waveCount];
            Dictionary<string, int>[] roleCounts = Enumerable.Range(0, waveCount)
                .Select(_ => new Dictionary<string, int>())
                .ToArray();

            foreach (IGrouping<string, Pawn> roleGroup in pawns
                .GroupBy(RoleFor)
                .OrderBy(group => group.Key))
            {
                foreach (Pawn pawn in roleGroup
                    .OrderByDescending(candidate => candidate.kindDef.combatPower)
                    .ThenBy(candidate => candidate.kindDef.defName)
                    .ThenBy(candidate => candidate.thingIDNumber))
                {
                    string role = roleGroup.Key;
                    int target = Enumerable.Range(0, waveCount)
                        .OrderBy(index => roleCounts[index].TryGetValue(role, out int count) ? count : 0)
                        .ThenBy(index => powers[index])
                        .ThenBy(index => waves[index].Count)
                        .ThenBy(index => index)
                        .First();
                    waves[target].Add(pawn);
                    powers[target] += pawn.kindDef.combatPower;
                    roleCounts[target][role] = roleCounts[target].TryGetValue(role, out int count)
                        ? count + 1
                        : 1;
                }
            }

            return waves;
        }
    }
}
