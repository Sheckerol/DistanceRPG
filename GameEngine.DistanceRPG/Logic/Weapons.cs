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

    /// <summary>
    /// The §1.1 modifier stacks every resolution site reads, bridged from
    /// <see cref="Abilities"/> so the shipped values hold until the catalogue
    /// replaces abilities with a forged spread: <c>CritRange n</c> is
    /// <c>CritWindow ×n</c> (the dagger still crits on 16+), <c>Block v</c> is
    /// <c>Block ×(v / 3)</c> (3 absorbed per stack, so the sword's 3 is one
    /// stack), <c>Brace n</c> is <c>Brace ×n</c>, and <c>HealCast</c> carries no
    /// stacks. Computed once at construction.
    /// </summary>
    public ModifierSet Modifiers { get; } = Bridge(Abilities);

    /// <summary>What one Block stack absorbs — the §1.1 per-stack value the bridge divides the legacy figure by.</summary>
    private const int LegacyBlockPerStack = 3;

    // The parity bridge, deleted with AbilityType: a translation of the legacy
    // ability list, not a behaviour switch.
    private static ModifierSet Bridge(IReadOnlyList<WeaponAbility> abilities)
        => ModifierSet.Of(abilities
            .Where(a => a.Type != AbilityType.HealCast)   // a cast is an enchantment, not a stack
            .Select(a => a.Type switch
            {
                AbilityType.CritRange => (ModifierType.CritWindow, a.Value),
                AbilityType.Block => (ModifierType.Block, a.Value / LegacyBlockPerStack),
                AbilityType.Brace => (ModifierType.Brace, a.Value),
                _ => throw new ArgumentOutOfRangeException(nameof(abilities), a.Type, "No modifier bridge for this ability."),
            })
            .ToArray());
}
