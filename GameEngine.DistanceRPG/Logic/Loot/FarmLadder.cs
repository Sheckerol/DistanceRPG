namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What one revival grants the dummy that earned it (§3.2): one third each,
/// drawn on the revival stream. A code-fixed enum rather than a content list,
/// because a draw's range may never depend on content — the rule
/// <see cref="EnemyPlacer"/> already states ("the stream depends on a count
/// fixed here rather than on the catalogue's length: adding a weapon re-rolls
/// nobody"), which here means adding an outcome would be a deliberate edit and
/// never a silent re-roll of every saved farm.
/// </summary>
public enum RevivalGain
{
    /// <summary>+<see cref="Tuning.ReviveStepPercent"/> of the dummy's current damage.</summary>
    Damage,

    /// <summary>+<see cref="Tuning.ReviveStepPercent"/> of its current max HP, restored full.</summary>
    Health,

    /// <summary>Both at once.</summary>
    Both,
}

/// <summary>
/// The §3.2 farm's arithmetic: the two ladders <c>DefeatCount</c> drives — how
/// fast a dummy comes back and how much stronger it is when it does — the odds
/// a deep farm buys, and the stream the revival rolls are drawn on.
/// <para>
/// Every member is a pure function of saved state and the §5.3 scalars, so a
/// dummy's whole history replays from <c>(mapSeed, spawnIndex, DefeatCount)</c>
/// and a save stores accumulated values rather than a stream position (§3.2,
/// §5.1). Nothing here reaches for <c>Random.Shared</c>: this phase's streams
/// are <c>mapSeed ^ salt</c> in <see cref="EnemyPlacer"/>'s pattern, and a
/// fallback to a process-wide RNG would be a save/reload divergence rather than
/// a default.
/// </para>
/// Engine-free and deterministic. <see cref="Mulberry32"/>, <c>MapGenerator</c>
/// and <c>Pathfinder</c> are untouched, so the golden parity this repo is built
/// on is not in the blast radius of any of it.
/// </summary>
public static class FarmLadder
{
    /// <summary>
    /// The revival stream's splitter, sibling to the drop's <c>LootSalt</c>:
    /// §3.2 asks for the roll on "the loot stream's sibling —
    /// <c>mapSeed ^ ReviveSalt</c> — never a continuation of an existing one".
    /// </summary>
    public const long ReviveSalt = 0x27D4EB2F;

    /// <summary>Percentages are integers out of this.</summary>
    private const int Percent = 100;

    /// <summary>The unique curve is quantised to integers out of this (see <see cref="UniqueChancePermille"/>).</summary>
    private const int Permille = 1000;

    private const int PermillePerPercent = Permille / Percent;

    /// <summary>
    /// The per-enemy term mixed into the revival seed, so the ladder belongs to
    /// the dummy rather than to the order the party walked the floor in.
    /// </summary>
    private const long SpawnMix = 0x2545F491;

    /// <summary>The per-cycle term, so each revival is its own seeded draw rather than a position in a running stream.</summary>
    private const long CycleMix = 0x9E3779B1;

    /// <summary>The outcomes a revival rolls between, counted off the enum for the reason <see cref="RevivalGain"/> gives.</summary>
    private static readonly int Gains = Enum.GetValues<RevivalGain>().Length;

    /// <summary>
    /// What one defeat adds to a dummy's <c>DefeatCount</c>: 1, or
    /// <see cref="Tuning.CleanKillBonus"/> for a clean kill — a killing blow
    /// dealing at least the target's <em>max</em> HP (settled.md, §3.2). The
    /// bar is the constitution and never what was left of it, which is what
    /// makes it uncheesable: softening a target never brings a clean kill
    /// closer. It also puts itself out of business by the most direct route,
    /// since the bar is the very number revival scaling raises.
    /// </summary>
    /// <param name="dealt">The post-mitigation, pre-clamp figure: a 40-damage crit into a 24-HP dummy reads as 40 (§3.2).</param>
    /// <param name="maxHp">The target's maximum, revival bonuses included.</param>
    public static int DefeatAdvance(int dealt, int maxHp)
        => dealt >= maxHp ? GameContent.Current.Tuning.CleanKillBonus : 1;

    /// <summary>
    /// Turns a dummy at <paramref name="defeatCount"/> takes to come back:
    /// <c>max(ResurrectTurnsFloor, ResurrectTurnsBase − n)</c> (§3.2). Speed
    /// front-loads and the statline back-loads, so the early farm gets busy and
    /// the late farm gets dangerous, and neither stage feels like the other one
    /// repeated. Three is a floor rather than a curve because a two-turn
    /// revival is a treadmill rather than a fight.
    /// </summary>
    public static int ResurrectTurns(int defeatCount)
    {
        var tuning = GameContent.Current.Tuning;
        return Math.Max(tuning.ResurrectTurnsFloor, tuning.ResurrectTurnsBase - defeatCount);
    }

    /// <summary>
    /// One revival's outcome, one third each (§3.2). The draw is the first off a
    /// freshly seeded <see cref="ReviveStream"/>, which is exactly why that
    /// factory finalizes its composed seed (<see cref="SplitMix32"/>).
    /// </summary>
    public static RevivalGain RollRevival(Mulberry32 rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return (RevivalGain)rng.NextInt(0, Gains - 1);
    }

    /// <summary>
    /// What a Damage or Health roll adds to <paramref name="current"/>:
    /// <see cref="Tuning.ReviveStepPercent"/> of the dummy's own current number,
    /// rounded to the nearest whole point and never zero — "nothing in this game
    /// has a zero". Proportional rather than flat, so a 9-damage dagger dummy
    /// and an 18-damage axe dummy grow at the same <em>relative</em> rate
    /// instead of a flat +5 landing 56% on one and 28% on the other (settled.md,
    /// which overrides §3.2's flat step); and it compounds, because each step
    /// reads the value the last one produced rather than the one it started from.
    /// </summary>
    /// <param name="current">
    /// The dummy's whole current number — its weapon damage plus its accumulated
    /// bonus, or its current maximum HP — never the bonus alone, which would
    /// floor at 1 forever and never compound, the one shape §3.2 rules out.
    /// </param>
    public static int Step(int current)
        => Math.Max(1, (current * GameContent.Current.Tuning.ReviveStepPercent + Percent / 2) / Percent);

    /// <summary>
    /// The odds a drop at <paramref name="defeatCount"/> is carrying a unique,
    /// as an integer 0..1000:
    /// <c>round(1000 × Ceiling / (1 + e^(−K × (n − Midpoint))))</c> (§3.2). A
    /// logistic rather than a flat per-cycle rate, so the curve starts
    /// negligible, gets genuinely hot across the cycles revival scaling has made
    /// dangerous, and flattens toward a ceiling it never reaches: no amount of
    /// grinding guarantees a unique.
    /// <para>
    /// <strong>Permille, and never a double, because <see cref="Math.Exp"/> is
    /// platform-dependent.</strong> IEEE-754 conformance covers + − × ÷ and
    /// sqrt, not the transcendentals: .NET's <c>Math.Exp</c> calls into the
    /// platform C runtime and is documented as possibly differing between
    /// operating systems and architectures. Rolling <c>NextDouble() &lt;
    /// chance</c> would be an exact comparison against a value whose last place
    /// can move, so a dummy saved at <c>DefeatCount</c> 20 could drop a unique
    /// on one machine and not on another — precisely the save/reload divergence
    /// the rest of this phase is built to rule out. Quantising first means a
    /// last-place difference can only change an outcome by moving the
    /// <em>rounded</em> permille, which takes a relative error near 1e-3 rather
    /// than 1e-16. The roll is <c>NextInt(0, 999) &lt; permille</c>, for the same
    /// reason <see cref="RollsAStack"/> draws an integer.
    /// </para>
    /// </summary>
    public static int UniqueChancePermille(int defeatCount)
    {
        var tuning = GameContent.Current.Tuning;
        double k = tuning.UniqueChanceKPercent / (double)Percent;
        return UniqueChancePermilleOf(Math.Exp(-k * (defeatCount - tuning.UniqueChanceMidpoint)));
    }

    /// <summary>
    /// The quantising half of <see cref="UniqueChancePermille"/>, taking the
    /// logistic's already-computed <c>e^(−K × (n − Midpoint))</c> term. It is a
    /// member of its own so a test can perturb that one platform-dependent value
    /// by a last place and assert the permille does not move, which is the
    /// guarantee the quantisation exists to buy.
    /// </summary>
    internal static int UniqueChancePermilleOf(double exponentialTerm)
    {
        int ceiling = GameContent.Current.Tuning.UniqueChanceCeilingPercent * PermillePerPercent;
        // Add a half and truncate: the same integer rounding Step uses, and no
        // second transcendental between the curve and the comparison.
        return (int)(ceiling / (1.0 + exponentialTerm) + 0.5);
    }

    /// <summary>
    /// Whether one farm cycle buys a stack: <c>NextInt(0, 99) &lt;
    /// DefeatStackChance</c> (§3.2). Flat rather than decaying, which settled.md
    /// accepts as designed — what ends a farm is the per-modifier
    /// <c>forged + 5</c> ceiling, not a falling rate. An integer draw and never
    /// <c>NextDouble</c>, for the reason <see cref="UniqueChancePermille"/> states.
    /// </summary>
    public static bool RollsAStack(Mulberry32 rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return rng.NextInt(0, Percent - 1) < GameContent.Current.Tuning.DefeatStackChance;
    }

    /// <summary>
    /// The stream cycle <paramref name="cycle"/> of the dummy at
    /// <paramref name="spawnIndex"/> rolls its revival on:
    /// <c>mapSeed ^ ReviveSalt</c> in <see cref="EnemyPlacer"/>'s pattern, plus a
    /// per-enemy and a per-cycle term. Seeding per cycle instead of running one
    /// long stream is what lets a save store accumulated values rather than a
    /// stream position (§3.2, §5.1): a load re-derives any roll without
    /// remembering where a stream stood, and the order the party kills two
    /// dummies in cannot change either one's ladder.
    /// <para>
    /// <paramref name="mapSeed"/> is the floor's own generation seed — the value
    /// <c>MapGenerator.Generate</c> was given — and the mix carries no floor
    /// term. That is safe only while every floor has a distinct seed: if floors
    /// are ever derived from one campaign seed, two floors' spawn index 3 would
    /// share a ladder, so Phase 4 owes this phase a per-floor seed or a floor
    /// term in both mixes.
    /// </para>
    /// </summary>
    public static Mulberry32 ReviveStream(long mapSeed, int spawnIndex, int cycle)
        => new(SplitMix32(mapSeed ^ ReviveSalt ^ (spawnIndex * SpawnMix) ^ (cycle * CycleMix)));

    /// <summary>
    /// The splitmix32 finalizer this phase's salted streams run a composed seed
    /// through before handing back a <see cref="Mulberry32"/>. The composition is
    /// a thin linear function of an index, and mulberry32's <em>first</em> output
    /// is a comparatively weak function of its state
    /// (<c>_state += 0x6d2b79f5; t = (_state ^ (_state &gt;&gt; 15)) * (_state | 1)</c>)
    /// — and the first draw off each fresh stream is exactly the one this phase
    /// makes load-bearing, so neighbouring spawn indices and consecutive cycles
    /// would otherwise be the most correlated draws in the game.
    /// <see cref="EnemyPlacer"/> never needed this because it runs one long
    /// stream off one seed. <see cref="Mulberry32"/> itself is not touched: it is
    /// pinned bit-for-bit against the JS original by the golden tests.
    /// </summary>
    internal static long SplitMix32(long composed)
    {
        unchecked
        {
            uint x = (uint)composed;
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }
}
