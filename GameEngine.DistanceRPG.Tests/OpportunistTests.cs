using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §1.2 Opportunist: the axe's free attack on a target that chooses to leave
/// its reach — the exit-edge mirror of Brace, on the same zone lookup and the
/// same per-turn pool, and never on forced movement.
/// </summary>
public class OpportunistTests
{
    private const float Tile = GameConstants.Tile;

    private static (float X, float Y) At(int r, int c) => (c * Tile + Tile / 2f, r * Tile + Tile / 2f);

    private static PartyMemberState Char(string id, int r, int c, string weaponId)
    {
        var (x, y) = At(r, c);
        var ch = TestPools.Char(id, x: x, y: y);
        ch.Inventory[0] = TestWeapons.Get(weaponId);
        return ch;
    }

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
    public void FiresWhenTargetWalksOut()
    {
        // A lone healer standing in A's axe reach flees on its turn: the moment
        // it chooses to step out of reach, the axe gets its free attack.
        var grid = new int[20, 30];
        var a = Char("A", 5, 5, "great_axe");   // reach 60, Opportunist x1
        var healer = Enemy(5, 6, "staff_of_renewal", hp: 50);
        healer.TurnsSinceSeen = 0;
        var turns = new TurnSystem(grid, new[] { a }, new[] { healer }, () => 10);
        var fired = new List<ActorState>();
        turns.OpportunistTriggered += r => fired.Add(r);
        int hits = 0, fled = 0;
        turns.EnemyHit += (_, _) => hits++;
        turns.EnemyFleeing += _ => fled++;

        turns.NotifyEnemyVisible(healer, true);
        turns.EndTurn();
        Advance(turns, 10f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(1, fled);
        Assert.Same(a, Assert.Single(fired));
        Assert.Equal(1, hits);
        Assert.Equal(50 - 18, healer.Hp);   // no shield on a staff
        Assert.False(EnemyAi.CanHit(a, healer, a.EquippedWeapon!, grid));
    }

    [Fact]
    public void NotOnForcedMove()
    {
        // B's sword (Push x3) shoves the dummy out of A's axe reach: it has not
        // disengaged, it has been moved, and A gets nothing. The pair was
        // released all the same, so a later walk back in and out is a real exit.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "great_axe");
        var b = Char("B", 4, 6, "tower_guard");
        b.EquippedWeapon!.Acquire(Push, 2);
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a, b }, new[] { enemy }, () => 10);
        int opportunist = 0, hits = 0;
        turns.OpportunistTriggered += _ => opportunist++;
        turns.EnemyHit += (_, _) => hits++;
        Assert.True(EnemyAi.CanHit(a, enemy, a.EquippedWeapon!, grid));

        Assert.True(turns.TryAttack(b, enemy));
        Assert.Equal(At(8, 6), (enemy.X, enemy.Y));
        Assert.False(EnemyAi.CanHit(a, enemy, a.EquippedWeapon!, grid));
        Assert.Equal(0, opportunist);
        Assert.Equal(1, hits);

        // Walks back in (no reaction: the axe watches the exit) and out again by choice: now it fires.
        (enemy.X, enemy.Y) = At(5, 6);
        turns.NotifyActorMoved(enemy, MoveKind.Voluntary);
        Assert.Equal(0, opportunist);
        (enemy.X, enemy.Y) = At(8, 6);
        turns.NotifyActorMoved(enemy, MoveKind.Voluntary);
        Assert.Equal(1, opportunist);
        Assert.Equal(2, hits);
        Assert.Equal(200 - 7 - 15, enemy.Hp);   // the sword's 10 and the axe's 18, each into Block 3
    }

    [Fact]
    public void RoutingAxe_NeverProvokesItself()
    {
        // A Routing Axe (Rout x2 here) shoves what it hits out of its own
        // reach — a forced exit, which its own Opportunist never answers.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "routing_axe");
        a.EquippedWeapon!.Acquire(Rout, 1);
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        int opportunist = 0, hits = 0;
        turns.OpportunistTriggered += _ => opportunist++;
        turns.EnemyHit += (_, _) => hits++;

        Assert.True(turns.TryAttack(a, enemy));

        Assert.Equal(At(5, 8), (enemy.X, enemy.Y));
        Assert.False(EnemyAi.CanHit(a, enemy, a.EquippedWeapon!, grid));
        Assert.Equal(0, opportunist);
        Assert.Equal(1, hits);
        Assert.Equal(200 - 15, enemy.Hp);
    }

    [Fact]
    public void OnePerStackPerTurn()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "great_axe");   // Opportunist x1
        var enemy = Enemy(5, 6);
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);
        int opportunist = 0;
        turns.OpportunistTriggered += _ => opportunist++;

        void Step(int r, int c)
        {
            (enemy.X, enemy.Y) = At(r, c);
            turns.NotifyActorMoved(enemy, MoveKind.Voluntary);
        }

        Step(5, 9);   // out: one free attack
        Assert.Equal(1, opportunist);
        Step(5, 6);   // back in: entry is the spear's edge, not the axe's
        Step(5, 9);   // out again: the stack is spent this turn
        Assert.Equal(1, opportunist);

        // A new turn restores it; the dummy is unseen and out of everyone's reach, so the enemy phase is quiet.
        turns.EndTurn();
        Advance(turns, 3f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        Step(5, 6);
        Step(5, 9);
        Assert.Equal(2, opportunist);

        // Two stacks, two free attacks a turn: one more this turn, then the pool is dry until the next.
        a.EquippedWeapon!.Acquire(Opportunist, 1);
        Step(5, 6);
        Step(5, 9);
        Assert.Equal(3, opportunist);
        Step(5, 6);
        Step(5, 9);
        Assert.Equal(3, opportunist);

        turns.EndTurn();
        Advance(turns, 3f);
        Step(5, 6);
        Step(5, 9);
        Step(5, 6);
        Step(5, 9);
        Step(5, 6);
        Step(5, 9);
        Assert.Equal(5, opportunist);
        Assert.Equal(200 - 5 * 15, enemy.Hp);   // 18 into Block 3, five times
    }

    [Fact]
    public void EnemyAxe_PunishesPartyLeaving()
    {
        // An axe dummy makes disengaging expensive: A walks out of a seen axe
        // dummy's reach and takes its free attack; an unseen one ambushes nobody.
        var grid = new int[20, 20];
        var a = Char("A", 5, 5, "weakspot_stiletto");
        var axe = Enemy(5, 6, "great_axe");
        var turns = new TurnSystem(grid, new[] { a }, new[] { axe }, () => 10);
        turns.NotifyEnemyVisible(axe, true);
        var fired = new List<ActorState>();
        turns.OpportunistTriggered += r => fired.Add(r);

        (a.X, a.Y) = At(5, 2);
        turns.NotifyCharacterMoved(a);
        Assert.Same(axe, Assert.Single(fired));
        Assert.Equal(TestPools.FixtureHp - 18, a.Hp);   // no shield on a dagger

        // Back in and out again: one per stack per turn.
        (a.X, a.Y) = At(5, 5);
        turns.NotifyCharacterMoved(a);
        (a.X, a.Y) = At(5, 2);
        turns.NotifyCharacterMoved(a);
        Assert.Single(fired);

        // Unseen: no ambush from the fog.
        var b = Char("B", 5, 5, "weakspot_stiletto");
        var lurker = Enemy(5, 6, "great_axe");
        var turns2 = new TurnSystem(new int[20, 20], new[] { b }, new[] { lurker }, () => 10);
        int ambushes = 0;
        turns2.OpportunistTriggered += _ => ambushes++;
        (b.X, b.Y) = At(5, 2);
        turns2.NotifyCharacterMoved(b);
        Assert.Equal(0, ambushes);
        Assert.Equal(TestPools.FixtureHp, b.Hp);
    }
}
