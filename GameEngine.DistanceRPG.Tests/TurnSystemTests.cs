using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class TurnSystemTests
{
    private const float Tile = GameConstants.Tile;

    private static PartyMemberState Char(string id, float x, float y, int weaponIdx = 0)
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = GameConstants.Weapons[weaponIdx];
        return c;
    }

    /// <summary>Advance the state machine in small steps.</summary>
    private static void Advance(TurnSystem turns, float seconds, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt)
            turns.Update(dt);
    }

    [Fact]
    public void EndTurn_BanksHalfUnspentMovement_AndRefillsNextTurn()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 15 * Tile, Y = 15 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        a.DistLeft = 100f; // 60 spent
        float banked = -1f;
        turns.TurnEnded += saved => banked = saved;

        turns.EndTurn();
        Assert.Equal(TurnPhase.TurnEnding, turns.Phase);
        Assert.Equal(50f, banked); // floor(100/2)

        Advance(turns, 3f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.Equal(1, turns.TurnCount);
        Assert.Equal(GameConstants.MaxDistance + 50f, a.EffectiveMax);
        Assert.Equal(a.EffectiveMax, a.DistLeft);
    }

    [Fact]
    public void EndTurn_BankIsCappedAtHalfBaseBudget()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        a.DistLeft = 300f; // from a previous bonus turn
        var enemy = new EnemyState { X = 15 * Tile, Y = 15 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        float banked = -1f;
        turns.TurnEnded += saved => banked = saved;
        turns.EndTurn();

        Assert.Equal(GameConstants.MaxDistance / 2f, banked);
    }

    [Fact]
    public void TryAttack_SpendsCost_DamagesEnemy_AndRespectsRules()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, weaponIdx: 0); // dagger: dmg 15, cost 30, range 40
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile }; // 50px away, surface 22 â‰¤ 40
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        AttackResolution? res = null;
        turns.EnemyHit += (_, r) => res = r;

        Assert.True(turns.TryAttack(a, enemy));
        Assert.Equal(GameConstants.MaxDistance - 30f, a.DistLeft);
        Assert.NotNull(res);
        // Dagger 15 into sword block 3 â†’ 12
        Assert.Equal(12, res.Value.Damage);
        Assert.Equal(50 - 12, enemy.Hp);
    }

    [Fact]
    public void TryAttack_FailsWithoutBudgetOrRangeOrLos()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        a.DistLeft = 29f; // dagger costs 30
        Assert.False(turns.TryAttack(a, enemy));

        a.DistLeft = 160f;
        enemy.X = 15 * Tile; // out of range
        Assert.False(turns.TryAttack(a, enemy));

        // In range (spear) but a wall tile sits between attacker and enemy.
        a.Inventory[0] = GameConstants.Weapons[2]; // spear, range 130
        enemy.X = 7 * Tile + 16f;                  // ~110px away, within reach
        grid[5, 6] = 1;                            // wall in the middle column
        Assert.False(turns.TryAttack(a, enemy));

        grid[5, 6] = 0; // clear it: same shot now lands
        Assert.True(turns.TryAttack(a, enemy));
    }

    [Fact]
    public void KillingEnemy_MarksDefeat_AndResurrectsAfterConfiguredTurns()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 5 * Tile + 50f, Y = 5 * Tile, Hp = 10 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        bool defeated = false, resurrected = false;
        turns.EnemyDefeated += _ => defeated = true;
        turns.EnemyResurrected += _ => resurrected = true;

        Assert.True(turns.TryAttack(a, enemy)); // 12 â‰¥ 10 HP
        Assert.True(defeated);
        Assert.False(enemy.Alive);
        Assert.Equal(0, enemy.DefeatedAtTurn);

        // The configured number of end-turn cycles later, the dummy comes back.
        for (int i = 0; i < GameConstants.DummyResurrectTurns; i++)
        {
            Assert.False(resurrected);
            turns.EndTurn();
            Advance(turns, 3f);
        }

        Assert.True(resurrected);
        Assert.True(enemy.Alive);
        Assert.Equal(enemy.MaxHp, enemy.Hp);
    }

    [Fact]
    public void EnemyTurn_WhenSeenAndAdjacent_AttacksThreeTimesWithSword()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, weaponIdx: 0); // dagger defender: no block
        var enemy = new EnemyState { X = 5 * Tile + 60f, Y = 5 * Tile }; // sword range
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;
        turns.NotifyEnemyVisible(enemy, true);

        turns.EndTurn();
        Advance(turns, 6f);

        // Budget 100, scaled sword cost 100/160*50 = 31.25 â†’ 3 attacks of 10.
        Assert.Equal(3, hits);
        Assert.Equal(GameConstants.PlayerHp - 30, a.Hp);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void EnemyTurn_UnseenForTwoTurns_DoesNotMove()
    {
        var grid = new int[20, 30];
        var a = Char("A", 2 * Tile, 5 * Tile);
        var enemy = new EnemyState { X = 25 * Tile, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        float startX = enemy.X;
        turns.EndTurn(); // never seen: TurnsSinceSeen 2 â†’ 3
        Advance(turns, 4f);

        Assert.Equal(startX, enemy.X);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void EnemyTurn_WhenSeen_MovesTowardTarget()
    {
        var grid = new int[20, 30];
        var a = Char("A", 2 * Tile + 16, 5 * Tile + 16);
        var enemy = new EnemyState { X = 25 * Tile + 16, Y = 5 * Tile + 16 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        float startX = enemy.X;
        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        // Moved its full 100-unit budget toward the character (straight line).
        Assert.Equal(startX - GameConstants.EnemyMove, enemy.X, 1);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void Brace_TriggersWhenEnemyWalksIntoSpearRange()
    {
        var grid = new int[20, 30];
        // Spear char; enemy 250px away: outside spear reach (130+28=158),
        // after its 100px approach it lands at 150px â†’ inside reach.
        var a = Char("A", 5 * Tile + 16, 5 * Tile + 16, weaponIdx: 2);
        var enemy = new EnemyState { X = a.X + 250f, Y = a.Y };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        PartyMemberState? braced = null;
        int enemyHits = 0;
        turns.BraceTriggered += c => braced = c;
        turns.EnemyHit += (_, _) => enemyHits++;

        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 10f);

        Assert.Same(a, braced);
        Assert.Equal(1, enemyHits); // the free brace attack landed
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void GameOver_WhenLastCharacterDies()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile);
        a.Hp = 5; // one sword hit kills
        var enemy = new EnemyState { X = 5 * Tile + 60f, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { enemy }, () => 10);

        bool over = false, died = false;
        turns.GameOver += () => over = true;
        turns.CharacterDied += _ => died = true;

        turns.NotifyEnemyVisible(enemy, true);
        turns.EndTurn();
        Advance(turns, 6f);

        Assert.True(died);
        Assert.True(over);
        Assert.Equal(TurnPhase.GameOver, turns.Phase);
        Assert.False(a.Alive);
    }

    [Fact]
    public void TwoAdjacentEnemies_BothActInSequence()
    {
        var grid = new int[20, 20];
        var a = Char("A", 5 * Tile, 5 * Tile, weaponIdx: 0); // dagger defender: no block
        var e1 = new EnemyState { X = 5 * Tile + 60f, Y = 5 * Tile }; // sword range
        var e2 = new EnemyState { X = 5 * Tile - 60f, Y = 5 * Tile };
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2 }, () => 10);

        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;
        turns.NotifyEnemyVisible(e1, true);
        turns.NotifyEnemyVisible(e2, true);

        turns.EndTurn();
        Advance(turns, 12f);

        // Each enemy lands its 3 scaled sword attacks (budget 100, cost 31.25).
        Assert.Equal(6, hits);
        Assert.Equal(GameConstants.PlayerHp - 60, a.Hp);
        Assert.Equal(TurnPhase.Player, turns.Phase);
    }

    [Fact]
    public void TwoEnemies_ApproachingTheSameTarget_DoNotStack()
    {
        var grid = new int[20, 30];
        var a = Char("A", 2 * Tile + 16, 5 * Tile + 16);
        // Same column, far beyond sword reach: both walk their full budget
        // toward A along the same row and would land on the same spot.
        var e1 = new EnemyState { X = 20 * Tile + 16, Y = 5 * Tile + 16 };
        var e2 = new EnemyState { X = 25 * Tile + 16, Y = 5 * Tile + 16 };
        var turns = new TurnSystem(grid, new[] { a }, new[] { e1, e2 }, () => 10);

        turns.NotifyEnemyVisible(e1, true);
        turns.NotifyEnemyVisible(e2, true);
        turns.EndTurn();
        Advance(turns, 15f);

        Assert.Equal(TurnPhase.Player, turns.Phase);
        float dx = e1.X - e2.X;
        float dy = e1.Y - e2.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        Assert.True(dist >= e1.Radius + e2.Radius,
            $"enemies ended {dist:0.0} apart, overlapping (radii sum {e1.Radius + e2.Radius})");
    }

    [Fact]
    public void PassiveEnemies_SkipInstantly_WithoutStallingTheTurn()
    {
        var grid = new int[20, 60];
        var a = Char("A", 2 * Tile, 5 * Tile);
        // Ten never-seen enemies scattered far away: none should cost a beat.
        var enemies = Enumerable.Range(0, 10)
            .Select(i => new EnemyState { X = (20 + 3 * i) * Tile, Y = 5 * Tile })
            .ToList();
        var turns = new TurnSystem(grid, new[] { a }, enemies, () => 10);

        turns.EndTurn();
        Advance(turns, 1.0f); // banner is 0.8s; idle enemies must add ~nothing

        Assert.Equal(TurnPhase.Player, turns.Phase);
        Assert.All(enemies, e => Assert.Equal((20 + 3 * enemies.IndexOf(e)) * Tile, e.X));
    }
}
