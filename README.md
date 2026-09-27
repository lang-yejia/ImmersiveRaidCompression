# Immersive Raid Compression

RimWorld 1.6 prototype for reducing oversized late-game raids without hidden combat stat buffs.

Version 0.3 handles hostile humanlike and mechanoid `Combat` raid groups plus oversized manhunter packs. It post-processes the vanilla PawnKind selection before Pawn generation and conservatively replaces low-cost enemies with higher-cost Core or official-DLC pawn kinds. Human groups use at most two-to-one merges; mechanoids may use three-to-one promotion because of their wider model-cost gaps. Human leaders, sappers, breachers, single-use rocket carriers, mechanoid breachers, and mech bosses are protected.

Every replacement targets 95–105% of the vanilla composition's actual PawnKind cost. If no legal higher-cost option exists, the threat remains unchanged. Manhunter packs prefer a stronger animal from the map's biomes, then allow a manhunter-eligible migrating species from Core or an official DLC. Their replacement count is based on the original pack's actual capped value—not the unspent incident budget—so compression cannot turn a count-capped pack into a stronger one. Insect, mech-cluster, and Anomaly handlers remain deferred until their event-specific rules are implemented.

The mod settings include a session-local **compression history** page. It records successful compressions and safe fallbacks, with threat source, counts, actual PawnKind cost, retained percentage, event budget, game tick, and full before/after composition. This audit history is deliberately not written to the save.

## Build

Set `RIMWORLD_DIR` to the RimWorld installation and `HARMONY_DIR` to the Harmony mod directory, then run:

```powershell
dotnet build .\Source\ImmersiveRaidCompression\ImmersiveRaidCompression.csproj -c Release
```

For local development, you can instead create an ignored `Directory.Build.props.user` in the repository root:

```xml
<Project>
  <PropertyGroup>
    <RimWorldDir>D:\Games\RimWorld</RimWorldDir>
    <HarmonyDir>D:\Games\RimWorld\Mods\Harmony</HarmonyDir>
    <PickleDir>D:\Tools\Rimworld-Pickle</PickleDir>
  </PropertyGroup>
</Project>
```

The optional Pickle runtime test assembly also needs `PICKLE_DIR`/`PickleDir`:

```powershell
dotnet build .\Source\ImmersiveRaidCompression.PickleSteps\ImmersiveRaidCompression.PickleSteps.csproj -c Release
```

## Current proof

The RimWorld 1.6 runtime suite currently covers three real incidents and opens the history page inside the live game:

- Human raid: 54 to 45 pawns, retaining 6,455 of 6,481 vanilla PawnKind points (99.6%).
- Mechanoid raid: 109 to 89 pawns, retaining 26,830 of 26,990 vanilla PawnKind points (99.4%) and introducing no mech bosses.
- Manhunter pack: 100 cougars to 36 mastodons, retaining 11,880 of 12,000 actual vanilla PawnKind points (99.0%).

All scenarios assert a real count reduction, 95–105% cost retention, detailed before/after telemetry, and no scenario errors. The human scenario also opens and renders the history window and verifies it remains present in the game's window stack.
