namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Where a pool that grows in points stands (§2.1): the points earned, the bar
/// they made (<c>StartingPool + Points</c>), how far the raw XP has carried into
/// the next point, and what that point costs. Every field is <em>replayed</em>
/// from the cumulative XP on read and none of it is stored, so one integer is
/// the whole pool — which is what makes Phase 4's snapshot a copy and Phase 5's
/// save a number.
/// </summary>
public readonly record struct PoolProgress(int Points, int Max, int XpIntoNext, int XpToNext);

/// <summary>Where a weapon class's ladder stands: the level it wields at and how far into the next. Replayed from raw XP exactly as <see cref="PoolProgress"/> is.</summary>
public readonly record struct LevelProgress(int Level, int XpIntoNext, int XpToNext);

/// <summary>
/// The §2.1/§2.2 arithmetic: one threshold function, four applications.
/// <para>
/// <strong>There is no rate multiplier anywhere</strong> (§2.3). XP is credited
/// raw — the payload's own integer — and the governing stat divides the
/// <em>threshold</em>, so a credit can never introduce a fractional gain that
/// has to be rounded, and a fighter simply crosses the same bar four times as
/// often as a wizard.
/// </para>
/// <para>
/// <strong>The replay is the definition, not a closed form.</strong> The
/// threshold is the <em>current</em> bar, so a pool self-slows and the total to
/// reach a point or a level is the sum of the per-step costs, each floored by
/// integer division on its own step. At stat 3, reaching weapon level 4 costs
/// <c>33 + 66 + 100 = 199</c>, not the 200 a closed form gives — the loop is
/// what the game means.
/// </para>
/// Engine-free and deterministic: progression rolls nothing, so it introduces no
/// RNG stream and cannot touch map, pathing or golden parity.
/// </summary>
public static class Progression
{
    /// <summary>
    /// The level a class nobody has trained wields at; the cost of leaving level
    /// L is <c>XpToNext(WeaponXpPerLevel * L, stat)</c>. Levels start at 1 rather
    /// than 0 because at 0 the doc's formula yields a free step, and nothing in
    /// this game has a zero. Both level effects are no-ops here —
    /// <c>floor(1 / 2) = 0</c> damage, <c>floor(1 / 3) = 0</c> movement — so a
    /// fresh wielder swings for exactly the weapon's own numbers.
    /// </summary>
    public const int StartingLevel = 1;

    /// <summary>Levels per point of the proficiency damage bonus: <c>+floor(L / 2)</c> (§2.2).</summary>
    public const int DamagePerLevels = 2;

    /// <summary>Levels per point of movement discount: <c>-1</c> per 3 levels (§2.2).</summary>
    public const int MovementDiscountPerLevels = 3;

    /// <summary>The floor the discount stops at, "so a weapon never becomes free" (§2.2): nothing in this game has a zero.</summary>
    public const int MinimumMovementCost = 1;

    /// <summary>
    /// The one function every pool calls (§2.3), verbatim: a full bar's worth of
    /// XP divided by the stat that governs it, truncating — <c>25 / 4 = 6</c> is
    /// the doc's own worked value. No guard and no floor live in here, so the
    /// arithmetic reads exactly as the doc states it. The callers below carry
    /// both (<see cref="Pool"/>, <see cref="Ladder"/>), and
    /// <see cref="ContentValidator.ValidateTuning"/> refuses a bar below 1 at
    /// load, so a broken tuning is a startup failure rather than a freeze.
    /// </summary>
    /// <param name="currentMax">The bar the pool is on now: the current maximum, or <c>WeaponXpPerLevel * level</c> for a ladder.</param>
    /// <param name="stat">The governing stat, 1..4. A flat 1 for a thing with no nature.</param>
    public static int XpToNext(int currentMax, int stat) => currentMax / stat;

    /// <summary>
    /// Replay <paramref name="xp"/> against the HP or mana pool: the bar starts
    /// at <see cref="Tuning.StartingPool"/> and each point costs the bar it just
    /// made, so growth is fast while you are fragile and glacial once you are
    /// not. A CON 1 wizard and a CON 4 fighter open the game with the same
    /// maximum and diverge from there — the whole thesis of §2.1 as arithmetic.
    /// </summary>
    /// <param name="xp">Cumulative raw XP credited to this pool.</param>
    /// <param name="stat">CON for health, INT for mana; a flat 1 for wear (Phase 6a).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stat"/> is outside 1..4, or <paramref name="xp"/> is negative.</exception>
    public static PoolProgress Pool(int xp, int stat)
    {
        RequireStat(stat);
        RequireXp(xp);
        int max = RequireBar(GameContent.Current.Tuning.StartingPool, nameof(Tuning.StartingPool));

        int points = 0;
        int remaining = xp;
        int cost = Step(max, stat);
        while (remaining >= cost)
        {
            remaining -= cost;
            points++;
            max++;
            cost = Step(max, stat);
        }
        return new PoolProgress(points, max, remaining, cost);
    }

    /// <summary>
    /// Replay <paramref name="xp"/> against a weapon class's ladder. The bar is
    /// <c>WeaponXpPerLevel * level</c>, which makes the doc's
    /// <c>xpToNext(L) = 100 * L / governingStat</c> the very same
    /// <see cref="XpToNext"/> call every other pool makes — one mechanism, two
    /// formulas on the page.
    /// </summary>
    /// <param name="xp">Cumulative raw XP credited to this class, for this character.</param>
    /// <param name="stat">The class's governing stat (<see cref="GoverningStat"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stat"/> is outside 1..4, or <paramref name="xp"/> is negative.</exception>
    public static LevelProgress Ladder(int xp, int stat)
    {
        RequireStat(stat);
        RequireXp(xp);
        int perLevel = RequireBar(GameContent.Current.Tuning.WeaponXpPerLevel, nameof(Tuning.WeaponXpPerLevel));

        int level = StartingLevel;
        int remaining = xp;
        int cost = Step(perLevel * level, stat);
        while (remaining >= cost)
        {
            remaining -= cost;
            level++;
            cost = Step(perLevel * level, stat);
        }
        return new LevelProgress(level, remaining, cost);
    }

    /// <summary>The proficiency damage bonus at <paramref name="level"/>: <c>+floor(L / 2)</c> (§2.2).</summary>
    public static int DamageBonus(int level) => level / DamagePerLevels;

    /// <summary>
    /// What a level-<paramref name="level"/> wielder takes off a weapon's own
    /// cost: <c>floor(L / 3)</c> (§2.2), before the floor
    /// <see cref="MovementCost"/> applies at the use site. Stated on its own
    /// because the proficiency row names the discount rather than the cost, and
    /// the number a readout prints is never arithmetic a readout does.
    /// </summary>
    public static int MovementDiscount(int level) => level / MovementDiscountPerLevels;

    /// <summary>
    /// The movement a swing costs its wielder: the weapon's own resolved cost —
    /// Light's discount already in it — less <see cref="MovementDiscount"/>,
    /// never below <see cref="MinimumMovementCost"/> (§2.2). The discount is the
    /// wielder's, so it is taken at the use site and never cached on the weapon.
    /// </summary>
    public static int MovementCost(int resolvedCost, int level)
        => Math.Max(MinimumMovementCost, resolvedCost - MovementDiscount(level));

    /// <summary>
    /// The stats a weapon class levels on — the §1 class table. One stat each,
    /// except Sword and Shield, which takes the better of STR and DEX. CON
    /// governs no weapon: it is the body's pool, not a craft.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No row for <paramref name="cls"/>; the table is complete over the enum by construction.</exception>
    public static IReadOnlyList<Stat> Governing(WeaponClass cls)
        => GoverningTable.TryGetValue(cls, out var row) ? row : throw new KeyNotFoundException($"No governing stat row for {cls}.");

    /// <summary>
    /// The single int <see cref="XpToNext"/> divides by for <paramref name="cls"/>
    /// in <paramref name="stats"/>' hands: the row's one stat, or the best of the
    /// row where it names more than one (§2.2, Sword and Shield's
    /// <c>max(STR, DEX)</c>, which <see cref="InnateStats.Best"/> names). The
    /// caller computes the max, so the threshold function never learns what a
    /// class is.
    /// </summary>
    public static int GoverningStat(WeaponClass cls, InnateStats stats)
    {
        var row = Governing(cls);
        int best = stats.Of(row[0]);
        for (int i = 1; i < row.Count; i++)
            best = Math.Max(best, stats.Of(row[i]));
        return best;
    }

    /// <summary>The §1 class table, complete over <see cref="WeaponClass"/> or it throws at static init.</summary>
    private static readonly IReadOnlyDictionary<WeaponClass, IReadOnlyList<Stat>> GoverningTable = BuildGoverning(
    [
        (WeaponClass.Dagger, [Stat.DEX]),
        (WeaponClass.Sword, [Stat.STR, Stat.DEX]),   // a shield bash is whichever of the two you have
        (WeaponClass.Spear, [Stat.STR]),
        (WeaponClass.Axe, [Stat.STR]),
        (WeaponClass.Ranged, [Stat.DEX]),
        (WeaponClass.Throwing, [Stat.STR]),
        (WeaponClass.Staff, [Stat.INT]),
        (WeaponClass.Wand, [Stat.INT]),
    ]);

    /// <summary>
    /// The cost of the next step, floored at 1: a bar small enough that integer
    /// division yields 0 would be a free step, and the replay loop is the only
    /// caller that could hang on one. The floor lives here and never inside
    /// <see cref="XpToNext"/>, which stays the doc's line.
    /// </summary>
    private static int Step(int bar, int stat) => Math.Max(1, XpToNext(bar, stat));

    private static void RequireStat(int stat)
    {
        if (stat < InnateStats.Low || stat > InnateStats.High)
            throw new ArgumentOutOfRangeException(nameof(stat), stat,
                $"A governing stat runs {InnateStats.Low}..{InnateStats.High}; a thing with no nature divides by {InnateStats.Low}.");
    }

    private static void RequireXp(int xp)
    {
        if (xp < 0)
            throw new ArgumentOutOfRangeException(nameof(xp), xp, "Cumulative XP is never negative: a pool is only ever credited.");
    }

    private static int RequireBar(int bar, string key)
    {
        if (bar < 1)
            throw new ArgumentOutOfRangeException(key, bar, $"{key} is the bar a pool divides; below 1 there is no step to take.");
        return bar;
    }

    private static IReadOnlyDictionary<WeaponClass, IReadOnlyList<Stat>> BuildGoverning((WeaponClass Class, Stat[] Stats)[] rows)
    {
        var table = rows.ToDictionary(r => r.Class, r => (IReadOnlyList<Stat>)r.Stats);
        foreach (var cls in Enum.GetValues<WeaponClass>())
            if (!table.ContainsKey(cls))
                throw new InvalidOperationException($"The governing-stat table has no row for {cls}.");
        return table;
    }
}
