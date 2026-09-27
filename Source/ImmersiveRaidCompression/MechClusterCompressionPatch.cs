using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    [HarmonyPatch(typeof(MechClusterGenerator), nameof(MechClusterGenerator.GenerateClusterSketch))]
    public static class MechClusterCompressionPatch
    {
        private const float MinimumRetainedCostFraction = 0.95f;
        private const float MaximumUpgradeFactor = 4f;
        private const int MaximumMergeWidth = 3;
        private const float CostEpsilon = 0.001f;

        public static void Postfix(float points, Map map, ref MechClusterSketch __result)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (__result?.pawns == null
                || settings == null
                || !settings.enableMechClusters
                || points < settings.minimumMechClusterPoints
                || __result.pawns.Count <= settings.mechClusterSoftPawnCap)
            {
                return;
            }

            List<MechClusterSketch.Mech> original = new List<MechClusterSketch.Mech>(__result.pawns);
            List<MechClusterSketch.Mech> compressed = TryCompress(
                original,
                settings.mechClusterSoftPawnCap);
            float originalCost = TotalCost(original);
            string originalComposition = DescribeComposition(original);
            string sourceName = map?.Parent?.LabelCap ?? map?.ToString() ?? "map";

            if (compressed.Count >= original.Count)
            {
                CompressionTelemetry.RecordSkipped(
                    "mech cluster",
                    sourceName,
                    original.Count,
                    originalCost,
                    points,
                    originalComposition,
                    "IRC_ReasonNoPromotion".Translate());
                return;
            }

            __result.pawns = compressed;
            float finalCost = TotalCost(compressed);
            int buildingCount = __result.buildingsSketch?.Things?.Count ?? 0;
            CompressionTelemetry.Record(
                "mech cluster",
                sourceName,
                original.Count,
                compressed.Count,
                originalCost,
                finalCost,
                points,
                original.Count(mech => mech.kindDef.isBoss),
                compressed.Count(mech => mech.kindDef.isBoss),
                originalComposition,
                DescribeComposition(compressed),
                "IRC_IdentityClusterPreserved".Translate(buildingCount));

            Log.Message(
                "[Immersive Raid Compression] mech cluster: "
                + original.Count + " -> " + compressed.Count
                + " mechanoids, vanilla kind cost " + originalCost.ToString("F0")
                + " -> " + finalCost.ToString("F0")
                + ", " + buildingCount + " buildings left untouched, cluster budget "
                + points.ToString("F0") + ".");
        }

        private static List<MechClusterSketch.Mech> TryCompress(
            List<MechClusterSketch.Mech> original,
            int targetCount)
        {
            MechanoidRaidCompressionPolicy policy = MechanoidRaidCompressionPolicy.Instance;
            List<MechClusterSketch.Mech> chosen = new List<MechClusterSketch.Mech>(original);
            float originalCost = TotalCost(original);
            float minimumCost = originalCost * MinimumRetainedCostFraction;
            List<PawnKindDef> candidates = DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(kind => kind != null
                    && kind.modContentPack != null
                    && (kind.modContentPack.IsCoreMod || kind.modContentPack.IsOfficialMod)
                    && MechClusterGenerator.MechKindSuitableForCluster(kind)
                    && !policy.IsProtectedKind(kind))
                .OrderByDescending(kind => kind.combatPower)
                .ToList();

            bool changed = false;
            while (chosen.Count > targetCount)
            {
                MergeProposal proposal = FindBestMerge(
                    original,
                    chosen,
                    candidates,
                    policy,
                    minimumCost);
                if (!proposal.Valid)
                {
                    break;
                }

                foreach (int index in proposal.Indices.OrderByDescending(index => index))
                {
                    chosen.RemoveAt(index);
                }
                chosen.Add(new MechClusterSketch.Mech
                {
                    kindDef = proposal.Replacement,
                    position = proposal.Position
                });
                changed = true;
            }

            if (!changed)
            {
                return original;
            }

            SpendRemainingCost(chosen, candidates, policy, originalCost);
            float finalCost = TotalCost(chosen);
            bool identityPreserved = ThreatIdentity.KindGroupIdentityIsPreserved(
                original.Select(mech => mech.kindDef).ToList(),
                chosen.Select(mech => mech.kindDef).ToList(),
                policy.RoleForKind,
                policy.IsProtectedKind,
                out string identityFailure);
            if (finalCost + CostEpsilon < minimumCost
                || finalCost > originalCost * 1.05f + CostEpsilon
                || !identityPreserved)
            {
                if (CompressionMod.Settings?.verboseLogging == true && identityFailure != null)
                {
                    Log.Message(
                        "[Immersive Raid Compression] rejected mech-cluster compression "
                        + "because it changed threat identity (" + identityFailure + ").");
                }
                return original;
            }

            return chosen;
        }

        private static MergeProposal FindBestMerge(
            List<MechClusterSketch.Mech> original,
            List<MechClusterSketch.Mech> chosen,
            List<PawnKindDef> candidates,
            MechanoidRaidCompressionPolicy policy,
            float minimumCost)
        {
            MergeProposal best = default;
            float currentCost = TotalCost(chosen);
            foreach (IGrouping<string, IndexedMech> roleGroup in chosen
                .Select((mech, index) => new IndexedMech(index, mech))
                .Where(item => !policy.IsProtectedKind(item.Mech.kindDef))
                .GroupBy(item => policy.RoleForKind(item.Mech.kindDef)))
            {
                List<IndexedMech> ordered = roleGroup
                    .OrderBy(item => item.Mech.kindDef.combatPower)
                    .ToList();
                int maximumWidth = Math.Min(MaximumMergeWidth, ordered.Count);
                for (int width = 2; width <= maximumWidth; width++)
                {
                    List<IndexedMech> group = ordered.Take(width).ToList();
                    float availableCost = group.Sum(item => item.Mech.kindDef.combatPower);
                    float oldHighestCost = group.Max(item => item.Mech.kindDef.combatPower);
                    PawnKindDef replacement = candidates.FirstOrDefault(kind =>
                        policy.RoleForKind(kind) == roleGroup.Key
                        && kind.combatPower > oldHighestCost + CostEpsilon
                        && kind.combatPower <= availableCost + CostEpsilon
                        && kind.combatPower <= oldHighestCost * MaximumUpgradeFactor + CostEpsilon
                        && currentCost - availableCost + kind.combatPower + CostEpsilon >= minimumCost);
                    if (replacement == null)
                    {
                        continue;
                    }

                    List<MechClusterSketch.Mech> tentative = new List<MechClusterSketch.Mech>(chosen);
                    foreach (int index in group.Select(item => item.Index).OrderByDescending(index => index))
                    {
                        tentative.RemoveAt(index);
                    }
                    tentative.Add(new MechClusterSketch.Mech
                    {
                        kindDef = replacement,
                        position = group[0].Mech.position
                    });
                    if (!ThreatIdentity.KindGroupIdentityIsPreserved(
                            original.Select(mech => mech.kindDef).ToList(),
                            tentative.Select(mech => mech.kindDef).ToList(),
                            policy.RoleForKind,
                            policy.IsProtectedKind,
                            out _))
                    {
                        continue;
                    }

                    float retainedCost = currentCost - availableCost + replacement.combatPower;
                    if (!best.Valid || retainedCost > best.ResultingCost)
                    {
                        best = new MergeProposal(
                            group.Select(item => item.Index).ToList(),
                            replacement,
                            group[0].Mech.position,
                            retainedCost);
                    }
                }
            }

            return best;
        }

        private static void SpendRemainingCost(
            List<MechClusterSketch.Mech> chosen,
            List<PawnKindDef> candidates,
            MechanoidRaidCompressionPolicy policy,
            float budget)
        {
            for (int pass = 0; pass < 100; pass++)
            {
                float remaining = budget - TotalCost(chosen);
                if (remaining <= CostEpsilon)
                {
                    return;
                }

                int bestIndex = -1;
                PawnKindDef bestKind = null;
                float bestDelta = 0f;
                for (int index = 0; index < chosen.Count; index++)
                {
                    PawnKindDef current = chosen[index].kindDef;
                    if (policy.IsProtectedKind(current))
                    {
                        continue;
                    }

                    foreach (PawnKindDef candidate in candidates)
                    {
                        float delta = candidate.combatPower - current.combatPower;
                        if (policy.RoleForKind(candidate) != policy.RoleForKind(current)
                            || delta <= CostEpsilon
                            || delta > remaining + CostEpsilon
                            || candidate.combatPower > current.combatPower * MaximumUpgradeFactor + CostEpsilon)
                        {
                            continue;
                        }

                        if (delta > bestDelta)
                        {
                            bestIndex = index;
                            bestKind = candidate;
                            bestDelta = delta;
                        }
                    }
                }

                if (bestIndex < 0)
                {
                    return;
                }

                MechClusterSketch.Mech upgraded = chosen[bestIndex];
                upgraded.kindDef = bestKind;
                chosen[bestIndex] = upgraded;
            }
        }

        private static float TotalCost(IEnumerable<MechClusterSketch.Mech> mechs)
        {
            return mechs.Sum(mech => mech.kindDef.combatPower);
        }

        private static string DescribeComposition(IEnumerable<MechClusterSketch.Mech> mechs)
        {
            return string.Join(", ", mechs
                .GroupBy(mech => mech.kindDef)
                .OrderBy(group => group.Key.label)
                .Select(group => group.Key.LabelCap + " ×" + group.Count()));
        }

        private readonly struct IndexedMech
        {
            public readonly int Index;
            public readonly MechClusterSketch.Mech Mech;

            public IndexedMech(int index, MechClusterSketch.Mech mech)
            {
                Index = index;
                Mech = mech;
            }
        }

        private readonly struct MergeProposal
        {
            public readonly IReadOnlyList<int> Indices;
            public readonly PawnKindDef Replacement;
            public readonly IntVec3 Position;
            public readonly float ResultingCost;
            public readonly bool Valid;

            public MergeProposal(
                IReadOnlyList<int> indices,
                PawnKindDef replacement,
                IntVec3 position,
                float resultingCost)
            {
                Indices = indices;
                Replacement = replacement;
                Position = position;
                ResultingCost = resultingCost;
                Valid = true;
            }
        }
    }
}
