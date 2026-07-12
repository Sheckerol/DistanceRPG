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

    private static readonly Vector4[] PartyColors =
    {
        Rgb(0xe94560), // A
        Rgb(0x3b8eff), // B
        Rgb(0x44cc66), // C
        Rgb(0xffdd44), // D
    };

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
            state.Inventory[0] = GameConstants.Weapons[GameConstants.CharStartingWeaponIdx[i]];
            state.Inventory[1] = GameConstants.Weapons[GameConstants.StaffWeaponIdx]; // healer option for anyone

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
        foreach (var (x, y, weaponIdx) in EnemyPlacer.PlaceEnemies(_map, MapSeed))
        {
            var state = new EnemyState { X = x, Y = y, Weapon = GameConstants.Weapons[weaponIdx] };
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
            _enemies.Select(e => e.State).ToList());

        _turns.TurnEnded += saved =>
        {
            Log.Info($"[Turns] End of turn — banked {saved} movement");
            LastBankedMovement = saved;
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
            Log.Info($"[Combat] Enemy hit: roll {res.Roll.Roll} ({res.Roll.Outcome}) for {res.Damage} (blocked {res.Blocked}) — enemy HP {enemy.Hp}");
            SpawnAttackTexts(EnemyObjectFor(enemy).Position, res);
        };

        _turns.CharacterHit += (c, res) =>
        {
            Log.Info($"[Combat] {c.Id} hit: roll {res.Roll.Roll} ({res.Roll.Outcome}) for {res.Damage} (blocked {res.Blocked}) — HP {c.Hp}");
            var obj = _party.First(p => p.State == c);
            SpawnAttackTexts(obj.Position, res);
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
        {
            Log.Info($"[Combat] {c.Id} gains {effect.Type} Lv{effect.Level}");
            var obj = _party.First(p => p.State == c);
            _hud.AddFloatingText(obj.Position, $"REGEN Lv{effect.Level}", HealColor, -52f);
        };

        _turns.CharacterHealed += (c, amount) =>
        {
            Log.Info($"[Combat] {c.Id} regenerates {amount} — HP {c.Hp}");
            var obj = _party.First(p => p.State == c);
            _hud.AddFloatingText(obj.Position, $"+{amount}", HealColor, 8f);
        };

        _turns.GameOver += () => Log.Info("[Turns] GAME OVER");
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

    /// <summary>Swap an inventory slot with the equipped slot (slot 0).</summary>
    private void EquipSlot(int slot)
    {
        var inventory = ActiveCharacter.State.Inventory;
        (inventory[0], inventory[slot]) = (inventory[slot], inventory[0]);
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

        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;
        if (w <= 0 || h <= 0) return;
        var (origin, dir) = _camera.ScreenToWorldRay(_mousePos.X, _mousePos.Y, w, h);

        // Nearest sphere hit wins: the enemy attacks, a party member selects.
        float bestT = float.MaxValue;
        Action? action = null;

        foreach (var enemy in _enemies)
        {
            if (!enemy.State.Alive || !enemy.IsActive) continue;
            if (RayHitsSphere(origin, dir, enemy.Position, 0.65f, out float tEnemy) && tEnemy < bestT)
            {
                bestT = tEnemy;
                var target = enemy.State;
                action = () => _turns.TryAttack(ActiveCharacter.State, target);
            }
        }

        for (int i = 0; i < _party.Count; i++)
        {
            var member = _party[i];
            if (!member.State.Alive) continue;
            if (RayHitsSphere(origin, dir, member.Position, 0.6f, out float tChar) && tChar < bestT)
            {
                bestT = tChar;
                int idx = i;
                var target = member.State;
                // A staff-wielder's ally-click is always a heal — identical
                // rules in and out of combat, spending the caster's movement.
                // It never falls back to a leader-switch, which would reset the
                // march formation mid-explore. Without a staff, the click
                // selects the ally as the new leader instead.
                bool casting = ActiveCharacter.State.EquippedWeapon?.IsCaster == true;
                action = casting
                    ? () => _turns.TryCast(ActiveCharacter.State, target)
                    : () => SetActiveCharacter(idx);
            }
        }

        action?.Invoke();
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
            enemy.IsActive = visible;
            _turns.NotifyEnemyVisible(enemy.State, visible);
        }
    }

    private EnemyObject EnemyObjectFor(EnemyState state) => _enemies.First(e => e.State == state);

    /// <summary>Live enemies as collision blockers; corpses are walkable.</summary>
    private List<GridCollision.Circle> LiveEnemyBlockers()
    {
        var blockers = new List<GridCollision.Circle>();
        foreach (var enemy in _enemies)
            if (enemy.State.Alive)
                blockers.Add(new GridCollision.Circle(enemy.State.X, enemy.State.Y, enemy.State.Radius));
        return blockers;
    }

    private void SpawnAttackTexts(Vector3 worldPos, AttackResolution res)
    {
        _hud.AddFloatingText(worldPos, $"ROLL: {res.Roll.Roll}", new Vector4(1f, 1f, 1f, 1f), -28f);

        var (label, color) = res.Roll.Outcome switch
        {
            RollOutcome.Crit => ($"CRIT! -{res.Damage}", new Vector4(1f, 0.87f, 0f, 1f)),
            RollOutcome.Weak => ($"WEAK -{res.Damage}", new Vector4(0.67f, 0.67f, 0.67f, 1f)),
            _ => ($"-{res.Damage}", new Vector4(1f, 0.27f, 0.27f, 1f)),
        };
        _hud.AddFloatingText(worldPos, label, color, 8f);

        if (res.Blocked > 0)
            _hud.AddFloatingText(worldPos, $"BLOCK {res.Blocked}", new Vector4(0.31f, 0.76f, 0.97f, 1f), -48f);
    }

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
