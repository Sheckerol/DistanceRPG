using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The generator must reproduce the JS original exactly for the game's real
/// seed (golden data), and every generated map must satisfy the structural
/// invariants the gameplay relies on.
/// </summary>
public class MapGeneratorTests
{
    [Fact]
    public void GoldenSeed_ProducesIdenticalRooms()
    {
        var golden = GoldenData.Instance;
        var map = GoldenData.GeneratedMap;

        Assert.Equal(golden.Rooms.Length, map.Rooms.Count);
        for (int i = 0; i < golden.Rooms.Length; i++)
        {
            golden.Rooms[i].AssertMatches(map.Rooms[i]);
            Assert.Equal((golden.Rooms[i].Cx, golden.Rooms[i].Cy), (map.Rooms[i].Cx, map.Rooms[i].Cy));
        }
    }

    [Fact]
    public void GoldenSeed_ProducesIdenticalCorridors()
    {
        var golden = GoldenData.Instance;
        var map = GoldenData.GeneratedMap;

        Assert.Equal(golden.Corridors.Length, map.Corridors.Count);
        for (int i = 0; i < golden.Corridors.Length; i++)
        {
            golden.Corridors[i].AssertMatches(map.Corridors[i]);
            Assert.Equal(golden.Corridors[i].Dir == "h", map.Corridors[i].Horizontal);
        }
    }

    [Fact]
    public void GoldenSeed_ProducesIdenticalGrid()
    {
        var golden = GoldenData.Instance;
        var map = GoldenData.GeneratedMap;

        for (int r = 0; r < MapGenerator.Rows; r++)
        {
            var row = new char[MapGenerator.Cols];
            for (int c = 0; c < MapGenerator.Cols; c++)
                row[c] = map.Grid[r, c] == 1 ? '1' : '0';
            Assert.Equal(golden.GridRows[r], new string(row));
        }
    }

    [Fact]
    public void GoldenSeed_ProducesIdenticalStartPositions()
    {
        var golden = GoldenData.Instance;
        var map = GoldenData.GeneratedMap;

        Assert.Equal((golden.PlayerStart[0], golden.PlayerStart[1]), map.PlayerStart);
        Assert.Equal((golden.EnemyStart[0], golden.EnemyStart[1]), map.EnemyStart);
    }

    [Fact]
    public void GoldenSeed_ExpandedCorridorsMatch()
    {
        var golden = GoldenData.Instance;
        // Generate a fresh map â€” expansion mutates corridors in place.
        var map = MapGenerator.Generate(new Mulberry32(golden.MapSeed));
        MapGenerator.ExpandCorridors(map.Corridors, map.Grid);

        Assert.Equal(golden.ExpandedCorridors.Length, map.Corridors.Count);
        for (int i = 0; i < golden.ExpandedCorridors.Length; i++)
            golden.ExpandedCorridors[i].AssertMatches(map.Corridors[i]);
    }

    [Theory]
    [InlineData(556432165)]
    [InlineData(2762136374L)]
    [InlineData(42)]
    [InlineData(999999)]
    public void Invariants_BorderIsWall_RoomsSeparated_MapConnected(long seed)
    {
        var map = MapGenerator.Generate(new Mulberry32(seed));

        // Border tiles are never carved
        for (int c = 0; c < MapGenerator.Cols; c++)
        {
            Assert.Equal(1, map.Grid[0, c]);
            Assert.Equal(1, map.Grid[MapGenerator.Rows - 1, c]);
        }
        for (int r = 0; r < MapGenerator.Rows; r++)
        {
            Assert.Equal(1, map.Grid[r, 0]);
            Assert.Equal(1, map.Grid[r, MapGenerator.Cols - 1]);
        }

        // Rooms keep a 2-tile separation
        for (int i = 0; i < map.Rooms.Count; i++)
            for (int j = i + 1; j < map.Rooms.Count; j++)
            {
                var a = map.Rooms[i];
                var b = map.Rooms[j];
                bool tooClose =
                    a.X < b.X + b.W + 2 && a.X + a.W + 2 > b.X &&
                    a.Y < b.Y + b.H + 2 && a.Y + a.H + 2 > b.Y;
                Assert.False(tooClose, $"rooms {i} and {j} violate 2-tile separation");
            }

        // Start tiles are open floor and every floor tile is reachable
        Assert.Equal(0, map.Grid[map.PlayerStart.Row, map.PlayerStart.Col]);
        Assert.Equal(0, map.Grid[map.EnemyStart.Row, map.EnemyStart.Col]);
        Assert.True(GridUtils.IsFullyConnected(map.Grid, map.PlayerStart.Row, map.PlayerStart.Col));
    }

    [Fact]
    public void SameSeed_SameMap_DifferentSeed_DifferentMap()
    {
        var a = MapGenerator.Generate(new Mulberry32(123));
        var b = MapGenerator.Generate(new Mulberry32(123));
        var c = MapGenerator.Generate(new Mulberry32(124));

        Assert.Equal(GridToString(a.Grid), GridToString(b.Grid));
        Assert.NotEqual(GridToString(a.Grid), GridToString(c.Grid));
    }

    private static string GridToString(int[,] grid)
    {
        var chars = new char[grid.Length];
        int i = 0;
        foreach (int v in grid) chars[i++] = (char)('0' + v);
        return new string(chars);
    }
}
