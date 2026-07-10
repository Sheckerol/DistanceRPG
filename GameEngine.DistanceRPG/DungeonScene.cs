using GameEngine.Core;
using GameEngine.Core.Diagnostics;
using GameEngine.Core.Particles;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The main gameplay scene: a procedurally generated dungeon explored by a
/// four-character party in turn-based, distance-budgeted combat. The dungeon
/// is the prototype's exact map (same seed, same generator), reinterpreted in
/// 3D: floor slab on the XZ plane, extruded wall boxes, angled top-down camera.
/// </summary>
public class DungeonScene : Scene
{
    /// <summary>The map seed the Phaser prototype ships with.</summary>
    public const long MapSeed = 2762136374;

    private const float WallHeight = 1.2f;
    private const float FloorThickness = 0.2f;

    // Palette carried over from the prototype's hex colours.
    private static readonly Vector4 WallColor = Rgb(0x3d405b);
    private static readonly Vector4 FloorColor = Rgb(0x0f3460);
    private static readonly Vector4 EnemyColor = Rgb(0xf5a623);
    private static readonly Vector4 DeadColor = Rgb(0x555555);

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
    private EnemyObject _enemy = null!;
    private (int R, int C) _lastEnemyTile = (-1, -1);
    private bool _enemyVisible;
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
    public EnemyObject Enemy => _enemy;
    public bool EnemyVisible => _enemyVisible;
    public bool InventoryOpen { get; private set; }
    public float LastBankedMovement { get; private set; }
    public Vector4 PartyColor(int idx) => PartyColors[idx];

    public override void Initialize()
    {
        base.Initialize();

        _map = MapGenerator.Generate(new Mulberry32(MapSeed));
        _fogBoxes = FogBoxBuilder.Build(_map); // also expands _map.Corridors in place
        _fog = new FogState(MapGenerator.Rows, MapGenerator.Cols);

        BuildFloor();
        BuildWalls();
        SpawnParty();
        SpawnEnemy();
        BuildFogOverlay();   // after all geometry: its translucent pass blends over everything
        BuildFogParticles(); // after the fog overlay: mist blends over the shroud
        WireTurnSystem();
        WireInput();

        // Initial fog reveal around the party, and hide the enemy if fogged.
        foreach (var member in _party)
            UpdateFogFor(member);
        UpdateEnemyVisibility();

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

        // While the enemy walks its waypoints, mirror its logic position and
        // refresh visibility when it crosses tile boundaries.
        if (_turns.Phase == TurnPhase.EnemyMoving)
        {
            _enemy.SyncTransform();
            var tile = LogicTile(_enemy.State.X, _enemy.State.Y);
            if (tile != _lastEnemyTile)
            {
                _lastEnemyTile = tile;
                UpdateEnemyVisibility();
            }
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

            var character = new CharacterObject(state, PartyColors[i]);
            _party.Add(character);
            AddGameObject(character);
        }

        _activeIdx = 0;
    }

    private void SpawnEnemy()
    {
        var state = new EnemyState
        {
            X = _map.EnemyStart.Col * GameConstants.Tile + GameConstants.Tile / 2f,
            Y = _map.EnemyStart.Row * GameConstants.Tile + GameConstants.Tile / 2f,
        };
        _enemy = new EnemyObject(state, EnemyColor);
        AddGameObject(_enemy);
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
        mist.LifetimeMin = 0.7f;
        mist.LifetimeMax = 1.3f;
        mist.Direction = Vector3.UnitY;
        mist.SpreadDegrees = 65f;
        mist.SpeedMin = 0.25f;
        mist.SpeedMax = 0.7f;
        mist.SizeMin = 0.35f;
        mist.SizeMax = 0.6f;
        mist.EndSizeFactor = 2.5f;
        mist.StartColor = new Vector4(0.72f, 0.78f, 0.92f, 0.4f);
        mist.EndColor = new Vector4(0.72f, 0.78f, 0.92f, 0f);
        mist.Drag = 1.2f;
        mist.Gravity = new Vector3(0f, 0.2f, 0f); // gentle updraft as the fog dissipates
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
        wisp.StartColor = new Vector4(0.6f, 0.65f, 0.8f, 0.14f);
        wisp.EndColor = new Vector4(0.6f, 0.65f, 0.8f, 0f);
        wisp.Drag = 0.4f;
        wisp.Gravity = new Vector3(0f, 0.05f, 0f);
        AddGameObject(wispObject);
    }

    private void WireTurnSystem()
    {
        _turns = new TurnSystem(_map.Grid, _party.Select(p => p.State).ToList(), _enemy.State);

        _turns.TurnEnded += saved =>
        {
            Log.Info($"[Turns] End of turn — banked {saved} movement");
            LastBankedMovement = saved;
            if (InventoryOpen) InventoryOpen = false;
            ResetFogVisibility();
        };

        _turns.PlayerTurnStarted += () =>
        {
            Log.Info($"[Turns] Player turn {_turns.TurnCount} begins");
            if (!ActiveCharacter.State.Alive)
                SetActiveCharacter(_party.FindIndex(p => p.State.Alive), force: true);
        };

        _turns.EnemyHit += res =>
        {
            Log.Info($"[Combat] Enemy hit: roll {res.Roll.Roll} ({res.Roll.Outcome}) for {res.Damage} (blocked {res.Blocked}) — enemy HP {_enemy.State.Hp}");
            SpawnAttackTexts(_enemy.Position, res);
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

        _turns.EnemyDefeated += () =>
        {
            Log.Info("[Combat] Enemy defeated!");
            SetColor(_enemy, DeadColor);
            _hud.AddFloatingText(_enemy.Position, "DEFEATED!", new Vector4(1f, 1f, 1f, 1f), -54f);
        };

        _turns.EnemyResurrected += () =>
        {
            Log.Info("[Combat] Enemy resurrected!");
            SetColor(_enemy, EnemyColor);
            UpdateEnemyVisibility();
        };

        _turns.BraceTriggered += c =>
        {
            Log.Info($"[Combat] {c.Id} braces!");
            var obj = _party.First(p => p.State == c);
            _hud.AddFloatingText(obj.Position, "BRACE!", new Vector4(0.53f, 1f, 1f, 1f), -52f);
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

        // Number keys select party members, or equip inventory slots while the
        // bag is open (2/3 swap that slot with the equipped slot).
        input.SubscribeToKeyPressed(_ => { if (!InventoryOpen) SetActiveCharacter(0); }, Keys.D1);
        input.SubscribeToKeyPressed(_ => { if (InventoryOpen) EquipSlot(1); else SetActiveCharacter(1); }, Keys.D2);
        input.SubscribeToKeyPressed(_ => { if (InventoryOpen) EquipSlot(2); else SetActiveCharacter(2); }, Keys.D3);
        input.SubscribeToKeyPressed(_ => { if (!InventoryOpen) SetActiveCharacter(3); }, Keys.D4);
        input.SubscribeToKeyPressed(_ => { if (!InventoryOpen) CycleActiveCharacter(); }, Keys.Tab);

        input.SubscribeToKeyPressed(_ => { if (!InventoryOpen) _turns.EndTurn(); }, Keys.Space, Keys.Enter);
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
        if (_turns.Phase == TurnPhase.GameOver) return;
        InventoryOpen = !InventoryOpen;
    }

    /// <summary>Swap an inventory slot with the equipped slot (slot 0).</summary>
    private void EquipSlot(int slot)
    {
        var inventory = ActiveCharacter.State.Inventory;
        (inventory[0], inventory[slot]) = (inventory[slot], inventory[0]);
    }

    private void UpdateActiveCharacterMovement(float deltaTime)
    {
        if (_turns.Phase != TurnPhase.Player || InventoryOpen) return;

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

        // Cap this frame's intended travel to the remaining budget.
        float dx = vx * deltaTime;
        float dy = vy * deltaTime;
        float frameDist = MathF.Sqrt(dx * dx + dy * dy);
        if (frameDist > state.DistLeft)
        {
            float scale = state.DistLeft / frameDist;
            dx *= scale;
            dy *= scale;
        }

        // Walls and the enemy block; party members pass through each other
        // (the prototype dropped inter-party colliders — they caused wall shoves).
        var blockers = _enemy.State.Alive
            ? new[] { new GridCollision.Circle(_enemy.State.X, _enemy.State.Y, _enemy.State.Radius) }
            : null;
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
    }

    // ── Combat input ─────────────────────────────────────────────────────────

    private void HandleClick()
    {
        if (InventoryOpen) return;
        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;
        if (w <= 0 || h <= 0) return;
        var (origin, dir) = _camera.ScreenToWorldRay(_mousePos.X, _mousePos.Y, w, h);

        // Nearest sphere hit wins: the enemy attacks, a party member selects.
        float bestT = float.MaxValue;
        Action? action = null;

        if (_enemy.State.Alive && _enemyVisible
            && RayHitsSphere(origin, dir, _enemy.Position, 0.65f, out float tEnemy) && tEnemy < bestT)
        {
            bestT = tEnemy;
            action = () => _turns.TryAttack(ActiveCharacter.State);
        }

        for (int i = 0; i < _party.Count; i++)
        {
            var member = _party[i];
            if (!member.State.Alive) continue;
            if (RayHitsSphere(origin, dir, member.Position, 0.6f, out float tChar) && tChar < bestT)
            {
                bestT = tChar;
                int idx = i;
                action = () => SetActiveCharacter(idx);
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

    /// <summary>Schedule mist puffs on revealed tiles, following the reveal ripple's stagger.</summary>
    private void QueueRevealMist(IReadOnlyList<(int R, int C, bool WasSeen)> tiles, (int R, int C) origin)
    {
        foreach (var (r, c, _) in tiles)
        {
            int dist = Math.Abs(r - origin.R) + Math.Abs(c - origin.C);
            var pos = WorldSpace.TileCenter(r, c, _fogOverlay.Height);
            _pendingMistBursts.Add((dist * 0.018f + 0.1f, pos));
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
                _mistEmitter.Burst(2, pos);
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
            _wispEmitter.Burst(1, WorldSpace.TileCenter(r, c, _fogOverlay.Height + 0.1f));
            break;
        }
    }

    private void UpdateEnemyVisibility()
    {
        var (r, c) = LogicTile(_enemy.State.X, _enemy.State.Y);
        bool visible = _fog.Visible[r, c];
        _enemyVisible = visible;
        _enemy.IsActive = visible;
        _turns.NotifyEnemyVisible(visible);
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
