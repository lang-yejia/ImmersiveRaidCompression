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

            CompressionPlan plan = CompressionEligibility.PlanFor(groupParms, original);
            if (plan == null)
            {
                return;
            }

            if (!plan.ShouldCompress)
            {
                string protectedComposition = CompressionTelemetry.DescribeComposition(original);
                if (!FleshbeastAttackContextPatch.OwnsTelemetry)
                {
                    CompressionTelemetry.RecordSkipped(
                        plan.Policy.ThreatType,
                        groupParms.faction.Name,
                        original.Count,
                        original.Sum(option => option.Cost),
                        pointsTotal,
                        protectedComposition,
                        plan.ProtectedReasonKey.Translate(),
                        plan.ClassificationSummary);
                }
                if (CompressionMod.Settings.verboseLogging)
                {
                    Verse.Log.Message(
                        "[Immersive Raid Compression] " + groupParms.faction.Name
                        + " (" + plan.Policy.ThreatType + "): kept vanilla because "
                        + plan.ProtectedReasonKey.Translate() + " "
                        + plan.ClassificationSummary);
                }
                return;
            }

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
                if (!FleshbeastAttackContextPatch.OwnsTelemetry)
                {
                    CompressionTelemetry.RecordSkipped(
                        plan.Policy.ThreatType,
                        groupParms.faction.Name,
                        original.Count,
                        result.OriginalCost,
                        pointsTotal,
                        composition,
                        "IRC_ReasonNoPromotion".Translate(),
                        plan.ClassificationSummary);
                }
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
            if (!FleshbeastAttackContextPatch.TryRecordSuccessfulCompression(groupParms.faction, original, result))
            {
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
                    !string.IsNullOrEmpty(plan.SuccessIdentityKey)
                        ? plan.SuccessIdentityKey.Translate()
                        : (bossPromoted
                            ? "IRC_IdentityRolesPreservedBossEligible"
                            : "IRC_IdentityRolesPreserved").Translate(),
                    null,
                    null,
                    plan.ClassificationSummary);
            }
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
