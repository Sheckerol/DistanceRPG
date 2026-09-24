using GameEngine.Core;
using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What the between-runs screen reaches the game through when a dungeon is
/// chosen. It is one method, so the screen never holds the concrete game that
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
}

/// <summary>
/// The screen between runs (§4.5, §6.7): the hub's dungeon list, and the last
/// run's summary above it. The game opens on it, and every run's end comes back
/// to it. The campaign's list decides what is offered
/// (<see cref="CampaignState.Listings"/>). Before the tutorial is beaten that is
/// the tutorial alone, with the warning §4.3 asks for. After, it is every dungeon
/// left, a locked one counting down, and with every one locked, the one offered
/// early. This screen only presents the list and passes the choice on.
/// <para>
/// W/S or the arrows move the choice over the lines that can be entered — a
/// locked line is read, not chosen. ENTER descends into the choice, once, and
/// the host begins the run (<see cref="IBetweenRunsHost.Descend"/>). ESC is not
/// consumed here, so the game quits (<c>DistanceRpgGame</c>). The choice starts
/// on the campaign's own default (<see cref="CampaignState.DefaultNext"/>): the
/// tutorial until it is beaten, then the first dungeon whose lock has lifted,
/// then the early offer.
/// </para>
/// <para>
/// Like every gameplay scene it is single-use: the host registers a fresh one
/// each time the screen is shown. What it reads is the campaign's, which was
/// written when the last run ended, and nothing here writes to it.
/// </para>
/// </summary>
public sealed class BetweenRunsScene : Scene
{
    private readonly CampaignState _campaign;
    private readonly IBetweenRunsHost _host;
    private readonly BetweenRunsHud _hud;

    private IReadOnlyList<DungeonListing> _listings = [];
    private int _chosen = -1;
    private bool _descendSent;

    /// <param name="game">The game window: its size and its shaders, for the text.</param>
    /// <param name="campaign">The campaign between its runs: no visit is in progress.</param>
    /// <param name="host">Who is told which dungeon was chosen (<see cref="IBetweenRunsHost.Descend"/>).</param>
    /// <exception cref="ArgumentException">A visit is in progress: its run has to end before the screen between runs can show.</exception>
    public BetweenRunsScene(Game game, CampaignState campaign, IBetweenRunsHost host) : base("BetweenRunsScene")
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(host);
        if (campaign.Visit is { } visit)
            throw new ArgumentException($"A visit to '{visit.Def.Id}' is in progress: the screen between runs shows only once its run has ended.", nameof(campaign));
        _campaign = campaign;
        _host = host;
        _hud = new BetweenRunsHud(game);
    }

    /// <summary>The dungeon list, as the campaign gave it when the screen opened.</summary>
    public IReadOnlyList<DungeonListing> Listings => _listings;

    /// <summary>The index of the listing ENTER descends into, or -1 when there is none to enter.</summary>
    public int Chosen => _chosen;

    /// <summary>How the last run ended, which the screen shows above the list, or null before the first.</summary>
    public RunResult? LastResult => _campaign.LastResult;

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

    private void WireInput()
    {
        var input = Input!;
        // KeyPressed fires once per physical press, so a held key moves the choice once.
        input.SubscribeToKeyPressed(_ => Move(-1), Keys.W, Keys.Up);
        input.SubscribeToKeyPressed(_ => Move(+1), Keys.S, Keys.Down);
        input.SubscribeToKeyPressed(_ => Descend(), Keys.Enter, Keys.KeyPadEnter);
    }

    /// <summary>Move the choice to the next line that can be entered, up or down, round the end of the list.</summary>
    private void Move(int step)
    {
        if (_descendSent) return;
        _chosen = NextChoice(_listings, _chosen, step);
    }

    /// <summary>
    /// ENTER: descend into the chosen dungeon, once. The host enters it and fades
    /// to the run. Never while the engine is still fading between scenes, and
    /// never twice: after the first press the screen is on its way out.
    /// </summary>
    private void Descend()
    {
        if (_descendSent || SceneManager.Instance.IsTransitioning) return;
        if (_chosen < 0 || _chosen >= _listings.Count || !_listings[_chosen].Selectable) return;

        var choice = _listings[_chosen];
        _descendSent = true;
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
