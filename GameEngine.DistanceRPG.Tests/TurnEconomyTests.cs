using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.2's turn economy: a weapon swap costs movement once combat has begun
/// and nothing out of it, gated on the same signal that grants marching, so
/// no weapon's downside is optional once the fight is on.
/// </summary>
public class TurnEconomyTests
{
    private const float Tile = GameConstants.Tile;

    private static PartyMemberState Char(string id, float x, float y, string weaponId)
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = TestWeapons.Get(weaponId);
        return c;
    }

    [Fact]
    public void Swap_Costs20InCombat_FreeOutOfCombat_RefusedUnder20()
    {
        var grid = new int[20, 30];
        var a = Char("A", 5 * Tile + 16, 5 * Tile + 16, "longbow");
        a.Inventory[1] = TestWeapons.Get("weakspot_stiletto");
        var enemy = new EnemyState { X = a.X + 60f, Y = a.Y };   // at knife range, not yet seen
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var spent = new List<(int Wanted, int Spent, string Source)>();
        turns.Events.On<MovementPayload>(GameEvent.MovementSpent, new HandlerPriority(9, 9), "probe", (p, s, _) =>
        {
            Assert.Same(a, s);
            spent.Add((p.Wanted, p.Spent, p.Source));
            return p;
        });

        // Out of combat — no live enemy seen this turn — a swap is free, both ways.
        Assert.False(turns.AnyLiveEnemySeenThisTurn);
        Assert.Equal(0, turns.SwapCost);
        Assert.True(turns.CanSwap(a, 1));
        Assert.True(turns.TrySwap(a, 1));
        Assert.Equal("weakspot_stiletto", a.EquippedWeapon!.Id);
        Assert.Equal("longbow", a.Inventory[1]!.Id);
        Assert.True(turns.TrySwap(a, 1));
        Assert.Equal("longbow", a.EquippedWeapon!.Id);
        Assert.Equal(GameConstants.MaxDistance, a.DistLeft);
        // A free swap is still a swap: the event records it, settled at zero, so whatever counts swaps sees every one.
        Assert.Equal(new[] { (0, 0, "swap"), (0, 0, "swap") }, spent);

        // Combat begins: the swap costs 20, spent through MovementSpent.
        turns.NotifyEnemyVisible(enemy, true);
        Assert.True(turns.AnyLiveEnemySeenThisTurn);
        Assert.Equal(20, turns.SwapCost);
        Assert.Equal(GameContent.Current.Tuning.WeaponSwapCost, turns.SwapCost);
        Assert.True(turns.TrySwap(a, 1));
        Assert.Equal("weakspot_stiletto", a.EquippedWeapon!.Id);
        Assert.Equal(GameConstants.MaxDistance - 20, a.DistLeft);
        Assert.Equal((20, 20, "swap"), spent[^1]);
        Assert.Equal(3, spent.Count);

        // A bow user caught at knife range pays 20 to swap and 30 to swing: 50 for the first hit.
        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(GameConstants.MaxDistance - 50, a.DistLeft);
        Assert.Equal(GameConstants.DummyHp - 12, enemy.Hp);   // 15 into Block 3

        // Under 20 the swap is refused and nothing changes; at exactly 20 it goes through.
        a.DistLeft = 19f;
        Assert.False(turns.CanSwap(a, 1));
        Assert.False(turns.TrySwap(a, 1));
        Assert.Equal("weakspot_stiletto", a.EquippedWeapon!.Id);
        Assert.Equal(19f, a.DistLeft);
        Assert.Equal(3, spent.Count);   // a refused swap raises nothing
        a.DistLeft = 20f;
        Assert.True(turns.TrySwap(a, 1));
        Assert.Equal("longbow", a.EquippedWeapon!.Id);
        Assert.Equal(0f, a.DistLeft);
        Assert.Equal((20, 20, "swap"), spent[^1]);
        Assert.Equal(4, spent.Count);

        // An empty slot is nothing to swap to, slot 0 is the equipped slot itself, and there is no slot 3.
        a.DistLeft = GameConstants.MaxDistance;
        Assert.Null(a.Inventory[2]);
        Assert.False(turns.CanSwap(a, 2));
        Assert.False(turns.CanSwap(a, 0));
        Assert.False(turns.CanSwap(a, 3));
        Assert.False(turns.TrySwap(a, 2));
        Assert.Equal(GameConstants.MaxDistance, a.DistLeft);

        // The signal is a *live* enemy seen: once the only one seen has fallen, swapping is free again this turn.
        enemy.Hp = 1;
        Assert.True(turns.TryAttack(a, enemy));   // the bow at a tile's range: 5 into Block 3 is enough
        Assert.False(enemy.Alive);
        Assert.False(turns.AnyLiveEnemySeenThisTurn);
        Assert.Equal(0, turns.SwapCost);
        float before = a.DistLeft;
        Assert.True(turns.TrySwap(a, 1));
        Assert.Equal(before, a.DistLeft);
        Assert.Equal((0, 0, "swap"), spent[^1]);   // free again, and still on the record
        Assert.Equal(5, spent.Count);

        // Not on the enemy's turn, and never for the fallen.
        var b = Char("B", 5 * Tile + 16, 5 * Tile + 16, "longbow");
        b.Inventory[1] = TestWeapons.Get("weakspot_stiletto");
        var far = new EnemyState { X = 25 * Tile, Y = 15 * Tile };
        var quiet = new TurnSystem(grid, new[] { b }, new[] { far }, () => 10);
        quiet.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, quiet.Phase);
        Assert.False(quiet.CanSwap(b, 1));
        for (float t = 0f; t < 3f; t += 1f / 30f) quiet.Update(1f / 30f);
        Assert.Equal(TurnPhase.Player, quiet.Phase);
        Assert.True(quiet.CanSwap(b, 1));
        b.Alive = false;
        Assert.False(quiet.CanSwap(b, 1));
    }
}
