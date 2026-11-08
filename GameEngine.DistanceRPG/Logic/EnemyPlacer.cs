namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Populates a generated map with enemy spawn positions: 0 to a room cap of
/// dummies (<see cref="RoomCap"/>, at most <see cref="MaxEnemiesPerRoom"/>) on
/// random distinct tiles of every room except the party's starting room.
///
/// This runs as a separate step after map generation, on its own RNG stream
/// derived from the map seed — a save system regenerates the dungeon from the
/// seed on every entry but places enemies only on the first, restoring their
/// saved states afterwards, so placement must not feed anything else's RNG.
/// </summary>
public static class EnemyPlacer
{
    public const int MaxEnemiesPerRoom = 4;

    /// <summary>
    /// A clear count no floor is capped under: what a placement outside a
    /// campaign — the golden dungeon, a test, a tool — places as, every room
    /// rolling the full 0 to <see cref="MaxEnemiesPerRoom"/> it always did.
    /// </summary>
    public const int Unprotected = MaxEnemiesPerRoom;

    /// <summary>
    /// Beginner protection: the most dummies a room on floor
    /// <paramref name="floorNumber"/> (1-based, as the player reads it) may hold,
    /// for a campaign that has cleared <paramref name="dungeonsCleared"/>
    /// dungeons — the floor number plus the clears, never more than
    /// <see cref="MaxEnemiesPerRoom"/>. So the tutorial's first floor holds one
    /// a room at most, the next dungeon's first floor two, and the full four
    /// arrive with depth or experience.
    /// </summary>
    public static int RoomCap(int floorNumber, int dungeonsCleared)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(floorNumber, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(dungeonsCleared);
        return (int)Math.Min(MaxEnemiesPerRoom, (long)floorNumber + dungeonsCleared);
    }

    /// <summary>The weapon a crowded room's healer is converted to.</summary>
    public const string HealerWeaponId = "staff_of_renewal";

    /// <summary>
    /// Stream splitter so enemy placement never aliases the map stream. Internal
    /// rather than private so a test asserting some new stream is not this one can
    /// name the constant instead of restating its value: a restated literal goes
    /// on passing if this splitter ever moves, which is the one thing such an
    /// assertion exists to catch (§3.2's revival stream, <c>FarmLadderTests</c>).
    /// </summary>
    internal const long SeedSalt = 0x9E3779B9;

    /// <summary>
    /// The classes rank and file roll from: the five close-range martial
    /// classes, until kiting AI and the wand placement scorer exist (settled).
    /// The draw decodes to a class and a variant role, so the stream depends on
    /// a count fixed here rather than on the catalogue's length: adding a
    /// weapon re-rolls nobody.
    /// </summary>
    public static readonly IReadOnlyList<WeaponClass> PlacedClasses =
        [WeaponClass.Dagger, WeaponClass.Sword, WeaponClass.Spear, WeaponClass.Axe, WeaponClass.Throwing];

    private static readonly int VariantsPerClass = Enum.GetValues<VariantRole>().Length;

    /// <summary>
    /// Deterministic spawns for a map seed: logic-space centre positions plus a
    /// weapon id per dummy. Rank and file roll one of the placed classes' four
    /// variants — a single draw per spawn, as before; a crowded room
    /// (<see cref="GameConstants.MinEnemiesForStaffHealer"/>+ enemies) may then
    /// convert one of its members into a staff healer.
    /// </summary>
    /// <param name="maxPerRoom">
    /// The most a room may hold (<see cref="RoomCap"/>): the count draw is one
    /// draw whatever the cap, so a lower cap changes how many stand in a room and
    /// never how many draws the stream has made before the count.
    /// </param>
    public static List<(float X, float Y, string WeaponId)> PlaceEnemies(MapData map, long mapSeed, int maxPerRoom = MaxEnemiesPerRoom)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPerRoom, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxPerRoom, MaxEnemiesPerRoom);
        var rng = new Mulberry32(mapSeed ^ SeedSalt);
        var spawns = new List<(float X, float Y, string WeaponId)>();
        var catalogue = GameContent.Current.Weapons;
        int loadouts = PlacedClasses.Count * VariantsPerClass;

        // DebugRooms is the pristine room list — FogBoxBuilder appends
        // synthetic union rooms to Rooms, which must not spawn anything.
        foreach (var room in map.DebugRooms)
        {
            if ((room.Cy, room.Cx) == map.PlayerStart) continue;

            int count = rng.NextInt(0, maxPerRoom);
            var taken = new HashSet<(int R, int C)>();
            int roomStart = spawns.Count;
            for (int i = 0; i < count; i++)
            {
                // A few tries to find a free tile; small rooms may not fit all.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int c = rng.NextInt(room.X, room.X + room.W - 1);
                    int r = rng.NextInt(room.Y, room.Y + room.H - 1);
                    if (!taken.Add((r, c))) continue;

                    // One draw, decoded as class then variant role — never a staff
                    // or wand: a caster heals or shapes, it does not stand and fight.
                    int k = rng.NextInt(0, loadouts - 1);
                    var weapon = catalogue.Variant(PlacedClasses[k / VariantsPerClass], (VariantRole)(k % VariantsPerClass));

                    spawns.Add((
                        c * GameConstants.Tile + GameConstants.Tile / 2f,
                        r * GameConstants.Tile + GameConstants.Tile / 2f,
                        weapon.Id));
                    break;
                }
            }

            // A crowded room may field a healer: swap one placed member's
            // weapon for the staff. Rolled after placement so the per-room draw
            // order stays stable; the roll happens only when the room qualifies.
            int placed = spawns.Count - roomStart;
            if (placed >= GameConstants.MinEnemiesForStaffHealer
                && rng.NextDouble() < GameConstants.StaffHealerChance)
            {
                int pick = roomStart + rng.NextInt(0, placed - 1);
                var (x, y, _) = spawns[pick];
                spawns[pick] = (x, y, HealerWeaponId);
            }
        }

        return spawns;
    }
}
