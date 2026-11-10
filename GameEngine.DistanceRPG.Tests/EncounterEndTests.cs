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

    // ── The revive and its cut (§7.1, step 2) ────────────────────────────────

    /// <summary>Hands the system the visit's cut against a snapshot taken now, at <paramref name="percent"/>.</summary>
    private static XpSnapshot ChargeDeaths(TurnSystem turns, PartyMemberState[] party, int percent)
    {
        var door = XpSnapshot.Of(party);
        turns.DeathCost = member => door.ClawBack([member], percent);
        return door;
    }

    private static void Fall(PartyMemberState member)
    {
        member.Hp = 0;
        member.Alive = false;
    }

    [Fact]
    public void AFallenMember_GetsUpAtOneHp_AsTheEncounterEnds_ManaAsItWas()
    {
        var (turns, party, enemy, log) = Fixture();
        turns.CharacterRevived += (member, _) => log.Add($"revived {member.Id} hp={member.Hp}");
        turns.NotifyEnemyVisible(enemy, true);
        int mana = party[1].Mana;
        Fall(party[1]);

        NextTurn(turns);   // the enemy still in view: down for the encounter
        Assert.False(party[1].Alive);

        turns.NotifyEnemyVisible(enemy, false);
        NextTurn(turns);

        Assert.True(party[1].Alive);
        Assert.Equal(1, party[1].Hp);   // PHASE 7 DECISION 6
        Assert.Equal(mana, party[1].Mana);
        Assert.Equal(party[1].EffectiveMax, party[1].DistLeft);   // up before the budgets refilled
        Assert.Equal(["turn", "revived B hp=1", "ended", "turn"], log);
    }

    [Fact]
    public void ARevive_CostsHalfTheRunGain_InEveryPool_Truncated()
    {
        var (turns, party, _, _) = Fixture();
        var member = party[1];
        ChargeDeaths(turns, party, 50);
        int dagger = member.WeaponXp[WeaponClass.Dagger], hp = member.HpXp, mana = member.ManaXp;
        member.WeaponXp[WeaponClass.Dagger] += 11;
        member.HpXp += 7;
        member.ManaXp += 4;
        IReadOnlyList<XpClawback>? paid = null;
        turns.CharacterRevived += (_, clawbacks) => paid = clawbacks;
        Fall(member);

        NextTurn(turns);

        Assert.Equal(dagger + 6, member.WeaponXp[WeaponClass.Dagger]);   // 11 - 11*50/100 = 11 - 5
        Assert.Equal(hp + 4, member.HpXp);                               // 7 - 3
        Assert.Equal(mana + 2, member.ManaXp);                           // 4 - 2
        Assert.Equal(
            [new XpClawback("B", "Dagger", dagger + 11, dagger + 6), new XpClawback("B", XpSnapshot.HpPool, hp + 7, hp + 4), new XpClawback("B", XpSnapshot.ManaPool, mana + 4, mana + 2)],
            paid);
    }

    [Fact]
    public void RepeatDeaths_HalveWhatIsLeft_AndNeverGoBelowTheDoor()
    {
        var (turns, party, _, _) = Fixture();
        var member = party[1];
        ChargeDeaths(turns, party, 50);
        int door = member.HpXp;
        member.HpXp += 40;

        int[] after = new int[4];
        for (int death = 0; death < after.Length; death++)
        {
            Fall(member);
            NextTurn(turns);
            Assert.True(member.Alive);
            after[death] = member.HpXp - door;
        }
        Assert.Equal([20, 10, 5, 3], after);   // 5 - 5*50/100 = 5 - 2: truncation keeps the odd point

        for (int death = 0; death < 3; death++) { Fall(member); NextTurn(turns); }
        Assert.Equal(door + 1, member.HpXp);   // 3 → 2 → 1 → 1: half of one point truncates to nothing, and the door is never reached
        Fall(member);
        IReadOnlyList<XpClawback>? paid = null;
        turns.CharacterRevived += (_, clawbacks) => paid = clawbacks;
        NextTurn(turns);
        Assert.Empty(paid!);   // nothing left to lose: REVIVED alone
    }

    [Fact]
    public void OnlyTheFallenPay()
    {
        var (turns, party, _, _) = Fixture();
        ChargeDeaths(turns, party, 50);
        party[0].HpXp += 10;
        party[1].HpXp += 10;
        int standing = party[0].HpXp;
        Fall(party[1]);

        NextTurn(turns);

        Assert.Equal(standing, party[0].HpXp);
        Assert.Equal(standing - 5, party[1].HpXp);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(100, 0)]
    public void TheShareIsTheTunings(int percent, int kept)
    {
        var (turns, party, _, _) = Fixture();
        ChargeDeaths(turns, party, percent);
        int door = party[1].HpXp;
        party[1].HpXp += 10;
        Fall(party[1]);

        NextTurn(turns);

        Assert.Equal(door + kept, party[1].HpXp);
    }

    [Fact]
    public void TheVisitCharges_TheTuningsShare_AgainstItsDoor()
    {
        // DungeonState.OpenTurns hands the system the cut: DeathXpLossPercent of the
        // gain since the visit's own entry snapshot.
        var script = VisitSaveTests.Flagship();
        var turns = script.Turns;
        var member = script.Party[1];
        var door = script.Visit.EntrySnapshot.Members.Single(m => m.MemberId == member.Id);
        member.HpXp = door.HpXp + 10;

        Assert.NotNull(turns.DeathCost);
        turns.DeathCost!(member);

        Assert.Equal(door.HpXp + 10 - 10 * GameContent.Current.Tuning.DeathXpLossPercent / 100, member.HpXp);
    }

    // ── Where they get up (§7.3, step 3) ─────────────────────────────────────

    private static (int R, int C) TileOf(ActorState actor)
        => ((int)MathF.Floor(actor.Y / Tile), (int)MathF.Floor(actor.X / Tile));

    private static void Centre(ActorState actor, int r, int c)
        => (actor.X, actor.Y) = ((c + 0.5f) * Tile, (r + 0.5f) * Tile);

    [Fact]
    public void ARevivedMember_GetsUpBesideTheLeader_NotWhereItFell()
    {
        var grid = new int[30, 30];
        var a = Member("A", 0, 0);
        var b = Member("B", 0, 0);
        var c = Member("C", 0, 0);
        Centre(a, 20, 20);
        Centre(b, 5, 5);    // fell far off, in the fight
        Centre(c, 10, 10);  // the one the player is moving
        var turns = new TurnSystem(grid, new[] { a, b, c }, Array.Empty<EnemyState>(), () => 10) { Leader = () => c };
        Fall(b);

        Assert.True(turns.CheckEncounterEnd());

        Assert.Equal((9, 10), TileOf(b));   // the first free tile of the leader's: up, down, left, right
        Assert.Equal(((10 + 0.5f) * Tile, (9 + 0.5f) * Tile), (b.X, b.Y));   // a tile's centre
    }

    [Fact]
    public void MembersRevivedTogether_TakeTheirOwnTiles_InRosterOrder_AroundEveryBody()
    {
        var grid = new int[30, 30];
        var a = Member("A", 0, 0);
        var b = Member("B", 0, 0);
        var c = Member("C", 0, 0);
        var enemy = new EnemyState();
        Centre(a, 10, 10);
        Centre(b, 2, 2);
        Centre(c, 3, 3);
        Centre(enemy, 9, 10);   // a corpse would not hold it; a living body does
        var turns = new TurnSystem(grid, new[] { a, b, c }, new[] { enemy }, () => 10) { Leader = () => a };
        Fall(b);
        Fall(c);

        Assert.True(turns.CheckEncounterEnd());   // the enemy is not in view

        Assert.Equal((11, 10), TileOf(b));   // up is the enemy's: down
        Assert.Equal((10, 9), TileOf(c));    // then left
    }

    [Theory]
    [InlineData(EnemyState.DummyRadius, 9)]   // a dummy two tiles up leaves the tile between free: up
    [InlineData(20f, 11)]                     // a boss's wider body reaches into it: down instead
    public void ARevivedMember_NeverGetsUpOverlappingABody(float radius, int expectedRow)
    {
        var grid = new int[30, 30];
        var a = Member("A", 0, 0);
        var b = Member("B", 0, 0);
        var enemy = new EnemyState { BodyRadius = radius };
        Centre(a, 10, 10);
        Centre(b, 2, 2);
        Centre(enemy, 8, 10);
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => 10) { Leader = () => a };
        Fall(b);

        Assert.True(turns.CheckEncounterEnd());   // the enemy is not in view

        Assert.Equal((expectedRow, 10), TileOf(b));
    }

    [Fact]
    public void InACorridor_TheyGetUpAlongIt_NeverThroughAWall()
    {
        // A one-tile corridor running left to right along row 5, walls either side;
        // the room beyond the wall above is nearer as the crow flies, and never reached.
        var grid = new int[12, 12];
        for (int r = 0; r < 12; r++)
            for (int col = 0; col < 12; col++)
                grid[r, col] = r == 5 || r <= 3 ? 0 : 1;
        var a = Member("A", 0, 0);
        var b = Member("B", 0, 0);
        var c = Member("C", 0, 0);
        Centre(a, 5, 6);
        Centre(c, 5, 5);   // left of the leader: taken
        Centre(b, 2, 2);
        var turns = new TurnSystem(grid, new[] { a, b, c }, Array.Empty<EnemyState>(), () => 10) { Leader = () => a };
        Fall(b);

        turns.CheckEncounterEnd();

        Assert.Equal((5, 7), TileOf(b));
    }

    [Fact]
    public void WithTheLeaderDown_TheFirstLivingMemberLeads()
    {
        var grid = new int[30, 30];
        var a = Member("A", 0, 0);
        var b = Member("B", 0, 0);
        var c = Member("C", 0, 0);
        Centre(a, 2, 2);
        Centre(b, 15, 15);
        Centre(c, 20, 20);
        var turns = new TurnSystem(grid, new[] { a, b, c }, Array.Empty<EnemyState>(), () => 10) { Leader = () => a };
        Fall(a);   // the leader fell; B is the first standing

        turns.CheckEncounterEnd();

        Assert.Equal((14, 15), TileOf(a));
    }

    [Fact]
    public void AnArrival_RevivesBesideTheLeader_WhenNoLiveEnemyIsInViewThere()
    {
        // The stairs (PHASE 7 DECISION 1): the party arrives with a body carried, and
        // the arrival's check (the scene's, after its lighting) gets it up on the new floor.
        var (turns, party, enemy, log) = Fixture();
        turns.Leader = () => party[0];
        Fall(party[1]);
        Centre(party[1], 20, 20);
        turns.NotifyEnemyInView(enemy, false);

        Assert.True(turns.CheckEncounterEnd());

        Assert.True(party[1].Alive);
        var (lr, lc) = TileOf(party[0]);
        var (r, c) = TileOf(party[1]);
        Assert.Equal(1, Math.Abs(r - lr) + Math.Abs(c - lc));
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
