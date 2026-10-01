using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;
using static GameEngine.DistanceRPG.Tests.TestPotions;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The alchemist's doors (§6.6, Phase 6b.4): between runs, once he has arrived — the
/// first clear of a dungeon besides the tutorial — a potion is mixed from the farm's
/// batch into a member's bag, a quality of <c>q</c> spending <c>q</c> units, into the
/// first empty bag slot and never the hand; refused before he arrives, past the
/// batch, and into a full bag, and never capped; and any potion in a bag is poured
/// back, its units back in the batch exactly (PHASE 6b DECISION 9). Nothing is mixed
/// or poured during a visit, and a reload never refills the batch: it is the last
/// run's yield less what the bags hold, worked out (PHASE 6b DECISION 6). Whole runs
/// through the campaign (<see cref="CampaignRuns"/>) on the numbers they were written
/// against (<see cref="Premise"/>), with the farm's rate and the potions' shares
/// stated here as these tests' own input, so a retune of the master fails none of it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class AlchemistTests : IDisposable
{
    /// <summary>The units a worker yields a counted run: this suite's input.</summary>
    private const int PerClear = 7;

    private static Tuning Mixing(Tuning t) => t with { FarmYieldPerClear = PerClear, HealthPotionPercent = 20, ManaPotionPercent = 20, PotionUseCost = 40 };

    private readonly IDisposable _premise = TestContent.UsePremise(tuning: Mixing);

    public void Dispose() => _premise.Dispose();

    private const long FiveDeep = CampaignRuns.FiveDeep;

    /// <summary>The game's own party, the tutorial beaten and then the Spear Hall: the alchemist arrived, and a batch of <see cref="PerClear"/> waiting. Each member carries a weapon in hand and one in its first bag slot, its other four empty.</summary>
    internal static CampaignState Arrived()
    {
        var campaign = CampaignState.Start(GameContent.Current);
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.True(Farm.AlchemistHasArrived(campaign));
        return campaign;
    }

    private static string Save(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    private static CampaignState Reload(CampaignState campaign) => SaveGame.Restore(SaveCodec.Read(Save(campaign)), GameContent.Current).Campaign;

    [Fact]
    public void AMixTakesItsQualityInUnits_IntoTheFirstFreeBagSlot()
    {
        // §6.6: "quality is just units: one unit makes an ordinary potion", and the
        // batch is split by hand. A potion of quality 3 spends 3 units of the 7 the
        // run yielded, and lands in the first empty bag slot of the member chosen —
        // A's slot 2, behind the staff in slot 1; the next behind it. Nothing else
        // moves, nothing ticks, nothing is rolled: no runs and no queue.
        var campaign = Arrived();
        var a = campaign.Party[0];
        var weapons = campaign.Party.Select(member => Enumerable.Range(0, member.Inventory.Length).Select(member.WeaponIn).ToArray()).ToArray();
        int counter = campaign.RunCounter;
        var last = campaign.LastResult;
        Assert.Equal(PerClear, Farm.Batch(campaign));
        Assert.Equal([2, 3, 4, 5], a.EmptyBagSlots);

        var mixed = Alchemist.Mix(campaign, "A", PotionType.Health, 3);
        Assert.Equal((Health(3), "A", 2, PerClear - 3), (mixed.Potion, mixed.MemberId, mixed.Slot, mixed.BatchLeft));
        Assert.Equal(Health(3), a.PotionIn(2));
        Assert.Equal((PerClear - 3, 3L), (Farm.Batch(campaign), Farm.Mixed(campaign)));
        Assert.Equal([3, 4, 5], a.EmptyBagSlots);

        var second = Alchemist.Mix(campaign, "A", PotionType.Mana, 1);
        Assert.Equal((Mana(1), 3, PerClear - 4), (second.Potion, second.Slot, second.BatchLeft));
        var other = Alchemist.Mix(campaign, "C", PotionType.Mana, 2);
        Assert.Equal(("C", 2, PerClear - 6), (other.MemberId, other.Slot, other.BatchLeft));

        // A slot freed ahead of the rest is the first again.
        Alchemist.PourBack(campaign, "A", 2);
        Assert.Equal(2, Alchemist.Mix(campaign, "A", PotionType.Health, 1).Slot);

        Assert.Equal([(2, Health(1)), (3, Mana(1))], a.Potions.ToArray());
        for (int i = 0; i < campaign.Party.Count; i++)
            Assert.Equal(weapons[i], Enumerable.Range(0, campaign.Party[i].Inventory.Length).Select(campaign.Party[i].WeaponIn));
        Assert.Equal((counter, (object?)last, (DungeonState?)null), (campaign.RunCounter, (object?)campaign.LastResult, campaign.Visit));
    }

    [Fact]
    public void NothingIsMixedBeforeTheAlchemistArrives()
    {
        // §6.6: "nobody mixes potions until you have beaten a dungeon that is not the
        // tutorial". A new campaign, the tutorial beaten, and a mercy that counted
        // before any real clear: each refused with when he arrives, naming the
        // tutorial as the enchanter's gate does, and a mix asked anyway throws and
        // lays nothing. The first real clear brings him, and a batch with him.
        var campaign = CampaignState.Start(GameContent.Current);
        const string notYet = "THE ALCHEMIST ARRIVES WITH THE FIRST CLEAR BEYOND THE TUTORIAL";
        Assert.Equal(notYet, Alchemist.NotYet(GameContent.Current.Dungeons.Tutorial));
        Assert.Equal(notYet, Alchemist.MixRefusal(campaign, "A", 1));
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        Assert.Equal(notYet, Alchemist.MixRefusal(campaign, "B", 1));
        var mercy = CampaignRuns.DeepRetreat(campaign, "spear_hall");
        Assert.Equal((true, 0), (mercy.ServiceTick, mercy.FarmYield));
        Assert.Equal(notYet, Alchemist.MixRefusal(campaign, "C", 1));
        var refused = Assert.Throws<InvalidOperationException>(() => Alchemist.Mix(campaign, "C", PotionType.Health, 1));
        Assert.Contains(notYet, refused.Message, StringComparison.Ordinal);
        Assert.All(campaign.Party, member => Assert.Empty(member.Potions));

        CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.Null(Alchemist.MixRefusal(campaign, "C", 1));
        Assert.Equal(PerClear, Farm.Batch(campaign));

        // The gate names the content's own tutorial, read off its row.
        Assert.Equal("THE ALCHEMIST ARRIVES WITH THE FIRST CLEAR BEYOND THE PRACTICE YARD",
            Alchemist.NotYet(GameContent.Current.Dungeons.Tutorial with { Name = "The Practice Yard" }));
    }

    [Fact]
    public void AMixPastTheBatchIsRefused_AndNoQualityIsCapped()
    {
        // The batch is the only ceiling (§6.6: "past that, more quality is pure
        // overshoot", and an oversize potion is never refused — Overheal can use it).
        // Quality 8 of a batch of 7 is refused, saying how many units it needs and
        // how many are left; 7, past the 5 that restores a whole pool, is mixed. With
        // the whole batch mixed, the next is refused as spent: pour one back. A
        // quality below 1 is no potion, and throws.
        var campaign = Arrived();
        Assert.Equal(5, Alchemist.QualityForAll(PotionType.Health));
        Assert.Equal("QUALITY 8 NEEDS 8 UNITS - 7 ARE LEFT", Alchemist.MixRefusal(campaign, "A", PerClear + 1));
        Assert.Throws<InvalidOperationException>(() => Alchemist.Mix(campaign, "A", PotionType.Health, PerClear + 1));
        Assert.Empty(campaign.Party[0].Potions);
        Assert.Null(Alchemist.MixRefusal(campaign, "A", PerClear));
        Assert.Equal(Health(PerClear), Alchemist.Mix(campaign, "A", PotionType.Health, PerClear).Potion);
        Assert.Equal(0, Farm.Batch(campaign));
        Assert.Equal("THE BATCH IS SPENT - POUR ONE BACK TO MIX ANOTHER", Alchemist.MixRefusal(campaign, "B", 1));
        Assert.Equal(Alchemist.Spent, Alchemist.MixRefusal(campaign, "B", 1));
        Assert.Equal("QUALITY 2 NEEDS 2 UNITS - 1 IS LEFT", Alchemist.Short(2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Alchemist.MixRefusal(campaign, "B", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Alchemist.Mix(campaign, "B", PotionType.Mana, -1));
        Assert.Throws<ArgumentException>(() => Alchemist.MixRefusal(campaign, "Z", 1));

        // No batch at all, and why: a run that did not count yields none; a farm at a
        // rate of 0 yields none on any; and a run that counted but yielded nothing — a
        // save from before the farm, Phase 6a's — waits for the next run that counts.
        var idle = Arrived();
        CampaignRuns.ShallowRetreat(idle, "long_gallery");
        Assert.Equal("NO BATCH - THE LAST RUN DID NOT COUNT", Alchemist.MixRefusal(idle, "A", 1));
        using (TestContent.UsePremise(tuning: t => Mixing(t) with { FarmYieldPerClear = 0 }))
        {
            CampaignRuns.DeepRetreat(idle, "long_gallery");
            Assert.Equal("NO BATCH - THE FARM YIELDS NOTHING", Alchemist.MixRefusal(idle, "A", 1));
        }
        var lifted = SaveGame.Restore(SaveCodec.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "saves", "phase-6a-between-runs.json"))),
            GameContent.Current).Campaign;
        Assert.Equal((true, 0), (lifted.LastResult!.ServiceTick, lifted.LastResult.FarmYield));
        Assert.Equal("NO BATCH YET - THE NEXT RUN THAT COUNTS BRINGS ONE", Alchemist.MixRefusal(lifted, lifted.Party[0].Id, 1));
    }

    [Fact]
    public void ABagWithNoRoomIsRefused_TheHandKeepsAWeapon()
    {
        // A potion goes in the bag, and a full bag takes none: A's four empty slots
        // filled, the next mix into A is refused — the hand keeps a weapon, so it is
        // never the room — while B's bag still takes one.
        var campaign = Arrived();
        for (int i = 0; i < 4; i++)
            Alchemist.Mix(campaign, "A", PotionType.Mana, 1);
        Assert.Empty(campaign.Party[0].EmptyBagSlots);
        Assert.Equal("[A] HAS NO ROOM IN THE BAG - THE HAND KEEPS A WEAPON", Alchemist.MixRefusal(campaign, "A", 1));
        Assert.Throws<InvalidOperationException>(() => Alchemist.Mix(campaign, "A", PotionType.Mana, 1));
        Assert.Equal(PerClear - 4, Farm.Batch(campaign));
        Assert.Null(Alchemist.MixRefusal(campaign, "B", 1));

        // The batch is asked before the bag: a full bag with nothing left says spent.
        Assert.Equal(PerClear - 4, Alchemist.Mix(campaign, "B", PotionType.Health, PerClear - 4).Potion.Quality);
        Assert.Equal(Alchemist.Spent, Alchemist.MixRefusal(campaign, "A", 1));
    }

    [Fact]
    public void APotionPouredBackReturnsItsUnitsExactly()
    {
        // PHASE 6b DECISION 9: any potion in a bag is poured back between runs, and
        // its units are back in the batch exactly — a misplaced mix costs nothing
        // before the descent. It leaves its slot empty, and every other slot as it
        // was; a slot holding no potion, or the hand, has nothing to pour.
        var campaign = Arrived();
        Alchemist.Mix(campaign, "A", PotionType.Health, 3);
        Alchemist.Mix(campaign, "B", PotionType.Mana, 2);
        Assert.Equal(PerClear - 5, Farm.Batch(campaign));

        var poured = Alchemist.PourBack(campaign, "A", 2);
        Assert.Equal((Health(3), "A", 2, PerClear - 2), (poured.Potion, poured.MemberId, poured.Slot, poured.BatchLeft));
        Assert.Null(campaign.Party[0].Inventory[2]);
        Assert.Equal("staff_of_renewal", campaign.Party[0].WeaponIn(1)!.Id);
        Assert.Equal(PerClear, Alchemist.PourBack(campaign, "B", 2).BatchLeft);
        Assert.Equal((PerClear, 0L), (Farm.Batch(campaign), Farm.Mixed(campaign)));

        // Mixed again another way: the same units, nothing lost or gained.
        Alchemist.Mix(campaign, "D", PotionType.Mana, 5);
        Assert.Equal(PerClear - 5, Farm.Batch(campaign));

        Assert.Throws<ArgumentException>(() => Alchemist.PourBack(campaign, "A", 2));   // empty
        Assert.Throws<ArgumentException>(() => Alchemist.PourBack(campaign, "A", 1));   // a weapon's
        Assert.Throws<ArgumentOutOfRangeException>(() => Alchemist.PourBack(campaign, "A", 0));   // the hand
        Assert.Throws<ArgumentException>(() => Alchemist.PourBack(campaign, "Z", 2));
        Assert.Equal(PerClear - 5, Farm.Batch(campaign));
    }

    [Fact]
    public void NothingIsMixedOrPouredDuringAVisit()
    {
        // The bags are changed by hand only between runs: once the party has
        // descended, the batch is 0 — what was left unmixed spoiled — and neither
        // door opens. A potion carried in stays where it is.
        var campaign = Arrived();
        Alchemist.Mix(campaign, "A", PotionType.Health, 2);
        campaign.Enter("long_gallery", FiveDeep).OpenTurns(campaign.Party, CampaignRuns.Ten);
        Assert.Equal(0, Farm.Batch(campaign));
        Assert.Throws<InvalidOperationException>(() => Alchemist.MixRefusal(campaign, "A", 1));
        Assert.Throws<InvalidOperationException>(() => Alchemist.Mix(campaign, "A", PotionType.Mana, 1));
        Assert.Throws<InvalidOperationException>(() => Alchemist.PourBack(campaign, "A", 2));
        Assert.Throws<InvalidOperationException>(() => campaign.Stow("A", Mana(1)));
        Assert.Throws<InvalidOperationException>(() => campaign.Unstow("A", 2));
        Assert.Equal([(2, Health(2))], campaign.Party[0].Potions.ToArray());
    }

    [Fact]
    public void AMixSurvivesAReload_AndTheBatchIsNotRefilled()
    {
        // R1, and PHASE 6b DECISION 6: the hub saves every change, and a mix writes
        // only a potion into a bag. So a quit and a load after a mix bring the potion
        // back and leave the batch as it was — never refilled — and a batch mixed to
        // the last unit is still spent after one. A pour-back after the load gives
        // the same units back.
        var campaign = Arrived();
        Alchemist.Mix(campaign, "A", PotionType.Health, 3);
        var restored = Reload(campaign);
        Assert.Equal([(2, Health(3))], restored.Party[0].Potions.ToArray());
        Assert.Equal((PerClear - 3, PerClear - 3), (Farm.Batch(campaign), Farm.Batch(restored)));

        Alchemist.Mix(restored, "B", PotionType.Mana, PerClear - 3);
        var again = Reload(restored);
        Assert.Equal(0, Farm.Batch(again));
        Assert.Equal(Alchemist.Spent, Alchemist.MixRefusal(again, "C", 1));
        Assert.Equal(PerClear - 3, Alchemist.PourBack(again, "B", 2).BatchLeft);
        Assert.Equal(PerClear - 3, Farm.Batch(Reload(again)));
    }

    [Fact]
    public void StowAndUnstowHaveTheirOneCallerEach()
    {
        // Read off the IL: the campaign's doors for a potion mixed and one poured back
        // are the alchemist's alone, so every mix has asked his gates first.
        Assert.Equal(["Alchemist.Mix"], CallersOf(typeof(CampaignState), "Stow"));
        Assert.Equal(["Alchemist.PourBack"], CallersOf(typeof(CampaignState), "Unstow"));
    }
}
