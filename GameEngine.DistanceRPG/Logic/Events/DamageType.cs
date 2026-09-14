namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The type a hit carries into the attunement chart (§1.4). <see cref="None"/>
/// is untyped — every martial swing, and Arcane. The four elements are the
/// wands' innate enchantments. A hit has exactly one type.
/// </summary>
public enum DamageType
{
    None,
    Flaming,
    Cold,
    Shocking,
    Acidic,
}
