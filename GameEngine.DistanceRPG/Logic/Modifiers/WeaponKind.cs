namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The sort of weapon a modifier may sit on (§1.1 "Kind"). Bow and throwing are
/// both <see cref="Ranged"/>; staff and wand are <see cref="Caster"/>. A relation
/// entry with no kind restriction reads as <see cref="Any"/>.
/// </summary>
[Flags]
public enum WeaponKind
{
    Melee = 1,
    Ranged = 2,
    Caster = 4,
    Any = 7,
}
