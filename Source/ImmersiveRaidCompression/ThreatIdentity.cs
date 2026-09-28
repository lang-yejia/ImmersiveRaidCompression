using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    /// <summary>
    /// Hard compatibility rules for replacements. Point value alone is not enough:
    /// a replacement must ask the player to solve substantially the same problem.
    /// </summary>
    public static class ThreatIdentity
    {
        private const float MinimumSpeedRatio = 0.80f;
        private const float MaximumSpeedRatio = 1.25f;
        private const float MinimumBodySizeRatio = 0.65f;
        private const float MaximumBodySizeRatio = 1.50f;
        private const float MaximumArmorDifference = 0.15f;
        private const float MaximumAnimalPowerRatio = 1.75f;

        public static bool AnimalKindsAreCompatible(
            PawnKindDef original,
            PawnKindDef candidate,
            out string reason)
        {
            reason = null;
            if (original?.RaceProps?.Animal != true || candidate?.RaceProps?.Animal != true)
            {
                reason = "not-animal";
                return false;
            }

            if (candidate.isBoss || candidate.combatPower <= original.combatPower)
            {
                reason = candidate.isBoss ? "boss" : "not-stronger";
                return false;
            }

            if (candidate.combatPower > original.combatPower * MaximumAnimalPowerRatio)
            {
                reason = "power-jump";
                return false;
            }

            RaceProperties originalRace = original.RaceProps;
            RaceProperties candidateRace = candidate.RaceProps;
            if (originalRace.predator != candidateRace.predator
                || originalRace.herdAnimal != candidateRace.herdAnimal
                || originalRace.packAnimal != candidateRace.packAnimal)
            {
                reason = "different-behavior";
                return false;
            }

            if (!WithinRatio(candidateRace.baseBodySize, originalRace.baseBodySize,
                    MinimumBodySizeRatio, MaximumBodySizeRatio))
            {
                reason = "body-size";
                return false;
            }

            float originalSpeed = Stat(original.race, StatDefOf.MoveSpeed);
            float candidateSpeed = Stat(candidate.race, StatDefOf.MoveSpeed);
            if (!WithinRatio(candidateSpeed, originalSpeed, MinimumSpeedRatio, MaximumSpeedRatio))
            {
                reason = "movement-speed";
                return false;
            }

            if (Math.Abs(Stat(candidate.race, StatDefOf.ArmorRating_Sharp)
                    - Stat(original.race, StatDefOf.ArmorRating_Sharp)) > MaximumArmorDifference
                || Math.Abs(Stat(candidate.race, StatDefOf.ArmorRating_Blunt)
                    - Stat(original.race, StatDefOf.ArmorRating_Blunt)) > MaximumArmorDifference)
            {
                reason = "armor-profile";
                return false;
            }

            if (!SameAbilities(original, candidate))
            {
                reason = "special-abilities";
                return false;
            }

            return true;
        }

        public static string AnimalIdentitySummary(PawnKindDef original, PawnKindDef candidate)
        {
            return "IRC_IdentityAnimalPreserved".Translate(
                Stat(original.race, StatDefOf.MoveSpeed).ToString("F1"),
                Stat(candidate.race, StatDefOf.MoveSpeed).ToString("F1"),
                original.RaceProps.baseBodySize.ToString("F2"),
                candidate.RaceProps.baseBodySize.ToString("F2"));
        }

        public static bool PawnGroupIdentityIsPreserved(
            IReadOnlyList<PawnGenOptionWithXenotype> original,
            IReadOnlyList<PawnGenOptionWithXenotype> compressed,
            ICompressionPolicy policy,
            ISet<PawnKindDef> permittedAddedBosses,
            out string reason)
        {
            reason = null;
            Dictionary<string, int> originalRoles = RoleCounts(original, policy);
            Dictionary<string, int> finalRoles = RoleCounts(compressed, policy);
            if (!originalRoles.Keys.OrderBy(role => role)
                    .SequenceEqual(finalRoles.Keys.OrderBy(role => role)))
            {
                reason = "role-set-changed";
                return false;
            }

            foreach (KeyValuePair<string, int> role in originalRoles)
            {
                float originalShare = role.Value / (float)original.Count;
                float finalShare = finalRoles[role.Key] / (float)compressed.Count;
                if (originalShare >= 0.15f && Math.Abs(finalShare - originalShare) > 0.15f)
                {
                    reason = "major-role-ratio-changed:" + role.Key;
                    return false;
                }
            }

            List<string> originalProtected = original
                .Where(policy.IsProtected)
                .Select(option => ProtectedIdentity(option))
                .OrderBy(value => value)
                .ToList();
            List<string> finalProtected = compressed
                .Where(policy.IsProtected)
                .Select(option => ProtectedIdentity(option))
                .OrderBy(value => value)
                .ToList();
            List<string> permittedAdditionalBosses = compressed
                .Where(option => option.Option.kind.isBoss
                    && permittedAddedBosses != null
                    && permittedAddedBosses.Contains(option.Option.kind))
                .Select(ProtectedIdentity)
                .ToList();
            bool protectedUnitsValid = originalProtected.SequenceEqual(finalProtected)
                || (permittedAdditionalBosses.Count == 1
                    && !original.Any(option => option.Option.kind.isBoss)
                    && finalProtected.Count == originalProtected.Count + 1
                    && originalProtected.SequenceEqual(
                        finalProtected.Where(identity => identity != permittedAdditionalBosses[0])));
            if (!protectedUnitsValid)
            {
                reason = "protected-units-changed";
                return false;
            }

            return true;
        }

        public static bool KindGroupIdentityIsPreserved(
            IReadOnlyList<PawnKindDef> original,
            IReadOnlyList<PawnKindDef> compressed,
            Func<PawnKindDef, string> roleFor,
            Func<PawnKindDef, bool> isProtected,
            out string reason)
        {
            reason = null;
            Dictionary<string, int> originalRoles = original
                .GroupBy(roleFor)
                .ToDictionary(group => group.Key, group => group.Count());
            Dictionary<string, int> finalRoles = compressed
                .GroupBy(roleFor)
                .ToDictionary(group => group.Key, group => group.Count());
            if (!originalRoles.Keys.OrderBy(role => role)
                    .SequenceEqual(finalRoles.Keys.OrderBy(role => role)))
            {
                reason = "role-set-changed";
                return false;
            }

            foreach (KeyValuePair<string, int> role in originalRoles)
            {
                float originalShare = role.Value / (float)original.Count;
                float finalShare = finalRoles[role.Key] / (float)compressed.Count;
                if (originalShare >= 0.15f && Math.Abs(finalShare - originalShare) > 0.15f)
                {
                    reason = "major-role-ratio-changed:" + role.Key;
                    return false;
                }
            }

            List<string> originalProtected = original
                .Where(isProtected)
                .Select(kind => kind.defName)
                .OrderBy(value => value)
                .ToList();
            List<string> finalProtected = compressed
                .Where(isProtected)
                .Select(kind => kind.defName)
                .OrderBy(value => value)
                .ToList();
            if (!originalProtected.SequenceEqual(finalProtected))
            {
                reason = "protected-units-changed";
                return false;
            }

            return true;
        }

        private static Dictionary<string, int> RoleCounts(
            IEnumerable<PawnGenOptionWithXenotype> options,
            ICompressionPolicy policy)
        {
            return options
                .GroupBy(policy.RoleFor)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        private static string ProtectedIdentity(PawnGenOptionWithXenotype option)
        {
            return option.Option.kind.defName + "|" + (option.Xenotype?.defName ?? "none");
        }

        private static bool SameAbilities(PawnKindDef left, PawnKindDef right)
        {
            IEnumerable<string> leftAbilities = (left.abilities ?? new List<AbilityDef>())
                .Select(ability => ability.defName)
                .OrderBy(name => name);
            IEnumerable<string> rightAbilities = (right.abilities ?? new List<AbilityDef>())
                .Select(ability => ability.defName)
                .OrderBy(name => name);
            return leftAbilities.SequenceEqual(rightAbilities);
        }

        private static bool WithinRatio(float value, float baseline, float minimum, float maximum)
        {
            if (baseline <= 0.001f)
            {
                return Math.Abs(value - baseline) <= 0.001f;
            }

            float ratio = value / baseline;
            return ratio >= minimum && ratio <= maximum;
        }

        private static float Stat(ThingDef thing, StatDef stat)
        {
            return thing == null ? 0f : thing.GetStatValueAbstract(stat);
        }
    }
}
