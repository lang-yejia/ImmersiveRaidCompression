Feature: Immersive Raid Compression runtime smoke

  Scenario: a high-point raid generates without errors
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When incident "RaidEnemy" fires with 10000 points
    And I wait 60 ticks
    Then the last compression handled a "human raid"
    And the last raid compression reduced the pawn count and retained at least 95 percent of vanilla kind cost
    And no errors were logged

  Scenario: a high-point mechanoid raid is compressed without bosses
    Given the save "test-colony" is loaded
    And raid compression telemetry is reset
    When a mechanoid raid fires with 30000 points
    And I wait 60 ticks
    Then the last compression handled a "mechanoid raid"
    And the last raid compression reduced the pawn count and retained at least 95 percent of vanilla kind cost
    And the last compression introduced no mechanoid bosses
    And no errors were logged
