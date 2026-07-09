namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Segment-vs-wall visibility test in logic units, replacing Phaser's
/// Geom.Intersects.LineToRectangle. A sight line is blocked if it crosses
/// (or starts/ends inside) any wall tile's rectangle.
/// </summary>
public static class LineOfSight
{
    /// <summary>
    /// True if the segment (x1,y1)-(x2,y2) touches no wall tile of the grid.
    /// Wall tiles are grid cells with value 1, each spanning Tile×Tile logic units.
    /// </summary>
    public static bool HasLineOfSight(int[,] grid, float x1, float y1, float x2, float y2)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        float tile = GameConstants.Tile;

        // Only wall tiles inside the segment's bounding box can block it.
        int c0 = Math.Max(0, (int)MathF.Floor(MathF.Min(x1, x2) / tile));
        int c1 = Math.Min(cols - 1, (int)MathF.Floor(MathF.Max(x1, x2) / tile));
        int r0 = Math.Max(0, (int)MathF.Floor(MathF.Min(y1, y2) / tile));
        int r1 = Math.Min(rows - 1, (int)MathF.Floor(MathF.Max(y1, y2) / tile));

        for (int r = r0; r <= r1; r++)
        {
            for (int c = c0; c <= c1; c++)
            {
                if (grid[r, c] != 1) continue;
                if (SegmentIntersectsRect(x1, y1, x2, y2, c * tile, r * tile, tile, tile))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True if the segment intersects or is contained in the axis-aligned
    /// rectangle at (rx, ry) with size (rw, rh). Liang-Barsky clipping.
    /// </summary>
    public static bool SegmentIntersectsRect(
        float x1, float y1, float x2, float y2,
        float rx, float ry, float rw, float rh)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float tMin = 0f, tMax = 1f;

        // Each pass clips the segment's parameter range [tMin, tMax] against one slab.
        if (!ClipSlab(-dx, x1 - rx, ref tMin, ref tMax)) return false;        // left
        if (!ClipSlab(dx, rx + rw - x1, ref tMin, ref tMax)) return false;    // right
        if (!ClipSlab(-dy, y1 - ry, ref tMin, ref tMax)) return false;        // top
        if (!ClipSlab(dy, ry + rh - y1, ref tMin, ref tMax)) return false;    // bottom

        return true;
    }

    private static bool ClipSlab(float p, float q, ref float tMin, ref float tMax)
    {
        if (p == 0f) return q >= 0f; // parallel: inside the slab or not at all

        float t = q / p;
        if (p < 0f)
        {
            if (t > tMax) return false;
            if (t > tMin) tMin = t;
        }
        else
        {
            if (t < tMin) return false;
            if (t < tMax) tMax = t;
        }
        return true;
    }
}
