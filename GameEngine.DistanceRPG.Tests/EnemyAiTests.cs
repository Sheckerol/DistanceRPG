using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class EnemyAiTests
{
    private const float Tile = GameConstants.Tile;

    private static PartyMemberState Char(string id, float x, float y, int weaponIdx = 0)
    {
        var c = new PartyMemberState { Id = id, ColorIndex = 0, X = x, Y = y };
        c.Inventory[0] = GameConstants.Weapons[weaponIdx];
        return c;
    }

    private static EnemyState Enemy(float x, float y) => new() { X = x, Y = y };

    [Fact]
    public void SelectTarget_PrefersHittableOverCloser()
    {
        var grid = new int[20, 20]; // all open
        var enemy = Enemy(10 * Tile, 10 * Tile);

        // "near" is closest but behind a wall; "far" is in sword range with LOS
        grid[10, 8] = 1;
        var near = Char("A", 7 * Tile + 16, 10 * Tile + 16); // behind the wall
        var far = Char("B", 13 * Tile, 10 * Tile);           // 3 tiles: in sword range

        var target = EnemyAi.SelectTarget(enemy, new[] { near, far }, grid);

        Assert.Same(far, target);
    }

    [Fact]
    public void SelectTarget_FallsBackToNearestAlive()
    {
        var grid = new int[20, 20];
        // Box the enemy in so nobody has LOS
        grid[9, 10] = grid[11, 10] = grid[10, 9] = grid[10, 11] = 1;
        grid[9, 9] = grid[9, 11] = grid[11, 9] = grid[11, 11] = 1;
        var enemy = Enemy(10 * Tile + 16, 10 * Tile + 16);

        var a = Char("A", 2 * Tile, 2 * Tile);
        var b = Char("B", 15 * Tile, 15 * Tile);
        var dead = Char("C", 10 * Tile, 12 * Tile);
        dead.Alive = false;

        var target = EnemyAi.SelectTarget(enemy, new[] { b, a, dead }, grid);

        // Neither has LOS; nearest alive wins. B at (15,15) vs A at (2,2):
        // enemy is at (10.5,10.5) tiles, so B (~6.4 tiles) beats A (~12 tiles).
        Assert.Same(b, target);
    }

    [Fact]
    public void SelectTarget_AllDead_ReturnsNull()
    {
        var grid = new int[5, 5];
        var dead = Char("A", 64, 64);
        dead.Alive = false;
        Assert.Null(EnemyAi.SelectTarget(Enemy(32, 32), new[] { dead }, grid));
    }

    [Fact]
    public void PlanMove_AlreadyInRange_NoWaypoints()
    {
        var grid = new int[10, 10];
        var enemy = Enemy(3 * Tile, 3 * Tile);
        var target = Char("A", 5 * Tile, 3 * Tile); // 64 px away, sword range 80

        var (waypoints, remaining) = EnemyAi.PlanMove(enemy, target, grid, GameConstants.EnemyMove);

        Assert.Empty(waypoints);
        Assert.Equal(GameConstants.EnemyMove, remaining);
    }

    [Fact]
    public void PlanMove_WalksTowardDistantTarget_UntilBudgetRunsOut()
    {
        var grid = new int[10, 30];
        var enemy = Enemy(2 * Tile + 16, 5 * Tile + 16);
        var target = Char("A", 25 * Tile + 16, 5 * Tile + 16); // far along +x

        var (waypoints, remaining) = EnemyAi.PlanMove(enemy, target, grid, GameConstants.EnemyMove);

        Assert.NotEmpty(waypoints);
        Assert.Equal(0f, remaining);

        // Total distance walked equals the budget (partial last waypoint).
        float total = 0f, px = enemy.X, py = enemy.Y;
        foreach (var (x, y) in waypoints)
        {
            total += MathF.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            px = x;
            py = y;
        }
        Assert.Equal(GameConstants.EnemyMove, total, 2);

        // It moved toward the target
        Assert.True(waypoints[^1].X > enemy.X);
    }

    [Fact]
    public void PlanMove_NoPath_ReturnsEmpty()
    {
        var grid = new int[10, 10];
        for (int r = 0; r < 10; r++) grid[r, 5] = 1; // full wall between them

        var enemy = Enemy(2 * Tile, 5 * Tile);
        var target = Char("A", 8 * Tile, 5 * Tile);

        var (waypoints, remaining) = EnemyAi.PlanMove(enemy, target, grid, GameConstants.EnemyMove);

        Assert.Empty(waypoints);
        Assert.Equal(GameConstants.EnemyMove, remaining);
    }
}
