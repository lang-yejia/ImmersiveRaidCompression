using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    public static class VanillaPawnKindCompressor
    {
        private const float CostEpsilon = 0.001f;
        private const float MinimumRetainedCostFraction = 0.95f;

        public static CompressionResult TryCompress(
            float pointsTotal,
            List<PawnGenOption> sourceOptions,
            PawnGroupMakerParms parms,
            List<PawnGenOptionWithXenotype> original,
            int targetCount,
            ICompressionPolicy policy)
        {
            List<PawnGenOptionWithXenotype> chosen = new List<PawnGenOptionWithXenotype>(original);
            float originalCost = TotalCost(chosen);
            float maxPawnCost = PawnGroupMakerUtility.MaxPawnCost(
                parms.faction,
                pointsTotal,
                parms.raidStrategy,
                parms.groupKind);

            List<PawnGenOptionWithXenotype> vanillaCandidates = PawnGroupMakerUtility.GetOptions(
                    parms,
                    parms.faction.def,
                    sourceOptions,
                    pointsTotal,
                    pointsTotal,
                    maxPawnCost)
                .Where(policy.IsCandidateAllowed)
                .ToList();
            HashSet<PawnKindDef> permittedBossPromotions = MechBossPromotionPolicy.SelectPermittedBossKinds(
                original,
                vanillaCandidates,
                parms,
                policy);
            List<PawnGenOptionWithXenotype> candidates = vanillaCandidates
                .Where(candidate => !candidate.Option.kind.isBoss
                    || permittedBossPromotions.Contains(candidate.Option.kind))
                .OrderByDescending(candidate => candidate.Cost)
                .ThenByDescending(candidate => candidate.SelectionWeight)
                .ToList();

            bool changed = false;
            float minimumAllowedCost = originalCost * MinimumRetainedCostFraction;
            while (chosen.Count > targetCount)
            {
                if (!TryMergeOneGroup(
                        chosen,
                        candidates,
                        parms,
                        policy,
                        minimumAllowedCost,
                        permittedBossPromotions))
                {
                    break;
                }

                changed = true;
            }

            if (changed)
            {
                SpendRemainingBudget(
                    chosen,
                    candidates,
                    parms,
                    policy,
                    System.Math.Min(pointsTotal, originalCost),
                    permittedBossPromotions);
            }

            float finalCost = TotalCost(chosen);
            if (changed && finalCost + CostEpsilon < originalCost * MinimumRetainedCostFraction)
            {
                return new CompressionResult(
                    new List<PawnGenOptionWithXenotype>(original),
                    originalCost,
                    originalCost,
                    false);
            }

            if (changed && !ThreatIdentity.PawnGroupIdentityIsPreserved(
                    original,
                    chosen,
                    policy,
                    permittedBossPromotions,
                    out string identityFailure))
            {
                if (CompressionMod.Settings?.verboseLogging == true)
                {
                    Verse.Log.Message(
                        "[Immersive Raid Compression] rejected a numerically valid compression "
                        + "because it changed threat identity (" + identityFailure + ").");
                }

                return new CompressionResult(
                    new List<PawnGenOptionWithXenotype>(original),
                    originalCost,
                    originalCost,
                    false);
            }

            return new CompressionResult(chosen, originalCost, finalCost, changed);
        }

        private static bool TryMergeOneGroup(
            List<PawnGenOptionWithXenotype> chosen,
            List<PawnGenOptionWithXenotype> candidates,
            PawnGroupMakerParms parms,
            ICompressionPolicy policy,
            float minimumAllowedCost,
            ISet<PawnKindDef> permittedBossPromotions)
        {
            float currentCost = TotalCost(chosen);
            List<List<IndexedOption>> mergeable = chosen
                .Select((option, index) => new IndexedOption(index, option, policy.RoleFor(option)))
                .Where(item => !policy.IsProtected(item.Option))
                .GroupBy(item => item.Role)
                .Where(group => group.Count() >= 2)
                .SelectMany(group => BuildRepresentativeMerges(group, policy.MaximumMergeWidth))
                .OrderBy(group => group.Sum(item => item.Option.Cost))
                .ThenByDescending(group => group.Count)
                .ToList();

            foreach (List<IndexedOption> mergeGroup in mergeable)
            {
                if (policy.PreserveKindPresence
                    && mergeGroup
                        .GroupBy(item => item.Option.Option.kind)
                        .Any(group => group.Count() >= chosen.Count(option =>
                            option.Option.kind == group.Key)))
                {
                    continue;
                }

                float availableCost = mergeGroup.Sum(item => item.Option.Cost);
                float oldHighestCost = mergeGroup.Max(item => item.Option.Cost);
                HashSet<int> removedIndices = new HashSet<int>(mergeGroup.Select(item => item.Index));
                List<PawnGenOptionWithXenotype> withoutGroup = chosen
                    .Where((option, index) => !removedIndices.Contains(index))
                    .ToList();

                PawnGenOptionWithXenotype? replacement = candidates
                    .Where(candidate => BossCandidateAllowed(candidate, chosen, permittedBossPromotions))
                    .Where(candidate => policy.RoleFor(candidate) == mergeGroup[0].Role)
                    .Where(candidate => candidate.Cost > oldHighestCost + CostEpsilon)
                    .Where(candidate => candidate.Cost <= availableCost + CostEpsilon)
                    .Where(candidate => currentCost - availableCost + candidate.Cost + CostEpsilon >= minimumAllowedCost)
                    .Where(candidate => candidate.Cost <= oldHighestCost * policy.MaximumUpgradeFactor + CostEpsilon)
                    .Where(candidate => PawnGroupMakerUtility.PawnGenOptionValid(candidate.Option, parms, withoutGroup))
                    .Cast<PawnGenOptionWithXenotype?>()
                    .FirstOrDefault();

                if (!replacement.HasValue)
                {
                    continue;
                }

                foreach (int index in removedIndices.OrderByDescending(index => index))
                {
                    chosen.RemoveAt(index);
                }
                chosen.Add(replacement.Value);
                return true;
            }

            return false;
        }

        private static IEnumerable<List<IndexedOption>> BuildRepresentativeMerges(
            IGrouping<string, IndexedOption> roleGroup,
            int maximumWidth)
        {
            List<IGrouping<PawnKindDef, IndexedOption>> kinds = roleGroup
                .GroupBy(item => item.Option.Option.kind)
                .OrderBy(group => group.First().Option.Cost)
                .ToList();

            int cappedWidth = System.Math.Min(maximumWidth, roleGroup.Count());
            for (int width = 2; width <= cappedWidth; width++)
            {
                foreach (int[] selection in KindSelections(kinds.Count, width, 0))
                {
                    Dictionary<int, int> required = selection
                        .GroupBy(index => index)
                        .ToDictionary(group => group.Key, group => group.Count());
                    if (required.Any(pair => kinds[pair.Key].Count() < pair.Value))
                    {
                        continue;
                    }

                    Dictionary<int, int> used = new Dictionary<int, int>();
                    List<IndexedOption> result = new List<IndexedOption>();
                    foreach (int kindIndex in selection)
                    {
                        int offset = used.TryGetValue(kindIndex, out int count) ? count : 0;
                        result.Add(kinds[kindIndex].Skip(offset).First());
                        used[kindIndex] = offset + 1;
                    }

                    yield return result;
                }
            }
        }

        private static IEnumerable<int[]> KindSelections(int kindCount, int width, int minimumIndex)
        {
            if (width == 0)
            {
                yield return new int[0];
                yield break;
            }

            for (int index = minimumIndex; index < kindCount; index++)
            {
                foreach (int[] suffix in KindSelections(kindCount, width - 1, index))
                {
                    int[] selection = new int[suffix.Length + 1];
                    selection[0] = index;
                    System.Array.Copy(suffix, 0, selection, 1, suffix.Length);
                    yield return selection;
                }
            }
        }

        private static void SpendRemainingBudget(
            List<PawnGenOptionWithXenotype> chosen,
            List<PawnGenOptionWithXenotype> candidates,
            PawnGroupMakerParms parms,
            ICompressionPolicy policy,
            float budget,
            ISet<PawnKindDef> permittedBossPromotions)
        {
            for (int pass = 0; pass < 100; pass++)
            {
                float remaining = budget - TotalCost(chosen);
                if (remaining <= CostEpsilon)
                {
                    return;
                }

                UpgradeChoice best = default;
                for (int index = 0; index < chosen.Count; index++)
                {
                    PawnGenOptionWithXenotype current = chosen[index];
                    if (policy.IsProtected(current))
                    {
                        continue;
                    }

                    if (policy.PreserveKindPresence
                        && chosen.Count(option => option.Option.kind == current.Option.kind) <= 1)
                    {
                        continue;
                    }

                    List<PawnGenOptionWithXenotype> withoutCurrent = chosen
                        .Where((option, candidateIndex) => candidateIndex != index)
                        .ToList();
                    foreach (PawnGenOptionWithXenotype candidate in candidates)
                    {
                        float delta = candidate.Cost - current.Cost;
                        if (!BossCandidateAllowed(candidate, chosen, permittedBossPromotions)
                            || policy.RoleFor(candidate) != policy.RoleFor(current)
                            || delta <= CostEpsilon
                            || delta > remaining + CostEpsilon
                            || candidate.Cost > current.Cost * policy.MaximumUpgradeFactor + CostEpsilon
                            || !PawnGroupMakerUtility.PawnGenOptionValid(candidate.Option, parms, withoutCurrent))
                        {
                            continue;
                        }

                        if (!best.Valid || delta > best.Delta)
                        {
                            best = new UpgradeChoice(index, candidate, delta);
                        }
                    }
                }

                if (!best.Valid)
                {
                    return;
                }

                chosen[best.Index] = best.Replacement;
            }
        }

        private static float TotalCost(IEnumerable<PawnGenOptionWithXenotype> options)
        {
            return options.Sum(option => option.Cost);
        }

        private static bool BossCandidateAllowed(
            PawnGenOptionWithXenotype candidate,
            List<PawnGenOptionWithXenotype> chosen,
            ISet<PawnKindDef> permittedBossPromotions)
        {
            return !candidate.Option.kind.isBoss
                || (permittedBossPromotions.Contains(candidate.Option.kind)
                    && !chosen.Any(option => option.Option.kind.isBoss));
        }

        private readonly struct IndexedOption
        {
            public readonly int Index;
            public readonly PawnGenOptionWithXenotype Option;
            public readonly string Role;

            public IndexedOption(int index, PawnGenOptionWithXenotype option, string role)
            {
                Index = index;
                Option = option;
                Role = role;
            }
        }

        private readonly struct UpgradeChoice
        {
            public readonly int Index;
            public readonly PawnGenOptionWithXenotype Replacement;
            public readonly float Delta;
            public readonly bool Valid;

            public UpgradeChoice(int index, PawnGenOptionWithXenotype replacement, float delta)
            {
                Index = index;
                Replacement = replacement;
                Delta = delta;
                Valid = true;
            }
        }
    }

    public static class MechBossPromotionPolicy
    {
        private const float MaximumPromotionChance = 0.10f;

        public static HashSet<PawnKindDef> SelectPermittedBossKinds(
            IReadOnlyList<PawnGenOptionWithXenotype> original,
            IReadOnlyList<PawnGenOptionWithXenotype> vanillaCandidates,
            PawnGroupMakerParms parms,
            ICompressionPolicy policy)
        {
            HashSet<PawnKindDef> none = new HashSet<PawnKindDef>();
            if (policy != MechanoidRaidCompressionPolicy.Instance
                || original.Any(option => option.Option.kind.isBoss))
            {
                return none;
            }

            MechRaidClassification classification = MechRaidClassifier.Analyze(
                original.Select(option => option.Option.kind),
                parms.raidStrategy);
            if (classification.Archetype != MechRaidArchetype.Mixed)
            {
                return none;
            }

            List<PawnGenOptionWithXenotype> bosses = vanillaCandidates
                .Where(option => option.Option.kind.isBoss)
                .GroupBy(option => option.Option.kind)
                .Select(group => group.OrderByDescending(option => option.SelectionWeight).First())
                .OrderBy(option => option.Option.kind.defName)
                .ToList();
            float totalWeight = vanillaCandidates.Sum(option => System.Math.Max(0f, option.SelectionWeight));
            float bossWeight = bosses.Sum(option => System.Math.Max(0f, option.SelectionWeight));
            if (bosses.Count == 0 || totalWeight <= 0f || bossWeight <= 0f)
            {
                return none;
            }

            float chance = PromotionChance(bossWeight, totalWeight);
            int seed = parms.seed.GetValueOrDefault() ^ StableHash("boss-promotion");
            if (!RollAllowsPromotion(seed, chance))
            {
                return none;
            }

            float selection = DeterministicUnit(seed ^ StableHash("boss-kind")) * bossWeight;
            foreach (PawnGenOptionWithXenotype boss in bosses)
            {
                selection -= System.Math.Max(0f, boss.SelectionWeight);
                if (selection <= 0f)
                {
                    return new HashSet<PawnKindDef> { boss.Option.kind };
                }
            }
            return new HashSet<PawnKindDef> { bosses[bosses.Count - 1].Option.kind };
        }

        public static float PromotionChance(float bossWeight, float totalWeight)
        {
            if (bossWeight <= 0f || totalWeight <= 0f)
            {
                return 0f;
            }
            return System.Math.Min(MaximumPromotionChance, bossWeight / totalWeight);
        }

        public static bool RollAllowsPromotion(int seed, float chance)
        {
            return chance > 0f && DeterministicUnit(seed) < System.Math.Min(MaximumPromotionChance, chance);
        }

        private static float DeterministicUnit(int seed)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= value >> 16;
                value *= 0x7feb352dU;
                value ^= value >> 15;
                value *= 0x846ca68bU;
                value ^= value >> 16;
                return (value & 0x00ffffffU) / 16777216f;
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value)
                {
                    hash = hash * 31 + character;
                }
                return hash;
            }
        }
    }

    public sealed class CompressionResult
    {
        public List<PawnGenOptionWithXenotype> Options { get; }
        public float OriginalCost { get; }
        public float FinalCost { get; }
        public bool Changed { get; }

        public CompressionResult(
            List<PawnGenOptionWithXenotype> options,
            float originalCost,
            float finalCost,
            bool changed)
        {
            Options = options;
            OriginalCost = originalCost;
            FinalCost = finalCost;
            Changed = changed;
        }
    }
}
