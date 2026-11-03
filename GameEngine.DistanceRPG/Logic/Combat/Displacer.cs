namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The geometry of a shove (§1.2, §1.7): which way a displaced actor goes and
/// which tile it steps onto next. Displacement never teleports — the turn
/// system walks an actor through <see cref="NextTile"/> one tile at a time,
/// notifying the move path at every step so each threat zone crossed fires —
/// and it stops early rather than overlapping anything: a wall, the edge of
/// the map or a tile another living actor holds ends the shove, through the
/// same anti-stacking mask the enemy planner routes around.
/// </summary>
public static class Displacer
{
    /// <summary>The units-to-tiles conversion a shove's "one tile" goes through, named for what it converts.</summary>
    private const float UnitsPerTile = GameConstants.LogicUnitsPerTile;

    /// <summary>
    /// The tile <paramref name="mover"/> would step onto next under
    /// <paramref name="d"/>, or null when that tile is off the grid, a wall,
    /// or held by another living actor (<paramref name="occupied"/>, which
    /// never needs to include the mover's own tile).
    /// </summary>
    public static (int R, int C)? NextTile(ActorState mover, Displacement d, int[,] grid, ISet<(int R, int C)> occupied)
    {
        ArgumentNullException.ThrowIfNull(mover);
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(occupied);

        var (r, c) = TileOf(mover);
        int nr = r + d.DirRow, nc = c + d.DirCol;
        if (nr < 0 || nr >= grid.GetLength(0) || nc < 0 || nc >= grid.GetLength(1))
            return null;

        var blocked = occupied as IReadOnlyCollection<(int R, int C)> ?? occupied.ToArray();
        var masked = EnemyAi.MaskBlocked(grid, blocked, r, c);
        return masked[nr, nc] != 0 ? null : (nr, nc);
    }

    /// <summary>
    /// The unit step along the dominant axis from <paramref name="from"/> to
    /// <paramref name="to"/> — a push's direction, reversed for a pull — as
    /// (row, column): 4-directional, never diagonal. Null when the two centres
    /// coincide; a tie goes to the column.
    /// </summary>
    public static (int DirRow, int DirCol)? Direction(ActorState from, ActorState to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        if (dx == 0f && dy == 0f) return null;
        return MathF.Abs(dx) >= MathF.Abs(dy)
            ? (0, dx > 0f ? 1 : -1)
            : (dy > 0f ? 1 : -1, 0);
    }

    /// <summary>The tile an actor's centre stands in: its position over <see cref="GameConstants.LogicUnitsPerTile"/>.</summary>
    public static (int R, int C) TileOf(ActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return ((int)MathF.Floor(actor.Y / UnitsPerTile), (int)MathF.Floor(actor.X / UnitsPerTile));
    }

    /// <summary>The centre of a tile in logic units — where a displaced actor is set down.</summary>
    public static (float X, float Y) CentreOf(int r, int c)
        => (c * UnitsPerTile + UnitsPerTile / 2f, r * UnitsPerTile + UnitsPerTile / 2f);
}
