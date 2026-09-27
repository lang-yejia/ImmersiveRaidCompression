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

        [Then("the last raid compression reduced the pawn count and retained at least {int} percent of vanilla kind cost")]
        public void LastCompressionReducedCountAndRetainedCost(PickleContext context, int minimumPercent)
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
        }
    }
}
