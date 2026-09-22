using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The §2.1/§2.2 arithmetic: one threshold function, the two replays built on
/// it, the class table, and the two level effects. Everything here is derived
/// from a cumulative integer — nothing is stored, so every number in a
/// <see cref="PoolProgress"/> or a <see cref="LevelProgress"/> is a claim about
/// the replay rather than about state.
/// </summary>
public class ProgressionTests
{
    /// <summary>The compiled bar both pools open on; the assertions below are written against it.</summary>
    private static int StartingPool => GameContent.Current.Tuning.StartingPool;

    [Theory]
    [InlineData(30, 4, 7)]     // the doc's worked value: 30 / 4 = 7.5 -> 7
    [InlineData(30, 1, 30)]    // a whole bar for a point, which is what a flat 1 means
    [InlineData(30, 2, 15)]
    [InlineData(30, 3, 10)]
    [InlineData(100, 3, 33)]   // the weapon ladder's first step at stat 3
    [InlineData(100, 4, 25)]
    public void XpToNext_IsTheBarOverTheStat(int currentMax, int stat, int expected)
        => Assert.Equal(expected, Progression.XpToNext(currentMax, stat));

    [Fact]
    public void Pool_SelfSlows_EachPointCostingTheNewBar()
    {
        // CON 4 from 30: 7, 7, then 8, because the threshold is the *current*
        // bar and the bar just moved. Growth is fast while you are fragile and
        // glacial once you are not; no pool runs away.
        Assert.Equal(30, StartingPool);

        Assert.Equal(new PoolProgress(Points: 0, Max: 30, XpIntoNext: 0, XpToNext: 7), Progression.Pool(0, 4));
        Assert.Equal(new PoolProgress(0, 30, 5, 7), Progression.Pool(5, 4));
        Assert.Equal(new PoolProgress(1, 31, 0, 7), Progression.Pool(7, 4));
        Assert.Equal(new PoolProgress(2, 32, 0, 8), Progression.Pool(14, 4));   // 32 / 4 = 8: the third point costs more than the first two
        Assert.Equal(new PoolProgress(3, 33, 0, 8), Progression.Pool(22, 4));
        Assert.Equal(new PoolProgress(4, 34, 0, 8), Progression.Pool(30, 4));
        Assert.Equal(new PoolProgress(4, 34, 6, 8), Progression.Pool(36, 4));
    }

    [Fact]
    public void Pool_AtStatOne_CostsAWholeBarAPoint()
    {
        // 30, then 31: a thing with no nature needs the whole pool cashed for
        // every step, which is why wear climbs an authored ladder rather than a curve.
        Assert.Equal(new PoolProgress(0, 30, 29, 30), Progression.Pool(29, 1));
        Assert.Equal(new PoolProgress(1, 31, 0, 31), Progression.Pool(30, 1));
        Assert.Equal(new PoolProgress(2, 32, 0, 32), Progression.Pool(61, 1));
    }

    [Fact]
    public void Pool_IsAPureFunctionOfXp()
    {
        // The pool is its raw XP and nothing else: replaying the same number
        // twice gives the same struct, and feeding it in one lump agrees with
        // feeding it a credit at a time. Both halves are asserted against a
        // hand-rolled replay rather than against Progression.Pool itself, since
        // comparing the function with the function holds for any implementation,
        // broken or not. The model spells the rule out: a point costs
        // XpToNext(currentBar, stat) -- pinned by literals in
        // XpToNext_IsTheBarOverTheStat -- the bar moves as the point lands, and
        // whatever is left carries into the next.
        static PoolProgress Consume(PoolProgress from, int credit, int stat)
        {
            int points = from.Points, max = from.Max, carried = from.XpIntoNext + credit;
            int cost = Math.Max(1, Progression.XpToNext(max, stat));   // floored, because a free step is forbidden
            while (carried >= cost)
            {
                carried -= cost;
                points++;
                max++;
                cost = Math.Max(1, Progression.XpToNext(max, stat));
            }
            return new PoolProgress(points, max, carried, cost);
        }

        static PoolProgress Fresh(int stat) => Consume(new PoolProgress(0, StartingPool, 0, 0), 0, stat);

        // One lump: every total to 200, replayed from nothing. Read ascending and
        // then descending, so a cache a rising sequence had warmed would show up
        // as a disagreement rather than as agreement with itself.
        for (int xp = 0; xp <= 200; xp++)
            Assert.Equal(Consume(Fresh(3), xp, 3), Progression.Pool(xp, 3));
        for (int xp = 200; xp >= 0; xp--)
            Assert.Equal(Consume(Fresh(3), xp, 3), Progression.Pool(xp, 3));

        // In pieces: each credit consumed against the bar it found, carrying the
        // remainder, lands exactly where the same total replayed from nothing does.
        int running = 0;
        var pieces = Fresh(4);
        foreach (int credit in new[] { 3, 1, 7, 4, 12, 9, 40, 6 })
        {
            running += credit;
            pieces = Consume(pieces, credit, 4);
            Assert.Equal(pieces, Progression.Pool(running, 4));
        }

        // The model is itself pinned by a literal, so neither side can drift.
        Assert.Equal(82, running);
        Assert.Equal(new PoolProgress(Points: 10, Max: 40, XpIntoNext: 0, XpToNext: 10), Progression.Pool(running, 4));
    }

    [Fact]
    public void CreditIsRaw_OnlyTheThresholdKnowsTheStat()
    {
        // The same hundred points of XP against CON 4 and CON 1. Nothing
        // multiplied the credit: both pools were fed the identical integer and
        // accounted for all of it — consumed plus carried equals what went in.
        // The stat divided the *threshold*, and that is the whole gap. The
        // ratio of points opens at fourfold and eases as the faster pool's bar
        // outgrows the slower one's: eleven points against three, here.
        const int xp = 100;

        var fast = Progression.Pool(xp, 4);
        var slow = Progression.Pool(xp, 1);

        Assert.Equal(new PoolProgress(11, 41, 8, 10), fast);
        Assert.Equal(new PoolProgress(3, 33, 7, 33), slow);

        Assert.Equal(92, xp - fast.XpIntoNext);   // spent on points
        Assert.Equal(93, xp - slow.XpIntoNext);
        Assert.True(fast.Points >= slow.Points * 3, $"{fast.Points} is not three times {slow.Points}");

        // Max is StartingPool plus points, and nothing else: stats touch no maximum.
        Assert.Equal(StartingPool + fast.Points, fast.Max);
        Assert.Equal(StartingPool + slow.Points, slow.Max);
    }

    [Fact]
    public void Ladder_IsTriangular_AndStartsAtLevelOne()
    {
        // A class nobody has trained wields at level 1, and xpToNext(L) is the
        // cost of leaving L: at 0 the doc's formula gives a free step, and
        // nothing in this game has a zero.
        Assert.Equal(new LevelProgress(Level: 1, XpIntoNext: 0, XpToNext: 25), Progression.Ladder(0, 4));
        Assert.Equal(new LevelProgress(1, 24, 25), Progression.Ladder(24, 4));

        // DEX 4: 25, then 50, then 75 -- 150 to level 4.
        Assert.Equal(new LevelProgress(2, 0, 50), Progression.Ladder(25, 4));
        Assert.Equal(new LevelProgress(3, 0, 75), Progression.Ladder(75, 4));
        Assert.Equal(new LevelProgress(4, 0, 100), Progression.Ladder(150, 4));

        // Stat 1: 100, 200, 300 -- 600 to level 4.
        Assert.Equal(new LevelProgress(2, 0, 200), Progression.Ladder(100, 1));
        Assert.Equal(new LevelProgress(3, 0, 300), Progression.Ladder(300, 1));
        Assert.Equal(new LevelProgress(4, 0, 400), Progression.Ladder(600, 1));

        // Stat 3 is the case that makes the replay the definition: 33 + 66 + 100
        // = 199 to level 4, not the 200 the closed form 100 * n(n-1) / 2 / stat
        // gives, because each step floors on its own.
        Assert.Equal(new LevelProgress(2, 0, 66), Progression.Ladder(33, 3));
        Assert.Equal(new LevelProgress(3, 0, 100), Progression.Ladder(99, 3));
        Assert.Equal(new LevelProgress(4, 0, 133), Progression.Ladder(199, 3));
        Assert.Equal(new LevelProgress(4, 1, 133), Progression.Ladder(200, 3));   // the closed form's 200 is one XP past the step
        Assert.Equal(3, Progression.Ladder(198, 3).Level);                        // and one short of it is still level 3
    }

    [Fact]
    public void GoverningStat_IsTheClassTable_SwordTakesTheBetterOfStrAndDex()
    {
        Assert.Equal(new[] { Stat.DEX }, Progression.Governing(WeaponClass.Dagger));
        Assert.Equal(new[] { Stat.STR, Stat.DEX }, Progression.Governing(WeaponClass.Sword));
        Assert.Equal(new[] { Stat.STR }, Progression.Governing(WeaponClass.Spear));
        Assert.Equal(new[] { Stat.STR }, Progression.Governing(WeaponClass.Axe));
        Assert.Equal(new[] { Stat.DEX }, Progression.Governing(WeaponClass.Ranged));
        Assert.Equal(new[] { Stat.STR }, Progression.Governing(WeaponClass.Throwing));
        Assert.Equal(new[] { Stat.INT }, Progression.Governing(WeaponClass.Staff));
        Assert.Equal(new[] { Stat.INT }, Progression.Governing(WeaponClass.Wand));

        // Complete over the enum, and CON governs no weapon: it is the body's
        // pool, not a craft.
        foreach (var cls in Enum.GetValues<WeaponClass>())
            Assert.NotEmpty(Progression.Governing(cls));
        Assert.DoesNotContain(Stat.CON, Enum.GetValues<WeaponClass>().SelectMany(cls => (IEnumerable<Stat>)Progression.Governing(cls)));

        // A, the thief: STR 1 / DEX 4 / CON 2 / INT 3.
        var a = new InnateStats(1, 4, 2, 3);
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Dagger, a));
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Sword, a));    // max(1, 4)
        Assert.Equal(1, Progression.GoverningStat(WeaponClass.Spear, a));
        Assert.Equal(1, Progression.GoverningStat(WeaponClass.Axe, a));
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Ranged, a));
        Assert.Equal(1, Progression.GoverningStat(WeaponClass.Throwing, a));
        Assert.Equal(3, Progression.GoverningStat(WeaponClass.Staff, a));
        Assert.Equal(3, Progression.GoverningStat(WeaponClass.Wand, a));

        // C, the arm: STR 4 / DEX 2 -- the sword takes the other side of the max.
        var c = new InnateStats(4, 2, 3, 1);
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Sword, c));
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Axe, c));
        Assert.Equal(1, Progression.GoverningStat(WeaponClass.Wand, c));     // C will never be the better caster

        // A thing with no nature divides by 1 whatever it is holding.
        foreach (var cls in Enum.GetValues<WeaponClass>())
            Assert.Equal(InnateStats.Low, Progression.GoverningStat(cls, InnateStats.None));
    }

    [Fact]
    public void LevelEffects_AreFlooredAndNeverFree()
    {
        // +floor(L / 2) damage.
        Assert.Equal(0, Progression.DamageBonus(Progression.StartingLevel));
        Assert.Equal(0, Progression.DamageBonus(1));
        Assert.Equal(1, Progression.DamageBonus(2));
        Assert.Equal(1, Progression.DamageBonus(3));
        Assert.Equal(3, Progression.DamageBonus(7));

        // -1 movement per 3 levels, off the weapon's own resolved cost.
        Assert.Equal(30, Progression.MovementCost(30, 1));
        Assert.Equal(30, Progression.MovementCost(30, 2));
        Assert.Equal(29, Progression.MovementCost(30, 3));
        Assert.Equal(27, Progression.MovementCost(30, 9));

        // Floored so a weapon never becomes free, however long it has been swung.
        Assert.Equal(Progression.MinimumMovementCost, Progression.MovementCost(1, 99));
        Assert.Equal(1, Progression.MovementCost(1, 99));

        // Level 1 changes nothing at all, which is what lets the ladder ship
        // before the effects bite.
        foreach (int cost in new[] { 15, 30, 40, 45, 55, 60 })
            Assert.Equal(cost, Progression.MovementCost(cost, Progression.StartingLevel));
    }

    [Fact]
    public void XpToNext_TakesAWeaponsFlatOne()
    {
        // The Phase 6a case: a weapon has no stats, so its wear capacity divides
        // by a flat 1 and needs a whole bar for every step. Same function.
        Assert.Equal(30, Progression.XpToNext(30, 1));
        Assert.Equal(new PoolProgress(1, 31, 0, 31), Progression.Pool(30, 1));
    }

    [Fact]
    public void AReplayRefusesAStatOutsideTheRange_AndNegativeXp()
    {
        // The guards live on the callers, never inside XpToNext, so the doc's
        // line stays the doc's line and a broken call is named rather than
        // silently dividing by zero or looping forever.
        Assert.Throws<ArgumentOutOfRangeException>(() => Progression.Pool(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Progression.Pool(10, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Progression.Pool(-1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => Progression.Ladder(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Progression.Ladder(10, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Progression.Ladder(-1, 4));
    }
}
