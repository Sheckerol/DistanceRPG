namespace GameEngine.DistanceRPG.Logic;

public static class GridUtils
{
    /// <summary>
    /// Find <paramref name="count"/> open floor tiles for the party, starting
    /// at the spawn tile and spiralling outward ring by ring (Chebyshev rings,
    /// max radius 7), matching the prototype's spawn placement. Pads with the
    /// first tile found if not enough open tiles are found.
    /// <para>
    /// <paramref name="blocked"/> names tiles nobody may be put on — the living
    /// enemies of a floor the party is arriving on (§4.2): a dummy may stand on
    /// the very stairs the party arrives by, and a member is placed around it,
    /// never inside it. The start tile itself is skipped when it is blocked, and
    /// the padding is then the first free tile the rings found. Without a set —
    /// or with one naming nothing near — the tiles are exactly the prototype's.
    /// </para>
    /// </summary>
    public static List<(int R, int C)> FindPartySpawnTiles(int[,] grid, int startR, int startC, int count,
        IReadOnlySet<(int R, int C)>? blocked = null)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        var tiles = new List<(int R, int C)>();
        if (blocked?.Contains((startR, startC)) != true)
            tiles.Add((startR, startC));
        var seen = new HashSet<(int, int)> { (startR, startC) };

        for (int ring = 1; tiles.Count < count && ring < 8; ring++)
        {
            for (int dr = -ring; dr <= ring && tiles.Count < count; dr++)
            {
                for (int dc = -ring; dc <= ring && tiles.Count < count; dc++)
                {
                    if (Math.Max(Math.Abs(dr), Math.Abs(dc)) != ring) continue;
                    int r = startR + dr;
                    int c = startC + dc;
                    if (!seen.Add((r, c))) continue;
                    if (r < 0 || c < 0 || r >= rows || c >= cols) continue;
                    if (grid[r, c] != 0) continue;
                    if (blocked?.Contains((r, c)) == true) continue;
                    tiles.Add((r, c));
                }
            }
        }

        var pad = tiles.Count > 0 ? tiles[0] : (startR, startC);
        while (tiles.Count < count) tiles.Add(pad);
        return tiles;
    }

    /// <summary>
    /// The nearest floor tile to <paramref name="start"/> that
    /// <paramref name="taken"/> does not claim, breadth-first — up, down, left,
    /// right, crossing floor tiles only, so the answer stays in the start's room
    /// or corridor — and the start itself whenever it is free. The neighbour order
    /// is fixed, so a tie is always settled the same way and never by a set's
    /// iteration. Falls back to the start when everything it can reach is taken.
    /// <para>
    /// The one search for "stand here, or as near as you can": a revived dummy
    /// wakes through it (<see cref="TurnSystem"/>, off whoever stands on its
    /// remnant), and a boss whose lair a placed dummy already holds waits on the
    /// tile it finds (<see cref="Floor"/>'s generation).
    /// </para>
    /// </summary>
    /// <param name="grid">The floor's grid: 0 is floor, anything else a wall.</param>
    /// <param name="start">Where the actor would stand.</param>
    /// <param name="taken">Whether a floor tile is held by someone else.</param>
    public static (int R, int C) NearestFreeTile(int[,] grid, (int R, int C) start, Func<(int R, int C), bool> taken)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(taken);

        int rows = grid.GetLength(0), cols = grid.GetLength(1);
        var visited = new HashSet<(int R, int C)> { start };
        var queue = new Queue<(int R, int C)>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            if (grid[r, c] == 0 && !taken((r, c))) return (r, c);

            foreach (var (nr, nc) in new[] { (r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1) })
            {
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                if (grid[nr, nc] != 0 || !visited.Add((nr, nc))) continue;
                queue.Enqueue((nr, nc));
            }
        }

        return start;
    }

    /// <summary>
    /// True if every floor tile is reachable from (startR, startC) — used by
    /// tests to prove generated maps are fully connected.
    /// </summary>
    public static bool IsFullyConnected(int[,] grid, int startR, int startC)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        var visited = new bool[rows, cols];
        var queue = new Queue<(int R, int C)>();
        visited[startR, startC] = true;
        queue.Enqueue((startR, startC));

        int reached = 0;
        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            reached++;
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int nr = r + dr, nc = c + dc;
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                if (visited[nr, nc] || grid[nr, nc] != 0) continue;
                visited[nr, nc] = true;
                queue.Enqueue((nr, nc));
            }
        }

        int floorTiles = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (grid[r, c] == 0) floorTiles++;

        return reached == floorTiles;
    }
}
