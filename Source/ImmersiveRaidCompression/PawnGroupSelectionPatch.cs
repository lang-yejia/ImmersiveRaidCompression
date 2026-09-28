using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    [HarmonyPatch(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.ChoosePawnGenOptionsByPoints))]
    public static class PawnGroupSelectionPatch
    {
        public static void Postfix(
            float pointsTotal,
            List<PawnGenOption> options,
            PawnGroupMakerParms groupParms,
            ref IEnumerable<PawnGenOptionWithXenotype> __result)
        {
            List<PawnGenOptionWithXenotype> original = __result?.ToList();
            if (original == null)
            {
                return;
            }

            CompressionPlan plan = CompressionEligibility.PlanFor(groupParms, original.Count);
            if (plan == null)
            {
                return;
            }

            MechRaidClassification mechClassification = plan.Policy is MechanoidRaidCompressionPolicy
                ? MechRaidClassifier.Analyze(groupParms, original)
                : null;

            CompressionResult result = VanillaPawnKindCompressor.TryCompress(
                pointsTotal,
                options,
                groupParms,
                original,
                plan.TargetCount,
                plan.Policy);

            if (!result.Changed)
            {
                string composition = CompressionTelemetry.DescribeComposition(original);
                CompressionTelemetry.RecordSkipped(
                    plan.Policy.ThreatType,
                    groupParms.faction.Name,
                    original.Count,
                    result.OriginalCost,
                    pointsTotal,
                    composition,
                    "IRC_ReasonNoPromotion".Translate(),
                    mechClassification?.Summary);
                if (CompressionMod.Settings.verboseLogging)
                {
                    Verse.Log.Message(
                        "[Immersive Raid Compression] " + groupParms.faction.Name
                        + " (" + plan.Policy.ThreatType + "): no legal same-role vanilla promotion was available for "
                        + original.Count + " pawns at " + pointsTotal.ToString("F0") + " points. Composition: "
                        + string.Join(", ", original
                            .GroupBy(option => option.Option.kind.defName)
                            .OrderBy(group => group.Key)
                            .Select(group => group.Key + " x" + group.Count()))
                        + ".");
                }

                return;
            }

            __result = result.Options;
            string originalComposition = CompressionTelemetry.DescribeComposition(original);
            string finalComposition = CompressionTelemetry.DescribeComposition(result.Options);
            bool bossPromoted = result.Options.Count(option => option.Option.kind.isBoss)
                > original.Count(option => option.Option.kind.isBoss);
            CompressionTelemetry.Record(
                plan.Policy.ThreatType,
                groupParms.faction.Name,
                original.Count,
                result.Options.Count,
                result.OriginalCost,
                result.FinalCost,
                pointsTotal,
                original.Count(option => option.Option.kind.isBoss),
                result.Options.Count(option => option.Option.kind.isBoss),
                originalComposition,
                finalComposition,
                (bossPromoted
                    ? "IRC_IdentityRolesPreservedBossEligible"
                    : "IRC_IdentityRolesPreserved").Translate(),
                null,
                null,
                mechClassification?.Summary);
            Verse.Log.Message(
                "[Immersive Raid Compression] " + groupParms.faction.Name
                + " (" + plan.Policy.ThreatType + ")"
                + ": " + original.Count + " -> " + result.Options.Count
                + " pawns, vanilla kind cost " + result.OriginalCost.ToString("F0")
                + " -> " + result.FinalCost.ToString("F0")
                + ", raid budget " + pointsTotal.ToString("F0") + ".");
        }
    }
}
