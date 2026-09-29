using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ImmersiveRaidCompression
{
    [HarmonyPatch]
    public static class HumanRaidArrivalContextPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetTypesFromAssembly(typeof(PawnsArrivalModeWorker).Assembly)
                .Where(type => typeof(PawnsArrivalModeWorker).IsAssignableFrom(type))
                .Select(type => AccessTools.DeclaredMethod(
                    type,
                    nameof(PawnsArrivalModeWorker.TryResolveRaidSpawnCenter)))
                .Where(method => method != null && !method.IsAbstract)
                .Distinct();
        }

        public static void Postfix(IncidentParms parms, bool __result)
        {
            if (__result && parms?.faction?.def.humanlikeFaction == true)
            {
                HumanRaidArrivalContext.Capture(parms);
            }
        }
    }

    public static class HumanRaidArrivalContext
    {
        [ThreadStatic] private static Faction faction;
        [ThreadStatic] private static RaidStrategyDef strategy;
        [ThreadStatic] private static PawnsArrivalModeDef arrivalMode;

        public static void Capture(IncidentParms parms)
        {
            faction = parms.faction;
            strategy = parms.raidStrategy;
            arrivalMode = parms.raidArrivalMode;
        }

        public static PawnsArrivalModeDef Take(PawnGroupMakerParms parms)
        {
            PawnsArrivalModeDef result = parms != null
                && parms.faction == faction
                && parms.raidStrategy == strategy
                    ? arrivalMode
                    : null;
            faction = null;
            strategy = null;
            arrivalMode = null;
            return result;
        }
    }

    public enum HumanRaidArchetype
    {
        DirectAssault,
        Siege,
        Sapper,
        Breach,
        CenterDrop,
        RandomDrop,
        MultiDirection,
        SpecialArrival
    }

    public enum HumanRaidTreatment
    {
        VanillaPromotion,
        SiegeVanillaPromotion,
        SapperEscortPromotion,
        BreachEscortPromotion,
        DropAssaultPromotion,
        ProtectedUntilDedicatedHandler
    }

    public sealed class HumanRaidClassification
    {
        public HumanRaidArchetype Archetype { get; }
        public HumanRaidTreatment Treatment { get; }
        public string StrategyDefName { get; }
        public string ArrivalDefName { get; }

        public string Summary => "IRC_HumanClassificationSummary".Translate(
            ("IRC_HumanArchetype_" + Archetype).Translate(),
            ("IRC_HumanTreatment_" + Treatment).Translate(),
            StrategyDefName,
            ArrivalDefName);

        public HumanRaidClassification(
            HumanRaidArchetype archetype,
            HumanRaidTreatment treatment,
            string strategyDefName,
            string arrivalDefName)
        {
            Archetype = archetype;
            Treatment = treatment;
            StrategyDefName = strategyDefName ?? "-";
            ArrivalDefName = arrivalDefName ?? "-";
        }
    }

    public static class HumanRaidClassifier
    {
        public static HumanRaidClassification Analyze(
            PawnGroupMakerParms parms,
            IEnumerable<PawnGenOptionWithXenotype> composition,
            PawnsArrivalModeDef arrivalMode)
        {
            return Analyze(
                composition?.Select(option => option.Option.kind),
                parms?.raidStrategy,
                arrivalMode);
        }

        public static HumanRaidClassification Analyze(
            IEnumerable<PawnKindDef> composition,
            RaidStrategyDef strategy,
            PawnsArrivalModeDef arrivalMode)
        {
            string strategyName = strategy?.defName ?? "-";
            string arrivalName = arrivalMode?.defName ?? "-";

            // A normal assault may contain an individually breach-capable pawn. The existing
            // human compression policy protects that pawn; it does not turn the whole raid
            // into a breach raid. Tactical family is defined by the raid strategy itself.
            if (Contains(strategyName, "Breach"))
            {
                return SpecialistRaid(
                    HumanRaidArchetype.Breach,
                    HumanRaidTreatment.BreachEscortPromotion,
                    arrivalMode,
                    strategyName,
                    arrivalName);
            }
            if (Contains(strategyName, "Sapper"))
            {
                return SpecialistRaid(
                    HumanRaidArchetype.Sapper,
                    HumanRaidTreatment.SapperEscortPromotion,
                    arrivalMode,
                    strategyName,
                    arrivalName);
            }
            if (Contains(strategyName, "Siege"))
            {
                return new HumanRaidClassification(
                    HumanRaidArchetype.Siege,
                    arrivalMode == PawnsArrivalModeDefOf.EdgeWalkIn
                        ? HumanRaidTreatment.SiegeVanillaPromotion
                        : HumanRaidTreatment.ProtectedUntilDedicatedHandler,
                    strategyName,
                    arrivalName);
            }
            if (Contains(arrivalName, "Groups") || Contains(arrivalName, "Distributed"))
            {
                return Protected(HumanRaidArchetype.MultiDirection, strategyName, arrivalName);
            }
            if (arrivalMode == PawnsArrivalModeDefOf.CenterDrop)
            {
                return new HumanRaidClassification(
                    HumanRaidArchetype.CenterDrop,
                    HumanRaidTreatment.DropAssaultPromotion,
                    strategyName,
                    arrivalName);
            }
            if (arrivalMode == PawnsArrivalModeDefOf.RandomDrop)
            {
                return new HumanRaidClassification(
                    HumanRaidArchetype.RandomDrop,
                    HumanRaidTreatment.DropAssaultPromotion,
                    strategyName,
                    arrivalName);
            }
            if (arrivalMode != PawnsArrivalModeDefOf.EdgeWalkIn
                && arrivalMode != PawnsArrivalModeDefOf.EdgeDrop)
            {
                return Protected(HumanRaidArchetype.SpecialArrival, strategyName, arrivalName);
            }

            return new HumanRaidClassification(
                HumanRaidArchetype.DirectAssault,
                HumanRaidTreatment.VanillaPromotion,
                strategyName,
                arrivalName);
        }

        private static HumanRaidClassification Protected(
            HumanRaidArchetype archetype,
            string strategyName,
            string arrivalName)
        {
            return new HumanRaidClassification(
                archetype,
                HumanRaidTreatment.ProtectedUntilDedicatedHandler,
                strategyName,
                arrivalName);
        }

        private static HumanRaidClassification SpecialistRaid(
            HumanRaidArchetype archetype,
            HumanRaidTreatment treatment,
            PawnsArrivalModeDef arrivalMode,
            string strategyName,
            string arrivalName)
        {
            return new HumanRaidClassification(
                archetype,
                arrivalMode == PawnsArrivalModeDefOf.EdgeWalkIn
                    ? treatment
                    : HumanRaidTreatment.ProtectedUntilDedicatedHandler,
                strategyName,
                arrivalName);
        }

        private static bool Contains(string value, string fragment)
        {
            return value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
