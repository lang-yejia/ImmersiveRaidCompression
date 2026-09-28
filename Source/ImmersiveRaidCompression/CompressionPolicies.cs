using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    public interface ICompressionPolicy
    {
        string ThreatType { get; }
        float MaximumUpgradeFactor { get; }
        int MaximumMergeWidth { get; }
        bool IsProtected(PawnGenOptionWithXenotype option);
        bool IsCandidateAllowed(PawnGenOptionWithXenotype option);
        string RoleFor(PawnGenOptionWithXenotype option);
    }

    public sealed class HumanRaidCompressionPolicy : ICompressionPolicy
    {
        public static readonly HumanRaidCompressionPolicy Instance = new HumanRaidCompressionPolicy();

        public string ThreatType => "human raid";
        public float MaximumUpgradeFactor => 3f;
        public int MaximumMergeWidth => 2;

        public bool IsProtected(PawnGenOptionWithXenotype option)
        {
            PawnKindDef kind = option.Option.kind;
            if (kind.factionLeader || kind.isGoodBreacher || kind.canBeSapper)
            {
                return true;
            }

            return kind.weaponTags != null && kind.weaponTags.Contains("GunSingleUse");
        }

        public bool IsCandidateAllowed(PawnGenOptionWithXenotype option)
        {
            return !IsProtected(option);
        }

        public string RoleFor(PawnGenOptionWithXenotype option)
        {
            PawnKindDef kind = option.Option.kind;
            List<string> tags = kind.weaponTags;
            string descriptor = (kind.defName + " " + string.Join(" ", tags ?? new List<string>()))
                .ToLowerInvariant();
            string role;
            if (ContainsAny(descriptor, "shield"))
            {
                role = "shield-melee";
            }
            else if (ContainsAny(descriptor, "sniper", "marksman", "longrange"))
            {
                role = "long-range";
            }
            else if (ContainsAny(descriptor, "grenad", "bomb", "launcher"))
            {
                role = "explosive";
            }
            else if (ContainsAny(descriptor, "molotov", "incendiary", "flame", "fire", "tox"))
            {
                role = "area-denial";
            }
            else if (ContainsAny(descriptor, "heavy", "shotgun"))
            {
                role = "heavy-ranged";
            }
            else if (tags != null)
            {
                bool melee = tags.Any(tag => tag.IndexOf("Melee", StringComparison.OrdinalIgnoreCase) >= 0);
                bool ranged = tags.Any(tag => tag.IndexOf("Ranged", StringComparison.OrdinalIgnoreCase) >= 0
                    || tag.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) >= 0);
                if (melee && ranged)
                {
                    role = "mixed";
                }
                else if (melee)
                {
                    role = "melee";
                }
                else if (ranged)
                {
                    role = "ranged";
                }
                else
                {
                    role = "unclassified";
                }
            }
            else
            {
                role = "unclassified";
            }

            // Xenotypes can carry fire breath, toxic attacks, unusual durability, or
            // other mechanics not represented by the PawnKind's weapon tags.
            return role + "|xenotype:" + (option.Xenotype?.defName ?? "none");
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            return fragments.Any(value.Contains);
        }
    }

    public sealed class MechanoidRaidCompressionPolicy : ICompressionPolicy
    {
        public static readonly MechanoidRaidCompressionPolicy Instance = new MechanoidRaidCompressionPolicy();

        public string ThreatType => "mechanoid raid";
        public float MaximumUpgradeFactor => 4f;
        public int MaximumMergeWidth => 3;

        public bool IsProtected(PawnGenOptionWithXenotype option)
        {
            return IsProtectedKind(option.Option.kind);
        }

        public bool IsCandidateAllowed(PawnGenOptionWithXenotype option)
        {
            return !IsForbiddenReplacementKind(option.Option.kind);
        }

        public string RoleFor(PawnGenOptionWithXenotype option)
        {
            return RoleForKind(option.Option.kind);
        }

        public bool IsProtectedKind(PawnKindDef kind)
        {
            return kind.isBoss || IsForbiddenReplacementKind(kind);
        }

        public bool IsForbiddenReplacementKind(PawnKindDef kind)
        {
            return kind.isGoodBreacher || kind.canBeSapper || IsNamed(kind, "Termite");
        }

        public string RoleForKind(PawnKindDef kind)
        {
            if (IsNamed(kind, "Scyther"))
            {
                return "melee";
            }

            if (IsNamed(kind, "Scorcher") || IsNamed(kind, "Burner"))
            {
                return "fire";
            }

            if (IsNamed(kind, "Tesseron"))
            {
                return "beam-fire";
            }

            if (IsNamed(kind, "Legionary") || IsNamed(kind, "Centurion"))
            {
                return "shield-support";
            }

            if (IsNamed(kind, "Lancer") || IsNamed(kind, "Pikeman"))
            {
                return "long-range";
            }

            if (IsNamed(kind, "Centipede") || IsNamed(kind, "Gunner"))
            {
                return "heavy-ranged";
            }

            if (kind.weaponTags != null && kind.weaponTags.Count > 0)
            {
                return "ranged";
            }

            return "specialist";
        }

        private static bool IsNamed(PawnKindDef kind, string fragment)
        {
            return kind.defName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
