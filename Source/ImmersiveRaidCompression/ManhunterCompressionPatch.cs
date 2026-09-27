using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ImmersiveRaidCompression
{
    [HarmonyPatch(typeof(IncidentWorker_AggressiveAnimals), "TryExecuteWorker")]
    public static class AggressiveAnimalIncidentContextPatch
    {
        public static int ActiveDepth { get; private set; }
        internal static PawnKindDef ReplacementKind { get; private set; }
        internal static int ReplacementCount { get; private set; }

        public static void Prefix()
        {
            if (ActiveDepth == 0)
            {
                ReplacementKind = null;
                ReplacementCount = 0;
            }
            ActiveDepth++;
            if (CompressionMod.Settings?.verboseLogging == true)
            {
                Log.Message("[Immersive Raid Compression] entered aggressive-animal incident generation.");
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            ActiveDepth = Math.Max(0, ActiveDepth - 1);
            if (ActiveDepth == 0)
            {
                ReplacementKind = null;
                ReplacementCount = 0;
            }

            return __exception;
        }

        internal static void RegisterCountOverride(PawnKindDef kind, int count)
        {
            ReplacementKind = kind;
            ReplacementCount = count;
        }
    }

    [HarmonyPatch(typeof(AggressiveAnimalIncidentUtility), nameof(AggressiveAnimalIncidentUtility.GetAnimalsCount))]
    public static class ManhunterAnimalCountPatch
    {
        public static void Postfix(PawnKindDef animalKind, ref int __result)
        {
            if (AggressiveAnimalIncidentContextPatch.ActiveDepth > 0
                && animalKind == AggressiveAnimalIncidentContextPatch.ReplacementKind
                && AggressiveAnimalIncidentContextPatch.ReplacementCount > 0)
            {
                __result = AggressiveAnimalIncidentContextPatch.ReplacementCount;
            }
        }
    }

    [HarmonyPatch]
    public static class ManhunterAnimalSelectionPatch
    {
        private static readonly MethodInfo CanArriveManhunterMethod = AccessTools.Method(
            typeof(AggressiveAnimalIncidentUtility),
            "CanArriveManhunter");
        private static readonly MethodInfo CanArriveWithPollutionMethod = AccessTools.Method(
            typeof(AggressiveAnimalIncidentUtility),
            "CanArriveWithPollution");

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(AggressiveAnimalIncidentUtility),
                nameof(AggressiveAnimalIncidentUtility.TryFindAggressiveAnimalKind),
                new[] { typeof(float), typeof(Map), typeof(PawnKindDef).MakeByRefType() });
        }

        public static void Postfix(float points, Map map, ref PawnKindDef animalKind, bool __result)
        {
            TryCompressSelection(points, map, ref animalKind, __result);
        }

        internal static void TryCompressSelection(
            float points,
            Map map,
            ref PawnKindDef animalKind,
            bool selectionSucceeded)
        {
            CompressionSettings settings = CompressionMod.Settings;
            if (settings?.verboseLogging == true)
            {
                Log.Message(
                    "[Immersive Raid Compression] evaluated aggressive-animal selection: success="
                    + selectionSucceeded + ", context=" + AggressiveAnimalIncidentContextPatch.ActiveDepth
                    + ", points=" + points.ToString("F0") + ", kind=" + (animalKind?.defName ?? "null")
                    + ", map=" + (map == null ? "null" : map.ToString())
                    + ", enabled=" + settings.enableManhunterPacks
                    + ", minimum=" + settings.minimumManhunterPoints.ToString("F0") + ".");
            }
            if (!selectionSucceeded
                || AggressiveAnimalIncidentContextPatch.ActiveDepth <= 0
                || settings == null
                || !settings.enableManhunterPacks
                || points < settings.minimumManhunterPoints
                || map == null
                || animalKind == null)
            {
                return;
            }

            int originalCount = AggressiveAnimalIncidentUtility.GetAnimalsCount(animalKind, points);
            if (settings.verboseLogging)
            {
                Log.Message(
                    "[Immersive Raid Compression] manhunter eligibility: count=" + originalCount
                    + ", cap=" + settings.manhunterSoftPawnCap
                    + ", enabled=" + settings.enableManhunterPacks
                    + ", minimumPoints=" + settings.minimumManhunterPoints.ToString("F0") + ".");
            }
            if (originalCount <= settings.manhunterSoftPawnCap)
            {
                return;
            }

            PawnKindDef originalKind = animalKind;
            // The vanilla eligibility method reads the map as well; passing the local
            // polluted-area hint is enough and avoids querying an incompletely restored
            // pollution grid while a fixture is still entering play.
            bool polluted = false;
            try
            {
                polluted = map.pollutionGrid != null
                    && map.pollutionGrid.TotalPollutionPercent > 0.001f;
            }
            catch (Exception exception)
            {
                if (settings.verboseLogging)
                {
                    Log.Warning(
                        "[Immersive Raid Compression] could not read map pollution; "
                        + "using the unpolluted animal rules: " + exception.GetBaseException().Message);
                }
            }

            List<AnimalCandidate> candidates = BuildCandidates(
                    map,
                    originalKind,
                    originalCount,
                    polluted)
                .OrderByDescending(candidate => candidate.IsLocal)
                .ThenBy(candidate => Math.Abs(candidate.Count - settings.manhunterSoftPawnCap))
                .ThenByDescending(candidate => candidate.Count <= settings.manhunterSoftPawnCap)
                .ThenBy(candidate => candidate.Kind.combatPower)
                .ToList();

            if (settings.verboseLogging)
            {
                Log.Message(
                    "[Immersive Raid Compression] found " + candidates.Count
                    + " stronger legal vanilla animal candidate(s).");
            }

            AnimalCandidate replacement = candidates.FirstOrDefault();
            string originalComposition = animalKind.LabelCap + " ×" + originalCount;
            float originalCost = originalCount * animalKind.combatPower;
            if (replacement == null)
            {
                CompressionTelemetry.RecordSkipped(
                    "manhunter pack",
                    map.Parent?.LabelCap ?? map.ToString(),
                    originalCount,
                    originalCost,
                    points,
                    originalComposition,
                    "IRC_ReasonNoAnimal".Translate());
                return;
            }

            animalKind = replacement.Kind;
            AggressiveAnimalIncidentContextPatch.RegisterCountOverride(
                replacement.Kind,
                replacement.Count);
            float finalCost = replacement.Count * replacement.Kind.combatPower;
            CompressionTelemetry.Record(
                "manhunter pack",
                map.Parent?.LabelCap ?? map.ToString(),
                originalCount,
                replacement.Count,
                originalCost,
                finalCost,
                points,
                0,
                0,
                originalComposition,
                replacement.Kind.LabelCap + " ×" + replacement.Count,
                ThreatIdentity.AnimalIdentitySummary(originalKind, replacement.Kind),
                originalKind.defName,
                replacement.Kind.defName);

            Log.Message(
                "[Immersive Raid Compression] manhunter pack: "
                + originalKind.defName + " x" + originalCount + " -> "
                + replacement.Kind.defName + " x" + replacement.Count
                + ", budget " + points.ToString("F0") + ".");
        }

        private static bool CanArriveManhunter(PawnKindDef kind)
        {
            return CanArriveManhunterMethod != null
                && (bool)CanArriveManhunterMethod.Invoke(null, new object[] { kind });
        }

        private static bool CanArriveWithPollution(PawnKindDef kind, Map map, bool polluted)
        {
            return CanArriveWithPollutionMethod != null
                && (bool)CanArriveWithPollutionMethod.Invoke(null, new object[] { kind, map, polluted });
        }

        private static IReadOnlyList<AnimalCandidate> BuildCandidates(
            Map map,
            PawnKindDef originalKind,
            int originalCount,
            bool polluted)
        {
            List<AnimalCandidate> candidates = new List<AnimalCandidate>();
            HashSet<PawnKindDef> localAnimalKinds = new HashSet<PawnKindDef>();
            IEnumerable<PawnKindDef> biomeAnimals;
            try
            {
                List<PawnKindDef> localAnimals = map.Biomes
                    .Where(biome => biome != null)
                    .SelectMany(biome => biome.AllWildAnimals ?? Enumerable.Empty<PawnKindDef>())
                    .Where(kind => kind != null)
                    .Distinct()
                    .ToList();
                localAnimalKinds = new HashSet<PawnKindDef>(localAnimals);
                biomeAnimals = localAnimals
                    .Concat(DefDatabase<PawnKindDef>.AllDefsListForReading.Where(kind =>
                        kind != null
                        && !localAnimalKinds.Contains(kind)
                        && kind.RaceProps?.Animal == true
                        && kind.modContentPack != null
                        && (kind.modContentPack.IsCoreMod || kind.modContentPack.IsOfficialMod)))
                    .Distinct()
                    .ToList();
            }
            catch (Exception exception)
            {
                Log.Error("[Immersive Raid Compression] could not enumerate biome animals: " + exception);
                return candidates;
            }

            if (CompressionMod.Settings?.verboseLogging == true)
            {
                List<PawnKindDef> loggedAnimals = biomeAnimals.ToList();
                Log.Message(
                    "[Immersive Raid Compression] vanilla animal pool has " + loggedAnimals.Count
                    + " animal kind(s); original combatPower=" + originalKind.combatPower.ToString("F0")
                    + ", strongest=" + (loggedAnimals.Count == 0
                        ? "none"
                        : loggedAnimals.OrderByDescending(kind => kind.combatPower).First().defName
                            + "/" + loggedAnimals.Max(kind => kind.combatPower).ToString("F0")) + ".");
            }

            foreach (PawnKindDef kind in biomeAnimals)
            {
                if (kind == originalKind)
                {
                    continue;
                }

                try
                {
                    if (!ThreatIdentity.AnimalKindsAreCompatible(
                            originalKind,
                            kind,
                            out string identityReason))
                    {
                        if (CompressionMod.Settings?.verboseLogging == true
                            && kind.combatPower > originalKind.combatPower)
                        {
                            Log.Message(
                                "[Immersive Raid Compression] rejected animal candidate "
                                + kind.defName + " to preserve threat identity ("
                                + identityReason + ").");
                        }
                        continue;
                    }

                    bool canArriveManhunter = CanArriveManhunter(kind);
                    bool pollutionEligible = !polluted || CanArriveWithPollution(kind, map, true);
                    if (!canArriveManhunter || !pollutionEligible)
                    {
                        continue;
                    }

                    int count = Math.Max(
                        1,
                        Mathf.RoundToInt(originalCount * originalKind.combatPower / kind.combatPower));
                    if (count < originalCount)
                    {
                        candidates.Add(new AnimalCandidate(
                            kind,
                            count,
                            localAnimalKinds.Contains(kind)));
                    }
                }
                catch (Exception exception)
                {
                    if (CompressionMod.Settings?.verboseLogging == true)
                    {
                        Log.Warning(
                            "[Immersive Raid Compression] rejected animal candidate " + kind.defName
                            + " after an eligibility check failed: " + exception.GetBaseException().Message);
                    }
                }
            }

            return candidates;
        }

        private sealed class AnimalCandidate
        {
            public PawnKindDef Kind { get; }
            public int Count { get; }
            public bool IsLocal { get; }

            public AnimalCandidate(PawnKindDef kind, int count, bool isLocal)
            {
                Kind = kind;
                Count = count;
                IsLocal = isLocal;
            }
        }
    }

    [HarmonyPatch]
    public static class ManhunterAnimalTileSelectionPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(AggressiveAnimalIncidentUtility),
                nameof(AggressiveAnimalIncidentUtility.TryFindAggressiveAnimalKind),
                new[] { typeof(float), typeof(PlanetTile), typeof(PawnKindDef).MakeByRefType() });
        }

        public static void Postfix(float points, PlanetTile tile, ref PawnKindDef animalKind, bool __result)
        {
            Map map = Find.CurrentMap;
            if (map == null || map.Tile != tile)
            {
                return;
            }

            ManhunterAnimalSelectionPatch.TryCompressSelection(
                points,
                map,
                ref animalKind,
                __result);
        }
    }
}
