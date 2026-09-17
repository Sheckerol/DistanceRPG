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
    }

    /// <summary>Every entry, in file order.</summary>
    public IReadOnlyList<WeaponDef> All { get; }

    public WeaponDef this[string id]
        => TryGet(id, out var def) ? def : throw new KeyNotFoundException($"No weapon '{id}' in the catalogue.");

    public bool TryGet(string id, out WeaponDef def) => _byId.TryGetValue(id, out def!);

    /// <summary>Every entry of <paramref name="cls"/>, in file order — the variants and, once they exist, the uniques.</summary>
    public IReadOnlyList<WeaponDef> ByClass(WeaponClass cls) => All.Where(d => d.Class == cls).ToArray();

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
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such weapon, or an enchantment it names is unknown.</exception>
    /// <exception cref="ArgumentException">A wand with no element, or an element on anything else.</exception>
    public Weapon Instantiate(string id, DamageType? element = null)
    {
        var def = this[id];
        var listed = def.Enchantments.Select(r => _enchantments[r.Id]).ToList();
        var attached = new List<Enchantment>(listed.Count + 1);

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

        return new Weapon(def, attached);
    }
}
