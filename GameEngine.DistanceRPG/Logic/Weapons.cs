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

    /// <summary>
    /// The attached entries, in attachment order — the weapon's own copy of what
    /// it was handed, and the only thing <see cref="CreditEnchantment"/> writes
    /// to. Private, and <see cref="Enchantments"/> hands out a read-only view of
    /// it rather than the array itself: attachment order is gameplay (§1.7) and
    /// fixes the order entries fire in, so it is the weapon's to change and
    /// nobody else's.
    /// </summary>
    private readonly Enchantment[] _enchantments;

    /// <param name="def">The row this item is stamped from.</param>
    /// <param name="enchantments">The entries attached, in attachment order: the forged ones first, anything acquired behind them.</param>
    /// <param name="forgedCount">
    /// How many of <paramref name="enchantments"/>, counted from the front, are
    /// forged. Defaults to what <paramref name="def"/> lists, so an entry handed
    /// in past that list is one something attached later;
    /// <see cref="WeaponCatalogue.Instantiate"/> passes its own count, because a
    /// wand's element is supplied at instantiation and is forged all the same
    /// (§1.4, §3.1) though no def lists it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="forgedCount"/> is not a count of <paramref name="enchantments"/>.</exception>
    public Weapon(WeaponDef def, IReadOnlyList<Enchantment> enchantments, int? forgedCount = null)
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
        _enchantments = enchantments.ToArray();   // attachment order, copied so nothing outside can reorder it
        Enchantments = Array.AsReadOnly(_enchantments);   // a live view: a credited entry shows through, a write does not get in
        int forged = forgedCount ?? Math.Min(def.Enchantments.Count, _enchantments.Length);
        if (forged < 0 || forged > _enchantments.Length)
            throw new ArgumentOutOfRangeException(nameof(forgedCount), forgedCount,
                $"'{def.Id}' was handed {_enchantments.Length} entries; the forged ones are a prefix of them.");
        ForgedEnchantmentCount = forged;
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

    /// <summary>
    /// How many of <see cref="Enchantments"/>, from the front, are forged: part
    /// of what the weapon <em>is</em>, rather than what was attached to it later
    /// (§3.1). The same line <see cref="Forged"/> draws through the modifiers,
    /// drawn through the entries — fixed at construction, never written again.
    /// <para>
    /// The distinction is not weapon-versus-magic: a staff's effect and a wand's
    /// element are forged, and they are the whole of what those weapons do — a
    /// wand with no forged entry would have no XP source at all (§2.2). It is
    /// what weapon proficiency is credited on, which is why it is a count here
    /// and not a convention at the credit site: a wizard's Arcane dagger levels
    /// daggers on the knife alone.
    /// </para>
    /// </summary>
    public int ForgedEnchantmentCount { get; }

    /// <summary>Whether the entry at <paramref name="index"/> of <see cref="Enchantments"/> is forged (<see cref="ForgedEnchantmentCount"/>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Nothing is attached at <paramref name="index"/>.</exception>
    public bool IsForged(int index)
    {
        if ((uint)index >= (uint)Enchantments.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{Name} ({Id}) carries {Enchantments.Count} enchantments.");
        return index < ForgedEnchantmentCount;
    }

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
    /// What this item would reserve of a wielder's pool if every entry fitted:
    /// the sum of the attached entries' <see cref="Enchantment.EffectiveLock"/>
    /// (§3.3). It is the item's own figure and knows no wielder, exactly as
    /// <see cref="ResolvedCost"/> does; what is actually taken out of a pool is
    /// <see cref="ActorState.PaidLocks"/>, which stops at the first entry that
    /// does not fit and leaves the rest dormant.
    /// <para>
    /// Derived rather than cached, because a tier is: <see cref="CreditEnchantment"/>
    /// replaces an entry and the deeper lock has to show through the same read.
    /// </para>
    /// </summary>
    public int TotalLock
    {
        get
        {
            int total = 0;
            foreach (var entry in _enchantments)
                total += entry.EffectiveLock;
            return total;
        }
    }

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
    /// this call, and the relations bind every one of them alike (§1.1: "the
    /// forge, a farm roll, a graft, and a boss theme"): a modifier
    /// <see cref="ModifierRules.Allowed"/> refuses beside what this weapon
    /// already holds — the wrong kind, an exclusion, a missing prerequisite, a
    /// forged-only modifier from zero, a member outside the table — is refused
    /// here too. A source gates its offers on the same predicate before it
    /// rolls (§1.7: the graft roll filters its candidates first), so a refusal
    /// here is a source that skipped its gate: a bug, thrown rather than a
    /// stack granted silently. Deepening what the weapon holds passes while
    /// nothing beside it excludes it, which is how a forged <c>Charges</c>
    /// deepens and a bow without one never gains any.
    /// </summary>
    /// <exception cref="InvalidOperationException">The relations refuse <paramref name="t"/> on this weapon beside its modifiers.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative: nothing is ever removed.</exception>
    public void Acquire(ModifierType t, int n)
    {
        if (!GameContent.Current.Modifiers.Allowed(t, Kind, Modifiers))
            throw new InvalidOperationException(
                $"{Name} ({Id}) cannot hold {t} beside {Modifiers}: the relations bind every source, so an offer is gated on ModifierRules.Allowed before it is made.");
        Modifiers = Modifiers.With(t, n, Forged);
        Resolve();
    }

    /// <summary>
    /// The second mutator, and the only one that touches an entry: credit
    /// <paramref name="xp"/> mana spent to the enchantment at
    /// <paramref name="index"/>, at the INT that was spending it (§3.3). The
    /// entry is a record and the climb is <see cref="Enchantment.WithXp"/>'s, so
    /// this replaces the element rather than mutating one — attachment order,
    /// the entry's identity and everything else about the weapon stay exactly
    /// where they were.
    /// <para>
    /// Every source of enchantment XP comes through here and hands over the same
    /// figure: the mana actually paid. A trigger the wielder paid for is one
    /// (§3.3), and the farm's grant is another — "a plain XP grant instead of a
    /// special case" (§3.5) — so the farm has no path of its own into a tier.
    /// </para>
    /// <para>
    /// The trigger's callers are the five appliers that write a mana record: the
    /// Cast, DamageTaken, DamageDealt, Killed and HealingAboveFull events each
    /// credit the entries their loop attributed a payment to
    /// (<see cref="EnchantmentPayment"/>). A handler never calls this — a handler
    /// never writes — and neither does a behaviour, which has no index.
    /// </para>
    /// </summary>
    /// <param name="index">The entry's position in <see cref="Enchantments"/>.</param>
    /// <param name="xp">Mana that entry paid; never negative.</param>
    /// <param name="stat">The INT that was doing the spending, 1..4; a flat 1 for a thing with no nature.</param>
    /// <exception cref="ArgumentOutOfRangeException">Nothing is attached at <paramref name="index"/>, <paramref name="xp"/> is negative, or <paramref name="stat"/> is outside 1..4.</exception>
    public void CreditEnchantment(int index, int xp, int stat)
    {
        if ((uint)index >= (uint)_enchantments.Length)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{Name} ({Id}) carries {_enchantments.Length} enchantments.");
        _enchantments[index] = _enchantments[index].WithXp(xp, stat);
    }

    public int Stacks(ModifierType t) => Modifiers.Stacks(t);

    private void Resolve()
    {
        ResolvedCost = Cost * (Percent - Modifiers.Value(ModifierType.Light)) / Percent;
        ResolvedManaCost = ManaCost * (Percent - Modifiers.Value(ModifierType.Resonant)) / Percent;
    }

    public override string ToString() => $"{Name} ({Id}: {Modifiers})";
}
