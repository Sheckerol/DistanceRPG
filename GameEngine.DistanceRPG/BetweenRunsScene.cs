using GameEngine.Core;
using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What the between-runs screen reaches the game through: a dungeon chosen, a
/// change the hub made to the campaign saved (<see cref="RequestSave"/>), and —
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

    /// <summary>
    /// Save the campaign once the frame is over: the hub has just changed it — a
    /// weapon moved between the bags and the stable, deposited at the enchanter, or
    /// taken back out of the shop (PHASE 6a DECISION 10). Asked after every change,
    /// so a crash between runs loses none of them; asking twice in a frame is one
    /// save, into the campaign's own slot, and between runs is always a safe point.
    /// It is only asked for, as the dungeon's autosaves are
    /// (<see cref="IRunHost.RequestSave"/>), and one that fails is said on the hub
    /// at once (<see cref="BetweenRunsScene.SaveFailed"/>).
    /// </summary>
    void RequestSave();
}

/// <summary>
/// The screen between runs (§4.5, §6.2, §6.7): two pages, TAB going from one to
/// the other. <strong>DUNGEONS</strong> is the hub's dungeon list, and the last
/// run's summary above it. <strong>WEAPONS</strong> (Phase 6a.2) is every
/// member's hand and bag, and the stable (<see cref="HubWeapons"/>), where a
/// weapon is taken up and put down elsewhere. A campaign continued or loaded
/// from the title opens on DUNGEONS when it was saved between runs — with
/// <c>CAMPAIGN RESUMED FROM SLOT 2</c> under the title — and so does a new one;
/// and every run's end comes back to it.
/// The campaign's list decides what is offered
/// (<see cref="CampaignState.Listings"/>). Before the tutorial is beaten that is
/// the tutorial alone, with the warning §4.3 asks for. After, it is every dungeon
/// left, a locked one counting down, and with every one locked, the one offered
/// early. This screen only presents the list and passes the choice on.
/// <para>
/// On DUNGEONS, W/S or the arrows move the choice over the lines that can be
/// entered — a locked line is read, not chosen. ENTER descends into the choice,
/// once, and the host begins the run (<see cref="IBetweenRunsHost.Descend"/>).
/// The choice starts on the campaign's own default
/// (<see cref="CampaignState.DefaultNext"/>): the tutorial until it is beaten,
/// then the first dungeon whose lock has lifted, then the early offer. NEW
/// CAMPAIGN is the title's (R2(e)), and no key here begins one.
/// </para>
/// <para>
/// On WEAPONS, W/S or the arrows move the choice over the weapons and the rooms,
/// and ENTER on a weapon takes it up. Carrying it, W/S go over the places it can
/// be put down alone, ENTER puts it down — a swap onto a weapon, into a room
/// where none lies; onto a hand is how a weapon is equipped — and ESC puts it
/// back where it was. TAB does nothing while a weapon is carried. The move is
/// the campaign's (<see cref="CampaignState.Move"/>), made once, as the weapon is
/// put down, so a carry is this screen's alone and nothing has moved until then.
/// A move that would leave a member carrying no weapon is refused, said in a row
/// over the keys until the next key, and the weapon stays taken up (PHASE 6a
/// DECISION 7). Every move asks the host for a save (PHASE 6a DECISION 10).
/// </para>
/// <para>
/// <strong>The enchanter</strong> (§6.2; Phase 6a.5): D on a weapon opens the
/// deposit panel over the page (<see cref="DepositPanel"/>) — ENCHANT or REFINE on
/// 1 and 2, each with its odds or, dimmed, its reason, which its key says; ENCHANT's
/// offer list, W/S and ENTER; then the confirm, where ENTER alone deposits
/// (<see cref="Enchanter.Deposit"/>). Before the tutorial is beaten D says the
/// enchanter is shut, over the keys, and opens nothing. T on a weapon in the shop
/// asks whether to take it back, its wear coming back with it, and ENTER alone does
/// (<see cref="Enchanter.TakeBack"/>; PHASE 6a DECISION 8). ESC goes back a step,
/// out of the panel from its services, and answers no to the ask. TAB, D and T do
/// nothing while either is up, and nothing changes the campaign but the ENTER that
/// deposits or takes back — each logged and followed by a save asked for (PHASE 6a
/// DECISION 10), the choice following the weapon into the shop and back out. That
/// ENTER asks the campaign again as it stands (<see cref="DepositPanel.RefusalNow"/>),
/// and refused, closes the panel and says why over the keys, doing nothing.
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
/// the menu, and with a weapon carried ESC puts it back instead of opening the
/// menu, and with the deposit panel or the take-back ask up it goes back out of
/// them: always consumed here, so the game never quits from this screen but by
/// the menu or the window's close.
/// </para>
/// <para>
/// Like every gameplay scene it is single-use: the host registers a fresh one
/// each time the screen is shown. What it reads is the campaign's, and it writes
/// to it only between runs, through the campaign's own moves and the enchanter,
/// and asks for a save after each (PHASE 6a DECISION 10). The dungeon list is
/// read once, when the screen is made: nothing the hub does moves a lock.
/// </para>
/// <para>
/// A screen is handed what the game has to say under its title — on the first one
/// a campaign opens on, the save's word on it: that it resumed, or a new one
/// begins, and what was set aside on the way (and, when a dev override or
/// <c>--new-campaign</c> skipped the title, what the data files raised) — and,
/// after a save that could not be written, that. The save's word is shown whole —
/// a set-aside notice's full path and how to try the file again, the numbers
/// changed since the save — and anything else is capped after it
/// (<see cref="NoticeText.Rows"/>). A save of the hub's own changes that cannot be
/// written is said there at once, the same way (<see cref="SaveFailed"/>), and a
/// failure is taken back once a later save goes through
/// (<see cref="SaveWentThrough"/>). The log keeps them all.
/// </para>
/// </summary>
public sealed class BetweenRunsScene : Scene
{
    /// <summary>What the screen is doing, which decides where a key goes (<see cref="Route"/>).</summary>
    internal enum HubMode
    {
        /// <summary>The menu ESC opens is up, over either page.</summary>
        Menu,

        /// <summary>The DUNGEONS page: the last run and the dungeon list.</summary>
        Dungeons,

        /// <summary>The WEAPONS page, browsing: nothing taken up.</summary>
        Weapons,

        /// <summary>The WEAPONS page with a weapon taken up, to be put down.</summary>
        Carrying,

        /// <summary>The deposit panel over WEAPONS (Phase 6a.5): its services, ENCHANT's offer list, or the confirm.</summary>
        Deposit,

        /// <summary>The take-back ask over WEAPONS (Phase 6a.5): whether to take a weapon in the shop back.</summary>
        Ask,
    }

    /// <summary>Where one key press goes between runs (<see cref="Route"/>).</summary>
    internal enum KeyRoute
    {
        /// <summary>ESC: the game's to route, which asks <see cref="HandleEscape"/>.</summary>
        Game,

        /// <summary>The dungeon list's: W/S and the arrows move the choice, ENTER descends, and every other key does nothing.</summary>
        List,

        /// <summary>The menu's: 1, 2 and 3 act, and every other key does nothing — ENTER, TAB and the arrows included.</summary>
        Menu,

        /// <summary>TAB on either page: to the other.</summary>
        Page,

        /// <summary>The WEAPONS page's, browsing: W/S and the arrows move the choice, ENTER takes a weapon up, D opens the deposit panel on a weapon, T the take-back ask on a weapon in the shop, and every other key does nothing.</summary>
        Weapons,

        /// <summary>The WEAPONS page's, carrying: W/S and the arrows move the choice over where it can go, ENTER puts it down, and every other key does nothing — TAB, D and T included.</summary>
        Carrying,

        /// <summary>The deposit panel's: 1 and 2 pick a service, W/S and the arrows choose on the offer list, ENTER chooses there and deposits on the confirm, and every other key does nothing — TAB, D and T included.</summary>
        Panel,

        /// <summary>The take-back ask's: ENTER takes the weapon back, and every other key does nothing.</summary>
        Ask,
    }

    private readonly Game _game;
    private readonly CampaignState _campaign;
    private readonly IBetweenRunsHost _host;
    private readonly BetweenRunsHud _hud;

    private readonly IReadOnlyList<DungeonListing> _listings;
    private int _chosen;

    // The WEAPONS page: its rows, read off the campaign each time, its choice, its
    // window and a weapon taken up — a carry is the page's alone, and the campaign
    // changes only when it is put down. Which page is up: DUNGEONS on every
    // showing. And what the last put-down was refused with, over the keys, until
    // the next key.
    private readonly HubWeapons _weapons;
    private bool _onWeapons;
    private string? _refusal;

    // The deposit panel or the take-back ask, up over WEAPONS (Phase 6a.5): the
    // screen's alone, like a carry — the campaign changes only at the ENTER that
    // deposits or takes back.
    private DepositPanel? _panel;

    // The host has been told something that ends this screen: a descent, or the
    // quit to the title. After it every key is ignored while the screen fades out.
    private bool _leaving;

    // The menu is up (ESC opens it), and what it answers under its lines — what a
    // save by hand came to — until the next key or until the menu closes. Menus:
    // never saved.
    private bool _menu;
    private string? _answer;

    // What the game had to say under the title, and how many of them, from the
    // first, are shown whole: a save of the hub's that failed is laid ahead of
    // them, and a failure is taken back out once a later save goes through.
    private IReadOnlyList<string> _notices;
    private int _shownWhole;

    /// <param name="game">The game window: its size and its shaders, for the text, and its close, which a dev-override session's QUIT GAME is. A screen built to be read, never shown, is handed none.</param>
    /// <param name="campaign">The campaign between its runs: no visit is in progress.</param>
    /// <param name="host">Who is told which dungeon was chosen (<see cref="IBetweenRunsHost.Descend"/>), asked for a save after each change the hub makes (<see cref="IBetweenRunsHost.RequestSave"/>), and asked for a save by hand and the way to the title (<see cref="ISaveHost"/>).</param>
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
        _notices = notices ?? [];
        _shownWhole = shownWhole;
        NoticeRows = NoticeText.Rows(_notices, _shownWhole);

        // The list as the campaign gives it now: nothing the hub does moves a lock.
        _listings = campaign.Listings();
        _chosen = FirstChoice(_listings, DefaultNextOrNull(campaign));
        _weapons = new HubWeapons(campaign);
    }

    /// <summary>The notices as the screen draws them under its title (<see cref="NoticeText.Rows"/>): wrapped, the save's word whole and the rest capped, or none — and a save of the hub's that failed, whole, ahead of them (<see cref="SaveFailed"/>), until a later save goes through (<see cref="SaveWentThrough"/>).</summary>
    internal IReadOnlyList<string> NoticeRows { get; private set; }

    /// <summary>The dungeon list, as the campaign gave it when the screen was made.</summary>
    public IReadOnlyList<DungeonListing> Listings => _listings;

    /// <summary>The index of the listing ENTER descends into, or -1 when there is none to enter.</summary>
    public int Chosen => _chosen;

    /// <summary>How the last run ended, which the screen shows above the list, or null before the first.</summary>
    public RunResult? LastResult => _campaign.LastResult;

    /// <summary>Whether the menu is up, over the hub.</summary>
    internal bool MenuOpen => _menu;

    /// <summary>What the screen is doing now: the menu, a page, a weapon carried on WEAPONS, or the deposit panel or the take-back ask over it.</summary>
    internal HubMode Mode
        => _menu ? HubMode.Menu
            : _panel is { Current: DepositPanel.Step.Ask } ? HubMode.Ask
            : _panel is not null ? HubMode.Deposit
            : !_onWeapons ? HubMode.Dungeons
            : _weapons.Carried is null ? HubMode.Weapons
            : HubMode.Carrying;

    /// <summary>The deposit panel or the take-back ask, while one is up over WEAPONS; null otherwise.</summary>
    internal DepositPanel? Panel => _panel;

    /// <summary>Whether the WEAPONS page is up, rather than DUNGEONS — under the menu too.</summary>
    internal bool OnWeapons => _onWeapons;

    /// <summary>The WEAPONS page: its rows, its choice, its window and a weapon carried.</summary>
    internal HubWeapons Weapons => _weapons;

    /// <summary>What the last key was refused with — a put-down that would leave a member unarmed (<see cref="HubWeapons.KeepsAWeapon"/>), D while the enchanter is shut (<see cref="Enchanter.Closed"/>), a dimmed service's number key, an ENTER on a panel the campaign has changed under (<see cref="DepositPanel.RefusalNow"/>) — said over the keys until the next key; null for nothing.</summary>
    internal string? Refusal => _refusal;

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
            + (_chosen >= 0 ? $" - {_listings[_chosen].Def.Name} chosen" : "")
            + $" - the stable holds {_campaign.Stable.Count}, the enchanter {_campaign.AtTheEnchanter.Count}");
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
    /// the menu answered and what the last key was refused with — each stays until
    /// the next key — before it does anything else. Nothing on the way out, or while
    /// the engine fades between scenes.
    /// </summary>
    internal void OnKey(Keys key)
    {
        if (_leaving || SceneManager.Instance.IsTransitioning) return;

        var route = Route(Mode, key);
        if (route != KeyRoute.Game)
        {
            _answer = null;
            _refusal = null;
        }
        switch (route)
        {
            case KeyRoute.Menu:
                MenuKey(key);
                break;
            case KeyRoute.List:
                ListKey(key);
                break;
            case KeyRoute.Page:
                _onWeapons = !_onWeapons;
                break;
            case KeyRoute.Weapons:
            case KeyRoute.Carrying:
                WeaponsKey(key);
                break;
            case KeyRoute.Panel:
                PanelKey(key);
                break;
            case KeyRoute.Ask:
                AskKey(key);
                break;
        }
    }

    /// <summary>
    /// Where one key press goes between runs, by what the screen is doing
    /// (<paramref name="mode"/>): ESC always to the game, which asks
    /// <see cref="HandleEscape"/>; with the menu up, every other key to the menu —
    /// 1, 2 and 3 act, and ENTER, TAB, W/S and the arrows do nothing, so nothing
    /// descends or moves behind the menu; TAB on either page, to the other; and
    /// otherwise to the page — the dungeon list, or WEAPONS browsing or carrying,
    /// where TAB is the carry's and does nothing — or to the deposit panel or the
    /// take-back ask up over it, whose every key is theirs, TAB included, so no page
    /// turns under them. Pure, so it is walked key by key (<c>BetweenRunsHudTests</c>).
    /// </summary>
    /// <param name="mode">What the screen is doing.</param>
    /// <param name="key">The key pressed.</param>
    internal static KeyRoute Route(HubMode mode, Keys key)
        => key == Keys.Escape ? KeyRoute.Game
            : mode switch
            {
                HubMode.Menu => KeyRoute.Menu,
                HubMode.Dungeons or HubMode.Weapons when key == Keys.Tab => KeyRoute.Page,
                HubMode.Dungeons => KeyRoute.List,
                HubMode.Weapons => KeyRoute.Weapons,
                HubMode.Carrying => KeyRoute.Carrying,
                HubMode.Deposit => KeyRoute.Panel,
                HubMode.Ask => KeyRoute.Ask,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a HubMode."),
            };

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
    /// The WEAPONS page's keys: W/S or the arrows move the choice — over the
    /// weapons, the rooms and the shop, or, carrying, over where the weapon can go —
    /// and ENTER takes the weapon chosen up, or puts the one carried down. Browsing,
    /// D opens the deposit panel on a weapon (<see cref="OpenDeposit"/>) and T the
    /// take-back ask on a weapon in the shop (<see cref="AskTakeBack"/>); carrying,
    /// both do nothing. Every other key does nothing.
    /// </summary>
    private void WeaponsKey(Keys key)
    {
        switch (key)
        {
            case Keys.W or Keys.Up:
                _weapons.Step(-1);
                break;
            case Keys.S or Keys.Down:
                _weapons.Step(+1);
                break;
            case Keys.Enter or Keys.KeyPadEnter:
                if (_weapons.Carried is null)
                    TakeUp();
                else
                    PutDown();
                break;
            case Keys.D when _weapons.Carried is null:
                OpenDeposit();
                break;
            case Keys.T when _weapons.Carried is null:
                AskTakeBack();
                break;
        }
    }

    /// <summary>ENTER on a weapon: taken up, which moves nothing yet — on anything else, nothing.</summary>
    private void TakeUp()
    {
        if (_weapons.TakeUp() is { } taken)
            Log.Info($"[Hub] Took up {taken.Weapon.Name} from {Where(taken.From)}");
    }

    /// <summary>
    /// ENTER while carrying: the weapon put down on the place chosen — a swap onto
    /// a weapon, into a room where none lies — through the campaign's one door
    /// (<see cref="CampaignState.Move"/>), and a save asked for (PHASE 6a DECISION
    /// 10). Put down where it was taken from, it is put back, and nothing moves. A
    /// move that would leave a member carrying no weapon is refused, said over the
    /// keys, and the weapon stays taken up (PHASE 6a DECISION 7).
    /// </summary>
    private void PutDown()
    {
        if (_weapons.Carried is not { } carried || _weapons.ChosenRow.Place is not { } to)
            return;
        if (to == carried.From)
        {
            _weapons.PutBack();
            Log.Info($"[Hub] Put {carried.Weapon.Name} back in {Where(to)}");
            return;
        }
        if (_campaign.WouldDisarm(carried.From, to) is { } unarmed)
        {
            _refusal = HubWeapons.KeepsAWeapon(unarmed.Id);
            Log.Info($"[Hub] Refused: {_refusal}");
            return;
        }

        string from = Where(carried.From), into = Where(to);   // named as they stood before the move closed the stable up
        var (moved, swapped, armed) = _campaign.Move(carried.From, to);
        _weapons.PutDown(moved);
        Log.Info($"[Hub] Moved {moved.Name} from {from} to {into}"
            + (swapped is null ? "" : $", and {swapped.Name} the other way")
            + (armed is null ? "" : $"; {carried.From.MemberId}'s hand, emptied, took up {armed.Name} from the bag"));
        _host.RequestSave();
    }

    /// <summary>A place as the log names it: <c>A's hand</c>, <c>A's bag slot 3</c>, <c>the stable (2 of 5)</c>, <c>the stable's end</c>.</summary>
    private string Where(WeaponPlace place)
        => !place.IsStable ? place.Index == 0 ? $"{place.MemberId}'s hand" : $"{place.MemberId}'s bag slot {place.Index + 1}"
            : place.Index < _campaign.Stable.Count ? $"the stable ({place.Index + 1} of {_campaign.Stable.Count})"
            : "the stable's end";

    // ── The enchanter (§6.2, Phase 6a.5) ────────────────────────────────────

    /// <summary>
    /// D on a weapon: the deposit panel over the page, its services asked of the
    /// enchanter as the campaign stands (<see cref="DepositPanel.Open"/>) — or,
    /// before the tutorial is beaten, the enchanter's gate said over the keys, and
    /// nothing opened (§6.3). On anything but a weapon out of the shop, nothing.
    /// </summary>
    private void OpenDeposit()
    {
        if (_weapons.ChosenRow is not { Kind: HubWeapons.RowKind.Weapon, Place: { } from, Weapon: { } weapon })
            return;
        if (!Enchanter.IsOpen(_campaign))
        {
            _refusal = Enchanter.Closed(GameContent.Current.Dungeons.Tutorial);
            Log.Info($"[Hub] Refused: {_refusal}");
            return;
        }
        _panel = DepositPanel.Open(_campaign, from);
        Log.Info($"[Hub] Deposit panel on {weapon.Name} in {Where(from)}: {string.Join("; ", _panel.Services.Select(option => option.Text))}");
    }

    /// <summary>T on a weapon in the shop: the take-back ask over the page (PHASE 6a DECISION 8). On anything else, nothing.</summary>
    private void AskTakeBack()
    {
        if (_weapons.ChosenRow is not { Kind: HubWeapons.RowKind.Shop, Deposit: { } deposit })
            return;
        _panel = DepositPanel.Asking(deposit);
        Log.Info($"[Hub] Asked to take back {deposit.Weapon.Name}, deposit #{deposit.Ordinal}");
    }

    /// <summary>
    /// The deposit panel's keys (<see cref="DepositPanel"/>): a number picks a
    /// service — a dimmed one's says why, over the keys — W/S or the arrows choose
    /// on the offer list, ENTER chooses there and deposits on the confirm. Every
    /// other key does nothing.
    /// </summary>
    private void PanelKey(Keys key)
    {
        var panel = _panel!;
        switch (key)
        {
            case Keys.W or Keys.Up:
                panel.Move(-1);
                break;
            case Keys.S or Keys.Down:
                panel.Move(+1);
                break;
            case Keys.Enter or Keys.KeyPadEnter:
                if (panel.Current == DepositPanel.Step.Offer)
                    panel.ChooseOffer();
                else if (panel.Current == DepositPanel.Step.Confirm)
                    MakeDeposit(panel);
                break;
            default:
                if (SlotMenu.Digit(key) is { } digit && panel.Pick(digit) is { } refused)
                {
                    _refusal = refused;
                    Log.Info($"[Hub] Refused: {refused}");
                }
                break;
        }
    }

    /// <summary>The take-back ask's keys: ENTER takes the weapon back; every other key does nothing.</summary>
    private void AskKey(Keys key)
    {
        if (key is Keys.Enter or Keys.KeyPadEnter)
            TakeBack(_panel!);
    }

    /// <summary>
    /// ENTER on the confirm: the weapon deposited (<see cref="Enchanter.Deposit"/>) —
    /// its whole pool cashed, its capacity grown on it, into the shop — the panel
    /// closed, the choice on the weapon's row in the shop, the deposit logged, and a
    /// save asked for (PHASE 6a DECISION 10). Asked again first
    /// (<see cref="Refused"/>): refused now, nothing is deposited.
    /// </summary>
    private void MakeDeposit(DepositPanel panel)
    {
        if (Refused(panel) || panel.From is not { } from)
            return;
        string where = Where(from);   // named as it stood before the deposit closed the stable up
        var made = Enchanter.Deposit(_campaign, from, panel.Service, panel.EnchantmentId);
        _panel = null;
        _weapons.Follow(made.Entry.Weapon);
        Log.Info(DepositedLine(made, from, where));
        _host.RequestSave();
    }

    /// <summary>
    /// ENTER on the ask: the weapon taken back (<see cref="Enchanter.TakeBack"/>) to
    /// the stable's end, its service forfeit and its deposit's cash reversed (PHASE
    /// 6a DECISION 8), the ask closed, the choice on the weapon, the take-back
    /// logged, and a save asked for (PHASE 6a DECISION 10). Asked again first
    /// (<see cref="Refused"/>): refused now, nothing is taken back.
    /// </summary>
    private void TakeBack(DepositPanel ask)
    {
        if (Refused(ask))
            return;
        var taken = Enchanter.TakeBack(_campaign, ask.TakingBack!);
        _panel = null;
        _weapons.Follow(taken.Entry.Weapon);
        Log.Info(TookBackLine(taken));
        _host.RequestSave();
    }

    /// <summary>
    /// The confirm's or the ask's ENTER asked again of the campaign as it stands
    /// (<see cref="DepositPanel.RefusalNow"/>), since the panel read it as it opened:
    /// refused, the panel closes, the reason is said over the keys until the next
    /// key, and nothing is done or saved. Nothing changes the campaign while a panel
    /// is up yet, so in play this lets every ENTER through.
    /// </summary>
    /// <returns>True when the ENTER is refused.</returns>
    private bool Refused(DepositPanel panel)
    {
        if (panel.RefusalNow(_campaign) is not { } refused)
            return false;
        _panel = null;
        _refusal = refused;
        Log.Info($"[Hub] Refused: {refused}");
        return true;
    }

    /// <summary>
    /// A deposit, as the log says it (the plan's 1h), from what the enchanter handed
    /// back (<see cref="DepositMade"/>; Logic never logs): <c>[Enchanter] Deposited
    /// Tower Guard (tower_guard) from B's hand: Enchant vampiric, 30 wear cashed
    /// (capacity 30 -> 31), 1 run, deposit #0; B's hand, emptied, took up Staff of
    /// Renewal from the bag</c>. The ordinal is named here, in the session that made
    /// the deposit, so a finished service's improvement roll, which the run's end
    /// logs, can be keyed again from the log.
    /// </summary>
    /// <param name="made">What the deposit did.</param>
    /// <param name="from">Where the weapon was taken from.</param>
    /// <param name="where">That place as the log names it, as it stood before the deposit.</param>
    internal static string DepositedLine(DepositMade made, WeaponPlace from, string where)
    {
        ArgumentNullException.ThrowIfNull(made);
        ArgumentNullException.ThrowIfNull(where);
        var entry = made.Entry;
        return $"[Enchanter] Deposited {entry.Weapon.Name} ({entry.Weapon.Id}) from {where}: {Bought(entry)}, {entry.WearAtDeposit} wear cashed"
            + $" (capacity {made.Before.Max} -> {made.After.Max}), {RunsSaid(entry.Runs)}, deposit #{entry.Ordinal}"
            + (made.Armed is { } armed ? $"; {from.MemberId}'s hand, emptied, took up {armed.Name} from the bag" : "");
    }

    /// <summary>
    /// A take-back, as the log says it (the plan's 1h), from what the enchanter
    /// handed back (<see cref="TakenBack"/>): <c>[Enchanter] Took back Tower Guard
    /// (tower_guard) to the stable's end: Enchant vampiric forfeited, 0 of 1 run
    /// worked lost, 30 wear returned (capacity 31 -> 30), deposit #0</c> — the
    /// capacity after as it was before the deposit.
    /// </summary>
    /// <param name="taken">What the take-back did.</param>
    internal static string TookBackLine(TakenBack taken)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var deposit = taken.Entry;
        return $"[Enchanter] Took back {deposit.Weapon.Name} ({deposit.Weapon.Id}) to the stable's end: {Bought(deposit)} forfeited,"
            + $" {deposit.RunsWorked} of {RunsSaid(deposit.Runs)} worked lost, {deposit.WearAtDeposit} wear returned"
            + $" (capacity {taken.Before.Max} -> {taken.After.Max}), deposit #{deposit.Ordinal}";
    }

    /// <summary>What a deposit bought, as the log names it: <c>Enchant vampiric</c>, <c>Refine</c>.</summary>
    private static string Bought(Deposit deposit)
        => deposit.Service == Service.Enchant && deposit.EnchantmentId is { } id ? $"Enchant {id}" : deposit.Service.ToString();

    /// <summary>Runs as the log says them: <c>1 run</c>, <c>2 runs</c>.</summary>
    private static string RunsSaid(int runs) => runs == 1 ? "1 run" : $"{runs} runs";

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
    /// The host could not write a save of the campaign between runs (PHASE 6a
    /// DECISION 10): said at once under the title, in the warning's orange, shown
    /// whole ahead of the notices up — where the player is, so a change on the hub
    /// whose save failed is never taken for saved — and there until a later save
    /// goes through, which takes it back (<see cref="SaveWentThrough"/>). The next
    /// screen says it too, on the same terms. A notice already shown whole is not
    /// said twice. A file of the slot the write set aside first is said the same
    /// way (<see cref="CampaignBoot.Save"/>), and is never taken back.
    /// </summary>
    /// <param name="notice">What the failed write says (<see cref="CampaignBoot.NotSaved"/>), or what the write set aside (<see cref="CampaignBoot.SetAsideNotice"/>).</param>
    internal void SaveFailed(string notice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notice);
        if (_notices.Take(_shownWhole).Contains(notice, StringComparer.Ordinal))
            return;
        _notices = [notice, .. _notices];
        _shownWhole++;
        NoticeRows = NoticeText.Rows(_notices, _shownWhole);
    }

    /// <summary>
    /// A save of the campaign went through (<see cref="CampaignBoot.Save"/>): the
    /// disk holds it again, so what the screen says of every save that failed
    /// since the last one that went through is no longer true, and is taken back
    /// from under the title — a failure of the hub's own, said at once
    /// (<see cref="SaveFailed"/>), and one the screen was handed from the run's end
    /// alike — as the game takes it back from the next screen. What else is said
    /// stays as it was: the save's word on the campaign, the data files' notices,
    /// and a file set aside, which is a fact about a file whatever the saves after
    /// it do, and never among <paramref name="stale"/>.
    /// </summary>
    /// <param name="stale">What each save that failed since the last one that went through said (<see cref="CampaignBoot.NotSaved"/>).</param>
    internal void SaveWentThrough(IReadOnlyCollection<string> stale)
    {
        ArgumentNullException.ThrowIfNull(stale);
        var kept = new List<string>(_notices.Count);
        int shownWhole = _shownWhole;
        for (int i = 0; i < _notices.Count; i++)
        {
            if (!stale.Contains(_notices[i], StringComparer.Ordinal))
                kept.Add(_notices[i]);
            else if (i < _shownWhole)
                shownWhole--;
        }
        if (kept.Count == _notices.Count)
            return;
        _notices = kept;
        _shownWhole = shownWhole;
        NoticeRows = NoticeText.Rows(_notices, _shownWhole);
    }

    /// <summary>
    /// ESC, asked by the game before it does anything else with it, and always
    /// taken here: with a weapon carried it puts it back, and nothing has moved; with
    /// the deposit panel up it goes back a step, and from its services closes it,
    /// nothing deposited; on the take-back ask it answers no; on the menu it closes
    /// it; with nothing up it opens the menu. So the game never quits from this
    /// screen but by the menu's QUIT TO TITLE, a dev-override session's QUIT GAME, or
    /// the window's close. Nothing here reads the content: the game's own ESC
    /// reaches it from before the window is up.
    /// </summary>
    public bool HandleEscape()
    {
        if (_leaving)
            return true;   // on its way out: the host has what it asked for
        _answer = null;
        _refusal = null;
        if (_weapons.PutBack() is { } putBack)
        {
            Log.Info($"[Hub] Put {putBack.Name} back: nothing moved");
            return true;
        }
        if (_panel is { } panel)
        {
            bool asked = panel.Current == DepositPanel.Step.Ask;
            if (panel.Back())
            {
                _panel = null;
                Log.Info(asked ? $"[Hub] Left {panel.Weapon.Name} in the shop: nothing taken back" : $"[Hub] Closed the deposit panel on {panel.Weapon.Name}: nothing deposited");
            }
            return true;
        }
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
