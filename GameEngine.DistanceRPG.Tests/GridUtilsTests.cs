using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class GridUtilsTests
{
    [Fact]
    public void FindPartySpawnTiles_StartsAtSpawnAndStaysOnFloor()
    {
        var map = GoldenData.GeneratedMap;
        var tiles = GridUtils.FindPartySpawnTiles(map.Grid, map.PlayerStart.Row, map.PlayerStart.Col, 4);

        Assert.Equal(4, tiles.Count);
        Assert.Equal((map.PlayerStart.Row, map.PlayerStart.Col), tiles[0]);
        Assert.Equal(4, tiles.Distinct().Count());
        foreach (var (r, c) in tiles)
            Assert.Equal(0, map.Grid[r, c]);
    }

    [Fact]
    public void FindPartySpawnTiles_PadsWithStartWhenBoxedIn()
    {
        // Single open tile surrounded by walls
        var grid = new int[3, 3];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                grid[r, c] = 1;
        grid[1, 1] = 0;

        var tiles = GridUtils.FindPartySpawnTiles(grid, 1, 1, 4);

        Assert.Equal(4, tiles.Count);
        Assert.All(tiles, t => Assert.Equal((1, 1), t));
    }

    [Fact]
    public void LineOfSight_BlockedByWallBetween()
    {
        // Corridor with a wall tile in the middle of row 1
        var grid = new int[3, 5];
        grid[1, 2] = 1;

        float tile = GameConstants.Tile;
        float y = 1.5f * tile;

        Assert.False(LineOfSight.HasLineOfSight(grid, 0.5f * tile, y, 4.5f * tile, y));
        // Sight line through open row 0 is clear
        Assert.True(LineOfSight.HasLineOfSight(grid, 0.5f * tile, 0.5f * tile, 4.5f * tile, 0.5f * tile));
    }

    [Fact]
    public void LineOfSight_DiagonalAroundCorner()
    {
        var grid = new int[4, 4];
        grid[1, 1] = 1;

        float tile = GameConstants.Tile;
        // Diagonal passing through the wall tile's square
        Assert.False(LineOfSight.HasLineOfSight(grid, 0.5f * tile, 0.5f * tile, 2.5f * tile, 2.5f * tile));
        // Parallel diagonal shifted one tile down passes beside it
        Assert.True(LineOfSight.HasLineOfSight(grid, 0.5f * tile, 2.5f * tile, 2.5f * tile, 3.5f * tile));
    }
}
