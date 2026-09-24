using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The screen between runs' text (§4.3, §4.5): the tutorial's warning, said
/// before the run is committed to (PHASE4-084); a locked dungeon's countdown in
/// runs (PHASE4-133); every dungeon named with its theme and never its depth;
/// and the last run's summary, which is the run's own. As with
/// <c>HudTextTests</c>, the drawing is untested, but what it draws is tested:
/// the strings, the list's rows in the order the screen draws them, where the
/// choice starts and how it moves, and the one piece of arithmetic that keeps
/// the list on the screen.
/// </summary>
public class BetweenRunsHudTests
{
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
}
