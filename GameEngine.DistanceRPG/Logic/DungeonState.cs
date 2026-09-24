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

    /// <summary>
    /// The down-stairs, in the room furthest from the arrival tile by A* cost;
    /// null on the boss floor, the dungeon's bottom (§4.3). A dummy may stand on
    /// them from the start: the stair draw never looks at <see cref="Enemies"/>
    /// (<see cref="StairPlacer.Place"/>), so an arrival here must be placed
    /// around it, never inside it.
    /// </summary>
    public (int R, int C)? DownStairs { get; }

    /// <summary>
    /// On the boss floor, the tile its furthest room holds instead of a way down:
    /// where the boss waits. Null on every other floor. Drawn like the
    /// down-stairs, so a dummy may already stand on it; the boss must then take
    /// the nearest free tile from it, never the dummy's.
    /// </summary>
    public (int R, int C)? Lair { get; }

    /// <summary>Whether the party has stood on this floor this visit: §5.1 saves visited floors only. Set by the visit on arrival.</summary>
    public bool Visited { get; internal set; }

    /// <summary>
    /// Build floor <paramref name="index"/> of the dungeon seeded
    /// <paramref name="mapSeed"/>: the map from <see cref="DungeonState.FloorSeed"/>,
    /// its fog boxes, its enemy rows — placed off the same floor seed and attuned
    /// to <paramref name="def"/>'s <see cref="DungeonDef.Attunement"/> — and its
    /// stairs, each on its own stream, none continuing another and none reading
    /// another's result: a row may stand on the far tile (<see cref="StairPlacer.Place"/>).
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

/// <summary>What using a stair does: the three kinds a floor can hold (§4.2).</summary>
public enum Stair
{
    /// <summary>Down to the next floor, arriving at its up-stairs. Every floor but the bottom has one.</summary>
    Down,

    /// <summary>Up to the floor above, arriving at its down-stairs. Every floor's arrival tile but the first floor's.</summary>
    Up,

    /// <summary>
    /// The first floor's up-stairs: out of the dungeon. The only way out — there
    /// is no shortcut exit (§4.3), so a party leaves by climbing back through
    /// every floor it descended.
    /// </summary>
    Exit,
}

/// <summary>A stair the party could use: what it does and the tile it is on (<see cref="DungeonState.StairUnder"/>).</summary>
/// <param name="Kind">Down, up, or the way out.</param>
/// <param name="Tile">The stair's tile, (row, column).</param>
public readonly record struct StairUse(Stair Kind, (int R, int C) Tile);

/// <summary>
/// Why the party cannot use a stair right now (<see cref="DungeonState.CanUse"/>).
/// These two and nothing else: there is no gate on how many enemies a floor
/// still holds (§4.4 — a percentage-of-enemies gate was considered and
/// rejected), so a floor can always be left.
/// <para>
/// Standing on the stair is not a refusal either. The stair asked about is the
/// one the active member stands on (<see cref="DungeonState.StairUnder"/>) — a
/// player off every stair has nothing to ask — so asking about a stair the
/// active member handed in is not on is a wrong question, answered with an
/// exception.
/// </para>
/// </summary>
public enum StairRefusal
{
    /// <summary>It is not the party's phase — the enemies are moving, the banner is up, the game is over — or no floor's turn system is open.</summary>
    NotThePlayersTurn,

    /// <summary>A living member is further from the stair than <see cref="Tuning.StairGatherTiles"/>: the party moves as one.</summary>
    PartyNotGathered,
}

/// <summary>
/// A visit to a dungeon (§4.5): every floor of it, built at entry and held for
/// as long as the party is inside, the party moving between them by stairs on
/// the visit's one clock, and the boss floor rolled at entry. §5.1's Run group
/// and its per-floor group are this object; <see cref="CampaignState"/> holds
/// it, and leaving drops it whole (§4.1: "leaving the dungeon discards all of
/// it").
/// <para>
/// <strong>A floor left is a floor kept.</strong> Every floor is built once, at
/// <see cref="Enter"/> — a pure function of the map seed, its index and the
/// dungeon — and never rebuilt inside the visit, so going back up a floor is a
/// pointer move: its rows (alive or dead, where they stand, what the farm made
/// of them) and its explored fog are exactly as the party left them.
/// </para>
/// <para>
/// <strong>One clock.</strong> <see cref="Turn"/> is the visit's; each floor's
/// <see cref="TurnSystem"/> opens on it and hands it back when the party leaves.
/// A floor nobody is on is frozen, except for resurrection timers: its dead
/// dummies' waits run on the same clock, and whichever came due while the party
/// was elsewhere are up when it returns (<see cref="TurnSystem.CatchUpRevivals"/>)
/// — up to the turn the boss fell, once it has.
/// </para>
/// <para>
/// <strong>The turn travels.</strong> Taking the stairs moves the party, never
/// the clock: members arrive with the movement they left with, nothing ticks,
/// and the turn's counters — attacks chosen, reactions spent, enemies seen —
/// go with them (<see cref="TurnCarry"/>), so a stair hop is never a refill.
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

    // The turn the visit's clock stood at when the last floor's system handed it
    // back: what Turn reads while no system is open, and what the next one opens on.
    private int _turn;

    // The counters of the turn in progress, merged across every floor the party
    // has stood on in it (TurnCarry.MergedWith). The next system imports it only
    // if it is still this turn's, so a carry that outlived its turn is dead weight
    // until the next transit replaces it.
    private TurnCarry? _carry;

    private DungeonState(DungeonDef def, long mapSeed, int bossFloor, Floor[] floors)
    {
        Def = def;
        MapSeed = mapSeed;
        BossFloor = bossFloor;
        Floors = floors;
    }

    /// <summary>The dungeon being visited.</summary>
    public DungeonDef Def { get; }

    /// <summary>The seed this visit was entered with: rolled per entry by whoever enters (never here), and everything below derives from it.</summary>
    public long MapSeed { get; }

    /// <summary>
    /// The boss floor, a 1-based floor <em>number</em> (<see cref="RollBossFloor(long, DungeonDef)"/>),
    /// and so the dungeon's depth: <see cref="Floors"/> holds exactly this many.
    /// Fixed at entry and shown to nobody before it is reached (§4.3).
    /// </summary>
    public int BossFloor { get; }

    /// <summary>
    /// Every floor of the visit, index 0 first — the first floor, whose up-stairs
    /// are the way out — to the boss floor at <c>BossFloor − 1</c>, the bottom.
    /// All of them built at <see cref="Enter"/> and none rebuilt inside the visit.
    /// </summary>
    public Floor[] Floors { get; }

    /// <summary>The 0-based index of the floor the party is on. The floor number a player reads is this plus one.</summary>
    public int FloorIndex { get; private set; }

    /// <summary>The floor the party is on.</summary>
    public Floor Current => Floors[FloorIndex];

    /// <summary>The deepest floor index the party has stood on this visit: what a failed run's mercy tick will read (§6.3's "floor 3 or deeper").</summary>
    public int DeepestFloorIndex { get; private set; }

    /// <summary>
    /// The visit's one clock (§4.1): the open floor's <see cref="TurnSystem.TurnCount"/>
    /// while one is open, and the count the last one handed back while none is.
    /// It starts at 0 on entry and only ever runs forward; taking the stairs
    /// moves no clock.
    /// </summary>
    public int Turn => Turns?.TurnCount ?? _turn;

    /// <summary>
    /// The turn system <see cref="OpenTurns"/> opened on <see cref="Current"/> —
    /// the one whoever plays the floor must hold — and null from a transit until
    /// the next <see cref="OpenTurns"/>. The previous floor's is dropped whole on
    /// the stairs, subscriptions and all.
    /// </summary>
    public TurnSystem? Turns { get; private set; }

    /// <summary>Whether the boss is dead (§4.3): from then on every floor opens with resurrection stopped. Never unset within the visit.</summary>
    public bool BossDefeated { get; private set; }

    /// <summary>The turn the boss fell, or -1 while it stands: the cutoff a floor's catch-up stops its clock at, since nothing revives after it.</summary>
    public int BossDefeatedAtTurn { get; private set; } = -1;

    /// <summary>
    /// Raised on every transit, either way, once every member stands on the
    /// floor reached and before its turn system opens: §6.7's "reaching a new
    /// floor" signal on the floor-transit path. Entering the dungeon is not a
    /// transit and raises nothing.
    /// <para>
    /// The second argument tells a first arrival from a return: true the first
    /// time the party stands on the floor this visit — always a descent, onto a
    /// floor deeper than any before it — and false on every return, down as well
    /// as up. It is the only way to tell: by the time this is raised the floor is
    /// already <see cref="Floor.Visited"/> and <see cref="DeepestFloorIndex"/>
    /// already counts it.
    /// </para>
    /// </summary>
    public event Action<Floor, bool>? FloorReached;

    /// <summary>
    /// Enter <paramref name="def"/> on <paramref name="mapSeed"/> (§4.5): roll the
    /// boss floor (<see cref="RollBossFloor(long, DungeonDef)"/>), build every
    /// floor down to it — each <see cref="Floor.Generate"/>, a pure function of
    /// the seed, its index and the dungeon, the last one the bottom — and stand
    /// the party at the first floor's arrival tile, each member on its own tile
    /// in roster order, as the scene has always spawned them.
    /// <para>
    /// Rolls nothing it is not handed: the seed is the caller's, drawn per entry
    /// — which is what re-rolls a failed dungeon's depth — so the same seed is
    /// the same dungeon, and the shipped seed's first floor is the shipped map.
    /// Static and campaign-free: the visit never holds the campaign that holds
    /// it, and it holds no party of its own — the members are the campaign's
    /// permanent half and are handed in wherever they are moved.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">The party is empty or names a null member: a visit needs someone to make it.</exception>
    public static DungeonState Enter(DungeonDef def, long mapSeed, IReadOnlyList<PartyMemberState> party)
    {
        ArgumentNullException.ThrowIfNull(def);
        RequireParty(party);

        int bossFloor = RollBossFloor(mapSeed, def);
        var floors = new Floor[bossFloor];
        for (int n = 0; n < bossFloor; n++)
            floors[n] = Floor.Generate(mapSeed, n, bottom: n == bossFloor - 1, def);

        var visit = new DungeonState(def, mapSeed, bossFloor, floors);
        visit.Arrive(party, floors[0].UpStairs);
        return visit;
    }

    /// <summary>
    /// Open the current floor's turn system on the visit's clock, and return it
    /// with the floor already caught up (§4.1).
    /// <para>
    /// It opens at <see cref="Turn"/> in the player phase, with this turn's
    /// counters from the floors already stood on in it (<see cref="TurnCarry"/>),
    /// the floor's own seed for its drops and revivals, and resurrection off once
    /// the boss is dead. Then every dummy here whose wait ran out while the party
    /// was elsewhere gets up (<see cref="TurnSystem.CatchUpRevivals"/>) — as of
    /// now, or as of the turn the boss fell once it has — and the zones are
    /// re-read around them. So the system comes back finished: whoever plays the
    /// floor builds it from the rows afterwards and learns of those revivals from
    /// the rows, never from <see cref="TurnSystem.EnemyResurrected"/>, which had no
    /// subscriber when it fired. Called once per arrival, after the party is
    /// placed (<see cref="Enter"/>, <see cref="Descend"/>, <see cref="Ascend"/>
    /// place it), so nothing wakes inside a member.
    /// </para>
    /// </summary>
    /// <param name="party">The members, in roster order — the same list every call of the visit is handed.</param>
    /// <param name="rollD20">The d20 the system rolls; its own fair die when omitted.</param>
    /// <exception cref="InvalidOperationException">A system is already open on this floor: only the stairs close one.</exception>
    public TurnSystem OpenTurns(IReadOnlyList<PartyMemberState> party, Func<int>? rollD20 = null)
    {
        RequireParty(party);
        if (Turns is not null)
            throw new InvalidOperationException(
                $"Floor {Current.Index + 1}'s turn system is already open: a floor is opened once per arrival, and only the stairs close it.");

        var floor = Current;
        var turns = new TurnSystem(floor.Map.Grid, party, floor.Enemies, rollD20, floor.Seed, firstTurn: _turn, carry: _carry)
        {
            ResurrectionActive = !BossDefeated,
        };
        turns.CatchUpRevivals(BossDefeated ? BossDefeatedAtTurn : _turn);
        Turns = turns;
        return turns;
    }

    /// <summary>
    /// The stair <paramref name="member"/> stands on — its tile is the stair's —
    /// or null: off every stair, dead, or on the bottom floor's lair, which leads
    /// nowhere. The first floor's arrival tile is the way out
    /// (<see cref="Stair.Exit"/>); every other floor's is <see cref="Stair.Up"/>.
    /// </summary>
    public StairUse? StairUnder(ActorState member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!member.Alive) return null;

        var tile = TileOf(member);
        foreach (var stair in StairsOf(Current))
            if (stair.Tile == tile)
                return stair;
        return null;
    }

    /// <summary>
    /// How many living members stand within <see cref="Tuning.StairGatherTiles"/>
    /// of <paramref name="stair"/> — centre to centre, the tiles converted to
    /// logic units (<see cref="GameConstants.LogicUnitsPerTile"/>) — which admits
    /// a marching line three ranks deep behind whoever is on the stair. The
    /// fallen are never counted: they are carried, not gathered.
    /// </summary>
    public int Gathered(IReadOnlyList<PartyMemberState> party, (int R, int C) stair)
    {
        ArgumentNullException.ThrowIfNull(party);

        float reachUnits = GameContent.Current.Tuning.StairGatherTiles * GameConstants.LogicUnitsPerTile;
        var (x, y) = CentreOf(stair);
        int gathered = 0;
        foreach (var member in party)
        {
            if (!member.Alive) continue;
            float dx = member.X - x, dy = member.Y - y;
            if (dx * dx + dy * dy <= reachUnits * reachUnits)
                gathered++;
        }
        return gathered;
    }

    /// <summary>
    /// Whether the party may use <paramref name="stair"/> now, or why not: only
    /// in the player phase of an open floor, and only with every living member
    /// gathered at it (<see cref="Gathered"/>) — the party moves as one, so
    /// nobody is ever on another floor, the fallen included. Nothing else is
    /// asked: a floor may be left with every enemy on it standing (§4.4: no gate).
    /// <para>
    /// <strong>Taken from the stair tile.</strong> The stairs are used by the
    /// active member standing on them, so the stair asked about is the one
    /// <see cref="StairUnder"/> finds under that member. Handed
    /// <paramref name="active"/>, the visit holds that half of the rule too: a
    /// party gathered beside a stair nobody stands on cannot take it — around a
    /// live dummy on the very tile, say, which guards the stairs until it falls
    /// or walks off, and never seals them. Whoever plays the floor passes the
    /// active member here and to <see cref="Descend"/> and <see cref="Ascend"/>.
    /// </para>
    /// </summary>
    /// <param name="stair">A stair of the current floor: the one <see cref="StairUnder"/> found under the active member.</param>
    /// <param name="party">The members, in roster order.</param>
    /// <param name="active">
    /// The member taking the stairs: one of <paramref name="party"/>, standing on
    /// <paramref name="stair"/>. Omitted, only the party's half is asked — the
    /// phase and the gathering.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="stair"/> is not a stair of the current floor, or
    /// <paramref name="active"/> is given and is not one of the party or does not stand on <paramref name="stair"/>.
    /// </exception>
    public StairRefusal? CanUse(StairUse stair, IReadOnlyList<PartyMemberState> party, PartyMemberState? active = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (!StairsOf(Current).Contains(stair))
            throw new ArgumentException($"{stair} is not a stair of floor {Current.Index + 1}.", nameof(stair));
        if (active is not null)
            RequireStandingOn(stair, party, active);

        if (Turns is not { Phase: TurnPhase.Player })
            return StairRefusal.NotThePlayersTurn;
        return Gathered(party, stair.Tile) < party.Count(m => m.Alive) ? StairRefusal.PartyNotGathered : null;
    }

    /// <summary>
    /// Take the down-stairs: one floor down, never more (§4.3 — floors are
    /// adjacent, and the bottom has no way down). The clock stops where it stood,
    /// the turn's counters go with the party (<see cref="TurnCarry"/>), the
    /// floor's system is dropped, and every member — the fallen too, carried —
    /// stands around the next floor's up-stairs. Open the new floor with
    /// <see cref="OpenTurns"/>.
    /// </summary>
    /// <param name="party">The members, in roster order — the fallen among them, who are carried.</param>
    /// <param name="active">The member taking the stairs, who must stand on them (<see cref="CanUse"/>). Omitted, only the party's half of the rule is asked.</param>
    /// <exception cref="InvalidOperationException">This is the bottom floor, or <see cref="CanUse"/> refuses the stairs.</exception>
    /// <exception cref="ArgumentException"><paramref name="active"/> is given and is not one of the party or does not stand on the down-stairs.</exception>
    public void Descend(IReadOnlyList<PartyMemberState> party, PartyMemberState? active = null)
    {
        RequireParty(party);
        var down = Current.DownStairs
            ?? throw new InvalidOperationException($"Floor {Current.Index + 1} is the dungeon's bottom: there is no way down from it.");
        Transit(party, active, new StairUse(Stair.Down, down), FloorIndex + 1);
    }

    /// <summary>
    /// Take the up-stairs: one floor up, arriving around that floor's
    /// down-stairs — the way back out goes through every floor the party came
    /// down (§4.4). Everything else as <see cref="Descend"/>. The first floor's
    /// up-stairs are the way out, which ends the visit and is not a transit.
    /// </summary>
    /// <param name="party">The members, in roster order — the fallen among them, who are carried.</param>
    /// <param name="active">The member taking the stairs, who must stand on them (<see cref="CanUse"/>). Omitted, only the party's half of the rule is asked.</param>
    /// <exception cref="InvalidOperationException">This is the first floor, or <see cref="CanUse"/> refuses the stairs.</exception>
    /// <exception cref="ArgumentException"><paramref name="active"/> is given and is not one of the party or does not stand on the up-stairs.</exception>
    public void Ascend(IReadOnlyList<PartyMemberState> party, PartyMemberState? active = null)
    {
        RequireParty(party);
        if (FloorIndex == 0)
            throw new InvalidOperationException("The first floor's up-stairs are the way out of the dungeon: leaving ends the visit, it moves no floor.");
        Transit(party, active, new StairUse(Stair.Up, Current.UpStairs), FloorIndex - 1);
    }

    /// <summary>
    /// The boss is dead (§4.3): resurrection stops for the rest of the visit, on
    /// every floor. The open floor's system stops at once; every floor opened
    /// afterwards opens with it stopped; and a floor caught up afterwards is
    /// caught up only to this turn, so a return that fell due before the boss
    /// fell still happens and none after it does. One-way, and a second call
    /// moves nothing. The boss's death calls this; so, until the boss exists,
    /// does the scene's dev switch.
    /// </summary>
    public void RecordBossDefeated()
    {
        if (BossDefeated) return;
        BossDefeated = true;
        BossDefeatedAtTurn = Turn;
        if (Turns is not null)
            Turns.ResurrectionActive = false;
    }

    /// <summary>The one transit: the stair asked of, the clock and the counters handed back, the old system dropped, the party stood on floor <paramref name="to"/>.</summary>
    private void Transit(IReadOnlyList<PartyMemberState> party, PartyMemberState? active, StairUse stair, int to)
    {
        if (CanUse(stair, party, active) is { } refusal)
            throw new InvalidOperationException($"The party cannot take the {stair.Kind} stairs of floor {Current.Index + 1}: {refusal}.");

        // CanUse refused a missing system, so there is one to hand the turn back.
        var leaving = Turns!;
        _turn = leaving.TurnCount;
        var carry = leaving.ExportCarry();
        _carry = _carry is null ? carry : _carry.MergedWith(carry);
        Turns = null;

        bool down = to > FloorIndex;
        FloorIndex = to;
        DeepestFloorIndex = Math.Max(DeepestFloorIndex, to);
        var floor = Current;
        bool firstVisit = !floor.Visited;   // read before the arrival marks it: afterwards every floor reached is visited
        Arrive(party, down ? floor.UpStairs : floor.DownStairs!.Value);
        FloorReached?.Invoke(floor, firstVisit);
    }

    /// <summary>
    /// Stand every member — the fallen too — on the current floor around
    /// <paramref name="arrival"/>, one tile each in roster order
    /// (<see cref="GridUtils.FindPartySpawnTiles"/>), never on a living enemy: a
    /// dummy may stand on the very stairs, and the party arrives around it. A
    /// corpse is walked over, so a dead one's tile is anyone's.
    /// </summary>
    private void Arrive(IReadOnlyList<PartyMemberState> party, (int R, int C) arrival)
    {
        var floor = Current;
        var occupied = new HashSet<(int R, int C)>();
        foreach (var enemy in floor.Enemies)
            if (enemy.Alive)
                occupied.Add(TileOf(enemy));

        var tiles = GridUtils.FindPartySpawnTiles(floor.Map.Grid, arrival.R, arrival.C, party.Count, occupied);
        for (int i = 0; i < party.Count; i++)
            (party[i].X, party[i].Y) = CentreOf(tiles[i]);
        floor.Visited = true;
    }

    /// <summary>The stairs <paramref name="floor"/> holds: its arrival tile — the way out on the first floor, the way up on every other — and its way down, except on the bottom.</summary>
    private static IEnumerable<StairUse> StairsOf(Floor floor)
    {
        yield return new StairUse(floor.Index == 0 ? Stair.Exit : Stair.Up, floor.UpStairs);
        if (floor.DownStairs is { } down)
            yield return new StairUse(Stair.Down, down);
    }

    /// <summary>
    /// The stair-tile half of the rule: <paramref name="active"/> is one of the
    /// party and stands on <paramref name="stair"/> — alive, on its tile
    /// (<see cref="StairUnder"/>). A party gathered beside the stairs, or around
    /// a live dummy standing on them, has nobody on them to take them.
    /// </summary>
    private void RequireStandingOn(StairUse stair, IReadOnlyList<PartyMemberState> party, PartyMemberState active)
    {
        if (!party.Contains(active))
            throw new ArgumentException($"Member '{active.Id}' is not one of the party: it takes no stairs for it.", nameof(active));
        if (StairUnder(active) != stair)
            throw new ArgumentException(
                $"Member '{active.Id}' does not stand on the {stair.Kind} stairs at {stair.Tile}: the stairs are taken from the stair tile, by the member standing on it.",
                nameof(active));
    }

    private static void RequireParty(IReadOnlyList<PartyMemberState> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.Count == 0)
            throw new ArgumentException("A visit needs a party: nobody was handed in.", nameof(party));
        if (party.Any(member => member is null))
            throw new ArgumentException("The party names a null member.", nameof(party));
    }

    private static (int R, int C) TileOf(ActorState actor)
        => ((int)MathF.Floor(actor.Y / GameConstants.Tile), (int)MathF.Floor(actor.X / GameConstants.Tile));

    private static (float X, float Y) CentreOf((int R, int C) tile)
        => (tile.C * GameConstants.Tile + GameConstants.Tile / 2f, tile.R * GameConstants.Tile + GameConstants.Tile / 2f);

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
