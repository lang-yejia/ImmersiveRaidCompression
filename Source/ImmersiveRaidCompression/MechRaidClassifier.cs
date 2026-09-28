using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    public enum MechRaidArchetype
    {
        Mixed,
        HomogeneousMelee,
        HomogeneousMilitor,
        HomogeneousCentipede,
        HomogeneousRole,
        BossLed,
        Breach
    }

    public enum MechRaidTreatment
    {
        VanillaPromotion,
        PhasedReinforcementCandidate,
        ProtectedStrategy
    }

    public sealed class MechRaidClassification
    {
        public MechRaidArchetype Archetype { get; }
        public MechRaidTreatment Treatment { get; }
        public string StrategyDefName { get; }
        public string DominantRole { get; }
        public float DominantFraction { get; }

        public string Summary => "IRC_MechClassificationSummary".Translate(
            ArchetypeLabel(),
            TreatmentLabel(),
            StrategyDefName ?? "-",
            DominantRole ?? "-",
            (DominantFraction * 100f).ToString("F0"));

        public MechRaidClassification(
            MechRaidArchetype archetype,
            MechRaidTreatment treatment,
            string strategyDefName,
            string dominantRole,
            float dominantFraction)
        {
            Archetype = archetype;
            Treatment = treatment;
            StrategyDefName = strategyDefName;
            DominantRole = dominantRole;
            DominantFraction = dominantFraction;
        }

        private string ArchetypeLabel()
        {
            return ("IRC_MechArchetype_" + Archetype).Translate();
        }

        private string TreatmentLabel()
        {
            return ("IRC_MechTreatment_" + Treatment).Translate();
        }
    }

    /// <summary>
    /// Read-only classification used to select a future mech-raid treatment. It does not
    /// alter pawn generation, arrival mode, lords, or spawn positions.
    /// </summary>
    public static class MechRaidClassifier
    {
        private const float HomogeneousRoleThreshold = 0.80f;

        public static MechRaidClassification Analyze(
            PawnGroupMakerParms parms,
            IEnumerable<PawnGenOptionWithXenotype> composition)
        {
            return Analyze(
                composition.Select(option => option.Option.kind),
                parms?.raidStrategy);
        }

        public static MechRaidClassification Analyze(
            IEnumerable<PawnKindDef> composition,
            RaidStrategyDef strategy)
        {
            List<PawnKindDef> kinds = composition.Where(kind => kind != null).ToList();
            string strategyName = strategy?.defName ?? "-";
            if (kinds.Count == 0)
            {
                return new MechRaidClassification(
                    MechRaidArchetype.Mixed,
                    MechRaidTreatment.VanillaPromotion,
                    strategyName,
                    "none",
                    0f);
            }

            MechanoidRaidCompressionPolicy policy = MechanoidRaidCompressionPolicy.Instance;
            IGrouping<string, PawnKindDef> dominant = kinds
                .GroupBy(policy.RoleForKind)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .First();
            float dominantFraction = dominant.Count() / (float)kinds.Count;

            bool breach = strategyName.IndexOf("Breach", StringComparison.OrdinalIgnoreCase) >= 0
                || kinds.Any(kind => kind.isGoodBreacher
                    || kind.canBeSapper
                    || Contains(kind, "Termite"));
            if (breach)
            {
                return new MechRaidClassification(
                    MechRaidArchetype.Breach,
                    MechRaidTreatment.ProtectedStrategy,
                    strategyName,
                    dominant.Key,
                    dominantFraction);
            }

            if (kinds.Any(kind => kind.isBoss))
            {
                return new MechRaidClassification(
                    MechRaidArchetype.BossLed,
                    MechRaidTreatment.ProtectedStrategy,
                    strategyName,
                    dominant.Key,
                    dominantFraction);
            }

            bool allScythers = kinds.All(kind => Contains(kind, "Scyther"));
            bool allMilitors = kinds.All(kind => Contains(kind, "Militor"));
            bool allCentipedes = kinds.All(kind => Contains(kind, "Centipede"));
            if (allScythers)
            {
                return Homogeneous(MechRaidArchetype.HomogeneousMelee, strategyName, dominant.Key);
            }

            if (allMilitors)
            {
                return Homogeneous(MechRaidArchetype.HomogeneousMilitor, strategyName, dominant.Key);
            }

            if (allCentipedes)
            {
                return Homogeneous(MechRaidArchetype.HomogeneousCentipede, strategyName, dominant.Key);
            }

            if (dominantFraction >= HomogeneousRoleThreshold)
            {
                return new MechRaidClassification(
                    MechRaidArchetype.HomogeneousRole,
                    MechRaidTreatment.PhasedReinforcementCandidate,
                    strategyName,
                    dominant.Key,
                    dominantFraction);
            }

            return new MechRaidClassification(
                MechRaidArchetype.Mixed,
                MechRaidTreatment.VanillaPromotion,
                strategyName,
                dominant.Key,
                dominantFraction);
        }

        private static MechRaidClassification Homogeneous(
            MechRaidArchetype archetype,
            string strategyName,
            string role)
        {
            return new MechRaidClassification(
                archetype,
                MechRaidTreatment.PhasedReinforcementCandidate,
                strategyName,
                role,
                1f);
        }

        private static bool Contains(PawnKindDef kind, string fragment)
        {
            return kind.defName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
