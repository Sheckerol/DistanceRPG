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
    public void FindPartySpawnTiles_StepsAroundBlockedTiles_TheStartIncluded()
    {
        // An arrival is placed around living enemies, never inside one (4.2's
        // stairs may have a dummy on them): a blocked tile is skipped wherever it
        // falls in the rings, the start tile included, and the rest fill on from
        // further along. With nothing blocked near, the tiles are the prototype's.
        var map = GoldenData.GeneratedMap;
        var (r, c) = map.PlayerStart;
        var plain = GridUtils.FindPartySpawnTiles(map.Grid, r, c, 4);
        Assert.Equal(plain, GridUtils.FindPartySpawnTiles(map.Grid, r, c, 4, new HashSet<(int R, int C)>()));
        Assert.Equal(plain, GridUtils.FindPartySpawnTiles(map.Grid, r, c, 4, new HashSet<(int R, int C)> { (0, 0) }));

        var blocked = new HashSet<(int R, int C)> { (r, c), plain[2] };
        var around = GridUtils.FindPartySpawnTiles(map.Grid, r, c, 4, blocked);
        var longer = GridUtils.FindPartySpawnTiles(map.Grid, r, c, 6);
        Assert.Equal(longer.Where(t => !blocked.Contains(t)).Take(4), around);
        Assert.DoesNotContain((r, c), around);
        Assert.Equal(4, around.Distinct().Count());

        // Boxed in with the start blocked: the padding is the one free tile, never the blocked start.
        var grid = new int[3, 4];
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 4; col++)
                grid[row, col] = 1;
        grid[1, 1] = 0;
        grid[1, 2] = 0;
        var boxed = GridUtils.FindPartySpawnTiles(grid, 1, 1, 4, new HashSet<(int R, int C)> { (1, 1) });
        Assert.All(boxed, t => Assert.Equal((1, 2), t));
    }

    [Fact]
    public void NearestFreeTile_IsTheStartWhenFree_ElseTheFirstFreeUpDownLeftRight()
    {
        // "Stand here, or as near as you can": what a revival wakes on and where a
        // boss waits when a dummy holds its lair. The start when it is free; else
        // breadth-first, up, down, left, right, across floor only — a fixed order,
        // so a tie is settled the same way every time and never by a set.
        var grid = new int[5, 5];
        grid[1, 2] = 1;   // a wall above the middle
        var start = (R: 2, C: 2);

        Assert.Equal(start, GridUtils.NearestFreeTile(grid, start, _ => false));
        Assert.Equal((3, 2), GridUtils.NearestFreeTile(grid, start, t => t == start));                 // up is a wall: down
        Assert.Equal((2, 1), GridUtils.NearestFreeTile(grid, start, t => t == start || t == (3, 2)));  // then left
        Assert.Equal((2, 3), GridUtils.NearestFreeTile(grid, start, t => t is (2, 2) or (3, 2) or (2, 1)));

        // Floor only: the search never crosses a wall to reach the tile behind it,
        // and with everything it can reach taken it stays put.
        var walled = new int[3, 5];
        for (int c = 0; c < 5; c++)
            walled[0, c] = walled[2, c] = 1;
        walled[1, 2] = 1;
        Assert.Equal((1, 0), GridUtils.NearestFreeTile(walled, (1, 1), t => t == (1, 1)));
        Assert.Equal((1, 1), GridUtils.NearestFreeTile(walled, (1, 1), t => t.C < 2));   // (1, 3) is behind the wall
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
