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

    [Theory]
    [InlineData(1, 0, 1)]   // the tutorial's first floor: one a room at most
    [InlineData(2, 0, 2)]
    [InlineData(3, 0, 3)]
    [InlineData(5, 0, 4)]   // the ceiling holds however deep
    [InlineData(1, 1, 2)]   // the second dungeon's first floor
    [InlineData(3, 1, 4)]
    [InlineData(4, 1, 4)]
    [InlineData(1, 3, 4)]
    [InlineData(int.MaxValue, int.MaxValue, 4)]
    public void RoomCap_IsFloorNumberPlusClears_NeverAboveFour(int floorNumber, int cleared, int cap)
        => Assert.Equal(cap, EnemyPlacer.RoomCap(floorNumber, cleared));

    [Fact]
    public void RoomCap_RefusesFloorZero_FloorsCountFromOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnemyPlacer.RoomCap(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnemyPlacer.RoomCap(1, -1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PlaceEnemies_HoldsEveryRoomToTheCap(int cap)
    {
        int most = 0;
        for (long seed = 1; seed <= 40; seed++)
        {
            var map = MapGenerator.Generate(new Mulberry32(seed));
            var tiles = EnemyPlacer.PlaceEnemies(map, seed, cap)
                .Select(p => ((int)(p.Y / GameConstants.Tile), (int)(p.X / GameConstants.Tile)))
                .ToList();
            foreach (var room in map.DebugRooms)
            {
                int inRoom = tiles.Count(t =>
                    t.Item2 >= room.X && t.Item2 < room.X + room.W &&
                    t.Item1 >= room.Y && t.Item1 < room.Y + room.H);
                Assert.InRange(inRoom, 0, cap);
                most = Math.Max(most, inRoom);
            }
        }
        Assert.Equal(cap, most);   // the cap is reached, not just respected
    }

    [Fact]
    public void PlaceEnemies_LeftUncapped_IsThePlacementItAlwaysWas()
        => Assert.Equal(EnemyPlacer.PlaceEnemies(Map(), Seed, EnemyPlacer.MaxEnemiesPerRoom), EnemyPlacer.PlaceEnemies(Map(), Seed));

    [Fact]
    public void ANewCampaignsTutorial_HoldsOneARoomOnItsFirstFloor_TwoOnItsSecond()
    {
        for (long seed = 1; seed <= 20; seed++)
        {
            var campaign = CampaignState.Start(GameContent.Current);
            var visit = campaign.Enter("tutorial", seed);
            for (int n = 0; n < visit.Floors.Length; n++)
                Assert.InRange(MostInARoom(visit.Floors[n]), 0, EnemyPlacer.RoomCap(n + 1, 0));
        }
    }

    [Fact]
    public void EachClear_RaisesTheFirstFloorsCap_UpToFour()
    {
        var hall = GameContent.Current.Dungeons["spear_hall"];
        var party = CampaignState.Start(GameContent.Current).Party;
        for (int cleared = 0; cleared <= 4; cleared++)
        {
            int most = 0;
            for (long seed = 1; seed <= 20; seed++)
                most = Math.Max(most, MostInARoom(DungeonState.Enter(hall, seed, party, cleared).Floors[0]));
            Assert.Equal(EnemyPlacer.RoomCap(1, cleared), most);
        }
    }

    [Fact]
    public void AReloadPlacesTheFloorsNobodyStoodOn_UnderTheCapTheEntrySaw()
    {
        // The tutorial's second floor is never stood on before the save, so the
        // load generates it fresh: it must come back under the entry's cap, not
        // the full four — whatever the campaign's clears say by then.
        var campaign = CampaignState.Start(GameContent.Current);
        var visit = campaign.Enter("tutorial", 1);
        visit.OpenTurns(campaign.Party);
        var before = visit.Floors[1].Enemies.Select(e => (e.X, e.Y, e.Weapon.Id)).ToList();

        var loaded = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign, null))), GameContent.Current).Campaign.Visit!;
        Assert.Equal(0, loaded.DungeonsCleared);
        Assert.Equal(before, loaded.Floors[1].Enemies.Select(e => (e.X, e.Y, e.Weapon.Id)).ToList());
    }

    /// <summary>The most placed dummies standing in any one room of <paramref name="floor"/> — the boss left out, who is no room's roll.</summary>
    private static int MostInARoom(Floor floor)
    {
        var tiles = floor.Enemies.Where(e => !ReferenceEquals(e, floor.Boss))
            .Select(e => ((int)(e.Y / GameConstants.Tile), (int)(e.X / GameConstants.Tile)))
            .ToList();
        return floor.Map.DebugRooms.Select(room => tiles.Count(t =>
            t.Item2 >= room.X && t.Item2 < room.X + room.W &&
            t.Item1 >= room.Y && t.Item1 < room.Y + room.H)).DefaultIfEmpty(0).Max();
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
