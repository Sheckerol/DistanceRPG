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
/// everything it has met, and every number a run carries out (the run counter,
/// the locks, the cleared and retired dungeons) — which every run is played by
/// and which outlives every scene (Phase 5 will load and save it). A run is a
/// <see cref="DungeonScene"/> over the campaign's visit: <see cref="BeginRun"/>
/// rolls the visit's seed — out here, never in <c>Logic/</c> — enters the
/// dungeon, and hands the scene a view of it; the run ends in the campaign
/// (<see cref="CampaignState.EndRun"/>), the scene shows its summary, and tells
/// the game when the player moves on (<see cref="IRunHost.RunOver"/>), and the
/// game begins the next one in the campaign's next dungeon
/// (<see cref="CampaignState.DefaultNext"/>).
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

    /// <summary>
    /// A dev override: set to a map seed — a whole number from 0 to 4294967295,
    /// the range a run's seed is rolled in — the boot run enters its dungeon on
    /// that seed instead of a rolled one, so a seed a log or a test names can be
    /// played: <c>DISTANCERPG_SEED=7</c> boots a tutorial whose golem drops The
    /// Bulwark. The boot run's alone — every run after it rolls its own, as every
    /// entry must (a failed dungeon's depth re-rolls). Anything that is not such a
    /// number is ignored, with a warning.
    /// </summary>
    internal const string SeedOverrideVariable = "DISTANCERPG_SEED";

    private CampaignState? _campaign;

    // The boot run's seed when the override named one, spent by the first
    // BeginRun that actually enters a dungeon.
    private long? _bootSeed;

    // A run asked for while the engine was mid-transition, begun once it is not.
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

        string? seedText = Environment.GetEnvironmentVariable(SeedOverrideVariable);
        _bootSeed = BootSeed(seedText);
        if (!string.IsNullOrWhiteSpace(seedText) && _bootSeed is null)
            Log.Warn($"[Run] {SeedOverrideVariable}='{seedText}' is not a map seed (a whole number from 0 to {uint.MaxValue}) - rolling one");
        BeginRun(boot);
    }

    /// <summary>
    /// The boot run's map seed: <paramref name="requested"/> — the
    /// <see cref="SeedOverrideVariable"/> override, surrounding whitespace ignored —
    /// when it is a whole number a run's seed can be (0 to 4294967295, the range
    /// <see cref="BeginRun"/> rolls in), and null otherwise, which rolls one.
    /// </summary>
    internal static long? BootSeed(string? requested)
        => uint.TryParse(requested?.Trim(), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out uint seed)
            ? seed
            : null;

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
    /// The run the current scene was playing is over, and its summary has been
    /// read: the campaign has already ended it (<see cref="CampaignState.EndRun"/>
    /// — the outcome, the tick, the lock or the retirement written, the visit
    /// discarded whole), so the next run begins, in the campaign's next dungeon
    /// (<see cref="CampaignState.DefaultNext"/>: the tutorial until it is beaten,
    /// then the first dungeon whose lock has lifted, and with every one locked the
    /// early offer), on a new seed, with the party rested. The between-runs screen
    /// takes this over when it exists.
    /// </summary>
    public void RunOver()
    {
        var campaign = _campaign ?? throw new InvalidOperationException("No campaign: the game has not loaded.");
        BeginRun(campaign.DefaultNext());
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
        long mapSeed = _bootSeed ?? (uint)Random.Shared.NextInt64();
        _bootSeed = null;   // the override is the boot run's alone
        var visit = campaign.Enter(dungeonId, mapSeed);
        Log.Info($"[Run] Entering {visit.Def.Name} ({visit.Def.Id}) on map seed {mapSeed} - run counter {campaign.RunCounter}");

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
    /// menu there — whose EXIT is how the game is quit — and does nothing on a
    /// run's summary, where ENTER is the one way on; only what a scene leaves
    /// unconsumed quits. Mid-transition between scenes it does nothing at all,
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
