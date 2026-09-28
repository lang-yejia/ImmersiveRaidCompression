using UnityEngine;
using Verse;

namespace ImmersiveRaidCompression
{
    public sealed class CompressionMod : Mod
    {
        public static CompressionSettings Settings { get; private set; }
        private Vector2 settingsScrollPosition;

        public CompressionMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CompressionSettings>();
        }

        public override string SettingsCategory()
        {
            return "Immersive Raid Compression";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, 1060f);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            listing.CheckboxLabeled(
                "IRC_EnableHumanRaids".Translate(),
                ref Settings.enableHumanRaids,
                "IRC_EnableHumanRaidsDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnableMechanoidRaids".Translate(),
                ref Settings.enableMechanoidRaids,
                "IRC_EnableMechanoidRaidsDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnablePhasedMechanoidWaves".Translate(),
                ref Settings.enablePhasedMechanoidWaves,
                "IRC_EnablePhasedMechanoidWavesDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnableTacticalMechDrops".Translate(),
                ref Settings.enableTacticalMechDrops,
                "IRC_EnableTacticalMechDropsDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnableManhunterPacks".Translate(),
                ref Settings.enableManhunterPacks,
                "IRC_EnableManhunterPacksDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnableMechClusters".Translate(),
                ref Settings.enableMechClusters,
                "IRC_EnableMechClustersDesc".Translate());
            listing.Gap();
            listing.Label("IRC_MinRaidPoints".Translate(Settings.minimumRaidPoints.ToString("F0")));
            Settings.minimumRaidPoints = Mathf.Round(listing.Slider(Settings.minimumRaidPoints, 500f, 10000f) / 100f) * 100f;
            listing.Label("IRC_HumanSoftPawnCap".Translate(Settings.humanSoftPawnCap));
            Settings.humanSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.humanSoftPawnCap, 10, 100));
            listing.Label("IRC_MechanoidSoftPawnCap".Translate(Settings.mechanoidSoftPawnCap));
            Settings.mechanoidSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.mechanoidSoftPawnCap, 8, 60));
            listing.Label("IRC_MechWaveSplitCount".Translate(Settings.mechWaveSplitCountThreshold));
            Settings.mechWaveSplitCountThreshold = Mathf.RoundToInt(listing.Slider(Settings.mechWaveSplitCountThreshold, 12, 120));
            listing.Label("IRC_MechWaveMinimumPoints".Translate(Settings.mechWaveMinimumPoints.ToString("F0")));
            Settings.mechWaveMinimumPoints = Mathf.Round(listing.Slider(Settings.mechWaveMinimumPoints, 500f, 5000f) / 100f) * 100f;
            listing.Label("IRC_MechWaveBudgetFraction".Translate((Settings.mechWaveBudgetFraction * 100f).ToString("F0")));
            Settings.mechWaveBudgetFraction = Mathf.Round(listing.Slider(Settings.mechWaveBudgetFraction, 0.10f, 0.40f) * 20f) / 20f;
            listing.Label("IRC_MechWaveTrigger".Translate((Settings.mechWaveTriggerFraction * 100f).ToString("F0")));
            Settings.mechWaveTriggerFraction = Mathf.Round(listing.Slider(Settings.mechWaveTriggerFraction, 0.2f, 0.85f) * 20f) / 20f;
            listing.Label("IRC_MechWaveDelay".Translate((Settings.mechWaveMinimumDelayTicks / 60f).ToString("F0")));
            Settings.mechWaveMinimumDelayTicks = Mathf.RoundToInt(listing.Slider(Settings.mechWaveMinimumDelayTicks, 60, 1800) / 60f) * 60;
            listing.Label("IRC_ManhunterSoftPawnCap".Translate(Settings.manhunterSoftPawnCap));
            Settings.manhunterSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.manhunterSoftPawnCap, 8, 100));
            listing.Label("IRC_MinManhunterPoints".Translate(Settings.minimumManhunterPoints.ToString("F0")));
            Settings.minimumManhunterPoints = Mathf.Round(listing.Slider(Settings.minimumManhunterPoints, 250f, 5000f) / 50f) * 50f;
            listing.Label("IRC_MechClusterSoftPawnCap".Translate(Settings.mechClusterSoftPawnCap));
            Settings.mechClusterSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.mechClusterSoftPawnCap, 4, 40));
            listing.Label("IRC_MinMechClusterPoints".Translate(Settings.minimumMechClusterPoints.ToString("F0")));
            Settings.minimumMechClusterPoints = Mathf.Round(listing.Slider(Settings.minimumMechClusterPoints, 500f, 10000f) / 100f) * 100f;
            listing.CheckboxLabeled(
                "IRC_VerboseLogging".Translate(),
                ref Settings.verboseLogging,
                "IRC_VerboseLoggingDesc".Translate());
            listing.Gap();
            if (listing.ButtonText("IRC_OpenHistory".Translate()))
            {
                Find.WindowStack.Add(new CompressionHistoryWindow());
            }
            listing.End();
            Widgets.EndScrollView();
        }
    }

    public sealed class CompressionSettings : ModSettings
    {
        private int settingsVersion = 1;
        public bool enableHumanRaids = true;
        public bool enableMechanoidRaids = true;
        public bool enablePhasedMechanoidWaves = true;
        public bool enableTacticalMechDrops = true;
        public bool enableManhunterPacks = true;
        public bool enableMechClusters = true;
        public float minimumRaidPoints = 2500f;
        public float minimumManhunterPoints = 1000f;
        public float minimumMechClusterPoints = 2500f;
        public int humanSoftPawnCap = 45;
        public int mechanoidSoftPawnCap = 24;
        public int mechWaveSplitCountThreshold = 36;
        public float mechWaveMinimumPoints = 1500f;
        public float mechWaveBudgetFraction = 0.15f;
        public float mechWaveTriggerFraction = 0.65f;
        public int mechWaveMinimumDelayTicks = 180;
        public int manhunterSoftPawnCap = 30;
        public int mechClusterSoftPawnCap = 16;
        public bool verboseLogging;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);
            Scribe_Values.Look(ref enableHumanRaids, "enableHumanRaids", true);
            Scribe_Values.Look(ref enableMechanoidRaids, "enableMechanoidRaids", true);
            Scribe_Values.Look(ref enablePhasedMechanoidWaves, "enablePhasedMechanoidWaves", true);
            Scribe_Values.Look(ref enableTacticalMechDrops, "enableTacticalMechDrops", true);
            Scribe_Values.Look(ref enableManhunterPacks, "enableManhunterPacks", true);
            Scribe_Values.Look(ref enableMechClusters, "enableMechClusters", true);
            Scribe_Values.Look(ref minimumRaidPoints, "minimumRaidPoints", 2500f);
            Scribe_Values.Look(ref minimumManhunterPoints, "minimumManhunterPoints", 1000f);
            Scribe_Values.Look(ref minimumMechClusterPoints, "minimumMechClusterPoints", 2500f);
            Scribe_Values.Look(ref humanSoftPawnCap, "humanSoftPawnCap", 45);
            Scribe_Values.Look(ref mechanoidSoftPawnCap, "mechanoidSoftPawnCap", 24);
            Scribe_Values.Look(ref mechWaveSplitCountThreshold, "mechWaveSplitCountThreshold", 36);
            Scribe_Values.Look(ref mechWaveMinimumPoints, "mechWaveMinimumPoints", 1500f);
            Scribe_Values.Look(ref mechWaveBudgetFraction, "mechWaveBudgetFraction", 0.15f);
            Scribe_Values.Look(ref mechWaveTriggerFraction, "mechWaveTriggerFraction", 0.65f);
            Scribe_Values.Look(ref mechWaveMinimumDelayTicks, "mechWaveMinimumDelayTicks", 180);
            Scribe_Values.Look(ref manhunterSoftPawnCap, "manhunterSoftPawnCap", 30);
            Scribe_Values.Look(ref mechClusterSoftPawnCap, "mechClusterSoftPawnCap", 16);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars && settingsVersion < 1)
            {
                enableTacticalMechDrops = true;
                mechWaveTriggerFraction = 0.65f;
                mechWaveMinimumDelayTicks = 180;
                settingsVersion = 1;
            }
        }
    }
}
