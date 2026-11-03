using System.Collections.Immutable;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.2 Overwatch: bank the shot instead of firing, and whatever enters the
/// bow's reach on the enemy turn is shot for free — walked or shoved in, but
/// on the enemy's turn, never the party's own — a ranged mirror of Brace on
/// the same threat-zone machinery.
/// </summary>
public class OverwatchTests
{
    private const float Tile = GameConstants.Tile;

    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, string weaponId)
    {
        var (x, y) = At(r, c);
        return TestPools.Holding(id, TestWeapons.Get(weaponId), x: x, y: y);
    }

    /// <summary>A sword dummy on A's row, 60 units outside the Crossbow's reach: its 100-unit approach crosses in.</summary>
    private static EnemyState Approacher(PartyMemberState a, int row)
        => new() { X = a.X + 320f + 28f + 60f, Y = At(row, 0).Y };

    /// <summary>A dummy on a tile's centre, with enough HP that nothing here kills it by accident.</summary>
    private static EnemyState Enemy(int r, int c, string weaponId = "arming_sword", int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = TestWeapons.Get(weaponId), Hp = hp };
    }

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
        Assert.Equal(GameConstants.DummyHp - 9, enemy.Hp);   // 5 + 7 (Longshot x1: the crossing is at the tenth tile) into Block 3

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
        Assert.Equal(GameConstants.DummyHp - 9, e1.Hp);   // 5 + 7 (Longshot x1 at the tenth tile) into Block 3
        Assert.Equal(GameConstants.DummyHp - 9, e2.Hp);
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
    public void HeldShots_CountDownAsTheyFire_AndLapseWithASwap()
    {
        // Overwatch x2 holds two shots, and the reading is the shots still
        // held, not the shots armed: while one is held A cannot hold again,
        // and on the enemy turn each dummy walking into the Crossbow's reach
        // takes one — 2 to 1, then 1 to 0, read as each fires. What is left
        // lapses with the turn, and a new turn may hold anew.
        var grid = new int[20, 30];
        var a = Char("A", 5, 2, "crossbow");
        a.EquippedWeapon!.Acquire(Overwatch, 1);
        var e1 = Approacher(a, 4);
        var e2 = Approacher(a, 5);
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2 }, () => 10);
        var held = new List<int>();
        turns.OverwatchTriggered += reactor => held.Add(reactor.HeldShots);

        Assert.True(turns.TryOverwatch(a));
        Assert.Equal(2, a.HeldShots);
        Assert.False(turns.CanOverwatch(a));   // a shot is still held

        turns.NotifyEnemyVisible(e1, true);
        turns.NotifyEnemyVisible(e2, true);
        turns.EndTurn();
        Advance(turns, 20f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(new[] { 1, 0 }, held);
        Assert.Equal(GameConstants.DummyHp - 9, e1.Hp);   // 5 + 7 (Longshot x1 at the tenth tile) into Block 3
        Assert.Equal(GameConstants.DummyHp - 9, e2.Hp);
        Assert.Equal(0, a.HeldShots);
        Assert.True(turns.CanOverwatch(a));    // a new turn: nothing held, the pool restored

        // A swap lets a held shot lapse with the weapon that held it (the
        // movement it cost is not refunded), and a swap back may hold anew.
        var c = Char("C", 5, 2, "crossbow");
        var far = Enemy(15, 15);
        var swap = new TurnSystem(grid, new[] { c }, new[] { far }, () => 10);
        Assert.True(swap.TryOverwatch(c));
        Assert.Equal(1, c.HeldShots);
        float spent = c.DistLeft;
        c.Inventory[1] = c.Inventory[0];
        c.Inventory[0] = TestWeapons.Get("weakspot_stiletto");
        swap.NotifyWeaponChanged(c);
        Assert.Equal(0, c.HeldShots);
        Assert.Equal(spent, c.DistLeft);
        Assert.False(swap.CanOverwatch(c));    // a dagger holds no shot
        (c.Inventory[0], c.Inventory[1]) = (c.Inventory[1], c.Inventory[0]);
        swap.NotifyWeaponChanged(c);
        Assert.True(swap.CanOverwatch(c));     // nothing fired, so the pool is untouched: it can be held again
    }

    [Fact]
    public void PartyShove_IntoHeldReach_OnThePartysTurn_FiresNothing()
    {
        // A held shot is banked for the enemy turn (§1.2: "if an enemy enters
        // line of sight during the enemy turn, it fires for free"). On the
        // party's own turn B's sword shoves a dummy into the Crossbow's reach:
        // a real entry, but not on the enemy's turn, so nothing fires and both
        // shots stay held for the turn they were banked for.
        var grid = new int[20, 20];
        var a = Char("A", 5, 2, "crossbow");
        a.EquippedWeapon!.Acquire(Overwatch, 1);
        var b = Char("B", 5, 14, "tower_guard");
        var enemy = Enemy(5, 13);   // eleven tiles from A: 352 less both radii is 324, just past the bow's 320
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => 10);
        int shots = 0;
        turns.OverwatchTriggered += _ => shots++;
        Assert.False(EnemyAi.CanHit(a, enemy, a.EquippedWeapon!, grid));

        Assert.True(turns.TryOverwatch(a));
        Assert.True(turns.TryAttack(b, enemy));
        Assert.Equal(At(5, 12), (enemy.X, enemy.Y));
        Assert.True(EnemyAi.CanHit(a, enemy, a.EquippedWeapon!, grid));   // shoved inside the reach
        Assert.Equal(0, shots);
        Assert.Equal(2, a.HeldShots);
        Assert.Equal(200 - 7, enemy.Hp);   // the sword's 10 into Block 3, and no bolt

        // The gate is whose phase it is, not how the body moved: the handler
        // reads the flag the turn system sets, and the same forced entry on
        // the enemy's turn earns the shot.
        var entry = new ThreatPayload(enemy, MoveKind.Forced, ZoneEdge.Enter, ImmutableArray<Reaction>.Empty,
            CombatRules.SurfaceDistanceUnits(a, enemy));
        Assert.Equal(Overwatch, Assert.Single(ReactionBehaviours.Overwatch(entry, a, enemy).Reactions).Source);
        Assert.True(ReactionBehaviours.Overwatch(entry with { OnReactorsTurn = true }, a, enemy).Reactions.IsDefaultOrEmpty);
    }

    [Fact]
    public void ForcedEntry_OnTheEnemyTurn_FiresTheHeldShot()
    {
        // Forced movement triggers the zones (settled), and on the enemy turn
        // a held shot answers a shove as it answers a walk. The dummy swings at
        // R's Riposte Blade from just outside the Crossbow's reach; the block
        // earns R's counter, whose shove carries the dummy a tile toward A —
        // into the reach, on the enemy's turn — and the held shot goes.
        var grid = new int[20, 20];
        var a = Char("A", 5, 2, "crossbow");
        var r = Char("R", 5, 16, "riposte_blade");
        var enemy = Enemy(5, 13);   // eleven tiles from A, just past the bow's reach; three from R, inside the sword's
        var turns = new TurnSystem(grid, new[] { a, r }, new[] { enemy }, () => 10);
        var fired = new List<ActorState>();
        turns.OverwatchTriggered += reactor => fired.Add(reactor);
        int counters = 0;
        turns.RiposteTriggered += _ => counters++;
        Assert.False(EnemyAi.CanHit(a, enemy, a.EquippedWeapon!, grid));

        Assert.True(turns.TryOverwatch(a));
        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(1, counters);
        Assert.Equal(At(5, 12), (enemy.X, enemy.Y));   // the counter's shove: a tile away from R, into A's reach
        Assert.Same(a, Assert.Single(fired));
        Assert.Equal(200 - 7 - 9, enemy.Hp);           // the counter's 10 and the bolt's 5 + 7 (Longshot x1 at the tenth tile), each into Block 3
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
