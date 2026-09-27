using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    /// <summary>
    /// Session-local audit trail used by the in-game history window and runtime tests.
    /// It never changes generation decisions and is intentionally not saved.
    /// </summary>
    public static class CompressionTelemetry
    {
        private const int MaximumHistoryEntries = 50;
        private static readonly List<CompressionSnapshot> history = new List<CompressionSnapshot>();

        public static CompressionSnapshot LastSuccessfulCompression { get; private set; }
        public static IReadOnlyList<CompressionSnapshot> History => history;

        public static void Record(
            string threatType,
            string sourceName,
            int originalCount,
            int finalCount,
            float originalCost,
            float finalCost,
            float threatBudget,
            int originalBossCount,
            int finalBossCount,
            string originalComposition,
            string finalComposition)
        {
            CompressionSnapshot snapshot = new CompressionSnapshot(
                true,
                threatType,
                sourceName,
                originalCount,
                finalCount,
                originalCost,
                finalCost,
                threatBudget,
                originalBossCount,
                finalBossCount,
                originalComposition,
                finalComposition,
                null,
                CurrentTick());
            LastSuccessfulCompression = snapshot;
            Add(snapshot);
        }

        public static void RecordSkipped(
            string threatType,
            string sourceName,
            int originalCount,
            float originalCost,
            float threatBudget,
            string originalComposition,
            string reason)
        {
            Add(new CompressionSnapshot(
                false,
                threatType,
                sourceName,
                originalCount,
                originalCount,
                originalCost,
                originalCost,
                threatBudget,
                0,
                0,
                originalComposition,
                originalComposition,
                reason,
                CurrentTick()));
        }

        public static string DescribeComposition(IEnumerable<PawnGenOptionWithXenotype> options)
        {
            return string.Join(", ", options
                .GroupBy(option => option.Option.kind)
                .OrderBy(group => group.Key.label)
                .Select(group => group.Key.LabelCap + " ×" + group.Count()));
        }

        public static void Reset()
        {
            LastSuccessfulCompression = null;
            history.Clear();
        }

        public static void ClearHistory()
        {
            history.Clear();
        }

        private static void Add(CompressionSnapshot snapshot)
        {
            history.Insert(0, snapshot);
            if (history.Count > MaximumHistoryEntries)
            {
                history.RemoveRange(MaximumHistoryEntries, history.Count - MaximumHistoryEntries);
            }
        }

        private static int CurrentTick()
        {
            return Current.Game == null ? 0 : Find.TickManager.TicksGame;
        }
    }

    public sealed class CompressionSnapshot
    {
        public bool Successful { get; }
        public string ThreatType { get; }
        public string SourceName { get; }
        public string FactionName => SourceName;
        public int OriginalCount { get; }
        public int FinalCount { get; }
        public float OriginalCost { get; }
        public float FinalCost { get; }
        public float ThreatBudget { get; }
        public float RaidBudget => ThreatBudget;
        public int OriginalBossCount { get; }
        public int FinalBossCount { get; }
        public string OriginalComposition { get; }
        public string FinalComposition { get; }
        public string Reason { get; }
        public int GameTick { get; }

        public float RetainedPercent => OriginalCost <= 0f ? 100f : FinalCost / OriginalCost * 100f;

        public CompressionSnapshot(
            bool successful,
            string threatType,
            string sourceName,
            int originalCount,
            int finalCount,
            float originalCost,
            float finalCost,
            float threatBudget,
            int originalBossCount,
            int finalBossCount,
            string originalComposition,
            string finalComposition,
            string reason,
            int gameTick)
        {
            Successful = successful;
            ThreatType = threatType;
            SourceName = sourceName;
            OriginalCount = originalCount;
            FinalCount = finalCount;
            OriginalCost = originalCost;
            FinalCost = finalCost;
            ThreatBudget = threatBudget;
            OriginalBossCount = originalBossCount;
            FinalBossCount = finalBossCount;
            OriginalComposition = originalComposition;
            FinalComposition = finalComposition;
            Reason = reason;
            GameTick = gameTick;
        }
    }
}
