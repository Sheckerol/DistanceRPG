namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A multiset of modifiers: <see cref="ModifierType"/> to stack count (§1.1).
/// An immutable value object. The only way a stack is ever added is
/// <see cref="With"/>, which clamps against the weapon's forged set, so a set
/// can never be built out of bounds — the invariant is kept at the door rather
/// than at every read (§1.7). Nothing is ever removed from a set: a satisfied
/// prerequisite stays satisfied.
/// </summary>
public sealed class ModifierSet : IEquatable<ModifierSet>
{
    private static readonly int TypeCount = Enum.GetValues<ModifierType>().Length;

    /// <summary>Stack counts indexed by enum ordinal, which gives enum order for free.</summary>
    private readonly int[] _stacks;

    /// <summary>No stacks of anything.</summary>
    public static readonly ModifierSet Empty = new(new int[TypeCount]);

    private ModifierSet(int[] stacks) => _stacks = stacks;

    /// <summary>
    /// Build a set directly, unclamped. This is the content loader's constructor
    /// for a weapon's forged spread: the forge is bounded by
    /// <see cref="ModifierRules.MaxForged"/> at validation, never by the
    /// acquisition cap (§1.7). A type named more than once sums — "the same
    /// modifier applied three times".
    /// </summary>
    public static ModifierSet Of(params (ModifierType Type, int Stacks)[] stacks)
    {
        var counts = new int[TypeCount];
        foreach (var (type, n) in stacks)
        {
            if (n < 0)
                throw new ArgumentOutOfRangeException(nameof(stacks), n, $"{type} x{n}: a stack count is never negative.");
            counts[Index(type)] += n;
        }
        return new ModifierSet(counts);
    }

    /// <summary>Stacks of <paramref name="t"/> in this set; 0 when absent.</summary>
    public int Stacks(ModifierType t) => _stacks[Index(t)];

    /// <summary>The §1.1 table value of <paramref name="t"/> at this set's stack count.</summary>
    public int Value(ModifierType t) => GameContent.Current.Modifiers.Resolve(t, Stacks(t));

    /// <summary>
    /// Add <paramref name="n"/> stacks of <paramref name="t"/>, clamped to
    /// <see cref="ModifierRules.Cap"/> over <paramref name="forged"/>'s stacks of
    /// the same type. The one add path for variants, uniques, farm depth and
    /// grafts alike: a stack landing on an already-capped modifier is a no-op,
    /// not a special case. Returns a new set; this one is unchanged.
    /// </summary>
    public ModifierSet With(ModifierType t, int n, ModifierSet forged)
    {
        ArgumentNullException.ThrowIfNull(forged);
        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n), n, "Stacks are only ever added; nothing is removed from a weapon.");

        int i = Index(t);
        var counts = (int[])_stacks.Clone();
        int cap = GameContent.Current.Modifiers.Cap(t, forged.Stacks(t));
        // The clamp bounds the addition, never the set: a count already above the
        // cap (only possible with a mismatched forged set) is left where it is.
        counts[i] = Math.Max(counts[i], Math.Min(counts[i] + n, cap));
        return new ModifierSet(counts);
    }

    /// <summary>The modifiers present, with their stack counts, in <see cref="ModifierType"/> order.</summary>
    public IEnumerable<(ModifierType Type, int Stacks)> Entries
    {
        get
        {
            for (int i = 0; i < _stacks.Length; i++)
                if (_stacks[i] > 0)
                    yield return ((ModifierType)i, _stacks[i]);
        }
    }

    private static int Index(ModifierType t)
    {
        int i = (int)t;
        if ((uint)i >= (uint)TypeCount)
            throw new ArgumentOutOfRangeException(nameof(t), t, "Not a ModifierType.");
        return i;
    }

    public bool Equals(ModifierSet? other)
        => other is not null && _stacks.AsSpan().SequenceEqual(other._stacks);

    public override bool Equals(object? obj) => Equals(obj as ModifierSet);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var count in _stacks)
            hash.Add(count);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        var entries = Entries.Select(e => $"{e.Type} x{e.Stacks}").ToArray();
        return entries.Length == 0 ? "(none)" : string.Join(", ", entries);
    }
}
