namespace GameEngine.DistanceRPG.Logic;

public static class GridUtils
{
    /// <summary>
    /// Find <paramref name="count"/> open floor tiles for the party, starting
    /// at the spawn tile and spiralling outward ring by ring (Chebyshev rings,
    /// max radius 7), matching the prototype's spawn placement. Pads with the
    /// start tile if not enough open tiles are found.
    /// </summary>
    public static List<(int R, int C)> FindPartySpawnTiles(int[,] grid, int startR, int startC, int count)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        var tiles = new List<(int R, int C)> { (startR, startC) };
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
                    tiles.Add((r, c));
                }
            }
        }

        while (tiles.Count < count) tiles.Add((startR, startC));
        return tiles;
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
