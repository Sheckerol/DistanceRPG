namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay constants carried over from the Phaser prototype. Distances stay
/// in the original logic units (pixels, <see cref="Tile"/> = 32 per tile);
/// the 3D presentation layer converts to world units when placing geometry.
/// Weapons live in the content catalogue (<see cref="GameContent.Weapons"/>)
/// and are named here by stable string id, never by index.
/// </summary>
public static class GameConstants
{
    /// <summary>
    /// Logic units per map tile: the one units-to-tiles conversion, named for
    /// what it converts (the <c>MovementUnitsPerMana</c> pattern, never a bare
    /// rate). What Longshot's "per tile" and a shove's "one tile" divide by.
    /// </summary>
    public const float LogicUnitsPerTile = 32f;

    /// <summary>
    /// The tile size in logic units: the short name tile geometry (centres,
    /// tile-of, line of sight, collision) reads. The same number as
    /// <see cref="LogicUnitsPerTile"/>, which unit conversions name instead.
    /// </summary>
    public const float Tile = LogicUnitsPerTile;

    /// <summary>Movement budget per character per turn, in logic units.</summary>
    public const float MaxDistance = 160f;

    /// <summary>Player movement speed, logic units per second.</summary>
    public const float Speed = 160f;

    /// <summary>Character collision radius, in logic units.</summary>
    public const float PlayerHalf = (32f - 4f) / 2f;

    public const int PlayerHp = 100;
    public const int DummyHp = 50;

    /// <summary>
    /// Mana pool per actor, for casts and for enchantment triggers; it comes
    /// back only from unspent movement (<see cref="Tuning.MovementUnitsPerMana"/>).
    /// </summary>
    public const int MaxMana = 100;

    /// <summary>Enemy movement budget per turn, in logic units.</summary>
    public const float EnemyMove = 100f;

    /// <summary>Enemy movement animation speed, logic units per second.</summary>
    public const float EnemySpeed = 150f;

    /// <summary>Turns after defeat before a dummy resurrects.</summary>
    public const int DummyResurrectTurns = 10;

    /// <summary>A room needs at least this many enemies to roll a staff healer.</summary>
    public const int MinEnemiesForStaffHealer = 3;

    /// <summary>Chance a qualifying room converts one of its enemies into a staff healer.</summary>
    public const double StaffHealerChance = 0.5;

    /// <summary>
    /// Members the starting party has (§2.1's four): the roster's exact length,
    /// checked against the roster at load and against the presentation's spawn
    /// capacity by test, so a longer roster aborts startup rather than spawning
    /// a prefix and silently dropping whoever came last.
    /// </summary>
    public const int PartySize = 4;

    /// <summary>
    /// Starting weapon id per party member A–D: dagger, sword, axe, staff
    /// (§2.1 names the classes; <c>party.json</c> will decide the items, so
    /// these are Phase 1's provisional picks).
    /// </summary>
    public static readonly IReadOnlyList<string> CharStartingWeaponIds =
        ["weakspot_stiletto", "tower_guard", "great_axe", "staff_of_renewal"];

    /// <summary>
    /// Slot-1 weapon id per party member: the engine-side Staff of Renewal
    /// for A–C, a healer option for anyone, as today; D, the caster, carries
    /// the Staff of Mire beside its own.
    /// </summary>
    public static readonly IReadOnlyList<string> StartingBagWeaponIds =
        ["staff_of_renewal", "staff_of_renewal", "staff_of_renewal", "staff_of_mire"];

    public static readonly IReadOnlyList<string> CharIds = new[] { "A", "B", "C", "D" };
}
