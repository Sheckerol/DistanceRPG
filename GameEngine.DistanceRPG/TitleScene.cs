using GameEngine.Core;
using GameEngine.Core.Diagnostics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What the title screen reaches the game through: a slot to load, a new campaign
/// to begin, the game to close. The screen never holds the concrete game that
/// hosts it, as the other screens never do (<see cref="IRunHost"/>,
/// <see cref="IBetweenRunsHost"/>).
/// </summary>
public interface ITitleHost
{
    /// <summary>
    /// Load slot <paramref name="slot"/> (0 the old autosave) and play the campaign it
    /// brings back, which saves into that slot from then on (the user's ruling of
    /// 2026-09-25 on the save slots, R5) — no other slot is touched — or, when
    /// nothing comes back, show the title again saying why. Called once, never
    /// while the engine is fading between scenes.
    /// </summary>
    void LoadSlot(int slot);

    /// <summary>
    /// Begin a campaign with the starting party in slot <paramref name="slot"/> —
    /// one from 1 to the count, chosen on NEW CAMPAIGN's list — which it saves into
    /// from then on (the user's ruling of 2026-09-25 on the save slots, R5): what
    /// that slot holds set aside, never deleted, every other slot untouched, the new
    /// campaign saved there before it is played, and the screen between runs over it
    /// — or, when it cannot be saved there, the title again saying why, the files put
    /// back. Called once, never mid-fade.
    /// </summary>
    void BeginNewCampaign(int slot);

    /// <summary>Close the game: no campaign is in play at the title, so nothing is saved.</summary>
    void Quit();
}

/// <summary>The title's options, in the ruling's order: each one's value is its row, top to bottom.</summary>
internal enum TitleOption
{
    Continue,
    NewCampaign,
    LoadGame,
    Quit,
}

/// <summary>What a key on the title asks its host for.</summary>
internal enum TitleAction
{
    /// <summary>Nothing of the host's: the key moved, asked, answered no, or was not the title's.</summary>
    None,

    /// <summary>Load a slot (<see cref="TitleStep.Slot"/>).</summary>
    Load,

    /// <summary>Begin a new campaign in a slot (<see cref="TitleStep.Slot"/>).</summary>
    NewCampaign,

    /// <summary>Close the game.</summary>
    Quit,
}

/// <summary>What one key press on the title comes to (<see cref="TitleMenu.Press"/>): an action for the host, and the slot a load loads or a new campaign begins in.</summary>
internal readonly record struct TitleStep(TitleAction Action, int Slot = 0);

/// <summary>
/// The title's choices (the user's ruling of 2026-09-25 on the save model,
/// R2(a)), pure — keys in, a step out — so every key is tested
/// (<c>TitleTests</c>), and only the drawing is the screen's
/// (<see cref="TitleHud"/>).
/// <list type="bullet">
/// <item><strong>The options, in the ruling's order</strong>: CONTINUE,
/// NEW CAMPAIGN, LOAD GAME, QUIT. CONTINUE is on when a slot would load something,
/// and loads the most recent save (<see cref="SaveListing.Newest"/>); with none
/// it is dimmed and W/S pass over it. The choice starts on CONTINUE when it is
/// on — ENTER at launch is one key to where the game stood — and otherwise on
/// NEW CAMPAIGN.</item>
/// <item><strong>NEW CAMPAIGN opens its slot list</strong>
/// (<see cref="SlotMenuMode.NewCampaign"/>), in the options' place: the slot the
/// new campaign begins in, and saves into from then on, is chosen there (the
/// user's ruling of 2026-09-25 on the save slots, R5) — slots 1 to the count, the
/// choice on the lowest empty one. An empty slot begins at once; one that holds
/// anything asks first, since the new campaign sets what it holds aside
/// (SLOT DECISION 2).</item>
/// <item><strong>LOAD GAME</strong> opens the load list
/// (<see cref="SlotMenuMode.Load"/>), in the options' place.</item>
/// <item><strong>A slot chosen loads at once</strong> — from the list, or
/// CONTINUE's — asking nothing: the campaign it brings back saves into that slot
/// from then on (the user's ruling of 2026-09-25 on the save slots, R5), and no
/// other slot is set aside. The load list's keys line says so, where the choice
/// is made (<see cref="TitleText.LoadKeysLine"/>).</item>
/// <item><strong>An ask takes exactly the next key, and only ENTER says
/// yes</strong>: every other key — N, Y, the arrows, a digit, the slot's own
/// among them — says no and is spent doing so, so a player pressing N to mean no
/// keeps what the ask named. ESC says no too (<see cref="Escape"/>). The one ask
/// is NEW CAMPAIGN's list's (<see cref="SlotMenu.Asking"/>).</item>
/// </list>
/// </summary>
internal sealed class TitleMenu
{
    /// <summary>The options, top to bottom.</summary>
    internal static readonly IReadOnlyList<TitleOption> Options = [TitleOption.Continue, TitleOption.NewCampaign, TitleOption.LoadGame, TitleOption.Quit];

    /// <param name="listing">The save folder's slots, as the title read them.</param>
    public TitleMenu(SaveListing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        Listing = listing;
        Count = SlotMenu.CountOf(listing);
        Cursor = (int)(ContinueOn ? TitleOption.Continue : TitleOption.NewCampaign);
    }

    /// <summary>The slots the title offers, as they were read when it opened.</summary>
    public SaveListing Listing { get; }

    /// <summary>How many manual slots there are: those the listing holds up to the count, beyond which a slot is listed as beyond it.</summary>
    public int Count { get; }

    /// <summary>The option chosen now, as its index in <see cref="Options"/>.</summary>
    public int Cursor { get; private set; }

    /// <summary>Whether CONTINUE is on: some slot would load something.</summary>
    public bool ContinueOn => Listing.Newest is not null;

    /// <summary>Whether an ask is up — NEW CAMPAIGN's, over a slot that holds anything: the next key is its alone.</summary>
    public bool Asking => List?.Asking is not null;

    /// <summary>The slot list while it is up, in the options' place — LOAD GAME's or NEW CAMPAIGN's, by its mode; null otherwise.</summary>
    public SlotMenu? List { get; private set; }

    /// <summary>
    /// One key press, once. The slot list takes it while it is up — an ask on it
    /// first, where ENTER says yes and any other key says no and is spent — and a
    /// slot it chooses is loaded, or a new campaign begun there, by the list's
    /// mode. Then the options: W/S or the arrows move (past CONTINUE when it is
    /// off, round the ends), and ENTER takes the one chosen. ESC comes through
    /// <see cref="Escape"/>, which the game asks first.
    /// </summary>
    public TitleStep Press(Keys key)
    {
        if (List is { } list)
        {
            switch (list.Press(key))
            {
                case SlotStep.Chosen:
                    return new TitleStep(list.Mode == SlotMenuMode.NewCampaign ? TitleAction.NewCampaign : TitleAction.Load, list.Chosen!.Value);
                case SlotStep.Back:
                    List = null;
                    break;
            }
            return default;
        }

        switch (key)
        {
            case Keys.W or Keys.Up:
                Cursor = Step(-1);
                break;
            case Keys.S or Keys.Down:
                Cursor = Step(+1);
                break;
            case Keys.Enter or Keys.KeyPadEnter:
                return Select();
        }
        return default;
    }

    /// <summary>
    /// ESC, asked by the game before it does anything else with it: on the slot
    /// list, an ask says no, and otherwise the list goes back to the options — each
    /// true, ESC spent — and on the options it is not the title's, so false: the
    /// game quits, as QUIT does.
    /// </summary>
    public bool Escape()
    {
        if (List is not { } list)
            return false;
        if (list.Press(Keys.Escape) == SlotStep.Back)
            List = null;
        return true;
    }

    /// <summary>ENTER on the option chosen now.</summary>
    private TitleStep Select()
    {
        switch (Options[Cursor])
        {
            case TitleOption.Continue:
                return Listing.Newest is { } newest ? new TitleStep(TitleAction.Load, newest.Number) : default;
            case TitleOption.NewCampaign:
                List = new SlotMenu(SlotMenuMode.NewCampaign, Listing, Count);
                return default;
            case TitleOption.LoadGame:
                List = new SlotMenu(SlotMenuMode.Load, Listing, Count);
                return default;
            default:
                return new TitleStep(TitleAction.Quit);
        }
    }

    /// <summary>The option one step from the chosen one, up or down, round the ends, past CONTINUE while it is off.</summary>
    private int Step(int step)
    {
        int at = Cursor;
        for (int moved = 0; moved < Options.Count; moved++)
        {
            at = ((at + step) % Options.Count + Options.Count) % Options.Count;
            if (Options[at] != TitleOption.Continue || ContinueOn)
                return at;
        }
        return Cursor;
    }
}

/// <summary>
/// The title screen (the user's ruling of 2026-09-25 on the save model, R2(a)):
/// the game opens on it, unless a dev override boots straight into play or
/// <c>--new-campaign</c> begins a campaign at launch. <c>DISTANCE RPG</c> across
/// the top; under it what the game has to say — the data files' notices, each
/// slot whose campaign could not be saved the last time play was left, a slot's
/// file the file system would not let the game read, and, back from a load that
/// brought nothing back or a NEW CAMPAIGN that did not begin, why — the save's
/// word whole and the rest capped
/// (<see cref="NoticeText.Rows"/>); then the options (<see cref="TitleMenu"/>) or
/// a slot list in their place — LOAD GAME's, or NEW CAMPAIGN's; the keys, or an
/// ask, along the bottom.
/// <para>
/// No campaign is in play here: the game holds none until a slot loads or a
/// new one begins, so a close from the title saves nothing. ESC is the game's: it
/// asks <see cref="HandleEscape"/> first, which backs out of an ask or a list,
/// and otherwise the game closes. Single-use, as every scene is: the host shows a
/// fresh one each time.
/// </para>
/// </summary>
public sealed class TitleScene : Scene
{
    private readonly ITitleHost _host;
    private readonly TitleHud _hud;

    // The host has been told something that ends this screen: a load, a new
    // campaign, the quit. After it every key is ignored while the screen fades out.
    private bool _leaving;

    /// <param name="game">The game window: its size and its shaders, for the text.</param>
    /// <param name="host">Who is told what was chosen (<see cref="ITitleHost"/>).</param>
    /// <param name="listing">The save folder's slots, as the host read them for this showing.</param>
    /// <param name="shownWhole">How many of <paramref name="notices"/>, from the first, are the save's word, shown whole (<see cref="StartupNotices.Leading"/>).</param>
    /// <param name="notices">What the game has to say under the title, or none.</param>
    public TitleScene(Game game, ITitleHost host, SaveListing listing, int shownWhole, IReadOnlyList<string>? notices = null) : base("TitleScene")
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(listing);
        _host = host;
        _hud = new TitleHud(game);
        Menu = new TitleMenu(listing);
        NoticeRows = NoticeText.Rows(notices ?? [], shownWhole);
    }

    /// <summary>The title's choices, and where they stand.</summary>
    internal TitleMenu Menu { get; }

    /// <summary>The notices as the screen draws them under its title (<see cref="NoticeText.Rows"/>): wrapped, the save's word whole and the rest capped, or none.</summary>
    internal IReadOnlyList<string> NoticeRows { get; }

    public override void Initialize()
    {
        base.Initialize();

        // Every key press, once, through one handler: an ask reads it first, so a
        // key that answers one never also moves or chooses. KeyPressed fires once
        // per physical press, so a held key acts once.
        Input!.SubscribeToKeyPressed(e => OnKey(e.Key));
        Log.Info(Menu.Listing.Newest is { } newest
            ? $"[Title] The title: CONTINUE loads {SaveSlots.Label(newest.Number)}"
            : "[Title] The title: no save yet - CONTINUE is off");
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
    /// ESC, asked by the game before it does anything else with it: true when it
    /// said no to an ask or closed a slot list, and is spent; false on the
    /// options, and the game closes (<c>DistanceRpgGame</c>).
    /// </summary>
    public bool HandleEscape()
    {
        if (_leaving)
            return true;   // on its way out: the host has what it asked for
        var asked = Menu.List?.Asking;
        var list = Menu.List;
        if (!Menu.Escape())
            return false;
        Log.Info(asked is not null ? $"[Title] {Asked(asked)} not taken (Escape)" : list is not null ? $"[Title] Back from the {Named(list)}" : "[Title] Escape");
        return true;
    }

    private void OnKey(Keys key)
    {
        if (key == Keys.Escape) return;   // the game's to route: it asks HandleEscape
        if (_leaving || SceneManager.Instance.IsTransitioning) return;

        var asked = Menu.List?.Asking;
        var step = Menu.Press(key);
        if (asked is not null && step.Action == TitleAction.None)
            Log.Info($"[Title] {Asked(asked)} not taken ({key})");
        else if (asked is null && Menu.List?.Asking is { } asking)
            Log.Info($"[Title] {Asked(asking)}? - ENTER to confirm, any other key to keep what it holds");
        if (Menu.List?.Said is { } said)
            Log.Info($"[Title] {said}");

        switch (step.Action)
        {
            case TitleAction.Load:
                _leaving = true;
                Log.Info($"[Title] Loading {SaveSlots.Label(step.Slot)}");
                _host.LoadSlot(step.Slot);
                break;
            case TitleAction.NewCampaign:
                _leaving = true;
                Log.Info($"[Title] NEW CAMPAIGN in {SaveSlots.Label(step.Slot)}");
                _host.BeginNewCampaign(step.Slot);
                break;
            case TitleAction.Quit:
                _leaving = true;
                Log.Info("[Title] QUIT");
                _host.Quit();
                break;
        }
    }

    /// <summary>An ask, for the log: NEW CAMPAIGN's, over the slot it names — the one there is.</summary>
    private static string Asked(SlotEntry slot) => $"NEW CAMPAIGN over {SaveSlots.Label(slot.Number)} (it holds {(slot.State == SlotState.Unreadable ? "files none of which reads" : "a campaign")})";

    /// <summary>A slot list, for the log: LOAD GAME's, or NEW CAMPAIGN's.</summary>
    private static string Named(SlotMenu list) => list.Mode == SlotMenuMode.NewCampaign ? "NEW CAMPAIGN list" : "load list";
}
