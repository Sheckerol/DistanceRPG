namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A weapon as an item: the statline and identity of its <see cref="WeaponDef"/>,
/// the modifier stacks it carries (§1.1) and the enchantments attached to it
/// (§3.3). Range and Cost are in logic units (32 per tile); Cost is subtracted
/// from the wielder's movement budget per swing or cast, ManaCost from the mana
/// pool per cast. A class rather than a record because <see cref="Modifiers"/>
/// is the live total: <see cref="Forged"/> is the identity spread, fixed at
/// construction and never written, and exists only to compute the ceiling;
/// <see cref="Modifiers"/> is forged plus acquired, and is what every
/// resolution site reads. Made by <see cref="WeaponCatalogue.Instantiate"/>,
/// one instance per item.
/// </summary>
public sealed class Weapon
{
    /// <summary>Percentages are integers out of this.</summary>
    private const int Percent = 100;

    public Weapon(WeaponDef def, IReadOnlyList<Enchantment> enchantments)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(enchantments);

        Id = def.Id;
        Name = def.Name;
        Class = def.Class;
        Role = def.Role;
        Range = def.Range;
        Damage = def.Damage;
        Cost = def.Cost;
        ManaCost = def.ManaCost;
        Forged = ModifierSet.Of(def.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());
        Modifiers = Forged;
        Enchantments = enchantments.ToArray();   // attachment order, copied so nothing outside can reorder it
        AreaShape = def.Shape;
        Unique = def.Unique;
        Resolve();
    }

    public string Id { get; }
    public string Name { get; }
    public WeaponClass Class { get; }
    public VariantRole? Role { get; }
    public int Range { get; }
    public int Damage { get; }
    public int Cost { get; }
    public int ManaCost { get; }

    /// <summary>The identity spread — class baseline, variant role, unique spread, dungeon theme — fixed here and never written again.</summary>
    public ModifierSet Forged { get; }

    /// <summary>The live total, forged plus acquired: what every resolution site reads.</summary>
    public ModifierSet Modifiers { get; private set; }

    /// <summary>In attachment order, which is gameplay (§1.7): never normalised, sorted or deduped.</summary>
    public IReadOnlyList<Enchantment> Enchantments { get; }

    /// <summary>A wand's geometry; null for everything else.</summary>
    public AreaShape? AreaShape { get; }

    public bool Unique { get; }

    /// <summary>The sort of weapon the relations file means by "kind": staff and wand cast, bow and throwing are ranged, the rest melee.</summary>
    public static WeaponKind KindOf(WeaponClass cls) => cls switch
    {
        WeaponClass.Staff or WeaponClass.Wand => WeaponKind.Caster,
        WeaponClass.Ranged or WeaponClass.Throwing => WeaponKind.Ranged,
        _ => WeaponKind.Melee,
    };

    public WeaponKind Kind => KindOf(Class);

    /// <summary>True for a staff or wand — a weapon that casts rather than strikes.</summary>
    public bool IsCaster => Kind == WeaponKind.Caster;

    /// <summary>A caster's innate enchantment — the effect a staff casts, the element a wand carries — first in attachment order; null on a martial weapon.</summary>
    public Enchantment? Innate => IsCaster && Enchantments.Count > 0 ? Enchantments[0] : null;

    /// <summary>
    /// The movement a swing or cast costs after Light's discount:
    /// <c>Cost * (100 - Modifiers.Value(Light)) / 100</c>, truncated. Computed
    /// once and cached, so the HUD and the movement gate agree on one number.
    /// </summary>
    public int ResolvedCost { get; private set; }

    /// <summary>The mana a cast costs after Resonant's discount, the same shape against <see cref="ManaCost"/>.</summary>
    public int ResolvedManaCost { get; private set; }

    /// <summary>
    /// The one mutator: add <paramref name="n"/> acquired stacks of
    /// <paramref name="t"/>, clamped to forged plus the acquired headroom, and
    /// refresh the resolved costs. Farm depth, grafts and improvements are all
    /// this call.
    /// </summary>
    public void Acquire(ModifierType t, int n)
    {
        Modifiers = Modifiers.With(t, n, Forged);
        Resolve();
    }

    public int Stacks(ModifierType t) => Modifiers.Stacks(t);

    private void Resolve()
    {
        ResolvedCost = Cost * (Percent - Modifiers.Value(ModifierType.Light)) / Percent;
        ResolvedManaCost = ManaCost * (Percent - Modifiers.Value(ModifierType.Resonant)) / Percent;
    }

    public override string ToString() => $"{Name} ({Id}: {Modifiers})";
}
