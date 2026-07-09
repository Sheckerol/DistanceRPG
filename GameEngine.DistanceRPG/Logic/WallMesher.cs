namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Merges contiguous wall tiles into larger rectangles so the 3D scene can
/// render a few hundred wall boxes instead of one per tile (the 50×70 grid is
/// mostly wall). Greedy: grow a run rightward, then extend it downward while
/// every tile in the widened row is still an unclaimed wall.
/// </summary>
public static class WallMesher
{
    public static List<MapRect> MergeWallTiles(int[,] grid)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        var claimed = new bool[rows, cols];
        var rects = new List<MapRect>();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (grid[r, c] != 1 || claimed[r, c]) continue;

                // Grow right
                int w = 1;
                while (c + w < cols && grid[r, c + w] == 1 && !claimed[r, c + w]) w++;

                // Grow down while the whole row segment is unclaimed wall
                int h = 1;
                while (r + h < rows && RowIsClaimableWall(grid, claimed, r + h, c, w)) h++;

                for (int rr = r; rr < r + h; rr++)
                    for (int cc = c; cc < c + w; cc++)
                        claimed[rr, cc] = true;

                rects.Add(new MapRect(c, r, w, h));
            }
        }

        return rects;
    }

    private static bool RowIsClaimableWall(int[,] grid, bool[,] claimed, int row, int c, int w)
    {
        for (int cc = c; cc < c + w; cc++)
            if (grid[row, cc] != 1 || claimed[row, cc])
                return false;
        return true;
    }
}
