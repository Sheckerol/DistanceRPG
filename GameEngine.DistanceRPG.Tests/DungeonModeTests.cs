using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The mode in Logic (Phase 8.1, PHASE 8 DECISION 1): exactly one of exploring,
/// resting and combat holds, and <see cref="TurnSystem.Mode"/> is the one place it
/// changes — into combat the moment a live enemy comes into view, in any phase,
/// and back to exploring only at a player turn's start (or a floor arrival) with
/// none in view. Exploring still plays in turns at this step.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class DungeonModeTests : IDisposable
{
    private const float Tile = GameConstants.Tile;

    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private static PartyMemberState Member(string id, float x, float y, string weaponId = "weakspot_stiletto")
        => TestPools.Holding(id, TestWeapons.Get(weaponId), x: x, y: y);

    /// <summary>One whole round: the party's turn ended, the enemies' played out, the next player turn begun.</summary>
    private static void NextTurn(TurnSystem turns)
    {
        turns.EndTurn();
        for (float t = 0f; t < 6f && turns.Phase != TurnPhase.Player; t += 1f / 30f)
            turns.Update(1f / 30f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    private static (TurnSystem Turns, PartyMemberState[] Party, EnemyState Enemy, List<string> Log) Fixture()
    {
        var grid = new int[30, 30];
        var party = new[] { Member("A", 3 * Tile, 3 * Tile), Member("B", 4 * Tile, 3 * Tile) };
        var enemy = new EnemyState { X = 25 * Tile, Y = 25 * Tile };
        var turns = new TurnSystem(grid, party, new[] { enemy }, () => 10);
        var log = new List<string>();
        turns.ModeChanged += (was, now) => log.Add($"{was}->{now}");
        turns.CombatEnded += () => log.Add("ended");
        return (turns, party, enemy, log);
    }

    [Fact]
    public void ASystemOpensExploring()
    {
        var (turns, _, _, log) = Fixture();
        Assert.Equal(DungeonMode.Exploring, turns.Mode);
        NextTurn(turns);
        Assert.Equal(DungeonMode.Exploring, turns.Mode);
        Assert.Empty(log);
    }

    [Fact]
    public void ALiveEnemyComingIntoView_StartsCombat_Once()
    {
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        Assert.Equal(DungeonMode.Combat, turns.Mode);
        turns.NotifyEnemyVisible(enemy, true);   // still in view: no second change
        Assert.Equal(["Exploring->Combat"], log);
    }

    [Fact]
    public void InView_WithoutASighting_IsCombatToo()
    {
        // A resumed turn's lighting, or an enemy standing in plain view as a turn
        // starts: in view is a fight, sighted or not.
        var (turns, _, enemy, _) = Fixture();
        turns.NotifyEnemyInView(enemy, true);
        Assert.False(turns.AnyLiveEnemySeenThisTurn);
        Assert.Equal(DungeonMode.Combat, turns.Mode);
    }

    [Fact]
    public void ACorpseInView_IsNoFight()
    {
        var (turns, _, enemy, log) = Fixture();
        enemy.Alive = false;
        turns.NotifyEnemyVisible(enemy, true);
        Assert.Equal(DungeonMode.Exploring, turns.Mode);
        Assert.Empty(log);
    }

    [Fact]
    public void AnEnemyWalkingIntoView_InTheEnemyPhase_StartsCombat()
    {
        var (turns, _, enemy, _) = Fixture();
        turns.EndTurn();
        Assert.NotEqual(TurnPhase.Player, turns.Phase);
        turns.NotifyEnemyVisible(enemy, true);
        Assert.Equal(DungeonMode.Combat, turns.Mode);
    }

    [Fact]
    public void CombatOutlastsSight_UntilTheNextTurnStart()
    {
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        turns.NotifyEnemyVisible(enemy, false);   // out of view mid-turn: still a fight
        Assert.Equal(DungeonMode.Combat, turns.Mode);

        NextTurn(turns);
        Assert.Equal(DungeonMode.Exploring, turns.Mode);
        Assert.Equal(["Exploring->Combat", "Combat->Exploring", "ended"], log);   // exploring before the end is raised
    }

    [Fact]
    public void KillingTheLastEnemyInView_LeavesCombatOn_UntilTheNextTurnStart()
    {
        var (turns, _, enemy, _) = Fixture();
        turns.ResurrectionActive = false;
        turns.NotifyEnemyVisible(enemy, true);
        enemy.Alive = false;
        Assert.Equal(DungeonMode.Combat, turns.Mode);

        NextTurn(turns);
        Assert.Equal(DungeonMode.Exploring, turns.Mode);
    }

    [Fact]
    public void ALiveEnemyStillInView_KeepsCombatAcrossTheTurn()
    {
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        NextTurn(turns);
        Assert.Equal(DungeonMode.Combat, turns.Mode);
        Assert.Equal(["Exploring->Combat"], log);
    }

    [Fact]
    public void ACheckWithALiveEnemyInView_IsCombat_WhateverTheModeSaid()
    {
        // A dummy that got up on a tile already lit as its corpse: in view, alive,
        // and nobody has told the system anything new. The turn's start makes it a fight.
        var (turns, _, enemy, _) = Fixture();
        enemy.Alive = false;
        turns.NotifyEnemyInView(enemy, true);
        Assert.Equal(DungeonMode.Exploring, turns.Mode);
        enemy.Alive = true;

        Assert.False(turns.CheckEncounterEnd());
        Assert.Equal(DungeonMode.Combat, turns.Mode);
    }

    [Fact]
    public void AnEnemyInView_MayReact_SightedOrNot()
    {
        // The fix after Phase 7 made an enemy in plain view count as seen for its
        // wake; its zone reads the in-view set too, so a spear standing in plain view
        // braces a walk-in even before anyone has crossed a tile to sight it.
        var grid = new int[20, 30];
        var a = Member("A", 5 * Tile + 16, 5 * Tile + 16);
        var enemy = new EnemyState { X = a.X + 200f, Y = a.Y, Weapon = TestWeapons.Get("skirmishers_pike") };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        int braces = 0;
        turns.EnemyBraceTriggered += _ => braces++;

        turns.NotifyEnemyInView(enemy, true);
        Assert.False(turns.AnyLiveEnemySeenThisTurn);
        a.X = enemy.X - 150f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(1, braces);
    }

    [Fact]
    public void AnEnemyNeitherSeenNorInView_StillNeverReacts()
    {
        var grid = new int[20, 30];
        var a = Member("A", 5 * Tile + 16, 5 * Tile + 16);
        var enemy = new EnemyState { X = a.X + 200f, Y = a.Y, Weapon = TestWeapons.Get("skirmishers_pike") };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        int braces = 0;
        turns.EnemyBraceTriggered += _ => braces++;

        turns.NotifyEnemyInView(enemy, true);
        turns.NotifyEnemyInView(enemy, false);   // it was in view, and is no longer
        a.X = enemy.X - 150f;
        turns.NotifyCharacterMoved(a);
        Assert.Equal(0, braces);
    }

    [Fact]
    public void ASameTurnCarry_WithALiveEnemySighted_OpensInCombat()
    {
        var grid = new int[30, 30];
        var party = new[] { Member("A", 3 * Tile, 3 * Tile) };
        var enemy = new EnemyState { X = 25 * Tile, Y = 25 * Tile };
        var first = new TurnSystem(grid, party, new[] { enemy }, () => 10);
        first.NotifyEnemyVisible(enemy, true);
        var carry = first.ExportCarry();

        Assert.Equal(DungeonMode.Combat, new TurnSystem(grid, party, new[] { enemy }, () => 10, carry: carry).Mode);
        // A carry from an earlier turn is dropped, and its sighting with it.
        Assert.Equal(DungeonMode.Exploring, new TurnSystem(grid, party, new[] { enemy }, () => 10, firstTurn: 1, carry: carry).Mode);
        // Its only sighting fallen since: no fight to come back to.
        enemy.Alive = false;
        Assert.Equal(DungeonMode.Exploring, new TurnSystem(grid, party, new[] { enemy }, () => 10, carry: carry).Mode);
    }

    [Fact]
    public void ResumeMode_TakesUpTheSavedMode_NeverARest()
    {
        var (turns, _, _, log) = Fixture();
        turns.ResumeMode(DungeonMode.Exploring);
        Assert.Empty(log);   // no change, nothing raised
        turns.ResumeMode(DungeonMode.Combat);
        Assert.Equal(DungeonMode.Combat, turns.Mode);
        Assert.Equal(["Exploring->Combat"], log);
        Assert.Throws<ArgumentOutOfRangeException>(() => turns.ResumeMode(DungeonMode.Resting));   // a save never holds a rest (PHASE 8 DECISION 18)
        Assert.Equal(DungeonMode.Combat, turns.Mode);
    }

    [Theory]
    [InlineData(true, false, DungeonMode.Exploring)]   // granted the march, nothing live sighted: it marched
    [InlineData(true, true, DungeonMode.Combat)]       // a Phase 7 save whose march a sighting revoked
    [InlineData(false, false, DungeonMode.Combat)]     // not marching: a fight's turn
    [InlineData(false, true, DungeonMode.Combat)]
    public void AResumedTurnsMode_IsReadOffTheView(bool marching, bool liveSeen, DungeonMode expected)
        => Assert.Equal(expected, DungeonScene.ResumedMode(new ViewDto("A", marching), liveSeen));

    [Fact]
    public void TheSwapCost_ReadsTheMode()
    {
        var (turns, _, enemy, _) = Fixture();
        Assert.Equal(0, turns.SwapCost);
        turns.NotifyEnemyInView(enemy, true);
        Assert.Equal(GameContent.Current.Tuning.WeaponSwapCost, turns.SwapCost);
    }
}
