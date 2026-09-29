using System;
using System.Collections.Generic;
using System.Linq;
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
                bool specialistRaid = classification.Treatment == HumanRaidTreatment.SapperEscortPromotion
                    || classification.Treatment == HumanRaidTreatment.BreachEscortPromotion;
                int targetCount = specialistRaid
                    ? HumanSpecialistCompressionRules.MinimumTargetCount(
                        original.Count,
                        original.Count(HumanRaidCompressionPolicy.Instance.IsProtected),
                        settings.humanSoftPawnCap)
                    : settings.humanSoftPawnCap;
                return new CompressionPlan(
                    targetCount,
                    HumanRaidCompressionPolicy.Instance,
                    classification.Summary,
                    classification.Treatment == HumanRaidTreatment.VanillaPromotion
                        || classification.Treatment == HumanRaidTreatment.SiegeVanillaPromotion
                        || specialistRaid
                        ? null
                        : "IRC_ReasonProtectedHumanStrategy",
                    specialistRaid
                        ? "IRC_IdentityPathingSpecialistsPreserved"
                        : classification.Treatment == HumanRaidTreatment.SiegeVanillaPromotion
                            ? "IRC_IdentitySiegePreserved"
                            : null);
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

    public static class HumanSpecialistCompressionRules
    {
        private const float MinimumEscortFraction = 0.75f;

        public static int MinimumTargetCount(int originalCount, int protectedCount, int configuredCap)
        {
            int boundedProtected = Math.Max(0, Math.Min(originalCount, protectedCount));
            int originalEscorts = Math.Max(0, originalCount - boundedProtected);
            int minimumEscorts = (int)Math.Ceiling(originalEscorts * MinimumEscortFraction);
            return Math.Max(configuredCap, boundedProtected + minimumEscorts);
        }
    }

    public sealed class CompressionPlan
    {
        public int TargetCount { get; }
        public ICompressionPolicy Policy { get; }
        public string ClassificationSummary { get; }
        public string ProtectedReasonKey { get; }
        public string SuccessIdentityKey { get; }
        public bool ShouldCompress => string.IsNullOrEmpty(ProtectedReasonKey);

        public CompressionPlan(
            int targetCount,
            ICompressionPolicy policy,
            string classificationSummary = null,
            string protectedReasonKey = null,
            string successIdentityKey = null)
        {
            TargetCount = targetCount;
            Policy = policy;
            ClassificationSummary = classificationSummary;
            ProtectedReasonKey = protectedReasonKey;
            SuccessIdentityKey = successIdentityKey;
        }
    }
}
