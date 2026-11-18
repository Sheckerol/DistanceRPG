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
        var turns = new TurnSystem(grid, party, new[] { enemy }, () => 10, mode: DungeonMode.Exploring);
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
    public void CombatOutlastsSight_ForAWholeTurn()
    {
        // PHASE 8 DECISION 22: the fight ends only once no living enemy has been in
        // view for a whole turn — breaking sight round a corner is not the end of it.
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        turns.NotifyEnemyVisible(enemy, false);   // out of view mid-turn: still a fight
        Assert.Equal(DungeonMode.Combat, turns.Mode);

        NextTurn(turns);
        Assert.Equal(DungeonMode.Combat, turns.Mode);   // it was in view this turn: a turn's grace

        NextTurn(turns);
        Assert.Equal(DungeonMode.Exploring, turns.Mode);   // a whole turn unseen
        Assert.Equal(["Exploring->Combat", "Combat->Exploring", "ended"], log);   // exploring before the end is raised
    }

    [Fact]
    public void AnEnemySeenInTheEnemysPhase_KeepsTheFightOnToo()
    {
        // In view at any moment of the turn counts, either side's phase: one that
        // walks into sight and out again while the enemies move is a fight's.
        var (turns, _, enemy, _) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        turns.NotifyEnemyVisible(enemy, false);
        NextTurn(turns);   // the grace turn

        turns.EndTurn();
        turns.NotifyEnemyVisible(enemy, true);    // round the corner on its own turn...
        turns.NotifyEnemyVisible(enemy, false);   // ...and back out of it
        for (float t = 0f; t < 6f && turns.Phase != TurnPhase.Player; t += 1f / 30f)
            turns.Update(1f / 30f);

        Assert.Equal(DungeonMode.Combat, turns.Mode);
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
    public void ABareSystemOpensInCombat_AVisitsFloorExploring_NothingElse()
    {
        var grid = new int[30, 30];
        var party = new[] { Member("A", 3 * Tile, 3 * Tile) };
        Assert.Equal(DungeonMode.Combat, new TurnSystem(grid, party, Array.Empty<EnemyState>()).Mode);
        Assert.Equal(DungeonMode.Exploring, new TurnSystem(grid, party, Array.Empty<EnemyState>(), mode: DungeonMode.Exploring).Mode);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TurnSystem(grid, party, Array.Empty<EnemyState>(), mode: (DungeonMode)99));
    }

    [Fact]
    public void ASameTurnCarry_OfAFight_OpensInCombat()
    {
        var grid = new int[30, 30];
        var party = new[] { Member("A", 3 * Tile, 3 * Tile) };
        var enemy = new EnemyState { X = 25 * Tile, Y = 25 * Tile };
        var first = new TurnSystem(grid, party, new[] { enemy }, () => 10, mode: DungeonMode.Exploring);
        first.NotifyEnemyVisible(enemy, true);   // the fight opens a new turn (PHASE 8 DECISION 15)
        var carry = first.ExportCarry();
        Assert.Equal((1, true), (carry.Turn, carry.InCombat));

        TurnSystem Open(int turn, TurnCarry c) => new(grid, party, new[] { enemy }, () => 10, firstTurn: turn, carry: c, mode: DungeonMode.Exploring);

        // The party left in combat: the floor arrived on opens in it.
        Assert.Equal(DungeonMode.Combat, Open(1, carry).Mode);
        // A carry from an earlier turn is dropped, and the fight with it.
        Assert.Equal(DungeonMode.Exploring, Open(2, carry).Mode);
        // A live enemy of this roster sighted this turn is a fight too; its only sighting fallen since, none.
        var sightedOnly = carry with { InCombat = false };
        Assert.Equal(DungeonMode.Combat, Open(1, sightedOnly).Mode);
        enemy.Alive = false;
        Assert.Equal(DungeonMode.Exploring, Open(1, sightedOnly).Mode);
    }

    [Fact]
    public void ResumeMode_TakesUpTheSavedMode_NothingElse()
    {
        var (turns, _, _, log) = Fixture();
        turns.ResumeMode(DungeonMode.Exploring);
        Assert.Empty(log);   // no change, nothing raised
        turns.ResumeMode(DungeonMode.Combat);
        Assert.Equal(DungeonMode.Combat, turns.Mode);
        Assert.Equal(["Exploring->Combat"], log);
        Assert.Throws<ArgumentOutOfRangeException>(() => turns.ResumeMode((DungeonMode)99));
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
