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
            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, 2020f);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            listing.CheckboxLabeled(
                "IRC_EnableHumanRaids".Translate(),
                ref Settings.enableHumanRaids,
                "IRC_EnableHumanRaidsDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnablePhasedPirateWaves".Translate(),
                ref Settings.enablePhasedPirateWaves,
                "IRC_EnablePhasedPirateWavesDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnableTacticalPirateDrops".Translate(),
                ref Settings.enableTacticalPirateDrops,
                "IRC_EnableTacticalPirateDropsDesc".Translate());
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
            listing.CheckboxLabeled(
                "IRC_EnableInfestations".Translate(),
                ref Settings.enableInfestations,
                "IRC_EnableInfestationsDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnableAnomalyMassThreats".Translate(),
                ref Settings.enableAnomalyMassThreats,
                "IRC_EnableAnomalyMassThreatsDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnablePhasedShamblerWaves".Translate(),
                ref Settings.enablePhasedShamblerWaves,
                "IRC_EnablePhasedShamblerWavesDesc".Translate());
            listing.Gap();
            listing.Label("IRC_MinRaidPoints".Translate(Settings.minimumRaidPoints.ToString("F0")));
            Settings.minimumRaidPoints = Mathf.Round(listing.Slider(Settings.minimumRaidPoints, 500f, 10000f) / 100f) * 100f;
            listing.Label("IRC_HumanSoftPawnCap".Translate(Settings.humanSoftPawnCap));
            Settings.humanSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.humanSoftPawnCap, 10, 100));
            listing.Label("IRC_PirateWaveSplitCount".Translate(Settings.pirateWaveSplitCountThreshold));
            Settings.pirateWaveSplitCountThreshold = Mathf.RoundToInt(listing.Slider(Settings.pirateWaveSplitCountThreshold, 12, 120));
            listing.Label("IRC_PirateWaveMinimumPoints".Translate(Settings.pirateWaveMinimumPoints.ToString("F0")));
            Settings.pirateWaveMinimumPoints = Mathf.Round(listing.Slider(Settings.pirateWaveMinimumPoints, 500f, 5000f) / 100f) * 100f;
            listing.Label("IRC_PirateWaveBudgetFraction".Translate((Settings.pirateWaveBudgetFraction * 100f).ToString("F0")));
            Settings.pirateWaveBudgetFraction = Mathf.Round(listing.Slider(Settings.pirateWaveBudgetFraction, 0.10f, 0.40f) * 20f) / 20f;
            listing.Label("IRC_PirateWaveTrigger".Translate((Settings.pirateWaveTriggerFraction * 100f).ToString("F0")));
            Settings.pirateWaveTriggerFraction = Mathf.Round(listing.Slider(Settings.pirateWaveTriggerFraction, 0.2f, 0.85f) * 20f) / 20f;
            listing.Label("IRC_PirateWaveDelay".Translate((Settings.pirateWaveMinimumDelayTicks / 60f).ToString("F0")));
            Settings.pirateWaveMinimumDelayTicks = Mathf.RoundToInt(listing.Slider(Settings.pirateWaveMinimumDelayTicks, 60, 1800) / 60f) * 60;
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
            listing.Label("IRC_InfestationSoftPawnCap".Translate(Settings.infestationSoftPawnCap));
            Settings.infestationSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.infestationSoftPawnCap, 20, 120));
            listing.Label("IRC_MinInfestationPoints".Translate(Settings.minimumInfestationPoints.ToString("F0")));
            Settings.minimumInfestationPoints = Mathf.Round(listing.Slider(Settings.minimumInfestationPoints, 1000f, 15000f) / 500f) * 500f;
            listing.Label("IRC_FleshbeastSoftPawnCap".Translate(Settings.fleshbeastSoftPawnCap));
            Settings.fleshbeastSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.fleshbeastSoftPawnCap, 4, 24));
            listing.Label("IRC_MinAnomalyThreatPoints".Translate(Settings.minimumAnomalyThreatPoints.ToString("F0")));
            Settings.minimumAnomalyThreatPoints = Mathf.Round(listing.Slider(Settings.minimumAnomalyThreatPoints, 1000f, 15000f) / 500f) * 500f;
            listing.Label("IRC_ShamblerWaveSplitCount".Translate(Settings.shamblerWaveSplitCountThreshold));
            Settings.shamblerWaveSplitCountThreshold = Mathf.RoundToInt(listing.Slider(Settings.shamblerWaveSplitCountThreshold, 24, 180));
            listing.Label("IRC_ShamblerWaveMinimumPoints".Translate(Settings.shamblerWaveMinimumPoints.ToString("F0")));
            Settings.shamblerWaveMinimumPoints = Mathf.Round(listing.Slider(Settings.shamblerWaveMinimumPoints, 500f, 5000f) / 100f) * 100f;
            listing.Label("IRC_ShamblerWaveBudgetFraction".Translate((Settings.shamblerWaveBudgetFraction * 100f).ToString("F0")));
            Settings.shamblerWaveBudgetFraction = Mathf.Round(listing.Slider(Settings.shamblerWaveBudgetFraction, 0.10f, 0.40f) * 20f) / 20f;
            listing.Label("IRC_ShamblerWaveTrigger".Translate((Settings.shamblerWaveTriggerFraction * 100f).ToString("F0")));
            Settings.shamblerWaveTriggerFraction = Mathf.Round(listing.Slider(Settings.shamblerWaveTriggerFraction, 0.2f, 0.85f) * 20f) / 20f;
            listing.Label("IRC_ShamblerWaveDelay".Translate((Settings.shamblerWaveMinimumDelayTicks / 60f).ToString("F0")));
            Settings.shamblerWaveMinimumDelayTicks = Mathf.RoundToInt(listing.Slider(Settings.shamblerWaveMinimumDelayTicks, 60, 1800) / 60f) * 60;
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
        private int settingsVersion = 5;
        public bool enableHumanRaids = true;
        public bool enablePhasedPirateWaves = true;
        public bool enableTacticalPirateDrops = true;
        public bool enableMechanoidRaids = true;
        public bool enablePhasedMechanoidWaves = true;
        public bool enableTacticalMechDrops = true;
        public bool enableManhunterPacks = true;
        public bool enableMechClusters = true;
        public bool enableInfestations = true;
        public bool enableAnomalyMassThreats = true;
        public bool enablePhasedShamblerWaves = true;
        public float minimumRaidPoints = 2500f;
        public float minimumManhunterPoints = 1000f;
        public float minimumMechClusterPoints = 2500f;
        public float minimumInfestationPoints = 5000f;
        public float minimumAnomalyThreatPoints = 3000f;
        public int humanSoftPawnCap = 45;
        public int pirateWaveSplitCountThreshold = 36;
        public float pirateWaveMinimumPoints = 1500f;
        public float pirateWaveBudgetFraction = 0.15f;
        public float pirateWaveTriggerFraction = 0.65f;
        public int pirateWaveMinimumDelayTicks = 180;
        public int mechanoidSoftPawnCap = 24;
        public int mechWaveSplitCountThreshold = 36;
        public float mechWaveMinimumPoints = 1500f;
        public float mechWaveBudgetFraction = 0.15f;
        public float mechWaveTriggerFraction = 0.65f;
        public int mechWaveMinimumDelayTicks = 180;
        public int manhunterSoftPawnCap = 30;
        public int mechClusterSoftPawnCap = 16;
        public int infestationSoftPawnCap = 45;
        public int fleshbeastSoftPawnCap = 8;
        public int shamblerWaveSplitCountThreshold = 60;
        public float shamblerWaveMinimumPoints = 1000f;
        public float shamblerWaveBudgetFraction = 0.15f;
        public float shamblerWaveTriggerFraction = 0.65f;
        public int shamblerWaveMinimumDelayTicks = 180;
        public bool verboseLogging;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);
            Scribe_Values.Look(ref enableHumanRaids, "enableHumanRaids", true);
            Scribe_Values.Look(ref enablePhasedPirateWaves, "enablePhasedPirateWaves", true);
            Scribe_Values.Look(ref enableTacticalPirateDrops, "enableTacticalPirateDrops", true);
            Scribe_Values.Look(ref enableMechanoidRaids, "enableMechanoidRaids", true);
            Scribe_Values.Look(ref enablePhasedMechanoidWaves, "enablePhasedMechanoidWaves", true);
            Scribe_Values.Look(ref enableTacticalMechDrops, "enableTacticalMechDrops", true);
            Scribe_Values.Look(ref enableManhunterPacks, "enableManhunterPacks", true);
            Scribe_Values.Look(ref enableMechClusters, "enableMechClusters", true);
            Scribe_Values.Look(ref enableInfestations, "enableInfestations", true);
            Scribe_Values.Look(ref enableAnomalyMassThreats, "enableAnomalyMassThreats", true);
            Scribe_Values.Look(ref enablePhasedShamblerWaves, "enablePhasedShamblerWaves", true);
            Scribe_Values.Look(ref minimumRaidPoints, "minimumRaidPoints", 2500f);
            Scribe_Values.Look(ref minimumManhunterPoints, "minimumManhunterPoints", 1000f);
            Scribe_Values.Look(ref minimumMechClusterPoints, "minimumMechClusterPoints", 2500f);
            Scribe_Values.Look(ref minimumInfestationPoints, "minimumInfestationPoints", 5000f);
            Scribe_Values.Look(ref minimumAnomalyThreatPoints, "minimumAnomalyThreatPoints", 3000f);
            Scribe_Values.Look(ref humanSoftPawnCap, "humanSoftPawnCap", 45);
            Scribe_Values.Look(ref pirateWaveSplitCountThreshold, "pirateWaveSplitCountThreshold", 36);
            Scribe_Values.Look(ref pirateWaveMinimumPoints, "pirateWaveMinimumPoints", 1500f);
            Scribe_Values.Look(ref pirateWaveBudgetFraction, "pirateWaveBudgetFraction", 0.15f);
            Scribe_Values.Look(ref pirateWaveTriggerFraction, "pirateWaveTriggerFraction", 0.65f);
            Scribe_Values.Look(ref pirateWaveMinimumDelayTicks, "pirateWaveMinimumDelayTicks", 180);
            Scribe_Values.Look(ref mechanoidSoftPawnCap, "mechanoidSoftPawnCap", 24);
            Scribe_Values.Look(ref mechWaveSplitCountThreshold, "mechWaveSplitCountThreshold", 36);
            Scribe_Values.Look(ref mechWaveMinimumPoints, "mechWaveMinimumPoints", 1500f);
            Scribe_Values.Look(ref mechWaveBudgetFraction, "mechWaveBudgetFraction", 0.15f);
            Scribe_Values.Look(ref mechWaveTriggerFraction, "mechWaveTriggerFraction", 0.65f);
            Scribe_Values.Look(ref mechWaveMinimumDelayTicks, "mechWaveMinimumDelayTicks", 180);
            Scribe_Values.Look(ref manhunterSoftPawnCap, "manhunterSoftPawnCap", 30);
            Scribe_Values.Look(ref mechClusterSoftPawnCap, "mechClusterSoftPawnCap", 16);
            Scribe_Values.Look(ref infestationSoftPawnCap, "infestationSoftPawnCap", 45);
            Scribe_Values.Look(ref fleshbeastSoftPawnCap, "fleshbeastSoftPawnCap", 8);
            Scribe_Values.Look(ref shamblerWaveSplitCountThreshold, "shamblerWaveSplitCountThreshold", 60);
            Scribe_Values.Look(ref shamblerWaveMinimumPoints, "shamblerWaveMinimumPoints", 1000f);
            Scribe_Values.Look(ref shamblerWaveBudgetFraction, "shamblerWaveBudgetFraction", 0.15f);
            Scribe_Values.Look(ref shamblerWaveTriggerFraction, "shamblerWaveTriggerFraction", 0.65f);
            Scribe_Values.Look(ref shamblerWaveMinimumDelayTicks, "shamblerWaveMinimumDelayTicks", 180);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars && settingsVersion < 1)
            {
                enableTacticalMechDrops = true;
                mechWaveTriggerFraction = 0.65f;
                mechWaveMinimumDelayTicks = 180;
                settingsVersion = 1;
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars && settingsVersion < 2)
            {
                enablePhasedPirateWaves = true;
                enableTacticalPirateDrops = true;
                pirateWaveSplitCountThreshold = 36;
                pirateWaveMinimumPoints = 1500f;
                pirateWaveBudgetFraction = 0.15f;
                pirateWaveTriggerFraction = 0.65f;
                pirateWaveMinimumDelayTicks = 180;
                settingsVersion = 2;
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars && settingsVersion < 3)
            {
                enableInfestations = true;
                minimumInfestationPoints = 5000f;
                infestationSoftPawnCap = 45;
                settingsVersion = 3;
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars && settingsVersion < 4)
            {
                enableAnomalyMassThreats = true;
                minimumAnomalyThreatPoints = 3000f;
                fleshbeastSoftPawnCap = 8;
                settingsVersion = 4;
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars && settingsVersion < 5)
            {
                enablePhasedShamblerWaves = true;
                shamblerWaveSplitCountThreshold = 60;
                shamblerWaveMinimumPoints = 1000f;
                shamblerWaveBudgetFraction = 0.15f;
                shamblerWaveTriggerFraction = 0.65f;
                shamblerWaveMinimumDelayTicks = 180;
                settingsVersion = 5;
            }
        }
    }
}
