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

        foreach (var (x, y, _) in positions)
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

    [Fact]
    public void PlaceEnemies_AssignsResolvableWeaponIds()
    {
        var spawns = EnemyPlacer.PlaceEnemies(Map(), Seed);
        var catalogue = GameContent.Current.Weapons;

        // Every id resolves, to one of the placed close-range classes or the healer's staff.
        Assert.All(spawns, s =>
        {
            var def = catalogue[s.WeaponId];
            Assert.True(EnemyPlacer.PlacedClasses.Contains(def.Class) || s.WeaponId == EnemyPlacer.HealerWeaponId,
                $"{s.WeaponId} is a {def.Class}, which rank and file never roll");
        });

        // Over a whole map the loadout should be mixed, not one weapon
        // (uniform draw across ~dozens of spawns — a single value would mean
        // the weapon roll is broken, not unlucky).
        if (spawns.Count >= 10)
            Assert.True(spawns.Select(s => s.WeaponId).Distinct().Count() > 1);
    }

    [Fact]
    public void PlaceEnemies_RollsFiveCloseRangeClasses_FourVariantsEach()
    {
        Assert.Equal(
            new[] { WeaponClass.Dagger, WeaponClass.Sword, WeaponClass.Spear, WeaponClass.Axe, WeaponClass.Throwing },
            EnemyPlacer.PlacedClasses);

        var catalogue = GameContent.Current.Weapons;
        var placeable = EnemyPlacer.PlacedClasses
            .SelectMany(cls => Enum.GetValues<VariantRole>().Select(role => catalogue.Variant(cls, role).Id))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(20, placeable.Count);

        var rolled = new HashSet<string>(StringComparer.Ordinal);
        for (long seed = 1; seed <= 40; seed++)
        {
            var map = MapGenerator.Generate(new Mulberry32(seed));
            foreach (var (_, _, id) in EnemyPlacer.PlaceEnemies(map, seed))
            {
                if (id == EnemyPlacer.HealerWeaponId) continue;
                Assert.Contains(id, placeable);
                rolled.Add(id);
            }
        }

        // Over enough maps every one of the twenty comes up, and nothing else
        // ever does: no bow, no wand, no debuff staff until their AI exists.
        Assert.True(placeable.SetEquals(rolled),
            $"never rolled: {string.Join(", ", placeable.Except(rolled))}");
    }

    [Fact]
    public void PlaceEnemies_StaffHealersOnlyInCrowdedRooms_AtMostOnePerRoom()
    {
        var map = Map();
        var spawns = EnemyPlacer.PlaceEnemies(map, Seed);

        foreach (var room in map.DebugRooms)
        {
            var inRoom = spawns.Where(s =>
            {
                int c = (int)(s.X / GameConstants.Tile), r = (int)(s.Y / GameConstants.Tile);
                return c >= room.X && c < room.X + room.W && r >= room.Y && r < room.Y + room.H;
            }).ToList();

            int staves = inRoom.Count(s => s.WeaponId == EnemyPlacer.HealerWeaponId);
            Assert.InRange(staves, 0, 1); // never more than one healer per room
            if (staves == 1)
                Assert.True(inRoom.Count >= GameConstants.MinEnemiesForStaffHealer,
                    "a healer should only appear in a room of 3+ enemies");
        }
        Assert.Equal("staff_of_renewal", EnemyPlacer.HealerWeaponId);
    }

    [Fact]
    public void PlaceEnemies_HealerConversionIsDeterministic()
    {
        var first = EnemyPlacer.PlaceEnemies(Map(), Seed);
        var second = EnemyPlacer.PlaceEnemies(Map(), Seed);
        Assert.Equal(
            first.Select(s => s.WeaponId),
            second.Select(s => s.WeaponId));
    }
}
