namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A floor's two stair tiles, as <see cref="StairPlacer.Place"/> finds them.
/// </summary>
/// <param name="Up">
/// The arrival tile: <see cref="MapData.PlayerStart"/>, where the party stands
/// when it comes down onto the floor (§4.2: "up-stairs at the arrival tile").
/// On the first floor it is the way out of the dungeon.
/// </param>
/// <param name="Far">
/// A tile inside the room furthest from the arrival tile by A* cost
/// (<see cref="StairPlacer.FurthestRoom"/>): the down-stairs on every floor but
/// the last, and on the last — the boss floor, the dungeon's bottom, where there
/// is no way down — the lair the boss waits in. Drawn without looking at the
/// floor's enemies, so a dummy may spawn on it (<see cref="StairPlacer.Place"/>).
/// </param>
public readonly record struct StairTiles((int R, int C) Up, (int R, int C) Far);

/// <summary>
/// Where a floor's stairs go (§4.2): a post-pass over a map
/// <see cref="MapGenerator"/> has already made, on a stream of its own —
/// <c>seed = mapSeed ^ StairSalt ^ floorIndex</c> — the pattern
/// <see cref="EnemyPlacer"/> established.
/// <para>
/// <strong>The generator gains nothing.</strong> Placing stairs inline would
/// reorder its RNG calls and move the golden dungeon, so this reads the finished
/// map and never touches the map's stream or the placer's: it builds its own
/// <see cref="Mulberry32"/> from <see cref="SeedFor"/> and draws on it exactly
/// once. <see cref="MapGenerator"/>, <see cref="Mulberry32"/> and
/// <see cref="Pathfinder"/> are called, never edited — the one thing asked of
/// <see cref="Pathfinder"/> is a path, and only the comparison made here is new.
/// </para>
/// <para>
/// Down-stairs go in the room furthest from <see cref="MapData.PlayerStart"/>
/// by A* cost through the existing <see cref="Pathfinder"/> — the walk the party
/// actually has to make, not the Manhattan distance
/// <see cref="MapData.EnemyStart"/> measures — and up-stairs at the arrival
/// tile. Engine-free and deterministic: the tiles are a pure function of
/// <c>(map, mapSeed, floorIndex)</c>, so a floor re-derives them rather than a
/// save storing them (§5.1).
/// </para>
/// </summary>
public static class StairPlacer
{
    /// <summary>
    /// The stair stream's splitter (§4.2's <c>StairSalt</c>, a value the doc
    /// leaves open): odd, 32-bit — murmur3's second finalizer multiplier — and
    /// distinct from every salt already in the tree, so no stair stream is a
    /// map's, a placer's or a boss's for any floor (<c>DungeonSeedTests</c>
    /// holds the whole set apart). A compiled constant, never a tuning key: it
    /// decides layout, the golden set's neighbour.
    /// </summary>
    public const long StairSalt = 0xC2B2AE35;

    /// <summary>
    /// What one cardinal step of a path costs, in thousandths of a step: the
    /// thousandfold of <see cref="Pathfinder"/>'s own 1. The unit is in the name
    /// because it is not a distance — nothing here is measured in logic units.
    /// </summary>
    public const int MilliStepsPerCardinal = 1000;

    /// <summary>
    /// What one diagonal step costs, in the same thousandths: the thousandfold
    /// of <see cref="Pathfinder"/>'s 1.414, so the two integers compare exactly
    /// as the pathfinder's own costs do.
    /// </summary>
    public const int MilliStepsPerDiagonal = 1414;

    /// <summary>The stair stream's seed for one floor of one dungeon, verbatim from §4.2: <c>mapSeed ^ StairSalt ^ floorIndex</c>.</summary>
    /// <param name="mapSeed">The dungeon's seed — the visit's, not the floor's.</param>
    /// <param name="floorIndex">The 0-based floor index.</param>
    public static long SeedFor(long mapSeed, int floorIndex) => mapSeed ^ StairSalt ^ floorIndex;

    /// <summary>
    /// The room the down-stairs go in: of <see cref="MapData.DebugRooms"/> — the
    /// pristine room list, for the reason <see cref="EnemyPlacer"/> gives — every
    /// room but the start room, the one whose centre has the greatest A* cost
    /// from <see cref="MapData.PlayerStart"/>. Ties go to the lower room index.
    /// <para>
    /// <strong>The cost is compared as an integer</strong> (<see cref="PathCost"/>),
    /// never as the pathfinder's summed doubles: IEEE addition is not associative,
    /// so two paths with the same step counts taken in a different order can sum
    /// an ulp apart, and a real tie would be settled by rounding noise instead of
    /// by the stated rule. Deterministic either way; the integers make the rule
    /// the one written here.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A room is unreachable from the start, or the map has no room but the start
    /// room — neither happens on a generated map, which is fully connected and
    /// always places more than one room, so either would be a broken map rather
    /// than one to route around.
    /// </exception>
    public static Room FurthestRoom(MapData map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var start = map.PlayerStart;
        Room? furthest = null;
        int furthestCost = -1;
        for (int i = 0; i < map.DebugRooms.Count; i++)
        {
            var room = map.DebugRooms[i];
            if ((room.Cy, room.Cx) == start) continue;   // the start room: the way in, never the way on

            var path = Pathfinder.FindPath(map.Grid, start.Row, start.Col, room.Cy, room.Cx)
                ?? throw new InvalidOperationException(
                    $"Room {i} (centre {room.Cy},{room.Cx}) is unreachable from the start {start}: a generated map is connected, so this one is broken.");

            // Strictly greater: an equal cost keeps the room already held, which
            // is the lower index — the tie-break is the iteration order.
            int cost = PathCost(start, path);
            if (cost > furthestCost)
            {
                furthest = room;
                furthestCost = cost;
            }
        }

        return furthest ?? throw new InvalidOperationException(
            $"The map has no room but the start room {start}: there is nowhere to put the stairs down.");
    }

    /// <summary>
    /// The cost of walking <paramref name="path"/> from <paramref name="from"/>,
    /// in thousandths of a step: <see cref="MilliStepsPerCardinal"/> per cardinal
    /// step and <see cref="MilliStepsPerDiagonal"/> per diagonal. Integers, so
    /// two paths with the same counts cost the same whatever order they take
    /// their diagonals in.
    /// </summary>
    /// <param name="from">The tile the path starts from, which <see cref="Pathfinder.FindPath"/> leaves out of the path it returns.</param>
    /// <param name="path">The tiles walked, each one step from the last.</param>
    /// <exception cref="ArgumentException">Two consecutive tiles are not one step apart.</exception>
    public static int PathCost((int R, int C) from, IReadOnlyList<(int R, int C)> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        int cost = 0;
        var at = from;
        foreach (var step in path)
        {
            int dr = Math.Abs(step.R - at.R);
            int dc = Math.Abs(step.C - at.C);
            if (dr + dc == 1)
                cost += MilliStepsPerCardinal;
            else if (dr == 1 && dc == 1)
                cost += MilliStepsPerDiagonal;
            else
                throw new ArgumentException($"{at} to {step} is not one step: a path moves one tile at a time.", nameof(path));
            at = step;
        }
        return cost;
    }

    /// <summary>
    /// The floor's two stair tiles: <see cref="StairTiles.Up"/> at the arrival
    /// tile, and <see cref="StairTiles.Far"/> drawn inside
    /// <see cref="FurthestRoom"/> by the stair stream's one draw —
    /// <c>NextInt(0, W·H − 1)</c> over the room's tiles, row-major — off
    /// <c>new Mulberry32(SeedFor(mapSeed, floorIndex))</c>. Every room tile is
    /// floor, so the tile is never a wall — though it may be occupied.
    /// <para>
    /// <strong>The draw never looks at the floor's enemies.</strong>
    /// <see cref="EnemyPlacer"/> puts 0–4 dummies on random tiles of the same
    /// room off a stream of its own, so on about one floor in 25 a dummy spawns
    /// on the far tile — the down-stairs, or on the bottom floor the lair
    /// (<c>FloorGenerationTests</c> pins a seed of each, and the rate). That is
    /// kept rather than redrawn: the stairs stay a function of the map and their
    /// own stream, so no change to the placer ever moves a staircase. Whatever
    /// reads the tile must cope with an occupant instead: an arrival places
    /// members around living enemies, never inside one; the boss takes the
    /// nearest free tile from the lair; and a live dummy on the stairs blocks
    /// them only until it walks off to close on the party or falls — a corpse is
    /// walked over — so it guards the stairs and never seals them.
    /// </para>
    /// </summary>
    /// <param name="map">The floor's generated map.</param>
    /// <param name="mapSeed">The dungeon's seed, which the stair seed is derived from.</param>
    /// <param name="floorIndex">The 0-based floor index, which keeps every floor's stair draw its own.</param>
    public static StairTiles Place(MapData map, long mapSeed, int floorIndex)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegative(floorIndex);

        var room = FurthestRoom(map);
        var rng = new Mulberry32(SeedFor(mapSeed, floorIndex));
        int k = rng.NextInt(0, room.W * room.H - 1);
        return new StairTiles(map.PlayerStart, (room.Y + k / room.W, room.X + k % room.W));
    }
}
