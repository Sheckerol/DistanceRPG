using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The exploring clock (Phase 8.2): while exploring the party has no budget and no
/// turn to end, and the world's clock runs only while the leader walks — a turn for
/// every <see cref="Tuning.MovementBudget"/> walked (PHASE 8 DECISION 4), each with
/// every boundary a played round raises and nobody acting. A fight that starts
/// opens a new turn on the spot (PHASE 8 DECISIONS 7 and 15). What the party does
/// while exploring spends no movement (PHASE 8 DECISION 21) and teaches as in
/// combat (PHASE 8 DECISION 24); waiting a turn out banks an idle turn's mana
/// (PHASE 8 DECISION 23).
/// </summary>
[Collection(TestContent.Collection)]
public sealed class WalkingClockTests : IDisposable
{
    private const float Tile = GameConstants.Tile;

    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private static float TurnLength => GameContent.Current.Tuning.MovementBudget;

    private static PartyMemberState Member(string id, int r, int c, string weaponId = "weakspot_stiletto")
        => TestPools.Holding(id, TestWeapons.Get(weaponId), x: (c + 0.5f) * Tile, y: (r + 0.5f) * Tile);

    private static EnemyState Far() => new() { X = 25.5f * Tile, Y = 25.5f * Tile };

    private static TurnSystem Exploring(PartyMemberState[] party, params EnemyState[] enemies)
        => new(new int[30, 30], party, enemies, () => 10, mode: DungeonMode.Exploring);

    [Fact]
    public void ATurnPassesAtExactlyATurnsWalk_AndNoneStandingStill()
    {
        var a = Member("A", 3, 3);
        var turns = Exploring([a], Far());
        int started = 0, ended = 0;
        turns.PlayerTurnStarted += () => started++;
        turns.TurnEnded += _ => ended++;

        Assert.Equal(0, turns.AdvanceExploring(0f));       // standing still: the world waits
        Assert.Equal(0, turns.AdvanceExploring(TurnLength - 1f));
        Assert.Equal((0, TurnLength - 1f), (turns.TurnCount, turns.WalkedThisTurn));
        Assert.Equal(1, turns.AdvanceExploring(1f));        // exactly a turn's walk
        Assert.Equal((1, 0f), (turns.TurnCount, turns.WalkedThisTurn));

        // A long stride passes as many turns as it holds, the rest carried on.
        Assert.Equal(2, turns.AdvanceExploring(2 * TurnLength + 30f));
        Assert.Equal((3, 30f), (turns.TurnCount, turns.WalkedThisTurn));

        // Each opens a party turn, and none ends one with a banner: nothing was played.
        Assert.Equal((3, 0), (started, ended));
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void NothingIsSpent_TheTurnNeverNeedsEnding()
    {
        var a = Member("A", 3, 3);
        var turns = Exploring([a], Far());
        turns.AdvanceExploring(10 * TurnLength);
        Assert.Equal(a.EffectiveMax, a.DistLeft);   // no budget: whatever the walk
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void PoisonTicksOnTheWalk()
    {
        var a = Member("A", 3, 3);
        a.ApplyStatus(StatusEffectType.Poison, null, 3);
        var turns = Exploring([a], Far());
        int ticks = 0;
        turns.ActorStatusTicked += (_, _) => ticks++;
        int hp = a.Hp;

        turns.AdvanceExploring(TurnLength);

        Assert.True(ticks > 0);
        Assert.True(a.Hp < hp);
        Assert.True(a.StatusLevel(StatusEffectType.Poison) < 3);   // and it decays, as a round does
    }

    [Fact]
    public void ADummysTimerRunsOnWalkingTurns()
    {
        var a = Member("A", 3, 3);
        var dummy = Far();
        dummy.Alive = false;
        dummy.Hp = 0;
        dummy.DefeatCount = 1;
        dummy.DefeatedAtTurn = 0;
        var turns = Exploring([a], dummy);
        int wait = FarmLadder.ResurrectTurns(1);

        turns.AdvanceExploring((wait - 1) * TurnLength);
        Assert.False(dummy.Alive);
        turns.AdvanceExploring(TurnLength);
        Assert.True(dummy.Alive);
        Assert.Equal(wait, turns.TurnCount);
    }

    [Fact]
    public void AnUnseenEnemysWakeRunsDown_OnWalkingTurns()
    {
        var a = Member("A", 3, 3);
        var pursuer = Far();
        pursuer.TurnsSinceSeen = 0;
        var turns = Exploring([a], pursuer);
        turns.AdvanceExploring(2 * TurnLength);
        Assert.Equal(2, pursuer.TurnsSinceSeen);   // passive from here, as an unseen enemy always went
    }

    [Fact]
    public void AWalkingTurnBanksNoMana()
    {
        // PHASE 8 DECISION 16: only a combat turn's end turns unspent movement into mana.
        var a = Member("A", 3, 3);
        a.Mana = 10;
        var turns = Exploring([a], Far());
        turns.AdvanceExploring(3 * TurnLength);
        Assert.Equal(10, a.Mana);
    }

    [Fact]
    public void AFightMidWalk_DropsTheWalkingTurn_AndOpensANewOne()
    {
        var a = Member("A", 3, 3);
        var b = Member("B", 3, 4);
        var enemy = Far();
        var turns = Exploring([a, b], enemy);
        turns.AdvanceExploring(TurnLength - 10f);
        b.Movement.Reset(5f, MoveReason.Restore);   // whatever a member was left with
        int started = 0;
        turns.PlayerTurnStarted += () => started++;

        turns.NotifyEnemyVisible(enemy, true);

        Assert.Equal(DungeonMode.Combat, turns.Mode);
        Assert.Equal((1, 0f, 1), (turns.TurnCount, turns.WalkedThisTurn, started));   // a new turn, the walk dropped
        Assert.Equal(b.EffectiveMax, b.DistLeft);                                    // every member's whole movement
        Assert.True(turns.AnyLiveEnemySeenThisTurn);                                 // and the enemy that started it seen in it
        Assert.Equal(0, turns.AdvanceExploring(TurnLength));                         // the walk counts for nothing in combat
        Assert.Equal(1, turns.TurnCount);

        // Back in view while the fight is on opens nothing more.
        turns.NotifyEnemyVisible(enemy, false);
        turns.NotifyEnemyVisible(enemy, true);
        Assert.Equal((1, 1), (turns.TurnCount, started));
    }

    [Fact]
    public void AFightStartingInsideAWalkingTurn_OpensNoSecondTurn()
    {
        // A dummy that gets up on a tile already in view: the walking turn that
        // raised it is the fight's new turn.
        var a = Member("A", 3, 3);
        var dummy = Far();
        dummy.Alive = false;
        dummy.Hp = 0;
        dummy.DefeatCount = 1;
        dummy.DefeatedAtTurn = -FarmLadder.ResurrectTurns(1) + 1;   // due on the next turn
        var turns = Exploring([a], dummy);
        turns.NotifyEnemyInView(dummy, true);   // its corpse in view: no fight
        Assert.Equal(DungeonMode.Exploring, turns.Mode);
        int started = 0;
        turns.PlayerTurnStarted += () => started++;

        Assert.Equal(1, turns.AdvanceExploring(3 * TurnLength));

        Assert.True(dummy.Alive);
        Assert.Equal(DungeonMode.Combat, turns.Mode);
        Assert.Equal((1, 1, 0f), (turns.TurnCount, started, turns.WalkedThisTurn));
    }

    /// <summary>Step the logic clock as live play does until the waited turn in progress has played out; how many steps it took.</summary>
    private static int PlayOut(TurnSystem turns)
    {
        int steps = 0;
        for (; turns.Waiting && steps < 10_000; steps++)
            turns.Update(SaveGame.LogicStepSeconds);
        return steps;
    }

    /// <summary>Wait a turn out and play it to its end.</summary>
    private static void Wait(TurnSystem turns)
    {
        Assert.True(turns.WaitTurn());
        PlayOut(turns);
    }

    [Fact]
    public void WaitTurn_PlaysOneTurnOutAtWalkingPace_DroppingTheWalk_AndOnlyExploring()
    {
        var a = Member("A", 3, 3);
        var enemy = Far();
        var turns = Exploring([a], enemy);
        turns.AdvanceExploring(100f);

        Assert.True(turns.WaitTurn());
        Assert.Equal((0, 0f, true), (turns.TurnCount, turns.WalkedThisTurn, turns.Waiting));   // the walk dropped, the turn not yet out
        Assert.False(turns.WaitTurn());                     // one wait at a time
        Assert.Equal(0, turns.AdvanceExploring(TurnLength)); // and the party stands through it
        int steps = PlayOut(turns);
        Assert.Equal((1, false), (turns.TurnCount, turns.Waiting));
        // A turn's walk at walking pace: a second at today's tuning.
        Assert.InRange(steps * SaveGame.LogicStepSeconds, TurnLength / GameConstants.Speed - 0.02f, TurnLength / GameConstants.Speed + 0.02f);

        turns.NotifyEnemyVisible(enemy, true);   // a fight: the turn is ended, not waited out
        int count = turns.TurnCount;
        Assert.False(turns.WaitTurn());
        Assert.Equal(count, turns.TurnCount);
    }

    [Fact]
    public void ACastWhileExploring_SpendsNoMovement_PaysItsMana_AndTeachesAsInCombat()
    {
        var a = Member("A", 5, 5, "staff_of_renewal");
        var b = Member("B", 5, 6);
        var turns = Exploring([a, b], Far());
        int mana = a.Mana, hpXpB = b.HpXp;
        float movement = a.DistLeft;

        Assert.True(turns.TryCast(a, b));

        Assert.Equal(movement, a.DistLeft);                         // no budget to spend
        Assert.True(a.Mana < mana);                                 // the cast still pays its mana
        Assert.Equal(2, b.StatusLevel(StatusEffectType.Regeneration));   // and lands
        Assert.Equal(2, a.WeaponXp[WeaponClass.Staff]);              // and teaches, as in combat
        Assert.Equal(1, a.EquippedWeapon!.Wear);                     // and wears

        b.Hp -= 20;
        turns.AdvanceExploring(TurnLength);                          // the regeneration heals on the walk...
        Assert.True(b.HpXp > hpXpB);                                 // ...and teaches HP
    }

    [Fact]
    public void ADrinkWhileExploring_IsFree_AndTeaches()
    {
        var a = Member("A", 5, 5);
        a.Inventory[1] = TestPotions.Health(2);
        var turns = Exploring([a], Far());
        a.Hp -= 10;
        int hpXp = a.HpXp;

        var drink = turns.TryDrink(a, 1);

        Assert.NotNull(drink);
        Assert.Equal(0, drink!.Cost);
        Assert.Equal(a.EffectiveMax, a.DistLeft);
        Assert.True(drink.Restored > 0);
        Assert.Equal(hpXp + drink.Restored, a.HpXp);
    }

    [Fact]
    public void AWaitedTurn_BanksAWholeIdleTurnsMana_TeachingNothing_AndRestoringNoHp()
    {
        // PHASE 8 DECISION 23: Space while exploring is an idle turn, the way mana
        // comes back outside combat; a walking turn banks none (above).
        var a = Member("A", 3, 3);
        var down = Member("B", 3, 4);
        down.Alive = false;
        down.Hp = 0;
        down.Mana = 0;
        a.Mana = 0;
        a.Hp -= 10;
        int hp = a.Hp, manaXp = a.ManaXp;
        var turns = Exploring([a, down], Far());
        int perTurn = (int)(a.EffectiveMax / Math.Max(1, GameContent.Current.Tuning.MovementUnitsPerMana));

        Assert.True(turns.WaitTurn());
        Assert.Equal(0, a.Mana);                                     // banked at the turn's end, not its start
        PlayOut(turns);

        Assert.Equal(Math.Min(perTurn, a.UsableMaxMana), a.Mana);
        Assert.Equal((hp, manaXp), (a.Hp, a.ManaXp));
        Assert.Equal(0, down.Mana);                                  // nothing for the dead

        for (int i = 0; i < 50; i++) Wait(turns);
        Assert.Equal(a.UsableMaxMana, a.Mana);                       // capped at the spendable pool
    }

    [Fact]
    public void AWaitCancelledByAFight_BanksNothing_AndTheFightOpensFresh()
    {
        var a = Member("A", 3, 3);
        a.Mana = 0;
        var enemy = Far();
        var turns = Exploring([a], enemy);
        Assert.True(turns.WaitTurn());
        for (int i = 0; i < 30; i++) turns.Update(SaveGame.LogicStepSeconds);   // a quarter of the wait

        turns.NotifyEnemyVisible(enemy, true);

        Assert.Equal((DungeonMode.Combat, false), (turns.Mode, turns.Waiting));
        Assert.Equal((1, 0), (turns.TurnCount, a.Mana));   // the fight's own turn; the wait's mana never came
        Assert.Equal(a.EffectiveMax, a.DistLeft);
    }

    // ── Pursuers (Phase 8.3) ─────────────────────────────────────────────────

    /// <summary>An awake enemy that saw the party and lost it, at (r, c), its last-seen spot set by leaving view.</summary>
    private static (TurnSystem Turns, EnemyState Enemy) Pursuit(PartyMemberState[] party, int r, int c, int turnsSinceSeen = 0)
    {
        var enemy = new EnemyState { X = (c + 0.5f) * Tile, Y = (r + 0.5f) * Tile, TurnsSinceSeen = turnsSinceSeen };
        var turns = new TurnSystem(new int[30, 30], party, [enemy], () => 10, mode: DungeonMode.Combat);
        turns.NotifyEnemyInView(enemy, true);
        turns.NotifyEnemyInView(enemy, false);
        turns.ResumeMode(DungeonMode.Exploring);
        return (turns, enemy);
    }

    [Fact]
    public void LeavingView_AnEnemyKeepsWhereTheNearestMemberStood()
    {
        var near = Member("A", 3, 10);
        var far = Member("B", 3, 3);
        var (_, enemy) = Pursuit([far, near], 3, 20);
        Assert.Equal((near.X, near.Y), enemy.LastSeenSpot);
    }

    [Fact]
    public void APursuer_WalksItsBudgetsShareOfTheWalk_ToItsSpot_AndStopsThere()
    {
        var a = Member("A", 3, 3);
        var (turns, enemy) = Pursuit([a], 3, 8);   // five tiles off: inside the two turns it stays awake
        var spot = enemy.LastSeenSpot!.Value;
        a.X = 10.5f * Tile;   // the party walked off; the spot stays where it was seen
        int walks = 0;
        turns.EnemiesWalked += () => walks++;
        float startX = enemy.X;
        float share = enemy.MoveBudget / TurnLength;

        turns.AdvanceExploring(TurnLength / 2);

        Assert.Equal(startX - TurnLength / 2 * share, enemy.X, 2);   // half a turn: half its budget, straight down the corridor
        Assert.True(walks > 0);

        turns.AdvanceExploring(20 * TurnLength);   // long enough, but it stops at the spot
        Assert.Equal(spot, (enemy.X, enemy.Y));
    }

    [Fact]
    public void AWaitedTurn_MovesAPursuerItsWholeBudget()
    {
        var a = Member("A", 3, 3);
        var (turns, enemy) = Pursuit([a], 3, 25);
        float startX = enemy.X;
        Wait(turns);
        Assert.Equal(startX - StatusBehaviours.MiredBudget(enemy, enemy.MoveBudget), enemy.X, 1);
    }

    [Fact]
    public void APassiveEnemy_OrOneNeverLost_StaysPut()
    {
        var a = Member("A", 3, 3);
        var (turns, passive) = Pursuit([a], 3, 20, turnsSinceSeen: 2);
        float x = passive.X;
        turns.AdvanceExploring(3 * TurnLength);
        Assert.Equal(x, passive.X);

        var b = Member("B", 3, 3);
        var unseen = new EnemyState { X = 20.5f * Tile, Y = 3.5f * Tile, TurnsSinceSeen = 0 };
        var other = Exploring([b], unseen);
        other.AdvanceExploring(TurnLength);
        Assert.Equal(20.5f * Tile, unseen.X);   // awake, but it never lost the party: nowhere to go
    }

    [Fact]
    public void APursuerGoesPassive_AfterTwoTurnsUnseen()
    {
        var a = Member("A", 3, 3);
        var (turns, enemy) = Pursuit([a], 3, 28, turnsSinceSeen: 1);
        turns.AdvanceExploring(TurnLength);   // one walking turn: its wake reaches 2
        float x = enemy.X;
        turns.AdvanceExploring(TurnLength);
        Assert.Equal(x, enemy.X);
    }

    [Fact]
    public void APursuerSightedMidWalk_StartsTheFight_AndTheWalkStops()
    {
        var a = Member("A", 3, 3);
        var (turns, enemy) = Pursuit([a], 3, 20);
        bool sighted = false;
        // The scene's part: the first step it walks puts it in view.
        turns.EnemiesWalked += () => { if (!sighted) { sighted = true; turns.NotifyEnemyVisible(enemy, true); } };

        int passed = turns.AdvanceExploring(3 * TurnLength);

        Assert.Equal((0, DungeonMode.Combat, 1), (passed, turns.Mode, turns.TurnCount));   // the fight's own turn, the walk's dropped
        float x = enemy.X;
        turns.AdvanceExploring(TurnLength);
        Assert.Equal(x, enemy.X);   // in combat its own phase moves it, not the walk
    }

    [Fact]
    public void TheSameCastInCombat_PaysMovement_AndTeaches()
    {
        // The rule's other side, so the test above is about the mode and nothing else.
        var a = Member("A", 5, 5, "staff_of_renewal");
        var b = Member("B", 5, 6);
        var turns = new TurnSystem(new int[30, 30], [a, b], [Far()], () => 10);
        float movement = a.DistLeft;

        Assert.True(turns.TryCast(a, b));

        Assert.True(a.DistLeft < movement);
        Assert.Equal(2, a.WeaponXp[WeaponClass.Staff]);
        Assert.Equal(1, a.EquippedWeapon!.Wear);
    }
}
