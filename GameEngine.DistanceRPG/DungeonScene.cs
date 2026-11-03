using GameEngine.Core;
using GameEngine.Core.Diagnostics;
using GameEngine.Core.Particles;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>Full-screen menus; at most one is open at a time.</summary>
public enum GameMenu
{
    None,
    Inventory,

    /// <summary>
    /// The pause menu (<see cref="PauseMenu"/>): 1 closes it, 2 saves the campaign
    /// into its own slot on the party's own turn, asking nothing — and off it says
    /// why not — and 3 quits to the title, the campaign's slot written first. ESC
    /// opens and closes it. What a save by hand came to is drawn inside it, under its
    /// lines, until the next key.
    /// </summary>
    Pause,

    /// <summary>
    /// "Leave the dungeon?" — opened by E on the first floor's way out with the
    /// party gathered, because leaving discards the whole visit (§4.1) and ends
    /// the run. It previews what leaving now means — the boss down and what you
    /// carry kept, or a retreat. E or ENTER leaves, ESC stays, and every other key
    /// and click does nothing while it is up: no member is selected and no bag is
    /// opened behind it.
    /// </summary>
    ConfirmLeave,

    /// <summary>
    /// The run is over (§4.4): its summary — the outcome, the spoils kept or lost,
    /// the XP kept or clawed back, whether the run counted, the lock or the
    /// tutorial closing. Opened by the campaign's <see cref="CampaignState.RunEnded"/>
    /// on either ending, the way out's or a wipe's. <strong>Modal</strong>: ENTER
    /// alone moves on — once — to the screen between runs, where the next dungeon
    /// is chosen; ESC, the bag and its X, the stairs, the number keys, the turn's
    /// keys and every click do nothing, because the run they would act on has ended.
    /// </summary>
    RunSummary,
}

/// <summary>
/// What a left click on the thing under the mouse would do, for the HUD's
/// cue: the action (ATTACK, CAST MIRE, BLAST: 2 CAUGHT) when the turn system
/// would take it now, or why not (OUT OF REACH, NEED 30 MOVE) when it would not.
/// </summary>
public readonly record struct ClickCue(string Label, bool Ready);

/// <summary>
/// What E would do on the stair the active member is standing on (§4.2), for
/// the cue the HUD draws at it: the stair, the cue — the stair's own verb when
/// the party may take it (<c>E: DESCEND</c>), or why not (<c>GATHER THE PARTY
/// - 2 OF 4 HERE</c>) — and whether it is the verb.
/// </summary>
public readonly record struct StairPrompt(StairUse Stair, string Cue, bool Ready);

/// <summary>A stair of the floor being played and the slab it is drawn as, for its label.</summary>
public sealed record StairMarker(StairUse Stair, GameObject Slab);

/// <summary>
/// A weapon lying on the floor being played, in one of its two forms. A
/// <strong>drop</strong> lies where its carrier fell (§3.5): it has the corpse
/// whose row fixes it and the drop itself. A <strong>loose item</strong> is a
/// weapon the party put down (§4.4): it has the floor's own record of it
/// (<see cref="Loose"/>) and nothing else. Both have the marker drawn on the tile
/// and that tile, and <c>G</c> takes either. A loose item is never dressed up as
/// a drop: no <see cref="Drop"/> is made for it, so nothing reads a farm depth or
/// a corpse into a weapon the party carried.
/// <para>
/// <strong>The tile is on the record rather than read back off the corpse.</strong>
/// The marker is fixed where the body lay when the item was laid down; a target
/// test that re-read <c>Corpse.X/Y</c> every frame would agree with it only
/// because nothing currently moves a corpse — an invariant held by two unrelated
/// facts (displacement applies to the living, and the resurrection relocation
/// cannot run while resurrection is stopped) rather than by the data. One source
/// for the item's position means the cue the HUD draws and the tile <c>G</c>
/// collects on cannot part company.
/// </para>
/// <para>
/// Nothing here is saved: this is a marker. A drop is its row's — a pure function
/// of the row and the floor's seed, pinned on the row by a save once it lies
/// there untaken (<see cref="EnemyState.PinnedDrop"/>), so a load hands back the
/// very weapon that was lying there (<see cref="TurnSystem.GroundItemOf"/>) — and
/// picking it up sets <see cref="EnemyState.DropTaken"/> on that row, which is
/// the one thing that retires the offer. A loose item is the floor's
/// (<see cref="Floor.LooseItems"/>), so it is stored there. Both are laid again on
/// every arrival, a resumed one's included.
/// </para>
/// </summary>
/// <param name="Corpse">A drop's corpse; null for a loose item.</param>
/// <param name="Drop">The drop; null for a loose item.</param>
/// <param name="Marker">The plate drawn on the tile.</param>
/// <param name="Tile">The tile it lies on, (row, column).</param>
/// <param name="Loose">A loose item's record on the floor; null for a drop.</param>
public sealed record GroundItem(EnemyState? Corpse, Drop? Drop, GameObject Marker, (int R, int C) Tile, LooseItem? Loose = null)
{
    /// <summary>The weapon lying here, whichever form the item takes.</summary>
    public Weapon Weapon => Drop?.Weapon ?? Loose!.Weapon;
}

/// <summary>
/// What the host hands a dungeon scene playing a visit a save brought back
/// (1f, Phase 5.8) — a slot loaded from the title, by CONTINUE or LOAD GAME: the
/// view the save carried and what the game has to say over the floor it resumes
/// on.
/// </summary>
/// <param name="View">
/// The save's view (<see cref="ViewDto"/>): the member who was active, and
/// whether the turn in progress marches. With it the scene resumes the turn the
/// save was taken in — that member active, the march as the turn decided it,
/// what the party could see this turn still lit. Null when the save carried
/// none: one taken before the floor was opened (right after the entry), which
/// the scene arrives on afresh, as it would have.
/// </param>
/// <param name="Leading">How many of <paramref name="Notices"/>, from the first, are the save's word on the campaign (<see cref="StartupNotices.Leading"/>): shown whole over the resumed floor, as the screen between runs shows them, where what the data files raised after them is capped (S8's review).</param>
/// <param name="Notices">What the game has to say — the save's word on the campaign, then what the data files raised — shown over the resumed floor (<see cref="DungeonHud.ShowResume"/>).</param>
public sealed record ResumeView(ViewDto? View, int Leading, IReadOnlyList<string> Notices);

/// <summary>
/// The main gameplay scene: one visit to a dungeon (§4.5), played a floor at a
/// time by a four-character party in turn-based, distance-budgeted combat.
/// Each floor is a procedurally generated map — floor 1 of the shipped seed is
/// the prototype's exact dungeon — reinterpreted in 3D: floor slab on the XZ
/// plane, extruded wall boxes, angled top-down camera.
/// <para>
/// <strong>A view of the visit, never its owner.</strong> The campaign and the
/// visit in it are handed in by the host (<see cref="IRunHost"/>), which rolled
/// the seed and entered the dungeon; the floors, their rows and their fog are
/// the visit's (<see cref="DungeonState"/>), the party is the campaign's, and
/// this scene draws whichever floor the visit stands on. Taking the stairs is a
/// swap inside it — tear the floor's objects down, move the visit, build the
/// next floor from what the visit holds — under a short fade.
/// </para>
/// <para>
/// <strong>The run ends in the campaign, and this scene shows it.</strong>
/// Leaving by the first floor's way out ends it (<see cref="CampaignState.EndRun"/>),
/// and so does a wipe, in Logic, at the turn system's game over; either way the
/// campaign says so (<see cref="CampaignState.RunEnded"/>) and this scene puts up
/// the summary (<see cref="GameMenu.RunSummary"/>). ENTER there hands the run back
/// to the host, which opens the screen between runs (<see cref="BetweenRunsScene"/>),
/// where the next dungeon is chosen and the next visit gets a scene of its own.
/// </para>
/// <para>
/// <strong>The turn system runs on a fixed logic clock</strong> (Phase 5.8, 1c):
/// never on a frame's own time, but in whole steps of
/// <see cref="SaveGame.LogicStepSeconds"/> (<see cref="LogicClock"/>), each one
/// <see cref="StepLogic"/> — the system's step plus the enemy-phase sighting
/// this scene feeds back. So the enemies' turn plays out the same at any frame
/// rate, and a quit's settle, which takes the same step, saves exactly the turn
/// the player would have met.
/// </para>
/// <para>
/// <strong>The run saves itself</strong> (1f): the scene asks its host for a
/// save (<see cref="IRunHost.RequestSave"/>) at every player turn's start and
/// after every floor arrival, and the host writes it once the frame is over.
/// What the save carries for the scene is its view (<see cref="View"/>): the
/// active member and whether the turn marches. A visit a save brought back is
/// played by a scene built with a <see cref="ResumeView"/>: the saved member
/// active, the march as the turn decided it, what the party could see this turn
/// still lit, the dice falling as they would have (the visit's own die,
/// <see cref="DungeonState.RollD20"/>), and <c>RESUMED</c> over the floor banner.
/// </para>
/// <para>
/// <strong>And the player saves by hand, and leaves play, from the pause
/// menu</strong> (the user's ruling of 2026-09-25 on the save model, R2(d),
/// R2(e); <see cref="PauseMenu"/>): 2 saves the campaign on the party's own turn
/// (<see cref="CanSaveNow"/>) — never while the enemies move — into its own slot,
/// asking nothing (the user's ruling of 2026-09-25 on the save slots, R5), the host
/// writing it at the frame's end (<see cref="ISaveHost.SaveNow"/>) and telling the
/// menu how it went (<see cref="SlotSaved"/>); 3 quits to the title, the host
/// writing the campaign's slot first (<see cref="ISaveHost.QuitToTitle"/>), and the
/// scene takes no key after it while it fades out.
/// </para>
/// </summary>
public class DungeonScene : Scene
{
    private const float WallHeight = 1.2f;
    private const float FloorThickness = 0.2f;

    // Palette carried over from the prototype's hex colours.
    private static readonly Vector4 WallColor = Rgb(0x3d405b);
    private static readonly Vector4 FloorColor = Rgb(0x0f3460);
    private static readonly Vector4 EnemyColor = Rgb(0xf5a623);
    internal static readonly Vector4 DeadColor = Rgb(0x555555);
    private static readonly Vector4 HealColor = Rgb(0x44dd77);
    private static readonly Vector4 TickColor = Rgb(0xb06ee0);   // a status tick's damage: neither a hit's red nor a heal's green
    private static readonly Vector4 CritColor = Rgb(0xffde00);     // a crit, a hit's or a cast's
    private static readonly Vector4 HitColor = Rgb(0xff4545);      // what a hit took off HP
    private static readonly Vector4 WeakColor = Rgb(0xababab);     // a natural 1, a fumble, and a Block the blow beat anyway
    private static readonly Vector4 BlockColor = Rgb(0x4fc2f7);    // what Block absorbed and what Ward swallowed
    private static readonly Vector4 RiderColor = Rgb(0xff77cc);    // a status a hit left riding on its target: SUNDERED!, WEAKENED!
    private static readonly Vector4 ReactionColor = Rgb(0x87ffff); // a reaction's callout, and a shot held for one
    private static readonly Vector4 CueColor = Rgb(0x9999a6);      // why a click or a key did nothing
    private static readonly Vector4 LevelUpColor = Rgb(0x8cff6b);  // a pool or a ladder crossing a bar (§2.2)
    private static readonly Vector4 LootColor = Rgb(0xffcf5a);     // a weapon on the floor, and the beat when it is taken (§3.5)
    private static readonly Vector4 StrongerColor = Rgb(0xff6a3d); // a dummy getting up stronger than it went down (§3.2): the threat half of its count

    /// <summary>
    /// The boss's stand-in (§4.3): stone-grey where every dummy is orange — the
    /// golem is carved, not trained — and a size larger, so the one thing on the
    /// floor that is not a dummy reads as such across a room. Grey again, and
    /// darker, once it falls, as every corpse is.
    /// </summary>
    private static readonly Vector4 BossColor = Rgb(0xa9a595);
    private const float BossSize = 1.4f;

    /// <summary>
    /// Each stair's colour, on its slab and on its label (§4.2): down violet, up
    /// teal, and the first floor's way out gold — three colours the dungeon's
    /// palette uses for nothing else, so a stair reads as a stair across a room.
    /// </summary>
    private static readonly IReadOnlyDictionary<Stair, Vector4> StairColors = new Dictionary<Stair, Vector4>
    {
        [Stair.Down] = Rgb(0x9b6bff),
        [Stair.Up] = Rgb(0x2ec4b6),
        [Stair.Exit] = Rgb(0xe8a93a),
    };

    /// <summary>The colour a stair of this kind is drawn and labelled in.</summary>
    internal static Vector4 StairColor(Stair kind) => StairColors.GetValueOrDefault(kind, WallColor);

    private static readonly Vector4[] PartyColors =
    {
        Rgb(0xe94560), // A
        Rgb(0x3b8eff), // B
        Rgb(0x44cc66), // C
        Rgb(0xffdd44), // D
    };

    /// <summary>
    /// Party members this scene can draw: one colour each, by roster position,
    /// for every member of the campaign's party (<see cref="SpawnParty"/>). The
    /// roster's length is the rule (<see cref="GameConstants.PartySize"/>) and
    /// this is the presentation's capacity for it; a test asserts the two are
    /// equal, because a member with no colour would have no stand-in.
    /// </summary>
    internal static int PartyColorCount => PartyColors.Length;

    private readonly Camera _camera;
    private readonly Game _game;
    private readonly DungeonHud _hud;

    // What this scene is a view of (§6.7): the campaign — the party, the
    // permanent half, and everything it has met — and the visit in progress,
    // captured once at construction. The visit is never read back off the
    // campaign afterwards: the host drops it and enters the next one the moment
    // this scene hands the run back, while this scene is still fading out.
    private readonly CampaignState _campaign;
    private readonly DungeonState _visit;
    private readonly IRunHost _host;

    // What the host handed a scene playing a visit a save brought back — the
    // save's view and what the game has to say — or null for a visit entered
    // fresh. Read once, by the first floor's arrival.
    private readonly ResumeView? _resume;

    // The fixed logic clock's remainder (LogicClock): the frame time not yet
    // stepped, always under one SaveGame.LogicStepSeconds.
    private float _logicClock;

    // The floor being played, and the three parts of it this scene reads most:
    // its map, its fog boxes and its fog, which is the floor's and not the
    // scene's (§4.5) — the floor keeps what the party explored. All four follow
    // the visit onto whichever floor it stands on (ArriveOnFloor).
    private Floor _floor = null!;
    private MapData _map = null!;
    private FogBoxSet _fogBoxes = null!;
    private FogState _fog = null!;
    private FogOverlayRenderer _fogOverlay = null!;

    // The floor's turn system: the one the visit's OpenTurns returned for the
    // floor being played (visit.Turns), replaced whole on every arrival.
    private TurnSystem _turns = null!;

    // Every object the floor being played put in the scene that has no list of
    // its own — its walls, its stair slabs, the fog overlay and the two fog
    // emitters — so a swap takes all of them down (TearDownFloor). The party's
    // stand-ins and the map-independent slab are the visit's, and stay.
    private readonly List<GameObject> _floorObjects = new();
    private readonly List<StairMarker> _stairMarkers = new();

    // Fog particles: mist puffs on reveal (staggered with the ripple) and
    // ambient wisps drifting over explored-but-out-of-view tiles.
    private ParticleEmitter _mistEmitter = null!;
    private ParticleEmitter _wispEmitter = null!;
    private readonly List<(float TimeLeft, Vector3 Position)> _pendingMistBursts = new();
    private readonly Random _fxRng = new();
    private float _wispTimer;
    private const float WispInterval = 0.15f;

    private readonly List<CharacterObject> _party = new();
    private readonly Dictionary<CharacterObject, (int R, int C)> _lastFogTile = new();

    // Non-combat marching: the rest of the party follows the active character
    // single file along their walked path.
    private readonly MarchingLine _march = new();
    private CharacterObject? _marchLeader;
    private bool _wasMarching;
    private bool _marchingThisTurn; // decided at each turn's start and on a stairs arrival; a resume reads it back from the save's view
    private const float MarchArriveTolerance = 2f;
    private readonly List<EnemyObject> _enemies = new();
    private readonly Dictionary<EnemyObject, (int R, int C)> _lastEnemyTile = new();
    private int _activeIdx;

    // What the permanent kills have left on the floor being played (§3.5). Not
    // stored by the floor: re-derived from the enemy rows on every arrival, and
    // dropped whole on every swap. Beside them, the markers of what the party
    // put down here (§4.4), laid from the floor's own list (Floor.LooseItems),
    // which is stored and outlives the swap. The list is in the order laid. What
    // G takes and how a tile's labels stack read it in the order G takes things
    // (InTakingOrder), which does not depend on that. What the campaign has met
    // (§3.3) is the campaign's, which outlives this scene and every visit — met
    // at the start from the loadout (CampaignState.Start) and added to at every
    // pickup.
    private readonly List<GroundItem> _groundItems = new();

    // The stairs (§4.2): a swap runs under a fade either side of it, and once the
    // run is over and its summary read, the scene fades to black and hands the
    // run back to the host. While any of it runs, presses and the movement they
    // drive are ignored — releases never are, so a key let go mid-fade is up on
    // arrival.
    private const float TransitFadeSeconds = 0.25f;
    private enum Transit { None, FadingOut, FadingIn, Leaving }
    private Transit _transit;
    private float _transitClock;
    private StairUse _transitStair;
    private bool _runOverSent;

    // The run's end (§4.4), as the campaign reported it. The next dungeon is not
    // this scene's to name: it is chosen on the screen between runs.
    private RunResult? _endedRun;

    // What the pause menu answers under its lines — what a save by hand came to, or
    // why 2 saved nothing — until the next key or until the menu closes. Menus: never
    // saved.
    private string? _answer;

    // Play has been left from the pause menu — the host told to quit to the title,
    // or, in a dev-override session, the window to close — so this scene is on its
    // way out: every key after it is ignored, as between runs, while the screen
    // fades to the title or the window closes. The menu stays up meanwhile, so
    // nothing behind it acts either, and nothing reaches the campaign the host has
    // let go (S7's review).
    private bool _leaving;

    // Held movement keys (arrows and WASD both drive the active character).
    private bool _up, _down, _left, _right;

    // Last known mouse position in window pixels, for click ray-picking.
    private Vector2 _mousePos;

    // Camera rig: fixed angled top-down view that hangs above/behind the
    // follow target; scroll wheel changes the distance.
    private Vector3 _cameraTarget;
    private float _cameraDistance = 12f;
    private const float MinCameraDistance = 5f;
    private const float MaxCameraDistance = 30f;
    private const float CameraFollowRate = 6f;

    /// <summary>A scene playing a visit entered fresh — by the screen between runs, or by a dev override booting straight in.</summary>
    /// <param name="camera">The game's camera, which the scene aims.</param>
    /// <param name="game">The game window: its size, its shaders, its close.</param>
    /// <param name="campaign">The campaign, with the visit this scene plays already entered (<see cref="CampaignState.Enter"/>): the party stands at its first floor's arrival.</param>
    /// <param name="host">Who is told when the run is over (<see cref="IRunHost.RunOver"/>), and asked for its saves (<see cref="IRunHost.RequestSave"/>).</param>
    /// <param name="notices">What the game's startup has to say over the first floor — the data files' notices — or none.</param>
    /// <exception cref="ArgumentException">The campaign has no visit in progress: there is nothing to play.</exception>
    public DungeonScene(Camera camera, Game game, CampaignState campaign, IRunHost host, IReadOnlyList<string>? notices = null)
        : this(camera, game, campaign, host)
    {
        // What the game's startup had to say: the data files' notices, when this
        // scene is the first the game opens on. A dev override boots straight in,
        // past the screen between runs that would say them otherwise. They go up
        // over the first floor's arrival banner. Every other run is handed none.
        // They are handed to the HUD here, not in Initialize: nothing before the
        // first Update ages them, and a test can read the rows with no window.
        _hud.ShowNotices(notices ?? []);
    }

    /// <summary>
    /// A scene playing a visit a save brought back (1f, Phase 5.8): the campaign a
    /// slot held, loaded from the title — CONTINUE or LOAD GAME — with its run in
    /// progress. The floor, the turn, every position and every counter are the
    /// visit's, restored; what the scene adds
    /// is what <paramref name="resumed"/> carries — the save's view, restored on
    /// the first floor's arrival rather than decided afresh — and <c>RESUMED</c>
    /// over the floor banner with the game's notices, the save's word among them
    /// shown whole (<see cref="DungeonHud.ShowResume"/>), handed to the HUD here
    /// for the reason the other constructor gives.
    /// </summary>
    /// <param name="camera">The game's camera, which the scene aims.</param>
    /// <param name="game">The game window: its size, its shaders, its close.</param>
    /// <param name="campaign">The campaign a save brought back, its visit in progress with no floor's system open (<see cref="SaveGame.Restore"/>).</param>
    /// <param name="host">Who is told when the run is over, and asked for its saves.</param>
    /// <param name="resumed">The save's view and the game's notices.</param>
    /// <exception cref="ArgumentException">The campaign has no visit in progress: there is nothing to resume.</exception>
    public DungeonScene(Camera camera, Game game, CampaignState campaign, IRunHost host, ResumeView resumed)
        : this(camera, game, campaign, host)
    {
        ArgumentNullException.ThrowIfNull(resumed);
        _resume = resumed;
        _hud.ShowResume(_visit, resumed.Notices, resumed.Leading);
    }

    /// <summary>What both constructors share: the campaign and its visit, the host, and the HUD.</summary>
    private DungeonScene(Camera camera, Game game, CampaignState campaign, IRunHost host) : base("DungeonScene")
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(host);
        _camera = camera;
        _game = game;
        _campaign = campaign;
        _visit = campaign.Visit
            ?? throw new ArgumentException("A dungeon scene plays a visit in progress: enter a dungeon before building one.", nameof(campaign));
        _host = host;
        _hud = new DungeonHud(game);
    }

    /// <summary>The notices as the HUD draws them over the first floor (<see cref="NoticeText.Rows"/>): wrapped and capped, the save's word whole over a resumed visit, or none — the startup's, or a resumed visit's — and a save that failed since, over whichever floor (<see cref="SaveFailed"/>).</summary>
    internal IReadOnlyList<string> NoticeRows => _hud.NoticeRows;

    /// <summary>
    /// The host could not write a save of the run this scene plays (1f): said over
    /// the floor at once, the save's word shown whole (<see cref="DungeonHud.ShowSaveFailed"/>),
    /// where the player is — not only on the next screen between runs, at the run's
    /// end — so a run whose saves fail is never played on, or quit, as though it
    /// were saved. A file of the slot the host's write set aside first, since no
    /// save could be read from it, is said the same way (<see cref="CampaignBoot.Save"/>).
    /// </summary>
    /// <param name="notice">What the failed write says (<see cref="CampaignBoot.NotSaved"/>), or what the write set aside (<see cref="CampaignBoot.SetAsideNotice"/>).</param>
    internal void SaveFailed(string notice) => _hud.ShowSaveFailed(notice);

    /// <summary>The banner's lines as the HUD draws them, top to bottom — over a resumed floor, <c>RESUMED</c> first — or none once it is down.</summary>
    internal IReadOnlyList<string> BannerLines => _hud.BannerLines;

    /// <summary>
    /// What a save carries for this scene (1b): the active member, and whether
    /// the turn in progress was granted the march. <c>Logic/</c> never reads it;
    /// a scene resuming the save does (<see cref="ResumeView"/>). Read by the host
    /// when it saves, once the frame — or a quit's settle — is over.
    /// </summary>
    internal ViewDto View => new(ActiveCharacter.State.Id, _marchingThisTurn);

    public CharacterObject ActiveCharacter => _party[_activeIdx];

    // HUD-facing state
    public IReadOnlyList<CharacterObject> Party => _party;
    public int ActiveIndex => _activeIdx;
    public TurnSystem Turns => _turns;
    public IReadOnlyList<EnemyObject> Enemies => _enemies;
    /// <summary>
    /// What lies on the floor being played, in the order <c>G</c> takes it from any
    /// one tile (<see cref="InTakingOrder"/>): the HUD stacks the labels on a tile
    /// in this order, the next to be taken lowest.
    /// </summary>
    public IReadOnlyList<GroundItem> GroundItems => InTakingOrder(_groundItems).ToArray();
    public CampaignState Campaign => _campaign;

    /// <summary>The visit this scene plays: the floor line reads the dungeon and the floor number off it, and never its depth. Kept after the run ends, when the campaign has already let it go, so the last floor still draws under the summary.</summary>
    public DungeonState Visit => _visit;

    /// <summary>How the run ended, once it has (§4.4) — what the summary shows — or null while it goes on.</summary>
    public RunResult? EndedRun => _endedRun;

    /// <summary>The stairs of the floor being played, each with the slab it is drawn as.</summary>
    public IReadOnlyList<StairMarker> StairMarkers => _stairMarkers;

    /// <summary>
    /// The menu that is open, or <see cref="GameMenu.None"/>. Whatever closes the
    /// bag — I, ESC, the turn's end, the run's end putting its summary up — also
    /// disarms its "leave" (<see cref="LeaveArmed"/>), here in the one place every
    /// one of those paths goes through, so the next time the bag opens, a number
    /// equips again. And whatever closes the pause menu drops its answer, here too.
    /// </summary>
    public GameMenu ActiveMenu
    {
        get => _activeMenu;
        private set
        {
            _activeMenu = value;
            if (value != GameMenu.Inventory)
                _leaveArmed = false;
            if (value != GameMenu.Pause)
                _answer = null;
        }
    }

    private GameMenu _activeMenu = GameMenu.None;

    public bool InventoryOpen => ActiveMenu == GameMenu.Inventory;

    /// <summary>
    /// True while the bag's X has armed "leave" (§4.4): the next bag number puts
    /// that slot's weapon down where the active member stands, rather than
    /// equipping it, and the bag's key hint says so (<see cref="DungeonHud.LeaveHint"/>).
    /// Only ever true with the bag open. X again, closing the bag, or the turn's
    /// end disarms it, and so does a weapon put down.
    /// </summary>
    public bool LeaveArmed => _leaveArmed && InventoryOpen;

    private bool _leaveArmed;
    public Vector2 MousePos => _mousePos;
    public bool PauseMenuOpen => ActiveMenu == GameMenu.Pause;

    public bool AnyMenuOpen => ActiveMenu != GameMenu.None;

    /// <summary>What the pause menu answers under its lines — what a save by hand came to (<see cref="SlotSaved"/>), or why 2 saved nothing — until the next key or until the menu closes; null for nothing.</summary>
    internal string? MenuAnswer => _answer;

    /// <summary>Whether this session has a title to quit to (<see cref="ISaveHost.HasTitle"/>): a dev-override session has none.</summary>
    internal bool HasTitle => _host.HasTitle;

    /// <summary>The slot the campaign saves into, by its name (<see cref="ISaveHost.SavesInto"/>), which the pause menu says under its options.</summary>
    internal string SavesInto => _host.SavesInto;

    /// <summary>
    /// Whether the campaign can be saved by hand now: the autosave's safe point
    /// (<see cref="SaveGame.IsSafePoint"/>) — the party's own turn with nothing in
    /// flight, never while the enemies take their turn or its banner is up — with
    /// no stair's fade running and the run not over. Off it, 2 on the pause menu
    /// answers <see cref="PauseMenu.SaveOnYourTurn"/> and saves nothing; the host
    /// asks the safe point again when it writes, and answers so if it has gone.
    /// </summary>
    internal bool CanSaveNow => !Transiting && _endedRun is null && SaveGame.IsSafePoint(_campaign);
    public float LastBankedMovement { get; private set; }

    /// <summary>True while a stair's fade runs, either side of the swap, or once the run's summary has been left for the screen between runs: presses are ignored.</summary>
    public bool Transiting => _transit != Transit.None;

    /// <summary>
    /// How far the screen has faded to black for the stairs, 0 to 1: up to black
    /// over <see cref="TransitFadeSeconds"/>, the floor swapped at black, and back
    /// down over as long again; leaving the dungeon fades up and stays black.
    /// </summary>
    public float TransitFade => _transit switch
    {
        Transit.FadingOut or Transit.Leaving => Math.Clamp(_transitClock / TransitFadeSeconds, 0f, 1f),
        Transit.FadingIn => Math.Clamp(1f - _transitClock / TransitFadeSeconds, 0f, 1f),
        _ => 0f,
    };

    /// <summary>
    /// What E would do right now on the stair the active member is standing on,
    /// or null: off every stair, with a menu open, during a fade, or outside the
    /// player phase — when E does nothing and there is nothing to say. The rule
    /// is the visit's (<see cref="DungeonState.CanUse"/>); the words are the HUD's.
    /// </summary>
    public StairPrompt? ActiveStairPrompt
    {
        get
        {
            if (Transiting || AnyMenuOpen || _turns.Phase != TurnPhase.Player) return null;
            var active = ActiveCharacter.State;
            if (_visit.StairUnder(active) is not { } stair) return null;

            var party = _campaign.Party;
            var refusal = _visit.CanUse(stair, party, active);
            string? cue = DungeonHud.StairCue(stair, refusal, _visit.Gathered(party, stair.Tile), LivingMembers());
            return cue is null ? null : new StairPrompt(stair, cue, refusal is null);
        }
    }

    /// <summary>
    /// The movement budget <paramref name="member"/>'s Mire cuts, as the Mire
    /// legend states it (<see cref="BudgetBeforeMire(PartyMemberState, TurnPhase, Tuning)"/>):
    /// in the player phase the whole this turn's cap was cut from, so the legend
    /// agrees with the MOVE readout at the same level; once the turn has ended,
    /// the coming turn's.
    /// </summary>
    public float BudgetBeforeMire(PartyMemberState member)
        => BudgetBeforeMire(member, _turns.Phase, GameContent.Current.Tuning);

    /// <summary>
    /// The whole a turn start cuts Mire from, for the Mire legend (1b). In the
    /// player phase it is the whole this turn's cap was cut from —
    /// <see cref="PartyMemberState.BudgetBeforeMire"/>, which the turn's start
    /// wrote (<see cref="PartyMemberState.StartTurn"/>) and a save carries. From
    /// the turn's end on it is the coming turn's: <paramref name="tuning"/>'s
    /// <see cref="Tuning.MovementBudget"/> plus what the member has just banked
    /// (<see cref="PartyMemberState.SavedMovement"/>), exactly what the next start
    /// will cut. Read off the member, never cached beside it, so a legend reads the
    /// same whole before a reload and after it.
    /// </summary>
    internal static float BudgetBeforeMire(PartyMemberState member, TurnPhase phase, Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(tuning);
        return phase == TurnPhase.Player ? member.BudgetBeforeMire : tuning.MovementBudget + member.SavedMovement;
    }

    // What the mouse is over and what a click there would do, refreshed each
    // frame through the same pick a click acts on (UpdateHover): the HUD's
    // hover cue, a wand's aim cue at the cursor, and the bodies its shape
    // would catch there.
    public EnemyObject? HoveredEnemy { get; private set; }
    public CharacterObject? HoveredMember { get; private set; }
    public ClickCue? HoverCue { get; private set; }
    public ClickCue? AimCue { get; private set; }
    public IReadOnlyList<ActorState> AimCaught { get; private set; } = Array.Empty<ActorState>();

    // The turn system's natural rolls as they are drawn (RollD20), for the cast cues.
    private int _lastRoll;
    private int _rollCount;
    private int _castRollCalledAt = -1;   // the roll count a cast was last called at: once per cast, however many statuses it lands
    private bool _watchCastRoll;
    private int? _castRoll;
    private int _castRollAt;              // the roll count at the watched cast's own roll

    /// <summary>
    /// Statuses a hit can leave on its attacker rather than on its target:
    /// BlockWeaken's Weakened, when the attacker's blow is blocked (§1.6). A
    /// hit's resolution lists only its defender's riders, so these levels are
    /// watched across each hit instead (<see cref="AnnounceAttackerRiders"/>).
    /// </summary>
    private static readonly StatusEffectType[] AttackerRiders = { StatusEffectType.Weakened };

    // Every actor's AttackerRiders levels as they stood before the hit now
    // resolving: taken at every roll — a hit rolls before its chain runs —
    // and again after every hit's floating text.
    private readonly Dictionary<(ActorState Actor, StatusEffectType Type), int> _riderLevels = new();

    /// <summary>
    /// True while the party marches freely. Marching is granted only at the
    /// start of a turn, or on arriving on a floor — no live enemy visible and
    /// the party regrouped within each member's max movement of the leader —
    /// and once revoked (a live enemy sighted) stays off for the rest of the
    /// turn. This keeps a split party from charging back into formation the
    /// moment line of sight to the enemy breaks mid-fight; a party that has
    /// just taken the stairs is together by construction, having gathered to
    /// take them. A resumed turn is neither: the grant comes back from the save's
    /// view and the sighting from the turn's own counters, so a march revoked
    /// before the quit stays revoked, and one granted stays granted.
    /// </summary>
    public bool Marching => _turns.Phase == TurnPhase.Player
        && _marchingThisTurn
        && !_turns.AnyLiveEnemySeenThisTurn;

    /// <summary>
    /// The turn-start (and floor-arrival) marching decision: needs the enemy
    /// dead or out of view and every living member within their own max
    /// movement of the leader.
    /// </summary>
    private void EvaluateMarchingForTurn()
    {
        _marchingThisTurn = false;
        if (_enemies.Any(e => e.State.Alive && e.IsActive)) return;

        var leader = ActiveCharacter.State;
        foreach (var member in _party)
        {
            var state = member.State;
            if (!state.Alive) continue;
            float dx = state.X - leader.X;
            float dy = state.Y - leader.Y;
            if (dx * dx + dy * dy > state.EffectiveMax * state.EffectiveMax) return;
        }

        _marchingThisTurn = true;
    }
    public Vector4 PartyColor(int idx) => PartyColors[idx];

    /// <summary>
    /// The first floor, entered: the same path every arrival takes
    /// (<see cref="ArriveOnFloor"/>) without a floor to leave first. The visit
    /// was entered by the host, so the floors exist and the party already stands
    /// around the first floor's arrival tile — the dungeon the scene has always
    /// generated, tile for tile, when the seed is the shipped one (floor 0's
    /// seed is the map seed itself). A visit a save brought back is arrived on
    /// the same way, where the save left the party, its turn resumed
    /// (<see cref="ResumeView"/>). Either way the arrival is saved, lit and with
    /// its march decided, once the frame is over.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        BuildFloor();   // the map-independent slab every floor of the visit stands on
        SpawnParty();
        _campaign.RunEnded += OnRunEnded;   // before any system opens: a wipe ends the run in Logic, and this scene shows it
        ArriveOnFloor(_visit.OpenTurns(_campaign.Party, RollD20), _resume);
        WireInput();
        _host.RequestSave();   // the first floor's arrival (1f)

        _camera.Yaw = -90f;   // face -Z (up the map)
        _camera.Pitch = -60f; // angled top-down
        _camera.Far = 300f;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        AdvanceTransit(deltaTime);
        _hud.Update(deltaTime);
        if (_transit == Transit.Leaving) return;   // the run is over here: nothing moves while the host takes over

        // The logic clock (1c): the turn system in whole fixed steps, never the
        // frame's own time, so the enemies' turn plays out the same at any frame
        // rate — and the same as a quit's settle, which steps StepLogic too.
        for (int steps = LogicClock.Steps(ref _logicClock, deltaTime, SaveGame.LogicStepSeconds); steps > 0; steps--)
            StepLogic();
        UpdateFogParticles(deltaTime);
        UpdateActiveCharacterMovement(deltaTime);
        UpdateMarchingFollowers(deltaTime);
        AutoEndTurnWhenDry();
        UpdateHover();
    }

    /// <summary>
    /// One step of the fixed logic clock (1c, 1f): the floor's system advanced by
    /// <see cref="SaveGame.LogicStepSeconds"/>, then what the scene feeds back
    /// while the enemies take their turn — their stand-ins mirrored to where the
    /// logic put them, and, when one has crossed a tile boundary, the sight
    /// refreshed, which tells the system who is in view
    /// (<see cref="TurnSystem.NotifyEnemyVisible"/>, whose zones may react). The
    /// one step live play takes, and the one a quit's settle takes
    /// (<see cref="CampaignBoot.SaveOnQuit"/>), so the settle saves the turn the
    /// player would have met. The game assembly's only caller of
    /// <see cref="TurnSystem.Update"/> outside <c>Logic/</c>.
    /// <para>
    /// A crossing is measured from where each enemy stood when the turn ended
    /// (<see cref="TakeEnemyTiles"/>), never from a tile an earlier turn — or an
    /// earlier session — left behind: so a resumed turn senses the enemies' walk
    /// exactly as the session that saved it would have. Syncing through the
    /// attack phase too catches the final sub-step of a walk, which lands in the
    /// same step the phase flips to <see cref="TurnPhase.EnemyAttacking"/>.
    /// </para>
    /// </summary>
    internal void StepLogic()
    {
        _turns.Update(SaveGame.LogicStepSeconds);
        if (_turns.Phase is not (TurnPhase.EnemyMoving or TurnPhase.EnemyAttacking))
            return;

        bool crossedTile = false;
        foreach (var enemy in _enemies)
        {
            enemy.SyncTransform();
            var tile = LogicTile(enemy.State.X, enemy.State.Y);
            if (_lastEnemyTile.TryGetValue(enemy, out var last) && last == tile) continue;
            _lastEnemyTile[enemy] = tile;
            crossedTile = true;
        }
        if (crossedTile)
            UpdateEnemyVisibility();
    }

    /// <summary>Take where every enemy stands, at the turn's end: the tiles the enemies' turn measures a crossing from (<see cref="StepLogic"/>).</summary>
    private void TakeEnemyTiles()
    {
        foreach (var enemy in _enemies)
            _lastEnemyTile[enemy] = LogicTile(enemy.State.X, enemy.State.Y);
    }

    public override void LateUpdate(float deltaTime)
    {
        base.LateUpdate(deltaTime);

        // Ease the follow target toward the active character, then park the
        // camera so its (fixed) view direction points at the target from the
        // current zoom distance.
        float t = Math.Min(1f, CameraFollowRate * deltaTime);
        _cameraTarget = Vector3.Lerp(_cameraTarget, ActiveCharacter.Position, t);
        _camera.Position = _cameraTarget - _camera.Front * _cameraDistance;
    }

    public override void Render(Camera camera)
    {
        base.Render(camera);
        _hud.Render(camera, this);
    }

    public override void Shutdown()
    {
        _campaign.RunEnded -= OnRunEnded;   // the campaign outlives this scene: the next run's end is the next scene's to show
        _hud.Dispose();
        base.Shutdown();
    }

    // ── The floor being played ───────────────────────────────────────────────
    //
    // One visit, many floors, one scene (§4.5): taking the stairs tears the
    // floor's objects down and builds the next floor's from what the visit
    // holds, in the order below. The order is the design — each step reads what
    // the one before it left:
    //
    //   1. fade out, then TearDownFloor  — every object and every cache of the floor left
    //   2. the visit moves               — Descend/Ascend place everyone, OpenTurns catches the floor up
    //   3. BuildFloorObjects             — walls, stairs, the enemies from their rows, the fog, its emitters
    //   4. the party's stand-ins synced  — the fallen included: they were carried
    //   5. WireTurnSystem                — every handler, on the new floor's system only
    //   6. RederiveGroundItems           — what a permanent kill left here, off the rows;
    //      then LayLoose                 — what the party put down here, off the floor's own list
    //   7. RevealArrival                 — lit only where the party now stands
    //   8. marching, the camera, the banner, and the fade back in
    //
    // Initialize is steps 3 to 8 for the first floor, with nothing to leave —
    // and for a visit a save brought back, steps 7 and 8 resume the saved turn
    // instead of starting one: its sight kept, its active member and its march
    // read back from the save's view (ResumeView).

    /// <summary>
    /// Play the floor the visit now stands on — steps 3 to 8 above — with
    /// <paramref name="turns"/>, the system the visit's
    /// <see cref="DungeonState.OpenTurns"/> returned for it. That system comes
    /// back with the floor already caught up (§4.1: a dummy whose wait ran out
    /// while the party was elsewhere is up and standing where it woke), so the
    /// enemy stand-ins are built from the rows as they now are and nothing here
    /// waits for a revival event: those fired before any handler was wired, to
    /// nobody, and nobody was there to see them. What the party put down here is
    /// laid again from the floor's own list (<see cref="Floor.LooseItems"/>), after
    /// what the corpses are lying on: a weapon left on the first floor is there,
    /// visible and takeable, when the party climbs back.
    /// <para>
    /// <strong>A resumed arrival goes on with the saved turn</strong> (1b, 1f)
    /// when the save carried the turn's view (<paramref name="resume"/>'s
    /// <see cref="ResumeView.View"/>): the saved member is active, what the party
    /// could see this turn stays lit — the pick and the march read it — with the
    /// boxes it stands in added, and the march is what the turn decided, never
    /// decided afresh; the system already holds the turn's counters, the enemies
    /// seen among them, which the lighting leaves as they were. A save that
    /// carried no view was taken before the floor was opened, and its floor is
    /// arrived on as it would have been. Either way the banner was put up by the
    /// scene's constructor, <c>RESUMED</c> over it.
    /// </para>
    /// </summary>
    /// <param name="turns">The system the visit opened on the floor.</param>
    /// <param name="resume">What the host handed a scene resuming a save, on the first floor's arrival; null for every other arrival.</param>
    private void ArriveOnFloor(TurnSystem turns, ResumeView? resume = null)
    {
        _floor = _visit.Current;
        _map = _floor.Map;
        _fogBoxes = _floor.FogBoxes;   // built with the floor, which expanded _map.Corridors in place
        _fog = _floor.Fog;
        Log.Info($"[Map] {_visit.Def.Name}, floor {_floor.Index + 1}: " + string.Join(", ",
            DungeonState.StairsOf(_floor).Select(s => $"{s.Kind} at {s.Tile}"))
            + (_floor.Boss is { } boss
                ? $"; {boss.Title} ({(boss.Alive ? "standing" : "fallen")}) holding {boss.Weapon.Name} at {Displacer.TileOf(boss)}, lair {_floor.Lair}"
                : "")
            + (_floor.LooseItems.Count > 0
                ? $"; left here: {string.Join(", ", _floor.LooseItems.Select(item => $"{item.Weapon.Name} at {item.Tile}"))}"
                : "")
            + $"; turn {turns.TurnCount}");

        BuildFloorObjects();
        foreach (var member in _party)
            member.SyncTransform();
        WireTurnSystem(turns);
        RederiveGroundItems();   // laid without a word: nothing lands as the party arrives (AnnounceDrop)
        foreach (var loose in _floor.LooseItems)
            LayLoose(loose);     // what the party put down here, where it was put

        if (resume?.View is { } kept)
        {
            // The saved turn goes on: its leader, its sight and its march.
            _activeIdx = ActiveIndexFor(_campaign.Party, kept.ActiveMemberId);
            RevealArrival(keepSight: true);
            _marchingThisTurn = kept.MarchingThisTurn;
            Log.Info($"[Save] Resumed on floor {_floor.Index + 1} at turn {turns.TurnCount}: {ActiveCharacter.State.Id} active, "
                + (_marchingThisTurn ? "the turn marching" : "the turn not marching"));
        }
        else
        {
            RevealArrival(keepSight: false);
            EvaluateMarchingForTurn();   // after visibility, which it reads; the party gathered to take the stairs
        }
        _cameraTarget = ActiveCharacter.Position;
        if (resume is null)
            _hud.ShowArrival(_visit);   // a resume's banner, RESUMED over it, is up already (ShowResume)
    }

    /// <summary>
    /// The member a resumed turn hands the lead to: the one the save's view names
    /// (<see cref="ViewDto.ActiveMemberId"/>), while it stands; failing that — no
    /// name, a name no member has, a member fallen — the first living member in
    /// roster order, as a turn's start hands the lead on; and the first member
    /// when nobody stands, which no save point holds.
    /// </summary>
    /// <returns>The index into <paramref name="party"/>, in roster order.</returns>
    internal static int ActiveIndexFor(IReadOnlyList<PartyMemberState> party, string? activeMemberId)
    {
        ArgumentNullException.ThrowIfNull(party);
        for (int i = 0; i < party.Count; i++)
            if (party[i].Id == activeMemberId && party[i].Alive)
                return i;
        for (int i = 0; i < party.Count; i++)
            if (party[i].Alive)
                return i;
        return 0;
    }

    /// <summary>
    /// Step 1: take down everything the floor being left put in the scene, and
    /// forget everything cached about it — the party's stand-ins and the slab
    /// stay. <strong>Every per-floor cache belongs on this list</strong>: one
    /// keyed by a kept object (a member) or holding the old floor's coordinates
    /// survives the swap otherwise and acts on the wrong map — the marching
    /// trail would walk the followers into the new floor's walls, a fog tile
    /// remembered for a member would skip their reveal, a wisp would drift over
    /// the next floor, a floating number would hang where nothing is.
    /// </summary>
    private void TearDownFloor()
    {
        foreach (var obj in _floorObjects)
            RemoveGameObject(obj);
        _floorObjects.Clear();
        _stairMarkers.Clear();
        foreach (var enemy in _enemies)
            RemoveGameObject(enemy);
        _enemies.Clear();
        // Drops' markers and loose items' alike. A loose item itself is the
        // floor's (Floor.LooseItems) and stays on it, to be laid again on a return.
        foreach (var item in _groundItems)
            RemoveGameObject(item.Marker);
        _groundItems.Clear();

        _pendingMistBursts.Clear();
        _hud.ClearFloatingTexts();
        _lastEnemyTile.Clear();
        _riderLevels.Clear();
        _lastFogTile.Clear();
        HoveredEnemy = null;
        HoveredMember = null;
        HoverCue = null;
        AimCue = null;
        AimCaught = Array.Empty<ActorState>();
        _march.Reset();
        _marchLeader = null;
        _wasMarching = false;
    }

    /// <summary>
    /// Step 3, in the order the scene has always built a floor: the walls, the
    /// stair slabs, the enemies' stand-ins, then the fog overlay — after all the
    /// geometry, so its translucent pass blends over everything — and its mist
    /// and wisps after that, so they blend over the shroud.
    /// </summary>
    private void BuildFloorObjects()
    {
        BuildWalls();
        BuildStairs();
        SpawnEnemies();
        BuildFogOverlay();
        BuildFogParticles();
    }

    /// <summary>
    /// Step 7: the floor is lit only where the party now stands
    /// (<see cref="LightArrival"/>). A floor returned to still holds the sight it
    /// had when the party left it, and read as it is that stale sight would arm
    /// the zones of the enemies it showed and price a swap at 20 for dummies
    /// nobody can see — so visibility is cleared first (the explored memory
    /// stays: it is the floor's), then revealed around each living member without
    /// the ripple, the reset <see cref="ResetFogVisibility"/> makes at a turn's
    /// end, less its refog. The enemies in sight are then told to the system as
    /// seen, as any sighting is.
    /// <para>
    /// With <paramref name="keepSight"/> — a resumed turn — the floor's sight is
    /// the turn's own, which the save kept: every tile it had lit stays lit, the
    /// boxes the party stands in are lit too (they already are), and the system
    /// is told nothing, since its counters came back with the turn and already
    /// say who was seen. Telling it afresh would count an enemy the saved turn
    /// had never noticed, and arm its zone where the turn left it quiet.
    /// </para>
    /// </summary>
    private void RevealArrival(bool keepSight)
    {
        _lastFogTile.Clear();   // a member arriving on the (row, column) it left on the other map is revealed, not skipped
        var living = _party.Where(member => member.State.Alive).ToList();
        LightArrival(_fog, _fogBoxes.FogBoxes, living.Select(member => LogicTile(member.State.X, member.State.Y)), keepSight);
        foreach (var member in living)
            _lastFogTile[member] = LogicTile(member.State.X, member.State.Y);
        _fogOverlay.MarkDirty();
        UpdateEnemyVisibility(notify: !keepSight);
    }

    /// <summary>
    /// An arrival's lighting, on <paramref name="fog"/>: every box one of
    /// <paramref name="livingTiles"/> stands in is revealed — lit, and explored —
    /// and, unless <paramref name="keepSight"/>, nothing else is lit: a stairs
    /// arrival clears what the floor last showed first. A resumed turn keeps its
    /// sight (1b: the current floor's <c>visible</c> is saved), so every tile the
    /// save had lit stays lit and the boxes the party stands in are added. The
    /// explored memory is only ever added to.
    /// </summary>
    /// <param name="fog">The floor's fog.</param>
    /// <param name="fogBoxes">The floor's fog boxes, which a tile lights by.</param>
    /// <param name="livingTiles">The tiles the living members stand on, (row, column).</param>
    /// <param name="keepSight">Keep what is lit (a resume) rather than clearing it first (a stairs arrival).</param>
    internal static void LightArrival(FogState fog, IReadOnlyList<MapRect> fogBoxes, IEnumerable<(int R, int C)> livingTiles, bool keepSight)
    {
        ArgumentNullException.ThrowIfNull(fog);
        ArgumentNullException.ThrowIfNull(fogBoxes);
        ArgumentNullException.ThrowIfNull(livingTiles);
        if (!keepSight)
            fog.ResetVisibility();
        foreach (var (r, c) in livingTiles)
            fog.RevealAt(r, c, fogBoxes);
    }

    // ── Setup ────────────────────────────────────────────────────────────────

    /// <summary>The floor slab under the whole map: every floor of a visit is the same 70 x 50, so it is built once and kept across swaps.</summary>
    private void BuildFloor()
    {
        float w = MapGenerator.Cols * WorldSpace.UnitsPerTile;
        float d = MapGenerator.Rows * WorldSpace.UnitsPerTile;
        var floor = new PrimitiveBoxObject(w, FloorThickness, d, FloorColor);
        floor.Position = new Vector3(w / 2f, -FloorThickness / 2f, d / 2f);
        AddGameObject(floor);
    }

    /// <summary>An object the floor being played owns: added to the scene, and taken down with the floor.</summary>
    private void AddFloorObject(GameObject obj)
    {
        AddGameObject(obj);
        _floorObjects.Add(obj);
    }

    private void BuildWalls()
    {
        foreach (var rect in WallMesher.MergeWallTiles(_map.Grid))
        {
            float w = rect.W * WorldSpace.UnitsPerTile;
            float d = rect.H * WorldSpace.UnitsPerTile;
            var wall = new PrimitiveBoxObject(w, WallHeight, d, WallColor);
            wall.Position = new Vector3(
                (rect.X + rect.W / 2f) * WorldSpace.UnitsPerTile,
                WallHeight / 2f,
                (rect.Y + rect.H / 2f) * WorldSpace.UnitsPerTile);
            AddFloorObject(wall);
        }
    }

    /// <summary>A stair's slab: a flat square filling most of its tile, low enough to walk over and to sit under a dummy standing on it.</summary>
    private const float StairSlabWidth = 0.9f;
    private const float StairSlabHeight = 0.06f;

    /// <summary>
    /// One slab per stair the visit says this floor holds
    /// (<see cref="DungeonState.StairsOf"/>), in the stair's colour — the kinds
    /// are the visit's, never restated here. Built before the enemies: a dummy
    /// may stand on the stairs from the start (the stair draw never reads the
    /// rows), and the slab is then drawn under it, guarding the stairs rather
    /// than sealing them. Under the fog like the walls: unseen, it is black;
    /// explored, it shows through the shroud, labelled (the HUD's).
    /// </summary>
    private void BuildStairs()
    {
        foreach (var stair in DungeonState.StairsOf(_floor))
        {
            var slab = new PrimitiveBoxObject(StairSlabWidth, StairSlabHeight, StairSlabWidth, StairColor(stair.Kind))
            {
                Position = WorldSpace.TileCenter(stair.Tile.R, stair.Tile.C, StairSlabHeight / 2f),
            };
            AddFloorObject(slab);
            _stairMarkers.Add(new StairMarker(stair, slab));
        }
    }

    /// <summary>
    /// A stand-in for every member of the campaign's party, in roster order
    /// (§2.1, §5.9) — one colour each by position, and a fallen one's grey
    /// (<see cref="MemberColor"/>). The members are the campaign's: built once for
    /// the whole campaign
    /// (<see cref="CampaignState.Start"/>), their loadout already met, and stood
    /// where the visit put them. The scene supplies only what is the scene's —
    /// the box and its colour. Built once per scene and kept across every swap:
    /// the party is the one thing every floor of the visit shares.
    /// </summary>
    private void SpawnParty()
    {
        var members = _campaign.Party;
        for (int i = 0; i < members.Count; i++)
        {
            var character = new CharacterObject(members[i], MemberColor(members[i], i));
            _party.Add(character);
            AddGameObject(character);
        }

        _activeIdx = 0;
    }

    /// <summary>
    /// The colour a member's stand-in is built in: its place's in the roster, or,
    /// fallen, the corpse's grey — as a death turns it (<c>CharacterDied</c>) and as
    /// an enemy row that is down is built (<see cref="SpawnEnemies"/>). A mid-run
    /// save brings a fallen member back fallen, and its stand-in was built in its
    /// live colour, a corpse that looked like a member standing (Phase 6a's audit).
    /// </summary>
    internal static Vector4 MemberColor(PartyMemberState member, int index)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.Alive ? PartyColors[index] : DeadColor;
    }

    /// <summary>
    /// One stand-in per enemy row of the floor, built from the row as it is now.
    /// The rows are the floor's (<see cref="Floor.Enemies"/>): EnemyPlacer decided
    /// positions and weapons off the floor's seed on its own stream, each keeps
    /// the index it was placed at (§3.2's <see cref="EnemyState.SpawnIndex"/>),
    /// and whatever happened to them while the party was here — or, for a dummy
    /// whose wait ran out, while it was elsewhere — is on them. The bottom
    /// floor's boss is the last row (<see cref="Floor.Boss"/>) and stands in its
    /// lair, larger and stone-grey. A row that is down is built as the corpse it
    /// is — the boss's included, on a floor the party returns to after it fell:
    /// grey and already lying on the floor, not a full-size box with no plate that
    /// nothing can click and anyone can walk through. The scene supplies only
    /// what is the scene's — the box, its size and its colour.
    /// </summary>
    private void SpawnEnemies()
    {
        foreach (var state in _floor.Enemies)
        {
            var enemy = new EnemyObject(state, state.Alive ? LiveColor(state) : DeadColor, state == _floor.Boss ? BossSize : 1f);
            if (!state.Alive)
                enemy.SetDefeatedVisual(true, snap: true);
            _enemies.Add(enemy);
            AddGameObject(enemy);
        }
        Log.Info($"[Map] {_enemies.Count(e => e.State.Alive)} of {_enemies.Count} enemies standing across {_map.DebugRooms.Count} rooms");
    }

    /// <summary>The colour an enemy's stand-in stands in: the boss's stone, every dummy's orange.</summary>
    private Vector4 LiveColor(EnemyState state) => state == _floor.Boss ? BossColor : EnemyColor;

    private void BuildFogOverlay()
    {
        var fogObject = new TransformNodeObject();
        _fogOverlay = fogObject.AddComponent<FogOverlayRenderer>();
        _fogOverlay.Fog = _fog;
        _fogOverlay.Height = WallHeight + 0.05f;
        AddFloorObject(fogObject);
    }

    // Added after the fog overlay so the mist blends over the shroud.
    private void BuildFogParticles()
    {
        var mistObject = new TransformNodeObject();
        _mistEmitter = mistObject.AddComponent<ParticleEmitter>();
        var mist = _mistEmitter.Simulation;
        mist.LifetimeMin = 0.8f;
        mist.LifetimeMax = 1.5f;
        mist.Direction = Vector3.UnitY;
        mist.SpreadDegrees = 80f;
        mist.SpeedMin = 0.2f;
        mist.SpeedMax = 0.55f;
        mist.SizeMin = 0.3f;
        mist.SizeMax = 0.55f;
        mist.EndSizeFactor = 2.8f;
        mist.StartColor = new Vector4(0.13f, 0.13f, 0.17f, 0.6f); // dark smoke, near the fog's black
        mist.EndColor = new Vector4(0.24f, 0.24f, 0.30f, 0f);     // thins toward gray as it fades
        mist.Drag = 1.2f;
        mist.Gravity = new Vector3(0f, 0.25f, 0f); // gentle updraft as the fog dissipates
        AddFloorObject(mistObject);

        var wispObject = new TransformNodeObject();
        _wispEmitter = wispObject.AddComponent<ParticleEmitter>();
        var wisp = _wispEmitter.Simulation;
        wisp.LifetimeMin = 2.5f;
        wisp.LifetimeMax = 4f;
        wisp.Direction = Vector3.UnitY;
        wisp.SpreadDegrees = 90f;
        wisp.SpeedMin = 0.1f;
        wisp.SpeedMax = 0.3f;
        wisp.SizeMin = 0.5f;
        wisp.SizeMax = 1.0f;
        wisp.EndSizeFactor = 1.8f;
        wisp.StartColor = new Vector4(0.10f, 0.10f, 0.14f, 0.16f);
        wisp.EndColor = new Vector4(0.10f, 0.10f, 0.14f, 0f);
        wisp.Drag = 0.4f;
        wisp.Gravity = new Vector3(0f, 0.05f, 0f);
        AddFloorObject(wispObject);
    }

    /// <summary>
    /// Step 5: take up <paramref name="turns"/> — the system the visit opened on
    /// the floor being played, built by the visit with the floor's own seed (so
    /// no two floors share a drop or a revival ladder, §3.2) and on its one
    /// clock — and wire every handler to it. The previous floor's system was
    /// dropped by the visit on the stairs, and its subscriptions die with it:
    /// nothing is unsubscribed because nothing will raise them again.
    /// </summary>
    private void WireTurnSystem(TurnSystem turns)
    {
        _turns = turns;

        _turns.TurnEnded += saved =>
        {
            Log.Info($"[Turns] End of turn — banked {saved} movement");
            LastBankedMovement = saved;
            if (InventoryOpen) ActiveMenu = GameMenu.None;   // the bag closes at the turn's end, and its "leave" is disarmed with it
            ResetFogVisibility();
            TakeEnemyTiles();   // what the enemies' turn measures a crossing from (StepLogic)
        };

        _turns.PlayerTurnStarted += OnPlayerTurnStarted;

        _turns.EnemyHit += (enemy, res) =>
        {
            Log.Info($"[Combat] Enemy hit: roll {res.Roll.Roll} ({res.Roll.Outcome}) for {res.Taken} of {res.Dealt} dealt (blocked {res.Blocked}, ward {res.WardSpent}) — enemy HP {enemy.Hp}");
            SpawnAttackTexts(enemy, EnemyObjectFor(enemy).Position, res);
        };

        _turns.CharacterHit += (c, res) =>
        {
            Log.Info($"[Combat] {c.Id} hit: roll {res.Roll.Roll} ({res.Roll.Outcome}) for {res.Taken} of {res.Dealt} dealt (blocked {res.Blocked}, ward {res.WardSpent}) — HP {c.Hp}");
            var obj = _party.First(p => p.State == c);
            SpawnAttackTexts(c, obj.Position, res);
        };

        _turns.CharacterDied += c =>
        {
            Log.Info($"[Combat] {c.Id} died");
            var obj = _party.First(p => p.State == c);
            SetColor(obj, DeadColor);
            if (obj == ActiveCharacter)
                SetActiveCharacter(_party.FindIndex(p => p.State.Alive), force: true);
        };

        _turns.EnemyDefeated += enemy =>
        {
            Log.Info("[Combat] Enemy defeated!");
            var obj = EnemyObjectFor(enemy);
            SetColor(obj, DeadColor);
            obj.SetDefeatedVisual(true); // eases down to a walkable floor remnant
            _hud.AddFloatingText(obj.Position, "DEFEATED!", new Vector4(1f, 1f, 1f, 1f), -54f);
        };

        // A defeat with resurrection stopped is the permanent kill (§3.2): the
        // weapon the dummy was carrying is on the floor where it fell, and the
        // turn system has already rolled it off the floor's own seed — the boss's
        // off its own table, forged with its dungeon's theme (§4.3). A unique says
        // so the moment it lands (AnnounceDrop). A corpse already lying on its item
        // is neither laid nor named again: a dummy that fell after the boss in the
        // same raise was laid, and named, by the boss's fall, whose held death ran
        // first — and then its own defeat hands the same item over.
        _turns.EnemyDropped += (corpse, drop) =>
        {
            if (DropOnFloor(corpse, drop) is { } laid)
                AnnounceDrop(laid);
        };

        // The boss is down (§4.3). The visit heard first — its handler was on this
        // system before this scene's — so resurrection has stopped here and on every
        // floor opened from now on, and the boss's own drop is already on the
        // floor (EnemyDropped fired before this). Whatever else is down is a
        // corpse that stays down now, and a corpse that stays down is lying on
        // what it was carrying: the rule every arrival reads, applied the moment it
        // starts being true. This is the moment those items land — a dummy that
        // fell earlier in the same swing, or one that was waiting to get up, has
        // shown nobody its item until now — so a unique among them says so as it
        // lands, exactly as a permanent kill's does. Then the banner says what it
        // means, and what the boss left behind.
        _turns.BossDefeated += boss =>
        {
            Log.Info($"[Run] {boss.Title ?? "The boss"} falls at turn {_visit.BossDefeatedAtTurn}: nothing revives for the rest of the visit, and every corpse is lying on its weapon");
            foreach (var laid in RederiveGroundItems())
                AnnounceDrop(laid);
            _hud.ShowBossFall(boss, _groundItems.FirstOrDefault(item => item.Corpse == boss)?.Drop);
        };

        _turns.EnemyResurrected += enemy =>
        {
            Log.Info("[Combat] Enemy resurrected!");
            var obj = EnemyObjectFor(enemy);
            SetColor(obj, LiveColor(enemy));
            obj.SetDefeatedVisual(false);
            obj.SyncTransform(); // resurrection may have relocated it off an occupied tile
            UpdateEnemyVisibility();

            // The beat that ties the count to the statline (§4.5): what this
            // revival's rolls added, over the dummy, if it got up in sight.
            var (damage, health) = obj.TakeRevivalGains();
            if (DungeonHud.RevivalBeat(damage, health) is { } beat)
            {
                Log.Info($"[Combat] Revived x{enemy.DefeatCount}: {beat}");
                if (obj.IsActive)
                    _hud.AddFloatingText(obj.Position, beat, StrongerColor, -64f);
            }
        };

        _turns.BraceTriggered += c =>
        {
            Log.Info($"[Combat] {c.Id} braces!");
            var obj = _party.First(p => p.State == c);
            _hud.AddFloatingText(obj.Position, "BRACE!", new Vector4(0.53f, 1f, 1f, 1f), -52f);
        };

        _turns.EnemyBraceTriggered += enemy =>
        {
            Log.Info("[Combat] Enemy braces!");
            _hud.AddFloatingText(EnemyObjectFor(enemy).Position, "BRACE!", new Vector4(1f, 0.6f, 0.4f, 1f), -52f);
        };

        _turns.CharacterBuffed += (c, effect) =>
            Log.Info($"[Combat] {c.Id} now carries {effect.Type} Lv{effect.Levels}");

        // A credit that bought something says so over whoever earned it (§2.2).
        // The beat is only a beat: what a credit was worth was settled by the
        // applier that raised this, and the label states the member's own state
        // -- no XP rule lives out here.
        _turns.XpCredited += (member, credit, gained) =>
        {
            string label = DungeonHud.LevelUpLabel(member, credit);
            Log.Info($"[Progress] {member.Id} {label} (+{gained})");
            if (TryObjectFor(member, out var obj) && obj.IsActive)
                _hud.AddFloatingText(obj.Position, label, LevelUpColor, -64f);
        };

        // The mana an entry spent burned the circle a step deeper (§3.3). The
        // same beat as a level-up and for the same reason: the applier settled
        // it, and the label only states what the entry now reads. It is the
        // wielder's beat whichever side is wielding -- an enemy healer's staff
        // levels by casting too -- so a dummy's tier-up simply finds no object
        // to float over.
        _turns.EnchantmentTiered += (wielder, weapon, entry) =>
        {
            Log.Info($"[Progress] {weapon.Name}: {entry.Def.Name} reaches tier {entry.Tier}");
            if (TryObjectFor(wielder, out var obj) && obj.IsActive)
                _hud.AddFloatingText(obj.Position, $"{entry.Def.Name.ToUpperInvariant()} T{entry.Tier}!", LevelUpColor, -64f);
        };

        // The hit that filled a member's weapon to its wear capacity (§6.1). The
        // weapon line already says FULL where the player reads it (WearReadout);
        // the log says when, once, since a full weapon banks nothing more.
        _turns.WeaponWornFull += (member, weapon) =>
            Log.Info($"[Wear] {member.Id}'s {weapon.Name} is full: wear {weapon.Wear} of {weapon.WearCapacity}");

        _turns.CharacterHealed += (c, amount) =>
        {
            Log.Info($"[Combat] {c.Id} regenerates {amount} — HP {c.Hp}");
            var obj = _party.First(p => p.State == c);
            _hud.AddFloatingText(obj.Position, $"+{amount}", HealColor, 8f);
        };

        _turns.EnemyBuffed += (enemy, effect) =>
            Log.Info($"[Combat] Enemy now carries {effect.Type} Lv{effect.Levels}");

        // A cast's status landing, whoever cast it on whom: the label is the
        // status's own name; the colour says whether it came from the target's
        // side. The cast rolled just before its status landed — nothing rolls
        // in between — so the last natural roll is the cast's: a crit, which
        // doubled the levels, leads the label with CRIT!, and the roll is
        // called over the caster once per cast (a crit halves the cast's
        // mana, a fumble doubles it). A party member's cast that lands no
        // status is called where it was made instead (CastStaff).
        _turns.ActorStatusApplied += (target, effect, source) =>
        {
            bool crit = CastRollOf(source, _lastRoll).IsCrit;
            bool hostile = (target is PartyMemberState) != (source is PartyMemberState);
            if (TryObjectFor(target, out var obj) && obj.IsActive)
                _hud.AddFloatingText(obj.Position, $"{(crit ? "CRIT! " : "")}{DungeonHud.StatusName(effect.Type, effect.Element)} Lv{effect.Levels}",
                    crit ? CritColor : hostile ? TickColor : HealColor, -52f);
            CallCastRoll(source, _lastRoll, _rollCount);
        };

        _turns.EnemyHealed += (enemy, amount) =>
        {
            Log.Info($"[Combat] Enemy regenerates {amount} — HP {enemy.Hp}");
            var obj = EnemyObjectFor(enemy);
            if (obj.IsActive)
                _hud.AddFloatingText(obj.Position, $"+{amount}", HealColor, 8f);
        };

        _turns.ActorStatusTicked += (actor, tick) =>
        {
            Log.Info($"[Combat] {tick.Type} ticks {tick.Damage} — HP {actor.Hp}");
            if (TryObjectFor(actor, out var obj) && obj.IsActive)
                _hud.AddFloatingText(obj.Position, $"-{tick.Damage} {DungeonHud.StatusName(tick.Type, tick.Element)}", TickColor, 8f);
        };

        _turns.EnemyFleeing += enemy =>
        {
            Log.Info("[Combat] Enemy healer flees!");
            var obj = EnemyObjectFor(enemy);
            if (obj.IsActive)
                _hud.AddFloatingText(obj.Position, "FLEE!", new Vector4(1f, 0.8f, 0.3f, 1f), -52f);
        };

        _turns.ActorDisplaced += (actor, tiles) =>
        {
            Log.Info($"[Combat] shoved {tiles} tile(s)");
            if (!TryObjectFor(actor, out var obj)) return;
            switch (obj)
            {
                case CharacterObject member:
                    member.SyncTransform();
                    UpdateFogFor(member);
                    break;
                case EnemyObject enemy:
                    enemy.SlideToState(); // eases to where the blow left it rather than teleporting
                    UpdateEnemyVisibility(); // shoved into the fog, it hides and lands on its tile
                    break;
            }
            if (obj.IsActive)
                _hud.AddFloatingText(obj.Position, $"SHOVED {tiles}", new Vector4(1f, 0.75f, 0.45f, 1f), -60f);
        };

        _turns.OpportunistTriggered += actor => AnnounceReaction(actor, "OPPORTUNIST!");
        _turns.OverwatchTriggered += actor => AnnounceReaction(actor, "OVERWATCH!");
        _turns.RiposteTriggered += actor => AnnounceReaction(actor, "RIPOSTE!");

        // The wipe has already ended the run by now (§4.4): the visit heard the
        // game over first and the campaign ended the run in Logic, whose
        // RunEnded put the summary up. What is left here is the log line.
        _turns.GameOver += () => Log.Info("[Turns] GAME OVER - the party is defeated");
    }

    /// <summary>
    /// A player turn has begun: a fallen leader hands the lead on, the turn's
    /// march is decided, and the turn is saved once the frame is over (1f: an
    /// autosave at every player turn's start) — asked for here and written by the
    /// host after the raise that began the turn has returned.
    /// </summary>
    private void OnPlayerTurnStarted()
    {
        Log.Info($"[Turns] Player turn {_turns.TurnCount} begins");
        if (!ActiveCharacter.State.Alive)
            SetActiveCharacter(_party.FindIndex(p => p.State.Alive), force: true);
        EvaluateMarchingForTurn(); // after the leader fixup: distances measure from a live leader
        _host.RequestSave();
    }

    /// <summary>A reaction's floating callout over whoever fired it, on either side.</summary>
    private void AnnounceReaction(ActorState actor, string label)
    {
        Log.Info($"[Combat] {label}");
        if (TryObjectFor(actor, out var obj) && obj.IsActive)
            _hud.AddFloatingText(obj.Position, label, new Vector4(0.53f, 1f, 1f, 1f), -52f);
    }

    private void WireInput()
    {
        var input = Input!;

        input.SubscribeToKeyPressed(_ => _up = true, Keys.W, Keys.Up);
        input.SubscribeToKeyReleased(_ => _up = false, Keys.W, Keys.Up);
        input.SubscribeToKeyPressed(_ => _down = true, Keys.S, Keys.Down);
        input.SubscribeToKeyReleased(_ => _down = false, Keys.S, Keys.Down);
        input.SubscribeToKeyPressed(_ => _left = true, Keys.A, Keys.Left);
        input.SubscribeToKeyReleased(_ => _left = false, Keys.A, Keys.Left);
        input.SubscribeToKeyPressed(_ => _right = true, Keys.D, Keys.Right);
        input.SubscribeToKeyReleased(_ => _right = false, Keys.D, Keys.Right);

        // KeyPressed fires once per physical press (the engine routes OS
        // auto-repeat to KeyRepeated), so one-shot actions like Tab-cycling
        // never machine-gun while held. Every press below does nothing while a
        // stair's fade runs (Transiting); the releases above never wait on it.
        //
        // Every key press, once more, through one handler with no key list
        // (AnyKey): it clears what the pause menu answered, and the number keys, 1
        // to HighestSlotKey, go on to one routed handler (NumberKey): they equip bag
        // slots while the bag is open, pick a pause-menu option while it is, and
        // select party members otherwise. The number keys have no subscription of
        // their own: beside this one, it could run first, and the answer 2 gives
        // would be cleared by the very key that gave it. Every other key below
        // already does nothing behind a menu (AnyMenuOpen).
        input.SubscribeToKeyPressed(e => AnyKey(e.Key));
        input.SubscribeToKeyPressed(_ => { if (!AnyMenuOpen && !Transiting) CycleActiveCharacter(); }, Keys.Tab);

        // Space ends the turn and nothing else; ENTER is routed by the menu open
        // — it ends the turn with none, and says yes to leaving the dungeon.
        input.SubscribeToKeyPressed(_ => { if (!AnyMenuOpen && !Transiting) _turns.EndTurn(); }, Keys.Space);
        input.SubscribeToKeyPressed(_ => EnterKey(), Keys.Enter, Keys.KeyPadEnter);
        // E takes the stairs the active member is standing on (§4.2), and says
        // yes to leaving when the way out has asked.
        input.SubscribeToKeyPressed(_ => StairKey(), Keys.E);
        input.SubscribeToKeyPressed(_ => ToggleInventory(), Keys.I, Keys.B);
        // X, in the bag, arms "leave": the next bag number puts that weapon down
        // where the active member stands, rather than equipping it (§4.4).
        input.SubscribeToKeyPressed(_ => LeaveKey(), Keys.X);
        // G takes what the active member is standing on (§3.5): a drop, or a
        // weapon the party put down.
        input.SubscribeToKeyPressed(_ => { if (!AnyMenuOpen && !Transiting) PickUp(); }, Keys.G);
        // O holds fire: a ranged weapon with Overwatch banks its shot against
        // whatever walks into reach on the enemy turn. The member says so — the
        // readout then shows the shots held — or says why it cannot.
        input.SubscribeToKeyPressed(_ =>
        {
            if (AnyMenuOpen || Transiting) return;
            var state = ActiveCharacter.State;
            if (_turns.TryOverwatch(state))
            {
                Log.Info($"[Combat] {state.Id} holds fire");
                _hud.AddFloatingText(ActiveCharacter.Position, "HOLDING FIRE", ReactionColor, -52f);
            }
            else if (OverwatchRefusal(state) is { } why)
            {
                _hud.AddFloatingText(ActiveCharacter.Position, why, CueColor, -52f);
            }
        }, Keys.O);
        // N casts a Nova: the one wand shape centred on the caster, so it needs
        // no aim. The other three are aimed by a click on the floor or on an enemy.
        input.SubscribeToKeyPressed(_ =>
        {
            if (AnyMenuOpen || Transiting) return;
            var state = ActiveCharacter.State;
            var wand = state.EquippedWeapon;
            if (wand?.AreaShape?.Kind != AreaShapeKind.Nova) return;
            if (!CastArea(state, (state.X, state.Y)))
                SayRefusal(ActiveCharacter, AreaCue(state, wand, (state.X, state.Y)).Cue);
        }, Keys.N);

        input.SubscribeToMouseMoved(e => _mousePos = e.Position);
        input.SubscribeToMouseButtonPressed(_ => HandleClick(), MouseButton.Left);

        input.SubscribeToMouseScroll(e =>
        {
            _cameraDistance = Math.Clamp(
                _cameraDistance - e.OffsetY * 1.5f, MinCameraDistance, MaxCameraDistance);
        });
    }

    // ── Party control ────────────────────────────────────────────────────────

    /// <summary>
    /// The highest number key the scene binds: the top of the number row. With
    /// the bag open a key equips the bag slot printed with its number — the
    /// panel numbers slots from 1, the weapon in hand being 1 — so keys 2 up
    /// reach every bag slot a member carries: 2-6 for the six of §4.4's carry,
    /// and 2-9 at the deepest carry the loader admits. That ceiling is
    /// <see cref="GameConstants.MaxInventorySlots"/>, refused past at load
    /// (<see cref="ContentValidator.RuleEverySlotHasAKey"/>) and held at or
    /// under this number by test, so no slot a member carries is beyond its
    /// key. The inventory panel's key hint reads its range off the same number
    /// (<see cref="DungeonHud.EquipHint"/>), so the hint and the bindings
    /// cannot drift apart.
    /// </summary>
    internal const int HighestSlotKey = 9;

    /// <summary>
    /// Every key press, once (<see cref="WireInput"/>): a number key goes on to
    /// <see cref="NumberKey"/>. Any key clears what the pause menu answered — it
    /// stays until the next key — before the key does anything else. Nothing during
    /// a stair's fade, and nothing once play has been left (<see cref="NumberKey"/>'s
    /// 3): the screen is fading to the title with the menu still up, and a key
    /// read then — a save asked for, the menu closed and the turn played on — would
    /// act on a campaign the host has let go. Internal, so a scene built with no
    /// window can be walked key by key through its menus.
    /// </summary>
    internal void AnyKey(Keys key)
    {
        if (Transiting || _leaving) return;
        if (ActiveMenu == GameMenu.Pause)
            _answer = null;
        if (key >= Keys.D1 && key <= Keys.D0 + HighestSlotKey)
            NumberKey(key - Keys.D0);
    }

    /// <summary>
    /// What a save by hand came to, from the host once it is written at the frame's
    /// end (<see cref="ISaveHost.SaveNow"/>): drawn inside the pause menu, under its
    /// lines, until the next key or until the menu closes — never as a notice, which
    /// is not drawn over a menu and would age out unseen behind it. With the menu
    /// closed by then, only the log keeps it.
    /// </summary>
    /// <param name="notice">What the save came to: <c>SAVED TO SLOT 2</c>, or why not.</param>
    internal void SlotSaved(string notice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notice);
        if (ActiveMenu == GameMenu.Pause)
            _answer = notice;
    }

    /// <summary>
    /// A number key, routed by the menu that is open (§4.4: the swap keys
    /// generalise to every slot): with the bag open, 2 and up swap the bag slot
    /// printed with that number into the hand — 1 is the hand itself — or, with
    /// the bag's X armed, put that slot's weapon down (<see cref="LeaveSlot"/>);
    /// on the pause menu (<see cref="PauseMenu.Press"/>), 1 closes it, 2 asks the
    /// host to save the campaign into its own slot on the party's own turn — the
    /// menu kept up for the answer — and otherwise says why not, and 3 quits to
    /// the title — the host writes the campaign's slot first, playing out the enemies' turn
    /// if the menu was opened during it (<see cref="ISaveHost.QuitToTitle"/>) — or,
    /// in a dev-override session, which has no title, closes the game, whose close
    /// saves its scratch slot as ever — either way play is left, and the scene takes
    /// no key after it (<see cref="AnyKey"/>); every other number does nothing there
    /// (never a member selected behind the menu); with no menu open, 1-4 select a
    /// party member. Behind any other menu — the way out's "leave the dungeon?"
    /// among them — a number key does nothing at all: it never selects a member
    /// behind a prompt. Nor during a stair's fade, nor once play is left.
    /// </summary>
    private void NumberKey(int digit)
    {
        if (Transiting || _leaving) return;
        if (PauseMenuOpen)
        {
            var (action, said) = PauseMenu.Press(digit, CanSaveNow, _host.HasTitle);
            switch (action)
            {
                case PauseAction.Close:
                    ActiveMenu = GameMenu.None;
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
                    Log.Info("[Run] QUIT TO TITLE");
                    _host.QuitToTitle();
                    break;
                case PauseAction.QuitGame:
                    _leaving = true;
                    Log.Info("[Run] QUIT GAME: a dev-override session has no title");
                    _game.Close();
                    break;
            }
        }
        else if (InventoryOpen)
        {
            int slot = digit - 1;   // the printed number is the slot plus one
            if (slot > 0 && slot < ActiveCharacter.State.Inventory.Length)
            {
                if (LeaveArmed)
                    LeaveSlot(slot);
                else
                    EquipSlot(slot);
            }
        }
        else if (!AnyMenuOpen)
        {
            SetActiveCharacter(digit - 1);   // a number past the party selects nobody
        }
    }

    private void SetActiveCharacter(int idx, bool force = false)
    {
        if (idx < 0 || idx >= _party.Count) return;
        if (!force && idx == _activeIdx) return;
        if (!_party[idx].State.Alive) return;
        _activeIdx = idx;
    }

    private void CycleActiveCharacter()
    {
        for (int step = 1; step <= _party.Count; step++)
        {
            int idx = (_activeIdx + step) % _party.Count;
            if (_party[idx].State.Alive)
            {
                _activeIdx = idx;
                return;
            }
        }
    }

    /// <summary>
    /// I opens the bag and closes it — from no menu or from the bag only, so it
    /// never swaps a prompt (the pause menu, "leave the dungeon?", the run's
    /// summary) for the bag behind the player's back — and never during a stair's
    /// fade or on the defeat screen. Closing it disarms its X
    /// (<see cref="ActiveMenu"/>).
    /// </summary>
    private void ToggleInventory()
    {
        if (Transiting || _turns.Phase == TurnPhase.GameOver) return;
        if (ActiveMenu is not (GameMenu.None or GameMenu.Inventory)) return;
        ActiveMenu = InventoryOpen ? GameMenu.None : GameMenu.Inventory;
    }

    /// <summary>
    /// Escape closes whatever menu is open — on "leave the dungeon?" that is the
    /// no — or opens the pause menu (<see cref="PauseMenu"/>), whose 2 saves the
    /// campaign into its own slot and whose 3 quits to the title. During a stair's
    /// fade, once the party is on its way to the screen between runs, and once play
    /// has been left from the pause menu, it is swallowed: nothing opens or closes
    /// over a floor being swapped or a scene fading out. <strong>On a run's end it
    /// does nothing</strong> — the summary, and the defeat screen under it. The summary
    /// is modal: ENTER is the one way on, and the screen between runs opens the
    /// menu. Nothing would be lost by a quit there — the run's end is saved once the
    /// frame it ended in is over, the summary with it — but a pause menu over a run
    /// that has ended would offer to save a run there is no longer any of. Always
    /// consumed, so the game never quits behind the scene's back.
    /// </summary>
    public bool HandleEscape()
    {
        if (Transiting || _leaving) return true;
        if (ActiveMenu == GameMenu.RunSummary || _turns is { Phase: TurnPhase.GameOver }) return true;   // no floor open yet is no game over
        ActiveMenu = AnyMenuOpen ? GameMenu.None : GameMenu.Pause;
        return true;
    }

    /// <summary>
    /// Swap an inventory slot with the equipped slot (slot 0) through the turn
    /// system, which prices it — 20 movement once combat has begun, free out of
    /// it — refuses an empty slot or a budget too short, and re-arms the
    /// member's threat zone for the new reach (a held shot lapses with the
    /// weapon that held it).
    /// </summary>
    private void EquipSlot(int slot)
    {
        var state = ActiveCharacter.State;
        if (_turns.TrySwap(state, slot))
            Log.Info($"[Combat] {state.Id} equips {state.EquippedWeapon?.Name} (swap cost {_turns.SwapCost})");
        else
            Log.Info($"[Combat] {state.Id} cannot swap to slot {slot} (cost {_turns.SwapCost}, movement {state.DistLeft:0})");
    }

    /// <summary>
    /// X, in the bag (§4.4): arm "leave", so the next bag number puts that slot's
    /// weapon down where the active member stands rather than equipping it, and
    /// the bag's key hint becomes <c>LEAVE WHICH? 2-6 - X CANCELS</c>. X again
    /// disarms it. It arms only in the party's own phase, which is when a member
    /// acts at all; outside the bag, or during a stair's fade, it does nothing
    /// and says nothing.
    /// </summary>
    private void LeaveKey()
    {
        if (Transiting || !InventoryOpen) return;
        if (_leaveArmed)
        {
            _leaveArmed = false;
            return;
        }
        if (_turns.Phase != TurnPhase.Player || !ActiveCharacter.State.Alive) return;
        _leaveArmed = true;
    }

    /// <summary>
    /// With the bag's X armed, a bag number (§4.4): the active member puts that
    /// slot's weapon down on the floor it stands on. The floor keeps it for the
    /// rest of the visit (<see cref="Floor.Leave"/>). Its marker goes down on the
    /// member's tile, where <c>G</c> takes it back like anything else lying there.
    /// It costs nothing: it is not a swap, and the weapon in hand stays in hand.
    /// A weapon put down disarms X. An empty slot says so, as a full bag says
    /// <c>BAG FULL</c>, and leaves X armed for another pick. What was left says so
    /// over the member in place of any answer still rising there, such as that
    /// <c>SLOT 3 IS EMPTY</c> or the <c>BAG FULL</c> that sent the player to the bag
    /// (<see cref="DungeonHud.AddAnswer"/>), so the two never stack on the open panel.
    /// </summary>
    private void LeaveSlot(int slot)
    {
        var member = ActiveCharacter.State;
        if (_turns.Phase != TurnPhase.Player || !member.Alive) return;
        if (member.Inventory[slot] is null)
        {
            SayRefusal(ActiveCharacter, new ClickCue(DungeonHud.EmptySlotCue(slot), false));
            return;
        }

        var loose = _floor.Leave(member, slot);
        _leaveArmed = false;
        LayLoose(loose);
        Log.Info($"[Loot] {member.Id} leaves {loose.Weapon.Name} from slot {slot + 1} on floor {_floor.Index + 1} at {loose.Tile}");
        _hud.AddAnswer(ActiveCharacter, ActiveCharacter.Position, DungeonHud.LeftBeat(loose.Weapon), LootColor, -52f);
    }

    private void UpdateActiveCharacterMovement(float deltaTime)
    {
        if (Transiting || _turns.Phase != TurnPhase.Player || AnyMenuOpen) return;

        var state = ActiveCharacter.State;
        if (!state.Alive || state.DistLeft <= 0f) return;

        float vx = _left ? -GameConstants.Speed : _right ? GameConstants.Speed : 0f;
        float vy = _up ? -GameConstants.Speed : _down ? GameConstants.Speed : 0f;
        if (vx == 0f && vy == 0f) return;

        if (vx != 0f && vy != 0f)
        {
            vx *= 0.707f;
            vy *= 0.707f;
        }

        // Cap this frame's intended travel to the remaining budget. While
        // marching, the slowest member sets the group's pace: the leader can
        // spend no more than the smallest budget left in the party, so nobody
        // gets left behind.
        float budget = state.DistLeft;
        if (Marching)
        {
            foreach (var member in _party)
                if (member.State.Alive)
                    budget = MathF.Min(budget, member.State.DistLeft);
            if (budget <= 0f) return;
        }

        float dx = vx * deltaTime;
        float dy = vy * deltaTime;
        float frameDist = MathF.Sqrt(dx * dx + dy * dy);
        if (frameDist > budget)
        {
            float scale = budget / frameDist;
            dx *= scale;
            dy *= scale;
        }

        // Walls and live enemies always block. In combat the party body-blocks
        // too — blockers are immovable circles here, so this is safe from the
        // two-body wall shoves that made the prototype drop inter-party
        // colliders. While marching, followers never block the leader, or
        // reversing through your own line would deadlock it.
        var blockers = LiveEnemyBlockers();
        if (!Marching)
        {
            foreach (var member in _party)
                if (member != ActiveCharacter && member.State.Alive)
                    blockers.Add(new GridCollision.Circle(member.State.X, member.State.Y, member.State.Radius));
        }
        var (nx, ny) = GridCollision.Move(_map.Grid, state.X, state.Y, state.Radius, dx, dy, blockers);

        // Budget depletes by distance actually travelled, so pushing into a
        // wall costs nothing while sliding along it costs the slide.
        float movedX = nx - state.X;
        float movedY = ny - state.Y;
        float moved = MathF.Sqrt(movedX * movedX + movedY * movedY);
        state.DistLeft = MathF.Max(0f, state.DistLeft - moved);

        state.X = nx;
        state.Y = ny;
        ActiveCharacter.SyncTransform();
        UpdateFogFor(ActiveCharacter);
        _turns.NotifyCharacterMoved(state); // spear enemies brace against walk-ins
    }

    // ── The stairs ───────────────────────────────────────────────────────────

    /// <summary>
    /// E (§4.2): take the stairs the active member is standing on. Whether the
    /// party may is the visit's to say (<see cref="DungeonState.CanUse"/>) — the
    /// player phase, and every living member gathered — and no enemy count
    /// enters into it (§4.4: no gate). With a menu open, during a fade, or
    /// outside the player phase it does nothing and says nothing, and off every
    /// stair it does nothing; with the party short it says how short. Down and
    /// up fade and swap the floor; the way out asks first, and under that
    /// prompt E is the yes.
    /// </summary>
    private void StairKey()
    {
        if (Transiting) return;
        if (ActiveMenu == GameMenu.ConfirmLeave)
        {
            Leave();
            return;
        }
        if (AnyMenuOpen || _turns.Phase != TurnPhase.Player) return;

        var active = ActiveCharacter.State;
        if (_visit.StairUnder(active) is not { } stair) return;

        var party = _campaign.Party;
        var refusal = _visit.CanUse(stair, party, active);
        if (refusal is not null)
        {
            if (DungeonHud.StairCue(stair, refusal, _visit.Gathered(party, stair.Tile), LivingMembers()) is { } why)
                SayRefusal(ActiveCharacter, new ClickCue(why, false));
            return;
        }

        if (stair.Kind == Stair.Exit)
            ActiveMenu = GameMenu.ConfirmLeave;
        else
            BeginTransit(stair);
    }

    /// <summary>ENTER, routed by the menu that is open: on to the screen between runs from the run's summary, the yes to leaving under the way out's prompt, the end of the turn with no menu, nothing behind any other menu or during a fade.</summary>
    private void EnterKey()
    {
        if (Transiting) return;
        if (ActiveMenu == GameMenu.RunSummary)
            ContinueAfterRun();
        else if (ActiveMenu == GameMenu.ConfirmLeave)
            Leave();
        else if (!AnyMenuOpen)
            _turns.EndTurn();
    }

    /// <summary>The living members: the party's size as far as gathering at the stairs goes, the fallen being carried.</summary>
    private int LivingMembers() => _campaign.Party.Count(member => member.Alive);

    /// <summary>
    /// Start down or up <paramref name="stair"/>: the screen fades to black over
    /// <see cref="TransitFadeSeconds"/>, the floor is swapped at black
    /// (<see cref="TakeStairs"/>), and the new floor fades in over as long again.
    /// Nothing is pressed and nobody walks while it runs.
    /// </summary>
    private void BeginTransit(StairUse stair)
    {
        Log.Info($"[Stairs] {ActiveCharacter.State.Id} leads the party {(stair.Kind == Stair.Down ? "down" : "up")} from floor {_floor.Index + 1}");
        _transitStair = stair;
        StartFadeOut(Transit.FadingOut);
    }

    /// <summary>
    /// Start a fade to black away from the floor being played: the stairs'
    /// (<see cref="Transit.FadingOut"/>) or, once the run is over and its summary
    /// read, the run's own (<see cref="Transit.Leaving"/>). The floor's arrival
    /// banner comes down first (<see cref="DungeonHud.HideArrival"/>). It is
    /// drawn over the fade, which is how the next floor's is up at black, so one
    /// still up from the arrival — a quick hop on — would otherwise hang on the
    /// black naming the floor the party is leaving.
    /// </summary>
    private void StartFadeOut(Transit fade)
    {
        _hud.HideArrival();
        _transit = fade;
        _transitClock = 0f;
    }

    /// <summary>
    /// The stairs' clock, once a frame: the fade out, the swap at black, the fade
    /// in; and, once the run's summary has been read, the fade to black after
    /// which the run is handed back to the host — once, whatever happens to this
    /// scene after.
    /// </summary>
    private void AdvanceTransit(float deltaTime)
    {
        switch (_transit)
        {
            case Transit.FadingOut:
                _transitClock += deltaTime;
                if (_transitClock < TransitFadeSeconds) return;
                TakeStairs(_transitStair);
                _transit = Transit.FadingIn;
                _transitClock = 0f;
                return;

            case Transit.FadingIn:
                _transitClock += deltaTime;
                if (_transitClock < TransitFadeSeconds) return;
                _transit = Transit.None;
                _transitClock = 0f;
                return;

            case Transit.Leaving:
                _transitClock = MathF.Min(_transitClock + deltaTime, TransitFadeSeconds);
                if (_transitClock < TransitFadeSeconds || _runOverSent) return;
                _runOverSent = true;
                _host.RunOver();
                return;
        }
    }

    /// <summary>
    /// The swap, at black — steps 1 to 8 of the floor's lifecycle, in order:
    /// the floor left is taken down, the visit moves the party (every member,
    /// the fallen included) and opens the next floor caught up, and that floor is
    /// built from what the visit holds. Asked of the visit once more first:
    /// nothing can have moved under the fade, but a refusal here swaps nothing
    /// and the fade simply comes back up on the floor the party never left. The
    /// arrival is saved once the frame is over (1f: an autosave after every floor
    /// arrival), so a quit on the new floor never finds the save on the old one.
    /// </summary>
    private void TakeStairs(StairUse stair)
    {
        var party = _campaign.Party;
        var active = ActiveCharacter.State;
        if (_visit.StairUnder(active) != stair || _visit.CanUse(stair, party, active) is not null)
        {
            Log.Warn($"[Stairs] The {stair.Kind} stairs at {stair.Tile} were no longer the party's to take: nothing moved");
            return;
        }

        TearDownFloor();
        if (stair.Kind == Stair.Down)
            _visit.Descend(party, active);
        else
            _visit.Ascend(party, active);
        ArriveOnFloor(_visit.OpenTurns(party, RollD20));
        _host.RequestSave();
    }

    /// <summary>
    /// The yes to "leave the dungeon?": the party walks out of the first floor's
    /// way out and the run ends there, in the campaign
    /// (<see cref="CampaignState.EndRun"/>) — a boss kill or a retreat, the
    /// spoils in the bags kept, the tick, the lock or the retirement written, and
    /// this visit discarded whole: every floor, every row, every farm (§4.1). The
    /// campaign's <see cref="CampaignState.RunEnded"/> puts the summary up
    /// (<see cref="OnRunEnded"/>). The way out is asked of the visit again first;
    /// nothing can have moved under the prompt, but if it has the prompt is simply
    /// withdrawn.
    /// </summary>
    private void Leave()
    {
        ActiveMenu = GameMenu.None;
        var active = ActiveCharacter.State;
        if (_visit.StairUnder(active) is not { Kind: Stair.Exit } exit || _visit.CanUse(exit, _campaign.Party, active) is not null)
            return;

        Log.Info($"[Run] The party leaves {_visit.Def.Name} at turn {_turns.TurnCount}: the visit is discarded whole");
        _campaign.EndRun(RunEnding.Exited);
    }

    /// <summary>
    /// The run is over (§4.4), by the way out or by a wipe — which ends it in
    /// Logic, inside the turn system's game over, before this scene hears of it:
    /// the summary goes up over whatever was open, modal, until ENTER moves on
    /// (<see cref="ContinueAfterRun"/>). This scene shows one run's end, its
    /// own: a second result — which nothing raises while this scene is current —
    /// is ignored.
    /// </summary>
    private void OnRunEnded(RunResult result)
    {
        if (_endedRun is not null) return;
        _endedRun = result;
        _hud.HideArrival();
        ActiveMenu = GameMenu.RunSummary;

        Log.Info($"[Run] {result.Outcome} in {result.DungeonId}: deepest floor {result.DeepestFloor}, boss {(result.BossDefeated ? "down" : "standing")}"
            + $", {(result.ServiceTick ? result.MercyTick ? "a mercy tick" : "a success tick" : "no tick")} (run counter {result.RunCounter})"
            + (result.LockedUntilRun is { } until ? $", locked until run {until}" : "")
            + (result.Retired ? ", retired for good" : "")
            + $"; spoils kept {result.SpoilsKept.Count}, lost {result.SpoilsLost.Count}; {result.Clawbacks.Count} pool(s) clawed back");
        foreach (var claw in result.Clawbacks)
            Log.Info($"[Run] {claw.MemberId} {claw.Pool} XP {claw.Before} -> {claw.After}");
    }

    /// <summary>
    /// ENTER on the run's summary: on to the screen between runs — once. The
    /// screen fades to black and the run is handed back to the host
    /// (<see cref="IRunHost.RunOver"/>), which shows the dungeon list with this
    /// run's summary above it, where the next dungeon is chosen. Never while the
    /// engine is still fading between scenes, and never twice: the fade, once
    /// started, is the only way on.
    /// </summary>
    private void ContinueAfterRun()
    {
        if (_endedRun is null || _runOverSent || _transit == Transit.Leaving || SceneManager.Instance.IsTransitioning) return;
        Log.Info("[Run] On to the screen between runs");
        StartFadeOut(Transit.Leaving);
    }

    // ── Marching formation ───────────────────────────────────────────────────

    /// <summary>
    /// Outside combat the rest of the party follows the active character in a
    /// single-file line (party order), each targeting a point a fixed
    /// arc-length back along the leader's walked path. Followers spend their
    /// own movement budgets as they walk and stop when dry.
    /// </summary>
    private void UpdateMarchingFollowers(float deltaTime)
    {
        if (Transiting) return;   // the stairs place everyone; nobody walks while they do
        if (!Marching)
        {
            _wasMarching = false;
            return;
        }

        // Entering march (or switching leader) starts a fresh trail — the old
        // one points at wherever the previous leader wandered.
        if (!_wasMarching || _marchLeader != ActiveCharacter)
        {
            _march.Reset();
            _marchLeader = ActiveCharacter;
        }
        _wasMarching = true;

        if (AnyMenuOpen) return;

        var leader = ActiveCharacter.State;
        _march.SetLeader(leader.X, leader.Y);

        int rank = 0;
        (float X, float Y) prev = (leader.X, leader.Y);
        foreach (var member in _party)
        {
            if (member == ActiveCharacter || !member.State.Alive) continue;
            rank++;

            var target = _march.PointBehind(rank * MarchingLine.Spacing);
            float stopAt = MarchArriveTolerance;
            if (target == null)
            {
                // Trail is younger than this rank's depth: tuck in behind the
                // previous marcher until the leader has walked far enough.
                target = prev;
                stopAt = MarchingLine.Spacing;
            }

            MoveFollowerToward(member, target.Value, stopAt, deltaTime);
            prev = (member.State.X, member.State.Y);
        }
    }

    /// <summary>
    /// Outside combat, cycle the turn automatically once the slowest living
    /// member is out of movement — the group is halted at that point anyway,
    /// since the leader paces itself to the smallest budget. Exploring never
    /// needs Space; combat turns always end by hand, where held-back movement
    /// is a choice.
    /// </summary>
    private void AutoEndTurnWhenDry()
    {
        // Marching implies the player phase. Never mid-fade: the stairs are
        // taken in the player phase, and a turn ended under the fade would leave
        // them nobody's to take.
        if (Transiting || !Marching || AnyMenuOpen) return;

        // Budgets deplete through float math and may stop just shy of zero;
        // treat anything below a hair's width as dry (a full budget is 160).
        const float dry = 0.05f;
        foreach (var member in _party)
            if (member.State.Alive && member.State.DistLeft <= dry)
            {
                _turns.EndTurn();
                return;
            }
    }

    private void MoveFollowerToward(CharacterObject member, (float X, float Y) target, float stopAt, float deltaTime)
    {
        var state = member.State;
        if (state.DistLeft <= 0f) return;

        float dx = target.X - state.X;
        float dy = target.Y - state.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist <= stopAt + 0.5f) return;

        // A touch faster than the leader when far behind, so gaps close
        // instead of merely holding steady.
        float speed = dist > 2f * MarchingLine.Spacing ? GameConstants.Speed * 1.25f : GameConstants.Speed;
        float step = MathF.Min(MathF.Min(speed * deltaTime, dist - stopAt), state.DistLeft);

        var (nx, ny) = GridCollision.Move(
            _map.Grid, state.X, state.Y, state.Radius, dx / dist * step, dy / dist * step, LiveEnemyBlockers());

        float movedX = nx - state.X;
        float movedY = ny - state.Y;
        float moved = MathF.Sqrt(movedX * movedX + movedY * movedY);
        if (moved <= 0f) return;

        state.DistLeft = MathF.Max(0f, state.DistLeft - moved);
        state.X = nx;
        state.Y = ny;
        member.SyncTransform();
        UpdateFogFor(member);
        _turns.NotifyCharacterMoved(state);
    }

    // ── Combat input ─────────────────────────────────────────────────────────

    private void HandleClick()
    {
        if (AnyMenuOpen || Transiting) return;

        // The HUD party list doubles as buttons and wins over world picking.
        if (DungeonHud.HitPartySelector(_mousePos, _party.Count, out int hudIdx))
        {
            SetActiveCharacter(hudIdx); // ignores dead members
            return;
        }

        if (!TryMouseRay(out var origin, out var dir)) return;
        var caster = ActiveCharacter.State;
        var held = caster.EquippedWeapon;

        // Nearest sphere hit wins: the enemy attacks, a party member selects.
        var (enemy, memberIdx) = PickAt(origin, dir);
        if (enemy != null)
        {
            // A wand's enemy-click aims its shape at the enemy (a Blast on
            // it, a Cone or Beam toward it); a staff's is a cast (a debuff
            // staff lands its effect; a support staff has no enemy cast,
            // and the click does nothing); a martial weapon's is a swing.
            // A click that does nothing says why, over the enemy.
            var target = enemy.State;
            bool acted = held?.AreaShape != null ? CastArea(caster, (target.X, target.Y))
                : held?.IsCaster == true ? CastStaff(caster, target)
                : _turns.TryAttack(caster, target);
            if (!acted)
                SayRefusal(enemy, EnemyCue(caster, held, target));
            return;
        }

        if (memberIdx >= 0)
        {
            // A support staff's ally-click is always a cast — identical
            // rules in and out of combat, spending the caster's movement.
            // It never falls back to a leader-switch, which would reset the
            // march formation mid-explore. Without one (a martial weapon,
            // or a debuff staff, which has no ally cast) the click selects
            // the ally as the new leader instead.
            var member = _party[memberIdx];
            if (!CastsOnAllies(held))
                SetActiveCharacter(memberIdx);
            else if (!CastStaff(caster, member.State))
                SayRefusal(member, AllyCue(caster, held, member.State));
            return;
        }

        // Nothing under the cursor: a wand aimed by a point (a Blast's centre,
        // a Cone's or Beam's direction) casts at the floor point the ray meets.
        if (held?.AreaShape is { Kind: not AreaShapeKind.Nova } && FloorPointOf(origin, dir) is { } aim)
            CastArea(caster, aim);
    }

    /// <summary>Whether an ally click with <paramref name="weapon"/> is a cast: a staff whose effect is not for enemies (a wand's element is, so a wand's ally click selects).</summary>
    private static bool CastsOnAllies(Weapon? weapon)
        => weapon is { IsCaster: true } && weapon.Innate?.Def.Targets != TargetSide.Enemy;

    /// <summary>
    /// Cast the active wand at <paramref name="aim"/> (logic units) through the
    /// turn system, which prices it and refuses an empty shape. The first d20
    /// the cast draws is its own — every hit it fans out to shares it — so it
    /// is watched (<see cref="WatchCast"/>) and called over the caster: a crit
    /// halves the cast's mana, a fumble doubles it; the hits show their own
    /// CRIT! or WEAK. Returns whether the cast went off.
    /// </summary>
    private bool CastArea(PartyMemberState caster, (float X, float Y) aim)
    {
        var wand = caster.EquippedWeapon;
        bool cast = WatchCast(caster, () => _turns.TryCastArea(caster, aim));
        if (cast)
            Log.Info($"[Combat] {caster.Id} casts {wand?.Name}");
        else
            Log.Info($"[Combat] {caster.Id} cannot cast {wand?.Name} there ({_turns.AreaTargets(caster, aim).Count} in the shape, movement {caster.DistLeft:0}, mana {caster.Mana})");
        return cast;
    }

    /// <summary>
    /// Cast the active staff on <paramref name="target"/> through the turn
    /// system, which prices it and refuses a target on the wrong side or out
    /// of reach. The status a cast lands calls its roll
    /// (<see cref="TurnSystem.ActorStatusApplied"/>), but a cast can land none
    /// — the innate's trigger unpaid out of what the cast left, as when a
    /// fumble's doubled cost takes the whole pool — and it still rolled and
    /// still had its mana halved or doubled, so the roll is watched here too
    /// (<see cref="WatchCast"/>) and called if no status did. Returns whether
    /// the cast went off.
    /// </summary>
    private bool CastStaff(PartyMemberState caster, ActorState target)
    {
        bool cast = WatchCast(caster, () => _turns.TryCast(caster, target));
        if (cast)
            Log.Info($"[Combat] {caster.Id} casts {caster.EquippedWeapon?.Name}");
        return cast;
    }

    /// <summary>
    /// Run a party member's cast with its roll watched: the first d20 drawn
    /// while it resolves is the cast's own (<see cref="RollD20"/>), and a cast
    /// that went off has that roll called over its caster — once
    /// (<see cref="CallCastRoll"/>), so a status the cast landed, which calls
    /// it first, is not echoed.
    /// </summary>
    private bool WatchCast(PartyMemberState caster, Func<bool> cast)
    {
        _castRoll = null;
        _watchCastRoll = true;
        bool went = cast();
        _watchCastRoll = false;
        if (went && _castRoll is int roll)
            CallCastRoll(caster, roll, _castRollAt);
        return went;
    }

    /// <summary>A cast's natural <paramref name="roll"/> called over its caster once, whichever cue reaches it first: <paramref name="rollCount"/> is its place in the roll count, the mark a cast's statuses and its call site share.</summary>
    private void CallCastRoll(ActorState caster, int roll, int rollCount)
    {
        if (_castRollCalledAt == rollCount) return;
        _castRollCalledAt = rollCount;
        AnnounceCastRoll(caster, roll);
    }

    /// <summary>
    /// The turn system's d20 — the visit's own (<see cref="DungeonState.RollD20"/>,
    /// USER DECISION 1 of Phase 5, ruled by the user, 2026-09-25: seeded by the
    /// visit and saved) — drawn here so the scene knows the natural rolls its cues
    /// read: the last one, which is a staff cast's own when its status lands (the
    /// cast rolls just before its Cast is raised, and nothing rolls in between),
    /// and, while a party member's cast is watched (<see cref="WatchCast"/>), the
    /// first, which is the cast's own before anything its hits set off (a counter,
    /// a carried shot) rolls again. A hit rolls before its chain runs, so every
    /// roll also takes the levels <see cref="AttackerRiders"/> stand at before it.
    /// <para>
    /// Being the visit's, the die rolls on across a save: its position is saved
    /// (<see cref="DungeonState.DiceRolled"/>), so after a reload the dice fall as
    /// they would have without one, and a reload is never a reroll — "just to
    /// protect players from themselves", the user's reason. The alternative the
    /// decision named — a fresh die per session, this one line rolling
    /// <c>Random.Shared</c> again — is the one the ruling turned down.
    /// </para>
    /// </summary>
    private int RollD20()
    {
        int roll = _visit.RollD20();
        _lastRoll = roll;
        _rollCount++;
        if (_watchCastRoll && _castRoll == null)
        {
            _castRoll = roll;
            _castRollAt = _rollCount;
        }
        TakeRiderLevels();
        return roll;
    }

    /// <summary>What a natural <paramref name="roll"/> did to a cast by <paramref name="caster"/>: the turn system's own reading (<see cref="CombatRules.RollToCast"/>) in the caster's crit window.</summary>
    private static CastRoll CastRollOf(ActorState caster, int roll)
        => CombatRules.RollToCast(roll, CombatRules.CritThreshold(caster), levels: 1, manaCost: 1);

    /// <summary>A cast's natural roll called over its caster (§1.6): a crit — the effect doubled, the mana halved — or a fumble, the mana doubled; nothing for an ordinary roll.</summary>
    private void AnnounceCastRoll(ActorState caster, int roll)
    {
        var cast = CastRollOf(caster, roll);
        if (!cast.IsCrit && !cast.IsFumble) return;
        Log.Info($"[Combat] cast {(cast.IsCrit ? "crits" : "fumbles")} on a natural {roll}");
        if (TryObjectFor(caster, out var obj) && obj.IsActive)
            _hud.AddFloatingText(obj.Position, cast.IsCrit ? "CRIT CAST! MANA HALVED" : "FUMBLE! MANA DOUBLED",
                cast.IsCrit ? CritColor : WeakColor, -76f);
    }

    /// <summary>The floor point, in logic units, where the mouse ray meets the floor plane; null when it never does.</summary>
    private static (float X, float Y)? FloorPointOf(Vector3 origin, Vector3 dir)
    {
        if (MathF.Abs(dir.Y) < 1e-6f) return null;
        float t = -origin.Y / dir.Y;
        if (t <= 0f) return null;
        return WorldSpace.ToLogic(origin + dir * t);
    }

    private static bool RayHitsSphere(Vector3 origin, Vector3 dir, Vector3 center, float radius, out float t)
    {
        t = 0f;
        var oc = origin - center;
        float b = Vector3.Dot(oc, dir);
        float c = oc.LengthSquared - radius * radius;
        float disc = b * b - c;
        if (disc < 0f) return false;
        t = -b - MathF.Sqrt(disc);
        return t > 0f;
    }

    // ── Click and aim cues ───────────────────────────────────────────────────

    /// <summary>
    /// Refresh what the mouse is over and what a left click there would do,
    /// through the same pick <see cref="HandleClick"/> acts on: an enemy's cue
    /// (the swing, a staff's cast, a wand's shape aimed at it), an ally's (a
    /// support staff's cast), or, over bare floor with a wand, the aim cue at
    /// the cursor — a Nova's around the caster wherever the mouse is — and in
    /// every wand case the bodies the shape would catch. The turn system's own
    /// gates decide whether a cue is ready; the reasons only explain its
    /// refusals. Only in the player phase with no menu open and no stair's fade
    /// running.
    /// </summary>
    private void UpdateHover()
    {
        HoveredEnemy = null;
        HoveredMember = null;
        HoverCue = null;
        AimCue = null;
        AimCaught = Array.Empty<ActorState>();
        if (Transiting || _turns.Phase != TurnPhase.Player || AnyMenuOpen) return;
        if (DungeonHud.HitPartySelector(_mousePos, _party.Count, out _)) return;
        if (!TryMouseRay(out var origin, out var dir)) return;

        var caster = ActiveCharacter.State;
        var held = caster.EquippedWeapon;
        var (enemy, memberIdx) = PickAt(origin, dir);
        if (enemy != null)
        {
            HoveredEnemy = enemy;
            if (held?.AreaShape != null)
            {
                var (cue, caught) = AreaCue(caster, held, (enemy.State.X, enemy.State.Y));
                HoverCue = cue;
                AimCaught = caught;
            }
            else
            {
                HoverCue = EnemyCue(caster, held, enemy.State);
            }
        }
        else if (memberIdx >= 0)
        {
            HoveredMember = _party[memberIdx];
            HoverCue = AllyCue(caster, held, HoveredMember.State);
        }
        else if (held?.AreaShape is { } shape)
        {
            // A Nova sits on its caster whatever the aim; the other shapes read the floor under the mouse.
            var aim = shape.Kind == AreaShapeKind.Nova ? (caster.X, caster.Y) : FloorPointOf(origin, dir);
            if (aim is { } point)
            {
                var (cue, caught) = AreaCue(caster, held, point);
                AimCue = cue;
                AimCaught = caught;
            }
        }
    }

    /// <summary>
    /// What clicking the enemy <paramref name="target"/> would do with
    /// <paramref name="held"/> — the swing, a debuff staff's cast (a support
    /// staff has none), a wand's shape aimed at it — and whether the turn
    /// system would take it now; null with nothing in hand.
    /// </summary>
    private ClickCue? EnemyCue(PartyMemberState caster, Weapon? held, EnemyState target)
    {
        if (held == null) return null;
        if (held.AreaShape != null) return AreaCue(caster, held, (target.X, target.Y)).Cue;
        if (held.IsCaster)
        {
            var effect = held.Innate?.Def;
            if (effect == null) return null;
            if (effect.Targets == TargetSide.Ally) return new ClickCue("CASTS ON ALLIES ONLY", false);
            return _turns.CanCast(caster, target)
                ? new ClickCue($"CAST {effect.Name.ToUpperInvariant()}", true)
                : new ClickCue(Reach(caster, held, target) ?? Shortfall(caster, held) ?? "CANNOT CAST", false);
        }
        return _turns.CanAttack(caster, target)
            ? new ClickCue("ATTACK", true)
            : new ClickCue(Reach(caster, held, target) ?? Shortfall(caster, held)
                ?? (_turns.HasAttackLeft(caster) ? "CANNOT ATTACK" : "NO CHARGES LEFT"), false);
    }

    /// <summary>What clicking the ally <paramref name="target"/> would cast with a support staff, and whether the turn system would take it now; null when the click would select the ally instead.</summary>
    private ClickCue? AllyCue(PartyMemberState caster, Weapon? held, PartyMemberState target)
    {
        if (!CastsOnAllies(held) || held!.Innate?.Def is not { } effect) return null;
        return _turns.CanCast(caster, target)
            ? new ClickCue($"CAST {effect.Name.ToUpperInvariant()}", true)
            : new ClickCue(Reach(caster, held, target) ?? Shortfall(caster, held) ?? "CANNOT CAST", false);
    }

    /// <summary>
    /// A wand's shape aimed at <paramref name="aim"/>: the bodies it would
    /// catch — the turn system's own <see cref="TurnSystem.AreaTargets"/>,
    /// which the cast reads too — and the cue: how many a click there catches,
    /// or why it would not cast (nobody in the shape, the movement or the mana short).
    /// </summary>
    private (ClickCue Cue, IReadOnlyList<ActorState> Caught) AreaCue(PartyMemberState caster, Weapon wand, (float X, float Y) aim)
    {
        var caught = _turns.AreaTargets(caster, aim);
        string shape = wand.AreaShape?.Kind.ToString().ToUpperInvariant() ?? "CAST";
        var cue = _turns.CanCastArea(caster, aim) ? new ClickCue($"{shape}: {caught.Count} CAUGHT", true)
            : caught.Count == 0 ? new ClickCue($"{shape}: NOTHING CAUGHT", false)
            : new ClickCue(Shortfall(caster, wand) ?? $"{shape}: CANNOT CAST", false);
        return (cue, caught);
    }

    /// <summary>Why <paramref name="target"/> is beyond <paramref name="weapon"/> from where <paramref name="from"/> stands — past its reach, or behind a wall — or null when it is within both.</summary>
    private string? Reach(ActorState from, Weapon weapon, ActorState target)
    {
        if (!CombatRules.InAttackRange(from.X, from.Y, from.Radius, target.X, target.Y, target.Radius, weapon))
            return "OUT OF REACH";
        return LineOfSight.HasLineOfSight(_map.Grid, from.X, from.Y, target.X, target.Y) ? null : "NO LINE OF SIGHT";
    }

    /// <summary>What the member is short of for one use of <paramref name="weapon"/> — the movement it costs in this member's hands, proficiency discount included (§2.2), a caster's resolved mana — or null when it has both.</summary>
    private static string? Shortfall(PartyMemberState member, Weapon weapon)
    {
        int cost = member.MovementCost(weapon);
        if (member.DistLeft < cost) return $"NEED {cost} MOVE";
        if (weapon.IsCaster && member.Mana < weapon.ResolvedManaCost) return $"NEED {weapon.ResolvedManaCost} MANA";
        return null;
    }

    /// <summary>Why the member cannot hold fire now, for a ranged weapon that holds it (Overwatch stacks); null outside the player phase or for a weapon that never can, where O does nothing.</summary>
    private string? OverwatchRefusal(PartyMemberState member)
    {
        var weapon = member.EquippedWeapon;
        if (_turns.Phase != TurnPhase.Player || !member.Alive || weapon is not { Kind: WeaponKind.Ranged }
            || member.Value(ModifierType.Overwatch) <= 0)
            return null;
        if (member.HeldShots > 0) return "ALREADY HOLDING";
        int cost = member.MovementCost(weapon);
        return member.DistLeft < cost ? $"NEED {cost} MOVE" : "NO SHOTS LEFT";
    }

    /// <summary>A click or key that did nothing says why, over <paramref name="obj"/>: only in the player phase, when the turn was the party's to spend, and in place of any answer still rising over it (<see cref="DungeonHud.AddAnswer"/>).</summary>
    private void SayRefusal(GameObject obj, ClickCue? cue)
    {
        if (_turns.Phase == TurnPhase.Player && obj.IsActive && cue is { Ready: false } refused)
            _hud.AddAnswer(obj, obj.Position, refused.Label, CueColor, -40f);
    }

    /// <summary>The mouse ray in world space; false while the window has no area.</summary>
    private bool TryMouseRay(out Vector3 origin, out Vector3 dir)
    {
        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;
        if (w <= 0 || h <= 0)
        {
            origin = dir = default;
            return false;
        }
        (origin, dir) = _camera.ScreenToWorldRay(_mousePos.X, _mousePos.Y, w, h);
        return true;
    }

    /// <summary>
    /// What the ray meets first: the nearest sphere hit among the living,
    /// visible enemies and the living party members, as the enemy or the
    /// member's index, or neither (null and -1). The one pick a click acts
    /// on and the hover cue reads.
    /// </summary>
    private (EnemyObject? Enemy, int Member) PickAt(Vector3 origin, Vector3 dir)
    {
        float bestT = float.MaxValue;
        EnemyObject? enemy = null;
        int member = -1;
        foreach (var candidate in _enemies)
        {
            if (!candidate.State.Alive || !candidate.IsActive) continue;
            // A boss's larger box is a larger target: the sphere grows with the stand-in.
            if (RayHitsSphere(origin, dir, candidate.Position, 0.65f * candidate.Size, out float t) && t < bestT)
            {
                bestT = t;
                enemy = candidate;
            }
        }
        for (int i = 0; i < _party.Count; i++)
        {
            if (!_party[i].State.Alive) continue;
            if (RayHitsSphere(origin, dir, _party[i].Position, 0.6f, out float t) && t < bestT)
            {
                bestT = t;
                enemy = null;
                member = i;
            }
        }
        return (enemy, member);
    }

    // ── Loot on the floor ────────────────────────────────────────────────────

    /// <summary>The marker a dropped weapon is drawn as: a small plate on the tile, clearly walkable, clearly not a body.</summary>
    private const float GroundItemWidth = 0.42f;
    private const float GroundItemHeight = 0.14f;

    /// <summary>What the HUD prints over the item the active member is standing on — the key, so the cue and the binding cannot drift apart.</summary>
    public const string PickupHint = "PRESS G";

    /// <summary>
    /// The ground item the active member is standing on — what <c>G</c> would
    /// take — or null: outside the player phase, with a menu open, during a
    /// stair's fade, or standing anywhere else. Tile equality, not reach: you
    /// pick up what is under you. Where more than one item lies on the tile, it is
    /// the next in the order <c>G</c> takes them (<see cref="NextToTake"/>). A drop
    /// comes before a weapon the party put down, whichever landed first, and
    /// <c>G</c> again takes the next.
    /// </summary>
    public GroundItem? PickupTarget
    {
        get
        {
            if (Transiting || _turns.Phase != TurnPhase.Player || AnyMenuOpen) return null;
            return NextToTake(_groundItems, LogicTile(ActiveCharacter.State.X, ActiveCharacter.State.Y));
        }
    }

    /// <summary>
    /// What lies on the floor, in the order <c>G</c> takes it from any one tile:
    /// every corpse's drop before every weapon the party put down, and each kind in
    /// the order it was laid (the sort is stable). Which one landed first does not
    /// matter. When a weapon is put down on a drop, that is the full bag's trade,
    /// and <c>G</c> takes the drop it made room for, never the weapon back. When a
    /// dummy falls for good on a weapon put down earlier in the stay, its drop is
    /// taken first as well. So the order on a tile is the same on every visit to the
    /// floor; an arrival lays the drops first anyway (<see cref="ArriveOnFloor"/>).
    /// </summary>
    internal static IEnumerable<GroundItem> InTakingOrder(IEnumerable<GroundItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.OrderBy(item => item.Loose is not null);   // a drop (false) sorts before a loose item (true)
    }

    /// <summary>What <c>G</c> takes next from <paramref name="tile"/>: the first of <paramref name="items"/> lying there in <see cref="InTakingOrder"/>, or null when nothing does.</summary>
    internal static GroundItem? NextToTake(IEnumerable<GroundItem> items, (int R, int C) tile)
        => InTakingOrder(items).FirstOrDefault(item => item.Tile == tile);

    /// <summary>
    /// Put a dropped weapon on the floor at the corpse it came off. The drop
    /// itself was rolled by the turn system off the floor's seed (§3.5); the
    /// scene supplies only what is the scene's — a marker on the tile, and the
    /// log line.
    /// <para>
    /// Where it lands is decided once, here: the marker's position and the item's
    /// tile are taken off the corpse in the same breath, so the cue and the key
    /// read one fact rather than two that have to keep agreeing.
    /// </para>
    /// </summary>
    /// <returns>The item laid, or null when <paramref name="corpse"/> is already lying on one — so a caller that names what lands (<see cref="AnnounceDrop"/>) names each item once.</returns>
    private GroundItem? DropOnFloor(EnemyState corpse, Drop drop)
    {
        // One item a corpse. The boss's own drop is laid by its EnemyDropped before
        // BossDefeated re-derives the floor, and the re-derivation skips a corpse
        // that already has one. A dummy that fell after the boss in the same raise
        // comes the other way round: the boss's held death runs first, its fall
        // lays the dummy's item, and then the dummy's own defeat hands the same
        // item over. The second lay, whichever it is, is refused here.
        if (_groundItems.Any(item => item.Corpse == corpse)) return null;

        Log.Info($"[Loot] Dropped {drop.Weapon.Name} [{drop.Weapon.Modifiers}] (x{drop.DefeatCount}"
            + $"{(drop.Weapon.Unique ? drop.WasUniqueRoll ? ", UNIQUE ROLL" : ", UNIQUE BY ARITHMETIC" : "")}"
            + $"{(corpse == _floor.Boss ? $", the boss's own, forged with {corpse.Theme}" : "")})");
        var marker = new PrimitiveBoxObject(GroundItemWidth, GroundItemHeight, GroundItemWidth, LootColor)
        {
            Position = WorldSpace.FromLogic(corpse.X, corpse.Y, GroundItemHeight / 2f),
        };
        AddGameObject(marker);
        var laid = new GroundItem(corpse, drop, marker, LogicTile(corpse.X, corpse.Y));
        _groundItems.Add(laid);
        UpdateGroundItemVisibility();
        return laid;
    }

    /// <summary>
    /// Lay the marker of a weapon the party put down on this floor (§4.4): the
    /// same plate a drop lies on, centred on the item's tile, and labelled as
    /// itself rather than as a corpse's drop (<see cref="DungeonHud.LooseItemRuns"/>).
    /// The item is the floor's (<see cref="Floor.LooseItems"/>). The marker is the
    /// scene's, laid when the weapon is put down and again on every arrival, and it
    /// is as visible as its tile. <c>G</c> takes it after any drop on its tile,
    /// whichever landed first (<see cref="InTakingOrder"/>): a full bag that puts its
    /// weak weapon down on the drop it wants picks up that drop next.
    /// </summary>
    private GroundItem LayLoose(LooseItem loose)
    {
        var (x, y) = Displacer.CentreOf(loose.Tile.R, loose.Tile.C);
        var marker = new PrimitiveBoxObject(GroundItemWidth, GroundItemHeight, GroundItemWidth, LootColor)
        {
            Position = WorldSpace.FromLogic(x, y, GroundItemHeight / 2f),
        };
        AddGameObject(marker);
        var laid = new GroundItem(null, null, marker, loose.Tile, loose);
        _groundItems.Add(laid);
        UpdateGroundItemVisibility();
        return laid;
    }

    /// <summary>
    /// The beat an item earns as it lands in front of the party, over the corpse
    /// it came off: a unique by name, in gold — <c>UNIQUE: THE BULWARK!</c>
    /// (<see cref="DungeonHud.UniqueBeat"/>) — whether the farm's lottery paid out
    /// or the boss's graft took its theme's Purity variant to ×3 (§4.3).
    /// <para>
    /// An item lands in front of the party two ways: a permanent kill hands it
    /// over (<see cref="TurnSystem.EnemyDropped"/>), and the boss's fall lays the
    /// item of every corpse down at that moment — a dummy that fell earlier in the
    /// same swing, one waiting to get up — none of which anyone has seen until
    /// then (<see cref="RederiveGroundItems"/>). Only an item
    /// <see cref="DropOnFloor"/> has just laid is named, so each is named once,
    /// whichever way it came. Never on an arrival's re-derivation: nothing lands
    /// as the party arrives — the floor holds what it held when the party left,
    /// or what the boss's fall laid on it while the party was elsewhere — and the
    /// gold label names a unique there.
    /// </para>
    /// In sight only, like every beat; a unique that fell in the fog is still
    /// named in gold when its tile is lit.
    /// </summary>
    private void AnnounceDrop(GroundItem laid)
    {
        // A drop's only: a loose item is laid by the party's own hand, and lands in front of nobody.
        if (laid is not { Corpse: { } corpse, Drop: { } drop } || DungeonHud.UniqueBeat(drop) is not { } beat) return;
        Log.Info($"[Loot] {beat}");
        var obj = EnemyObjectFor(corpse);
        if (obj.IsActive)
            _hud.AddFloatingText(obj.Position, beat, LootColor, UniqueBeatOffset);
    }

    /// <summary>Where a unique's beat hangs over its corpse: above the DEFEATED! the same death floats, so the two read in order.</summary>
    private const float UniqueBeatOffset = -84f;

    /// <summary>
    /// Every weapon this floor is holding that is not already on it (§3.5): a
    /// dummy killed permanently and not picked up is lying on the weapon it
    /// carried, and nothing stored that — the enemy row and the floor's seed
    /// re-derive the identical item.
    /// <para>
    /// Read on every arrival — on a first visit nothing is dead and it places
    /// nothing; on a floor returned to after resurrection stopped, every corpse
    /// left there is lying on its weapon again, re-derived rather than
    /// remembered (and a loaded floor, §5.1, is the same case) — and again the
    /// moment resurrection stops, because that is the other way a corpse becomes
    /// one that is offering rather than one that is going to get up.
    /// </para>
    /// </summary>
    /// <returns>
    /// What it laid, in the order laid. The boss's fall names a unique among
    /// them, because that is the moment those items land
    /// (<see cref="AnnounceDrop"/>); an arrival lays them without a word.
    /// </returns>
    private List<GroundItem> RederiveGroundItems()
    {
        var laid = new List<GroundItem>();
        foreach (var enemy in _enemies)
        {
            if (_groundItems.Any(item => item.Corpse == enemy.State)) continue;
            if (_turns.GroundItemOf(enemy.State) is { } drop && DropOnFloor(enemy.State, drop) is { } item)
                laid.Add(item);
        }
        return laid;
    }

    /// <summary>
    /// Take what the active member is standing on: a drop, or a weapon the party
    /// put down. The bag, the retired offer, the met entries and the run's spoils
    /// are <see cref="Pickup.Take"/>'s — or, for a loose item, <see cref="Pickup.TakeLoose"/>'s,
    /// which takes it off the floor's list and never makes it a spoil — and each
    /// decides them without a window and is tested that way. What is left here is
    /// the scene's: the marker that goes, the reach that changed, and the cue that
    /// says why a full bag took nothing. That is a carry limit, not a fault:
    /// §4.4's six slots a member, 24 party-wide, which loot shares with the weapons
    /// being carried. So the cue says how to make room: the bag's X puts one down
    /// here. A spoil taken is carried home by the way out and lost to a wipe (§4.4).
    /// </summary>
    private void PickUp()
    {
        if (PickupTarget is not { } item) return;

        var member = ActiveCharacter.State;
        int slot = item switch
        {
            { Loose: { } loose } => Pickup.TakeLoose(_campaign, _floor, loose, member),
            { Corpse: { } corpse, Drop: { } drop } => Pickup.Take(_campaign, corpse, drop, member, _visit),
            _ => throw new InvalidOperationException("A ground item is a drop, with its corpse, or a loose item: this one is neither."),
        };
        if (slot == Pickup.BagFull)
        {
            SayRefusal(ActiveCharacter, new ClickCue(DungeonHud.BagFullCue(member.Inventory.Length), false));
            return;
        }

        _groundItems.Remove(item);
        RemoveGameObject(item.Marker);
        if (slot == 0)
            _turns.NotifyWeaponChanged(member);   // an empty hand filled is a reach changed

        Log.Info($"[Loot] {member.Id} picks up {item.Weapon.Name} into slot {slot + 1}{(item.Loose is null ? "" : ", back off the floor")}");
        // G's answer, like its BAG FULL: it replaces the LEFT ... HERE of a trade made a moment before.
        _hud.AddAnswer(ActiveCharacter, ActiveCharacter.Position, $"TOOK {item.Weapon.ShownName}", LootColor, -52f);
    }

    /// <summary>A ground item is as visible as the tile it sits on, exactly as the corpse that left it was.</summary>
    private void UpdateGroundItemVisibility()
    {
        foreach (var item in _groundItems)
        {
            var (r, c) = item.Tile;
            item.Marker.IsActive = _fog.Visible[r, c];
        }
    }

    // ── Fog & enemy visibility ───────────────────────────────────────────────

    private void UpdateFogFor(CharacterObject member, bool animate = true)
    {
        var tile = LogicTile(member.State.X, member.State.Y);
        if (_lastFogTile.TryGetValue(member, out var last) && last == tile) return;
        _lastFogTile[member] = tile;

        var newly = _fog.RevealAt(tile.R, tile.C, _fogBoxes.FogBoxes);
        if (newly.Count > 0)
        {
            _fogOverlay.MarkDirty();
            if (animate)
            {
                _fogOverlay.AnimateReveal(newly, tile);
                QueueRevealMist(newly, tile);
            }
        }
        UpdateEnemyVisibility();
    }

    /// <summary>
    /// Start-of-enemy-turn reset: only currently-occupied boxes stay lit.
    /// Tiles that stayed in view re-reveal silently (no flash, matching the
    /// prototype); tiles that left view grow their shroud back as a ripple
    /// closing toward the active character.
    /// </summary>
    private void ResetFogVisibility()
    {
        var oldVisible = (bool[,])_fog.Visible.Clone();

        _fog.ResetVisibility();
        _lastFogTile.Clear();
        foreach (var member in _party)
        {
            if (member.State.Alive)
                UpdateFogFor(member, animate: false);
        }

        var lostTiles = new List<(int R, int C)>();
        for (int r = 0; r < _fog.Rows; r++)
            for (int c = 0; c < _fog.Cols; c++)
                if (oldVisible[r, c] && !_fog.Visible[r, c] && _fog.Seen[r, c])
                    lostTiles.Add((r, c));
        _fogOverlay.AnimateRefog(lostTiles, LogicTile(ActiveCharacter.State.X, ActiveCharacter.State.Y));

        _fogOverlay.MarkDirty();
        UpdateEnemyVisibility();
    }

    /// <summary>
    /// Schedule smoke puffs on revealed tiles, following the reveal ripple's
    /// stagger: several puffs scattered through each tile's volume, each
    /// timed to when the dropping fog surface passes its height, so the
    /// sinking cube reads as dissolving into smoke at its surface.
    /// </summary>
    private void QueueRevealMist(IReadOnlyList<(int R, int C, bool WasSeen)> tiles, (int R, int C) origin)
    {
        foreach (var (r, c, _) in tiles)
        {
            int dist = Math.Abs(r - origin.R) + Math.Abs(c - origin.C);
            float rippleDelay = dist * 0.018f;
            var center = WorldSpace.TileCenter(r, c);

            for (int i = 0; i < 3; i++)
            {
                float y = 0.15f + (float)_fxRng.NextDouble() * (WallHeight - 0.3f);
                var pos = center + new Vector3(
                    ((float)_fxRng.NextDouble() - 0.5f) * WorldSpace.UnitsPerTile * 0.8f,
                    y,
                    ((float)_fxRng.NextDouble() - 0.5f) * WorldSpace.UnitsPerTile * 0.8f);

                // The cube top drops as 1 - t² of its full height, so it
                // passes this puff's y at t = √(1 − y/top) of the 250ms drop.
                float surfaceHits = MathF.Sqrt(Math.Max(0f, 1f - y / _fogOverlay.Height)) * 0.25f;
                _pendingMistBursts.Add((rippleDelay + surfaceHits + (float)_fxRng.NextDouble() * 0.05f, pos));
            }
        }
    }

    private void UpdateFogParticles(float deltaTime)
    {
        // Fire scheduled reveal puffs whose ripple delay has elapsed.
        for (int i = _pendingMistBursts.Count - 1; i >= 0; i--)
        {
            var (timeLeft, pos) = _pendingMistBursts[i];
            timeLeft -= deltaTime;
            if (timeLeft <= 0f)
            {
                _mistEmitter.Burst(1, pos);
                _pendingMistBursts.RemoveAt(i);
            }
            else
            {
                _pendingMistBursts[i] = (timeLeft, pos);
            }
        }

        // Ambient wisps over explored-but-out-of-view tiles.
        _wispTimer -= deltaTime;
        if (_wispTimer > 0f) return;
        _wispTimer = WispInterval;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            int r = _fxRng.Next(_fog.Rows);
            int c = _fxRng.Next(_fog.Cols);
            if (!_fog.Seen[r, c] || _fog.Visible[r, c]) continue;
            float y = 0.2f + (float)_fxRng.NextDouble() * (WallHeight - 0.35f);
            _wispEmitter.Burst(1, WorldSpace.TileCenter(r, c, y));
            break;
        }
    }

    /// <summary>
    /// Show every enemy stand-in as visible as the tile it stands on, and, with
    /// <paramref name="notify"/>, tell the floor's system which enemies are in
    /// sight — a sighting, which it counts as seen this turn (whose zones may
    /// react). A resumed turn's lighting shows without telling
    /// (<see cref="RevealArrival"/>): its sightings came back with its counters.
    /// </summary>
    private void UpdateEnemyVisibility(bool notify = true)
    {
        foreach (var enemy in _enemies)
        {
            var (r, c) = LogicTile(enemy.State.X, enemy.State.Y);
            bool visible = _fog.Visible[r, c];
            enemy.SetVisible(visible); // hidden, a shove's slide ends where the logic put it
            if (notify)
                _turns.NotifyEnemyVisible(enemy.State, visible);
        }
        UpdateGroundItemVisibility();   // what a corpse left is lit by the same fog the corpse was
    }

    private EnemyObject EnemyObjectFor(EnemyState state) => _enemies.First(e => e.State == state);

    /// <summary>The scene object of an actor on either side, for events the turn system raises per actor rather than per side.</summary>
    private bool TryObjectFor(ActorState actor, out GameObject obj)
    {
        GameObject? found = actor switch
        {
            PartyMemberState member => _party.FirstOrDefault(p => p.State == member),
            EnemyState enemy => _enemies.FirstOrDefault(e => e.State == enemy),
            _ => null,
        };
        obj = found!;
        return found != null;
    }

    /// <summary>Live enemies as collision blockers; corpses are walkable.</summary>
    private List<GridCollision.Circle> LiveEnemyBlockers()
    {
        var blockers = new List<GridCollision.Circle>();
        foreach (var enemy in _enemies)
            if (enemy.State.Alive)
                blockers.Add(new GridCollision.Circle(enemy.State.X, enemy.State.Y, enemy.State.Radius));
        return blockers;
    }

    /// <summary>
    /// A hit's floating text: the roll; then one row with what reached HP in
    /// the middle, what Block did on its left — the points it absorbed, BLOCK
    /// SKIPPED when a crit went straight through it, BLOCK 0 when the blow beat
    /// it anyway — and on its right what Ward swallowed, dealt but never taken
    /// (Dealt = Taken + Ward, §1.6); then, above it and apart from the number,
    /// a beat per rider the hit left on its target — SUNDERED!, WEAKENED!, and
    /// whatever else rode it (a Pin's MIRE!, a burn's BURNING!) — and over its
    /// attacker the WEAKENED! a blocked blow earns (<see cref="AnnounceAttackerRiders"/>).
    /// </summary>
    private void SpawnAttackTexts(ActorState defender, Vector3 worldPos, AttackResolution res)
    {
        _hud.AddFloatingText(worldPos, $"ROLL: {res.Roll.Roll}", new Vector4(1f, 1f, 1f, 1f), -28f);

        bool crit = res.Roll.Outcome == RollOutcome.Crit;
        var (label, color) = res.Roll.Outcome switch
        {
            RollOutcome.Crit => ($"CRIT! -{res.Taken}", CritColor),
            RollOutcome.Weak => ($"WEAK -{res.Taken}", WeakColor),
            _ => ($"-{res.Taken}", HitColor),
        };
        var row = new List<DungeonHud.Run>();
        if (res.Blocked > 0)
            row.Add(new DungeonHud.Run($"BLOCK {res.Blocked}", BlockColor));
        else if (defender.Value(ModifierType.Block) > 0)
            row.Add(crit ? new DungeonHud.Run("BLOCK SKIPPED", CritColor) : new DungeonHud.Run("BLOCK 0", WeakColor));
        row.Add(new DungeonHud.Run(label, color));
        if (res.WardSpent > 0)
            row.Add(new DungeonHud.Run($"WARD {res.WardSpent}", BlockColor));
        _hud.AddFloatingRow(worldPos, 8f, row);

        float y = RiderBeatTop;
        foreach (var rider in res.Riders)
        {
            _hud.AddFloatingText(worldPos, $"{DungeonHud.StatusName(rider.Type, rider.Element)}!", RiderColor, y);
            y -= RiderBeatStep;
        }
        AnnounceAttackerRiders(defender);
    }

    // Where a hit's rider beats start above the actor they landed on, and how far apart they stack.
    private const float RiderBeatTop = -78f;
    private const float RiderBeatStep = 16f;

    /// <summary>
    /// The beats a hit's resolution cannot carry: its riders are its
    /// defender's, but BlockWeaken weakens the attacker whose blow was
    /// blocked. Any actor but the defender whose <see cref="AttackerRiders"/>
    /// level rose since the hit rolled gets the beat a rider gets — WEAKENED!
    /// over the attacker — and the levels are taken afresh for the next hit,
    /// which may share this one's roll (an area cast's hits do).
    /// </summary>
    private void AnnounceAttackerRiders(ActorState defender)
    {
        foreach (var actor in Actors())
        {
            if (actor == defender || !TryObjectFor(actor, out var obj) || !obj.IsActive) continue;
            float y = RiderBeatTop;
            foreach (var type in AttackerRiders)
            {
                if (actor.StatusLevel(type) <= _riderLevels.GetValueOrDefault((actor, type))) continue;
                _hud.AddFloatingText(obj.Position, $"{DungeonHud.StatusName(type, null)}!", RiderColor, y);
                y -= RiderBeatStep;
            }
        }
        TakeRiderLevels();
    }

    /// <summary>Take every actor's <see cref="AttackerRiders"/> levels as they stand: the mark the next hit's rises are read against.</summary>
    private void TakeRiderLevels()
    {
        foreach (var actor in Actors())
            foreach (var type in AttackerRiders)
                _riderLevels[(actor, type)] = actor.StatusLevel(type);
    }

    /// <summary>Everyone on the map, the party then the enemies.</summary>
    private IEnumerable<ActorState> Actors()
        => _party.Select(p => (ActorState)p.State).Concat(_enemies.Select(e => e.State));

    private static (int R, int C) LogicTile(float x, float y)
        => ((int)MathF.Floor(y / GameConstants.Tile), (int)MathF.Floor(x / GameConstants.Tile));

    private static void SetColor(GameObject obj, Vector4 color)
    {
        foreach (var renderer in obj.GetComponents<MeshRenderer>())
            renderer.DiffuseColor = color;
    }

    private static Vector4 Rgb(int hex) => new(
        ((hex >> 16) & 0xff) / 255f,
        ((hex >> 8) & 0xff) / 255f,
        (hex & 0xff) / 255f,
        1f);
}
