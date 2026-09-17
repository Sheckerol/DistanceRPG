using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.2 Overwatch: bank the shot instead of firing, and whatever enters the
/// bow's reach on the enemy turn is shot for free — a ranged mirror of Brace
/// on the same threat-zone machinery.
/// </summary>
public class OverwatchTests
{
    private const float Tile = GameConstants.Tile;

    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, string weaponId)
    {
        var (x, y) = At(r, c);
        var ch = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        ch.Inventory[0] = TestWeapons.Get(weaponId);
        return ch;
    }

    /// <summary>A sword dummy on A's row, 60 units outside the Crossbow's reach: its 100-unit approach crosses in.</summary>
    private static EnemyState Approacher(PartyMemberState a, int row)
        => new() { X = a.X + 320f + 28f + 60f, Y = At(row, 0).Y };

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    [Fact]
    public void BankedShot_FiresOnEnemyEnteringReach()
    {
        // A holds fire with the Crossbow (range 320): the dummy that walks into
        // reach on its turn is shot for free. The same walk past an unarmed
        // crossbow is just a walk.
        var grid = new int[20, 30];
        var a = Char("A", 5, 2, "crossbow");
        var enemy = Approacher(a, 5);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var fired = new List<ActorState>();
        turns.OverwatchTriggered += r => fired.Add(r);
        int hits = 0;
        turns.EnemyHit += (_, _) => hits++;

        Assert.True(turns.CanOverwatch(a));
        Assert.True(turns.TryOverwatch(a));
        Assert.Equal(GameConstants.MaxDistance - a.EquippedWeapon!.ResolvedCost, a.DistLeft);
        Assert.Equal(1, a.HeldShots);
        Assert.False(turns.TryOverwatch(a));   // already holding

        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Same(a, Assert.Single(fired));
        Assert.Equal(1, hits);
        Assert.Equal(GameConstants.DummyHp - 2, enemy.Hp);   // 5 into Block 3

        var b = Char("B", 5, 2, "crossbow");
        var walker = Approacher(b, 5);
        var quiet = new TurnSystem(new int[20, 30], new[] { b }, new[] { walker }, () => 10);
        int shots = 0;
        quiet.OverwatchTriggered += _ => shots++;
        quiet.NotifyEnemyVisible(walker, true);
        quiet.EndTurn();
        Advance(quiet, 10f);
        Assert.Equal(0, shots);
        Assert.Equal(GameConstants.DummyHp, walker.Hp);
    }

    [Fact]
    public void HeldShotsEqualStacks()
    {
        // Overwatch x2 holds two shots: the first two dummies walking into reach
        // are shot, the third walks in for free.
        var grid = new int[20, 30];
        var a = Char("A", 5, 2, "crossbow");
        a.EquippedWeapon!.Acquire(Overwatch, 1);
        var e1 = Approacher(a, 4);
        var e2 = Approacher(a, 5);
        var e3 = Approacher(a, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2, e3 }, () => 10);
        int shots = 0;
        turns.OverwatchTriggered += _ => shots++;

        Assert.True(turns.TryOverwatch(a));
        Assert.Equal(2, a.HeldShots);

        foreach (var e in new[] { e1, e2, e3 })
            turns.NotifyEnemyVisible(e, true);
        turns.EndTurn();
        Advance(turns, 20f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(2, shots);
        Assert.Equal(GameConstants.DummyHp - 2, e1.Hp);
        Assert.Equal(GameConstants.DummyHp - 2, e2.Hp);
        Assert.Equal(GameConstants.DummyHp, e3.Hp);
    }

    [Fact]
    public void ClearedNextTurn()
    {
        var grid = new int[20, 30];
        var a = Char("A", 5, 2, "crossbow");
        var enemy = new EnemyState { X = 25 * Tile, Y = 15 * Tile };   // far and never seen: it sits the turn out
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        Assert.True(turns.TryOverwatch(a));
        Assert.Equal(1, a.HeldShots);
        Assert.False(turns.CanOverwatch(a));

        turns.EndTurn();
        Advance(turns, 3f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(0, a.HeldShots);         // a held shot lapses with the turn
        Assert.True(turns.CanOverwatch(a));   // and can be held again
    }

    [Fact]
    public void RangedOnly()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "tower_guard");
        var enemy = new EnemyState { X = 15 * Tile, Y = 15 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        Assert.False(turns.CanOverwatch(a));   // a sword holds no shot: no Overwatch stacks
        a.Inventory[0] = TestWeapons.Make("Hold Blade", 80, 10, 50, manaCost: 0, WeaponClass.Sword, (Overwatch, 1));
        Assert.False(turns.CanOverwatch(a));   // even forged with it by fiat, a melee weapon cannot hold fire
        a.Inventory[0] = TestWeapons.Get("staff_of_renewal");
        Assert.False(turns.CanOverwatch(a));   // a caster casts
        a.Inventory[0] = TestWeapons.Get("longbow");
        Assert.False(turns.CanOverwatch(a));   // ranged, but no Overwatch
        a.Inventory[0] = TestWeapons.Make("Held Bow", 320, 5, 30, manaCost: 0, WeaponClass.Ranged, (Overwatch, 1));
        Assert.True(turns.CanOverwatch(a));
        a.Inventory[0] = TestWeapons.Make("Held Darts", 190, 9, 15, manaCost: 0, WeaponClass.Throwing, (Overwatch, 1));
        Assert.True(turns.CanOverwatch(a));    // throwing is ranged too
        a.DistLeft = 14f;
        Assert.False(turns.CanOverwatch(a));   // holding costs the throw's movement
        a.DistLeft = 15f;
        Assert.True(turns.TryOverwatch(a));
        Assert.Equal(0f, a.DistLeft);
        Assert.Equal(1, a.HeldShots);
    }
}
