namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// 2D circle-vs-tile-grid collision in logic space, replacing Phaser's arcade
/// physics. Movement is resolved one axis at a time (X then Y), which gives
/// the classic wall-sliding feel: a head-on push stops at the wall, a diagonal
/// push slides along it. Other actors block as circles the same way.
/// </summary>
public static class GridCollision
{
    /// <summary>A circular blocker (another character, the enemy).</summary>
    public readonly record struct Circle(float X, float Y, float R);

    /// <summary>
    /// Move a circle of radius <paramref name="r"/> from (x, y) by (dx, dy),
    /// sliding along wall tiles and blockers. Returns the final position.
    /// </summary>
    public static (float X, float Y) Move(
        int[,] grid, float x, float y, float r, float dx, float dy,
        IReadOnlyList<Circle>? blockers = null)
    {
        float worldW = grid.GetLength(1) * GameConstants.Tile;
        float worldH = grid.GetLength(0) * GameConstants.Tile;

        // X pass
        x += dx;
        x = Math.Clamp(x, r, worldW - r);
        x = ResolveWalls(grid, x, y, r, alongX: true, movedPositive: dx > 0);
        if (blockers != null) x = ResolveBlockersX(blockers, x, y, r);

        // Y pass
        y += dy;
        y = Math.Clamp(y, r, worldH - r);
        y = ResolveWalls(grid, x, y, r, alongX: false, movedPositive: dy > 0);
        if (blockers != null) y = ResolveBlockersY(blockers, x, y, r);

        return (x, y);
    }

    /// <summary>
    /// Push the circle out of any overlapping wall tile along one axis.
    /// The perpendicular axis is left alone — its pass handles its own overlaps.
    /// </summary>
    private static float ResolveWalls(int[,] grid, float x, float y, float r, bool alongX, bool movedPositive)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        float tile = GameConstants.Tile;

        int c0 = Math.Max(0, (int)MathF.Floor((x - r) / tile));
        int c1 = Math.Min(cols - 1, (int)MathF.Floor((x + r) / tile));
        int r0 = Math.Max(0, (int)MathF.Floor((y - r) / tile));
        int r1 = Math.Min(rows - 1, (int)MathF.Floor((y + r) / tile));

        for (int tr = r0; tr <= r1; tr++)
        {
            for (int tc = c0; tc <= c1; tc++)
            {
                if (grid[tr, tc] != 1) continue;

                float x0 = tc * tile, x1 = x0 + tile;
                float y0 = tr * tile, y1 = y0 + tile;

                float closestX = Math.Clamp(x, x0, x1);
                float closestY = Math.Clamp(y, y0, y1);
                float ox = x - closestX;
                float oy = y - closestY;
                float d2 = ox * ox + oy * oy;
                if (d2 >= r * r) continue;

                if (alongX)
                {
                    if (ox == 0f && oy == 0f)
                    {
                        // Centre inside the tile: back out the way we came.
                        x = movedPositive ? x0 - r : x1 + r;
                    }
                    else if (ox != 0f)
                    {
                        // Side or corner contact: place the centre so the
                        // horizontal gap to the closest point is exactly enough
                        // for radius r given the vertical offset.
                        float needed = MathF.Sqrt(r * r - oy * oy);
                        x = ox > 0f ? closestX + needed : closestX - needed;
                    }
                    // ox == 0, oy != 0: vertical contact — the Y pass owns it.
                }
                else
                {
                    if (ox == 0f && oy == 0f)
                    {
                        y = movedPositive ? y0 - r : y1 + r;
                    }
                    else if (oy != 0f)
                    {
                        float needed = MathF.Sqrt(r * r - ox * ox);
                        y = oy > 0f ? closestY + needed : closestY - needed;
                    }
                }
            }
        }

        return alongX ? x : y;
    }

    private static float ResolveBlockersX(IReadOnlyList<Circle> blockers, float x, float y, float r)
    {
        foreach (var b in blockers)
        {
            float sum = r + b.R;
            float dy = y - b.Y;
            if (Math.Abs(dy) >= sum) continue;
            float dx = x - b.X;
            if (dx * dx + dy * dy >= sum * sum) continue;

            float needed = MathF.Sqrt(sum * sum - dy * dy);
            x = dx >= 0f ? b.X + needed : b.X - needed;
        }
        return x;
    }

    private static float ResolveBlockersY(IReadOnlyList<Circle> blockers, float x, float y, float r)
    {
        foreach (var b in blockers)
        {
            float sum = r + b.R;
            float dx = x - b.X;
            if (Math.Abs(dx) >= sum) continue;
            float dy = y - b.Y;
            if (dx * dx + dy * dy >= sum * sum) continue;

            float needed = MathF.Sqrt(sum * sum - dx * dx);
            y = dy >= 0f ? b.Y + needed : b.Y - needed;
        }
        return y;
    }
}
