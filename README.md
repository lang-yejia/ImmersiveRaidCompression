# Immersive Raid Compression

RimWorld 1.6 prototype for reducing oversized late-game raids without hidden combat stat buffs.

Version 0.18 handles hostile humanlike and mechanoid `Combat` raid groups, oversized manhunter packs, mech cluster defenders, ordinary infestations, storyteller-triggered Fleshbeast Attacks, and high-count shambler threats. It uses stronger vanilla units or two-to-three qualified reinforcement waves without adding hidden stat modifiers. Protected specialists and special encounter structures remain exact; shamblers are never promoted into a tactically different roster.

## Implementation status

Implemented and runtime-covered:

- Ordinary human `ImmediateAttack` raids arriving by `EdgeWalkIn` or `EdgeDrop`: classified, then passed to the existing same-role vanilla PawnKind promotion system. Tactical roles, xenotype groups, protected equipment carriers, and 95–105% actual kind cost are preserved.
- Oversized ordinary pirate edge assaults: after vanilla-roster promotion, the remaining force may become two or three role-balanced waves. Every wave must clear a dynamic point floor; weak tails are merged. Later waves use validated vanilla drop pods within 30 cells of a surviving attacker or fall back as one complete batch to the original edge arrival, then join the original raid Lord and inherit its live state.
- Ordinary tribal raids: potential sapper-capable warrior kinds remain eligible outside actual sapper and breach strategies. Every promotion comes only from the active tribal faction's vanilla group maker, so technology level, xenotype, combat role, leaders, true breachers, and single-use weapons remain intact. Specialist raids continue to use the stricter protection policy.
- Human `Siege` raids arriving by `EdgeWalkIn`: only ordinary combat PawnKind selections are compressed. The resolved siege strategy, vanilla `LordJob_Siege`, builder assignment, mortar blueprints, construction supplies, protected specialists, preparation phase, and transition to direct assault remain controlled by vanilla code rather than being replaced or reimplemented.
- Human sapper and breach raids arriving by `EdgeWalkIn`: every original sapper, breacher, leader, and single-use heavy-weapon carrier is retained exactly. Only ordinary escorts are promoted, at least 75% of the original escort count remains, and the vanilla raid Lord and path-opening AI remain in control.
- Human `CenterDrop` and `RandomDrop` raids: the already-resolved vanilla arrival worker and landing logic remain unchanged. Protected units remain exact and at least 80% of the original pawn count is retained, preserving immediate landing density while still reducing late-game pod and pawn load.
- Human grouped and distributed arrivals, including `EdgeWalkInGroups`, `EdgeDropGroups`, and `EdgeWalkInDistributed`: the resolved vanilla worker still chooses the approach directions and positions. Protected units remain exact and at least 90% of the original pawn count is retained. Runtime tests additionally require multiple distinct map-edge approaches to remain represented.
- Mixed mechanoid raids: same-role vanilla promotion, with an optional single boss drawn only from that group's legal weighted vanilla pool and bounded by its vanilla-relative chance.
- Homogeneous pure-role mechanoid edge raids: two or three dynamically point-qualified waves, weak-tail merging, safe vanilla tactical drop-pod reinforcement, original-Lord membership, and live wait/attack-state inheritance.
- Mech clusters: defender-only compression; structures, positions, materials, rotation, quality, hit points, and activation state remain untouched.
- Manhunter packs: replacement only by a stronger vanilla species with compatible predator/herd/pack behavior, mobility, body size, armor, and special abilities.
- Ordinary `Infestation`: all vanilla tunnel and hive objects remain. The mod evaluates the exact vanilla insect-selection rule for each tunnel, compresses the event-wide initial insect composition to 95–105% combat power, retains at least one of every selected insect kind, and distributes the stronger vanilla insects across the infestation area. Deep-drill, wastepack, quest, and scripted infestations are outside this handler.
- Storyteller `FleshbeastAttack`: the incident-level point threshold is checked before any change, then each vanilla burrow group's repeated fingerspikes, toughspikes, and trispikes may be consolidated within their shared spike-ranged family. Every kind selected by vanilla remains represented; Bulbfreaks, bosses, unknown specials, burrows, emergence timing, positions, Lords, and assault behavior remain exact. Player rituals, pit-gate events, sites, quests, and scripted spawns never enter this handler. All affected burrow groups are aggregated into one audit record for the incident.
- Large storyteller `ShamblerSwarm`: the exact generated shamblers are balanced by PawnKind and combat power across two or three substantial waves. The first wave uses the original spawn loop; later waves reuse the same vanilla edge area, lifespan, quest tag, and original `LordJob_EntitySwarm`.
- Large storyteller `ShamblerSwarmAnimals`: the same phased policy applies without changing the selected animal species or rare chimera rolls. Every generated pawn remains exact.
- Large storyteller `ShamblerAssault`: each wave calls the original `EdgeWalkInDistributedGroups` worker, preserving multi-edge pressure. Deferred pawns receive the same vanilla shambler lifespan processing and join the first wave's original assault Lord immediately after arrival.
- Audit history: successful compression and protected/skipped decisions include threat classification and before/after evidence.

Classified and deliberately protected, but not compressed yet:

- Unknown special human arrival modes outside the explicitly supported edge, drop, grouped, and distributed families.
- Human factions whose active vanilla group maker has no legal higher-value same-role PawnKind. These remain wholly vanilla instead of receiving a buff or tactically invalid substitution.

Raid-family classification follows the resolved vanilla raid strategy and arrival mode. An ordinary direct assault does not become a sapper or breach raid merely because a PawnKind is marked as potentially sapper-capable. That kind may be promoted normally in an ordinary assault; the stricter policy is selected only for a resolved sapper or breach strategy, while true `isGoodBreacher` kinds remain protected everywhere.

Pirate phasing is intentionally narrower than pirate compression. It applies only to vanilla pirate factions whose resolved strategy is an ordinary direct assault and whose arrival is `EdgeWalkIn` or `EdgeDrop`. Siege, sapper, breach, center-drop, random-drop, grouped, distributed, and unknown special arrivals never enter the pirate wave controller. Common melee, ranged, heavy, explosive, area-denial, shield, and xenotype role groups are spread across qualified waves instead of being concentrated in one batch.

The currently agreed performance scope is implemented. Small `SmallShamblerSwarm` incidents, `GhoulAttack`, player-summoned bosses, already-small special threats, rituals, quests, and scripted encounters remain deliberately outside it.

Deliberately outside the current performance scope are player-summoned mech bosses, already-small special threats, and quest/script-authored raids or infestations. Their authored composition and timing stay vanilla.

Before a large ordinary mechanoid raid is changed, a classifier identifies scyther swarms, militor swarms, centipede formations, other role-dominant swarms, mixed forces, breach forces, and boss-led forces. A 100% single-role force may use phased reinforcement only when the vanilla arrival mode is `EdgeWalkIn` or `EdgeDrop` and its generated pawn count exceeds the configurable split threshold. The planner creates no more than three waves, then requires every wave to reach the higher of the fixed point floor and a configurable percentage of the already-adjusted vanilla raid budget. If three waves cannot all qualify, it merges and rebalances them into two; if two qualified batches are impossible, the raid stays whole.

Later waves prefer native RimWorld drop pods clustered 6–18 cells from a surviving original attacker. Every exact pod cell is selected before spawning and must be standable, visible, unroofed, outside the home area, at least 25 cells from every live player pawn, and at least 18 cells from every player building. If the entire batch cannot satisfy those rules, no partial tactical drop occurs and the whole batch uses its original edge arrival instead. The shorter default cadence releases after three seconds once surviving active power falls to 65%, and pods open after 110 ticks. Reinforcements join the original raid Lord, so they attack immediately when the survivors are attacking and wait only when that same Lord is still waiting. Pawn references, exact batch boundaries, point proof, anchors, strategy, and timing are save-persistent.

Center and random drops, grouped or distributed arrivals, water emergence, breachers, and boss-led forces never enter the generic wave system. This avoids teleporting reinforcements behind defenses or erasing the identity of special raid strategies. Mixed forces continue to use same-role vanilla promotion. A boss can be introduced only for a mixed force, only from that exact vanilla group maker's legal weighted options, at no more than its relative vanilla weight (hard-capped at 10%), and at most once; existing boss-led raids remain protected. The audit history shows the classification, qualified wave count, first and released batch points, dynamic minimum, vanilla arrival mode, reserved edge anchor, and remaining queue.

Every replacement targets 95–105% of the vanilla composition's actual PawnKind cost. It must also preserve the complete tactical-role set, protected units, and the proportions of major roles. Human roles distinguish long-range, heavy, explosive, area-denial, shield-melee, ordinary melee, and ordinary ranged units; xenotypes are kept in separate role groups. Mechanoid roles distinguish melee, long-range, heavy ranged, fire, beam-fire, shields, and specialists. If these invariants fail, the entire compression is discarded.

Manhunter replacements must match the original species' predator, herd, and pack behavior and stay within strict limits for combat-power jump, movement speed, body size, armor, and special abilities. Their count is based on the original pack's actual capped value—not unspent incident budget. A fast predator can therefore no longer become a slow animal tank merely because their point totals match.

Mech cluster compression runs only after `MechClusterGenerator` finishes the vanilla sketch and only replaces entries in its defender list. The building sketch, turrets, shields, problem causers, walls, dormancy/activation state, and all surviving or replacement defender positions remain vanilla. A deterministic A/B runtime test generates the same cluster with compression disabled and enabled and compares every building's definition, material, position, rotation, quality, and hit points.

Ordinary infestation compression plans the whole event rather than treating each tunnel independently. This matters because a standard high-point infestation can create many tunnels that each carry only one low-value initial insect. The planner preserves all tunnels and hives, spreads the reduced force over the original infestation footprint, keeps every vanilla-selected insect caste, and leaves later hive reproduction to vanilla. Only the initial combat wave is compressed.

Fleshbeast Attack compression respects the inverse structure: vanilla splits one incident budget into multiple roughly 500-point burrow groups, so the configurable soft cap applies per burrow group while the minimum point threshold applies to the complete incident. Compression is attempted only inside the three ordinary spike-ranged kinds. A failed group-level identity or 95–105% cost check falls back to that complete vanilla group; successful groups are combined into one event-level history entry so the audit page describes the actual incident rather than flooding it with dozens of small records.

Shambler phasing does not change total count or total combat power; it lowers simultaneous pawn load. It activates only above both the Anomaly incident-point threshold and a configurable pawn-count threshold. The planner requests no more than three waves, requires each wave to reach the higher of a fixed point floor and a percentage of incident points, and replans from three waves to two when a tail is too weak. If two qualified waves are impossible, the complete event stays vanilla. The next wave is released after the short minimum spacing once active, non-downed combat power falls below the configured fraction of first-wave power. Reinforcements join the existing Lord synchronously, so an attacking group receives attacking reinforcements rather than a separate waiting encounter.

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

- Human direct edge raid: 258 to 210 pawns, retaining 29,059 of 29,992 vanilla PawnKind points (96.9%) while preserving all tactical roles and individually protected units.
- Human siege: 167 to 138 pawns, retaining 19,265 of 19,487 vanilla PawnKind points (98.9%), with the live raid still controlled by RimWorld's original `LordJob_Siege`.
- Human sapper raid: 154 to 128 pawns, retaining 17,861 of 17,988 vanilla PawnKind points (99.3%); all protected specialists remain and the escort floor is enforced.
- Human breach raid: 130 to 109 pawns, retaining 14,965 of 14,968 vanilla PawnKind points (100.0%); all protected specialists remain and the escort floor is enforced.
- Human center-drop raid: 77 to 65 pawns, retaining 8,995 of 8,998 vanilla PawnKind points (100.0%) while retaining more than 80% of the landing force.
- Human random-drop raid: 115 to 96 pawns, retaining 13,490 of 13,490 vanilla PawnKind points (100.0%) while retaining more than 80% of the landing force.
- Human grouped edge raid: 232 to 209 pawns, retaining 26,984 of 26,986 vanilla PawnKind points (100.0%) while keeping multiple edge approaches.
- Human all-around distributed raid: 258 to 233 pawns, retaining 29,987 of 29,992 vanilla PawnKind points (100.0%) while keeping at least three represented map edges.
- Tribal direct raid: 355 to 299 pawns, retaining 29,976 of 29,984 vanilla PawnKind points (100.0%), using only higher-tier PawnKinds from the active vanilla tribal group maker.
- Pirate direct assault: 258 to 210 pawns by vanilla promotion, then three role-balanced waves of 70. The first wave carried 9,085 points against a 4,500-point dynamic minimum; the next 70-pawn wave arrived in vanilla drop pods near a surviving attacker and joined the original Lord.
- Mechanoid raid: mixed forces retain same-role promotion and can add at most one low-probability boss from the current vanilla weighted pool; a deterministic scyther-only force of 66 is rebalanced into three point-qualified waves of 22. The next wave uses native drop pods near a survivor, passes player/base exclusion checks, and joins the original Lord after active combat power falls below the threshold.
- Manhunter pack: 100 cougars to 75 wargs, retaining 12,000 of 12,000 actual vanilla PawnKind points (100%) with compatible behavior, speed, size, armor, and abilities.
- Mech cluster A/B sketch: 21 to 18 defenders, retaining 3,615 of 3,670 points (98.5%) while all 74 buildings and the activation state remain field-for-field identical in the deterministic signature.
- Real mech-cluster incident: 21 to 20 defenders, retaining 4,285 of 4,315 points (99.3%) and spawning successfully.
- Ordinary infestation: 62 vanilla tunnels and 62 hives remain unchanged while the initial force falls from 62 insects to 17 stronger vanilla insects; the targeted runtime scenario verifies 95–105% combat-power retention and complete before/after telemetry.
- Storyteller Fleshbeast Attack: in the targeted 30,000-point incident, the affected vanilla burrow groups fell from 76 to 60 ordinary spike-ranged fleshbeasts while retaining 3,895 of 3,935 PawnKind points (99.0%). The scenario also verifies that vanilla pit-burrow spawners are still created, every selected kind remains represented, and the audit page receives one aggregated event record.

The established 17-scenario suite covers the earlier threat families. The ordinary-infestation scenario passed separately after 1,800 simulated ticks, including exact tunnel/hive preservation. The targeted Fleshbeast Attack scenario also passed independently in RimWorld 1.6, including count reduction, 95–105% cost retention, kind-set identity proof, detailed aggregated telemetry, and vanilla pit-burrow creation. Dedicated scenarios prove that scyther swarms, mixed forces, breach forces, and boss-led forces receive distinct safe treatment decisions; only edge-walk and edge-drop pure-role swarms are wave-eligible; and a deferred wave releases automatically from the resolved vanilla edge region.
