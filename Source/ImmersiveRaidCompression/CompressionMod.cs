using UnityEngine;
using Verse;

namespace ImmersiveRaidCompression
{
    public sealed class CompressionMod : Mod
    {
        public static CompressionSettings Settings { get; private set; }

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
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "IRC_EnableHumanRaids".Translate(),
                ref Settings.enableHumanRaids,
                "IRC_EnableHumanRaidsDesc".Translate());
            listing.CheckboxLabeled(
                "IRC_EnableMechanoidRaids".Translate(),
                ref Settings.enableMechanoidRaids,
                "IRC_EnableMechanoidRaidsDesc".Translate());
            listing.Gap();
            listing.Label("IRC_MinRaidPoints".Translate(Settings.minimumRaidPoints.ToString("F0")));
            Settings.minimumRaidPoints = Mathf.Round(listing.Slider(Settings.minimumRaidPoints, 500f, 10000f) / 100f) * 100f;
            listing.Label("IRC_HumanSoftPawnCap".Translate(Settings.humanSoftPawnCap));
            Settings.humanSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.humanSoftPawnCap, 10, 100));
            listing.Label("IRC_MechanoidSoftPawnCap".Translate(Settings.mechanoidSoftPawnCap));
            Settings.mechanoidSoftPawnCap = Mathf.RoundToInt(listing.Slider(Settings.mechanoidSoftPawnCap, 8, 60));
            listing.CheckboxLabeled(
                "IRC_VerboseLogging".Translate(),
                ref Settings.verboseLogging,
                "IRC_VerboseLoggingDesc".Translate());
            listing.End();
        }
    }

    public sealed class CompressionSettings : ModSettings
    {
        public bool enableHumanRaids = true;
        public bool enableMechanoidRaids = true;
        public float minimumRaidPoints = 2500f;
        public int humanSoftPawnCap = 45;
        public int mechanoidSoftPawnCap = 24;
        public bool verboseLogging;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableHumanRaids, "enableHumanRaids", true);
            Scribe_Values.Look(ref enableMechanoidRaids, "enableMechanoidRaids", true);
            Scribe_Values.Look(ref minimumRaidPoints, "minimumRaidPoints", 2500f);
            Scribe_Values.Look(ref humanSoftPawnCap, "humanSoftPawnCap", 45);
            Scribe_Values.Look(ref mechanoidSoftPawnCap, "mechanoidSoftPawnCap", 24);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
        }
    }
}
