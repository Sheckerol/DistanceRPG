using GameEngine.Core;
using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What the between-runs screen reaches the game through: a dungeon chosen, and —
/// from the menu ESC opens (<see cref="ISaveHost"/>) — a save by hand into the
/// campaign's own slot and the way back to the title. The screen never holds the concrete game that hosts it
/// (§6.7: the campaign outlives every scene, and the scenes are views of it). It
/// is the dungeon list's half of what <see cref="IRunHost"/> is to a dungeon scene.
/// A new campaign is the title's to begin (<see cref="ITitleHost.BeginNewCampaign"/>),
/// never this screen's (the user's ruling of 2026-09-25 on the save model, R2(e)).
/// </summary>
public interface IBetweenRunsHost : ISaveHost
{
    /// <summary>
    /// The player chose <paramref name="dungeonId"/>, which the campaign lets the
    /// party enter now (<see cref="DungeonListing.Selectable"/>). Begin a run
    /// there. The screen calls this once, never while the engine is still fading
    /// between scenes.
    /// </summary>
    void Descend(string dungeonId);
}

/// <summary>
/// The screen between runs (§4.5, §6.7): the hub's dungeon list, and the last
/// run's summary above it. A campaign continued or loaded from the title opens on
/// it when it was saved between runs — with <c>CAMPAIGN RESUMED</c> under the
/// title — and so does a new one; and every run's end comes back to it. The
/// campaign's list decides what is offered (<see cref="CampaignState.Listings"/>).
/// Before the tutorial is beaten that is the tutorial alone, with the warning §4.3
/// asks for. After, it is every dungeon left, a locked one counting down, and with
/// every one locked, the one offered early. This screen only presents the list and
/// passes the choice on.
/// <para>
/// W/S or the arrows move the choice over the lines that can be entered — a
/// locked line is read, not chosen. ENTER descends into the choice, once, and
/// the host begins the run (<see cref="IBetweenRunsHost.Descend"/>). The choice
/// starts on the campaign's own default (<see cref="CampaignState.DefaultNext"/>):
/// the tutorial until it is beaten, then the first dungeon whose lock has lifted,
/// then the early offer. NEW CAMPAIGN is the title's (R2(e)), and no key here
/// begins one.
/// </para>
/// <para>
/// <strong>ESC opens the menu</strong> — the dungeon's pause menu, over the hub
/// (the user's ruling of 2026-09-25 on the save model, R2(d), R2(e);
/// <see cref="PauseMenu"/>): 1 closes it, 2 saves the campaign into its own slot,
/// asking nothing (the user's ruling of 2026-09-25 on the save slots, R5) —
/// between runs is always a safe point — and 3 quits to the title, the host
/// writing the campaign's slot first (<see cref="ISaveHost.QuitToTitle"/>), or, in
/// a dev-override session, which has no title, closes the game. <strong>While the
/// menu is up every key is its</strong> — 1, 2 and 3 act and every other key does
/// nothing, so ENTER never descends behind it (<see cref="Route"/>). ESC closes
/// the menu: always consumed here, so the game never quits from this screen but by
/// the menu or the window's close.
/// </para>
/// <para>
/// Like every gameplay scene it is single-use: the host registers a fresh one
/// each time the screen is shown. What it reads is the campaign's, which was
/// written when the last run ended, and nothing here writes to it.
/// </para>
/// <para>
/// A screen is handed what the game has to say under its title — on the first one
/// a campaign opens on, the save's word on it: that it resumed, or a new one
/// begins, and what was set aside on the way (and, when a dev override or
/// <c>--new-campaign</c> skipped the title, what the data files raised) — and,
/// after a save that could not be written, that. The save's word is shown whole —
/// a set-aside notice's full path and how to try the file again, the numbers
/// changed since the save — and anything else is capped after it
/// (<see cref="NoticeText.Rows"/>). The log keeps them all.
/// </para>
/// </summary>
public sealed class BetweenRunsScene : Scene
{
    /// <summary>Where one key press goes between runs (<see cref="Route"/>).</summary>
    internal enum KeyRoute
    {
        /// <summary>ESC: the game's to route, which asks <see cref="HandleEscape"/>.</summary>
        Game,

        /// <summary>The dungeon list's: W/S and the arrows move the choice, ENTER descends, and every other key does nothing.</summary>
        List,

        /// <summary>The menu's: 1, 2 and 3 act, and every other key does nothing — ENTER and the arrows included.</summary>
        Menu,
    }

    private readonly Game _game;
    private readonly CampaignState _campaign;
    private readonly IBetweenRunsHost _host;
    private readonly BetweenRunsHud _hud;

    private readonly IReadOnlyList<DungeonListing> _listings;
    private int _chosen;

    // The host has been told something that ends this screen: a descent, or the
    // quit to the title. After it every key is ignored while the screen fades out.
    private bool _leaving;

    // The menu is up (ESC opens it), and what it answers under its lines — what a
    // save by hand came to — until the next key or until the menu closes. Menus:
    // never saved.
    private bool _menu;
    private string? _answer;

    /// <param name="game">The game window: its size and its shaders, for the text, and its close, which a dev-override session's QUIT GAME is. A screen built to be read, never shown, is handed none.</param>
    /// <param name="campaign">The campaign between its runs: no visit is in progress.</param>
    /// <param name="host">Who is told which dungeon was chosen (<see cref="IBetweenRunsHost.Descend"/>), and asked for a save by hand and the way to the title (<see cref="ISaveHost"/>).</param>
    /// <param name="shownWhole">How many of <paramref name="notices"/>, from the first, are the save's word on the campaign, shown whole (<see cref="StartupNotices.Leading"/>).</param>
    /// <param name="notices">What the game has to say under the title — the save's word and the data files' notices — or none.</param>
    /// <exception cref="ArgumentException">A visit is in progress: its run has to end before the screen between runs can show.</exception>
    public BetweenRunsScene(Game game, CampaignState campaign, IBetweenRunsHost host, int shownWhole, IReadOnlyList<string>? notices = null) : base("BetweenRunsScene")
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(host);
        if (campaign.Visit is { } visit)
            throw new ArgumentException($"A visit to '{visit.Def.Id}' is in progress: the screen between runs shows only once its run has ended.", nameof(campaign));
        _game = game;
        _campaign = campaign;
        _host = host;
        _hud = new BetweenRunsHud(game);
        NoticeRows = NoticeText.Rows(notices ?? [], shownWhole);

        // The list as the campaign gives it now: nothing changes the campaign
        // between this screen being made and its being shown.
        _listings = campaign.Listings();
        _chosen = FirstChoice(_listings, DefaultNextOrNull(campaign));
    }

    /// <summary>The notices as the screen draws them under its title (<see cref="NoticeText.Rows"/>): wrapped, the save's word whole and the rest capped, or none.</summary>
    internal IReadOnlyList<string> NoticeRows { get; }

    /// <summary>The dungeon list, as the campaign gave it when the screen was made.</summary>
    public IReadOnlyList<DungeonListing> Listings => _listings;

    /// <summary>The index of the listing ENTER descends into, or -1 when there is none to enter.</summary>
    public int Chosen => _chosen;

    /// <summary>How the last run ended, which the screen shows above the list, or null before the first.</summary>
    public RunResult? LastResult => _campaign.LastResult;

    /// <summary>Whether the menu is up, over the hub.</summary>
    internal bool MenuOpen => _menu;

    /// <summary>What the menu answers under its lines — what a save by hand came to (<see cref="SlotSaved"/>) — until the next key or until the menu closes; null for nothing.</summary>
    internal string? MenuAnswer => _answer;

    /// <summary>Whether this session has a title to quit to (<see cref="ISaveHost.HasTitle"/>): a dev-override session has none.</summary>
    internal bool HasTitle => _host.HasTitle;

    /// <summary>The slot the campaign saves into, by its name (<see cref="ISaveHost.SavesInto"/>), which the menu says under its options.</summary>
    internal string SavesInto => _host.SavesInto;

    public override void Initialize()
    {
        base.Initialize();
        WireInput();

        Log.Info($"[Hub] Between runs, run counter {_campaign.RunCounter}: "
            + (_listings.Count == 0 ? "nothing left to enter" : string.Join("; ", _listings.Select(BetweenRunsHud.ListingLine)))
            + (_chosen >= 0 ? $" - {_listings[_chosen].Def.Name} chosen" : ""));
    }

    public override void Render(Camera camera)
    {
        base.Render(camera);
        _hud.Render(this);
    }

    public override void Shutdown()
    {
        _hud.Dispose();
        base.Shutdown();
    }

    /// <summary>
    /// Every key press, once, through one handler: the menu reads it first while it
    /// is up (<see cref="Route"/>), so a key it takes never also moves the choice or
    /// descends. KeyPressed fires once per physical press, so a held key acts once.
    /// </summary>
    private void WireInput() => Input!.SubscribeToKeyPressed(e => OnKey(e.Key));

    /// <summary>
    /// One key press: where <see cref="Route"/> sends it. Any key but ESC clears what
    /// the menu answered — it stays until the next key — before it does anything
    /// else. Nothing on the way out, or while the engine fades between scenes.
    /// </summary>
    internal void OnKey(Keys key)
    {
        if (_leaving || SceneManager.Instance.IsTransitioning) return;

        var route = Route(_menu, key);
        if (route != KeyRoute.Game)
            _answer = null;
        switch (route)
        {
            case KeyRoute.Menu:
                MenuKey(key);
                break;
            case KeyRoute.List:
                ListKey(key);
                break;
        }
    }

    /// <summary>
    /// Where one key press goes between runs: ESC to the game, which asks
    /// <see cref="HandleEscape"/>; with the menu up, every other key to the menu —
    /// 1, 2 and 3 act, and ENTER, W/S and the arrows do nothing, so nothing descends
    /// behind the menu; and otherwise to the dungeon list. Pure, so it is walked key
    /// by key (<c>BetweenRunsHudTests</c>).
    /// </summary>
    /// <param name="menu">Whether the menu is up.</param>
    /// <param name="key">The key pressed.</param>
    internal static KeyRoute Route(bool menu, Keys key)
        => key == Keys.Escape ? KeyRoute.Game
            : menu ? KeyRoute.Menu
            : KeyRoute.List;

    /// <summary>The dungeon list's keys: W/S or the arrows move the choice, ENTER descends; every other key does nothing.</summary>
    private void ListKey(Keys key)
    {
        switch (key)
        {
            case Keys.W or Keys.Up:
                Move(-1);
                break;
            case Keys.S or Keys.Down:
                Move(+1);
                break;
            case Keys.Enter or Keys.KeyPadEnter:
                Descend();
                break;
        }
    }

    /// <summary>
    /// The menu's keys (<see cref="PauseMenu.Press"/>): 1 closes it, 2 asks the host
    /// to save the campaign into its own slot — between runs is always a safe point
    /// — the menu kept up for the answer, and 3 quits to the title, or, in a
    /// dev-override session, closes the game, whose close saves its scratch slot as
    /// ever. Every other key does nothing.
    /// </summary>
    private void MenuKey(Keys key)
    {
        if (SlotMenu.Digit(key) is not { } digit)
            return;
        var (action, said) = PauseMenu.Press(digit, canSave: true, _host.HasTitle);
        switch (action)
        {
            case PauseAction.Close:
                _menu = false;
                break;
            case PauseAction.Save:
                Log.Info($"[Save] SAVE GAME: saving by hand into {_host.SavesInto}, at the frame's end");
                _host.SaveNow();
                break;
            case PauseAction.Refuse:
                _answer = said;
                Log.Info($"[Save] {said}");
                break;
            case PauseAction.QuitToTitle:
                _leaving = true;
                Log.Info("[Hub] QUIT TO TITLE");
                _host.QuitToTitle();
                break;
            case PauseAction.QuitGame:
                _leaving = true;
                Log.Info("[Hub] QUIT GAME: a dev-override session has no title");
                _game.Close();
                break;
        }
    }

    /// <summary>
    /// What a save by hand came to, from the host once it is written at the frame's
    /// end (<see cref="ISaveHost.SaveNow"/>): drawn inside the menu, under its
    /// lines, until the next key or until the menu closes. With the menu closed by
    /// then, only the log keeps it.
    /// </summary>
    /// <param name="notice">What the save came to: <c>SAVED TO SLOT 2</c>, or why not.</param>
    internal void SlotSaved(string notice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notice);
        if (_menu)
            _answer = notice;
    }

    /// <summary>
    /// ESC, asked by the game before it does anything else with it, and always
    /// taken here: on the menu it closes it; with nothing up it opens the menu. So
    /// the game never quits from this screen but by the menu's QUIT TO TITLE, a
    /// dev-override session's QUIT GAME, or the window's close.
    /// </summary>
    public bool HandleEscape()
    {
        if (_leaving)
            return true;   // on its way out: the host has what it asked for
        _answer = null;
        _menu = !_menu;
        return true;
    }

    /// <summary>Move the choice to the next line that can be entered, up or down, round the end of the list.</summary>
    private void Move(int step) => _chosen = NextChoice(_listings, _chosen, step);

    /// <summary>
    /// ENTER: descend into the chosen dungeon, once. The host enters it and fades
    /// to the run. Never while the engine is still fading between scenes, and
    /// never twice: after the first press the screen is on its way out.
    /// </summary>
    private void Descend()
    {
        if (_chosen < 0 || _chosen >= _listings.Count || !_listings[_chosen].Selectable) return;

        var choice = _listings[_chosen];
        _leaving = true;
        Log.Info($"[Hub] Descending into {choice.Def.Name}{(choice.Early ? ", offered early: every dungeon is locked" : "")}");
        _host.Descend(choice.Def.Id);
    }

    /// <summary>The campaign's default next dungeon, or null when content leaves none that is not retired.</summary>
    private static string? DefaultNextOrNull(CampaignState campaign)
    {
        try
        {
            return campaign.DefaultNext();
        }
        catch (InvalidOperationException)
        {
            return null;   // nothing left to enter: the list says so
        }
    }

    /// <summary>
    /// Where the choice starts: the line of <paramref name="defaultId"/>, the
    /// campaign's default next dungeon, which can always be entered. Failing
    /// that, the first line that can be entered. Failing that, -1: nothing to
    /// choose, and ENTER does nothing.
    /// </summary>
    internal static int FirstChoice(IReadOnlyList<DungeonListing> listings, string? defaultId)
    {
        ArgumentNullException.ThrowIfNull(listings);
        for (int i = 0; i < listings.Count; i++)
            if (listings[i].Selectable && listings[i].Def.Id == defaultId)
                return i;
        for (int i = 0; i < listings.Count; i++)
            if (listings[i].Selectable)
                return i;
        return -1;
    }

    /// <summary>
    /// The choice one step from <paramref name="from"/>: the next line up
    /// (<paramref name="step"/> -1) or down (+1) that can be entered, going round
    /// the end of the list. A locked line is passed over — it is there to be read,
    /// and ENTER could do nothing with it. With no other line to go to, the
    /// choice stays where it is.
    /// </summary>
    internal static int NextChoice(IReadOnlyList<DungeonListing> listings, int from, int step)
    {
        ArgumentNullException.ThrowIfNull(listings);
        int count = listings.Count;
        if (count == 0 || step == 0) return from;
        int direction = Math.Sign(step);
        int at = from;
        for (int moved = 0; moved < count; moved++)
        {
            at = ((at + direction) % count + count) % count;
            if (listings[at].Selectable)
                return at;
        }
        return from;
    }
}
