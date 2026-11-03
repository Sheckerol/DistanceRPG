namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded enchantment entries (§5.7), keyed by stable string id and kept in
/// file order, with <see cref="Tuning.ApplyPercent"/> laid over each entry's
/// own percentage and <see cref="Tuning.ArcanePotency"/> over the magnitude of
/// the one id it names, and the attunement chart (§1.4) read off the relations
/// the elements sit in: four types in two opposed pairs, never a matrix. Beside
/// the entries it holds the two relations the file keeps for enchantments
/// (§5.5): <see cref="NeverRolled"/>, the ids no roll grants — the unique
/// souls, cross-checked against the <c>unique</c> flag on load — and the
/// enchantment-keyed <c>requires</c> rows (<see cref="Prerequisites"/>), a
/// lingering element's element. Phase 1 shipped the eight innates — the four
/// staff effects and the four wand elements — Vampiric, and the nine unique
/// souls; Phase 3 completed §3.3's starting set with Arcane, Shattering, the
/// shielding entry and Echoing, the last declared with its behaviour open.
/// </summary>
public sealed class EnchantmentCatalogue
{
    private readonly Dictionary<string, EnchantmentDef> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<DamageType, EnchantmentDef> _elements = new();
    private readonly Dictionary<string, string[]> _requires = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<DamageType, DamageType> _opposed;

    /// <exception cref="ContentException">An entry is malformed (<see cref="ContentValidator.ValidateEnchantments"/>), the chart is broken (<see cref="ContentValidator.ValidateOpposition"/>) or the unique flag and <c>neverRolled</c> disagree (<see cref="ContentValidator.ValidateNeverRolled"/>).</exception>
    public EnchantmentCatalogue(EnchantmentsData data, Tuning tuning, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);
        ContentValidator.ValidateEnchantments(data);   // never build around a malformed entry
        _opposed = ContentValidator.ValidateOpposition(data, restricted);   // the chart: every element opposed by exactly one, both ways
        ContentValidator.ValidateNeverRolled(data, restricted);            // one question, one answer: unique entries are exactly the never-rolled ones

        var all = new List<EnchantmentDef>(data.Enchantments.Count);
        foreach (var entry in data.Enchantments)
        {
            // The tuning table wins per id; an id it does not name keeps the
            // catalogue's percentage. Arcane's magnitude is laid over the same
            // way and for the same reason (§5.3: a number a playtest turns lives
            // in tuning.json rather than in a content row) — and keyed the same
            // way too, by the id the dial names rather than by the kind. Keyed by
            // the kind it would overwrite every bonus-damage row there will ever
            // be: a second such entry would silently inherit Arcane's dial with
            // no way for content to say otherwise, and the magnitude the
            // validator checks on those rows would be one nothing ever reads.
            var def = entry with
            {
                ApplyPercent = tuning.Lookup(x => x.ApplyPercent, entry.Id, entry.ApplyPercent),
                Potency = entry.Id == Tuning.ArcaneId ? tuning.ArcanePotency : entry.Potency,
            };
            all.Add(def);
            _byId[def.Id] = def;
            if (def.Effect == EffectKind.ElementalDamage && def.DamageType is { } type && type != DamageType.None)
                _elements[type] = def;   // one per type, by the opposition check
        }
        All = all;
        Ids = new HashSet<string>(_byId.Keys, StringComparer.Ordinal);
        NeverRolled = new HashSet<string>(restricted.NeverRolled, StringComparer.Ordinal);
        Rollable = all.Where(e => !NeverRolled.Contains(e.Id)).ToArray();

        // The relations keyed by an enchantment: read here, beside the catalogue, never by the modifier rules.
        foreach (var (id, needs) in restricted.Requires)
            if (_byId.ContainsKey(id))
                _requires[id] = needs.ToArray();
    }

    /// <summary>Every entry, in file order.</summary>
    public IReadOnlyList<EnchantmentDef> All { get; }

    /// <summary>The ids content may name: what the relations file is checked against.</summary>
    public IReadOnlySet<string> Ids { get; }

    /// <summary>
    /// The ids no roll grants and no service copies (§5.5 <c>neverRolled</c>):
    /// the unique souls, which exist only on the uniques that carry them.
    /// Cross-checked with the entries' <c>unique</c> flag on load.
    /// </summary>
    public IReadOnlySet<string> NeverRolled { get; }

    /// <summary>
    /// Everything not in <see cref="NeverRolled"/>, in file order: the entries a
    /// grant may consider, which is a wider set than the entries a grant may
    /// hand out.
    /// <para>
    /// <strong>It is not itself a grantable pool.</strong> A drop draws from
    /// <see cref="LootTable.DropOrder"/>, which starts from a code-fixed order
    /// (file order re-indexes on every append) and then filters out the entries
    /// that cannot work on what they would land on: a declared-inert kind, and a
    /// kind whose only behaviour sits on an event that class never raises. Both
    /// of those live here — <c>echoing</c> fires nothing at all, <c>aegis</c>
    /// fires only as a compiled step on a defender — and neither can be moved
    /// into <see cref="NeverRolled"/>, which
    /// <see cref="ContentValidator.ValidateNeverRolled"/> pins to the unique
    /// flag. So §6.4's enchanter must apply that same live-behaviour test rather
    /// than <see cref="NeverRolled"/> alone, or it will offer an entry that can
    /// only reserve a lock; docs/roadmap/open-questions.md carries that, beside
    /// the note that <c>aegis</c> has no source at all until the enchanter
    /// exists.
    /// </para>
    /// </summary>
    public IReadOnlyList<EnchantmentDef> Rollable { get; }

    public EnchantmentDef this[string id]
        => TryGet(id, out var def) ? def : throw new KeyNotFoundException($"No enchantment '{id}' in the catalogue.");

    public bool TryGet(string id, out EnchantmentDef def) => _byId.TryGetValue(id, out def!);

    /// <summary>The ids <paramref name="id"/> cannot exist without — the relations' enchantment-keyed <c>requires</c> row — or none.</summary>
    public IReadOnlyList<string> Prerequisites(string id)
        => _requires.TryGetValue(id, out var needs) ? needs : Array.Empty<string>();

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
