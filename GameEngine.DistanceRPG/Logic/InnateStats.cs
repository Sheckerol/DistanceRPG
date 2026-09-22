namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The four innate stats (§2.1), in the order the doc's table lists them.
/// Member order is the order <see cref="InnateStats.Entries"/> yields, which is
/// what the HUD prints and what a save writes, so a row and a serialised spread
/// can never drift apart.
/// </summary>
public enum Stat
{
    STR,
    DEX,
    CON,
    INT,
}

/// <summary>
/// One party member's permanent spread (§2.1): the numbers 1, 2, 3 and 4 in
/// some order, so every spread sums to <see cref="Sum"/> and "no character is
/// better endowed than another; they are differently arranged". Fixed at
/// creation and never raised.
/// <para>
/// Nothing multiplies a value by a stat. A stat divides a <em>threshold</em>
/// (§2.3: "there is no rate multiplier anywhere"), so a 4 against a 1 is a
/// fourfold difference in how fast a pool grows and never a bonus on a number —
/// which is why this type has no place in the damage pipeline and lives on
/// <c>PartyMemberState</c> rather than on <c>ActorState</c>.
/// </para>
/// <para>
/// A value type because a spread has no identity: two members arranged the same
/// way carry the same spread. The permutation rule is checked where content is
/// loaded (<see cref="ContentValidator.ValidateParty"/>), not here — <see cref="None"/>
/// is deliberately not a permutation.
/// </para>
/// </summary>
public readonly record struct InnateStats(int STR, int DEX, int CON, int INT)
{
    /// <summary>The bottom of the range: "hopeless at exactly one thing", and the divisor a thing with no nature uses (§2.1).</summary>
    public const int Low = 1;

    /// <summary>The top of the range: "excellent at exactly one thing". The best value, because a stat divides.</summary>
    public const int High = 4;

    /// <summary>What every spread sums to (§2.1) — a consequence of the permutation rule, named so a test can state it.</summary>
    public const int Sum = 10;

    /// <summary>
    /// A thing with no nature: every bar costs itself, which is the divisor a
    /// weapon's wear uses (§2.1, "a weapon divides by 1"). What an actor carries
    /// when no roster entry gave it a spread. Not a permutation, and never
    /// validated as one.
    /// </summary>
    public static readonly InnateStats None = new(Low, Low, Low, Low);

    /// <summary>The one accessor: a caller names the stat it wants. There is no int indexer, so nothing reads a spread by position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stat"/> is not a <see cref="Stat"/>.</exception>
    public int Of(Stat stat) => stat switch
    {
        Stat.STR => STR,
        Stat.DEX => DEX,
        Stat.CON => CON,
        Stat.INT => INT,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Not a Stat."),
    };

    /// <summary>
    /// The better of two stats — Sword &amp; Shield's <c>max(STR, DEX)</c> (§2.2).
    /// The caller computes the max so <see cref="Progression.XpToNext"/> still
    /// takes a single int; <see cref="Progression.GoverningStat"/> is the general
    /// form over a class's whole row.
    /// </summary>
    public int Best(Stat a, Stat b) => Math.Max(Of(a), Of(b));

    /// <summary>Every stat with its value, in <see cref="Stat"/> order — deterministic for the HUD and for a save, the <see cref="ModifierSet.Entries"/> precedent.</summary>
    public IEnumerable<(Stat Stat, int Value)> Entries =>
    [
        (Stat.STR, STR),
        (Stat.DEX, DEX),
        (Stat.CON, CON),
        (Stat.INT, INT),
    ];

    /// <summary>
    /// The four values are <see cref="Low"/>..<see cref="High"/> with no repeat:
    /// the §2.1 rule, which rejects a duplicate, an out-of-range value and — with
    /// them — any wrong <see cref="Total"/>. Checked at load (§5.7), never in the
    /// constructor: "free to check and silently wrong if it drifts".
    /// </summary>
    public bool IsPermutation
    {
        get
        {
            int seen = 0;
            foreach (var (_, value) in Entries)
            {
                if (value < Low || value > High)
                    return false;
                int bit = 1 << value;
                if ((seen & bit) != 0)
                    return false;
                seen |= bit;
            }
            return true;
        }
    }

    /// <summary>The spread's total: <see cref="Sum"/> for every permutation, and the cheapest way to see one that is not.</summary>
    public int Total => STR + DEX + CON + INT;

    public override string ToString() => $"STR {STR} / DEX {DEX} / CON {CON} / INT {INT}";
}
