# Immersive Raid Compression

RimWorld 1.6 prototype for reducing oversized late-game raids without hidden combat stat buffs.

Version 0.8 handles hostile humanlike and mechanoid `Combat` raid groups, oversized manhunter packs, and mech cluster defenders. It post-processes vanilla PawnKind or cluster-sketch selection before Pawn generation and conservatively replaces low-cost enemies with higher-cost Core or official-DLC pawn kinds. Human groups use at most two-to-one merges; mechanoids may use three-to-one promotion because of their wider model-cost gaps. Human leaders, sappers, breachers, single-use rocket carriers, existing mechanoid bosses, and mechanoid breachers are protected.

Before a large ordinary mechanoid raid is changed, a classifier identifies scyther swarms, militor swarms, centipede formations, other role-dominant swarms, mixed forces, breach forces, and boss-led forces. A 100% single-role force may use phased reinforcement only when the vanilla arrival mode is `EdgeWalkIn` or `EdgeDrop` and its generated pawn count exceeds the configurable split threshold. The planner creates no more than three waves, then requires every wave to reach the higher of the fixed point floor and a configurable percentage of the already-adjusted vanilla raid budget. If three waves cannot all qualify, it merges and rebalances them into two; if two qualified batches are impossible, the raid stays whole.

Later waves prefer native RimWorld drop pods clustered 6–18 cells from a surviving original attacker. Every exact pod cell is selected before spawning and must be standable, visible, unroofed, outside the home area, at least 25 cells from every live player pawn, and at least 18 cells from every player building. If the entire batch cannot satisfy those rules, no partial tactical drop occurs and the whole batch uses its original edge arrival instead. The shorter default cadence releases after three seconds once surviving active power falls to 65%, and pods open after 110 ticks. Reinforcements join the original raid Lord, so they attack immediately when the survivors are attacking and wait only when that same Lord is still waiting. Pawn references, exact batch boundaries, point proof, anchors, strategy, and timing are save-persistent.

Center and random drops, grouped or distributed arrivals, water emergence, breachers, and boss-led forces never enter the generic wave system. This avoids teleporting reinforcements behind defenses or erasing the identity of special raid strategies. Mixed forces continue to use same-role vanilla promotion. A boss can be introduced only for a mixed force, only from that exact vanilla group maker's legal weighted options, at no more than its relative vanilla weight (hard-capped at 10%), and at most once; existing boss-led raids remain protected. The audit history shows the classification, qualified wave count, first and released batch points, dynamic minimum, vanilla arrival mode, reserved edge anchor, and remaining queue.

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
- Mechanoid raid: mixed forces retain same-role promotion and can add at most one low-probability boss from the current vanilla weighted pool; a deterministic scyther-only force of 66 is rebalanced into three point-qualified waves of 22. The next wave uses native drop pods near a survivor, passes player/base exclusion checks, and joins the original Lord after active combat power falls below the threshold.
- Manhunter pack: 100 cougars to 75 wargs, retaining 12,000 of 12,000 actual vanilla PawnKind points (100%) with compatible behavior, speed, size, armor, and abilities.
- Mech cluster A/B sketch: 21 to 18 defenders, retaining 3,615 of 3,670 points (98.5%) while all 74 buildings and the activation state remain field-for-field identical in the deterministic signature.
- Real mech-cluster incident: 21 to 20 defenders, retaining 4,285 of 4,315 points (99.3%) and spawning successfully.

All scenarios assert a real count reduction, 95–105% cost retention, tactical-identity proof, detailed before/after telemetry, and no scenario errors. Dedicated scenarios prove that scyther swarms, mixed forces, breach forces, and boss-led forces receive distinct safe treatment decisions; only edge-walk and edge-drop pure-role swarms are wave-eligible; and a deferred wave releases automatically from the resolved vanilla edge region. The animal scenario additionally proves that its selected replacement is profile-compatible and explicitly rejects the incompatible mastodon tank. The human scenario opens and renders the history window and verifies it remains present in the game's window stack.
