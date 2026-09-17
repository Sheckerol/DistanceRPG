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
    public event Action<PartyMemberState, StatusEffect>? CharacterBuffed;   // staff cast landed
    public event Action<PartyMemberState, int>? CharacterHealed;            // end-of-turn regen tick (HP restored)
    public event Action<EnemyState, StatusEffect>? EnemyBuffed;             // enemy healer's cast landed on an ally
    public event Action<EnemyState, int>? EnemyHealed;                      // end-of-enemy-turn regen tick
    public event Action<EnemyState>? EnemyFleeing;                          // lone healer turning tail
    public event Action? GameOver;

    // ── Enemy-turn working state ─────────────────────────────────────────────
    private int _enemyIdx;            // index of the enemy currently acting
    private float _timer;
    private float _enemyBudget;
    private List<(float X, float Y)> _waypoints = new();
    private int _waypointIdx;
    private bool _enemyTurnPending;   // banner is up; enemy turn starts when it ends
    private bool _nextEnemyPending;   // pause before the next enemy acts (or control returns)

    // ── Threat zones ─────────────────────────────────────────────────────────
    // Spear-wielders on both sides threaten a zone: whoever walks *into* their
    // reach eats a free attack, up to the weapon's Brace value uses per turn.
    // Standing inside when the zone arms never triggers — walking in is what
    // costs. Both directions run on the same ThreatZone (entry-edge pair set +
    // per-bracer use pool) and differ only in when they arm and whether a pair
    // re-arms on leaving reach:
    //  - Party braces arm per enemy walk (ArmPartyBraces): every living member
    //    holding a Brace weapon watches the acting enemy, those already holding
    //    it in reach marked inside. A member stabs a given walk at most once —
    //    pairs are never released mid-walk. (The prototype only braced the
    //    enemy's own chosen target — with formations and many enemies, the
    //    nearest member is always the target and back-row spears would never
    //    fire.)
    //  - Enemy braces arm once per player turn (StartPlayerTurn, after
    //    resurrections) for every live enemy, seen or not. A member whose
    //    movement carries them into a *seen* spear enemy's reach eats a free
    //    poke; leaving reach releases the pair, so a later re-entry costs again.
    private readonly ThreatZone _partyBraces = new();
    private readonly ThreatZone _enemyBraces = new();
    private readonly List<PartyMemberState> _braceWatchers = new(); // party bracers armed against the current walk

    // ── The event table ──────────────────────────────────────────────────────
    // Every behaviour hangs off this one dispatcher (§1.7): the compiled
    // chains are registered once, and this system registers the appliers —
    // the places the settled results are written — because they raise its
    // typed events. Raises happen at the phase boundaries and when an attack
    // lands; anything a handler or applier triggers is queued, never raised.
    private readonly EventTable _events = new();

    /// <summary>The table, for tests that probe the raise points and the queued follow-ups.</summary>
    internal EventTable Events => _events;

    // The typed hit feed of each actor on the roster, keyed by the actor
    // itself. The constructor's typed lists are the one place an actor's kind
    // is known, so the DamageTaken applier looks its target up here rather
    // than asking what kind of actor it struck; a third kind of actor arrives
    // as a third typed list and a third feed, and the applier is untouched.
    private readonly Dictionary<ActorState, Action<AttackResolution>> _hitFeeds = new(ReferenceEqualityComparer.Instance);

    private EnemyState ActingEnemy => _enemies[_enemyIdx];

    public TurnSystem(int[,] grid, IReadOnlyList<PartyMemberState> party,
        IReadOnlyList<EnemyState> enemies, Func<int>? rollD20 = null)
    {
        _grid = grid;
        _party = party;
        _enemies = enemies;
        _rollD20 = rollD20 ?? (() => Random.Shared.Next(1, 21));

        Behaviours.RegisterAll(_events);
        _events.Applies<DamagePayload>(GameEvent.DamageTaken, ApplyDamage);

        // The roster is fixed here, and with it which typed feed each actor's hits reach.
        foreach (var member in party)
            _hitFeeds[member] = resolution => CharacterHit?.Invoke(member, resolution);
        foreach (var enemy in enemies)
            _hitFeeds[enemy] = resolution => EnemyHit?.Invoke(enemy, resolution);
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

            if (!EnemyAi.CanHit(enemy, mover, enemy.Weapon, _grid))
            {
                _enemyBraces.Leave(enemy, mover); // leaving reach re-arms the pair
                continue;
            }
            if (!_enemyBraces.Enter(enemy, mover)) continue; // was already in reach

            var brace = enemy.Weapon.GetAbility(AbilityType.Brace);
            if (brace == null) continue;
            if (!_seenThisTurn.Contains(enemy)) continue; // no ambushes from the fog
            if (_enemyBraces.UsesThisTurn(enemy) >= brace.Value) continue;

            _enemyBraces.Spend(enemy);
            EnemyBraceTriggered?.Invoke(enemy);
            ResolveAttackOnCharacter(enemy, enemy.Weapon, mover);
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
        if (w == null || w.IsCaster || c.DistLeft < w.Cost) return false; // a staff heals allies, it can't strike
        return EnemyAi.CanHit(c, enemy, w, _grid);
    }

    /// <summary>Attack an enemy with the given member. Returns false if not allowed.</summary>
    public bool TryAttack(PartyMemberState c, EnemyState enemy)
    {
        if (!CanAttack(c, enemy)) return false;
        var w = c.EquippedWeapon!;

        c.DistLeft = MathF.Max(0f, c.DistLeft - w.Cost);
        ResolveAttackOnEnemy(c, w, enemy);
        return true;
    }

    /// <summary>
    /// Can the caster cast their equipped staff on <paramref name="ally"/>
    /// (self allowed)? Needs a caster weapon, enough movement and mana, and the
    /// ally within range and line of sight.
    /// </summary>
    public bool CanCast(PartyMemberState caster, PartyMemberState ally)
    {
        if (Phase != TurnPhase.Player || !caster.Alive || !ally.Alive) return false;
        var w = caster.EquippedWeapon;
        if (w == null || !w.IsCaster) return false;
        if (caster.DistLeft < w.Cost || caster.Mana < w.ManaCost) return false;
        return EnemyAi.CanHit(caster, ally, w, _grid);
    }

    /// <summary>
    /// Cast the equipped staff on an ally: spend movement and mana, then stack
    /// its heal-over-time buff. Returns false if the cast is not allowed.
    /// </summary>
    public bool TryCast(PartyMemberState caster, PartyMemberState ally)
    {
        if (!CanCast(caster, ally)) return false;
        var w = caster.EquippedWeapon!;
        var heal = w.GetAbility(AbilityType.HealCast)!;

        caster.DistLeft = MathF.Max(0f, caster.DistLeft - w.Cost);
        caster.Mana -= w.ManaCost;
        var effect = ally.ApplyStatusEffect(StatusEffectType.Regeneration, heal.Value);
        CharacterBuffed?.Invoke(ally, effect);
        return true;
    }

    /// <summary>End the player turn: bank leftover movement, then run the enemy.</summary>
    public void EndTurn()
    {
        if (Phase != TurnPhase.Player) return;

        float totalSaved = 0f;
        foreach (var c in _party)
        {
            totalSaved += c.EndTurnSaveMovement();
            c.RegenManaFromUnusedMovement();
            // The member's turn ends here; the status tick beside it becomes
            // that event's handlers once the status table lands.
            _events.Raise(GameEvent.TurnEnd, new TurnPayload(TurnCount, Side.Party), c, c);
            TickStatusEffects(c, (a, h) => CharacterHealed?.Invoke(a, h));
        }

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

        // The enemy phase opens for the whole roster — acting, passive or dead —
        // so every actor sees TurnStart, TurnEnd and RoundEnd once per round,
        // in that order; a handler that only means the living checks Alive.
        foreach (var enemy in _enemies)
            _events.Raise(GameEvent.TurnStart, new TurnPayload(TurnCount, Side.Enemy), enemy, enemy);

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
            if (enemy.TurnsSinceSeen >= 2 && !HasPassiveAction(enemy)) continue;

            StartEnemyAction(enemy);
            return;
        }

        StartPlayerTurn();
    }

    private void StartEnemyAction(EnemyState enemy)
    {
        _enemyBudget = GameConstants.EnemyMove;

        if (enemy.IsHealer)
        {
            StartHealerAction(enemy);
            return;
        }

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

        ArmPartyBraces(enemy);
        var blocked = OccupiedTilesExcept(enemy);

        var (waypoints, remaining) = EnemyAi.PlanMove(enemy, target, _grid, _enemyBudget, blocked);
        _enemyBudget = remaining;

        if (waypoints.Count == 0)
        {
            // No movement means no reach was crossed — straight to attacks.
            BeginAttackPhase();
            return;
        }

        BeginWalk(waypoints);
    }

    /// <summary>
    /// A staff healer's turn: while it has a living ally it moves to mend the
    /// most-wounded one (then casts in the attack phase); alone, it flees the
    /// party. Unseen healers stay put but still cast on any ally already in reach.
    /// </summary>
    private void StartHealerAction(EnemyState healer)
    {
        ArmPartyBraces(healer);
        var blocked = OccupiedTilesExcept(healer);

        if (!EnemyAi.HasLivingAlly(healer, _enemies))
        {
            EnemyFleeing?.Invoke(healer);
            var (flee, fleeLeft) = EnemyAi.PlanFlee(healer, _party, _grid, _enemyBudget, blocked);
            _enemyBudget = fleeLeft;
            if (flee.Count == 0) { BeginAttackPhase(); return; } // cornered — beats will no-op
            BeginWalk(flee);
            return;
        }

        var ally = EnemyAi.SelectHealTarget(healer, _enemies);
        // No wounded ally, or one already in reach, or standing pat while unseen:
        // skip straight to the cast phase (which no-ops if nothing's castable).
        if (ally == null || healer.TurnsSinceSeen >= 2 || EnemyAi.CanHit(healer, ally, healer.Weapon, _grid))
        {
            BeginAttackPhase();
            return;
        }

        var (waypoints, remaining) = EnemyAi.PlanApproach(
            healer, ally.X, ally.Y, ally.Radius, healer.Weapon.Range, _grid, _enemyBudget, blocked);
        _enemyBudget = remaining;
        if (waypoints.Count == 0) { BeginAttackPhase(); return; }
        BeginWalk(waypoints);
    }

    // Before an enemy walks, the party's spear-wielders arm against it. Anyone
    // already holding it in reach is marked inside — walking *into* reach is
    // what triggers a brace, standing there is not.
    private void ArmPartyBraces(EnemyState enemy)
    {
        _partyBraces.Clear();
        _braceWatchers.Clear();
        foreach (var member in _party)
        {
            if (!member.Alive) continue;
            var w = member.EquippedWeapon;
            if (w == null || w.GetAbility(AbilityType.Brace) == null) continue;

            _braceWatchers.Add(member);
            if (EnemyAi.CanHit(member, enemy, w, _grid))
                _partyBraces.MarkInside(member, enemy);
        }
    }

    // Every other living actor blocks this enemy's path — enemies act
    // sequentially, so each planner queues behind the ones already in position
    // instead of piling onto the same tile.
    private HashSet<(int R, int C)> OccupiedTilesExcept(EnemyState enemy)
    {
        var blocked = new HashSet<(int R, int C)>();
        foreach (var other in _enemies)
            if (other != enemy && other.Alive)
                blocked.Add(TileOf(other.X, other.Y));
        foreach (var member in _party)
            if (member.Alive)
                blocked.Add(TileOf(member.X, member.Y));
        return blocked;
    }

    private void BeginWalk(List<(float X, float Y)> waypoints)
    {
        _waypoints = waypoints;
        _waypointIdx = 0;
        Phase = TurnPhase.EnemyMoving;
    }

    private bool AnyHittableBy(EnemyState enemy)
        => _party.Any(c => c.Alive && EnemyAi.CanHit(enemy, c, enemy.Weapon, _grid));

    /// <summary>
    /// Does this enemy have a reason to act while passive (unseen 2+ turns)? An
    /// attacker needs someone in reach; a healer needs a wounded ally to mend.
    /// A lone healer only flees once it's actually seen — there's no point
    /// running through the fog from a party that can't see it — so while unseen
    /// it stays put like any other idle dummy.
    /// </summary>
    private bool HasPassiveAction(EnemyState enemy)
        => enemy.IsHealer
            ? EnemyAi.HasLivingAlly(enemy, _enemies) && EnemyAi.SelectHealTarget(enemy, _enemies) != null
            : AnyHittableBy(enemy);

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
    /// Fire a free retaliation from each armed member whose reach contains the
    /// enemy right now. A member stabs a given walk at most once (the pair is
    /// marked inside for the rest of the walk), and the weapon's Brace value
    /// caps their uses per turn across all walks.
    /// </summary>
    private void TryBracesAgainst(EnemyState enemy)
    {
        for (int i = _braceWatchers.Count - 1; i >= 0; i--)
        {
            if (!enemy.Alive) return;
            var member = _braceWatchers[i];
            if (!member.Alive) continue;

            var weapon = member.EquippedWeapon;
            if (weapon == null) continue;
            var brace = weapon.GetAbility(AbilityType.Brace);
            if (brace == null) continue;

            if (_partyBraces.UsesThisTurn(member) >= brace.Value) continue;
            if (!EnemyAi.CanHit(member, enemy, weapon, _grid)) continue;
            if (!_partyBraces.Enter(member, enemy)) continue; // stood inside when the walk began, or already stabbed this walk

            _partyBraces.Spend(member);
            BraceTriggered?.Invoke(member);
            ResolveAttackOnEnemy(member, weapon, enemy);
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

        if (enemy.IsHealer)
        {
            TryEnemyHealBeat(enemy);
            return;
        }

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
        ResolveAttackOnCharacter(enemy, enemy.Weapon, target);
        if (Phase == TurnPhase.GameOver) return;

        _timer = AttackBeatSeconds;
    }

    /// <summary>
    /// A healer's attack-phase beat: cast Regeneration on the most-wounded ally
    /// in reach, spending scaled budget, one cast per beat until dry or nobody
    /// needs mending. A fleeing/idle healer simply finds no target and passes.
    /// </summary>
    private void TryEnemyHealBeat(EnemyState healer)
    {
        float scaledCost = GameConstants.EnemyMove / GameConstants.MaxDistance * healer.Weapon.Cost;
        var ally = EnemyAi.SelectHealTarget(healer, _enemies);

        if (!healer.Alive || _enemyBudget < scaledCost
            || ally == null || !EnemyAi.CanHit(healer, ally, healer.Weapon, _grid))
        {
            _nextEnemyPending = true;
            _timer = AttackBeatSeconds;
            return;
        }

        _enemyBudget -= scaledCost;
        var heal = healer.Weapon.GetAbility(AbilityType.HealCast)!;
        var effect = ally.ApplyStatusEffect(StatusEffectType.Regeneration, heal.Value);
        EnemyBuffed?.Invoke(ally, effect);

        _timer = AttackBeatSeconds;
    }

    private void StartPlayerTurn()
    {
        // End of the enemy turn: each enemy's turn ends (regen ticks on the
        // allies a healer mended; the dead shed theirs), then the round — one
        // player phase and one enemy phase — closes for every actor. The whole
        // roster, whatever its state; all of it under the turn that is ending,
        // and before resurrections (which restore full HP) are considered.
        foreach (var enemy in _enemies)
        {
            _events.Raise(GameEvent.TurnEnd, new TurnPayload(TurnCount, Side.Enemy), enemy, enemy);
            TickStatusEffects(enemy, (a, h) => EnemyHealed?.Invoke(a, h));
        }
        foreach (var member in _party)
            _events.Raise(GameEvent.RoundEnd, new TurnPayload(TurnCount, Side.Party), member, member);
        foreach (var enemy in _enemies)
            _events.Raise(GameEvent.RoundEnd, new TurnPayload(TurnCount, Side.Enemy), enemy, enemy);

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

        _partyBraces.ResetUses();
        _enemyBraces.ResetUses();

        // Snapshot who already stands inside each live enemy's reach (after
        // resurrections placed everyone): those pairs never brace this turn.
        _enemyBraces.Clear();
        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive) continue;
            foreach (var member in _party)
                if (member.Alive && EnemyAi.CanHit(enemy, member, enemy.Weapon, _grid))
                    _enemyBraces.MarkInside(enemy, member);
        }

        // The player phase opens for every member, the fallen included — the
        // boundary events are the roster's; only the budget refill is the living's.
        foreach (var c in _party)
        {
            if (c.Alive)
                c.StartTurn();
            _events.Raise(GameEvent.TurnStart, new TurnPayload(TurnCount, Side.Party), c, c);
        }

        Phase = TurnPhase.Player;
        PlayerTurnStarted?.Invoke();
    }

    /// <summary>
    /// Status tick for any actor, run at the end of that side's turn: each
    /// Regeneration effect heals HP equal to its level (capped at missing HP),
    /// then loses a level and is dropped at zero. A dead actor simply sheds every
    /// effect — regen can't resurrect. The caller lends its side's healed event,
    /// so CharacterHealed and EnemyHealed stay separately typed for the HUD.
    /// </summary>
    private static void TickStatusEffects<TActor>(TActor actor, Action<TActor, int>? onHealed)
        where TActor : ActorState
    {
        if (!actor.Alive)
        {
            actor.StatusEffects.Clear();
            return;
        }

        for (int i = actor.StatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = actor.StatusEffects[i];
            if (effect.Type == StatusEffectType.Regeneration)
            {
                int healed = Math.Min(effect.Level, actor.MaxHp - actor.Hp);
                if (healed > 0)
                {
                    actor.Hp += healed;
                    onHealed?.Invoke(actor, healed);
                }
            }

            if (--effect.Level <= 0)
                actor.StatusEffects.RemoveAt(i);
        }
    }

    // ── Shared attack plumbing ───────────────────────────────────────────────

    /// <summary>
    /// The one place an attack lands, whoever swings and whoever is hit: the
    /// attacker's natural roll and the surface distance seed a DamagePayload,
    /// DamageTaken runs the §1.6 chain on this system's table, and the applier
    /// (<see cref="ApplyDamage"/>) writes the settled result once — HP, the
    /// side's typed hit event, the queued follow-ups. What a death *means*
    /// (movement lost, defeat turn, party wipe) stays the typed wrappers'
    /// business, which runs after the raise returns. Returns true when the
    /// target died.
    /// </summary>
    private bool ResolveAttackOn(ActorState attacker, Weapon attackerWeapon, ActorState target)
    {
        CombatRules.Resolve(_events, attacker, target, attackerWeapon,
            CombatRules.SurfaceDistanceUnits(attacker, target), _rollD20);
        return !target.Alive;
    }

    /// <summary>
    /// The DamageTaken applier — the single world-write for a hit. Takes
    /// <see cref="DamagePayload.Taken"/> off the target's HP, lands the settled
    /// status applications on both sides, hands the projected resolution to the
    /// target's typed hit feed (CharacterHit or EnemyHit, fixed when the roster
    /// was typed at construction — never by asking the target its kind), and
    /// queues the follow-ups for the table to drain after it returns, in this
    /// order: Killed on a death, DamageDealt always, Crit on a crit. The order
    /// is a ruling of the decomposition (1b rule 5), pinned by
    /// DamagePipelineTests, not an accident of this method. Death consequences
    /// belong to the typed wrappers.
    /// </summary>
    private void ApplyDamage(DamagePayload settled, ActorState attacker, ActorState target, EventTable table)
    {
        target.Hp = Math.Max(0, target.Hp - settled.Taken);
        if (!settled.ApplyToDefender.IsDefaultOrEmpty)
            foreach (var status in settled.ApplyToDefender)
                target.ApplyStatusEffect(status.Type, status.Levels);
        if (!settled.ApplyToAttacker.IsDefaultOrEmpty)
            foreach (var status in settled.ApplyToAttacker)
                attacker.ApplyStatusEffect(status.Type, status.Levels);

        if (!_hitFeeds.TryGetValue(target, out var hitFeed))
            throw new InvalidOperationException("Hit an actor that is not on this turn system's roster.");
        hitFeed(CombatRules.Project(settled));

        if (target.Hp <= 0)
        {
            target.Alive = false;
            table.Enqueue(GameEvent.Killed, new KillPayload(settled.Weapon, settled.Dealt, settled.Taken), attacker, target);
        }
        table.Enqueue(GameEvent.DamageDealt, settled, attacker, target);
        if (settled.IsCrit)
            table.Enqueue(GameEvent.Crit, new CritPayload(settled.Weapon, settled.Roll), attacker, target);
    }

    private void ResolveAttackOnCharacter(ActorState attacker, Weapon attackerWeapon, PartyMemberState target)
    {
        if (!ResolveAttackOn(attacker, attackerWeapon, target)) return;

        target.SavedMovement = 0;
        CharacterDied?.Invoke(target);

        if (_party.All(c => !c.Alive))
        {
            Phase = TurnPhase.GameOver;
            _timer = GameOverPauseSeconds;
            GameOver?.Invoke();
        }
    }

    private void ResolveAttackOnEnemy(ActorState attacker, Weapon attackerWeapon, EnemyState target)
    {
        if (!ResolveAttackOn(attacker, attackerWeapon, target)) return;

        target.DefeatedAtTurn = TurnCount;
        EnemyDefeated?.Invoke(target);
    }

    private static float Dist2(EnemyState enemy, PartyMemberState c)
    {
        float dx = enemy.X - c.X;
        float dy = enemy.Y - c.Y;
        return dx * dx + dy * dy;
    }
}
