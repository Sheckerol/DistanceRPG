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
        => new(FixtureContent.Party.Members.Select((m, i) => i == index ? edit(m) : m).ToList());

    private static ContentException Rejected(PartyData party)
        => Assert.Throws<ContentException>(() => TestContent.Use(party: party));

    [Fact]
    public void Roster_IsTheDocsTable()
    {
        Assert.Equal(GameConstants.PartySize, Roster.All.Count);
        Assert.Equal(new[] { "A", "B", "C", "D" }, Roster.All.Select(m => m.Id));
        Assert.Equal(new[] { "Shield", "Axe", "Caster", "Dagger" }, Roster.All.Select(m => m.Name));

        // The four spreads, verbatim: STR / DEX / CON / INT.
        Assert.Equal(new InnateStats(3, 1, 4, 2), Member("A").Stats);
        Assert.Equal(new InnateStats(4, 2, 3, 1), Member("B").Stats);
        Assert.Equal(new InnateStats(2, 3, 1, 4), Member("C").Stats);
        Assert.Equal(new InnateStats(1, 4, 2, 3), Member("D").Stats);

        // A sword and shield, an axe, a staff and a dagger: the axe goes to B, the
        // only STR 4, and C gives up the second spear for the staff.
        Assert.Equal("tower_guard", Member("A").StartingWeaponId);
        Assert.Equal("great_axe", Member("B").StartingWeaponId);
        Assert.Equal("staff_of_renewal", Member("C").StartingWeaponId);
        Assert.Equal("weakspot_stiletto", Member("D").StartingWeaponId);

        var catalogue = GameContent.Current.Weapons;
        Assert.Equal(
            new[] { WeaponClass.Sword, WeaponClass.Axe, WeaponClass.Staff, WeaponClass.Dagger },
            Roster.All.Select(m => catalogue[m.StartingWeaponId].Class));

        // A second weapon in each bag, each member's own (the user's swap of
        // 2026-11-19, after play; until then a Staff of Renewal in every bag, and a
        // Staff of Mire in C's): Darts for A, a Hasta for B, a Wand of the Blast for
        // C and a Shortbow for D.
        Assert.Equal(new[] { "darts" }, Member("A").BagWeaponIds);
        Assert.Equal(new[] { "skirmishers_pike" }, Member("B").BagWeaponIds);
        Assert.Equal(new[] { "wand_of_the_blast" }, Member("C").BagWeaponIds);
        Assert.Equal(new[] { "hunting_bow" }, Member("D").BagWeaponIds);
        Assert.Equal(
            new[] { WeaponClass.Throwing, WeaponClass.Spear, WeaponClass.Wand, WeaponClass.Ranged },
            Roster.All.Select(m => catalogue[m.BagWeaponIds.Single()].Class));

        // Each bag weapon levels on a stat its member has at 3 or 4, so none is a
        // weapon its member is the party's worst with.
        Assert.All(Roster.All, m => Assert.True(
            Progression.GoverningStat(catalogue[m.BagWeaponIds.Single()].Class, m.Stats) >= 3,
            $"{m.Id} levels its {m.BagWeaponIds.Single()} on a {Progression.GoverningStat(catalogue[m.BagWeaponIds.Single()].Class, m.Stats)}"));

        // A wand's element is its drop's to roll, and a starting weapon is no drop:
        // the roster names C's, and nobody else's. Flaming is the user's choice, not a
        // guess (2026-11-20). The party is made with it — a Flaming wand, forged, at tier 1.
        Assert.Equal(new DamageType?[] { null, null, DamageType.Flaming, null }, Roster.All.Select(m => m.WandElement));
        var party = CampaignState.Start(GameContent.Current).Party;   // the roster made, as NEW CAMPAIGN makes it
        var c = party.Single(member => member.Id == "C");
        var wand = Assert.IsType<Weapon>(c.Inventory[1]);
        Assert.Equal(("wand_of_the_blast", "flaming", 1), (wand.Id, Assert.Single(wand.Enchantments).Id, wand.Enchantments[0].Tier));
        Assert.True(wand.IsForged(0));
        Assert.True(wand.CastsWith(wand.Enchantments[0].Def));
        Assert.Equal("staff_of_renewal", c.EquippedWeapon!.Id);   // the party's one heal, in hand

        // And every member is made, each holding two weapons.
        Assert.All(party, member => Assert.Equal(2, member.Inventory.OfType<Weapon>().Count()));

        // The catalogue's own lookups: by id, and a miss that names itself.
        Assert.True(Roster.TryGet("D", out var d));
        Assert.Same(Member("D"), d);
        Assert.False(Roster.TryGet("E", out _));
        Assert.Throws<KeyNotFoundException>(() => Roster["E"]);
    }

    [Fact]
    public void EveryStatIsSomebodysFourExactlyOnce_AndCIsTheOnlyConOne()
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

        // C is the only CON 1: the caster is the single thing that must not be
        // reached -- not a shared frailty but a specific one.
        Assert.Equal(new[] { "C" }, Roster.All.Where(m => m.Stats.CON == InnateStats.Low).Select(m => m.Id));

        // The two pairs, each splitting opposite halves of the stat list.
        // Front line -- A is the party's body, B its arm.
        var a = Member("A").Stats;
        var b = Member("B").Stats;
        Assert.Equal((4, 3), (a.CON, a.STR));
        Assert.Equal((4, 3), (b.STR, b.CON));
        Assert.Equal((2, 1), (a.INT, a.DEX));
        Assert.Equal((2, 1), (b.DEX, b.INT));

        // Back line -- D and C divide speed and mind the same way.
        var d = Member("D").Stats;
        var c = Member("C").Stats;
        Assert.Equal((4, 3), (d.DEX, d.INT));
        Assert.Equal((4, 3), (c.INT, c.DEX));
        Assert.Equal((2, 1), (d.CON, d.STR));
        Assert.Equal((2, 1), (c.STR, c.CON));

        // And the consequence the whole table exists for: C levels a staff four
        // times as fast as B does, and B an axe four times as fast as C.
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Staff, c));
        Assert.Equal(1, Progression.GoverningStat(WeaponClass.Staff, b));
        Assert.Equal(4, Progression.GoverningStat(WeaponClass.Axe, b));
        Assert.Equal(2, Progression.GoverningStat(WeaponClass.Axe, c));
    }

    [Fact]
    public void Validator_Rejects_ANonPermutation_AnUnknownWeapon_ATooDeepBag_ADuplicateId_ARosterWithNoCaster_AWrongSize()
    {
        // Two 4s and two 1s still sum to 10, which is exactly why the rule is
        // the permutation and not the sum.
        var spread = Rejected(With(3, m => m with { Stats = new InnateStats(4, 4, 1, 1) }));
        Assert.Equal("D", spread.EntryId);
        Assert.Equal(ContentValidator.RulePartyStatPermutation, spread.Rule);

        // A missing weapon aborts the load, naming the member -- never a
        // KeyNotFoundException at spawn, half a party later.
        var equipped = Rejected(With(1, m => m with { StartingWeaponId = "great_axe_of_typos" }));
        Assert.Equal("B", equipped.EntryId);
        Assert.Equal(ContentValidator.RulePartyWeaponExists, equipped.Rule);

        var bagged = Rejected(With(2, m => m with { BagWeaponIds = ["staff_of_mire", "staff_of_blght"], WandElement = null }));
        Assert.Equal("C", bagged.EntryId);
        Assert.Equal(ContentValidator.RulePartyWeaponExists, bagged.Rule);

        // One equipped plus a whole share of the carry bagged is a weapon more
        // than a member's slots, whatever share tuning.json gives each.
        int slots = FixtureContent.Tuning.PartyCarrySlots / GameConstants.PartySize;
        var deep = Rejected(With(2, m => m with
        {
            BagWeaponIds = Enumerable.Range(0, slots)
                .Select(i => new[] { "staff_of_mire", "staff_of_blight", "staff_of_warding", "staff_of_renewal", "flensing_knife", "arming_sword" }[i % 6])
                .ToList(),
        }));
        Assert.Equal("C", deep.EntryId);
        Assert.Equal(ContentValidator.RulePartyBagFits, deep.Rule);

        var duplicate = Rejected(With(0, m => m with { Id = "D" }));
        Assert.Equal("D", duplicate.EntryId);
        Assert.Equal(ContentValidator.RuleDuplicateId, duplicate.Rule);

        // A party with no caster never discovers half the game.
        var martial = Rejected(With(2, m => m with { StartingWeaponId = "great_axe" }));
        Assert.Equal(ContentValidator.RulePartyHasACaster, martial.Rule);

        // A wand's element is its drop's to roll, so a member who starts with one
        // names it — or NEW CAMPAIGN would throw making the party, long after the
        // load passed: none named, one no wand carries, and one named by a member
        // with no wand to carry it are each refused, naming the member.
        var unnamed = Rejected(With(2, m => m with { WandElement = null }));
        Assert.Equal(("C", ContentValidator.RulePartyWandElement), (unnamed.EntryId, unnamed.Rule));
        Assert.Contains("wand_of_the_blast has no element named for it", unnamed.Message);
        var unmade = Rejected(With(2, m => m with { WandElement = DamageType.None }));
        Assert.Equal(("C", ContentValidator.RulePartyWandElement), (unmade.EntryId, unmade.Rule));
        Assert.Contains("wand_of_the_blast cannot be made of None", unmade.Message);
        var stray = Rejected(With(0, m => m with { WandElement = DamageType.Cold }));
        Assert.Equal(("A", ContentValidator.RulePartyWandElement), (stray.EntryId, stray.Rule));
        foreach (var element in new[] { DamageType.Flaming, DamageType.Cold, DamageType.Shocking, DamageType.Acidic })   // any element a wand carries will do
            using (TestContent.Use(party: With(2, m => m with { WandElement = element })))
                Assert.Equal(element, CampaignState.Start(GameContent.Current).Party[2].WeaponIn(1)!.Enchantments[0].Def.DamageType);

        // A roster longer or shorter than the party's slots would otherwise
        // spawn a prefix and drop whoever came last, silently.
        var short_ = Rejected(new PartyData(FixtureContent.Party.Members.Take(3).ToList()));
        Assert.Equal(ContentValidator.RulePartySize, short_.Rule);

        var long_ = Rejected(new PartyData([.. FixtureContent.Party.Members, FixtureContent.Party.Members[0] with { Id = "E" }]));
        Assert.Equal(ContentValidator.RulePartySize, long_.Rule);

        // Every refusal left the running content alone.
        Assert.Equal(GameConstants.PartySize, GameContent.Current.Party.All.Count);
    }

    [Fact]
    public void DefaultParty_Validates_AndIsReachableThroughGameContent()
    {
        ContentValidator.ValidateParty(FixtureContent.Party, GameContent.Current.Weapons);   // does not throw

        var before = GameContent.Current;
        var renamed = With(3, m => m with { Name = "Thief" });

        using (TestContent.Use(party: renamed))
        {
            Assert.NotSame(before, GameContent.Current);
            Assert.Equal("Thief", GameContent.Current.Party["D"].Name);
            Assert.Equal(new InnateStats(1, 4, 2, 3), GameContent.Current.Party["D"].Stats);   // the rest of the row is untouched
            Assert.Equal(GameConstants.PartySize, GameContent.Current.Party.All.Count);
        }

        Assert.Same(before, GameContent.Current);
        Assert.Equal("Dagger", GameContent.Current.Party["D"].Name);

        // The two tuning keys the roster's pools are measured against are
        // tuning.json's to set; what is the code's is the loader's refusal of a
        // bar there is no step off.
        var zeroPool = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(FixtureContent.Tuning with { StartingPool = 0 }));
        Assert.Equal(ContentValidator.RulePoolBarIsPositive, zeroPool.Rule);
        Assert.Equal(nameof(Tuning.StartingPool), zeroPool.EntryId);

        var zeroLadder = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(FixtureContent.Tuning with { WeaponXpPerLevel = 0 }));
        Assert.Equal(ContentValidator.RulePoolBarIsPositive, zeroLadder.Rule);
        Assert.Equal(nameof(Tuning.WeaponXpPerLevel), zeroLadder.EntryId);
    }

    [Fact]
    public void StartingPool_IsTheBarBothPoolsMeasureAgainst()
    {
        // The roster is content and so is the bar it grows from: a tuning swap
        // moves every member's first point, because nothing caches a maximum.
        using var _ = TestContent.Use(tuning: FixtureContent.Tuning with { StartingPool = 40 });

        Assert.Equal(40, Progression.Pool(0, 4).Max);
        Assert.Equal(10, Progression.Pool(0, 4).XpToNext);   // 40 / 4
        Assert.Equal(40, Progression.Pool(0, 1).XpToNext);   // a whole bar, whatever the bar is
    }
}
