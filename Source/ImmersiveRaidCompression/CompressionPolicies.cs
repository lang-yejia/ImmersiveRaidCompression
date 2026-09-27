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
            List<string> tags = option.Option.kind.weaponTags;
            if (tags != null)
            {
                bool melee = tags.Any(tag => tag.IndexOf("Melee", StringComparison.OrdinalIgnoreCase) >= 0);
                bool ranged = tags.Any(tag => tag.IndexOf("Ranged", StringComparison.OrdinalIgnoreCase) >= 0
                    || tag.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) >= 0);
                if (melee && ranged)
                {
                    return "mixed";
                }

                if (melee)
                {
                    return "melee";
                }

                if (ranged)
                {
                    return "ranged";
                }
            }

            return "unclassified";
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
            PawnKindDef kind = option.Option.kind;
            return kind.isBoss || kind.isGoodBreacher || kind.canBeSapper || IsNamed(kind, "Termite");
        }

        public bool IsCandidateAllowed(PawnGenOptionWithXenotype option)
        {
            return !IsProtected(option);
        }

        public string RoleFor(PawnGenOptionWithXenotype option)
        {
            PawnKindDef kind = option.Option.kind;
            if (IsNamed(kind, "Scyther"))
            {
                return "melee";
            }

            if (IsNamed(kind, "Scorcher") || IsNamed(kind, "Burner"))
            {
                return "fire";
            }

            if (IsNamed(kind, "Legionary") || IsNamed(kind, "Centurion"))
            {
                return "shield-support";
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
