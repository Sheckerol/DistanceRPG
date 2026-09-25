using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Where the tuning meets the content, the tuning bends (1g; A3): three keys are
/// measured against the content as well as themselves — the carry against the
/// roster's loadouts, the forge limits against the weapons' spreads, the boss
/// offset against the dungeons' pinned floors — and a value in its own range
/// that would break shipped content is raised to what the content needs, with a
/// warning naming the entry that needed it; never an abort. Content no in-range
/// value could serve is left to fail as content. Nothing here is made current.
/// </summary>
public class TuningFitTests
{
    private static readonly Tuning BuiltIn = ShippedContent.Tuning;

    private static Tuning Fit(Tuning tuning, List<string> warnings, WeaponsData? weapons = null, DungeonsData? dungeons = null, PartyData? party = null)
        => TuningFit.ToContent(tuning, weapons ?? ShippedContent.Weapons, dungeons ?? ShippedContent.Dungeons, party ?? ShippedContent.Party, warnings);

    private static GameContent Load(Tuning tuning, PartyData? party = null, DungeonsData? dungeons = null, WeaponsData? weapons = null)
        => TestContent.Load(tuning, ShippedContent.Restricted, ShippedContent.Enchantments, weapons ?? ShippedContent.Weapons, party, dungeons);

    [Fact]
    public void ACarryTheRosterCannotFitIsRaisedToIt_NamingTheMember()
    {
        // A carry of 4 is one slot a member, and every member of the shipped
        // roster starts with two weapons (RulePartyBagFits): the carry is raised
        // to 8 — two a member, shared evenly — naming who needed it and what they
        // carry, and the roster then fits.
        var warnings = new List<string>();
        var fitted = Fit(BuiltIn with { PartyCarrySlots = 4 }, warnings);
        Assert.Equal(8, fitted.PartyCarrySlots);
        Assert.Equal(["tuning.json: PartyCarrySlots 4 is 1 slot a member, and A carries 2 weapons (weakspot_stiletto, staff_of_renewal) - using 8"], warnings);
        ContentValidator.ValidateParty(ShippedContent.Party, Load(fitted).Weapons, fitted);
        Assert.Throws<ContentException>(() => Load(BuiltIn with { PartyCarrySlots = 4 }));   // unfitted, the roster does not

        // A carry that already fits is left alone, and says nothing.
        warnings.Clear();
        Assert.Equal(BuiltIn.PartyCarrySlots, Fit(BuiltIn, warnings).PartyCarrySlots);
        Assert.Equal(8, Fit(BuiltIn with { PartyCarrySlots = 8 }, warnings).PartyCarrySlots);
        Assert.Empty(warnings);

        // Through the loader, as a playtest meets it: a file of 4 plays 8, and says so.
        var load = ShippedData.Load(new ContentTexts(Tuning: """{ "PartyCarrySlots": 4 }"""));
        Assert.Equal(8, load.Content!.Tuning.PartyCarrySlots);
        Assert.Contains(load.Notices, notice => notice.Contains("A carries 2 weapons", StringComparison.Ordinal));
    }

    [Fact]
    public void AForgeLimitBelowAShippedSpreadIsRaisedToIt_NamingTheWeapon()
    {
        // A forge limit below a shipped spread breaks RuleMaxForged. CritWindow 2
        // is below the Widowmaker's x3, the deepest any weapon forges it: 3,
        // naming it. Cleave 1 is below the Great Axe's x2 — no Cleave unique exists
        // yet, so x2 is the deepest — and is raised to 2, not to a unique's 3.
        var warnings = new List<string>();
        var limits = new Dictionary<ModifierType, int>(BuiltIn.MaxForged) { [CritWindow] = 2, [Cleave] = 1, [Light] = 1, [Brace] = 3 };
        var fitted = Fit(BuiltIn with { MaxForged = limits }, warnings);
        var rules = Load(fitted).Modifiers;
        Assert.Equal((3, 2, 1, 3), (rules.MaxForged(CritWindow), rules.MaxForged(Cleave), rules.MaxForged(Light), rules.MaxForged(Brace)));
        Assert.Equal(
        [
            "tuning.json: MaxForged.CritWindow 2 is below widowmaker, which is forged CritWindow x3 - using 3",
            "tuning.json: MaxForged.Cleave 1 is below great_axe, which is forged Cleave x2 - using 2",
        ], warnings);

        // A limit at or above every forging is left alone, and says nothing.
        warnings.Clear();
        Fit(BuiltIn, warnings);
        Fit(BuiltIn with { MaxForged = new Dictionary<ModifierType, int>(BuiltIn.MaxForged) { [Pin] = 2, [Charges] = 3 } }, warnings);
        Assert.Empty(warnings);

        // Every limit at 1: after the fit, every dungeon's theme limit is still its
        // unique's x3 — the Purity coincidence the boss's graft lands on
        // (RuleThemeHasAUnique) — and the content loads.
        var ones = Enum.GetValues<ModifierType>().ToDictionary(t => t, _ => 1);
        var all = Fit(BuiltIn with { MaxForged = ones }, warnings);
        var content = Load(all);
        foreach (var dungeon in content.Dungeons.All)
            Assert.Equal(3, content.Modifiers.MaxForged(dungeon.Theme));
    }

    [Fact]
    public void ABaselineNoWeaponCarriesIsRaisedToIt_NamingTheClass()
    {
        // A class row's baseline is held to the forge limit too (RuleMaxForged on
        // the row), and every weapon of a class carries it at least as deep, so the
        // weapons' fit serves it — until a class fields no weapon. Every bow gone,
        // with the Long Gallery that hunts one, and the Ranged row forging Longshot
        // x2 under a limit of 1, which the spears' own x1 is within: unfitted, the
        // row is refused; fitted, the limit is raised to 2 naming the class, and
        // the content loads.
        var noBows = ShippedContent.Weapons with
        {
            Classes = ShippedContent.Weapons.Classes
                .Select(row => row.Class == WeaponClass.Ranged ? row with { Baseline = new Dictionary<ModifierType, int> { [CritWindow] = 1, [Longshot] = 2 } } : row)
                .ToList(),
            Weapons = ShippedContent.Weapons.Weapons.Where(w => w.Class != WeaponClass.Ranged).ToList(),
        };
        var noGallery = new DungeonsData(ShippedContent.Dungeons.Dungeons.Where(d => d.Theme != Longshot).ToList());
        var low = BuiltIn with { MaxForged = new Dictionary<ModifierType, int>(BuiltIn.MaxForged) { [Longshot] = 1 } };
        Assert.Equal(("Ranged", ContentValidator.RuleMaxForged), Refusal(() => Load(low, dungeons: noGallery, weapons: noBows)));

        var warnings = new List<string>();
        var fitted = Fit(low, warnings, weapons: noBows, dungeons: noGallery);
        Assert.Equal(2, fitted.MaxForged[Longshot]);
        Assert.Equal(["tuning.json: MaxForged.Longshot 1 is below the Ranged class, whose baseline carries Longshot x2 - using 2"], warnings);
        Assert.Equal(2, Load(fitted, dungeons: noGallery, weapons: noBows).Modifiers.MaxForged(Longshot));
    }

    [Fact]
    public void AnOffsetThatLetsAPinnedBossPayTheFarmIsRaised_NamingTheDungeon()
    {
        // A one-floor range at floor 1 with the shallowest farm (headroom 1: a
        // well-farmed weapon banks 2) is fine for the tuning's own range, and the
        // offset of 0 passes the tuning's own clamp — but the tutorial pins its
        // floor to 2, whose boss would pay 2 stacks against a farm of 2
        // (RuleBossPaysLessThanTheFarm on the dungeon). The offset is raised to 1,
        // naming the dungeon and the floor.
        var tuning = BuiltIn with { BossFloorMin = 1, BossFloorMax = 1, BossStackOffset = 0, AcquiredHeadroom = 1 };
        ContentValidator.ValidateTuning(tuning);
        Assert.Throws<ContentException>(() => Load(tuning));

        var warnings = new List<string>();
        var fitted = Fit(tuning, warnings);
        Assert.Equal(1, fitted.BossStackOffset);
        Assert.Equal(["tuning.json: BossStackOffset 0 lets the tutorial boss on floor 2 pay 2 stacks, and a well-farmed weapon banks 2 - using 1"], warnings);
        var dungeons = Load(fitted).Dungeons;
        Assert.Equal(1, dungeons.MostStacksPaid(dungeons.Tutorial));   // one stack against a farm of two
        Assert.True(dungeons.MostStacksPaid(dungeons.Tutorial) < ContentValidator.FarmBanks(fitted.AcquiredHeadroom));

        // The shipped numbers need nothing.
        warnings.Clear();
        Assert.Equal(BuiltIn.BossStackOffset, Fit(BuiltIn, warnings).BossStackOffset);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ContentNoValueCanServeIsLeftToFailAsContent()
    {
        // Content no in-range value can serve is content's own error (1g): a member
        // carrying ten weapons (forty slots, past the thirty-six the number keys
        // reach), a dungeon pinned to floor 500, a weapon forged x4. The tuning is
        // left as it was, says nothing, and the content fails as content, naming
        // the entry and the rule — for the floor, the range every number of a
        // content file lies in (the user's ruling of 2026-09-25, R3), asked before
        // anything reads the floor.
        var warnings = new List<string>();

        var ten = new PartyData(ShippedContent.Party.Members.Select((m, i) => i == 0
            ? m with { BagWeaponIds = Enumerable.Repeat("staff_of_renewal", 9).ToList() }
            : m).ToList());
        var carry = Fit(BuiltIn, warnings, party: ten);
        Assert.Equal(BuiltIn.PartyCarrySlots, carry.PartyCarrySlots);   // left as it was
        Assert.Equal(ContentValidator.RulePartyBagFits, Assert.Throws<ContentException>(() => Load(carry, party: ten)).Rule);

        var deep = new DungeonsData(ShippedContent.Dungeons.Dungeons
            .Select(d => d.Id == "spear_hall" ? d with { BossFloorMin = 500, BossFloorMax = 500 } : d).ToList());
        var offset = Fit(BuiltIn, warnings, dungeons: deep);
        Assert.Equal(BuiltIn.BossStackOffset, offset.BossStackOffset);
        var refused = Assert.Throws<ContentException>(() => Load(offset, dungeons: deep));
        Assert.Equal(("spear_hall", ContentValidator.RuleNumberInRange), (refused.EntryId, refused.Rule));
        Assert.StartsWith($"bossFloorMin 500 is outside 1 to {ContentBounds.DeepestBossFloor} - ", refused.Detail, StringComparison.Ordinal);

        var forged = ShippedContent.Weapons with
        {
            Weapons = ShippedContent.Weapons.Weapons
                .Select(w => w.Id == "widowmaker" ? w with { Forged = new Dictionary<ModifierType, int>(w.Forged) { [CritWindow] = 4 } } : w).ToList(),
        };
        var limit = Fit(BuiltIn, warnings, weapons: forged);
        Assert.Equal(BuiltIn.MaxForged[CritWindow], new ModifierRules(limit, ShippedContent.Restricted).MaxForged(CritWindow));
        Assert.Equal(("widowmaker", ContentValidator.RuleMaxForged), Refusal(() => Load(limit, weapons: forged)));

        Assert.Empty(warnings);
    }

    private static (string EntryId, string Rule) Refusal(Action load)
    {
        var refused = Assert.Throws<ContentException>(load);
        return (refused.EntryId, refused.Rule);
    }
}
