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
    Pause,
}

/// <summary>
/// What a left click on the thing under the mouse would do, for the HUD's
/// cue: the action (ATTACK, CAST MIRE, BLAST: 2 CAUGHT) when the turn system
/// would take it now, or why not (OUT OF REACH, NEED 30 MOVE) when it would not.
/// </summary>
public readonly record struct ClickCue(string Label, bool Ready);

/// <summary>
/// The main gameplay scene: a procedurally generated dungeon explored by a
/// four-character party in turn-based, distance-budgeted combat. The dungeon
/// is the prototype's exact map (same seed, same generator), reinterpreted in
/// 3D: floor slab on the XZ plane, extruded wall boxes, angled top-down camera.
/// </summary>
public class DungeonScene : Scene
{
    /// <summary>
    /// This run's dungeon seed, rolled fresh per scene and logged at startup
    /// so any map can be reproduced. (Golden tests still pin the prototype's
    /// shipped seed to guarantee generator parity.)
    /// </summary>
    public long MapSeed { get; } = (uint)Random.Shared.NextInt64();

    private const float WallHeight = 1.2f;
    private const float FloorThickness = 0.2f;

    // Palette carried over from the prototype's hex colours.
    private static readonly Vector4 WallColor = Rgb(0x3d405b);
    private static readonly Vector4 FloorColor = Rgb(0x0f3460);
    private static readonly Vector4 EnemyColor = Rgb(0xf5a623);
    private static readonly Vector4 DeadColor = Rgb(0x555555);
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

    private static readonly Vector4[] PartyColors =
    {
        Rgb(0xe94560), // A
        Rgb(0x3b8eff), // B
        Rgb(0x44cc66), // C
        Rgb(0xffdd44), // D
    };

    /// <summary>
    /// Party members this scene can spawn: one colour each, and the count
    /// <see cref="SpawnParty"/> asks the map for spawn tiles for. The roster's
    /// length is the rule (<see cref="GameConstants.PartySize"/>) and this is the
    /// presentation's capacity for it; a test asserts the two are equal, because
    /// a roster longer than this spawns a prefix and drops whoever came last
    /// without a word.
    /// </summary>
    internal static int PartyColorCount => PartyColors.Length;

    private readonly Camera _camera;
    private readonly Game _game;
    private readonly DungeonHud _hud;

    private MapData _map = null!;
    private FogBoxSet _fogBoxes = null!;
    private FogState _fog = null!;
    private FogOverlayRenderer _fogOverlay = null!;
    private TurnSystem _turns = null!;

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
    private bool _marchingThisTurn; // decided once at each turn start
    private const float MarchArriveTolerance = 2f;
    private readonly List<EnemyObject> _enemies = new();
    private readonly Dictionary<EnemyObject, (int R, int C)> _lastEnemyTile = new();
    private int _activeIdx;

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

    public DungeonScene(Camera camera, Game game) : base("DungeonScene")
    {
        _camera = camera;
        _game = game;
        _hud = new DungeonHud(game);
    }

    public CharacterObject ActiveCharacter => _party[_activeIdx];

    // HUD-facing state
    public IReadOnlyList<CharacterObject> Party => _party;
    public int ActiveIndex => _activeIdx;
    public TurnSystem Turns => _turns;
    public IReadOnlyList<EnemyObject> Enemies => _enemies;
    public GameMenu ActiveMenu { get; private set; } = GameMenu.None;
    public bool InventoryOpen => ActiveMenu == GameMenu.Inventory;
    public Vector2 MousePos => _mousePos;
    public bool PauseMenuOpen => ActiveMenu == GameMenu.Pause;
    public bool AnyMenuOpen => ActiveMenu != GameMenu.None;
    public float LastBankedMovement { get; private set; }

    // What each member banked at the last turn's end (TurnEnded): the bonus
    // its turn start adds to the base allowance before Mire cuts the whole.
    private readonly Dictionary<PartyMemberState, float> _banked = new();

    /// <summary>
    /// The movement budget <paramref name="member"/>'s Mire cuts: the base
    /// allowance plus what it banked at the last turn's end — the whole its
    /// turn start cuts (<see cref="PartyMemberState.StartTurn"/>). In the
    /// player phase that is what this turn's cap was cut from, so a Mire
    /// legend agrees with the MOVE readout at the same level; once the turn
    /// has ended, it is the coming turn's.
    /// </summary>
    public float BudgetBeforeMire(PartyMemberState member)
        => GameConstants.MaxDistance + _banked.GetValueOrDefault(member);

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
    /// start of a turn — no live enemy visible and the party regrouped within
    /// each member's max movement of the leader — and once revoked (a live
    /// enemy sighted) stays off for the rest of the turn. This keeps a split
    /// party from charging back into formation the moment line of sight to
    /// the enemy breaks mid-fight.
    /// </summary>
    public bool Marching => _turns.Phase == TurnPhase.Player
        && _marchingThisTurn
        && !_turns.AnyLiveEnemySeenThisTurn;

    /// <summary>
    /// The turn-start marching decision: needs the enemy dead or out of view
    /// and every living member within their own max movement of the leader.
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

    public override void Initialize()
    {
        base.Initialize();

        Log.Info($"[Map] Generating dungeon with seed {MapSeed}");
        _map = MapGenerator.Generate(new Mulberry32(MapSeed));
        _fogBoxes = FogBoxBuilder.Build(_map); // also expands _map.Corridors in place
        _fog = new FogState(MapGenerator.Rows, MapGenerator.Cols);

        BuildFloor();
        BuildWalls();
        SpawnParty();
        SpawnEnemies();
        BuildFogOverlay();   // after all geometry: its translucent pass blends over everything
        BuildFogParticles(); // after the fog overlay: mist blends over the shroud
        WireTurnSystem();
        WireInput();

        // Initial fog reveal around the party, and hide the enemy if fogged.
        foreach (var member in _party)
            UpdateFogFor(member);
        UpdateEnemyVisibility();
        EvaluateMarchingForTurn(); // the first turn starts without a PlayerTurnStarted event

        _cameraTarget = ActiveCharacter.Position;
        _camera.Yaw = -90f;   // face -Z (up the map)
        _camera.Pitch = -60f; // angled top-down
        _camera.Far = 300f;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        _turns.Update(deltaTime);
        _hud.Update(deltaTime);
        UpdateFogParticles(deltaTime);
        UpdateActiveCharacterMovement(deltaTime);
        UpdateMarchingFollowers(deltaTime);
        AutoEndTurnWhenDry();

        // While the enemy turn runs, mirror logic positions and refresh
        // visibility when someone crosses a tile boundary. Syncing through
        // the attack phase too catches the final sub-step of a walk, which
        // lands in the same frame the phase flips to EnemyAttacking.
        if (_turns.Phase is TurnPhase.EnemyMoving or TurnPhase.EnemyAttacking)
        {
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

        UpdateHover();
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
        _hud.Dispose();
        base.Shutdown();
    }

    // ── Setup ────────────────────────────────────────────────────────────────

    private void BuildFloor()
    {
        float w = MapGenerator.Cols * WorldSpace.UnitsPerTile;
        float d = MapGenerator.Rows * WorldSpace.UnitsPerTile;
        var floor = new PrimitiveBoxObject(w, FloorThickness, d, FloorColor);
        floor.Position = new Vector3(w / 2f, -FloorThickness / 2f, d / 2f);
        AddGameObject(floor);
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
            AddGameObject(wall);
        }
    }

    private void SpawnParty()
    {
        var tiles = GridUtils.FindPartySpawnTiles(
            _map.Grid, _map.PlayerStart.Row, _map.PlayerStart.Col, PartyColors.Length);

        for (int i = 0; i < tiles.Count; i++)
        {
            var state = new PartyMemberState
            {
                Id = GameConstants.CharIds[i],
                ColorIndex = i,
                X = tiles[i].C * GameConstants.Tile + GameConstants.Tile / 2f,
                Y = tiles[i].R * GameConstants.Tile + GameConstants.Tile / 2f,
            };
            var catalogue = GameContent.Current.Weapons;
            state.Inventory[0] = catalogue.Instantiate(GameConstants.CharStartingWeaponIds[i]);
            state.Inventory[1] = catalogue.Instantiate(GameConstants.StartingBagWeaponIds[i]); // a staff for everyone

            var character = new CharacterObject(state, PartyColors[i]);
            _party.Add(character);
            AddGameObject(character);
        }

        _activeIdx = 0;
    }

    /// <summary>
    /// First-entry enemy population: EnemyPlacer decides positions from the
    /// map seed on its own RNG stream. When save games arrive, a loaded
    /// entry skips this and restores saved enemy states instead.
    /// </summary>
    private void SpawnEnemies()
    {
        foreach (var (x, y, weaponId) in EnemyPlacer.PlaceEnemies(_map, MapSeed))
        {
            var state = new EnemyState { X = x, Y = y, Weapon = GameContent.Current.Weapons.Instantiate(weaponId) };
            var enemy = new EnemyObject(state, EnemyColor);
            _enemies.Add(enemy);
            AddGameObject(enemy);
        }
        Log.Info($"[Map] Spawned {_enemies.Count} enemies across {_map.DebugRooms.Count} rooms");
    }

    private void BuildFogOverlay()
    {
        var fogObject = new TransformNodeObject();
        _fogOverlay = fogObject.AddComponent<FogOverlayRenderer>();
        _fogOverlay.Fog = _fog;
        _fogOverlay.Height = WallHeight + 0.05f;
        AddGameObject(fogObject);
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
        AddGameObject(mistObject);

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
        AddGameObject(wispObject);
    }

    private void WireTurnSystem()
    {
        _turns = new TurnSystem(_map.Grid, _party.Select(p => p.State).ToList(),
            _enemies.Select(e => e.State).ToList(), RollD20);

        _turns.TurnEnded += saved =>
        {
            Log.Info($"[Turns] End of turn — banked {saved} movement");
            LastBankedMovement = saved;
            foreach (var member in _party)
                _banked[member.State] = member.State.SavedMovement;
            if (InventoryOpen) ActiveMenu = GameMenu.None;
            ResetFogVisibility();
        };

        _turns.PlayerTurnStarted += () =>
        {
            Log.Info($"[Turns] Player turn {_turns.TurnCount} begins");
            if (!ActiveCharacter.State.Alive)
                SetActiveCharacter(_party.FindIndex(p => p.State.Alive), force: true);
            EvaluateMarchingForTurn(); // after the leader fixup: distances measure from a live leader
        };

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

        _turns.EnemyResurrected += enemy =>
        {
            Log.Info("[Combat] Enemy resurrected!");
            var obj = EnemyObjectFor(enemy);
            SetColor(obj, EnemyColor);
            obj.SetDefeatedVisual(false);
            obj.SyncTransform(); // resurrection may have relocated it off an occupied tile
            UpdateEnemyVisibility();
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

        _turns.GameOver += () => Log.Info("[Turns] GAME OVER");
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
        // never machine-gun while held.
        //
        // Number keys select party members, equip inventory slots while the
        // bag is open (2/3 swap that slot with the equipped slot), or pick a
        // pause-menu option (1 close, 2 exit).
        input.SubscribeToKeyPressed(_ =>
        {
            if (PauseMenuOpen) ActiveMenu = GameMenu.None;
            else if (!AnyMenuOpen) SetActiveCharacter(0);
        }, Keys.D1);
        input.SubscribeToKeyPressed(_ =>
        {
            if (PauseMenuOpen) _game.Close();
            else if (InventoryOpen) EquipSlot(1);
            else SetActiveCharacter(1);
        }, Keys.D2);
        input.SubscribeToKeyPressed(_ =>
        {
            if (InventoryOpen) EquipSlot(2);
            else if (!AnyMenuOpen) SetActiveCharacter(2);
        }, Keys.D3);
        input.SubscribeToKeyPressed(_ => { if (!AnyMenuOpen) SetActiveCharacter(3); }, Keys.D4);
        input.SubscribeToKeyPressed(_ => { if (!AnyMenuOpen) CycleActiveCharacter(); }, Keys.Tab);

        input.SubscribeToKeyPressed(_ => { if (!AnyMenuOpen) _turns.EndTurn(); }, Keys.Space, Keys.Enter);
        input.SubscribeToKeyPressed(_ => ToggleInventory(), Keys.I, Keys.B);
        // O holds fire: a ranged weapon with Overwatch banks its shot against
        // whatever walks into reach on the enemy turn. The member says so — the
        // readout then shows the shots held — or says why it cannot.
        input.SubscribeToKeyPressed(_ =>
        {
            if (AnyMenuOpen) return;
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
            if (AnyMenuOpen) return;
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

    private void ToggleInventory()
    {
        if (_turns.Phase == TurnPhase.GameOver || PauseMenuOpen) return;
        ActiveMenu = InventoryOpen ? GameMenu.None : GameMenu.Inventory;
    }

    /// <summary>
    /// Escape closes whatever menu is open, or opens the pause menu. Only on
    /// the game-over screen is it left unconsumed, so the game quits directly.
    /// </summary>
    public bool HandleEscape()
    {
        if (_turns.Phase == TurnPhase.GameOver) return false;
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

    private void UpdateActiveCharacterMovement(float deltaTime)
    {
        if (_turns.Phase != TurnPhase.Player || AnyMenuOpen) return;

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

    // ── Marching formation ───────────────────────────────────────────────────

    /// <summary>
    /// Outside combat the rest of the party follows the active character in a
    /// single-file line (party order), each targeting a point a fixed
    /// arc-length back along the leader's walked path. Followers spend their
    /// own movement budgets as they walk and stop when dry.
    /// </summary>
    private void UpdateMarchingFollowers(float deltaTime)
    {
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
        if (!Marching || AnyMenuOpen) return; // Marching implies the player phase

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
        if (AnyMenuOpen) return;

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
    /// The turn system's d20 — the same fair die its default rolls — drawn
    /// here so the scene knows the natural rolls its cues read: the last one,
    /// which is a staff cast's own when its status lands (the cast rolls just
    /// before its Cast is raised, and nothing rolls in between), and, while a
    /// party member's cast is watched (<see cref="WatchCast"/>), the first,
    /// which is the cast's own before anything its hits set off (a counter, a
    /// carried shot) rolls again. A hit rolls before its chain runs, so every
    /// roll also takes the levels <see cref="AttackerRiders"/> stand at before it.
    /// </summary>
    private int RollD20()
    {
        int roll = Random.Shared.Next(1, 21);
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
    /// refusals. Only in the player phase with no menu open.
    /// </summary>
    private void UpdateHover()
    {
        HoveredEnemy = null;
        HoveredMember = null;
        HoverCue = null;
        AimCue = null;
        AimCaught = Array.Empty<ActorState>();
        if (_turns.Phase != TurnPhase.Player || AnyMenuOpen) return;
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

    /// <summary>A click or key that did nothing says why, over <paramref name="obj"/>: only in the player phase, when the turn was the party's to spend.</summary>
    private void SayRefusal(GameObject obj, ClickCue? cue)
    {
        if (_turns.Phase == TurnPhase.Player && obj.IsActive && cue is { Ready: false } refused)
            _hud.AddFloatingText(obj.Position, refused.Label, CueColor, -40f);
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
            if (RayHitsSphere(origin, dir, candidate.Position, 0.65f, out float t) && t < bestT)
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

    private void UpdateEnemyVisibility()
    {
        foreach (var enemy in _enemies)
        {
            var (r, c) = LogicTile(enemy.State.X, enemy.State.Y);
            bool visible = _fog.Visible[r, c];
            enemy.SetVisible(visible); // hidden, a shove's slide ends where the logic put it
            _turns.NotifyEnemyVisible(enemy.State, visible);
        }
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
