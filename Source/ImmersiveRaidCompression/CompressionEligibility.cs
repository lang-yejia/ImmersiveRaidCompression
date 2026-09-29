using System.Collections.Generic;
using RimWorld;

namespace ImmersiveRaidCompression
{
    public static class CompressionEligibility
    {
        public static CompressionPlan PlanFor(
            PawnGroupMakerParms parms,
            IReadOnlyList<PawnGenOptionWithXenotype> original)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (settings == null || parms == null)
            {
                return null;
            }

            if (parms.groupKind != PawnGroupKindDefOf.Combat || parms.raidStrategy == null)
            {
                return null;
            }

            if (parms.points < settings.minimumRaidPoints || parms.faction?.HostileTo(Faction.OfPlayer) != true)
            {
                return null;
            }

            if (settings.enableHumanRaids
                && parms.faction.def.humanlikeFaction
                && original.Count > settings.humanSoftPawnCap)
            {
                PawnsArrivalModeDef arrivalMode = HumanRaidArrivalContext.Take(parms);
                HumanRaidClassification classification = HumanRaidClassifier.Analyze(
                    parms,
                    original,
                    arrivalMode);
                return new CompressionPlan(
                    settings.humanSoftPawnCap,
                    HumanRaidCompressionPolicy.Instance,
                    classification.Summary,
                    classification.Treatment == HumanRaidTreatment.VanillaPromotion
                        ? null
                        : "IRC_ReasonProtectedHumanStrategy");
            }

            if (settings.enableMechanoidRaids
                && parms.faction.def == FactionDefOf.Mechanoid
                && original.Count > settings.mechanoidSoftPawnCap)
            {
                MechRaidClassification classification = MechRaidClassifier.Analyze(parms, original);
                return new CompressionPlan(
                    settings.mechanoidSoftPawnCap,
                    MechanoidRaidCompressionPolicy.Instance,
                    classification.Summary);
            }

            return null;
        }
    }

    public sealed class CompressionPlan
    {
        public int TargetCount { get; }
        public ICompressionPolicy Policy { get; }
        public string ClassificationSummary { get; }
        public string ProtectedReasonKey { get; }
        public bool ShouldCompress => string.IsNullOrEmpty(ProtectedReasonKey);

        public CompressionPlan(
            int targetCount,
            ICompressionPolicy policy,
            string classificationSummary = null,
            string protectedReasonKey = null)
        {
            TargetCount = targetCount;
            Policy = policy;
            ClassificationSummary = classificationSummary;
            ProtectedReasonKey = protectedReasonKey;
        }
    }
}
