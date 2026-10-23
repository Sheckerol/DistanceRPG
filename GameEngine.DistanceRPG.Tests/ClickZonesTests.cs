using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// After play (2026-10): "make all the menus clickable". A key's words on a keys
/// line or a menu row click as the key (<see cref="ClickZones.KeySpans"/>), and a
/// list's row as the screen says — the title's options and slots, the hub's
/// dungeons, weapons, potions and offers. Where the zones lie is the drawing's and
/// untested; what each line's keys are, what a zone does, and what each click
/// does to its list are pure, and walked here.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class ClickZonesTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    private static Keys[] KeysOf(string line, bool leadOnly = false) => ClickZones.KeySpans(line, leadOnly).Select(span => span.Key).ToArray();

    [Fact]
    public void EveryKeysLine_ClicksAsTheKeysItNames()
    {
        var member = TestPools.Char("A");
        var help = KeysOf(DungeonHud.HelpLine(member));
        Assert.Equal([Keys.Space, Keys.E, Keys.I, Keys.C, Keys.Tab, Keys.Escape], help.Where(key => key != Keys.O));   // WASD MOVE, CLICK ENEMY, SCROLL ZOOM: no key's

        Assert.Equal([Keys.W, Keys.S, Keys.Enter, Keys.Tab, Keys.Escape], KeysOf(BetweenRunsHud.KeysLine));
        Assert.Equal([Keys.W, Keys.S, Keys.Enter, Keys.D, Keys.T, Keys.Tab, Keys.Escape], KeysOf(BetweenRunsHud.WeaponsKeysLine));
        Assert.Equal([Keys.W, Keys.S, Keys.A, Keys.D, Keys.H, Keys.M, Keys.T, Keys.Tab, Keys.Escape], KeysOf(BetweenRunsHud.AlchemistKeysLine));
        Assert.Equal([Keys.Escape], KeysOf(DepositPanel.ServicesKeys));   // "1, 2 OR 3" is the services' own rows'
        Assert.Equal([Keys.Enter, Keys.Escape], KeysOf(DepositPanel.ConfirmKeys));
        Assert.Equal([Keys.W, Keys.S, Keys.Enter, Keys.Escape], KeysOf(TitleText.KeysLine));
        Assert.Equal([Keys.Enter], KeysOf(DungeonHud.ContinueHint));
        Assert.Equal([Keys.I], KeysOf(DungeonHud.EquipHint(6)));
        Assert.Equal([Keys.X], KeysOf(DungeonHud.LeaveHint(6)));
        Assert.Equal([Keys.X], KeysOf(DungeonHud.LeaveKeyHint));
        Assert.Equal([Keys.C], KeysOf(DungeonHud.CharacterSheetLines(member)[^1].Text));
        Assert.Equal([Keys.D1, Keys.D2, Keys.D3], PauseMenu.Options(hasTitle: true).SelectMany(row => KeysOf(row, leadOnly: true)));
    }

    [Fact]
    public void AQuestionsKeysAreReadAfterIt_AndAPairSplitsDownTheMiddle()
    {
        string prompt = DungeonHud.LeavePrompt();   // LEAVE THE DUNGEON? E OR ENTER: YES - ESC: NO
        var spans = ClickZones.KeySpans(prompt);
        Assert.Equal(Keys.E, spans[0].Key);
        Assert.Equal("E OR ENTER: YES", prompt.Substring(spans[0].Start, spans[0].Length));
        Assert.Equal(Keys.Escape, spans[1].Key);

        var pair = ClickZones.KeySpans("W/S OR ARROWS CHOOSE");
        Assert.Equal([(0, 10, Keys.W), (10, 10, Keys.S)], pair.Select(span => (span.Start, span.Length, span.Key)));

        Assert.Equal([Keys.Tab], KeysOf("TAB/1-4 SWITCH"));   // a key and no key: the key's alone
    }

    [Fact]
    public void AMenuRow_IsItsLeadKeyAlone_PastTheChosenMarker()
    {
        // A word of what an option says is never a key: "A CHOSEN ENCHANTMENT" is no A.
        var service = ClickZones.KeySpans("1  ENCHANT - A CHOSEN ENCHANTMENT", leadOnly: true);
        Assert.Equal((0, 33, Keys.D1), (service.Single().Start, service.Single().Length, service.Single().Key));

        var chosen = Assert.Single(ClickZones.KeySpans("> 2  REFINE - NOTHING ATTACHED <", leadOnly: true));
        Assert.Equal((2, Keys.D2), (chosen.Start, chosen.Key));
        Assert.Empty(ClickZones.KeySpans("PAUSED", leadOnly: true));
    }

    [Fact]
    public void AClick_TakesTheLastZoneLaidOverIt_AndAKeysZonePressesTheKey()
    {
        using var zones = new ClickZones();
        var clicked = new List<string>();
        zones.Add(0, 0, 100, 100, () => clicked.Add("under"));
        zones.Add(40, 40, 20, 20, () => clicked.Add("over"));

        Assert.True(zones.Click(new Vector2(50, 50)));
        Assert.True(zones.Click(new Vector2(10, 10)));
        Assert.False(zones.Click(new Vector2(150, 10)));
        Assert.Equal(["over", "under"], clicked);

        // A key's zone presses it through the engine's input, and lets it go.
        var heard = new List<string>();
        Action<KeyPressedEvent> pressed = e => heard.Add($"down {e.Key}");
        Action<KeyReleasedEvent> released = e => heard.Add($"up {e.Key}");
        InputEventManager.Instance.SubscribeToKeyPressed(pressed, Keys.F12);
        InputEventManager.Instance.SubscribeToKeyReleased(released, Keys.F12);
        try
        {
            ClickZones.Press(Keys.F12);
        }
        finally
        {
            InputEventManager.Instance.UnsubscribeFromKeyPressed(pressed);
            InputEventManager.Instance.UnsubscribeFromKeyReleased(released);
        }
        Assert.Equal(["down F12", "up F12"], heard);
    }

    private static SaveListing Listing(params SlotEntry[] held)
    {
        var all = Enumerable.Range(1, 3)
            .Select(n => held.FirstOrDefault(slot => slot.Number == n) ?? new SlotEntry(n, SlotState.Empty, null, false, false, false, false))
            .ToList();
        return new SaveListing(all, []);
    }

    private static SlotEntry Held(int number) => new(number, SlotState.Readable, new SlotHeader(DateTimeOffset.UnixEpoch, 3, "spear_hall", 4, 3), false, false, false, false);

    [Fact]
    public void ATitleClick_TakesAnOption_OrASlot_AsItsKeyWould()
    {
        var bare = new TitleMenu(Listing());
        Assert.Equal(default, bare.Click((int)TitleOption.Continue));   // off: nothing, the choice where it was
        Assert.Equal((int)TitleOption.NewCampaign, bare.Cursor);
        Assert.Equal(new TitleStep(TitleAction.Quit), bare.Click((int)TitleOption.Quit));

        var menu = new TitleMenu(Listing(Held(2)));
        Assert.Equal(new TitleStep(TitleAction.Load, 2), menu.Click((int)TitleOption.Continue));

        // LOAD GAME: an empty slot says so and loads nothing; a held one loads.
        Assert.Equal(default, menu.Click((int)TitleOption.LoadGame));
        Assert.Equal(SlotMenuMode.Load, menu.List!.Mode);
        Assert.Equal(default, menu.Click(0));
        Assert.Equal(SlotText.IsEmpty(1), menu.List.Said);
        Assert.Equal(new TitleStep(TitleAction.Load, 2), menu.Click(1));

        // NEW CAMPAIGN: an empty slot begins; a held one asks, and a click is not ENTER - it says no.
        var fresh = new TitleMenu(Listing(Held(2)));
        fresh.Click((int)TitleOption.NewCampaign);
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 1), fresh.Click(0));
        fresh = new TitleMenu(Listing(Held(2)));
        fresh.Click((int)TitleOption.NewCampaign);
        Assert.Equal(default, fresh.Click(1));
        Assert.True(fresh.Asking);
        Assert.Equal(default, fresh.Click(1));
        Assert.False(fresh.Asking);
        fresh.Click(1);
        Assert.Equal(new TitleStep(TitleAction.NewCampaign, 2), fresh.Press(Keys.Enter));   // ENTER - or its words clicked - says yes
    }

    [Fact]
    public void AHubRowClick_ChoosesIt_AndAClickOnTheChoiceIsEnter()
    {
        var host = new RecordingHost();
        var scene = new BetweenRunsScene(game: null!, CampaignState.Start(GameContent.Current), host, shownWhole: 0);

        // WEAPONS: a click moves the choice where W/S would stop, and on the choice takes it up.
        scene.OnKey(Keys.Tab);
        var rows = scene.Weapons.Rows();
        int other = Enumerable.Range(0, rows.Count).First(i => i != scene.Weapons.Chosen && rows[i].Choosable);
        scene.ClickWeapon(other);
        Assert.Equal(other, scene.Weapons.Chosen);
        int stop = Enumerable.Range(0, rows.Count).FirstOrDefault(i => !rows[i].Choosable, -1);
        if (stop >= 0)
        {
            scene.ClickWeapon(stop);
            Assert.Equal(other, scene.Weapons.Chosen);   // a heading is no choice
        }
        Assert.Null(scene.Weapons.Carried);
        scene.ClickWeapon(other);
        Assert.NotNull(scene.Weapons.Carried);
        Assert.True(scene.HandleEscape());   // put back

        // Behind the menu, a row's click does nothing.
        scene.HandleEscape();
        Assert.True(scene.MenuOpen);
        scene.ClickWeapon(other);
        Assert.Null(scene.Weapons.Carried);
        scene.HandleEscape();

        // DUNGEONS: a click on the dungeon chosen descends into it.
        scene.OnKey(Keys.Tab);
        scene.OnKey(Keys.Tab);
        Assert.Equal(BetweenRunsScene.HubPage.Dungeons, scene.Page);
        scene.ClickDungeon(scene.Chosen);
        Assert.Equal([scene.Listings[scene.Chosen].Def.Id], host.Descended);
    }
}
