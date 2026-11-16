namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Breadcrumb trail behind the marching leader, used to keep the party in a
/// single-file line outside combat. The leader's walked path is sampled into
/// short segments; each follower targets the point a fixed arc-length behind
/// the leader along that path, so the line snakes through doorways exactly
/// where the leader walked instead of cutting corners into walls.
/// </summary>
public sealed class MarchingLine
{
    /// <summary>Arc-length between marchers, in logic units (just under a tile).</summary>
    public const float Spacing = 30f;

    private const float BreadcrumbStep = 4f;

    /// <summary>Longest path ever needed: three followers deep, plus slack.</summary>
    private const float MaxKeptLength = 3 * Spacing + 2 * BreadcrumbStep;

    private readonly List<(float X, float Y)> _trail = new(); // oldest → newest
    private float _headX, _headY;
    private bool _hasHead;

    public void Reset()
    {
        _trail.Clear();
        _hasHead = false;
    }

    /// <summary>Advance the leader's position, dropping breadcrumbs as they move.</summary>
    public void SetLeader(float x, float y)
    {
        if (!_hasHead)
        {
            _trail.Add((x, y));
            _hasHead = true;
        }
        else
        {
            var (lx, ly) = _trail[^1];
            float dx = x - lx, dy = y - ly;
            if (dx * dx + dy * dy >= BreadcrumbStep * BreadcrumbStep)
            {
                _trail.Add((x, y));
                Prune();
            }
        }

        _headX = x;
        _headY = y;
    }

    /// <summary>
    /// The point <paramref name="back"/> arc-units behind the leader along the
    /// walked path, or null while the trail is still shorter than that.
    /// </summary>
    public (float X, float Y)? PointBehind(float back)
    {
        if (!_hasHead) return null;

        float px = _headX, py = _headY;
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            var (qx, qy) = _trail[i];
            float dx = qx - px, dy = qy - py;
            float seg = MathF.Sqrt(dx * dx + dy * dy);
            if (seg >= back)
            {
                float t = seg > 0f ? back / seg : 0f;
                return (px + dx * t, py + dy * t);
            }

            back -= seg;
            px = qx;
            py = qy;
        }

        return null;
    }

    /// <summary>
    /// Where a follower heads next to reach its place in the line (PHASE 8
    /// DECISION 13): the place itself while the straight line to it is clear of
    /// walls; otherwise the furthest tile centre of the shortest path to it
    /// (<see cref="Pathfinder.FindPath"/>) that it can walk straight to — so a member
    /// the fight left in another room walks round to the line rather than into the
    /// wall between. With no path at all, the place itself: the walls stop it as
    /// they always did.
    /// </summary>
    /// <param name="grid">The floor's tiles.</param>
    /// <param name="from">The follower, in logic units.</param>
    /// <param name="target">Its place in the line, in logic units.</param>
    public static (float X, float Y) Waypoint(int[,] grid, (float X, float Y) from, (float X, float Y) target)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (LineOfSight.HasLineOfSight(grid, from.X, from.Y, target.X, target.Y)) return target;

        var start = TileOf(from);
        var goal = TileOf(target);
        var path = Pathfinder.FindPath(grid, start.R, start.C, goal.R, goal.C);
        if (path is null || path.Count == 0) return target;

        var next = Centre(path[0]);   // the first step is a neighbour: walkable whatever the sight says
        for (int i = 1; i < path.Count; i++)
        {
            var centre = Centre(path[i]);
            if (!LineOfSight.HasLineOfSight(grid, from.X, from.Y, centre.X, centre.Y)) break;
            next = centre;
        }
        return next;

        static (int R, int C) TileOf((float X, float Y) p)
            => ((int)MathF.Floor(p.Y / GameConstants.Tile), (int)MathF.Floor(p.X / GameConstants.Tile));
        static (float X, float Y) Centre((int R, int C) t)
            => ((t.C + 0.5f) * GameConstants.Tile, (t.R + 0.5f) * GameConstants.Tile);
    }

    private void Prune()
    {
        // Walk back from the head; once the kept length is exceeded, drop
        // everything older — but keep the first breadcrumb past the cut so
        // PointBehind can still interpolate into that final segment.
        float px = _headX, py = _headY, length = 0f;
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            var (qx, qy) = _trail[i];
            float dx = qx - px, dy = qy - py;
            length += MathF.Sqrt(dx * dx + dy * dy);
            if (length > MaxKeptLength)
            {
                _trail.RemoveRange(0, i);
                return;
            }

            px = qx;
            py = qy;
        }
    }
}
