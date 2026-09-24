using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §4.5's <see cref="Floor"/>: one map, the rows placed on it, its own fog and
/// its stairs, each a pure function of <c>(mapSeed, index, def)</c> — floor 0 of
/// the shipped seed the shipped dungeon to the tile, every other floor its own
/// dungeon, and nothing any of it draws on shared with anything else. In the
/// content collection because one test loads an attuned dungeon.
/// </summary>
[Collection(TestContent.Collection)]
public class FloorGenerationTests
{
    /// <summary>The shipped seed.</summary>
    private const long GoldenSeed = 2762136374;

    private static DungeonDef Tutorial => GameContent.Current.Dungeons.Tutorial;

    private static string GridText(int[,] grid)
    {
        var chars = new char[grid.Length];
        int i = 0;
        foreach (int v in grid) chars[i++] = (char)('0' + v);
        return new string(chars);
    }

    /// <summary>What a row is at generation: where it stands, what it holds, its spawn index and its attunement.</summary>
    private static (float X, float Y, string WeaponId, int SpawnIndex, DamageType? Attunement) Row(EnemyState e)
        => (e.X, e.Y, e.Weapon.Id, e.SpawnIndex, e.Attunement);

    /// <summary>A drop as a string: the item, its spread, its entries and tiers, and the farm facts beside it.</summary>
    private static string Describe(Drop drop)
        => $"{drop.Weapon.Id} [{drop.Weapon.Modifiers}] {string.Join(",", drop.Weapon.Enchantments.Select(e => $"{e.Def.Id}:{e.Tier}"))} x{drop.DefeatCount} {drop.WasUniqueRoll}";

    /// <summary>The first two outputs of a stream as one number: equal seeds give equal fingerprints.</summary>
    private static ulong Fingerprint(Mulberry32 rng)
        => ((ulong)(uint)(rng.NextDouble() * 4294967296.0) << 32) | (uint)(rng.NextDouble() * 4294967296.0);

    [Fact]
    public void FloorZeroOfTheGoldenSeedIsTheShippedDungeon()
    {
        // PHASE4-009, -017: floor 0 is the golden 70x50 dungeon for the shipped
        // seed, produced through the floor with no draw reordered anywhere —
        // compared against the JS original's grid, rooms, start and fog boxes.
        // Never against GoldenData.GeneratedMap's corridors: building the fog
        // boxes expands the floor's corridors in place, as it always has.
        var golden = GoldenData.Instance;
        var floor = Floor.Generate(golden.MapSeed, 0, bottom: false, Tutorial);
        var map = floor.Map;

        Assert.Equal(GoldenSeed, golden.MapSeed);
        Assert.Equal(golden.MapSeed, floor.Seed);
        Assert.Equal(0, floor.Index);

        for (int r = 0; r < MapGenerator.Rows; r++)
        {
            var row = new char[MapGenerator.Cols];
            for (int c = 0; c < MapGenerator.Cols; c++)
                row[c] = map.Grid[r, c] == 1 ? '1' : '0';
            Assert.Equal(golden.GridRows[r], new string(row));
        }

        Assert.Equal(golden.Rooms.Length, map.DebugRooms.Count);
        for (int i = 0; i < golden.Rooms.Length; i++)
        {
            golden.Rooms[i].AssertMatches(map.DebugRooms[i]);
            Assert.Equal((golden.Rooms[i].Cx, golden.Rooms[i].Cy), (map.DebugRooms[i].Cx, map.DebugRooms[i].Cy));
        }

        Assert.Equal((golden.PlayerStart[0], golden.PlayerStart[1]), map.PlayerStart);
        Assert.Equal((golden.EnemyStart[0], golden.EnemyStart[1]), map.EnemyStart);

        Assert.Equal(golden.ExpandedCorridors.Length, map.Corridors.Count);
        for (int i = 0; i < golden.ExpandedCorridors.Length; i++)
            golden.ExpandedCorridors[i].AssertMatches(map.Corridors[i]);
        Assert.Equal(golden.UnionFogBoxes.Length, floor.FogBoxes.UnionBoxes.Count);
        for (int i = 0; i < golden.UnionFogBoxes.Length; i++)
            golden.UnionFogBoxes[i].AssertMatches(floor.FogBoxes.UnionBoxes[i]);
        Assert.Equal(golden.FogBoxes.Length, floor.FogBoxes.FogBoxes.Count);
        for (int i = 0; i < golden.FogBoxes.Length; i++)
            golden.FogBoxes[i].AssertMatches(floor.FogBoxes.FogBoxes[i]);

        // The rows are exactly what the placer puts on a fresh golden map, in the
        // order it put them, each keeping that index: the one-floor game's spawn.
        var fresh = MapGenerator.Generate(new Mulberry32(golden.MapSeed));
        var spawns = EnemyPlacer.PlaceEnemies(fresh, golden.MapSeed);
        Assert.NotEmpty(spawns);
        Assert.Equal(
            spawns.Select((s, i) => (s.X, s.Y, s.WeaponId, i)),
            floor.Enemies.Select(e => (e.X, e.Y, e.Weapon.Id, e.SpawnIndex)));

        // And its stairs: up at the start, down in the furthest room.
        Assert.Equal(map.PlayerStart, floor.UpStairs);
        Assert.Equal(StairPlacer.Place(fresh, golden.MapSeed, 0).Far, floor.DownStairs);
        Assert.Null(floor.Lair);
    }

    [Fact]
    public void EveryFloorIsItsOwnDungeon_AndTheSameOneEveryTime()
    {
        // PHASE4-145: each floor a pure function of (mapSeed, index, def). Ten
        // floors of one seed are ten different dungeons, and generating them
        // again — in the opposite order, so nothing can ride on what was built
        // before — gives every grid, row and stair back identically.
        var floors = Enumerable.Range(0, 10).Select(n => Floor.Generate(GoldenSeed, n, bottom: n == 9, Tutorial)).ToArray();
        var again = Enumerable.Range(0, 10).Reverse().Select(n => Floor.Generate(GoldenSeed, n, bottom: n == 9, Tutorial))
            .OrderBy(f => f.Index).ToArray();

        Assert.Equal(10, floors.Select(f => GridText(f.Map.Grid)).Distinct().Count());
        Assert.Equal(10, floors.Select(f => f.Seed).Distinct().Count());

        for (int n = 0; n < 10; n++)
        {
            Assert.Equal(n, floors[n].Index);
            Assert.Equal(DungeonState.FloorSeed(GoldenSeed, n), floors[n].Seed);
            Assert.Equal(GridText(floors[n].Map.Grid), GridText(again[n].Map.Grid));
            Assert.Equal(floors[n].Enemies.Select(Row), again[n].Enemies.Select(Row));
            Assert.Equal((floors[n].UpStairs, floors[n].DownStairs, floors[n].Lair), (again[n].UpStairs, again[n].DownStairs, again[n].Lair));
            Assert.Equal(
                floors[n].FogBoxes.FogBoxes.Select(b => (b.X, b.Y, b.W, b.H)),
                again[n].FogBoxes.FogBoxes.Select(b => (b.X, b.Y, b.W, b.H)));

            // Two generations are two floors, never one shared: the rows and the
            // fog are state a visit changes, so each floor holds its own.
            Assert.NotSame(floors[n].Enemies[0], again[n].Enemies[0]);
            Assert.NotSame(floors[n].Fog, again[n].Fog);
        }
    }

    [Fact]
    public void TwoFloorsSpawnIndexThreeShareNeitherADropNorALadder()
    {
        // Phase 3 left an obligation (its StreamFor and ReviveStream say so): the
        // drop and revival mixes carry no floor term, which is safe only while
        // every floor has a seed of its own. Each floor's turn system rolls off
        // Floor.Seed, so spawn index 3 on floor 2 and spawn index 3 on floor 3 are
        // two dummies with two drops and two ladders.
        var two = Floor.Generate(GoldenSeed, 2, bottom: false, Tutorial);
        var three = Floor.Generate(GoldenSeed, 3, bottom: false, Tutorial);
        var a = two.Enemies[3];
        var b = three.Enemies[3];
        Assert.Equal((3, 3), (a.SpawnIndex, b.SpawnIndex));
        Assert.NotEqual(two.Seed, three.Seed);

        Assert.NotEqual(Fingerprint(LootTable.StreamFor(two.Seed, 3)), Fingerprint(LootTable.StreamFor(three.Seed, 3)));
        for (int cycle = 0; cycle < 20; cycle++)
            Assert.NotEqual(
                Fingerprint(FarmLadder.ReviveStream(two.Seed, 3, cycle)),
                Fingerprint(FarmLadder.ReviveStream(three.Seed, 3, cycle)));

        // Through LootTable.Roll: the same dummy — same weapon, same spawn index,
        // farmed to the same depth — collected on floor 2 and on floor 3 is not
        // the same run of drops; collected twice on one floor it is.
        var twin = new EnemyState { Weapon = GameContent.Current.Weapons.Instantiate(a.Weapon.Id), SpawnIndex = 3 };
        string[] Farm(long floorSeed) => Enumerable.Range(0, 30).Select(depth =>
        {
            twin.DefeatCount = depth;
            return Describe(LootTable.Roll(twin, floorSeed));
        }).ToArray();

        var onTwo = Farm(two.Seed);
        Assert.NotEqual(onTwo, Farm(three.Seed));
        Assert.Equal(onTwo, Farm(two.Seed));
    }

    [Fact]
    public void GeneratingAFloorLeavesTheMapStreamAlone()
    {
        // PHASE4-009, -018 — EnemyPlacerTests.PlaceEnemies_DoesNotConsumeTheMapStream's
        // shape, for the stairs as well as the enemies: generate, place both, and
        // a fresh map stream still yields the identical dungeon; the map stream
        // itself carries on exactly where generation left it.
        const int n = 4;
        long floorSeed = DungeonState.FloorSeed(GoldenSeed, n);

        var stream = new Mulberry32(floorSeed);
        var map = MapGenerator.Generate(stream);
        var stairs = StairPlacer.Place(map, GoldenSeed, n);
        var spawns = EnemyPlacer.PlaceEnemies(map, floorSeed);
        double carriedOn = stream.NextDouble();

        var untouched = new Mulberry32(floorSeed);
        var again = MapGenerator.Generate(untouched);
        Assert.Equal(untouched.NextDouble(), carriedOn);
        Assert.Equal(GridText(again.Grid), GridText(map.Grid));
        Assert.Equal(again.PlayerStart, map.PlayerStart);
        Assert.Equal(again.DebugRooms.Select(r => (r.X, r.Y, r.W, r.H)), map.DebugRooms.Select(r => (r.X, r.Y, r.W, r.H)));

        // Stairs first or enemies first, each is the same: neither draws on
        // anything the other does.
        var other = MapGenerator.Generate(new Mulberry32(floorSeed));
        var spawnsFirst = EnemyPlacer.PlaceEnemies(other, floorSeed);
        Assert.Equal(spawns, spawnsFirst);
        Assert.Equal(stairs, StairPlacer.Place(other, GoldenSeed, n));

        // And the floor is those three calls on that map.
        var floor = Floor.Generate(GoldenSeed, n, bottom: false, Tutorial);
        Assert.Equal(GridText(again.Grid), GridText(floor.Map.Grid));
        Assert.Equal(spawns.Select((s, i) => (s.X, s.Y, s.WeaponId, i)), floor.Enemies.Select(e => (e.X, e.Y, e.Weapon.Id, e.SpawnIndex)));
        Assert.Equal((stairs.Up, stairs.Far), (floor.UpStairs, floor.DownStairs!.Value));
    }

    [Fact]
    public void EveryEnemyCarriesTheDungeonsAttunement()
    {
        // PHASE3-258, handed to Phase 4: attunement is the dungeon's, not the
        // individual's — every enemy an attuned dungeon spawns carries it, on
        // every floor, and an unattuned dungeon's carry none. The shipped five
        // are unattuned (attunement is its own field; none is set yet), so the
        // attuned one here is a fixture loaded through the content path.
        var hall = ContentDefaults.Dungeons.Dungeons.Single(d => d.Id == "spear_hall");
        var frost = hall with
        {
            Id = "frost_hall",
            Name = "The Frost Hall",
            Attunement = DamageType.Cold,
            Boss = hall.Boss with { Id = "frost_warden", Name = "Frost Warden" },
        };
        using var _ = TestContent.Use(dungeons: new DungeonsData([.. ContentDefaults.Dungeons.Dungeons, frost]));
        var attuned = GameContent.Current.Dungeons["frost_hall"];

        for (int n = 0; n < 3; n++)
        {
            var floor = Floor.Generate(GoldenSeed, n, bottom: n == 2, attuned);
            Assert.NotEmpty(floor.Enemies);
            Assert.All(floor.Enemies, e => Assert.Equal(DamageType.Cold, e.Attunement));

            // Attunement moves no draw: the same seed's unattuned floor is the
            // same dungeon with the same rows, attuned to nothing.
            var plain = Floor.Generate(GoldenSeed, n, bottom: n == 2, Tutorial);
            Assert.All(plain.Enemies, e => Assert.Null(e.Attunement));
            Assert.Equal(
                plain.Enemies.Select(e => (e.X, e.Y, e.Weapon.Id, e.SpawnIndex)),
                floor.Enemies.Select(e => (e.X, e.Y, e.Weapon.Id, e.SpawnIndex)));
        }
    }

    [Fact]
    public void FogIsPerFloor()
    {
        // PHASE4-144: the fog is the floor's, not the scene's. Every floor starts
        // dark, sized to its own map; exploring one lights nothing on another;
        // and what was explored stays explored through a turn's visibility reset
        // — the memory a floor keeps while the party is anywhere else.
        var first = Floor.Generate(GoldenSeed, 0, bottom: false, Tutorial);
        var second = Floor.Generate(GoldenSeed, 1, bottom: false, Tutorial);

        Assert.NotSame(first.Fog, second.Fog);
        foreach (var floor in new[] { first, second })
        {
            Assert.Equal((floor.Map.Grid.GetLength(0), floor.Map.Grid.GetLength(1)), (floor.Fog.Rows, floor.Fog.Cols));
            Assert.DoesNotContain(true, floor.Fog.Seen.Cast<bool>());
            Assert.DoesNotContain(true, floor.Fog.Visible.Cast<bool>());
        }

        var (r, c) = first.UpStairs;
        Assert.NotEmpty(first.Fog.RevealAt(r, c, first.FogBoxes.FogBoxes));
        Assert.True(first.Fog.Seen[r, c]);
        Assert.DoesNotContain(true, second.Fog.Seen.Cast<bool>());

        first.Fog.ResetVisibility();
        Assert.True(first.Fog.Seen[r, c]);
        Assert.False(first.Fog.Visible[r, c]);

        // The fog is stored, never re-derived: the same floor built afresh is dark.
        Assert.False(Floor.Generate(GoldenSeed, 0, bottom: false, Tutorial).Fog.Seen[r, c]);
    }

    [Fact]
    public void ARestoredFloorStandsOnTheTerrainGenerationBuilds()
    {
        // PHASE5-025, -026: maps are never serialized — a floor a save brings back
        // is its terrain regenerated from the seed, the very ground generation
        // builds: the grid, the rooms, the start, the corridors as the fog boxes
        // expanded them, the fog boxes, the stairs and the lair. On it stand the
        // save's rows — the objects handed in, placed by nobody — the save's
        // explored fog, and the floor counts as visited. Nothing is placed and no
        // boss is rolled: the placer's stream is never drawn.
        var hall = GameContent.Current.Dungeons["spear_hall"];
        int bossFloor = DungeonState.RollBossFloor(GoldenSeed, hall);
        Assert.Equal(5, bossFloor);
        for (int n = 0; n < bossFloor; n++)
        {
            bool bottom = n == bossFloor - 1;
            var generated = Floor.Generate(GoldenSeed, n, bottom, hall, promisedStacks: 1);

            // The rows as a visit left them: one moved, one fallen — nothing here is the placer's.
            var rows = generated.Enemies;
            rows[0].X += GameConstants.Tile;
            rows[1].Alive = false;
            var (r, c) = generated.UpStairs;
            generated.Fog.RevealAt(r, c, generated.FogBoxes.FogBoxes);
            var seen = FogState.Encode(generated.Fog.Seen);

            var restored = Floor.Restore(GoldenSeed, n, bottom, rows, generated.Boss, [], seen, visible: null);
            Assert.Equal((n, generated.Seed), (restored.Index, restored.Seed));
            Assert.Equal(GridText(generated.Map.Grid), GridText(restored.Map.Grid));
            Assert.Equal(GridText(generated.Map.RoomGrid), GridText(restored.Map.RoomGrid));
            Assert.Equal(generated.Map.DebugRooms.Select(room => (room.X, room.Y, room.W, room.H)), restored.Map.DebugRooms.Select(room => (room.X, room.Y, room.W, room.H)));
            Assert.Equal((generated.Map.PlayerStart, generated.Map.EnemyStart), (restored.Map.PlayerStart, restored.Map.EnemyStart));
            Assert.Equal(generated.Map.Corridors.Select(k => (k.X, k.Y, k.W, k.H)), restored.Map.Corridors.Select(k => (k.X, k.Y, k.W, k.H)));
            Assert.Equal(generated.FogBoxes.FogBoxes.Select(b => (b.X, b.Y, b.W, b.H)), restored.FogBoxes.FogBoxes.Select(b => (b.X, b.Y, b.W, b.H)));
            Assert.Equal((generated.UpStairs, generated.DownStairs, generated.Lair), (restored.UpStairs, restored.DownStairs, restored.Lair));

            Assert.Equal(rows.Count, restored.Enemies.Count);
            for (int i = 0; i < rows.Count; i++)
                Assert.Same(rows[i], restored.Enemies[i]);
            Assert.Same(generated.Boss, restored.Boss);
            Assert.Equal(seen, FogState.Encode(restored.Fog.Seen));
            Assert.DoesNotContain(true, restored.Fog.Visible.Cast<bool>());
            Assert.True(restored.Visited);
            Assert.Empty(restored.LooseItems);
        }

        // The placer's stream untouched: a restore calls neither the placer nor the
        // boss's roll, and the terrain it stands on is Generate's own helper.
        var calls = TestIl.Calls(typeof(Floor).Assembly)
            .Where(call => call.From.DeclaringType == typeof(Floor) && call.From.Name is nameof(Floor.Restore) or "Terrain")
            .Select(call => $"{call.Target.DeclaringType?.Name}.{call.Target.Name}")
            .ToHashSet();
        Assert.Contains("Floor.Terrain", calls);
        Assert.Contains("StairPlacer.Place", calls);
        Assert.DoesNotContain("EnemyPlacer.PlaceEnemies", calls);
        Assert.DoesNotContain("Floor.PlaceEnemies", calls);
        Assert.DoesNotContain("Floor.PlaceBoss", calls);
        Assert.DoesNotContain("DungeonState.RollBoss", calls);
        var generate = TestIl.Calls(typeof(Floor).Assembly)
            .Where(call => call.From.DeclaringType == typeof(Floor) && call.From.Name == nameof(Floor.Generate))
            .Select(call => $"{call.Target.DeclaringType?.Name}.{call.Target.Name}")
            .ToArray();
        Assert.Contains("Floor.Terrain", generate);
        Assert.Contains("Floor.PlaceEnemies", generate);

        // What no floor can hold is refused at the door: a boss above the bottom,
        // none on it, a spawn index twice, a fog row that does not fit the map.
        var bottomFloor = Floor.Generate(GoldenSeed, bossFloor - 1, bottom: true, hall, promisedStacks: 1);
        var dark = FogState.Encode(new bool[MapGenerator.Rows, MapGenerator.Cols]);
        Assert.Throws<ArgumentException>(() => Floor.Restore(GoldenSeed, bossFloor - 1, bottom: true, bottomFloor.Enemies, boss: null, [], dark, null));
        Assert.Throws<ArgumentException>(() => Floor.Restore(GoldenSeed, 0, bottom: false, bottomFloor.Enemies, bottomFloor.Boss, [], dark, null));
        Assert.Throws<ArgumentException>(() => Floor.Restore(GoldenSeed, 0, bottom: false, [bottomFloor.Enemies[0], bottomFloor.Enemies[0]], null, [], dark, null));
        Assert.Throws<ArgumentException>(() => Floor.Restore(GoldenSeed, 0, bottom: false, [], null, [], dark.Skip(1).ToArray(), null));
    }

    [Fact]
    public void TheBottomFloorHasNoWayDown()
    {
        // PHASE4-022: the boss floor is the dungeon's bottom. It has no
        // down-stairs; its furthest room holds the lair instead, on the very tile
        // the down-stairs would have taken — the flag changes no draw, only what
        // the tile is. Every floor above it has a way down and no lair.
        var hall = GameContent.Current.Dungeons["spear_hall"];
        int bossFloor = DungeonState.RollBossFloor(GoldenSeed, hall);
        Assert.InRange(bossFloor, 5, 10);

        var bottom = Floor.Generate(GoldenSeed, bossFloor - 1, bottom: true, hall);
        Assert.Null(bottom.DownStairs);
        Assert.NotNull(bottom.Lair);
        Assert.True(StairPlacer.FurthestRoom(bottom.Map).Contains(bottom.Lair!.Value.R, bottom.Lair.Value.C));
        Assert.Equal(bottom.Map.PlayerStart, bottom.UpStairs);

        // The same map and the same placed rows, in the same order: the flag moves
        // no draw. What it adds is the dungeon's boss, appended after every placed
        // row on a stream of its own (Phase 4.5), so the placed rows are the
        // bottom floor's rows less its last.
        var asIfNot = Floor.Generate(GoldenSeed, bossFloor - 1, bottom: false, hall);
        Assert.Equal(asIfNot.DownStairs, bottom.Lair);
        Assert.Equal(GridText(asIfNot.Map.Grid), GridText(bottom.Map.Grid));
        Assert.Equal(asIfNot.Enemies.Select(Row), bottom.Enemies.SkipLast(1).Select(Row));
        Assert.Same(bottom.Boss, bottom.Enemies[^1]);
        Assert.Null(asIfNot.Boss);

        for (int n = 0; n < bossFloor - 1; n++)
        {
            var above = Floor.Generate(GoldenSeed, n, bottom: false, hall);
            Assert.NotNull(above.DownStairs);
            Assert.Null(above.Lair);
        }
    }

    [Fact]
    public void ADummyMaySpawnOnTheFarTile_AndNeitherPassMovesForTheOther()
    {
        // PHASE4-013, -022: the far tile is the stair stream's one draw over the
        // furthest room, and EnemyPlacer puts 0-4 dummies on random tiles of that
        // same room off the floor's own stream. Neither pass reads the other, so
        // a dummy sometimes spawns on the down-stairs — or, on the bottom floor,
        // in the lair. A known case, kept rather than redrawn: the stairs stay a
        // function of the map and their own stream, so no placer change ever
        // moves a staircase, and whatever reads the tile copes with an occupant.
        // Arrival places members around living enemies, the boss takes the
        // nearest free tile from the lair, and a live dummy holds the stairs only
        // until it walks off to close on the party or falls (a corpse is walked
        // over): it guards the stairs, never seals them.
        var tutorial = Tutorial;

        // The tutorial's floor 1 of seed 2084311110: a spear dummy on the way
        // down — floor 1 always has one, the tutorial's bottom being floor 2.
        const long DownSeed = 2084311110;
        Assert.Equal(2, DungeonState.RollBossFloor(DownSeed, tutorial));
        var first = Floor.Generate(DownSeed, 0, bottom: false, tutorial);
        Assert.Equal((19, 4), first.DownStairs);
        var guard = Assert.Single(first.Enemies, e => Displacer.TileOf(e) == first.DownStairs!.Value);
        Assert.Equal((9, "phalanx_spear", true), (guard.SpawnIndex, guard.Weapon.Id, guard.Alive));

        // Neither moved for the other: the stairs are the stair stream's draw on
        // the bare map, and the dummy stands exactly where the placer put it.
        long floorSeed = DungeonState.FloorSeed(DownSeed, 0);
        var bare = MapGenerator.Generate(new Mulberry32(floorSeed));
        Assert.Equal(StairPlacer.Place(bare, DownSeed, 0).Far, first.DownStairs);
        var placed = EnemyPlacer.PlaceEnemies(bare, floorSeed)[guard.SpawnIndex];
        Assert.Equal((placed.X, placed.Y, placed.WeaponId), (guard.X, guard.Y, guard.Weapon.Id));

        // The tutorial's floor 2, its bottom, of seed 1697034193: a thrower in
        // the lair. The boss cannot stand there on this seed and takes the
        // nearest free tile from it instead.
        const long LairSeed = 1697034193;
        Assert.Equal(2, DungeonState.RollBossFloor(LairSeed, tutorial));
        var bottom = Floor.Generate(LairSeed, 1, bottom: true, tutorial);
        Assert.Equal((58, 28), bottom.Lair);
        var squatter = Assert.Single(bottom.Enemies, e => Displacer.TileOf(e) == bottom.Lair!.Value);
        Assert.Equal((15, "bandolier", true), (squatter.SpawnIndex, squatter.Weapon.Id, squatter.Alive));

        // Not a one-off. Over the stair tests' sweep — the golden seed, then 99
        // spread over the 32-bit range, ten floors each — 41 floors of the 1000
        // have a dummy on the far tile, in 33 of the 100 ten-floor dungeons:
        // about one floor in 25, a third of the deepest visits. Pinned as measured.
        var perDungeon = new int[100];
        Parallel.For(0, perDungeon.Length, i =>
        {
            long seed = i == 0 ? GoldenSeed : (uint)(i * 2654435761u);
            for (int n = 0; n < 10; n++)
            {
                var floor = Floor.Generate(seed, n, bottom: false, tutorial);
                var far = floor.DownStairs!.Value;
                if (floor.Enemies.Any(e => Displacer.TileOf(e) == far))
                    perDungeon[i]++;
            }
        });
        Assert.Equal((41, 33), (perDungeon.Sum(), perDungeon.Count(hits => hits > 0)));
    }
}
