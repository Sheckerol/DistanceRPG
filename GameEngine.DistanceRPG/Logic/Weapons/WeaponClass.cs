namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The eight weapon classes (§1.2). The three prototype classes come first, so
/// the enum order carries the index parity the string ids replaced: Dagger,
/// Sword, Spear, then everything the roadmap added after them. Phase 3's drop
/// tables key off this.
/// </summary>
public enum WeaponClass
{
    Dagger,
    Sword,
    Spear,
    Axe,
    Ranged,
    Throwing,
    Staff,
    Wand,
}
