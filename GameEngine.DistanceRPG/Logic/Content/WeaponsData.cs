namespace GameEngine.DistanceRPG.Logic;

/// <summary>An enchantment a weapon arrives with, by id, at a tier (1 unless the entry says otherwise; a unique is always 1).</summary>
public sealed record EnchantmentRef(string Id, int Tier = 1);

/// <summary>
/// One row of §5.6 <c>weapons.json</c>: the statline, the forged spread and
/// the enchantments a weapon of this id arrives with. Immutable content; a
/// <see cref="Weapon"/> is instantiated from it per item, never shared.
/// </summary>
/// <param name="Id">The stable string id everything refers to it by.</param>
/// <param name="Name">The display name.</param>
/// <param name="Class">One of the eight classes; fixes the weapon's <see cref="WeaponKind"/>.</param>
/// <param name="Role">The variant role for a martial variant; null for casters and uniques.</param>
/// <param name="Range">Reach, surface to surface, in logic units (32 per tile).</param>
/// <param name="Damage">Base damage per hit.</param>
/// <param name="Cost">Movement per attack or cast, before Light's discount.</param>
/// <param name="ManaCost">Mana per cast, before Resonant's discount; 0 for a martial weapon.</param>
/// <param name="Forged">The forged spread: modifier to stack count (§1.1). Bounded by MaxForged at validation.</param>
/// <param name="Enchantments">Innate ids in attachment order, which is gameplay and never reordered (§1.7).</param>
/// <param name="Shape">A wand's geometry; null for everything else.</param>
/// <param name="Unique">A unique: one modifier at x3 over a variant's spread, carrying a soul.</param>
/// <param name="DerivedFrom">The variant id a unique is derived from; null otherwise.</param>
public sealed record WeaponDef(
    string Id, string Name, WeaponClass Class, VariantRole? Role,
    int Range, int Damage, int Cost, int ManaCost,
    IReadOnlyDictionary<ModifierType, int> Forged,
    IReadOnlyList<EnchantmentRef> Enchantments,
    AreaShape? Shape, bool Unique, string? DerivedFrom);

/// <summary>The §5.6 <c>weapons.json</c> shape: the weapon rows in file order.</summary>
public sealed record WeaponsData(IReadOnlyList<WeaponDef> Weapons);
