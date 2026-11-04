using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class MovementLedgerTests
{
    private const float Tile = GameConstants.Tile;

    [Fact]
    public void TrySpend_PaysWholeWhenThereIsEnough()
    {
        var ledger = new MovementLedger(100f);
        Assert.True(ledger.TrySpend(30, MoveReason.Attack));
        Assert.Equal(70f, ledger.Left);
        Assert.True(ledger.TrySpend(70, MoveReason.Attack));      // exactly what is left
        Assert.Equal(0f, ledger.Left);
    }

    [Fact]
    public void TrySpend_RefusesAndChangesNothingWhenShort()
    {
        var ledger = new MovementLedger(29f);
        var heard = new List<MovementChange>();
        ledger.Changed += heard.Add;

        Assert.False(ledger.TrySpend(30, MoveReason.Attack));
        Assert.Equal(29f, ledger.Left);
        Assert.Empty(heard);
    }

    [Fact]
    public void SpendUpTo_TakesWhatIsLeftAndSaysHowMuch()
    {
        var ledger = new MovementLedger(10f);
        Assert.Equal(2.5f, ledger.SpendUpTo(2.5f, MoveReason.Walk));
        Assert.Equal(7.5f, ledger.SpendUpTo(40f, MoveReason.Walk));   // only the rest
        Assert.Equal(0f, ledger.Left);
        Assert.Equal(0f, ledger.SpendUpTo(5f, MoveReason.Walk));      // nothing left: nothing taken
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-3f)]
    [InlineData(float.NaN)]
    public void SpendUpTo_NothingWantedTakesNothing(float wanted)
    {
        var ledger = new MovementLedger(10f);
        Assert.Equal(0f, ledger.SpendUpTo(wanted, MoveReason.Walk));
        Assert.Equal(10f, ledger.Left);
    }

    [Fact]
    public void EveryChange_IsHeardWithBeforeAfterAndWhy()
    {
        var ledger = new MovementLedger(160f);
        var heard = new List<MovementChange>();
        ledger.Changed += heard.Add;

        ledger.SpendUpTo(4.25f, MoveReason.Walk);
        ledger.TrySpend(30, MoveReason.Attack);
        ledger.Reset(160f, MoveReason.TurnStart);
        ledger.Reset(160f, MoveReason.TurnStart);                       // no change: nothing to hear

        Assert.Equal(
            [
                new MovementChange(160f, 155.75f, MoveReason.Walk),
                new MovementChange(155.75f, 125.75f, MoveReason.Attack),
                new MovementChange(125.75f, 160f, MoveReason.TurnStart),
            ],
            heard);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void ANegativeBalanceOrCost_IsRefusedLoudly(float bad)
    {
        var ledger = new MovementLedger(10f);
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Reset(bad, MoveReason.Restore));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.TrySpend(bad, MoveReason.Attack));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovementLedger(bad));
    }

    [Fact]
    public void AMember_StartsOnTheBudget_AndATurnRefillsThroughTheLedger()
    {
        var member = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
        Assert.Equal(Premise.Budget, member.DistLeft);

        var heard = new List<MovementChange>();
        member.Movement.Changed += heard.Add;
        member.Movement.SpendUpTo(50f, MoveReason.Walk);
        member.StartTurn();

        Assert.Equal(MoveReason.TurnStart, heard[^1].Reason);
        Assert.Equal(member.EffectiveMax, member.DistLeft);
    }

    [Fact]
    public void AnAttack_IsPaidThroughTheLedger()
    {
        var grid = new int[20, 20];
        var a = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"), x: 5 * Tile, y: 5 * Tile);
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var heard = new List<MovementChange>();
        a.Movement.Changed += heard.Add;

        Assert.True(turns.TryAttack(a, enemy));

        var paid = Assert.Single(heard);
        Assert.Equal(MoveReason.Attack, paid.Reason);
        Assert.Equal(a.MovementCost(a.EquippedWeapon!), paid.Before - paid.After);
    }

    [Fact]
    public void AnAttackShortOfMovement_IsRefusedAndCostsNothing()
    {
        var grid = new int[20, 20];
        var a = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"), x: 5 * Tile, y: 5 * Tile);
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        a.Movement.Reset(a.MovementCost(a.EquippedWeapon!) - 1, MoveReason.Restore);
        float before = a.DistLeft;

        Assert.False(turns.TryAttack(a, enemy));
        Assert.Equal(before, a.DistLeft);
        Assert.Equal(enemy.MaxHp, enemy.Hp);
    }
}
