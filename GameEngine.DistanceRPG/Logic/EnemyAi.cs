namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Enemy decision-making ported from the prototype: pick the best target
/// (prefer hittable, then visible, then nearest) and plan a budget-limited
/// move along the A* path toward it.
/// </summary>
public static class EnemyAi
{
    /// <summary>Surface-to-surface range plus line-of-sight check.</summary>
    public static bool CanHit(EnemyState enemy, PartyMemberState target, Weapon weapon, int[,] grid)
    {
        if (!CombatRules.InAttackRange(enemy.X, enemy.Y, enemy.Radius, target.X, target.Y, target.Radius, weapon))
            return false;
        return LineOfSight.HasLineOfSight(grid, enemy.X, enemy.Y, target.X, target.Y);
    }

    /// <summary>Symmetric check for a party member attacking the enemy.</summary>
    public static bool CharCanHit(PartyMemberState c, EnemyState enemy, Weapon? weapon, int[,] grid)
    {
        if (weapon == null) return false;
        if (!CombatRules.InAttackRange(c.X, c.Y, c.Radius, enemy.X, enemy.Y, enemy.Radius, weapon))
            return false;
        return LineOfSight.HasLineOfSight(grid, c.X, c.Y, enemy.X, enemy.Y);
    }

    /// <summary>
    /// Target priority: in weapon range with LOS → any LOS → anyone alive;
    /// nearest by centre distance within the chosen pool.
    /// </summary>
    public static PartyMemberState? SelectTarget(EnemyState enemy, IReadOnlyList<PartyMemberState> party, int[,] grid)
    {
        var alive = party.Where(c => c.Alive).ToList();
        if (alive.Count == 0) return null;

        var inRangeLos = alive.Where(c => CanHit(enemy, c, enemy.Weapon, grid)).ToList();
        var pool = inRangeLos.Count > 0
            ? inRangeLos
            : alive.Where(c => LineOfSight.HasLineOfSight(grid, enemy.X, enemy.Y, c.X, c.Y)).ToList() is { Count: > 0 } withLos
                ? withLos
                : alive;

        PartyMemberState best = pool[0];
        float bestDist = DistSq(enemy, best);
        for (int i = 1; i < pool.Count; i++)
        {
            float d = DistSq(enemy, pool[i]);
            if (d < bestDist)
            {
                best = pool[i];
                bestDist = d;
            }
        }
        return best;
    }

    /// <summary>
    /// Plan the enemy's move toward the target: A* to within weapon range,
    /// then walk the path until the movement budget runs out (with a partial
    /// final waypoint if more than 1 unit of budget remains).
    /// Returns the waypoints and the budget left after moving.
    /// <paramref name="blockedTiles"/> are treated as walls (tiles occupied
    /// by other actors), so enemies queue and fan out instead of piling onto
    /// the same spot; a fully boxed-in enemy simply stays put.
    /// </summary>
    public static (List<(float X, float Y)> Waypoints, float RemainingBudget) PlanMove(
        EnemyState enemy, PartyMemberState target, int[,] grid, float budget,
        IReadOnlyCollection<(int R, int C)>? blockedTiles = null)
    {
        var waypoints = new List<(float X, float Y)>();

        // Already in range with LOS: no movement needed.
        if (CanHit(enemy, target, enemy.Weapon, grid))
            return (waypoints, budget);

        float tile = GameConstants.Tile;
        int enemyR = (int)MathF.Floor(enemy.Y / tile);
        int enemyC = (int)MathF.Floor(enemy.X / tile);
        int targetR = (int)MathF.Floor(target.Y / tile);
        int targetC = (int)MathF.Floor(target.X / tile);

        // Mask occupied tiles as walls on a copy — the Pathfinder itself must
        // stay JS-identical (golden tests), so it never learns about actors.
        if (blockedTiles is { Count: > 0 })
        {
            grid = (int[,])grid.Clone();
            int rows = grid.GetLength(0), cols = grid.GetLength(1);
            foreach (var (r, c) in blockedTiles)
                if (r >= 0 && r < rows && c >= 0 && c < cols && (r, c) != (enemyR, enemyC))
                    grid[r, c] = 1;
        }

        int weaponRangeTiles = Math.Max(1, (int)MathF.Floor(enemy.Weapon.Range / tile));
        var path = Pathfinder.FindPath(grid, enemyR, enemyC, targetR, targetC, weaponRangeTiles);
        if (path == null || path.Count == 0)
            return (waypoints, budget);

        float prevX = enemy.X;
        float prevY = enemy.Y;
        foreach (var (r, c) in path)
        {
            float tx = c * tile + tile / 2f;
            float ty = r * tile + tile / 2f;
            float dx = tx - prevX;
            float dy = ty - prevY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > budget)
            {
                if (budget > 1f)
                {
                    float frac = budget / dist;
                    waypoints.Add((prevX + dx * frac, prevY + dy * frac));
                }
                budget = 0f;
                break;
            }
            budget -= dist;
            waypoints.Add((tx, ty));
            prevX = tx;
            prevY = ty;
        }

        return (waypoints, budget);
    }

    private static float DistSq(EnemyState enemy, PartyMemberState c)
    {
        float dx = enemy.X - c.X;
        float dy = enemy.Y - c.Y;
        return dx * dx + dy * dy;
    }
}
