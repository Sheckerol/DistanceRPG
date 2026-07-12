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
    public event Action<EnemyState>? EnemyBraceTriggered;
    public event Action? GameOver;

    // ── Enemy-turn working state ─────────────────────────────────────────────
    private int _enemyIdx;            // index of the enemy currently acting
    private float _timer;
    private float _enemyBudget;
    private List<(float X, float Y)> _waypoints = new();
    private int _waypointIdx;
    private bool _enemyTurnPending;   // banner is up; enemy turn starts when it ends
    private bool _nextEnemyPending;   // pause before the next enemy acts (or control returns)

    // Brace bookkeeping: spear-wielders threaten a zone. Members NOT already
    // holding the acting enemy in reach are snapshotted before it walks; any
    // of them whose reach it enters retaliates for free, up to the weapon's
    // Brace value uses per turn. (The prototype only braced the enemy's own
    // chosen target — with formations and many enemies, the nearest member
    // is always the target and back-row spears would never fire.)
    private readonly List<PartyMemberState> _braceCandidates = new();
    private readonly Dictionary<PartyMemberState, int> _braceUsesThisTurn = new();

    // Enemy-side brace mirror: spear dummies threaten a zone too. Pairs
    // already in reach at the start of the player turn never trigger; a
    // character whose movement carries them into a seen spear enemy's reach
    // eats a free poke, up to the enemy weapon's Brace value per turn.
    private readonly HashSet<(EnemyState Enemy, PartyMemberState Member)> _inEnemyReach = new();
    private readonly Dictionary<EnemyState, int> _enemyBraceUsesThisTurn = new();

    private EnemyState ActingEnemy => _enemies[_enemyIdx];

    public TurnSystem(int[,] grid, IReadOnlyList<PartyMemberState> party,
        IReadOnlyList<EnemyState> enemies, Func<int>? rollD20 = null)
    {
        _grid = grid;
        _party = party;
        _enemies = enemies;
        _rollD20 = rollD20 ?? (() => Random.Shared.Next(1, 21));
    }

    /// <summary>
    /// Scene calls this after a character's position changes during the
    /// player phase. Walking into a seen, live, spear-wielding enemy's reach
    /// triggers its brace: a free retaliation, up to the weapon's Brace value
    /// per turn. Leaving reach re-arms the pair (further entries still cost
    /// the enemy a use). Pairs already in reach when the turn began never
    /// trigger — standing ground is safe, walking in is not.
    /// </summary>
    public void NotifyCharacterMoved(PartyMemberState mover)
    {
        if (Phase != TurnPhase.Player || !mover.Alive) return;

        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive) continue;

            bool inReach = EnemyAi.CanHit(enemy, mover, enemy.Weapon, _grid);
            if (!inReach)
            {
                _inEnemyReach.Remove((enemy, mover));
                continue;
            }
            if (!_inEnemyReach.Add((enemy, mover))) continue; // was already in reach

            var brace = enemy.Weapon.GetAbility(AbilityType.Brace);
            if (brace == null) continue;
            if (!_seenThisTurn.Contains(enemy)) continue; // no ambushes from the fog

            _enemyBraceUsesThisTurn.TryGetValue(enemy, out int used);
            if (used >= brace.Value) continue;

            _enemyBraceUsesThisTurn[enemy] = used + 1;
            EnemyBraceTriggered?.Invoke(enemy);
            ResolveAttackOnCharacter(enemy, mover);
            if (Phase == TurnPhase.GameOver) return;
        }
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

        // Snapshot who could NOT reach this enemy yet — walking into their
        // reach is what triggers a brace.
        _braceCandidates.Clear();
        foreach (var member in _party)
        {
            var w = member.EquippedWeapon;
            if (member.Alive && w?.GetAbility(AbilityType.Brace) != null
                && !EnemyAi.CharCanHit(member, enemy, w, _grid))
                _braceCandidates.Add(member);
        }

        // Everyone else on the board blocks this enemy's path — enemies act
        // sequentially, so each planner sees the ones already in position and
        // queues behind them instead of piling onto the same tile.
        var blocked = new HashSet<(int R, int C)>();
        foreach (var other in _enemies)
            if (other != enemy && other.Alive)
                blocked.Add(TileOf(other.X, other.Y));
        foreach (var member in _party)
            if (member.Alive)
                blocked.Add(TileOf(member.X, member.Y));

        var (waypoints, remaining) = EnemyAi.PlanMove(enemy, target, _grid, _enemyBudget, blocked);
        _enemyBudget = remaining;

        if (waypoints.Count == 0)
        {
            // No movement means no reach was crossed — straight to attacks.
            BeginAttackPhase();
            return;
        }

        _waypoints = waypoints;
        _waypointIdx = 0;
        Phase = TurnPhase.EnemyMoving;
    }

    private bool AnyHittableBy(EnemyState enemy)
        => _party.Any(c => c.Alive && EnemyAi.CanHit(enemy, c, enemy.Weapon, _grid));

    private static (int R, int C) TileOf(float x, float y)
        => ((int)MathF.Floor(y / GameConstants.Tile), (int)MathF.Floor(x / GameConstants.Tile));

    /// <summary>
    /// Nearest floor tile not occupied by a living actor, breadth-first from
    /// the enemy's own tile (so its own tile wins when it's free). Expansion
    /// only crosses floor, keeping the result in the same room or corridor.
    /// Falls back to staying put if everything reachable is somehow taken.
    /// </summary>
    private (int R, int C) FindNearestFreeTile(EnemyState self)
    {
        var start = TileOf(self.X, self.Y);
        int rows = _grid.GetLength(0), cols = _grid.GetLength(1);
        var visited = new HashSet<(int R, int C)> { start };
        var queue = new Queue<(int R, int C)>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            if (IsFreeTile(r, c, self)) return (r, c);

            foreach (var (nr, nc) in new[] { (r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1) })
            {
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                if (_grid[nr, nc] != 0 || !visited.Add((nr, nc))) continue;
                queue.Enqueue((nr, nc));
            }
        }

        return start;
    }

    private bool IsFreeTile(int r, int c, EnemyState self)
    {
        if (_grid[r, c] != 0) return false;
        if (_party.Any(m => m.Alive && TileOf(m.X, m.Y) == (r, c))) return false;
        return !_enemies.Any(e => e != self && e.Alive && TileOf(e.X, e.Y) == (r, c));
    }

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

        // Braces resolve mid-walk: every spear-wielder stabs the moment the
        // enemy crosses into their reach — zones merely passed through count,
        // not just wherever the walk ends. A kill stops the walk on the spot.
        TryBracesAgainst(enemy);
        if (!enemy.Alive)
        {
            Phase = TurnPhase.EnemyAttacking;
            _nextEnemyPending = true;
            _timer = BraceDeathPauseSeconds;
            return;
        }

        if (_waypointIdx >= _waypoints.Count)
            BeginAttackPhase();
    }

    /// <summary>
    /// Fire a free retaliation from each brace candidate whose reach contains
    /// the enemy right now. A member stabs a given walk at most once (they
    /// leave the candidate list), and the weapon's Brace value caps their
    /// uses per turn across all walks.
    /// </summary>
    private void TryBracesAgainst(EnemyState enemy)
    {
        for (int i = _braceCandidates.Count - 1; i >= 0; i--)
        {
            if (!enemy.Alive) return;
            var member = _braceCandidates[i];
            if (!member.Alive) continue;

            var weapon = member.EquippedWeapon;
            var brace = weapon?.GetAbility(AbilityType.Brace);
            if (brace == null) continue;

            _braceUsesThisTurn.TryGetValue(member, out int used);
            if (used >= brace.Value) continue;
            if (!EnemyAi.CharCanHit(member, enemy, weapon, _grid)) continue;

            _braceCandidates.RemoveAt(i);
            _braceUsesThisTurn[member] = used + 1;
            BraceTriggered?.Invoke(member);
            ResolveAttackOnEnemy(weapon!, enemy);
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
        ResolveAttackOnCharacter(enemy, target);
        if (Phase == TurnPhase.GameOver) return;

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

                // Someone may be standing on the remnant: wake up on the
                // nearest free tile instead of inside them.
                var (r, c) = FindNearestFreeTile(enemy);
                enemy.X = c * GameConstants.Tile + GameConstants.Tile / 2f;
                enemy.Y = r * GameConstants.Tile + GameConstants.Tile / 2f;

                EnemyResurrected?.Invoke(enemy);
            }
        }

        _braceUsesThisTurn.Clear();
        _enemyBraceUsesThisTurn.Clear();

        // Snapshot who already stands inside each live enemy's reach (after
        // resurrections placed everyone): those pairs never brace this turn.
        _inEnemyReach.Clear();
        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive) continue;
            foreach (var member in _party)
                if (member.Alive && EnemyAi.CanHit(enemy, member, enemy.Weapon, _grid))
                    _inEnemyReach.Add((enemy, member));
        }

        foreach (var c in _party)
        {
            if (c.Alive) c.StartTurn();
        }

        Phase = TurnPhase.Player;
        PlayerTurnStarted?.Invoke();
    }

    // ── Shared attack plumbing ───────────────────────────────────────────────

    private void ResolveAttackOnCharacter(EnemyState enemy, PartyMemberState target)
    {
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
            }
        }
    }

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
