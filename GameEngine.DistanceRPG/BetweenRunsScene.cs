using GameEngine.Core;
using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What the between-runs screen reaches the game through: a dungeon chosen, or
/// a new campaign confirmed. The screen never holds the concrete game that
/// hosts it (§6.7: the campaign outlives every scene, and the scenes are views
/// of it). It is the dungeon list's half of what <see cref="IRunHost"/> is to a
/// dungeon scene.
/// </summary>
public interface IBetweenRunsHost
{
    /// <summary>
    /// The player chose <paramref name="dungeonId"/>, which the campaign lets the
    /// party enter now (<see cref="DungeonListing.Selectable"/>). Begin a run
    /// there. The screen calls this once, never while the engine is still fading
    /// between scenes.
    /// </summary>
    void Descend(string dungeonId);

    /// <summary>
    /// The player confirmed NEW CAMPAIGN (N, then N again): replace the campaign
    /// with a fresh one from the roster, save it over the saved one, and show the
    /// screen between runs again over it. The screen calls this once, never while
    /// the engine is still fading between scenes.
    /// </summary>
    void NewCampaign();
}

/// <summary>
/// The screen between runs (§4.5, §6.7): the hub's dungeon list, and the last
/// run's summary above it. The game opens on it — a resumed campaign's with
/// <c>CAMPAIGN RESUMED</c> under the title — and every run's end comes back to
/// it. The campaign's list decides what is offered
/// (<see cref="CampaignState.Listings"/>). Before the tutorial is beaten that is
/// the tutorial alone, with the warning §4.3 asks for. After, it is every dungeon
/// left, a locked one counting down, and with every one locked, the one offered
/// early. This screen only presents the list and passes the choice on.
/// <para>
/// W/S or the arrows move the choice over the lines that can be entered — a
/// locked line is read, not chosen. ENTER descends into the choice, once, and
/// the host begins the run (<see cref="IBetweenRunsHost.Descend"/>). N arms the
/// NEW CAMPAIGN prompt, and N again confirms it (<see cref="IBetweenRunsHost.NewCampaign"/>);
/// while it is armed the next key is the prompt's alone, and any key but N only
/// disarms it (<see cref="NextPrompt"/>). ESC is the game's: it asks
/// <see cref="HandleEscape"/> first, which takes it only to disarm the prompt,
/// and otherwise the game saves the campaign and quits (<c>DistanceRpgGame</c>).
/// The choice starts on the campaign's own default
/// (<see cref="CampaignState.DefaultNext"/>): the tutorial until it is beaten,
/// then the first dungeon whose lock has lifted, then the early offer.
/// </para>
/// <para>
/// Like every gameplay scene it is single-use: the host registers a fresh one
/// each time the screen is shown. What it reads is the campaign's, which was
/// written when the last run ended, and nothing here writes to it.
/// </para>
/// <para>
/// A screen is handed what the game has to say under its title — the startup's
/// notices on the first one: the save's word on the campaign, then what the data
/// files raised — and, after a save that could not be written, that. The save's
/// word is shown whole — a set-aside notice's full path and how to try the file
/// again, the numbers changed since the save — and what the data files raised is
/// capped after it (<see cref="NoticeText.Rows"/>). The log keeps them all.
/// </para>
/// </summary>
public sealed class BetweenRunsScene : Scene
{
    /// <summary>What one key press does to the NEW CAMPAIGN prompt (<see cref="NextPrompt"/>).</summary>
    internal enum PromptStep
    {
        /// <summary>The prompt is not armed and this key is not N: the screen handles the key as ever.</summary>
        PassOn,

        /// <summary>N, unarmed: the prompt is armed, and the next key is its alone.</summary>
        Arm,

        /// <summary>N, armed: a new campaign.</summary>
        Confirm,

        /// <summary>Any other key, armed: the prompt is disarmed, and the key does nothing else.</summary>
        Disarm,
    }

    private readonly CampaignState _campaign;
    private readonly IBetweenRunsHost _host;
    private readonly BetweenRunsHud _hud;

    private IReadOnlyList<DungeonListing> _listings = [];
    private int _chosen = -1;

    // The host has been told something that ends this screen: a descent or a
    // new campaign. After it every key is ignored while the screen fades out.
    private bool _leaving;

    // The NEW CAMPAIGN prompt is up: the next key is its alone.
    private bool _armed;

    /// <param name="game">The game window: its size and its shaders, for the text.</param>
    /// <param name="campaign">The campaign between its runs: no visit is in progress.</param>
    /// <param name="host">Who is told which dungeon was chosen (<see cref="IBetweenRunsHost.Descend"/>), or that a new campaign was confirmed.</param>
    /// <param name="shownWhole">How many of <paramref name="notices"/>, from the first, are the save's word on the campaign, shown whole (<see cref="StartupNotices.Leading"/>).</param>
    /// <param name="notices">What the game has to say under the title — the save's word and the data files' notices — or none.</param>
    /// <exception cref="ArgumentException">A visit is in progress: its run has to end before the screen between runs can show.</exception>
    public BetweenRunsScene(Game game, CampaignState campaign, IBetweenRunsHost host, int shownWhole, IReadOnlyList<string>? notices = null) : base("BetweenRunsScene")
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(host);
        if (campaign.Visit is { } visit)
            throw new ArgumentException($"A visit to '{visit.Def.Id}' is in progress: the screen between runs shows only once its run has ended.", nameof(campaign));
        _campaign = campaign;
        _host = host;
        _hud = new BetweenRunsHud(game);
        NoticeRows = NoticeText.Rows(notices ?? [], shownWhole);
    }

    /// <summary>The notices as the screen draws them under its title (<see cref="NoticeText.Rows"/>): wrapped, the save's word whole and the rest capped, or none.</summary>
    internal IReadOnlyList<string> NoticeRows { get; }

    /// <summary>The dungeon list, as the campaign gave it when the screen opened.</summary>
    public IReadOnlyList<DungeonListing> Listings => _listings;

    /// <summary>The index of the listing ENTER descends into, or -1 when there is none to enter.</summary>
    public int Chosen => _chosen;

    /// <summary>How the last run ended, which the screen shows above the list, or null before the first.</summary>
    public RunResult? LastResult => _campaign.LastResult;

    /// <summary>Whether the NEW CAMPAIGN prompt is up, which the screen asks along its bottom in place of its keys.</summary>
    internal bool NewCampaignArmed => _armed;

    public override void Initialize()
    {
        base.Initialize();

        _listings = _campaign.Listings();
        _chosen = FirstChoice(_listings, DefaultNextOrNull(_campaign));
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
    /// Every key press, once, through one handler: the prompt reads it first, so
    /// the N that arms it is never also the key that disarms it, and a key the
    /// prompt takes never also moves the choice or descends. KeyPressed fires once
    /// per physical press, so a held key acts once.
    /// </summary>
    private void WireInput() => Input!.SubscribeToKeyPressed(e => OnKey(e.Key));

    private void OnKey(Keys key)
    {
        if (key == Keys.Escape) return;   // the game's to route: it asks HandleEscape
        if (_leaving || SceneManager.Instance.IsTransitioning) return;

        switch (NextPrompt(_armed, key))
        {
            case PromptStep.Arm:
                _armed = true;
                Log.Info("[Hub] NEW CAMPAIGN? - N again to confirm, any other key to keep the campaign");
                return;
            case PromptStep.Confirm:
                _armed = false;
                _leaving = true;
                Log.Info("[Hub] NEW CAMPAIGN confirmed");
                _host.NewCampaign();
                return;
            case PromptStep.Disarm:
                _armed = false;
                Log.Info($"[Hub] NEW CAMPAIGN not taken ({key}): the campaign stays");
                return;
        }

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
    /// What one key press does to the NEW CAMPAIGN prompt (1f): unarmed, N arms
    /// it and every other key passes on to the screen — ENTER descends, ESC saves
    /// and quits. Armed, N confirms, and every other key — ENTER, the arrows and
    /// ESC included — disarms it and is spent doing so. Asking twice is the whole
    /// guard: the saved campaign is replaced.
    /// </summary>
    internal static PromptStep NextPrompt(bool armed, Keys key)
        => armed
            ? key == Keys.N ? PromptStep.Confirm : PromptStep.Disarm
            : key == Keys.N ? PromptStep.Arm : PromptStep.PassOn;

    /// <summary>
    /// ESC, asked by the game before it does anything else with it: true when
    /// ESC disarmed the NEW CAMPAIGN prompt and is spent; false otherwise, and
    /// the game saves the campaign and quits.
    /// </summary>
    public bool HandleEscape()
    {
        if (NextPrompt(_armed, Keys.Escape) != PromptStep.Disarm)
            return false;
        _armed = false;
        Log.Info("[Hub] NEW CAMPAIGN not taken (Escape): the campaign stays");
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
