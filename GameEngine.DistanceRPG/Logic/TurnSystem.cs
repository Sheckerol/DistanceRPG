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
/// <see cref="Update"/>, extended to any number of enemies: on the enemy turn
/// they act one at a time (plan → walk → attack beats), and passive enemies
/// (unseen for 2+ turns with nobody in reach) skip instantly so a populated
/// maze doesn't stall the turn. Presentation (floating text, banners, greying
/// out corpses) subscribes to the events. Timing constants match the
/// original's tween/delayedCall pacing.
/// </summary>
public sealed class TurnSystem
{
    private const float BannerSeconds = 0.8f;
    private const float AttackBeatSeconds = 0.5f;
    private const float BraceDeathPauseSeconds = 0.4f;
    private const float GameOverPauseSeconds = 1.0f;

    private readonly int[,] _grid;
    private readonly IReadOnlyList<PartyMemberState> _party;
    private readonly IReadOnlyList<EnemyState> _enemies;
    private readonly Func<int> _rollD20;

    public TurnPhase Phase { get; private set; } = TurnPhase.Player;

    /// <summary>Completed turn count; increments when a new player turn starts.</summary>
    public int TurnCount { get; private set; }

    /// <summary>Enemies a party member has laid eyes on this turn (cleared each player turn).</summary>
    private readonly HashSet<EnemyState> _seenThisTurn = new();

    /// <summary>True once any still-living enemy has been seen this turn — the combat-footing signal.</summary>
    public bool AnyLiveEnemySeenThisTurn => _seenThisTurn.Any(e => e.Alive);

    // ── Events for the presentation layer ────────────────────────────────────
    public event Action<float>? TurnEnded;                                 // total movement banked
    public event Action? PlayerTurnStarted;
    public event Action<PartyMemberState, AttackResolution>? CharacterHit;
    public event Action<PartyMemberState>? CharacterDied;
    public event Action<EnemyState, AttackResolution>? EnemyHit;
    public event Action<EnemyState>? EnemyDefeated;
    public event Action<EnemyState>? EnemyResurrected;
    public event Action<PartyMemberState>? BraceTriggered;
    public event Action? GameOver;

    // ── Enemy-turn working state ─────────────────────────────────────────────
    private int _enemyIdx;            // index of the enemy currently acting
    private float _timer;
    private float _enemyBudget;
    private List<(float X, float Y)> _waypoints = new();
    private int _waypointIdx;
    private PartyMemberState? _moveTarget;
    private Weapon? _moveTargetWeapon;
    private bool _wasInRangeBeforeMove;
    private bool _braceUsedThisTurn;
    private bool _enemyTurnPending;   // banner is up; enemy turn starts when it ends
    private bool _nextEnemyPending;   // pause before the next enemy acts (or control returns)

    private EnemyState ActingEnemy => _enemies[_enemyIdx];

    public TurnSystem(int[,] grid, IReadOnlyList<PartyMemberState> party,
        IReadOnlyList<EnemyState> enemies, Func<int>? rollD20 = null)
    {
        _grid = grid;
        _party = party;
        _enemies = enemies;
        _rollD20 = rollD20 ?? (() => Random.Shared.Next(1, 21));
    }

    /// <summary>Scene calls this whenever an enemy's tile visibility changes.</summary>
    public void NotifyEnemyVisible(EnemyState enemy, bool visible)
    {
        if (!visible) return;
        // Seeing an enemy during the enemy turn also counts (the prototype
        // updates visibility while enemies walk).
        if (Phase is TurnPhase.Player or TurnPhase.EnemyMoving or TurnPhase.EnemyAttacking)
            _seenThisTurn.Add(enemy);
    }

    // ── Player actions ───────────────────────────────────────────────────────

    public bool CanAttack(PartyMemberState c, EnemyState enemy)
    {
        if (Phase != TurnPhase.Player || !enemy.Alive || !c.Alive) return false;
        var w = c.EquippedWeapon;
        if (w == null || c.DistLeft < w.Cost) return false;
        return EnemyAi.CharCanHit(c, enemy, w, _grid);
    }

    /// <summary>Attack an enemy with the given member. Returns false if not allowed.</summary>
    public bool TryAttack(PartyMemberState c, EnemyState enemy)
    {
        if (!CanAttack(c, enemy)) return false;
        var w = c.EquippedWeapon!;

        c.DistLeft = MathF.Max(0f, c.DistLeft - w.Cost);
        ResolveAttackOnEnemy(w, enemy);
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
                    if (_nextEnemyPending)
                    {
                        _nextEnemyPending = false;
                        AdvanceToNextEnemy();
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
        // Seen bookkeeping happens once for everyone, before anyone acts.
        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive) continue;
            if (_seenThisTurn.Contains(enemy))
                enemy.TurnsSinceSeen = 0;
            else
                enemy.TurnsSinceSeen++;
        }

        _enemyIdx = -1;
        AdvanceToNextEnemy();
    }

    /// <summary>
    /// Hand the enemy turn to the next enemy that will actually do something;
    /// when none remain, the player turn starts. Dead enemies and passive
    /// ones — unseen for 2+ turns with nobody in reach — skip instantly, so a
    /// maze full of idle dummies costs no wall-clock time.
    /// </summary>
    private void AdvanceToNextEnemy()
    {
        while (++_enemyIdx < _enemies.Count)
        {
            var enemy = _enemies[_enemyIdx];
            if (!enemy.Alive) continue;
            if (enemy.TurnsSinceSeen >= 2 && !AnyHittableBy(enemy)) continue;

            StartEnemyAction(enemy);
            return;
        }

        StartPlayerTurn();
    }

    private void StartEnemyAction(EnemyState enemy)
    {
        _enemyBudget = GameConstants.EnemyMove;

        // Unseen for 2+ turns: the dummy stays put (it may still attack —
        // AdvanceToNextEnemy only let it through because someone is in reach).
        if (enemy.TurnsSinceSeen >= 2)
        {
            BeginAttackPhase();
            return;
        }

        var target = EnemyAi.SelectTarget(enemy, _party, _grid);
        if (target == null)
        {
            BeginAttackPhase();
            return;
        }

        _moveTarget = target;
        _moveTargetWeapon = target.EquippedWeapon;
        _wasInRangeBeforeMove = EnemyAi.CharCanHit(target, enemy, _moveTargetWeapon, _grid);

        var (waypoints, remaining) = EnemyAi.PlanMove(enemy, target, _grid, _enemyBudget);
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

    private bool AnyHittableBy(EnemyState enemy)
        => _party.Any(c => c.Alive && EnemyAi.CanHit(enemy, c, enemy.Weapon, _grid));

    private void UpdateEnemyMovement(float deltaTime)
    {
        var enemy = ActingEnemy;
        float step = GameConstants.EnemySpeed * deltaTime;

        while (step > 0f && _waypointIdx < _waypoints.Count)
        {
            var (wx, wy) = _waypoints[_waypointIdx];
            float dx = wx - enemy.X;
            float dy = wy - enemy.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= step)
            {
                enemy.X = wx;
                enemy.Y = wy;
                step -= dist;
                _waypointIdx++;
            }
            else
            {
                enemy.X += dx / dist * step;
                enemy.Y += dy / dist * step;
                step = 0f;
            }
        }

        if (_waypointIdx >= _waypoints.Count)
            AfterEnemyMove();
    }

    private void AfterEnemyMove()
    {
        var enemy = ActingEnemy;

        // Brace: a free retaliation when an enemy walks into the braced
        // character's reach — once per turn, only if they weren't already
        // in range and are still that enemy's chosen target.
        var target = _moveTarget;
        var weapon = _moveTargetWeapon;
        if (target != null && weapon?.GetAbility(AbilityType.Brace) != null
            && !_braceUsedThisTurn && !_wasInRangeBeforeMove)
        {
            var t2 = EnemyAi.SelectTarget(enemy, _party, _grid);
            if (t2 == target && EnemyAi.CharCanHit(target, enemy, weapon, _grid))
            {
                _braceUsedThisTurn = true;
                BraceTriggered?.Invoke(target);
                ResolveAttackOnEnemy(weapon, enemy);
            }
        }

        if (enemy.Alive)
        {
            BeginAttackPhase();
        }
        else
        {
            // Brace killed this one mid-walk: short pause, then the next enemy acts.
            Phase = TurnPhase.EnemyAttacking;
            _nextEnemyPending = true;
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
        var enemy = ActingEnemy;

        // Attack cost scales the same way the prototype scaled it: the enemy's
        // budget is 100 vs the player's 160, so weapon costs shrink to match.
        float scaledCost = GameConstants.EnemyMove / GameConstants.MaxDistance * enemy.Weapon.Cost;

        if (!enemy.Alive || _enemyBudget < scaledCost)
        {
            _nextEnemyPending = true;
            _timer = AttackBeatSeconds;
            return;
        }

        var hittable = _party.Where(c => c.Alive && EnemyAi.CanHit(enemy, c, enemy.Weapon, _grid)).ToList();
        if (hittable.Count == 0)
        {
            _nextEnemyPending = true;
            _timer = AttackBeatSeconds;
            return;
        }

        var target = hittable[0];
        float bestDist = Dist2(enemy, target);
        for (int i = 1; i < hittable.Count; i++)
        {
            float d = Dist2(enemy, hittable[i]);
            if (d < bestDist)
            {
                target = hittable[i];
                bestDist = d;
            }
        }

        _enemyBudget -= scaledCost;
        var resolution = CombatRules.ResolveAttack(enemy.Weapon, target.EquippedWeapon, _rollD20);
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
        _seenThisTurn.Clear();
        TurnCount++;

        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive && TurnCount - enemy.DefeatedAtTurn >= GameConstants.DummyResurrectTurns)
            {
                enemy.Hp = enemy.MaxHp;
                enemy.Alive = true;
                enemy.TurnsSinceSeen = 2;
                EnemyResurrected?.Invoke(enemy);
            }
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

    private void ResolveAttackOnEnemy(Weapon attackerWeapon, EnemyState enemy)
    {
        var resolution = CombatRules.ResolveAttack(attackerWeapon, enemy.Weapon, _rollD20);
        enemy.Hp = Math.Max(0, enemy.Hp - resolution.Damage);
        EnemyHit?.Invoke(enemy, resolution);

        if (enemy.Hp <= 0)
        {
            enemy.Alive = false;
            enemy.DefeatedAtTurn = TurnCount;
            EnemyDefeated?.Invoke(enemy);
        }
    }

    private static float Dist2(EnemyState enemy, PartyMemberState c)
    {
        float dx = enemy.X - c.X;
        float dy = enemy.Y - c.Y;
        return dx * dx + dy * dy;
    }
}
