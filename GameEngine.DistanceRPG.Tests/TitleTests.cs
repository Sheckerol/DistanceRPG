using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The title screen (the user's ruling of 2026-09-25 on the save model, R2(a);
/// Phase 5.S6): CONTINUE, NEW CAMPAIGN, LOAD GAME and QUIT, in that order;
/// CONTINUE naming what it loads, off with nothing to load; NEW CAMPAIGN asking
/// before it sets the autosave aside; LOAD GAME's list naming every slot, the
/// empty ones marked; a manual slot loaded over a different autosave asking first;
/// every ask taking ENTER alone as yes; ESC backing out, then quitting. As with
/// <c>BetweenRunsHudTests</c>, the drawing is untested, but what it draws is: the
/// strings, the keys, and where every row goes — all of it pure, off the menu
/// (<see cref="TitleMenu"/>, <see cref="SlotMenu"/>), the words
/// (<see cref="TitleText"/>, <see cref="SlotText"/>) and the layout
/// (<see cref="TitleHud.Layout"/>). The slots are the listing's as
/// <c>SaveListing.Read</c> hands them, built here row by row — a real save folder
/// where what the listing reads matters — under the numbers the runs were written
/// against (<see cref="Premise"/>), so the content collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class TitleTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>The shipped window, and the least room kept either side of a row.</summary>
    private const float W = 1280f, H = 720f, Margin = 20f;

    /// <summary>What a row the title draws can hold: the HUD's glyphs, and never an apostrophe, a quote, a brace or a backslash.</summary>
    private const string Font = "^[A-Z0-9 :,./()>=<+-]+$";

    /// <summary>A zone two hours ahead of UTC, so a row shows the player's own time rather than the save's UTC.</summary>
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone("DistanceRPG +2", TimeSpan.FromHours(2), "+2", "+2");

    /// <summary>12:32 UTC — 14:32 two hours ahead — on the day of the ruling.</summary>
    private static readonly DateTimeOffset At = new(2026, 9, 25, 12, 32, 0, TimeSpan.Zero);

    private static DungeonCatalogue Dungeons => GameContent.Current.Dungeons;

    private static SlotHeader Header(DateTimeOffset? at, int runs, string? dungeon = null, int? floor = null)
        => new(at, runs, dungeon, floor, dungeon is null ? null : 3);

    private static SlotEntry Slot(int number, SlotState state = SlotState.Readable, SlotHeader? header = null, bool unfinished = false,
        bool beyond = false, bool ahead = false, bool same = false)
        => new(number, state, state is SlotState.Readable or SlotState.FromBackup ? header ?? Header(At, 0) : null, unfinished, beyond, ahead, same);

    private static SlotEntry Empty(int number) => Slot(number, SlotState.Empty);

    /// <summary>A listing of the autosave and slots 1 to 3, each as given or empty, and any beyond them.</summary>
    private static SaveListing Listing(params SlotEntry[] slots)
    {
        var all = Enumerable.Range(0, 4).Select(n => slots.FirstOrDefault(slot => slot.Number == n) ?? Empty(n))
            .Concat(slots.Where(slot => slot.Number > 3))
            .OrderBy(slot => slot.Number)
            .ToList();
        return new SaveListing(all, []);
    }

    /// <summary>The autosave mid-run on floor 4 of the Spear Hall, three runs in: what the plan's words were written against.</summary>
    private static SlotEntry SpearHallAutosave(DateTimeOffset? at = null) => Slot(0, header: Header(at ?? At, 3, "spear_hall", 4));

    /// <summary>The rows the title draws, as laid out in the shipped window, and nothing else.</summary>
    private static IReadOnlyList<TitleHud.TitleRow> Drawn(TitleMenu menu, IReadOnlyList<string>? notices = null)
        => TitleHud.Layout(menu, notices ?? [], W, H, Dungeons, PlusTwo);

    private static string[] Options(TitleMenu menu)
        => Drawn(menu).Where(row => row.Scale == TitleHud.OptionScale).Select(row => row.Text).ToArray();

    /// <summary>A menu after the given keys.</summary>
    private static TitleMenu After(SaveListing listing, params Keys[] keys)
    {
        var menu = new TitleMenu(listing);
        foreach (var key in keys)
            menu.Press(key);
        return menu;
    }

    /// <summary>Every row fits the window: in the font, within its width with room either side, and no two overlapping.</summary>
    private static void Fits(IReadOnlyList<TitleHud.TitleRow> rows, float height = H)
    {
        Assert.All(rows, row => Assert.Matches(Font, row.Text));
        Assert.All(rows, row =>
        {
            float left = row.Left ?? W / 2f - row.Width / 2f;
            Assert.True(left >= Margin - 0.001f && left + row.Width <= W - Margin + 0.001f, $"'{row.Text}' runs off a {W}-wide window");
        });
        var down = rows.OrderBy(row => row.Y).ToList();
        for (int i = 1; i < down.Count; i++)
            Assert.True(down[i].Y >= down[i - 1].Bottom, $"'{down[i].Text}' overlaps '{down[i - 1].Text}'");
        Assert.True(down[^1].Bottom <= height, $"'{down[^1].Text}' runs off a {height}-tall window");
    }

    [Fact]
    public void TheTitleOffersContinueNewLoadQuit_InThatOrder()
    {
        // R2(a): "the game opens on a TITLE SCREEN offering, in this order,
        // CONTINUE / NEW CAMPAIGN / LOAD GAME / QUIT". Each option's value is its
        // row, top to bottom.
        Assert.Equal([TitleOption.Continue, TitleOption.NewCampaign, TitleOption.LoadGame, TitleOption.Quit], TitleMenu.Options);
        Assert.Equal([0, 1, 2, 3], TitleMenu.Options.Select(option => (int)option));
        Assert.Equal(("DISTANCE RPG", "NEW CAMPAIGN", "LOAD GAME", "QUIT"), (TitleText.Title, TitleText.NewCampaign, TitleText.LoadGame, TitleText.Quit));

        // No save anywhere: CONTINUE says so, dimmed, and the choice starts on NEW
        // CAMPAIGN; W/S and the arrows pass over CONTINUE, round the ends.
        var none = new TitleMenu(Listing());
        Assert.False(none.ContinueOn);
        Assert.Equal((int)TitleOption.NewCampaign, none.Cursor);
        Assert.Equal(["CONTINUE - NO SAVE YET", "> NEW CAMPAIGN <", "LOAD GAME", "QUIT"], Options(none));
        Assert.Equal([DungeonHud.DarkGrey, DungeonHud.Yellow, DungeonHud.White, DungeonHud.White],
            Drawn(none).Where(row => row.Scale == TitleHud.OptionScale).Select(row => row.Color));
        int[] walked = [.. new[] { Keys.W, Keys.W, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.Down, Keys.Down }.Select(key => { none.Press(key); return none.Cursor; })];
        Assert.Equal([3, 2, 1, 2, 1, 2, 3, 1], walked);
        Assert.DoesNotContain(0, walked);
        Assert.Equal(new TitleStep(TitleAction.NewCampaign), new TitleMenu(Listing()).Press(Keys.Enter));   // nothing to set aside: it begins at once

        // A save to load: CONTINUE is on, first, and chosen — ENTER at launch is one
        // key to where the game stood.
        var some = new TitleMenu(Listing(SpearHallAutosave()));
        Assert.True(some.ContinueOn);
        Assert.Equal((int)TitleOption.Continue, some.Cursor);
        Assert.Equal(["> CONTINUE - AUTOSAVE - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32 <", "NEW CAMPAIGN", "LOAD GAME", "QUIT"], Options(some));
        Assert.Equal(new TitleStep(TitleAction.Load, SaveSlots.Autosave), some.Press(Keys.Enter));
        int[] around = [.. new[] { Keys.S, Keys.S, Keys.S, Keys.S, Keys.W }.Select(key => { some.Press(key); return some.Cursor; })];
        Assert.Equal([1, 2, 3, 0, 3], around);

        // QUIT closes the game; LOAD GAME opens the list, in the options' place.
        Assert.Equal(new TitleStep(TitleAction.Quit), some.Press(Keys.Enter));
        var loading = After(Listing(SpearHallAutosave()), Keys.S, Keys.S);
        Assert.Equal(default, loading.Press(Keys.KeyPadEnter));
        Assert.NotNull(loading.Loading);
        Assert.Empty(Options(loading));

        // Any other key does nothing on the options.
        var still = new TitleMenu(Listing(SpearHallAutosave()));
        foreach (var key in new[] { Keys.N, Keys.Y, Keys.D1, Keys.Space, Keys.E, Keys.Tab })
            Assert.Equal((default(TitleStep), 0, false, (SlotMenu?)null), (still.Press(key), still.Cursor, still.Asking, still.Loading));

        // The keys the options answer, along the bottom.
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER SELECTS - ESC QUITS", TitleText.KeysLine);
        Assert.Equal((TitleText.KeysLine, DungeonHud.Grey), (Drawn(some)[^1].Text, Drawn(some)[^1].Color));
        Assert.Equal((TitleText.Title, DungeonHud.Cyan), (Drawn(some)[0].Text, Drawn(some)[0].Color));
    }

    [Fact]
    public void ContinueSaysWhatItLoads()
    {
        InTempFolder(folder =>
        {
            // CONTINUE names the most recent save by its own row — the slot, where the
            // campaign stands, the runs counted and when it was saved, in the player's
            // own zone — so what ENTER does is on the screen before it is pressed.
            // Mid-run, in the autosave, read off a real save folder: the Spear Hall's
            // second floor, one run in, saved at 12:32 UTC and shown at 14:32.
            var slots = new SaveSlots(folder, 3);
            var campaign = CampaignState.Start(GameContent.Current);
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            VisitScript.Enter(campaign, "spear_hall", CampaignRuns.FiveDeep).Down();
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.Autosave), campaign, new ViewDto("A"), savedAt: At));
            var listing = SaveListing.Read(slots, At.AddHours(1));
            string midRun = Assert.Single(Options(new TitleMenu(listing)), option => option.Contains("CONTINUE", StringComparison.Ordinal));
            Assert.Equal("> CONTINUE - AUTOSAVE - THE SPEAR HALL FLOOR 2 - 1 RUN - 2026-09-25 14:32 <", midRun);

            // Between runs, in slot 2, the newest: BETWEEN RUNS, and a count in runs.
            var betweenRuns = CampaignState.Start(GameContent.Current);
            CampaignRuns.Clear(betweenRuns, "tutorial", CampaignRuns.GoldenSeed);
            CampaignRuns.DeepRetreat(betweenRuns, "spear_hall");
            Assert.Null(CampaignBoot.Save(slots.Slot(2), betweenRuns, savedAt: At.AddMinutes(10)));
            var newer = SaveListing.Read(slots, At.AddHours(1));
            Assert.Equal(2, newer.Newest!.Number);
            Assert.Equal("CONTINUE - SLOT 2 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:42",
                TitleHud.OptionText(TitleOption.Continue, new TitleMenu(newer), Dungeons, PlusTwo));

            // No time recorded — a save from before the slots — leaves the time off;
            // the list's markers row is where NO TIME RECORDED is said.
            var untimed = new TitleMenu(Listing(Slot(0, header: Header(null, 0))));
            Assert.Equal("CONTINUE - AUTOSAVE - BETWEEN RUNS - 0 RUNS", TitleHud.OptionText(TitleOption.Continue, untimed, Dungeons, PlusTwo));

            // From a backup — the save cannot be read, the backup can — CONTINUE names
            // what the load brings back: the backup's own campaign and time, where the
            // save that cannot be read was the later.
            Assert.Null(CampaignBoot.Save(slots.Slot(3), betweenRuns, savedAt: At.AddMinutes(20)));
            Assert.Null(CampaignBoot.Save(slots.Slot(3), campaign, new ViewDto("A"), savedAt: At.AddMinutes(30)));
            File.WriteAllText(slots.Store(3).SavePath, "not a save");
            var backupNewest = SaveListing.Read(slots, At.AddHours(1));
            Assert.Equal((3, SlotState.FromBackup), (backupNewest.Newest!.Number, backupNewest.Newest.State));
            Assert.Equal("CONTINUE - SLOT 3 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:52",
                TitleHud.OptionText(TitleOption.Continue, new TitleMenu(backupNewest), Dungeons, PlusTwo));

            // Every one in the font, and within 1280 at the options' scale.
            foreach (var menu in new[] { new TitleMenu(listing), new TitleMenu(newer), untimed, new TitleMenu(backupNewest) })
                Fits(Drawn(menu));
        });
    }

    [Fact]
    public void NewCampaignAsksOnlyWhenItSetsASaveAside()
    {
        // USER DECISION 3: NEW CAMPAIGN sets the autosave slot's files aside, never
        // deleted — so it begins at once when that slot holds nothing, and asks first
        // when it holds anything. A manual slot holding a save asks nothing: every
        // manual slot is kept.
        var nothingToSetAside = After(Listing(Slot(1)), Keys.S);
        Assert.Equal((int)TitleOption.NewCampaign, nothingToSetAside.Cursor);
        Assert.Equal(new TitleStep(TitleAction.NewCampaign), nothingToSetAside.Press(Keys.Enter));
        Assert.False(nothingToSetAside.Asking);

        // A save in the autosave: ENTER asks, naming what it sets aside — where the
        // campaign there stands — and that the manual slots are kept; in yellow where
        // the keys stand, the keys line giving way to it.
        var listing = Listing(SpearHallAutosave(), Slot(2));
        TitleMenu Asked()
        {
            var menu = After(listing, Keys.S);
            Assert.Equal(default, menu.Press(Keys.Enter));
            Assert.True(menu.Asking);
            Assert.Equal(TitleAsk.NewCampaign, menu.Ask);
            return menu;
        }
        var asked = Asked();
        Assert.Equal(("NEW CAMPAIGN SETS THE AUTOSAVE ASIDE - THE SPEAR HALL FLOOR 4 - 3 RUNS",
            "AS FILES, NEVER DELETED - SLOTS 1 TO 3 ARE KEPT - ENTER: YES - ESC: NO"), TitleHud.AskRows(asked, Dungeons));
        var drawn = Drawn(asked);
        Assert.Equal([("NEW CAMPAIGN SETS THE AUTOSAVE ASIDE - THE SPEAR HALL FLOOR 4 - 3 RUNS", DungeonHud.Yellow),
            ("AS FILES, NEVER DELETED - SLOTS 1 TO 3 ARE KEPT - ENTER: YES - ESC: NO", DungeonHud.Yellow)],
            drawn.TakeLast(2).Select(row => (row.Text, row.Color)));
        Assert.DoesNotContain(drawn, row => row.Text == TitleText.KeysLine);
        Assert.Equal(H - TitleHud.KeysFromBottom, drawn[^1].Y);

        // ENTER alone confirms — the keypad's too.
        Assert.Equal(new TitleStep(TitleAction.NewCampaign), Asked().Press(Keys.Enter));
        Assert.Equal(new TitleStep(TitleAction.NewCampaign), Asked().Press(Keys.KeyPadEnter));

        // N must never confirm: N, Y, and every other key answer no and are spent —
        // the ask goes, the autosave is kept, and the key does nothing else (S moves
        // no cursor, a digit opens nothing). The guarantee the between-runs prompt's
        // test held (a key that answers a prompt never also acts), walked key by key.
        foreach (var key in new[] { Keys.N, Keys.Y, Keys.Escape, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.Space, Keys.D1, Keys.D0, Keys.Tab, Keys.LeftShift, Keys.E })
        {
            var menu = Asked();
            Assert.Equal(default, menu.Press(key));
            Assert.Equal((false, (int)TitleOption.NewCampaign, (SlotMenu?)null), (menu.Asking, menu.Cursor, menu.Loading));
        }

        // N, then N — the old prompt's yes — is two noes here: the first answers the
        // ask, the second is nothing on the options. The campaign is never replaced.
        var twice = Asked();
        Assert.Equal(new[] { default(TitleStep), default(TitleStep) }, new[] { twice.Press(Keys.N), twice.Press(Keys.N) });
        Assert.False(twice.Asking);

        // ESC, which the game asks first, answers no as well.
        var escaped = Asked();
        Assert.True(escaped.Escape());
        Assert.False(escaped.Asking);

        // A backup alone, and files none of which reads, are something to set aside
        // too: each asks, the latter naming what it can.
        Assert.Equal(TitleAsk.NewCampaign, After(Listing(Slot(0, SlotState.FromBackup, Header(At, 1))), Keys.S, Keys.Enter).Ask);
        var unreadable = After(Listing(Slot(0, SlotState.Unreadable)), Keys.Enter);   // nothing to continue: the choice starts on NEW CAMPAIGN
        Assert.Equal("NEW CAMPAIGN SETS THE AUTOSAVE ASIDE - CANNOT BE READ", TitleHud.AskRows(unreadable, Dungeons).What);

        // The slots kept are counted: one slot is SLOT 1.
        Assert.Equal("AS FILES, NEVER DELETED - SLOT 1 IS KEPT - ENTER: YES - ESC: NO", TitleText.NewCampaignAsk("BETWEEN RUNS - 1 RUN", 1).Keys);
        Assert.Throws<ArgumentOutOfRangeException>(() => TitleText.NewCampaignAsk("BETWEEN RUNS - 1 RUN", 0));
    }

    [Fact]
    public void TheLoadListNamesEverySlot_AndMarksTheEmpty()
    {
        // R2(d): "the slot list shows, per slot, enough to choose by (e.g. dungeon
        // and floor or 'between runs', run count, when it was saved) and marks empty
        // slots". A slot is its own row, and its markers' row under it when any apply.
        string Rows(SlotEntry entry, int count = 3) => string.Join(" | ", SlotText.Rows(entry, count, Dungeons, PlusTwo));

        Assert.Equal("SLOT 2 - EMPTY", Rows(Empty(2)));
        Assert.Equal("AUTOSAVE - EMPTY", Rows(Empty(0)));
        Assert.Equal("SLOT 3 - CANNOT BE READ", Rows(Slot(3, SlotState.Unreadable)));
        Assert.Equal("SLOT 1 - THE KNIFE WARRENS FLOOR 12 - 123 RUNS - 2026-09-25 14:32", Rows(Slot(1, header: Header(At, 123, "knife_warrens", 12))));
        Assert.Equal("AUTOSAVE - BETWEEN RUNS - 1 RUN - 2026-09-25 14:32", Rows(Slot(0, header: Header(At, 1))));

        // Each marker, on the row under its slot's, in the order they are drawn.
        Assert.Equal("SLOT 1 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | FROM ITS BACKUP", Rows(Slot(1, SlotState.FromBackup, Header(At, 2))));
        Assert.Equal("SLOT 1 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | ITS LAST LOAD DID NOT FINISH", Rows(Slot(1, header: Header(At, 2), unfinished: true)));
        Assert.Equal("SLOT 1 - BETWEEN RUNS - 2 RUNS - 2027-09-25 14:32 | AHEAD OF THIS CLOCK", Rows(Slot(1, header: Header(At.AddYears(1), 2), ahead: true)));
        Assert.Equal("SLOT 1 - BETWEEN RUNS - 2 RUNS | NO TIME RECORDED", Rows(Slot(1, header: Header(null, 2))));
        Assert.Equal("SLOT 5 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | BEYOND THE 3 SLOTS - LOAD ONLY", Rows(Slot(5, header: Header(At, 2), beyond: true)));
        Assert.Equal("SLOT 2 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | BEYOND THE 1 SLOT - LOAD ONLY", Rows(Slot(2, header: Header(At, 2), beyond: true), count: 1));
        Assert.Equal("SLOT 9 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | FROM ITS BACKUP - ITS LAST LOAD DID NOT FINISH - AHEAD OF THIS CLOCK - BEYOND THE 3 SLOTS - LOAD ONLY",
            Rows(Slot(9, SlotState.FromBackup, Header(At, 2), unfinished: true, beyond: true, ahead: true)));
        Assert.Equal("SLOT 4 - CANNOT BE READ | ITS LAST LOAD DID NOT FINISH - BEYOND THE 3 SLOTS - LOAD ONLY",
            Rows(Slot(4, SlotState.Unreadable, unfinished: true, beyond: true)));

        // Nothing drawn is the file's raw text: a dungeon the content does not have is
        // named as such, and a time at the calendar's end is shown in UTC, never thrown.
        Assert.Equal("SLOT 1 - AN UNKNOWN DUNGEON FLOOR 2 - 1 RUN - 2026-09-25 14:32", Rows(Slot(1, header: Header(At, 1, "no_such_dungeon", 2))));
        Assert.Equal("SLOT 1 - THE SPEAR HALL - 1 RUN - 2026-09-25 14:32", Rows(Slot(1, header: Header(At, 1, "spear_hall", null))));
        Assert.Equal("9999-12-31 23:59", SlotText.When(DateTimeOffset.MaxValue, PlusTwo));
        Assert.Equal("NO TIME RECORDED", SlotText.When(null, PlusTwo));

        // The list stands in the options' place, and the notices': ten slots, each
        // with its markers' row, the chosen one marked in yellow and the empty or
        // beyond ones dimmed, fit a 720-pixel window between the title and the keys.
        // (Every time here is ahead of the clock, so none counts, and the most recent
        // save is the tie's: the autosave, where the list starts.)
        var all = Enumerable.Range(0, SaveSlots.Most + 1)
            .Select(n => Slot(n, SlotState.FromBackup, Header(At.AddMinutes(n), 100 + n, "knife_warrens", 10 + n), unfinished: true, beyond: n > 3, ahead: true))
            .ToList();
        var menu = After(new SaveListing(all, []), Keys.S, Keys.S, Keys.Enter);
        Assert.NotNull(menu.Loading);
        string[] notices = [.. NoticeText.Rows(Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1").ToList())];
        var drawn = Drawn(menu, notices);
        Assert.DoesNotContain(drawn, row => notices.Contains(row.Text));
        Assert.DoesNotContain(drawn, row => row.Text.Contains(TitleText.NewCampaign, StringComparison.Ordinal) || row.Text.Contains("QUIT", StringComparison.Ordinal));
        var listed = drawn.Where(row => row.Scale == SlotText.Scale).ToList();
        Assert.Equal(2 * all.Count, listed.Count);
        Assert.Equal(SaveSlots.Autosave, menu.Loading!.Rows[menu.Loading.Cursor].Number);
        Assert.Equal((DungeonHud.Yellow, true), (listed[0].Color, listed[0].Text.StartsWith("> AUTOSAVE - ", StringComparison.Ordinal) && listed[0].Text.EndsWith(" <", StringComparison.Ordinal)));
        Assert.Equal((DungeonHud.White, true), (listed[2].Color, listed[2].Text.StartsWith("  SLOT 1 - ", StringComparison.Ordinal)));
        Assert.Equal(DungeonHud.DarkGrey, listed[8].Color);   // slot 4, beyond the count
        Assert.All(listed.Where((_, i) => i % 2 == 1), row => Assert.Equal(DungeonHud.Grey, row.Color));
        Assert.Single(listed.Select(row => row.Left).Distinct());   // one left-aligned block
        Fits(drawn);
        Assert.True(listed[^1].Bottom <= H - TitleHud.KeysFromBottom - TitleHud.BottomPitch, "the list runs into the keys");
        Assert.Equal((TitleText.LoadKeysLine, DungeonHud.Grey), (drawn[^1].Text, drawn[^1].Color));
        Assert.Contains(drawn, row => row.Text == TitleText.LoadGame && row.Y == TitleHud.NoticeTop);

        // An empty slot is dimmed and marked by its own row.
        var sparse = After(Listing(SpearHallAutosave()), Keys.S, Keys.S, Keys.Enter);
        var sparseRows = Drawn(sparse).Where(row => row.Scale == SlotText.Scale).ToList();
        Assert.Equal(["> AUTOSAVE - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32 <", "  SLOT 1 - EMPTY", "  SLOT 2 - EMPTY", "  SLOT 3 - EMPTY"],
            sparseRows.Select(row => row.Text));
        Assert.Equal([DungeonHud.Yellow, DungeonHud.DarkGrey, DungeonHud.DarkGrey, DungeonHud.DarkGrey], sparseRows.Select(row => row.Color));
        Fits(Drawn(sparse));
    }

    [Fact]
    public void TheLoadListTakesItsKeys()
    {
        // The load list, key by key: W/S and the arrows move over the rows that hold
        // something, round the ends; a digit takes its slot, 0 the autosave; ENTER
        // takes the one chosen; an empty slot is refused and says so until the next
        // key; ESC goes back. The list starts on the most recent save.
        var listing = Listing(Slot(0, header: Header(At, 1)), Slot(2, header: Header(At.AddHours(1), 2)), Slot(3, SlotState.Unreadable),
            Slot(5, header: Header(At, 3), beyond: true));
        var list = new SlotMenu(SlotMenuMode.Load, listing, 3);
        Assert.Equal([0, 1, 2, 3, 5], list.Rows.Select(row => row.Number));
        Assert.Equal(2, list.Rows[list.Cursor].Number);

        int Walk(Keys key)
        {
            var step = list.Press(key);
            Assert.True(step is SlotStep.Moved or SlotStep.None, $"{key} was {step}");
            return list.Rows[list.Cursor].Number;
        }
        Assert.Equal([3, 5, 0, 2, 0, 5, 3, 5], new[] { Keys.S, Keys.S, Keys.S, Keys.S, Keys.W, Keys.Up, Keys.Up, Keys.Down }.Select(Walk));

        // A digit of the number row takes its slot, as the bag's digits do in the
        // dungeon: loadable ones chosen (the unreadable one too — its load sets its
        // files aside and says so), the cursor with it.
        foreach (var (key, number) in new[] { (Keys.D2, 2), (Keys.D5, 5), (Keys.D3, 3), (Keys.D0, 0) })
        {
            Assert.Equal(SlotStep.Chosen, list.Press(key));
            Assert.Equal((number, number), (list.Chosen, list.Rows[list.Cursor].Number));
            Assert.Null(list.Said);
        }

        // The keypad's are no digits here: with NumLock off its 2 is down to the
        // player, and the engine names the physical key, so it never loads slot 2.
        foreach (var key in new[] { Keys.KeyPad2, Keys.KeyPad0, Keys.KeyPad5 })
            Assert.Equal((SlotStep.None, (int?)null, (string?)null, 0), (list.Press(key), list.Chosen, list.Said, list.Rows[list.Cursor].Number));

        // An empty slot, or one the list does not have, is refused: it says so, and the
        // choice stays where it was — until the next key, which clears what it said.
        foreach (var (key, said) in new[] { (Keys.D1, "SLOT 1 IS EMPTY"), (Keys.D7, "SLOT 7 IS EMPTY"), (Keys.D4, "SLOT 4 IS EMPTY") })
        {
            Assert.Equal(SlotStep.Refused, list.Press(key));
            Assert.Equal((said, (int?)null, 0), (list.Said, list.Chosen, list.Rows[list.Cursor].Number));
        }
        Assert.Equal(SlotStep.None, list.Press(Keys.N));
        Assert.Null(list.Said);

        // ENTER takes the one chosen; ESC goes back; any other key does nothing.
        list.Press(Keys.S);
        Assert.Equal(SlotStep.Chosen, list.Press(Keys.Enter));
        Assert.Equal(2, list.Chosen);
        Assert.Equal(SlotStep.Chosen, list.Press(Keys.KeyPadEnter));
        foreach (var key in new[] { Keys.Space, Keys.E, Keys.Tab, Keys.Y })
            Assert.Equal((SlotStep.None, (int?)null), (list.Press(key), list.Chosen));
        Assert.Equal(SlotStep.Back, list.Press(Keys.Escape));

        // Nothing anywhere: no row is chosen, W/S go nowhere, and ENTER says so.
        var empty = new SlotMenu(SlotMenuMode.Load, Listing(), 3);
        Assert.Equal(-1, empty.Cursor);
        Assert.Equal((SlotStep.None, SlotStep.None), (empty.Press(Keys.S), empty.Press(Keys.W)));
        Assert.Equal(SlotStep.Refused, empty.Press(Keys.Enter));
        Assert.Equal("EVERY SLOT IS EMPTY", empty.Said);
        Assert.Equal("AUTOSAVE IS EMPTY", (empty.Press(Keys.D0), empty.Said).Said);

        // Through the title: what the list said is drawn above its keys, in orange,
        // until the next key; a slot chosen with no autosave to set aside loads.
        var menu = After(Listing(Slot(2, header: Header(At, 2))), Keys.S, Keys.S, Keys.Enter);
        Assert.Equal(default, menu.Press(Keys.D1));
        var drawn = Drawn(menu);
        Assert.Equal(("SLOT 1 IS EMPTY", DungeonHud.Orange), (drawn[^2].Text, drawn[^2].Color));
        Assert.Equal(H - TitleHud.KeysFromBottom - TitleHud.BottomPitch, drawn[^2].Y);
        Assert.Equal(new TitleStep(TitleAction.Load, 2), menu.Press(Keys.D2));
        Assert.DoesNotContain(Drawn(menu), row => row.Text == "SLOT 1 IS EMPTY");

        // The keys line says what the keys do (S6's review): W/S and the arrows only
        // choose, and a digit loads at once, as ENTER does — never "0-9 CHOOSE",
        // which a player would read as a digit that only picks.
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER OR 0-9 LOADS - ESC BACK", TitleText.LoadKeysLine);
    }

    [Fact]
    public void LoadingASlotOverAnotherAutosaveAsksFirst()
    {
        // USER DECISION 2: a manual slot's campaign autosaves into the autosave slot,
        // so loading one while the autosave holds a different campaign asks first —
        // from the list (a digit, or ENTER) or from CONTINUE, when the manual slot is
        // the newest — and only ENTER loads it; every other key answers no, loads
        // nothing, and is spent, and the list stays up.
        var listing = Listing(SpearHallAutosave(), Slot(2, header: Header(At.AddHours(1), 1)), Slot(3, header: Header(At.AddMinutes(-5), 3), same: true));
        Assert.True(listing.NeedsRoom(2));
        TitleMenu AskedFromTheList(Keys choose)
        {
            var menu = After(listing, Keys.S, Keys.S, Keys.Enter);
            Assert.NotNull(menu.Loading);
            if (choose is Keys.Enter)
                Assert.Equal(2, menu.Loading!.Rows[menu.Loading.Cursor].Number);   // the newest, where the list starts
            Assert.Equal(default, menu.Press(choose));
            Assert.Equal((true, TitleAsk.Load, 2), (menu.Asking, menu.Ask, menu.AskSlot));
            return menu;
        }

        var asked = AskedFromTheList(Keys.D2);
        Assert.Equal(("LOADING SLOT 2 SETS THE AUTOSAVE ASIDE - THE SPEAR HALL FLOOR 4 - 3 RUNS", "AS FILES, NEVER DELETED - ENTER: YES - ESC: NO"),
            TitleHud.AskRows(asked, Dungeons));
        Assert.Equal([("LOADING SLOT 2 SETS THE AUTOSAVE ASIDE - THE SPEAR HALL FLOOR 4 - 3 RUNS", DungeonHud.Yellow), ("AS FILES, NEVER DELETED - ENTER: YES - ESC: NO", DungeonHud.Yellow)],
            Drawn(asked).TakeLast(2).Select(row => (row.Text, row.Color)));
        Assert.Contains(Drawn(asked), row => row.Scale == SlotText.Scale);   // over the list, which stays

        // ENTER alone loads it — then the host sets the autosave aside, once slot 2's
        // campaign has come back (CampaignBootTests.ALoadedSlotSetsTheAutosaveAside_NeverDeleted).
        Assert.Equal(new TitleStep(TitleAction.Load, 2), AskedFromTheList(Keys.D2).Press(Keys.Enter));
        Assert.Equal(new TitleStep(TitleAction.Load, 2), AskedFromTheList(Keys.Enter).Press(Keys.KeyPadEnter));

        foreach (var key in new[] { Keys.N, Keys.Y, Keys.D2, Keys.D0, Keys.Escape, Keys.W, Keys.S, Keys.Space, Keys.Tab })
        {
            var menu = AskedFromTheList(Keys.D2);
            Assert.Equal(default, menu.Press(key));
            Assert.False(menu.Asking);
            Assert.NotNull(menu.Loading);
            Assert.Equal(2, menu.Loading!.Rows[menu.Loading.Cursor].Number);   // spent: nothing moved
        }
        var escaped = AskedFromTheList(Keys.D2);
        Assert.True(escaped.Escape());
        Assert.Equal((false, true), (escaped.Asking, escaped.Loading is not null));

        // CONTINUE on slot 2, the newest, asks the same from the options.
        var continued = new TitleMenu(listing);
        Assert.Equal((int)TitleOption.Continue, continued.Cursor);
        Assert.Equal(default, continued.Press(Keys.Enter));
        Assert.Equal((TitleAsk.Load, 2, (SlotMenu?)null), (continued.Ask, continued.AskSlot, continued.Loading));
        Assert.Equal(new TitleStep(TitleAction.Load, 2), continued.Press(Keys.Enter));

        // No ask when nothing would be set aside: the autosave itself chosen; a slot
        // holding the autosave's own save (a hand save, then a quit that changed
        // nothing); an autosave slot that holds nothing.
        Assert.Equal(new TitleStep(TitleAction.Load, SaveSlots.Autosave), After(listing, Keys.S, Keys.S, Keys.Enter).Press(Keys.D0));
        Assert.Equal(new TitleStep(TitleAction.Load, 3), After(listing, Keys.S, Keys.S, Keys.Enter).Press(Keys.D3));
        var noAutosave = Listing(Slot(2, header: Header(At, 1)));
        Assert.Equal(new TitleStep(TitleAction.Load, 2), After(noAutosave, Keys.S, Keys.S, Keys.Enter).Press(Keys.D2));
        Assert.Equal(new TitleStep(TitleAction.Load, 2), new TitleMenu(noAutosave).Press(Keys.Enter));

        // A slot none of whose files read when the list was made is asked too — a file
        // another program held then can load a moment later, and then the autosave is
        // set aside — but in words that promise no set-aside, since its load most
        // likely brings nothing back and leaves the autosave as it was (S6's review).
        // A slot resumed from its backup keeps the plain words: its load brings the
        // backup back.
        var unreadableToo = Listing(SpearHallAutosave(), Slot(1, SlotState.Unreadable), Slot(3, SlotState.FromBackup, Header(At, 2)));
        Assert.True(unreadableToo.NeedsRoom(1));
        var unreadable = After(unreadableToo, Keys.S, Keys.S, Keys.Enter);
        Assert.Equal(default, unreadable.Press(Keys.D1));
        Assert.Equal((true, TitleAsk.Load, 1), (unreadable.Asking, unreadable.Ask, unreadable.AskSlot));
        Assert.Equal(("SLOT 1 CANNOT BE READ - IF IT LOADS, THE AUTOSAVE IS SET ASIDE - THE SPEAR HALL FLOOR 4 - 3 RUNS", "AS FILES, NEVER DELETED - ENTER: YES - ESC: NO"),
            TitleHud.AskRows(unreadable, Dungeons));
        Assert.Equal([("SLOT 1 CANNOT BE READ - IF IT LOADS, THE AUTOSAVE IS SET ASIDE - THE SPEAR HALL FLOOR 4 - 3 RUNS", DungeonHud.Yellow),
            ("AS FILES, NEVER DELETED - ENTER: YES - ESC: NO", DungeonHud.Yellow)], Drawn(unreadable).TakeLast(2).Select(row => (row.Text, row.Color)));
        Assert.Equal(new TitleStep(TitleAction.Load, 1), unreadable.Press(Keys.Enter));   // ENTER alone tries it
        var declined = After(unreadableToo, Keys.S, Keys.S, Keys.Enter, Keys.D1);
        Assert.Equal((default(TitleStep), false), (declined.Press(Keys.N), declined.Asking));
        var fromBackup = After(unreadableToo, Keys.S, Keys.S, Keys.Enter, Keys.D3);
        Assert.Equal(("LOADING SLOT 3 SETS THE AUTOSAVE ASIDE - THE SPEAR HALL FLOOR 4 - 3 RUNS", "AS FILES, NEVER DELETED - ENTER: YES - ESC: NO"),
            TitleHud.AskRows(fromBackup, Dungeons));
    }

    [Fact]
    public void EveryRowAndAskFitsTheWindow()
    {
        // The worst rows the title can draw, built and measured at 6 px a character
        // times their scale: each fits the shipped 1280-pixel window with room either
        // side, and each is in the HUD's font.
        void Within(string row, float scale)
        {
            Assert.Matches(Font, row);
            Assert.True(row.Length * TitleHud.GlyphAdvance * scale <= W - 2 * Margin, $"'{row}' is {row.Length * TitleHud.GlyphAdvance * scale} px at {scale}");
        }

        // The worst slot row: the longest dungeon name the content has, a two-digit
        // floor, three-digit runs, the time, and the marks of the chosen row.
        var longest = Dungeons.All.MaxBy(dungeon => dungeon.Name.Length)!;
        var worst = Slot(SaveSlots.Most, header: Header(At, 123, longest.Id, 12));
        string worstRow = $"> {SlotText.Row(worst, Dungeons, PlusTwo)} <";
        Assert.Equal($"> SLOT 9 - {longest.Name.ToUpperInvariant()} FLOOR 12 - 123 RUNS - 2026-09-25 14:32 <", worstRow);
        Within(worstRow, SlotText.Scale);

        // The worst markers' row: every marker that can stand together, indented.
        var everyMarker = Slot(SaveSlots.Most, SlotState.FromBackup, Header(At, 1), unfinished: true, beyond: true, ahead: true);
        string markers = SlotText.Rows(everyMarker, 3, Dungeons, PlusTwo)[1];
        Assert.Equal(101, markers.Length);
        Within($"    {markers}", SlotText.Scale);

        // Each ask's two rows, at the keys line's scale: NEW CAMPAIGN's at nine slots,
        // a load's of slot 9 — and of slot 9 listed CANNOT BE READ, the longer — each
        // naming the worst autosave.
        string place = SlotText.Summary(worst with { Number = SaveSlots.Autosave }, Dungeons);
        foreach (var (what, keys) in new[]
            {
                TitleText.NewCampaignAsk(place, SaveSlots.Most), TitleText.LoadAsk(SaveSlots.Most, place), TitleText.LoadAsk(SaveSlots.Most, place, unreadable: true),
            })
        {
            Within(what, TitleHud.KeysScale);
            Within(keys, TitleHud.KeysScale);
        }

        // CONTINUE with the longest name, marked, at the options' scale; and the keys.
        var newest = worst with { Number = SaveSlots.Autosave };
        Within($"> {TitleText.Continue(SlotText.Row(newest, Dungeons, PlusTwo))} <", TitleHud.OptionScale);
        Within(TitleText.NoSaveYet, TitleHud.OptionScale);
        Within(TitleText.KeysLine, TitleHud.KeysScale);
        Within(TitleText.LoadKeysLine, TitleHud.KeysScale);
        Within(TitleText.Title, TitleHud.TitleScale);
        Within(SlotText.NothingToLoad, TitleHud.KeysScale);
        Within(SlotText.IsEmpty(SaveSlots.Most), TitleHud.KeysScale);

        // And laid out, every screen the title can show fits a 720-pixel window: the
        // options under the most notices the screen can be handed (the save's word
        // whole to its ceiling, three more rows and the row that counts the rest);
        // the list of ten worst slots; each ask over them.
        var worstListing = new SaveListing(
            [.. Enumerable.Range(0, SaveSlots.Most + 1).Select(n => Slot(n, SlotState.FromBackup, Header(At.AddMinutes(n), 123, longest.Id, 12), unfinished: true, beyond: n > 3, ahead: true))], []);
        var mostNotices = NoticeText.Rows([.. Enumerable.Range(1, NoticeText.MaxWholeRows).Select(i => $"the save word row {i}"),
            .. Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1")], whole: NoticeText.MaxWholeRows);
        Assert.Equal(NoticeText.MaxWholeRows + NoticeText.MaxRows + 1, mostNotices.Count);
        Fits(Drawn(new TitleMenu(worstListing), mostNotices));
        Fits(Drawn(After(worstListing, Keys.S, Keys.Enter), mostNotices));                    // NEW CAMPAIGN's ask, at nine slots
        Fits(Drawn(After(worstListing, Keys.S, Keys.S, Keys.Enter), mostNotices));            // the list
        var overTheList = After(new SaveListing([.. worstListing.Slots.Select(slot => slot.Number == 0 ? slot with { State = SlotState.Readable } : slot)], []),
            Keys.S, Keys.S, Keys.Enter, Keys.D9);
        Assert.Equal(TitleAsk.Load, overTheList.Ask);
        Fits(Drawn(overTheList, mostNotices));                                                // a load's ask over the list
        var overAnUnreadable = After(new SaveListing([.. overTheList.Listing.Slots.Select(slot => slot.Number == SaveSlots.Most ? slot with { State = SlotState.Unreadable, Header = null } : slot)], []),
            Keys.S, Keys.S, Keys.Enter, Keys.D9);
        Assert.StartsWith("SLOT 9 CANNOT BE READ - IF IT LOADS, ", TitleHud.AskRows(overAnUnreadable, Dungeons).What, StringComparison.Ordinal);
        Fits(Drawn(overAnUnreadable, mostNotices));                                           // and over a slot that cannot be read, the longer
        Assert.True(Drawn(new TitleMenu(worstListing), mostNotices).Single(row => row.Text.StartsWith("QUIT", StringComparison.Ordinal)).Bottom
            <= H - TitleHud.KeysFromBottom - TitleHud.BottomPitch, "the options run into the bottom rows");
    }

    [Fact]
    public void EscapeBacksOutThenQuits()
    {
        // ESC is the game's to route: it asks the title first. The list goes back to
        // the options; an ask says no — over the list, the list stays; and on the
        // options it is not the title's, so the game closes, as QUIT does.
        var listing = Listing(SpearHallAutosave(), Slot(2, header: Header(At.AddHours(1), 1)));
        var menu = new TitleMenu(listing);
        Assert.False(menu.Escape());

        menu.Press(Keys.S);
        menu.Press(Keys.S);
        menu.Press(Keys.Enter);
        Assert.NotNull(menu.Loading);
        Assert.True(menu.Escape());
        Assert.Null(menu.Loading);
        Assert.Equal((int)TitleOption.LoadGame, menu.Cursor);   // back where it was
        Assert.False(menu.Escape());

        menu.Press(Keys.Enter);
        menu.Press(Keys.D2);
        Assert.True(menu.Asking);
        Assert.True(menu.Escape());                          // the ask: no
        Assert.Equal((false, true), (menu.Asking, menu.Loading is not null));
        Assert.True(menu.Escape());                          // the list: back
        Assert.False(menu.Escape());                         // the options: the game's

        menu.Press(Keys.W);
        menu.Press(Keys.Enter);
        Assert.Equal(TitleAsk.NewCampaign, menu.Ask);
        Assert.True(menu.Escape());
        Assert.False(menu.Escape());

        // The scene routes it the same, built and read, never played: the notices it
        // was handed laid out under the title, the save's word whole.
        var host = new NoTitleHost();
        string[] notices = [CampaignBoot.QuitUnsaved, "tuning.json: MovementBudget 20000 is above 10000 - using 10000"];
        var scene = new TitleScene(game: null!, host, listing, shownWhole: 1, notices);
        Assert.Equal([.. NoticeText.Wrap(CampaignBoot.QuitUnsaved), "TUNING.JSON: MOVEMENTBUDGET 20000 IS ABOVE 10000 - USING 10000"], scene.NoticeRows);
        Assert.False(scene.HandleEscape());
        scene.Menu.Press(Keys.S);
        scene.Menu.Press(Keys.S);
        scene.Menu.Press(Keys.Enter);
        Assert.True(scene.HandleEscape());
        Assert.False(scene.HandleEscape());
        Assert.Equal(0, host.Calls);
    }

    /// <summary>A fresh empty folder under the system's temp folder, deleted with everything in it once <paramref name="use"/> is done.</summary>
    private static void InTempFolder(Action<string> use)
    {
        var folder = Directory.CreateTempSubdirectory("distancerpg-");
        try
        {
            use(folder.FullName);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>The host of a title that is built and read, never played: nothing is chosen on it.</summary>
    private sealed class NoTitleHost : ITitleHost
    {
        public int Calls { get; private set; }

        public void LoadSlot(int slot) => Calls++;

        public void BeginNewCampaign() => Calls++;

        public void Quit() => Calls++;
    }
}
