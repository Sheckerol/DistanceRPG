using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The encounter's end in Logic (Phase 7, §7.2, step 1): the turn system keeps
/// which live enemies are in view now, on both edges, and at a player turn's
/// start — or a floor arrival — raises <see cref="TurnSystem.CombatEnded"/> when
/// none is and there was a fight to end (an enemy sighted since the last check)
/// or a member is down. Nothing revives yet; this is the moment a revive will
/// hang off.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class EncounterEndTests : IDisposable
{
    private const float Tile = GameConstants.Tile;

    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private static PartyMemberState Member(string id, float x, float y)
        => TestPools.Holding(id, TestWeapons.Get("weakspot_stiletto"), x: x, y: y);

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    /// <summary>One whole round: the party's turn ended, the enemies' played out, the next player turn begun.</summary>
    private static void NextTurn(TurnSystem turns)
    {
        turns.EndTurn();
        Advance(turns, 6f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    private static (TurnSystem Turns, PartyMemberState[] Party, EnemyState Enemy, List<string> Log) Fixture()
    {
        var grid = new int[30, 30];
        var party = new[] { Member("A", 3 * Tile, 3 * Tile), Member("B", 4 * Tile, 3 * Tile) };
        var enemy = new EnemyState { X = 25 * Tile, Y = 25 * Tile };
        var turns = new TurnSystem(grid, party, new[] { enemy }, () => 10);
        var log = new List<string>();
        turns.CombatEnded += () => log.Add("ended");
        turns.PlayerTurnStarted += () => log.Add("turn");
        return (turns, party, enemy, log);
    }

    [Fact]
    public void ALiveEnemyInView_KeepsTheEncounterOn()
    {
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        Assert.True(turns.AnyLiveEnemyInView);

        NextTurn(turns);

        Assert.True(turns.AnyLiveEnemyInView);   // nobody said it left view
        Assert.Equal(["turn"], log);
    }

    [Fact]
    public void LosingSight_EndsTheEncounter_AtTheNextTurnStart_Once()
    {
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        turns.NotifyEnemyVisible(enemy, false);   // out of view mid-turn: nothing yet (PHASE 7 DECISION 5)
        Assert.False(turns.AnyLiveEnemyInView);
        Assert.Empty(log);

        NextTurn(turns);
        Assert.Equal(["ended", "turn"], log);   // before the turn's own start, so a revive gets a full budget

        NextTurn(turns);
        Assert.Equal(["ended", "turn", "turn"], log);   // the encounter is closed: no second end
    }

    [Fact]
    public void KillingTheLastEnemyInView_EndsTheEncounter()
    {
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        enemy.Alive = false;   // its corpse stays in view, and a corpse is no fight
        turns.ResurrectionActive = false;

        Assert.False(turns.AnyLiveEnemyInView);
        NextTurn(turns);
        Assert.Equal(["ended", "turn"], log);
    }

    [Fact]
    public void AMemberDown_EndsTheEncounter_WithNoSightingToClose()
    {
        // A save taken with a member down (PHASE 7 DECISION 3): the encounter's
        // open flag did not come back, and the fallen member is reason enough.
        var (turns, party, _, log) = Fixture();
        party[1].Alive = false;

        NextTurn(turns);
        Assert.Equal(["ended", "turn"], log);
    }

    [Fact]
    public void NoFightAndNobodyDown_EndsNothing()
    {
        var (turns, _, _, log) = Fixture();
        NextTurn(turns);
        NextTurn(turns);
        Assert.Equal(["turn", "turn"], log);
    }

    [Fact]
    public void InViewWithoutASighting_HoldsTheFight_ButCountsAsNoSighting()
    {
        // A resumed turn's lighting: the enemy is there, but the saved turn
        // already says whether it was seen.
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyInView(enemy, true);

        Assert.True(turns.AnyLiveEnemyInView);
        Assert.False(turns.AnyLiveEnemySeenThisTurn);
        Assert.False(turns.CheckEncounterEnd());
        Assert.Empty(log);
    }

    [Fact]
    public void TheArrivalCheck_EndsAnOpenEncounter_AndNotTwice()
    {
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        turns.NotifyEnemyVisible(enemy, false);

        Assert.True(turns.CheckEncounterEnd());
        Assert.False(turns.CheckEncounterEnd());
        Assert.Equal(["ended"], log);
    }

    [Fact]
    public void AnEnemyWalkingBackIntoView_ReopensTheFight()
    {
        var (turns, _, enemy, log) = Fixture();
        turns.NotifyEnemyVisible(enemy, true);
        turns.NotifyEnemyVisible(enemy, false);
        turns.NotifyEnemyVisible(enemy, true);

        NextTurn(turns);
        Assert.Equal(["turn"], log);
    }
}
