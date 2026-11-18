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

    [Fact]
    public void WaitTurn_PassesOneTurn_DroppingTheWalk_AndOnlyExploring()
    {
        var a = Member("A", 3, 3);
        var enemy = Far();
        var turns = Exploring([a], enemy);
        turns.AdvanceExploring(100f);

        Assert.True(turns.WaitTurn());
        Assert.Equal((1, 0f), (turns.TurnCount, turns.WalkedThisTurn));

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

        turns.WaitTurn();

        Assert.Equal(Math.Min(perTurn, a.UsableMaxMana), a.Mana);
        Assert.Equal((hp, manaXp), (a.Hp, a.ManaXp));
        Assert.Equal(0, down.Mana);                                  // nothing for the dead

        for (int i = 0; i < 50; i++) turns.WaitTurn();
        Assert.Equal(a.UsableMaxMana, a.Mana);                       // capped at the spendable pool
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
