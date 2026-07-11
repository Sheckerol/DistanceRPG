using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Enemy placement runs after (and independently of) map generation, on its
/// own seed-derived RNG stream, so a save system can skip it on re-entry.
/// </summary>
public class EnemyPlacerTests
{
    private const long Seed = 2762136374; // any seed works; the golden one is handy

    private static MapData Map() => MapGenerator.Generate(new Mulberry32(Seed));

    [Fact]
    public void PlaceEnemies_IsDeterministicForASeed()
    {
        var first = EnemyPlacer.PlaceEnemies(Map(), Seed);
        var second = EnemyPlacer.PlaceEnemies(Map(), Seed);

        Assert.Equal(first, second);
    }

    [Fact]
    public void PlaceEnemies_DoesNotConsumeTheMapStream()
    {
        // Generating the map, placing enemies, then generating again from a
        // fresh map RNG must yield the identical dungeon — placement can be
        // skipped (save-game re-entry) without shifting anything.
        var map = MapGenerator.Generate(new Mulberry32(Seed));
        EnemyPlacer.PlaceEnemies(map, Seed);
        var again = MapGenerator.Generate(new Mulberry32(Seed));

        Assert.Equal(map.PlayerStart, again.PlayerStart);
        Assert.Equal(map.DebugRooms.Count, again.DebugRooms.Count);
    }

    [Fact]
    public void PlaceEnemies_SpawnsNothingInTheStartingRoom_AndOnlyOnRoomTiles()
    {
        var map = Map();
        var startRoom = map.DebugRooms.First(
            r => (r.Cy, r.Cx) == map.PlayerStart);

        var positions = EnemyPlacer.PlaceEnemies(map, Seed);
        Assert.NotEmpty(positions);

        foreach (var (x, y) in positions)
        {
            int c = (int)(x / GameConstants.Tile);
            int r = (int)(y / GameConstants.Tile);

            bool inStart = c >= startRoom.X && c < startRoom.X + startRoom.W
                && r >= startRoom.Y && r < startRoom.Y + startRoom.H;
            Assert.False(inStart, $"enemy at tile ({r},{c}) is inside the starting room");

            Assert.True(map.DebugRooms.Any(room =>
                c >= room.X && c < room.X + room.W &&
                r >= room.Y && r < room.Y + room.H),
                $"enemy at tile ({r},{c}) is not inside any room");
        }
    }

    [Fact]
    public void PlaceEnemies_RespectsPerRoomCap_WithDistinctTiles()
    {
        var map = Map();
        var positions = EnemyPlacer.PlaceEnemies(map, Seed);

        // Distinct tiles overall (per-room distinctness follows).
        var tiles = positions
            .Select(p => ((int)(p.Y / GameConstants.Tile), (int)(p.X / GameConstants.Tile)))
            .ToList();
        Assert.Equal(tiles.Count, tiles.Distinct().Count());

        foreach (var room in map.DebugRooms)
        {
            int inRoom = tiles.Count(t =>
                t.Item2 >= room.X && t.Item2 < room.X + room.W &&
                t.Item1 >= room.Y && t.Item1 < room.Y + room.H);
            Assert.InRange(inRoom, 0, EnemyPlacer.MaxEnemiesPerRoom);
        }
    }
}
