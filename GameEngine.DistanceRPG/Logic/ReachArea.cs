namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A set of points on the floor, held on a fine lattice: a node every
/// <see cref="Step"/> logic units (<see cref="NodesPerTile"/> to a tile), node
/// (row, col) sitting at (col·Step, row·Step) — so every tile's centre and
/// corners are nodes, and a one-tile corridor's centre line is a row of them.
/// What <see cref="ReachFields"/> answers in: where an actor's centre can walk
/// to this turn, or where a target's centre would be inside its reach. Logic
/// units throughout; the presentation converts at its own edge (WorldSpace).
/// </summary>
public sealed class ReachArea
{
    /// <summary>Lattice nodes per tile, along each axis.</summary>
    public const int NodesPerTile = 8;

    /// <summary>Logic units between neighbouring nodes.</summary>
    public const float Step = GameConstants.Tile / NodesPerTile;

    private readonly bool[] _in;
    private readonly float[]? _cost;

    internal ReachArea(int row0, int col0, int rows, int cols, bool[] inside, float[]? cost)
    {
        Row0 = row0;
        Col0 = col0;
        Rows = rows;
        Cols = cols;
        _in = inside;
        _cost = cost;
        foreach (bool b in inside) if (b) Count++;
    }

    /// <summary>An area holding no point at all.</summary>
    public static ReachArea Empty { get; } = new(0, 0, 0, 0, [], null);

    /// <summary>The lattice row of the window's first row (the window is the only part ever filled).</summary>
    public int Row0 { get; }
    /// <summary>The lattice column of the window's first column.</summary>
    public int Col0 { get; }
    /// <summary>The window's height in nodes.</summary>
    public int Rows { get; }
    /// <summary>The window's width in nodes.</summary>
    public int Cols { get; }
    /// <summary>How many nodes the area holds.</summary>
    public int Count { get; }

    /// <summary>Where lattice node (row, col) sits, in logic units.</summary>
    public static (float X, float Y) NodePoint(int row, int col) => (col * Step, row * Step);

    /// <summary>Does the area hold lattice node (row, col)? Anything outside the window is outside the area.</summary>
    public bool Contains(int row, int col)
    {
        int r = row - Row0, c = col - Col0;
        return r >= 0 && c >= 0 && r < Rows && c < Cols && _in[r * Cols + c];
    }

    /// <summary>Does the area hold the node nearest the logic point (x, y)?</summary>
    public bool ContainsPoint(float x, float y)
        => Contains((int)MathF.Round(y / Step), (int)MathF.Round(x / Step));

    /// <summary>
    /// The walking distance to lattice node (row, col) — for an area from
    /// <see cref="ReachFields.Walkable"/> — or +∞ where it is not in the area
    /// or the area carries no distances.
    /// </summary>
    public float CostAt(int row, int col)
    {
        if (_cost == null || !Contains(row, col)) return float.PositiveInfinity;
        return _cost[(row - Row0) * Cols + (col - Col0)];
    }
}

/// <summary>
/// How far an actor can move this turn and what it can reach from where it
/// stands, worked out from the very rules play runs on — never a guess:
/// <list type="bullet">
/// <item><see cref="Walkable"/>: the shortest walking distance to every point
/// within the budget, around walls and bodies, for a body of the actor's size.
/// Movement steers in eight directions with diagonals scaled to the same speed
/// (DungeonScene's 0.707), and the budget pays for distance actually covered,
/// so the true cost of a walk is the octile distance around the obstacles —
/// exactly what an eight-way lattice search measures. A node is open where a
/// body of the radius there touches no wall tile (<see cref="GridCollision"/>'s
/// rule, grid value 1) and no blocker, inside the map's edge.</item>
/// <item><see cref="InReach"/>: every point a target's centre could stand at
/// and be hit — surface distance within the weapon's range
/// (<see cref="CombatRules.InAttackRange"/>) and line of sight from the
/// attacker's centre (<see cref="LineOfSight"/>), the two halves of
/// <see cref="EnemyAi.CanHit"/>.</item>
/// </list>
/// An enemy walks A* tile paths rather than steering, but its tiles step the
/// same eight ways at the same octile cost, so its walkable area reads off the
/// same search.
/// </summary>
public static class ReachFields
{
    private static readonly float Diagonal = ReachArea.Step * MathF.Sqrt(2f);

    // A hair of float slack, so a budget that lands exactly on a node keeps it.
    private const float Slack = 1e-3f;

    /// <summary>
    /// Every lattice node a body of <paramref name="radius"/> at
    /// (<paramref name="x"/>, <paramref name="y"/>) can walk its centre to for
    /// at most <paramref name="budget"/> logic units, with the distance to each.
    /// </summary>
    public static ReachArea Walkable(int[,] grid, float x, float y, float radius, float budget,
        IReadOnlyList<GridCollision.Circle>? blockers = null)
    {
        if (budget < 0f || float.IsNaN(budget)) return ReachArea.Empty;

        int latRows = grid.GetLength(0) * ReachArea.NodesPerTile + 1;
        int latCols = grid.GetLength(1) * ReachArea.NodesPerTile + 1;
        int span = (int)MathF.Ceiling(budget / ReachArea.Step) + 2;
        int cRow = (int)MathF.Round(y / ReachArea.Step), cCol = (int)MathF.Round(x / ReachArea.Step);
        int row0 = Math.Max(0, cRow - span), col0 = Math.Max(0, cCol - span);
        int row1 = Math.Min(latRows - 1, cRow + span), col1 = Math.Min(latCols - 1, cCol + span);
        if (row1 < row0 || col1 < col0) return ReachArea.Empty;

        int rows = row1 - row0 + 1, cols = col1 - col0 + 1;
        int n = rows * cols;
        // Which nodes a body fits on, settled up front: every one is asked anyway.
        var open = new bool[n];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var (px, py) = ReachArea.NodePoint(row0 + r, col0 + c);
            open[r * cols + c] = BodyFits(grid, px, py, radius, blockers);
        }
        var cost = new float[n];
        Array.Fill(cost, float.PositiveInfinity);
        var done = new bool[n];
        var queue = new PriorityQueue<int, float>();

        // The actor stands between nodes: each of the four around it is a
        // straight walk away (well within one lattice step).
        int fr = (int)MathF.Floor(y / ReachArea.Step) - row0, fc = (int)MathF.Floor(x / ReachArea.Step) - col0;
        for (int dr = 0; dr <= 1; dr++)
        for (int dc = 0; dc <= 1; dc++)
        {
            int r = fr + dr, c = fc + dc;
            if (r < 0 || c < 0 || r >= rows || c >= cols || !open[r * cols + c]) continue;
            var (px, py) = ReachArea.NodePoint(row0 + r, col0 + c);
            float d = Octile(px - x, py - y);
            if (d > budget + Slack || d >= cost[r * cols + c]) continue;
            cost[r * cols + c] = d;
            queue.Enqueue(r * cols + c, d);
        }

        float limit = budget + Slack;
        while (queue.TryDequeue(out int i, out float d))
        {
            if (done[i]) continue;
            done[i] = true;
            int r = i / cols, c = i - r * cols;
            bool up = r > 0 && open[i - cols], down = r < rows - 1 && open[i + cols];
            bool left = c > 0 && open[i - 1], right = c < cols - 1 && open[i + 1];

            float straight = d + ReachArea.Step;
            if (straight <= limit)
            {
                if (up) Relax(i - cols, straight);
                if (down) Relax(i + cols, straight);
                if (left) Relax(i - 1, straight);
                if (right) Relax(i + 1, straight);
            }
            // A diagonal step past a corner would clip it: both the straight
            // steps it cuts between must be open too.
            float diagonal = d + Diagonal;
            if (diagonal <= limit)
            {
                if (up && left && open[i - cols - 1]) Relax(i - cols - 1, diagonal);
                if (up && right && open[i - cols + 1]) Relax(i - cols + 1, diagonal);
                if (down && left && open[i + cols - 1]) Relax(i + cols - 1, diagonal);
                if (down && right && open[i + cols + 1]) Relax(i + cols + 1, diagonal);
            }
        }

        void Relax(int ni, float nd)
        {
            if (done[ni] || nd >= cost[ni]) return;
            cost[ni] = nd;
            queue.Enqueue(ni, nd);
        }

        return new ReachArea(row0, col0, rows, cols, done, cost);
    }

    /// <summary>
    /// Every lattice node where a target of <paramref name="targetRadius"/>
    /// could stand (clear of the walls) and be hit by an attacker of
    /// <paramref name="attackerRadius"/> at (<paramref name="x"/>, <paramref name="y"/>)
    /// with a weapon of <paramref name="range"/>: surface distance within the
    /// range and a clear line of sight between the centres.
    /// </summary>
    public static ReachArea InReach(int[,] grid, float x, float y, float attackerRadius, float targetRadius, float range)
    {
        if (range < 0f || float.IsNaN(range)) return ReachArea.Empty;

        float centres = range + attackerRadius + targetRadius;   // the farthest centre-to-centre a hit spans
        int latRows = grid.GetLength(0) * ReachArea.NodesPerTile + 1;
        int latCols = grid.GetLength(1) * ReachArea.NodesPerTile + 1;
        int span = (int)MathF.Ceiling(centres / ReachArea.Step) + 1;
        int cRow = (int)MathF.Round(y / ReachArea.Step), cCol = (int)MathF.Round(x / ReachArea.Step);
        int row0 = Math.Max(0, cRow - span), col0 = Math.Max(0, cCol - span);
        int row1 = Math.Min(latRows - 1, cRow + span), col1 = Math.Min(latCols - 1, cCol + span);
        if (row1 < row0 || col1 < col0) return ReachArea.Empty;

        int rows = row1 - row0 + 1, cols = col1 - col0 + 1;
        var inside = new bool[rows * cols];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var (px, py) = ReachArea.NodePoint(row0 + r, col0 + c);
            if (!CombatRules.InAttackRangeOf(x, y, attackerRadius, px, py, targetRadius, range)) continue;
            if (!BodyFits(grid, px, py, targetRadius, null)) continue;
            if (!LineOfSight.HasLineOfSight(grid, x, y, px, py)) continue;
            inside[r * cols + c] = true;
        }
        return new ReachArea(row0, col0, rows, cols, inside, null);
    }

    /// <summary>
    /// Could a body of <paramref name="radius"/> stand centred at (x, y)?
    /// Inside the map's edge, touching no wall tile and overlapping no blocker —
    /// the overlaps <see cref="GridCollision.Move"/> pushes a body out of.
    /// </summary>
    public static bool BodyFits(int[,] grid, float x, float y, float radius, IReadOnlyList<GridCollision.Circle>? blockers)
    {
        int gridRows = grid.GetLength(0), gridCols = grid.GetLength(1);
        float tile = GameConstants.Tile;
        if (x < radius || y < radius || x > gridCols * tile - radius || y > gridRows * tile - radius) return false;

        int c0 = Math.Max(0, (int)MathF.Floor((x - radius) / tile));
        int c1 = Math.Min(gridCols - 1, (int)MathF.Floor((x + radius) / tile));
        int r0 = Math.Max(0, (int)MathF.Floor((y - radius) / tile));
        int r1 = Math.Min(gridRows - 1, (int)MathF.Floor((y + radius) / tile));
        for (int tr = r0; tr <= r1; tr++)
        for (int tc = c0; tc <= c1; tc++)
        {
            if (grid[tr, tc] != 1) continue;
            float ox = x - Math.Clamp(x, tc * tile, (tc + 1) * tile);
            float oy = y - Math.Clamp(y, tr * tile, (tr + 1) * tile);
            if (ox * ox + oy * oy < radius * radius) return false;
        }

        if (blockers != null)
            foreach (var b in blockers)
            {
                float dx = x - b.X, dy = y - b.Y, sum = radius + b.R;
                if (dx * dx + dy * dy < sum * sum) return false;
            }
        return true;
    }

    /// <summary>The eight-way walking distance across (dx, dy) in the open.</summary>
    public static float Octile(float dx, float dy)
    {
        float ax = MathF.Abs(dx), ay = MathF.Abs(dy);
        return MathF.Max(ax, ay) + (MathF.Sqrt(2f) - 1f) * MathF.Min(ax, ay);
    }
}
