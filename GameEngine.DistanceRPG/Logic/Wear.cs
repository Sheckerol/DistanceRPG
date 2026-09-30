namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The §6.1, §6.2 and §6.4 arithmetic of wear, under the user's ruling of
/// 2026-09-25 on what a deposit costs (R4, settled.md): what a hit banks, what a
/// weapon's wear capacity is, where its enchantment slots open, what attaching each
/// enchantment costs, how many runs a service takes (Phase 6a.3a), and the odds a
/// service improves the weapon anyway (Phase 6a.4). Engine-free and deterministic —
/// it rolls nothing: the enchanter rolls on its odds — and it reads
/// the current tuning the way <see cref="Progression"/> does, so every number is
/// a <c>tuning.json</c> key and none is a copy of one.
/// <para>
/// <strong>Wear is a resource, not damage</strong> (S:355). A weapon banks it by
/// being used, up to its capacity, and a full weapon plays exactly as a fresh one
/// does: nothing in combat reads it. The enchanter spends it, the whole pool at a
/// deposit, and the capacity grows on what was cashed.
/// </para>
/// <para>
/// <strong>Capacity is replayed, never stored</strong> (PHASE 6a DECISION 2): a
/// weapon keeps the wear it has ever cashed (<see cref="Weapon.WearCashed"/>), and
/// its capacity and the progress toward the next point are worked out from that
/// on read, as a member's maxima are from its XP. So one integer is the whole
/// history, and a <c>StartingPool</c> or <c>WearCapacityPerBar</c> edit moves
/// every weapon's capacity at once — which the save's hash says
/// (<see cref="TuningKey.HashedFrom"/>).
/// </para>
/// </summary>
public static class Wear
{
    /// <summary>
    /// A weapon's wear capacity, replayed from <paramref name="cashed"/>, the
    /// wear it has ever cashed at the enchanter (R4, S:1659–1665): the capacity
    /// opens on <see cref="Tuning.StartingPool"/>, and every full bar's worth of
    /// wear cashed adds <see cref="Tuning.WearCapacityPerBar"/> to it — the bar
    /// being the capacity at the time — with the progress carrying from one
    /// deposit to the next and no top. <c>Max</c> is the capacity,
    /// <c>XpIntoNext</c> the progress toward the next point and <c>XpToNext</c>
    /// the bar, <c>Points</c> the full bars cashed.
    /// <para>
    /// <c>WearCapacityPerBar</c> 0 freezes capacity at the start: "a dial rather
    /// than an error" (S:1757). That is worked out in closed form — the progress
    /// wraps a bar that never grows — so a frozen capacity never replays a billion
    /// bars of one wear. Otherwise the replay is <see cref="Progression"/>'s, at a
    /// flat 1: a bar of at least 1 that grows by at least 1 a point is crossed at
    /// most about 46 000 times by any count a save holds
    /// (<see cref="ContentBounds.MostCount"/>).
    /// </para>
    /// </summary>
    /// <param name="cashed">The wear the weapon has ever cashed: 0 on every new weapon.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cashed"/> is negative.</exception>
    public static PoolProgress Capacity(int cashed)
    {
        if (cashed < 0)
            throw new ArgumentOutOfRangeException(nameof(cashed), cashed, "Wear cashed is never negative: a weapon only ever cashes wear in.");

        var tuning = GameContent.Current.Tuning;
        if (tuning.WearCapacityPerBar > 0)
            return Progression.Pool(cashed, InnateStats.Low, tuning.StartingPool, tuning.WearCapacityPerBar);

        if (tuning.StartingPool < 1)
            throw new ArgumentOutOfRangeException(nameof(Tuning.StartingPool), tuning.StartingPool, "StartingPool is the bar a capacity opens on; below 1 there is no bar to fill.");
        int bar = tuning.StartingPool;
        return new PoolProgress(cashed / bar, bar, cashed % bar, bar);
    }

    /// <summary>
    /// The capacity enchantment slot <paramref name="n"/> opens at (R4,
    /// S:1666–1667): <c>FirstSlotCapacity + SlotCapacityStep × (n − 1)</c> — 30,
    /// 35, 40, 45 and 50. What a weapon arrives with fills its slots from the first
    /// ("a free slot is read literally", S:1713–1720), so the slot a weapon's next
    /// enchantment needs is its count plus one. In a <c>long</c>: no tuning in
    /// range wraps it.
    /// </summary>
    /// <param name="n">The slot, from 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is below 1.</exception>
    public static long SlotOpensAt(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        var tuning = GameContent.Current.Tuning;
        return tuning.FirstSlotCapacity + (long)tuning.SlotCapacityStep * (n - 1);
    }

    /// <summary>
    /// How many enchantment slots are open at <paramref name="capacity"/>: the
    /// largest n no more than <see cref="Tuning.EnchantmentSlots"/> whose slot has
    /// opened (<see cref="SlotOpensAt"/>), or 0 below the first.
    /// </summary>
    public static int SlotsOpen(int capacity)
    {
        int slots = GameContent.Current.Tuning.EnchantmentSlots;
        int open = 0;
        while (open < slots && SlotOpensAt(open + 1) <= capacity)
            open++;
        return open;
    }

    /// <summary>
    /// The least wear a deposit must cash to attach enchantment <paramref name="n"/>
    /// (R4, S:1668–1671): <c>FirstEnchantmentWear + EnchantmentWearStep × (n − 1)</c>
    /// — 30, 35, 40, 45 and 50. A deposit spends the whole pool whatever the
    /// price, and capacity above it is breathing room. It depends on n alone, so
    /// refining never raises it. In a <c>long</c>, as the slots are.
    /// </summary>
    /// <param name="n">The enchantment being attached, from 1: the weapon's count plus one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is below 1.</exception>
    public static long Price(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        var tuning = GameContent.Current.Tuning;
        return tuning.FirstEnchantmentWear + (long)tuning.EnchantmentWearStep * (n - 1);
    }

    /// <summary>
    /// The runs a service at the enchanter takes (§6.2, S:357–358): the weapon's
    /// enchantment count, what it arrived with included, plus one — for every
    /// deposit, a refinement too. So the tutorial's one run lands on a first
    /// enchantment's one run, and the cumulative 1, 3, 6, 10, 15 of attaching five
    /// in a row. The <c>+ 1</c> is a rule, not a key (PHASE 6a DECISION 6).
    /// </summary>
    /// <param name="count">The weapon's enchantments as it is deposited.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static int ServiceRuns(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return count + 1;
    }

    /// <summary>Percentages are integers out of this.</summary>
    private const int Percent = 100;

    /// <summary>
    /// The odds a service improves the weapon anyway (§6.4, S:1149–1154; R4's
    /// consequence 2), as a whole-number fraction — <c>Numerator</c> in
    /// <c>Denominator</c>. Enchanting's are <see cref="Tuning.GraftBaseChance"/> in
    /// 100, flat, whatever the wear; refining's are those times <c>1 + wear ÷
    /// StartingPool</c>, on the whole deposit: 6 in 100 cashing a fresh full bar of
    /// 30, 7 cashing 40 at capacity 40, 9 cashing 60 at capacity 60, and no top —
    /// "refinement improves whether the roll lands, not what it lands on"
    /// (S:782–784). One expression with one branch (P6:873–875), in integers:
    /// <c>GraftBaseChance × (StartingPool + wear)</c> out of <c>100 × StartingPool</c>,
    /// the wear counted for a refinement alone, so nothing is rounded, and a
    /// numerator at or past its denominator lands every time. In <c>long</c>s: no
    /// tuning in range and no wear a save holds wraps them.
    /// <para>
    /// These are the odds, never the roll: a deposit that cashed nothing rolls
    /// nothing whatever its odds say — "no wear, no roll" (P6:501–503) — which is
    /// the roll's own guard (<see cref="Enchanter"/>).
    /// </para>
    /// </summary>
    /// <param name="service">What the deposit bought.</param>
    /// <param name="wearAtDeposit">The wear the deposit cashed: the weapon's whole pool as it went in.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="wearAtDeposit"/> is negative.</exception>
    public static (long Numerator, long Denominator) ImproveOdds(Service service, int wearAtDeposit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wearAtDeposit);
        var tuning = GameContent.Current.Tuning;
        long pool = tuning.StartingPool;
        return ((long)tuning.GraftBaseChance * (pool + (service == Service.Refine ? wearAtDeposit : 0)), Percent * pool);
    }

    /// <summary>
    /// One hit landed with <paramref name="weapon"/> (§6.1): it banks
    /// <see cref="Tuning.WearPerHit"/>, up to its capacity — "per hit rather than
    /// per swing, per turn or scaled by damage" (S:785–788) — and true on the hit
    /// that fills it, which the turn system raises as
    /// <see cref="TurnSystem.WeaponWornFull"/>. A weapon already full banks nothing
    /// and says nothing again. The one door wear accrues through: the turn
    /// system's wear feed for a party member calls it, once for each body a hit
    /// lands on and for each landed cast of a staff (PHASE 6a DECISION 1), and
    /// nothing else does (<c>WearTests.WearAccruesInOnePlace</c>).
    /// </summary>
    /// <returns>True when this hit took the weapon from short of its capacity to full.</returns>
    internal static bool Accrue(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        if (weapon.WornFull)
            return false;
        weapon.AccrueWear(GameContent.Current.Tuning.WearPerHit);
        return weapon.WornFull;
    }
}
