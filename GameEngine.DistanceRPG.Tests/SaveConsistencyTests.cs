using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestSlots;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// A save is raw JSON too (the user's ruling of 2026-09-25, R3(b): "since we are
/// storing in raw json we should probably not trust the fields"). Every count
/// whose larger value would wrap the arithmetic that reads it has a ceiling
/// where it is read, beside its floor, read off <see cref="ContentBounds"/>; and
/// once every field is read on its own, the fields are asked of each other
/// (<see cref="SaveConsistency"/>). A save no play could have written is refused
/// whole as one that cannot be read — set aside, never deleted, the backup
/// tried, the notice naming the field and what contradicts it or its ceiling —
/// and never a crash. Everything play writes still loads, the saves the
/// pre-rework code wrote included, and so does the one state the ruling's
/// example names: a lock that has lifted. Campaigns are the game's own, played
/// through whole runs on the numbers the runs were written against
/// (<see cref="Premise"/>), made current for each test; several cases change the
/// content over it, so the content collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class SaveConsistencyTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private const long FiveDeep = CampaignRuns.FiveDeep;

    private const long GoldenSeed = CampaignRuns.GoldenSeed;

    /// <summary>The HUD's glyphs, upper-cased: what the notice of a save set aside can draw — no apostrophe, quote, brace, backslash or semicolon.</summary>
    private const string Font = @"^[A-Z0-9 .,:\-_!?()/<>\[\]+=#*]+$";

    private static readonly string[] Fixtures = ["pre-rework-mid-run.json", "pre-rework-between-runs.json"];

    /// <summary>Why the run counter stops below <see cref="ContentBounds.MostRuns"/>: what its refusal says after the value and the ceiling.</summary>
    private const string RunsWhy = "at the end of a run the counter, and the lock a clear writes on it, would pass the largest whole number - and no player counts two billion runs";

    /// <summary>Why a weapon's wear stops where it does: what its refusal says after the value and the ceiling.</summary>
    private static readonly string WearWhy =
        $"a weapon banks no more wear than its capacity, which opens on the largest bar a pool opens on, {ContentBounds.Bar} (StartingPool at its most), and grows {ContentBounds.MostCapacityPerBar} at most (WearCapacityPerBar at its most) for every full bar cashed, each bar one wear or more";

    private static string FixtureText(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "saves", name));

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    private static string Save(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    private static LoadedCampaign Load(string text) => SaveGame.Restore(SaveCodec.Read(text), GameContent.Current);

    /// <summary><paramref name="text"/> with <paramref name="edit"/> made to its tree.</summary>
    private static string Edited(string text, Action<JsonObject> edit)
    {
        var tree = JsonNode.Parse(text)!.AsObject();
        edit(tree);
        return tree.ToJsonString();
    }

    /// <summary>The restore of <paramref name="text"/>: refused, and where.</summary>
    private static SaveFormatException Refused(string text) => Assert.Throws<SaveFormatException>(() => Load(text));

    /// <summary><paramref name="text"/> restores: nothing in it is refused.</summary>
    private static LoadedCampaign Restores(string text, string what)
    {
        try
        {
            return Load(text);
        }
        catch (SaveFormatException refused)
        {
            throw new Xunit.Sdk.XunitException($"{what}: refused at {refused.Message}");
        }
    }

    /// <summary><paramref name="campaign"/> saves, the save restores, and the campaign it restores writes the very same text: nothing refused, nothing lost.</summary>
    private static void Loads(CampaignState campaign, string what)
    {
        string text = Save(campaign);
        Assert.Equal(text, Save(Restores(text, what).Campaign));
    }

    /// <summary>A refusal, where it names and what it says there, and every row of it drawn in the HUD's font.</summary>
    private static void RefusedAt(string text, string where, string problem)
    {
        var refused = Refused(text);
        Assert.Equal((where, problem), (refused.Where, refused.Problem));
        Assert.All(NoticeText.Wrap(refused.Message), row => Assert.Matches(Font, row));
    }

    private static JsonObject CampaignNode(JsonObject tree) => tree["campaign"]!.AsObject();

    private static JsonObject Last(JsonObject tree) => tree["campaign"]!["lastResult"]!.AsObject();

    private static JsonObject VisitNode(JsonObject tree) => tree["campaign"]!["visit"]!.AsObject();

    private static JsonObject FloorNode(JsonObject tree, int index) => VisitNode(tree)["floors"]![index]!.AsObject();

    private static JsonObject MemberNode(JsonObject tree, int member) => tree["campaign"]!["party"]![member]!.AsObject();

    private static JsonObject WeaponNode(JsonObject tree, int member, int slot) => MemberNode(tree, member)["inventory"]![slot]!.AsObject();

    private static JsonObject DepositNode(JsonObject tree, int index) => tree["campaign"]!["enchanter"]![index]!.AsObject();

    private static JsonObject ReportNode(JsonObject tree, int index) => Last(tree)["services"]![index]!.AsObject();

    /// <summary>The list index of the boss's row among the enemies of <paramref name="floor"/> in <paramref name="text"/>'s visit.</summary>
    private static int BossRow(string text, int floor)
    {
        var rows = FloorNode(JsonNode.Parse(text)!.AsObject(), floor)["enemies"]!.AsArray();
        return Enumerable.Range(0, rows.Count).Single(k => rows[k]!["boss"]!.GetValue<bool>());
    }

    // ── Campaigns, played ────────────────────────────────────────────────────

    /// <summary>The tutorial beaten and retired, and the Spear Hall cleared five floors deep and locked: its clear the last run.</summary>
    private static CampaignState HallCleared()
    {
        var campaign = NewCampaign();
        CampaignRuns.Clear(campaign, "tutorial", GoldenSeed);
        var hall = CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Assert.Equal((2, 5, (int?)5), (campaign.RunCounter, hall.DeepestFloor, hall.BossFloorRevealed));
        Assert.Equal(campaign.LockedUntilRun("spear_hall"), hall.LockedUntilRun);
        Assert.Equal(["tutorial"], campaign.RetiredInOrder);
        return campaign;
    }

    /// <summary>
    /// <see cref="HallCleared"/>, and then the Spear Hall's cooldown played out: deep
    /// retreats down the Long Gallery, each counting as a mercy, until the counter
    /// has passed the lock — which stays in the campaign, lifted.
    /// </summary>
    private static CampaignState LockPlayedOut()
    {
        var campaign = HallCleared();
        int until = campaign.LockedUntilRun("spear_hall");
        while (campaign.RunCounter <= until)
            CampaignRuns.DeepRetreat(campaign, "long_gallery");
        Assert.Contains(("spear_hall", until), campaign.LockedInOrder);
        Assert.True(until < campaign.RunCounter, $"the lock {until}, the counter {campaign.RunCounter}");
        return campaign;
    }

    /// <summary>A wipe on the first floor of <paramref name="dungeonId"/>, the boss standing: a weapon taken off a corpse into B's bag, XP gained in two pools, and every member down.</summary>
    private static RunResult WipeWithASpoil(CampaignState campaign, string dungeonId)
    {
        var visit = campaign.Enter(dungeonId, FiveDeep);
        visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        var corpse = visit.Current.Enemies[0];
        var spoil = new Drop(TestWeapons.Get("hatchet"), DefeatCount: 0, WasUniqueRoll: false);
        Assert.NotEqual(Pickup.BagFull, Pickup.Take(campaign, corpse, spoil, campaign.Party[1], visit));
        campaign.Party[0].Credit(new XpCredit(XpPool.Weapon, WeaponClass.Dagger, 40));
        campaign.Party[2].Credit(new XpCredit(XpPool.Health, null, 9));
        foreach (var member in campaign.Party)
            member.Alive = false;
        var result = campaign.EndRun(RunEnding.Wiped);
        Assert.Equal((RunOutcome.Death, false), (result.Outcome, result.BossDefeated));
        Assert.NotEmpty(result.SpoilsLost);
        Assert.NotEmpty(result.Clawbacks);
        return result;
    }

    /// <summary>
    /// A wipe at the bottom of <paramref name="dungeonId"/> with its boss down —
    /// killed the boss, died on the way out — which clears and locks the dungeon as
    /// a boss kill does (§6.3), and counts.
    /// </summary>
    private static RunResult WipeWithTheBossDown(CampaignState campaign, string dungeonId)
    {
        var visit = campaign.Enter(dungeonId, FiveDeep);
        var turns = visit.OpenTurns(campaign.Party, CampaignRuns.Ten);
        while (visit.Current.DownStairs is not null)
            turns = CampaignRuns.Down(visit, campaign.Party);
        visit.Current.Boss!.Alive = false;
        visit.RecordBossDefeated();
        var result = CampaignRuns.Wipe(campaign, turns);
        Assert.Equal((RunOutcome.Death, true, true), (result.Outcome, result.BossDefeated, result.ServiceTick));
        Assert.NotNull(result.LockedUntilRun);
        return result;
    }

    /// <summary><paramref name="hits"/> hits' wear on <paramref name="weapon"/>, through the one door a landed hit banks it through (<see cref="Wear.Accrue"/>).</summary>
    private static Weapon Worn(Weapon weapon, int hits)
    {
        for (int i = 0; i < hits; i++)
            Wear.Accrue(weapon);
        return weapon;
    }

    // ── What play writes ─────────────────────────────────────────────────────

    [Fact]
    public void WhatPlayCanWriteAlwaysLoads()
    {
        // Both saves the pre-rework code wrote hold every ceiling and every check.
        foreach (string name in Fixtures)
            Restores(FixtureText(name), name);

        // A campaign after each outcome, saved between runs: nothing played yet; a
        // clear that retires; a clear that locks; a retreat that counts as a mercy,
        // and one that counts nothing; a wipe with the boss standing, a spoil lost
        // and XP clawed back; and a wipe with the boss down, which clears and locks.
        var campaign = NewCampaign();
        Loads(campaign, "a new campaign");
        CampaignRuns.Clear(campaign, "tutorial", GoldenSeed);
        Loads(campaign, "the tutorial retired");
        CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
        Loads(campaign, "the Spear Hall cleared and locked");
        CampaignRuns.DeepRetreat(campaign, "long_gallery");
        Loads(campaign, "a retreat that counts as a mercy");
        CampaignRuns.ShallowRetreat(campaign, "long_gallery");
        Loads(campaign, "a retreat that counts nothing");
        WipeWithASpoil(campaign, "knife_warrens");
        Loads(campaign, "a wipe with the boss standing");
        WipeWithTheBossDown(campaign, "quiver_pits");
        Loads(campaign, "a wipe with the boss down");

        // Mid-run, the Flagship: the boss down and its drop taken, a dummy farmed,
        // drops lying and taken, spoils in a bag and on a floor, XP gained since the door.
        Loads(VisitSaveTests.Flagship().Campaign, "the Flagship");

        // A lock the counter has passed; and check 12's everyday case — the Spear
        // Hall re-entered once its lock has lifted and retreated from, then entered
        // again and wiped on with the boss standing: each of those runs locks
        // nothing, so its summary names no lock while the campaign's lifted lock
        // stands, and each loads.
        var lifted = LockPlayedOut();
        Loads(lifted, "a lock the counter has passed");
        var retreat = CampaignRuns.ShallowRetreat(lifted, "spear_hall");
        Assert.Equal(("spear_hall", (int?)null), (retreat.DungeonId, retreat.LockedUntilRun));
        Loads(lifted, "a retreat from a dungeon whose lock has lifted");
        var wipe = CampaignRuns.Wipe(lifted, lifted.Enter("spear_hall", FiveDeep).OpenTurns(lifted.Party, CampaignRuns.Ten));
        Assert.Equal(("spear_hall", false, (int?)null), (wipe.DungeonId, wipe.BossDefeated, wipe.LockedUntilRun));
        Assert.True(lifted.LockedUntilRun("spear_hall") < lifted.RunCounter);
        Loads(lifted, "a wipe with the boss standing in a dungeon whose lock has lifted");

        // Check 3, withdrawn: dungeons.json beside the exe may move retiresOnClear
        // onto a dungeon already cleared and locked (a retiring dungeon pins its
        // floor), and its next clear retires it and leaves the old lock where it was:
        // retired and still locked, which play wrote, and which loads.
        int oldLock = lifted.LockedUntilRun("spear_hall");
        using (TestContent.UsePremise(dungeons: data => new DungeonsData(data.Dungeons.Select(def => def.Id switch
               {
                   "spear_hall" => def with { RetiresOnClear = true, BossFloorMin = 5, BossFloorMax = 5 },
                   "tutorial" => def with { RetiresOnClear = false },
                   _ => def,
               }).ToArray())))
        {
            var retiring = CampaignRuns.Clear(lifted, "spear_hall", FiveDeep);
            Assert.Equal((true, (int?)null), (retiring.Retired, retiring.LockedUntilRun));
            Assert.True(lifted.IsRetired("spear_hall"));
            Assert.Contains(("spear_hall", oldLock), lifted.LockedInOrder);
            Loads(lifted, "a dungeon retired with its old lock standing");
        }

        // Check 12's exception, the early offer: with every dungeon that is not
        // retired locked — here the content holds the tutorial and the Spear Hall
        // alone — the Spear Hall is offered early, and entering it lifts its lock to
        // the counter while the last run's summary still names the lock its clear
        // wrote. Saved at the entry, and a turn in on the open floor: both load.
        using (TestContent.UsePremise(dungeons: data => new DungeonsData(data.Dungeons.Where(def => def.Id is "tutorial" or "spear_hall").ToArray())))
        {
            var early = NewCampaign();
            CampaignRuns.Clear(early, "tutorial", GoldenSeed);
            var cleared = CampaignRuns.Clear(early, "spear_hall", FiveDeep);
            Assert.Equal("spear_hall", early.EarlyOffer());
            var script = new VisitScript(early);
            early.Enter("spear_hall", FiveDeep);
            Assert.Equal(early.RunCounter, early.LockedUntilRun("spear_hall"));
            Assert.NotEqual(cleared.LockedUntilRun, early.LockedUntilRun("spear_hall"));
            Assert.Equal(cleared.LockedUntilRun, early.LastResult!.LockedUntilRun);
            Loads(early, "the early offer entered");
            script.Open();
            script.EndTurn();
            Loads(early, "the early offer a turn in");
        }

        // Wear (Phase 6a.1): a weapon the party wields keeps what it has banked
        // wherever it goes — in a hand, full, in a bag, a spoil kept or lost, a
        // weapon put down on a floor — and a capacity a tuning lowered below it
        // since: each is what play writes, and each loads.
        var worn = NewCampaign();
        CampaignRuns.Clear(worn, "tutorial", GoldenSeed);
        Worn(worn.Party[0].Inventory[0]!, 12);
        Assert.True(Worn(worn.Party[1].Inventory[0]!, 40).WornFull);
        Loads(worn, "worn weapons in hand, one full");

        var visit = worn.Enter("spear_hall", FiveDeep);
        visit.OpenTurns(worn.Party, CampaignRuns.Ten);
        var gear = Worn(worn.Party[3].Inventory[1]!, 7);   // D's second staff, cast with before it went back in the bag
        Assert.Same(gear, visit.Current.Leave(worn.Party[3], 1).Weapon);
        Loads(worn, "a worn weapon put down on a floor, mid-run");
        var kept = new Drop(TestWeapons.Get("hatchet"), DefeatCount: 0, WasUniqueRoll: false);
        Assert.NotEqual(Pickup.BagFull, Pickup.Take(worn, visit.Current.Enemies[0], kept, worn.Party[2], visit));
        Worn(kept.Weapon, 5);
        var exited = CampaignRuns.Exit(worn);
        Assert.Equal(5, Assert.Single(exited.SpoilsKept).Wear);
        Loads(worn, "a worn spoil kept");

        var wiping = worn.Enter("knife_warrens", FiveDeep);
        var wipeTurns = wiping.OpenTurns(worn.Party, CampaignRuns.Ten);
        var lost = new Drop(TestWeapons.Get("hatchet"), DefeatCount: 0, WasUniqueRoll: false);
        Assert.NotEqual(Pickup.BagFull, Pickup.Take(worn, wiping.Current.Enemies[0], lost, worn.Party[1], wiping));
        Worn(lost.Weapon, 3);
        var wiped = CampaignRuns.Wipe(worn, wipeTurns);
        Assert.Equal(3, Assert.Single(wiped.SpoilsLost).Wear);
        Loads(worn, "a worn spoil lost to a wipe");

        using (TestContent.UsePremise(tuning: t => t with { StartingPool = 20 }))
        {
            Assert.True(worn.Party[1].Inventory[0]!.Wear > worn.Party[1].Inventory[0]!.WearCapacity);
            Loads(worn, "wear over a capacity a tuning lowered since");
        }

        // The enchanter (Phase 6a.3a), on the ruling's numbers: a shop with a worked
        // deposit, a fresh one and an unworn Refine, and a last run that worked two;
        // then a run that did not count, reporting every deposit stalled — a fresh
        // one at 0 runs worked; then more deposits than the enchanter works on, so a
        // run that counts leaves the oldest stalled, a fresh one among them at 0; and
        // a finished service back in the stable.
        using (TestContent.UsePremise(tuning: TestShop.Ruled))
        {
            var shop = TestShop.Shop();
            Assert.Equal(0, shop.AtTheEnchanter[0].WearAtDeposit);   // an unworn Refine
            Loads(shop, "a shop working two, with an unworn refine and a fresh deposit");

            var idle = TestShop.NotCounted(shop);
            Assert.Equal([(true, 1), (true, 1), (true, 0)], idle.Services!.Select(report => (report.Stalled, report.RunsWorked)));
            Loads(shop, "a run that did not count, with deposits in the shop");

            TestShop.Give(shop, "D", 2, TestShop.Worn("tower_guard", wear: 30));
            TestShop.Give(shop, "D", 3, TestShop.Worn("tower_guard", wear: 30));
            Enchanter.Deposit(shop, new WeaponPlace("D", 2), Service.Refine);
            Enchanter.Deposit(shop, new WeaponPlace("D", 3), Service.Enchant, "cold");
            Loads(shop, "five in the shop");
            var worked = TestShop.Counted(shop);
            Assert.Equal([(true, 1), (true, 1), (true, 0), (false, 1), (false, 1)], worked.Services!.Select(report => (report.Stalled, report.RunsWorked)));
            Assert.Equal(2, worked.Services!.Count(report => report.Finished));
            Loads(shop, "a run that counted, a fresh deposit stalled at 0 and two finished");
        }

        // And a dungeons.json beside the game that moves retiresOnClear, since the
        // deposits were made, onto a dungeon not beaten: the shop play filled once
        // the tutorial retired still loads (check 30 asks the retired list, not
        // which dungeon today's content retires), and its services still finish.
        string filled;
        using (TestContent.UsePremise(tuning: TestShop.Ruled))
            filled = Save(TestShop.Shop());
        using (TestContent.UsePremise(tuning: TestShop.Ruled, dungeons: TestShop.TutorialMoved))
        {
            var moved = Restores(filled, "a shop, retiresOnClear moved since onto a dungeon not beaten").Campaign;
            Assert.False(moved.IsRetired(GameContent.Current.Dungeons.Tutorial.Id));
            Assert.Equal(3, moved.AtTheEnchanter.Count);
            Loads(moved, "a shop, retiresOnClear moved since, saved again");
            TestShop.Counted(moved);
            Loads(moved, "a shop worked after retiresOnClear moved");
        }
    }

    [Fact]
    public void ALiftedLockIsNoContradiction()
    {
        // The ruling's own example — "a lock ending before the current run count" —
        // is play's everyday state, so it is never refused (settled.md R3(b), flagged
        // for the user to confirm). A clear writes a lock, and nothing ever removes
        // one: a lock that has lifted stays in the campaign, reading a run already
        // reached (CampaignState.LockedInOrder), since a lifted lock already reads as
        // open (IsSelectable: the counter at or past it) and the early offer compares
        // locks lifted or not; and the early offer itself writes the counter as a
        // lock. Refusing it would refuse every campaign that played a cooldown out.
        var campaign = LockPlayedOut();
        int until = campaign.LockedUntilRun("spear_hall");
        Assert.True(campaign.IsSelectable(GameContent.Current.Dungeons["spear_hall"]));
        string text = Save(campaign);
        var restored = Restores(text, "a lifted lock").Campaign;
        Assert.Equal(campaign.LockedInOrder, restored.LockedInOrder);
        Assert.Contains(("spear_hall", until), restored.LockedInOrder);
        Assert.True(restored.IsSelectable(GameContent.Current.Dungeons["spear_hall"]));
        Assert.Equal(text, Save(restored));

        // A lock at the counter itself is a clear under a tuning of no cooldown: it
        // lifts as it is written, and the save loads.
        using (TestContent.UsePremise(tuning: t => t with { CooldownRunsPerFloor = 0 }))
        {
            var quick = NewCampaign();
            CampaignRuns.Clear(quick, "tutorial", GoldenSeed);
            var hall = CampaignRuns.Clear(quick, "spear_hall", FiveDeep);
            Assert.Equal(quick.RunCounter, hall.LockedUntilRun);
            Loads(quick, "a lock at the counter");
        }

        // The form of the example no play writes is a last run's own lock before its
        // own counter: a clear locks from the counter on — the counter plus the runs
        // a floor, never less — so that summary contradicts itself, and is refused.
        var cleared = HallCleared();
        RefusedAt(Edited(Save(cleared), tree => Last(tree)["lockedUntilRun"] = cleared.RunCounter - 1), "campaign.lastResult.lockedUntilRun",
            $"{cleared.RunCounter - 1} is below the run counter, {cleared.RunCounter}: a clear locks its dungeon from the counter on, never before it");
    }

    // ── Contradictions ───────────────────────────────────────────────────────

    [Fact]
    public void ACampaignThatContradictsItselfIsRefusedNamingWhere()
    {
        // Consistent saves, each field of which is in range: the tutorial cleared
        // and retired and the Spear Hall cleared and locked, its clear the last run;
        // the tutorial's clear alone; and a wipe after them, a spoil lost and XP
        // clawed back. Each edit below makes one check the first to fail — checks 1
        // to 14 but 3, which is withdrawn — and the refusal names the field and, in
        // its first clause, what contradicts it, every row in the font of the notice
        // that says so.
        var hallCampaign = HallCleared();
        string hall = Save(hallCampaign);
        int counter = hallCampaign.RunCounter;
        int locked = hallCampaign.LockedUntilRun("spear_hall");
        Restores(hall, "the Spear Hall cleared");

        var tutorialCampaign = NewCampaign();
        CampaignRuns.Clear(tutorialCampaign, "tutorial", GoldenSeed);
        string tutorial = Save(tutorialCampaign);
        Restores(tutorial, "the tutorial cleared");

        var wipedCampaign = HallCleared();
        var wipe = WipeWithASpoil(wipedCampaign, "long_gallery");
        string wiped = Save(wipedCampaign);
        Restores(wiped, "the wipe");
        var clawed = wipe.Clawbacks[0];

        JsonNode AWeapon() => WeaponNode(JsonNode.Parse(hall)!.AsObject(), 0, 0).DeepClone();

        (string Text, Action<JsonObject> Edit, string Where, string Problem)[] contradictions =
        [
            // 1. A retired dungeon never cleared.
            (hall, tree => CampaignNode(tree)["cleared"] = new JsonArray("spear_hall"),
                "campaign.retired[0]", "tutorial is retired and was never cleared: a dungeon retires on its clear, which records it cleared"),
            // 2. A locked dungeon never cleared.
            (hall, tree => CampaignNode(tree)["cleared"] = new JsonArray("tutorial"),
                "campaign.locks[0].dungeonId", "spear_hall is locked and was never cleared: only a clear writes a lock"),
            // 4. No run ended, and runs counted, or a dungeon cleared.
            (hall, tree => CampaignNode(tree)["lastResult"] = null,
                "campaign.runCounter", $"the counter stands at {counter} and no run has ended: only the end of a run moves the counter, and it writes the last result"),
            (hall, tree => { CampaignNode(tree)["lastResult"] = null; CampaignNode(tree)["runCounter"] = 0; },
                "campaign.cleared[0]", "spear_hall is cleared and no run has ended: only the end of a run clears a dungeon, and it writes the last result"),
            // 5. The outcome against the boss.
            (hall, tree => Last(tree)["outcome"] = nameof(RunOutcome.Retreat),
                "campaign.lastResult.outcome", "Retreat with the boss down: a party that walks out with the boss down has made a boss kill"),
            (hall, tree => Last(tree)["bossDefeated"] = false,
                "campaign.lastResult.outcome", "BossKill with the boss standing: a run is a boss kill only when the party walks out with the boss down"),
            // 6. The tick.
            (hall, tree => Last(tree)["serviceTick"] = false,
                "campaign.lastResult.serviceTick", "not counted with the boss down: a run that downs its boss always counts"),
            (wiped, tree => Last(tree)["serviceTick"] = true,
                "campaign.lastResult.serviceTick", "counted with the boss standing and no mercy earned: a run counts for its boss or for its mercy, and for nothing else"),
            (wiped, tree => Last(tree)["mercyTick"] = true,
                "campaign.lastResult.serviceTick", "not counted with a mercy tick: the mercy tick is a count"),
            (hall, tree => Last(tree)["mercyTick"] = true,
                "campaign.lastResult.mercyTick", "a mercy tick with the boss down: the mercy is for a run that failed deep, never for a win"),
            // 7. A clear locks or retires, a failure neither, and a lock is from the counter on.
            (hall, tree => Last(tree)["lockedUntilRun"] = null,
                "campaign.lastResult.lockedUntilRun", "no lock and not retired, with the boss down: a clear locks its dungeon, or retires it"),
            (hall, tree => Last(tree)["retired"] = true,
                "campaign.lastResult.retired", "retired and locked both: a clear retires a dungeon in place of locking it"),
            (wiped, tree => Last(tree)["retired"] = true,
                "campaign.lastResult.retired", "retired with the boss standing: only a clear retires a dungeon"),
            (wiped, tree => Last(tree)["lockedUntilRun"] = locked,
                "campaign.lastResult.lockedUntilRun", $"{locked} with the boss standing: only a clear locks a dungeon"),
            (hall, tree => Last(tree)["lockedUntilRun"] = counter - 1,
                "campaign.lastResult.lockedUntilRun", $"{counter - 1} is below the run counter, {counter}: a clear locks its dungeon from the counter on, never before it"),
            // 8. The boss floor is the deepest floor, told on reaching it; and no floor is past the deepest a boss waits on.
            (hall, tree => Last(tree)["bossFloorRevealed"] = 4,
                "campaign.lastResult.bossFloorRevealed", "4 is not the deepest floor the party reached, 5: the boss floor is told only on reaching it, and nothing lies below it"),
            (hall, tree => Last(tree)["bossFloorRevealed"] = null,
                "campaign.lastResult.bossFloorRevealed", "not revealed with the boss down: the boss is fought on its own floor, so a party that downed it reached it"),
            (hall, tree => { Last(tree)["deepestFloor"] = ContentBounds.DeepestBossFloor + 1; Last(tree)["bossFloorRevealed"] = ContentBounds.DeepestBossFloor + 1; },
                "campaign.lastResult.deepestFloor",
                $"{ContentBounds.DeepestBossFloor + 1} is above {ContentBounds.DeepestBossFloor}: no dungeon goes deeper than the deepest floor a boss waits on (BossFloorMax at its most)"),
            // 9. Spoils and XP: kept on the way out, lost and clawed back by a wipe alone.
            (hall, tree => Last(tree)["spoilsLost"] = new JsonArray(AWeapon()),
                "campaign.lastResult.spoilsLost", "1 lost on a run that did not wipe: only a wipe takes spoils out of the bags"),
            (hall, tree => Last(tree)["clawbacks"] = JsonNode.Parse("""[{ "memberId": "A", "pool": "Dagger", "before": 10, "after": 5 }]"""),
                "campaign.lastResult.clawbacks", "1 clawed back on a run that did not wipe: only a wipe claws XP back"),
            (wiped, tree => Last(tree)["spoilsKept"] = new JsonArray(AWeapon()),
                "campaign.lastResult.spoilsKept", "1 kept on a run that wiped: a wipe loses every spoil still in a bag"),
            // 10. A claw-back takes back, once a pool.
            (wiped, tree => Last(tree)["clawbacks"]![0]!["after"] = clawed.Before + 1,
                "campaign.lastResult.clawbacks[0].after", $"{clawed.Before + 1} is above what the pool held, {clawed.Before}: a claw-back takes XP back, and never hands it out"),
            (wiped, tree => { var list = Last(tree)["clawbacks"]!.AsArray(); list.Add(list[0]!.DeepClone()); },
                $"campaign.lastResult.clawbacks[{wipe.Clawbacks.Count}]", $"the {clawed.Pool} pool of {clawed.MemberId} is clawed back twice: a wipe claws each pool back once"),
            // 11. The summary's counter is the campaign's.
            (hall, tree => Last(tree)["runCounter"] = counter - 1,
                "campaign.lastResult.runCounter", $"{counter - 1} where the campaign has counted {counter}: the counter moves only at the end of a run, which writes it here too"),
            // 12. A clear writes one lock to both.
            (hall, tree => Last(tree)["lockedUntilRun"] = locked + 1,
                "campaign.lastResult.lockedUntilRun",
                $"{locked + 1} where the campaign locks spear_hall until run {locked}: a clear writes one lock to both, and only the early offer moves it after"),
            (hall, tree => CampaignNode(tree)["locks"] = new JsonArray(),
                "campaign.lastResult.lockedUntilRun", $"{locked} and the campaign holds no lock on spear_hall: a clear writes one lock to both"),
            // 13. A clear records its dungeon cleared, and a retiring clear retired.
            (tutorial, tree => { CampaignNode(tree)["cleared"] = new JsonArray(); CampaignNode(tree)["retired"] = new JsonArray(); },
                "campaign.lastResult.dungeonId", "tutorial is not cleared and its last run downed the boss: a clear records its dungeon cleared"),
            (tutorial, tree => CampaignNode(tree)["retired"] = new JsonArray(),
                "campaign.lastResult.dungeonId", "tutorial is not retired and its last run retired it: a retiring clear records it retired"),
            // 14. One colour a member.
            (hall, tree => MemberNode(tree, 1)["colorIndex"] = 0,
                "campaign.party[1].colorIndex", "0 is already the colour of member A: a member is coloured by its place in the roster, one colour each"),
        ];
        foreach (var (text, edit, where, problem) in contradictions)
            RefusedAt(Edited(text, edit), where, problem);

        // Check 8 at the shared ceiling: a last run as deep as the deepest floor a
        // boss waits on loads — ContentBounds.DeepestBossFloor, the ceiling a
        // visit's boss floor is held to as well (ContentBoundsTests).
        Restores(Edited(hall, tree =>
        {
            Last(tree)["deepestFloor"] = ContentBounds.DeepestBossFloor;
            Last(tree)["bossFloorRevealed"] = ContentBounds.DeepestBossFloor;
        }), "a summary at the deepest floor");
    }

    [Fact]
    public void AVisitThatContradictsItsCampaignIsRefused()
    {
        // Checks 15 to 19, the visit in progress against its campaign and itself:
        // entered as a run is entered, played as one plays. A campaign that retired
        // the tutorial and entered the Spear Hall, saved at the entry, and the
        // Flagship, saved on floor 4 with the boss down.
        var entry = NewCampaign();
        CampaignRuns.Clear(entry, "tutorial", GoldenSeed);
        entry.Enter("spear_hall", FiveDeep);
        string entered = Save(entry);
        int counter = entry.RunCounter;
        Restores(entered, "the Spear Hall entered");

        // 15. Entering refuses a retired dungeon, and a locked one bar the early offer.
        RefusedAt(Edited(entered, tree =>
            {
                CampaignNode(tree)["cleared"] = new JsonArray("spear_hall", "tutorial");
                CampaignNode(tree)["retired"] = new JsonArray("spear_hall", "tutorial");
            }),
            "campaign.visit.dungeonId", "spear_hall is retired: a retired dungeon is gone for good, and no run enters it");
        JsonNode LockAt(int until) => JsonNode.Parse($$"""[{ "dungeonId": "spear_hall", "lockedUntilRun": {{until}} }]""")!;
        RefusedAt(Edited(entered, tree =>
            {
                CampaignNode(tree)["cleared"] = new JsonArray("spear_hall", "tutorial");
                CampaignNode(tree)["locks"] = LockAt(counter + 1);
            }),
            "campaign.visit.dungeonId",
            $"spear_hall is locked until run {counter + 1} and the counter stands at {counter}: a locked dungeon is entered only as the early offer, which lifts its lock to the counter");
        Restores(Edited(entered, tree =>
        {
            CampaignNode(tree)["cleared"] = new JsonArray("spear_hall", "tutorial");
            CampaignNode(tree)["locks"] = LockAt(counter);
        }), "the early offer's lock, lifted to the counter");

        // 19, before the boss fell: nothing is handed over while the dungeon resurrects.
        RefusedAt(Edited(entered, tree => FloorNode(tree, 0)["enemies"]![0]!["dropTaken"] = true),
            "campaign.visit.floors[0].enemies[0].dropTaken", "taken with the boss standing: nothing is handed over while the dungeon resurrects");

        // The Flagship: the party earned before the door, and A since.
        var flagship = VisitSaveTests.Flagship();
        string text = Save(flagship.Campaign);
        var party = flagship.Campaign.Party;
        Restores(text, "the Flagship");
        int dagger = party[0].WeaponXp[WeaponClass.Dagger];
        int throwing = party[1].WeaponXp[WeaponClass.Throwing];
        const string OnlyAdds = "a visit only adds XP, and a wipe claws it back only as the run ends";

        // 16. What a pool held at the door is no more than it holds now: each edit
        // puts one more at the door than the member holds — a class B never used among them.
        Assert.Equal(0, throwing);
        RefusedAt(Edited(text, tree => VisitNode(tree)["entrySnapshot"]!["members"]![0]!["weaponXp"]!["Dagger"] = dagger + 1),
            "campaign.visit.entrySnapshot.members[0].weaponXp.Dagger", $"{dagger + 1} is more than member A holds now, {dagger}: {OnlyAdds}");
        RefusedAt(Edited(text, tree => VisitNode(tree)["entrySnapshot"]!["members"]![1]!["weaponXp"]!["Throwing"] = throwing + 1),
            "campaign.visit.entrySnapshot.members[1].weaponXp.Throwing", $"{throwing + 1} is more than member B holds now, {throwing}: {OnlyAdds}");
        RefusedAt(Edited(text, tree => VisitNode(tree)["entrySnapshot"]!["members"]![2]!["hpXp"] = party[2].HpXp + 1),
            "campaign.visit.entrySnapshot.members[2].hpXp", $"{party[2].HpXp + 1} is more than member C holds now, {party[2].HpXp}: {OnlyAdds}");
        RefusedAt(Edited(text, tree => VisitNode(tree)["entrySnapshot"]!["members"]![3]!["manaXp"] = party[3].ManaXp + 1),
            "campaign.visit.entrySnapshot.members[3].manaXp", $"{party[3].ManaXp + 1} is more than member D holds now, {party[3].ManaXp}: {OnlyAdds}");

        // 17. The promise is a stack a floor at most.
        int depth = flagship.Visit.BossFloor;
        RefusedAt(Edited(text, tree => VisitNode(tree)["promisedStacks"] = depth + 1),
            "campaign.visit.promisedStacks", $"{depth + 1} is above the boss floor, {depth}: a boss drop is promised a stack a floor at most");

        // 18. The boss row stands exactly while the visit has it standing: here it is
        // down, and a boss never rises.
        int boss = BossRow(text, depth - 1);
        RefusedAt(Edited(text, tree => FloorNode(tree, depth - 1)["enemies"]![boss]!["body"]!["alive"] = true),
            $"campaign.visit.floors[{depth - 1}].enemies[{boss}].body.alive",
            "the boss stands and the visit has it down: its death is what records it down, and a boss never rises");

        // 19. A drop taken lies on a corpse: the fang on floor 4 whose drop was taken, stood back up.
        var fang = FloorNode(JsonNode.Parse(text)!.AsObject(), 3)["enemies"]!.AsArray();
        int taken = Enumerable.Range(0, fang.Count).First(k => fang[k]!["dropTaken"]!.GetValue<bool>());
        Assert.Equal(1, fang[taken]!["spawnIndex"]!.GetValue<int>());
        RefusedAt(Edited(text, tree => FloorNode(tree, 3)["enemies"]![taken]!["body"]!["alive"] = true),
            $"campaign.visit.floors[3].enemies[{taken}].dropTaken", "taken from a standing row: a drop lies only on a corpse, and nothing rises once the boss is down");

        // 18, the other way: at the bottom with the boss standing, its row fallen
        // while the visit has it standing — its fall records it down at once.
        var bottom = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        while (bottom.Floor.DownStairs is not null)
            bottom.Down();
        string standing = Save(bottom.Campaign);
        Restores(standing, "the bottom reached, the boss standing");
        int lair = bottom.Visit.BossFloor - 1;
        int standingBoss = BossRow(standing, lair);
        RefusedAt(Edited(standing, tree => FloorNode(tree, lair)["enemies"]![standingBoss]!["body"]!["alive"] = false),
            $"campaign.visit.floors[{lair}].enemies[{standingBoss}].body.alive", "the boss has fallen and the visit has it standing: its fall records it down at once");
    }

    // ── Ceilings ─────────────────────────────────────────────────────────────

    [Fact]
    public void AFieldPastItsCeilingIsRefused_AtItsCeilingItLoads()
    {
        // Every count whose larger value would wrap the arithmetic that reads it has
        // a ceiling where it is read, beside its floor, off ContentBounds — a clamp's
        // ceiling or a rule, never the tuning in hand. At it the save loads; one past
        // it is refused at the field's own path, before any cross-check, the value
        // and the ceiling in the first clause. Each edit keeps the rest whole: a
        // forged stack raised with its live one, a run counter with its summary's.
        // (The boss floor's is S3's, ContentBoundsTests.)
        var campaign = HallCleared();
        campaign.Party[2].Credit(new XpCredit(XpPool.Health, null, 9));
        string hall = Save(campaign);
        var tree0 = JsonNode.Parse(hall)!.AsObject();
        int hpXp = campaign.Party[2].HpXp;
        Assert.True(hpXp > 0);

        // A's weapon, its first forged modifier and one it was not forged with; D's staff and its innate.
        var forgedTable = WeaponNode(tree0, 0, 0)["forged"]!.AsObject();
        string forged = forgedTable.First().Key;
        int forgedStacks = forgedTable[forged]!.GetValue<int>();
        string unforged = Enum.GetNames<ModifierType>().First(name => !forgedTable.ContainsKey(name));
        Assert.Equal(("staff_of_renewal", "regeneration"), (WeaponNode(tree0, 3, 0)["id"]!.GetValue<string>(), WeaponNode(tree0, 3, 0)["enchantments"]![0]!["id"]!.GetValue<string>()));
        var ids = GameContent.Current.Enchantments.All.Where(def => !def.Unique).Select(def => def.Id).ToArray();
        JsonArray Entries(int count) => new(Enumerable.Range(0, count)
            .Select(i => (JsonNode?)JsonNode.Parse($$"""{ "id": "{{ids[i % ids.Length]}}", "tier": 1, "xp": 0 }"""))
            .ToArray());

        int deepestForge = ContentBounds.DeepestForge, mostAcquired = ContentBounds.MostAcquired, deepestStack = ContentBounds.DeepestStack;
        int deepestTier = ContentBounds.DeepestTier, mostEnchantments = ContentBounds.MostEnchantments, bar = ContentBounds.Bar;
        int perBar = ContentBounds.MostCapacityPerBar;
        int mostRuns = ContentBounds.MostRuns - 1;
        void Runs(JsonObject tree, int counter, int summary, int summaryLock, int campaignLock)
        {
            CampaignNode(tree)["runCounter"] = counter;
            Last(tree)["runCounter"] = summary;
            Last(tree)["lockedUntilRun"] = summaryLock;
            CampaignNode(tree)["locks"]![0]!["lockedUntilRun"] = campaignLock;
        }
        string AcquiredWhy(int stacks) => $"acquisition stops at the {stacks} forged plus the most headroom any tuning gives, {mostAcquired} (AcquiredHeadroom at its most)";

        (string What, string Text, Action<JsonObject> AtCeiling, Action<JsonObject> Past, string Where, string Problem)[] ceilings =
        [
            // 0a. A forged stack: no forge goes deeper than MaxForged's clamp ceiling.
            ("a forged stack", hall,
                tree => { WeaponNode(tree, 0, 0)["forged"]![forged] = deepestForge; WeaponNode(tree, 0, 0)["modifiers"]![forged] = deepestForge; },
                tree => { WeaponNode(tree, 0, 0)["forged"]![forged] = deepestForge + 1; WeaponNode(tree, 0, 0)["modifiers"]![forged] = deepestForge + 1; },
                $"campaign.party[0].inventory[0].forged.{forged}", $"{deepestForge + 1} is above {deepestForge}: no forge goes deeper (MaxForged at its most)"),
            // 0b. A live stack: its forged count plus AcquiredHeadroom's clamp ceiling — forged, and not.
            ("a live stack", hall,
                tree => WeaponNode(tree, 0, 0)["modifiers"]![forged] = forgedStacks + mostAcquired,
                tree => WeaponNode(tree, 0, 0)["modifiers"]![forged] = forgedStacks + mostAcquired + 1,
                $"campaign.party[0].inventory[0].modifiers.{forged}", $"{forgedStacks + mostAcquired + 1} is above {forgedStacks + mostAcquired}: {AcquiredWhy(forgedStacks)}"),
            ("a live stack never forged", hall,
                tree => WeaponNode(tree, 0, 0)["modifiers"]![unforged] = mostAcquired,
                tree => WeaponNode(tree, 0, 0)["modifiers"]![unforged] = mostAcquired + 1,
                $"campaign.party[0].inventory[0].modifiers.{unforged}", $"{mostAcquired + 1} is above {mostAcquired}: {AcquiredWhy(0)}"),
            // 0c. A member's innate: no deeper than any stack grows.
            ("an innate stack", hall,
                tree => MemberNode(tree, 1)["innate"] = JsonNode.Parse($$"""{ "Block": {{deepestStack}} }"""),
                tree => MemberNode(tree, 1)["innate"] = JsonNode.Parse($$"""{ "Block": {{deepestStack + 1}} }"""),
                "campaign.party[1].innate.Block",
                $"{deepestStack + 1} is above {deepestStack}: no stack grows deeper than the deepest forge plus the most headroom (MaxForged and AcquiredHeadroom at their most)"),
            // 0d. A tier: uncapped by design; the bound is where its arithmetic stops being whole.
            ("a tier", hall,
                tree => WeaponNode(tree, 3, 0)["enchantments"]![0]!["tier"] = deepestTier,
                tree => WeaponNode(tree, 3, 0)["enchantments"]![0]!["tier"] = deepestTier + 1,
                "campaign.party[3].inventory[0].enchantments[0].tier",
                $"{deepestTier + 1} is above {deepestTier}: tier is uncapped and play never climbs near this, but past it the lock and the levels of the entry would pass the largest whole number"),
            // 0e. The entries a weapon holds: EnchantmentSlots' clamp ceiling.
            ("a weapon's entries", hall,
                tree => WeaponNode(tree, 0, 0)["enchantments"] = Entries(mostEnchantments),
                tree => WeaponNode(tree, 0, 0)["enchantments"] = Entries(mostEnchantments + 1),
                "campaign.party[0].inventory[0].enchantments",
                $"{mostEnchantments + 1} entries, more than the {mostEnchantments} a weapon can hold: none arrives with more than {ContentValidator.MostArrivingEnchantments}, and the enchanter fills {mostEnchantments} slots at most"),
            // 0f. The run counter, and the last run's copy of it, each at the ceiling
            // with the rest. A lock keeps its floor alone
            // (ALockKeepsItsFloorAlone_SoEveryLockPlayWritesLoads).
            ("the run counter", hall,
                tree => Runs(tree, mostRuns, mostRuns, mostRuns, mostRuns),
                tree => Runs(tree, mostRuns + 1, mostRuns, mostRuns, mostRuns),
                "campaign.runCounter", $"{mostRuns + 1} is above {mostRuns}: {RunsWhy}"),
            ("the last run's counter", hall,
                tree => Runs(tree, mostRuns, mostRuns, mostRuns, mostRuns),
                tree => Runs(tree, mostRuns, mostRuns + 1, mostRuns, mostRuns),
                "campaign.lastResult.runCounter", $"{mostRuns + 1} is above {mostRuns}: {RunsWhy}"),
            // 0i. A weapon's wear: no capacity any tuning in range gives it, the largest
            // bar plus the most a bar adds for each wear cashed (Phase 6a.1).
            ("a weapon's wear", hall,
                tree => WeaponNode(tree, 0, 0)["wear"] = bar,
                tree => WeaponNode(tree, 0, 0)["wear"] = bar + 1,
                "campaign.party[0].inventory[0].wear", $"{bar + 1} is above {bar}: {WearWhy}"),
            ("a weapon's wear, a bar cashed", hall,
                tree => { WeaponNode(tree, 0, 0)["wearCashed"] = 30; WeaponNode(tree, 0, 0)["wear"] = bar + 30 * perBar; },
                tree => { WeaponNode(tree, 0, 0)["wearCashed"] = 30; WeaponNode(tree, 0, 0)["wear"] = bar + 30 * perBar + 1; },
                "campaign.party[0].inventory[0].wear", $"{bar + 30 * perBar + 1} is above {bar + 30 * perBar}: {WearWhy}"),
            // 0h. A member's hit points: the largest bar plus a point for each HP XP.
            ("a member's hit points", hall,
                tree => MemberNode(tree, 2)["body"]!["hp"] = bar + hpXp,
                tree => MemberNode(tree, 2)["body"]!["hp"] = bar + hpXp + 1,
                "campaign.party[2].body.hp",
                $"{bar + hpXp + 1} is above {bar + hpXp}: a member holds no more hit points than the largest bar a pool opens on, {bar} (StartingPool at its most), plus a point for each HP XP"),
        ];
        foreach (var (what, text, atCeiling, past, where, problem) in ceilings)
        {
            Restores(Edited(text, atCeiling), $"{what} at its ceiling");
            RefusedAt(Edited(text, past), where, problem);
        }

        // The member's ceiling is summed as a long: HP XP at its own ceiling, the
        // count sanity bound, cannot wrap it below the hit points it allows.
        Restores(Edited(hall, tree =>
        {
            MemberNode(tree, 2)["hpXp"] = ContentBounds.MostCount;
            MemberNode(tree, 2)["body"]!["hp"] = bar + ContentBounds.MostCount;
        }), "hit points under the ceiling of the most HP XP");

        // 0h. A row's hit points: the largest bar plus what its revivals added — the
        // Flagship's halberd, standing on floor 4, given a revival's gain.
        string flagship = Save(VisitSaveTests.Flagship().Campaign);
        Assert.True(FloorNode(JsonNode.Parse(flagship)!.AsObject(), 3)["enemies"]![0]!["body"]!["alive"]!.GetValue<bool>());
        string Halberd(int hp) => Edited(flagship, tree =>
        {
            var row = FloorNode(tree, 3)["enemies"]![0]!;
            row["revivalMaxHp"] = 7;
            row["body"]!["hp"] = hp;
        });
        Restores(Halberd(bar + 7), "a row's hit points at its ceiling");
        RefusedAt(Halberd(bar + 8), "campaign.visit.floors[3].enemies[0].body.hp",
            $"{bar + 8} is above {bar + 7}: a row holds no more hit points than the largest bar a pool opens on, {bar} (StartingPool at its most), plus what its revivals added");

        // Both saves the pre-rework code wrote lie under every ceiling.
        foreach (string name in Fixtures)
            Restores(FixtureText(name), name);

        // And each ceiling is a clamp's ceiling or a rule, read off the schema: a
        // clamp moved there moves the ceiling with it.
        static int Top(string key) => TuningSchema.Find(key) switch
        {
            IntKey k => k.Max,
            TableKey k => k.Max,
            _ => throw new Xunit.Sdk.XunitException($"the schema has no number key {key}"),
        };
        Assert.Equal(Top(nameof(Tuning.AcquiredHeadroom)), ContentBounds.MostAcquired);
        Assert.Equal(int.MaxValue / (Top(nameof(Tuning.ApplyPercent)) * Top(nameof(Tuning.ArcanePotency))), ContentBounds.DeepestTier);
        Assert.Equal(int.MaxValue - Top(nameof(Tuning.BossFloorMax)) * Top(nameof(Tuning.CooldownRunsPerFloor)), ContentBounds.MostRuns);
        Assert.Equal(Top(nameof(Tuning.EnchantmentSlots)), ContentBounds.MostEnchantments);
        Assert.Equal(Top(nameof(Tuning.EnchantmentSlots)) + 1, ContentBounds.MostServiceRuns);
        Assert.Equal(Top(nameof(Tuning.EnchanterWorksOn)), ContentBounds.MostWorked);
        Assert.Equal(Top(nameof(Tuning.WearCapacityPerBar)), ContentBounds.MostCapacityPerBar);
        Assert.Equal(int.MaxValue / (2 * Top(nameof(Tuning.EffectPerLevel))), ContentBounds.MostLevels);
        Assert.Equal(Top(nameof(Tuning.Offset)) + Top(nameof(Tuning.PerStack)) * ContentBounds.DeepestStack, ContentBounds.MostHeldShots);
    }

    [Fact]
    public void ACountPlayGrowsStopsShortOfItsWrap_ABodyStandsOnTheMap_AndABudgetNests()
    {
        // The audit's gaps (Phase 5.S): every count play grows a step at a time is
        // held to the sanity bound below which the next step cannot wrap
        // (ContentBounds.MostCount); a status's levels below where their effect
        // wraps, a hold below the largest Overwatch value; a row's spawn index
        // inside its floor's rows; a float finite — the next save could not write
        // infinity — a body on the map, and a member's four budgets as a turn
        // leaves them. At each ceiling the save loads; one past it is refused at
        // the field's own path.
        const string countWhy = "a count play grows a turn or a gain at a time never nears it, and past it the next step would pass the largest whole number";
        const string offTheMap = "that way, and a body stands on it";
        int most = ContentBounds.MostCount;
        string hall = Save(HallCleared());
        string mid = Save(VisitSaveTests.Flagship().Campaign);
        int rows = FloorNode(JsonNode.Parse(mid)!.AsObject(), 3)["enemies"]!.AsArray().Count;
        float width = MapGenerator.Cols * GameConstants.Tile, height = MapGenerator.Rows * GameConstants.Tile;
        float longest = ContentBounds.Distance * 1.5f;
        void Budgets(JsonObject tree, float whole, float cap, float left)
        {
            var member = MemberNode(tree, 0);
            member["budgetBeforeMire"] = whole;
            member["effectiveMax"] = cap;
            member["distLeft"] = left;
        }
        void Turn(JsonObject tree, int turn)
        {
            VisitNode(tree)["turn"] = turn;
            if (VisitNode(tree)["carry"] is JsonObject carry)
                carry["turn"] = turn;   // the turn's counters are this turn's
        }

        (string What, string Text, Action<JsonObject> AtCeiling, Action<JsonObject> Past, string Where, string Problem)[] ceilings =
        [
            ("HP XP", hall, tree => MemberNode(tree, 1)["hpXp"] = most, tree => MemberNode(tree, 1)["hpXp"] = most + 1,
                "campaign.party[1].hpXp", $"{most + 1} is above {most}: {countWhy}"),
            ("weapon XP", hall, tree => MemberNode(tree, 1)["weaponXp"]!["Dagger"] = most, tree => MemberNode(tree, 1)["weaponXp"]!["Dagger"] = most + 1,
                "campaign.party[1].weaponXp.Dagger", $"{most + 1} is above {most}: {countWhy}"),
            ("an entry's XP", hall,
                tree => WeaponNode(tree, 3, 0)["enchantments"]![0]!["xp"] = most,
                tree => WeaponNode(tree, 3, 0)["enchantments"]![0]!["xp"] = most + 1,
                "campaign.party[3].inventory[0].enchantments[0].xp", $"{most + 1} is above {most}: {countWhy}"),
            // Wear cashed grows a deposit at a time, and wear a hit at a time (Phase 6a.1):
            // counts, each at the sanity bound, the wear with enough cashed that its
            // own capacity ceiling is no nearer.
            ("wear cashed", hall,
                tree => WeaponNode(tree, 0, 0)["wearCashed"] = most,
                tree => WeaponNode(tree, 0, 0)["wearCashed"] = most + 1,
                "campaign.party[0].inventory[0].wearCashed", $"{most + 1} is above {most}: {countWhy}"),
            ("wear", hall,
                tree => { WeaponNode(tree, 0, 0)["wearCashed"] = most; WeaponNode(tree, 0, 0)["wear"] = most; },
                tree => { WeaponNode(tree, 0, 0)["wearCashed"] = most; WeaponNode(tree, 0, 0)["wear"] = most + 1; },
                "campaign.party[0].inventory[0].wear", $"{most + 1} is above {most}: {countWhy}"),
            ("negative wear", hall, tree => { }, tree => WeaponNode(tree, 0, 0)["wear"] = -1,
                "campaign.party[0].inventory[0].wear", "-1 is below 0"),
            ("mana", hall, tree => MemberNode(tree, 3)["body"]!["mana"] = most, tree => MemberNode(tree, 3)["body"]!["mana"] = most + 1,
                "campaign.party[3].body.mana", $"{most + 1} is above {most}: {countWhy}"),
            ("a held shot", hall,
                tree => MemberNode(tree, 0)["body"]!["heldShots"] = ContentBounds.MostHeldShots,
                tree => MemberNode(tree, 0)["body"]!["heldShots"] = ContentBounds.MostHeldShots + 1,
                "campaign.party[0].body.heldShots",
                $"{ContentBounds.MostHeldShots + 1} is above {ContentBounds.MostHeldShots}: a hold is the Overwatch value of the wielder, no more than its offset plus a stack worth for the deepest stack (Offset, PerStack, MaxForged and AcquiredHeadroom at their most)"),
            ("a status's levels", hall,
                tree => MemberNode(tree, 0)["body"]!["statuses"] = JsonNode.Parse($$"""[ { "type": "Mire", "element": null, "levels": {{ContentBounds.MostLevels}} } ]"""),
                tree => MemberNode(tree, 0)["body"]!["statuses"] = JsonNode.Parse($$"""[ { "type": "Mire", "element": null, "levels": {{ContentBounds.MostLevels + 1}} } ]"""),
                "campaign.party[0].body.statuses[0].levels",
                $"{ContentBounds.MostLevels + 1} is above {ContentBounds.MostLevels}: levels are uncapped and land a hit at a time, but past it the levels times their largest effect (EffectPerLevel at its most) would pass the largest whole number"),
            ("x", hall, tree => MemberNode(tree, 0)["body"]!["x"] = width, tree => MemberNode(tree, 0)["body"]!["x"] = width + 1,
                "campaign.party[0].body.x", $"{width + 1} is off the map, 0 to {width}: a map is {MapGenerator.Cols} tiles of {GameConstants.Tile} {offTheMap}"),
            ("y", hall, tree => MemberNode(tree, 0)["body"]!["y"] = 0, tree => MemberNode(tree, 0)["body"]!["y"] = -1,
                "campaign.party[0].body.y", $"-1 is off the map, 0 to {height}: a map is {MapGenerator.Rows} tiles of {GameConstants.Tile} {offTheMap}"),
            ("an infinite x", hall, tree => { }, tree => MemberNode(tree, 0)["body"]!["x"] = JsonNode.Parse("1e39"),
                "campaign.party[0].body.x", "infinity is no number play writes: a position or a budget is a finite number"),
            ("the whole a turn cut from", hall,
                tree => Budgets(tree, longest, 0, 0), tree => Budgets(tree, longest + 1, 0, 0),
                "campaign.party[0].budgetBeforeMire",
                $"{longest + 1} is above {longest}: a turn opens on the budget plus the bank, at most half the budget again, and no budget is longer than tuning.json measures (MovementBudget at its most)"),
            ("a cap past its whole", hall, tree => Budgets(tree, 100, 100, 0), tree => Budgets(tree, 100, 101, 0),
                "campaign.party[0].effectiveMax", "101 is above 100: a cap is the whole this turn cut it from less what Mire took (budgetBeforeMire)"),
            ("movement left past the cap", hall, tree => Budgets(tree, 100, 90, 90), tree => Budgets(tree, 100, 90, 91),
                "campaign.party[0].distLeft", "91 is above 90: movement left is what is left of the cap this turn (effectiveMax)"),
            ("a negative bank", hall, tree => MemberNode(tree, 0)["savedMovement"] = 0, tree => MemberNode(tree, 0)["savedMovement"] = -1,
                "campaign.party[0].savedMovement", "-1 is below 0: movement is spent to nothing at most"),
            ("the visit's turn", mid, tree => Turn(tree, most), tree => Turn(tree, most + 1),
                "campaign.visit.turn", $"{most + 1} is above {most}: {countWhy}"),
            ("the dice rolled", mid, tree => VisitNode(tree)["diceRolled"] = most, tree => VisitNode(tree)["diceRolled"] = most + 1,
                "campaign.visit.diceRolled", $"{most + 1} is above {most}: {countWhy}"),
            ("a row's defeats", mid,
                tree => FloorNode(tree, 3)["enemies"]![0]!["defeatCount"] = most,
                tree => FloorNode(tree, 3)["enemies"]![0]!["defeatCount"] = most + 1,
                "campaign.visit.floors[3].enemies[0].defeatCount", $"{most + 1} is above {most}: {countWhy}"),
            ("a row's spawn index", mid, tree => { }, tree => FloorNode(tree, 3)["enemies"]![0]!["spawnIndex"] = rows,
                "campaign.visit.floors[3].enemies[0].spawnIndex",
                $"{rows} is above {rows - 1}: a floor numbers its rows from 0 in the order it placed them, the boss last, and never loses one"),
            ("a row off the map", mid, tree => { }, tree => FloorNode(tree, 3)["enemies"]![0]!["body"]!["x"] = -1e30,
                "campaign.visit.floors[3].enemies[0].body.x", $"{-1e30f} is off the map, 0 to {width}: a map is {MapGenerator.Cols} tiles of {GameConstants.Tile} {offTheMap}"),
        ];
        foreach (var (what, text, atCeiling, past, where, problem) in ceilings)
        {
            Restores(Edited(text, atCeiling), $"{what} at its ceiling");
            RefusedAt(Edited(text, past), where, problem);
        }

        // The enchanter's counts (Phase 6a.3a): the deposits made and what a deposit
        // cashed are counts play grows a deposit at a time, the latter with its
        // weapon's cash beside it — and at the most a weapon's cash can be, what a
        // deposit cashed is held below the count's own ceiling by check 28, so that
        // its take-back gives back a weapon that loads; the runs a service takes are
        // the count plus one, no more than a weapon holding the most takes, a
        // deposit's and a report's alike; a report's runs worked no more than its
        // runs; and an ordinal and the runs worked are never below 0.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        string shop = Save(TestShop.Shop());
        int cashedAtMost = (int)((ContentBounds.Bar + (long)ContentBounds.MostCapacityPerBar * most) / (ContentBounds.MostCapacityPerBar + 1));
        int runs = ContentBounds.MostServiceRuns;
        string runsWhy = $"a service takes the count of the weapon plus one runs, and no weapon holds more than {ContentBounds.MostEnchantments} (EnchantmentSlots at its most)";
        var ids = new[] { "vampiric", "cold", "flaming" };
        void Held(JsonObject tree, int deposit, int count, int serviceRuns)
        {
            DepositNode(tree, deposit)["weapon"]!["enchantments"] = new JsonArray(Enumerable.Range(0, count)
                .Select(i => (JsonNode?)JsonNode.Parse($$"""{ "id": "{{ids[i % ids.Length]}}", "tier": 1, "xp": 0 }""")).ToArray());
            DepositNode(tree, deposit)["weapon"]!["forgedEnchantments"] = 0;
            DepositNode(tree, deposit)["runs"] = serviceRuns;
        }
        (string What, Action<JsonObject> AtCeiling, Action<JsonObject> Past, string Where, string Problem)[] shopCeilings =
        [
            ("the deposits made", tree => CampaignNode(tree)["depositsMade"] = most, tree => CampaignNode(tree)["depositsMade"] = most + 1,
                "campaign.depositsMade", $"{most + 1} is above {most}: {countWhy}"),
            ("what a deposit cashed",
                tree => { DepositNode(tree, 2)["weapon"]!["wearCashed"] = most; DepositNode(tree, 2)["wearAtDeposit"] = cashedAtMost; },
                tree => { DepositNode(tree, 2)["weapon"]!["wearCashed"] = most; DepositNode(tree, 2)["wearAtDeposit"] = most + 1; },
                "campaign.enchanter[2].wearAtDeposit", $"{most + 1} is above {most}: {countWhy}"),
            ("a deposit's runs", tree => Held(tree, 2, runs - 1, runs), tree => Held(tree, 2, runs - 1, runs + 1),
                "campaign.enchanter[2].runs", $"{runs + 1} is above {runs}: {runsWhy}"),
            ("a report's runs", tree => ReportNode(tree, 0)["runs"] = runs, tree => ReportNode(tree, 0)["runs"] = runs + 1,
                "campaign.lastResult.services[0].runs", $"{runs + 1} is above {runs}: {runsWhy}"),
            ("a report's runs worked", tree => ReportNode(tree, 0)["runsWorked"] = 2, tree => ReportNode(tree, 0)["runsWorked"] = 3,
                "campaign.lastResult.services[0].runsWorked", "3 is above 2: a service works no more runs than it takes (runs)"),
            ("a negative ordinal", tree => { }, tree => DepositNode(tree, 0)["ordinal"] = -1, "campaign.enchanter[0].ordinal", "-1 is below 0"),
            ("negative runs worked", tree => { }, tree => DepositNode(tree, 0)["runsWorked"] = -1, "campaign.enchanter[0].runsWorked", "-1 is below 0"),
        ];
        foreach (var (what, atCeiling, past, where, problem) in shopCeilings)
        {
            Restores(Edited(shop, atCeiling), $"{what} at its ceiling");
            RefusedAt(Edited(shop, past), where, problem);
        }

        // What a deposit cashed at the most check 28 lets it, taken back: the weapon
        // it gives back holds that as its wear, under what it had cashed before, and
        // loads. One more, and it would not: refused at the deposit.
        var atMost = Restores(Edited(shop, tree => { DepositNode(tree, 2)["weapon"]!["wearCashed"] = most; DepositNode(tree, 2)["wearAtDeposit"] = cashedAtMost; }), "what a deposit cashed at its most").Campaign;
        var guard = atMost.AtTheEnchanter[2];
        Enchanter.TakeBack(atMost, guard);
        Assert.Equal((cashedAtMost, most - cashedAtMost), (guard.Weapon.Wear, guard.Weapon.WearCashed));
        Loads(atMost, "the weapon a deposit at its most gives back");
        Assert.Equal("campaign.enchanter[2].wearAtDeposit",
            Refused(Edited(shop, tree => { DepositNode(tree, 2)["weapon"]!["wearCashed"] = most; DepositNode(tree, 2)["wearAtDeposit"] = cashedAtMost + 1; })).Where);
    }

    [Fact]
    public void AQueueThatContradictsItselfIsRefused()
    {
        // Checks 21–25, 27 and 28 (Phase 6a.3a): a shop and a last run's reports each
        // of whose fields could be true while together they could not. A deposit takes
        // the next ordinal and cashes the whole pool, which a take-back gives back as
        // a weapon that loads; its runs are the count plus one, and the tick that
        // works the last brings it back; an Enchant names an enchantment the campaign
        // has met, and a refinement none, in the shop and in a report of it; a
        // stalled report is short of its last run, and a worked one was worked, on a
        // run that counted, which works the newest and one at least, no more of them
        // than the enchanter works on; and no more reports than deposits made. The
        // shop: A's staff refined
        // unworn (0, a run worked of 2), B's staff enchanted Vampiric (1, a run worked
        // of 2), C's Tower Guard refined (2, fresh); the last run worked 0 and 1.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        string shop = Save(TestShop.Shop());
        Restores(shop, "the shop");

        (Action<JsonObject> Edit, string Where, string Problem)[] contradictions =
        [
            // 21. The next ordinal, each its own, below the count.
            (tree => DepositNode(tree, 1)["ordinal"] = 0, "campaign.enchanter[1].ordinal",
                "0 follows 0: the shop is in deposit order, and each deposit takes the next ordinal, never one reused"),
            (tree => CampaignNode(tree)["depositsMade"] = 2, "campaign.enchanter[2].ordinal",
                "2 is not below the deposits made, 2: a deposit takes the next ordinal, and the count moves past it"),
            // 22. The whole pool cashed, and nothing worn in the shop.
            (tree => DepositNode(tree, 0)["weapon"]!["wear"] = 3, "campaign.enchanter[0].weapon.wear",
                "3 worn in the shop: the deposit cashed it all, and nothing wears a weapon there"),
            // 23. The last run worked brings it back.
            (tree => DepositNode(tree, 0)["runsWorked"] = 2, "campaign.enchanter[0].runsWorked",
                "2 of 2 runs worked in the shop: the tick that works the last run brings the weapon back"),
            // 24. The count plus one.
            (tree => DepositNode(tree, 2)["runs"] = 2, "campaign.enchanter[2].runs",
                "2 for a weapon holding 0 enchantments: a service takes the count plus one runs, and the count does not move in the shop"),
            // 25. What an Enchant buys, and what a refinement does not.
            (tree => DepositNode(tree, 1)["enchantment"] = null, "campaign.enchanter[1].enchantment",
                "missing on an Enchant: a deposit to enchant names the enchantment it buys"),
            (tree => DepositNode(tree, 1)["enchantment"] = "shocking", "campaign.enchanter[1].enchantment",
                "shocking is not among the seen: the enchanter offers only what the campaign has met"),
            (tree => DepositNode(tree, 0)["enchantment"] = "vampiric", "campaign.enchanter[0].enchantment",
                "vampiric named on a Refine: a refinement attaches nothing"),
            // 27. The reports.
            (tree => { ReportNode(tree, 0)["stalled"] = true; ReportNode(tree, 0)["runsWorked"] = 2; }, "campaign.lastResult.services[0].runsWorked",
                "2 of 2 runs on a stalled report: a stalled deposit is still in the shop, short of its last run"),
            (tree => ReportNode(tree, 0)["runsWorked"] = 0, "campaign.lastResult.services[0].runsWorked",
                "0 of 2 runs on a worked report: the run worked it a run at least"),
            (tree => { Last(tree)["serviceTick"] = false; Last(tree)["mercyTick"] = false; }, "campaign.lastResult.services[0].stalled",
                "false on a run that did not count: a run that did not count works nothing, and reports every deposit stalled"),
            (tree => Last(tree)["services"] = new JsonArray(Enumerable.Range(0, ContentBounds.MostWorked + 1).Select(_ => ReportNode(tree, 0).DeepClone()).ToArray()),
                "campaign.lastResult.services", $"{ContentBounds.MostWorked + 1} worked, more than the {ContentBounds.MostWorked} the enchanter works on at most (EnchanterWorksOn at its most)"),
            // 27. A report says what its deposit bought, as the deposit does (25).
            (tree => ReportNode(tree, 1)["enchantment"] = null, "campaign.lastResult.services[1].enchantment",
                "missing on an Enchant: a report says what its deposit bought, and a deposit to enchant names its enchantment"),
            (tree => ReportNode(tree, 1)["enchantment"] = "shocking", "campaign.lastResult.services[1].enchantment",
                "shocking is not among the seen: its deposit bought what the campaign had met, and the campaign forgets nothing it has met"),
            (tree => ReportNode(tree, 0)["enchantment"] = "vampiric", "campaign.lastResult.services[0].enchantment",
                "vampiric named on a Refine: a refinement attaches nothing"),
            // 27. A run that counted works the newest, one at least, in deposit order.
            (tree => ReportNode(tree, 1)["stalled"] = true, "campaign.lastResult.services[1].stalled",
                "true on the newest deposit, on a run that counted: a run that counts works the newest, and one at least"),
            (tree => Last(tree)["services"]!.AsArray().Insert(1, Stalled(ReportNode(tree, 0))), "campaign.lastResult.services[1].stalled",
                "true after a worked report, on a run that counted: a run that counts works the newest, and the reports are in deposit order"),
            // 27. No more reports than deposits made.
            (tree => { Last(tree)["services"]!.AsArray().Insert(0, Stalled(ReportNode(tree, 0))); Last(tree)["services"]!.AsArray().Insert(0, Stalled(ReportNode(tree, 0))); },
                "campaign.lastResult.services", "4 reports and 3 deposits made: a report speaks of a deposit, and each deposit is its own"),
            // 28. What the deposit cashed, the weapon has cashed — and a take-back of it gives back a weapon that loads.
            (tree => DepositNode(tree, 1)["wearAtDeposit"] = 196, "campaign.enchanter[1].wearAtDeposit",
                "196 is above what the weapon has ever cashed, 195: the deposit cashed it"),
            (tree => { DepositNode(tree, 1)["weapon"]!["wearCashed"] = 20_000; DepositNode(tree, 1)["wearAtDeposit"] = 20_000; }, "campaign.enchanter[1].wearAtDeposit",
                $"20000 is above {ContentBounds.Bar}, the most a weapon that had cashed 0 banks: the deposit cashed what the weapon banked, and a take-back puts it back"),
        ];
        foreach (var (edit, where, problem) in contradictions)
            RefusedAt(Edited(shop, edit), where, problem);

        // At their edges the same fields load: a stalled report one run short, a
        // worked one finished, a deposit cashing all the weapon ever cashed — and
        // one cashing the most a weapon that had cashed nothing banks, whose
        // take-back gives back a weapon that loads.
        Restores(Edited(shop, tree => { ReportNode(tree, 0)["stalled"] = true; ReportNode(tree, 0)["runsWorked"] = 1; }), "a stalled report a run short");
        Restores(Edited(shop, tree => ReportNode(tree, 0)["runsWorked"] = 2), "a worked report finished");
        Restores(Edited(shop, tree => DepositNode(tree, 1)["wearAtDeposit"] = 195), "a deposit of all the wear ever cashed");
        int bar = ContentBounds.Bar;
        var full = Restores(Edited(shop, tree => { DepositNode(tree, 1)["weapon"]!["wearCashed"] = bar; DepositNode(tree, 1)["wearAtDeposit"] = bar; }), "a deposit of the largest bar").Campaign;
        var staff = full.AtTheEnchanter[1];
        Enchanter.TakeBack(full, staff);
        Assert.Equal((bar, 0), (staff.Weapon.Wear, staff.Weapon.WearCashed));
        Loads(full, "the weapon a deposit of the largest bar gives back");

        static JsonNode Stalled(JsonObject report)
        {
            var stalled = report.DeepClone().AsObject();
            stalled["stalled"] = true;
            return stalled;
        }
    }

    [Fact]
    public void AnEnchantThatWouldCompleteIntoAnUnloadableWeaponIsRefused()
    {
        // Check 29 (Phase 6a.3a): what an Enchant attaches as it finishes is the
        // weapon's next entry, and must load — so an Enchant on a weapon already
        // holding the most a weapon can is none play queued (its own gate and the
        // ceiling on attaching refuse it). A refinement of the same weapon attaches
        // nothing, and loads.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        string shop = Save(TestShop.Shop());
        int most = ContentBounds.MostEnchantments;
        var ids = new[] { "vampiric", "cold", "flaming" };
        JsonArray Entries() => new(Enumerable.Range(0, most).Select(i => (JsonNode?)JsonNode.Parse($$"""{ "id": "{{ids[i % ids.Length]}}", "tier": 1, "xp": 0 }""")).ToArray());
        void Full(JsonObject tree, int index)
        {
            DepositNode(tree, index)["weapon"]!["enchantments"] = Entries();
            DepositNode(tree, index)["weapon"]!["forgedEnchantments"] = 0;
            DepositNode(tree, index)["runs"] = most + 1;
        }
        RefusedAt(Edited(shop, tree => Full(tree, 1)), "campaign.enchanter[1].weapon.enchantments",
            $"{most} entries on a deposit to enchant: the one it attaches would be more than the {most} a weapon can hold");
        Restores(Edited(shop, tree => Full(tree, 2)), "a refinement of a weapon holding the most");
    }

    [Fact]
    public void AQueueBeforeTheTutorialIsRetiredIsRefused()
    {
        // Check 30: the enchanter opens when the tutorial is beaten (§6.3), which
        // retires it, and retired is never undone — so a shop holding anything, a
        // run's end reporting on one, or a deposit ever made, with no dungeon
        // retired, is none play wrote, each refused at its own path. Asked of the
        // retired list alone, with no content: a dungeons.json that moves
        // retiresOnClear since refuses nothing play wrote (WhatPlayCanWriteAlwaysLoads).
        // Nothing at the enchanter and nothing ever deposited, before then, is every
        // new campaign's.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        string shop = Save(TestShop.Shop());
        const string why = "the enchanter opens once the tutorial is beaten, which retires it, and takes nothing before";
        static void Standing(JsonObject tree) => CampaignNode(tree)["retired"] = new JsonArray();
        (Action<JsonObject> Edit, string Where, string Problem)[] refusals =
        [
            (Standing, "campaign.enchanter", $"3 in the shop and no dungeon is retired: {why}"),
            (tree => { Standing(tree); CampaignNode(tree)["enchanter"] = null; }, "campaign.lastResult.services", $"2 reported at the enchanter and no dungeon is retired: {why}"),
            (tree => { Standing(tree); CampaignNode(tree)["enchanter"] = null; Last(tree)["services"] = null; }, "campaign.depositsMade", $"3 made and no dungeon is retired: {why}"),
        ];
        foreach (var (edit, where, problem) in refusals)
            RefusedAt(Edited(shop, edit), where, problem);
        Restores(Edited(shop, tree => { Standing(tree); CampaignNode(tree)["enchanter"] = null; Last(tree)["services"] = null; CampaignNode(tree)["depositsMade"] = 0; }),
            "nothing at the enchanter and nothing deposited, the tutorial standing");
    }

    [Fact]
    public void AReportNamingNoWeaponIsRefused()
    {
        // A report names its weapon and its enchantment by id, and the summary names
        // them every frame, on the hub and at a run's end: each must be the
        // content's, refused at its own path, so drawing never throws. So must a
        // deposit's weapon and enchantment, and each service be one by name. The runs
        // a report says are held where they are read.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        string shop = Save(TestShop.Shop());
        (Action<JsonObject> Edit, string Where)[] refusals =
        [
            (tree => ReportNode(tree, 0)["weaponId"] = "no_such_weapon", "campaign.lastResult.services[0].weaponId"),
            (tree => ReportNode(tree, 1)["enchantment"] = "no_such_enchantment", "campaign.lastResult.services[1].enchantment"),
            (tree => ReportNode(tree, 1)["service"] = "Polish", "campaign.lastResult.services[1].service"),
            (tree => ReportNode(tree, 0)["runs"] = 0, "campaign.lastResult.services[0].runs"),
            (tree => ReportNode(tree, 0)["runsWorked"] = -1, "campaign.lastResult.services[0].runsWorked"),
            (tree => Last(tree)["services"]!.AsArray().Insert(0, null), "campaign.lastResult.services[0]"),
            (tree => DepositNode(tree, 0)["weapon"]!["id"] = "no_such_weapon", "campaign.enchanter[0].weapon.id"),
            (tree => DepositNode(tree, 1)["enchantment"] = "no_such_enchantment", "campaign.enchanter[1].enchantment"),
            (tree => DepositNode(tree, 2)["service"] = "Polish", "campaign.enchanter[2].service"),
            (tree => DepositNode(tree, 2)["runs"] = 0, "campaign.enchanter[2].runs"),
            (tree => DepositNode(tree, 1)["wearAtDeposit"] = 0, "campaign.enchanter[1].wearAtDeposit"),
            (tree => CampaignNode(tree)["enchanter"]!.AsArray().Insert(1, null), "campaign.enchanter[1]"),
        ];
        foreach (var (edit, where) in refusals)
            Assert.Equal(where, Refused(Edited(shop, edit)).Where);
        Assert.Equal("0 is below 1: an enchantment is bought with a deposit of FirstEnchantmentWear or more, and no tuning prices one below 1",
            Refused(Edited(shop, tree => DepositNode(tree, 1)["wearAtDeposit"] = 0)).Problem);
    }

    [Fact]
    public void AQueuedEnchantmentTheRelationsNowRefuseStillLoads()
    {
        // "The item was legal when it was made" (settled.md, R3(b)): a deposit queued
        // under one restricted.json loads under another that now lists its
        // enchantment among the never-rolled, or excludes it beside what the weapon
        // carries — the file beside the game is JSON-master data an edit may move —
        // and its service finishes as it was bought.
        string shop;
        using (TestContent.UsePremise(tuning: TestShop.Ruled))
            shop = Save(TestShop.Shop());
        var restricted = ShippedContent.Restricted;
        foreach (var edited in new[]
                 {
                     restricted with { NeverRolled = [.. restricted.NeverRolled, "vampiric"] },
                     restricted with { Excludes = [.. restricted.Excludes, ["vampiric", "regeneration"]] },
                 })
        {
            using (TestContent.UsePremise(tuning: TestShop.Ruled, restricted: edited))
            {
                var loaded = Restores(shop, "a queued enchantment the relations now refuse").Campaign;
                TestShop.Counted(loaded);
                var back = loaded.Stable.Single(weapon => weapon.Enchantments.Count == 2);
                Assert.Equal(["regeneration", "vampiric"], back.Enchantments.Select(entry => entry.Id));
            }
        }
    }

    [Fact]
    public void AQueuedInertKindIsRefused()
    {
        // Phase 6a.3b: the enchanter never offers an enchantment of a kind declared
        // and inert (LootTable.IsInert) — and whether a kind does anything is the
        // code's, which no file beside the game edits — so no deposit play made can
        // name one. A shop whose Enchant names Echoing, met, is refused at the
        // deposit's enchantment; the same shop naming an element it met loads. (What
        // restricted.json says of a queued id is not asked: the case above.)
        string shop;
        using (TestContent.UsePremise(tuning: TestShop.Ruled))
            shop = Save(TestShop.Shop());
        Assert.Equal("vampiric", JsonNode.Parse(shop)!["campaign"]!["enchanter"]![1]!["enchantment"]!.GetValue<string>());

        string Buying(string id) => Edited(shop, tree =>
        {
            var seen = CampaignNode(tree)["seen"]!.AsArray().Select(node => node!.GetValue<string>()).Append(id).Distinct().Order(StringComparer.Ordinal);
            CampaignNode(tree)["seen"] = new JsonArray(seen.Select(met => (JsonNode)JsonValue.Create(met)!).ToArray());
            CampaignNode(tree)["enchanter"]![1]!["enchantment"] = id;
        });
        using (TestContent.UsePremise(tuning: TestShop.Ruled))
        {
            Assert.True(LootTable.IsInert(GameContent.Current.Enchantments["echoing"]));
            RefusedAt(Buying("echoing"), "campaign.enchanter[1].enchantment",
                "echoing is an enchantment that does nothing yet, which the enchanter never offers, so no deposit could have bought it");
            Restores(Buying("flaming"), "a queued element the campaign met");
        }
    }

    [Fact]
    public void AnEnemyOrADropCarryingWearIsRefused()
    {
        // Check 20 (Phase 6a.1): no enemy's weapon ever wears — its wear feed does
        // nothing — and a drop is made when it is rolled, so neither has banked
        // wear or cashed any. A save that says otherwise is none play wrote. The
        // Flagship: a halberd standing on floor 4, and a corpse on floor 4 lying on
        // its drop; and a weapon the party put down there, which is the party's and
        // keeps its wear.
        string text = Save(VisitSaveTests.Flagship().Campaign);
        Restores(text, "the Flagship");
        const string EnemyWhy = "no enemy weapon is ever one the party wields, so it never wears";
        const string DropWhy = "a drop is made when it is rolled, and nothing has wielded it since";
        RefusedAt(Edited(text, tree => FloorNode(tree, 3)["enemies"]![0]!["weapon"]!["wear"] = 1),
            "campaign.visit.floors[3].enemies[0].weapon.wear", $"1 worn: {EnemyWhy}");
        RefusedAt(Edited(text, tree => FloorNode(tree, 3)["enemies"]![0]!["weapon"]!["wearCashed"] = 30),
            "campaign.visit.floors[3].enemies[0].weapon.wearCashed", $"30 cashed: {EnemyWhy}");

        var floors = VisitNode(JsonNode.Parse(text)!.AsObject())["floors"]!.AsArray();
        var (f, k) = Enumerable.Range(0, floors.Count)
            .SelectMany(n => Enumerable.Range(0, floors[n]!["enemies"]!.AsArray().Count).Select(row => (n, row)))
            .First(at => floors[at.n]!["enemies"]![at.row]!["groundItem"] is JsonObject);
        RefusedAt(Edited(text, tree => FloorNode(tree, f)["enemies"]![k]!["groundItem"]!["weapon"]!["wear"] = 4),
            $"campaign.visit.floors[{f}].enemies[{k}].groundItem.weapon.wear", $"4 worn: {DropWhy}");
        RefusedAt(Edited(text, tree => FloorNode(tree, f)["enemies"]![k]!["groundItem"]!["weapon"]!["wearCashed"] = 1),
            $"campaign.visit.floors[{f}].enemies[{k}].groundItem.weapon.wearCashed", $"1 cashed: {DropWhy}");

        // The boss's row is an enemy's like any other: the Spear Hall's bottom floor, reached.
        var bottom = VisitScript.Enter(NewCampaign(), "spear_hall", FiveDeep);
        while (bottom.Floor.DownStairs is not null)
            bottom.Down();
        string lair = Save(bottom.Campaign);
        int deepest = bottom.Visit.BossFloor - 1, boss = BossRow(lair, deepest);
        RefusedAt(Edited(lair, tree => FloorNode(tree, deepest)["enemies"]![boss]!["weapon"]!["wear"] = 2),
            $"campaign.visit.floors[{deepest}].enemies[{boss}].weapon.wear", $"2 worn: {EnemyWhy}");

        // What the party put down is the party's, and keeps what it banked.
        Assert.NotEmpty(FloorNode(JsonNode.Parse(text)!.AsObject(), 3)["looseItems"]!.AsArray());
        Restores(Edited(text, tree =>
        {
            var weapon = FloorNode(tree, 3)["looseItems"]![0]!["weapon"]!;
            weapon["wear"] = 9;
            weapon["wearCashed"] = 30;
        }), "a worn weapon put down");
    }

    [Fact]
    public void AWeaponWornPastAnyCapacityIsRefused()
    {
        // A weapon's wear has a ceiling no tuning in range can move (Phase 6a.1), as
        // a member's hit points do: its capacity opens on a bar no larger than Bar
        // and grows at most MostCapacityPerBar a full bar, each bar one wear cashed
        // or more, and accrual never banks past capacity — so wear is at most Bar
        // plus MostCapacityPerBar for each wear cashed. At it the save loads, one
        // past it is refused at the wear's own path. A fresh weapon — nothing
        // cashed — carrying 900 000 is refused so.
        string hall = Save(HallCleared());
        int bar = ContentBounds.Bar, perBar = ContentBounds.MostCapacityPerBar;
        string Wearing(int wear, int cashed) => Edited(hall, tree =>
        {
            WeaponNode(tree, 0, 0)["wear"] = wear;
            WeaponNode(tree, 0, 0)["wearCashed"] = cashed;
        });
        foreach (int cashed in new[] { 0, 1, 7, 1335 })
        {
            long ceiling = bar + (long)perBar * cashed;
            var loaded = Restores(Wearing((int)ceiling, cashed), $"wear at its ceiling with {cashed} cashed").Campaign.Party[0].Inventory[0]!;
            Assert.Equal(((int)ceiling, cashed), (loaded.Wear, loaded.WearCashed));
            RefusedAt(Wearing((int)ceiling + 1, cashed), "campaign.party[0].inventory[0].wear", $"{ceiling + 1} is above {ceiling}: {WearWhy}");
        }
        RefusedAt(Wearing(900_000, 0), "campaign.party[0].inventory[0].wear", $"900000 is above {bar}: {WearWhy}");

        // Summed as a long: wear cashed at its own ceiling cannot wrap the wear's,
        // and the weapon it loads replays its capacity from the most a count holds.
        var most = Restores(Wearing(ContentBounds.MostCount, ContentBounds.MostCount), "the most wear, the most cashed").Campaign.Party[0].Inventory[0]!;
        Assert.Equal(Wear.Capacity(ContentBounds.MostCount).Max, most.WearCapacity);
        Assert.True(most.WornFull);

        // And no tuning in range gives a capacity past the ceiling: at each end of
        // StartingPool's and WearCapacityPerBar's clamps, whatever was cashed.
        foreach (int start in new[] { 1, 30, bar })
            foreach (int growth in new[] { 0, 1, perBar })
                using (TestContent.UsePremise(tuning: t => t with { StartingPool = start, WearCapacityPerBar = growth }))
                    foreach (int cashed in new[] { 0, 1, 29, 30, 1335, 1_000_000 })
                        Assert.True(Wear.Capacity(cashed).Max <= bar + (long)perBar * cashed,
                            $"StartingPool {start}, WearCapacityPerBar {growth}, {cashed} cashed: capacity {Wear.Capacity(cashed).Max}");
    }

    [Fact]
    public void ALockKeepsItsFloorAlone_SoEveryLockPlayWritesLoads()
    {
        // Nothing reads a lock but against the counter: entering and the list
        // compare the two (CampaignState.IsSelectable, EarlyOffer), and the two
        // countdowns take the counter from a lock above it — the list's, and the
        // summary's, whose lock check 7 holds at or above its own counter. So no
        // lock wraps anything, and a lock keeps its floor alone. A clear writes the
        // counter plus up to 99 x 99 runs, so a lock held to the counter's ceiling
        // would refuse what a clear writes on a counter near it (the review of
        // Phase 5.S4, which found the first draft holding the locks to it).
        var campaign = HallCleared();
        string hall = Save(campaign);
        int top = ContentBounds.MostRuns - 1;   // the most a save's run counter can be
        int cooldown = campaign.LockedUntilRun("spear_hall") - campaign.RunCounter;
        Assert.True(cooldown > 0, $"the Spear Hall's cooldown, {cooldown}");

        // The counter moved, its summary's with it, and the Spear Hall's lock — as
        // its clear wrote it, to the campaign and the summary alike — moved too.
        string Counted(int counter, int until) => Edited(hall, tree =>
        {
            CampaignNode(tree)["runCounter"] = counter;
            Last(tree)["runCounter"] = counter;
            Last(tree)["lockedUntilRun"] = until;
            CampaignNode(tree)["locks"]![0]!["lockedUntilRun"] = until;
        });

        // Any lock at or past the counter loads — the largest whole number over a
        // counter at its own ceiling included — and one below 0 is refused, at
        // either path.
        Restores(Counted(top, int.MaxValue), "a lock at the largest whole number");
        RefusedAt(Edited(hall, tree => CampaignNode(tree)["locks"]![0]!["lockedUntilRun"] = -1), "campaign.locks[0].lockedUntilRun", "-1 is below 0");
        RefusedAt(Edited(hall, tree => Last(tree)["lockedUntilRun"] = -1), "campaign.lastResult.lockedUntilRun", "-1 is below 0");

        // What play writes: a counter a run short of its ceiling, its Spear Hall lock
        // already past that ceiling, and the Long Gallery cleared on it. The tick
        // takes the counter to its ceiling and the clear locks the Long Gallery
        // until past it, and that save loads, whole.
        var nearTop = Restores(Counted(top - 1, top - 1 + cooldown), "a counter a run short of its ceiling").Campaign;
        var gallery = CampaignRuns.Clear(nearTop, "long_gallery", FiveDeep);
        Assert.Equal(top, nearTop.RunCounter);
        Assert.True(gallery.LockedUntilRun > top, $"the lock {gallery.LockedUntilRun}, the counter's ceiling {top}");
        Loads(nearTop, "a lock a clear wrote past the counter's ceiling");

        // The walk's known edge (settled.md, R3(b)): the run counter is the one count
        // play itself carries up to its ceiling. One more ticking run on it — the
        // Knife Warrens cleared — takes it to MostRuns, and the next load refuses
        // that save at the counter. No ceiling on a count play adds to can admit
        // everything played on from it; only a hand edit starts a counter this high.
        CampaignRuns.Clear(nearTop, "knife_warrens", FiveDeep);
        Assert.Equal(ContentBounds.MostRuns, nearTop.RunCounter);
        RefusedAt(Save(nearTop), "campaign.runCounter", $"{ContentBounds.MostRuns} is above {top}: {RunsWhy}");
    }

    // ── Set aside ────────────────────────────────────────────────────────────

    [Fact]
    public void AContradictionIsSetAside_NeverACrash()
    {
        // As a playtester makes them, in campaign.json: the tutorial retired while
        // nothing is cleared; a carried weapon's entry at tier 99999; the run counter
        // at the largest whole number. Each is refused as a save that cannot be read:
        // the launch sets it aside by the next free number, its bytes as they were,
        // says so naming the field and the contradiction or the ceiling — every row
        // in the font — and resumes the backup, one save older. Never a crash.
        (Action<JsonObject> Edit, string Why, string Shown)[] cases =
        [
            (tree => CampaignNode(tree)["cleared"] = new JsonArray(),
                "campaign.retired[0]: tutorial is retired and was never cleared",
                "CAMPAIGN.JSON COULD NOT BE READ (CAMPAIGN.RETIRED[0]: TUTORIAL IS RETIRED AND WAS NEVER CLEARED) - SET ASIDE AS "),
            (tree => WeaponNode(tree, 3, 0)["enchantments"]![0]!["tier"] = 99999,
                $"campaign.party[3].inventory[0].enchantments[0].tier: 99999 is above {ContentBounds.DeepestTier}",
                $"CAMPAIGN.JSON COULD NOT BE READ (CAMPAIGN.PARTY[3].INVENTORY[0].ENCHANTMENTS[0].TIER: 99999 IS ABOVE {ContentBounds.DeepestTier}) - SET ASIDE AS "),
            (tree => CampaignNode(tree)["runCounter"] = int.MaxValue,
                $"campaign.runCounter: {int.MaxValue} is above {ContentBounds.MostRuns - 1}",
                $"CAMPAIGN.JSON COULD NOT BE READ (CAMPAIGN.RUNCOUNTER: {int.MaxValue} IS ABOVE {ContentBounds.MostRuns - 1}) - SET ASIDE AS "),
        ];
        foreach (var (edit, why, shown) in cases)
        {
            var folder = Directory.CreateTempSubdirectory("distancerpg-");
            try
            {
                // The backup the tutorial's clear, the save a run further on — edited.
                var store = new SaveStore(folder.FullName);
                var campaign = NewCampaign();
                CampaignRuns.Clear(campaign, "tutorial", GoldenSeed);
                Assert.Null(CampaignBoot.Save(AsSlot1(store), campaign));
                CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
                Assert.Null(CampaignBoot.Save(AsSlot1(store), campaign));
                string broken = Edited(store.Read()!, edit);
                File.WriteAllText(store.SavePath, broken);

                var log = new List<string>();
                var boot = CampaignBoot.Load(AsSlot1(store), GameContent.Current, log.Add);
                Assert.True(boot.Loaded);
                Assert.Equal(1, boot.Campaign!.RunCounter);   // the backup
                string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");
                Assert.Equal(broken, File.ReadAllText(aside));
                Assert.Collection(boot.Notices,
                    notice => Assert.Equal(CampaignBoot.Resumed(Named(1), backup: true), notice),
                    notice =>
                    {
                        Assert.Equal(CampaignBoot.SetAsideNotice(SaveStore.FileName, $"could not be read ({why})", aside), notice);
                        Assert.StartsWith(shown, notice, StringComparison.Ordinal);
                        Assert.EndsWith(CampaignBoot.TryAgain, notice, StringComparison.Ordinal);
                        // The rows the notice is drawn in; the save folder's path is the machine's own.
                        Assert.All(NoticeText.Wrap(notice.Replace(aside, "", StringComparison.Ordinal)), row => Assert.Matches(Font, row));
                    });
                Assert.Contains(log, line => line.Contains("could not be resumed", StringComparison.Ordinal) && line.Contains(why, StringComparison.Ordinal));
                Assert.True(store.WasResuming);
                store.ClearResuming();
            }
            finally
            {
                folder.Delete(recursive: true);
            }
        }
    }
}
