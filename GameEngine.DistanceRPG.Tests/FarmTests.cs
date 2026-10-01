using System.Reflection;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;
using static GameEngine.DistanceRPG.Tests.TestPotions;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The farm (§6.6, Phase 6b.3): where potions come from. Every dungeon besides the
/// tutorial beaten once is a worker, and every run that counts — a boss kill or
/// §6.3's mercy, the enchanter's tick — yields <c>FarmYieldPerClear</c> units a
/// worker in the same event (PHASE 6b DECISION 4), the clearing run at its new
/// count (DECISION 5). The tutorial never counts, asked of the retired list. The
/// batch is a flow, not a stock: the last run's yield less what the bags hold,
/// worked out and never a field (DECISION 6), 0 while a visit is held, and never
/// added to; every potion carried spoils as the run ends, out or wiped. The
/// alchemist arrives with the first real clear, derived. Whole runs through the
/// campaign (<see cref="CampaignRuns"/>) on the numbers they were written against
/// (<see cref="Premise"/>), with the farm's rate stated here as these tests' own
/// input, so a retune of the master fails none of it — but for the one test of the
/// master's derivation, which reads the master where the game does.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class FarmTests : IDisposable
{
    /// <summary>The units a worker yields a counted run: these tests' own number, the shipped one's derivation aside.</summary>
    private const int PerClear = 7;

    private readonly IDisposable _premise = TestContent.UsePremise(tuning: Farming);

    public void Dispose() => _premise.Dispose();

    /// <summary>The premise's tuning, with the farm's rate these tests state.</summary>
    private static Tuning Farming(Tuning t) => t with { FarmYieldPerClear = PerClear };

    private const long FiveDeep = CampaignRuns.FiveDeep;

    private const long GoldenSeed = CampaignRuns.GoldenSeed;

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    /// <summary>Every dungeon of the content that does not retire on its clear, in the file's order: the farm's workers to be.</summary>
    private static string[] RealDungeons => GameContent.Current.Dungeons.All.Where(def => !def.RetiresOnClear).Select(def => def.Id).ToArray();

    /// <summary>The tutorial beaten and retired: the hub open, and the farm with no worker yet.</summary>
    private static CampaignState TutorialBeaten()
    {
        var campaign = NewCampaign();
        CampaignRuns.Clear(campaign, "tutorial", GoldenSeed);
        Assert.Equal(0, Farm.Clears(campaign));
        return campaign;
    }

    /// <summary>A wipe at the bottom of <paramref name="dungeonId"/> with its boss down — killed the boss, died on the way out.</summary>
    private static RunResult WipeWithTheBossDown(CampaignState campaign, string dungeonId)
    {
        var visit = campaign.Enter(dungeonId, FiveDeep);
        var turns = visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        while (visit.Current.DownStairs is not null)
            turns = CampaignRuns.Down(visit, campaign.Party);
        visit.Current.Boss!.Alive = false;
        visit.RecordBossDefeated();
        return CampaignRuns.Wipe(campaign, turns);
    }

    [Fact]
    public void EveryFirstClearBesidesTheTutorialAddsToTheYield()
    {
        // §6.6: "potionsProduced per run = FarmYieldPerClear × non-tutorial dungeons
        // first-cleared". The tutorial's clear adds nothing; each real dungeon beaten
        // for the first time adds a worker, and the run that beat it yields at the
        // count it ends with.
        var campaign = NewCampaign();
        var tutorial = CampaignRuns.Clear(campaign, "tutorial", GoldenSeed);
        Assert.Equal((true, true, 0, 0), (tutorial.FirstClear, tutorial.Retired, tutorial.FarmClears, tutorial.FarmYield));

        int workers = 0;
        foreach (string id in RealDungeons)
        {
            var cleared = CampaignRuns.Clear(campaign, id, FiveDeep);
            workers++;
            Assert.Equal((id, true, workers, PerClear * workers), (cleared.DungeonId, cleared.FirstClear, cleared.FarmClears, cleared.FarmYield));
            Assert.Equal(workers, Farm.Clears(campaign));
        }

        // The yield is the rate times the workers, worked out wide and held at the
        // count a save reads it back to.
        Assert.Equal((0, PerClear, PerClear * 3), (Farm.Yield(0, counted: true), Farm.Yield(1, counted: true), Farm.Yield(3, counted: true)));
        using (TestContent.UsePremise(tuning: t => t with { FarmYieldPerClear = 1000 }))
            Assert.Equal(ContentBounds.MostCount, Farm.Yield(int.MaxValue, counted: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Farm.Yield(-1, counted: true));
    }

    [Fact]
    public void ARepeatClearAddsNothing()
    {
        // The farm counts dungeons beaten once: a dungeon cleared again finds itself
        // in the set already, so the run is no first clear and the farm keeps its
        // count — it yields what any counted run at that count yields. The content
        // holds the tutorial and the Spear Hall alone, so the Spear Hall is offered
        // early while its lock stands, and is cleared again.
        using var two = TestContent.UsePremise(tuning: Farming, dungeons: data => new DungeonsData(data.Dungeons.Where(def => def.Id is "tutorial" or "spear_hall").ToArray()));
        var campaign = TutorialBeaten();
        var first = CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.Equal((true, 1, PerClear), (first.FirstClear, first.FarmClears, first.FarmYield));
        Assert.Equal("spear_hall", campaign.EarlyOffer());
        var again = CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.Equal((false, 1, PerClear), (again.FirstClear, again.FarmClears, again.FarmYield));
        Assert.Equal(1, Farm.Clears(campaign));
    }

    [Fact]
    public void TheTutorialNeverCounts_AskedOfTheRetiredList()
    {
        // The tutorial retires on its clear, so the farm, counting the dungeons
        // cleared and not retired, never counts it (§6.6: "the tutorial does not
        // count"). It asks the campaign's own records, never which dungeon today's
        // dungeons.json retires on its clear: a copy beside the game that moves
        // retiresOnClear off the tutorial and onto the Spear Hall leaves the count
        // where it was, before a save and after one — and the Spear Hall, beaten
        // under it, retires and never counts either.
        var campaign = TutorialBeaten();
        Assert.False(Farm.AlchemistHasArrived(campaign));
        using (TestContent.UsePremise(tuning: Farming, dungeons: TestShop.TutorialMoved))
        {
            Assert.False(GameContent.Current.Dungeons["tutorial"].RetiresOnClear);
            Assert.Equal(0, Farm.Clears(campaign));
            var restored = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign))), GameContent.Current).Campaign;
            Assert.Equal(0, Farm.Clears(restored));

            var hall = CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
            Assert.Equal((true, true, 0, 0), (hall.FirstClear, hall.Retired, hall.FarmClears, hall.FarmYield));
            Assert.False(Farm.AlchemistHasArrived(campaign));
        }
    }

    [Fact]
    public void AClearingRunYieldsAtItsNewCount()
    {
        // PHASE 6b DECISION 5: the clear is recorded before the yield is worked out,
        // so the run that beats a dungeon for the first time yields at the count it
        // brings — the first real clear "hands over an entire category" at once, a
        // batch waiting as the run ends, and the next first clear yields at two.
        var campaign = TutorialBeaten();
        var hall = CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.Equal((1, PerClear), (hall.FarmClears, hall.FarmYield));
        Assert.Equal(PerClear, Farm.Batch(campaign));
        var gallery = CampaignRuns.Clear(campaign, "long_gallery", FiveDeep);
        Assert.Equal((2, 2 * PerClear), (gallery.FarmClears, gallery.FarmYield));
        Assert.Equal(2 * PerClear, Farm.Batch(campaign));
    }

    [Fact]
    public void ARunThatCountsYields_AMercyIncluded_OneThatDoesNotYieldsNothing()
    {
        // PHASE 6b DECISION 4: the farm ticks with the enchanter, on every run that
        // counts — a boss kill, or a failure deep enough for §6.3's mercy — and a run
        // that does not count yields nothing, so no batch waits after it.
        var campaign = TutorialBeaten();
        CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        var mercy = CampaignRuns.DeepRetreat(campaign, "long_gallery");
        Assert.Equal((true, false, 1, PerClear), (mercy.MercyTick, mercy.FirstClear, mercy.FarmClears, mercy.FarmYield));
        Assert.Equal(PerClear, Farm.Batch(campaign));
        var shallow = CampaignRuns.ShallowRetreat(campaign, "long_gallery");
        Assert.Equal((false, 1, 0), (shallow.ServiceTick, shallow.FarmClears, shallow.FarmYield));
        Assert.Equal(0, Farm.Batch(campaign));
        Assert.Equal(0, Farm.Yield(4, counted: false));

        // A rate of 0 is a dial: the farm yields nothing on a run that counts.
        using (TestContent.UsePremise(tuning: t => t with { FarmYieldPerClear = 0 }))
        {
            var idle = CampaignRuns.DeepRetreat(campaign, "long_gallery");
            Assert.Equal((true, 1, 0), (idle.ServiceTick, idle.FarmClears, idle.FarmYield));
        }
    }

    [Fact]
    public void ABossKilledThenAWipeIsAClearThatYields()
    {
        // "Killed the boss, died on the way out" is a clear (§6.3, settled.md): the
        // run counts, the dungeon is beaten for the first time, and the farm grows
        // and yields at its new count, as the enchanter ticks.
        var campaign = TutorialBeaten();
        var wiped = WipeWithTheBossDown(campaign, "spear_hall");
        Assert.Equal((RunOutcome.Death, true, true), (wiped.Outcome, wiped.BossDefeated, wiped.ServiceTick));
        Assert.Equal((true, 1, PerClear), (wiped.FirstClear, wiped.FarmClears, wiped.FarmYield));
        Assert.True(Farm.AlchemistHasArrived(campaign));
    }

    [Fact]
    public void TheBatchIsAFlow_NeverAStock()
    {
        // §6.6: "what you do not pick up is gone ... next run's batch is a fresh
        // one". The batch is the last run's yield less what the bags hold: potions of
        // 3 units leave 4 of 7. A held visit reads it as 0 — what was left unmixed
        // spoiled on the descent — and the next counted run's end replaces the yield,
        // never adds the leftover to it: 7 again, not 11.
        var campaign = TutorialBeaten();
        CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Carrying(campaign, "A", (2, Health(2)), (3, Mana(1)));
        Assert.Equal((3L, PerClear - 3), (Farm.Mixed(campaign), Farm.Batch(campaign)));

        campaign.Enter("long_gallery", FiveDeep).OpenTurns(campaign.Party, CampaignRuns.Ten);
        Assert.Equal(0, Farm.Batch(campaign));
        Assert.Equal(3L, Farm.Mixed(campaign));   // carried in: what was mixed rides with the party
        var visit = campaign.Visit!;
        CampaignRuns.Down(visit, campaign.Party);
        CampaignRuns.Down(visit, campaign.Party);
        var next = CampaignRuns.Exit(campaign);
        Assert.True(next.MercyTick);
        Assert.Equal(PerClear, next.FarmYield);
        Assert.Equal((0L, PerClear), (Farm.Mixed(campaign), Farm.Batch(campaign)));

        // A campaign before any run has no batch.
        Assert.Equal(0, Farm.Batch(NewCampaign()));
    }

    [Fact]
    public void EveryPotionCarriedSpoilsAtTheRunsEnd_OutOrWiped()
    {
        // §6.6: "a mixed potion keeps exactly as long as the trip it was mixed for".
        // Every potion carried as the run ends is taken out of its bag and listed in
        // the result, in roster then slot order, on the way out and on a wipe alike
        // — a consumable lost by its lifetime, never gear forfeited — and the bags
        // keep every weapon they held.
        var campaign = TutorialBeaten();
        CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Carrying(campaign, "C", (3, Health(3)));
        Carrying(campaign, "A", (4, Mana(1)), (2, Health(2)));
        var weapons = campaign.Party.Select(member => Enumerable.Range(0, member.Inventory.Length).Select(member.WeaponIn).ToArray()).ToArray();
        var walked = CampaignRuns.ShallowRetreat(campaign, "long_gallery");
        Assert.Equal([Health(2), Mana(1), Health(3)], walked.Spoiled!);
        Assert.All(campaign.Party, member => Assert.Empty(member.Potions));
        for (int i = 0; i < campaign.Party.Count; i++)
            Assert.Equal(weapons[i], Enumerable.Range(0, campaign.Party[i].Inventory.Length).Select(campaign.Party[i].WeaponIn));

        // Wiped, the boss standing: the potions spoil all the same, beside the spoils
        // the wipe loses — and a run that carried none says so with nothing.
        var none = CampaignRuns.DeepRetreat(campaign, "long_gallery");
        Assert.Null(none.Spoiled);
        Carrying(campaign, "B", (2, Mana(2)));
        Carrying(campaign, "D", (3, Health(1)));
        var wiping = campaign.Enter("long_gallery", FiveDeep);
        var wiped = CampaignRuns.Wipe(campaign, wiping.OpenTurns(campaign.Party, CampaignRuns.Ten));
        Assert.Equal(RunOutcome.Death, wiped.Outcome);
        Assert.Equal([Mana(2), Health(1)], wiped.Spoiled!);
        Assert.All(campaign.Party, member => Assert.Empty(member.Potions));
    }

    [Fact]
    public void TheBatchNeverReachesCampaignState()
    {
        // §6.7: "the batch must never reach CampaignState". The run's yield is a fact
        // of its result (PHASE 6b DECISION 6), and the batch is worked out from it
        // and the bags: so a save and a load leave the batch as it was — a reload
        // never refills it (R1) — the campaign declares no field for a farm or a
        // batch, and the spoilage has one caller, the run's end, read off the IL.
        var campaign = TutorialBeaten();
        CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Carrying(campaign, "B", (2, Health(2)));
        int batch = Farm.Batch(campaign);
        Assert.Equal(PerClear - 2, batch);
        var restored = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign))), GameContent.Current).Campaign;
        Assert.Equal((batch, 2L), (Farm.Batch(restored), Farm.Mixed(restored)));

        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Assert.DoesNotContain(typeof(CampaignState).GetMembers(Declared),
            member => new[] { "Batch", "Farm", "Yield" }.Any(word => member.Name.Contains(word, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(["CampaignState.EndRun"], CallersOf(typeof(Farm), nameof(Farm.Spoil)));
    }

    [Fact]
    public void TheAlchemistArrivesWithTheFirstRealClear()
    {
        // §6.6: "nobody mixes potions until you have beaten a dungeon that is not the
        // tutorial". Derived from the clear set, never saved: not in a new campaign,
        // not after the tutorial, not after a failed run — and on the first clear of a
        // real dungeon, whose result says so: a first clear, not retired, the farm's
        // first worker (what the summary and the log read).
        var campaign = NewCampaign();
        Assert.False(Farm.AlchemistHasArrived(campaign));
        CampaignRuns.Clear(campaign, "tutorial", GoldenSeed);
        Assert.False(Farm.AlchemistHasArrived(campaign));
        CampaignRuns.DeepRetreat(campaign, "spear_hall");
        Assert.False(Farm.AlchemistHasArrived(campaign));
        var arrival = CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.Equal((true, false, 1), (arrival.FirstClear, arrival.Retired, arrival.FarmClears));
        Assert.True(Farm.AlchemistHasArrived(campaign));
        var restored = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign))), GameContent.Current).Campaign;
        Assert.True(Farm.AlchemistHasArrived(restored));
    }

    [Fact]
    public void AMovedRetiresOnClearCanTakeTheFarmsOneWorker_AndTheAlchemistWithIt()
    {
        // PHASE 6b DECISION 11, shipped provisionally: the alchemist is there while the
        // farm has a worker. A dungeons.json beside the game may move retiresOnClear
        // onto a dungeon already beaten (check 3's withdrawal) — here the Spear Hall,
        // the farm's one worker — and the hub then lists it alone, the content's
        // tutorial now, until it is beaten again. That clear retires it: the farm has
        // no worker, and the alchemist goes with it. The next first clear of a dungeon
        // that does not retire brings him again, and the summary says he has arrived.
        // The log says what is true both times — the dungeon beaten for the first time
        // is the one the farm counts — and never that it is the first dungeon beaten
        // besides the tutorial: the Spear Hall was. No cooldown, so the Spear Hall's
        // lock lifts as its clear writes it.
        static Tuning Quick(Tuning t) => Farming(t) with { CooldownRunsPerFloor = 0 };
        using var quick = TestContent.UsePremise(tuning: Quick);
        var campaign = TutorialBeaten();
        CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.True(Farm.AlchemistHasArrived(campaign));
        using (TestContent.UsePremise(tuning: Quick, dungeons: TestShop.TutorialMoved))
        {
            Assert.Equal(["spear_hall"], campaign.Listings().Select(listing => listing.Def.Id));
            var retiring = CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
            Assert.Equal((false, true, 0, 0), (retiring.FirstClear, retiring.Retired, retiring.FarmClears, retiring.FarmYield));
            Assert.False(Farm.AlchemistHasArrived(campaign));
            Assert.Null(BetweenRunsHud.FarmLine(retiring));
            Assert.Empty(DistanceRpgGame.FarmLines(retiring));

            var regained = CampaignRuns.Clear(campaign, "long_gallery", FiveDeep);
            Assert.Equal((true, false, 1, PerClear), (regained.FirstClear, regained.Retired, regained.FarmClears, regained.FarmYield));
            Assert.True(Farm.AlchemistHasArrived(campaign));
            Assert.Equal($"A FIRST CLEAR - THE ALCHEMIST HAS ARRIVED - THE FARM YIELDED {PerClear} UNITS", BetweenRunsHud.FarmLine(regained)!.Value.Text);
            Assert.Equal("[Farm] The alchemist has arrived: The Long Gallery, beaten for the first time, is the one dungeon the farm counts",
                DistanceRpgGame.FarmLines(regained)[0]);
        }
    }

    /// <summary>§6.6's loadout: "it carries the same six", a full loadout of maximum-quality potions.</summary>
    private const int Loadout = 6;

    [Fact]
    public void TheShippedYieldIsAboutOneLoadoutOfMaximumPotions()
    {
        // §6.6's target, stated as the rule rather than the number: "a campaign that
        // has cleared everything should produce about one full loadout of
        // maximum-quality potions per run". The master's FarmYieldPerClear is that
        // loadout — six potions at the quality that restores a whole pool from 1 HP,
        // 100 / HealthPotionPercent rounded up (Alchemist.QualityForAll), 6 x 5 = 30
        // at 20 — over the dungeons that do not retire on their clear, rounded down,
        // as tuning.json's comment derives it: 7.5 -> 7, so a full clear yields the
        // target or less, short of it by less than a unit a dungeon. Whatever would
        // re-derive another rate fails here, saying re-derive it: a fifth dungeon in
        // the master, a percent that moves the quality for a whole pool (21 to 24
        // still ask 5; 25 asks 4), or a rate that is not that rounding — 6, or
        // DECISION 7's rounded-up 8.
        //
        // What this pins, and all it pins: the master copy, when the suite runs. A
        // dungeon added to the copy beside the game in a playtest, or to the master
        // without a test run, raises a full clear's yield with no warning — the rate
        // is not re-derived for you, which is what PHASE 6b DECISION 7 says plainly.
        var tuning = ShippedContent.Tuning;
        int perClear = tuning.FarmYieldPerClear;
        int dungeons = ShippedContent.Dungeons.Dungeons.Count(def => !def.RetiresOnClear);
        int target = Loadout * Alchemist.QualityForAllAt(tuning.HealthPotionPercent);
        Assert.True(dungeons > 0, "the master has no dungeon besides the tutorial for the farm to count");
        long fullClear = (long)perClear * dungeons;
        Assert.True(perClear == target / dungeons,
            $"FarmYieldPerClear {perClear} over {dungeons} dungeons yields {fullClear} a full clear, against a loadout of {Loadout} at quality {Alchemist.QualityForAllAt(tuning.HealthPotionPercent)}, {target}, which is {target / dungeons} a dungeon rounded down: re-derive FarmYieldPerClear");
    }

    [Fact]
    public void WhatTheFarmDidIsLoggedAtTheEdge()
    {
        // Logic never logs (LogicPurityTests): what the farm did is handed over in the
        // result and written by the game — the alchemist's arrival, what the run
        // yielded or that it did not count, and the potions that spoiled — nothing
        // before the alchemist, unless potions spoiled. A handler on the campaign's
        // RunEnded beside the enchanter's, wired where it is and let go where it is,
        // read off the IL.
        var campaign = NewCampaign();
        var tutorial = CampaignRuns.Clear(campaign, "tutorial", GoldenSeed);
        Assert.Empty(DistanceRpgGame.FarmLines(tutorial));
        var hall = CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.Equal(
        [
            "[Farm] The alchemist has arrived: The Spear Hall, beaten for the first time, is the one dungeon the farm counts",
            $"[Farm] The run counted: 1 dungeon beaten yields a batch of {PerClear} units",
        ], DistanceRpgGame.FarmLines(hall));
        var gallery = CampaignRuns.Clear(campaign, "long_gallery", FiveDeep);
        Assert.Equal([$"[Farm] The run counted: 2 dungeons beaten yield a batch of {2 * PerClear} units"], DistanceRpgGame.FarmLines(gallery));
        Carrying(campaign, "A", (2, Health(2)), (3, Mana(1)));
        var idle = CampaignRuns.ShallowRetreat(campaign, "knife_warrens");
        Assert.Equal(
        [
            "[Farm] The run did not count: no batch",
            "[Farm] 2 potions spoiled at the run's end: Health Q2, Mana Q1",
        ], DistanceRpgGame.FarmLines(idle));
        Assert.Equal(["[Farm] The run counted: 1 dungeon beaten yields a batch of 1 unit"], DistanceRpgGame.FarmLines(hall with { FirstClear = false, FarmYield = 1 }));
        Assert.Equal(["[Farm] The run counted: 2 dungeons beaten yield nothing"], DistanceRpgGame.FarmLines(gallery with { FirstClear = false, FarmYield = 0 }));
        Assert.Equal(["[Farm] 1 potion spoiled at the run's end: Mana Q4"], DistanceRpgGame.FarmLines(tutorial with { Spoiled = [Mana(4)] }));

        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        static bool Same(MethodBase x, MethodBase y) => x.Module == y.Module && x.MetadataToken == y.MetadataToken;
        var game = typeof(DistanceRpgGame);
        var handler = game.GetMethod("LogTheFarm", Declared)!;
        // Each site counted, not each method: Adopt wires the new campaign and lets the
        // one it replaces go, DropCampaign lets it go.
        Assert.Equal([("Adopt", 2), ("DropCampaign", 1)], Edges(game.Assembly).Where(edge => IsDelegate(edge.Op) && Same(edge.Target, handler))
            .GroupBy(edge => edge.From.Name).Select(sites => (sites.Key, sites.Count())).OrderBy(sites => sites.Key, StringComparer.Ordinal));
        Assert.Contains(CallsIn(handler), called => called.DeclaringType?.Name == "Log" && called.Name == "Info");
        Assert.Contains(CallsIn(handler), called => Same(called, game.GetMethod(nameof(DistanceRpgGame.FarmLines), Declared)!));
        foreach (string letsGo in new[] { "Adopt", "DropCampaign" })
        {
            var calls = CallsIn(game.GetMethod(letsGo, Declared)!).Select(called => called.Name).ToList();
            Assert.True(calls.Count(name => name == "remove_RunEnded") == 3,   // the save request, the enchanter's log and the farm's, each let go
                $"{letsGo} lets {calls.Count(name => name == "remove_RunEnded")} RunEnded handlers go, not the three it wires");
        }
    }
}
