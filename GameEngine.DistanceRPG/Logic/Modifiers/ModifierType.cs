namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The modifiers a weapon can carry as stacks (§1.1). A modifier has no
/// magnitude of its own: there is no <c>Brace 3</c>, only <c>Brace x3</c>, and
/// what one stack is worth lives in <see cref="ModifierRules"/> alone.
/// Member order is fixed by §1.7 and is the order <see cref="ModifierSet.Entries"/>
/// enumerates in, so it is part of the deterministic surface (HUD, saves).
/// </summary>
public enum ModifierType
{
    Brace,
    Block,
    CritWindow,
    CritMultiplier,
    Cleave,
    Charges,
    Longshot,
    Light,
    Riposte,
    Push,
    Drag,
    Splitting,
    Overwatch,
    Opportunist,
    Rout,
    Pin,
    Softening,
    CritWeaken,
    CritSunder,
    BlockWeaken,

    /// <summary>Reserved by §1.7: no behaviour and no content anywhere in Phase 1.</summary>
    OnHitPoison,

    Resonant,
    Momentum,   // Momentum: enchantment-only
}
