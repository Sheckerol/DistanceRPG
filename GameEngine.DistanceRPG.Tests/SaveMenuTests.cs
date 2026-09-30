using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static GameEngine.DistanceRPG.Tests.TestIl;
using static GameEngine.DistanceRPG.Tests.TestSlots;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Saving by hand, and leaving play for the title (the user's ruling of
/// 2026-09-25 on the save model, R2(d) and R2(e); Phase 5.S7), as the user's
/// ruling of the same day on the save slots (R5; Phase 5.T4) has it: "any saving
/// happens on that save slot instead of choosing a save slot everytime you save".
/// The one menu both screens with a campaign in play open on ESC — <c>1  CLOSE</c>,
/// <c>2  SAVE GAME</c>, <c>3  QUIT TO TITLE</c> (<see cref="PauseMenu"/>) — saves
/// only at a safe point, into the campaign's own slot, asking nothing, through the
/// one door every save of the campaign goes through; a save by hand of the
/// campaign the slot holds already stamps its time, the backup kept; what is set
/// aside is the files' to decide; the answer is drawn inside the menu, never as a
/// notice, and the menu names the slot; and QUIT TO TITLE writes the campaign's
/// slot through the window close's own door before the title. A dev-override
/// session saves its scratch slot, and only that. As with <c>TitleTests</c>, the
/// drawing is untested but what it draws is: the words, the keys, where every row
/// goes — all pure — and the scene walked key by key, built with no window. The
/// saves are real, each on a temporary folder of its own, under the numbers the
/// runs were written against (<see cref="Premise"/>), so the content collection
/// holds it.
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

    /// <summary>18:10 UTC, the day before the ruling.</summary>
    private static readonly DateTimeOffset At = new(2026, 9, 24, 18, 10, 0, TimeSpan.Zero);

    private const BindingFlags Declared = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>What the menu says under its options for a campaign in slot 2.</summary>
    private static readonly string InSlot2 = DungeonHud.PauseSavesLine("SLOT 2");

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

    /// <summary>Every file under <paramref name="folder"/>, by its path from there, with its bytes.</summary>
    private static Dictionary<string, byte[]> Snapshot(string folder)
        => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(folder, path), File.ReadAllBytes);

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

    /// <summary>
    /// <paramref name="method"/>'s body in step with <see cref="Walk"/>, an instruction at a time: where it
    /// starts, and what its operand says as a number — a local's index, a branch's target as an offset into
    /// the body, a string's token — or -1 for anything else.
    /// </summary>
    private static List<(int Start, int Operand)> Operands(MethodBase method)
    {
        byte[] il = Body(method)!;
        return InstructionsAt(il)
            .Select(i => (i.At - i.Op.Size, i.Op.OperandType switch
            {
                OperandType.ShortInlineVar => il[i.At],
                OperandType.InlineVar => BitConverter.ToUInt16(il, i.At),
                OperandType.ShortInlineBrTarget => i.At + 1 + (sbyte)il[i.At],
                OperandType.InlineBrTarget => i.At + 4 + BitConverter.ToInt32(il, i.At),
                OperandType.InlineString => BitConverter.ToInt32(il, i.At),
                _ => -1,
            }))
            .ToList();
    }

    /// <summary>The local an instruction loads — or, with <paramref name="load"/> false, stores — by its index; null for one that does neither.</summary>
    private static int? Local(OpCode op, int operand, bool load)
    {
        OpCode[] numbered = load ? [OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3] : [OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3];
        int at = Array.IndexOf(numbered, op);
        if (at >= 0)
            return at;
        return op == (load ? OpCodes.Ldloc_S : OpCodes.Stloc_S) || op == (load ? OpCodes.Ldloc : OpCodes.Stloc) ? operand : null;
    }

    /// <summary>Every method in the game assembly that calls <paramref name="target"/>, as <c>Type.Method</c> of its outermost type.</summary>
    private static string[] Callers(MethodBase target)
        => Calls(typeof(DistanceRpgGame).Assembly).Where(c => Same(c.Target, target))
            .Select(c => $"{Root(c.From.DeclaringType)!.Name}.{c.From.Name}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>Every method of <paramref name="type"/>, and of the types nested in it — its lambdas' closures — that has a body.</summary>
    private static IEnumerable<MethodBase> MethodsOf(Type type)
        => type.GetNestedTypes(Declared).Prepend(type)
            .SelectMany(nested => nested.GetMethods(Declared).Cast<MethodBase>().Concat(nested.GetConstructors(Declared)))
            .Where(method => Body(method) is not null);

    /// <summary>The menu's rows fit the window: in the font, within its width with room either side, no two overlapping, and the block between its margins.</summary>
    private static void Clear(IReadOnlyList<DungeonHud.MenuRow> rows, float height = H)
    {
        Assert.All(rows, row => Assert.Matches(Font, row.Text));
        Assert.All(rows, row =>
        {
            float left = W / 2f - row.Width / 2f;
            Assert.True(left >= Margin - 0.001f && left + row.Width <= W - Margin + 0.001f, $"'{row.Text}' runs off a {W}-wide window");
        });
        var down = rows.OrderBy(row => row.Y).ToList();
        for (int i = 1; i < down.Count; i++)
            Assert.True(down[i].Y >= down[i - 1].Bottom, $"'{down[i].Text}' overlaps '{down[i - 1].Text}'");
        Assert.True(down[0].Y >= DungeonHud.MenuTopMargin - 0.001f, $"'{down[0].Text}' rises past the menu's top margin");
        Assert.True(down[^1].Bottom <= height - DungeonHud.MenuBottomMargin + 0.001f, $"'{down[^1].Text}' runs into the line along the bottom of a {height}-tall window");
    }

    private static IReadOnlyList<DungeonHud.MenuRow> Laid(IReadOnlyList<string> options, bool canSave, string? answer, string? savesLine = null, float height = H)
        => DungeonHud.PauseMenuLayout(W, height, options, canSave, savesLine ?? InSlot2, answer);

    [Fact]
    public void ThePauseMenuSavesOnYourOwnTurn_AndQuitsToTheTitle()
    {
        // R2(d): "a manual save can be made at ANY SAFE POINT ... never while enemies
        // are moving"; R2(e): the menus leave play for the title; R5: "any saving
        // happens on that save slot instead of choosing a save slot everytime you
        // save". The menu, digit by digit: 1 closes it; 2 saves at a safe point — no
        // list, nothing asked — and says why not off one; 3 quits to the title — at
        // any point, since the quit's own door plays the enemies' turn out first.
        Assert.Equal((PauseAction.Close, (string?)null), PauseMenu.Press(1, canSave: true, hasTitle: true));
        Assert.Equal((PauseAction.Close, (string?)null), PauseMenu.Press(1, canSave: false, hasTitle: false));
        Assert.Equal((PauseAction.Save, (string?)null), PauseMenu.Press(2, canSave: true, hasTitle: true));
        Assert.Equal((PauseAction.Refuse, "SAVE ON YOUR OWN TURN"), PauseMenu.Press(2, canSave: false, hasTitle: true));
        Assert.Equal((PauseAction.QuitToTitle, (string?)null), PauseMenu.Press(3, canSave: true, hasTitle: true));
        Assert.Equal((PauseAction.QuitToTitle, (string?)null), PauseMenu.Press(3, canSave: false, hasTitle: true));
        foreach (int digit in new[] { 0, 4, 5, 9 })
            Assert.Equal((PauseAction.None, (string?)null), PauseMenu.Press(digit, canSave: true, hasTitle: true));

        // A dev-override session has no title: 3 quits the game. Its 2 saves as every
        // session's does — the campaign's own slot is its scratch slot — and off a
        // safe point says why, as every session's does.
        Assert.Equal(["1  CLOSE", "2  SAVE GAME", "3  QUIT GAME"], PauseMenu.Options(hasTitle: false));
        Assert.Equal((PauseAction.QuitGame, (string?)null), PauseMenu.Press(3, canSave: true, hasTitle: false));
        Assert.Equal((PauseAction.Save, (string?)null), PauseMenu.Press(2, canSave: true, hasTitle: false));
        Assert.Equal((PauseAction.Refuse, "SAVE ON YOUR OWN TURN"), PauseMenu.Press(2, canSave: false, hasTitle: false));

        // Walked through a dungeon scene, built with no window over a run entered and
        // not yet opened — a safe point. ESC opens the menu; 2 asks the host to save,
        // once, and the menu stays up for the answer, which the host gives once the
        // frame is over — no list opens in its place; ESC closes the menu; 3 asks the
        // host to leave play for the title.
        var host = new RecordingHost();
        var scene = new DungeonScene(camera: null!, game: null!, Entered(), host);
        Assert.True(scene.HandleEscape());
        Assert.True(scene.PauseMenuOpen);
        Assert.True(scene.CanSaveNow);
        scene.AnyKey(Keys.D2);
        Assert.Equal((1, true, (string?)null), (host.HandSaves, scene.PauseMenuOpen, scene.MenuAnswer));
        Assert.True(scene.HandleEscape());
        Assert.False(scene.AnyMenuOpen);
        scene.HandleEscape();
        scene.AnyKey(Keys.D3);
        Assert.Equal((1, 1), (host.QuitsToTitle, host.HandSaves));

        // During the enemies' turn — its banner up, here — 2 saves nothing and says
        // why, inside the menu, and asks the host for nothing; 3 still quits.
        var ending = Entered();
        var turns = ending.Visit!.OpenTurns(ending.Party, CampaignRuns.Ten);
        turns.EndTurn();
        Assert.False(SaveGame.IsSafePoint(ending));
        var busy = new RecordingHost();
        var offTurn = new DungeonScene(camera: null!, game: null!, ending, busy);
        offTurn.HandleEscape();
        Assert.False(offTurn.CanSaveNow);
        offTurn.AnyKey(Keys.D2);
        Assert.Equal((PauseMenu.SaveOnYourTurn, true, 0), (offTurn.MenuAnswer, offTurn.PauseMenuOpen, busy.HandSaves));
        offTurn.AnyKey(Keys.D3);
        Assert.Equal(1, busy.QuitsToTitle);

        // And off a title: 2 asks the host to save — the scratch slot, its campaign's
        // own — as every session's does.
        var scratch = new RecordingHost(hasTitle: false, savesInto: SaveSlot.ScratchName);
        var apart = new DungeonScene(camera: null!, game: null!, Entered(), scratch);
        apart.HandleEscape();
        apart.AnyKey(Keys.D2);
        Assert.Equal((1, (string?)null, true), (scratch.HandSaves, apart.MenuAnswer, apart.PauseMenuOpen));
    }

    [Fact]
    public void SaveGameSavesIntoTheCampaignsOwnSlot_AndSaysWhich()
    {
        // The user's ruling of 2026-09-25 on the save slots (R5): "you choose a save
        // slot and any saving happens on that save slot instead of choosing a save
        // slot everytime you save". SAVE GAME is one key: 2 at a safe point saves,
        // and off one says why.
        Assert.Equal((PauseAction.Save, (string?)null), PauseMenu.Press(2, canSave: true, hasTitle: true));
        Assert.Equal((PauseAction.Refuse, PauseMenu.SaveOnYourTurn), PauseMenu.Press(2, canSave: false, hasTitle: true));

        // Walked through both screens: 2 asks the host to save at once, and again —
        // no list opens, no digit chooses a slot, nothing asks — the menu stays up for
        // the answer, and no key after it reaches what is behind the menu: nothing is
        // asked twice, nothing is left, and between runs nothing moves or descends.
        var host = new RecordingHost();
        var entered = Entered();
        var scene = new DungeonScene(camera: null!, game: null!, entered, host);
        scene.HandleEscape();
        scene.AnyKey(Keys.D2);
        scene.AnyKey(Keys.D2);
        Assert.Equal((2, GameMenu.Pause), (host.HandSaves, scene.ActiveMenu));
        int active = scene.ActiveIndex;

        // In the dungeon the menu takes the number keys alone, through the one
        // handler every key goes through (AnyKey): the digits it does not use do
        // nothing — 0, which it passes to no one, among them.
        foreach (var key in new[] { Keys.D0, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9 })
            scene.AnyKey(key);
        Assert.Equal((2, 0, GameMenu.Pause, true, active), (host.HandSaves, host.QuitsToTitle, scene.ActiveMenu, scene.CanSaveNow, scene.ActiveIndex));

        // Every other key has a handler of its own (WireInput), which the menu never
        // routes, each doing nothing behind a menu (the T4 review). Each is pressed
        // with the menu up — the window's own call, since a scene built with no
        // window has none, over the turn system the visit opens as a floor's would
        // be — and leaves the scene as it was: the menu up, the turn the party's,
        // the same member active, the host asked for nothing more. The held movement
        // keys only mark a key held, which the frame reads behind no menu.
        var turns = entered.Visit!.OpenTurns(entered.Party, CampaignRuns.Ten);
        typeof(DungeonScene).GetField("_turns", Declared)!.SetValue(scene, turns);
        Assert.True(scene.CanSaveNow);
        var wire = Walk(typeof(DungeonScene).GetMethod("WireInput", Declared)!);
        var pressed = new List<MethodInfo>();
        MethodInfo? handler = null;
        foreach (var instruction in wire)
        {
            if (instruction.Op == OpCodes.Ldftn)
            {
                handler = (MethodInfo)instruction.Called!;
            }
            else if (instruction.Called?.DeclaringType == typeof(GameEngine.Core.InputContext))
            {
                if (instruction.Called.Name == nameof(GameEngine.Core.InputContext.SubscribeToKeyPressed))
                    pressed.Add(handler!);
                handler = null;   // each subscription hands over the handler made just before it
            }
        }
        var anyKey = typeof(DungeonScene).GetMethod(nameof(DungeonScene.AnyKey), Declared)!;
        pressed.RemoveAll(method => Walk(method).Any(i => Same(i.Called, anyKey)));   // walked above, key by key
        Assert.True(pressed.Count >= 10, $"WireInput subscribes {pressed.Count} key presses beside AnyKey's");
        foreach (var press in pressed)
            press.Invoke(scene, [null]);
        Assert.Equal((2, 0, GameMenu.Pause, TurnPhase.Player, true, active), (host.HandSaves, host.QuitsToTitle, scene.ActiveMenu, turns.Phase, scene.CanSaveNow, scene.ActiveIndex));

        // And the three keys that act at once, in the lambda WireInput hands them —
        // TAB picks the next member (with no floor built there is no party here to
        // pick from), SPACE ends the turn, G picks up — ask whether a menu is open
        // before they act.
        var menuOpen = typeof(DungeonScene).GetProperty(nameof(DungeonScene.AnyMenuOpen))!.GetMethod!;
        foreach (var act in new MethodBase[]
                 {
                     typeof(DungeonScene).GetMethod("CycleActiveCharacter", Declared)!,
                     typeof(TurnSystem).GetMethod(nameof(TurnSystem.EndTurn))!,
                     typeof(DungeonScene).GetMethod("PickUp", Declared)!,
                 })
        {
            var acting = pressed.Select(Walk).Where(walked => walked.Any(i => Same(i.Called, act))).ToList();
            Assert.Single(acting);
            int asked = acting[0].FindIndex(i => Same(i.Called, menuOpen));
            Assert.True(asked >= 0 && asked < acting[0].FindIndex(i => Same(i.Called, act)), $"a key's handler reaches {act.Name} without asking whether a menu is open");
        }

        var hubHost = new RecordingHost();
        var hub = new BetweenRunsScene(game: null!, CampaignRuns.Campaign(), hubHost, shownWhole: 0);
        hub.HandleEscape();
        hub.OnKey(Keys.D2);
        hub.OnKey(Keys.D2);
        foreach (var key in new[] { Keys.D0, Keys.D4, Keys.D9, Keys.Enter, Keys.KeyPadEnter, Keys.W, Keys.S, Keys.Down })
            hub.OnKey(key);
        Assert.Equal((2, true, 0, 0), (hubHost.HandSaves, hub.MenuOpen, hubHost.Descended.Count, hub.Chosen));

        // The menu says which slot is the campaign's, under its options: the slot it
        // was loaded from or begun in — the old autosave, for a campaign still in the
        // save folder — or a dev override's scratch slot.
        Assert.Equal("PROGRESS SAVES TO SLOT 2 AT EVERY TURN", DungeonHud.PauseSavesLine(scene.SavesInto));
        Assert.Equal("PROGRESS SAVES TO THE OLD AUTOSAVE AT EVERY TURN", DungeonHud.PauseSavesLine(Named(SaveSlots.OldAutosave).Name));
        Assert.Equal("PROGRESS SAVES TO THE SCRATCH SLOT AT EVERY TURN", DungeonHud.PauseSavesLine(Named(null).Name));
        Assert.Contains(Laid(PauseMenu.Options(hasTitle: true), scene.CanSaveNow, answer: null, DungeonHud.PauseSavesLine(scene.SavesInto)),
            row => row.Text == "PROGRESS SAVES TO SLOT 2 AT EVERY TURN");

        // What the save came to, as the host answers it (CampaignBoot.SaveAnswer):
        // SAVED TO SLOT 2 in green, with each file the write set aside first, where
        // and how to try it again; or why not, in orange; drawn inside the menu.
        InTempFolder(folder =>
        {
            var slots = new SaveSlots(folder, 3);
            var slot2 = slots.Slot(2);
            Assert.Null(CampaignBoot.Save(slot2, Cleared(), savedAt: At, byHand: true));
            Assert.Equal("SAVED TO SLOT 2", CampaignBoot.SaveAnswer(slot2, failed: null, []));

            // A newer build's save planted in the slot behind the game's back, as the
            // campaign is played from it: set aside first, and said.
            var planted = new SaveSlots(folder, 3);
            File.WriteAllText(planted.Store(2).SavePath, Newer(planted.Store(2).Read()!));
            var said = new List<string>();
            string? failed = CampaignBoot.Save(planted.Slot(2), Retreated(), savedAt: At.AddHours(1), said: said, byHand: true);
            Assert.Null(failed);
            string aside = Path.Combine(planted.Store(2).Folder, "campaign.unreadable-1.json");
            Assert.Equal([CampaignBoot.SetAsideNotice(SaveStore.FileName, $"could not be read (version: {SaveCodec.CurrentVersion + 1} is newer than this build reads ({SaveCodec.CurrentVersion}))", aside)], said);
            string answer = CampaignBoot.SaveAnswer(planted.Slot(2), failed, said);
            Assert.Equal($"SAVED TO SLOT 2 - {said[0]}", answer);
            Assert.Equal(DungeonHud.Green, DungeonHud.AnswerColor(answer));

            // A slot whose store refuses its writes this session answers why, and
            // writes nothing.
            var refusing = new SaveSlots(folder, 3);
            refusing.Store(2).RefuseWrites("campaign.json could not be read (x) and could not be set aside (another program has it open)");
            byte[] before = File.ReadAllBytes(refusing.Store(2).SavePath);
            string? refused = CampaignBoot.Save(refusing.Slot(2), Cleared(), savedAt: At.AddHours(2), byHand: true);
            Assert.Equal("SLOT 2 COULD NOT BE SAVED (CAMPAIGN.JSON COULD NOT BE READ (X) AND COULD NOT BE SET ASIDE (ANOTHER PROGRAM HAS IT OPEN)) - WHAT IT HELD IS KEPT - SEE THE LOG",
                CampaignBoot.SaveAnswer(refusing.Slot(2), refused, []));
            Assert.Equal(before, File.ReadAllBytes(refusing.Store(2).SavePath));
            Assert.Equal(DungeonHud.Orange, DungeonHud.AnswerColor(refused!));

            // A save that failed after the guard set a file aside says both, the
            // failure first, in orange — the file is set aside whichever way the save
            // went — and lays out clear (the T4 review).
            string failedAfter = CampaignBoot.NotSaved(slot2, "the disk is full");
            string both = CampaignBoot.SaveAnswer(slot2, failedAfter, said);
            Assert.Equal($"{failedAfter} - {said[0]}", both);
            Assert.Equal(DungeonHud.Orange, DungeonHud.AnswerColor(both));
            Clear(Laid(PauseMenu.Options(hasTitle: true), canSave: true, both));

            // A dev override's scratch slot says its own name.
            var scratch = SaveSlot.Scratch(CampaignBoot.StoreFor(folder, new BootRequest("spear_hall")));
            Assert.Null(CampaignBoot.Save(scratch, Cleared(), savedAt: At, byHand: true));
            Assert.Equal("SAVED TO THE SCRATCH SLOT", CampaignBoot.SaveAnswer(scratch, failed: null, []));
        });
        scene.SlotSaved(CampaignBoot.SaveAnswer(Named(2), failed: null, []));
        Assert.Equal("SAVED TO SLOT 2", scene.MenuAnswer);
        hub.SlotSaved(CampaignBoot.SaveAnswer(Named(2), failed: null, []));
        Assert.Equal("SAVED TO SLOT 2", hub.MenuAnswer);

        // Read off the IL: neither screen makes, holds or reads a slot list — the one
        // SlotMenu member either touches is its reading of a digit key — and SAVE
        // GAME reaches the host from the two menus' number keys alone.
        foreach (var screen in new[] { typeof(DungeonScene), typeof(BetweenRunsScene) })
        {
            Assert.DoesNotContain(screen.GetFields(Declared), field => field.FieldType == typeof(SlotMenu));
            Assert.DoesNotContain(screen.GetProperties(Declared), property => property.PropertyType == typeof(SlotMenu));
            var touched = MethodsOf(screen).SelectMany(Walk)
                .Where(i => i.Called?.DeclaringType == typeof(SlotMenu) || i.Field?.DeclaringType == typeof(SlotMenu))
                .Select(i => i.Called?.Name ?? i.Field!.Name)
                .Distinct();
            Assert.All(touched, name => Assert.Equal(nameof(SlotMenu.Digit), name));
        }
        Assert.Equal(["BetweenRunsScene.MenuKey", "DungeonScene.NumberKey"], Callers(typeof(ISaveHost).GetMethod(nameof(ISaveHost.SaveNow))!));
    }

    [Fact]
    public void ASaveIntoAnUnreadableSlotSetsItAsideFirst()
    {
        InTempFolder(folder =>
        {
            // The campaign's own slot holding a newer build's save — put there behind
            // the game's back — which this build never reads nor writes over: SAVE GAME
            // sets it aside and saves there — the old bytes kept in the slot's own
            // folder as campaign.unreadable-1.json, never deleted, the new save
            // written, and the answer saying both. The files decide, never a listing.
            var slots = new SaveSlots(folder, 3);
            var slot3 = slots.Store(3);
            Directory.CreateDirectory(slot3.Folder);
            File.WriteAllText(slot3.SavePath, Newer(SaveCodec.Write(SaveGame.Capture(Cleared(), savedAt: At))));
            byte[] old = File.ReadAllBytes(slot3.SavePath);
            Assert.Equal(SlotState.Unreadable, SaveListing.Read(slots, At.AddHours(1)).Slots.Single(slot => slot.Number == 3).State);

            var campaign = Retreated();
            var log = new List<string>();
            var said = new List<string>();
            string? failed = CampaignBoot.Save(slots.Slot(3), campaign, view: null, log.Add, At.AddHours(1), said, byHand: true);
            string notice = CampaignBoot.SaveAnswer(slots.Slot(3), failed, said);
            string aside = Path.Combine(slot3.Folder, "campaign.unreadable-1.json");
            Assert.Null(failed);
            Assert.Equal(old, File.ReadAllBytes(aside));
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(campaign, savedAt: At.AddHours(1))), slot3.Read());
            Assert.False(slot3.HasBackup);   // moved into place: nothing was rotated
            Assert.Equal($"SAVED TO SLOT 3 - CAMPAIGN.JSON COULD NOT BE READ (VERSION: {SaveCodec.CurrentVersion + 1} IS NEWER THAN THIS BUILD READS ({SaveCodec.CurrentVersion})) - SET ASIDE AS {aside} - {CampaignBoot.TryAgain}",
                notice);
            Assert.Contains(log, line => line.Contains($"Wrote {slot3.SavePath}", StringComparison.Ordinal));

            // Drawn in the menu, wrapped as a notice is — the path with forward
            // slashes, which the font has — and green.
            var answer = Laid(PauseMenu.Options(hasTitle: true), canSave: true, notice).Where(row => row.Scale == 1.5f && row.Color == DungeonHud.Green).ToList();
            Assert.Equal(NoticeText.Wrap(notice), answer.Select(row => row.Text));
            Assert.DoesNotContain(answer, row => row.Text.Contains('\\', StringComparison.Ordinal));

            // The slot loads its new campaign; the old autosave was never touched;
            // and a second save by hand into the slot rotates as every save does.
            var load = CampaignBoot.Load(slots.Slot(3), GameContent.Current);
            Assert.True(load.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 3"], load.Notices);
            slot3.ClearResuming();
            Assert.False(slots.Store(SaveSlots.OldAutosave).HasSave);
            Assert.Null(CampaignBoot.Save(slots.Slot(3), Cleared(), savedAt: At.AddHours(2), byHand: true));
            Assert.Equal("SAVED TO SLOT 3", CampaignBoot.SaveAnswer(slots.Slot(3), failed: null, []));
            Assert.True(slot3.HasBackup);
            Assert.Equal(old, File.ReadAllBytes(aside));
        });
    }

    [Fact]
    public void AManualSaveIsTakenOnlyAtASafePoint()
    {
        // R2(d): "never while enemies are moving (the same safe points autosave
        // uses)". SAVE GAME is only asked for (SaveNow) and written at the frame's end
        // by the one door every save of the campaign in play goes through
        // (WriteRequestedSave), which asks the safe point there. Read off the IL:
        // SaveNow stores the hand request alone — never the automatic one, which a
        // safe point would wait for — and the hand request is stored true nowhere
        // else; the door takes it and clears it before it asks the safe point, and
        // off one answers it and returns, so a save by hand off a safe point is
        // answered SAVE ON YOUR OWN TURN and written neither then nor later. The
        // automatic request is cleared only past the safe point, so one that finds
        // the campaign off it is kept for the next frame that stands at one.
        var game = typeof(DistanceRpgGame);
        var hand = game.GetField("_handSaveRequested", Declared)!;
        var automatic = game.GetField("_saveRequested", Declared)!;
        var saveNow = Walk(game.GetMethod(nameof(DistanceRpgGame.SaveNow))!);
        Assert.Equal(1, saveNow.Count(i => i.Op == OpCodes.Stfld && Names(i.Field, hand)));
        Assert.DoesNotContain(saveNow, i => i.Op == OpCodes.Stfld && Names(i.Field, automatic));
        static bool SetsTrue(List<(OpCode Op, MethodBase? Called, FieldInfo? Field)> walked, FieldInfo flag)
            => walked.Select((instruction, at) => (instruction, at))
                .Any(i => i.instruction.Op == OpCodes.Stfld && Names(i.instruction.Field, flag) && walked[i.at - 1].Op == OpCodes.Ldc_I4_1);
        var setsHand = MethodsOf(game).Where(method => SetsTrue(Walk(method), hand)).Select(method => method.Name);
        Assert.Equal(["SaveNow"], setsHand);

        var writeMethod = game.GetMethod("WriteRequestedSave", Declared)!;
        var write = Walk(writeMethod);
        var save = game.GetMethod("Save", Declared)!;
        var answer = game.GetMethod("AnswerHandSave", Declared)!;
        int safe = write.FindIndex(i => Same(i.Called, typeof(SaveGame).GetMethod(nameof(SaveGame.IsSafePoint))!));
        int written = write.FindIndex(i => Same(i.Called, save));
        int handTaken = write.FindIndex(i => i.Op == OpCodes.Ldfld && Names(i.Field, hand));
        int handCleared = write.FindLastIndex(i => i.Op == OpCodes.Stfld && Names(i.Field, hand));
        Assert.True(safe >= 0 && safe < written, "WriteRequestedSave writes before it asks the safe point");
        Assert.True(handTaken < handCleared && handCleared < safe, "WriteRequestedSave does not take and clear the save by hand before it asks the safe point");
        Assert.Equal(OpCodes.Ldc_I4_0, write[handCleared - 1].Op);
        int autoCleared = write.FindLastIndex(i => i.Op == OpCodes.Stfld && Names(i.Field, automatic));
        Assert.True(safe < autoCleared && autoCleared < written, "WriteRequestedSave drops an automatic request off a safe point");
        Assert.Equal(OpCodes.Ldc_I4_0, write[autoCleared - 1].Op);
        var answered = write.Select((instruction, at) => (instruction, at)).Where(i => Same(i.instruction.Called, answer)).Select(i => i.at).ToList();
        Assert.Equal(2, answered.Count);
        Assert.True(safe < answered[0] && answered[0] < written && written < answered[1],
            "WriteRequestedSave does not answer a save by hand off a safe point before it could write, and one that was written after");

        // What the door says, and to whom (the T4 review): off a safe point, the
        // menu's own SAVE ON YOUR OWN TURN, as it stands; and each answer, and the
        // byHand that Save is handed, is the hand request's own — the local the door
        // took it into — so the menu is answered, and an unchanged save restamped,
        // for a save by hand alone.
        var operands = Operands(writeMethod);
        int? Loaded(int at) => Local(write[at].Op, operands[at].Operand, load: true);
        int? Stored(int at) => Local(write[at].Op, operands[at].Operand, load: false);
        Assert.Equal(OpCodes.Ldstr, write[answered[0] - 1].Op);
        Assert.Equal(PauseMenu.SaveOnYourTurn, writeMethod.Module.ResolveString(operands[answered[0] - 1].Operand));
        Assert.True(Stored(handTaken + 1) is not null, "WriteRequestedSave does not take the save by hand into a local of its own");
        int byHand = Stored(handTaken + 1)!.Value;
        Assert.True(Loaded(written - 1) == byHand, "WriteRequestedSave does not hand Save the save by hand's own request as byHand");
        bool OnTheHandRequest(int branch)   // the value a branch tests, followed back through any temporaries it passed through
        {
            int at = branch - 1;
            int? local = Loaded(at);
            while (local is { } temporary && temporary != byHand && at >= 2 && Stored(at - 1) == temporary)
                local = Loaded(at -= 2);
            return local == byHand;
        }
        foreach (int call in answered)
            Assert.True(Enumerable.Range(0, call).Any(branch => (write[branch].Op == OpCodes.Brfalse_S || write[branch].Op == OpCodes.Brfalse)
                    && operands[branch].Operand > operands[call].Start && OnTheHandRequest(branch)),
                "WriteRequestedSave answers a save that was not asked for by hand");

        // A campaign between runs carries no view: the door reads the campaign's
        // visit, past the safe point, before it asks the playing scene for its view.
        // And a save by hand never plays the enemies' turn out: nothing the door
        // reaches settles the campaign to a safe point — that is the quit's alone.
        var visit = typeof(CampaignState).GetProperty(nameof(CampaignState.Visit))!.GetMethod!;
        int scene = write.FindIndex(i => Same(i.Called, game.GetMethod("PlayingScene", Declared)!));
        int visitRead = write.FindLastIndex(scene, i => Same(i.Called, visit));
        Assert.True(safe < visitRead && visitRead < scene && scene < written, "WriteRequestedSave asks the playing scene for a view before it has read whether a run is in progress");
        var settle = typeof(SaveGame).GetMethod(nameof(SaveGame.SettleToSafePoint))!;
        Assert.DoesNotContain(Reachable(writeMethod), method => Same(method, settle));

        InTempFolder(folder =>
        {
            // CampaignBoot.Save asked straight, off a safe point — the lance
            // mid-stride — which the door above never does: a save by hand writes
            // nothing, not even the slot's folder, settles nothing, and says it could
            // not save, in its catch-all words. SAVE ON YOUR OWN TURN is the door's,
            // pinned above by the IL alone.
            var slots = new SaveSlots(folder, 3);
            var walking = ReloadDeterminismTests.MidWalk();
            Assert.False(SaveGame.IsSafePoint(walking.Campaign));
            Assert.StartsWith("SLOT 1 COULD NOT BE SAVED (", CampaignBoot.Save(slots.Slot(1), walking.Campaign, new ViewDto("A"), savedAt: At, byHand: true), StringComparison.Ordinal);
            Assert.False(Directory.Exists(slots.Store(1).Folder));
            Assert.Equal(TurnPhase.EnemyMoving, walking.Turns.Phase);   // and nothing was settled

            // In the party's own turn mid-run: saved, the view with it, and a load of the
            // slot brings back the same text.
            var script = VisitScript.Enter(NewCampaign(), "spear_hall", CampaignRuns.FiveDeep, visitsOwnDie: true);
            Assert.True(SaveGame.IsSafePoint(script.Campaign));
            var view = new ViewDto("B", MarchingThisTurn: true);
            Assert.Null(CampaignBoot.Save(slots.Slot(1), script.Campaign, view, savedAt: At, byHand: true));
            string text = slots.Store(1).Read()!;
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(script.Campaign, view, At)), text);
            var load = CampaignBoot.Load(slots.Slot(1), GameContent.Current);
            Assert.True(load.Loaded);
            Assert.Equal(view, load.View);
            Assert.Equal("CAMPAIGN RESUMED FROM SLOT 1", load.Notices[0]);
            Assert.Equal(text, SaveCodec.Write(SaveGame.Capture(load.Campaign!, load.View, At)));
            slots.Store(1).ClearResuming();

            // Between runs is always a safe point, and a campaign saved between runs
            // with no view comes back with none. That the door hands it none, whatever
            // the playing scene holds, is pinned above by the IL alone: CampaignBoot.Save
            // keeps whatever view it is handed.
            Assert.Null(CampaignBoot.Save(slots.Slot(2), Retreated(), view: null, savedAt: At, byHand: true));
            Assert.Null(SaveCodec.Read(slots.Store(2).Read()!).View);

            // A slot whose store refuses its writes this session — a file there would
            // neither read nor move — answers with why, and writes nothing (S6's review:
            // the refusal is the session's).
            var refusing = slots.Store(3);
            refusing.RefuseWrites("campaign.json could not be read (x) and could not be set aside (another program has it open)");
            Assert.Equal("SLOT 3 COULD NOT BE SAVED (CAMPAIGN.JSON COULD NOT BE READ (X) AND COULD NOT BE SET ASIDE (ANOTHER PROGRAM HAS IT OPEN)) - WHAT IT HELD IS KEPT - SEE THE LOG",
                CampaignBoot.Save(slots.Slot(3), Retreated(), savedAt: At, byHand: true));
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
        var host = new RecordingHost();
        var scene = new DungeonScene(camera: null!, game: null!, Entered(), host, [data]);
        var notices = scene.NoticeRows.ToList();
        Assert.Single(notices);
        scene.HandleEscape();
        scene.AnyKey(Keys.D2);
        Assert.Equal((1, true), (host.HandSaves, scene.PauseMenuOpen));
        scene.SlotSaved(CampaignBoot.SavedTo(Named(2)));
        Assert.Equal("SAVED TO SLOT 2", scene.MenuAnswer);
        Assert.Equal(notices, scene.NoticeRows);   // no notice added
        var drawn = Laid(PauseMenu.Options(hasTitle: true), scene.CanSaveNow, scene.MenuAnswer);
        Assert.Contains(drawn, row => row.Text == "SAVED TO SLOT 2" && row.Color == DungeonHud.Green);
        Assert.True(drawn.Single(row => row.Text == "SAVED TO SLOT 2").Y > drawn.Single(row => row.Text == DungeonHud.EscToClose).Bottom);
        scene.SaveFailed(CampaignBoot.NotSaved(Named(SaveSlots.OldAutosave), "the disk is full"));   // a failed autosave is a notice, over the floor: the answer stays
        Assert.Equal("SAVED TO SLOT 2", scene.MenuAnswer);
        scene.AnyKey(Keys.X);
        Assert.Null(scene.MenuAnswer);   // the next key

        scene.SlotSaved(CampaignBoot.SavedTo(Named(2)));
        scene.HandleEscape();
        Assert.Null(scene.MenuAnswer);   // the menu closing
        scene.HandleEscape();
        Assert.Null(scene.MenuAnswer);   // and opened again, it has nothing to say
        scene.HandleEscape();
        scene.SlotSaved(CampaignBoot.SavedTo(Named(2)));
        Assert.Null(scene.MenuAnswer);   // with the menu closed by the time the save is written, only the log keeps it

        // A save that failed, and 2 off a safe point, answer in the warning's orange.
        string failed = CampaignBoot.NotSaved(Named(2), "another program has it open");
        Assert.Equal("SLOT 2 COULD NOT BE SAVED (ANOTHER PROGRAM HAS IT OPEN) - WHAT IT HELD IS KEPT - SEE THE LOG", failed);
        var failedRows = Laid(PauseMenu.Options(hasTitle: true), canSave: true, failed).Where(row => row.Scale == 1.5f && row.Color == DungeonHud.Orange).ToList();
        Assert.Equal(NoticeText.Wrap(failed), failedRows.Select(row => row.Text));
        Assert.Equal(DungeonHud.Orange, DungeonHud.AnswerColor(PauseMenu.SaveOnYourTurn));
        Assert.Equal(DungeonHud.Green, DungeonHud.AnswerColor(CampaignBoot.SavedTo(Named(9))));
        Assert.Equal(DungeonHud.Green, DungeonHud.AnswerColor(CampaignBoot.SavedTo(Named(null))));

        // Off a safe point the menu dims SAVE GAME, and 2 says why inside it.
        var ending = Entered();
        ending.Visit!.OpenTurns(ending.Party, CampaignRuns.Ten).EndTurn();
        var offTurn = new DungeonScene(camera: null!, game: null!, ending, new RecordingHost());
        offTurn.HandleEscape();
        offTurn.AnyKey(Keys.D2);
        var dimmed = Laid(PauseMenu.Options(hasTitle: true), offTurn.CanSaveNow, offTurn.MenuAnswer);
        Assert.Equal(DungeonHud.DarkGrey, dimmed.Single(row => row.Text == PauseMenu.SaveLine).Color);
        Assert.Contains(dimmed, row => row.Text == "SAVE ON YOUR OWN TURN" && row.Color == DungeonHud.Orange);
        Assert.Empty(offTurn.NoticeRows);
    }

    [Fact]
    public void TheMenuLaysOutClear_AndNamesItsSlot()
    {
        // The menu's two lines sit under its last option, however many options there
        // are — they no longer sit at a fixed height a third option would land on —
        // and the answer under them; nothing overlaps, and the block keeps clear of
        // the line along the bottom (the dungeon's help line, the hub's keys).
        Assert.True(DungeonHud.MenuBottomMargin >= BetweenRunsHud.KeysLineFromBottom);
        const string failure = "SLOT 2 COULD NOT BE SAVED (ANOTHER PROGRAM HAS IT OPEN) - WHAT IT HELD IS KEPT - SEE THE LOG";

        // The longest answer a save by hand comes to (the T4 review): slot 9's
        // failure after the write guard set a file aside first — one at most: once
        // the save is set aside, the write moves into place and the backup is left
        // unread — its full path under the player's own folder.
        string aside = CampaignBoot.SetAsideNotice(SaveStore.FileName,
            $"could not be read (version: {SaveCodec.CurrentVersion + 1} is newer than this build reads ({SaveCodec.CurrentVersion}))",
            @"C:\Users\a-player-with-a-long-name\AppData\Local\DistanceRPG\saves\slot-9\campaign.unreadable-10.json");
        string answer = CampaignBoot.SaveAnswer(Named(9), CampaignBoot.NotSaved(Named(9), "another program has it open"), [aside]);
        Assert.StartsWith("SLOT 9 COULD NOT BE SAVED (ANOTHER PROGRAM HAS IT OPEN) - WHAT IT HELD IS KEPT - SEE THE LOG - CAMPAIGN.JSON COULD NOT BE READ", answer, StringComparison.Ordinal);
        foreach (var options in new IReadOnlyList<string>[] { [PauseMenu.CloseLine, PauseMenu.SaveLine], PauseMenu.Options(hasTitle: true), PauseMenu.Options(hasTitle: false) })
        {
            foreach (string? said in new[] { null, failure, answer })
            {
                var rows = Laid(options, canSave: true, said);
                Clear(rows);
                var optionRows = rows.Where(row => options.Contains(row.Text)).ToList();
                Assert.Equal(options, optionRows.Select(row => row.Text));
                var saves = rows.Single(row => row.Text == InSlot2);
                var esc = rows.Single(row => row.Text == DungeonHud.EscToClose);
                Assert.True(saves.Y > optionRows[^1].Bottom, "the menu's line sits over its last option");
                Assert.True(esc.Y > saves.Bottom);
                Assert.Equal(DungeonHud.PausedTitle, rows[0].Text);
                if (said is not null)
                    Assert.True(rows.Where(row => row.Color == DungeonHud.Orange).All(row => row.Y > esc.Bottom), "the answer sits over the menu's lines");
            }
        }

        // The line names the slot the campaign saves into (R5), so the player always
        // knows which slot is theirs: every slot's name, the old autosave's and the
        // scratch slot's, in the font and within 1280 at the line's scale, the
        // longest answer under it too. With none to name, it says only that the run
        // saves itself.
        var names = Enumerable.Range(SaveSlots.OldAutosave, SaveSlots.Most + 1).Select(n => Named(n).Name).Append(Named(null).Name).ToList();
        foreach (string name in names)
        {
            string line = DungeonHud.PauseSavesLine(name);
            Assert.Equal($"PROGRESS SAVES TO {name} AT EVERY TURN", line);
            Clear(Laid(PauseMenu.Options(hasTitle: name != SaveSlot.ScratchName), canSave: true, answer, line));
        }
        Assert.Equal("PROGRESS SAVES AT EVERY TURN", DungeonHud.PauseSavesLine(""));
        foreach (string row in new[] { PauseMenu.SaveOnYourTurn, DungeonHud.PauseSavesLine(SaveSlot.ScratchName) })
            Assert.Matches(Font, row);
    }

    [Fact]
    public void QuittingToTheTitleSavesTheCampaignsSlotFirst()
    {
        // R2(e): "ESC/QUIT from play returns to the title (saving the autosave
        // first)" — the campaign's own slot, since the user's ruling on the save
        // slots (R5). QUIT TO TITLE writes it through the window close's own door —
        // DistanceRpgGame.SaveOnQuit, which hands CampaignBoot.SaveOnQuit the
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

        // The quit's door saves the campaign's own slot: it reads the slot the
        // campaign is bound to, and never the save folder's slots.
        var onQuit = Walk(game.GetMethod("SaveOnQuit", Declared)!);
        Assert.Contains(onQuit, i => i.Op == OpCodes.Ldfld && Names(i.Field, game.GetField("_slot", Declared)!));
        Assert.DoesNotContain(onQuit, i => i.Op == OpCodes.Ldfld && Names(i.Field, game.GetField("_slots", Declared)!));

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
            Assert.Null(CampaignBoot.Save(AsOldAutosave(store), campaign, savedAt: At));
            CampaignRuns.ShallowRetreat(campaign, "tutorial");   // played on since: the quit has something to write

            string failed = CampaignBoot.NotSaved(AsOldAutosave(store), "the disk is full");   // what an autosave since then said
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
            Assert.True(CampaignBoot.SaveOnQuit(AsOldAutosave(store), campaign, view: null, step: null, savedAt: At.AddMinutes(1), saved: TakeBack));
            Assert.Equal(0, told);
            Assert.NotNull(store.ReadUnsaved());
            Directory.Delete(store.TempPath);
            Assert.True(CampaignBoot.TakeUnsaved(store, report: null));

            // Nor does a quit during the enemies' turn that never comes round to the
            // party's, or that has no scene to play it out with: nothing is written.
            var stuck = ReloadDeterminismTests.MidWalk();
            Assert.False(CampaignBoot.SaveOnQuit(AsOldAutosave(store), stuck.Campaign, () => new ViewDto("A"), () => { }, saved: TakeBack));
            Assert.False(CampaignBoot.SaveOnQuit(AsOldAutosave(store), stuck.Campaign, () => new ViewDto("A"), step: null, saved: TakeBack));
            Assert.Equal(0, told);
            Assert.True(CampaignBoot.TakeUnsaved(store, report: null));

            // A quit whose save goes through tells, once: the title is handed nothing of
            // the failure, and what else leads it stays.
            Assert.True(CampaignBoot.SaveOnQuit(AsOldAutosave(store), campaign, view: null, step: null, savedAt: At.AddMinutes(2), saved: TakeBack));
            Assert.Equal(1, told);
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(campaign, savedAt: At.AddMinutes(2))), store.Read());
            Assert.Null(store.ReadUnsaved());
            Assert.Equal(["data"], notices.Take());

            // So does one whose save the disk holds already: nothing needed writing, and
            // the disk holds the campaign as it was left.
            Assert.True(CampaignBoot.SaveOnQuit(AsOldAutosave(store), campaign, view: null, step: null, savedAt: At.AddMinutes(3), saved: TakeBack));
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
        // then on. 2 asks no save; 1 leaves the menu up, so nothing behind it acts
        // (every other key and click does nothing behind a menu); 3 asks nothing
        // twice; and ESC is swallowed. No key reaches the campaign the host let go.
        var host = new RecordingHost();
        var scene = new DungeonScene(camera: null!, game: null!, Entered(), host);
        scene.HandleEscape();
        scene.AnyKey(Keys.D3);
        Assert.Equal(1, host.QuitsToTitle);
        foreach (var key in new[] { Keys.D2, Keys.D1, Keys.D3, Keys.D4, Keys.Enter, Keys.Escape })
            scene.AnyKey(key);
        Assert.True(scene.HandleEscape());
        Assert.True(scene.PauseMenuOpen);
        Assert.Equal((0, 1), (host.HandSaves, host.QuitsToTitle));

        // Play not left, the same keys act: 1 closes the menu, and ESC opens it again.
        var staying = new DungeonScene(camera: null!, game: null!, Entered(), new RecordingHost());
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
    public void ADevOverrideSessionSavesOnlyItsScratchSlot()
    {
        // A dev-override session plays a scratch campaign apart from the player's
        // saves, with no title and none of the save folder's slots: the slot its
        // campaign is bound to is its scratch slot, so SAVE GAME saves there — "any
        // saving happens on that save slot" (the user's ruling of 2026-09-25 on the
        // save slots, R5) — and never anywhere else, so no path puts a scratch
        // campaign in the player's slots.
        Assert.Equal((PauseAction.Save, (string?)null), PauseMenu.Press(2, canSave: true, hasTitle: false));

        // Read off the IL: SaveNow only asks — it touches no CampaignBoot or SaveStore
        // member — and the write is the frame's end's, the one door, which saves
        // through the slot the campaign is bound to (_slot, a dev override's scratch
        // slot: SaveSlotsTests.ADevOverrideSessionNeverTouchesTheSlots) and never
        // reads the save folder's slots or asks them for one.
        var game = typeof(DistanceRpgGame);
        var saveNow = game.GetMethod(nameof(DistanceRpgGame.SaveNow))!;
        Assert.DoesNotContain(Reachable(saveNow), method => method.DeclaringType == typeof(CampaignBoot) || method.DeclaringType == typeof(SaveStore) || method.DeclaringType == typeof(SaveSlots));
        var writeMethod = game.GetMethod("WriteRequestedSave", Declared)!;
        var write = Walk(writeMethod);
        Assert.Contains(write, i => i.Op == OpCodes.Ldfld && Names(i.Field, game.GetField("_slot", Declared)!));
        Assert.DoesNotContain(write, i => i.Op == OpCodes.Ldfld && Names(i.Field, game.GetField("_slots", Declared)!));
        Assert.DoesNotContain(write, i => i.Called?.DeclaringType == typeof(SaveSlots));
        Assert.Equal(["DistanceRpgGame.OnUpdateFrame"], Callers(writeMethod));

        InTempFolder(folder =>
        {
            // On a folder: the player's old autosave and a slot hold saves; the dev
            // override's campaign, saved by hand into its scratch slot — twice, the
            // second time the campaign unchanged, which restamps it — changes
            // override/ alone.
            var slots = new SaveSlots(folder, 3);
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.OldAutosave), Retreated(), savedAt: At));
            Assert.Null(CampaignBoot.Save(slots.Slot(1), Cleared(), savedAt: At));
            var before = Snapshot(folder);

            var request = new BootRequest("spear_hall");
            var scratch = SaveSlot.Scratch(CampaignBoot.StoreFor(folder, request));
            var boot = CampaignBoot.BootOverride(scratch, GameContent.Current, request);
            Assert.Null(CampaignBoot.Save(scratch, boot.Campaign, savedAt: At.AddHours(1), byHand: true));
            Assert.Null(CampaignBoot.Save(scratch, boot.Campaign, savedAt: At.AddHours(2), byHand: true));
            Assert.Equal("SAVED TO THE SCRATCH SLOT", CampaignBoot.SaveAnswer(scratch, failed: null, []));
            Assert.Equal(SaveGame.TimeText(At.AddHours(2)), SaveCodec.Read(scratch.Store.Read()!).SavedAt);

            var after = Snapshot(folder);
            string overrideFolder = SaveStore.OverrideSlot + Path.DirectorySeparatorChar;
            Assert.Equal(before.Keys.Order(), after.Keys.Where(path => !path.StartsWith(overrideFolder, StringComparison.Ordinal)).Order());
            Assert.All(before, file => Assert.Equal(file.Value, after[file.Key]));
            Assert.Contains(Path.Combine(SaveStore.OverrideSlot, SaveStore.FileName), after.Keys);
        });

        // Between runs, walked: 2 asks the host to save, and the menu says where.
        var host = new RecordingHost(hasTitle: false, savesInto: SaveSlot.ScratchName);
        var betweenRuns = new BetweenRunsScene(game: null!, CampaignRuns.Campaign(), host, shownWhole: 0);
        betweenRuns.HandleEscape();
        betweenRuns.OnKey(Keys.D2);
        Assert.Equal((1, true, (string?)null), (host.HandSaves, betweenRuns.MenuOpen, betweenRuns.MenuAnswer));
        Assert.Equal("PROGRESS SAVES TO THE SCRATCH SLOT AT EVERY TURN", DungeonHud.PauseSavesLine(betweenRuns.SavesInto));
    }
}

/// <summary>
/// The host of a screen built with no window and walked key by key: what it is
/// asked for is recorded, it names the slot the campaign saves into as it was
/// handed it, and it saves, quits and descends nothing itself. Its two
/// <c>RequestSave</c>s are its two hosts' apart (Phase 6a.2): the hub's autosave
/// is counted, and a dungeon's, which a screen walked with no window never asks
/// for, is refused.
/// </summary>
internal sealed class RecordingHost : IRunHost, IBetweenRunsHost
{
    public RecordingHost(bool hasTitle = true, string savesInto = "SLOT 2")
    {
        HasTitle = hasTitle;
        SavesInto = savesInto;
    }

    public bool HasTitle { get; }

    /// <summary>The slot the campaign in play saves into, by its name, as the host was handed it.</summary>
    public string SavesInto { get; }

    /// <summary>How many saves by hand were asked for.</summary>
    public int HandSaves { get; private set; }

    /// <summary>How many times play was left for the title.</summary>
    public int QuitsToTitle { get; private set; }

    /// <summary>Every dungeon descended into, in order.</summary>
    public List<string> Descended { get; } = [];

    /// <summary>How many saves the hub asked for after a change it made (<see cref="IBetweenRunsHost.RequestSave"/>, PHASE 6a DECISION 10).</summary>
    public int HubSavesAsked { get; private set; }

    public void SaveNow() => HandSaves++;

    public void QuitToTitle() => QuitsToTitle++;

    public void Descend(string dungeonId) => Descended.Add(dungeonId);

    public void RunOver() => throw new InvalidOperationException("A screen walked with no window is never played to a run's end.");

    void IRunHost.RequestSave() => throw new InvalidOperationException("A screen walked with no window never opens a floor, so it never asks for an autosave.");

    void IBetweenRunsHost.RequestSave() => HubSavesAsked++;
}
