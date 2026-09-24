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
/// and which outlives every scene (Phase 5 will load and save it).
/// </para>
/// <para>
/// <strong>Two scenes take turns.</strong> The game opens on the screen between
/// runs (<see cref="BetweenRunsScene"/>): the hub's dungeon list, which offers
/// the tutorial alone until it is beaten. A dungeon chosen there
/// (<see cref="IBetweenRunsHost.Descend"/>) is a run: <see cref="BeginRun"/>
/// rolls the visit's seed — out here, never in <c>Logic/</c> — enters the
/// dungeon, and hands a <see cref="DungeonScene"/> a view of it. The run ends in
/// the campaign (<see cref="CampaignState.EndRun"/>), the scene shows its
/// summary, and when the player moves on it tells the game
/// (<see cref="IRunHost.RunOver"/>). The game goes back to the screen between
/// runs, where the last run's summary sits above the list and the next dungeon
/// is chosen.
/// </para>
/// <para>
/// Gameplay scenes are single-use (the engine disposes one for good when it is
/// shut down), so each showing of either scene gets a fresh instance, registered
/// in place of the last and faded to by the engine's own transition. No engine
/// change: unregister, register, transition — never while a transition is
/// already running.
/// </para>
/// <para>
/// <strong>The numbers are read once, here, at startup</strong> (§5.3): the data
/// files beside the executable (<see cref="DataFiles"/>, <c>tuning.json</c>
/// first) are loaded into the content the whole session plays under
/// (<see cref="ContentFiles.Load"/>), made current before the campaign is built
/// from it, and never read again — a run resolves under one set of numbers.
/// Every path read or missed, every number the file changed and every notice the
/// load raised goes to the log; the notices also go to whichever scene the game
/// opens on first, so a playtest never runs under numbers it did not mean
/// without saying so on screen.
/// </para>
/// </summary>
public class DistanceRpgGame : Game, IRunHost, IBetweenRunsHost
{
    /// <summary>The one name the current run's scene is registered under.</summary>
    private const string DungeonSceneName = "DungeonScene";

    /// <summary>The one name the screen between runs is registered under.</summary>
    private const string BetweenRunsSceneName = "BetweenRunsScene";

    /// <summary>
    /// A dev override: set to a dungeon's id (<c>spear_hall</c>, say), the game
    /// boots straight into that dungeon, past the screen between runs, which is
    /// how a five- to ten-floor dungeon is playtested without first beating the
    /// tutorial that opens the hub. The run it boots ends back on that screen
    /// like any other. An id that names no dungeon is ignored, with a warning,
    /// and the game opens on the screen between runs as usual.
    /// </summary>
    internal const string DungeonOverrideVariable = "DISTANCERPG_DUNGEON";

    /// <summary>
    /// A dev override: set to a map seed — a whole number from 0 to 4294967295,
    /// the range a run's seed is rolled in — the first run the game enters is on
    /// that seed instead of a rolled one, so a seed a log or a test names can be
    /// played. With <c>DISTANCERPG_SEED=7</c>, the tutorial chosen first is one
    /// whose golem drops The Bulwark. It is the first run's alone: every run after
    /// it rolls its own, as every entry must (a failed dungeon's depth re-rolls).
    /// Anything that is not such a number is ignored, with a warning.
    /// </summary>
    internal const string SeedOverrideVariable = "DISTANCERPG_SEED";

    private CampaignState? _campaign;

    // What the data files raised at startup — a tuning value clamped or fitted,
    // a file missing or unreadable — handed to the first scene the game opens on
    // and to no other. The log has them all.
    private readonly StartupNotices _startupNotices = new();

    // The boot seed when the override named one, spent by the first BeginRun
    // that actually enters a dungeon.
    private long? _bootSeed;

    // A scene asked for while the engine was mid-transition — a run, or the
    // screen between runs — shown once it is not.
    private Action? _pendingScene;

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

        if (!LoadData())
        {
            Close();   // the content does not load: nothing to play, and it is in the log why
            return;
        }

        var content = GameContent.Current;
        _campaign = CampaignState.Start(content);

        string? requested = Environment.GetEnvironmentVariable(DungeonOverrideVariable);
        bool straightIn = BootsIntoADungeon(requested, content.Dungeons);
        if (!string.IsNullOrWhiteSpace(requested) && !straightIn)
            Log.Warn($"[Run] {DungeonOverrideVariable}='{requested}' names no dungeon (there are: "
                + string.Join(", ", content.Dungeons.All.Select(d => d.Id)) + ") - opening the screen between runs");

        string? seedText = Environment.GetEnvironmentVariable(SeedOverrideVariable);
        _bootSeed = BootSeed(seedText);
        if (!string.IsNullOrWhiteSpace(seedText) && _bootSeed is null)
            Log.Warn($"[Run] {SeedOverrideVariable}='{seedText}' is not a map seed (a whole number from 0 to {uint.MaxValue}) - rolling one");

        if (straightIn)
            BeginRun(BootDungeonId(requested, content.Dungeons));
        else
            ShowBetweenRuns();
    }

    /// <summary>
    /// Read the data files beside the executable, once (§5.3: never
    /// hot-reloaded), and make what they load the content the session plays
    /// under. The log gets every path read or missed, every notice, the numbers
    /// the tuning changed and the tuning's hash; the notices wait for the first
    /// scene. False — logged, with nothing made current — when the content does
    /// not load, which no <c>tuning.json</c> can cause: every tuning a file can
    /// make is clamped and fitted to the content it plays beside.
    /// </summary>
    private bool LoadData()
    {
        string directory = AppContext.BaseDirectory;
        var load = ContentFiles.Load(DataFiles.Read(directory, line => Log.Info(line)));
        foreach (string notice in load.Notices)
            Log.Warn($"[Data] {notice}");
        Log.Info(DataFiles.TuningSummary(directory, load.TuningOverrides));

        if (load.Content is not { } content)
        {
            var error = load.Error;
            Log.Error($"[Data] The content does not load - {error?.Message ?? "no content and no reason"}");
            return false;
        }

        GameContent.Use(content);
        Log.Info($"[Data] Playing under tuning {TuningJson.Hash(content.Tuning)}");
        _startupNotices.Hold(load.Notices);
        return true;
    }

    /// <summary>
    /// The first run's map seed: <paramref name="requested"/> — the
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
    /// Whether the game boots straight into a dungeon, past the screen between
    /// runs. It does when <paramref name="requested"/>, the
    /// <see cref="DungeonOverrideVariable"/> override with surrounding whitespace
    /// ignored, names one of <paramref name="dungeons"/>. Otherwise the game opens
    /// on the screen between runs, which is where a campaign begins.
    /// </summary>
    internal static bool BootsIntoADungeon(string? requested, DungeonCatalogue dungeons)
    {
        ArgumentNullException.ThrowIfNull(dungeons);
        return !string.IsNullOrWhiteSpace(requested) && dungeons.TryGet(requested.Trim(), out _);
    }

    /// <summary>
    /// The dungeon a campaign's first run goes into: <paramref name="requested"/>
    /// — the <see cref="DungeonOverrideVariable"/> override, surrounding whitespace
    /// ignored — when it names one of <paramref name="dungeons"/>, and the game
    /// boots straight into it (<see cref="BootsIntoADungeon"/>). Otherwise it is
    /// the tutorial, which is where a campaign begins: the screen between runs
    /// offers nothing else until the tutorial is beaten.
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
    /// read. The campaign has already ended it (<see cref="CampaignState.EndRun"/>):
    /// the outcome, the tick, the lock or the retirement are written and the
    /// visit is discarded whole. So the game goes back to the screen between
    /// runs, where the last run's summary sits above the dungeon list and the
    /// next dungeon is chosen.
    /// </summary>
    public void RunOver() => ShowBetweenRuns();

    /// <summary>A dungeon was chosen on the screen between runs: begin a run in it.</summary>
    public void Descend(string dungeonId)
    {
        ArgumentNullException.ThrowIfNull(dungeonId);
        BeginRun(dungeonId);
    }

    /// <summary>
    /// Fade to a fresh screen between runs, over the campaign as it now stands:
    /// the hub's dungeon list and the last run's summary. Asked for
    /// mid-transition, it waits for the engine to finish rather than start a
    /// second transition on top of it.
    /// </summary>
    private void ShowBetweenRuns()
    {
        if (SceneManager.Instance.IsTransitioning)
        {
            _pendingScene = ShowBetweenRuns;
            return;
        }

        var campaign = _campaign ?? throw new InvalidOperationException("No campaign: the game has not loaded.");
        SwapTo(BetweenRunsSceneName, new BetweenRunsScene(this, campaign, this, _startupNotices.Take()));
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
        if (SceneManager.Instance.IsTransitioning)
        {
            _pendingScene = () => BeginRun(dungeonId);
            return;
        }

        var campaign = _campaign ?? throw new InvalidOperationException("No campaign: the game has not loaded.");
        long mapSeed = _bootSeed ?? (uint)Random.Shared.NextInt64();
        _bootSeed = null;   // the override is the first run's alone
        var visit = campaign.Enter(dungeonId, mapSeed);
        Log.Info($"[Run] Entering {visit.Def.Name} ({visit.Def.Id}) on map seed {mapSeed} - run counter {campaign.RunCounter}");

        SwapTo(DungeonSceneName, new DungeonScene(_camera!, this, campaign, this, _startupNotices.Take()));
    }

    /// <summary>
    /// Register <paramref name="scene"/> under <paramref name="name"/> in place
    /// of the last scene registered there, and fade to it. A scene that is
    /// current stays loaded until the fade swaps it out, which is the scene
    /// manager's own contract.
    /// </summary>
    private static void SwapTo(string name, Scene scene)
    {
        var scenes = SceneManager.Instance;
        if (scenes.HasScene(name))
            scenes.UnregisterScene(name);
        scenes.RegisterScene(name, scene);
        scenes.TransitionToScene(name);
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        if (_pendingScene is { } pending && !SceneManager.Instance.IsTransitioning)
        {
            _pendingScene = null;
            pending();
        }
    }

    /// <summary>
    /// Escape goes to the scene being played. In a dungeon it closes a menu or
    /// opens the pause menu, whose EXIT is how a run is quit, and it does nothing
    /// on a run's summary, where ENTER is the one way on. The screen between runs
    /// consumes none of it, so there ESC quits the game. Only what a scene leaves
    /// unconsumed quits. Mid-transition between scenes it does nothing at all,
    /// neither scene's to answer and never a quit on the way in or out of a run.
    /// </summary>
    private void OnEscape()
    {
        var scenes = SceneManager.Instance;
        if (scenes.IsTransitioning) return;
        if ((scenes.CurrentScene as DungeonScene)?.HandleEscape() != true)
            Close();
    }
}
