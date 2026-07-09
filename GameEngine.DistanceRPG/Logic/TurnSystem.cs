namespace GameEngine.DistanceRPG.Logic;

/// <summary>Combat/turn phases, mirroring the prototype's implicit state machine.</summary>
public enum TurnPhase
{
    /// <summary>Party moves and attacks freely.</summary>
    Player,

    /// <summary>"End of Turn!" banner pause before the enemy acts.</summary>
    TurnEnding,

    /// <summary>Enemy walking its planned waypoints.</summary>
    EnemyMoving,

    /// <summary>Enemy spending leftover budget on attacks (one per beat).</summary>
    EnemyAttacking,

    /// <summary>Everyone is dead.</summary>
    GameOver,
}

/// <summary>
/// The turn state machine ported from the prototype, engine-free and driven by
/// <see cref="Update"/>. Owns all combat/turn state transitions; presentation
/// (floating text, banners, greying out corpses) subscribes to the events.
/// Timing constants match the original's tween/delayedCall pacing.
/// </summary>
public sealed class TurnSystem
{
    private const float BannerSeconds = 0.8f;
    private const float AttackBeatSeconds = 0.5f;
    private const float BraceDeathPauseSeconds = 0.4f;
    private const float GameOverPauseSeconds = 1.0f;

    private readonly int[,] _grid;
    private readonly IReadOnlyList<PartyMemberState> _party;
    private readonly EnemyState _enemy;
    private readonly Func<int> _rollD20;

    public TurnPhase Phase { get; private set; } = TurnPhase.Player;

    /// <summary>Completed turn count; increments when a new player turn starts.</summary>
    public int TurnCount { get; private set; }

    /// <summary>True once any party member has seen the enemy this player turn.</summary>
    public bool EnemySeenThisTurn { get; private set; }

    // ── Events for the presentation layer ────────────────────────────────────
    public event Action<float>? TurnEnded;                                 // total movement banked
    public event Action? PlayerTurnStarted;
    public event Action<PartyMemberState, AttackResolution>? CharacterHit;
    public event Action<PartyMemberState>? CharacterDied;
    public event Action<AttackResolution>? EnemyHit;
    public event Action? EnemyDefeated;
    public event Action? EnemyResurrected;
    public event Action<PartyMemberState>? BraceTriggered;
    public event Action? GameOver;

    // ── Enemy-turn working state ─────────────────────────────────────────────
    private float _timer;
    private float _enemyBudget;
    private List<(float X, float Y)> _waypoints = new();
    private int _waypointIdx;
    private PartyMemberState? _moveTarget;
    private Weapon? _moveTargetWeapon;
    private bool _wasInRangeBeforeMove;
    private bool _braceUsedThisTurn;
    private bool _enemyTurnPending;   // banner is up; enemy turn starts when it ends
    private bool _playerTurnPending;  // pause before control returns to the party

    public TurnSystem(int[,] grid, IReadOnlyList<PartyMemberState> party, EnemyState enemy, Func<int>? rollD20 = null)
    {
        _grid = grid;
        _party = party;
        _enemy = enemy;
        _rollD20 = rollD20 ?? (() => Random.Shared.Next(1, 21));
    }

    /// <summary>Scene calls this whenever the enemy's tile visibility changes.</summary>
    public void NotifyEnemyVisible(bool visible)
    {
        if (visible && Phase == TurnPhase.Player)
            EnemySeenThisTurn = true;
        // Seeing the enemy during its own movement also counts (the prototype
        // updates visibility while the enemy walks).
        if (visible && (Phase == TurnPhase.EnemyMoving || Phase == TurnPhase.EnemyAttacking))
            EnemySeenThisTurn = true;
    }

    // ── Player actions ───────────────────────────────────────────────────────

    public bool CanAttack(PartyMemberState c)
    {
        if (Phase != TurnPhase.Player || !_enemy.Alive || !c.Alive) return false;
        var w = c.EquippedWeapon;
        if (w == null || c.DistLeft < w.Cost) return false;
        return EnemyAi.CharCanHit(c, _enemy, w, _grid);
    }

    /// <summary>Attack the enemy with the given member. Returns false if not allowed.</summary>
    public bool TryAttack(PartyMemberState c)
    {
        if (!CanAttack(c)) return false;
        var w = c.EquippedWeapon!;

        c.DistLeft = MathF.Max(0f, c.DistLeft - w.Cost);
        ResolveAttackOnEnemy(w);
        return true;
    }

    /// <summary>End the player turn: bank leftover movement, then run the enemy.</summary>
    public void EndTurn()
    {
        if (Phase != TurnPhase.Player) return;

        float totalSaved = 0f;
        foreach (var c in _party)
            totalSaved += c.EndTurnSaveMovement();

        Phase = TurnPhase.TurnEnding;
        _timer = BannerSeconds;
        _enemyTurnPending = true;
        TurnEnded?.Invoke(totalSaved);
    }

    // ── Frame update ─────────────────────────────────────────────────────────

    public void Update(float deltaTime)
    {
        switch (Phase)
        {
            case TurnPhase.TurnEnding:
                _timer -= deltaTime;
                if (_timer <= 0f && _enemyTurnPending)
                {
                    _enemyTurnPending = false;
                    StartEnemyTurn();
                }
                break;

            case TurnPhase.EnemyMoving:
                UpdateEnemyMovement(deltaTime);
                break;

            case TurnPhase.EnemyAttacking:
                _timer -= deltaTime;
                if (_timer <= 0f)
                {
                    if (_playerTurnPending)
                    {
                        _playerTurnPending = false;
                        StartPlayerTurn();
                    }
                    else
                    {
                        TryEnemyAttackBeat();
                    }
                }
                break;
        }
    }

    // ── Enemy turn ───────────────────────────────────────────────────────────

    private void StartEnemyTurn()
    {
        if (!_enemy.Alive)
        {
            StartPlayerTurn();
            return;
        }

        if (EnemySeenThisTurn)
            _enemy.TurnsSinceSeen = 0;
        else
            _enemy.TurnsSinceSeen++;

        _enemyBudget = GameConstants.EnemyMove;

        // Unseen for 2+ turns: the dummy stays put (it may still attack if
        // someone parked next to it).
        if (_enemy.TurnsSinceSeen >= 2)
        {
            BeginAttackPhase();
            return;
        }

        var target = EnemyAi.SelectTarget(_enemy, _party, _grid);
        if (target == null)
        {
            BeginAttackPhase();
            return;
        }

        _moveTarget = target;
        _moveTargetWeapon = target.EquippedWeapon;
        _wasInRangeBeforeMove = EnemyAi.CharCanHit(target, _enemy, _moveTargetWeapon, _grid);

        var (waypoints, remaining) = EnemyAi.PlanMove(_enemy, target, _grid, _enemyBudget);
        _enemyBudget = remaining;

        if (waypoints.Count == 0)
        {
            AfterEnemyMove();
            return;
        }

        _waypoints = waypoints;
        _waypointIdx = 0;
        Phase = TurnPhase.EnemyMoving;
    }

    private void UpdateEnemyMovement(float deltaTime)
    {
        float step = GameConstants.EnemySpeed * deltaTime;

        while (step > 0f && _waypointIdx < _waypoints.Count)
        {
            var (wx, wy) = _waypoints[_waypointIdx];
            float dx = wx - _enemy.X;
            float dy = wy - _enemy.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= step)
            {
                _enemy.X = wx;
                _enemy.Y = wy;
                step -= dist;
                _waypointIdx++;
            }
            else
            {
                _enemy.X += dx / dist * step;
                _enemy.Y += dy / dist * step;
                step = 0f;
            }
        }

        if (_waypointIdx >= _waypoints.Count)
            AfterEnemyMove();
    }

    private void AfterEnemyMove()
    {
        // Brace: a free retaliation when the enemy walks into the braced
        // character's reach — once per turn, only if they weren't already
        // in range and are still the enemy's chosen target.
        var target = _moveTarget;
        var weapon = _moveTargetWeapon;
        if (target != null && weapon?.GetAbility(AbilityType.Brace) != null
            && !_braceUsedThisTurn && !_wasInRangeBeforeMove)
        {
            var t2 = EnemyAi.SelectTarget(_enemy, _party, _grid);
            if (t2 == target && EnemyAi.CharCanHit(target, _enemy, weapon, _grid))
            {
                _braceUsedThisTurn = true;
                BraceTriggered?.Invoke(target);
                ResolveAttackOnEnemy(weapon);
            }
        }

        if (_enemy.Alive)
        {
            BeginAttackPhase();
        }
        else
        {
            // Brace killed the dummy mid-turn: short pause, then hand back control.
            Phase = TurnPhase.EnemyAttacking;
            _playerTurnPending = true;
            _timer = BraceDeathPauseSeconds;
        }
    }

    private void BeginAttackPhase()
    {
        Phase = TurnPhase.EnemyAttacking;
        _timer = 0f; // first beat resolves on the next update
    }

    private void TryEnemyAttackBeat()
    {
        // Attack cost scales the same way the prototype scaled it: the enemy's
        // budget is 100 vs the player's 160, so weapon costs shrink to match.
        float scaledCost = GameConstants.EnemyMove / GameConstants.MaxDistance * _enemy.Weapon.Cost;

        if (!_enemy.Alive || _enemyBudget < scaledCost)
        {
            _playerTurnPending = true;
            _timer = AttackBeatSeconds;
            return;
        }

        var hittable = _party.Where(c => c.Alive && EnemyAi.CanHit(_enemy, c, _enemy.Weapon, _grid)).ToList();
        if (hittable.Count == 0)
        {
            _playerTurnPending = true;
            _timer = AttackBeatSeconds;
            return;
        }

        var target = hittable[0];
        float bestDist = Dist2(target);
        for (int i = 1; i < hittable.Count; i++)
        {
            float d = Dist2(hittable[i]);
            if (d < bestDist)
            {
                target = hittable[i];
                bestDist = d;
            }
        }

        _enemyBudget -= scaledCost;
        var resolution = CombatRules.ResolveAttack(_enemy.Weapon, target.EquippedWeapon, _rollD20);
        target.Hp = Math.Max(0, target.Hp - resolution.Damage);
        CharacterHit?.Invoke(target, resolution);

        if (target.Hp <= 0)
        {
            target.Alive = false;
            target.SavedMovement = 0;
            CharacterDied?.Invoke(target);

            if (_party.All(c => !c.Alive))
            {
                Phase = TurnPhase.GameOver;
                _timer = GameOverPauseSeconds;
                GameOver?.Invoke();
                return;
            }
        }

        _timer = AttackBeatSeconds;
    }

    private void StartPlayerTurn()
    {
        EnemySeenThisTurn = false;
        TurnCount++;

        if (!_enemy.Alive && TurnCount - _enemy.DefeatedAtTurn >= GameConstants.DummyResurrectTurns)
        {
            _enemy.Hp = _enemy.MaxHp;
            _enemy.Alive = true;
            _enemy.TurnsSinceSeen = 2;
            EnemyResurrected?.Invoke();
        }

        _braceUsedThisTurn = false;
        foreach (var c in _party)
        {
            if (c.Alive) c.StartTurn();
        }

        Phase = TurnPhase.Player;
        PlayerTurnStarted?.Invoke();
    }

    // ── Shared attack plumbing ───────────────────────────────────────────────

    private void ResolveAttackOnEnemy(Weapon attackerWeapon)
    {
        var resolution = CombatRules.ResolveAttack(attackerWeapon, _enemy.Weapon, _rollD20);
        _enemy.Hp = Math.Max(0, _enemy.Hp - resolution.Damage);
        EnemyHit?.Invoke(resolution);

        if (_enemy.Hp <= 0)
        {
            _enemy.Alive = false;
            _enemy.DefeatedAtTurn = TurnCount;
            EnemyDefeated?.Invoke();
        }
    }

    private float Dist2(PartyMemberState c)
    {
        float dx = _enemy.X - c.X;
        float dy = _enemy.Y - c.Y;
        return dx * dx + dy * dy;
    }
}
