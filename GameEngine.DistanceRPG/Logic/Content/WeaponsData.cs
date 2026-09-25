namespace GameEngine.DistanceRPG.Logic;

/// <summary>An enchantment a weapon arrives with, by id, at a tier (1 unless the entry says otherwise; a unique is always 1).</summary>
public sealed record EnchantmentRef(string Id, int Tier = 1);

/// <summary>
/// A weapon as the game plays it (§5.6): the statline, the forged spread and
/// the enchantments a weapon of this id arrives with. Immutable content; a
/// <see cref="Weapon"/> is instantiated from it per item, never shared.
/// <c>weapons.json</c> writes a variant as its class and what it adds to the
/// class's row, and a unique as its variant and what it raises; the reader
/// composes each into this whole row (<see cref="WeaponsJson"/>), so everything
/// after the load reads a weapon's numbers here and never asks its class.
/// </summary>
/// <param name="Id">The stable string id everything refers to it by.</param>
/// <param name="Name">The display name.</param>
/// <param name="Class">One of the eight classes; its row in <c>weapons.json</c> (<see cref="WeaponClassDef"/>) fixes the weapon's <see cref="WeaponKind"/>.</param>
/// <param name="Role">The variant role for a martial variant; null for casters and uniques.</param>
/// <param name="Range">Reach, surface to surface, in logic units (32 per tile).</param>
/// <param name="Damage">Base damage per hit.</param>
/// <param name="Cost">Movement per attack or cast, before Light's discount.</param>
/// <param name="ManaCost">Mana per cast, before Resonant's discount; 0 for a martial weapon.</param>
/// <param name="Forged">The forged spread: modifier to stack count (§1.1) — the class baseline and what the variant adds, and what a unique raises. Bounded by MaxForged at validation.</param>
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

/// <summary>
/// One weapon class as <c>weapons.json</c>'s <c>classes</c> section holds it
/// (the content rework, 1d; the user's ruling of 2026-09-25: the weapon types
/// are JSON): what a class carries as data — the kind it plays as, the stats it
/// levels on, the modifier it is named for, the baseline every weapon of it is
/// forged with, and the statline its variants share. The class itself is code
/// (<see cref="WeaponClass"/>): what a staff and a wand <em>do</em>, where the
/// placer and the caster drop order name it; its row is its numbers.
/// </summary>
/// <param name="Class">The class the row is for: every <see cref="WeaponClass"/> member has exactly one.</param>
/// <param name="Kind">What the relations mean by kind (§1.1): melee, ranged or caster. Caster exactly for the Staff and the Wand, which the code casts with.</param>
/// <param name="Stats">The stats the class levels on, the best of them governing (§2.2): one, or the Sword's two; never CON.</param>
/// <param name="Signature">The modifier the class is named for (§1.2's class feature), which a Purity variant deepens and a dungeon's theme is drawn from (§4.3); null for a caster.</param>
/// <param name="Baseline">What every weapon of the class is forged with (§1.2): the signature and one more, or a caster's <c>Resonant x1</c>.</param>
/// <param name="Range">The reach its variants share, in logic units (32 per tile).</param>
/// <param name="Damage">The damage its variants share.</param>
/// <param name="Cost">The movement an attack or cast takes, before Light's discount.</param>
/// <param name="ManaCost">The mana a cast takes, before Resonant's discount; 0 for a martial class.</param>
public sealed record WeaponClassDef(
    WeaponClass Class, WeaponKind Kind, IReadOnlyList<Stat> Stats, ModifierType? Signature,
    IReadOnlyDictionary<ModifierType, int> Baseline,
    int Range, int Damage, int Cost, int ManaCost);

/// <summary>
/// The §5.6 <c>weapons.json</c> content: the class rows, and the weapon rows —
/// the variants in file order, then the uniques in file order, each composed
/// whole (<see cref="WeaponDef"/>).
/// </summary>
/// <param name="Classes">One row per <see cref="WeaponClass"/> member, in the enum's order as the reader hands them over.</param>
/// <param name="Weapons">The variants, then the uniques, in file order: the order <see cref="WeaponCatalogue.All"/> keeps.</param>
public sealed record WeaponsData(IReadOnlyList<WeaponClassDef> Classes, IReadOnlyList<WeaponDef> Weapons)
{
    /// <summary>The row for <paramref name="cls"/> — the first, where records built in code hold two — or null where there is none.</summary>
    public WeaponClassDef? ClassOf(WeaponClass cls)
    {
        foreach (var row in Classes)
            if (row.Class == cls)
                return row;
        return null;
    }
}
