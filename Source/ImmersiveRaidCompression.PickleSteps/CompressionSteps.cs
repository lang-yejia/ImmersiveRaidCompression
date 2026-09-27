using System.Linq;
using RimWorks.Pickle;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression.PickleTests
{
    [PickleSteps]
    public sealed class CompressionSteps
    {
        [Given("raid compression telemetry is reset")]
        public void ResetTelemetry(PickleContext context)
        {
            CompressionTelemetry.Reset();
            CompressionMod.Settings.verboseLogging = true;
            CompressionMod.Settings.enableManhunterPacks = true;
            CompressionMod.Settings.minimumManhunterPoints = 1000f;
            CompressionMod.Settings.manhunterSoftPawnCap = 30;
        }

        [When("a mechanoid raid fires with {int} points")]
        public void MechanoidRaidFires(PickleContext context, int points)
        {
            Map map = Find.CurrentMap;
            context.Assert(map != null, "No current map is loaded.");

            Faction faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Mechanoid);
            context.Assert(faction != null, "The mechanoid faction is not present in this world.");

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamed("RaidEnemy");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.points = points;
            parms.faction = faction;
            parms.forced = true;
            // This deterministic seed exercises one of the faction's mixed makers.
            parms.pawnGroupMakerSeed = context.ScenarioSeed + 3;

            bool fired = incident.Worker.TryExecute(parms);
            context.Assert(fired, "The forced mechanoid raid declined to fire.");
        }

        [Then("the last compression handled a {string}")]
        public void LastCompressionHandledThreat(PickleContext context, string threatType)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful raid compression was recorded.");
            context.Assert(
                snapshot.ThreatType == threatType,
                "Expected compression type '" + threatType + "' but got '" + snapshot.ThreatType + "'.");
        }

        [Then("the last compression introduced no mechanoid bosses")]
        public void NoMechanoidBossesIntroduced(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful raid compression was recorded.");
            context.Assert(
                snapshot.FinalBossCount == snapshot.OriginalBossCount,
                "Compression changed the boss count from " + snapshot.OriginalBossCount
                + " to " + snapshot.FinalBossCount + ".");
        }

        [Then("compression history contains detailed before and after compositions")]
        public void HistoryContainsCompositions(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful compression was recorded.");
            context.Assert(CompressionTelemetry.History.Count > 0, "Compression history is empty.");
            context.Assert(
                !string.IsNullOrWhiteSpace(snapshot.OriginalComposition),
                "Original composition was not recorded.");
            context.Assert(
                !string.IsNullOrWhiteSpace(snapshot.FinalComposition),
                "Final composition was not recorded.");
        }

        [Then("the last compression preserved its tactical identity")]
        public void TacticalIdentityWasPreserved(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful compression was recorded.");
            context.Assert(snapshot.IdentityPreserved, "Compression reported a tactical identity violation.");
            context.Assert(
                !string.IsNullOrWhiteSpace(snapshot.IdentitySummary),
                "Compression did not record its tactical identity proof.");
        }

        [Then("the manhunter replacement has a compatible animal tactical profile")]
        public void ManhunterReplacementHasCompatibleProfile(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful compression was recorded.");
            PawnKindDef original = DefDatabase<PawnKindDef>.GetNamedSilentFail(snapshot.OriginalPrimaryDefName);
            PawnKindDef replacement = DefDatabase<PawnKindDef>.GetNamedSilentFail(snapshot.FinalPrimaryDefName);
            context.Assert(original != null, "The original animal kind was not recorded.");
            context.Assert(replacement != null, "The replacement animal kind was not recorded.");
            context.Assert(
                ThreatIdentity.AnimalKindsAreCompatible(original, replacement, out string reason),
                "Animal replacement changed the tactical profile: " + reason + ".");
            context.Assert(
                replacement.defName.IndexOf("Mastodon", System.StringComparison.OrdinalIgnoreCase) < 0,
                "A fast predator was incorrectly replaced by a mastodon-style tank.");
        }

        [Then("an incompatible large animal tank is rejected for the original manhunter species")]
        public void IncompatibleAnimalTankIsRejected(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful compression was recorded.");
            PawnKindDef original = DefDatabase<PawnKindDef>.GetNamedSilentFail(snapshot.OriginalPrimaryDefName);
            PawnKindDef mastodon = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mastodon");
            context.Assert(original != null, "The original animal kind was not recorded.");
            context.Assert(mastodon != null, "The official mastodon definition is unavailable.");
            context.Assert(
                !ThreatIdentity.AnimalKindsAreCompatible(original, mastodon, out _),
                "The identity guard incorrectly accepted a large animal tank.");
        }

        [When("I open the compression history window")]
        public void OpenCompressionHistory(PickleContext context)
        {
            context.Assert(Find.WindowStack != null, "The game window stack is unavailable.");
            Find.WindowStack.Add(new CompressionHistoryWindow());
        }

        [Then("the compression history window is open")]
        public void CompressionHistoryIsOpen(PickleContext context)
        {
            context.Assert(
                Find.WindowStack.Windows.Any(window => window is CompressionHistoryWindow),
                "The compression history window was not added to the game window stack.");
        }

        [Then("the last raid compression reduced the pawn count and retained between {int} and {int} percent of vanilla kind cost")]
        public void LastCompressionReducedCountAndRetainedCost(
            PickleContext context,
            int minimumPercent,
            int maximumPercent)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful raid compression was recorded.");
            context.Assert(
                snapshot.FinalCount < snapshot.OriginalCount,
                "Compression did not reduce pawn count: " + snapshot.OriginalCount + " -> " + snapshot.FinalCount + ".");

            float retainedPercent = snapshot.OriginalCost <= 0f
                ? 100f
                : snapshot.FinalCost / snapshot.OriginalCost * 100f;
            context.Assert(
                retainedPercent + 0.001f >= minimumPercent,
                "Compression retained only " + retainedPercent.ToString("F1")
                + "% of vanilla kind cost; expected at least " + minimumPercent + "%.");
            context.Assert(
                retainedPercent <= maximumPercent + 0.001f,
                "Compression inflated vanilla kind cost to " + retainedPercent.ToString("F1")
                + "%; expected at most " + maximumPercent + "%.");
        }
    }
}
