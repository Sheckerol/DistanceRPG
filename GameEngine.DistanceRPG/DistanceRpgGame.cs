using System.ComponentModel;
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
/// <see cref="CampaignState"/> in play — the party, built once, and everything it
/// has met, and every number a run carries out (the run counter, the locks, the
/// cleared and retired dungeons) — which every run is played by and which
/// outlives every scene. It is saved on its own, and the title continues it
/// (below).
/// </para>
/// <para>
/// <strong>The game opens on the title</strong> (<see cref="TitleScene"/>; the
/// user's ruling of 2026-09-25 on the save model, R2(a)): CONTINUE, NEW CAMPAIGN,
/// LOAD GAME, QUIT. No campaign is in play there: one comes into play when a slot
/// loads (<see cref="LoadSlot"/>) or a new one begins
/// (<see cref="BeginNewCampaign(string)"/>), and QUIT closes the game
/// (<see cref="Quit"/>). A dev-override launch has no title — it boots straight
/// into its scratch campaign — and <c>--new-campaign</c> begins a campaign at
/// launch, straight to the screen between runs.
/// </para>
/// <para>
/// <strong>Then two scenes take turns.</strong> The screen between runs
/// (<see cref="BetweenRunsScene"/>): the hub's dungeon list, which offers the
/// tutorial alone until it is beaten. A dungeon chosen there
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
/// first) are loaded over the copies the game carries built in — the master
/// copies, each the fallback for its file (<see cref="ShippedData.Load"/>) —
/// into the content the whole session plays under, made current before the
/// campaign is built from it, and never read again — a run resolves under one
/// set of numbers.
/// Every path read or missed, every number the file changed and every notice the
/// load raised goes to the log; the notices also go to whichever scene the game
/// opens on first — the title, as a rule — so a playtest never runs under numbers
/// it did not mean without saying so on screen. A content file that is there and broken stops
/// the game (§5.4): no campaign is built, and the game opens on the screen that
/// names the file, the entry and the rule (<see cref="StartupErrorScene"/>),
/// where ESC quits.
/// </para>
/// <para>
/// <strong>The campaign saves itself, and the title continues it</strong> (§5.1,
/// 1f; <see cref="CampaignBoot"/>, <see cref="SaveStore"/>, <see cref="SaveSlots"/>,
/// <see cref="SaveListing"/>). One campaign in play, saved into the autosave slot
/// — the player's save folder itself (<c>DISTANCERPG_SAVE_DIR</c> moves it), whose
/// manual slots, <c>slot-1</c> to <c>slot-N</c>, sit beside it (R2). The title
/// lists the slots (<see cref="ShowTitle"/>): CONTINUE loads the most recent save,
/// LOAD GAME any slot; no play writes a manual slot yet. Whatever slot a campaign
/// was loaded from, it autosaves into the autosave slot — a manual slot's campaign
/// making room there first, the autosave's own files set aside after the title
/// asked (<see cref="CampaignBoot.MakeRoom"/>; USER DECISION 2). Every save carries
/// its time, from the clock here (<see cref="_clock"/>), never read in
/// <c>Logic/</c>. It is written only at a safe point
/// (<see cref="SaveGame.IsSafePoint"/>) and only once the frame a save was asked
/// for in is over, so no write runs inside a turn system's raise: when a run is
/// entered, at every player turn's start and after every floor arrival (the
/// scene asks, <see cref="IRunHost.RequestSave"/>), at a run's end, when NEW
/// CAMPAIGN begins one — on the title, or <c>--new-campaign</c> at launch — and on
/// quitting — the pause menu's <c>2  SAVE AND QUIT</c>, ESC on the screen between
/// runs, or the window's close button, all of which arrive at
/// <see cref="OnClosing"/>. A quit during the enemies' turn plays it out to the
/// next player turn first, on the playing scene's own logic step
/// (<see cref="CampaignBoot.SaveOnQuit"/>), and saves the turn the player would
/// have met. A load resumes the campaign where it stood: on the screen between
/// runs, <c>CAMPAIGN RESUMED</c> under the title, or in the dungeon at the saved
/// floor and turn, <c>RESUMED</c> over the floor banner (<see cref="ResumeRun"/>);
/// one that brings nothing back goes back to the title, saying why. A save that
/// cannot be read, or whose resume crashed the game, is set aside where the
/// notice says. A write that fails is a log line and a notice — at once over the
/// floor being played, and on the next screen until a later save goes through —
/// never a crash, and never costs the save before it, nor a file of the slot that
/// cannot be read, which the write sets aside first and says so; a quit that
/// cannot save leaves a marker, and the title says, the next time it opens, that
/// what the autosave holds is from before that quit. A dev-override launch plays a
/// fresh campaign in a slot of its own and never touches the campaign.
/// </para>
/// </summary>
public class DistanceRpgGame : Game, IRunHost, IBetweenRunsHost, ITitleHost
{
    /// <summary>The one name the current run's scene is registered under.</summary>
    private const string DungeonSceneName = "DungeonScene";

    /// <summary>The one name the screen between runs is registered under.</summary>
    private const string BetweenRunsSceneName = "BetweenRunsScene";

    /// <summary>The one name the title is registered under.</summary>
    private const string TitleSceneName = "TitleScene";

    /// <summary>The name the screen a broken content file stops the game on is registered under.</summary>
    private const string StartupErrorSceneName = "StartupErrorScene";

    /// <summary>
    /// A dev override: set to a dungeon's id (<c>spear_hall</c>, say), the game
    /// boots straight into that dungeon, past the title and the screen between
    /// runs, which is how a five- to ten-floor dungeon is playtested without first
    /// beating the tutorial that opens the hub. The run it boots ends back on the
    /// screen between runs like any other. It plays a fresh campaign in the save
    /// folder's <c>override</c> slot, emptied at every such launch, and never reads
    /// or writes the campaign's own saves; it has no title. An id that names no
    /// dungeon is ignored, with a warning, and the game opens on the title as usual.
    /// </summary>
    internal const string DungeonOverrideVariable = "DISTANCERPG_DUNGEON";

    /// <summary>
    /// A dev override: set to a map seed — a whole number from 0 to 4294967295,
    /// the range a run's seed is rolled in — the first run the game enters is on
    /// that seed instead of a rolled one, so a seed a log or a test names can be
    /// played. With <c>DISTANCERPG_SEED=7</c>, the tutorial chosen first is one
    /// whose golem drops The Bulwark. It is the first run's alone: every run after
    /// it rolls its own, as every entry must (a failed dungeon's depth re-rolls).
    /// Like the dungeon override, it plays a fresh campaign in the override slot,
    /// with no title: the game opens on the screen between runs.
    /// Anything that is not such a number is ignored, with a warning.
    /// </summary>
    internal const string SeedOverrideVariable = "DISTANCERPG_SEED";

    /// <summary>What the command line asked for: <c>--new-campaign</c>, here.</summary>
    private readonly LaunchOptions _launch;

    private CampaignState? _campaign;

    // The clock a save's time is read from: the edge's, never Logic's.
    private readonly Func<DateTimeOffset> _clock = () => DateTimeOffset.UtcNow;

    // The save folder's slots: the autosave and the manual ones. Never made on a
    // dev-override launch, whose saves all go to its own slot; null too when the
    // content did not load.
    private SaveSlots? _slots;

    // Where this session's campaign autosaves — at a run's entry, every turn's
    // start, every arrival, a run's end and every quit: the autosave slot's store,
    // or the dev override's own. Null only when the content did not load.
    private SaveStore? _autosave;

    // The slot this session's campaign was loaded from, whose resume marker is
    // cleared once the loaded screen is up: the slot the title loaded, the
    // autosave's for a new campaign, or the dev override's.
    private SaveStore? _loadedFrom;

    // The slots as the title last listed them: what its asks were put on, and so
    // what a load it chose reads NeedsRoom from — the one question both read.
    private SaveListing? _listing;

    // What the game has to say on the next screen it shows: the data files'
    // notices and the save's at startup, a save that could not be written later.
    // The log has them all.
    private readonly StartupNotices _notices = new();

    // The notices of the saves that failed since the last one that went through,
    // led for the next screen: a save that goes through takes them back, since the
    // disk holds the campaign again and they are no longer true.
    private readonly List<string> _failedSaves = new();

    // The boot seed when the override named one, spent by the first BeginRun
    // that actually enters a dungeon.
    private long? _bootSeed;

    // A scene asked for while the engine was mid-transition — a run, or the
    // screen between runs — shown once it is not.
    private Action? _pendingScene;

    // A save was asked for since the last one — a run entered or ended, a player
    // turn begun, a floor arrived on: written at the end of the frame, once no
    // raise is running, and not before the campaign stands at a safe point.
    private bool _saveRequested;

    // A resumed campaign whose first screen is not up yet: the resume marker is
    // cleared once it is.
    private bool _resuming;

    // The quit's save has been written: a close is saved once, however it came.
    private bool _quitSaved;

    public DistanceRpgGame()
        : this(new LaunchOptions(WriteDataDirectory: null, NewCampaign: false))
    {
    }

    /// <param name="launch">What the command line asked for (<see cref="Program.Parse"/>).</param>
    internal DistanceRpgGame(LaunchOptions launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        _launch = launch;

        // Esc goes to the scene first (menus, the title's asks and its load list),
        // and quits only if it is left unconsumed. KeyPressed is edge-only, so
        // holding Esc doesn't flicker the pause menu.
        _inputEventManager.SubscribeToKeyPressed(_ => OnEscape(), Keys.Escape);
    }

    protected override void OnLoad()
    {
        base.OnLoad(); // _camera is created here

        // Black background so the void beyond the map reads as unexplored fog.
        GL.ClearColor(0f, 0f, 0f, 1f);

        if (!LoadData())
            return;   // the content does not load: the screen says why, no campaign is built, and ESC quits

        var content = GameContent.Current;
        BootCampaign(content, LaunchRequest(content));   // and the first scene: the title, as a rule
    }

    /// <summary>
    /// Read the data files beside the executable, once (§5.3: never
    /// hot-reloaded), over the copies the game carries built in, and make what
    /// they load the content the session plays under. The log gets every path
    /// read or missed, every notice, the numbers the tuning changed and the
    /// tuning's hash; the notices wait for the first scene. False when the
    /// content does not load — a content file that is there and broken (§5.4:
    /// "invalid content aborts startup, naming the entry and the rule it
    /// broke"), or a built-in copy that is broken or missing from the build;
    /// no <c>tuning.json</c> beside the game can cause it, since every tuning a
    /// file can make is clamped and fitted to the content it plays beside. Then
    /// nothing is made current, the failure's lines go to the log, and the game
    /// opens on the screen that shows them (<see cref="StartupErrorScene"/>),
    /// handed the load's notices. Should reading what the build carries ever
    /// throw, the whole error goes to the log and the same screen names it: a
    /// broken build is a screen, never a crash before the window.
    /// </summary>
    private bool LoadData()
    {
        string directory = AppContext.BaseDirectory;
        ContentLoad load;
        try
        {
            load = ShippedData.Load(DataFiles.Read(directory, line => Log.Info(line)));
        }
        catch (Exception thrown) when (thrown is not OutOfMemoryException)
        {
            Log.Error($"[Data] The data files the game carries built in could not be read: {thrown}");
            var failure = new ContentException("the built-in data files", ContentValidator.RuleBuiltInCopy,
                $"reading them threw {thrown.GetType().Name}: {thrown.Message.Replace("'", "", StringComparison.Ordinal)}");
            load = new ContentLoad(null, failure, [], []);
        }
        foreach (string notice in load.Notices)
            Log.Warn($"[Data] {notice}");
        if (load.Tuning is not null)
            Log.Info(DataFiles.TuningSummary(directory, load.TuningOverrides));

        if (load.Content is not { } content)
        {
            var error = load.Error ?? throw new InvalidOperationException("The data files loaded neither content nor a reason why not.");
            Log.Error($"[Data] The content does not load - {error.Message}");
            foreach (string line in StartupErrorScene.Lines(error, load.TuningOverrides, load.ReadBeside))
                Log.Error($"[Data] {line}");
            SwapTo(StartupErrorSceneName, new StartupErrorScene(this, error, load.TuningOverrides, load.ReadBeside, load.Notices));
            return false;
        }

        GameContent.Use(content);
        Log.Info($"[Data] Playing under tuning {TuningJson.Hash(content.Tuning)}");
        _notices.Hold(load.Notices);
        return true;
    }

    /// <summary>
    /// What this launch was asked for: the dev overrides from the environment —
    /// each ignored, with a warning, when it is not what it should be — and
    /// <c>--new-campaign</c> from the command line.
    /// </summary>
    private BootRequest LaunchRequest(GameContent content)
    {
        string? requested = Environment.GetEnvironmentVariable(DungeonOverrideVariable);
        bool straightIn = BootsIntoADungeon(requested, content.Dungeons);
        if (!string.IsNullOrWhiteSpace(requested) && !straightIn)
            Log.Warn($"[Run] {DungeonOverrideVariable}='{requested}' names no dungeon (there are: "
                + string.Join(", ", content.Dungeons.All.Select(d => d.Id)) + ") - it is ignored");

        string? seedText = Environment.GetEnvironmentVariable(SeedOverrideVariable);
        long? seed = BootSeed(seedText);
        if (!string.IsNullOrWhiteSpace(seedText) && seed is null)
            Log.Warn($"[Run] {SeedOverrideVariable}='{seedText}' is not a map seed (a whole number from 0 to {uint.MaxValue}) - it is ignored");

        return new BootRequest(straightIn ? BootDungeonId(requested, content.Dungeons) : null, seed, _launch.NewCampaign);
    }

    /// <summary>
    /// What this launch opens on, and the saves it plays with: the save folder
    /// read from the environment — <c>DISTANCERPG_SAVE_DIR</c>, else the player's
    /// own data folder — and, by what the launch asked for:
    /// <list type="bullet">
    /// <item>a dev override: a scratch campaign in the folder's override slot
    /// (<see cref="CampaignBoot.BootOverride"/>), which is where every save of the
    /// session goes — the slots are never made, so nothing of the session reaches
    /// the autosave or a manual slot — straight into its dungeon, or onto the
    /// screen between runs, with no title;</item>
    /// <item><c>--new-campaign</c>: the slots made, and a new campaign begun at
    /// once (<see cref="BeginNewCampaign(string)"/>), straight to the screen between
    /// runs — developer tooling, which asks nothing;</item>
    /// <item>otherwise the slots made and the title shown (<see cref="ShowTitle"/>):
    /// the first scene, where the player chooses what to play.</item>
    /// </list>
    /// </summary>
    private void BootCampaign(GameContent content, BootRequest request)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
            Log.Warn("[Save] The system names no local data folder: the saves go under the working folder");
        string saves = SaveStore.SaveDirectory(Environment.GetEnvironmentVariable(SaveStore.DirectoryVariable), localAppData);

        if (request.DevOverride)
        {
            // The scratch slot is where this session loads from and saves to: the
            // slots are never made, so no save of the session reaches them.
            var scratch = CampaignBoot.StoreFor(saves, request);
            _autosave = scratch;
            _loadedFrom = scratch;
            Log.Info($"[Save] Saves in {scratch.Folder} - the dev override's own slot");
            var boot = CampaignBoot.BootOverride(scratch, content, request, line => Log.Info(line));
            Adopt(boot.Campaign);
            foreach (string notice in boot.Notices)
                Log.Info($"[Save] {notice}");
            _notices.Lead(boot.Notices);
            _bootSeed = boot.FirstSeed;
            if (boot.StraightInto is { } dungeonId)
                BeginRun(dungeonId);
            else
                ShowBetweenRuns();
            return;
        }

        var slots = new SaveSlots(saves, content.Tuning.SaveSlots);
        _slots = slots;
        _autosave = slots.Store(SaveSlots.Autosave);
        _loadedFrom = _autosave;
        Log.Info($"[Save] Saves in {slots.Folder}: the autosave and {slots.Count} {(slots.Count == 1 ? "slot" : "slots")}");
        if (request.NewCampaign)
            BeginNewCampaign(Program.NewCampaignOption);
        else
            ShowTitle();
    }

    /// <summary>
    /// Fade to a fresh title (R2(a)): the autosave's unsaved-quit marker taken
    /// (<see cref="CampaignBoot.TakeUnsaved"/>) and the slots listed at the clock's
    /// time (<see cref="SaveListing.Read"/>) — never a file moved — and what there is
    /// to say led, the save's word shown whole: <paramref name="loadWord"/> first —
    /// back from a load that brought nothing back, what it set aside
    /// (<see cref="CampaignBoot.NothingLoadedWord"/>) — then that the autosave could
    /// not be written the last time play was left (<see cref="CampaignBoot.QuitUnsaved"/>),
    /// then a slot's file the file system would not let the game read; the data
    /// files' notices after them, capped, on the first showing. No campaign is in
    /// play at the title. Asked for mid-transition, it waits for the engine to
    /// finish.
    /// </summary>
    /// <param name="loadWord">What a load that brought nothing back says, leading the rest; null for nothing.</param>
    private void ShowTitle(IReadOnlyList<string>? loadWord = null)
    {
        if (SceneManager.Instance.IsTransitioning)
        {
            _pendingScene = () => ShowTitle(loadWord);
            return;
        }

        var slots = _slots ?? throw new InvalidOperationException("No save slots: a dev-override launch has no title.");
        Action<string> report = line => Log.Info(line);
        var said = new List<string>(loadWord ?? []);
        if (CampaignBoot.TakeUnsaved(slots.Store(SaveSlots.Autosave), report))
            said.Add(CampaignBoot.QuitUnsaved);
        var listing = SaveListing.Read(slots, _clock(), report);
        foreach (string notice in listing.Notices)
        {
            Log.Warn($"[Save] {notice}");
            said.Add(notice);
        }
        Log.Info(listing.Newest is { } newest
            ? $"[Save] The most recent save: {SaveSlots.Label(newest.Number)} - CONTINUE loads it"
            : "[Save] No slot holds a save yet");
        _listing = listing;
        _notices.Lead(said);

        // How many of the notices are the save's word, which the title shows whole,
        // is read before the take that hands them over and empties the holder.
        SwapTo(TitleSceneName, new TitleScene(this, this, listing, _notices.Leading, _notices.Take()));
    }

    /// <summary>
    /// The title chose slot <paramref name="slot"/> — CONTINUE's most recent save,
    /// or one from LOAD GAME: load it (<see cref="CampaignBoot.Load"/>) and play the
    /// campaign it brings back, on the screen between runs or in the dungeon at
    /// the saved floor and turn (<see cref="ResumeRun"/>), its word led for that
    /// screen, its resume marker cleared once the screen is up. A manual slot's
    /// campaign autosaves into the autosave slot, so when the title asked to set a
    /// different autosave aside (<see cref="SaveListing.NeedsRoom"/>) that is done
    /// now — once a campaign has come back, and before anything is written
    /// (<see cref="CampaignBoot.MakeRoom"/>) — and said after the load's word. A load
    /// that brings nothing back goes back to the title, which says so first and
    /// what was set aside, and leaves the autosave as it was.
    /// </summary>
    public void LoadSlot(int slot)
    {
        var slots = _slots ?? throw new InvalidOperationException("No save slots: a dev-override launch has no title to load from.");
        var autosave = _autosave ?? throw new InvalidOperationException("No autosave slot: the game has not loaded.");
        var content = GameContent.Current;
        Action<string> report = line => Log.Info(line);
        var store = slots.Store(slot);
        Log.Info($"[Save] Loading {SaveSlots.Label(slot)} from {store.Folder}");
        var load = CampaignBoot.Load(store, content, report, slot);
        if (load.Campaign is not { } campaign)
        {
            var word = CampaignBoot.NothingLoadedWord(load, slot);
            foreach (string notice in word)
                Log.Info($"[Save] {notice}");
            ShowTitle(word);
            return;
        }

        var said = new List<string>(load.Notices);
        if (_listing?.NeedsRoom(slot) == true)
            said.AddRange(CampaignBoot.MakeRoom(autosave, slot, report));
        Adopt(campaign);
        _saveRequested = false;
        _loadedFrom = store;
        _resuming = true;   // the loaded screen clears the slot's resume marker once it is up
        foreach (string notice in said)
            Log.Info($"[Save] {notice}");
        _notices.Lead(said);
        if (campaign.Visit is not null)
            ResumeRun(load.View);   // the save held a run in progress: it goes on in the dungeon
        else
            ShowBetweenRuns();
    }

    /// <summary>NEW CAMPAIGN, confirmed on the title: <see cref="BeginNewCampaign(string)"/>, the notices naming no command line.</summary>
    void ITitleHost.BeginNewCampaign() => BeginNewCampaign(how: null);

    /// <summary>
    /// A new campaign — NEW CAMPAIGN on the title, or <c>--new-campaign</c> at
    /// launch: the autosave slot's backup and save set aside as
    /// <c>campaign.abandoned-N.json</c>, never deleted (<see cref="CampaignBoot.Abandon"/>;
    /// USER DECISION 3) — the manual slots untouched — and the starting party,
    /// nothing met, every dungeon open, saved at once, so the folder holds it from
    /// the start: a quit mid-run before its first run ends continues it, never the
    /// old campaign. A store that refuses writes has said why already (a file that
    /// would not move), so it is only logged. Then the screen between runs over it,
    /// saying <c>A NEW CAMPAIGN BEGINS</c> and where the old files went; a write
    /// that fails is led too, and the new campaign's word leads it.
    /// </summary>
    /// <param name="how">What asked for it, which the set-aside notices name: <c>--new-campaign</c>, or null for the title's NEW CAMPAIGN.</param>
    private void BeginNewCampaign(string? how)
    {
        var store = _autosave ?? throw new InvalidOperationException("No autosave slot: the game has not loaded.");
        Log.Info($"[Hub] A new campaign ({how ?? "NEW CAMPAIGN on the title"}): the party from the roster, nothing met, every dungeon open - the autosave's campaign is set aside");
        var boot = CampaignBoot.Abandon(store, GameContent.Current, line => Log.Info(line), how);
        Adopt(boot.Campaign);
        _saveRequested = false;
        _resuming = false;
        _loadedFrom = store;
        foreach (string notice in boot.Notices)
            Log.Info($"[Save] {notice}");
        if (store.WritesRefused is { } refused)
            Log.Info($"[Save] The new campaign is not saved this session: {refused}");
        else
            Save(boot.Campaign, store, $"at once, as {how ?? "NEW CAMPAIGN"} began it");
        _notices.Lead(boot.Notices);
        ShowBetweenRuns();
    }

    /// <summary>QUIT on the title: the window closes. No campaign is in play there, so the close saves nothing (<see cref="OnClosing"/>).</summary>
    public void Quit() => Close();

    /// <summary>Play <paramref name="campaign"/> from now on: its run's end is a save request, and the campaign it replaces keeps no wire to the game.</summary>
    private void Adopt(CampaignState campaign)
    {
        if (_campaign is { } replaced)
            replaced.RunEnded -= SaveAtTheRunsEnd;
        _campaign = campaign;
        campaign.RunEnded += SaveAtTheRunsEnd;
    }

    /// <summary>
    /// A run has ended, by the way out or by a wipe — which ends it inside the turn
    /// system's raise — so the save is only asked for here, and written once the
    /// frame is over (<see cref="WriteRequestedSave"/>).
    /// </summary>
    private void SaveAtTheRunsEnd(RunResult result) => _saveRequested = true;

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
    /// Whether the game boots straight into a dungeon, past the title and the
    /// screen between runs. It does when <paramref name="requested"/>, the
    /// <see cref="DungeonOverrideVariable"/> override with surrounding whitespace
    /// ignored, names one of <paramref name="dungeons"/>. Otherwise the game opens
    /// on the title, where a campaign is continued or begun.
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
    /// visit is discarded whole — and saved, at the end of the frame the run
    /// ended in. So the game goes back to the screen between runs, where the last
    /// run's summary sits above the dungeon list and the next dungeon is chosen.
    /// </summary>
    public void RunOver() => ShowBetweenRuns();

    /// <summary>
    /// The playing scene asks for a save (1f): a player turn has started, or the
    /// party has arrived on a floor. Only asked for here — it arrives inside the
    /// turn system's raise that began the turn — and written at the end of the
    /// frame (<see cref="WriteRequestedSave"/>), once the campaign stands at a
    /// safe point.
    /// </summary>
    public void RequestSave() => _saveRequested = true;

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
        // How many of the notices are the save's word, which the screen shows
        // whole, is read before the take that hands them over and empties the holder.
        SwapTo(BetweenRunsSceneName, new BetweenRunsScene(this, campaign, this, _notices.Leading, _notices.Take()));
    }

    /// <summary>
    /// Begin a run in <paramref name="dungeonId"/>: roll the visit's seed and
    /// log it (any run can be reproduced from the log), enter the dungeon — the
    /// party rested and stood at its first floor's arrival — ask for the entry to
    /// be saved (1f: the save a quit on the way in resumes), and fade to a fresh
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
        RequestSave();

        SwapTo(DungeonSceneName, new DungeonScene(_camera!, this, campaign, this, _notices.Take()));
    }

    /// <summary>
    /// Play on the run a resumed save held (1f, Phase 5.8): no seed is rolled and
    /// nothing is entered — the visit is the save's, every floor it reached stood
    /// back on its regenerated terrain — so the scene that plays it is handed the
    /// save's view (<paramref name="view"/>: the member who was active, whether
    /// the turn marched) and what the launch has to say — the save's word on the
    /// campaign shown whole, as the screen between runs shows it — and it opens the
    /// floor the party stands on and goes on with the saved turn
    /// (<see cref="ResumeView"/>). Asked for mid-transition, it waits for the
    /// engine to finish.
    /// </summary>
    private void ResumeRun(ViewDto? view)
    {
        if (SceneManager.Instance.IsTransitioning)
        {
            _pendingScene = () => ResumeRun(view);
            return;
        }

        var campaign = _campaign ?? throw new InvalidOperationException("No campaign: the game has not loaded.");
        var visit = campaign.Visit ?? throw new InvalidOperationException("No run is in progress: there is nothing to resume in the dungeon.");
        Log.Info($"[Run] Resuming {visit.Def.Name} ({visit.Def.Id}) on map seed {visit.MapSeed}: floor {visit.FloorIndex + 1}, turn {visit.Turn}"
            + $" - run counter {campaign.RunCounter}{(view is null ? " - the save carried no view: the floor is arrived on afresh" : "")}");

        // How many of the notices are the save's word, which the resumed floor
        // shows whole as the screen between runs does, is read before the take
        // that hands them over and empties the holder.
        SwapTo(DungeonSceneName, new DungeonScene(_camera!, this, campaign, this, new ResumeView(view, _notices.Leading, _notices.Take())));
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

        // The frame is over, and no raise is running: what it asked of the save is done now.
        if (_resuming)
            ClearResumeOnceUp();
        if (_saveRequested)
            WriteRequestedSave();
    }

    /// <summary>
    /// A resumed campaign's first screen is up — current, its fade in done, and
    /// its first updates run — so the resume did not crash the game, and its
    /// marker goes: the next load resumes as ever, rather than setting the save
    /// aside as crashed.
    /// </summary>
    private void ClearResumeOnceUp()
    {
        var scenes = SceneManager.Instance;
        if (scenes.IsTransitioning || scenes.CurrentScene is null)
            return;
        _resuming = false;
        ClearResumeMarker("the resumed campaign's first screen is up");
    }

    /// <summary>Clear the resume marker of the slot the campaign was loaded from, saying why; a marker that will not go is a log line, never a crash.</summary>
    private void ClearResumeMarker(string why)
    {
        if (_loadedFrom is not { } store)
            return;
        try
        {
            store.ClearResuming();
            Log.Info($"[Save] {store.ResumingPath} cleared: {why}");
        }
        catch (Exception error)
        {
            Log.Warn($"[Save] {store.ResumingPath} could not be cleared - the next launch sets the save aside as crashed: {error}");
        }
    }

    /// <summary>
    /// The save a run's end, a run's entry, a player turn's start or a floor
    /// arrival asked for, written into the autosave slot now that the frame is
    /// over — with the playing scene's view when a run is in progress. A request
    /// that finds the campaign off a safe point — the enemies' turn begun in the
    /// same frame, the party out of movement on a march — waits for the next frame
    /// that stands at one. And one that arrives while a loaded campaign's first
    /// screen is not up yet waits for it: kept, and written the frame the resume
    /// marker clears, so a load whose first screen crashes never reaches the
    /// autosave, and the marker left in its slot sets that save aside next time.
    /// </summary>
    private void WriteRequestedSave()
    {
        if (_campaign is not { } campaign || _autosave is not { } store)
        {
            _saveRequested = false;
            return;
        }
        if (_resuming || !SaveGame.IsSafePoint(campaign))
            return;
        _saveRequested = false;
        if (campaign.Visit is { } visit)
            Save(campaign, store, $"mid-run in {visit.Def.Name}, floor {visit.FloorIndex + 1}, turn {visit.Turn}", PlayingScene(campaign)?.View);
        else
            Save(campaign, store, "at the run's end");
    }

    /// <summary>
    /// Write the campaign (<see cref="CampaignBoot.Save"/>), which never throws,
    /// stamped with the clock's time: every line to the log. A write that fails is
    /// said at once over the floor being played, when a run is
    /// (<see cref="DungeonScene.SaveFailed"/>), and on the next screen the game
    /// shows — there once, however many times it failed, and only until a later
    /// save goes through, which takes it back (<see cref="StartupNotices.Withdraw"/>).
    /// A file of the slot the write set aside first, since no save could be read
    /// from it, is said the same way — at once, and on the next screen — and never
    /// taken back: it is a fact about a file, whatever the saves after it do. The
    /// save on the disk is kept whatever happens.
    /// </summary>
    private void Save(CampaignState campaign, SaveStore store, string when, ViewDto? view = null)
    {
        Log.Info($"[Save] Saving the campaign {when}");
        var said = new List<string>();
        string? failed = CampaignBoot.Save(store, campaign, view, line => Log.Info(line), _clock(), said);
        foreach (string setAside in said)
            Log.Warn($"[Save] {setAside}");
        if (failed is null)
        {
            foreach (string stale in _failedSaves)
                _notices.Withdraw(stale);
            _failedSaves.Clear();
        }
        else
        {
            Log.Warn($"[Save] {failed}");
            if (!_failedSaves.Contains(failed))
                _failedSaves.Add(failed);
            said.Add(failed);
        }
        if (said.Count == 0)
            return;

        // Led for the next screen, and shown at once over the floor being played,
        // each whole ahead of what is up — so the first said is laid last, on top.
        _notices.Lead(said);
        var scene = PlayingScene(campaign);
        for (int i = said.Count - 1; i >= 0; i--)
            scene?.SaveFailed(said[i]);
    }

    /// <summary>
    /// The dungeon scene playing <paramref name="campaign"/>'s run in progress,
    /// when it is the scene up now — the one whose view a save carries and whose
    /// step a quit settles with — or null: between runs, on the screen between
    /// runs before a run's scene has opened, or once the run has ended.
    /// </summary>
    private static DungeonScene? PlayingScene(CampaignState campaign)
        => SceneManager.Instance.CurrentScene is DungeonScene scene && campaign.Visit is { } visit && ReferenceEquals(scene.Visit, visit)
            ? scene
            : null;

    /// <summary>
    /// The window is closing — ESC left unconsumed, the pause menu's save and
    /// quit, the title's QUIT, or the close button — and every one of them arrives
    /// here (the engine's own <c>Close</c> raises it too), where the campaign is
    /// saved. At the title, and behind the screen a broken content file stops the
    /// game on, there is no campaign, and nothing to save. Nothing thrown here
    /// stops the window closing: a failure is a log line.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        try
        {
            SaveOnQuit();
        }
        catch (Exception error)
        {
            Log.Error($"[Save] Saving on quit failed - the save on the disk is kept: {error}");
        }
        base.OnClosing(e);
    }

    /// <summary>
    /// The quit's save, once however the quit came, through the quit's own door
    /// (<see cref="CampaignBoot.SaveOnQuit"/>). Between runs the campaign is saved
    /// as it stands. Mid-run the run in progress is saved (1f): a quit during the
    /// enemies' turn plays it out to the next player turn first, one step of the
    /// playing scene's own logic clock at a time — <c>DungeonScene.StepLogic</c>,
    /// the step live play takes — and then saves it with the scene's view as it
    /// stands after. With no scene playing the run — a quit on the way in, before
    /// its first floor has opened — no turn is running to play out, and the
    /// campaign is saved as the entry left it. No screen follows a quit, so a quit
    /// that cannot save leaves a marker beside the autosave instead, and the title
    /// says, the next time it opens, that what the autosave holds is from before
    /// this quit.
    /// </summary>
    private void SaveOnQuit()
    {
        if (_quitSaved)
            return;
        _quitSaved = true;
        if (_campaign is not { } campaign || _autosave is not { } store)
            return;   // the title, or the content never loaded: no campaign in play

        var scene = PlayingScene(campaign);
        Log.Info(campaign.Visit is { } visit ? $"[Save] Saving the campaign on quit, mid-run in {visit.Def.Name}" : "[Save] Saving the campaign on quit");
        Action? step = scene is null ? null : scene.StepLogic;
        CampaignBoot.SaveOnQuit(store, campaign, scene is null ? null : () => scene.View, step, line => Log.Info(line), _clock());

        if (_resuming)
        {
            _resuming = false;
            ClearResumeMarker("the game quit cleanly");
        }
    }

    /// <summary>
    /// Escape goes to the scene being played. In a dungeon it closes a menu or
    /// opens the pause menu, whose quit is how a run is left, and it does nothing
    /// on a run's summary, where ENTER is the one way on. On the title it says no
    /// to an ask or closes the load list, and on the options it is not consumed,
    /// so the game closes, as QUIT does, with nothing to save. On the screen
    /// between runs it is never consumed, so the game saves the campaign and quits
    /// (<see cref="OnClosing"/>) — as it quits from the screen a broken content
    /// file stops the game on, which has no campaign behind it. Only what a scene
    /// leaves unconsumed quits. Mid-transition between scenes it does nothing at
    /// all, neither scene's to answer and never a quit on the way in or out of a
    /// run.
    /// </summary>
    private void OnEscape()
    {
        var scenes = SceneManager.Instance;
        if (scenes.IsTransitioning) return;
        bool consumed = scenes.CurrentScene switch
        {
            DungeonScene dungeon => dungeon.HandleEscape(),
            BetweenRunsScene betweenRuns => betweenRuns.HandleEscape(),
            TitleScene title => title.HandleEscape(),
            _ => false,
        };
        if (!consumed)
            Close();
    }
}
