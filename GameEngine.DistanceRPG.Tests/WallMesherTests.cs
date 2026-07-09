using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class WallMesherTests
{
    [Fact]
    public void MergedRects_CoverExactlyTheWallTiles_WithoutOverlap()
    {
        var map = GoldenData.GeneratedMap;
        var rects = WallMesher.MergeWallTiles(map.Grid);

        int rows = map.Grid.GetLength(0);
        int cols = map.Grid.GetLength(1);
        var covered = new int[rows, cols];
        foreach (var rect in rects)
            for (int r = rect.Y; r < rect.Y + rect.H; r++)
                for (int c = rect.X; c < rect.X + rect.W; c++)
                    covered[r, c]++;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                Assert.Equal(map.Grid[r, c] == 1 ? 1 : 0, covered[r, c]);
    }

    [Fact]
    public void MergesFarFewerRectsThanTiles()
    {
        var map = GoldenData.GeneratedMap;
        var rects = WallMesher.MergeWallTiles(map.Grid);

        int wallTiles = 0;
        foreach (int v in map.Grid)
            if (v == 1) wallTiles++;

        Assert.True(rects.Count < wallTiles / 4,
            $"expected heavy merging, got {rects.Count} rects for {wallTiles} wall tiles");
    }

    [Fact]
    public void SolidGrid_MergesToSingleRect()
    {
        var grid = new int[4, 6];
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 6; c++)
                grid[r, c] = 1;

        var rects = WallMesher.MergeWallTiles(grid);

        var rect = Assert.Single(rects);
        Assert.Equal((0, 0, 6, 4), (rect.X, rect.Y, rect.W, rect.H));
    }

    [Fact]
    public void EmptyGrid_ProducesNoRects()
    {
        Assert.Empty(WallMesher.MergeWallTiles(new int[4, 4]));
    }
}
