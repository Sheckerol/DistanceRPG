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
        // screen between runs names the keys that choose and descend, and ESC, which
        // opens the menu — where the campaign is saved by hand and play is left for
        // the title. NEW CAMPAIGN is the title's now, so no key here begins one, and
        // the line names none.
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER DESCENDS - ESC MENU", BetweenRunsHud.KeysLine);
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
        Assert.Equal((BetweenRunsHud.MenuKeysLine(hasTitle: true), DungeonHud.Grey), BetweenRunsHud.BottomLine(menu: true));
        Assert.Equal((BetweenRunsHud.MenuKeysLine(hasTitle: false), DungeonHud.Grey), BetweenRunsHud.BottomLine(menu: true, hasTitle: false));
        foreach (string line in new[] { BetweenRunsHud.MenuKeysLine(hasTitle: true), BetweenRunsHud.MenuKeysLine(hasTitle: false) })
        {
            Assert.Matches("^[A-Z0-9 /-]+$", line);
            Assert.True(line.Length * 6f * BetweenRunsHud.KeysLineScale <= 1280f - 40f, line);
        }

        // Where a key goes, walked key by key: with the menu up, ENTER, W, S and the
        // arrows are the menu's — which does nothing with them, so nothing descends
        // behind it — as every other key is; ESC is always the game's, which asks
        // HandleEscape.
        foreach (var key in new[] { Keys.Enter, Keys.KeyPadEnter, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.D1, Keys.D2, Keys.D3, Keys.N, Keys.Space })
            Assert.Equal(BetweenRunsScene.KeyRoute.Menu, BetweenRunsScene.Route(menu: true, key));
        foreach (var key in new[] { Keys.Enter, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.D2, Keys.N })
            Assert.Equal(BetweenRunsScene.KeyRoute.List, BetweenRunsScene.Route(menu: false, key));
        foreach (bool menu in new[] { false, true })
            Assert.Equal(BetweenRunsScene.KeyRoute.Game, BetweenRunsScene.Route(menu, Keys.Escape));

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
}
