using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The §2.1 spread: the permutation rule, the neutral spread that is
/// deliberately not one, and the accessors the HUD and a save read in a fixed
/// order. Pure arithmetic over a value type; nothing here touches content.
/// </summary>
public class InnateStatsTests
{
    /// <summary>
    /// The twenty-four arrangements of 1, 2, 3 and 4 — every legal spread there
    /// is — followed by the four shapes the rule exists to refuse: a duplicate,
    /// a value below the range, a value above it, and the flat spread that is
    /// both.
    /// </summary>
    public static TheoryData<int, int, int, int, bool> Spreads
    {
        get
        {
            var data = new TheoryData<int, int, int, int, bool>();
            for (int str = InnateStats.Low; str <= InnateStats.High; str++)
                for (int dex = InnateStats.Low; dex <= InnateStats.High; dex++)
                    for (int con = InnateStats.Low; con <= InnateStats.High; con++)
                        for (int @int = InnateStats.Low; @int <= InnateStats.High; @int++)
                            if (str != dex && str != con && str != @int && dex != con && dex != @int && con != @int)
                                data.Add(str, dex, con, @int, true);

            data.Add(4, 4, 1, 1, false);   // duplicates: two 4s and two 1s still sum to 10
            data.Add(0, 2, 3, 4, false);   // below the range
            data.Add(1, 2, 3, 5, false);   // above the range
            data.Add(1, 1, 1, 1, false);   // InnateStats.None: the neutral spread, never a member's
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Spreads))]
    public void Permutation_AcceptsAll24_RejectsDuplicatesOutOfRangeAndWrongSum(int str, int dex, int con, int @int, bool permutation)
    {
        var stats = new InnateStats(str, dex, con, @int);

        Assert.Equal(permutation, stats.IsPermutation);
        if (permutation)
            Assert.Equal(InnateStats.Sum, stats.Total);   // "every spread sums to 10" follows from the rule; it is not a second one
    }

    [Fact]
    public void Permutation_CountsTwentyFour()
    {
        // The generator above is the fixture the theory runs on; if it ever
        // stopped producing every arrangement the theory would quietly shrink.
        Assert.Equal(24, Spreads.Count(row => (bool)row[4]!));
        Assert.Equal(4, Spreads.Count(row => !(bool)row[4]!));
    }

    [Fact]
    public void None_IsEveryStatAtOne_AndIsNotAPermutation()
    {
        // A thing with no nature: every bar costs itself, which is the divisor a
        // weapon's wear uses. It is the spread an actor carries when no roster
        // entry gave it one, so it must never be validated as a member's.
        Assert.Equal(new InnateStats(1, 1, 1, 1), InnateStats.None);
        Assert.False(InnateStats.None.IsPermutation);
        Assert.Equal(4, InnateStats.None.Total);
        Assert.NotEqual(InnateStats.Sum, InnateStats.None.Total);

        foreach (var (stat, _) in InnateStats.None.Entries)
            Assert.Equal(InnateStats.Low, InnateStats.None.Of(stat));

        Assert.Equal(1, InnateStats.Low);
        Assert.Equal(4, InnateStats.High);
        Assert.Equal(10, InnateStats.Sum);
    }

    [Fact]
    public void Entries_AreInStatOrder()
    {
        // Enum order, always: the HUD prints these and a save writes them, so a
        // row and a serialised spread can never come out in different orders.
        var a = new InnateStats(STR: 1, DEX: 4, CON: 2, INT: 3);

        Assert.Equal(new[] { Stat.STR, Stat.DEX, Stat.CON, Stat.INT }, a.Entries.Select(e => e.Stat));
        Assert.Equal(new[] { 1, 4, 2, 3 }, a.Entries.Select(e => e.Value));
        Assert.Equal(new[] { Stat.STR, Stat.DEX, Stat.CON, Stat.INT }, Enum.GetValues<Stat>());

        // Of is the one accessor, and it agrees with the enumeration entry by entry.
        foreach (var (stat, value) in a.Entries)
            Assert.Equal(value, a.Of(stat));

        // Best is the named two-stat case the class table needs: Sword & Shield's max(STR, DEX).
        Assert.Equal(4, a.Best(Stat.STR, Stat.DEX));
        Assert.Equal(4, a.Best(Stat.DEX, Stat.STR));
        Assert.Equal(3, a.Best(Stat.CON, Stat.INT));
        Assert.Equal(1, a.Best(Stat.STR, Stat.STR));

        Assert.Throws<ArgumentOutOfRangeException>(() => a.Of((Stat)99));
    }

    [Fact]
    public void ASpreadIsAValue_NotAnIdentity()
    {
        // Equatable for free, which is what lets a test compare spreads and a
        // save round-trip one: two members arranged the same way carry the same
        // spread, and neither can write it.
        Assert.Equal(new InnateStats(2, 3, 1, 4), new InnateStats(2, 3, 1, 4));
        Assert.NotEqual(new InnateStats(2, 3, 1, 4), new InnateStats(2, 3, 4, 1));
        Assert.Equal("STR 2 / DEX 3 / CON 1 / INT 4", new InnateStats(2, 3, 1, 4).ToString());
    }
}
