namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded enchantment entries (§5.7), keyed by stable string id and kept in
/// file order, with <see cref="Tuning.ApplyPercent"/> laid over each entry's
/// own percentage. Phase 1 ships the eight innates — the four staff effects and
/// the four wand elements; Phase 3 fills the rest of the catalogue.
/// </summary>
public sealed class EnchantmentCatalogue
{
    private readonly Dictionary<string, EnchantmentDef> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<DamageType, EnchantmentDef> _elements = new();

    /// <exception cref="ContentException">An entry is malformed (<see cref="ContentValidator.ValidateEnchantments"/>).</exception>
    public EnchantmentCatalogue(EnchantmentsData data, Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(tuning);
        ContentValidator.ValidateEnchantments(data);   // never build around a malformed entry

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
}
