using System.Diagnostics.CodeAnalysis;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded enchantment entries (§5.7) — the rows of the game's catalogue,
/// which is code (<see cref="EnchantmentDefinitions"/>) — keyed by stable string
/// id and kept in the catalogue's order, with <see cref="Tuning.ApplyPercent"/>
/// laid over each entry's own percentage and <see cref="Tuning.ArcanePotency"/>
/// over the magnitude of the one id it names, and the attunement chart (§1.4)
/// read off the relations the elements sit in: four types in two opposed pairs,
/// never a matrix. Beside the entries it holds the two relations
/// <c>restricted.json</c> keeps for enchantments (§5.5):
/// <see cref="NeverRolled"/>, the ids no roll grants — the unique souls, as the
/// shipped file has it, and whatever the file says, since the relation is its
/// alone — the enchantment-keyed <c>requires</c> rows
/// (<see cref="Prerequisites"/>), a lingering element's element, and the
/// exclusion groups that name enchantments (<see cref="Excludes"/>), the opposed
/// elements. Phase 1 shipped the eight innates — the four
/// staff effects and the four wand elements — Vampiric, and the nine unique
/// souls; Phase 3 completed §3.3's starting set with Arcane, Shattering, the
/// shielding entry and Echoing, the last declared with its behaviour open.
/// </summary>
public sealed class EnchantmentCatalogue
{
    private readonly Dictionary<string, EnchantmentDef> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<DamageType, EnchantmentDef> _elements = new();
    private readonly Dictionary<string, string[]> _requires = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _excludes = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<DamageType, DamageType> _opposed;

    /// <exception cref="ContentException">An entry is malformed (<see cref="ContentValidator.ValidateEnchantments"/>), a number it leaves to the tuning is not there (<see cref="ContentValidator.ValidateDials"/>), the chart is broken (<see cref="ContentValidator.ValidateOpposition"/>) or <c>neverRolled</c> names something that is no enchantment of the catalogue (<see cref="ContentValidator.ValidateNeverRolled"/>).</exception>
    public EnchantmentCatalogue(EnchantmentsData data, Tuning tuning, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);
        ContentValidator.ValidateEnchantments(data);   // never build around a malformed entry
        ContentValidator.ValidateDials(data, tuning);  // nor around a number no one sets
        _opposed = ContentValidator.ValidateOpposition(data, restricted);   // the chart: every element opposed by exactly one, both ways
        ContentValidator.ValidateNeverRolled(data, restricted);            // neverRolled names this catalogue's entries: which ones is the file's alone

        var all = new List<EnchantmentDef>(data.Enchantments.Count);
        foreach (var entry in data.Enchantments)
        {
            // The tuning table wins per id; an id it does not name keeps the
            // catalogue's percentage — and the rows that leave theirs to it
            // (EnchantmentDefinitions.SetByTuning) are named there, or the load
            // has stopped. Arcane's magnitude is laid over the same way and for
            // the same reason (§5.3: a number a playtest turns lives in
            // tuning.json rather than in a catalogue row) — and keyed the same
            // way too, by the id the dial names rather than by the kind. Keyed by
            // the kind it would overwrite every bonus-damage row there will ever
            // be: a second such entry would silently inherit Arcane's dial with
            // no way for its row to say otherwise, and the magnitude the
            // validator checks on those rows would be one nothing ever reads.
            var def = entry with
            {
                ApplyPercent = tuning.ApplyPercent.TryGetValue(entry.Id, out int dial) ? dial : entry.ApplyPercent,
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

        // And the exclusion groups that name enchantments, expanded both ways as a
        // group means: a group names modifiers or enchantments, never both
        // (ContentValidator.ValidateModifierRelations), and no id twice.
        foreach (var group in restricted.Excludes)
            foreach (var a in group.Where(_byId.ContainsKey))
            {
                if (!_excludes.TryGetValue(a, out var excluded))
                    _excludes[a] = excluded = new HashSet<string>(StringComparer.Ordinal);
                excluded.UnionWith(group.Where(b => b != a && _byId.ContainsKey(b)));
            }
    }

    /// <summary>Every entry, in the catalogue's order.</summary>
    public IReadOnlyList<EnchantmentDef> All { get; }

    /// <summary>The ids content may name: what the relations file is checked against.</summary>
    public IReadOnlySet<string> Ids { get; }

    /// <summary>
    /// The ids no roll grants and no service copies (§5.5 <c>neverRolled</c>):
    /// in the shipped file the unique souls, which exist only on the uniques that
    /// carry them. <c>restricted.json</c>'s alone (the user's ruling of
    /// 2026-09-25): the load asks only that each is an entry here, never that the
    /// list matches the entries' <c>unique</c> flag, which pins a tier.
    /// </summary>
    public IReadOnlySet<string> NeverRolled { get; }

    /// <summary>
    /// Everything not in <see cref="NeverRolled"/>, in the catalogue's order: the
    /// entries a grant may consider, which is a wider set than the entries a
    /// grant may hand out.
    /// <para>
    /// <strong>It is not itself a grantable pool.</strong> A drop draws from
    /// <see cref="LootTable.DropOrder"/>, which starts from a code-fixed order
    /// (the catalogue's re-indexes on every append) and then filters out the entries
    /// that cannot work on what they would land on: a declared-inert kind, and a
    /// kind whose only behaviour sits on an event that class never raises. Both
    /// of those live here — <c>echoing</c> fires nothing at all, <c>aegis</c>
    /// fires only as a compiled step on a defender — and the shipped
    /// <see cref="NeverRolled"/> lists neither: it is an economy rule about souls,
    /// not a list of what cannot fire. So the enchanter's offer
    /// (<see cref="Enchanter.Offers"/>, Phase 6a.3b) applies the inert half of that
    /// test beside <see cref="NeverRolled"/> (<see cref="LootTable.IsInert"/>), and
    /// never offers an entry that can only reserve a lock. <c>aegis</c> passes it —
    /// a defender's step fires on any weapon — but nothing puts it among what a
    /// campaign has met: no drop rolls it and no shipped weapon carries it
    /// (docs/roadmap/open-questions.md, PHASE 6a DECISION 4).
    /// </para>
    /// </summary>
    public IReadOnlyList<EnchantmentDef> Rollable { get; }

    public EnchantmentDef this[string id]
        => TryGet(id, out var def) ? def : throw new KeyNotFoundException($"No enchantment '{id}' in the catalogue.");

    public bool TryGet(string id, out EnchantmentDef def) => _byId.TryGetValue(id, out def!);

    /// <summary>The ids <paramref name="id"/> cannot exist without — the relations' enchantment-keyed <c>requires</c> row — or none.</summary>
    public IReadOnlyList<string> Prerequisites(string id)
        => _requires.TryGetValue(id, out var needs) ? needs : Array.Empty<string>();

    /// <summary>
    /// Whether <paramref name="id"/>, attached behind the entries
    /// <paramref name="ahead"/> (their ids, in attachment order), lacks a
    /// prerequisite ahead of it (<see cref="Prerequisites"/>), and the first it
    /// lacks in its <c>requires</c> row's order. A dependency is met on the weapon
    /// itself, ahead of the dependent: the loop fires in attachment order (§1.7),
    /// and a lingering element reads the element that fired before it.
    /// <para>
    /// <strong>The one question</strong> content validation asks of every entry a
    /// weapon lists (<see cref="ContentValidator.RuleEnchantmentDependency"/>) and
    /// the enchanter asks of an entry it would attach last
    /// (<see cref="Enchanter.Offers"/>), so the two can never part.
    /// </para>
    /// </summary>
    public bool LacksPrerequisiteAhead(string id, IEnumerable<string> ahead, [NotNullWhen(true)] out string? missing)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(ahead);
        foreach (var need in Prerequisites(id))
            if (!ahead.Contains(need, StringComparer.Ordinal))
            {
                missing = need;
                return true;
            }
        missing = null;
        return false;
    }

    /// <summary>Whether <c>restricted.json</c>'s <c>excludes</c> keep <paramref name="a"/> and <paramref name="b"/> off one weapon — two enchantments one group names, as the opposed elements are — both ways, as a group means.</summary>
    public bool Excludes(string a, string b) => _excludes.TryGetValue(a, out var excluded) && excluded.Contains(b);

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
