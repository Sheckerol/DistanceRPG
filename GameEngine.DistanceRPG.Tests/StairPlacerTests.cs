using System.Collections.Concurrent;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §4.2's stairs: a post-pass over a finished map, on a stream of its own —
/// down-stairs in the room furthest from the start by A* cost, up-stairs at the
/// arrival tile — deterministic for a seed and a floor, and touching nothing the
/// map or the enemies draw on.
/// </summary>
public class StairPlacerTests
{
    /// <summary>The shipped seed: floor 0 of it is the golden dungeon.</summary>
    private const long GoldenSeed = 2762136374;

    /// <summary>The <paramref name="i"/>th map seed of a sweep: the golden one first, then a multiplicative spread over the 32-bit range.</summary>
    private static long SweepSeed(int i) => i == 0 ? GoldenSeed : (uint)(i * 2654435761u);

    /// <summary>Floor <paramref name="n"/>'s map of the dungeon seeded <paramref name="seed"/>, generated as the floor generates it.</summary>
    private static MapData FloorMap(long seed, int n) => MapGenerator.Generate(new Mulberry32(DungeonState.FloorSeed(seed, n)));

    /// <summary>The tile at row-major position <paramref name="k"/> of <paramref name="room"/>.</summary>
    private static (int R, int C) TileOf(Room room, int k) => (room.Y + k / room.W, room.X + k % room.W);

    /// <summary>
    /// The true cost of walking from <paramref name="start"/> to every tile, in the
    /// same thousandths <see cref="StairPlacer.PathCost"/> counts in: a Dijkstra
    /// over the grid under the pathfinder's own move rules — eight directions, no
    /// cutting a corner past a wall — sharing no code with the placer, so it is an
    /// oracle for "furthest" rather than a restatement of it.
    /// </summary>
    private static int[,] WalkingCosts(int[,] grid, (int R, int C) start)
    {
        int rows = grid.GetLength(0), cols = grid.GetLength(1);
        var cost = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                cost[r, c] = int.MaxValue;

        cost[start.R, start.C] = 0;
        var queue = new PriorityQueue<(int R, int C), int>();
        queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var at, out int sofar))
        {
            if (sofar > cost[at.R, at.C]) continue;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = at.R + dr, nc = at.C + dc;
                    if (nr < 0 || nr >= rows || nc < 0 || nc >= cols || grid[nr, nc] != 0) continue;
                    bool diagonal = dr != 0 && dc != 0;
                    if (diagonal && (grid[at.R + dr, at.C] != 0 || grid[at.R, at.C + dc] != 0)) continue;
                    int next = sofar + (diagonal ? StairPlacer.MilliStepsPerDiagonal : StairPlacer.MilliStepsPerCardinal);
                    if (next >= cost[nr, nc]) continue;
                    cost[nr, nc] = next;
                    queue.Enqueue((nr, nc), next);
                }
        }
        return cost;
    }

    /// <summary>
    /// A synthetic map: an open hall with a wall border, <paramref name="pillars"/>
    /// standing in it, and a one-tile room at each of <paramref name="centres"/> —
    /// the first the start room. Only room centres and the grid matter to the
    /// furthest-room rule.
    /// </summary>
    private static MapData Hall(int size, (int R, int C)[] pillars, params (int R, int C)[] centres)
    {
        var grid = new int[size, size];
        var roomGrid = new int[size, size];
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
            {
                grid[r, c] = r == 0 || c == 0 || r == size - 1 || c == size - 1 ? 1 : 0;
                roomGrid[r, c] = -1;
            }
        foreach (var (r, c) in pillars)
            grid[r, c] = 1;

        var rooms = centres.Select(t => new Room(t.C, t.R, 1, 1, cx: t.C, cy: t.R)).ToList();
        for (int i = 0; i < rooms.Count; i++)
            roomGrid[rooms[i].Cy, rooms[i].Cx] = i;

        return new MapData
        {
            Grid = grid,
            RoomGrid = roomGrid,
            Rooms = rooms,
            Corridors = [],
            PlayerStart = (centres[0].R, centres[0].C),
            EnemyStart = (centres[^1].R, centres[^1].C),
            DebugRooms = rooms.Select(r => r.Clone()).ToList(),
            DebugCorridors = [],
        };
    }

    /// <summary>A path's steps as letters, D for a diagonal and C for a cardinal: the order the pathfinder took them in.</summary>
    private static string Shape((int R, int C) from, IEnumerable<(int R, int C)> path)
    {
        var letters = new List<char>();
        foreach (var step in path)
        {
            letters.Add(step.R != from.R && step.C != from.C ? 'D' : 'C');
            from = step;
        }
        return new string(letters.ToArray());
    }

    /// <summary>A path's cost summed as the pathfinder sums it: doubles, 1 and 1.414, in path order.</summary>
    private static double SummedAsDoubles((int R, int C) from, IEnumerable<(int R, int C)> path)
    {
        double g = 0;
        foreach (var step in path)
        {
            g += step.R != from.R && step.C != from.C ? 1.414 : 1;
            from = step;
        }
        return g;
    }

    [Fact]
    public void TheUpStairsAreTheArrivalTile()
    {
        // PHASE4-014: up-stairs at the arrival tile — PlayerStart, where the party
        // spawns: the first member stands on it and the rest spiral out from it.
        for (int i = 0; i < 20; i++)
        {
            long seed = SweepSeed(i);
            for (int n = 0; n < 3; n++)
            {
                var map = FloorMap(seed, n);
                var stairs = StairPlacer.Place(map, seed, n);
                Assert.Equal(map.PlayerStart, stairs.Up);
                Assert.Equal(stairs.Up, GridUtils.FindPartySpawnTiles(map.Grid, stairs.Up.R, stairs.Up.C, GameConstants.PartySize)[0]);
                Assert.NotEqual(stairs.Up, stairs.Far);
            }
        }
    }

    [Fact]
    public void TheDownStairsAreInTheRoomFurthestByAStarCost()
    {
        // PHASE4-013: the down-stairs go in the room furthest from PlayerStart by
        // A* cost. Checked by brute force over every room: each room's centre is
        // costed by an independent Dijkstra, the pathfinder's own path to it costs
        // exactly that (A* finds the true walk, so "A* cost" is the walk), and the
        // chosen room is the costliest, the lowest index among any tie.
        var failures = new ConcurrentBag<string>();
        Parallel.For(0, 12 * 10, k =>
        {
            long seed = SweepSeed(k / 10);
            int n = k % 10;
            var map = FloorMap(seed, n);
            var start = map.PlayerStart;
            var walk = WalkingCosts(map.Grid, start);

            int best = -1;
            Room? expected = null;
            foreach (var room in map.DebugRooms)
            {
                if ((room.Cy, room.Cx) == start) continue;
                int cost = walk[room.Cy, room.Cx];
                var path = Pathfinder.FindPath(map.Grid, start.Row, start.Col, room.Cy, room.Cx)!;
                if (StairPlacer.PathCost(start, path) != cost)
                    failures.Add($"seed {seed} floor {n}: A* to ({room.Cy},{room.Cx}) costs {StairPlacer.PathCost(start, path)}, the walk {cost}");
                if (cost > best)
                {
                    best = cost;
                    expected = room;
                }
            }

            var chosen = StairPlacer.FurthestRoom(map);
            if (!ReferenceEquals(expected, chosen))
                failures.Add($"seed {seed} floor {n}: chose room {map.DebugRooms.IndexOf(chosen)}, the furthest is {map.DebugRooms.IndexOf(expected!)}");
            var far = StairPlacer.Place(map, seed, n).Far;
            if (!chosen.Contains(far.R, far.C))
                failures.Add($"seed {seed} floor {n}: the down-stairs {far} are outside the furthest room");
        });
        Assert.Empty(failures.Order(StringComparer.Ordinal));

        // A walk, not a distance as the crow flies. On the shipped dungeon itself
        // the two disagree: EnemyStart — the room whose centre is furthest by
        // Manhattan distance — is 53 tiles away as the crow flies and closer on
        // foot than a room 22 tiles away whose corridors wind. Pinned before use.
        var golden = MapGenerator.Generate(new Mulberry32(GoldenSeed));
        var furthest = StairPlacer.FurthestRoom(golden);
        var start0 = golden.PlayerStart;
        var costs = WalkingCosts(golden.Grid, start0);
        int Manhattan((int R, int C) t) => Math.Abs(t.R - start0.Row) + Math.Abs(t.C - start0.Col);

        Assert.Equal((GoldenData.Instance.PlayerStart[0], GoldenData.Instance.PlayerStart[1]), start0);
        Assert.Equal((5, 9), golden.EnemyStart);
        Assert.Equal((6, 42, 13), (golden.DebugRooms.IndexOf(furthest), furthest.Cy, furthest.Cx));
        Assert.Equal((53, 22), (Manhattan(golden.EnemyStart), Manhattan((furthest.Cy, furthest.Cx))));
        Assert.True(costs[furthest.Cy, furthest.Cx] > costs[golden.EnemyStart.Row, golden.EnemyStart.Col]);

        // So the golden dungeon's down-stairs are here, a regression pin on the
        // one draw the stair stream makes over that room.
        Assert.Equal(new StairTiles(start0, (40, 17)), StairPlacer.Place(golden, GoldenSeed, 0));
    }

    [Fact]
    public void TiesGoToTheLowerRoomIndex()
    {
        // An open hall with one pillar at (4, 2): from the start at (5, 5) the
        // pathfinder walks to (3, 2) diagonal-diagonal-straight and to (7, 2)
        // straight-diagonal-diagonal. Two diagonals and one straight step each —
        // the same walk's worth — taken in a different order, and summed as
        // doubles in path order the two come out an ulp apart. Pinned before use.
        (int R, int C) start = (5, 5), up = (3, 2), down = (7, 2);
        (int R, int C)[] pillar = [(4, 2)];
        var map = Hall(11, pillar, start, up, down);
        var toUp = Pathfinder.FindPath(map.Grid, start.R, start.C, up.R, up.C)!;
        var toDown = Pathfinder.FindPath(map.Grid, start.R, start.C, down.R, down.C)!;

        Assert.Equal(("DDC", "CDD"), (Shape(start, toUp), Shape(start, toDown)));
        Assert.Equal(2 * StairPlacer.MilliStepsPerDiagonal + StairPlacer.MilliStepsPerCardinal, StairPlacer.PathCost(start, toUp));
        Assert.Equal(StairPlacer.PathCost(start, toUp), StairPlacer.PathCost(start, toDown));
        Assert.True(SummedAsDoubles(start, toUp) > SummedAsDoubles(start, toDown));

        // A tie goes to the lower room index — whichever room that is. Listed
        // with the doubles' smaller sum first, a comparison of summed doubles
        // would pick the other room: the integers are what make it a tie.
        Assert.Same(map.DebugRooms[1], StairPlacer.FurthestRoom(map));
        var swapped = Hall(11, pillar, start, down, up);
        Assert.Same(swapped.DebugRooms[1], StairPlacer.FurthestRoom(swapped));
        Assert.Equal((down.R, down.C), (swapped.DebugRooms[1].Cy, swapped.DebugRooms[1].Cx));

        // And a strictly further room still wins from any index.
        var further = Hall(11, pillar, start, up, down, (9, 1));
        Assert.Same(further.DebugRooms[3], StairPlacer.FurthestRoom(further));
    }

    [Fact]
    public void PathCostCountsStepsAsIntegers()
    {
        // A thousandth of a step per unit: the pathfinder's own 1 and 1.414 times
        // a thousand, so the integers compare exactly as its costs do, and a jump
        // that is no step at all is refused rather than costed.
        Assert.Equal((1000, 1414), (StairPlacer.MilliStepsPerCardinal, StairPlacer.MilliStepsPerDiagonal));
        Assert.Equal(0, StairPlacer.PathCost((4, 4), []));
        Assert.Equal(1000 + 1414 + 1414 + 1000, StairPlacer.PathCost((4, 4), [(4, 5), (5, 6), (6, 5), (7, 5)]));
        Assert.Throws<ArgumentException>(() => StairPlacer.PathCost((4, 4), [(4, 6)]));
        Assert.Throws<ArgumentException>(() => StairPlacer.PathCost((4, 4), [(4, 4)]));
    }

    [Fact]
    public void NeverTheStartRoom_AlwaysAFloorTileInsideIt()
    {
        // Two hundred seeds, ten floors each: the down-stairs never land in the
        // start room, always on floor, always inside the room they were drawn in.
        const int Seeds = 200, Floors = 10;
        var failures = new ConcurrentBag<string>();
        Parallel.For(0, Seeds * Floors, k =>
        {
            long seed = SweepSeed(k / Floors);
            int n = k % Floors;
            var map = FloorMap(seed, n);
            var stairs = StairPlacer.Place(map, seed, n);
            var (r, c) = stairs.Far;

            int startRoom = map.DebugRooms.FindIndex(room => (room.Cy, room.Cx) == map.PlayerStart);
            int drawnIn = map.RoomGrid[r, c];
            if (map.Grid[r, c] != 0)
                failures.Add($"seed {seed} floor {n}: {stairs.Far} is a wall");
            if (drawnIn < 0 || !map.DebugRooms[drawnIn].Contains(r, c))
                failures.Add($"seed {seed} floor {n}: {stairs.Far} is not inside a room");
            if (drawnIn == startRoom)
                failures.Add($"seed {seed} floor {n}: {stairs.Far} is in the start room");
            if (stairs.Up != map.PlayerStart)
                failures.Add($"seed {seed} floor {n}: up-stairs {stairs.Up} are not the start {map.PlayerStart}");
        });
        Assert.Empty(failures.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheStairSeedIsTheDocsFormulaVerbatim()
    {
        // PHASE4-011: seed = mapSeed ^ StairSalt ^ floorIndex, with the value the
        // plan fixes for the salt the doc leaves open (PHASE4-012) — a literal
        // here on purpose, so this pins the formula rather than restating it.
        Assert.Equal(0xC2B2AE35, StairPlacer.StairSalt);
        foreach (long seed in new[] { GoldenSeed, 0L, 1L, int.MaxValue, int.MaxValue + 1L, uint.MaxValue })
            for (int f = 0; f < 32; f++)
                Assert.Equal(seed ^ 0xC2B2AE35 ^ f, StairPlacer.SeedFor(seed, f));
    }

    [Fact]
    public void TheTileComesOffItsOwnStream()
    {
        // PHASE4-010, -018: the down-stairs are the one draw
        // new Mulberry32(SeedFor(seed, f)) makes over the furthest room's tiles,
        // row-major — and not the draw the placer's stream, the map's fresh
        // stream or the map's stream carried on past generation would make. Each
        // of those agrees with the placed tile only by the odds of landing on one
        // tile of a room, and across a sweep each is caught disagreeing.
        int trials = 0, placerAgrees = 0, mapAgrees = 0, continuedAgrees = 0;
        for (int i = 0; i < 8; i++)
        {
            long seed = SweepSeed(i);
            for (int n = 0; n < 4; n++)
            {
                long floorSeed = DungeonState.FloorSeed(seed, n);
                var continued = new Mulberry32(floorSeed);
                var map = MapGenerator.Generate(continued);
                var room = StairPlacer.FurthestRoom(map);
                (int R, int C) Draw(Mulberry32 rng) => TileOf(room, rng.NextInt(0, room.W * room.H - 1));

                var placed = StairPlacer.Place(map, seed, n).Far;
                Assert.Equal(Draw(new Mulberry32(StairPlacer.SeedFor(seed, n))), placed);

                trials++;
                if (Draw(new Mulberry32(floorSeed ^ EnemyPlacer.SeedSalt)) == placed) placerAgrees++;
                if (Draw(new Mulberry32(floorSeed)) == placed) mapAgrees++;
                if (Draw(continued) == placed) continuedAgrees++;
            }
        }

        Assert.NotEqual(trials, placerAgrees);
        Assert.NotEqual(trials, mapAgrees);
        Assert.NotEqual(trials, continuedAgrees);
    }

    [Fact]
    public void PlacementIsDeterministicForASeedAndAFloor()
    {
        // PHASE4-018: the same (seed, floor) places the same stairs, on a map
        // generated afresh or on the same one, whatever was placed in between —
        // the placer keeps no state and shares no stream.
        for (int i = 0; i < 6; i++)
        {
            long seed = SweepSeed(i);
            for (int n = 0; n < 3; n++)
            {
                var map = FloorMap(seed, n);
                var first = StairPlacer.Place(map, seed, n);

                StairPlacer.Place(FloorMap(seed, n + 1), seed, n + 1);
                StairPlacer.Place(map, seed + 1, n);
                StairPlacer.Place(map, seed, n + 1);

                Assert.Equal(first, StairPlacer.Place(map, seed, n));
                Assert.Equal(first, StairPlacer.Place(FloorMap(seed, n), seed, n));
            }
        }

        // The room is the map's; the tile in it is the floor's own draw. Placed
        // over one map under ten floor indices, the room holds and the tile moves.
        var golden = MapGenerator.Generate(new Mulberry32(GoldenSeed));
        var room = StairPlacer.FurthestRoom(golden);
        var tiles = Enumerable.Range(0, 10).Select(f => StairPlacer.Place(golden, GoldenSeed, f).Far).ToArray();
        Assert.All(tiles, t => Assert.True(room.Contains(t.R, t.C)));
        Assert.True(tiles.Distinct().Count() > 1);
    }
}
