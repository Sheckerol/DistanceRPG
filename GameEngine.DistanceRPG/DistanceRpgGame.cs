using GameEngine.Core;
using GameEngine.Core.Diagnostics;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Turn-based party RPG ported from the original Phaser prototype.
/// <para>
/// <strong>The game owns the campaign</strong> (§6.7): one
/// <see cref="CampaignState"/> for the process — the party, built once, and
/// everything it has met — which every run is played by and which outlives every
/// scene (Phase 5 will load and save it). A run is a <see cref="DungeonScene"/>
/// over the campaign's visit: <see cref="BeginRun"/> rolls the visit's seed —
/// out here, never in <c>Logic/</c> — enters the dungeon, and hands the scene a
/// view of it; the scene tells the game when the run is over
/// (<see cref="IRunHost.RunOver"/>), and the game begins the next one.
/// </para>
/// <para>
/// Gameplay scenes are single-use (the engine disposes one for good when it is
/// shut down), so every run gets a fresh scene, registered in place of the last
/// and faded to by the engine's own transition. No engine change: unregister,
/// register, transition — never while a transition is already running.
/// </para>
/// </summary>
public class DistanceRpgGame : Game, IRunHost
{
    /// <summary>The one name the current run's scene is registered under.</summary>
    private const string DungeonSceneName = "DungeonScene";

    /// <summary>
    /// A dev override: set to a dungeon's id (<c>spear_hall</c>, say), the game
    /// boots straight into that dungeon rather than the tutorial — how a five-
    /// to ten-floor dungeon is playtested before the between-runs screen exists
    /// to choose one. An id that names no dungeon is ignored, with a warning.
    /// </summary>
    internal const string DungeonOverrideVariable = "DISTANCERPG_DUNGEON";

    private CampaignState? _campaign;

    // The dungeon the current run is in, for RunOver to begin again; and a run
    // asked for while the engine was mid-transition, begun once it is not.
    private string? _runDungeonId;
    private string? _pendingRun;

    public DistanceRpgGame()
    {
        // Esc quits only if the current scene doesn't consume it (menus). KeyPressed
        // is edge-only, so holding Esc doesn't flicker the pause menu.
        _inputEventManager.SubscribeToKeyPressed(_ => OnEscape(), Keys.Escape);
    }

    protected override void OnLoad()
    {
        base.OnLoad(); // _camera is created here

        // Black background so the void beyond the map reads as unexplored fog.
        GL.ClearColor(0f, 0f, 0f, 1f);

        var content = GameContent.Current;
        _campaign = CampaignState.Start(content);

        string? requested = Environment.GetEnvironmentVariable(DungeonOverrideVariable);
        string boot = BootDungeonId(requested, content.Dungeons);
        if (!string.IsNullOrWhiteSpace(requested) && boot != requested.Trim())
            Log.Warn($"[Run] {DungeonOverrideVariable}='{requested}' names no dungeon (there are: "
                + string.Join(", ", content.Dungeons.All.Select(d => d.Id)) + $") - booting {boot}");
        BeginRun(boot);
    }

    /// <summary>
    /// The dungeon the game boots into: <paramref name="requested"/> — the
    /// <see cref="DungeonOverrideVariable"/> override, surrounding whitespace
    /// ignored — when it names one of <paramref name="dungeons"/>, and the
    /// tutorial otherwise, which is where a campaign begins.
    /// </summary>
    internal static string BootDungeonId(string? requested, DungeonCatalogue dungeons)
    {
        ArgumentNullException.ThrowIfNull(dungeons);
        if (!string.IsNullOrWhiteSpace(requested) && dungeons.TryGet(requested.Trim(), out var def))
            return def.Id;
        return dungeons.Tutorial.Id;
    }

    /// <summary>
    /// The run the current scene was playing is over (the party left by the
    /// way out): the visit is discarded whole — every floor, every row, every
    /// farm (§4.1) — and a fresh visit to the same dungeon begins, on a new
    /// seed, with the party rested. What happens here grows with the run's end
    /// (its outcomes, the next dungeon) and the between-runs screen.
    /// </summary>
    public void RunOver()
    {
        var campaign = _campaign ?? throw new InvalidOperationException("No campaign: the game has not loaded.");
        string dungeonId = campaign.Visit?.Def.Id ?? _runDungeonId ?? GameContent.Current.Dungeons.Tutorial.Id;
        campaign.Abandon();
        BeginRun(dungeonId);
    }

    /// <summary>
    /// Begin a run in <paramref name="dungeonId"/>: roll the visit's seed and
    /// log it (any run can be reproduced from the log), enter the dungeon — the
    /// party rested and stood at its first floor's arrival — and fade to a fresh
    /// scene playing it. Asked for mid-transition, it waits for the engine to
    /// finish rather than start a second one on top of it.
    /// </summary>
    private void BeginRun(string dungeonId)
    {
        var scenes = SceneManager.Instance;
        if (scenes.IsTransitioning)
        {
            _pendingRun = dungeonId;
            return;
        }

        var campaign = _campaign ?? throw new InvalidOperationException("No campaign: the game has not loaded.");
        long mapSeed = (uint)Random.Shared.NextInt64();
        var visit = campaign.Enter(dungeonId, mapSeed);
        _runDungeonId = visit.Def.Id;
        Log.Info($"[Run] Entering {visit.Def.Name} ({visit.Def.Id}) on map seed {mapSeed}");

        var scene = new DungeonScene(_camera!, this, campaign, this);
        if (scenes.HasScene(DungeonSceneName))
            scenes.UnregisterScene(DungeonSceneName);   // the last run's scene stays loaded until the fade swaps it out
        scenes.RegisterScene(DungeonSceneName, scene);
        scenes.TransitionToScene(DungeonSceneName);
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        if (_pendingRun is { } dungeonId && !SceneManager.Instance.IsTransitioning)
        {
            _pendingRun = null;
            BeginRun(dungeonId);
        }
    }

    /// <summary>
    /// Escape goes to the scene being played: it closes a menu or opens the pause
    /// menu there, and only what the scene leaves unconsumed — the game-over
    /// screen's — quits. Mid-transition between scenes it does nothing at all,
    /// neither scene's to answer and never a quit on the way into a run.
    /// </summary>
    private void OnEscape()
    {
        var scenes = SceneManager.Instance;
        if (scenes.IsTransitioning) return;
        if ((scenes.CurrentScene as DungeonScene)?.HandleEscape() != true)
            Close();
    }
}
