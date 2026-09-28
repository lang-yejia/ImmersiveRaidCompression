# Immersive Raid Compression

RimWorld 1.6 prototype for reducing oversized late-game raids without hidden combat stat buffs.

Version 0.5 handles hostile humanlike and mechanoid `Combat` raid groups, oversized manhunter packs, and mech cluster defenders. It post-processes vanilla PawnKind or cluster-sketch selection before Pawn generation and conservatively replaces low-cost enemies with higher-cost Core or official-DLC pawn kinds. Human groups use at most two-to-one merges; mechanoids may use three-to-one promotion because of their wider model-cost gaps. Human leaders, sappers, breachers, single-use rocket carriers, mechanoid breachers, and mech bosses are protected.

Before a large ordinary mechanoid raid is changed, a read-only classifier identifies scyther swarms, militor swarms, centipede formations, other role-dominant swarms, mixed forces, breach forces, and boss-led forces. Homogeneous forces are only marked as candidates for a future arrival-aware phased-reinforcement system; this version does not delay or respawn any pawn. Mixed forces continue to use same-role vanilla promotion, while breach and boss-led forces are marked as protected strategies. The classification, vanilla raid strategy, dominant role, and dominant percentage are shown in the audit history.

Every replacement targets 95–105% of the vanilla composition's actual PawnKind cost. It must also preserve the complete tactical-role set, protected units, and the proportions of major roles. Human roles distinguish long-range, heavy, explosive, area-denial, shield-melee, ordinary melee, and ordinary ranged units; xenotypes are kept in separate role groups. Mechanoid roles distinguish melee, long-range, heavy ranged, fire, beam-fire, shields, and specialists. If these invariants fail, the entire compression is discarded.

Manhunter replacements must match the original species' predator, herd, and pack behavior and stay within strict limits for combat-power jump, movement speed, body size, armor, and special abilities. Their count is based on the original pack's actual capped value—not unspent incident budget. A fast predator can therefore no longer become a slow animal tank merely because their point totals match.

Mech cluster compression runs only after `MechClusterGenerator` finishes the vanilla sketch and only replaces entries in its defender list. The building sketch, turrets, shields, problem causers, walls, dormancy/activation state, and all surviving or replacement defender positions remain vanilla. A deterministic A/B runtime test generates the same cluster with compression disabled and enabled and compares every building's definition, material, position, rotation, quality, and hit points. Insect and Anomaly handlers remain deferred until their event-specific rules are implemented.

The mod settings include a session-local **compression history** page. It records successful compressions and safe fallbacks, with threat source, counts, actual PawnKind cost, retained percentage, event budget, game tick, full before/after composition, and a tactical-identity proof. This audit history is deliberately not written to the save.

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

The RimWorld 1.6 runtime suite currently covers four threat families and opens the history page inside the live game:

- Human raid: 54 to 45 pawns, retaining 6,442 of 6,481 vanilla PawnKind points (99.4%) while preserving all tactical roles.
- Mechanoid raid: 57 to 55 pawns, retaining 14,915 of 14,975 vanilla PawnKind points (99.6%), preserving all tactical roles, and introducing no mech bosses.
- Manhunter pack: 100 cougars to 75 wargs, retaining 12,000 of 12,000 actual vanilla PawnKind points (100%) with compatible behavior, speed, size, armor, and abilities.
- Mech cluster A/B sketch: 21 to 18 defenders, retaining 3,615 of 3,670 points (98.5%) while all 74 buildings and the activation state remain field-for-field identical in the deterministic signature.
- Real mech-cluster incident: 21 to 20 defenders, retaining 4,285 of 4,315 points (99.3%) and spawning successfully.

All scenarios assert a real count reduction, 95–105% cost retention, tactical-identity proof, detailed before/after telemetry, and no scenario errors. A dedicated classifier scenario also proves that scyther swarms, mixed forces, breach forces, and boss-led forces receive distinct safe treatment decisions. The animal scenario additionally proves that its selected replacement is profile-compatible and explicitly rejects the incompatible mastodon tank. The human scenario opens and renders the history window and verifies it remains present in the game's window stack.
