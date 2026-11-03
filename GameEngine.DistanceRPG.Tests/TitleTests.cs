using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The title screen (the user's ruling of 2026-09-25 on the save model, R2(a);
/// Phase 5.S6): CONTINUE, NEW CAMPAIGN, LOAD GAME and QUIT, in that order;
/// CONTINUE naming what it loads, off with nothing to load; NEW CAMPAIGN choosing
/// the slot it begins in, asking only over one in use; LOAD GAME's list naming
/// every slot, the empty ones marked; a slot chosen loading at once, the campaign
/// it brings back saving into it (the user's ruling of 2026-09-25 on the save
/// slots, R5); every ask taking ENTER alone as yes; ESC backing out, then
/// quitting. As with
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
        bool beyond = false, bool ahead = false, bool refused = false)
        => new(number, state, state is SlotState.Readable or SlotState.FromBackup ? header ?? Header(At, 0) : null, unfinished, beyond, ahead, refused);

    private static SlotEntry Empty(int number) => Slot(number, SlotState.Empty);

    /// <summary>
    /// A listing of slots 1 to 3, each as given or empty, and any given beyond them
    /// — and the old autosave's row only when one is given, as the listing lists it
    /// only while the save folder itself holds a save or a backup.
    /// </summary>
    private static SaveListing Listing(params SlotEntry[] slots)
    {
        var all = Enumerable.Range(1, 3).Select(n => slots.FirstOrDefault(slot => slot.Number == n) ?? Empty(n))
            .Concat(slots.Where(slot => slot.Number == SaveSlots.OldAutosave || slot.Number > 3))
            .OrderBy(slot => slot.Number)
            .ToList();
        return new SaveListing(all, []);
    }

    /// <summary>Slot <paramref name="number"/> — the old autosave for 0 — mid-run on floor 4 of the Spear Hall, three runs in: what the plan's words were written against.</summary>
    private static SlotEntry SpearHall(int number, DateTimeOffset? at = null) => Slot(number, header: Header(at ?? At, 3, "spear_hall", 4));

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
        var newCampaign = new TitleMenu(Listing());
        Assert.Equal(default, newCampaign.Press(Keys.Enter));   // NEW CAMPAIGN opens its list, to choose the slot it begins in
        Assert.Equal(SlotMenuMode.NewCampaign, newCampaign.List!.Mode);
        Assert.Empty(Options(newCampaign));

        // A save to load: CONTINUE is on, first, and chosen — ENTER at launch is one
        // key to where the game stood — naming the slot it loads. The old autosave,
        // while the save folder itself holds a campaign, is continued the same way.
        var some = new TitleMenu(Listing(SpearHall(2)));
        Assert.True(some.ContinueOn);
        Assert.Equal((int)TitleOption.Continue, some.Cursor);
        Assert.Equal(["> CONTINUE - SLOT 2 - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32 <", "NEW CAMPAIGN", "LOAD GAME", "QUIT"], Options(some));
        Assert.Equal(new TitleStep(TitleAction.Load, 2), some.Press(Keys.Enter));
        var old = new TitleMenu(Listing(SpearHall(SaveSlots.OldAutosave)));
        Assert.Equal("> CONTINUE - OLD AUTOSAVE - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32 <", Options(old)[0]);
        Assert.Equal(new TitleStep(TitleAction.Load, SaveSlots.OldAutosave), old.Press(Keys.Enter));
        int[] around = [.. new[] { Keys.S, Keys.S, Keys.S, Keys.S, Keys.W }.Select(key => { some.Press(key); return some.Cursor; })];
        Assert.Equal([1, 2, 3, 0, 3], around);

        // QUIT closes the game; LOAD GAME opens the list, in the options' place.
        Assert.Equal(new TitleStep(TitleAction.Quit), some.Press(Keys.Enter));
        var loading = After(Listing(SpearHall(2)), Keys.S, Keys.S);
        Assert.Equal(default, loading.Press(Keys.KeyPadEnter));
        Assert.Equal(SlotMenuMode.Load, loading.List!.Mode);
        Assert.Empty(Options(loading));

        // Any other key does nothing on the options.
        var still = new TitleMenu(Listing(SpearHall(2)));
        foreach (var key in new[] { Keys.N, Keys.Y, Keys.D1, Keys.Space, Keys.E, Keys.Tab })
            Assert.Equal((default(TitleStep), 0, false, (SlotMenu?)null), (still.Press(key), still.Cursor, still.Asking, still.List));

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
            // Mid-run, in slot 1, read off a real save folder: the Spear Hall's second
            // floor, one run in, saved at 12:32 UTC and shown at 14:32.
            var slots = new SaveSlots(folder, 3);
            var campaign = CampaignState.Start(GameContent.Current);
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            VisitScript.Enter(campaign, "spear_hall", CampaignRuns.FiveDeep).Down();
            Assert.Null(CampaignBoot.Save(slots.Slot(1), campaign, new ViewDto("A"), savedAt: At));
            var listing = SaveListing.Read(slots, At.AddHours(1));
            string midRun = Assert.Single(Options(new TitleMenu(listing)), option => option.Contains("CONTINUE", StringComparison.Ordinal));
            Assert.Equal("> CONTINUE - SLOT 1 - THE SPEAR HALL FLOOR 2 - 1 RUN - 2026-09-25 14:32 <", midRun);

            // Between runs, in slot 2, the newest: BETWEEN RUNS, and a count in runs.
            var betweenRuns = CampaignState.Start(GameContent.Current);
            CampaignRuns.Clear(betweenRuns, "tutorial", CampaignRuns.GoldenSeed);
            CampaignRuns.DeepRetreat(betweenRuns, "spear_hall");
            Assert.Null(CampaignBoot.Save(slots.Slot(2), betweenRuns, savedAt: At.AddMinutes(10)));
            var newer = SaveListing.Read(slots, At.AddHours(1));
            Assert.Equal(2, newer.Newest!.Number);
            Assert.Equal("CONTINUE - SLOT 2 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:42",
                TitleHud.OptionText(TitleOption.Continue, new TitleMenu(newer), Dungeons, PlusTwo));

            // No time recorded — a save from before the slots, which stands in the save
            // folder itself, the old autosave — leaves the time off; the list's markers
            // row is where NO TIME RECORDED is said.
            var untimed = new TitleMenu(Listing(Slot(SaveSlots.OldAutosave, header: Header(null, 0))));
            Assert.Equal("CONTINUE - OLD AUTOSAVE - BETWEEN RUNS - 0 RUNS", TitleHud.OptionText(TitleOption.Continue, untimed, Dungeons, PlusTwo));

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
    public void NewCampaignChoosesItsSlot_AskingOnlyOverOneInUse()
    {
        // The user's ruling of 2026-09-25 on the save slots (R5): "you choose a save
        // slot and any saving happens on that save slot". NEW CAMPAIGN opens a list,
        // in the options' place, of slots 1 to the count — never the old autosave,
        // never a slot beyond the count — every one of which a campaign can begin in. The
        // choice starts on the lowest empty slot, so an ENTER pressed without looking
        // sets nothing aside while a slot is free. An empty slot begins at once; one
        // that holds anything asks first, naming the campaign in it, as the old games
        // ask to overwrite a file (SLOT DECISION 2), and only ENTER says yes.
        var listing = Listing(SpearHall(SaveSlots.OldAutosave), Slot(1, header: Header(At, 3, "spear_hall", 4)), Slot(3, SlotState.Unreadable),
            Slot(5, header: Header(At, 2), beyond: true));
        TitleMenu Opened()
        {
            var menu = After(listing, Keys.S);   // past CONTINUE, on NEW CAMPAIGN
            Assert.Equal(default, menu.Press(Keys.Enter));
            Assert.Equal(SlotMenuMode.NewCampaign, menu.List!.Mode);
            Assert.False(menu.Asking);
            return menu;
        }
        var opened = Opened();
        Assert.Equal([1, 2, 3], opened.List!.Rows.Select(row => row.Number));
        Assert.Equal((2, 2), (listing.NewCampaignSlot, opened.List.Rows[opened.List.Cursor].Number));   // the lowest empty slot

        // Drawn: its heading where the notices stand, its slots — each white, since a
        // campaign can begin in any of them, the choice in yellow — and its keys.
        var drawn = Drawn(opened);
        Assert.Contains(drawn, row => row.Text == "NEW CAMPAIGN - CHOOSE A SLOT" && row.Y == TitleHud.NoticeTop);
        Assert.Equal("NEW CAMPAIGN - CHOOSE A SLOT", TitleText.NewCampaignHeading);
        var slotRows = drawn.Where(row => row.Scale == SlotText.Scale).ToList();
        Assert.Equal(["  SLOT 1 - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32", "> SLOT 2 - EMPTY <", "  SLOT 3 - CANNOT BE READ"], slotRows.Select(row => row.Text));
        Assert.Equal([DungeonHud.White, DungeonHud.Yellow, DungeonHud.White], slotRows.Select(row => row.Color));
        Assert.Empty(Options(opened));
        Assert.Equal(("W/S OR ARROWS CHOOSE - ENTER OR 1-3 BEGINS - ESC BACK", DungeonHud.Grey), (drawn[^1].Text, drawn[^1].Color));
        Assert.Equal(TitleText.NewCampaignKeysLine(3), drawn[^1].Text);
        Fits(drawn);

        // The keys line's digits are the slots the list offers, 1 to the count, since
        // a digit above it begins nothing — never 0, the old autosave.
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER OR 1 BEGINS - ESC BACK", TitleText.NewCampaignKeysLine(1));
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER OR 1-9 BEGINS - ESC BACK", TitleText.NewCampaignKeysLine(SaveSlots.Most));
        Assert.Throws<ArgumentOutOfRangeException>(() => TitleText.NewCampaignKeysLine(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TitleText.NewCampaignKeysLine(SaveSlots.Most + 1));

        // W/S and the arrows move over every slot, round the ends.
        int Walk(Keys key)
        {
            Assert.Equal(default, opened.Press(key));
            return opened.List!.Rows[opened.List.Cursor].Number;
        }
        Assert.Equal([3, 1, 2, 1, 3, 2], new[] { Keys.S, Keys.S, Keys.Down, Keys.W, Keys.Up, Keys.W }.Select(Walk));

        // An empty slot begins at once: ENTER on it, the keypad's too, or its digit.
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 2), Opened().Press(Keys.Enter));
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 2), Opened().Press(Keys.KeyPadEnter));
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 2), Opened().Press(Keys.D2));

        // A slot that holds anything asks — by its digit, or ENTER on it — naming what
        // it holds, in yellow where the keys stand, the keys line giving way to it.
        const string Begin = "BEGIN HERE - WHAT IT HOLDS LEAVES LOAD GAME, KEPT AS FILES, NEVER DELETED - ENTER: YES - ESC: NO";
        TitleMenu Asked(int slot, params Keys[] keys)
        {
            var menu = Opened();
            foreach (var key in keys)
                Assert.Equal(default, menu.Press(key));
            Assert.True(menu.Asking);
            Assert.Equal((slot, slot), (menu.List!.Asking!.Number, menu.List.Rows[menu.List.Cursor].Number));
            return menu;
        }
        var asked = Asked(1, Keys.D1);
        Assert.Equal(("SLOT 1 HOLDS THE SPEAR HALL FLOOR 4 - 3 RUNS", Begin), TitleHud.AskRows(asked, Dungeons));
        var askDrawn = Drawn(asked);
        Assert.Equal([("SLOT 1 HOLDS THE SPEAR HALL FLOOR 4 - 3 RUNS", DungeonHud.Yellow), (Begin, DungeonHud.Yellow)],
            askDrawn.TakeLast(2).Select(row => (row.Text, row.Color)));
        Assert.DoesNotContain(askDrawn, row => row.Text == TitleText.NewCampaignKeysLine(3));
        Assert.Equal(H - TitleHud.KeysFromBottom, askDrawn[^1].Y);
        Fits(askDrawn);
        Assert.Equal(TitleHud.AskRows(asked, Dungeons), TitleHud.AskRows(Asked(1, Keys.W, Keys.Enter), Dungeons));

        // Files none of which reads are something to set aside too: the ask names
        // what it can. A backup alone is a campaign, named by the backup.
        Assert.Equal(("SLOT 3 CANNOT BE READ", Begin), TitleHud.AskRows(Asked(3, Keys.D3), Dungeons));
        Assert.Equal(("SLOT 3 CANNOT BE READ", Begin), TitleText.NewCampaignAsk(3, SlotText.CannotBeRead, unreadable: true));
        var backupOnly = After(Listing(Slot(1, SlotState.FromBackup, Header(At, 1))), Keys.S, Keys.Enter, Keys.D1);
        Assert.Equal(("SLOT 1 HOLDS BETWEEN RUNS - 1 RUN", Begin), TitleHud.AskRows(backupOnly, Dungeons));

        // ENTER alone says yes — the keypad's too: the new campaign begins there.
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 1), Asked(1, Keys.D1).Press(Keys.Enter));
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 3), Asked(3, Keys.D3).Press(Keys.KeyPadEnter));

        // N must never say yes: N, Y, the slot's own digit, the arrows and every other
        // key say no and are spent — the ask goes, what the slot holds is kept, the
        // list stays with the choice where it was, and the key does nothing else (S
        // moves nothing, another digit begins nothing, and nothing is said).
        foreach (var key in new[] { Keys.N, Keys.Y, Keys.D1, Keys.D2, Keys.D0, Keys.D5, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.Space, Keys.Tab, Keys.LeftShift, Keys.E, Keys.KeyPad2 })
        {
            var menu = Asked(1, Keys.D1);
            Assert.Equal(default, menu.Press(key));
            Assert.Equal((false, SlotMenuMode.NewCampaign, 1, (string?)null), (menu.Asking, menu.List!.Mode, menu.List.Rows[menu.List.Cursor].Number, menu.List.Said));
        }

        // N, then N — the old prompt's yes — is two noes here.
        var twice = Asked(1, Keys.D1);
        Assert.Equal(new[] { default(TitleStep), default(TitleStep) }, new[] { twice.Press(Keys.N), twice.Press(Keys.N) });
        Assert.False(twice.Asking);

        // ESC, which the game asks first, answers the ask no; then it closes the list,
        // back to the options where NEW CAMPAIGN is still chosen; then it is the game's.
        var escaped = Asked(1, Keys.D1);
        Assert.True(escaped.Escape());
        Assert.Equal((false, SlotMenuMode.NewCampaign), (escaped.Asking, escaped.List!.Mode));
        Assert.True(escaped.Escape());
        Assert.Equal(((int)TitleOption.NewCampaign, (SlotMenu?)null), (escaped.Cursor, escaped.List));
        Assert.False(escaped.Escape());

        // Never the old autosave, and never a slot beyond the count: 0 and the
        // keypad do nothing, and a digit above the count says why — in the HUD's
        // font, in orange above the keys — until the next key. Slot 5 holds a save
        // beyond the count, and is still never offered.
        var never = Opened();
        foreach (var key in new[] { Keys.D0, Keys.KeyPad0, Keys.KeyPad1, Keys.KeyPad2, Keys.KeyPad5 })
            Assert.Equal((default(TitleStep), (string?)null, false, 2), (never.Press(key), never.List!.Said, never.Asking, never.List.Rows[never.List.Cursor].Number));
        Assert.Equal(default, never.Press(Keys.D5));
        Assert.Equal(("SLOT 5 IS BEYOND THE 3 SLOTS", false, 2), (never.List!.Said, never.Asking, never.List.Rows[never.List.Cursor].Number));
        var saidDrawn = Drawn(never);
        Assert.Equal(("SLOT 5 IS BEYOND THE 3 SLOTS", DungeonHud.Orange), (saidDrawn[^2].Text, saidDrawn[^2].Color));
        Assert.Equal(H - TitleHud.KeysFromBottom - TitleHud.BottomPitch, saidDrawn[^2].Y);
        Fits(saidDrawn);
        Assert.Equal(default, never.Press(Keys.D9));
        Assert.Equal("SLOT 9 IS BEYOND THE 3 SLOTS", never.List!.Said);
        Assert.Equal(default, never.Press(Keys.N));
        Assert.Null(never.List!.Said);

        // At a count of one — slot 2 beyond it, holding a save — the list is slot 1
        // alone, chosen though it is in use (no slot is free), and 2 says it is beyond
        // the one slot.
        var one = new TitleMenu(new SaveListing([SpearHall(SaveSlots.OldAutosave), Slot(1, header: Header(At, 1)), Slot(2, header: Header(At, 2), beyond: true)], []));
        Assert.Equal(1, one.Count);
        one.Press(Keys.S);
        one.Press(Keys.Enter);
        Assert.Equal([1], one.List!.Rows.Select(row => row.Number));
        Assert.Equal(1, one.List.Rows[one.List.Cursor].Number);
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER OR 1 BEGINS - ESC BACK", Drawn(one)[^1].Text);
        Assert.Equal(default, one.Press(Keys.D2));
        Assert.Equal("SLOT 2 IS BEYOND THE 1 SLOT", one.List!.Said);
        Assert.Matches(Font, one.List.Said);
        Assert.Equal(default, one.Press(Keys.Enter));
        Assert.Equal(1, one.List!.Asking!.Number);   // slot 1 holds a campaign: it asks

        // A slot that takes no saves this session asks nothing: no campaign can begin
        // there, so a yes could set nothing aside (the audit of the ruling on the
        // save slots). It is chosen at once — by its digit, or ENTER on it — and the
        // game says why and begins nothing (DistanceRpgGame.BeginNewCampaign); its row
        // says so where the list stands. A slot in use that takes saves still asks.
        var refusing = Listing(Slot(1, header: Header(At, 3, "spear_hall", 4), refused: true), Slot(2), Slot(3, SlotState.Unreadable, refused: true));
        TitleMenu Refusing() => After(refusing, Keys.S, Keys.Enter);
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 1), Refusing().Press(Keys.D1));
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 1), Refusing().Press(Keys.Enter));   // every slot in use: the list starts on slot 1
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 3), Refusing().Press(Keys.D3));
        var stillAsks = Refusing();
        Assert.Equal(default, stillAsks.Press(Keys.D2));
        Assert.Equal(2, stillAsks.List!.Asking!.Number);
        var refusingDrawn = Drawn(Refusing());
        Assert.Equal(["> SLOT 1 - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32 <", "    TAKES NO SAVES THIS SESSION", "  SLOT 2 - BETWEEN RUNS - 0 RUNS - 2026-09-25 14:32",
            "  SLOT 3 - CANNOT BE READ", "    TAKES NO SAVES THIS SESSION"], refusingDrawn.Where(row => row.Scale == SlotText.Scale).Select(row => row.Text));
        Fits(refusingDrawn);

        // Every slot in use: the choice starts on slot 1, which asks. Nothing
        // anywhere: slot 1, which begins at once — what the old autosave holds, or
        // whether it is listed at all, never enters into it.
        var full = After(Listing(Slot(1), Slot(2), Slot(3)), Keys.S, Keys.Enter);
        Assert.Equal(1, full.List!.Rows[full.List.Cursor].Number);
        Assert.Equal(default, full.Press(Keys.Enter));
        Assert.Equal(1, full.List!.Asking!.Number);
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 1), After(Listing(), Keys.Enter).Press(Keys.Enter));
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 1), After(Listing(Slot(SaveSlots.OldAutosave, SlotState.Unreadable)), Keys.Enter).Press(Keys.Enter));
    }

    [Fact]
    public void TheLoadListNamesEverySlot_AndMarksTheEmpty()
    {
        // R2(d): "the slot list shows, per slot, enough to choose by (e.g. dungeon
        // and floor or 'between runs', run count, when it was saved) and marks empty
        // slots". A slot is its own row, and its markers' row under it when any apply.
        string Rows(SlotEntry entry, int count = 3) => string.Join(" | ", SlotText.Rows(entry, count, Dungeons, PlusTwo));

        Assert.Equal("SLOT 2 - EMPTY", Rows(Empty(2)));
        Assert.Equal("SLOT 3 - CANNOT BE READ", Rows(Slot(3, SlotState.Unreadable)));
        Assert.Equal("SLOT 1 - THE KNIFE WARRENS FLOOR 12 - 123 RUNS - 2026-09-25 14:32", Rows(Slot(1, header: Header(At, 123, "knife_warrens", 12))));

        // The save folder itself, while it holds a campaign, is the OLD AUTOSAVE —
        // never listed empty, since it is listed only while it holds a save or a
        // backup — with the markers any slot has: here a save from before the slots,
        // with no time, resumed from its backup. It is listed only while the launch
        // could not move it into a slot of its own (the user's ruling on the save
        // slots, R5; OldAutosave.MoveIntoASlot), and its row says so, last, whatever
        // it holds — a save, a backup, or files none of which reads.
        Assert.Equal("OLD AUTOSAVE - BETWEEN RUNS - 1 RUN - 2026-09-25 14:32 | NOT YET MOVED INTO A SLOT", Rows(Slot(SaveSlots.OldAutosave, header: Header(At, 1))));
        Assert.Equal("OLD AUTOSAVE - BETWEEN RUNS - 1 RUN | FROM ITS BACKUP - NO TIME RECORDED - NOT YET MOVED INTO A SLOT", Rows(Slot(SaveSlots.OldAutosave, SlotState.FromBackup, Header(null, 1))));
        Assert.Equal("OLD AUTOSAVE - CANNOT BE READ | ITS LAST LOAD DID NOT FINISH - NOT YET MOVED INTO A SLOT", Rows(Slot(SaveSlots.OldAutosave, SlotState.Unreadable, unfinished: true)));
        Assert.Equal("NOT YET MOVED INTO A SLOT", SlotText.NotMovedYet);

        // Each marker, on the row under its slot's, in the order they are drawn.
        Assert.Equal("SLOT 1 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | FROM ITS BACKUP", Rows(Slot(1, SlotState.FromBackup, Header(At, 2))));
        Assert.Equal("SLOT 1 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | ITS LAST LOAD DID NOT FINISH", Rows(Slot(1, header: Header(At, 2), unfinished: true)));
        Assert.Equal("SLOT 1 - BETWEEN RUNS - 2 RUNS - 2027-09-25 14:32 | AHEAD OF THIS CLOCK", Rows(Slot(1, header: Header(At.AddYears(1), 2), ahead: true)));
        Assert.Equal("SLOT 1 - BETWEEN RUNS - 2 RUNS | NO TIME RECORDED", Rows(Slot(1, header: Header(null, 2))));
        // A slot above the count is marked as beyond it, and nothing more: loaded,
        // it is chosen, and saves into itself (R5).
        Assert.Equal("SLOT 5 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | BEYOND THE 3 SLOTS", Rows(Slot(5, header: Header(At, 2), beyond: true)));
        Assert.Equal("SLOT 2 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | BEYOND THE 1 SLOT", Rows(Slot(2, header: Header(At, 2), beyond: true), count: 1));
        Assert.Equal("SLOT 9 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | FROM ITS BACKUP - ITS LAST LOAD DID NOT FINISH - AHEAD OF THIS CLOCK - BEYOND THE 3 SLOTS",
            Rows(Slot(9, SlotState.FromBackup, Header(At, 2), unfinished: true, beyond: true, ahead: true)));
        Assert.Equal("SLOT 4 - CANNOT BE READ | ITS LAST LOAD DID NOT FINISH - BEYOND THE 3 SLOTS",
            Rows(Slot(4, SlotState.Unreadable, unfinished: true, beyond: true)));

        // A slot that refused its writes earlier this session says so, in place of
        // what a load would do — resume its backup, set a crashed save aside — since
        // its load brings nothing back and sets nothing aside (the audit of the
        // ruling on the save slots); the other markers stand. The old autosave a
        // launch could not move takes no saves that session, and says so the same
        // way. An empty slot has no markers still: nothing of it would load.
        Assert.Equal("SLOT 2 - BETWEEN RUNS - 2 RUNS - 2026-09-25 14:32 | TAKES NO SAVES THIS SESSION",
            Rows(Slot(2, SlotState.FromBackup, Header(At, 2), unfinished: true, refused: true)));
        Assert.Equal("SLOT 5 - CANNOT BE READ | TAKES NO SAVES THIS SESSION - BEYOND THE 3 SLOTS", Rows(Slot(5, SlotState.Unreadable, beyond: true, refused: true)));
        Assert.Equal("OLD AUTOSAVE - BETWEEN RUNS - 0 RUNS | TAKES NO SAVES THIS SESSION - NO TIME RECORDED - NOT YET MOVED INTO A SLOT",
            Rows(Slot(SaveSlots.OldAutosave, SlotState.FromBackup, Header(null, 0), refused: true)));
        Assert.Equal("SLOT 2 - EMPTY", Rows(Empty(2) with { TakesNoSaves = true }));
        Assert.Equal("TAKES NO SAVES THIS SESSION", SlotText.TakesNoSaves);

        // Nothing drawn is the file's raw text: a dungeon the content does not have is
        // named as such, and a time at the calendar's end is shown in UTC, never thrown.
        Assert.Equal("SLOT 1 - AN UNKNOWN DUNGEON FLOOR 2 - 1 RUN - 2026-09-25 14:32", Rows(Slot(1, header: Header(At, 1, "no_such_dungeon", 2))));
        Assert.Equal("SLOT 1 - THE SPEAR HALL - 1 RUN - 2026-09-25 14:32", Rows(Slot(1, header: Header(At, 1, "spear_hall", null))));
        Assert.Equal("9999-12-31 23:59", SlotText.When(DateTimeOffset.MaxValue, PlusTwo));

        // The list stands in the options' place, and the notices': ten slots, each
        // with its markers' row, the chosen one marked in yellow and the empty ones
        // dimmed — never one beyond the count, which loads like any — fit a
        // 720-pixel window between the title and the keys.
        // (Every time here is ahead of the clock, so none counts, and the most recent
        // save is the tie's: the lowest number, the old autosave, where the list
        // starts. With it listed, 0 loads it too, and the keys say 0-9.)
        var all = Enumerable.Range(0, SaveSlots.Most + 1)
            .Select(n => Slot(n, SlotState.FromBackup, Header(At.AddMinutes(n), 100 + n, "knife_warrens", 10 + n), unfinished: true, beyond: n > 3, ahead: true))
            .ToList();
        var menu = After(new SaveListing(all, []), Keys.S, Keys.S, Keys.Enter);
        Assert.Equal(SlotMenuMode.Load, menu.List!.Mode);
        string[] notices = [.. NoticeText.Rows(Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1").ToList())];
        var drawn = Drawn(menu, notices);
        Assert.DoesNotContain(drawn, row => notices.Contains(row.Text));
        Assert.DoesNotContain(drawn, row => row.Text.Contains(TitleText.NewCampaign, StringComparison.Ordinal) || row.Text.Contains("QUIT", StringComparison.Ordinal));
        var listed = drawn.Where(row => row.Scale == SlotText.Scale).ToList();
        Assert.Equal(2 * all.Count, listed.Count);
        Assert.Equal(SaveSlots.OldAutosave, menu.List!.Rows[menu.List.Cursor].Number);
        Assert.Equal((DungeonHud.Yellow, true), (listed[0].Color, listed[0].Text.StartsWith("> OLD AUTOSAVE - ", StringComparison.Ordinal) && listed[0].Text.EndsWith(" <", StringComparison.Ordinal)));
        Assert.Equal((DungeonHud.White, true), (listed[2].Color, listed[2].Text.StartsWith("  SLOT 1 - ", StringComparison.Ordinal)));
        Assert.Equal(DungeonHud.White, listed[8].Color);   // slot 4, beyond the count: loaded like any, and played
        Assert.All(listed.Where((_, i) => i % 2 == 1), row => Assert.Equal(DungeonHud.Grey, row.Color));
        Assert.EndsWith($" - {SlotText.NotMovedYet}", listed[1].Text, StringComparison.Ordinal);   // the old autosave's markers, and no other slot's
        Assert.Single(listed, row => row.Text.Contains(SlotText.NotMovedYet, StringComparison.Ordinal));
        Assert.Single(listed.Select(row => row.Left).Distinct());   // one left-aligned block
        Fits(drawn);
        Assert.True(listed[^1].Bottom <= H - TitleHud.KeysFromBottom - TitleHud.BottomPitch, "the list runs into the keys");
        Assert.Equal((TitleText.LoadKeysLine(oldAutosave: true), DungeonHud.Grey), (drawn[^1].Text, drawn[^1].Color));
        Assert.Contains(drawn, row => row.Text == TitleText.LoadGame && row.Y == TitleHud.NoticeTop);

        // An empty slot is dimmed and marked by its own row. With no old autosave
        // listed, the list starts at slot 1, and the keys say 1-9.
        var sparse = After(Listing(SpearHall(2)), Keys.S, Keys.S, Keys.Enter);
        var sparseDrawn = Drawn(sparse);
        var sparseRows = sparseDrawn.Where(row => row.Scale == SlotText.Scale).ToList();
        Assert.Equal(["  SLOT 1 - EMPTY", "> SLOT 2 - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32 <", "  SLOT 3 - EMPTY"],
            sparseRows.Select(row => row.Text));
        Assert.Equal([DungeonHud.DarkGrey, DungeonHud.Yellow, DungeonHud.DarkGrey], sparseRows.Select(row => row.Color));
        Assert.Equal((TitleText.LoadKeysLine(oldAutosave: false), DungeonHud.Grey), (sparseDrawn[^1].Text, sparseDrawn[^1].Color));
        Fits(sparseDrawn);
    }

    [Fact]
    public void TheLoadListTakesItsKeys()
    {
        // The load list, key by key: W/S and the arrows move over the rows that hold
        // something, round the ends; a digit takes its slot, 0 the old autosave while
        // it is listed; ENTER takes the one chosen; an empty slot is refused and says
        // so until the next key; ESC goes back. The list starts on the most recent save.
        var listing = Listing(Slot(SaveSlots.OldAutosave, header: Header(At, 1)), Slot(2, header: Header(At.AddHours(1), 2)), Slot(3, SlotState.Unreadable),
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

        // Nothing anywhere: no row is chosen, W/S go nowhere, and ENTER says so. With
        // no old autosave listed, 0 does nothing and says nothing: there is no slot 0
        // to be empty.
        var empty = new SlotMenu(SlotMenuMode.Load, Listing(), 3);
        Assert.Equal(-1, empty.Cursor);
        Assert.Equal((SlotStep.None, SlotStep.None), (empty.Press(Keys.S), empty.Press(Keys.W)));
        Assert.Equal(SlotStep.Refused, empty.Press(Keys.Enter));
        Assert.Equal("EVERY SLOT IS EMPTY", empty.Said);
        Assert.Equal((SlotStep.None, (int?)null, (string?)null), (empty.Press(Keys.D0), empty.Chosen, empty.Said));

        // Through the title: what the list said is drawn above its keys, in orange,
        // until the next key; a slot chosen loads.
        var menu = After(Listing(Slot(2, header: Header(At, 2))), Keys.S, Keys.S, Keys.Enter);
        Assert.Equal(default, menu.Press(Keys.D1));
        var drawn = Drawn(menu);
        Assert.Equal(("SLOT 1 IS EMPTY", DungeonHud.Orange), (drawn[^2].Text, drawn[^2].Color));
        Assert.Equal(H - TitleHud.KeysFromBottom - TitleHud.BottomPitch, drawn[^2].Y);
        Assert.Equal(new TitleStep(TitleAction.Load, 2), menu.Press(Keys.D2));
        Assert.DoesNotContain(Drawn(menu), row => row.Text == "SLOT 1 IS EMPTY");

        // A slot loads at once, whatever another slot holds (the user's ruling of
        // 2026-09-25 on the save slots, R5): the campaign it brings back saves into
        // it, and no other slot is set aside, so nothing asks — chosen from the list
        // by its digit or by ENTER, a slot listed CANNOT BE READ too, the old
        // autosave by 0, or by CONTINUE, with the old autosave holding a different
        // campaign.
        var others = Listing(SpearHall(SaveSlots.OldAutosave), Slot(2, header: Header(At.AddHours(1), 1)), Slot(3, SlotState.Unreadable), Slot(5, header: Header(At, 4), beyond: true));
        foreach (var (key, slot) in new[] { (Keys.D2, 2), (Keys.Enter, 2), (Keys.D3, 3), (Keys.D5, 5), (Keys.D0, SaveSlots.OldAutosave) })
        {
            var chosen = After(others, Keys.S, Keys.S, Keys.Enter);
            Assert.Equal(new TitleStep(TitleAction.Load, slot), chosen.Press(key));
            Assert.False(chosen.Asking);
        }
        var continued = new TitleMenu(others);
        Assert.Equal((int)TitleOption.Continue, continued.Cursor);
        Assert.Equal(new TitleStep(TitleAction.Load, 2), continued.Press(Keys.Enter));
        Assert.False(continued.Asking);

        // With the old autosave not listed — the save folder holds no campaign of its
        // own — 0 loads nothing and says nothing, and the list stays as it was.
        var noOld = After(Listing(Slot(2, header: Header(At, 1))), Keys.S, Keys.S, Keys.Enter);
        Assert.Equal((default(TitleStep), (string?)null, 2), (noOld.Press(Keys.D0), noOld.List!.Said, noOld.List.Rows[noOld.List.Cursor].Number));
        Assert.Equal(SlotMenuMode.Load, noOld.List.Mode);

        // The keys line says what the keys do (S6's review): W/S and the arrows only
        // choose, and a digit loads at once, as ENTER does — never "0-9 CHOOSE",
        // which a player would read as a digit that only picks — 1-9, and 0-9 while
        // the old autosave is listed, which 0 loads. And it says what a load means
        // where the choice is made: play saves into the slot loaded.
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER OR 1-9 LOADS - PLAY SAVES INTO THE SLOT YOU LOAD - ESC BACK", TitleText.LoadKeysLine(oldAutosave: false));
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER OR 0-9 LOADS - PLAY SAVES INTO THE SLOT YOU LOAD - ESC BACK", TitleText.LoadKeysLine(oldAutosave: true));
        Assert.Equal(TitleText.LoadKeysLine(oldAutosave: false), Drawn(noOld)[^1].Text);
        Assert.Equal(TitleText.LoadKeysLine(oldAutosave: true), Drawn(After(others, Keys.S, Keys.S, Keys.Enter))[^1].Text);
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

        // The worst slot row: the longest label — the old autosave's — the longest
        // dungeon name the content has, a two-digit floor, three-digit runs, the
        // time, and the marks of the chosen row.
        var longest = Dungeons.All.MaxBy(dungeon => dungeon.Name.Length)!;
        var worst = Slot(SaveSlots.Most, header: Header(At, 123, longest.Id, 12));
        var oldWorst = worst with { Number = SaveSlots.OldAutosave };
        string worstRow = $"> {SlotText.Row(oldWorst, Dungeons, PlusTwo)} <";
        Assert.Equal($"> OLD AUTOSAVE - {longest.Name.ToUpperInvariant()} FLOOR 12 - 123 RUNS - 2026-09-25 14:32 <", worstRow);
        Assert.True(worstRow.Length > $"> {SlotText.Row(worst, Dungeons, PlusTwo)} <".Length);
        Within(worstRow, SlotText.Scale);

        // The worst markers' row: every marker that can stand together, indented —
        // a slot's, beyond the count, and the old autosave's, never beyond it and
        // not yet moved into a slot, which is the wider.
        var everyMarker = Slot(SaveSlots.Most, SlotState.FromBackup, Header(At, 1), unfinished: true, beyond: true, ahead: true);
        string markers = SlotText.Rows(everyMarker, 3, Dungeons, PlusTwo)[1];
        Assert.Equal(89, markers.Length);
        Within($"    {markers}", SlotText.Scale);
        string oldMarkers = SlotText.Rows(everyMarker with { Number = SaveSlots.OldAutosave, Beyond = false }, 3, Dungeons, PlusTwo)[1];
        Assert.Equal("FROM ITS BACKUP - ITS LAST LOAD DID NOT FINISH - AHEAD OF THIS CLOCK - NOT YET MOVED INTO A SLOT", oldMarkers);
        Assert.Equal(96, oldMarkers.Length);
        Within($"    {oldMarkers}", SlotText.Scale);
        // One that takes no saves this session trades two of them for one, narrower.
        Assert.True(SlotText.Rows(everyMarker with { Number = SaveSlots.OldAutosave, Beyond = false, TakesNoSaves = true }, 3, Dungeons, PlusTwo)[1].Length < oldMarkers.Length);

        // The ask's two rows, at the keys line's scale: NEW CAMPAIGN's, over slot 9
        // holding the worst campaign, and over one that cannot be read. (A load asks
        // nothing: R5.)
        var (what, keys) = TitleText.NewCampaignAsk(SaveSlots.Most, SlotText.Summary(worst, Dungeons));
        Assert.Equal($"SLOT 9 HOLDS {longest.Name.ToUpperInvariant()} FLOOR 12 - 123 RUNS", what);
        Assert.Equal(96, keys.Length);
        Within(what, TitleHud.KeysScale);
        Within(keys, TitleHud.KeysScale);
        Within(TitleText.NewCampaignAsk(SaveSlots.Most, SlotText.CannotBeRead, unreadable: true).What, TitleHud.KeysScale);

        // CONTINUE with the longest label and name, marked, at the options' scale;
        // and the keys.
        Within($"> {TitleText.Continue(SlotText.Row(oldWorst, Dungeons, PlusTwo))} <", TitleHud.OptionScale);
        Within(TitleText.NoSaveYet, TitleHud.OptionScale);
        Within(TitleText.KeysLine, TitleHud.KeysScale);
        Within(TitleText.LoadKeysLine(oldAutosave: true), TitleHud.KeysScale);
        Within(TitleText.LoadKeysLine(oldAutosave: false), TitleHud.KeysScale);
        Within(TitleText.NewCampaignKeysLine(SaveSlots.Most), TitleHud.KeysScale);
        Within(TitleText.NewCampaignHeading, TitleHud.ListHeadingScale);
        Within(SlotText.NotOffered(SaveSlots.Most, SaveSlots.Most - 1), TitleHud.KeysScale);
        Within(TitleText.Title, TitleHud.TitleScale);
        Within(SlotText.NothingToLoad, TitleHud.KeysScale);
        Within(SlotText.IsEmpty(SaveSlots.Most), TitleHud.KeysScale);

        // And laid out, every screen the title can show fits a 720-pixel window: the
        // options under the most notices the screen can be handed (the save's word
        // whole to its ceiling, three more rows and the row that counts the rest);
        // the list of ten worst slots; NEW CAMPAIGN's list of nine, and its ask over
        // the worst of them.
        var worstListing = new SaveListing(
            [.. Enumerable.Range(0, SaveSlots.Most + 1).Select(n => Slot(n, SlotState.FromBackup, Header(At.AddMinutes(n), 123, longest.Id, 12), unfinished: true, beyond: n > 3, ahead: true))], []);
        var mostNotices = NoticeText.Rows([.. Enumerable.Range(1, NoticeText.MaxWholeRows).Select(i => $"the save word row {i}"),
            .. Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1")], whole: NoticeText.MaxWholeRows);
        Assert.Equal(NoticeText.MaxWholeRows + NoticeText.MaxRows + 1, mostNotices.Count);
        Fits(Drawn(new TitleMenu(worstListing), mostNotices));
        Fits(Drawn(After(worstListing, Keys.S, Keys.S, Keys.Enter), mostNotices));            // the list
        var nineListing = new SaveListing([.. worstListing.Slots.Select(slot => slot with { Beyond = false })], []);
        var nine = After(nineListing, Keys.S, Keys.Enter);                                    // NEW CAMPAIGN's list, at nine slots
        Assert.Equal((SlotMenuMode.NewCampaign, SaveSlots.Most), (nine.List!.Mode, nine.List.Rows.Count));
        var nineDrawn = Drawn(nine, mostNotices);
        Fits(nineDrawn);
        Assert.True(nineDrawn.Where(row => row.Scale == SlotText.Scale).Max(row => row.Bottom) <= H - TitleHud.KeysFromBottom - TitleHud.BottomPitch, "the list runs into the keys");
        Assert.Equal(default, nine.Press(Keys.D9));
        Assert.Equal(SaveSlots.Most, nine.List!.Asking!.Number);
        Fits(Drawn(nine, mostNotices));                                                       // its ask, over slot 9
        Assert.True(Drawn(new TitleMenu(worstListing), mostNotices).Single(row => row.Text.StartsWith("QUIT", StringComparison.Ordinal)).Bottom
            <= H - TitleHud.KeysFromBottom - TitleHud.BottomPitch, "the options run into the bottom rows");
    }

    [Fact]
    public void EscapeBacksOutThenQuits()
    {
        // ESC is the game's to route: it asks the title first. A list goes back to
        // the options; an ask — NEW CAMPAIGN's list's, the one there is — says no, and
        // the list stays; and on the options it is not the title's, so the game
        // closes, as QUIT does.
        var listing = Listing(SpearHall(1), Slot(2, header: Header(At.AddHours(1), 1)));
        var menu = new TitleMenu(listing);
        Assert.False(menu.Escape());

        menu.Press(Keys.S);
        menu.Press(Keys.S);
        menu.Press(Keys.Enter);
        Assert.Equal(SlotMenuMode.Load, menu.List!.Mode);
        Assert.True(menu.Escape());
        Assert.Null(menu.List);
        Assert.Equal((int)TitleOption.LoadGame, menu.Cursor);   // back where it was
        Assert.False(menu.Escape());

        menu.Press(Keys.Enter);
        Assert.NotNull(menu.List);
        Assert.True(menu.Escape());                          // the list: back
        Assert.False(menu.Escape());                         // the options: the game's

        menu.Press(Keys.W);
        menu.Press(Keys.Enter);
        Assert.Equal(SlotMenuMode.NewCampaign, menu.List!.Mode);
        menu.Press(Keys.D2);                                 // slot 2 holds a campaign: it asks
        Assert.True(menu.Asking);
        Assert.True(menu.Escape());                          // the ask: no
        Assert.Equal((false, SlotMenuMode.NewCampaign), (menu.Asking, menu.List!.Mode));
        Assert.True(menu.Escape());                          // the list: back
        Assert.Equal((false, (int)TitleOption.NewCampaign, (SlotMenu?)null), (menu.Asking, menu.Cursor, menu.List));
        Assert.False(menu.Escape());                         // the options: the game's

        // The scene routes it the same, built and read, never played: the notices it
        // was handed laid out under the title, the save's word whole.
        var host = new NoTitleHost();
        string quitUnsaved = CampaignBoot.QuitUnsaved(TestSlots.Named(SaveSlots.OldAutosave));
        string[] notices = [quitUnsaved, "tuning.json: MovementBudget 20000 is above 10000 - using 10000"];
        var scene = new TitleScene(game: null!, host, listing, shownWhole: 1, notices);
        Assert.Equal([.. NoticeText.Wrap(quitUnsaved), "TUNING.JSON: MOVEMENTBUDGET 20000 IS ABOVE 10000 - USING 10000"], scene.NoticeRows);
        Assert.False(scene.HandleEscape());
        scene.Menu.Press(Keys.S);
        scene.Menu.Press(Keys.S);
        scene.Menu.Press(Keys.Enter);
        Assert.True(scene.HandleEscape());
        Assert.False(scene.HandleEscape());
        scene.Menu.Press(Keys.W);
        scene.Menu.Press(Keys.Enter);
        scene.Menu.Press(Keys.D2);
        Assert.True(scene.Menu.Asking);
        Assert.True(scene.HandleEscape());                   // the ask: no
        Assert.True(scene.HandleEscape());                   // NEW CAMPAIGN's list: back
        Assert.False(scene.HandleEscape());                  // the options: the game's
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

        public void BeginNewCampaign(int slot) => Calls++;

        public void Quit() => Calls++;
    }
}
