namespace ImmersiveRaidCompression
{
    /// <summary>
    /// Read-only diagnostics for logs, automated tests, and future settings UI.
    /// This is deliberately not saved and has no effect on raid generation.
    /// </summary>
    public static class CompressionTelemetry
    {
        public static CompressionSnapshot LastSuccessfulCompression { get; private set; }

        public static void Record(
            string threatType,
            string factionName,
            int originalCount,
            int finalCount,
            float originalCost,
            float finalCost,
            float raidBudget,
            int originalBossCount,
            int finalBossCount)
        {
            LastSuccessfulCompression = new CompressionSnapshot(
                threatType,
                factionName,
                originalCount,
                finalCount,
                originalCost,
                finalCost,
                raidBudget,
                originalBossCount,
                finalBossCount);
        }

        public static void Reset()
        {
            LastSuccessfulCompression = null;
        }
    }

    public sealed class CompressionSnapshot
    {
        public string ThreatType { get; }
        public string FactionName { get; }
        public int OriginalCount { get; }
        public int FinalCount { get; }
        public float OriginalCost { get; }
        public float FinalCost { get; }
        public float RaidBudget { get; }
        public int OriginalBossCount { get; }
        public int FinalBossCount { get; }

        public CompressionSnapshot(
            string threatType,
            string factionName,
            int originalCount,
            int finalCount,
            float originalCost,
            float finalCost,
            float raidBudget,
            int originalBossCount,
            int finalBossCount)
        {
            ThreatType = threatType;
            FactionName = factionName;
            OriginalCount = originalCount;
            FinalCount = finalCount;
            OriginalCost = originalCost;
            FinalCost = finalCost;
            RaidBudget = raidBudget;
            OriginalBossCount = originalBossCount;
            FinalBossCount = finalBossCount;
        }
    }
}
