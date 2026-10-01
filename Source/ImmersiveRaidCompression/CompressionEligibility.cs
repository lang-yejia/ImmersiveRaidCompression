using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

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

            if (FleshbeastAttackContextPatch.Active
                && settings.enableAnomalyMassThreats
                && ModsConfig.AnomalyActive
                && parms.groupKind?.defName == "Fleshbeasts"
                && FleshbeastAttackContextPatch.Points >= settings.minimumAnomalyThreatPoints
                && original.Count > settings.fleshbeastSoftPawnCap)
            {
                return new CompressionPlan(
                    settings.fleshbeastSoftPawnCap,
                    FleshbeastCompressionPolicy.Instance,
                    "IRC_FleshbeastClassificationSummary".Translate(),
                    null,
                    "IRC_IdentityFleshbeastPreserved");
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
                bool dropRaid = classification.Treatment == HumanRaidTreatment.DropAssaultPromotion;
                bool multiFrontRaid = classification.Treatment == HumanRaidTreatment.MultiFrontPromotion;
                HumanRaidCompressionPolicy humanPolicy = specialistRaid
                    ? HumanRaidCompressionPolicy.SpecialistInstance
                    : HumanRaidCompressionPolicy.Instance;
                bool tribalRaid = parms.faction.def.techLevel <= TechLevel.Neolithic;
                int targetCount = specialistRaid
                    ? HumanCompressionCountRules.MinimumSpecialistTargetCount(
                        original.Count,
                        original.Count(humanPolicy.IsProtected),
                        settings.humanSoftPawnCap)
                    : dropRaid
                        ? HumanCompressionCountRules.MinimumDropTargetCount(
                            original.Count,
                            settings.humanSoftPawnCap)
                        : multiFrontRaid
                            ? HumanCompressionCountRules.MinimumMultiFrontTargetCount(
                                original.Count,
                                settings.humanSoftPawnCap)
                    : settings.humanSoftPawnCap;
                return new CompressionPlan(
                    targetCount,
                    humanPolicy,
                    classification.Summary,
                    classification.Treatment == HumanRaidTreatment.VanillaPromotion
                        || classification.Treatment == HumanRaidTreatment.SiegeVanillaPromotion
                        || specialistRaid
                        || dropRaid
                        || multiFrontRaid
                        ? null
                        : "IRC_ReasonProtectedHumanStrategy",
                    specialistRaid
                        ? "IRC_IdentityPathingSpecialistsPreserved"
                        : dropRaid
                            ? "IRC_IdentityDropArrivalPreserved"
                        : multiFrontRaid
                            ? "IRC_IdentityMultiFrontArrivalPreserved"
                        : classification.Treatment == HumanRaidTreatment.SiegeVanillaPromotion
                            ? "IRC_IdentitySiegePreserved"
                            : tribalRaid
                                ? "IRC_IdentityTribalPromotionPreserved"
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

    public static class HumanCompressionCountRules
    {
        private const float MinimumEscortFraction = 0.75f;

        public static int MinimumSpecialistTargetCount(
            int originalCount,
            int protectedCount,
            int configuredCap)
        {
            int boundedProtected = Math.Max(0, Math.Min(originalCount, protectedCount));
            int originalEscorts = Math.Max(0, originalCount - boundedProtected);
            int minimumEscorts = (int)Math.Ceiling(originalEscorts * MinimumEscortFraction);
            return Math.Max(configuredCap, boundedProtected + minimumEscorts);
        }

        public static int MinimumDropTargetCount(int originalCount, int configuredCap)
        {
            long boundedOriginal = Math.Max(0, originalCount);
            int minimumDropCount = (int)Math.Min(
                int.MaxValue,
                (boundedOriginal * 4L + 4L) / 5L);
            return Math.Max(configuredCap, minimumDropCount);
        }

        public static int MinimumMultiFrontTargetCount(int originalCount, int configuredCap)
        {
            long boundedOriginal = Math.Max(0, originalCount);
            int minimumMultiFrontCount = (int)Math.Min(
                int.MaxValue,
                (boundedOriginal * 9L + 9L) / 10L);
            return Math.Max(configuredCap, minimumMultiFrontCount);
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
