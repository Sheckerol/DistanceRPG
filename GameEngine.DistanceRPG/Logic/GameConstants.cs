namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay constants carried over from the Phaser prototype. Distances stay
/// in the original logic units (pixels, <see cref="Tile"/> = 32 per tile);
/// the 3D presentation layer converts to world units when placing geometry.
/// </summary>
public static class GameConstants
{
    /// <summary>Logic units per map tile.</summary>
    public const float Tile = 32f;

    /// <summary>Movement budget per character per turn, in logic units.</summary>
    public const float MaxDistance = 160f;

    /// <summary>Player movement speed, logic units per second.</summary>
    public const float Speed = 160f;

    /// <summary>Character collision radius, in logic units.</summary>
    public const float PlayerHalf = (32f - 4f) / 2f;

    public const int PlayerHp = 100;
    public const int DummyHp = 50;

    /// <summary>Enemy movement budget per turn, in logic units.</summary>
    public const float EnemyMove = 100f;

    /// <summary>Enemy movement animation speed, logic units per second.</summary>
    public const float EnemySpeed = 150f;

    /// <summary>Turns after defeat before the dummy resurrects.</summary>
    public const int DummyResurrectTurns = 3;

    public static readonly IReadOnlyList<Weapon> Weapons = new[]
    {
        new Weapon("Dagger", Range: 40, Damage: 15, Cost: 30,
            new[] { new WeaponAbility(AbilityType.CritRange, 4) }),
        new Weapon("Sword", Range: 80, Damage: 10, Cost: 50,
            new[] { new WeaponAbility(AbilityType.Block, 3) }),
        new Weapon("Spear", Range: 130, Damage: 7, Cost: 40,
            new[] { new WeaponAbility(AbilityType.Brace, 1) }),
    };

    /// <summary>Starting weapon index (into <see cref="Weapons"/>) per party member A–D.</summary>
    public static readonly IReadOnlyList<int> CharStartingWeaponIdx = new[] { 0, 1, 2, 2 };

    public static readonly IReadOnlyList<string> CharIds = new[] { "A", "B", "C", "D" };
}
