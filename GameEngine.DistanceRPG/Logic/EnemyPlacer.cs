namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Populates a generated map with enemy spawn positions: 0–4 dummies on
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

    /// <summary>Stream splitter so enemy placement never aliases the map stream.</summary>
    private const long SeedSalt = 0x9E3779B9;

    /// <summary>
    /// Deterministic spawns for a map seed: logic-space centre positions plus a
    /// weapon (index into <see cref="GameConstants.Weapons"/>) per dummy. Rank
    /// and file roll only attack weapons; a crowded room
    /// (<see cref="GameConstants.MinEnemiesForStaffHealer"/>+ enemies) may then
    /// convert one of its members into a staff healer.
    /// </summary>
    public static List<(float X, float Y, int WeaponIdx)> PlaceEnemies(MapData map, long mapSeed)
    {
        var rng = new Mulberry32(mapSeed ^ SeedSalt);
        var spawns = new List<(float X, float Y, int WeaponIdx)>();

        // Ordinary enemies never roll a staff — it heals, it can't fight — so
        // the loadout draw is restricted to the non-caster weapons.
        var attackWeapons = new List<int>();
        for (int i = 0; i < GameConstants.Weapons.Count; i++)
            if (!GameConstants.Weapons[i].IsCaster)
                attackWeapons.Add(i);

        // DebugRooms is the pristine room list — FogBoxBuilder appends
        // synthetic union rooms to Rooms, which must not spawn anything.
        foreach (var room in map.DebugRooms)
        {
            if ((room.Cy, room.Cx) == map.PlayerStart) continue;

            int count = rng.NextInt(0, MaxEnemiesPerRoom);
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

                    spawns.Add((
                        c * GameConstants.Tile + GameConstants.Tile / 2f,
                        r * GameConstants.Tile + GameConstants.Tile / 2f,
                        attackWeapons[rng.NextInt(0, attackWeapons.Count - 1)]));
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
                spawns[pick] = (x, y, GameConstants.StaffWeaponIdx);
            }
        }

        return spawns;
    }
}
