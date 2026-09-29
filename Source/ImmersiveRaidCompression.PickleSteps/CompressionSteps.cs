using System.Collections.Generic;
using System.Linq;
using RimWorks.Pickle;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ImmersiveRaidCompression.PickleTests
{
    [PickleSteps]
    public sealed class CompressionSteps
    {
        private bool mechClusterStructureMatched;
        private bool mechClusterPositionsCameFromVanillaSketch;
        private bool mechRaidClassifierMatchedExpectedFamilies;
        private bool mechBossPromotionRulesAreBounded;
        private bool humanRaidClassifierMatchedExpectedFamilies;
        private bool humanSpecialistEscortFloorWorks;
        private bool humanDropDensityFloorWorks;
        private Faction humanSiegeFaction;
        private Faction humanMultiFrontFaction;
        private int expectedMultiFrontSides;
        private bool phasedMechWaveSplitAndReleased;
        private bool phasedMechWaveUsedEdgeAnchor;
        private bool phasedMechWavesMeetDynamicMinimum;
        private bool phasedMechUndersizedTailsWereMerged;
        private bool phasedMechUnsafeSplitFallsBackToVanilla;
        private List<Pawn> phasedMechFirstWave;
        private int phasedMechDeferredBeforeRelease;
        private Lord phasedMechOriginalLord;

        [Given("raid compression telemetry is reset")]
        public void ResetTelemetry(PickleContext context)
        {
            CompressionTelemetry.Reset();
            CompressionMod.Settings.verboseLogging = true;
            CompressionMod.Settings.enableHumanRaids = true;
            CompressionMod.Settings.minimumRaidPoints = 1000f;
            CompressionMod.Settings.humanSoftPawnCap = 8;
            CompressionMod.Settings.enableManhunterPacks = true;
            CompressionMod.Settings.minimumManhunterPoints = 1000f;
            CompressionMod.Settings.manhunterSoftPawnCap = 30;
            CompressionMod.Settings.enableMechClusters = true;
            CompressionMod.Settings.minimumMechClusterPoints = 500f;
            CompressionMod.Settings.mechClusterSoftPawnCap = 8;
            CompressionMod.Settings.enablePhasedMechanoidWaves = true;
            CompressionMod.Settings.enableTacticalMechDrops = true;
            CompressionMod.Settings.mechanoidSoftPawnCap = 8;
            CompressionMod.Settings.mechWaveSplitCountThreshold = 12;
            CompressionMod.Settings.mechWaveMinimumPoints = 1000f;
            CompressionMod.Settings.mechWaveBudgetFraction = 0.15f;
            CompressionMod.Settings.mechWaveTriggerFraction = 0.65f;
            CompressionMod.Settings.mechWaveMinimumDelayTicks = 180;
        }

        [When("an ordinary human edge raid fires with {int} points")]
        public void OrdinaryHumanEdgeRaidFires(PickleContext context, int points)
        {
            Map map = Find.CurrentMap;
            context.Assert(map != null, "No current map is loaded.");

            FactionDef pirateWaster = DefDatabase<FactionDef>.GetNamed("PirateWaster");
            Faction faction = Find.FactionManager.FirstFactionOfDef(pirateWaster);
            context.Assert(
                faction != null && faction.HostileTo(Faction.OfPlayer),
                "No hostile waster pirate faction is present in this world.");

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamed("RaidEnemy");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.points = points;
            parms.faction = faction;
            parms.raidStrategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.forced = true;
            parms.pawnGroupMakerSeed = context.ScenarioSeed;

            bool fired = incident.Worker.TryExecute(parms);
            context.Assert(fired, "The forced ordinary human edge raid declined to fire.");
        }

        [When("a human siege raid fires with {int} points")]
        public void HumanSiegeRaidFires(PickleContext context, int points)
        {
            Map map = Find.CurrentMap;
            context.Assert(map != null, "No current map is loaded.");

            FactionDef pirateWaster = DefDatabase<FactionDef>.GetNamed("PirateWaster");
            humanSiegeFaction = Find.FactionManager.FirstFactionOfDef(pirateWaster);
            context.Assert(
                humanSiegeFaction != null && humanSiegeFaction.HostileTo(Faction.OfPlayer),
                "No hostile waster pirate faction is present in this world.");

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamed("RaidEnemy");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.points = points;
            parms.faction = humanSiegeFaction;
            parms.raidStrategy = DefDatabase<RaidStrategyDef>.GetNamed("Siege");
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.forced = true;
            parms.pawnGroupMakerSeed = context.ScenarioSeed;

            bool fired = incident.Worker.TryExecute(parms);
            context.Assert(fired, "The forced human siege raid declined to fire.");
        }

        [When("a human sapper raid fires with {int} points")]
        public void HumanSapperRaidFires(PickleContext context, int points)
        {
            FireHumanSpecialistRaid(context, points, "ImmediateAttackSappers", "sapper");
        }

        [When("a human breach raid fires with {int} points")]
        public void HumanBreachRaidFires(PickleContext context, int points)
        {
            FireHumanSpecialistRaid(context, points, "ImmediateAttackBreaching", "breach");
        }

        private static void FireHumanSpecialistRaid(
            PickleContext context,
            int points,
            string strategyDefName,
            string description)
        {
            Map map = Find.CurrentMap;
            context.Assert(map != null, "No current map is loaded.");

            FactionDef pirateWaster = DefDatabase<FactionDef>.GetNamed("PirateWaster");
            Faction faction = Find.FactionManager.FirstFactionOfDef(pirateWaster);
            context.Assert(
                faction != null && faction.HostileTo(Faction.OfPlayer),
                "No hostile waster pirate faction is present in this world.");

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamed("RaidEnemy");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.points = points;
            parms.faction = faction;
            parms.raidStrategy = DefDatabase<RaidStrategyDef>.GetNamed(strategyDefName);
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.forced = true;
            parms.pawnGroupMakerSeed = context.ScenarioSeed;

            bool fired = incident.Worker.TryExecute(parms);
            context.Assert(fired, "The forced human " + description + " raid declined to fire.");
        }

        [When("a human center-drop raid fires with {int} points")]
        public void HumanCenterDropRaidFires(PickleContext context, int points)
        {
            FireHumanDropRaid(context, points, PawnsArrivalModeDefOf.CenterDrop, "center-drop");
        }

        [When("a human random-drop raid fires with {int} points")]
        public void HumanRandomDropRaidFires(PickleContext context, int points)
        {
            FireHumanDropRaid(context, points, PawnsArrivalModeDefOf.RandomDrop, "random-drop");
        }

        private static void FireHumanDropRaid(
            PickleContext context,
            int points,
            PawnsArrivalModeDef arrivalMode,
            string description)
        {
            Map map = Find.CurrentMap;
            context.Assert(map != null, "No current map is loaded.");

            FactionDef pirateWaster = DefDatabase<FactionDef>.GetNamed("PirateWaster");
            Faction faction = Find.FactionManager.FirstFactionOfDef(pirateWaster);
            context.Assert(
                faction != null && faction.HostileTo(Faction.OfPlayer),
                "No hostile waster pirate faction is present in this world.");

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamed("RaidEnemy");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.points = points;
            parms.faction = faction;
            parms.raidStrategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
            parms.raidArrivalMode = arrivalMode;
            parms.forced = true;
            parms.pawnGroupMakerSeed = context.ScenarioSeed;

            bool fired = incident.Worker.TryExecute(parms);
            context.Assert(fired, "The forced human " + description + " raid declined to fire.");
        }

        [When("a grouped human edge raid fires with {int} points")]
        public void GroupedHumanEdgeRaidFires(PickleContext context, int points)
        {
            FireHumanMultiFrontRaid(
                context,
                points,
                DefDatabase<PawnsArrivalModeDef>.GetNamed("EdgeWalkInGroups"),
                2,
                "grouped edge");
        }

        [When("a distributed human edge raid fires with {int} points")]
        public void DistributedHumanEdgeRaidFires(PickleContext context, int points)
        {
            FireHumanMultiFrontRaid(
                context,
                points,
                DefDatabase<PawnsArrivalModeDef>.GetNamed("EdgeWalkInDistributed"),
                3,
                "distributed edge");
        }

        private void FireHumanMultiFrontRaid(
            PickleContext context,
            int points,
            PawnsArrivalModeDef arrivalMode,
            int minimumSides,
            string description)
        {
            Map map = Find.CurrentMap;
            context.Assert(map != null, "No current map is loaded.");

            FactionDef pirateWaster = DefDatabase<FactionDef>.GetNamed("PirateWaster");
            humanMultiFrontFaction = Find.FactionManager.FirstFactionOfDef(pirateWaster);
            context.Assert(
                humanMultiFrontFaction != null && humanMultiFrontFaction.HostileTo(Faction.OfPlayer),
                "No hostile waster pirate faction is present in this world.");

            expectedMultiFrontSides = minimumSides;
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamed("RaidEnemy");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.points = points;
            parms.faction = humanMultiFrontFaction;
            parms.raidStrategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
            parms.raidArrivalMode = arrivalMode;
            parms.forced = true;
            parms.pawnGroupMakerSeed = context.ScenarioSeed;

            bool fired = incident.Worker.TryExecute(parms);
            context.Assert(fired, "The forced human " + description + " raid declined to fire.");
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

        [When("I classify representative vanilla mechanoid forces")]
        public void ClassifyRepresentativeMechanoidForces(PickleContext context)
        {
            RaidStrategyDef immediate = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
            RaidStrategyDef breachStrategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttackBreaching");
            PawnKindDef scyther = DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther");
            PawnKindDef militor = DefDatabase<PawnKindDef>.GetNamed("Mech_Militor");
            PawnKindDef pikeman = DefDatabase<PawnKindDef>.GetNamed("Mech_Pikeman");
            PawnKindDef termite = DefDatabase<PawnKindDef>.GetNamed("Mech_Termite_Breach");
            PawnKindDef warqueen = DefDatabase<PawnKindDef>.GetNamed("Mech_Warqueen");

            MechRaidClassification swarm = MechRaidClassifier.Analyze(
                Enumerable.Repeat(scyther, 30),
                immediate);
            MechRaidClassification mixed = MechRaidClassifier.Analyze(
                Enumerable.Repeat(scyther, 10)
                    .Concat(Enumerable.Repeat(militor, 10))
                    .Concat(Enumerable.Repeat(pikeman, 10)),
                immediate);
            MechRaidClassification breach = MechRaidClassifier.Analyze(
                Enumerable.Repeat(scyther, 29).Concat(new[] { termite }),
                breachStrategy);
            MechRaidClassification bossLed = MechRaidClassifier.Analyze(
                Enumerable.Repeat(militor, 29).Concat(new[] { warqueen }),
                immediate);

            List<PawnKindDef> pureSwarm = Enumerable.Repeat(scyther, 30).ToList();
            bool arrivalRulesMatched =
                MechRaidWaveController.CanPhase(pureSwarm, immediate, PawnsArrivalModeDefOf.EdgeWalkIn)
                && MechRaidWaveController.CanPhase(pureSwarm, immediate, PawnsArrivalModeDefOf.EdgeDrop)
                && !MechRaidWaveController.CanPhase(
                    pureSwarm,
                    immediate,
                    PawnsArrivalModeDefOf.CenterDrop)
                && !MechRaidWaveController.CanPhase(
                    pureSwarm,
                    immediate,
                    PawnsArrivalModeDefOf.RandomDrop)
                && !MechRaidWaveController.CanPhase(
                    pureSwarm,
                    immediate,
                    PawnsArrivalModeDefOf.EdgeWalkInGroups);

            mechRaidClassifierMatchedExpectedFamilies =
                swarm.Archetype == MechRaidArchetype.HomogeneousMelee
                && swarm.Treatment == MechRaidTreatment.PhasedReinforcementCandidate
                && mixed.Archetype == MechRaidArchetype.Mixed
                && mixed.Treatment == MechRaidTreatment.VanillaPromotion
                && breach.Archetype == MechRaidArchetype.Breach
                && breach.Treatment == MechRaidTreatment.ProtectedStrategy
                && bossLed.Archetype == MechRaidArchetype.BossLed
                && bossLed.Treatment == MechRaidTreatment.ProtectedStrategy
                && arrivalRulesMatched;
            float cappedChance = MechBossPromotionPolicy.PromotionChance(50f, 100f);
            bool atLeastOneWinningSeed = Enumerable.Range(0, 1000)
                .Any(seed => MechBossPromotionPolicy.RollAllowsPromotion(seed, cappedChance));
            bool atLeastOneLosingSeed = Enumerable.Range(0, 1000)
                .Any(seed => !MechBossPromotionPolicy.RollAllowsPromotion(seed, cappedChance));
            mechBossPromotionRulesAreBounded =
                MechanoidRaidCompressionPolicy.Instance.IsProtectedKind(warqueen)
                && !MechanoidRaidCompressionPolicy.Instance.IsForbiddenReplacementKind(warqueen)
                && System.Math.Abs(cappedChance - 0.10f) < 0.0001f
                && System.Math.Abs(MechBossPromotionPolicy.PromotionChance(1f, 100f) - 0.01f) < 0.0001f
                && atLeastOneWinningSeed
                && atLeastOneLosingSeed;
        }

        [Then("mixed mechanoid boss promotion is rare and bounded")]
        public void BossPromotionIsVanillaBounded(PickleContext context)
        {
            context.Assert(
                mechBossPromotionRulesAreBounded,
                "The boss promotion rules did not preserve bosses while enforcing the ten-percent deterministic cap.");
        }

        [Then("the mechanoid classifier separates swarms, mixed forces, breaches, and boss-led forces")]
        public void ClassifierSeparatesMechanicalForces(PickleContext context)
        {
            context.Assert(
                mechRaidClassifierMatchedExpectedFamilies,
                "The mechanoid raid classifier did not preserve the expected tactical families.");
        }

        [Then("the last mechanoid raid history record contains a treatment classification")]
        public void MechanoidHistoryContainsClassification(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful mechanoid compression was recorded.");
            context.Assert(
                !string.IsNullOrWhiteSpace(snapshot.ClassificationSummary),
                "The mechanoid raid record did not contain a classification summary.");
        }

        [When("I classify representative vanilla human raid strategies")]
        public void ClassifyRepresentativeHumanRaids(PickleContext context)
        {
            PawnKindDef pirate = DefDatabase<PawnKindDef>.GetNamed("Pirate");
            PawnKindDef breacher = DefDatabase<PawnKindDef>.GetNamed("Tribal_Breacher");
            RaidStrategyDef immediate = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
            RaidStrategyDef siege = DefDatabase<RaidStrategyDef>.GetNamed("Siege");
            RaidStrategyDef sapper = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttackSappers");
            RaidStrategyDef breach = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttackBreaching");
            PawnsArrivalModeDef edgeGroups = DefDatabase<PawnsArrivalModeDef>.GetNamed("EdgeWalkInGroups");
            PawnsArrivalModeDef edgeDropGroups = DefDatabase<PawnsArrivalModeDef>.GetNamed("EdgeDropGroups");
            PawnsArrivalModeDef edgeDistributed = DefDatabase<PawnsArrivalModeDef>.GetNamed("EdgeWalkInDistributed");

            HumanRaidClassification direct = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                immediate,
                PawnsArrivalModeDefOf.EdgeWalkIn);
            HumanRaidClassification directWithProtectedBreacher = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 49).Concat(new[] { breacher }),
                immediate,
                PawnsArrivalModeDefOf.EdgeWalkIn);
            HumanRaidClassification siegeRaid = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                siege,
                PawnsArrivalModeDefOf.EdgeWalkIn);
            HumanRaidClassification invalidDropSiege = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                siege,
                PawnsArrivalModeDefOf.CenterDrop);
            HumanRaidClassification sapperRaid = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                sapper,
                PawnsArrivalModeDefOf.EdgeWalkIn);
            HumanRaidClassification breachRaid = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 49).Concat(new[] { breacher }),
                breach,
                PawnsArrivalModeDefOf.EdgeWalkIn);
            HumanRaidClassification invalidGroupedBreach = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 49).Concat(new[] { breacher }),
                breach,
                edgeGroups);
            HumanRaidClassification centerDrop = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                immediate,
                PawnsArrivalModeDefOf.CenterDrop);
            HumanRaidClassification randomDrop = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                immediate,
                PawnsArrivalModeDefOf.RandomDrop);
            HumanRaidClassification multiDirection = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                immediate,
                edgeGroups);
            HumanRaidClassification multiDirectionDrop = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                immediate,
                edgeDropGroups);
            HumanRaidClassification distributed = HumanRaidClassifier.Analyze(
                Enumerable.Repeat(pirate, 50),
                immediate,
                edgeDistributed);

            humanRaidClassifierMatchedExpectedFamilies =
                direct.Archetype == HumanRaidArchetype.DirectAssault
                && direct.Treatment == HumanRaidTreatment.VanillaPromotion
                && directWithProtectedBreacher.Archetype == HumanRaidArchetype.DirectAssault
                && directWithProtectedBreacher.Treatment == HumanRaidTreatment.VanillaPromotion
                && siegeRaid.Archetype == HumanRaidArchetype.Siege
                && siegeRaid.Treatment == HumanRaidTreatment.SiegeVanillaPromotion
                && invalidDropSiege.Archetype == HumanRaidArchetype.Siege
                && invalidDropSiege.Treatment == HumanRaidTreatment.ProtectedUntilDedicatedHandler
                && sapperRaid.Archetype == HumanRaidArchetype.Sapper
                && sapperRaid.Treatment == HumanRaidTreatment.SapperEscortPromotion
                && breachRaid.Archetype == HumanRaidArchetype.Breach
                && breachRaid.Treatment == HumanRaidTreatment.BreachEscortPromotion
                && invalidGroupedBreach.Treatment == HumanRaidTreatment.ProtectedUntilDedicatedHandler
                && centerDrop.Archetype == HumanRaidArchetype.CenterDrop
                && centerDrop.Treatment == HumanRaidTreatment.DropAssaultPromotion
                && randomDrop.Archetype == HumanRaidArchetype.RandomDrop
                && randomDrop.Treatment == HumanRaidTreatment.DropAssaultPromotion
                && multiDirection.Archetype == HumanRaidArchetype.MultiDirection
                && multiDirection.Treatment == HumanRaidTreatment.MultiFrontPromotion
                && multiDirectionDrop.Treatment == HumanRaidTreatment.MultiFrontPromotion
                && distributed.Treatment == HumanRaidTreatment.MultiFrontPromotion
                && new[] { invalidDropSiege, invalidGroupedBreach }
                    .All(result => result.Treatment == HumanRaidTreatment.ProtectedUntilDedicatedHandler);
            humanSpecialistEscortFloorWorks =
                HumanCompressionCountRules.MinimumSpecialistTargetCount(100, 10, 20) == 78
                && HumanCompressionCountRules.MinimumSpecialistTargetCount(20, 5, 18) == 18;
            humanDropDensityFloorWorks =
                HumanCompressionCountRules.MinimumDropTargetCount(100, 20) == 80
                && HumanCompressionCountRules.MinimumDropTargetCount(20, 18) == 18;
            humanDropDensityFloorWorks = humanDropDensityFloorWorks
                && HumanCompressionCountRules.MinimumMultiFrontTargetCount(100, 20) == 90
                && HumanCompressionCountRules.MinimumMultiFrontTargetCount(20, 18) == 18;
        }

        [Then("the human raid classifier assigns dedicated treatments and protects invalid combinations")]
        public void HumanClassifierProtectsSpecialStrategies(PickleContext context)
        {
            context.Assert(
                humanRaidClassifierMatchedExpectedFamilies
                    && humanSpecialistEscortFloorWorks
                    && humanDropDensityFloorWorks,
                "The human raid classifier did not separate ordinary assaults from siege, sapper, breach, drop, and multi-direction raids.");
        }

        [Then("the last human raid history record contains a treatment classification")]
        public void HumanHistoryContainsClassification(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful human raid compression was recorded.");
            context.Assert(
                snapshot.ThreatType == "human raid"
                && !string.IsNullOrWhiteSpace(snapshot.ClassificationSummary),
                "The human raid record did not contain a classification summary.");
        }

        [Then("the compressed human siege retains its vanilla siege controller")]
        public void HumanSiegeRetainsVanillaController(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful human siege compression was recorded.");
            context.Assert(
                snapshot.ThreatType == "human raid"
                && snapshot.ClassificationSummary.Contains("IRC_HumanArchetype_Siege".Translate()),
                "The compression record was not classified as a human siege.");
            context.Assert(
                snapshot.IdentitySummary == "IRC_IdentitySiegePreserved".Translate(),
                "The siege-specific preservation proof was not recorded.");
            context.Assert(
                Find.CurrentMap.lordManager.lords.Any(lord =>
                    lord.faction == humanSiegeFaction && lord.LordJob is LordJob_Siege),
                "The compressed force did not retain RimWorld's vanilla LordJob_Siege controller.");
        }

        [Then("the compressed specialist raid preserves every path-opening unit and its escort floor")]
        public void SpecialistRaidPreservesPathOpeners(PickleContext context)
        {
            AssertSpecialistRaidPreserved(context, "IRC_HumanArchetype_Sapper");
        }

        [Then("the compressed breach raid preserves every path-opening unit and its escort floor")]
        public void BreachRaidPreservesPathOpeners(PickleContext context)
        {
            AssertSpecialistRaidPreserved(context, "IRC_HumanArchetype_Breach");
        }

        private static void AssertSpecialistRaidPreserved(
            PickleContext context,
            string archetypeTranslationKey)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful specialist raid compression was recorded.");
            context.Assert(
                snapshot.ThreatType == "human raid"
                && snapshot.ClassificationSummary.Contains(archetypeTranslationKey.Translate()),
                "The compression record did not contain the expected specialist raid classification.");
            context.Assert(
                snapshot.IdentitySummary == "IRC_IdentityPathingSpecialistsPreserved".Translate(),
                "The specialist and escort preservation proof was not recorded.");
        }

        [Then("the compressed center-drop raid preserves its vanilla arrival and density floor")]
        public void CenterDropRaidPreservesArrival(PickleContext context)
        {
            AssertDropRaidPreserved(context, "IRC_HumanArchetype_CenterDrop");
        }

        [Then("the compressed random-drop raid preserves its vanilla arrival and density floor")]
        public void RandomDropRaidPreservesArrival(PickleContext context)
        {
            AssertDropRaidPreserved(context, "IRC_HumanArchetype_RandomDrop");
        }

        private static void AssertDropRaidPreserved(
            PickleContext context,
            string archetypeTranslationKey)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful human drop-raid compression was recorded.");
            context.Assert(
                snapshot.ThreatType == "human raid"
                && snapshot.ClassificationSummary.Contains(archetypeTranslationKey.Translate()),
                "The compression record did not contain the expected drop-raid classification.");
            context.Assert(
                snapshot.IdentitySummary == "IRC_IdentityDropArrivalPreserved".Translate(),
                "The vanilla drop-arrival and density preservation proof was not recorded.");
            context.Assert(
                snapshot.FinalCount >= System.Math.Ceiling(snapshot.OriginalCount * 0.80f),
                "The compressed drop raid fell below its eighty-percent pawn-density floor.");
        }

        [Then("the compressed multi-front raid preserves its vanilla approaches and density floor")]
        public void MultiFrontRaidPreservesApproaches(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful multi-front raid compression was recorded.");
            context.Assert(
                snapshot.ThreatType == "human raid"
                && snapshot.ClassificationSummary.Contains("IRC_HumanArchetype_MultiDirection".Translate()),
                "The compression record was not classified as a multi-front human raid.");
            context.Assert(
                snapshot.IdentitySummary == "IRC_IdentityMultiFrontArrivalPreserved".Translate(),
                "The vanilla multi-front arrival and density preservation proof was not recorded.");
            context.Assert(
                snapshot.FinalCount >= System.Math.Ceiling(snapshot.OriginalCount * 0.90d),
                "The compressed multi-front raid fell below its ninety-percent pawn-density floor.");

            Map map = Find.CurrentMap;
            int representedSides = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.Faction == humanMultiFrontFaction && !pawn.Dead)
                .Select(pawn => NearestMapEdge(pawn.Position, map))
                .Distinct()
                .Count();
            context.Assert(
                representedSides >= expectedMultiFrontSides,
                "The original arrival worker did not leave enough distinct map-edge approaches after compression.");
        }

        private static int NearestMapEdge(IntVec3 position, Map map)
        {
            int left = position.x;
            int right = map.Size.x - 1 - position.x;
            int bottom = position.z;
            int top = map.Size.z - 1 - position.z;
            int minimum = System.Math.Min(System.Math.Min(left, right), System.Math.Min(bottom, top));
            if (minimum == left)
            {
                return 0;
            }
            if (minimum == right)
            {
                return 1;
            }
            return minimum == bottom ? 2 : 3;
        }

        [When("I stage a homogeneous mechanoid edge wave at {int} points")]
        public void StageAndReleaseHomogeneousMechanoidWave(PickleContext context, int points)
        {
            Map map = Find.CurrentMap;
            Faction faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Mechanoid);
            context.Assert(map != null, "No current map is loaded.");
            context.Assert(faction != null, "The mechanoid faction is not present.");

            PawnGroupMaker maker = faction.def.pawnGroupMakers.FirstOrDefault(candidate =>
                candidate.kindDef == PawnGroupKindDefOf.Combat
                && candidate.options.Count == 1
                && candidate.options[0].kind.defName == "Mech_Scyther");
            context.Assert(maker != null, "The vanilla scyther-only pawn group maker is unavailable.");

            RaidStrategyDef strategy = DefDatabase<RaidStrategyDef>.GetNamed("StageThenAttack");
            PawnGroupMakerParms groupParms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                points = points,
                faction = faction,
                raidStrategy = strategy,
                seed = context.ScenarioSeed + 1701
            };
            List<Pawn> pawns = maker.GeneratePawns(groupParms).ToList();
            int originalCount = pawns.Count;
            context.Assert(
                originalCount > CompressionMod.Settings.mechWaveSplitCountThreshold,
                "The scyther-only maker did not exceed the configured split threshold.");
            float generatedPower = MechWavePlanner.CombatPower(pawns);
            phasedMechUnsafeSplitFallsBackToVanilla =
                MechWavePlanner.Build(
                    pawns,
                    CompressionMod.Settings.mechanoidSoftPawnCap,
                    originalCount,
                    0f,
                    0f,
                    points) == null
                && MechWavePlanner.Build(
                    pawns,
                    CompressionMod.Settings.mechanoidSoftPawnCap,
                    1,
                    generatedPower * 0.75f,
                    0f,
                    points) == null;

            IncidentParms incidentParms = new IncidentParms
            {
                target = map,
                points = points,
                faction = faction,
                forced = true,
                sendLetter = false,
                raidStrategy = strategy,
                raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
                pawnGroupKind = PawnGroupKindDefOf.Combat
            };
            context.Assert(
                incidentParms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(incidentParms),
                "The vanilla edge-walk arrival could not resolve an entry anchor.");
            IntVec3 anchor = incidentParms.spawnCenter;

            incidentParms.raidArrivalMode.Worker.Arrive(pawns, incidentParms);
            strategy.Worker.MakeLords(incidentParms, pawns);
            int firstWaveCount = pawns.Count;
            MechRaidReinforcementComponent component = Current.Game.GetComponent<MechRaidReinforcementComponent>();
            phasedMechFirstWave = new List<Pawn>(pawns);
            phasedMechDeferredBeforeRelease = component.PendingPawnCount;
            phasedMechWaveSplitAndReleased =
                component.PendingPlanCount == 1
                && phasedMechDeferredBeforeRelease == originalCount - firstWaveCount;
            float dynamicMinimum = System.Math.Max(
                CompressionMod.Settings.mechWaveMinimumPoints,
                points * CompressionMod.Settings.mechWaveBudgetFraction);
            int naiveWaveCount = (int)System.Math.Ceiling(
                originalCount / (double)CompressionMod.Settings.mechanoidSoftPawnCap);
            phasedMechWavesMeetDynamicMinimum = component.AllPlannedWavesMeetMinimum
                && component.MinimumPlannedWavePoints + 0.01f >= dynamicMinimum
                && MechWavePlanner.CombatPower(pawns) + 0.01f >= dynamicMinimum
                && component.TotalPlannedWaveCount >= 2
                && component.TotalPlannedWaveCount <= 3;
            phasedMechUndersizedTailsWereMerged = component.TotalPlannedWaveCount >= 2
                && component.TotalPlannedWaveCount < naiveWaveCount;
            phasedMechWaveUsedEdgeAnchor = DistanceToMapEdge(anchor, map) <= 1
                && map.mapPawns.AllPawnsSpawned
                    .Where(pawn => pawn.Faction == faction && !pawn.Dead)
                    .All(pawn => DistanceToMapEdge(pawn.Position, map) <= 20);
        }

        [Then("every planned mechanoid wave meets the dynamic minimum and undersized tails are merged")]
        public void PlannedWavesMeetDynamicMinimum(PickleContext context)
        {
            context.Assert(
                phasedMechWavesMeetDynamicMinimum,
                "At least one planned mechanoid wave fell below the dynamic point minimum.");
            context.Assert(
                phasedMechUndersizedTailsWereMerged,
                "The planner did not merge undersized tail waves into qualified batches.");
            context.Assert(
                phasedMechUnsafeSplitFallsBackToVanilla,
                "The planner did not keep the vanilla whole force when the count threshold or minimum-wave rule made splitting unsafe.");
        }

        [When("I defeat the active first mechanoid wave")]
        public void DefeatActiveFirstMechanoidWave(PickleContext context)
        {
            context.Assert(phasedMechFirstWave != null, "No staged first wave was recorded.");
            phasedMechOriginalLord = phasedMechFirstWave
                .Select(LordUtility.GetLord)
                .FirstOrDefault(candidate => candidate != null);
            context.Assert(phasedMechOriginalLord != null, "The original mechanoid wave has no raid Lord.");
            List<Pawn> active = phasedMechFirstWave
                .Where(pawn => pawn.Spawned && !pawn.Dead)
                .ToList();
            context.Assert(active.Count >= 2, "The active wave is too small to leave a reinforcement anchor.");
            foreach (Pawn pawn in active.Skip(1))
            {
                pawn.Kill(null);
            }
        }

        [Then("the homogeneous mechanoid force is split into an active and deferred force")]
        public void HomogeneousMechanoidForceIsSplit(PickleContext context)
        {
            context.Assert(
                phasedMechWaveSplitAndReleased,
                "The homogeneous mechanoid force did not split into active and deferred forces.");
        }

        [Then("the next mechanoid wave releases automatically")]
        public void NextMechanoidWaveReleasesAutomatically(PickleContext context)
        {
            MechRaidReinforcementComponent component = Current.Game.GetComponent<MechRaidReinforcementComponent>();
            context.Assert(
                component.PendingPawnCount < phasedMechDeferredBeforeRelease,
                "The next mechanoid wave did not release after active combat power fell below the threshold.");
            CompressionSnapshot record = CompressionTelemetry.History.FirstOrDefault(
                snapshot => !string.IsNullOrWhiteSpace(snapshot.WaveSummary));
            context.Assert(record != null, "The reinforcement release was not recorded in compression history.");
            List<Pawn> reinforcements = Find.CurrentMap.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.Faction?.def == FactionDefOf.Mechanoid && !pawn.Dead)
                .ToList();
            context.Assert(reinforcements.Count > 0, "No live reinforcement pawns were found.");
            context.Assert(
                reinforcements.All(pawn => LordUtility.GetLord(pawn) == phasedMechOriginalLord),
                "A reinforcement did not inherit the original raid Lord and its current wait/attack state.");
        }

        [Then("the staged mechanoid waves use the resolved vanilla edge region")]
        public void StagedMechanoidWavesUseVanillaEdge(PickleContext context)
        {
            context.Assert(
                phasedMechWaveUsedEdgeAnchor,
                "A staged mechanoid wave appeared outside the resolved vanilla edge region.");
        }

        [Then("the next mechanoid wave uses safe vanilla drop pods near survivors")]
        public void NextWaveUsesSafeTacticalDropPods(PickleContext context)
        {
            Map map = Find.CurrentMap;
            MechRaidReinforcementComponent component = Current.Game.GetComponent<MechRaidReinforcementComponent>();
            IReadOnlyList<IntVec3> cells = component.LastTacticalDropCells;
            IntVec3 anchor = component.LastTacticalDropAnchor;
            context.Assert(component.LastReleaseUsedTacticalDrop, "The reinforcement fell back to edge arrival.");
            context.Assert(cells.Count > 0, "No tactical drop cells were recorded.");
            context.Assert(anchor.IsValid, "No surviving-attacker drop anchor was recorded.");

            List<Pawn> playerPawns = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.Faction == Faction.OfPlayer && !pawn.Dead)
                .ToList();
            List<Building> playerBuildings = map.listerBuildings.allBuildingsColonist;
            context.Assert(
                cells.All(cell => cell.InBounds(map)
                    && !cell.Roofed(map)
                    && !cell.Fogged(map)
                    && !map.areaManager.Home[cell]
                    && playerPawns.All(pawn => pawn.Position.DistanceToSquared(cell) >= 25 * 25)
                    && playerBuildings.All(building => building.Position.DistanceToSquared(cell) >= 18 * 18)),
                "A tactical reinforcement pod landed in an unsafe player or base exclusion zone.");
            context.Assert(
                cells.All(cell => cell.DistanceToSquared(anchor) >= 6 * 6
                    && cell.DistanceToSquared(anchor) <= 18 * 18),
                "A tactical reinforcement pod did not land near a surviving original attacker.");
        }


        [When("I compare mech cluster generation with and without compression at {int} points")]
        public void CompareMechClusterGeneration(PickleContext context, int points)
        {
            Map map = Find.CurrentMap;
            context.Assert(map != null, "No current map is loaded.");

            int seed = context.ScenarioSeed + 701;
            CompressionMod.Settings.enableMechClusters = false;
            MechClusterSketch baseline;
            Rand.PushState(seed);
            try
            {
                baseline = MechClusterGenerator.GenerateClusterSketch(points, map, true, false);
            }
            finally
            {
                Rand.PopState();
            }

            CompressionTelemetry.Reset();
            CompressionMod.Settings.enableMechClusters = true;
            MechClusterSketch compressed;
            Rand.PushState(seed);
            try
            {
                compressed = MechClusterGenerator.GenerateClusterSketch(points, map, true, false);
            }
            finally
            {
                Rand.PopState();
            }

            context.Assert(baseline != null, "Vanilla mech cluster generation returned null.");
            context.Assert(compressed != null, "Compressed mech cluster generation returned null.");
            mechClusterStructureMatched = baseline.startDormant == compressed.startDormant
                && BuildingSignature(baseline.buildingsSketch)
                    .SequenceEqual(BuildingSignature(compressed.buildingsSketch));
            HashSet<IntVec3> baselinePositions = new HashSet<IntVec3>(
                baseline.pawns.Select(mech => mech.position));
            mechClusterPositionsCameFromVanillaSketch = compressed.pawns
                .All(mech => baselinePositions.Contains(mech.position));
        }

        [Then("the mech cluster building sketch and activation state are unchanged")]
        public void MechClusterStructureIsUnchanged(PickleContext context)
        {
            context.Assert(
                mechClusterStructureMatched,
                "Compression changed the mech cluster building sketch or activation state.");
        }

        [Then("every compressed mech cluster defender uses a vanilla sketch position")]
        public void MechClusterDefenderPositionsAreVanilla(PickleContext context)
        {
            context.Assert(
                mechClusterPositionsCameFromVanillaSketch,
                "Compression introduced a defender position that was not in the vanilla sketch.");
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

        [Then("the last compression introduced at most one mechanoid boss")]
        public void AtMostOneMechanoidBossIntroduced(PickleContext context)
        {
            CompressionSnapshot snapshot = CompressionTelemetry.LastSuccessfulCompression;
            context.Assert(snapshot != null, "No successful raid compression was recorded.");
            context.Assert(
                snapshot.FinalBossCount >= snapshot.OriginalBossCount
                && snapshot.FinalBossCount <= snapshot.OriginalBossCount + 1,
                "Compression changed the boss count outside the permitted zero-or-one promotion: "
                + snapshot.OriginalBossCount + " -> " + snapshot.FinalBossCount + ".");
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

        private static IEnumerable<string> BuildingSignature(Sketch sketch)
        {
            if (sketch == null)
            {
                return Enumerable.Empty<string>();
            }

            return sketch.Entities
                .Select(entity => entity is SketchThing thing
                    ? "thing|" + thing.def.defName
                        + "|" + (thing.stuff?.defName ?? "none")
                        + "|" + thing.pos
                        + "|" + thing.rot.AsInt
                        + "|" + thing.stackCount
                        + "|" + thing.quality
                        + "|" + thing.hitPoints
                    : entity is SketchTerrain terrain
                        ? "terrain|" + terrain.def.defName
                            + "|" + (terrain.stuffForComparingSimilar?.defName ?? "none")
                            + "|" + terrain.treatSimilarAsSame
                            + "|" + terrain.pos
                    : entity.GetType().FullName + "|" + entity.pos)
                .OrderBy(value => value)
                .ToList();
        }

        private static int DistanceToMapEdge(IntVec3 cell, Map map)
        {
            return System.Math.Min(
                System.Math.Min(cell.x, map.Size.x - 1 - cell.x),
                System.Math.Min(cell.z, map.Size.z - 1 - cell.z));
        }
    }
}
