Feature: Immersive Raid Compression runtime smoke

  Scenario: a high-point raid generates without errors
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When an ordinary human edge raid fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "human raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And compression history contains detailed before and after compositions
    And the last compression preserved its tactical identity
    And the last human raid history record contains a treatment classification
    When I open the compression history window
    And I wait 1 ticks
    Then the compression history window is open
    And no errors were logged

  Scenario: a high-point mixed mechanoid raid is compressed with bounded boss promotion
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When a mechanoid raid fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "mechanoid raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the last compression introduced at most one mechanoid boss
    And compression history contains detailed before and after compositions
    And the last mechanoid raid history record contains a treatment classification
    And the last compression preserved its tactical identity
    When I open the compression history window
    And I wait 1 ticks
    Then the compression history window is open
    And no errors were logged

  Scenario: vanilla mechanoid force families receive distinct safe treatments
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When I classify representative vanilla mechanoid forces
    Then the mechanoid classifier separates swarms, mixed forces, breaches, and boss-led forces
    And mixed mechanoid boss promotion is rare and bounded
    And no errors were logged

  Scenario: vanilla human raid families protect their special challenges
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When I classify representative vanilla human raid strategies
    Then the human raid classifier assigns dedicated treatments and protects invalid combinations
    And no errors were logged

  Scenario: a high-point human siege keeps its vanilla siege workflow
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When a human siege raid fires with 30000 points
    And I wait 180 ticks
    Then the last compression handled a "human raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the compressed human siege retains its vanilla siege controller
    And compression history contains detailed before and after compositions
    And no errors were logged

  Scenario: a high-point human sapper raid keeps its path-opening specialists
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When a human sapper raid fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "human raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the compressed specialist raid preserves every path-opening unit and its escort floor
    And compression history contains detailed before and after compositions
    And no errors were logged

  Scenario: a high-point human breach raid keeps its path-opening specialists
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When a human breach raid fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "human raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the compressed breach raid preserves every path-opening unit and its escort floor
    And compression history contains detailed before and after compositions
    And no errors were logged

  Scenario: a high-point human center-drop raid keeps its vanilla arrival pressure
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When a human center-drop raid fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "human raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the compressed center-drop raid preserves its vanilla arrival and density floor
    And no errors were logged

  Scenario: a high-point human random-drop raid keeps its vanilla arrival pressure
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When a human random-drop raid fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "human raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the compressed random-drop raid preserves its vanilla arrival and density floor
    And no errors were logged

  Scenario: a homogeneous mechanoid swarm arrives in controlled edge waves
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When I stage a homogeneous mechanoid edge wave at 10000 points
    Then the homogeneous mechanoid force is split into an active and deferred force
    And every planned mechanoid wave meets the dynamic minimum and undersized tails are merged
    And the staged mechanoid waves use the resolved vanilla edge region
    And I wait 60 ticks
    When I defeat the active first mechanoid wave
    And I wait 660 ticks
    Then the next mechanoid wave releases automatically
    And the next mechanoid wave uses safe vanilla drop pods near survivors
    And no errors were logged

  Scenario: a high-point manhunter pack uses a stronger vanilla animal
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When incident "ManhunterPack" fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "manhunter pack"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And compression history contains detailed before and after compositions
    And the last compression preserved its tactical identity
    And the manhunter replacement has a compatible animal tactical profile
    And an incompatible large animal tank is rejected for the original manhunter species
    And no errors were logged

  Scenario: a high-point mech cluster keeps its complete structure
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When I compare mech cluster generation with and without compression at 10000 points
    Then the last compression handled a "mech cluster"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the last compression introduced no mechanoid bosses
    And the mech cluster building sketch and activation state are unchanged
    And every compressed mech cluster defender uses a vanilla sketch position
    And compression history contains detailed before and after compositions
    And the last compression preserved its tactical identity
    And no errors were logged

  Scenario: a compressed mech cluster spawns through the real incident
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When incident "MechCluster" fires with 10000 points
    And I wait 60 ticks
    Then the last compression handled a "mech cluster"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the last compression introduced no mechanoid bosses
    And the last compression preserved its tactical identity
    And no errors were logged
