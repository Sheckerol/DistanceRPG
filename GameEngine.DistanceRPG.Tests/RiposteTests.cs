using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.2 Riposte: a successful block grants a free counter-swing, once per
/// stack per turn — and a crit, which bypasses Block entirely, is never a
/// successful block.
/// </summary>
public class RiposteTests
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

    private static EnemyState Enemy(int r, int c, string weaponId = "arming_sword", int hp = 200)
    {
        var (x, y) = At(r, c);
        return new EnemyState { X = x, Y = y, Weapon = TestWeapons.Get(weaponId), Hp = hp };
    }

    /// <summary>One hit through the turn system's table, whoever swings: the applier writes, and whatever it queues drains.</summary>
    private static void Hit(TurnSystem turns, ActorState attacker, ActorState defender, int roll)
        => CombatRules.Resolve(turns.Events, attacker, defender, attacker.EquippedWeapon!,
            CombatRules.SurfaceDistanceUnits(attacker, defender), () => roll);

    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    [Fact]
    public void CounterOnSuccessfulBlock_NotOnCrit_UsesPerTurn()
    {
        // The Riposte Blade: a blocked hit is answered with a free counter-swing,
        // once per stack per turn; a crit is never blocked, so it is never answered.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "riposte_blade");   // Block x1, Riposte x1, Push x1
        var enemy = Enemy(5, 6);                    // Arming Sword: Block x1, Push x1
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var countered = new List<ActorState>();
        turns.RiposteTriggered += r => countered.Add(r);
        var enemyHits = new List<AttackResolution>();
        turns.EnemyHit += (_, r) => enemyHits.Add(r);

        Hit(turns, enemy, a, 10);
        Assert.Equal(GameConstants.PlayerHp - 7, a.Hp);   // 10 into Block 3: a successful block
        Assert.Same(a, Assert.Single(countered));
        var counter = Assert.Single(enemyHits);
        Assert.Equal((7, 3), (counter.Taken, counter.Blocked));   // the counter is a hit like any other: 10 into the dummy's Block 3
        Assert.Equal(200 - 7, enemy.Hp);
        // Both blades shove: the dummy's hit moved A a tile west, then A's counter moved the dummy a tile east.
        Assert.Equal(At(5, 4), (a.X, a.Y));
        Assert.Equal(At(5, 7), (enemy.X, enemy.Y));

        // The second blocked hit this turn goes unanswered: one counter per stack per turn.
        Hit(turns, enemy, a, 10);
        Assert.Equal(GameConstants.PlayerHp - 14, a.Hp);
        Assert.Single(countered);
        Assert.Equal(200 - 7, enemy.Hp);

        // A new turn restores it. (The dummy is out of sword reach of A and unseen, so the enemy phase is quiet.)
        turns.EndTurn();
        Advance(turns, 3f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        (a.X, a.Y) = At(5, 6);   // back beside it
        turns.NotifyCharacterMoved(a);
        Hit(turns, enemy, a, 10);
        Assert.Equal(2, countered.Count);

        // Not on a crit: Block is skipped entirely, so there is no successful block to answer.
        var b = Char("B", 5, 5, "riposte_blade");
        var kris = Enemy(5, 6, "disarming_kris");   // crits on 19+
        var crit = new TurnSystem(grid, new[] { b }, new[] { kris }, () => 19);
        int counters = 0;
        crit.RiposteTriggered += _ => counters++;
        Hit(crit, kris, b, 19);
        Assert.Equal(GameConstants.PlayerHp - 45, b.Hp);
        Assert.Equal(0, counters);
        Assert.Equal(200, kris.Hp);

        // Not out of reach: a blocked arrow from across the room has no counter-swing to answer it.
        var c = Char("C", 5, 5, "riposte_blade");
        var archer = Enemy(5, 12, "hunting_bow");   // seven tiles off: 196 surface units, past the blade's 80
        var far = new TurnSystem(grid, new[] { c }, new[] { archer }, () => 10);
        int farCounters = 0;
        far.RiposteTriggered += _ => farCounters++;
        Hit(far, archer, c, 10);
        Assert.Equal(GameConstants.PlayerHp - 6, c.Hp);   // 5 + 4 (Longshot x1: seven tiles, four past the free three) into Block 3, blocked all the same
        Assert.Equal(0, farCounters);
        Assert.Equal(200, archer.Hp);
    }

    [Fact]
    public void EnemyRiposte_CountersAPartyMember()
    {
        // Same handler, other side: a Riposte Blade dummy blocks A's sword and counters.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "tower_guard");          // Block x2: absorbs 6
        var enemy = Enemy(5, 6, "riposte_blade");
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        var countered = new List<ActorState>();
        turns.RiposteTriggered += r => countered.Add(r);
        int characterHits = 0;
        turns.CharacterHit += (_, _) => characterHits++;

        Assert.True(turns.TryAttack(a, enemy));

        Assert.Equal(200 - 7, enemy.Hp);                   // 10 into Block 3
        Assert.Same(enemy, Assert.Single(countered));
        Assert.Equal(1, characterHits);
        Assert.Equal(GameConstants.PlayerHp - 4, a.Hp);   // the counter's 10 into the Tower Guard's 6 — and no counter to the counter: A holds no Riposte
    }
}
