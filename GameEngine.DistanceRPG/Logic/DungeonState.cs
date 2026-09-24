namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One floor of a dungeon (§4.5): one <see cref="MapGenerator"/> map, the rows
/// <see cref="EnemyPlacer"/> put on it, its explored fog, and its stairs — each a
/// pure function of <c>(mapSeed, index, def)</c>, so the same arguments build the
/// same floor on every run and every machine.
/// <para>
/// <strong>Stored versus derived.</strong> What a visit changes lives here and
/// is what a save would store (§5.1): the enemy rows — position, HP,
/// <c>Alive</c>, the farm's history — and <see cref="Fog"/>'s explored memory.
/// Everything else re-derives bit-identically from the seed: the map, the fog
/// boxes, the stairs (their own stream, <see cref="StairPlacer"/>), and every
/// ground item a permanent kill left (<see cref="TurnSystem.GroundItemOf"/>).
/// So a floor left and returned to is a pointer move, never a rebuild.
/// </para>
/// <para>
/// <strong>Floor 0 is the golden dungeon, by arithmetic.</strong>
/// <see cref="DungeonState.FloorSeed"/> of index 0 is the map seed itself, so
/// floor 0 makes exactly the three calls the one-floor game always made —
/// <c>MapGenerator.Generate(new Mulberry32(mapSeed))</c>,
/// <see cref="FogBoxBuilder.Build"/>, <c>EnemyPlacer.PlaceEnemies(map, mapSeed)</c>
/// — and the shipped seed's floor 0 is the shipped dungeon to the tile.
/// </para>
/// </summary>
public sealed class Floor
{
    private Floor(int index, long seed, MapData map, FogBoxSet fogBoxes, IReadOnlyList<EnemyState> enemies,
        StairTiles stairs, bool bottom)
    {
        Index = index;
        Seed = seed;
        Map = map;
        FogBoxes = fogBoxes;
        Fog = new FogState(map.Grid.GetLength(0), map.Grid.GetLength(1));
        Enemies = enemies;
        UpStairs = stairs.Up;
        DownStairs = bottom ? null : stairs.Far;
        Lair = bottom ? stairs.Far : null;
    }

    /// <summary>The 0-based floor index: what every seed reads. The floor <em>number</em> a player reads is <c>Index + 1</c>.</summary>
    public int Index { get; }

    /// <summary>
    /// <see cref="DungeonState.FloorSeed"/> of this floor: what its map was
    /// generated from, what <see cref="EnemyPlacer"/> placed its rows from, and
    /// the <c>mapSeed</c> this floor's turn system rolls its drops and revivals
    /// off (§3.2, §3.5) — so spawn index 3 on two floors shares neither a drop
    /// nor a ladder, and no Phase 3 mix needs a floor term.
    /// </summary>
    public long Seed { get; }

    /// <summary>The floor's map, regenerated from <see cref="Seed"/> and never saved. Its <see cref="MapData.Corridors"/> are expanded in place by <see cref="FogBoxes"/>' build, as the scene's always were.</summary>
    public MapData Map { get; }

    /// <summary>The fog reveal boxes of <see cref="Map"/> (<see cref="FogBoxBuilder.Build"/>).</summary>
    public FogBoxSet FogBoxes { get; }

    /// <summary>
    /// This floor's fog of war, per floor rather than per scene (§4.5): what the
    /// party has explored here stays explored while it is anywhere else in the
    /// dungeon, and a new floor starts dark.
    /// </summary>
    public FogState Fog { get; }

    /// <summary>
    /// The floor's actors other than the party: <see cref="EnemyPlacer"/>'s rows
    /// in spawn order, each with its <see cref="EnemyState.SpawnIndex"/> and the
    /// dungeon's <see cref="DungeonDef.Attunement"/>. The rows are the floor's
    /// stored state — whatever happens to them is what a return finds.
    /// </summary>
    public IReadOnlyList<EnemyState> Enemies { get; }

    /// <summary>The arrival tile, <see cref="MapData.PlayerStart"/> (§4.2). On the first floor it is the way out.</summary>
    public (int R, int C) UpStairs { get; }

    /// <summary>The down-stairs, in the room furthest from the arrival tile by A* cost; null on the boss floor, the dungeon's bottom (§4.3).</summary>
    public (int R, int C)? DownStairs { get; }

    /// <summary>On the boss floor, the tile its furthest room holds instead of a way down: where the boss waits. Null on every other floor.</summary>
    public (int R, int C)? Lair { get; }

    /// <summary>Whether the party has stood on this floor this visit: §5.1 saves visited floors only. Set by the visit on arrival.</summary>
    public bool Visited { get; internal set; }

    /// <summary>
    /// Build floor <paramref name="index"/> of the dungeon seeded
    /// <paramref name="mapSeed"/>: the map from <see cref="DungeonState.FloorSeed"/>,
    /// its fog boxes, its enemy rows — placed off the same floor seed and attuned
    /// to <paramref name="def"/>'s <see cref="DungeonDef.Attunement"/> — and its
    /// stairs, each on its own stream, none continuing another.
    /// </summary>
    /// <param name="mapSeed">The dungeon's seed, rolled once per entry.</param>
    /// <param name="index">The 0-based floor index.</param>
    /// <param name="bottom">
    /// Whether this is the boss floor, the dungeon's bottom: its far tile is then
    /// the <see cref="Lair"/> and there is no <see cref="DownStairs"/>. It changes
    /// no draw — the map, the rows and the stair tile are the same either way.
    /// </param>
    /// <param name="def">The dungeon, whose attunement every enemy it spawns carries.</param>
    internal static Floor Generate(long mapSeed, int index, bool bottom, DungeonDef def)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(def);

        long seed = DungeonState.FloorSeed(mapSeed, index);
        var map = MapGenerator.Generate(new Mulberry32(seed));
        var fogBoxes = FogBoxBuilder.Build(map);   // expands map.Corridors in place, exactly as the scene always has
        var enemies = PlaceEnemies(map, seed, def);
        var stairs = StairPlacer.Place(map, mapSeed, index);
        return new Floor(index, seed, map, fogBoxes, enemies, stairs, bottom);
    }

    /// <summary>
    /// The enemy rows, first entry: <see cref="EnemyPlacer"/> decides where they
    /// stand and what they hold off the floor's seed, on its own stream; each row
    /// keeps the index it was placed at (§3.2's <see cref="EnemyState.SpawnIndex"/>),
    /// which seeds its drop and its revival ladder, and carries the dungeon's
    /// attunement — the dungeon's and never the individual's (§1.4, §4.3). A
    /// save restores these rows instead of placing them again (§5.1).
    /// </summary>
    private static EnemyState[] PlaceEnemies(MapData map, long floorSeed, DungeonDef def)
    {
        var weapons = GameContent.Current.Weapons;
        var rows = new List<EnemyState>();
        foreach (var (x, y, weaponId) in EnemyPlacer.PlaceEnemies(map, floorSeed))
        {
            rows.Add(new EnemyState
            {
                X = x,
                Y = y,
                Weapon = weapons.Instantiate(weaponId),
                SpawnIndex = rows.Count,
                Attunement = def.Attunement,
            });
        }
        return rows.ToArray();
    }
}

/// <summary>
/// A visit to a dungeon (§4.5): every floor of it, the boss floor rolled at
/// entry, and the stream arithmetic that makes each floor its own dungeon.
/// <para>
/// This is the static half — the per-floor seed and the boss floor's roll, pure
/// functions of the map seed. The visit itself (the floors held, the party
/// moving between them, the one clock) is built on it.
/// </para>
/// <para>
/// <strong>Every stream here is its own</strong>, derived from the map seed with
/// a named salt exactly as <see cref="EnemyPlacer"/> derives its own, never a
/// continuation of another and never <c>Random.Shared</c>. The salts are
/// compiled constants and never tuning keys: they decide dungeon layout, which
/// is the golden set's neighbour.
/// </para>
/// </summary>
public sealed class DungeonState
{
    /// <summary>
    /// The per-floor splitter (§4.2's <c>FloorSalt</c>, a value the doc leaves
    /// open): odd, 32-bit — murmur3's first finalizer multiplier — and distinct
    /// from every other salt in the tree. Multiplied by the floor index, so floor
    /// 0's term is 0 and floor 0 is the map seed itself. <see cref="Mulberry32"/>
    /// keeps a seed's low 32 bits, so <c>FloorSalt × n</c> running past 2³² is
    /// harmless and deterministic.
    /// </summary>
    public const long FloorSalt = 0x85EBCA6B;

    /// <summary>
    /// The boss stream's splitter (§4.3's <c>BossSalt</c>, a value the doc leaves
    /// open): odd, 32-bit — xxHash's PRIME32_5 — and distinct from every other
    /// salt in the tree. One stream per visit, drawn once per visit at entry.
    /// </summary>
    public const long BossSalt = 0x165667B1;

    private DungeonState()
    {
    }

    /// <summary>
    /// Floor <paramref name="n"/>'s seed, verbatim from §4.2:
    /// <c>floorSeed(n) = mapSeed ^ (FloorSalt * n)</c>. Floor 0 resolves to exactly
    /// <paramref name="mapSeed"/> — <c>FloorSalt × 0</c> is 0 and <c>x ^ 0</c> is
    /// <c>x</c> — so floor 0 stays golden. Raw rather than finalized: the formula
    /// is the doc's, and floor 0 must stay the map seed.
    /// </summary>
    /// <param name="mapSeed">The dungeon's seed.</param>
    /// <param name="n">The 0-based floor index.</param>
    public static long FloorSeed(long mapSeed, int n) => mapSeed ^ (FloorSalt * n);

    /// <summary>
    /// The boss floor, a 1-based floor <em>number</em> — the dungeon's depth,
    /// decided at entry "before you take the first step" and fixed for the visit
    /// (§4.3): <c>min + new Mulberry32(mapSeed ^ BossSalt).NextInt(0, max − min)</c>
    /// over <paramref name="def"/>'s range (<see cref="DungeonCatalogue.BossFloorRange"/>).
    /// At the tuning's 5–10 that is the doc's own
    /// <c>5 + new Mulberry32(mapSeed ^ BossSalt).NextInt(0, 5)</c>; the tutorial
    /// pins both ends to 2 and always comes out 2. Shown to nobody before it is
    /// reached (§4.3: the roll is not disclosed).
    /// </summary>
    /// <param name="mapSeed">The dungeon's seed, rolled fresh per entry — which is what re-rolls a failed dungeon's depth.</param>
    /// <param name="def">The dungeon whose range is rolled over.</param>
    public static int RollBossFloor(long mapSeed, DungeonDef def) => RollBossFloor(BossStream(mapSeed), def);

    /// <summary>The boss stream, raw as §4.3 writes it: <c>new Mulberry32(mapSeed ^ BossSalt)</c>. Its first draw is the floor.</summary>
    internal static Mulberry32 BossStream(long mapSeed) => new(mapSeed ^ BossSalt);

    /// <summary>
    /// The boss stream's first draw, the floor. <strong>The draw is taken
    /// whatever the range</strong> — a pinned range still draws
    /// <c>NextInt(0, 0)</c> — so every later draw on the stream sits where it
    /// would for any range, and the tutorial's boss rolls what it holds off the
    /// same position every other dungeon's does.
    /// </summary>
    internal static int RollBossFloor(Mulberry32 bossStream, DungeonDef def)
    {
        ArgumentNullException.ThrowIfNull(bossStream);
        ArgumentNullException.ThrowIfNull(def);

        var (min, max) = GameContent.Current.Dungeons.BossFloorRange(def);
        return min + bossStream.NextInt(0, max - min);
    }
}
