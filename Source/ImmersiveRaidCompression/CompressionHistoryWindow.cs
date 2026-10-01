using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ImmersiveRaidCompression
{
    public sealed class CompressionHistoryWindow : Window
    {
        private const float CardGap = 10f;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(920f, 700f);

        public CompressionHistoryWindow()
        {
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 180f, 36f), "IRC_HistoryTitle".Translate());
            Text.Font = GameFont.Small;

            Rect clearButton = new Rect(inRect.xMax - 170f, inRect.y, 170f, 32f);
            if (Widgets.ButtonText(clearButton, "IRC_ClearHistory".Translate()))
            {
                CompressionTelemetry.ClearHistory();
            }

            Rect explanation = new Rect(inRect.x, inRect.y + 38f, inRect.width, 42f);
            Widgets.Label(explanation, "IRC_HistoryExplanation".Translate());

            Rect outRect = new Rect(inRect.x, explanation.yMax + 6f, inRect.width, inRect.height - explanation.yMax - 48f);
            IReadOnlyList<CompressionSnapshot> records = CompressionTelemetry.History;
            if (records.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(outRect, "IRC_HistoryEmpty".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float viewWidth = outRect.width - 18f;
            float contentHeight = 0f;
            for (int i = 0; i < records.Count; i++)
            {
                contentHeight += RecordHeight(records[i], viewWidth) + CardGap;
            }

            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentHeight, outRect.height));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float y = 0f;
            for (int i = 0; i < records.Count; i++)
            {
                float height = RecordHeight(records[i], viewWidth);
                DrawRecord(new Rect(0f, y, viewWidth, height), records[i]);
                y += height + CardGap;
            }
            Widgets.EndScrollView();
        }

        private static float RecordHeight(CompressionSnapshot record, float width)
        {
            float textWidth = width - 24f;
            float originalHeight = Text.CalcHeight("IRC_OriginalComposition".Translate(record.OriginalComposition), textWidth);
            float finalHeight = Text.CalcHeight("IRC_FinalComposition".Translate(record.FinalComposition), textWidth);
            float reasonHeight = record.Successful
                ? 0f
                : Text.CalcHeight("IRC_HistoryReason".Translate(record.Reason), textWidth) + 4f;
            float identityHeight = Text.CalcHeight(
                "IRC_HistoryIdentity".Translate(record.IdentitySummary),
                textWidth) + 4f;
            float classificationHeight = string.IsNullOrEmpty(record.ClassificationSummary)
                ? 0f
                : Text.CalcHeight(
                    "IRC_HistoryClassification".Translate(record.ClassificationSummary),
                    textWidth) + 4f;
            float waveHeight = string.IsNullOrEmpty(record.WaveSummary)
                ? 0f
                : Text.CalcHeight("IRC_HistoryWave".Translate(record.WaveSummary), textWidth) + 4f;
            return 92f + originalHeight + finalHeight + identityHeight + classificationHeight + waveHeight + reasonHeight;
        }

        private static void DrawRecord(Rect rect, CompressionSnapshot record)
        {
            Color background = record.Successful
                ? new Color(0.14f, 0.24f, 0.16f, 0.9f)
                : new Color(0.25f, 0.20f, 0.12f, 0.9f);
            Widgets.DrawBoxSolid(rect, background);
            Widgets.DrawBox(rect);

            Rect inner = rect.ContractedBy(12f);
            Color oldColor = GUI.color;
            GUI.color = record.Successful ? Color.green : new Color(1f, 0.78f, 0.3f);
            Text.Font = GameFont.Medium;
            Widgets.Label(
                new Rect(inner.x, inner.y, inner.width, 30f),
                (record.Successful ? "IRC_HistorySuccess" : "IRC_HistorySkipped").Translate(
                    ThreatLabel(record.ThreatType),
                    record.SourceName));
            GUI.color = oldColor;
            Text.Font = GameFont.Small;

            float y = inner.y + 33f;
            Widgets.Label(
                new Rect(inner.x, y, inner.width, 24f),
                "IRC_HistoryNumbers".Translate(
                    record.OriginalCount,
                    record.FinalCount,
                    record.OriginalCost.ToString("F0"),
                    record.FinalCost.ToString("F0"),
                    record.RetainedPercent.ToString("F1"),
                    record.ThreatBudget.ToString("F0"),
                    record.GameTick));
            y += 27f;

            string original = "IRC_OriginalComposition".Translate(record.OriginalComposition);
            float originalHeight = Text.CalcHeight(original, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, originalHeight), original);
            y += originalHeight + 3f;

            string final = "IRC_FinalComposition".Translate(record.FinalComposition);
            float finalHeight = Text.CalcHeight(final, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, finalHeight), final);
            y += finalHeight + 3f;

            string identity = "IRC_HistoryIdentity".Translate(record.IdentitySummary);
            float identityHeight = Text.CalcHeight(identity, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, identityHeight), identity);
            y += identityHeight + 3f;

            if (!string.IsNullOrEmpty(record.ClassificationSummary))
            {
                string classification = "IRC_HistoryClassification".Translate(record.ClassificationSummary);
                float classificationHeight = Text.CalcHeight(classification, inner.width);
                Widgets.Label(new Rect(inner.x, y, inner.width, classificationHeight), classification);
                y += classificationHeight + 3f;
            }

            if (!string.IsNullOrEmpty(record.WaveSummary))
            {
                string wave = "IRC_HistoryWave".Translate(record.WaveSummary);
                float waveHeight = Text.CalcHeight(wave, inner.width);
                Widgets.Label(new Rect(inner.x, y, inner.width, waveHeight), wave);
                y += waveHeight + 3f;
            }

            if (!record.Successful)
            {
                Widgets.Label(
                    new Rect(inner.x, y, inner.width, rect.yMax - y - 8f),
                    "IRC_HistoryReason".Translate(record.Reason));
            }
        }

        private static string ThreatLabel(string threatType)
        {
            switch (threatType)
            {
                case "human raid":
                    return "IRC_ThreatHumanRaid".Translate();
                case "mechanoid raid":
                    return "IRC_ThreatMechanoidRaid".Translate();
                case "manhunter pack":
                    return "IRC_ThreatManhunterPack".Translate();
                case "mech cluster":
                    return "IRC_ThreatMechCluster".Translate();
                case "shambler swarm":
                    return "IRC_ThreatShamblerSwarm".Translate();
                case "shambler animal swarm":
                    return "IRC_ThreatShamblerAnimalSwarm".Translate();
                case "shambler assault":
                    return "IRC_ThreatShamblerAssault".Translate();
                default:
                    return threatType;
            }
        }
    }
}
