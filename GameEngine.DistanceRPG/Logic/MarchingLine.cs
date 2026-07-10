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
