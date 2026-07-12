namespace GameEngine.DistanceRPG.Logic;

/// <summary>Kinds of ongoing status effect a party member can carry.</summary>
public enum StatusEffectType
{
    /// <summary>
    /// Heal-over-time: at end of turn it restores HP equal to its level, then
    /// loses a level; at zero the effect is dropped. Casting a staff stacks it.
    /// </summary>
    Regeneration,
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
