using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The marching breadcrumb trail: followers target points measured by
/// arc-length back along the leader's walked path.
/// </summary>
public class MarchingLineTests
{
    private const int Precision = 3;

    /// <summary>Walk the leader along a polyline in small steps, like frame updates would.</summary>
    private static MarchingLine Walk(params (float X, float Y)[] corners)
    {
        var line = new MarchingLine();
        var (x, y) = corners[0];
        line.SetLeader(x, y);

        for (int i = 1; i < corners.Length; i++)
        {
            var (tx, ty) = corners[i];
            float dx = tx - x, dy = ty - y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            int steps = Math.Max(1, (int)(dist / 2f)); // 2-unit steps
            for (int s = 1; s <= steps; s++)
                line.SetLeader(x + dx * s / steps, y + dy * s / steps);
            (x, y) = (tx, ty);
        }

        return line;
    }

    [Fact]
    public void PointBehind_StraightWalk_LandsOnPath()
    {
        var line = Walk((0, 0), (100, 0));

        var p = line.PointBehind(30f);
        Assert.NotNull(p);
        Assert.Equal(70f, p.Value.X, Precision);
        Assert.Equal(0f, p.Value.Y, Precision);
    }

    [Fact]
    public void PointBehind_TrailTooShort_ReturnsNull()
    {
        var line = Walk((0, 0), (20, 0));

        Assert.Null(line.PointBehind(30f));
    }

    [Fact]
    public void PointBehind_CorneredPath_FollowsTheCorner()
    {
        // East 50, then south 50. 60 units back from the head crosses the
        // corner at (50, 0) and continues ~10 along the east leg. Breadcrumbs
        // sample every 4 units, so allow one step of corner-cut error.
        var line = Walk((0, 0), (50, 0), (50, 50));

        var p = line.PointBehind(60f);
        Assert.NotNull(p);
        Assert.InRange(p.Value.X, 36f, 44f);
        Assert.InRange(p.Value.Y, -4f, 4f);
    }

    [Fact]
    public void Prune_LongWalk_KeepsEnoughForDeepestRank()
    {
        var line = Walk((0, 0), (1000, 0));

        // Three ranks deep must still resolve after a very long walk.
        var p = line.PointBehind(3 * MarchingLine.Spacing);
        Assert.NotNull(p);
        Assert.Equal(1000f - 3 * MarchingLine.Spacing, p.Value.X, Precision);
        Assert.Equal(0f, p.Value.Y, Precision);
    }

    [Fact]
    public void Reset_ForgetsTheOldPath()
    {
        var line = Walk((0, 0), (100, 0));
        line.Reset();
        line.SetLeader(200f, 200f);

        Assert.Null(line.PointBehind(30f));
    }
}
