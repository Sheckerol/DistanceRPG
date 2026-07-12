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
    /// Deterministic spawns for a map seed: logic-space centre positions plus
    /// a uniformly random weapon (index into <see cref="GameConstants.Weapons"/>)
    /// per dummy.
    /// </summary>
    public static List<(float X, float Y, int WeaponIdx)> PlaceEnemies(MapData map, long mapSeed)
    {
        var rng = new Mulberry32(mapSeed ^ SeedSalt);
        var spawns = new List<(float X, float Y, int WeaponIdx)>();

        // DebugRooms is the pristine room list — FogBoxBuilder appends
        // synthetic union rooms to Rooms, which must not spawn anything.
        foreach (var room in map.DebugRooms)
        {
            if ((room.Cy, room.Cx) == map.PlayerStart) continue;

            int count = rng.NextInt(0, MaxEnemiesPerRoom);
            var taken = new HashSet<(int R, int C)>();
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
                        rng.NextInt(0, GameConstants.Weapons.Count - 1)));
                    break;
                }
            }
        }

        return spawns;
    }
}
