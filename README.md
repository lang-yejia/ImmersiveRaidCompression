# Immersive Raid Compression

RimWorld 1.6 prototype for reducing oversized late-game raids without hidden combat stat buffs.

Version 0.2 handles hostile humanlike and mechanoid `Combat` raid groups. It post-processes the vanilla PawnKind selection before Pawn generation and conservatively merges low-cost pawns into a higher-cost vanilla pawn of the same broad combat role. Human groups use at most two-to-one merges; mechanoids may use three-to-one promotion because of their wider model-cost gaps. Human leaders, sappers, breachers, single-use rocket carriers, mechanoid breachers, and mech bosses are protected.

Every merge is stopped before the result can fall below 95% of the original vanilla PawnKind cost. If the active PawnGroupMaker has no suitable higher-cost option, the prototype leaves the remaining pawns unchanged. In particular, vanilla single-model mechanoid groups cannot be vertically promoted without crossing group boundaries and are deliberately left alone. Animal, insect, mech-cluster, and Anomaly handlers remain deferred until their event-specific rules are implemented.

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

The RimWorld 1.6 runtime suite currently covers two real incidents:

- Human raid: 54 to 45 pawns, retaining 6,455 of 6,481 vanilla PawnKind points (99.6%).
- Mechanoid raid: 109 to 89 pawns, retaining 26,830 of 26,990 vanilla PawnKind points (99.4%) and introducing no mech bosses.

Both scenarios assert a real count reduction, at least 95% point retention, and no scenario errors.
