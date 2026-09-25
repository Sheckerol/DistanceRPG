using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Saving by hand, and leaving play for the title (the user's ruling of
/// 2026-09-25 on the save model, R2(d) and R2(e); Phase 5.S7). The one menu both
/// screens with a campaign in play open on ESC — <c>1  CLOSE</c>, <c>2  SAVE
/// GAME</c>, <c>3  QUIT TO TITLE</c> (<see cref="PauseMenu"/>) — saves only at a
/// safe point, into a manual slot chosen on the save list, which asks before it
/// saves over a slot or sets one aside, ENTER alone saying yes; what is set aside
/// is the files' to decide, never the list's; the answer is drawn inside the menu,
/// never as a notice; and QUIT TO TITLE writes the autosave through the window
/// close's own door before the title. A dev-override session saves only its
/// scratch slot. As with <c>TitleTests</c>, the drawing is untested but what it
/// draws is: the words, the keys, where every row goes — all pure — and the scene
/// walked key by key, built with no window. The saves are real, each on a
/// temporary folder of its own, under the numbers the runs were written against
/// (<see cref="Premise"/>), so the content collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class SaveMenuTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>The shipped window, and the least room kept either side of a row.</summary>
    private const float W = 1280f, H = 720f, Margin = 20f;

    /// <summary>What a row the menu draws can hold: the HUD's glyphs, and never an apostrophe, a quote, a brace or a backslash.</summary>
    private const string Font = "^[A-Z0-9 :,./()>=<+-]+$";

    /// <summary>A zone two hours ahead of UTC, so a row shows the player's own time rather than the save's UTC.</summary>
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone("DistanceRPG +2", TimeSpan.FromHours(2), "+2", "+2");

    /// <summary>18:10 UTC — 20:10 two hours ahead — the day before the ruling.</summary>
    private static readonly DateTimeOffset At = new(2026, 9, 24, 18, 10, 0, TimeSpan.Zero);

    private const BindingFlags Declared = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static DungeonCatalogue Dungeons => GameContent.Current.Dungeons;

    private static SlotHeader Header(DateTimeOffset? at, int runs, string? dungeon = null, int? floor = null)
        => new(at, runs, dungeon, floor, dungeon is null ? null : 3);

    private static SlotEntry Slot(int number, SlotState state = SlotState.Readable, SlotHeader? header = null, bool unfinished = false,
        bool beyond = false, bool ahead = false)
        => new(number, state, state is SlotState.Readable or SlotState.FromBackup ? header ?? Header(At, 0) : null, unfinished, beyond, ahead, SameAsAutosave: false);

    /// <summary>A listing of the autosave and slots 1 to <paramref name="count"/>, each as given or empty, and any given beyond them.</summary>
    private static SaveListing Listing(int count, params SlotEntry[] slots)
        => new([.. Enumerable.Range(0, count + 1).Select(n => slots.FirstOrDefault(slot => slot.Number == n) ?? Slot(n, SlotState.Empty))
            .Concat(slots.Where(slot => slot.Number > count))
            .OrderBy(slot => slot.Number)], []);

    /// <summary>The save folder the plan's words were written against: the autosave mid-run on floor 4 of the Spear Hall, slot 2 between runs, slot 3 that cannot be read, slot 5 beyond the count.</summary>
    private static SaveListing Worded() => Listing(3,
        Slot(0, header: Header(At, 3, "spear_hall", 4)),
        Slot(2, header: Header(At, 2)),
        Slot(3, SlotState.Unreadable),
        Slot(5, header: Header(At, 1), beyond: true));

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    /// <summary>A campaign one run on: the tutorial walked out of on its first floor.</summary>
    private static CampaignState Retreated()
    {
        var campaign = NewCampaign();
        CampaignRuns.ShallowRetreat(campaign, "tutorial");
        return campaign;
    }

    /// <summary>A campaign with the tutorial beaten and gone.</summary>
    private static CampaignState Cleared()
    {
        var campaign = NewCampaign();
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        return campaign;
    }

    /// <summary>A campaign with a run entered and no floor opened yet: a safe point, and a dungeon scene can be built over it with no window.</summary>
    private static CampaignState Entered()
    {
        var campaign = NewCampaign();
        campaign.Enter("spear_hall", CampaignRuns.FiveDeep);
        return campaign;
    }

    /// <summary><paramref name="text"/> stamped with a version newer than this build reads.</summary>
    private static string Newer(string text)
    {
        var tree = JsonNode.Parse(text)!.AsObject();
        tree["version"] = SaveCodec.CurrentVersion + 1;
        return tree.ToJsonString();
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

    private static bool Same(MethodBase? a, MethodBase b) => a is not null && a.Module == b.Module && a.MetadataToken == b.MetadataToken;

    /// <summary><paramref name="method"/>'s body, an instruction at a time: each with the method it calls or names and the field it loads or stores, when its operand is one.</summary>
    private static List<(OpCode Op, MethodBase? Called, FieldInfo? Field)> Walk(MethodBase method)
    {
        byte[] il = Body(method)!;
        return InstructionsAt(il)
            .Select(i => (i.Op,
                i.Token != 0 && i.Op.OperandType is OperandType.InlineMethod or OperandType.InlineTok ? Resolve(method, i.Token) : null,
                i.Op.OperandType == OperandType.InlineField ? FieldAt(method, BitConverter.ToInt32(il, i.At)) : null))
            .ToList();
    }

    /// <summary>The field <paramref name="token"/> names in <paramref name="method"/>'s body, resolved in the method's own generic context; null for one no context resolves.</summary>
    private static FieldInfo? FieldAt(MethodBase method, int token)
    {
        try
        {
            return method.Module.ResolveField(token, method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool Names(FieldInfo? field, FieldInfo named) => field is not null && field.Module == named.Module && field.MetadataToken == named.MetadataToken;

    /// <summary>Every method in the game assembly that calls <paramref name="target"/>, as <c>Type.Method</c> of its outermost type.</summary>
    private static string[] Callers(MethodBase target)
        => Calls(typeof(DistanceRpgGame).Assembly).Where(c => Same(c.Target, target))
            .Select(c => $"{Root(c.From.DeclaringType)!.Name}.{c.From.Name}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>The menu's rows fit the window: in the font, within its width with room either side, no two overlapping, and the block between its margins.</summary>
    private static void Clear(IReadOnlyList<DungeonHud.MenuRow> rows, float height = H)
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
        Assert.True(down[0].Y >= DungeonHud.MenuTopMargin - 0.001f, $"'{down[0].Text}' rises past the menu's top margin");
        Assert.True(down[^1].Bottom <= height - DungeonHud.MenuBottomMargin + 0.001f, $"'{down[^1].Text}' runs into the line along the bottom of a {height}-tall window");
    }

    private static IReadOnlyList<DungeonHud.MenuRow> Laid(IReadOnlyList<string> options, bool canSave, string? answer, SlotMenu? list, float height = H)
        => DungeonHud.PauseMenuLayout(W, height, options, canSave, answer, list, Dungeons, PlusTwo);

    [Fact]
    public void ThePauseMenuSavesOnYourOwnTurn_AndQuitsToTheTitle()
    {
        // R2(d): "a manual save can be made at ANY SAFE POINT ... never while enemies
        // are moving"; R2(e): the menus leave play for the title. The menu, digit by
        // digit: 1 closes it; 2 opens the save list at a safe point and says why not
        // off one; 3 quits to the title — at any point, since the quit's own door plays
        // the enemies' turn out first.
        Assert.Equal((PauseAction.Close, (string?)null), PauseMenu.Press(1, canSave: true, hasTitle: true));
        Assert.Equal((PauseAction.Close, (string?)null), PauseMenu.Press(1, canSave: false, hasTitle: false));
        Assert.Equal((PauseAction.OpenSaves, (string?)null), PauseMenu.Press(2, canSave: true, hasTitle: true));
        Assert.Equal((PauseAction.Refuse, "SAVE ON YOUR OWN TURN"), PauseMenu.Press(2, canSave: false, hasTitle: true));
        Assert.Equal((PauseAction.QuitToTitle, (string?)null), PauseMenu.Press(3, canSave: true, hasTitle: true));
        Assert.Equal((PauseAction.QuitToTitle, (string?)null), PauseMenu.Press(3, canSave: false, hasTitle: true));
        foreach (int digit in new[] { 0, 4, 5, 9 })
            Assert.Equal((PauseAction.None, (string?)null), PauseMenu.Press(digit, canSave: true, hasTitle: true));

        // A dev-override session has no title and saves apart: 3 quits the game, and 2
        // says why it saves nothing, at a safe point or not.
        Assert.Equal(["1  CLOSE", "2  SAVE GAME", "3  QUIT GAME"], PauseMenu.Options(hasTitle: false));
        Assert.Equal((PauseAction.QuitGame, (string?)null), PauseMenu.Press(3, canSave: true, hasTitle: false));
        Assert.Equal((PauseAction.Refuse, "A DEV OVERRIDE LAUNCH SAVES ONLY ITS SCRATCH SLOT"), PauseMenu.Press(2, canSave: false, hasTitle: false));

        // Walked through a dungeon scene, built with no window over a run entered and
        // not yet opened — a safe point. ESC opens the menu; 2 asks the host for the
        // slots and opens the save list in the options' place; ESC goes back; 3 asks
        // the host to leave play for the title.
        var host = new RecordingHost(Worded());
        var scene = new DungeonScene(camera: null!, game: null!, Entered(), host);
        Assert.True(scene.HandleEscape());
        Assert.True(scene.PauseMenuOpen);
        Assert.True(scene.CanSaveNow);
        scene.AnyKey(Keys.D2);
        Assert.Equal((true, 1, SlotMenuMode.Save), (scene.SavesOpen, host.SlotsListed, scene.SaveList!.Mode));
        Assert.True(scene.HandleEscape());
        Assert.Equal((true, false, (SlotMenu?)null), (scene.PauseMenuOpen, scene.SavesOpen, scene.SaveList));
        scene.AnyKey(Keys.D3);
        Assert.Equal(1, host.QuitsToTitle);

        // During the enemies' turn — its banner up, here — 2 saves nothing and says
        // why, inside the menu, and asks the host for nothing; 3 still quits.
        var ending = Entered();
        var turns = ending.Visit!.OpenTurns(ending.Party, CampaignRuns.Ten);
        turns.EndTurn();
        Assert.False(SaveGame.IsSafePoint(ending));
        var busy = new RecordingHost(Worded());
        var offTurn = new DungeonScene(camera: null!, game: null!, ending, busy);
        offTurn.HandleEscape();
        Assert.False(offTurn.CanSaveNow);
        offTurn.AnyKey(Keys.D2);
        Assert.Equal((PauseMenu.SaveOnYourTurn, false, 0), (offTurn.MenuAnswer, offTurn.SavesOpen, busy.SlotsListed));
        offTurn.AnyKey(Keys.D3);
        Assert.Equal(1, busy.QuitsToTitle);

        // And off a title: 2 says so, and nothing is listed or saved.
        var scratch = new RecordingHost(Worded(), hasTitle: false);
        var apart = new DungeonScene(camera: null!, game: null!, Entered(), scratch);
        apart.HandleEscape();
        apart.AnyKey(Keys.D2);
        Assert.Equal((PauseMenu.NoSaveInOverride, 0, 0), (apart.MenuAnswer, scratch.SlotsListed, scratch.SavedTo.Count));
    }

    [Fact]
    public void TheSaveListAsksBeforeOverwriting()
    {
        // R2(d): "saving into an occupied slot asks before overwriting". The save
        // list, key by key: the autosave shown and never chosen, the manual slots to
        // the count, a slot beyond it never offered; an empty slot saved at once; an
        // occupied one asking, and only ENTER saying yes.
        var listing = Worded();
        var list = new SlotMenu(SlotMenuMode.Save, listing, 3);
        Assert.Equal([0, 1, 2, 3], list.Rows.Select(row => row.Number));
        Assert.Equal(1, list.Rows[list.Cursor].Number);   // the first empty slot: an ENTER pressed without looking saves over nothing

        // W/S and the arrows move over the manual slots, round the ends, never onto
        // the autosave.
        int Walked(Keys key)
        {
            list.Press(key);
            return list.Rows[list.Cursor].Number;
        }
        Assert.Equal([2, 3, 1, 3, 2, 1, 2], new[] { Keys.S, Keys.S, Keys.S, Keys.W, Keys.Up, Keys.Up, Keys.Down }.Select(Walked));

        // An empty slot is chosen at once, by its digit or by ENTER on it.
        Assert.Equal((SlotStep.Chosen, (int?)1, (SlotEntry?)null), (list.Press(Keys.D1), list.Chosen, list.Asking));
        Assert.Equal((SlotStep.Chosen, (int?)1), (list.Press(Keys.Enter), list.Chosen));

        // The autosave saves itself, and a slot beyond the count keeps its save for
        // loading: each is refused, saying why, until the next key.
        Assert.Equal((SlotStep.Refused, "THE AUTOSAVE SAVES ITSELF"), (list.Press(Keys.D0), list.Said));
        Assert.Equal((SlotStep.Refused, "SLOT 5 IS BEYOND THE 3 SLOTS"), (list.Press(Keys.D5), list.Said));
        Assert.Equal((SlotStep.Refused, "SLOT 9 IS BEYOND THE 3 SLOTS"), (list.Press(Keys.D9), list.Said));
        Assert.Equal((SlotStep.None, (string?)null), (list.Press(Keys.N), list.Said));
        Assert.Equal("SLOT 2 IS BEYOND THE 1 SLOT", SlotText.NotOffered(2, 1));

        // An occupied slot asks first, naming what it holds and when it was saved,
        // in the player's own zone.
        SlotMenu Asked(Keys choose)
        {
            var menu = new SlotMenu(SlotMenuMode.Save, listing, 3);
            Assert.Equal(SlotStep.Asking, menu.Press(choose));
            return menu;
        }
        var asked = Asked(Keys.D2);
        Assert.Equal((2, 2, (int?)null), (asked.Asking!.Number, asked.Rows[asked.Cursor].Number, asked.Chosen));
        Assert.Equal(("SLOT 2 HOLDS BETWEEN RUNS - 2 RUNS - 2026-09-24 20:10", "ENTER: SAVE OVER IT - ESC: NO"), SlotText.AskAbout(asked.Asking, Dungeons, PlusTwo));
        Assert.Equal(("SLOT 2 HOLDS BETWEEN RUNS - 2 RUNS - 2026-09-24 20:10", "ENTER: SAVE OVER IT - ESC: NO"),
            SlotText.OverwriteAsk(2, "BETWEEN RUNS - 2 RUNS", "2026-09-24 20:10"));

        // ENTER alone confirms — the keypad's too.
        Assert.Equal((SlotStep.Chosen, (int?)2, (SlotEntry?)null), (asked.Press(Keys.Enter), asked.Chosen, asked.Asking));
        Assert.Equal(SlotStep.Chosen, Asked(Keys.D2).Press(Keys.KeyPadEnter));

        // The slot's digit again, N, Y, ESC and every other key answer no and are
        // spent: the ask goes, nothing is chosen, and the key does nothing else.
        foreach (var key in new[] { Keys.D2, Keys.N, Keys.Y, Keys.Escape, Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.D1, Keys.D0, Keys.Space, Keys.G, Keys.Tab, Keys.E })
        {
            var menu = Asked(Keys.D2);
            Assert.Equal((SlotStep.None, (int?)null, (SlotEntry?)null, (string?)null), (menu.Press(key), menu.Chosen, menu.Asking, menu.Said));
            Assert.Equal(2, menu.Rows[menu.Cursor].Number);
        }

        // A slot whose save cannot be read asks whether to set it aside and save
        // there; one whose backup reads says the backup is kept.
        var unreadable = Asked(Keys.D3);
        Assert.Equal(("SLOT 3 CANNOT BE READ", "ENTER: SET IT ASIDE AND SAVE HERE - ESC: NO"), SlotText.AskAbout(unreadable.Asking!, Dungeons, PlusTwo));
        Assert.Equal((SlotStep.Chosen, (int?)3), (unreadable.Press(Keys.Enter), unreadable.Chosen));
        Assert.Equal(("SLOT 3 CANNOT BE READ - ITS BACKUP IS KEPT", "ENTER: SET IT ASIDE AND SAVE HERE - ESC: NO"),
            SlotText.AskAbout(Slot(3, SlotState.FromBackup, Header(At, 1)), Dungeons, PlusTwo));

        // ENTER on an occupied slot asks as its digit does; with every slot holding a
        // save the list starts on slot 1.
        var entered = new SlotMenu(SlotMenuMode.Save, listing, 3);
        entered.Press(Keys.S);
        Assert.Equal((SlotStep.Asking, 2), (entered.Press(Keys.Enter), entered.Asking!.Number));
        var full = new SlotMenu(SlotMenuMode.Save, Listing(3, Slot(1), Slot(2), Slot(3)), 3);
        Assert.Equal(1, full.Rows[full.Cursor].Number);

        // The keys line says what the keys do: a digit saves at once, as ENTER does.
        Assert.Equal("W/S OR ARROWS CHOOSE - ENTER OR 1-9 SAVES - ESC BACK", SlotText.SaveKeysLine);

        // The dungeon's routing — what its catch-all calls while the list is up —
        // hands the list every key but ESC: G, Tab, I, E and Space each answer an ask
        // no, exactly as on the title and between runs. ESC is the game's, which asks
        // the scene's HandleEscape.
        foreach (var key in new[] { Keys.G, Keys.Tab, Keys.I, Keys.E, Keys.Space })
        {
            var menu = Asked(Keys.D2);
            Assert.Equal(SlotStep.None, DungeonScene.RouteWhileSaving(menu, key));
            Assert.Null(menu.Asking);
        }
        var escaped = Asked(Keys.D2);
        Assert.Equal(SlotStep.None, DungeonScene.RouteWhileSaving(escaped, Keys.Escape));
        Assert.NotNull(escaped.Asking);
        Assert.Equal(SlotStep.Asking, DungeonScene.RouteWhileSaving(new SlotMenu(SlotMenuMode.Save, listing, 3), Keys.D2));

        // Walked through a dungeon scene: every key the list's while it is up — the
        // digit that opened it is never read by the list too, and the digit that
        // saves from it never reopens it — and ESC answering the ask no, then going
        // back to the menu, then closing it.
        var host = new RecordingHost(listing);
        var scene = new DungeonScene(camera: null!, game: null!, Entered(), host);
        scene.HandleEscape();
        scene.AnyKey(Keys.D2);
        Assert.Equal((true, (SlotEntry?)null, 0), (scene.SavesOpen, scene.SaveList!.Asking, host.SavedTo.Count));
        scene.AnyKey(Keys.D2);
        Assert.Equal(2, scene.SaveList!.Asking!.Number);
        scene.AnyKey(Keys.G);
        Assert.Equal((true, (SlotEntry?)null), (scene.SavesOpen, scene.SaveList!.Asking));
        scene.AnyKey(Keys.D2);
        Assert.True(scene.HandleEscape());
        Assert.Equal((true, (SlotEntry?)null), (scene.SavesOpen, scene.SaveList!.Asking));   // the ask: no
        Assert.True(scene.HandleEscape());
        Assert.Equal((false, true), (scene.SavesOpen, scene.PauseMenuOpen));                // the list: back
        Assert.True(scene.HandleEscape());
        Assert.False(scene.AnyMenuOpen);                                                    // the menu: closed
        scene.HandleEscape();
        scene.AnyKey(Keys.D2);
        scene.AnyKey(Keys.D2);
        scene.AnyKey(Keys.Enter);
        Assert.Equal([2], host.SavedTo);
        Assert.Equal((true, false), (scene.PauseMenuOpen, scene.SavesOpen));               // closed back to the menu
        scene.AnyKey(Keys.D2);
        scene.AnyKey(Keys.D1);
        Assert.Equal([2, 1], host.SavedTo);
        Assert.Equal((true, false), (scene.PauseMenuOpen, scene.SavesOpen));               // the 1 saved, and closed nothing more
    }

    [Fact]
    public void ASaveIntoAnUnreadableSlotSetsItAsideFirst()
    {
        InTempFolder(folder =>
        {
            // A slot holding a newer build's save, which this build never reads nor
            // writes over: the list asks whether to set it aside and save there, and
            // ENTER does exactly that — the old bytes kept in the slot's own folder as
            // campaign.unreadable-1.json, never deleted, the new save written, and the
            // answer saying both.
            var slots = new SaveSlots(folder, 3);
            var slot3 = slots.Store(3);
            Directory.CreateDirectory(slot3.Folder);
            File.WriteAllText(slot3.SavePath, Newer(SaveCodec.Write(SaveGame.Capture(Cleared(), savedAt: At))));
            byte[] old = File.ReadAllBytes(slot3.SavePath);
            var listing = SaveListing.Read(slots, At.AddHours(1));
            var entry = listing.Slots.Single(slot => slot.Number == 3);
            Assert.Equal(SlotState.Unreadable, entry.State);
            var list = new SlotMenu(SlotMenuMode.Save, listing, 3);
            Assert.Equal(SlotStep.Asking, list.Press(Keys.D3));
            Assert.Equal(SlotText.UnreadableAsk(3, backupKept: false), SlotText.AskAbout(list.Asking!, Dungeons, PlusTwo));
            Assert.Equal((SlotStep.Chosen, (int?)3), (list.Press(Keys.Enter), list.Chosen));

            var campaign = Retreated();
            var log = new List<string>();
            var (saved, notice) = CampaignBoot.SaveTo(slot3, campaign, view: null, 3, log.Add, At.AddHours(1));
            string aside = Path.Combine(slot3.Folder, "campaign.unreadable-1.json");
            Assert.True(saved);
            Assert.Equal(old, File.ReadAllBytes(aside));
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(campaign, savedAt: At.AddHours(1))), slot3.Read());
            Assert.False(slot3.HasBackup);   // moved into place: nothing was rotated
            Assert.Equal($"SAVED TO SLOT 3 - CAMPAIGN.JSON COULD NOT BE READ (VERSION: {SaveCodec.CurrentVersion + 1} IS NEWER THAN THIS BUILD READS ({SaveCodec.CurrentVersion})) - SET ASIDE AS {aside} - {CampaignBoot.TryAgain}",
                notice);
            Assert.Contains(log, line => line.Contains($"Wrote {slot3.SavePath}", StringComparison.Ordinal));

            // Drawn in the menu, wrapped as a notice is — the path with forward
            // slashes, which the font has — and green.
            var answer = Laid(PauseMenu.Options(hasTitle: true), canSave: true, notice, list: null).Where(row => row.Scale == 1.5f && row.Color == DungeonHud.Green).ToList();
            Assert.Equal(NoticeText.Wrap(notice), answer.Select(row => row.Text));
            Assert.DoesNotContain(answer, row => row.Text.Contains('\\', StringComparison.Ordinal));

            // The slot loads its new campaign; the autosave was never touched; and a
            // second save into the slot rotates as every save does.
            var load = CampaignBoot.Load(slot3, GameContent.Current, number: 3);
            Assert.True(load.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 3"], load.Notices);
            slot3.ClearResuming();
            Assert.False(slots.Store(SaveSlots.Autosave).HasSave);
            Assert.Equal((true, "SAVED TO SLOT 3"), CampaignBoot.SaveTo(slot3, Cleared(), view: null, 3, savedAt: At.AddHours(2)));
            Assert.True(slot3.HasBackup);
            Assert.Equal(old, File.ReadAllBytes(aside));
        });
    }

    [Fact]
    public void ASaveIntoAFromBackupSlotKeepsTheBackup()
    {
        InTempFolder(folder =>
        {
            // Slots 2 and 3 each hold a save over a backup — written by one session —
            // and then each save is broken behind the game's back, as a crash or a hand
            // edit would, while its backup still reads.
            var earlier = new SaveSlots(folder, 3);
            foreach (int number in new[] { 2, 3 })
            {
                Assert.Null(CampaignBoot.Save(earlier.Store(number), Cleared(), savedAt: At));
                Assert.Null(CampaignBoot.Save(earlier.Store(number), Retreated(), savedAt: At.AddHours(1)));
            }
            var stale = SaveListing.Read(earlier, At.AddHours(2));   // read before slot 2 broke: it says Readable
            Assert.Equal(SlotState.Readable, stale.Slots.Single(slot => slot.Number == 2).State);
            var backups = new[] { 2, 3 }.ToDictionary(number => number, number => File.ReadAllBytes(earlier.Store(number).BackupPath));
            foreach (int number in new[] { 2, 3 })
                File.WriteAllText(earlier.Store(number).SavePath, "{ \"version\": 1, \"torn\"");
            var broken = backups.Keys.ToDictionary(number => number, number => File.ReadAllBytes(earlier.Store(number).SavePath));

            // A session that lists slot 3 afresh finds it from its backup, and asks in
            // the words that say the backup is kept.
            var session = new SaveSlots(folder, 3);
            var listing = SaveListing.Read(session, At.AddHours(2));
            Assert.Equal(SlotState.FromBackup, listing.Slots.Single(slot => slot.Number == 3).State);
            var list = new SlotMenu(SlotMenuMode.Save, listing, 3);
            Assert.Equal(SlotStep.Asking, list.Press(Keys.D3));
            Assert.Equal(("SLOT 3 CANNOT BE READ - ITS BACKUP IS KEPT", "ENTER: SET IT ASIDE AND SAVE HERE - ESC: NO"),
                SlotText.AskAbout(list.Asking!, Dungeons, PlusTwo));

            // Saved into: the broken save set aside in the slot's own folder, the new save
            // moved into place, and the backup's bytes as they were — a replace would
            // have rotated the broken save over it.
            var campaign = NewCampaign();
            var (saved, notice) = CampaignBoot.SaveTo(session.Store(3), campaign, view: null, 3, savedAt: At.AddHours(3));
            var slot3 = session.Store(3);
            Assert.True(saved);
            Assert.StartsWith("SAVED TO SLOT 3 - CAMPAIGN.JSON COULD NOT BE READ (", notice, StringComparison.Ordinal);
            Assert.Equal(broken[3], File.ReadAllBytes(Path.Combine(slot3.Folder, "campaign.unreadable-1.json")));
            Assert.Equal(backups[3], File.ReadAllBytes(slot3.BackupPath));
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(campaign, savedAt: At.AddHours(3))), slot3.Read());

            // The files decide, never the list: slot 2, which the stale list still calls
            // Readable — so its ask was the overwrite ask — is set aside the same way,
            // and its backup kept.
            var staleList = new SlotMenu(SlotMenuMode.Save, stale, 3);
            staleList.Press(Keys.D2);
            Assert.StartsWith("SLOT 2 HOLDS ", SlotText.AskAbout(staleList.Asking!, Dungeons, PlusTwo).What, StringComparison.Ordinal);
            var (again, said) = CampaignBoot.SaveTo(session.Store(2), campaign, view: null, 2, savedAt: At.AddHours(3));
            Assert.True(again);
            Assert.StartsWith("SAVED TO SLOT 2 - CAMPAIGN.JSON COULD NOT BE READ (", said, StringComparison.Ordinal);
            Assert.Equal(broken[2], File.ReadAllBytes(Path.Combine(session.Store(2).Folder, "campaign.unreadable-1.json")));
            Assert.Equal(backups[2], File.ReadAllBytes(session.Store(2).BackupPath));
        });
    }

    [Fact]
    public void AManualSaveIsTakenOnlyAtASafePoint()
    {
        InTempFolder(folder =>
        {
            // R2(d): "never while enemies are moving (the same safe points autosave
            // uses)". Off a safe point — the lance mid-stride — nothing is written, not
            // even the slot's folder, and the menu says why.
            var slots = new SaveSlots(folder, 3);
            var walking = ReloadDeterminismTests.MidWalk();
            Assert.False(SaveGame.IsSafePoint(walking.Campaign));
            var log = new List<string>();
            Assert.Equal((false, "SAVE ON YOUR OWN TURN"), CampaignBoot.SaveTo(slots.Store(1), walking.Campaign, new ViewDto("A"), 1, log.Add, At));
            Assert.False(Directory.Exists(slots.Store(1).Folder));
            Assert.Contains(log, line => line.Contains("off a safe point", StringComparison.Ordinal));
            Assert.Equal(TurnPhase.EnemyMoving, walking.Turns.Phase);   // and nothing was settled: a save by hand never plays the enemies' turn out

            // In the party's own turn mid-run: saved, the view with it, and a load of the
            // slot brings back the same text.
            var script = VisitScript.Enter(NewCampaign(), "spear_hall", CampaignRuns.FiveDeep, visitsOwnDie: true);
            Assert.True(SaveGame.IsSafePoint(script.Campaign));
            var view = new ViewDto("B", MarchingThisTurn: true);
            Assert.Equal((true, "SAVED TO SLOT 1"), CampaignBoot.SaveTo(slots.Store(1), script.Campaign, view, 1, savedAt: At));
            string text = slots.Store(1).Read()!;
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(script.Campaign, view, At)), text);
            var load = CampaignBoot.Load(slots.Store(1), GameContent.Current, number: 1);
            Assert.True(load.Loaded);
            Assert.Equal(view, load.View);
            Assert.Equal("CAMPAIGN RESUMED FROM SLOT 1", load.Notices[0]);
            Assert.Equal(text, SaveCodec.Write(SaveGame.Capture(load.Campaign!, load.View, At)));
            slots.Store(1).ClearResuming();

            // Between runs is always a safe point, and a campaign between runs carries no
            // view, whatever it is handed.
            Assert.Equal((true, "SAVED TO SLOT 2"), CampaignBoot.SaveTo(slots.Store(2), Retreated(), new ViewDto("A"), 2, savedAt: At));
            Assert.Null(SaveCodec.Read(slots.Store(2).Read()!).View);

            // The autosave is never saved into by hand.
            Assert.Throws<ArgumentOutOfRangeException>(() => CampaignBoot.SaveTo(slots.Store(SaveSlots.Autosave), Retreated(), null, SaveSlots.Autosave));

            // A slot whose store refuses its writes this session — a file there would
            // neither read nor move — answers with why, and writes nothing (S6's review:
            // the refusal is the session's).
            var refusing = slots.Store(3);
            refusing.RefuseWrites("campaign.json could not be read (x) and could not be set aside (another program has it open)");
            Assert.Equal((false, "SLOT 3 COULD NOT BE SAVED (CAMPAIGN.JSON COULD NOT BE READ (X) AND COULD NOT BE SET ASIDE (ANOTHER PROGRAM HAS IT OPEN)) - WHAT IT HELD IS KEPT - SEE THE LOG"),
                CampaignBoot.SaveTo(refusing, Retreated(), null, 3, savedAt: At));
            Assert.False(refusing.HasSave);
        });
    }

    [Fact]
    public void TheAnswerStaysInsideTheMenu()
    {
        // What a save by hand came to is drawn inside the menu, under its lines —
        // never as a notice: notices are not drawn over a menu, and would age out
        // unseen behind it. It has no timer: only the next key, or the menu closing,
        // takes it away.
        const string data = "tuning.json: MovementUnitsPerMana 0 is below 1 - using 1";
        var host = new RecordingHost(Worded());
        var scene = new DungeonScene(camera: null!, game: null!, Entered(), host, [data]);
        var notices = scene.NoticeRows.ToList();
        Assert.Single(notices);
        scene.HandleEscape();
        scene.AnyKey(Keys.D2);
        scene.AnyKey(Keys.D1);
        Assert.Equal([1], host.SavedTo);
        Assert.Equal((true, false), (scene.PauseMenuOpen, scene.SavesOpen));
        scene.SlotSaved(CampaignBoot.SavedTo(1));
        Assert.Equal("SAVED TO SLOT 1", scene.MenuAnswer);
        Assert.Equal(notices, scene.NoticeRows);   // no notice added
        var drawn = Laid(PauseMenu.Options(hasTitle: true), scene.CanSaveNow, scene.MenuAnswer, scene.SaveList);
        Assert.Contains(drawn, row => row.Text == "SAVED TO SLOT 1" && row.Color == DungeonHud.Green);
        Assert.True(drawn.Single(row => row.Text == "SAVED TO SLOT 1").Y > drawn.Single(row => row.Text == DungeonHud.EscToClose).Bottom);
        scene.SaveFailed(CampaignBoot.WriteFailed("the disk is full"));   // a failed autosave is a notice, over the floor: the answer stays
        Assert.Equal("SAVED TO SLOT 1", scene.MenuAnswer);
        scene.AnyKey(Keys.X);
        Assert.Null(scene.MenuAnswer);   // the next key

        scene.SlotSaved(CampaignBoot.SavedTo(1));
        scene.HandleEscape();
        Assert.Null(scene.MenuAnswer);   // the menu closing
        scene.HandleEscape();
        Assert.Null(scene.MenuAnswer);   // and opened again, it has nothing to say
        scene.HandleEscape();
        scene.SlotSaved(CampaignBoot.SavedTo(1));
        Assert.Null(scene.MenuAnswer);   // with the menu closed by the time the save is written, only the log keeps it

        // A save that failed, and 2 off a safe point, answer in the warning's orange.
        string failed = CampaignBoot.SlotNotSaved(2, "another program has it open");
        Assert.Equal("SLOT 2 COULD NOT BE SAVED (ANOTHER PROGRAM HAS IT OPEN) - WHAT IT HELD IS KEPT - SEE THE LOG", failed);
        var failedRows = Laid(PauseMenu.Options(hasTitle: true), canSave: true, failed, list: null).Where(row => row.Scale == 1.5f && row.Color == DungeonHud.Orange).ToList();
        Assert.Equal(NoticeText.Wrap(failed), failedRows.Select(row => row.Text));
        Assert.Equal(DungeonHud.Orange, DungeonHud.AnswerColor(PauseMenu.SaveOnYourTurn));
        Assert.Equal(DungeonHud.Orange, DungeonHud.AnswerColor(PauseMenu.NoSaveInOverride));
        Assert.Equal(DungeonHud.Green, DungeonHud.AnswerColor(CampaignBoot.SavedTo(9)));

        // Off a safe point the menu dims SAVE GAME, and 2 says why inside it.
        var ending = Entered();
        ending.Visit!.OpenTurns(ending.Party, CampaignRuns.Ten).EndTurn();
        var offTurn = new DungeonScene(camera: null!, game: null!, ending, new RecordingHost(Worded()));
        offTurn.HandleEscape();
        offTurn.AnyKey(Keys.D2);
        var dimmed = Laid(PauseMenu.Options(hasTitle: true), offTurn.CanSaveNow, offTurn.MenuAnswer, offTurn.SaveList);
        Assert.Equal(DungeonHud.DarkGrey, dimmed.Single(row => row.Text == PauseMenu.SaveLine).Color);
        Assert.Contains(dimmed, row => row.Text == "SAVE ON YOUR OWN TURN" && row.Color == DungeonHud.Orange);
        Assert.Empty(offTurn.NoticeRows);
    }

    [Fact]
    public void TheMenusLayOutClear_AndTheListFits720()
    {
        // The menu's two lines sit under its last option, however many options there
        // are — they no longer sit at a fixed height a third option would land on —
        // and the answer under them; nothing overlaps, and the block keeps clear of
        // the line along the bottom (the dungeon's help line, the hub's keys).
        Assert.True(DungeonHud.MenuBottomMargin >= BetweenRunsHud.KeysLineFromBottom);
        const string answer = "SLOT 2 COULD NOT BE SAVED (ANOTHER PROGRAM HAS IT OPEN) - WHAT IT HELD IS KEPT - SEE THE LOG";
        foreach (var options in new IReadOnlyList<string>[] { [PauseMenu.CloseLine, PauseMenu.SaveLine], PauseMenu.Options(hasTitle: true), PauseMenu.Options(hasTitle: false) })
        {
            foreach (string? said in new[] { null, answer })
            {
                var rows = Laid(options, canSave: true, said, list: null);
                Clear(rows);
                var optionRows = rows.Where(row => options.Contains(row.Text)).ToList();
                Assert.Equal(options, optionRows.Select(row => row.Text));
                var saves = rows.Single(row => row.Text == DungeonHud.PauseSavesLine);
                var esc = rows.Single(row => row.Text == DungeonHud.EscToClose);
                Assert.True(saves.Y > optionRows[^1].Bottom, "the menu's line sits over its last option");
                Assert.True(esc.Y > saves.Bottom);
                Assert.Equal(DungeonHud.PausedTitle, rows[0].Text);
                if (said is not null)
                    Assert.True(rows.Where(row => row.Color == DungeonHud.Orange).All(row => row.Y > esc.Bottom), "the answer sits over the menu's lines");
            }
        }

        // The save list in the options' place: the autosave and nine slots, each with
        // its markers' row, the longest dungeon name, a two-digit floor and three-digit
        // runs, fit a 720-pixel window — the title staying, then the list, then its
        // keys; and each ask's two rows in the keys' place.
        var longest = Dungeons.All.MaxBy(dungeon => dungeon.Name.Length)!;
        var worst = new SaveListing([.. Enumerable.Range(0, SaveSlots.Most + 1)
            .Select(n => Slot(n, SlotState.FromBackup, Header(At.AddMinutes(n), 123, longest.Id, 12), unfinished: true, ahead: true))], []);
        var list = new SlotMenu(SlotMenuMode.Save, worst, SaveSlots.Most);
        Assert.Equal(SaveSlots.Most + 1, list.Rows.Count);
        var laid = Laid(PauseMenu.Options(hasTitle: true), canSave: true, answer: null, list);
        Clear(laid);
        Assert.Equal(2 * list.Rows.Count, laid.Count(row => row.Scale == SlotText.Scale));
        Assert.Equal((DungeonHud.PausedTitle, SlotText.SaveKeysLine), (laid[0].Text, laid[^1].Text));
        Assert.DoesNotContain(laid, row => row.Text == DungeonHud.PauseSavesLine || row.Text == PauseMenu.CloseLine);
        Assert.StartsWith($"  {SaveSlots.AutosaveLabel} - ", laid[1].Text, StringComparison.Ordinal);
        Assert.EndsWith($" - {SlotText.SavesItself}", laid[1].Text, StringComparison.Ordinal);
        Assert.Equal(DungeonHud.DarkGrey, laid[1].Color);   // shown, never chosen
        foreach (var key in new[] { Keys.D9, Keys.D0 })
        {
            var asking = new SlotMenu(SlotMenuMode.Save, worst, SaveSlots.Most);
            asking.Press(key);
            Clear(Laid(PauseMenu.Options(hasTitle: true), canSave: true, answer: null, asking));   // an ask's two rows, or what 0 said over the keys
        }
        var readable = new SlotMenu(SlotMenuMode.Save, new SaveListing([.. worst.Slots.Select(slot => slot with { State = SlotState.Readable })], []), SaveSlots.Most);
        readable.Press(Keys.D9);
        Clear(Laid(PauseMenu.Options(hasTitle: true), canSave: true, answer: null, readable));

        // Both save-list asks' two rows fit 1280 at the keys line's scale, in the font.
        string place = SlotText.Summary(Slot(SaveSlots.Most, header: Header(At, 123, longest.Id, 12)), Dungeons);
        foreach (var (what, keys) in new[] { SlotText.OverwriteAsk(SaveSlots.Most, place, SlotText.When(At, PlusTwo)), SlotText.UnreadableAsk(SaveSlots.Most, backupKept: true) })
            foreach (string row in new[] { what, keys })
            {
                Assert.Matches(Font, row);
                Assert.True(row.Length * 6f * DungeonHud.MenuKeysScale <= W - 2 * Margin, $"'{row}' runs off a {W}-wide window");
            }
        foreach (string row in new[] { SlotText.SaveKeysLine, SlotText.AutosaveSavesItself, SlotText.NotOffered(SaveSlots.Most, 1), PauseMenu.SaveOnYourTurn, PauseMenu.NoSaveInOverride })
            Assert.Matches(Font, row);
    }

    [Fact]
    public void QuittingToTheTitleSavesTheAutosaveFirst()
    {
        // R2(e): "ESC/QUIT from play returns to the title (saving the autosave
        // first)". QUIT TO TITLE writes the autosave through the window close's own
        // door — DistanceRpgGame.SaveOnQuit, which hands CampaignBoot.SaveOnQuit the
        // playing scene's StepLogic (OnlyTheSceneStepDrivesTheTurnSystem pins that) —
        // before it shows the title; drops the campaign, so a close from the title
        // saves nothing; and stores false to that once-only door between them, so the
        // next campaign's quit saves. Read off the IL.
        var game = typeof(DistanceRpgGame);
        var quit = game.GetMethod(nameof(DistanceRpgGame.QuitToTitle))!;
        var walked = Walk(quit);
        var quitSaved = game.GetField("_quitSaved", Declared)!;
        int saved = walked.FindIndex(i => Same(i.Called, game.GetMethod("SaveOnQuit", Declared)!));
        int dropped = walked.FindIndex(i => Same(i.Called, game.GetMethod("DropCampaign", Declared)!));
        int rearmed = walked.FindIndex(i => i.Op == OpCodes.Stfld && Names(i.Field, quitSaved));
        int titled = walked.FindIndex(i => Same(i.Called, game.GetMethod("ShowTitle", Declared)!));
        Assert.True(saved >= 0 && saved < dropped && dropped < titled, "QuitToTitle does not save, then drop the campaign, then show the title");
        Assert.True(saved < rearmed && rearmed < titled, "QuitToTitle does not re-arm the quit's door between its save and the title");
        Assert.Equal(OpCodes.Ldc_I4_0, walked[rearmed - 1].Op);   // false: the next campaign's quit saves
        Assert.Single(walked, i => i.Op == OpCodes.Stfld && Names(i.Field, quitSaved));
        var campaign = game.GetField("_campaign", Declared)!;
        Assert.Contains(Walk(game.GetMethod("DropCampaign", Declared)!), i => i.Op == OpCodes.Stfld && Names(i.Field, campaign));

        // The once-only door sets itself true as it saves, and only QuitToTitle arms it
        // again. (The field is the game's own, so only the game and the types nested in
        // it — its lambdas' closures — can store it.)
        var stores = game.GetNestedTypes(Declared).Prepend(game).SelectMany(type => type.GetMethods(Declared).Cast<MethodBase>())
            .Where(method => Body(method) is not null && Walk(method).Any(i => i.Op == OpCodes.Stfld && Names(i.Field, quitSaved)))
            .Select(method => method.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["QuitToTitle", "SaveOnQuit"], stores);

        // The pause menu's 3 and the menu's between runs reach it through the host,
        // and nothing else does.
        Assert.Equal(["BetweenRunsScene.MenuKey", "DungeonScene.NumberKey"], Callers(typeof(ISaveHost).GetMethod(nameof(ISaveHost.QuitToTitle))!));
        Assert.Equal(["DistanceRpgGame.OnClosing", "DistanceRpgGame.QuitToTitle"], Callers(game.GetMethod("SaveOnQuit", Declared)!));
    }

    [Fact]
    public void AQuitWhoseSaveWentThroughTakesBackAFailedAutosavesWord()
    {
        InTempFolder(folder =>
        {
            // S7's review: an autosave that failed is said on the next screen until a
            // later save goes through — and the quit's own save is one. So the quit's
            // door tells its caller once the campaign stands on the disk as it was
            // left, and the title QUIT TO TITLE opens is never told the campaign could
            // not be saved when the quit's own save put it there. Walked as the game
            // wires it: the failure led for the next screen, the quit's door handed
            // what takes it back.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            Assert.Null(CampaignBoot.Save(store, campaign, savedAt: At));
            CampaignRuns.ShallowRetreat(campaign, "tutorial");   // played on since: the quit has something to write

            string failed = CampaignBoot.WriteFailed("the disk is full");   // what an autosave since then said
            var notices = new StartupNotices();
            notices.Hold(["data"]);
            notices.Lead([failed]);
            int told = 0;
            void TakeBack()
            {
                told++;
                notices.Withdraw(failed);
            }

            // A quit whose write fails tells nothing: the failure stays for the title,
            // beside the marker the title says too.
            Directory.CreateDirectory(store.TempPath);
            Assert.True(CampaignBoot.SaveOnQuit(store, campaign, view: null, step: null, savedAt: At.AddMinutes(1), saved: TakeBack));
            Assert.Equal(0, told);
            Assert.NotNull(store.ReadUnsaved());
            Directory.Delete(store.TempPath);
            Assert.True(CampaignBoot.TakeUnsaved(store, report: null));

            // Nor does a quit during the enemies' turn that never comes round to the
            // party's, or that has no scene to play it out with: nothing is written.
            var stuck = ReloadDeterminismTests.MidWalk();
            Assert.False(CampaignBoot.SaveOnQuit(store, stuck.Campaign, () => new ViewDto("A"), () => { }, saved: TakeBack));
            Assert.False(CampaignBoot.SaveOnQuit(store, stuck.Campaign, () => new ViewDto("A"), step: null, saved: TakeBack));
            Assert.Equal(0, told);
            Assert.True(CampaignBoot.TakeUnsaved(store, report: null));

            // A quit whose save goes through tells, once: the title is handed nothing of
            // the failure, and what else leads it stays.
            Assert.True(CampaignBoot.SaveOnQuit(store, campaign, view: null, step: null, savedAt: At.AddMinutes(2), saved: TakeBack));
            Assert.Equal(1, told);
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(campaign, savedAt: At.AddMinutes(2))), store.Read());
            Assert.Null(store.ReadUnsaved());
            Assert.Equal(["data"], notices.Take());

            // So does one whose save the disk holds already: nothing needed writing, and
            // the disk holds the campaign as it was left.
            Assert.True(CampaignBoot.SaveOnQuit(store, campaign, view: null, step: null, savedAt: At.AddMinutes(3), saved: TakeBack));
            Assert.Equal(2, told);
        });

        // Read off the IL: the game's SaveOnQuit hands the quit's door the take-back —
        // a delegate made before the call, never run in its place — and the take-back
        // is reached from there and from the autosave's Save alone; it withdraws each
        // failed save's word and forgets them. And dropping the campaign forgets them
        // too, so no later campaign's save takes anything back on the dropped one's
        // behalf.
        var game = typeof(DistanceRpgGame);
        var takeBack = game.GetMethod("TakeBackFailedSaves", Declared)!;
        var quitting = Walk(game.GetMethod("SaveOnQuit", Declared)!);
        int handed = quitting.FindIndex(i => i.Op == OpCodes.Ldftn && Same(i.Called, takeBack));
        int door = quitting.FindIndex(i => Same(i.Called, typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.SaveOnQuit), Declared)!));
        Assert.True(handed >= 0 && handed < door, "The game's SaveOnQuit does not hand the quit's door what takes a failed save's word back");
        Assert.DoesNotContain(quitting, i => IsCall(i.Op) && Same(i.Called, takeBack));
        Assert.Equal(["DistanceRpgGame.Save called", "DistanceRpgGame.SaveOnQuit handed"],
            Edges(game.Assembly).Where(e => Same(e.Target, takeBack))
                .Select(e => $"{Root(e.From.DeclaringType)!.Name}.{e.From.Name} {(IsDelegate(e.Op) ? "handed" : "called")}")
                .Distinct().Order(StringComparer.Ordinal));

        var failedSaves = game.GetField("_failedSaves", Declared)!;
        static bool Forgets(List<(OpCode Op, MethodBase? Called, FieldInfo? Field)> walked, FieldInfo list)
            => walked.Select((instruction, at) => (instruction, at)).Any(i => i.instruction.Op == OpCodes.Ldfld && Names(i.instruction.Field, list)
                && i.at + 1 < walked.Count && walked[i.at + 1].Called is { Name: nameof(List<string>.Clear) } clear && clear.DeclaringType == typeof(List<string>));
        var takingBack = Walk(takeBack);
        Assert.Contains(takingBack, i => Same(i.Called, typeof(StartupNotices).GetMethod(nameof(StartupNotices.Withdraw))!));
        Assert.True(Forgets(takingBack, failedSaves), "TakeBackFailedSaves does not forget what it took back");
        Assert.True(Forgets(Walk(game.GetMethod("DropCampaign", Declared)!), failedSaves), "DropCampaign does not forget the dropped campaign's failed saves");
    }

    [Fact]
    public void LeavingPlayClosesTheDungeonsKeys()
    {
        // S7's review: once the pause menu's 3 has asked the host to leave play, the
        // dungeon is still the scene up for the first half of the engine's fade to
        // the title, its menu still up — and, as between runs, it reads no key from
        // then on. 2 lists no slots; 1 leaves the menu up, so nothing behind it acts
        // (every other key and click does nothing behind a menu); 3 asks nothing
        // twice; and ESC is swallowed. No key reaches the campaign the host let go.
        var host = new RecordingHost(Worded());
        var scene = new DungeonScene(camera: null!, game: null!, Entered(), host);
        scene.HandleEscape();
        scene.AnyKey(Keys.D3);
        Assert.Equal(1, host.QuitsToTitle);
        foreach (var key in new[] { Keys.D2, Keys.D1, Keys.D3, Keys.D4, Keys.Enter, Keys.Escape })
            scene.AnyKey(key);
        Assert.True(scene.HandleEscape());
        Assert.Equal((true, false, (SlotMenu?)null), (scene.PauseMenuOpen, scene.SavesOpen, scene.SaveList));
        Assert.Equal((0, 0, 1), (host.SlotsListed, host.SavedTo.Count, host.QuitsToTitle));

        // Play not left, the same keys act: 1 closes the menu, and ESC opens it again.
        var staying = new DungeonScene(camera: null!, game: null!, Entered(), new RecordingHost(Worded()));
        staying.HandleEscape();
        staying.AnyKey(Keys.D1);
        Assert.False(staying.AnyMenuOpen);
        Assert.True(staying.HandleEscape());
        Assert.True(staying.PauseMenuOpen);

        // A dev-override session's 3  QUIT GAME — the window's close, which a scene
        // built with no window cannot walk — leaves play the same way: NumberKey marks
        // the scene as leaving before it hands on either way out, and the keys' one
        // door, the number keys' and ESC's each read the mark first. Read off the IL.
        var leaving = typeof(DungeonScene).GetField("_leaving", Declared)!;
        var numberKey = Walk(typeof(DungeonScene).GetMethod("NumberKey", Declared)!);
        var marked = numberKey.Select((instruction, at) => (instruction, at))
            .Where(i => i.instruction.Op == OpCodes.Stfld && Names(i.instruction.Field, leaving)).Select(i => i.at).ToList();
        int toTitle = numberKey.FindIndex(i => Same(i.Called, typeof(ISaveHost).GetMethod(nameof(ISaveHost.QuitToTitle))!));
        int closed = numberKey.FindIndex(i => Same(i.Called, typeof(GameEngine.Core.Game).GetMethod("Close", Type.EmptyTypes)!.GetBaseDefinition()));
        Assert.Equal(2, marked.Count);
        Assert.All(marked, at => Assert.Equal(OpCodes.Ldc_I4_1, numberKey[at - 1].Op));
        Assert.True(marked[0] < toTitle && toTitle < marked[1] && marked[1] < closed, "NumberKey leaves play before it marks the scene as leaving");
        foreach (string reads in new[] { "AnyKey", "NumberKey", "HandleEscape" })
        {
            var walked = Walk(typeof(DungeonScene).GetMethod(reads, Declared)!);
            int read = walked.FindIndex(i => i.Op == OpCodes.Ldfld && Names(i.Field, leaving));
            Assert.True(read >= 0 && walked.Take(read).All(i => i.Called is null || i.Called.Name == "get_" + nameof(DungeonScene.Transiting)),
                $"DungeonScene.{reads} acts before it reads whether play has been left");
        }
    }

    [Fact]
    public void ASaveByHandThenAQuitWithNothingPlayedContinuesTheSlot()
    {
        InTempFolder(folder =>
        {
            // S7's review, accepted as it stands (settled.md, R2, saving by hand): a
            // save by hand into an empty slot, then QUIT TO TITLE with nothing played
            // since. The autosave holds that campaign already, so the quit writes
            // nothing and the autosave keeps the time its state was first written (R2,
            // the folder), while the slot carries the later time of the save by hand:
            // CONTINUE — the most recent save by its own time — names the slot. It holds
            // the autosave's own campaign, so loading it asks nothing and sets nothing
            // aside, and the campaign it brings back is the autosave's.
            var slots = new SaveSlots(folder, 3);
            var autosave = slots.Store(SaveSlots.Autosave);
            var campaign = Retreated();
            Assert.Null(CampaignBoot.Save(autosave, campaign, savedAt: At));   // the autosave at the turn's start
            Assert.Equal((true, "SAVED TO SLOT 1"), CampaignBoot.SaveTo(slots.Store(1), campaign, view: null, 1, savedAt: At.AddMinutes(1)));
            int told = 0;
            Assert.True(CampaignBoot.SaveOnQuit(autosave, campaign, view: null, step: null, savedAt: At.AddMinutes(2), saved: () => told++));
            Assert.Equal(1, told);   // the quit went through: the disk holds the campaign as it was left
            Assert.False(autosave.HasBackup);   // and wrote nothing: no save rotated into the backup

            var listing = SaveListing.Read(slots, At.AddMinutes(3));
            Assert.Equal((DateTimeOffset?)At, listing.Autosave.Header!.SavedAt);
            var newest = listing.Newest!;
            Assert.Equal((1, true, (DateTimeOffset?)At.AddMinutes(1)), (newest.Number, newest.SameAsAutosave, newest.Header!.SavedAt));
            Assert.False(listing.NeedsRoom(1));

            var load = CampaignBoot.Load(slots.Store(1), GameContent.Current, number: 1);
            Assert.True(load.Loaded);
            Assert.Equal(SaveCodec.Untimed(autosave.Read()!), SaveCodec.Untimed(SaveCodec.Write(SaveGame.Capture(load.Campaign!))));
            slots.Store(1).ClearResuming();
        });
    }

    [Fact]
    public void ADevOverrideSessionNeverSavesToASlot()
    {
        // A dev-override session plays a scratch campaign apart from the player's
        // saves, with no title and no manual slots: its menu's 2 says so, and the host
        // refuses a save by hand outright — so no path past the menu can put a scratch
        // campaign in the player's slot-n.
        Assert.Equal((PauseAction.Refuse, PauseMenu.NoSaveInOverride), PauseMenu.Press(2, canSave: true, hasTitle: false));

        // Read off the IL: SaveToSlot asks HasTitle before it asks for anything, and
        // HasTitle is whether the session made the save folder's slots, which a dev
        // override never does; the write is the frame's end's, which reads the slots
        // before it saves, into a slot's own store, never the session's autosave.
        var game = typeof(DistanceRpgGame);
        var saveToSlot = game.GetMethod(nameof(DistanceRpgGame.SaveToSlot))!;
        var hasTitle = game.GetProperty(nameof(DistanceRpgGame.HasTitle))!.GetMethod!;
        var requested = game.GetField("_slotRequested", Declared)!;
        var slots = game.GetField("_slots", Declared)!;
        var saveTo = typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.SaveTo), Declared)!;
        var asking = Walk(saveToSlot);
        int asked = asking.FindIndex(i => Same(i.Called, hasTitle));
        int stored = asking.FindIndex(i => i.Op == OpCodes.Stfld && Names(i.Field, requested));
        Assert.True(asked >= 0 && asked < stored, "SaveToSlot asks for a save before it has asked whether the session has slots");
        Assert.DoesNotContain(Reachable(saveToSlot), method => Same(method, saveTo));
        Assert.Contains(Walk(hasTitle), i => i.Op == OpCodes.Ldfld && Names(i.Field, slots));

        var write = game.GetMethod("WriteRequestedSlot", Declared)!;
        Assert.Equal(["DistanceRpgGame.WriteRequestedSlot"], Callers(saveTo));
        Assert.Equal(["DistanceRpgGame.OnUpdateFrame"], Callers(write));
        var writing = Walk(write);
        int readSlots = writing.FindIndex(i => i.Op == OpCodes.Ldfld && Names(i.Field, slots));
        int savedTo = writing.FindIndex(i => Same(i.Called, saveTo));
        Assert.True(readSlots >= 0 && readSlots < savedTo, "WriteRequestedSlot saves before it has read the slots");
        Assert.Contains(writing, i => Same(i.Called, typeof(SaveSlots).GetMethod(nameof(SaveSlots.Store))!));
        Assert.DoesNotContain(writing, i => i.Op == OpCodes.Ldfld && Names(i.Field, game.GetField("_autosave", Declared)!));

        // Both screens ask the host whether it has a title before 2 does anything.
        foreach (var (scene, menu) in new[] { (typeof(DungeonScene), "NumberKey"), (typeof(BetweenRunsScene), "MenuKey") })
        {
            var keys = Walk(scene.GetMethod(menu, Declared)!);
            int title = keys.FindIndex(i => Same(i.Called, typeof(ISaveHost).GetProperty(nameof(ISaveHost.HasTitle))!.GetMethod!));
            int press = keys.FindIndex(i => Same(i.Called, typeof(PauseMenu).GetMethod(nameof(PauseMenu.Press), Declared)!));
            Assert.True(title >= 0 && title < press, $"{scene.Name}.{menu} presses the menu before it knows whether the session has a title");
        }

        // Between runs, walked: 2 says why, and the host is asked for nothing.
        var host = new RecordingHost(Worded(), hasTitle: false);
        var betweenRuns = new BetweenRunsScene(game: null!, CampaignRuns.Campaign(), host, shownWhole: 0);
        betweenRuns.HandleEscape();
        betweenRuns.OnKey(Keys.D2);
        Assert.Equal((PauseMenu.NoSaveInOverride, (SlotMenu?)null, 0, 0), (betweenRuns.MenuAnswer, betweenRuns.SaveList, host.SlotsListed, host.SavedTo.Count));
    }
}

/// <summary>
/// The host of a screen built with no window and walked key by key: what it is
/// asked for is recorded, the slots it lists are the ones it was handed, and it
/// saves, quits and descends nothing itself.
/// </summary>
internal sealed class RecordingHost : IRunHost, IBetweenRunsHost
{
    private readonly SaveListing _listing;

    public RecordingHost(SaveListing listing, bool hasTitle = true)
    {
        _listing = listing;
        HasTitle = hasTitle;
    }

    public bool HasTitle { get; }

    /// <summary>How many times the slots were listed.</summary>
    public int SlotsListed { get; private set; }

    /// <summary>Every slot a save by hand was asked for, in order.</summary>
    public List<int> SavedTo { get; } = [];

    /// <summary>How many times play was left for the title.</summary>
    public int QuitsToTitle { get; private set; }

    /// <summary>Every dungeon descended into, in order.</summary>
    public List<string> Descended { get; } = [];

    public SaveListing Slots()
    {
        SlotsListed++;
        return _listing;
    }

    public void SaveToSlot(int slot) => SavedTo.Add(slot);

    public void QuitToTitle() => QuitsToTitle++;

    public void Descend(string dungeonId) => Descended.Add(dungeonId);

    public void RunOver() => throw new InvalidOperationException("A screen walked with no window is never played to a run's end.");

    public void RequestSave() => throw new InvalidOperationException("A screen walked with no window never opens a floor, so it never asks for an autosave.");
}
