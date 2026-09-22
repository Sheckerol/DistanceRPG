using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The §3.2 farm's arithmetic, wired to nothing: the two ladders
/// <c>DefeatCount</c> drives, the logistic the deep farm gambles on, and the
/// revival stream. Everything here is a pure function of saved state and the
/// §5.3 knobs, so every assertion is a claim about the curve rather than about
/// anything a dummy did. In the content collection because three of them swap
/// tuning through <see cref="TestContent.Use"/>.
/// </summary>
[Collection(TestContent.Collection)]
public class FarmLadderTests
{
    /// <summary>The prototype's own map seed; it overflows int32, which is the interesting case for a stream mix.</summary>
    private const long Seed = 2762136374;

    /// <summary>
    /// <see cref="EnemyPlacer"/>'s splitter, which is private there. Restated so
    /// this file can assert the revival stream is not the placer's.
    /// </summary>
    private const long PlacerSalt = 0x9E3779B9;

    /// <summary>
    /// The drop's splitter. S3 declares it on <c>LootTable</c> when the drop
    /// lands; until then the value is stated here, because the claim under test
    /// is that the two salts of §3.2 are distinct.
    /// </summary>
    private const long LootSalt = 0x5BF03635;

    private static Tuning Tuning => GameContent.Current.Tuning;

    [Theory]
    [InlineData(5, 10)]      // 1%
    [InlineData(10, 35)]     // 3.5%
    [InlineData(12, 56)]     // 5.6%
    [InlineData(15, 110)]    // 11%
    [InlineData(18, 190)]    // 19%
    [InlineData(20, 250)]    // 25%, the midpoint: half the ceiling exactly
    [InlineData(25, 390)]    // 39%
    [InlineData(30, 470)]    // 47%
    [InlineData(40, 500)]    // 50%, the ceiling to the doc's precision
    public void UniqueChance_MatchesTheDocsTable(int defeatCount, int docPermille)
    {
        // The doc prints the table to one decimal place of a percent, so the
        // claim is +/- half a point: five permille, which is exactly what the
        // 47% row costs. Asserted against the permille figure and never against
        // a double, because the permille is what the roll actually compares.
        int permille = FarmLadder.UniqueChancePermille(defeatCount);
        Assert.True(Math.Abs(permille - docPermille) <= 5,
            $"DefeatCount {defeatCount}: {permille} permille against the doc's {docPermille}.");
    }

    [Fact]
    public void UniqueChance_NeverExceedsTheCeiling_AndNeverReachesCertainty()
    {
        // "No certainty, ever" is a claim about certainty, not about strictly
        // undershooting the asymptote: 0.50 / (1 + e^(-0.26 x 980)) is exactly
        // 0.50 in IEEE-754 doubles, and the doc's own table prints 50% at n=40.
        // So the ceiling is reached and never passed, and 1000 never is.
        int ceiling = Tuning.UniqueChanceCeilingPercent * 10;
        int previous = -1;

        for (int n = 0; n <= 1000; n++)
        {
            int permille = FarmLadder.UniqueChancePermille(n);
            Assert.InRange(permille, 0, ceiling);
            Assert.True(permille < 1000, $"DefeatCount {n} reached certainty at {permille} permille.");
            Assert.True(permille >= previous, $"DefeatCount {n} fell back to {permille} from {previous}.");
            previous = permille;
        }

        Assert.Equal(ceiling, previous);   // it does arrive, which is why the claim is about certainty
    }

    [Fact]
    public void UniqueChance_IsStableUnderALastPlacePerturbation()
    {
        // The guarantee the permille quantisation buys: Math.Exp is not covered
        // by IEEE-754 conformance and may differ in its last place between
        // platforms, so a raw `NextDouble() < chance` could hand two machines
        // different outcomes from the same save. Moving the exponential by an
        // ulp must not move the permille.
        double k = Tuning.UniqueChanceKPercent / 100.0;

        for (int n = 0; n <= 200; n++)
        {
            double exponential = Math.Exp(-k * (n - Tuning.UniqueChanceMidpoint));
            int permille = FarmLadder.UniqueChancePermille(n);

            Assert.Equal(permille, FarmLadder.UniqueChancePermilleOf(exponential));
            Assert.Equal(permille, FarmLadder.UniqueChancePermilleOf(Math.BitIncrement(exponential)));
            Assert.Equal(permille, FarmLadder.UniqueChancePermilleOf(Math.BitDecrement(exponential)));
        }
    }

    [Fact]
    public void UniqueChance_MovesWithItsThreeKnobs()
    {
        // All three are knobs: the ceiling sets how much grinding can ever be
        // worth, the midpoint moves the hot zone, and K controls how sharply it
        // arrives. Each is turned on its own, against the compiled curve.
        Assert.Equal(250, FarmLadder.UniqueChancePermille(20));
        Assert.Equal(393, FarmLadder.UniqueChancePermille(25));

        using (TestContent.Use(tuning: ContentDefaults.Tuning with { UniqueChanceCeilingPercent = 80 }))
        {
            Assert.Equal(400, FarmLadder.UniqueChancePermille(20));   // still half the ceiling at the midpoint
            Assert.Equal(800, FarmLadder.UniqueChancePermille(1000));
        }

        using (TestContent.Use(tuning: ContentDefaults.Tuning with { UniqueChanceMidpoint = 30 }))
        {
            Assert.Equal(250, FarmLadder.UniqueChancePermille(30));   // the hot zone moved, whole
            Assert.True(FarmLadder.UniqueChancePermille(20) < 250);
        }

        using (TestContent.Use(tuning: ContentDefaults.Tuning with { UniqueChanceKPercent = 52 }))
        {
            Assert.Equal(250, FarmLadder.UniqueChancePermille(20));   // the midpoint is where K does nothing
            Assert.True(FarmLadder.UniqueChancePermille(25) > 393);   // and everywhere else it steepens
            Assert.True(FarmLadder.UniqueChancePermille(15) < 107);
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(2, 8)]
    [InlineData(4, 6)]
    [InlineData(6, 4)]
    [InlineData(7, 3)]
    [InlineData(40, 3)]
    public void ResurrectTurns_ShortensThenFloorsAtThree(int defeatCount, int turns)
        => Assert.Equal(turns, FarmLadder.ResurrectTurns(defeatCount));

    [Fact]
    public void DefeatAdvance_IsTwoWhenTheBlowClearsMaxHp()
    {
        // The doc's worked dummy, 24 HP. The bar is the constitution and never
        // what is left of it: a 2-damage finisher into a dummy standing at 3 HP
        // still advances by one, because the comparison never learns how
        // softened the target was — which is what makes it uncheesable.
        Assert.Equal(1, FarmLadder.DefeatAdvance(dealt: 2, maxHp: 24));
        Assert.Equal(2, FarmLadder.DefeatAdvance(dealt: 40, maxHp: 24));
        Assert.Equal(2, FarmLadder.DefeatAdvance(dealt: 24, maxHp: 24));
        Assert.Equal(1, FarmLadder.DefeatAdvance(dealt: 23, maxHp: 24));

        // And the same against the dummy the game actually fields.
        Assert.Equal(1, FarmLadder.DefeatAdvance(dealt: GameConstants.DummyHp - 1, maxHp: GameConstants.DummyHp));
        Assert.Equal(2, FarmLadder.DefeatAdvance(dealt: GameConstants.DummyHp, maxHp: GameConstants.DummyHp));

        // The bonus is the knob, not the 2.
        using var _ = TestContent.Use(tuning: ContentDefaults.Tuning with { CleanKillBonus = 3 });
        Assert.Equal(3, FarmLadder.DefeatAdvance(dealt: 40, maxHp: 24));
        Assert.Equal(1, FarmLadder.DefeatAdvance(dealt: 2, maxHp: 24));
    }

    [Fact]
    public void Step_RoundsToTheNearestPoint_AndIsNeverZero()
    {
        Assert.Equal(3, FarmLadder.Step(18));   // 2.7 -> 3
        Assert.Equal(1, FarmLadder.Step(9));    // 1.35 -> 1
        Assert.Equal(1, FarmLadder.Step(1));    // 0.15 -> 0, floored: nothing in this game has a zero

        // The compounding the proportional step buys, as arithmetic: each step
        // reads the value the last one produced, so an 18-damage axe dummy's
        // five Damage rolls climb 18 -> 21 -> 24 -> 28 -> 32 -> 37.
        var climb = new List<int> { 18 };
        for (int i = 0; i < 5; i++)
            climb.Add(climb[^1] + FarmLadder.Step(climb[^1]));
        Assert.Equal(new[] { 18, 21, 24, 28, 32, 37 }, climb);
    }

    [Fact]
    public void RollsAStack_TracksItsChance()
    {
        // An integer draw for the reason the unique curve is quantised, and the
        // chance is flat rather than decaying: what ends a farm is the
        // per-modifier ceiling, not a falling rate.
        int hits = 0;
        var rng = new Mulberry32(Seed);
        for (int i = 0; i < 1000; i++)
            if (FarmLadder.RollsAStack(rng))
                hits++;
        Assert.InRange(hits, 450, 550);

        using (TestContent.Use(tuning: ContentDefaults.Tuning with { DefeatStackChance = 0 }))
        {
            var never = new Mulberry32(Seed);
            for (int i = 0; i < 100; i++)
                Assert.False(FarmLadder.RollsAStack(never));
        }

        using (TestContent.Use(tuning: ContentDefaults.Tuning with { DefeatStackChance = 100 }))
        {
            var always = new Mulberry32(Seed);
            for (int i = 0; i < 100; i++)
                Assert.True(FarmLadder.RollsAStack(always));
        }
    }

    [Fact]
    public void ReviveStream_IsPerEnemyPerCycle_AndDiffersFromThePlacersAndTheLoots()
    {
        // Same inputs, same stream: a load re-derives a roll rather than
        // remembering where a stream stood.
        Assert.Equal(Draw(FarmLadder.ReviveStream(Seed, 3, 2)), Draw(FarmLadder.ReviveStream(Seed, 3, 2)));

        // A different dummy, a different cycle or a different floor is a
        // different stream, so no two of them ever share a ladder.
        Assert.NotEqual(Draw(FarmLadder.ReviveStream(Seed, 3, 2)), Draw(FarmLadder.ReviveStream(Seed, 4, 2)));
        Assert.NotEqual(Draw(FarmLadder.ReviveStream(Seed, 3, 2)), Draw(FarmLadder.ReviveStream(Seed, 3, 3)));
        Assert.NotEqual(Draw(FarmLadder.ReviveStream(Seed, 3, 2)), Draw(FarmLadder.ReviveStream(Seed + 1, 3, 2)));

        // And it is nobody else's stream: new randomness gets its own salt,
        // never a continuation of an existing one.
        Assert.NotEqual(PlacerSalt, FarmLadder.ReviveSalt);
        Assert.NotEqual(LootSalt, FarmLadder.ReviveSalt);
        Assert.NotEqual(Draw(new Mulberry32(Seed ^ PlacerSalt)), Draw(FarmLadder.ReviveStream(Seed, 0, 0)));
        Assert.NotEqual(Draw(new Mulberry32(Seed ^ LootSalt)), Draw(FarmLadder.ReviveStream(Seed, 0, 0)));
    }

    [Fact]
    public void TheFirstDrawOffNeighbouringStreamsIsNotBanded()
    {
        // Each revival is its own seeded stream, so the draw this phase makes
        // load-bearing is always a *first* draw — and mulberry32's first output
        // is a weak function of its state, while the seeds differ by a thin
        // linear term in the index. The splitmix32 finalizer is what stops
        // neighbouring dummies and consecutive cycles running in blocks.
        //
        // The sweep is over the index at one fixed seed, deliberately: sweeping
        // seeds instead would average the banding away and detect nothing,
        // because the banding is exactly a within-floor correlation.
        var acrossEnemies = Enumerable.Range(0, 64)
            .Select(i => FarmLadder.RollRevival(FarmLadder.ReviveStream(Seed, i, 0)))
            .ToList();

        Assert.Equal(3, acrossEnemies.Distinct().Count());
        foreach (var gain in Enum.GetValues<RevivalGain>())
            Assert.InRange(acrossEnemies.Count(g => g == gain), 15, 30);   // 24 / 21 / 19 as it stands
        Assert.InRange(LongestRun(acrossEnemies), 1, 4);                   // 3 as it stands; 5 without the finalizer

        var acrossCycles = Enumerable.Range(0, 10)
            .Select(c => FarmLadder.RollRevival(FarmLadder.ReviveStream(Seed, 3, c)))
            .ToList();

        Assert.Equal(3, acrossCycles.Distinct().Count());
        Assert.InRange(LongestRun(acrossCycles), 1, 4);
    }

    [Fact]
    public void TheKnobsAreTheDocsNumbers_AndAreRefusedWhenTheyCannotMeanAnything()
    {
        Assert.Equal(10, Tuning.ResurrectTurnsBase);
        Assert.Equal(3, Tuning.ResurrectTurnsFloor);
        Assert.Equal(50, Tuning.DefeatStackChance);
        Assert.Equal(2, Tuning.CleanKillBonus);
        Assert.Equal(15, Tuning.ReviveStepPercent);
        Assert.Equal(50, Tuning.UniqueChanceCeilingPercent);
        Assert.Equal(20, Tuning.UniqueChanceMidpoint);
        Assert.Equal(26, Tuning.UniqueChanceKPercent);
        Assert.Equal(10, Tuning.DropEnchantChancePercent);
        Assert.Equal(4, Tuning.ArcanePotency);

        // A floor of 0 lets ResurrectTurns return 0 and a dummy revive the turn
        // it died, so it is a named startup failure rather than a hang to find.
        Rejected(t => t with { ResurrectTurnsFloor = 0 },
            nameof(Tuning.ResurrectTurnsFloor), ContentValidator.RuleRevivalTakesATurn);

        // A chance outside 0..100 makes the draw it is compared against
        // meaningless in one direction or the other.
        Rejected(t => t with { DefeatStackChance = 101 },
            nameof(Tuning.DefeatStackChance), ContentValidator.RuleChanceIsAPercentage);
        Rejected(t => t with { DropEnchantChancePercent = -1 },
            nameof(Tuning.DropEnchantChancePercent), ContentValidator.RuleChanceIsAPercentage);
        Rejected(t => t with { UniqueChanceCeilingPercent = 200 },
            nameof(Tuning.UniqueChanceCeilingPercent), ContentValidator.RuleChanceIsAPercentage);

        // The midpoint is a DefeatCount, and a count is never negative.
        Rejected(t => t with { UniqueChanceMidpoint = -1 },
            nameof(Tuning.UniqueChanceMidpoint), ContentValidator.RuleCurveMidpointIsACount);
    }

    private static void Rejected(Func<Tuning, Tuning> edit, string key, string rule)
    {
        var ex = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(edit(ContentDefaults.Tuning)));
        Assert.Equal((key, rule), (ex.EntryId, ex.Rule));
        Assert.Contains(ex.Rule, ex.Message);
    }

    private static double Draw(Mulberry32 rng) => rng.NextDouble();

    private static int LongestRun(IReadOnlyList<RevivalGain> gains)
    {
        int longest = 1;
        int run = 1;
        for (int i = 1; i < gains.Count; i++)
        {
            run = gains[i] == gains[i - 1] ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }
        return longest;
    }
}
