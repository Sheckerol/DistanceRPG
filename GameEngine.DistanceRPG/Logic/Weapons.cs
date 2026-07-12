namespace GameEngine.DistanceRPG.Logic;

public enum AbilityType
{
    /// <summary>Widens the crit window: crit on rolls ≥ 20 - value.</summary>
    CritRange,

    /// <summary>Absorbs up to <c>value</c> incoming damage, never below 1 taken.</summary>
    Block,

    /// <summary>Free attack when the enemy moves into this character's range.</summary>
    Brace,

    /// <summary>
    /// A staff cast: instead of attacking, applies a <see cref="StatusEffectType.Regeneration"/>
    /// buff to a targeted ally. <c>Value</c> is the buff level added per cast.
    /// </summary>
    HealCast,
}

public sealed record WeaponAbility(AbilityType Type, int Value);

/// <summary>
/// A weapon. Range and Cost are in logic units (the original game's pixels,
/// 32 per tile); Cost is subtracted from the wielder's movement budget per
/// swing. <paramref name="ManaCost"/> is spent per use in addition to Cost —
/// zero for ordinary weapons, positive for staves that cast.
/// </summary>
public sealed record Weapon(
    string Name, int Range, int Damage, int Cost,
    IReadOnlyList<WeaponAbility> Abilities, int ManaCost = 0)
{
    public WeaponAbility? GetAbility(AbilityType type)
        => Abilities.FirstOrDefault(a => a.Type == type);

    /// <summary>True for a staff — a weapon that casts a buff rather than striking.</summary>
    public bool IsCaster => GetAbility(AbilityType.HealCast) != null;
}
