namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded weapon entries (§5.6), keyed by stable string id and kept in
/// file order, and the one place a <see cref="Weapon"/> is made from one:
/// <see cref="Instantiate"/> hands out a fresh instance every call, because
/// acquired stacks are per item and two actors never share a weapon.
/// </summary>
public sealed class WeaponCatalogue
{
    private readonly Dictionary<string, WeaponDef> _byId = new(StringComparer.Ordinal);
    private readonly EnchantmentCatalogue _enchantments;

    /// <summary>Build over validated data; the ids are unique by then, and every enchantment ref resolves.</summary>
    public WeaponCatalogue(WeaponsData data, EnchantmentCatalogue enchantments)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(enchantments);
        _enchantments = enchantments;

        All = data.Weapons.ToArray();
        foreach (var def in All)
            if (!_byId.TryAdd(def.Id, def))
                throw new ContentException(def.Id, ContentValidator.RuleDuplicateId);
        Uniques = All.Where(d => d.Unique).ToArray();
    }

    /// <summary>Every entry, in file order.</summary>
    public IReadOnlyList<WeaponDef> All { get; }

    public WeaponDef this[string id]
        => TryGet(id, out var def) ? def : throw new KeyNotFoundException($"No weapon '{id}' in the catalogue.");

    public bool TryGet(string id, out WeaponDef def) => _byId.TryGetValue(id, out def!);

    /// <summary>
    /// The variants of <paramref name="cls"/>, in file order: what a drop or a
    /// placement chooses among. The class's uniques are not among them — each
    /// is hand-placed, never rolled — and are listed apart (<see cref="Uniques"/>).
    /// </summary>
    public IReadOnlyList<WeaponDef> ByClass(WeaponClass cls) => All.Where(d => d.Class == cls && !d.Unique).ToArray();

    /// <summary>The unique table (§1.5), in file order: every entry carrying the unique flag, each derived from a variant.</summary>
    public IReadOnlyList<WeaponDef> Uniques { get; }

    /// <summary>
    /// The unique authored from <paramref name="variantId"/>, or null where
    /// nobody wrote one: the first half of §3.2's unique roll, which prefers the
    /// unique of the variant the drop has already rolled.
    /// </summary>
    public WeaponDef? UniqueDerivedFrom(string variantId)
        => Uniques.FirstOrDefault(d => string.Equals(d.DerivedFrom, variantId, StringComparison.Ordinal));

    /// <summary>
    /// The uniques of <paramref name="cls"/>, in file order: the other half of
    /// that roll. Eleven uniques cover thirty-two variants, so two thirds of
    /// variants have none of their own — a won roll re-steers over this list
    /// rather than handing back an ordinary weapon, which would leave those farms
    /// at an effective 0% however deep they ran, against §3.2's "it keeps
    /// climbing either way". File order, so a draw over it means the same thing
    /// on every run.
    /// </summary>
    public IReadOnlyList<WeaponDef> UniquesOf(WeaponClass cls) => Uniques.Where(d => d.Class == cls).ToArray();

    /// <summary>The one martial variant of <paramref name="cls"/> in <paramref name="role"/>: what a placement roll decodes to.</summary>
    public WeaponDef Variant(WeaponClass cls, VariantRole role)
    {
        var found = All.Where(d => d.Class == cls && d.Role == role && !d.Unique).ToList();
        return found.Count == 1
            ? found[0]
            : throw new KeyNotFoundException($"{cls} has {found.Count} {role} variants; exactly one is expected.");
    }

    /// <summary>
    /// A fresh <see cref="Weapon"/> of <paramref name="id"/>. A wand's innate is
    /// its element, supplied here by the caller because the roll belongs to the
    /// drop (§3.1): it becomes the first enchantment, ahead of anything the
    /// entry lists. Nothing else takes an element. A unique enchantment is
    /// attached at tier 1 whatever the entry asked for.
    /// <para>
    /// Everything attached here is forged — what the weapon is as it drops, the
    /// element included (§3.1) — so the count goes to the item rather than the
    /// def's list being counted again there: only what is grafted on afterwards
    /// is acquired (<see cref="Weapon.ForgedEnchantmentCount"/>).
    /// </para>
    /// <para>
    /// <paramref name="rolled"/> is the same door for a martial drop's rare
    /// entry (§3.1, <see cref="LootTable"/>): it is forged like everything else
    /// attached here — which is what makes it farmable (§3.2) and what counts
    /// toward service time (§6.2) — and it goes on here rather than in the loot
    /// table so that this stays "the one place a <see cref="Weapon"/> is made
    /// from" a def. It arrives at tier 1, always, whatever it is.
    /// </para>
    /// </summary>
    /// <param name="id">The catalogue row to stamp from.</param>
    /// <param name="element">A wand's element, rolled by the drop; null for everything else.</param>
    /// <param name="rolled">An entry the drop rolled onto this item, attached last and forged; null for a weapon that rolled none.</param>
    /// <exception cref="KeyNotFoundException">No such weapon, or an enchantment it names is unknown.</exception>
    /// <exception cref="ArgumentException">A wand with no element, or an element on anything else.</exception>
    public Weapon Instantiate(string id, DamageType? element = null, EnchantmentDef? rolled = null)
    {
        var def = this[id];
        var listed = def.Enchantments.Select(r => _enchantments[r.Id]).ToList();
        var attached = new List<Enchantment>(listed.Count + 2);

        if (def.Class == WeaponClass.Wand)
        {
            var fixedElement = listed.FirstOrDefault(e => e.Effect == EffectKind.ElementalDamage);
            if (fixedElement == null)
            {
                if (element is null or DamageType.None)
                    throw new ArgumentException($"'{id}' is a wand: its element is supplied at instantiation.", nameof(element));
                attached.Add(new Enchantment(_enchantments.InnateFor(element.Value), Tier: 1));
            }
            else if (element is not null && element != fixedElement.DamageType)
            {
                throw new ArgumentException($"'{id}' is fixed to {fixedElement.DamageType}; it cannot be instantiated as {element}.", nameof(element));
            }
        }
        else if (element is not null)
        {
            throw new ArgumentException($"'{id}' takes no element: only a wand does.", nameof(element));
        }

        for (int i = 0; i < listed.Count; i++)
        {
            var entry = listed[i];
            attached.Add(new Enchantment(entry, entry.Unique ? 1 : Math.Max(1, def.Enchantments[i].Tier)));
        }

        if (rolled != null)
            attached.Add(new Enchantment(rolled, Tier: 1));   // always exactly one, always tier 1 (§3.1)

        return new Weapon(def, attached, forgedCount: attached.Count);
    }
}
