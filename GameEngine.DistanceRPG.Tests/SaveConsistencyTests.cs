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
                Assert.Null(CampaignBoot.Save(AsAutosave(store), campaign));
                CampaignRuns.Clear(campaign, "spear_hall", FiveDeep);
                Assert.Null(CampaignBoot.Save(AsAutosave(store), campaign));
                string broken = Edited(store.Read()!, edit);
                File.WriteAllText(store.SavePath, broken);

                var log = new List<string>();
                var boot = CampaignBoot.Load(AsAutosave(store), GameContent.Current, log.Add);
                Assert.True(boot.Loaded);
                Assert.Equal(1, boot.Campaign!.RunCounter);   // the backup
                string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");
                Assert.Equal(broken, File.ReadAllText(aside));
                Assert.Collection(boot.Notices,
                    notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
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
