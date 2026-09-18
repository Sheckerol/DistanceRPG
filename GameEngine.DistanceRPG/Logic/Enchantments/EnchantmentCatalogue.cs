namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded enchantment entries (§5.7), keyed by stable string id and kept in
/// file order, with <see cref="Tuning.ApplyPercent"/> laid over each entry's
/// own percentage, and the attunement chart (§1.4) read off the relations the
/// elements sit in: four types in two opposed pairs, never a matrix. Phase 1
/// ships the eight innates — the four staff effects and the four wand
/// elements; Phase 3 fills the rest of the catalogue.
/// </summary>
public sealed class EnchantmentCatalogue
{
    private readonly Dictionary<string, EnchantmentDef> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<DamageType, EnchantmentDef> _elements = new();
    private readonly IReadOnlyDictionary<DamageType, DamageType> _opposed;

    /// <exception cref="ContentException">An entry is malformed (<see cref="ContentValidator.ValidateEnchantments"/>) or the chart is broken (<see cref="ContentValidator.ValidateOpposition"/>).</exception>
    public EnchantmentCatalogue(EnchantmentsData data, Tuning tuning, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);
        ContentValidator.ValidateEnchantments(data);   // never build around a malformed entry
        _opposed = ContentValidator.ValidateOpposition(data, restricted);   // the chart: every element opposed by exactly one, both ways

        var all = new List<EnchantmentDef>(data.Enchantments.Count);
        foreach (var entry in data.Enchantments)
        {
            // The tuning table wins per id; an id it does not name keeps the catalogue's percentage.
            var def = entry with { ApplyPercent = tuning.Lookup(x => x.ApplyPercent, entry.Id, entry.ApplyPercent) };
            all.Add(def);
            _byId[def.Id] = def;
            if (def.Effect == EffectKind.ElementalDamage && def.DamageType is { } type && type != DamageType.None)
                _elements[type] = def;   // one per type, by the opposition check
        }
        All = all;
        Ids = new HashSet<string>(_byId.Keys, StringComparer.Ordinal);
    }

    /// <summary>Every entry, in file order.</summary>
    public IReadOnlyList<EnchantmentDef> All { get; }

    /// <summary>The ids content may name: what the relations file is checked against.</summary>
    public IReadOnlySet<string> Ids { get; }

    public EnchantmentDef this[string id]
        => TryGet(id, out var def) ? def : throw new KeyNotFoundException($"No enchantment '{id}' in the catalogue.");

    public bool TryGet(string id, out EnchantmentDef def) => _byId.TryGetValue(id, out def!);

    /// <summary>The innate a wand of <paramref name="element"/> carries: the entry whose hit is typed with it.</summary>
    public EnchantmentDef InnateFor(DamageType element)
        => _elements.TryGetValue(element, out var def)
            ? def
            : throw new KeyNotFoundException($"No innate enchantment carries {element}.");

    /// <summary>The type <paramref name="type"/> is opposed by on the chart (§1.4), or null for an untyped hit.</summary>
    public DamageType? OpposedTo(DamageType type)
        => _opposed.TryGetValue(type, out var opposed) ? opposed : null;

    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> are an opposed pair — the chart's x1.5 relation, symmetric by validation.</summary>
    public bool Opposes(DamageType a, DamageType b) => a != DamageType.None && OpposedTo(a) == b;
}
