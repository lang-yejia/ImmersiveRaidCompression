using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    [HarmonyPatch(typeof(IncidentWorker_FleshbeastAttack), "TryExecuteWorker")]
    public static class FleshbeastAttackContextPatch
    {
        public struct ContextState
        {
            public bool Active;
            public float Points;
            internal CompressionAggregate Aggregate;
        }

        [ThreadStatic] internal static bool Active;
        [ThreadStatic] internal static float Points;
        [ThreadStatic] private static CompressionAggregate aggregate;

        public static void Prefix(IncidentParms parms, out ContextState __state)
        {
            __state = new ContextState { Active = Active, Points = Points, Aggregate = aggregate };
            Active = true;
            Points = parms?.points ?? 0f;
            aggregate = new CompressionAggregate(Points);
        }

        public static Exception Finalizer(Exception __exception, ContextState __state)
        {
            CompressionAggregate completed = aggregate;
            Active = __state.Active;
            Points = __state.Points;
            aggregate = __state.Aggregate;
            if (__exception == null && completed != null && completed.Changed)
            {
                completed.Commit();
            }
            return __exception;
        }

        public static bool OwnsTelemetry => Active;

        public static bool TryRecordSuccessfulCompression(
            Faction faction,
            List<PawnGenOptionWithXenotype> original,
            CompressionResult result)
        {
            if (!Active || aggregate == null)
            {
                return false;
            }

            aggregate.Add(faction, original, result.Options, result.OriginalCost, result.FinalCost);
            return true;
        }

        internal sealed class CompressionAggregate
        {
            private readonly float threatBudget;
            private readonly Dictionary<PawnKindDef, int> originalKinds = new Dictionary<PawnKindDef, int>();
            private readonly Dictionary<PawnKindDef, int> finalKinds = new Dictionary<PawnKindDef, int>();
            private string sourceName;
            private int originalCount;
            private int finalCount;
            private float originalCost;
            private float finalCost;

            public bool Changed => finalCount < originalCount;

            public CompressionAggregate(float threatBudget)
            {
                this.threatBudget = threatBudget;
            }

            public void Add(
                Faction faction,
                IEnumerable<PawnGenOptionWithXenotype> original,
                IEnumerable<PawnGenOptionWithXenotype> compressed,
                float originalGroupCost,
                float finalGroupCost)
            {
                List<PawnGenOptionWithXenotype> originalList = original.ToList();
                List<PawnGenOptionWithXenotype> compressedList = compressed.ToList();
                sourceName = faction?.Name ?? Faction.OfEntities?.Name ?? "Anomaly";
                originalCount += originalList.Count;
                finalCount += compressedList.Count;
                originalCost += originalGroupCost;
                finalCost += finalGroupCost;
                AddKinds(originalKinds, originalList);
                AddKinds(finalKinds, compressedList);
            }

            public void Commit()
            {
                CompressionTelemetry.Record(
                    FleshbeastCompressionPolicy.Instance.ThreatType,
                    sourceName,
                    originalCount,
                    finalCount,
                    originalCost,
                    finalCost,
                    threatBudget,
                    0,
                    0,
                    DescribeKinds(originalKinds),
                    DescribeKinds(finalKinds),
                    "IRC_IdentityFleshbeastPreserved".Translate(),
                    null,
                    null,
                    "IRC_FleshbeastClassificationSummary".Translate());
                Verse.Log.Message(
                    "[Immersive Raid Compression] " + sourceName + " (fleshbeast attack total): "
                    + originalCount + " -> " + finalCount + " pawns, vanilla kind cost "
                    + originalCost.ToString("F0") + " -> " + finalCost.ToString("F0")
                    + ", incident budget " + threatBudget.ToString("F0") + ".");
            }

            private static void AddKinds(
                IDictionary<PawnKindDef, int> counts,
                IEnumerable<PawnGenOptionWithXenotype> options)
            {
                foreach (PawnGenOptionWithXenotype option in options)
                {
                    PawnKindDef kind = option.Option.kind;
                    counts[kind] = counts.TryGetValue(kind, out int count) ? count + 1 : 1;
                }
            }

            private static string DescribeKinds(IDictionary<PawnKindDef, int> counts)
            {
                return string.Join(", ", counts
                    .OrderBy(pair => pair.Key.label)
                    .Select(pair => pair.Key.LabelCap + " ×" + pair.Value));
            }
        }
    }

    public sealed class FleshbeastCompressionPolicy : ICompressionPolicy
    {
        public static readonly FleshbeastCompressionPolicy Instance = new FleshbeastCompressionPolicy();

        public string ThreatType => "fleshbeast attack";
        public float MaximumUpgradeFactor => 4f;
        public int MaximumMergeWidth => 4;
        public bool PreserveKindPresence => true;

        public bool IsProtected(PawnGenOptionWithXenotype option)
        {
            PawnKindDef kind = option.Option.kind;
            return kind.isBoss || kind.defName == "Bulbfreak" || !IsSpikeFamily(kind);
        }

        public bool IsCandidateAllowed(PawnGenOptionWithXenotype option)
        {
            return IsSpikeFamily(option.Option.kind) && !option.Option.kind.isBoss;
        }

        public string RoleFor(PawnGenOptionWithXenotype option)
        {
            PawnKindDef kind = option.Option.kind;
            if (kind.defName == "Bulbfreak")
            {
                return "bulbfreak";
            }
            if (kind.isBoss)
            {
                return "dreadmeld";
            }
            return IsSpikeFamily(kind) ? "spike-ranged" : "protected-special";
        }

        private static bool IsSpikeFamily(PawnKindDef kind)
        {
            return kind?.defName == "Fingerspike"
                || kind?.defName == "Toughspike"
                || kind?.defName == "Trispike";
        }
    }
}
