namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Kinds of ongoing status effect an actor can carry — the §1.5 table, in its
/// fixed order. <see cref="Regeneration"/> is the shipped heal-over-time; the
/// rest are declared here because the enchantment catalogue names them (a
/// staff's innate says what it applies), and their behaviour arrives with the
/// status table.
/// </summary>
public enum StatusEffectType
{
    /// <summary>
    /// Heal-over-time: at end of turn it restores HP equal to its level, then
    /// loses a level; at zero the effect is dropped. Casting a staff stacks it.
    /// </summary>
    Regeneration,

    /// <summary>A pool absorbing 1 damage per level before HP, spent as it absorbs.</summary>
    Ward,

    /// <summary>Damage per turn equal to its level, decaying.</summary>
    Poison,

    /// <summary>Cuts the target's movement budget by 10% a level.</summary>
    Mire,

    /// <summary>Takes +1 damage per level from every hit.</summary>
    Sundered,

    /// <summary>Deals -1 damage per level with every hit, floored at 1.</summary>
    Weakened,

    /// <summary>Damage per turn equal to its level, decaying — the weapon's own DoT.</summary>
    Bleeding,

    /// <summary>Hidden: surplus healing points, converting into Ward.</summary>
    OverhealPool,

    /// <summary>Damage per turn equal to its level, decaying, keyed on the element that lit it.</summary>
    Searing,

    /// <summary>Block stripped for a round, 1 per level.</summary>
    Softened,
}

/// <summary>
/// A stacking, self-decaying status effect. Each end of turn it fires for an
/// amount equal to its <see cref="Level"/>, then the level drops by one; the
/// effect is removed once the level would fall to zero.
/// </summary>
public sealed class StatusEffect
{
    public required StatusEffectType Type { get; init; }
    public int Level { get; set; }
}
