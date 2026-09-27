Feature: Immersive Raid Compression runtime smoke

  Scenario: a high-point raid generates without errors
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When incident "RaidEnemy" fires with 10000 points
    And I wait 60 ticks
    Then the last compression handled a "human raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And compression history contains detailed before and after compositions
    And the last compression preserved its tactical identity
    When I open the compression history window
    And I wait 1 ticks
    Then the compression history window is open
    And no errors were logged

  Scenario: a high-point mechanoid raid is compressed without bosses
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When a mechanoid raid fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "mechanoid raid"
    And the last raid compression reduced the pawn count and retained between 95 and 105 percent of vanilla kind cost
    And the last compression introduced no mechanoid bosses
    And compression history contains detailed before and after compositions
    And the last compression preserved its tactical identity
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
