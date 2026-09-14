namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The §1.1 table, one place only: what a stack is worth, where a modifier
/// caps, and the relations that decide what a weapon may hold. The numbers
/// come from <see cref="Tuning"/> and the relations from <see cref="RestrictedData"/>
/// (§5.5), so this is the loaded singleton §1.7 asks for, reached as
/// <c>GameContent.Current.Modifiers</c>. Only the predicates stay compiled:
/// <see cref="Allowed"/>, because it is the question rather than the data, and
/// <see cref="Symmetric"/>, because a half-declared exclusion must be impossible.
/// </summary>
public sealed class ModifierRules
{
    /// <summary>Fallback when tuning names no offset: nothing is granted before the first stack.</summary>
    private const int DefaultOffset = 0;

    /// <summary>
    /// Fallback when tuning names no forge limit: a unique is a variant with one
    /// modifier raised to x3, so nothing is forged past 3 (§1.1, §1.5).
    /// </summary>
    private const int DefaultMaxForged = 3;

    private static readonly IReadOnlyDictionary<string, ModifierType> ByName =
        Enum.GetValues<ModifierType>().ToDictionary(t => t.ToString(), t => t, StringComparer.Ordinal);

    /// <summary>The stable string ids modifiers go by in content: their enum names.</summary>
    public static IReadOnlySet<string> ModifierIds { get; } = new HashSet<string>(ByName.Keys, StringComparer.Ordinal);

    /// <summary>Resolve a content id to a modifier; false for enchantment ids and typos alike.</summary>
    public static bool TryParseId(string id, out ModifierType type) => ByName.TryGetValue(id, out type);

    private readonly Tuning _tuning;

    /// <summary>
    /// Build the rules from data. Enchantment ids share the relations file
    /// (§5.5) but not these tables: members that are not modifiers are dropped
    /// here and validated alongside the enchantment catalogue instead.
    /// </summary>
    public ModifierRules(Tuning tuning, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);

        _tuning = tuning;
        AcquiredHeadroom = tuning.AcquiredHeadroom;

        Excludes = Symmetric(restricted.Excludes.Select(group => Modifiers(group)));
        ForgedOnly = new SortedSet<ModifierType>(Modifiers(restricted.ForgedOnly));   // enum order: deterministic to enumerate

        var requires = new SortedDictionary<ModifierType, ModifierType[]>();
        foreach (var (id, needs) in restricted.Requires)
            if (TryParseId(id, out var t))
                requires[t] = Modifiers(needs).Distinct().OrderBy(x => x).ToArray();
        Requires = requires;

        var kinds = new SortedDictionary<ModifierType, WeaponKind>();
        foreach (var (id, kind) in restricted.Kind)
            if (TryParseId(id, out var t))
                kinds[t] = RestrictedData.TryParseKind(kind, out var parsed)
                    ? parsed
                    : throw new ContentException(id, ContentValidator.RuleKindValue);
        RequiresKind = kinds;
    }

    /// <summary>
    /// Acquired stacks a modifier may gain above its forged count — one shared
    /// budget per modifier type that every acquired source draws on (§1.1).
    /// </summary>
    public int AcquiredHeadroom { get; }

    /// <summary>What one stack of <paramref name="t"/> is worth (the §1.1 table).</summary>
    public int PerStack(ModifierType t) => _tuning.PerStack.GetValueOrDefault(t, 0);

    /// <summary>What <paramref name="t"/> grants before its first stack: CritMultiplier's base x2, Charges' first throw.</summary>
    public int Offset(ModifierType t) => _tuning.Offset.GetValueOrDefault(t, DefaultOffset);

    /// <summary>
    /// The most of <paramref name="t"/> any weapon may be forged with. Checked when
    /// a weapon is built — by the content validator — never in <see cref="ModifierSet.With"/>:
    /// it constrains the forge and never acquisition.
    /// </summary>
    public int MaxForged(ModifierType t) => _tuning.MaxForged.GetValueOrDefault(t, DefaultMaxForged);

    /// <summary>These two cannot coexist. The symmetric closure of the file's groups.</summary>
    public IReadOnlyDictionary<ModifierType, ModifierType[]> Excludes { get; }

    /// <summary>Deepenable if already present, never grantable from zero.</summary>
    public IReadOnlySet<ModifierType> ForgedOnly { get; }

    /// <summary>Cannot exist without these. Not symmetric — a dependency, not a pair.</summary>
    public IReadOnlyDictionary<ModifierType, ModifierType[]> Requires { get; }

    /// <summary>Only on this sort of weapon; absent means <see cref="WeaponKind.Any"/>.</summary>
    public IReadOnlyDictionary<ModifierType, WeaponKind> RequiresKind { get; }

    /// <summary>
    /// The ceiling on a stack count: forged plus the acquired headroom. No second
    /// term on purpose — there is no flat cap anywhere in the system, so anything
    /// that looks like one appearing here later is a design regression.
    /// </summary>
    public int Cap(ModifierType t, int forgedStacks)
        => forgedStacks + AcquiredHeadroom;    // no clamp: nothing is capped

    /// <summary>The raw number a stack count resolves to; whether it is absolute or proportional is the modifier's business.</summary>
    public int Resolve(ModifierType t, int stacks)
        => Offset(t) + PerStack(t) * stacks;

    /// <summary>
    /// The one definition of "this weapon cannot hold that": kind, forged-only,
    /// exclusion and prerequisites folded into one predicate every caller asks.
    /// It gates offers rather than rejecting after the fact.
    /// </summary>
    public bool Allowed(ModifierType t, WeaponKind kind, ModifierSet present)
        => RequiresKind.GetValueOrDefault(t, WeaponKind.Any).HasFlag(kind)
        && (!ForgedOnly.Contains(t) || present.Stacks(t) > 0)
        && !Excludes.GetValueOrDefault(t, []).Any(x => present.Stacks(x) > 0)
        &&  Requires.GetValueOrDefault(t, []).All(x => present.Stacks(x) > 0);

    /// <summary>
    /// Close exclusion groups into directed pairs: every member of a group
    /// excludes every other member, both ways. A group is declared once as an
    /// array, so it can never be half-declared. Keys and arrays are in enum order.
    /// </summary>
    public static IReadOnlyDictionary<ModifierType, ModifierType[]> Symmetric(IEnumerable<ModifierType[]> groups)
    {
        var closure = new SortedDictionary<ModifierType, SortedSet<ModifierType>>();
        foreach (var group in groups)
            foreach (var a in group)
                foreach (var b in group)
                {
                    if (a == b)
                        continue;
                    if (!closure.TryGetValue(a, out var excluded))
                        closure[a] = excluded = new SortedSet<ModifierType>();
                    excluded.Add(b);
                }

        var result = new SortedDictionary<ModifierType, ModifierType[]>();
        foreach (var (a, excluded) in closure)
            result[a] = excluded.ToArray();
        return result;
    }

    private static ModifierType[] Modifiers(IEnumerable<string> ids)
    {
        var found = new List<ModifierType>();
        foreach (var id in ids)
            if (TryParseId(id, out var t))
                found.Add(t);
        return found.ToArray();
    }
}
