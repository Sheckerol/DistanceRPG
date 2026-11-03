namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One floor of a dungeon (§4.5): one <see cref="MapGenerator"/> map, the rows
/// <see cref="EnemyPlacer"/> put on it, its explored fog, and its stairs — and,
/// on the bottom floor, the dungeon's boss — each a pure function of
/// <c>(mapSeed, index, def)</c>, so the same arguments build the same floor on
/// every run and every machine.
/// <para>
/// <strong>Stored versus derived.</strong> What a visit changes lives here and
/// is what a save stores (§5.1): the enemy rows — position, HP,
/// <c>Alive</c>, the farm's history — <see cref="Fog"/>'s explored memory, and
/// the weapons the party put down here (<see cref="LooseItems"/>), which nothing
/// could give back. Everything else re-derives bit-identically from the seed:
/// the map, the fog boxes, the stairs (their own stream, <see cref="StairPlacer"/>), and every
/// ground item a permanent kill left (<see cref="TurnSystem.GroundItemOf"/>) — which a
/// save pins as it lay, since its derivation reads the loaded tuning and content
/// too (<see cref="EnemyState.PinnedDrop"/>). So a floor left and returned to is a
/// pointer move, never a rebuild, and a floor loaded is its terrain regenerated
/// with the saved state stood on it (<see cref="Restore"/>). The
/// boss's row is a row like the rest — where it stands, what it holds, whether
/// it has fallen — and only its statline and its theme are content's to
/// re-derive; the stacks its drop is promised are the visit's
/// (<see cref="DungeonState.PromisedStacks"/>).
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
        StairTiles stairs, bool bottom, EnemyState? boss)
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
        Boss = boss;
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
    /// dungeon's <see cref="DungeonDef.Attunement"/>, and on the bottom floor the
    /// <see cref="Boss"/> last of all. The rows are the floor's stored state —
    /// whatever happens to them is what a return finds.
    /// </summary>
    public IReadOnlyList<EnemyState> Enemies { get; }

    /// <summary>
    /// The dungeon's one boss (§4.3), on the bottom floor and nowhere else: the
    /// last of <see cref="Enemies"/>, its spawn index after every dummy's, waiting
    /// in the <see cref="Lair"/> — or on the nearest free tile to it, when a dummy
    /// was placed there first. Null on every other floor. Its death stops
    /// resurrection for the rest of the visit (<see cref="DungeonState.OpenTurns"/>
    /// hands it to the floor's turn system as the boss).
    /// </summary>
    public EnemyState? Boss { get; }

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
    /// down-stairs, so a dummy may already stand on it; the boss then takes the
    /// nearest free tile from it (<see cref="GridUtils.NearestFreeTile"/>), never
    /// the dummy's.
    /// </summary>
    public (int R, int C)? Lair { get; }

    /// <summary>Whether the party has stood on this floor this visit: §5.1 saves visited floors only. Set by the visit on arrival, and by a load for every floor it restores (<see cref="Restore"/>).</summary>
    public bool Visited { get; internal set; }

    /// <summary>
    /// The weapons the party has put down on this floor (<see cref="Leave"/>), in
    /// the order they were left. This is the one list a floor stores rather than
    /// re-derives: no corpse fixes a loose item and no seed rolls it, so a save
    /// keeps each whole — the weapon and its tile (§5.1). Each one lies here for
    /// the rest of the visit, whichever floor the party is on, until someone takes
    /// it back (<see cref="Pickup.TakeLoose"/>). A new visit's floors hold none:
    /// leaving the dungeon discards everything in it (§4.1), and a weapon left
    /// behind is in it now.
    /// </summary>
    public List<LooseItem> LooseItems { get; } = new();

    /// <summary>
    /// <paramref name="member"/> puts down the weapon in its bag slot
    /// <paramref name="slot"/> where it stands (§4.4): out of the bag and onto
    /// this floor at the member's tile, where it lies for the rest of the visit
    /// (<see cref="LooseItems"/>). It is free. It is not a swap, and the weapon in
    /// hand does not change.
    /// <para>
    /// <strong>This is what makes the climb out <em>targeted</em>.</strong> Loot
    /// and weapons share the same six slots (there is no haul bag), so a full bag
    /// can still trade: the weak pickup goes down beside the better drop it makes
    /// room for, and the better one goes into the slot it freed.
    /// </para>
    /// <para>
    /// Only a bag slot, 1 and up. Slot 0 is the weapon in hand, and nobody empties
    /// their own hand this way. The weapon is still whatever it was: a spoil stays
    /// a spoil, and gear stays gear. But a spoil lying here is in no bag, so the
    /// run's end counts it neither kept nor lost
    /// (<see cref="CampaignState.EndRun"/>): it is lost with the dungeon unless
    /// someone picks it up again.
    /// </para>
    /// <para>
    /// <strong>A weapon only.</strong> A potion is poured out, never left: it is
    /// gone, not lying here (PHASE 6b DECISION 1,
    /// <see cref="PartyMemberState.TakePotion"/>), so a floor and its save hold
    /// weapons alone.
    /// </para>
    /// </summary>
    /// <param name="member">The member putting it down, standing on this floor.</param>
    /// <param name="slot">The bag slot it comes out of: 1 up to the member's last slot.</param>
    /// <returns>The item now lying on this floor, the last of <see cref="LooseItems"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slot"/> is the hand (0), or no slot the member carries.</exception>
    /// <exception cref="ArgumentException">
    /// The slot is empty, so there is nothing to put down, or holds a potion, which
    /// is poured out and never left; or the member does not stand on a floor tile
    /// of this floor.
    /// </exception>
    public LooseItem Leave(PartyMemberState member, int slot)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (slot < 1 || slot >= member.Inventory.Length)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, slot == 0
                ? "Slot 0 is the weapon in hand: only a bag slot is put down, and nobody empties their own hand this way."
                : $"Member '{member.Id}' carries slots 0 to {member.Inventory.Length - 1}, the hand and its bag: there is no slot {slot}.");
        if (member.Inventory[slot] is Potion potion)
            throw new ArgumentException($"Slot {slot} of member '{member.Id}' holds a {potion.Type} potion: a potion is poured out, never left (PHASE 6b DECISION 1).", nameof(slot));
        var weapon = member.WeaponIn(slot)
            ?? throw new ArgumentException($"Slot {slot} of member '{member.Id}' is empty: there is nothing in it to put down.", nameof(slot));

        var tile = Displacer.TileOf(member);
        var grid = Map.Grid;
        if (tile.R < 0 || tile.R >= grid.GetLength(0) || tile.C < 0 || tile.C >= grid.GetLength(1) || grid[tile.R, tile.C] != 0)
            throw new ArgumentException(
                $"Member '{member.Id}' stands at {tile}, which is not a floor tile of floor {Index + 1}: a weapon is put down where its carrier stands.",
                nameof(member));

        member.Inventory[slot] = null;
        var item = new LooseItem(weapon, tile);
        LooseItems.Add(item);
        return item;
    }

    /// <summary>
    /// Build floor <paramref name="index"/> of the dungeon seeded
    /// <paramref name="mapSeed"/>: the map from <see cref="DungeonState.FloorSeed"/>,
    /// its fog boxes, its enemy rows — placed off the same floor seed, attuned to
    /// <paramref name="def"/>'s <see cref="DungeonDef.Attunement"/> and carrying
    /// its <see cref="DungeonDef.Theme"/> — and its stairs, each on its own
    /// stream, none continuing another and none reading another's result: a row
    /// may stand on the far tile (<see cref="StairPlacer.Place"/>). The bottom
    /// floor adds its boss after them (<see cref="Boss"/>), carrying the stacks
    /// its drop was promised.
    /// </summary>
    /// <param name="mapSeed">The dungeon's seed, rolled once per entry.</param>
    /// <param name="index">The 0-based floor index.</param>
    /// <param name="bottom">
    /// Whether this is the boss floor, the dungeon's bottom: its far tile is then
    /// the <see cref="Lair"/>, there is no <see cref="DownStairs"/>, and the boss
    /// is appended to the rows. It moves no other draw — the map, the placed rows
    /// and the stair tile are the same either way; the boss rolls on its own
    /// stream (<see cref="DungeonState.RollBoss"/>).
    /// </param>
    /// <param name="def">The dungeon, whose attunement and theme every enemy it spawns carries, and whose boss the bottom holds.</param>
    /// <param name="promisedStacks">
    /// The guaranteed stacks the boss's drop carries (§4.3), written on the boss
    /// row: <see cref="DungeonState.Enter"/> hands over the
    /// <see cref="DungeonState.PromisedStacks"/> it fixed for the visit, and a load
    /// would hand over the saved one. Null reads
    /// <see cref="DungeonState.PromisedStacksFor"/> the floor this is — the bottom
    /// floor's number is the boss floor, <paramref name="index"/> plus one.
    /// Nothing reads it above the bottom, which holds no boss.
    /// </param>
    internal static Floor Generate(long mapSeed, int index, bool bottom, DungeonDef def, int? promisedStacks = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(def);

        var (seed, map, fogBoxes) = Terrain(mapSeed, index);
        var rows = PlaceEnemies(map, seed, def);
        var stairs = StairPlacer.Place(map, mapSeed, index);

        EnemyState[] enemies = rows;
        EnemyState? boss = null;
        if (bottom)
        {
            int promised = promisedStacks ?? DungeonState.PromisedStacksFor(index + 1);
            boss = PlaceBoss(map, stairs.Far, rows, mapSeed, def, promised);
            enemies = [.. rows, boss];
        }
        return new Floor(index, seed, map, fogBoxes, enemies, stairs, bottom, boss);
    }

    /// <summary>
    /// Floor <paramref name="index"/> of the dungeon seeded <paramref name="mapSeed"/>
    /// as a save describes it (§5.1): the terrain <see cref="Generate"/> builds —
    /// the map, its fog boxes and its stairs, regenerated from the seed, since maps
    /// are never serialized — standing the saved rows on it instead of placing
    /// any: "regenerate the dungeon from the seed on every entry, place enemies
    /// only on the first visit, restore saved states afterwards". The explored
    /// fog comes back, and on the floor the party stands on what it can see this
    /// turn; so do the weapons put down here, in the order they were left. A
    /// saved floor is a visited one.
    /// <para>
    /// Nothing is placed, rolled or arrived: <see cref="EnemyPlacer"/>'s stream
    /// is never drawn, the boss is never rolled — its row is the save's, what it
    /// holds included — and nobody is moved. The rows are the load's to have
    /// built (<see cref="SaveGame.Restore"/>); this takes them as they are and
    /// refuses only what no floor can hold.
    /// </para>
    /// </summary>
    /// <param name="mapSeed">The dungeon's seed, as saved.</param>
    /// <param name="index">The 0-based floor index.</param>
    /// <param name="bottom">Whether this is the boss floor: the far tile is then the lair, and <paramref name="boss"/> is required.</param>
    /// <param name="rows">Every row of the floor in spawn order, the boss's among them on the bottom floor, each spawn index once.</param>
    /// <param name="boss">The row among <paramref name="rows"/> that is the floor's boss: on the bottom floor, and only there.</param>
    /// <param name="looseItems">The weapons put down here, in the order they were left, each on a floor tile of the map.</param>
    /// <param name="seen">The explored fog, one string per map row, <c>'1'</c> for a tile seen (<see cref="FogState.Encode"/>).</param>
    /// <param name="visible">What the party can see this turn in the same encoding, or null for nothing: the current floor's alone.</param>
    /// <exception cref="ArgumentException">
    /// A row is null or a spawn index repeats; a boss where the floor is not the
    /// bottom, none where it is, or one that is not among the rows; a fog row that
    /// does not fit the map; or a loose item off the floor.
    /// </exception>
    internal static Floor Restore(long mapSeed, int index, bool bottom, IReadOnlyList<EnemyState> rows, EnemyState? boss,
        IReadOnlyList<LooseItem> looseItems, IReadOnlyList<string> seen, IReadOnlyList<string>? visible)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(looseItems);
        ArgumentNullException.ThrowIfNull(seen);
        if (rows.Any(row => row is null))
            throw new ArgumentException($"Floor {index + 1}'s rows name a null row.", nameof(rows));
        if (rows.Select(row => row.SpawnIndex).Distinct().Count() != rows.Count)
            throw new ArgumentException($"Floor {index + 1}'s rows repeat a spawn index: each row has its own, which seeds its drop and its ladder.", nameof(rows));
        if (bottom != (boss is not null))
            throw new ArgumentException(bottom
                ? $"Floor {index + 1} is the dungeon's bottom, and names no boss: the bottom floor holds the dungeon's one boss."
                : $"Floor {index + 1} is not the dungeon's bottom, and names a boss: only the bottom floor holds one.", nameof(boss));
        if (boss is not null && !rows.Any(row => ReferenceEquals(row, boss)))
            throw new ArgumentException($"Floor {index + 1}'s boss is not one of its rows.", nameof(boss));

        var (seed, map, fogBoxes) = Terrain(mapSeed, index);
        var stairs = StairPlacer.Place(map, mapSeed, index);
        var floor = new Floor(index, seed, map, fogBoxes, rows.ToArray(), stairs, bottom, boss);

        FogState.Decode(seen, floor.Fog.Seen, nameof(seen));
        if (visible is not null)
            FogState.Decode(visible, floor.Fog.Visible, nameof(visible));

        var grid = map.Grid;
        for (int i = 0; i < looseItems.Count; i++)
        {
            var item = looseItems[i] ?? throw new ArgumentException($"Floor {index + 1}'s loose item {i} is null.", nameof(looseItems));
            var (r, c) = item.Tile;
            if (r < 0 || r >= grid.GetLength(0) || c < 0 || c >= grid.GetLength(1) || grid[r, c] != 0)
                throw new ArgumentException(
                    $"Floor {index + 1}'s loose item {i}, {item.Weapon.Name}, lies at {item.Tile}, which is not a floor tile of the map: a weapon is put down where its carrier stood.",
                    nameof(looseItems));
            floor.LooseItems.Add(item);
        }

        floor.Visited = true;
        return floor;
    }

    /// <summary>
    /// A floor's ground: its seed, the map generated from it, and the map's fog
    /// boxes — whose build expands <see cref="MapData.Corridors"/> in place,
    /// exactly as the scene's always did. What <see cref="Generate"/> and
    /// <see cref="Restore"/> both stand a floor on, in <see cref="Generate"/>'s
    /// order, so a saved floor is regenerated to the tile.
    /// </summary>
    private static (long Seed, MapData Map, FogBoxSet FogBoxes) Terrain(long mapSeed, int index)
    {
        long seed = DungeonState.FloorSeed(mapSeed, index);
        var map = MapGenerator.Generate(new Mulberry32(seed));
        var fogBoxes = FogBoxBuilder.Build(map);   // expands map.Corridors in place, exactly as the scene always has
        return (seed, map, fogBoxes);
    }

    /// <summary>
    /// The enemy rows, first entry: <see cref="EnemyPlacer"/> decides where they
    /// stand and what they hold off the floor's seed, on its own stream; each row
    /// keeps the index it was placed at (§3.2's <see cref="EnemyState.SpawnIndex"/>),
    /// which seeds its drop and its revival ladder, and carries the dungeon's
    /// attunement — the dungeon's and never the individual's (§1.4, §4.3) — and
    /// its theme, which every drop in the dungeon rolls for (§4.3). Neither moves
    /// a draw: they are stamped on rows already placed. A save restores these
    /// rows instead of placing them again (§5.1).
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
                Theme = def.Theme,
            });
        }
        return rows.ToArray();
    }

    /// <summary>
    /// The boss row (§4.3): what it holds rolled off the visit's boss stream
    /// (<see cref="DungeonState.RollBoss"/> — a class its dungeon's theme can be
    /// forged onto and an enemy can field, and the variant), built to the
    /// dungeon's statline (<see cref="DungeonState.BossRow"/>) with the stacks its
    /// drop was promised, and appended after every placed row, so its
    /// <see cref="EnemyState.SpawnIndex"/> follows theirs and no dummy's stream
    /// moves. It waits in the lair — or, when a placed dummy already stands there
    /// (the lair is drawn without reading the rows), on the nearest free floor
    /// tile to it (<see cref="GridUtils.NearestFreeTile"/>): the rule an arrival
    /// follows, never on top of the dummy, and never by moving the dummy or the lair.
    /// </summary>
    private static EnemyState PlaceBoss(MapData map, (int R, int C) lair, IReadOnlyList<EnemyState> rows, long mapSeed, DungeonDef def,
        int promisedStacks)
    {
        var roll = DungeonState.RollBoss(mapSeed, def);
        var weapons = GameContent.Current.Weapons;
        var boss = DungeonState.BossRow(def, weapons.Instantiate(weapons.Variant(roll.Class, roll.Role).Id), promisedStacks);

        var held = new HashSet<(int R, int C)>();
        foreach (var row in rows)
            held.Add(Displacer.TileOf(row));   // every placed row: all of them stand at generation
        var (r, c) = GridUtils.NearestFreeTile(map.Grid, lair, held.Contains);
        (boss.X, boss.Y) = Displacer.CentreOf(r, c);
        boss.SpawnIndex = rows.Count;
        return boss;
    }
}

/// <summary>
/// A weapon the party put down (§4.4, <see cref="Floor.Leave"/>): the weapon
/// itself and the tile it lies on. It lies on the floor where it was left, in
/// that floor's <see cref="Floor.LooseItems"/>, for the rest of the visit, and
/// anyone can take it back (<see cref="Pickup.TakeLoose"/>). It is gone with the
/// dungeon.
/// <para>
/// <strong>Not a drop.</strong> No corpse fixes it and no seed rolls it, so
/// nothing re-derives it: the floor stores it, and a save keeps both halves (§5.1).
/// It carries nothing a drop takes from the corpse it came off. There is no farm
/// depth (the count was the dummy's, not the weapon's), and taking it back never
/// makes it a spoil (<see cref="DungeonState.Spoils"/> are what came off a corpse).
/// </para>
/// </summary>
/// <param name="Weapon">The weapon: the very instance that left the bag, by reference.</param>
/// <param name="Tile">Where it lies, (row, column): the tile its carrier stood on.</param>
public sealed record LooseItem(Weapon Weapon, (int R, int C) Tile);

/// <summary>
/// What the boss stream rolls at entry (§4.3, <see cref="DungeonState.RollBoss"/>),
/// in the order it draws them: the boss's floor, then the class of the weapon it
/// <em>wields</em> and that weapon's variant role. What it drops is another draw
/// on another stream, over its dungeon's whole drop table.
/// </summary>
/// <param name="Floor">The boss floor, a 1-based floor number: <see cref="DungeonState.RollBossFloor(long, DungeonDef)"/>.</param>
/// <param name="Class">The class it holds, one its dungeon allows in its hands (<see cref="DungeonCatalogue.WieldClasses"/>).</param>
/// <param name="Role">The variant of that class it holds.</param>
public sealed record BossRoll(int Floor, WeaponClass Class, VariantRole Role);

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
/// the visit's one clock, and the boss floor rolled at entry — the boss waiting
/// on it, whose death stops resurrection on every floor for the rest of the
/// visit (<see cref="BossDefeated"/>). §5.1's Run group and its per-floor group
/// are this object; <see cref="CampaignState"/> holds it, and leaving drops it
/// whole (§4.1: "leaving the dungeon discards all of it").
/// <para>
/// <strong>A floor left is a floor kept.</strong> Every floor is built once, at
/// <see cref="Enter"/> — a pure function of the map seed, its index and the
/// dungeon — and never rebuilt inside the visit, so going back up a floor is a
/// pointer move: its rows (alive or dead, where they stand, what the farm made
/// of them), its explored fog, and whatever the party put down on it
/// (<see cref="Floor.LooseItems"/>) are exactly as the party left them.
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
/// is the golden set's neighbour. The visit's d20 is one of them
/// (<see cref="RollD20"/>): each draw a stream of its own, keyed by how many the
/// visit has rolled before it (<see cref="DiceRolled"/>), which a save keeps — so
/// a reload rolls exactly the dice play without one would have rolled.
/// </para>
/// <para>
/// <strong>And what the run's end reads</strong> (§4.4): the party's XP as it
/// stood at the door (<see cref="EntrySnapshot"/>), what the party has picked up
/// this visit (<see cref="Spoils"/>), and the wipe that ends a run in Logic
/// (<see cref="Wiped"/>). The visit decides none of the outcome: the campaign
/// that holds it does (<see cref="CampaignState.EndRun"/>), and the visit is
/// discarded once it has.
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

    /// <summary>
    /// The visit's d20 splitter (<see cref="DiceStream"/>): odd, 32-bit —
    /// splitmix32's first multiplier — and distinct from every other salt and mix
    /// in the tree. Checked against every drop, revival and boss-drop stream of
    /// the floors a boss can sit on under the shipped
    /// <see cref="Tuning.BossFloorMax"/>, and clean there (Phase 5.7's collision
    /// check, pinned by <c>DungeonSeedTests</c>). A tuning that digs deeper can
    /// make a d20 and a revival share a number: the first 58 floors stay clean,
    /// but floor index 58's revival roll for spawn 7, cycle 16 is dice draw 2801,
    /// the one meeting in the checked domain on any floor <c>tuning.json</c>'s
    /// clamp reaches (99). Harmless, like draw 34,430's meeting with floor index
    /// 8's: the kind of sharing settled.md's Phase 4 pass accepts between the
    /// floors' own streams. The test pins both. A compiled constant like every
    /// salt: it decides what a seed rolls, so it is parity's and never a tuning key.
    /// </summary>
    public const long DiceSalt = 0x7FEB352D;

    /// <summary>
    /// The per-draw term of <see cref="DiceStream"/>, multiplied by the draw's
    /// index: odd — splitmix32's second multiplier — so no two of the first 2³²
    /// draws share a composed seed, and each draw is its own seeded stream rather
    /// than a position in a running one.
    /// </summary>
    internal const long DiceMix = 0x846CA68B;

    // The turn the visit's clock stood at when the last floor's system handed it
    // back: what Turn reads while no system is open, and what the next one opens on.
    private int _turn;

    // The counters of the turn in progress, merged across every floor the party
    // has stood on in it (TurnCarry.MergedWith). The next system imports it only
    // if it is still this turn's, so a carry that outlived its turn is dead weight
    // until the next transit replaces it.
    private TurnCarry? _carry;

    // The weapons taken into a bag this visit (Pickup.Take), by reference and in
    // the order they were taken; never pruned — the run's end reads which of
    // them are still in a bag.
    private readonly List<Weapon> _spoils = new();

    private DungeonState(DungeonDef def, long mapSeed, int bossFloor, int promisedStacks, Floor[] floors, XpSnapshot entrySnapshot)
    {
        Def = def;
        MapSeed = mapSeed;
        BossFloor = bossFloor;
        PromisedStacks = promisedStacks;
        Floors = floors;
        EntrySnapshot = entrySnapshot;
        Spoils = _spoils.AsReadOnly();
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
    /// How many runs beating this visit's boss locks its dungeon for (§4.3):
    /// <c>BossFloor × CooldownRunsPerFloor</c> — as many runs as the floor the boss
    /// waits on, at the shipped one run a floor — counted on the campaign's run
    /// counter.
    /// <para>
    /// <strong>The one place the rule is written.</strong> The boss floor's banner
    /// promises the lock before the fight by reading this
    /// (<c>DungeonHud.BossFloorBanner</c>), and the lock a clear writes is
    /// <c>RunCounter + LockRuns</c> — never a second copy of the arithmetic — so
    /// the promise and the lock can never state different numbers: any change to
    /// the rule (a rounding, a per-dungeon length, a tuning captured at entry) is
    /// made here, once. The depth is the visit's own, fixed at entry; the runs a
    /// floor are <see cref="Tuning.CooldownRunsPerFloor"/> as it stands when
    /// asked. A dungeon that retires on its clear
    /// (<see cref="DungeonDef.RetiresOnClear"/>, the tutorial) is closed rather
    /// than locked, and never applies it.
    /// </para>
    /// </summary>
    public int LockRuns => BossFloor * GameContent.Current.Tuning.CooldownRunsPerFloor;

    /// <summary>
    /// The guaranteed stacks the boss's drop carries (§4.3: "one acquired stack
    /// per floor past 4"): <see cref="PromisedStacksFor"/> the
    /// <see cref="BossFloor"/> — 1 at floor 5 and 6 at floor 10, and none for the
    /// tutorial's floor 2, whose "one stack" is the forged graft itself.
    /// Guaranteed rather than rolled for: the count is the floor's, and only where
    /// the stacks land is random (<see cref="BossLoot.Roll"/>).
    /// <para>
    /// <strong>Fixed once, at <see cref="Enter"/></strong>, under the tuning
    /// current then, and written on the boss row at generation
    /// (<see cref="EnemyState.PromisedStacks"/>): that is how <see cref="BossFloor"/>
    /// "survives into the drop resolution" (§4.5). The drop reads the row, so a
    /// drop re-derived later in the visit pays the same stacks whatever the
    /// tuning has become since, and a save stores this number beside
    /// <see cref="BossFloor"/> rather than re-deriving either through a
    /// <c>tuning.json</c> that may have changed (§5.1).
    /// </para>
    /// </summary>
    public int PromisedStacks { get; }

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

    /// <summary>The deepest floor index the party has stood on this visit: what the run's end reads for a failed run's mercy tick (§6.3's "floor 3 or deeper") and for whether the depth was reached (§4.3).</summary>
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
    /// How many d20s the visit's own die has rolled (<see cref="RollD20"/>): the
    /// die's whole position, one integer, and the one stream position a save
    /// holds — every other stream in <c>Logic/</c> is fresh per use and keyed by a
    /// counter the rows already carry. 0 at <see cref="Enter"/>; a load hands back
    /// the saved count, so the next roll after a reload is the roll play without
    /// the reload would have made next. A die injected into
    /// <see cref="OpenTurns"/> in its place moves nothing here.
    /// </summary>
    public int DiceRolled { get; private set; }

    /// <summary>
    /// The visit's own d20 (USER DECISION 1 of Phase 5, ruled by the user,
    /// 2026-09-25: seeded and saved, "just to protect players from themselves"):
    /// draw <see cref="DiceRolled"/> of the visit's dice stream,
    /// <c>DiceStream(MapSeed, DiceRolled).NextInt(1, 20)</c>, and count it. What
    /// every floor's system rolls when <see cref="OpenTurns"/> is handed no die —
    /// the swings, the reactions, the casts, the enemies' beats — so the dice a
    /// visit rolls are a pure function of its map seed and how many it has rolled,
    /// and a reload rolls on from exactly there: no reload-to-reroll. One counter
    /// for the whole visit, on every floor: the stairs move the party and never
    /// the die. The price, stated with the decision and accepted with the ruling:
    /// a turn replayed after the process is killed past its autosave knows its
    /// next draws.
    /// </summary>
    /// <returns>A natural roll, 1 to 20, each face equally likely.</returns>
    public int RollD20() => DiceStream(MapSeed, DiceRolled++).NextInt(1, 20);

    /// <summary>
    /// The stream draw <paramref name="draw"/> of the visit seeded
    /// <paramref name="mapSeed"/> rolls on:
    /// <c>SplitMix32(mapSeed ^ DiceSalt ^ draw × DiceMix)</c>, finalized as every
    /// counter-keyed stream here is (<see cref="FarmLadder.SplitMix32"/>), since a
    /// draw is the first output of its own fresh stream — the house convention
    /// (a stream per index, never a skip-ahead through one), which is what makes
    /// the die's position one integer a save can hold. Keyed by the visit's map
    /// seed, not a floor's: the die is the visit's, whatever floor it is rolled on.
    /// </summary>
    /// <param name="mapSeed">The visit's map seed.</param>
    /// <param name="draw">The 0-based index of the draw: how many the visit had rolled before it.</param>
    internal static Mulberry32 DiceStream(long mapSeed, int draw)
        => new(FarmLadder.SplitMix32(mapSeed ^ DiceSalt ^ (draw * DiceMix)));

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
    /// The party's XP pools as they stood when it stepped in (§4.4): every
    /// member's weapon XP in every class, HP XP and mana XP, taken at
    /// <see cref="Enter"/> — innate stats never, since they never move. A wipe
    /// claws back a share of each pool's gain since this; any other ending
    /// leaves it untouched, and it goes with the visit. §5.1's Run group saves it.
    /// </summary>
    public XpSnapshot EntrySnapshot { get; }

    /// <summary>
    /// This visit's spoils (§4.4): every weapon <see cref="Pickup.Take"/> has put
    /// into a bag off a corpse, by reference, in the order taken, and never
    /// removed. What the party carried in is never one, and a weapon put down and
    /// taken back is the spoil it already was, or not one. The run's end counts
    /// only the spoils still in some member's bag — kept on the way out, lost to
    /// a wipe — so a spoil that has left every bag needs no bookkeeping here.
    /// <para>
    /// Nothing drops while the dungeon still resurrects (§3.2), so no spoil can
    /// exist before the boss falls: that is why a retreat forfeits the loot by
    /// construction rather than by a rule.
    /// </para>
    /// </summary>
    public IReadOnlyList<Weapon> Spoils { get; }

    /// <summary>
    /// The party wiped (§4.4): raised from the <see cref="TurnSystem.GameOver"/>
    /// of every floor's system <see cref="OpenTurns"/> opens, so the run ends in
    /// Logic at that transition, whatever is or is not watching. A boss that fell
    /// in the same cascade is already recorded (<see cref="BossDefeated"/>) when
    /// this is raised, whichever of the two deaths drained first.
    /// <see cref="CampaignState.Enter"/> subscribes it to
    /// <see cref="CampaignState.EndRun"/> — the wiring runs campaign to visit, and
    /// the visit never holds the campaign.
    /// </summary>
    public event Action? Wiped;

    /// <summary>
    /// A weapon <see cref="Pickup.Take"/> just put into a bag off a corpse: one of
    /// this visit's spoils (<see cref="Spoils"/>), recorded once whatever happens
    /// to it after.
    /// </summary>
    internal void RecordSpoil(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        if (!_spoils.Any(spoil => ReferenceEquals(spoil, weapon)))
            _spoils.Add(weapon);
    }

    /// <summary>
    /// Enter <paramref name="def"/> on <paramref name="mapSeed"/> (§4.5): roll the
    /// boss floor (<see cref="RollBossFloor(long, DungeonDef)"/>), fix the stacks
    /// its boss's drop is promised (<see cref="PromisedStacks"/>), build every
    /// floor down to it — each <see cref="Floor.Generate"/>, a pure function of
    /// the seed, its index and the dungeon, the last one the bottom, its boss
    /// carrying the promise — and stand the party at the first floor's arrival
    /// tile, each member on its own tile in roster order, as the scene has always
    /// spawned them. The party's XP is taken as it stands at the door
    /// (<see cref="EntrySnapshot"/>), for the run's end to read.
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
        int promised = PromisedStacksFor(bossFloor);   // once, under the tuning current now
        var floors = new Floor[bossFloor];
        for (int n = 0; n < bossFloor; n++)
            floors[n] = Floor.Generate(mapSeed, n, bottom: n == bossFloor - 1, def, promised);

        var visit = new DungeonState(def, mapSeed, bossFloor, promised, floors, XpSnapshot.Of(party));
        visit.Arrive(party, floors[0].UpStairs);
        return visit;
    }

    /// <summary>
    /// A visit as a save describes it (§5.1's Run group): the dungeon, the seed,
    /// and the depth and the promise the entry fixed — the values the load uses,
    /// never rolled or re-derived through a tuning that may have changed since
    /// (§4.3: "a tuning change during the visit moves neither") — every floor
    /// already built (the visited ones restored, <see cref="Floor.Restore"/>; the
    /// rest generated from the seed, the depth and the promise), the floor the
    /// party is on and the deepest it has reached, the clock, the boss's death,
    /// the party's XP at the door, this turn's counters, the spoils — the very
    /// weapons now in the bags and on the floors — and how many d20s the visit's
    /// own die has rolled, so it rolls on from there. The one door a visit comes
    /// back through: internal, beside <see cref="Enter"/>.
    /// <para>
    /// <strong>It opens nothing, places nobody and rolls nothing.</strong> No
    /// system is open (<see cref="Turns"/> is null): whoever plays the floor opens
    /// it (<see cref="OpenTurns"/>), which imports the saved counters and catches
    /// the floor up exactly as an arrival does. The party stands where the save
    /// has it — arriving would move it to the stairs — and the rows where the
    /// floors have them.
    /// </para>
    /// </summary>
    /// <param name="def">The dungeon.</param>
    /// <param name="mapSeed">The seed it was entered on.</param>
    /// <param name="bossFloor">The boss floor, a 1-based floor number: <paramref name="floors"/> holds exactly this many.</param>
    /// <param name="promisedStacks">The stacks the boss's drop was promised at entry.</param>
    /// <param name="entrySnapshot">The party's XP at the door.</param>
    /// <param name="floors">Every floor, index 0 first: the visited ones down to <paramref name="deepestFloorIndex"/>, the rest never stood on.</param>
    /// <param name="floorIndex">The floor the party is on.</param>
    /// <param name="deepestFloorIndex">The deepest floor it has stood on.</param>
    /// <param name="turn">The visit's clock.</param>
    /// <param name="bossDefeated">Whether the boss has fallen.</param>
    /// <param name="bossDefeatedAtTurn">The turn it fell, or -1 while it stands.</param>
    /// <param name="carry">This turn's counters, or null for none: only this turn's, since a carry from an earlier one is dead weight.</param>
    /// <param name="spoils">The spoils in the order taken, each the very weapon where it now is.</param>
    /// <param name="diceRolled">How many d20s the visit's own die had rolled (<see cref="DiceRolled"/>): where it rolls on from.</param>
    /// <exception cref="ArgumentException">
    /// The floors are not the dungeon's depth, out of order, or visited other than
    /// down to the deepest; the floor or the deepest is out of the dungeon; the
    /// boss's death names no turn it could have been; the carry is another turn's;
    /// or a spoil is null or listed twice.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The depth is below one floor, or the promise, the clock or the die's count is negative.</exception>
    internal static DungeonState Restore(DungeonDef def, long mapSeed, int bossFloor, int promisedStacks, XpSnapshot entrySnapshot, Floor[] floors,
        int floorIndex, int deepestFloorIndex, int turn, bool bossDefeated, int bossDefeatedAtTurn, TurnCarry? carry, IReadOnlyList<Weapon> spoils,
        int diceRolled)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(entrySnapshot);
        ArgumentNullException.ThrowIfNull(floors);
        ArgumentNullException.ThrowIfNull(spoils);
        ArgumentOutOfRangeException.ThrowIfLessThan(bossFloor, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(promisedStacks);
        ArgumentOutOfRangeException.ThrowIfNegative(turn);
        ArgumentOutOfRangeException.ThrowIfNegative(diceRolled);
        if (floors.Length != bossFloor)
            throw new ArgumentException($"{floors.Length} floors for a boss on floor {bossFloor}: a visit holds every floor down to its boss.", nameof(floors));
        if (floorIndex < 0 || floorIndex > deepestFloorIndex || deepestFloorIndex >= bossFloor)
            throw new ArgumentException(
                $"Floor index {floorIndex} with the deepest {deepestFloorIndex} in a dungeon {bossFloor} floors deep: the party stands on a floor it has reached, inside the dungeon.",
                nameof(floorIndex));
        for (int n = 0; n < floors.Length; n++)
        {
            var floor = floors[n] ?? throw new ArgumentException($"Floor {n + 1} is null.", nameof(floors));
            if (floor.Index != n)
                throw new ArgumentException($"Floor {n + 1} holds the floor indexed {floor.Index}: the floors run in order from the first.", nameof(floors));
            if (floor.Visited != (n <= deepestFloorIndex))
                throw new ArgumentException(
                    $"Floor {n + 1} is {(floor.Visited ? "" : "not ")}visited with the deepest reached {deepestFloorIndex + 1}: floors are adjacent, so every floor down to the deepest was stood on, and none below it.",
                    nameof(floors));
            if ((floor.DownStairs is null) != (n == bossFloor - 1))
                throw new ArgumentException($"Floor {n + 1} is {(floor.DownStairs is null ? "" : "not ")}a bottom floor in a dungeon {bossFloor} floors deep.", nameof(floors));
        }
        if (bossDefeated ? bossDefeatedAtTurn < 0 || bossDefeatedAtTurn > turn : bossDefeatedAtTurn != -1)
            throw new ArgumentException(
                bossDefeated
                    ? $"The boss fell at turn {bossDefeatedAtTurn}, and the clock stands at {turn}: it fell on a turn the visit has had."
                    : $"The boss stands, and fell at turn {bossDefeatedAtTurn}: a standing boss fell at no turn (-1).",
                nameof(bossDefeatedAtTurn));
        if (carry is not null && carry.Turn != turn)
            throw new ArgumentException($"The counters are turn {carry.Turn}'s, and the clock stands at {turn}: a visit carries this turn's alone.", nameof(carry));
        for (int i = 0; i < spoils.Count; i++)
        {
            if (spoils[i] is null)
                throw new ArgumentException($"Spoil {i} is null.", nameof(spoils));
            for (int j = 0; j < i; j++)
                if (ReferenceEquals(spoils[j], spoils[i]))
                    throw new ArgumentException($"Spoil {i} is spoil {j} again: a weapon is one spoil, once.", nameof(spoils));
        }

        var visit = new DungeonState(def, mapSeed, bossFloor, promisedStacks, floors, entrySnapshot)
        {
            FloorIndex = floorIndex,
            DeepestFloorIndex = deepestFloorIndex,
            BossDefeated = bossDefeated,
            BossDefeatedAtTurn = bossDefeatedAtTurn,
            DiceRolled = diceRolled,
        };
        visit._turn = turn;
        visit._carry = carry;
        visit._spoils.AddRange(spoils);
        return visit;
    }

    /// <summary>
    /// The counters of the turn in progress as a save must keep them
    /// (<see cref="TurnCarry"/>): the floors already stood on in this turn, merged
    /// with the open system's own (<see cref="TurnSystem.ExportCarry"/>) exactly
    /// as the stairs merge them — the open system's figures win, and another
    /// floor's enemies are kept — or, with no system open, the carry the stairs
    /// left. This turn's alone: null when the counters are an earlier turn's, and
    /// when there are none. For reading: the save sorts what it reads into its own
    /// lists, and nothing here writes the live counters.
    /// </summary>
    internal TurnCarry? CurrentCarry()
    {
        var carry = Turns is { } open
            ? (_carry is null ? open.ExportCarry() : _carry.MergedWith(open.ExportCarry()))
            : _carry;
        if (carry is null || carry.Turn != Turn)
            return null;
        return carry.Attacks.Count == 0 && carry.ZoneUses.Count == 0 && carry.ReactionUses.Count == 0 && carry.Seen.Count == 0
            ? null
            : carry;
    }

    /// <summary>
    /// What <paramref name="row"/> lies on right now, for a floor with no system
    /// open (§3.5): exactly what <see cref="TurnSystem.GroundItemOf"/> answers for
    /// a row of the floor it is open on. Nothing while the dungeon still
    /// resurrects — the boss standing — nothing on a row standing, and nothing
    /// once taken; a pin a load handed back (<see cref="EnemyState.PinnedDrop"/>)
    /// before anything derived; and otherwise the drop the roster's feed would
    /// roll, off the floor's own seed — the boss's own drop for
    /// <see cref="Floor.Boss"/> (<see cref="BossLoot.Roll"/>), every other row's
    /// own class's (<see cref="LootTable.Roll"/>).
    /// </summary>
    /// <param name="floor">The floor the row is on.</param>
    /// <param name="row">One of <paramref name="floor"/>'s rows.</param>
    /// <param name="bossDefeated">Whether the visit's boss has fallen: what stops resurrection and lays every corpse's drop.</param>
    /// <exception cref="ArgumentException"><paramref name="row"/> is not one of <paramref name="floor"/>'s rows: its drop is its own floor's to derive.</exception>
    internal static Drop? GroundItemOf(Floor floor, EnemyState row, bool bossDefeated)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(row);
        if (!floor.Enemies.Any(enemy => ReferenceEquals(enemy, row)))
            throw new ArgumentException($"The row is not one of floor {floor.Index + 1}'s: its drop is its own floor's to derive.", nameof(row));
        if (!bossDefeated || row.Alive || row.DropTaken)
            return null;
        return row.PinnedDrop ?? (ReferenceEquals(row, floor.Boss) ? BossLoot.Roll(row, floor.Seed) : LootTable.Roll(row, floor.Seed));
    }

    /// <summary>
    /// Open the current floor's turn system on the visit's clock, and return it
    /// with the floor already caught up (§4.1).
    /// <para>
    /// It opens at <see cref="Turn"/> in the player phase, with this turn's
    /// counters from the floors already stood on in it (<see cref="TurnCarry"/>),
    /// the floor's own seed for its drops and revivals, the visit's own die
    /// unless it is handed another (<see cref="RollD20"/>), the floor's
    /// <see cref="Floor.Boss"/> — if it has one — rostered as the boss, and
    /// resurrection off once the boss is dead. The visit hears the boss's death
    /// on that system before anyone else does and records it
    /// (<see cref="RecordBossDefeated"/>), and hears a wipe the same way and
    /// raises it (<see cref="Wiped"/>): in Logic, whatever is watching — first
    /// recording a boss that fell in the wipe's own cascade but whose death had
    /// not drained yet, so the run's end never reads a dead boss as standing. Then
    /// every dummy here whose wait ran out while the party was elsewhere gets up
    /// (<see cref="TurnSystem.CatchUpRevivals"/>) — as of now, or as of the turn
    /// the boss fell once it has — and the zones are re-read around them. So the
    /// system comes back finished: whoever plays the floor builds it from the rows
    /// afterwards and learns of those revivals from the rows, never from
    /// <see cref="TurnSystem.EnemyResurrected"/>, which had no subscriber when it
    /// fired. Called once per arrival, after the party is placed
    /// (<see cref="Enter"/>, <see cref="Descend"/>, <see cref="Ascend"/> place
    /// it), so nothing wakes inside a member.
    /// </para>
    /// </summary>
    /// <param name="party">The members, in roster order — the same list every call of the visit is handed.</param>
    /// <param name="rollD20">
    /// The d20 the system rolls. Omitted, it is the visit's own
    /// (<see cref="RollD20"/>), whose position a save keeps — never
    /// <c>Random.Shared</c>, which no save could reproduce. A die handed in rolls
    /// instead: a test's moves the visit's die not at all, and the game's scene
    /// hands one that rolls the visit's own and notes each roll for its cues.
    /// </param>
    /// <exception cref="InvalidOperationException">A system is already open on this floor: only the stairs close one.</exception>
    public TurnSystem OpenTurns(IReadOnlyList<PartyMemberState> party, Func<int>? rollD20 = null)
    {
        RequireParty(party);
        if (Turns is not null)
            throw new InvalidOperationException(
                $"Floor {Current.Index + 1}'s turn system is already open: a floor is opened once per arrival, and only the stairs close it.");

        var floor = Current;
        var turns = new TurnSystem(floor.Map.Grid, party, floor.Enemies, rollD20 ?? RollD20, floor.Seed,
            firstTurn: _turn, carry: _carry, boss: floor.Boss)
        {
            ResurrectionActive = !BossDefeated,
        };
        turns.BossDefeated += _ => RecordBossDefeated();
        turns.GameOver += () =>
        {
            // One cascade can take the boss and the last member together, and its
            // held deaths drain in the order they fell: with the member's first,
            // the game is over before the boss's own death feed has run. What killed
            // the boss, a blow or a tick, wrote its row before any death drained, so
            // the row says it is down: record the kill, and the wipe ends a run whose
            // boss is dead (§4.4, §6.3: killed the boss, died on the way out).
            if (floor.Boss is { Alive: false } && !BossDefeated)
                RecordBossDefeated();
            Wiped?.Invoke();   // the run ends at the wipe, in Logic, before anyone watching hears it
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
    /// moves nothing. The boss's death calls this, through the subscription
    /// <see cref="OpenTurns"/> makes on the system of every floor it opens — or
    /// the wipe does, ahead of it, when the boss and the last member fell in one
    /// cascade and the member's death drained first (<see cref="Wiped"/>).
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

    /// <summary>
    /// The stairs <paramref name="floor"/> holds, arrival tile first: its
    /// up-stairs — the way out on the first floor, the way up on every other —
    /// then its way down, except on the bottom. The one place a stair's kind is
    /// decided: <see cref="StairUnder"/> and <see cref="CanUse"/> read it, and so
    /// does whoever draws the floor, which labels each stair by what it does and
    /// never restates which up-stairs leads out.
    /// </summary>
    public static IReadOnlyList<StairUse> StairsOf(Floor floor)
    {
        ArgumentNullException.ThrowIfNull(floor);
        var stairs = new List<StairUse>(2) { new(floor.Index == 0 ? Stair.Exit : Stair.Up, floor.UpStairs) };
        if (floor.DownStairs is { } down)
            stairs.Add(new StairUse(Stair.Down, down));
        return stairs;
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

    /// <summary>
    /// The guaranteed stacks a boss on floor <paramref name="bossFloor"/> pays
    /// with its drop (§4.3): <c>max(0, bossFloor − BossStackOffset)</c> — one per
    /// floor past four at the shipped offset, so 1 at floor 5 and 6 at floor 10,
    /// the table's numbers, and 0 at the tutorial's floor 2. The most any boss
    /// pays is <c>BossFloorMax − BossStackOffset</c>, six, deliberately short of
    /// the ten a two-modifier farm can bank: the farm stays the only route to a
    /// finished weapon, and a load refuses a tuning or a dungeon that would pay
    /// that much (<see cref="ContentValidator.RuleBossPaysLessThanTheFarm"/>).
    /// Read at <see cref="Enter"/> and fixed there (<see cref="PromisedStacks"/>).
    /// </summary>
    /// <param name="bossFloor">The boss floor, a 1-based floor number.</param>
    public static int PromisedStacksFor(int bossFloor)
        => PromisedStacksFor(bossFloor, GameContent.Current.Tuning);

    /// <summary>
    /// <see cref="PromisedStacksFor(int)"/> under <paramref name="tuning"/> rather
    /// than the current content's: what a load checks a boss floor range against
    /// while the tuning it is loading is not current yet.
    /// </summary>
    /// <param name="bossFloor">The boss floor, a 1-based floor number.</param>
    /// <param name="tuning">The tuning whose <see cref="Tuning.BossStackOffset"/> prices the floor.</param>
    public static int PromisedStacksFor(int bossFloor, Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        return Math.Max(0, bossFloor - tuning.BossStackOffset);
    }

    /// <summary>
    /// The redraw bound on the boss's class draw (<see cref="RollBoss"/>): past
    /// this many draws on classes its dungeon cannot put in its hands, the first
    /// class it can is taken. It guarantees termination and nothing else — at one
    /// wieldable class in five, 64 misses running is about one visit in two
    /// million — and it is compiled, like the salts: it decides what the boss holds.
    /// </summary>
    internal const int MaxBossClassDraws = 64;

    /// <summary>The roles a boss's weapon rolls between, counted off the code-fixed enum: a draw's range never depends on content.</summary>
    private static readonly VariantRole[] BossRoles = Enum.GetValues<VariantRole>();

    /// <summary>
    /// The boss, rolled at entry off the boss stream (§4.3,
    /// <c>new Mulberry32(mapSeed ^ BossSalt)</c>), in three draws:
    /// <list type="number">
    /// <item>the floor — <see cref="RollBossFloor(long, DungeonDef)"/>, the
    /// stream's first draw, so nothing drawn after it can move it;</item>
    /// <item>the class it <em>wields</em> — <c>NextInt(0, PlacedClasses.Count − 1)</c>
    /// over the compiled <see cref="EnemyPlacer.PlacedClasses"/>, drawn again
    /// while the class is not one its dungeon allows in its hands
    /// (<see cref="DungeonCatalogue.WieldClasses"/>: a class its theme can be
    /// forged onto that an enemy can field), up to
    /// <see cref="MaxBossClassDraws"/> draws, then the first allowed class in that
    /// order;</item>
    /// <item>the variant role of what it holds — <c>NextInt(0, 3)</c>.</item>
    /// </list>
    /// <para>
    /// <strong>Redrawn, never re-steered.</strong> Stepping on from a refused
    /// class to the next allowed one would weight whichever class follows a gap in
    /// the list — a Spear Hall boss would hold a dagger three times in five —
    /// where a redraw keeps the allowed classes equally likely. And no draw's
    /// range depends on content: both index compiled lists, and content (the
    /// relations, through the table) decides only whether a class draw is kept.
    /// So a relations edit moves a boss only when one of its class draws landed
    /// on a class whose legality the edit changed; a seed whose first class draw
    /// is allowed before and after keeps its class and its role.
    /// </para>
    /// <para>
    /// What the boss holds is this; what it drops is its own draw on its own
    /// stream, over its dungeon's whole drop table (§4.3) — the Long Gallery's
    /// boss swings no bow and may still drop one.
    /// </para>
    /// </summary>
    /// <param name="mapSeed">The dungeon's seed, rolled fresh per entry.</param>
    /// <param name="def">The dungeon whose boss is rolled.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="def"/>'s theme leaves its boss nothing to hold — refused at
    /// load (<see cref="ContentValidator.RuleBossWieldsAClass"/>), so only a dungeon
    /// the loaded content never validated can get here.
    /// </exception>
    public static BossRoll RollBoss(long mapSeed, DungeonDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        var wieldable = GameContent.Current.Dungeons.WieldClasses(def);
        if (wieldable.Count == 0)
            throw new InvalidOperationException(
                $"'{def.Id}': no class an enemy fields carries {def.Theme}, so its boss has nothing to hold ({ContentValidator.RuleBossWieldsAClass}).");

        var stream = BossStream(mapSeed);
        int floor = RollBossFloor(stream, def);

        var placed = EnemyPlacer.PlacedClasses;
        WeaponClass? held = null;
        for (int draw = 0; draw < MaxBossClassDraws && held is null; draw++)
        {
            var drawn = placed[stream.NextInt(0, placed.Count - 1)];
            if (wieldable.Contains(drawn))
                held = drawn;
        }
        held ??= placed.First(cls => wieldable.Contains(cls));

        var role = BossRoles[stream.NextInt(0, BossRoles.Length - 1)];
        return new BossRoll(floor, held.Value, role);
    }

    /// <summary>
    /// A boss as its dungeon makes it (§4.3, §5.8): an enemy row with
    /// <paramref name="def"/>'s boss statline — its hit points
    /// (<see cref="EnemyState.BaseMaxHp"/>) and its movement
    /// (<see cref="EnemyState.MoveBudget"/>) — its name on the plate, and its
    /// innate modifiers, the theme among them, carried on the actor whatever it
    /// holds (<see cref="ActorState.Innate"/>): weapon plus innate at resolution,
    /// and never a weapon's ceiling, its own included. It is attuned and themed as
    /// everything the dungeon spawns is, carries the stacks its drop is promised
    /// (<see cref="EnemyState.PromisedStacks"/>), and wields
    /// <paramref name="weapon"/> exactly as a dummy wields its own. Unplaced:
    /// generation stands it in the lair and gives it the spawn index after every
    /// dummy's.
    /// </summary>
    /// <param name="def">The dungeon whose boss this is.</param>
    /// <param name="weapon">What it holds: the variant <see cref="RollBoss"/> rolled, in play.</param>
    /// <param name="promisedStacks">The guaranteed stacks its drop carries: the visit's <see cref="PromisedStacks"/>; 0, the tutorial's, when a caller has none to give.</param>
    public static EnemyState BossRow(DungeonDef def, Weapon weapon, int promisedStacks = 0)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentOutOfRangeException.ThrowIfNegative(promisedStacks);

        var boss = def.Boss;
        return new EnemyState
        {
            Weapon = weapon,
            BaseMaxHp = boss.MaxHp,
            MoveBudget = boss.MovementBudget,
            Innate = ModifierSet.Of(boss.Innate.Select(kv => (kv.Key, kv.Value)).ToArray()),
            Title = boss.Name,
            Attunement = def.Attunement,
            Theme = def.Theme,
            PromisedStacks = promisedStacks,
        };
    }
}
