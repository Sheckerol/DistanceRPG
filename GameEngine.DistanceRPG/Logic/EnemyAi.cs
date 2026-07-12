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
        => PlanApproach(enemy, target.X, target.Y, target.Radius, enemy.Weapon.Range, grid, budget, blockedTiles);

    /// <summary>
    /// Plan a budget-limited move to bring the enemy within <paramref name="rangeUnits"/>
    /// (surface-to-surface) and line of sight of a point — used both to close on
    /// an attack target and for a healer to reach a wounded ally.
    /// </summary>
    public static (List<(float X, float Y)> Waypoints, float RemainingBudget) PlanApproach(
        EnemyState enemy, float targetX, float targetY, float targetRadius, int rangeUnits,
        int[,] grid, float budget, IReadOnlyCollection<(int R, int C)>? blockedTiles = null)
    {
        var waypoints = new List<(float X, float Y)>();

        // Already in range with LOS: no movement needed.
        float dx0 = targetX - enemy.X, dy0 = targetY - enemy.Y;
        float d0 = MathF.Sqrt(dx0 * dx0 + dy0 * dy0);
        if (d0 - enemy.Radius - targetRadius <= rangeUnits
            && LineOfSight.HasLineOfSight(grid, enemy.X, enemy.Y, targetX, targetY))
            return (waypoints, budget);

        float tile = GameConstants.Tile;
        int enemyR = (int)MathF.Floor(enemy.Y / tile);
        int enemyC = (int)MathF.Floor(enemy.X / tile);
        int targetR = (int)MathF.Floor(targetY / tile);
        int targetC = (int)MathF.Floor(targetX / tile);

        grid = MaskBlocked(grid, blockedTiles, enemyR, enemyC);

        int rangeTiles = Math.Max(1, (int)MathF.Floor(rangeUnits / tile));
        var path = Pathfinder.FindPath(grid, enemyR, enemyC, targetR, targetC, rangeTiles);
        if (path == null || path.Count == 0)
            return (waypoints, budget);

        return WalkTilePath(enemy.X, enemy.Y, path, budget);
    }

    /// <summary>
    /// Plan a budget-limited retreat: breadth-first over reachable floor within
    /// budget, pick the tile that maximises distance to the nearest living party
    /// member, and walk toward it. Used by a lone healer with no ally to mend.
    /// </summary>
    public static (List<(float X, float Y)> Waypoints, float RemainingBudget) PlanFlee(
        EnemyState enemy, IReadOnlyList<PartyMemberState> party, int[,] grid, float budget,
        IReadOnlyCollection<(int R, int C)>? blockedTiles = null)
    {
        var empty = new List<(float X, float Y)>();
        var threats = party.Where(p => p.Alive).ToList();
        if (threats.Count == 0) return (empty, budget);

        float tile = GameConstants.Tile;
        int startR = (int)MathF.Floor(enemy.Y / tile);
        int startC = (int)MathF.Floor(enemy.X / tile);
        grid = MaskBlocked(grid, blockedTiles, startR, startC);
        int rows = grid.GetLength(0), cols = grid.GetLength(1);

        // Reach is bounded by budget; a tile of slack lets the last partial step
        // still count. BFS records parents so we can rebuild the path.
        int maxDepth = (int)MathF.Ceiling(budget / tile) + 1;
        var parent = new Dictionary<(int R, int C), (int R, int C)> { [(startR, startC)] = (startR, startC) };
        var depth = new Dictionary<(int R, int C), int> { [(startR, startC)] = 0 };
        var queue = new Queue<(int R, int C)>();
        queue.Enqueue((startR, startC));

        (int R, int C) best = (startR, startC);
        float bestScore = NearestThreatDistSq(startR, startC, threats);

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            float score = NearestThreatDistSq(r, c, threats);
            if (score > bestScore) { bestScore = score; best = (r, c); }
            if (depth[(r, c)] >= maxDepth) continue;

            foreach (var (nr, nc) in new[] { (r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1) })
            {
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                if (grid[nr, nc] != 0 || parent.ContainsKey((nr, nc))) continue;
                parent[(nr, nc)] = (r, c);
                depth[(nr, nc)] = depth[(r, c)] + 1;
                queue.Enqueue((nr, nc));
            }
        }

        if (best == (startR, startC)) return (empty, budget); // nowhere safer to go

        var path = new List<(int R, int C)>();
        for (var t = best; t != (startR, startC); t = parent[t]) path.Add(t);
        path.Reverse();

        return WalkTilePath(enemy.X, enemy.Y, path, budget);
    }

    /// <summary>
    /// The most-wounded living ally (another enemy below full HP) a healer should
    /// mend, by HP fraction; null when no ally needs healing.
    /// </summary>
    public static EnemyState? SelectHealTarget(EnemyState healer, IReadOnlyList<EnemyState> enemies)
    {
        EnemyState? best = null;
        float bestFrac = 1f;
        foreach (var e in enemies)
        {
            if (e == healer || !e.Alive || e.Hp >= e.MaxHp) continue;
            float frac = (float)e.Hp / e.MaxHp;
            if (best == null || frac < bestFrac) { best = e; bestFrac = frac; }
        }
        return best;
    }

    /// <summary>True when the healer has at least one other living enemy to support.</summary>
    public static bool HasLivingAlly(EnemyState healer, IReadOnlyList<EnemyState> enemies)
        => enemies.Any(e => e != healer && e.Alive);

    /// <summary>Can the healer cast on <paramref name="ally"/> right now (range + LOS)?</summary>
    public static bool CanHealFrom(EnemyState healer, EnemyState ally, int[,] grid)
        => CombatRules.InAttackRange(healer.X, healer.Y, healer.Radius, ally.X, ally.Y, ally.Radius, healer.Weapon)
            && LineOfSight.HasLineOfSight(grid, healer.X, healer.Y, ally.X, ally.Y);

    /// <summary>
    /// Mask actor-occupied tiles as walls on a copy — the Pathfinder itself must
    /// stay JS-identical (golden tests), so it never learns about actors. The
    /// mover's own tile is never masked.
    /// </summary>
    private static int[,] MaskBlocked(int[,] grid, IReadOnlyCollection<(int R, int C)>? blocked, int selfR, int selfC)
    {
        if (blocked is not { Count: > 0 }) return grid;
        grid = (int[,])grid.Clone();
        int rows = grid.GetLength(0), cols = grid.GetLength(1);
        foreach (var (r, c) in blocked)
            if (r >= 0 && r < rows && c >= 0 && c < cols && (r, c) != (selfR, selfC))
                grid[r, c] = 1;
        return grid;
    }

    /// <summary>
    /// Walk a tile path from a start position, spending budget by distance and
    /// clipping the final step to what's left (a partial waypoint when &gt; 1 unit
    /// remains). Returns the waypoints and the leftover budget.
    /// </summary>
    private static (List<(float X, float Y)>, float) WalkTilePath(
        float startX, float startY, IReadOnlyList<(int R, int C)> path, float budget)
    {
        float tile = GameConstants.Tile;
        var waypoints = new List<(float X, float Y)>();
        float prevX = startX, prevY = startY;
        foreach (var (r, c) in path)
        {
            float tx = c * tile + tile / 2f;
            float ty = r * tile + tile / 2f;
            float dx = tx - prevX, dy = ty - prevY;
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

    private static float NearestThreatDistSq(int r, int c, IReadOnlyList<PartyMemberState> threats)
    {
        float x = c * GameConstants.Tile + GameConstants.Tile / 2f;
        float y = r * GameConstants.Tile + GameConstants.Tile / 2f;
        float best = float.MaxValue;
        foreach (var p in threats)
        {
            float dx = x - p.X, dy = y - p.Y;
            best = MathF.Min(best, dx * dx + dy * dy);
        }
        return best;
    }

    private static float DistSq(EnemyState enemy, PartyMemberState c)
    {
        float dx = enemy.X - c.X;
        float dy = enemy.Y - c.Y;
        return dx * dx + dy * dy;
    }
}
