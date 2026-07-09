using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class PathfinderTests
{
    [Fact]
    public void GoldenSeed_FullPathMatchesJs()
    {
        AssertPathMatches(GoldenData.Instance.Paths.FullPath, maxRange: 0, reversed: false);
    }

    [Fact]
    public void GoldenSeed_Range2PathMatchesJs()
    {
        AssertPathMatches(GoldenData.Instance.Paths.Range2, maxRange: 2, reversed: false);
    }

    [Fact]
    public void GoldenSeed_Range4ReversedPathMatchesJs()
    {
        AssertPathMatches(GoldenData.Instance.Paths.Range4, maxRange: 4, reversed: true);
    }

    private static void AssertPathMatches(GoldenTile[]? expected, int maxRange, bool reversed)
    {
        var golden = GoldenData.Instance;
        var map = GoldenData.GeneratedMap;
        var (fromR, fromC) = reversed ? map.EnemyStart : map.PlayerStart;
        var (toR, toC) = reversed ? map.PlayerStart : map.EnemyStart;

        var path = Pathfinder.FindPath(map.Grid, fromR, fromC, toR, toC, maxRange);

        Assert.NotNull(expected);
        Assert.NotNull(path);
        Assert.Equal(expected.Length, path.Count);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal((expected[i].R, expected[i].C), path[i]);
    }

    [Fact]
    public void SameStartAndGoal_ReturnsEmptyPath()
    {
        var grid = OpenGrid(5, 5);
        var path = Pathfinder.FindPath(grid, 2, 2, 2, 2);
        Assert.NotNull(path);
        Assert.Empty(path);
    }

    [Fact]
    public void UnreachableGoal_ReturnsNull()
    {
        var grid = OpenGrid(5, 5);
        // Wall off the right side entirely
        for (int r = 0; r < 5; r++) grid[r, 3] = 1;

        Assert.Null(Pathfinder.FindPath(grid, 2, 0, 2, 4));
    }

    [Fact]
    public void DiagonalMove_RequiresBothCardinalsOpen()
    {
        // 0 1
        // 1 0   â€” diagonal from (0,0) to (1,1) must not cut the corner
        var grid = new int[2, 2];
        grid[0, 1] = 1;
        grid[1, 0] = 1;

        Assert.Null(Pathfinder.FindPath(grid, 0, 0, 1, 1));
    }

    [Fact]
    public void PrefersDiagonalOverDogleg()
    {
        var grid = OpenGrid(5, 5);
        var path = Pathfinder.FindPath(grid, 0, 0, 4, 4);

        Assert.NotNull(path);
        Assert.Equal(4, path.Count); // pure diagonal
        Assert.Equal((4, 4), path[^1]);
    }

    [Fact]
    public void MaxRange_StopsShortOfGoal()
    {
        var grid = OpenGrid(10, 10);
        var path = Pathfinder.FindPath(grid, 0, 0, 9, 9, maxRange: 3);

        Assert.NotNull(path);
        var (r, c) = path[^1];
        Assert.True(Math.Max(Math.Abs(r - 9), Math.Abs(c - 9)) <= 3);
        Assert.True(path.Count < 9);
    }

    private static int[,] OpenGrid(int rows, int cols) => new int[rows, cols];
}
