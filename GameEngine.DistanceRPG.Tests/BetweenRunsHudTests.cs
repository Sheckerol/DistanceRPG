using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static GameEngine.DistanceRPG.Tests.TestSlots;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The screen between runs' text (§4.3, §4.5): the tutorial's warning, said
/// before the run is committed to (PHASE4-084); a locked dungeon's countdown in
/// runs (PHASE4-133); every dungeon named with its theme and never its depth;
/// and the last run's summary, which is the run's own. As with
/// <c>HudTextTests</c>, the drawing is untested, but what it draws is tested:
/// the strings, the list's rows in the order the screen draws them, where the
/// choice starts and how it moves, and the one piece of arithmetic that keeps
/// the list on the screen. The runs behind the lists stand on the numbers they
/// were written against — the boss floor range, the mercy floor, the lock a
/// floor (<see cref="Premise"/>) — made current for each test, so the content
/// collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class BetweenRunsHudTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private static DungeonCatalogue Dungeons => GameContent.Current.Dungeons;

    private static string[] Texts(IEnumerable<BetweenRunsHud.ListRow> rows) => rows.Select(r => r.Text).ToArray();

    private static string[] Texts(IEnumerable<DungeonHud.Run> lines) => lines.Select(r => r.Text).ToArray();

    /// <summary>The list's rows as the screen draws them, with its choice where the screen starts it.</summary>
    private static string[] Rows(CampaignState campaign)
    {
        var listed = campaign.Listings();
        return Texts(BetweenRunsHud.ListRows(listed, BetweenRunsScene.FirstChoice(listed, campaign.DefaultNext())));
    }

    [Fact]
    public void TheTutorialWarnsBeforeItIsCommittedTo()
    {
        // PHASE4-084, §4.3: the tutorial "is the one dungeon the game is allowed to
        // warn you about". A first-time player who loses the theme without knowing
        // the rule has been cheated rather than taught, so the list says it plainly
        // under the tutorial, where the run is chosen, before anything is
        // committed to: beating the golem closes the dungeon, and its Block farm,
        // for good. The rest of the dungeons are named as behind it.
        const string warning = "BEATING THE STONE GOLEM CLOSES IT FOR GOOD - AND ITS BLOCK FARM WITH IT";
        var tutorial = Dungeons.Tutorial;
        Assert.Equal(warning, BetweenRunsHud.TutorialWarning(tutorial));

        var campaign = CampaignRuns.Campaign();
        Assert.Equal(new[]
        {
            BetweenRunsHud.ChooseHeading,
            "> THE TUTORIAL - THEMED ON BLOCK <",
            warning,
            "EVERY OTHER DUNGEON OPENS WHEN THE TUTORIAL IS BEATEN",
        }, Rows(campaign));

        // Failing it leaves it, and its warning, where they were.
        CampaignRuns.ShallowRetreat(campaign, "tutorial");
        Assert.Contains(warning, Rows(campaign));

        // The boss and the farm are the dungeon's own, read off its row: content
        // that changed them changes the warning, and it can never name a boss or a
        // farm the dungeon does not have.
        var rebuilt = tutorial with { Theme = ModifierType.Brace, Boss = tutorial.Boss with { Name = "Clay Titan" } };
        Assert.Equal("BEATING THE CLAY TITAN CLOSES IT FOR GOOD - AND ITS BRACE FARM WITH IT", BetweenRunsHud.TutorialWarning(rebuilt));

        // It is the tutorial's alone. No dungeon that locks rather than retires is
        // warned about, and once the golem falls the warning is gone with the
        // tutorial.
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        var hub = Rows(campaign);
        Assert.Equal(Dungeons.All.Count(d => !d.RetiresOnClear) + 1, hub.Length);   // the heading and one row a dungeon: nothing under any of them
        Assert.DoesNotContain(hub, row => row.Contains("FOR GOOD", StringComparison.Ordinal));
        Assert.DoesNotContain(hub, row => row.Contains("TUTORIAL", StringComparison.Ordinal));
    }

    [Fact]
    public void ALockedLineCountsDownInRuns()
    {
        // PHASE4-133: a locked dungeon is on the list with a countdown, "Available
        // in 4 runs", and never missing from it. The count is in runs, singular at
        // one. Under the list, the rule for what the count counts: runs that count,
        // not entries.
        var hall = Dungeons["spear_hall"];
        Assert.Equal("THE SPEAR HALL - THEMED ON BRACE - LOCKED - AVAILABLE IN 4 RUNS",
            BetweenRunsHud.ListingLine(new DungeonListing(hall, Selectable: false, RunsUntilAvailable: 4, Early: false)));
        Assert.Equal("THE SPEAR HALL - THEMED ON BRACE - LOCKED - AVAILABLE IN 1 RUN",
            BetweenRunsHud.ListingLine(new DungeonListing(hall, false, 1, false)));

        // A floor-5 clear, then one run that counts elsewhere: four to go, drawn as
        // such between the open lines, with the rule under the list.
        var campaign = CampaignRuns.Campaign();
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        Assert.Equal(5, DungeonState.RollBossFloor(CampaignRuns.FiveDeep, hall));
        CampaignRuns.Clear(campaign, "spear_hall", CampaignRuns.FiveDeep);
        CampaignRuns.DeepRetreat(campaign, "long_gallery");
        Assert.Equal(new[]
        {
            BetweenRunsHud.ChooseHeading,
            "THE SPEAR HALL - THEMED ON BRACE - LOCKED - AVAILABLE IN 4 RUNS",
            "> THE LONG GALLERY - THEMED ON LONGSHOT <",
            "THE KNIFE WARRENS - THEMED ON CRIT WINDOW",
            "THE QUIVER PITS - THEMED ON CHARGES",
            "A LOCK COUNTS DOWN ON RUNS THAT COUNT - A CLEAR, OR A FAILED RUN FROM FLOOR 3",
        }, Rows(campaign));
        Assert.Equal(3, GameContent.Current.Tuning.MercyFloor);

        // A locked line reads dimmed, and the choice never lands on it.
        var rows = BetweenRunsHud.ListRows(campaign.Listings(), chosen: 1);
        Assert.Equal(DungeonHud.DarkGrey, rows[1].Color);
        Assert.Equal(DungeonHud.Yellow, rows[2].Color);

        // With every dungeon locked, the early offer says why it can be entered.
        Assert.Equal("THE SPEAR HALL - THEMED ON BRACE - OFFERED EARLY - EVERY DUNGEON IS LOCKED",
            BetweenRunsHud.ListingLine(new DungeonListing(hall, true, 0, Early: true)));
    }

    [Fact]
    public void AListingNamesTheThemeAndNeverTheDepth()
    {
        // Each dungeon is themed on one modifier (§4.3), which is the reason to
        // pick it, so its line names it. The depth is rolled at each entry and
        // learned by reaching it (§4.3, PHASE4-024), so no line says how deep a
        // dungeon goes: not the tutorial's fixed two, not the tuning's five to ten.
        // An open line carries no number at all, and a locked line's only number is
        // its countdown.
        foreach (var def in Dungeons.All)
        {
            string line = BetweenRunsHud.ListingLine(new DungeonListing(def, true, 0, false));
            Assert.Equal($"{def.Name.ToUpperInvariant()} - THEMED ON {DungeonHud.ModifierName(def.Theme)}", line);
            Assert.DoesNotContain(line, char.IsDigit);
            Assert.DoesNotContain("FLOOR", line, StringComparison.Ordinal);
            Assert.DoesNotContain("DEEP", line, StringComparison.Ordinal);

            string locked = BetweenRunsHud.ListingLine(new DungeonListing(def, false, 7, false));
            Assert.Equal("7", new string(locked.Where(char.IsDigit).ToArray()));
        }

        Assert.Equal("THE TUTORIAL - THEMED ON BLOCK", BetweenRunsHud.ListingLine(new DungeonListing(Dungeons.Tutorial, true, 0, false)));
        Assert.Equal("THE KNIFE WARRENS - THEMED ON CRIT WINDOW", BetweenRunsHud.ListingLine(new DungeonListing(Dungeons["knife_warrens"], true, 0, false)));

        // Nor is it hidden anywhere a listing carries: a listing holds a dungeon,
        // whether it can be entered, a countdown and the early flag, and nothing a
        // depth could be read from.
        Assert.Equal(new[] { "Def", "Early", "RunsUntilAvailable", "Selectable" },
            typeof(DungeonListing).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheSummaryIsTheRunsOwn()
    {
        // Above the list sits the last run's summary: that run's own facts, off its
        // result — the facts its end showed over the last floor, less that panel's
        // key, since here ENTER descends. A new campaign has no last run, and shows
        // none.
        var campaign = CampaignRuns.Campaign();
        Assert.Null(campaign.LastResult);

        // The tutorial is two floors deep on every visit, so the summary promises
        // no new depth and points at no floor 3 it does not have.
        var retreat = CampaignRuns.ShallowRetreat(campaign, "tutorial");
        Assert.Same(retreat, campaign.LastResult);
        var lines = BetweenRunsHud.SummaryLines(campaign.LastResult!);
        Assert.Equal(new[]
        {
            "RETREATED",
            "THE TUTORIAL - DEEPEST FLOOR 1",
            "THE STONE GOLEM STILL STANDS",
            "NO SPOILS - NOTHING DROPS WHILE THE BOSS STANDS",
            "XP KEPT",
            "RUN NOT COUNTED - 0 RUNS SO FAR",
        }, Texts(lines));

        // The same lines, in the same colours, as the dungeon's own summary, whose
        // one extra line is its key. Here there is no key line, because ENTER
        // descends into the chosen dungeon.
        var panel = DungeonHud.RunSummaryLines(retreat);
        Assert.Equal(lines, panel.Take(panel.Count - 1));
        Assert.Equal(DungeonHud.ContinueHint, panel[^1].Text);
        Assert.DoesNotContain(Texts(lines), line => line.Contains("ENTER", StringComparison.Ordinal));

        // The next run's end replaces it whole: the clear's own summary, nothing
        // left over from the retreat before it.
        var clear = CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        Assert.Same(clear, campaign.LastResult);
        Assert.Equal(new[]
        {
            "CLEARED",
            "THE TUTORIAL - DEEPEST FLOOR 2",
            "THE STONE GOLEM FELL ON FLOOR 2",
            "NO SPOILS CARRIED",
            "XP KEPT",
            "RUN COUNTED - 1 RUN SO FAR",
            "THE TUTORIAL IS CLOSED FOR GOOD",
        }, Texts(BetweenRunsHud.SummaryLines(campaign.LastResult!)));
    }

    [Fact]
    public void TheChoiceStartsOnTheDefault_AndStepsOverLockedLines()
    {
        // The screen's choice starts on the campaign's own default: the tutorial
        // until it is beaten, then the first open dungeon, then the early offer. W
        // and S move it over the lines that can be entered, round the ends of the
        // list. A locked line is read, not chosen: ENTER could do nothing with it.
        var campaign = CampaignRuns.Campaign();
        var fresh = campaign.Listings();
        Assert.Equal(0, BetweenRunsScene.FirstChoice(fresh, campaign.DefaultNext()));
        Assert.Equal(0, BetweenRunsScene.NextChoice(fresh, 0, +1));   // nowhere else to go
        Assert.Equal(0, BetweenRunsScene.NextChoice(fresh, 0, -1));

        DungeonListing Line(string id, bool open) => new(Dungeons[id], open, open ? 0 : 3, false);
        var listed = new[] { Line("spear_hall", false), Line("long_gallery", true), Line("knife_warrens", false), Line("quiver_pits", true) };
        Assert.Equal(3, BetweenRunsScene.FirstChoice(listed, "quiver_pits"));
        Assert.Equal(1, BetweenRunsScene.FirstChoice(listed, "spear_hall"));   // locked: the first open line instead
        Assert.Equal(1, BetweenRunsScene.FirstChoice(listed, null));
        Assert.Equal(3, BetweenRunsScene.NextChoice(listed, 1, +1));           // past the locked Knife Warrens
        Assert.Equal(1, BetweenRunsScene.NextChoice(listed, 3, +1));           // round the end, past the locked Spear Hall
        Assert.Equal(3, BetweenRunsScene.NextChoice(listed, 1, -1));

        Assert.Equal(-1, BetweenRunsScene.FirstChoice([], null));              // nothing left: nothing to choose
        Assert.Equal(-1, BetweenRunsScene.FirstChoice(listed.Where(l => !l.Selectable).ToArray(), "spear_hall"));
        Assert.Equal(-1, BetweenRunsScene.NextChoice([], -1, +1));
    }

    [Fact]
    public void TheListIsNeverPushedOffA720Window()
    {
        // The list is what the screen is for, so the summary above it gives way.
        // An ordinary summary is shown whole with the list right under it. The
        // longest one a run can write (a wipe that loses a full haul and claws
        // back from every member) is cut short above a list that still clears the
        // keys line, at the shipped 1280 x 720.
        const float h = 720f;
        var campaign = CampaignRuns.Campaign();
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        float listHeight = BetweenRunsHud.ListRows(campaign.Listings(), 0).Sum(row => row.Pitch)
            + 20f;   // and room for the lock rule, the longest the shipped list gets

        var ordinary = BetweenRunsHud.Layout(h, summaryLines: 7, listHeight);
        Assert.Equal(7, ordinary.SummaryRows);
        Assert.True(ordinary.ListTop + listHeight <= h - BetweenRunsHud.ListBottomMargin);

        var longest = BetweenRunsHud.Layout(h, summaryLines: 40, listHeight);
        Assert.InRange(longest.SummaryRows, 1, 39);
        Assert.Equal(h - BetweenRunsHud.ListBottomMargin, longest.ListTop + listHeight, precision: 3);

        // Before any run, nothing above it: the list sits in the upper third.
        var alone = BetweenRunsHud.Layout(h, summaryLines: 0, listHeight);
        Assert.Equal(0, alone.SummaryRows);
        Assert.InRange(alone.ListTop, 100f, h / 3f);
    }

    [Fact]
    public void NoticesSitUnderTheTitle_AndNeverPushTheListOffA720Window()
    {
        // The startup notices (1h, 1i) — a tuning.json value clamped, a file
        // missing — sit under the title, in the rows the one wrapper lays them out
        // in, and push the last run and the list down by their height. The list is
        // still never pushed off the shipped 1280 x 720: the summary gives way.
        const float h = 720f;
        var notices = NoticeText.Rows(Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1").ToList());
        Assert.Equal(NoticeText.MaxRows + 1, notices.Count);   // three, and how many more are in the log

        var campaign = CampaignRuns.Campaign();
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        float listHeight = BetweenRunsHud.ListRows(campaign.Listings(), 0).Sum(row => row.Pitch) + 20f;

        // Under the title, and above everything they push down.
        Assert.True(BetweenRunsHud.NoticeTop > BetweenRunsHud.TitleBottom);
        var plain = BetweenRunsHud.Layout(h, summaryLines: 7, listHeight);
        var noted = BetweenRunsHud.Layout(h, summaryLines: 7, listHeight, notices.Count);
        Assert.Equal(0f, plain.Shift);
        Assert.Equal(notices.Count * BetweenRunsHud.NoticePitch, noted.Shift);
        Assert.True(BetweenRunsHud.NoticeTop + notices.Count * BetweenRunsHud.NoticePitch <= BetweenRunsHud.LastRunHeadingY + noted.Shift);
        Assert.Equal(7, noted.SummaryRows);
        Assert.Equal(plain.ListTop + noted.Shift, noted.ListTop);
        Assert.True(noted.ListTop + listHeight <= h - BetweenRunsHud.ListBottomMargin);

        // The longest summary a run can write gives way further; the list still clears the keys line.
        var longest = BetweenRunsHud.Layout(h, summaryLines: 40, listHeight, notices.Count);
        Assert.InRange(longest.SummaryRows, 1, BetweenRunsHud.Layout(h, summaryLines: 40, listHeight).SummaryRows - 1);
        Assert.Equal(h - BetweenRunsHud.ListBottomMargin, longest.ListTop + listHeight, precision: 3);

        // Before any run: the list sits under the notices, and still on the screen.
        var alone = BetweenRunsHud.Layout(h, summaryLines: 0, listHeight, notices.Count);
        Assert.True(alone.ListTop >= BetweenRunsHud.NoticeTop + notices.Count * BetweenRunsHud.NoticePitch);
        Assert.True(alone.ListTop + listHeight <= h - BetweenRunsHud.ListBottomMargin);

        // No notices: the screen is exactly as it was.
        Assert.Empty(NoticeText.Rows([]));
        Assert.Equal(plain, BetweenRunsHud.Layout(h, summaryLines: 7, listHeight, noticeRows: 0));
    }

    [Fact]
    public void TheSavesWordShownWholeStillLeavesTheListOnA720Window()
    {
        // S5's review: the save's word leads the notices and is shown whole, up to
        // its ceiling, and the data files' notices take their three rows after it,
        // then the row that counts the rest — the most rows of notices the screen
        // between runs can be handed. Under all of them the list is still never
        // pushed off the shipped 1280 x 720: the summary gives way, as ever.
        const float h = 720f;
        int most = NoticeText.MaxWholeRows + NoticeText.MaxRows + 1;
        var saysWhole = Enumerable.Range(1, NoticeText.MaxWholeRows).Select(i => $"the save's word, row {i}");
        var data = Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1");
        var rows = NoticeText.Rows([.. saysWhole, .. data], whole: NoticeText.MaxWholeRows);
        Assert.Equal(most, rows.Count);
        Assert.Equal(NoticeText.More(2), rows[^1]);

        foreach (bool cleared in new[] { false, true })
        {
            var campaign = CampaignRuns.Campaign();
            if (cleared)
                CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            float listHeight = BetweenRunsHud.ListRows(campaign.Listings(), 0).Sum(row => row.Pitch) + 20f;   // and room for the lock rule

            foreach (int summaryLines in new[] { 0, 7, 40 })
            {
                var layout = BetweenRunsHud.Layout(h, summaryLines, listHeight, rows.Count);
                Assert.True(BetweenRunsHud.NoticeTop + rows.Count * BetweenRunsHud.NoticePitch <= layout.ListTop,
                    $"the notices run into the list ({summaryLines} summary lines, tutorial cleared: {cleared})");
                Assert.True(layout.ListTop + listHeight <= h - BetweenRunsHud.ListBottomMargin,
                    $"the list is pushed off the window ({summaryLines} summary lines, tutorial cleared: {cleared})");
            }
        }
    }

    [Fact]
    public void TheKeysChooseAndDescend()
    {
        // 1f, and the user's ruling of 2026-09-25 on the save model, R2(e): the
        // screen between runs names the keys that choose and descend, TAB, which
        // turns to the WEAPONS page (Phase 6a.2), and ESC, which opens the menu —
        // where the campaign is saved by hand and play is left for the title. NEW
        // CAMPAIGN is the title's now, so no key here begins one, and the line names
        // none.
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER DESCENDS - TAB WEAPONS - ESC MENU", BetweenRunsHud.KeysLine);
        Assert.DoesNotContain("NEW CAMPAIGN", BetweenRunsHud.KeysLine, StringComparison.Ordinal);

        // The keys stand along the bottom in their grey.
        Assert.Equal((BetweenRunsHud.KeysLine, DungeonHud.Grey), BetweenRunsHud.BottomLine());

        // It fits the shipped 1280-wide window at the keys line's scale, with room either side.
        foreach (string line in new[] { BetweenRunsHud.KeysLine })
            Assert.True(line.Length * 6f * 1.3f <= 1280f - 40f, line);

        // A resumed campaign says so first under the title, in green — news, not a
        // warning — and every other notice keeps the warning's orange.
        Assert.Equal("CAMPAIGN RESUMED", BetweenRunsHud.Resumed);
        Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(NoticeText.Rows([BetweenRunsHud.Resumed])[0]));
        Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(NoticeText.Rows([CampaignBoot.Resumed(Named(1), backup: true)])[0]));
        Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor(NoticeText.Rows([CampaignBoot.TuningChanged])[0]));
        Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor("TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1"));
    }

    [Fact]
    public void TheMenuBetweenRunsSavesAndQuitsToTheTitle()
    {
        // The user's ruling of 2026-09-25 on the save model, R2(d) and R2(e): between
        // runs is a safe point, and ESC there opens the dungeon's pause menu over the
        // hub — 1 CLOSE, 2 SAVE GAME, 3 QUIT TO TITLE — its keys line in the hub's
        // keys' place while it is up. SAVE GAME saves the campaign's own slot at once,
        // asking nothing (the user's ruling of the same day on the save slots, R5), so
        // no list ever stands in the menu's place.
        Assert.Equal("1  CLOSE - 2  SAVE GAME - 3  QUIT TO TITLE - ESC CLOSES", BetweenRunsHud.MenuKeysLine(hasTitle: true));
        Assert.Equal("1  CLOSE - 2  SAVE GAME - 3  QUIT GAME - ESC CLOSES", BetweenRunsHud.MenuKeysLine(hasTitle: false));
        Assert.Equal((BetweenRunsHud.KeysLine, DungeonHud.Grey), BetweenRunsHud.BottomLine());
        Assert.Equal((BetweenRunsHud.MenuKeysLine(hasTitle: true), DungeonHud.Grey), BetweenRunsHud.BottomLine(BetweenRunsScene.HubMode.Menu));
        Assert.Equal((BetweenRunsHud.MenuKeysLine(hasTitle: false), DungeonHud.Grey), BetweenRunsHud.BottomLine(BetweenRunsScene.HubMode.Menu, hasTitle: false));
        foreach (string line in new[] { BetweenRunsHud.MenuKeysLine(hasTitle: true), BetweenRunsHud.MenuKeysLine(hasTitle: false) })
        {
            Assert.Matches("^[A-Z0-9 /-]+$", line);
            Assert.True(line.Length * 6f * BetweenRunsHud.KeysLineScale <= 1280f - 40f, line);
        }

        // Where a key goes, walked key by key: with the menu up, ENTER, W, S and the
        // arrows are the menu's — which does nothing with them, so nothing descends
        // behind it — as every other key is; ESC is always the game's, which asks
        // HandleEscape. (The other modes' keys: EveryModeRoutesEveryKey_EscThroughHandleEscape.)
        foreach (var key in new[] { Keys.Enter, Keys.KeyPadEnter, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.D1, Keys.D2, Keys.D3, Keys.N, Keys.Space })
            Assert.Equal(BetweenRunsScene.KeyRoute.Menu, BetweenRunsScene.Route(BetweenRunsScene.HubMode.Menu, key));
        foreach (var key in new[] { Keys.Enter, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.D2, Keys.N })
            Assert.Equal(BetweenRunsScene.KeyRoute.List, BetweenRunsScene.Route(BetweenRunsScene.HubMode.Dungeons, key));
        foreach (var mode in new[] { BetweenRunsScene.HubMode.Dungeons, BetweenRunsScene.HubMode.Menu })
            Assert.Equal(BetweenRunsScene.KeyRoute.Game, BetweenRunsScene.Route(mode, Keys.Escape));

        // Walked through the screen itself, built with no window over a new campaign,
        // the tutorial chosen, its campaign in slot 2: its host records what it is
        // asked for.
        var host = new RecordingHost();
        var scene = new BetweenRunsScene(game: null!, CampaignRuns.Campaign(), host, shownWhole: 0);
        Assert.Equal((0, false), (scene.Chosen, scene.MenuOpen));
        Assert.True(scene.HandleEscape());
        Assert.True(scene.MenuOpen);
        foreach (var key in new[] { Keys.Enter, Keys.KeyPadEnter, Keys.W, Keys.S, Keys.Up, Keys.Down })
            scene.OnKey(key);
        Assert.Equal((0, true, 0), (host.Descended.Count, scene.MenuOpen, scene.Chosen));   // nothing descended, or moved, behind the menu

        // The menu names the campaign's slot under its options. 2 asks the host to
        // save — between runs is always a safe point — and the menu stays up; what
        // the save came to, once the host has written it, is drawn inside the menu,
        // green, over the hub, clear of the keys line along the bottom of a
        // 720-pixel window, until the next key.
        string savesLine = DungeonHud.PauseSavesLine(scene.SavesInto);
        Assert.Equal("PROGRESS SAVES TO SLOT 2 AT EVERY TURN", savesLine);
        scene.OnKey(Keys.D2);
        Assert.Equal((1, true, (string?)null), (host.HandSaves, scene.MenuOpen, scene.MenuAnswer));
        scene.SlotSaved(CampaignBoot.SavedTo(Named(2)));
        Assert.Equal("SAVED TO SLOT 2", scene.MenuAnswer);
        var menuRows = DungeonHud.PauseMenuLayout(1280f, 720f, PauseMenu.Options(hasTitle: true), canSave: true, savesLine, scene.MenuAnswer);
        Assert.Contains(menuRows, row => row.Text == "SAVED TO SLOT 2" && row.Color == DungeonHud.Green);
        Assert.Contains(menuRows, row => row.Text == savesLine && row.Color == DungeonHud.Grey);
        Assert.True(menuRows.Max(row => row.Bottom) <= 720f - BetweenRunsHud.KeysLineFromBottom, "the menu runs into the keys line");
        scene.OnKey(Keys.X);
        Assert.Null(scene.MenuAnswer);

        // ESC closes the menu, and closed, the hub is the hub again: ENTER descends.
        Assert.True(scene.HandleEscape());
        Assert.False(scene.MenuOpen);
        Assert.Equal(1, host.HandSaves);
        scene.OnKey(Keys.Enter);
        Assert.Equal(["tutorial"], host.Descended);

        // 3 leaves play for the title, through the host, and the screen is on its way
        // out: nothing after it is read.
        var leaving = new RecordingHost();
        var quitting = new BetweenRunsScene(game: null!, CampaignRuns.Campaign(), leaving, shownWhole: 0);
        quitting.HandleEscape();
        quitting.OnKey(Keys.D3);
        Assert.Equal(1, leaving.QuitsToTitle);
        quitting.OnKey(Keys.D2);
        quitting.OnKey(Keys.Enter);
        Assert.Equal((0, 0), (leaving.HandSaves, leaving.Descended.Count));
        Assert.True(quitting.HandleEscape());
    }

    // ── The WEAPONS page (§6.2, Phase 6a.2) ─────────────────────────────────

    /// <summary>A screen between runs walked with no window, over <paramref name="campaign"/> — the game's own starting party when none is handed in — and its recording host.</summary>
    private static (BetweenRunsScene Scene, RecordingHost Host) Hub(CampaignState? campaign = null, IReadOnlyList<string>? notices = null, int shownWhole = 0)
    {
        var host = new RecordingHost();
        return (new BetweenRunsScene(game: null!, campaign ?? CampaignState.Start(GameContent.Current), host, shownWhole, notices), host);
    }

    /// <summary>A weapon of every row the content ships, as a drop makes one: a wand that names no element of its own on one it is handed.</summary>
    private static Weapon Made(WeaponDef def)
        => GameContent.Current.Weapons.Instantiate(def.Id,
            def.Class == WeaponClass.Wand && !def.Enchantments.Any(entry => GameContent.Current.Enchantments[entry.Id].Effect == EffectKind.ElementalDamage)
                ? DamageType.Shocking
                : null);

    /// <summary>
    /// <paramref name="def"/>'s row as the most a save may load one carrying: its
    /// drop's spreads, and ten of the longest enchantment the content ships at tier
    /// 99 (<see cref="ContentBounds.MostEnchantments"/>), with four-digit wear and
    /// cash — the widest and the tallest detail the page can be handed.
    /// </summary>
    private static Weapon Laden(WeaponDef def)
    {
        var made = Made(def);
        var soul = GameContent.Current.Enchantments.All.MaxBy(entry => entry.Name.Length)!;
        return new Weapon(def, made.Forged, made.Modifiers, Enumerable.Range(0, ContentBounds.MostEnchantments).Select(_ => new Enchantment(soul, 99)).ToArray(), 0,
            wear: 9999, wearCashed: 9998);
    }

    /// <summary>Every member's slots, as they stand: what a carry must leave alone.</summary>
    private static Weapon?[][] Bags(CampaignState campaign) => campaign.Party.Select(member => member.Inventory.ToArray()).ToArray();

    /// <summary>The chosen row's text on the page.</summary>
    private static string ChosenText(BetweenRunsScene scene) => scene.Weapons.ChosenRow.Text;

    /// <summary>Step the page's choice down until its row reads <paramref name="text"/>, failing if a lap never meets it.</summary>
    private static void ChooseRow(BetweenRunsScene scene, string text)
    {
        for (int step = 0; step <= scene.Weapons.Rows().Count; step++)
        {
            if (ChosenText(scene) == text)
                return;
            scene.OnKey(Keys.S);
        }
        Assert.Fail($"No row the choice stops on reads '{text}'");
    }

    [Fact]
    public void TabSwitchesPages_AndTheMenuTakesEveryKey()
    {
        // §6.2, Phase 6a.2: the screen between runs has two pages, and TAB goes from
        // one to the other — DUNGEONS, where every showing opens, and WEAPONS. The
        // title, the notices and the menu are the same on both; the keys line is the
        // page's.
        var (scene, host) = Hub();
        Assert.Equal((BetweenRunsScene.HubMode.Dungeons, false), (scene.Mode, scene.OnWeapons));
        Assert.Equal(BetweenRunsHud.KeysLine, BetweenRunsHud.BottomLine(scene.Mode, scene.HasTitle).Text);
        scene.OnKey(Keys.Tab);
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, true), (scene.Mode, scene.OnWeapons));
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER TAKES IT UP - TAB DUNGEONS - ESC MENU", BetweenRunsHud.BottomLine(scene.Mode, scene.HasTitle).Text);
        scene.OnKey(Keys.Tab);
        Assert.Equal(BetweenRunsScene.HubMode.Dungeons, scene.Mode);
        scene.OnKey(Keys.Tab);

        // ESC opens the menu over WEAPONS, and while it is up every key is its: TAB
        // turns no page, ENTER takes nothing up, W/S move nothing.
        string chosen = ChosenText(scene);
        Assert.True(scene.HandleEscape());
        Assert.Equal((BetweenRunsScene.HubMode.Menu, true), (scene.Mode, scene.OnWeapons));
        Assert.Equal(BetweenRunsHud.MenuKeysLine(hasTitle: true), BetweenRunsHud.BottomLine(scene.Mode, scene.HasTitle).Text);
        foreach (var key in new[] { Keys.Tab, Keys.Enter, Keys.KeyPadEnter, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.D, Keys.T })
            scene.OnKey(key);
        Assert.Equal((BetweenRunsScene.HubMode.Menu, true, chosen), (scene.Mode, scene.OnWeapons, ChosenText(scene)));
        Assert.Null(scene.Weapons.Carried);

        // Closed, the page it was opened over is back, as it was; and the menu's 2
        // saves by hand there as on DUNGEONS.
        scene.OnKey(Keys.D2);
        Assert.Equal(1, host.HandSaves);
        Assert.True(scene.HandleEscape());
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, chosen), (scene.Mode, ChosenText(scene)));
        Assert.Equal((0, 0), (host.Descended.Count, host.HubSavesAsked));
    }

    [Fact]
    public void EveryModeRoutesEveryKey_EscThroughHandleEscape()
    {
        // The hub's modes by its keys (the plan's §1g table, the modes Phase 6a.2
        // has): where Route sends each key — ESC always to the game, which asks
        // HandleEscape — and then what the screen does with it, walked.
        var modes = Enum.GetValues<BetweenRunsScene.HubMode>();
        Keys[] keys = [Keys.Tab, Keys.Enter, Keys.KeyPadEnter, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.D1, Keys.D2, Keys.D3, Keys.D, Keys.T, Keys.N, Keys.Space];
        foreach (var mode in modes)
        {
            Assert.Equal(BetweenRunsScene.KeyRoute.Game, BetweenRunsScene.Route(mode, Keys.Escape));
            foreach (var key in keys)
            {
                var expected = mode switch
                {
                    BetweenRunsScene.HubMode.Menu => BetweenRunsScene.KeyRoute.Menu,
                    BetweenRunsScene.HubMode.Dungeons => key == Keys.Tab ? BetweenRunsScene.KeyRoute.Page : BetweenRunsScene.KeyRoute.List,
                    BetweenRunsScene.HubMode.Weapons => key == Keys.Tab ? BetweenRunsScene.KeyRoute.Page : BetweenRunsScene.KeyRoute.Weapons,
                    _ => BetweenRunsScene.KeyRoute.Carrying,   // TAB is the carry's, and does nothing
                };
                Assert.True(expected == BetweenRunsScene.Route(mode, key), $"{mode}, {key}: routed to {BetweenRunsScene.Route(mode, key)}, not {expected}");
            }
        }

        // DUNGEONS: W/S choose, ENTER descends, TAB turns the page, and every other
        // key is inert — D and T are WEAPONS' keys to come (Phase 6a.5), and here
        // they say nothing.
        var (scene, host) = Hub();
        foreach (var key in new[] { Keys.D, Keys.T, Keys.D1, Keys.N, Keys.Space })
            scene.OnKey(key);
        Assert.Equal((BetweenRunsScene.HubMode.Dungeons, 0, (string?)null), (scene.Mode, scene.Chosen, scene.Refusal));

        // WEAPONS, browsing: W/S choose, ENTER takes up, and the rest is inert.
        scene.OnKey(Keys.Tab);
        string first = ChosenText(scene);
        foreach (var key in new[] { Keys.D, Keys.T, Keys.D1, Keys.D2, Keys.N, Keys.Space })
            scene.OnKey(key);
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, first), (scene.Mode, ChosenText(scene)));
        scene.OnKey(Keys.S);
        Assert.NotEqual(first, ChosenText(scene));
        scene.OnKey(Keys.W);
        Assert.Equal(first, ChosenText(scene));

        // Carrying: TAB, D, T and the digits are inert — the page stays WEAPONS, the
        // weapon stays taken up; W/S choose where; ESC, through HandleEscape, puts it
        // back and opens no menu.
        scene.OnKey(Keys.Enter);
        Assert.Equal(BetweenRunsScene.HubMode.Carrying, scene.Mode);
        foreach (var key in new[] { Keys.Tab, Keys.D, Keys.T, Keys.D1, Keys.D2, Keys.D3, Keys.N, Keys.Space })
            scene.OnKey(key);
        Assert.Equal((BetweenRunsScene.HubMode.Carrying, true, first), (scene.Mode, scene.OnWeapons, ChosenText(scene)));
        Assert.True(scene.HandleEscape());
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, false), (scene.Mode, scene.MenuOpen));

        // ESC from each other mode: on a page it opens the menu, on the menu it
        // closes it — the page under it unchanged.
        Assert.True(scene.HandleEscape());
        Assert.Equal(BetweenRunsScene.HubMode.Menu, scene.Mode);
        Assert.True(scene.HandleEscape());
        Assert.Equal(BetweenRunsScene.HubMode.Weapons, scene.Mode);
        scene.OnKey(Keys.Tab);
        Assert.True(scene.HandleEscape());
        Assert.Equal((BetweenRunsScene.HubMode.Menu, false), (scene.Mode, scene.OnWeapons));
        Assert.True(scene.HandleEscape());
        Assert.Equal(BetweenRunsScene.HubMode.Dungeons, scene.Mode);
        Assert.Equal((0, 0, 0), (host.Descended.Count, host.HubSavesAsked, host.HandSaves));
    }

    [Fact]
    public void TheWeaponsPageListsEveryMemberAndTheStable()
    {
        // §6.2: WEAPONS lists every member's hand and bag, in roster order, then the
        // stable, a row a weapon with its wear; a row for the room a bag has left,
        // and the stable's room, always there — it is unbounded (PHASE 6a DECISION 7).
        // Last the enchanter (Phase 6a.3a), shut until the tutorial is beaten
        // (TheEnchanterSectionShowsWorkingAndStalled).
        var campaign = CampaignState.Start(GameContent.Current);
        campaign.Move(new WeaponPlace("A", 1), WeaponPlace.InTheStable(0));
        Assert.Equal(new[]
        {
            "THE PARTY AND THE STABLE",
            "[A]",
            "  HAND  WEAKSPOT STILETTO - WEAR 0/30",
            "  BAG   - ROOM FOR 5 MORE -",
            "[B]",
            "  HAND  TOWER GUARD - WEAR 0/30",
            "  BAG   STAFF OF RENEWAL - WEAR 0/30",
            "  BAG   - ROOM FOR 4 MORE -",
            "[C]",
            "  HAND  GREAT AXE - WEAR 0/30",
            "  BAG   STAFF OF RENEWAL - WEAR 0/30",
            "  BAG   - ROOM FOR 4 MORE -",
            "[D]",
            "  HAND  STAFF OF RENEWAL - WEAR 0/30",
            "  BAG   STAFF OF MIRE - WEAR 0/30",
            "  BAG   - ROOM FOR 4 MORE -",
            "THE STABLE - 1 WEAPON",
            "  STAFF OF RENEWAL - WEAR 0/30",
            "  - ROOM -",
            "THE ENCHANTER OPENS WHEN THE TUTORIAL IS BEATEN",
        }, HubWeapons.Rows(campaign).Select(row => row.Text));
        Assert.Equal("THE STABLE - EMPTY", HubWeapons.StableHeading(0));
        Assert.Equal("THE STABLE - 3 WEAPONS", HubWeapons.StableHeading(3));

        // A full bag has no room row; an empty hand is a row of its own, somewhere a
        // weapon can go; a full weapon reads FULL.
        var b = campaign.Party[1];
        for (int slot = 2; slot < b.Inventory.Length; slot++)
            b.Inventory[slot] = TestWeapons.Get("darts");
        var a = campaign.Party[0];
        var def = GameContent.Current.Weapons["weakspot_stiletto"];
        var full = new Weapon(def, a.Inventory[0]!.Forged, a.Inventory[0]!.Modifiers, [], 0, wear: 30);
        a.Inventory[0] = null;
        a.Inventory[2] = full;
        var rows = HubWeapons.Rows(campaign);
        Assert.Equal(new[] { "[A]", "  HAND  - EMPTY -", "  BAG   WEAKSPOT STILETTO - WEAR 30/30 FULL", "  BAG   - ROOM FOR 4 MORE -" },
            rows.Skip(1).Take(4).Select(row => row.Text));
        Assert.DoesNotContain(rows, row => row.Place?.MemberId == "B" && row.Kind == HubWeapons.RowKind.Room);
        Assert.Equal(new WeaponPlace("A", 0), rows[2].Place);
        Assert.Equal(new WeaponPlace("A", 1), rows[4].Place);   // a weapon put in the room takes the first empty slot
        Assert.Equal(WeaponPlace.InTheStable(1), rows[^2].Place);   // the stable's end, over the enchanter's heading

        // Drawn: headings and members grey, a room dark grey, a weapon white — its
        // wear green once full — and a unique's name orange. The chosen row is
        // yellow, its indent marked and its end closed, as the dungeon list's choice
        // is; a weapon taken up reads cyan where it lies.
        Assert.Equal(DungeonHud.Grey, Assert.Single(HubWeapons.Runs(rows[0])).Color);
        Assert.Equal(DungeonHud.DarkGrey, Assert.Single(HubWeapons.Runs(rows[2])).Color);
        var fullRuns = HubWeapons.Runs(rows[3]);
        Assert.Equal(("  BAG   ", DungeonHud.White), (fullRuns[0].Text, fullRuns[0].Color));
        Assert.Equal(("WEAR 30/30 FULL", DungeonHud.Green), (fullRuns[^1].Text, fullRuns[^1].Color));
        var chosen = HubWeapons.Runs(rows[3], chosen: true);
        Assert.Equal("> BAG   WEAKSPOT STILETTO - WEAR 30/30 FULL <", string.Concat(chosen.Select(run => run.Text)));
        Assert.Equal((DungeonHud.Yellow, DungeonHud.Green), (chosen[0].Color, chosen[3].Color));
        Assert.Equal("> HAND  - EMPTY - <", Assert.Single(HubWeapons.Runs(rows[2], chosen: true)).Text);
        Assert.Equal(DungeonHud.Cyan, HubWeapons.Runs(rows[3], carried: true)[0].Color);
        var knife = new HubWeapons.Row(HubWeapons.RowKind.Weapon, HubWeapons.StableLabel, WeaponPlace.InTheStable(0), TestWeapons.Get("flensing_knife_unique"));
        Assert.True(knife.Weapon!.Unique);
        Assert.Equal(DungeonHud.Orange, HubWeapons.Runs(knife)[1].Color);
        Assert.Equal("THE NAMELESS KNIFE", HubWeapons.The(knife.Weapon));
        Assert.Equal("THE BULWARK", HubWeapons.The(TestWeapons.Get("the_bulwark")));

        // The chosen weapon, said whole on the right: where it is, its name, the
        // dungeon's readouts with no wielder, its wear and its capacity, and its
        // slots and what its next enchantment costs (Phase 6a.3a; their words are
        // TheEnchanterSectionShowsWorkingAndStalled's).
        var stiletto = TestWeapons.Get("weakspot_stiletto");
        var detail = BetweenRunsHud.DetailRows(new HubWeapons.Row(HubWeapons.RowKind.Weapon, HubWeapons.HandLabel, new WeaponPlace("A", 0), stiletto));
        Assert.Equal(new[]
        {
            "IN THE HAND OF [A]",
            "WEAKSPOT STILETTO",
            DungeonHud.WeaponStats(stiletto),
            $"* {DungeonHud.ModifierReadout(stiletto)}",
            "WEAR 0 OF 30 - 30 MORE FILLS IT",
            "CAPACITY 30 - 0 OF 30 CASHED TOWARD 31",
            BetweenRunsHud.SlotsLine(stiletto),
            BetweenRunsHud.PriceLine(stiletto),
        }, detail.Select(row => row.Text));
        Assert.Equal(DungeonHud.Green, BetweenRunsHud.DetailRows(rows[3]).Single(row => row.Text.StartsWith("WEAR", StringComparison.Ordinal)).Color);
        Assert.Equal("WEAR 30 OF 30 - FULL - TIME TO ROTATE IT INTO THE SHOP", DungeonHud.WearLine(full));
        Assert.Equal("IN THE STABLE", BetweenRunsHud.WhereLine(WeaponPlace.InTheStable(3)));
        Assert.Equal("IN THE BAG OF [D]", BetweenRunsHud.WhereLine(new WeaponPlace("D", 2)));
        var serviced = new Weapon(GameContent.Current.Weapons["arming_sword"], stiletto.Forged, stiletto.Modifiers, [], 0, wear: 12, wearCashed: 42);
        Assert.Equal("CAPACITY 31 - 12 OF 31 CASHED TOWARD 32", BetweenRunsHud.CapacityLine(serviced));
        using (TestContent.UsePremise(tuning: t => t with { WearCapacityPerBar = 0 }))
            Assert.Equal("CAPACITY 30 - IT DOES NOT GROW", BetweenRunsHud.CapacityLine(serviced));

        // A caster that casts nothing says why, under its enchantments and in orange
        // (the 6a.2 review; the plan's detail panel): a staff whose first enchantment
        // applies no status, a wand whose first is no element, or either with none —
        // what the dungeon's cast gates refuse. Only a hand-edited save holds one: no
        // weapon the content ships, as dropped, says it.
        var enchantments = GameContent.Current.Enchantments.All;
        var noStatus = enchantments.First(def => def.Applies is null);
        var noElement = enchantments.First(def => def.Applies is not null && def.DamageType is null or DamageType.None);
        var staffDef = GameContent.Current.Weapons["staff_of_renewal"];
        var wandDef = GameContent.Current.Weapons.All.First(def => def.Class == WeaponClass.Wand);
        var (staffMade, wandMade) = (Made(staffDef), Made(wandDef));
        foreach (var caster in new[]
        {
            new Weapon(staffDef, staffMade.Forged, staffMade.Modifiers, [], 0),
            new Weapon(staffDef, staffMade.Forged, staffMade.Modifiers, [new Enchantment(noStatus, 1), .. staffMade.Enchantments], 0),
            new Weapon(wandDef, wandMade.Forged, wandMade.Modifiers, [], 0),
            new Weapon(wandDef, wandMade.Forged, wandMade.Modifiers, [new Enchantment(noElement, 1), .. wandMade.Enchantments], 0),
        })
        {
            Assert.True(BetweenRunsHud.CastsNothing(caster), $"{caster.Name} carrying {caster.Enchantments.Count} casts something");
            var said = BetweenRunsHud.DetailRows(new HubWeapons.Row(HubWeapons.RowKind.Weapon, HubWeapons.BagLabel, new WeaponPlace("D", 1), caster));
            var why = Assert.Single(said, line => line.Text == BetweenRunsHud.CastsNothingLine);
            Assert.Equal(DungeonHud.Orange, why.Color);
            Assert.Equal(said.Count - 5, said.ToList().IndexOf(why));   // under the enchantments, over the wear, the capacity, the slots and the price
        }
        Assert.DoesNotContain(GameContent.Current.Weapons.All.Select(Made), BetweenRunsHud.CastsNothing);
        var dStaff = campaign.Party[3].EquippedWeapon!;
        Assert.True(dStaff.IsCaster);
        Assert.DoesNotContain(BetweenRunsHud.DetailRows(new HubWeapons.Row(HubWeapons.RowKind.Weapon, HubWeapons.HandLabel, new WeaponPlace("D", 0), dStaff)),
            line => line.Text == BetweenRunsHud.CastsNothingLine);

        // A room says where it is and how a weapon gets there; a heading says nothing.
        Assert.Equal(new[] { "THE HAND OF [A] IS EMPTY", BetweenRunsHud.RoomHint }, BetweenRunsHud.DetailRows(rows[2]).Select(row => row.Text));
        Assert.Equal(new[] { "ROOM IN THE BAG OF [A]", BetweenRunsHud.RoomHint }, BetweenRunsHud.DetailRows(rows[4]).Select(row => row.Text));
        Assert.Equal("THE END OF THE STABLE - IT KEEPS ANY NUMBER OF WEAPONS", BetweenRunsHud.DetailRows(rows[^2])[0].Text);
        Assert.Empty(BetweenRunsHud.DetailRows(rows[0]));
        Assert.Empty(BetweenRunsHud.DetailRows(rows[^1]));

        // A readout's entries wrap to the column, never split, the rest indented.
        Assert.Equal(new[] { "* AAAA  BBBB", "  CCCC  DD" }, BetweenRunsHud.Listed("* ", "AAAA  BBBB  CCCC  DD", 12));
        Assert.Empty(BetweenRunsHud.Listed("* ", "", 12));
    }

    [Fact]
    public void ChoosingSkipsHeadings_AndTheWindowScrolls()
    {
        // The page is a window over the rows, the choice kept in view. W/S stop on
        // the weapons and the rooms alone — never a heading or a member — round the
        // ends, and a stable of 20 scrolls the window, which fits between the most
        // notice rows the screen can be handed and the refusal row over the keys.
        var start = CampaignState.Start(GameContent.Current);
        var stable = Enumerable.Range(0, 20).Select(_ => TestWeapons.Get("hunting_bow")).ToArray();
        var campaign = CampaignState.Restore(start.Party, start.SeenInOrder, 0, [], [], [], lastResult: null, visit: null, stable);
        const float h = 720f;
        int most = NoticeText.MaxWholeRows + NoticeText.MaxRows + 1;
        var layout = BetweenRunsHud.WeaponsPage(h, most);

        // The window under the notices, and over the refusal row, which is over the keys.
        Assert.True(layout.Top >= BetweenRunsHud.NoticeTop + most * BetweenRunsHud.NoticePitch, "the page's rows run into the notices");
        Assert.InRange(layout.Rows, 5, 40);
        Assert.True(layout.Top + (layout.Rows - 1) * BetweenRunsHud.WeaponRowPitch + 7f * BetweenRunsHud.WeaponRowScale <= layout.Bottom);
        Assert.True(layout.Bottom <= h - BetweenRunsHud.RefusalFromBottom, "the page's rows run into the refusal row");
        Assert.True(h - BetweenRunsHud.RefusalFromBottom + 7f * BetweenRunsHud.KeysLineScale <= h - BetweenRunsHud.KeysLineFromBottom, "the refusal row runs into the keys");
        Assert.True(BetweenRunsHud.WeaponsPage(h).Rows > layout.Rows);   // fewer notices, more rows

        var saysWhole = Enumerable.Range(1, NoticeText.MaxWholeRows).Select(i => $"the save's word, row {i}");
        var data = Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1");
        var (scene, _) = Hub(campaign, notices: [.. saysWhole, .. data], shownWhole: NoticeText.MaxWholeRows);
        Assert.Equal(most, scene.NoticeRows.Count);
        scene.OnKey(Keys.Tab);
        var rows = scene.Weapons.Rows();
        int choosable = rows.Count(row => row.Choosable);
        Assert.Equal(20 + 1 + 4 * 3, choosable);   // the stable's 20 and its room, and each member's hand, bag weapon and room
        var met = new HashSet<int>();
        for (int step = 0; step < choosable; step++)
        {
            int at = scene.Weapons.Chosen;
            Assert.True(rows[at].Choosable, $"the choice stopped on '{rows[at].Text}'");
            met.Add(at);
            int top = scene.Weapons.Window(layout.Rows);
            Assert.InRange(at, top, top + layout.Rows - 1);
            scene.OnKey(Keys.S);
        }
        Assert.Equal(choosable, met.Count);
        Assert.Equal(met.Min(), scene.Weapons.Chosen);   // round the end, to the first again
        scene.OnKey(Keys.W);
        Assert.Equal("  - ROOM -", ChosenText(scene));   // and up round the start, to the stable's room at the bottom
        Assert.Equal(rows.Count - layout.Rows, scene.Weapons.Window(layout.Rows));

        // The window moves only as far as the choice asks.
        Assert.Equal(0, HubWeapons.ScrollTop(0, 3, 30, 10));
        Assert.Equal(1, HubWeapons.ScrollTop(0, 10, 30, 10));
        Assert.Equal(5, HubWeapons.ScrollTop(5, 14, 30, 10));
        Assert.Equal(4, HubWeapons.ScrollTop(5, 4, 30, 10));
        Assert.Equal(20, HubWeapons.ScrollTop(25, 29, 30, 10));
        Assert.Equal(0, HubWeapons.ScrollTop(7, 8, 9, 10));   // every row fits: no scroll

        // Walked a frame at a time, as the HUD asks it (the 6a.2 review): W/S onto a
        // row already in view never move the window — it once pulled [B] in over B's
        // hand at the top whenever the choice left room, so W and S between two rows
        // in view moved it a row each press. A choice that is the first under a
        // member's name or a heading brings that row in with it, where there is room,
        // and the last row it stops on the rows under it (Phase 6a.3a).
        // The rows: the party's heading, then each member's name, hand, bag weapon
        // and room at 1-4, 5-8, 9-12 and 13-16; the stable's heading at 17, its
        // twenty at 18-37 and its room at 38; and the enchanter's heading at 39.
        const int fit = 15;
        var (walked, _) = Hub(campaign);
        walked.OnKey(Keys.Tab);
        int Press(Keys key)
        {
            walked.OnKey(key);
            return walked.Weapons.Window(fit);
        }
        Assert.Equal((2, 0), (walked.Weapons.Chosen, walked.Weapons.Window(fit)));
        while (walked.Weapons.Chosen != 20)
            Press(Keys.S);
        Assert.Equal(6, walked.Weapons.Window(fit));   // B's hand at the top: [B] has no room over it with the choice at the bottom
        for (int press = 0; press < 3; press++)
        {
            Assert.Equal(6, Press(Keys.W));
            Assert.Equal(19, walked.Weapons.Chosen);
            Assert.Equal(6, Press(Keys.S));
            Assert.Equal(20, walked.Weapons.Chosen);
        }
        while (walked.Weapons.Chosen != 7)
            Assert.Equal(6, Press(Keys.W));   // up to B's bag weapon, the window where it was
        Assert.Equal(5, Press(Keys.W));   // B's hand, the first under [B]: [B] comes in over it
        Assert.Equal(("[B]", "  HAND  TOWER GUARD - WEAR 0/30"), (walked.Weapons.Rows()[5].Text, ChosenText(walked)));
        Assert.Equal(5, Press(Keys.S));   // and stays in
        Assert.Equal(5, Press(Keys.W));
        Assert.Equal(4, Press(Keys.W));   // A's room, out of view above: just far enough
        Assert.Equal(3, Press(Keys.W));
        Assert.Equal(0, Press(Keys.W));   // A's hand: [A] and the party's heading come in over it
        Assert.Equal(40 - fit, Press(Keys.W));   // round the start to the stable's room: the last window, the enchanter's heading under it in view
        while (walked.Weapons.Chosen != 18)
            Press(Keys.W);
        Assert.Equal(17, walked.Weapons.Window(fit));   // the stable's first weapon, its heading over it
        Assert.Equal(HubWeapons.StableHeading(20), walked.Weapons.Rows()[17].Text);

        // And the tallest detail the page draws fits in the same height beside the
        // window: every weapon the content ships, as dropped and as the most a save
        // may load it carrying — ten of the longest enchantment, which on a staff or
        // a wand is no cast, so its CASTS NOTHING line too.
        var tallestWeapon = GameContent.Current.Weapons.All.SelectMany(def => new[] { Made(def), Laden(def) })
            .Select(weapon => BetweenRunsHud.DetailRows(new HubWeapons.Row(HubWeapons.RowKind.Weapon, HubWeapons.BagLabel, new WeaponPlace("A", 1), weapon)))
            .MaxBy(detail => detail.Sum(row => row.Pitch))!;
        float tallest = tallestWeapon.Sum(row => row.Pitch);
        Assert.True(layout.Top + tallest <= layout.Bottom,
            $"the tallest detail, {tallestWeapon[1].Text}'s at {tallest} px, runs into the refusal row under the most notices ({layout.Bottom - layout.Top} px)");
    }

    [Fact]
    public void CarryingTargetsSlotsRoomsAndTheStableOnly_EscPutsItBack_NothingMovesUntilPutDown()
    {
        // ENTER on a weapon takes it up: nothing moves — the campaign, and a save of
        // it, are as they were — and the keys line names it. W/S then stop on the
        // places it can be put down alone. ESC puts it back; ENTER on a place puts it
        // down there, the one moment the campaign changes.
        var campaign = CampaignState.Start(GameContent.Current);
        var (scene, host) = Hub(campaign);
        var bags = Bags(campaign);
        string saved = SaveCodec.Write(SaveGame.Capture(campaign));
        scene.OnKey(Keys.Tab);
        scene.OnKey(Keys.S);
        Assert.Equal("  BAG   STAFF OF RENEWAL - WEAR 0/30", ChosenText(scene));
        var staff = campaign.Party[0].Inventory[1]!;
        scene.OnKey(Keys.Enter);
        Assert.Equal(BetweenRunsScene.HubMode.Carrying, scene.Mode);
        Assert.Equal((new WeaponPlace("A", 1), staff), scene.Weapons.Carried);
        Assert.Equal("CARRYING THE STAFF OF RENEWAL - W/S CHOOSE WHERE - ENTER PUTS IT DOWN - ESC PUTS IT BACK",
            BetweenRunsHud.BottomLine(scene.Mode, scene.HasTitle, scene.Weapons.Carried?.Weapon).Text);
        Assert.Equal(bags, Bags(campaign));
        Assert.Empty(campaign.Stable);
        Assert.Equal(saved, SaveCodec.Write(SaveGame.Capture(campaign)));

        // Every stop is a target: a slot, a room, the stable's weapons and its room.
        var rows = scene.Weapons.Rows();
        for (int step = 0; step < rows.Count; step++)
        {
            Assert.True(rows[scene.Weapons.Chosen].Target, $"carrying, the choice stopped on '{ChosenText(scene)}'");
            scene.OnKey(Keys.S);
        }

        // ESC puts it back: nothing moved, nothing asked of the host, the choice on it.
        Assert.True(scene.HandleEscape());
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, false), (scene.Mode, scene.MenuOpen));
        Assert.Equal("  BAG   STAFF OF RENEWAL - WEAR 0/30", ChosenText(scene));
        Assert.Equal(bags, Bags(campaign));
        Assert.Equal(0, host.HubSavesAsked);

        // ENTER on its own place puts it back too, and asks nothing.
        scene.OnKey(Keys.Enter);
        scene.OnKey(Keys.Enter);
        Assert.Equal(BetweenRunsScene.HubMode.Weapons, scene.Mode);
        Assert.Equal(bags, Bags(campaign));
        Assert.Equal(0, host.HubSavesAsked);

        // Put down at the stable's end: it is there, its old slot empty, the choice
        // on it where it went, and the host asked for a save.
        scene.OnKey(Keys.Enter);
        ChooseRow(scene, HubWeapons.StableRoom);
        scene.OnKey(Keys.Enter);
        Assert.Equal(BetweenRunsScene.HubMode.Weapons, scene.Mode);
        Assert.Equal([staff], campaign.Stable);
        Assert.Null(campaign.Party[0].Inventory[1]);
        Assert.Equal("  STAFF OF RENEWAL - WEAR 0/30", ChosenText(scene));
        Assert.Equal(1, host.HubSavesAsked);

        // Taken up from the stable and put down onto C's hand: a swap — equipping —
        // the axe going to the stable where the staff was.
        var axe = campaign.Party[2].EquippedWeapon!;
        scene.OnKey(Keys.Enter);
        ChooseRow(scene, "  HAND  GREAT AXE - WEAR 0/30");
        scene.OnKey(Keys.Enter);
        Assert.Same(staff, campaign.Party[2].EquippedWeapon);
        Assert.Equal([axe], campaign.Stable);
        Assert.Equal(2, host.HubSavesAsked);
    }

    [Fact]
    public void AMoveAsksTheHostForASave()
    {
        // PHASE 6a DECISION 10: every change the hub makes to the campaign asks the
        // host for a save (IBetweenRunsHost.RequestSave), written at the frame's end
        // into the campaign's own slot. A move asks once; a weapon put back, and a
        // put-down refused, change nothing and ask nothing.
        var campaign = CampaignState.Start(GameContent.Current);
        var (scene, host) = Hub(campaign);
        scene.OnKey(Keys.Tab);

        // A's staff to the stable: asked once.
        ChooseRow(scene, "  BAG   STAFF OF RENEWAL - WEAR 0/30");
        scene.OnKey(Keys.Enter);
        ChooseRow(scene, HubWeapons.StableRoom);
        scene.OnKey(Keys.Enter);
        Assert.Equal(1, host.HubSavesAsked);

        // A's stiletto, its last weapon, to the stable: refused, said in the refusal
        // row — PHASE 6a DECISION 7 — and still taken up; nothing moved, nothing asked.
        var bags = Bags(campaign);
        ChooseRow(scene, "  HAND  WEAKSPOT STILETTO - WEAR 0/30");
        scene.OnKey(Keys.Enter);
        ChooseRow(scene, HubWeapons.StableRoom);
        scene.OnKey(Keys.Enter);
        Assert.Equal("EACH MEMBER KEEPS A WEAPON - [A] WOULD CARRY NONE", scene.Refusal);
        Assert.Equal(HubWeapons.KeepsAWeapon("A"), scene.Refusal);
        Assert.Equal(BetweenRunsScene.HubMode.Carrying, scene.Mode);
        Assert.Equal(bags, Bags(campaign));
        Assert.Single(campaign.Stable);
        Assert.Equal(1, host.HubSavesAsked);

        // Into B's bag room: refused the same way — it leaves A with none.
        ChooseRow(scene, "  BAG   - ROOM FOR 4 MORE -");
        Assert.Null(scene.Refusal);   // until the next key
        scene.OnKey(Keys.Enter);
        Assert.Equal(HubWeapons.KeepsAWeapon("A"), scene.Refusal);

        // Onto the stable's staff instead: a swap, which leaves A armed — asked again.
        ChooseRow(scene, "  STAFF OF RENEWAL - WEAR 0/30");
        scene.OnKey(Keys.Enter);
        Assert.Null(scene.Refusal);
        Assert.Equal("staff_of_renewal", campaign.Party[0].EquippedWeapon!.Id);
        Assert.Equal("weakspot_stiletto", Assert.Single(campaign.Stable).Id);
        Assert.Equal(2, host.HubSavesAsked);

        // ESC clears a refusal too, and puts the weapon back.
        ChooseRow(scene, "  HAND  STAFF OF RENEWAL - WEAR 0/30");
        scene.OnKey(Keys.Enter);
        ChooseRow(scene, HubWeapons.StableRoom);
        scene.OnKey(Keys.Enter);
        Assert.NotNull(scene.Refusal);
        Assert.True(scene.HandleEscape());
        Assert.Equal((BetweenRunsScene.HubMode.Weapons, (string?)null), (scene.Mode, scene.Refusal));
        Assert.Equal(2, host.HubSavesAsked);

        // The game's log line for the hub's save says who asked, where every other
        // automatic save between runs is the run's end's.
        var when = typeof(DistanceRpgGame).GetMethod("When", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        string When(bool byHand, bool runsEnd) => (string)when.Invoke(null, [campaign, byHand, runsEnd])!;
        Assert.Equal("between runs, after a change on the hub", When(byHand: false, runsEnd: false));
        Assert.Equal("at the run's end", When(byHand: false, runsEnd: true));
        Assert.Equal("by hand, between runs", When(byHand: true, runsEnd: false));
    }

    [Fact]
    public void AHubSaveThatFailsIsSaidAtOnce()
    {
        // PHASE 6a DECISION 10, the plan's 1h: a save of the hub's changes that could
        // not be written is said at once under the title, in the warning's orange,
        // shown whole ahead of what was there — the save's word among it — so a move
        // whose save failed is never taken for saved. Said twice, it is said once.
        string resumed = CampaignBoot.Resumed(Named(2), backup: false);
        var (scene, _) = Hub(notices: [resumed, "tuning.json: Key1 0 is below 1 - using 1"], shownWhole: 1);
        var before = scene.NoticeRows.ToList();
        string failed = CampaignBoot.NotSaved(Named(2), "The disk is full.");
        scene.SaveFailed(failed);
        var rows = scene.NoticeRows;
        var failedRows = NoticeText.Wrap(failed);
        Assert.Equal([.. failedRows, .. before], rows);
        Assert.All(failedRows, row => Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor(row)));
        Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(rows[failedRows.Count]));
        scene.SaveFailed(failed);
        Assert.Equal(rows, scene.NoticeRows);

        // A second failure, of another kind, leads in its turn; both pages still
        // leave their list and their window on the shipped window.
        string aside = CampaignBoot.SetAsideNotice("CAMPAIGN.JSON", "COULD NOT BE READ", @"C:\saves\slot-2\campaign.unreadable-1.json");
        scene.SaveFailed(aside);
        Assert.Equal(NoticeText.Wrap(aside), scene.NoticeRows.Take(NoticeText.Wrap(aside).Count));
        Assert.True(BetweenRunsHud.WeaponsPage(720f, scene.NoticeRows.Count).Rows >= 5);

        // A later save that goes through takes the failure back on the hub (the
        // 6a.2 review): the slot holds the campaign again, so the row saying it could
        // not is no longer true — taken back as the next screen's is, not left for
        // the rest of the showing. What else is said stays as it was: the save's
        // word, the data files', and a file set aside, a fact about a file.
        const string data = "tuning.json: Key1 0 is below 1 - using 1";
        scene.SaveWentThrough([]);
        Assert.Equal(NoticeText.Rows([aside, failed, resumed, data], 3), scene.NoticeRows);
        scene.SaveWentThrough([failed]);
        Assert.Equal(NoticeText.Rows([aside, resumed, data], 2), scene.NoticeRows);
        Assert.Equal([.. NoticeText.Wrap(aside), .. before], scene.NoticeRows);
        Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(scene.NoticeRows[NoticeText.Wrap(aside).Count]));

        // Failing again, it is said again, at once.
        scene.SaveFailed(failed);
        Assert.Equal(failedRows, scene.NoticeRows.Take(failedRows.Count));

        // And a failure the screen was handed — the run's end's save, which failed
        // before the screen was up — is taken back the same way, by the hub's next
        // save that goes through; a stale notice the screen never said moves nothing.
        string atTheEnd = CampaignBoot.NotSaved(Named(2), "Another program has it open.");
        var (handed, _) = Hub(notices: [atTheEnd, resumed, data], shownWhole: 2);
        handed.SaveWentThrough([atTheEnd, CampaignBoot.NotSaved(Named(2), "never said")]);
        Assert.Equal(NoticeText.Rows([resumed, data], 1), handed.NoticeRows);
    }

    // ── The enchanter (§6.2, §6.3; Phase 6a.3a) ─────────────────────────────

    [Fact]
    public void TheEnchanterSectionShowsWorkingAndStalled()
    {
        // §6.2: "the smith is working on these two" reads at a glance, and a stalled
        // weapon is shown as stalled, never silent. WEAPONS ends with the enchanter:
        // shut until the tutorial is beaten (§6.3, item 31), then how many deposits it
        // works on and each weapon in the shop, the newest first — WORKING in cyan,
        // STALLED in yellow, each with its runs — or that nothing is in it. A shop row
        // is chosen to be read, never taken up or put down on.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        Assert.Equal("THE ENCHANTER OPENS WHEN THE TUTORIAL IS BEATEN", HubWeapons.Rows(CampaignState.Start(GameContent.Current))[^1].Text);
        Assert.Equal(HubWeapons.NothingInTheShop, HubWeapons.Rows(TestShop.Opened())[^1].Text);
        Assert.Equal("THE ENCHANTER - NOTHING IN THE SHOP", HubWeapons.NothingInTheShop);

        var campaign = TestShop.Shop();
        var rows = HubWeapons.Rows(campaign);
        Assert.Equal(new[]
        {
            "  - ROOM -",
            "THE ENCHANTER - WORKS ON THE 2 NEWEST",
            "  WORKING  TOWER GUARD - 0 OF 1 RUN",
            "  WORKING  STAFF OF RENEWAL - 1 OF 2 RUNS",
            "  STALLED  STAFF OF RENEWAL - 1 OF 2 RUNS",
        }, rows.TakeLast(5).Select(row => row.Text));
        Assert.Equal("THE ENCHANTER - WORKS ON THE NEWEST", HubWeapons.EnchanterHeading(1));
        Assert.Equal("THE ENCHANTER - WORKS ON THE 10 NEWEST", HubWeapons.EnchanterHeading(10));
        var (fresh, worked, stalled) = (rows[^3], rows[^2], rows[^1]);
        Assert.Equal([2, 1, 0], new[] { fresh, worked, stalled }.Select(row => row.Deposit!.Ordinal));
        Assert.All(new[] { fresh, worked, stalled }, row => Assert.Equal((HubWeapons.RowKind.Shop, true, false, (WeaponPlace?)null), (row.Kind, row.Choosable, row.Target, row.Place)));
        Assert.Equal((false, true, 2), (worked.Stalled, stalled.Stalled, stalled.Ahead));
        Assert.All(rows.Where(row => row.Kind != HubWeapons.RowKind.Shop), row => Assert.False(row.Stalled));

        // A dungeons.json beside the game that moves retiresOnClear since, onto a
        // dungeon not beaten, closes the enchanter to new deposits — and hides
        // nothing already in the shop, which the runs that count still work.
        using (TestContent.UsePremise(tuning: TestShop.Ruled, dungeons: TestShop.TutorialMoved))
        {
            Assert.False(campaign.IsRetired(GameContent.Current.Dungeons.Tutorial.Id));
            Assert.Equal(rows.TakeLast(4).Select(row => row.Text), HubWeapons.Rows(campaign).TakeLast(4).Select(row => row.Text));
            Assert.Equal(Enchanter.Closed(GameContent.Current.Dungeons.Tutorial), Enchanter.Refusal(campaign, campaign.Party[0].Inventory[0]!, Service.Refine));
        }

        // Drawn: a worked row cyan, a stalled one yellow, a unique's name orange;
        // chosen, marked as every choice is.
        Assert.All(HubWeapons.Runs(worked), run => Assert.Equal(DungeonHud.Cyan, run.Color));
        Assert.All(HubWeapons.Runs(stalled), run => Assert.Equal(DungeonHud.Yellow, run.Color));
        Assert.Equal("> WORKING  STAFF OF RENEWAL - 1 OF 2 RUNS <", string.Concat(HubWeapons.Runs(worked, chosen: true).Select(run => run.Text)));
        var knife = new HubWeapons.Row(HubWeapons.RowKind.Shop, HubWeapons.WorkingLabel, Weapon: TestWeapons.Get("flensing_knife_unique"), Deposit: worked.Deposit);
        Assert.Equal(DungeonHud.Orange, HubWeapons.Runs(knife)[1].Color);

        // Said whole on the right: that it is at the enchanter, what it bought and its
        // runs, and whether it is worked — when it comes back — or stalled, and on how
        // many newer; then the readouts, its wear and its capacity. No slots or price:
        // its service is changing them.
        var staff = worked.Weapon!;
        Assert.Equal(new[]
        {
            "AT THE ENCHANTER",
            "STAFF OF RENEWAL",
            "ENCHANT VAMPIRIC - 1 OF 2 RUNS",
            "WORKING - BACK AFTER 1 MORE RUN THAT COUNTS",
            DungeonHud.WeaponStats(staff),
        }, BetweenRunsHud.DetailRows(worked).Take(5).Select(row => row.Text));
        Assert.Equal(new[] { "REFINE - 1 OF 2 RUNS", "STALLED - 2 NEWER DEPOSITS ARE AHEAD OF IT" }, BetweenRunsHud.DetailRows(stalled).Skip(2).Take(2).Select(row => row.Text));
        Assert.Equal((DungeonHud.Cyan, DungeonHud.Yellow), (BetweenRunsHud.DetailRows(worked)[3].Color, BetweenRunsHud.DetailRows(stalled)[3].Color));
        Assert.Equal("REFINE - 0 OF 1 RUN", BetweenRunsHud.DetailRows(fresh)[2].Text);
        Assert.Equal(CapacityLineOf(staff), BetweenRunsHud.DetailRows(worked)[^1].Text);
        Assert.DoesNotContain(BetweenRunsHud.DetailRows(worked), row => row.Text.StartsWith("ENCHANTMENTS", StringComparison.Ordinal) || row.Text.StartsWith("THE NEXT", StringComparison.Ordinal));
        Assert.Equal("WORKING - BACK AFTER 2 MORE RUNS THAT COUNT", BetweenRunsHud.WorkingLine(new Deposit(0, staff, Service.Refine, 0, 3) { RunsWorked = 1 }));
        Assert.Equal("STALLED - 1 NEWER DEPOSIT IS AHEAD OF IT", BetweenRunsHud.StalledLine(1));

        // A weapon out of the shop says its slots and its next enchantment's price
        // (R4): what its souls fill from the first, the slot it waits on, and what a
        // deposit for the next costs, or that every slot is filled.
        string Slots(string id, int cashed) => BetweenRunsHud.SlotsLine(TestShop.Worn(id, 0, cashed));
        string Price(string id, int cashed) => BetweenRunsHud.PriceLine(TestShop.Worn(id, 0, cashed));
        Assert.Equal("ENCHANTMENTS 0 - SLOTS OPEN 1 OF 5 - SLOT 2 OPENS AT CAPACITY 35", Slots("tower_guard", 0));
        Assert.Equal("ENCHANTMENTS 1 - SLOTS OPEN 1 OF 5 - SLOT 2 OPENS AT CAPACITY 35", Slots("staff_of_renewal", 126));
        Assert.Equal("ENCHANTMENTS 1 - SLOTS OPEN 2 OF 5 - SLOT 3 OPENS AT CAPACITY 40", Slots("staff_of_renewal", 160));
        Assert.Equal("ENCHANTMENTS 3 - SLOTS OPEN 1 OF 5 - SLOT 4 OPENS AT CAPACITY 45", Slots("flensing_knife_unique", 0));
        Assert.Equal("ENCHANTMENTS 3 - ALL 5 SLOTS OPEN", Slots("flensing_knife_unique", 790));
        Assert.Equal("THE NEXT NEEDS A DEPOSIT OF 30 WEAR - 1 RUN IN THE SHOP", Price("tower_guard", 0));
        Assert.Equal("THE NEXT NEEDS A DEPOSIT OF 45 WEAR - 4 RUNS IN THE SHOP", Price("flensing_knife_unique", 0));
        using (TestContent.UsePremise(tuning: t => TestShop.Ruled(t) with { EnchantmentSlots = 3 }))
        {
            Assert.Equal("ALL 3 SLOTS ARE FILLED - REFINING IS LEFT", Price("flensing_knife_unique", 0));
            Assert.Equal("ENCHANTMENTS 3 - SLOTS OPEN 1 OF 3", Slots("flensing_knife_unique", 0));
        }

        // Browsing stops on the shop's rows; carrying never does, and ENTER on one
        // takes nothing up.
        var (scene, _) = Hub(campaign);
        scene.OnKey(Keys.Tab);
        ChooseRow(scene, stalled.Text);
        Assert.Same(stalled.Deposit, scene.Weapons.ChosenRow.Deposit);
        scene.OnKey(Keys.Enter);
        Assert.Equal(BetweenRunsScene.HubMode.Weapons, scene.Mode);
        Assert.Null(scene.Weapons.Carried);
        scene.OnKey(Keys.W);
        scene.OnKey(Keys.W);
        scene.OnKey(Keys.W);
        Assert.Equal(HubWeapons.StableRoom, ChosenText(scene));
        scene.OnKey(Keys.Enter);   // the stable is empty: its room holds nothing to take up
        ChooseRow(scene, "  HAND  WEAKSPOT STILETTO - WEAR 0/30");
        scene.OnKey(Keys.Enter);
        for (int step = 0; step < scene.Weapons.Rows().Count; step++)
        {
            scene.OnKey(Keys.S);
            Assert.NotEqual(HubWeapons.RowKind.Shop, scene.Weapons.ChosenRow.Kind);
        }
        Assert.True(scene.HandleEscape());

        // And the page's end is in view with the last row a choice stops on: the
        // shop's oldest, under the most notice rows.
        var layout = BetweenRunsHud.WeaponsPage(720f, NoticeText.MaxWholeRows + NoticeText.MaxRows + 1);
        ChooseRow(scene, stalled.Text);
        int top = scene.Weapons.Window(layout.Rows);
        Assert.InRange(scene.Weapons.Rows().Count - 1, top, top + layout.Rows - 1);

        // Every string of the shop fits its column in the HUD's font, at the widest
        // numbers: two-digit runs, three-digit newer deposits, the longest shipped
        // name and enchantment; and the tallest detail a shop row draws — the most a
        // save may load a weapon carrying — fits the height beside the window.
        var longest = GameContent.Current.Weapons.All.MaxBy(def => def.Name.Length)!;
        var soul = GameContent.Current.Enchantments.All.MaxBy(def => def.Name.Length)!;
        var wide = new Deposit(999, Made(longest), Service.Enchant, 9999, ContentBounds.MostServiceRuns, soul.Id) { RunsWorked = ContentBounds.MostServiceRuns - 1 };
        foreach (var (label, waits) in new[] { (HubWeapons.WorkingLabel, false), (HubWeapons.StalledLabel, true) })
        {
            var row = new HubWeapons.Row(HubWeapons.RowKind.Shop, label, Weapon: wide.Weapon, Deposit: wide, Ahead: 999, Stalled: waits);
            foreach (bool chosen in new[] { false, true })
                Fits(string.Concat(HubWeapons.Runs(row, chosen).Select(run => run.Text)), BetweenRunsHud.WeaponRowScale, BetweenRunsHud.ColumnWidth);
            foreach (var line in BetweenRunsHud.DetailRows(row))
            {
                Fits(line.Text, line.Scale, BetweenRunsHud.ColumnWidth);
                Glyphs(line.Text);
            }
        }
        foreach (string heading in new[] { HubWeapons.EnchanterHeading(10), HubWeapons.NothingInTheShop, Enchanter.Closed(GameContent.Current.Dungeons.Tutorial) })
        {
            Fits(heading, BetweenRunsHud.WeaponRowScale, BetweenRunsHud.ColumnWidth);
            Glyphs(heading);
        }
        using (TestContent.UsePremise(tuning: t => TestShop.Ruled(t) with { FirstSlotCapacity = 9999, SlotCapacityStep = 9999, EnchantmentSlots = 10, FirstEnchantmentWear = 9999, EnchantmentWearStep = 9999 }))
        {
            var nine = new Weapon(longest, Made(longest).Forged, Made(longest).Modifiers, Enumerable.Range(0, 9).Select(_ => new Enchantment(soul, 1)).ToArray(), 0, wear: 9999, wearCashed: 0);
            foreach (string line in new[] { BetweenRunsHud.SlotsLine(nine), BetweenRunsHud.PriceLine(nine) })
            {
                Fits(line, BetweenRunsHud.DetailScale, BetweenRunsHud.ColumnWidth);
                Glyphs(line);
            }
        }
        var tallest = GameContent.Current.Weapons.All
            .Select(def => BetweenRunsHud.DetailRows(new HubWeapons.Row(HubWeapons.RowKind.Shop, HubWeapons.StalledLabel, Weapon: Laden(def), Deposit: wide, Ahead: 2, Stalled: true)))
            .Max(detail => detail.Sum(row => row.Pitch));
        Assert.True(layout.Top + tallest <= layout.Bottom, $"the tallest shop detail, {tallest} px, runs into the refusal row under the most notices ({layout.Bottom - layout.Top} px)");

        static void Fits(string text, float scale, float width)
            => Assert.True(text.Length * 6f * scale <= width, $"'{text}' is {text.Length * 6f * scale} px, past {width}");
        static void Glyphs(string text) => Assert.Matches(@"^[A-Z0-9 .,:_!?()/<>\[\]+=#*-]*$", text.ToUpperInvariant());
        static string CapacityLineOf(Weapon weapon) => BetweenRunsHud.CapacityLine(weapon);
    }

    [Fact]
    public void TheSummarySaysWhatCameBack()
    {
        // §6.2, §6.3: a run's summary says what the enchanter did on its end, after
        // whether it counted — a service back in the stable with what it attached,
        // one worked a run on, the weapons left stalled — and a run that did not
        // count says the enchanter worked nothing. The same lines on the hub and at
        // the run's end, and after a load.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        var campaign = TestShop.Shop();
        var worked = BetweenRunsHud.SummaryLines(campaign.LastResult!);
        int counted = Array.FindIndex(Texts(worked), line => line.StartsWith("RUN COUNTED", StringComparison.Ordinal));
        Assert.Equal(new[] { "THE ENCHANTER WORKED THE STAFF OF RENEWAL - 1 OF 2 RUNS", "THE ENCHANTER WORKED THE STAFF OF RENEWAL - 1 OF 2 RUNS" },
            Texts(worked).Skip(counted + 1).Take(2));
        Assert.All(worked.Skip(counted + 1).Take(2), line => Assert.Equal(DungeonHud.Cyan, line.Color));
        Assert.Equal(counted + 3, worked.Count);   // nothing stalled, and a retreat locks nothing

        // Nothing counted: the enchanter worked nothing, in grey, and names no weapon.
        var idle = TestShop.NotCounted(campaign);
        var nothing = BetweenRunsHud.SummaryLines(idle);
        int notCounted = Array.FindIndex(Texts(nothing), line => line.StartsWith("RUN NOT COUNTED", StringComparison.Ordinal));
        Assert.Equal(new DungeonHud.Run(BetweenRunsHud.WorkedNothing, DungeonHud.Grey), nothing[notCounted + 1]);
        Assert.Equal("THE ENCHANTER WORKED NOTHING - THE RUN DID NOT COUNT", BetweenRunsHud.WorkedNothing);
        Assert.Equal(notCounted + 2, nothing.Count);

        // A counted run: the Tower Guard's refinement and the staff's Vampiric back,
        // what they came back with and what their improvement rolls did — nothing,
        // on these draws (Phase 6a.4) — and A's staff, older, stalled in yellow.
        var back = TestShop.Counted(campaign);
        Assert.Equal(new[]
        {
            "STAFF OF RENEWAL IS BACK FROM THE ENCHANTER - ATTACHED VAMPIRIC - NO CHANGE",
            "TOWER GUARD IS BACK FROM THE ENCHANTER - REFINED - NO CHANGE",
            "STALLED AT THE ENCHANTER: STAFF OF RENEWAL",
        }, Texts(BetweenRunsHud.SummaryLines(back)).SkipWhile(line => !line.StartsWith("RUN COUNTED", StringComparison.Ordinal)).Skip(1));
        Assert.Equal(DungeonHud.Yellow, BetweenRunsHud.SummaryLines(back)[^1].Color);

        // The dungeon's end panel shows the same, with its key under them; and a load
        // of the save shows the same again.
        var panel = DungeonHud.RunSummaryLines(back);
        Assert.Equal(BetweenRunsHud.SummaryLines(back), panel.Take(panel.Count - 1));
        var loaded = SaveGame.Restore(SaveCodec.Read(SaveCodec.Write(SaveGame.Capture(campaign))), GameContent.Current).Campaign;
        Assert.Equal(BetweenRunsHud.SummaryLines(back), BetweenRunsHud.SummaryLines(loaded.LastResult!));

        // A report naming a weapon or an enchantment the content no longer has is
        // named by its id, never a throw while the screen draws.
        var gone = back with { Services = [new ServiceReport("gone_weapon", Service.Enchant, 1, 1, EnchantmentId: "gone_soul")] };
        Assert.Contains("GONE_WEAPON IS BACK FROM THE ENCHANTER - ATTACHED GONE_SOUL - NO CHANGE", Texts(BetweenRunsHud.SummaryLines(gone)));
    }

    [Fact]
    public void AGraftTurnsItsLineGreen()
    {
        // §6.4, Phase 6a.4: a finished service's line ends with what its improvement
        // roll did — DEEPENED and GRAFTED name the modifier as the dungeon names it,
        // NO CHANGE when the weapon came back as it went (a roll that missed, one
        // that found nothing, or none made) — and a graft, the story §6.4 says the
        // game tells, turns its line green, while every other finished line stays
        // cyan, as a worked one does. The dungeon's end panel shows the same, as far
        // as its cap of two.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        var back = TestShop.Counted(TestShop.Shop()) with
        {
            Services =
            [
                new ServiceReport("tower_guard", Service.Refine, 1, 1, Improved: new Improvement(ImprovementKind.Graft, ModifierType.Riposte), Roll: new ImproveRoll(3, 3000, 180)),
                new ServiceReport("great_axe", Service.Enchant, 2, 2, EnchantmentId: "vampiric", Improved: new Improvement(ImprovementKind.Deepen, ModifierType.CritWindow)),
                new ServiceReport("weakspot_stiletto", Service.Refine, 1, 1, Roll: new ImproveRoll(2999, 3000, 180)),
            ],
        };
        var lines = BetweenRunsHud.SummaryLines(back).SkipWhile(line => !line.Text.StartsWith("RUN COUNTED", StringComparison.Ordinal)).Skip(1).ToArray();
        var grafted = new DungeonHud.Run("TOWER GUARD IS BACK FROM THE ENCHANTER - REFINED - GRAFTED RIPOSTE", DungeonHud.Green);
        var deepened = new DungeonHud.Run("GREAT AXE IS BACK FROM THE ENCHANTER - ATTACHED VAMPIRIC - DEEPENED CRIT WINDOW", DungeonHud.Cyan);
        Assert.Equal([grafted, deepened, new("WEAKSPOT STILETTO IS BACK FROM THE ENCHANTER - REFINED - NO CHANGE", DungeonHud.Cyan)], lines);
        Assert.Equal(("GRAFTED BLOCK", "DEEPENED CRIT WINDOW", "NO CHANGE"),
            (BetweenRunsHud.Outcome(new Improvement(ImprovementKind.Graft, ModifierType.Block)),
                BetweenRunsHud.Outcome(new Improvement(ImprovementKind.Deepen, ModifierType.CritWindow)), BetweenRunsHud.Outcome(null)));

        var panel = DungeonHud.RunSummaryLines(back);
        Assert.Equal([grafted, deepened, new(BetweenRunsHud.MoreAtTheEnchanter(1), DungeonHud.Cyan)],
            panel.SkipWhile(line => !line.Text.StartsWith("RUN COUNTED", StringComparison.Ordinal)).Skip(1).Take(3));
    }

    [Fact]
    public void TheStalledLineWrapsAndCaps_AndOnlyTheHubNamesTab()
    {
        // A summary never runs away with the enchanter: it names so many finished and
        // worked services — three on the hub, two on the run's end panel, which has no
        // cut — then says how many more; and it names three stalled weapons at most,
        // wrapped as the spoils are, then how many more. Only the hub points at the
        // page that shows the rest (TAB WEAPONS): the dungeon's end panel shares the
        // lines, and TAB does nothing there.
        using var ruled = TestContent.UsePremise(tuning: TestShop.Ruled);
        var longest = GameContent.Current.Weapons.All.MaxBy(def => def.Name.Length)!;
        string name = longest.Name.ToUpperInvariant();
        var reports = Enumerable.Range(0, 5).Select(_ => new ServiceReport(longest.Id, Service.Refine, 1, 3))
            .Concat(Enumerable.Range(0, 6).Select(_ => new ServiceReport(longest.Id, Service.Refine, 0, 2, Stalled: true)))
            .ToArray();
        var result = new RunResult("spear_hall", RunOutcome.Retreat, false, 3, null, true, true, 4, null, false, [], [], [], reports);

        var hub = Texts(BetweenRunsHud.SummaryLines(result));
        int counted = Array.FindIndex(hub, line => line.StartsWith("RUN COUNTED", StringComparison.Ordinal));
        var enchanter = hub.Skip(counted + 1).ToArray();
        Assert.Equal(Enumerable.Repeat($"THE ENCHANTER WORKED THE {name} - 1 OF 3 RUNS", BetweenRunsHud.HubServiceLines), enchanter.Take(3));
        Assert.Equal("AND 2 MORE AT THE ENCHANTER", enchanter[3]);
        var stalled = enchanter.Skip(4).ToArray();
        Assert.StartsWith(BetweenRunsHud.StalledHeading, stalled[0], StringComparison.Ordinal);
        Assert.True(stalled.Length > 1, "three of the longest names wrap");
        Assert.Equal(BetweenRunsHud.MostStalledNames, stalled.Sum(line => line.Split(name).Length - 1));
        Assert.EndsWith("AND 3 MORE", stalled[^1], StringComparison.Ordinal);
        Assert.All(hub, line => Assert.True(line.Length <= 70, $"'{line}' runs off the panel"));

        // The hub alone says TAB, on the line of how many more.
        var shown = Texts(BetweenRunsHud.HubSummaryLines(result));
        Assert.Equal("AND 2 MORE AT THE ENCHANTER - TAB WEAPONS", Assert.Single(shown, line => line.Contains("TAB", StringComparison.Ordinal)));
        Assert.Equal(hub.Length, shown.Length);
        Assert.DoesNotContain(hub, line => line.Contains("TAB", StringComparison.Ordinal));
        var panel = Texts(DungeonHud.RunSummaryLines(result));
        Assert.DoesNotContain(panel, line => line.Contains("TAB", StringComparison.Ordinal));
        Assert.Equal(DungeonHud.DungeonServiceLines, panel.Count(line => line.StartsWith("THE ENCHANTER WORKED", StringComparison.Ordinal)));
        Assert.Contains("AND 3 MORE AT THE ENCHANTER", panel);

        // Fewer than the cap: no line of how many more, and nothing for TAB to show.
        var few = result with { Services = reports.Skip(3).ToArray() };
        Assert.DoesNotContain(Texts(BetweenRunsHud.HubSummaryLines(few)), line => line.Contains("MORE AT THE ENCHANTER", StringComparison.Ordinal));
        Assert.Equal(Texts(BetweenRunsHud.SummaryLines(few)), Texts(BetweenRunsHud.HubSummaryLines(few)));
    }

    [Fact]
    public void EveryWeaponsPageStringFitsItsColumn_InTheHudsFont()
    {
        // Every string the WEAPONS page draws fits its own column at its own scale:
        // the rows the left, the detail the right — each 580 px, side by side on the
        // shipped 1280 — and the lines across the window, the keys and the refusal,
        // 1240. The worst case is the longest shipped name, marked as chosen, with
        // four-digit wear and capacity; every weapon the content ships is drawn, as
        // dropped and as the most a save may load it carrying (Laden: ten of the
        // longest enchantment at tier 99). Their height beside the window, under the
        // most notice rows the screen can lead, is ChoosingSkipsHeadings'.
        using var numbers = TestContent.UsePremise(tuning: t => t with { StartingPool = 9999 });
        var weapons = GameContent.Current.Weapons;
        var longest = weapons.All.MaxBy(def => def.Name.Length)!;
        var worn = Laden(longest);
        Assert.Equal((9999, true), (worn.WearCapacity, worn.WornFull));

        static void Fits(string text, float scale, float width, string what)
            => Assert.True(text.Length * 6f * scale <= width, $"{what}: '{text}' is {text.Length * 6f * scale} px, past {width}");
        static void Glyphs(string text) => Assert.Matches(@"^[A-Z0-9 .,:_!?()/<>\[\]+=#*-]*$", text.ToUpperInvariant());

        var drawn = weapons.All.SelectMany(def => new[] { Made(def), Laden(def) }).ToList();
        foreach (var weapon in drawn)
        {
            foreach (var place in new[] { new WeaponPlace("A", 0), new WeaponPlace("A", 3), WeaponPlace.InTheStable(9998) })
            {
                string label = place.IsStable ? HubWeapons.StableLabel : place.Index == 0 ? HubWeapons.HandLabel : HubWeapons.BagLabel;
                var row = new HubWeapons.Row(HubWeapons.RowKind.Weapon, label, place, weapon);
                foreach (bool chosen in new[] { false, true })
                    Fits(string.Concat(HubWeapons.Runs(row, chosen).Select(run => run.Text)), BetweenRunsHud.WeaponRowScale, BetweenRunsHud.ColumnWidth, "a row");
                foreach (var line in BetweenRunsHud.DetailRows(row))
                    Fits(line.Text, line.Scale, BetweenRunsHud.ColumnWidth, "the detail");
            }
            Fits(BetweenRunsHud.CarryingKeysLine(weapon), BetweenRunsHud.KeysLineScale, BetweenRunsHud.FullWidth, "the keys line");
        }

        foreach (string room in new[] { HubWeapons.EmptyHand, HubWeapons.BagRoom(GameConstants.MaxInventorySlots - 1), HubWeapons.StableRoom })
        {
            var row = new HubWeapons.Row(HubWeapons.RowKind.Room, room, WeaponPlace.InTheStable(0));
            Fits(Assert.Single(HubWeapons.Runs(row, chosen: true)).Text, BetweenRunsHud.WeaponRowScale, BetweenRunsHud.ColumnWidth, "a row");
            Glyphs(room);
        }
        foreach (string heading in new[] { HubWeapons.PartyHeading, HubWeapons.StableHeading(99_999), HubWeapons.MemberLine("A") })
        {
            Fits(heading, BetweenRunsHud.WeaponRowScale, BetweenRunsHud.ColumnWidth, "a heading");
            Glyphs(heading);
        }
        foreach (var place in new[] { new WeaponPlace("A", 0), new WeaponPlace("A", 5), WeaponPlace.InTheStable(99_999) })
        {
            foreach (string text in new[] { BetweenRunsHud.WhereLine(place), BetweenRunsHud.RoomLine(place), BetweenRunsHud.RoomHint })
            {
                Fits(text, BetweenRunsHud.DetailScale, BetweenRunsHud.ColumnWidth, "the detail");
                Glyphs(text);
            }
        }
        foreach (string text in new[] { DungeonHud.WearLine(worn), BetweenRunsHud.CapacityLine(worn), DungeonHud.WearReadout(worn) })
            Glyphs(text);
        Fits(BetweenRunsHud.CastsNothingLine, BetweenRunsHud.DetailScale, BetweenRunsHud.ColumnWidth, "the detail");
        Glyphs(BetweenRunsHud.CastsNothingLine);

        foreach (string line in new[] { BetweenRunsHud.KeysLine, BetweenRunsHud.WeaponsKeysLine, HubWeapons.KeepsAWeapon("A"), BetweenRunsHud.MenuKeysLine(hasTitle: true) })
        {
            Fits(line, BetweenRunsHud.KeysLineScale, BetweenRunsHud.FullWidth, "a line across the window");
            Glyphs(line);
        }
        Glyphs(BetweenRunsHud.CarryingKeysLine(TestWeapons.Get("arming_sword")));

        // The columns stand side by side inside the shipped window, apart.
        Assert.True(640f + BetweenRunsHud.LeftColumn >= 20f);
        Assert.True(640f + BetweenRunsHud.LeftColumn + BetweenRunsHud.ColumnWidth <= 640f + BetweenRunsHud.RightColumn);
        Assert.True(640f + BetweenRunsHud.RightColumn + BetweenRunsHud.ColumnWidth <= 1280f - 20f);
    }
}
