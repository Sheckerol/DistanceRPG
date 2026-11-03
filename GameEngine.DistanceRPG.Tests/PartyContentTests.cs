using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The §5.9 starting roster as content: the §2.1 table, the invariants that
/// table has and the rules the loader actually enforces. In the content
/// collection because the validator tests load rosters through
/// <see cref="TestContent.Use"/>.
/// </summary>
[Collection(TestContent.Collection)]
public class PartyContentTests
{
    private static PartyRoster Roster => GameContent.Current.Party;

    private static PartyMemberDef Member(string id) => Roster[id];

    /// <summary>The compiled roster with <paramref name="edit"/> applied to the member at <paramref name="index"/>.</summary>
    private static PartyData With(int index, Func<PartyMemberDef, PartyMemberDef> edit)
        => new(ContentDefaults.Party.Members.Select((m, i) => i == index ? edit(m) : m).ToList());

    private static ContentException Rejected(PartyData party)
        => Assert.Throws<ContentException>(() => TestContent.Use(party: party));

    [Fact]
    public void Roster_IsTheDocsTable()
    {
        Assert.Equal(GameConstants.PartySize, Roster.All.Count);
        Assert.Equal(new[] { "A", "B", "C", "D" }, Roster.All.Select(m => m.Id));
        Assert.Equal(new[] { "Dagger", "Shield", "Axe", "Caster" }, Roster.All.Select(m => m.Name));

        // The four spreads, verbatim: STR / DEX / CON / INT.
        Assert.Equal(new InnateStats(1, 4, 2, 3), Member("A").Stats);
        Assert.Equal(new InnateStats(3, 1, 4, 2), Member("B").Stats);
        Assert.Equal(new InnateStats(4, 2, 3, 1), Member("C").Stats);
        Assert.Equal(new InnateStats(2, 3, 1, 4), Member("D").Stats);

        // A dagger, a sword and shield, an axe, and a staff: the axe goes to C,
        // the only STR 4, and D gives up the second spear for the staff.
        Assert.Equal("weakspot_stiletto", Member("A").StartingWeaponId);
        Assert.Equal("tower_guard", Member("B").StartingWeaponId);
        Assert.Equal("great_axe", Member("C").StartingWeaponId);
        Assert.Equal("staff_of_renewal", Member("D").StartingWeaponId);

        var catalogue = GameContent.Current.Weapons;
        Assert.Equal(
            new[] { WeaponClass.Dagger, WeaponClass.Sword, WeaponClass.Axe, WeaponClass.Staff },
            Roster.All.Select(m => catalogue[m.StartingWeaponId].Class));

        // The slot-1 staff the party has always carried, and D's second, debuff
        // staff: Mire, not Blight -- the on-theme choice for a game where
        // movement is the resource.
        Assert.Equal(new[] { "staff_of_renewal" }, Member("A").BagWeaponIds);
        Assert.Equal(new[] { "staff_of_renewal" }, Member("B").BagWeaponIds);
        Assert.Equal(new[] { "staff_of_renewal" }, Member("C").BagWeaponIds);
        Assert.Equal(new[] { "staff_of_mire" }, Member("D").BagWeaponIds);

        // No spear anywhere: the party's own Brace demonstration is dropped, and
        // Brace is taught from the receiving end instead.
        Assert.DoesNotContain(
            WeaponClass.Spear,
            Roster.All.SelectMany(m => m.BagWeaponIds.Prepend(m.StartingWeaponId)).Select(id => catalogue[id].Class));

        // The catalogue's own lookups: by id, and a miss that names itself.
        Assert.True(Roster.TryGet("D", out var d));
        Assert.Same(Member("D"), d);
        Assert.False(Roster.TryGet("E", out _));
        Assert.Throws<KeyNotFoundException>(() => Roster["E"]);
    }

    [Fact]
    public void EveryStatIsSomebodysFourExactlyOnce_AndDIsTheOnlyConOne()
    {
        // "No character is better endowed than another; they are differently
        // arranged": every spread is a permutation and sums to 10.
        foreach (var member in Roster.All)
        {
            Assert.True(member.Stats.IsPermutation, $"{member.Id}: {member.Stats}");
            Assert.Equal(InnateStats.Sum, member.Stats.Total);
        }

        // Each pair mirrors the other's ranking, so no stat is anyone's 4 twice
        // and every stat is somebody's best.
        foreach (var stat in Enum.GetValues<Stat>())
            Assert.Single(Roster.All, m => m.Stats.Of(stat) == InnateStats.High);

        // D is the only CON 1: the caster is the single thing that must not be
        // reached -- not a shared frailty but a specific one.
        Assert.Equal(new[] { "D" }, Roster.All.Where(m => m.Stats.CON == InnateStats.Low).Select(m => m.Id));

        // The two pairs, each splitting opposite halves of the stat list.
        // Front line -- B is the party's body, C its arm.
        var b = Member("B").Stats;
        var c = Member("C").Stats;
        Assert.Equal((4, 3), (b.CON, b.STR));
        Assert.Equal((4, 3), (c.STR, c.CON));
        Assert.Equal((2, 1), (b.INT, b.DEX));
        Assert.Equal((2, 1), (c.DEX, c.INT));

        // Back line -- A and D divide speed and mind the same way.
        var a = Member("A").Stats;
        var d = Member("D").Stats;
        Assert.Equal((4, 3), (a.DEX, a.INT));
        Assert.Equal((4, 3), (d.INT, d.DEX));
        Assert.Equal((2, 1), (a.CON, a.STR));
        Assert.Equal((2, 1), (d.STR, d.CON));

        // And the consequence the whole table exists for: D levels a staff four
        // times as fast as C does, and C an axe four times as fast as D.
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Staff, d));
        Assert.Equal(1, Progression.GoverningStat(WeaponClass.Staff, c));
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Axe, c));
        Assert.Equal(2, Progression.GoverningStat(WeaponClass.Axe, d));
    }

    [Fact]
    public void Validator_Rejects_ANonPermutation_AnUnknownWeapon_ATooDeepBag_ADuplicateId_ARosterWithNoCaster_AWrongSize()
    {
        // Two 4s and two 1s still sum to 10, which is exactly why the rule is
        // the permutation and not the sum.
        var spread = Rejected(With(0, m => m with { Stats = new InnateStats(4, 4, 1, 1) }));
        Assert.Equal("A", spread.EntryId);
        Assert.Equal(ContentValidator.RulePartyStatPermutation, spread.Rule);

        // A missing weapon aborts the load, naming the member -- never a
        // KeyNotFoundException at spawn, half a party later.
        var equipped = Rejected(With(2, m => m with { StartingWeaponId = "great_axe_of_typos" }));
        Assert.Equal("C", equipped.EntryId);
        Assert.Equal(ContentValidator.RulePartyWeaponExists, equipped.Rule);

        var bagged = Rejected(With(3, m => m with { BagWeaponIds = ["staff_of_mire", "staff_of_blght"] }));
        Assert.Equal("D", bagged.EntryId);
        Assert.Equal(ContentValidator.RulePartyWeaponExists, bagged.Rule);

        // One equipped plus three bagged is four weapons into three slots.
        var deep = Rejected(With(3, m => m with { BagWeaponIds = ["staff_of_mire", "staff_of_blight", "staff_of_warding"] }));
        Assert.Equal("D", deep.EntryId);
        Assert.Equal(ContentValidator.RulePartyBagFits, deep.Rule);

        var duplicate = Rejected(With(1, m => m with { Id = "A" }));
        Assert.Equal("A", duplicate.EntryId);
        Assert.Equal(ContentValidator.RuleDuplicateId, duplicate.Rule);

        // A party with no caster never discovers half the game.
        var martial = Rejected(With(3, m => m with { StartingWeaponId = "great_axe" }));
        Assert.Equal(ContentValidator.RulePartyHasACaster, martial.Rule);

        // A roster longer or shorter than the party's slots would otherwise
        // spawn a prefix and drop whoever came last, silently.
        var short_ = Rejected(new PartyData(ContentDefaults.Party.Members.Take(3).ToList()));
        Assert.Equal(ContentValidator.RulePartySize, short_.Rule);

        var long_ = Rejected(new PartyData([.. ContentDefaults.Party.Members, ContentDefaults.Party.Members[0] with { Id = "E" }]));
        Assert.Equal(ContentValidator.RulePartySize, long_.Rule);

        // Every refusal left the running content alone.
        Assert.Equal(GameConstants.PartySize, GameContent.Current.Party.All.Count);
    }

    [Fact]
    public void DefaultParty_Validates_AndIsReachableThroughGameContent()
    {
        ContentValidator.ValidateParty(ContentDefaults.Party, GameContent.Current.Weapons);   // does not throw

        var before = GameContent.Current;
        var renamed = With(0, m => m with { Name = "Thief" });

        using (TestContent.Use(party: renamed))
        {
            Assert.NotSame(before, GameContent.Current);
            Assert.Equal("Thief", GameContent.Current.Party["A"].Name);
            Assert.Equal(new InnateStats(1, 4, 2, 3), GameContent.Current.Party["A"].Stats);   // the rest of the row is untouched
            Assert.Equal(GameConstants.PartySize, GameContent.Current.Party.All.Count);
        }

        Assert.Same(before, GameContent.Current);
        Assert.Equal("Dagger", GameContent.Current.Party["A"].Name);

        // The two tuning keys the roster's pools are measured against, and the
        // loader's refusal of a bar there is no step off.
        Assert.Equal(30, GameContent.Current.Tuning.StartingPool);
        Assert.Equal(100, GameContent.Current.Tuning.WeaponXpPerLevel);

        var zeroPool = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(new Tuning { StartingPool = 0 }));
        Assert.Equal(ContentValidator.RulePoolBarIsPositive, zeroPool.Rule);
        Assert.Equal(nameof(Tuning.StartingPool), zeroPool.EntryId);

        var zeroLadder = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(new Tuning { WeaponXpPerLevel = 0 }));
        Assert.Equal(ContentValidator.RulePoolBarIsPositive, zeroLadder.Rule);
        Assert.Equal(nameof(Tuning.WeaponXpPerLevel), zeroLadder.EntryId);
    }

    [Fact]
    public void StartingPool_IsTheBarBothPoolsMeasureAgainst()
    {
        // The roster is content and so is the bar it grows from: a tuning swap
        // moves every member's first point, because nothing caches a maximum.
        using var _ = TestContent.Use(tuning: ContentDefaults.Tuning with { StartingPool = 40 });

        Assert.Equal(40, Progression.Pool(0, 4).Max);
        Assert.Equal(10, Progression.Pool(0, 4).XpToNext);   // 40 / 4
        Assert.Equal(40, Progression.Pool(0, 1).XpToNext);   // a whole bar, whatever the bar is
    }
}
