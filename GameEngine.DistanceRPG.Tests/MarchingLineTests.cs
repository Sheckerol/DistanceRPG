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
    // ── Walking into formation (PHASE 8 DECISION 13) ─────────────────────────

    /// <summary>A 10×12 room split by a wall down column 5, open only at the bottom row (row 8).</summary>
    private static int[,] SplitRoom()
    {
        var grid = new int[10, 12];
        for (int r = 0; r < 10; r++)
        for (int c = 0; c < 12; c++)
            if (r == 0 || c == 0 || r == 9 || c == 11 || (c == 5 && r < 8)) grid[r, c] = 1;
        return grid;
    }

    private static (float X, float Y) At(int r, int c) => ((c + 0.5f) * GameConstants.Tile, (r + 0.5f) * GameConstants.Tile);

    [Fact]
    public void Waypoint_InTheClear_IsThePlaceItself()
    {
        var grid = SplitRoom();
        var target = (X: At(2, 3).X + 5f, Y: At(2, 3).Y);
        Assert.Equal(target, MarchingLine.Waypoint(grid, At(6, 2), target));
    }

    [Fact]
    public void Waypoint_BehindAWall_WalksRoundIt()
    {
        // Follower top-left, its place top-right: the wall between, the way round at the bottom.
        var grid = SplitRoom();
        var from = At(2, 2);
        var target = At(2, 8);
        var waypoint = MarchingLine.Waypoint(grid, from, target);

        Assert.NotEqual(target, waypoint);
        Assert.True(LineOfSight.HasLineOfSight(grid, from.X, from.Y, waypoint.X, waypoint.Y));   // walkable straight to
        int row = (int)(waypoint.Y / GameConstants.Tile), col = (int)(waypoint.X / GameConstants.Tile);
        Assert.Equal(0, grid[row, col]);
        Assert.True(col < 5, "still on the follower's side: the wall is passed at the bottom, not through");
        Assert.True(row > 2, "headed down, towards the gap");
    }

    [Fact]
    public void Waypoint_FollowedStepByStep_ReachesThePlace()
    {
        var grid = SplitRoom();
        var at = At(2, 2);
        var target = At(2, 8);
        for (int step = 0; step < 50 && at != target; step++)
            at = MarchingLine.Waypoint(grid, at, target);
        Assert.Equal(target, at);
    }

    [Fact]
    public void Waypoint_WithNoWayRound_IsThePlace_AndTheWallsStopIt()
    {
        var grid = SplitRoom();
        grid[8, 5] = 1;   // the gap closed
        var target = At(2, 8);
        Assert.Equal(target, MarchingLine.Waypoint(grid, At(2, 2), target));
    }
}
