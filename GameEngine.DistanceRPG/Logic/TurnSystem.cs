using System.Collections.Immutable;

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
    public event Action<PartyMemberState, StatusEffect>? CharacterBuffed;   // a cast landed a status on the member: an ally's buff or an enemy's debuff
    public event Action<PartyMemberState, int>? CharacterHealed;            // end-of-turn regen tick (HP restored)
    public event Action<EnemyState, StatusEffect>? EnemyBuffed;             // a cast landed a status on the enemy: its healer's mend or the party's debuff
    public event Action<EnemyState, int>? EnemyHealed;                      // end-of-enemy-turn regen tick
    public event Action<EnemyState>? EnemyFleeing;                          // lone healer turning tail
    public event Action<ActorState, StatusTick>? ActorStatusTicked;         // a damage-over-time tick took HP (healing ticks arrive as CharacterHealed/EnemyHealed)
    public event Action<ActorState, StatusEffect, ActorState>? ActorStatusApplied;   // a cast landed a status on the target (first) from the source (last), either side
    public event Action<ActorState, int>? ActorDisplaced;                   // shoved or dragged this many tiles, one at a time through the move path
    public event Action<ActorState>? OpportunistTriggered;                  // a free attack on a target that chose to leave reach
    public event Action<ActorState>? OverwatchTriggered;                    // a held shot fired at a target entering reach
    public event Action<ActorState>? RiposteTriggered;                      // a successful block answered with a counter-swing
    public event Action? GameOver;

    // ── Enemy-turn working state ─────────────────────────────────────────────
    private int _enemyIdx;            // index of the enemy currently acting
    private float _timer;
    private float _enemyBudget;
    private List<(float X, float Y)> _waypoints = new();
    private int _waypointIdx;
    private bool _enemyTurnPending;   // banner is up; enemy turn starts when it ends
    private bool _nextEnemyPending;   // pause before the next enemy acts (or control returns)

    // ── Threat zones and reaction pools ──────────────────────────────────────
    // A reactor's reach is a threat zone facing the other side: whoever crosses
    // its edge is answered for free — a spear's brace or a held shot on the way
    // in, an axe's opportunity attack on a chosen way out — up to the weapon's
    // value in uses per turn. One ThreatZone per side holds the (reactor,
    // mover) pairs currently inside a reach and each reactor's uses this turn.
    // The pair sets are kept true continuously: snapshotted when the roster is
    // fixed, at each player turn's start (after resurrections place everyone)
    // and at each enemy phase's start (whatever is equipped when the enemy
    // phase begins is what reacts), refreshed silently for the reactor
    // whenever *it* moves or changes weapon (a reach can change without a
    // step: NotifyWeaponChanged), and
    // crossed — with the reactions raised — whenever the *mover* does, whether
    // it walked or was shoved. Standing inside a zone when it arms never
    // triggers; walking or being moved in is what costs, and leaving releases
    // the pair so a later re-entry counts again. Both sides run the same code:
    // the only asymmetry is that an enemy's zone is armed only once it has been
    // seen this turn (no ambushes from the fog). Riposte answers being hit
    // rather than being approached, so its uses live in their own pool.
    private readonly ThreatZone _partyZone = new();     // party reactors watching enemy movers
    private readonly ThreatZone _enemyZone = new();     // enemy reactors watching party movers
    private readonly ThreatZone[] _zones;               // by the reactor's Side
    private readonly IReadOnlyList<ActorState>[] _rosters;   // by Side
    private readonly Dictionary<ActorState, Side> _sides = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ActorState, Func<bool>> _mayReact = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ActorState, Action> _braceFeeds = new(ReferenceEqualityComparer.Instance);
    // What firing a reaction of each source does besides queueing the free
    // attack: the typed announcement, and for Overwatch letting one held shot
    // go. A new reaction source adds its entry here; the applier is untouched.
    private readonly Dictionary<ModifierType, Action<ActorState>> _onReactionFired;
    private readonly ReactionPool _ripostes = new();

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

    // The same for what a heal restored and for a death: the appliers write
    // the number and hand the side's typed event, or the side's death
    // consequences, to the feed the roster fixed for that actor.
    private readonly Dictionary<ActorState, Action<int>> _healFeeds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ActorState, Action> _deathFeeds = new(ReferenceEqualityComparer.Instance);

    // And for a cast's status landing: the Cast applier hands the entry now on
    // the target to the side's typed event (CharacterBuffed, EnemyBuffed) the same way.
    private readonly Dictionary<ActorState, Action<StatusEffect>> _statusFeeds = new(ReferenceEqualityComparer.Instance);

    // And for movement spent through the table (a swap): the applier takes the
    // settled amount off the budget the roster fixed for the actor — a party
    // member's DistLeft, the acting enemy's budget.
    private readonly Dictionary<ActorState, Action<int>> _movementFeeds = new(ReferenceEqualityComparer.Instance);

    // ── Turn economy ─────────────────────────────────────────────────────────
    // Attacks each actor has chosen to make since its side's turn began — a
    // brace, a held shot, a counter or an opportunity attack is a reaction and
    // counts for nothing — read by the Charges cap: an actor with Charges
    // stacks may choose its Charges value of attacks a turn (one throw plus one
    // per stack) and no more, whatever movement is left; without stacks only
    // the budget binds. Reset when each side's turn starts.
    private readonly Dictionary<ActorState, int> _attacksThisTurn = new(ReferenceEqualityComparer.Instance);

    // Actors that died inside the raise in progress — a hit, a status tick, a
    // queued reaction or counter deep in a cascade. Their death consequences
    // run once the outermost raise returns, the queued Killed chain drained,
    // so a Killed behaviour sees the same world whichever way the actor died.
    private readonly List<ActorState> _heldDeaths = new();

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
        _events.Applies<TurnPayload>(GameEvent.TurnEnd, ApplyStatusTicks);
        _events.Applies<TurnPayload>(GameEvent.RoundEnd, ApplyStatusTicks);
        _events.Applies<HealPayload>(GameEvent.HealingReceived, ApplyHealing);
        _events.Applies<HealPayload>(GameEvent.HealingAboveFull, ApplyHealingAboveFull);
        _events.Applies<ThreatPayload>(GameEvent.ThreatZoneEntered, ApplyReactions);
        _events.Applies<MovementPayload>(GameEvent.MovementSpent, ApplyMovementSpent);
        _events.Applies<CastPayload>(GameEvent.Cast, ApplyCast);
        _events.Applies<ManaPayload>(GameEvent.ManaSpent, ApplyManaSpent);

        // The roster is fixed here, and with it which typed feed each actor's
        // hits, heals, death, brace and movement reach, which side it fights
        // on, and whether its zone may fire right now.
        foreach (var member in party)
        {
            _hitFeeds[member] = resolution => CharacterHit?.Invoke(member, resolution);
            _healFeeds[member] = amount => CharacterHealed?.Invoke(member, amount);
            _deathFeeds[member] = () => OnCharacterDied(member);
            _statusFeeds[member] = effect => CharacterBuffed?.Invoke(member, effect);
            _braceFeeds[member] = () => BraceTriggered?.Invoke(member);
            _movementFeeds[member] = spent => member.DistLeft = MathF.Max(0f, member.DistLeft - spent);
            _sides[member] = Side.Party;
            _mayReact[member] = static () => true;                       // the party's zones are always armed
        }
        foreach (var enemy in enemies)
        {
            _hitFeeds[enemy] = resolution => EnemyHit?.Invoke(enemy, resolution);
            _healFeeds[enemy] = amount => EnemyHealed?.Invoke(enemy, amount);
            _deathFeeds[enemy] = () => OnEnemyDefeated(enemy);
            _statusFeeds[enemy] = effect => EnemyBuffed?.Invoke(enemy, effect);
            _braceFeeds[enemy] = () => EnemyBraceTriggered?.Invoke(enemy);
            _movementFeeds[enemy] = spent => _enemyBudget = MathF.Max(0f, _enemyBudget - spent);   // only the acting enemy spends
            _sides[enemy] = Side.Enemy;
            _mayReact[enemy] = () => _seenThisTurn.Contains(enemy);      // no ambushes from the fog
        }
        _rosters = [party.ToArray<ActorState>(), enemies.ToArray<ActorState>()];
        _zones = [_partyZone, _enemyZone];
        _onReactionFired = new Dictionary<ModifierType, Action<ActorState>>
        {
            [ModifierType.Brace] = reactor => _braceFeeds[reactor](),
            [ModifierType.Opportunist] = reactor => OpportunistTriggered?.Invoke(reactor),
            [ModifierType.Overwatch] = reactor =>
            {
                reactor.HeldShots = Math.Max(0, reactor.HeldShots - 1);   // the shot held is the shot fired
                OverwatchTriggered?.Invoke(reactor);
            },
        };

        SnapshotZones();
    }

    /// <summary>
    /// Scene calls this after a character's position changes during the
    /// player phase: a voluntary walk on the one move path
    /// (<see cref="NotifyActorMoved"/>).
    /// </summary>
    public void NotifyCharacterMoved(PartyMemberState mover) => NotifyActorMoved(mover, MoveKind.Voluntary);

    /// <summary>
    /// The one move path (§1.2, §1.7): call this after an actor's position
    /// changes, whether it walked (<see cref="MoveKind.Voluntary"/>) or was
    /// shoved (<see cref="MoveKind.Forced"/>). The actor's own threat zone
    /// follows it silently; then every zone on the other side whose edge the
    /// move crossed raises <see cref="GameEvent.ThreatZoneEntered"/> — the
    /// exit edge first, then the entry edge — and the reactions its handlers
    /// settle are spent and fired by the applier: a brace or a held shot on
    /// entry however the move came about, an opportunity attack on a chosen
    /// exit only. Standing inside a zone when it armed never triggers —
    /// walking in is what costs — and leaving re-arms the pair. A party
    /// member's zone is always armed; an enemy's only once it has been seen
    /// this turn. Inside a running cascade the crossings queue behind it (a
    /// shove's braces fire once the shove has finished); otherwise they
    /// resolve, and any death they caused settles, before this returns.
    /// </summary>
    public void NotifyActorMoved(ActorState mover, MoveKind kind)
    {
        ArgumentNullException.ThrowIfNull(mover);
        if (Phase == TurnPhase.GameOver || !mover.Alive) return;
        if (!_sides.ContainsKey(mover))
            throw new InvalidOperationException("Moved an actor that is not on this turn system's roster.");

        RefreshZoneOf(mover);
        TryReactionsAgainst(mover, ZoneEdge.Exit, kind);
        TryReactionsAgainst(mover, ZoneEdge.Enter, kind);
        if (!_events.Running)
            RunHeldDeaths();
    }

    /// <summary>
    /// Scene calls this after an actor's equipped weapon changes (an inventory
    /// swap): a reach can change without a step, so the actor's own zone is
    /// re-read from where everyone stands, silently — a mover already inside
    /// the new reach is marked so and never fires until it leaves and comes
    /// back, one now outside a shorter reach is released so a real entry
    /// counts again. A held shot lapses with the weapon that held it; the
    /// movement it cost is not refunded. Nothing fires: nobody moved.
    /// </summary>
    public void NotifyWeaponChanged(ActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!_sides.ContainsKey(actor))
            throw new InvalidOperationException("Changed the weapon of an actor that is not on this turn system's roster.");

        actor.HeldShots = 0;
        if (actor.Alive)
            RefreshZoneOf(actor);
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

    /// <summary>
    /// Can the member attack the enemy? A martial weapon in hand, the swing's
    /// resolved movement to spare, an attack left under Charges, and the
    /// enemy in reach and sight.
    /// </summary>
    public bool CanAttack(PartyMemberState c, EnemyState enemy)
    {
        if (Phase != TurnPhase.Player || !enemy.Alive || !c.Alive) return false;
        var w = c.EquippedWeapon;
        if (w == null || w.IsCaster || c.DistLeft < w.ResolvedCost) return false; // a caster casts, it can't strike
        if (!HasAttackLeft(c)) return false;                                      // Charges is a cap: the throws are spent whatever movement is left
        return EnemyAi.CanHit(c, enemy, w, _grid);
    }

    /// <summary>
    /// Attack an enemy with the given member: the swing's movement is paid
    /// once, however many targets a cleave fans it out to. Returns false if
    /// not allowed.
    /// </summary>
    public bool TryAttack(PartyMemberState c, EnemyState enemy)
    {
        if (!CanAttack(c, enemy)) return false;
        var w = c.EquippedWeapon!;

        c.DistLeft = MathF.Max(0f, c.DistLeft - w.ResolvedCost);
        CountAttack(c);
        AttackWith(c, enemy);
        return true;
    }

    /// <summary>Attacks <paramref name="actor"/> has chosen to make since its side's turn began; reactions count for nothing.</summary>
    public int AttacksThisTurn(ActorState actor)
        => _attacksThisTurn.TryGetValue(actor, out int attacks) ? attacks : 0;

    /// <summary>
    /// The attacks <paramref name="actor"/> may choose to make a turn under
    /// <see cref="ModifierType.Charges"/> — its Charges value, one throw plus
    /// one per stack — or null when it holds no Charges stacks and only the
    /// movement budget binds. A cap, never a grant: granted from zero it would
    /// make a bow worse, which is why Charges is forged only (§1.1).
    /// </summary>
    public int? AttacksPerTurn(ActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.Stacks(ModifierType.Charges) > 0 ? actor.Value(ModifierType.Charges) : null;
    }

    /// <summary>Whether <paramref name="actor"/> may choose another attack this turn: under its Charges cap, or uncapped.</summary>
    public bool HasAttackLeft(ActorState actor)
        => AttacksPerTurn(actor) is not int cap || AttacksThisTurn(actor) < cap;

    private void CountAttack(ActorState actor) => _attacksThisTurn[actor] = AttacksThisTurn(actor) + 1;

    /// <summary>
    /// What a weapon swap costs right now: <see cref="Tuning.WeaponSwapCost"/>
    /// movement once a live enemy has been seen this turn — the combat-footing
    /// signal that also grants marching — and nothing out of combat, so
    /// loadout management between fights stays free while a mid-fight swap is
    /// a real decision: a bow user caught at knife range pays the swap and
    /// then the swing (§1.2).
    /// </summary>
    public int SwapCost => AnyLiveEnemySeenThisTurn ? GameContent.Current.Tuning.WeaponSwapCost : 0;

    /// <summary>
    /// Can the member swap the weapon in <paramref name="slot"/> for the
    /// equipped one? The slot must hold a weapon — a swap equips something —
    /// and the member must have the swap's movement to spare.
    /// </summary>
    public bool CanSwap(PartyMemberState c, int slot)
    {
        if (Phase != TurnPhase.Player || !c.Alive) return false;
        if (slot <= 0 || slot >= c.Inventory.Length || c.Inventory[slot] == null) return false;
        return c.DistLeft >= SwapCost;
    }

    /// <summary>
    /// Swap the weapon in <paramref name="slot"/> with the equipped one (slot
    /// 0): spend the swap's movement through <see cref="GameEvent.MovementSpent"/>,
    /// raised for every swap so the event is the one record of them all (a
    /// free swap settles at zero and the applier writes nothing), exchange
    /// the slots, and re-arm the member's threat zone for the new reach — a
    /// held shot lapses with the weapon that held it. Returns false if not
    /// allowed.
    /// </summary>
    public bool TrySwap(PartyMemberState c, int slot)
    {
        if (!CanSwap(c, slot)) return false;

        int cost = SwapCost;
        _events.Raise(GameEvent.MovementSpent, new MovementPayload(cost, cost, "swap"), c, c);
        (c.Inventory[0], c.Inventory[slot]) = (c.Inventory[slot], c.Inventory[0]);
        NotifyWeaponChanged(c);
        return true;
    }

    /// <summary>
    /// Can the caster cast their equipped staff on <paramref name="target"/>?
    /// Needs a staff whose innate effect lands on the target's side (a buff on
    /// an ally or on the caster itself, a debuff on an enemy), enough movement
    /// and mana at the resolved costs, and the target within range and line
    /// of sight. The innate's trigger is no gate: unaffordable, it scales or
    /// does not fire (<see cref="EnchantmentBehaviours"/>).
    /// </summary>
    public bool CanCast(PartyMemberState caster, ActorState target)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(target);
        if (Phase != TurnPhase.Player || !CanCastOn(caster, target)) return false;
        var w = caster.EquippedWeapon!;   // CanCastOn gated on it
        return caster.DistLeft >= w.ResolvedCost && caster.Mana >= w.ResolvedManaCost;
    }

    /// <summary>
    /// Cast the equipped staff on <paramref name="target"/>: spend the cast's
    /// resolved movement, then resolve the cast through the table. A staff's
    /// cast is its hit (§1.3), so it rolls, crits, fumbles and pays through
    /// <see cref="CastWith"/> as a swing lands through
    /// <see cref="ResolveAttackOn"/>. Returns false if the cast is not allowed.
    /// </summary>
    public bool TryCast(PartyMemberState caster, ActorState target)
    {
        if (!CanCast(caster, target)) return false;
        var w = caster.EquippedWeapon!;

        caster.DistLeft = MathF.Max(0f, caster.DistLeft - w.ResolvedCost);
        CastWith(caster, target);
        return true;
    }

    /// <summary>
    /// Can the caster cast their equipped wand with its shape aimed at
    /// <paramref name="aim"/>, a point in logic units? Needs a wand — the one
    /// weapon that carries a shape — enough movement and mana at the resolved
    /// costs, and at least one actor caught: a cast is a hit, and a shape with
    /// nobody in it has nothing to hit, exactly as a staff needs its target in
    /// reach. A Blast's aim is the point it centres
    /// on (pulled back to the wand's reach), a Cone's or Beam's the direction
    /// from the caster, and a Nova ignores it. The element's trigger is no
    /// gate: unaffordable, it does not fire and the hits land untyped.
    /// </summary>
    public bool CanCastArea(PartyMemberState caster, (float X, float Y) aim)
    {
        ArgumentNullException.ThrowIfNull(caster);
        if (Phase != TurnPhase.Player || !caster.Alive) return false;
        var w = caster.EquippedWeapon;
        if (w == null || !IsAreaCaster(w)) return false;
        if (caster.DistLeft < w.ResolvedCost || caster.Mana < w.ResolvedManaCost) return false;
        return AreaTargets(caster, aim).Count > 0;
    }

    /// <summary>
    /// A wand: the weapon that carries a shape (§1.4). The shape is the whole
    /// test — content fixes a shape on a Wand and on nothing else, and
    /// instantiation fixes a wand's element — so no kind of innate is asked
    /// here, and <see cref="AreaTargets"/> gates on the same datum.
    /// </summary>
    private static bool IsAreaCaster(Weapon w) => w.AreaShape != null;

    /// <summary>
    /// The actors a cast of <paramref name="caster"/>'s wand aimed at
    /// <paramref name="aim"/> would catch right now, nearest first: everyone on
    /// the far side inside the shape and in sight, and — only while friendly
    /// fire is on (<see cref="Tuning.FriendlyFireEnabled"/>, off until the
    /// placement scorer and the kiting AI exist) — the caster's own side as
    /// well, the caster itself never. Empty for anything but a wand, or for
    /// an actor off the roster. The scene's aim preview reads this; the cast
    /// reads it once, at the cast.
    /// </summary>
    public IReadOnlyList<ActorState> AreaTargets(ActorState caster, (float X, float Y) aim)
    {
        ArgumentNullException.ThrowIfNull(caster);
        var shape = caster.EquippedWeapon?.AreaShape;
        if (shape == null || !_sides.TryGetValue(caster, out var side))
            return Array.Empty<ActorState>();

        IEnumerable<ActorState> candidates = _rosters[(int)Opposite(side)];
        if (GameContent.Current.Tuning.FriendlyFireEnabled)
            candidates = candidates.Concat(_rosters[(int)side]);
        return AreaShapes.Targets(shape, caster, aim, candidates, _grid);
    }

    /// <summary>
    /// Cast the equipped wand at <paramref name="aim"/>: spend the cast's
    /// resolved movement, then resolve it through the table — one Cast,
    /// paying the cast's mana and the element's trigger once, and one hit per
    /// actor the shape caught, every one typed with the element and on the one
    /// roll the cast made (<see cref="CastAreaWith"/>). Returns false if the
    /// cast is not allowed.
    /// </summary>
    public bool TryCastArea(PartyMemberState caster, (float X, float Y) aim)
    {
        if (!CanCastArea(caster, aim)) return false;
        var w = caster.EquippedWeapon!;

        caster.DistLeft = MathF.Max(0f, caster.DistLeft - w.ResolvedCost);
        CastAreaWith(caster, aim);
        return true;
    }

    /// <summary>
    /// Can the member hold fire? Overwatch is ranged only — holding a shot is
    /// what a nocked arrow does — and the member needs Overwatch stacks, the
    /// swing's movement to spare, no shot already held, and shots left in this
    /// turn's pool: once the held shots have all fired, holding again would
    /// buy nothing.
    /// </summary>
    public bool CanOverwatch(PartyMemberState c)
    {
        if (Phase != TurnPhase.Player || !c.Alive) return false;
        var w = c.EquippedWeapon;
        if (w == null || w.IsCaster || w.Kind != WeaponKind.Ranged) return false;
        int shots = c.Value(ModifierType.Overwatch);
        if (shots <= 0 || c.HeldShots > 0) return false;
        if (_partyZone.UsesThisTurn(c) >= shots) return false;
        return c.DistLeft >= w.ResolvedCost;
    }

    /// <summary>
    /// Bank the shot instead of firing: spend the weapon's resolved cost and
    /// arm the member's Overwatch value in held shots, which makes its reach a
    /// threat zone — an enemy entering it during the enemy turn is shot for
    /// free, one shot per stack — until the next player turn clears them.
    /// Returns false if not allowed.
    /// </summary>
    public bool TryOverwatch(PartyMemberState c)
    {
        if (!CanOverwatch(c)) return false;
        var w = c.EquippedWeapon!;

        c.DistLeft = MathF.Max(0f, c.DistLeft - w.ResolvedCost);
        c.HeldShots = c.Value(ModifierType.Overwatch);
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
            c.RegenManaFromUnusedMovement(c.DistLeft);
            // The member's turn ends here: the status ticks are this event's
            // handlers, its applier writes what they settled, and a tick
            // death's consequences follow once the raise has returned.
            RaiseBoundary(GameEvent.TurnEnd, new TurnPayload(TurnCount, Side.Party), c);
        }

        // A tick can take the last member: the wipe stands, the turn does not go on.
        if (Phase == TurnPhase.GameOver) return;

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
        // The enemy side's turn starts: its chosen attacks are counted afresh.
        _attacksThisTurn.Clear();

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
            RaiseBoundary(GameEvent.TurnStart, new TurnPayload(TurnCount, Side.Enemy), enemy);

        // Whatever is equipped when the enemy phase begins is what reacts
        // (settled): both sides' zones re-arm from where everyone stands and
        // what everyone holds, so a swap made this turn is accounted for even
        // if it went unannounced — a mover already inside a reach never fires
        // on its first step; one outside it fires on walking in.
        SnapshotZones();

        _enemyIdx = -1;
        AdvanceToNextEnemy();
    }

    /// <summary>
    /// Hand the enemy turn to the next enemy that will actually do something;
    /// when none remain, the player turn starts. Dead enemies and passive
    /// ones — unseen for 2+ turns with nobody in reach — skip instantly, so a
    /// maze full of idle dummies costs no wall-clock time. On the way past,
    /// each enemy banks the budget it left as mana by the party's divisor
    /// (§1.3): the one that just acted whatever its action left, an idle one
    /// the whole of its (mired) budget.
    /// </summary>
    private void AdvanceToNextEnemy()
    {
        if (_enemyIdx >= 0 && _enemyIdx < _enemies.Count)
            ActingEnemy.RegenManaFromUnusedMovement(_enemyBudget);

        while (++_enemyIdx < _enemies.Count)
        {
            var enemy = _enemies[_enemyIdx];
            if (!enemy.Alive) continue;
            if (enemy.TurnsSinceSeen >= 2 && !HasPassiveAction(enemy))
            {
                enemy.RegenManaFromUnusedMovement(StatusBehaviours.MiredBudget(enemy, GameConstants.EnemyMove));
                continue;
            }

            StartEnemyAction(enemy);
            return;
        }

        StartPlayerTurn();
    }

    private void StartEnemyAction(EnemyState enemy)
    {
        // The budget after Mire's cut, the same 10% a level the party pays.
        _enemyBudget = StatusBehaviours.MiredBudget(enemy, GameConstants.EnemyMove);

        if (enemy.IsSupportCaster)
        {
            StartSupportAction(enemy);
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
    /// A support caster's turn (a staff whose innate lands on allies): while
    /// it has a living ally it moves to reach the one its effect is for (the
    /// most-wounded for a mending staff, the nearest for any other buff), then
    /// casts in the attack phase; alone, it flees the party. Unseen casters
    /// stay put but still cast on an ally already in reach.
    /// </summary>
    private void StartSupportAction(EnemyState caster)
    {
        var blocked = OccupiedTilesExcept(caster);

        if (!EnemyAi.HasLivingAlly(caster, _enemies))
        {
            EnemyFleeing?.Invoke(caster);
            var (flee, fleeLeft) = EnemyAi.PlanFlee(caster, _party, _grid, _enemyBudget, blocked);
            _enemyBudget = fleeLeft;
            if (flee.Count == 0) { BeginAttackPhase(); return; } // cornered: beats will no-op
            BeginWalk(flee);
            return;
        }

        var ally = EnemyAi.SelectSupportTarget(caster, _enemies);
        // Nobody to cast on, or the ally already in reach, or standing pat while unseen:
        // skip straight to the cast phase (which no-ops if nothing's castable).
        if (ally == null || caster.TurnsSinceSeen >= 2 || EnemyAi.CanHit(caster, ally, caster.Weapon, _grid))
        {
            BeginAttackPhase();
            return;
        }

        var (waypoints, remaining) = EnemyAi.PlanApproach(
            caster, ally.X, ally.Y, ally.Radius, caster.Weapon.Range, _grid, _enemyBudget, blocked);
        _enemyBudget = remaining;
        if (waypoints.Count == 0) { BeginAttackPhase(); return; }
        BeginWalk(waypoints);
    }

    // Every other living actor blocks this actor's path — enemies act
    // sequentially, so each planner queues behind the ones already in position
    // instead of piling onto the same tile — and stops a shove the same way.
    private HashSet<(int R, int C)> OccupiedTilesExcept(ActorState self)
    {
        var blocked = new HashSet<(int R, int C)>();
        foreach (var other in _enemies)
            if (other != self && other.Alive)
                blocked.Add(TileOf(other.X, other.Y));
        foreach (var member in _party)
            if (member != self && member.Alive)
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
    /// attacker, or a debuff caster, needs someone in reach; a support caster
    /// needs an ally its effect is for. A lone support caster only flees once
    /// it's actually seen (there's no point running through the fog from a
    /// party that can't see it), so while unseen it stays put like any other
    /// idle dummy.
    /// </summary>
    private bool HasPassiveAction(EnemyState enemy)
        => enemy.IsSupportCaster
            ? EnemyAi.HasLivingAlly(enemy, _enemies) && EnemyAi.SelectSupportTarget(enemy, _enemies) != null
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

        // Reactions resolve mid-walk: every zone the enemy crosses into fires
        // the moment it does — a spear's brace, a held shot — and every zone it
        // chooses to leave fires the axe watching the exit; zones merely passed
        // through count, not just wherever the walk ends. A kill stops the walk
        // on the spot, and so does a shove: a reaction that displaced the enemy
        // moved it off the path it planned, so the rest of the walk is dropped
        // and it acts from where it landed.
        float plannedX = enemy.X, plannedY = enemy.Y;
        NotifyActorMoved(enemy, MoveKind.Voluntary);
        if (Phase == TurnPhase.GameOver) return;   // a counter from inside the cascade took the last member
        if (!enemy.Alive)
        {
            Phase = TurnPhase.EnemyAttacking;
            _nextEnemyPending = true;
            _timer = BraceDeathPauseSeconds;
            return;
        }
        if (enemy.X != plannedX || enemy.Y != plannedY)
        {
            _waypointIdx = _waypoints.Count;
            BeginAttackPhase();
            return;
        }

        if (_waypointIdx >= _waypoints.Count)
            BeginAttackPhase();
    }

    // ── Threat zones ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuild both sides' pair sets from where everyone stands: a mover
    /// already inside a reach is marked so, and never fires that zone until it
    /// leaves and comes back. Run when the roster is fixed, at each player
    /// turn's start (after resurrections have placed everyone) and at each
    /// enemy phase's start (with whatever is equipped by then).
    /// </summary>
    private void SnapshotZones()
    {
        _partyZone.Clear();
        _enemyZone.Clear();
        foreach (var roster in _rosters)
            foreach (var reactor in roster)
                if (reactor.Alive)
                    RefreshZoneOf(reactor);
    }

    /// <summary>
    /// The zone of <paramref name="reactor"/> follows it: every living actor on
    /// the other side is marked inside or released according to what the
    /// reactor now holds and where it now stands, after a step or a swap.
    /// Silent — a reactor walking up to a target has not
    /// made the target move, and only the mover's own crossing fires anything.
    /// </summary>
    private void RefreshZoneOf(ActorState reactor)
    {
        var side = _sides[reactor];
        var zone = _zones[(int)side];
        var weapon = reactor.EquippedWeapon;
        foreach (var mover in _rosters[(int)Opposite(side)])
        {
            if (mover.Alive && weapon != null && EnemyAi.CanHit(reactor, mover, weapon, _grid))
                zone.MarkInside(reactor, mover);
            else
                zone.Leave(reactor, mover);
        }
    }

    /// <summary>
    /// The generalised brace resolver, taking which edge it cares about: for
    /// every reactor on the other side whose reach <paramref name="mover"/>
    /// just crossed on <paramref name="edge"/>, raise
    /// <see cref="GameEvent.ThreatZoneEntered"/> with the reactor as self and
    /// the mover as other — the handlers decide what the crossing earns, the
    /// applier spends and fires it. Same lookup, same pool, same resolver for a
    /// spear's entry, a held shot's entry and an axe's exit (§1.2).
    /// </summary>
    private void TryReactionsAgainst(ActorState mover, ZoneEdge edge, MoveKind kind)
    {
        var reactorSide = Opposite(_sides[mover]);
        var zone = _zones[(int)reactorSide];
        foreach (var reactor in _rosters[(int)reactorSide])
        {
            if (!mover.Alive) return;   // a reaction already resolved took it: nothing further crosses anything
            if (!reactor.Alive)
            {
                zone.Leave(reactor, mover);
                continue;
            }

            var weapon = reactor.EquippedWeapon;
            bool inside = weapon != null && EnemyAi.CanHit(reactor, mover, weapon, _grid);
            bool crossed = edge == ZoneEdge.Enter
                ? inside && zone.Enter(reactor, mover)
                : !inside && zone.Leave(reactor, mover);
            if (!crossed) continue;

            // The distance is read here, at the crossing: the reaction it earns
            // may resolve only after the shove that caused it has finished.
            _events.Enqueue(GameEvent.ThreatZoneEntered,
                new ThreatPayload(mover, kind, edge, ImmutableArray<Reaction>.Empty,
                    CombatRules.SurfaceDistanceUnits(reactor, mover)), reactor, mover);
        }
    }

    /// <summary>
    /// The ThreatZoneEntered applier: each reaction the handlers settled is
    /// fired if its reactor and the mover still stand, the reactor's zone is
    /// armed (an enemy's only once seen this turn), and the reactor has a use
    /// of the reaction's modifier left this turn — spent whether the crossing
    /// was a walk or a shove, which is what bounds a displacement chain. The
    /// free attack is queued as a DamageTaken from reactor to mover at the
    /// distance the crossing was read at (not where a finished shove left the
    /// mover), told to the side's typed brace event or the reaction's own, and
    /// for Overwatch lets one held shot go. The use pool is the zone's, keyed
    /// by reactor alone: a weapon holds at most one of Brace, Opportunist and
    /// Overwatch (one exclusion group), so it is the weapon's budget, as the
    /// design asks; an innate stack of one beside a weapon's stack of another
    /// (Phase 2) would share it, and is where the pool grows a modifier key.
    /// </summary>
    private void ApplyReactions(ThreatPayload settled, ActorState reactor, ActorState mover, EventTable table)
    {
        if (settled.Reactions.IsDefaultOrEmpty) return;
        foreach (var reaction in settled.Reactions)
        {
            var who = reaction.Reactor;
            if (!who.Alive || !mover.Alive) continue;
            if (!_mayReact[who]()) continue;

            var zone = _zones[(int)_sides[who]];
            if (zone.UsesThisTurn(who) >= who.Value(reaction.Source)) continue;
            zone.Spend(who);

            if (_onReactionFired.TryGetValue(reaction.Source, out var fired))
                fired(who);
            table.Enqueue(GameEvent.DamageTaken,
                DamagePayload.Initial(reaction.Weapon, _rollD20(), settled.DistanceUnits), who, mover);
        }
    }

    private static Side Opposite(Side side) => side == Side.Party ? Side.Enemy : Side.Party;

    private void BeginAttackPhase()
    {
        Phase = TurnPhase.EnemyAttacking;
        _timer = 0f; // first beat resolves on the next update
    }

    private void TryEnemyAttackBeat()
    {
        var enemy = ActingEnemy;

        if (enemy.Weapon.IsCaster)
        {
            TryEnemyCastBeat(enemy);
            return;
        }

        // Attack cost scales the same way the prototype scaled it: the enemy's
        // budget is 100 vs the player's 160, so weapon costs shrink to match.
        float scaledCost = GameConstants.EnemyMove / GameConstants.MaxDistance * enemy.Weapon.ResolvedCost;

        // The same gates as the party's: the budget, and Charges' cap on chosen attacks.
        if (!enemy.Alive || _enemyBudget < scaledCost || !HasAttackLeft(enemy))
        {
            _nextEnemyPending = true;
            _timer = AttackBeatSeconds;
            return;
        }

        var target = NearestHittable(enemy);
        if (target == null)
        {
            _nextEnemyPending = true;
            _timer = AttackBeatSeconds;
            return;
        }

        _enemyBudget -= scaledCost;
        CountAttack(enemy);
        AttackWith(enemy, target);
        if (Phase == TurnPhase.GameOver) return;

        _timer = AttackBeatSeconds;
    }

    /// <summary>The nearest living party member <paramref name="enemy"/>'s weapon reaches from where it stands (ties in roster order), or null.</summary>
    private PartyMemberState? NearestHittable(EnemyState enemy)
    {
        PartyMemberState? target = null;
        float bestDist = float.MaxValue;
        foreach (var c in _party)
        {
            if (!c.Alive || !EnemyAi.CanHit(enemy, c, enemy.Weapon, _grid)) continue;
            float d = Dist2(enemy, c);
            if (d < bestDist)
            {
                target = c;
                bestDist = d;
            }
        }
        return target;
    }

    /// <summary>
    /// A caster's attack-phase beat: cast its staff on the target its innate
    /// is for (a support staff on the ally it mends or buffs, a debuff staff
    /// on the nearest party member in reach), spending scaled budget and mana
    /// at the resolved costs, one cast per beat until dry, out of mana or out
    /// of targets. Through the same cast core as the party's, so it rolls,
    /// crits and pays its triggers the same way. A fleeing or idle caster
    /// simply finds no target and passes.
    /// </summary>
    private void TryEnemyCastBeat(EnemyState caster)
    {
        var weapon = caster.Weapon;
        float scaledCost = GameConstants.EnemyMove / GameConstants.MaxDistance * weapon.ResolvedCost;
        ActorState? target = caster.IsSupportCaster
            ? EnemyAi.SelectSupportTarget(caster, _enemies)
            : NearestHittable(caster);

        if (_enemyBudget < scaledCost || caster.Mana < weapon.ResolvedManaCost
            || target == null || !CanCastOn(caster, target))
        {
            _nextEnemyPending = true;
            _timer = AttackBeatSeconds;
            return;
        }

        _enemyBudget -= scaledCost;
        CastWith(caster, target);
        if (Phase == TurnPhase.GameOver) return;

        _timer = AttackBeatSeconds;
    }

    private void StartPlayerTurn()
    {
        // End of the enemy turn: each enemy's turn ends (its statuses tick —
        // regen on the allies a healer mended, poison on the poisoned; the dead
        // shed theirs), then the round — one player phase and one enemy phase —
        // closes for every actor and every status not ticked at a turn's end
        // loses its level. The whole roster, whatever its state; all of it
        // under the turn that is ending, and before resurrections (which
        // restore full HP) are considered.
        foreach (var enemy in _enemies)
            RaiseBoundary(GameEvent.TurnEnd, new TurnPayload(TurnCount, Side.Enemy), enemy);
        foreach (var member in _party)
            RaiseBoundary(GameEvent.RoundEnd, new TurnPayload(TurnCount, Side.Party), member);
        foreach (var enemy in _enemies)
            RaiseBoundary(GameEvent.RoundEnd, new TurnPayload(TurnCount, Side.Enemy), enemy);

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

        // A new turn: every reaction's uses are restored, held shots lapse,
        // the party's chosen attacks are counted afresh, and both sides' zones
        // re-arm from where everyone now stands (after resurrections placed
        // everyone): pairs already inside never fire this turn.
        _partyZone.ResetUses();
        _enemyZone.ResetUses();
        _ripostes.Reset();
        _attacksThisTurn.Clear();
        foreach (var roster in _rosters)
            foreach (var actor in roster)
                actor.HeldShots = 0;
        SnapshotZones();

        // The player phase opens for every member, the fallen included — the
        // boundary events are the roster's; only the budget refill is the living's.
        foreach (var c in _party)
        {
            if (c.Alive)
                c.StartTurn();
            RaiseBoundary(GameEvent.TurnStart, new TurnPayload(TurnCount, Side.Party), c);
        }

        Phase = TurnPhase.Player;
        PlayerTurnStarted?.Invoke();
    }

    // ── Status ticks and healing ─────────────────────────────────────────────

    /// <summary>
    /// The TurnEnd and RoundEnd applier — the one place a status tick is
    /// written. The status handlers settled a <see cref="StatusTick"/> per
    /// status for the actor; this applies them in order: a damage tick takes
    /// HP and can kill, in which case the dead shed everything, Killed is
    /// queued with no weapon, and the side's death consequences are held for
    /// <see cref="RaiseBoundary"/> to run once the raise — and with it the
    /// Killed chain — has returned; a healing tick queues HealingReceived, whose
    /// own applier restores the HP; a level change lands on the status list,
    /// which drops an entry at zero. Dead actors take no damage and no heal —
    /// regen cannot resurrect — but do shed.
    /// </summary>
    private void ApplyStatusTicks(TurnPayload settled, ActorState self, ActorState other, EventTable table)
        => ApplyTicks(settled.Ticks, self, table);

    private void ApplyTicks(ImmutableArray<StatusTick> ticks, ActorState self, EventTable table)
    {
        if (ticks.IsDefaultOrEmpty) return;
        foreach (var tick in ticks)
        {
            if (self.Alive && tick.Damage > 0)
            {
                self.Hp = Math.Max(0, self.Hp - tick.Damage);
                ActorStatusTicked?.Invoke(self, tick);
                if (self.Hp <= 0)
                {
                    self.Alive = false;
                    self.StatusEffects.Clear();
                    table.Enqueue(GameEvent.Killed, new KillPayload(Weapon: null, tick.Damage, tick.Damage), self, self);
                    _heldDeaths.Add(self);
                    return;
                }
            }
            if (self.Alive && tick.Healing > 0)
                table.Enqueue(GameEvent.HealingReceived, new HealPayload(tick.Healing, Applied: 0, Overflow: 0, tick.Type.ToString()), self, self);
            if (tick.LevelsDelta != 0)
                self.AdjustStatus(tick.Type, tick.Element, tick.LevelsDelta);
        }
    }

    /// <summary>
    /// The HealingReceived applier: restore what the chain settled as applied
    /// — never past full, never on the dead — tell the side's typed healed
    /// feed, land any status the chain settled, and queue HealingAboveFull
    /// with the overflow so whatever banks surplus healing can react to it.
    /// </summary>
    private void ApplyHealing(HealPayload settled, ActorState self, ActorState other, EventTable table)
    {
        if (!self.Alive) return;
        // Never past full — and no fault on an actor already past it (a fixture's HP, a maximum lowered later): nothing to restore is zero.
        int applied = Math.Clamp(settled.Applied, 0, Math.Max(0, self.MaxHp - self.Hp));
        self.Hp += applied;
        if (applied > 0)
        {
            if (!_healFeeds.TryGetValue(self, out var healed))
                throw new InvalidOperationException("Healed an actor that is not on this turn system's roster.");
            healed(applied);
        }
        ApplyTicks(settled.Ticks, self, table);
        if (settled.Overflow > 0)
            table.Enqueue(GameEvent.HealingAboveFull, settled with { Ticks = default }, self, other);
    }

    /// <summary>The HealingAboveFull applier: the status changes the chain settled — the hidden pool converting into Ward.</summary>
    private void ApplyHealingAboveFull(HealPayload settled, ActorState self, ActorState other, EventTable table)
        => ApplyTicks(settled.Ticks, self, table);

    /// <summary>
    /// The MovementSpent applier: take what the chain settled as spent off the
    /// actor's budget (nothing, for a free swap raised at zero), through the
    /// feed the roster fixed for it — never by asking the actor its kind.
    /// </summary>
    private void ApplyMovementSpent(MovementPayload settled, ActorState self, ActorState other, EventTable table)
    {
        if (settled.Spent <= 0) return;
        if (!_movementFeeds.TryGetValue(self, out var spend))
            throw new InvalidOperationException("Spent the movement of an actor that is not on this turn system's roster.");
        spend(settled.Spent);
    }

    /// <summary>
    /// Raise one of the three boundary events for one actor — as both self and
    /// other — and then run the death consequences of anyone a tick killed in
    /// it. The raise drained the queued Killed chain before returning, so this
    /// is the tick death's counterpart of <see cref="ResolveAttackOn"/> running
    /// them after a hit's raise returns: a Killed behaviour sees the same
    /// world whichever way the actor died.
    /// </summary>
    private void RaiseBoundary(GameEvent evt, TurnPayload payload, ActorState actor)
    {
        _events.Raise(evt, payload, actor, actor);
        RunHeldDeaths();
    }

    /// <summary>
    /// The side's consequences for each actor that died in the raise that
    /// just returned — the primary target of a hit, a tick's victim, a mover a
    /// queued brace took, an attacker a counter took — through the feed the
    /// roster fixed, in the order they fell. Never called mid-cascade.
    /// </summary>
    private void RunHeldDeaths()
    {
        if (_heldDeaths.Count == 0) return;
        var died = _heldDeaths.ToArray();
        _heldDeaths.Clear();
        foreach (var actor in died)
        {
            if (!_deathFeeds.TryGetValue(actor, out var feed))
                throw new InvalidOperationException("An actor that is not on this turn system's roster died.");
            feed();
        }
    }

    // ── Shared attack plumbing ───────────────────────────────────────────────

    /// <summary>
    /// The one swing, whoever swings, with whatever they hold: the primary
    /// target is resolved, then —
    /// the attacker's <see cref="ModifierType.Cleave"/> value in extra targets,
    /// nearest first among the other actors of the far side the weapon
    /// reaches, all chosen at the swing so nothing the first hit sets off can
    /// hide a target from it — each extra target is resolved as a hit the
    /// cleave fanned out (<see cref="DamagePayload.FromCleave"/>, which Rout
    /// reads), the movement having been paid once by the caller. Every hit
    /// settles, cascades and holds its deaths through
    /// <see cref="ResolveAttackOn"/>, one after another, each on its own roll;
    /// the swing stops short when its attacker has fallen to a counter or the
    /// game is over.
    /// </summary>
    private void AttackWith(ActorState attacker, ActorState primary)
    {
        var weapon = attacker.EquippedWeapon!;   // the callers gated on it: a swing is with what the attacker holds
        var caught = CleaveTargets(attacker, weapon, primary);
        ResolveAttackOn(attacker, weapon, primary);
        foreach (var target in caught)
        {
            if (Phase == TurnPhase.GameOver || !attacker.Alive) return;
            ResolveAttackOn(attacker, weapon, target, fromCleave: true);
        }
    }

    /// <summary>
    /// The other living actors on the far side that <paramref name="weapon"/>
    /// reaches from where <paramref name="attacker"/> stands, nearest first —
    /// ties in roster order, a stable sort, so the choice is data and never a
    /// roll — up to the attacker's Cleave value; none without it.
    /// </summary>
    private List<ActorState> CleaveTargets(ActorState attacker, Weapon weapon, ActorState primary)
    {
        int extra = attacker.Value(ModifierType.Cleave);
        if (extra <= 0) return new List<ActorState>();
        return _rosters[(int)Opposite(_sides[attacker])]
            .Where(t => t != primary && t.Alive && EnemyAi.CanHit(attacker, t, weapon, _grid))
            .OrderBy(t => Dist2(attacker, t))
            .Take(extra)
            .ToList();
    }

    /// <summary>
    /// The one place a hit lands, whoever swings and whoever is hit: the
    /// attacker's natural roll and the surface distance seed a DamagePayload,
    /// DamageTaken runs the §1.6 chain on this system's table, and the applier
    /// (<see cref="ApplyDamage"/>) writes the settled result once — HP, the
    /// side's typed hit event, the queued follow-ups: a counter, a shove and
    /// the braces it fires, all drained before the raise returns. What a death
    /// *means* (movement lost, defeat turn, party wipe) runs after it, for
    /// everyone the cascade took, through <see cref="RunHeldDeaths"/>.
    /// <paramref name="fromCleave"/> marks a hit a swing fanned out to beyond
    /// its primary target; <paramref name="type"/> is the element an area
    /// cast settled for its hits, <paramref name="roll"/> the one natural roll
    /// that cast made and every hit shares (a swing rolls its own), and
    /// <paramref name="onAlly"/> marks a hit on the attacker's own side.
    /// </summary>
    private void ResolveAttackOn(ActorState attacker, Weapon attackerWeapon, ActorState target, bool fromCleave = false,
        DamageType type = DamageType.None, int? roll = null, bool onAlly = false)
    {
        Func<int> rollD20 = roll is int shared ? () => shared : _rollD20;
        CombatRules.Resolve(_events, attacker, target, attackerWeapon,
            CombatRules.SurfaceDistanceUnits(attacker, target), rollD20, fromCleave, type, onAlly);
        RunHeldDeaths();
    }

    // ── Casting ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The cast every caster makes, whichever side, before its own budget is
    /// asked: a staff in hand whose innate applies a status (a wand's area
    /// cast is its own path), the target on the side the innate lands on
    /// (the caster's own for a buff, itself included; the other for a debuff)
    /// and within range and sight. Off-roster actors cast on nobody.
    /// </summary>
    private bool CanCastOn(ActorState caster, ActorState target)
    {
        if (!caster.Alive || !target.Alive) return false;
        var w = caster.EquippedWeapon;
        if (w == null || !w.IsCaster) return false;
        var innate = w.Innate;
        if (innate?.Def.Applies == null) return false;
        if (!_sides.TryGetValue(caster, out var casterSide) || !_sides.TryGetValue(target, out var targetSide)) return false;
        if (!TargetsSide(innate.Def.Targets, sameSide: casterSide == targetSide)) return false;
        return EnemyAi.CanHit(caster, target, w, _grid);
    }

    /// <summary>Whether an effect for <paramref name="targets"/> may land across <paramref name="sameSide"/>: a buff on its own side, a debuff on the other, either for one that takes any.</summary>
    private static bool TargetsSide(TargetSide targets, bool sameSide)
        => targets == TargetSide.Any || (targets == TargetSide.Ally) == sameSide;

    /// <summary>
    /// The one place a cast lands, whoever casts (§1.3 "a cast is a hit"):
    /// the caster's natural roll, the same d20 a swing makes in the same crit
    /// window, settles what it did to the cast (<see cref="CombatRules.RollToCast"/>:
    /// a crit doubles the innate's levels and halves the mana, a fumble
    /// doubles the mana), Cast runs the enchantment chain on this system's
    /// table (the innate's status at its levels, the trigger paid out of what
    /// the cast left, in attachment order) and the applier
    /// (<see cref="ApplyCast"/>) writes the settled result once. Deaths a
    /// cast's cascade caused settle after it, as after a hit; no Phase 1 cast
    /// causes one.
    /// </summary>
    private void CastWith(ActorState caster, ActorState target)
    {
        var weapon = caster.EquippedWeapon!;   // the callers gated on it: a cast is with what the caster holds
        var innate = weapon.Innate!;
        var roll = CombatRules.RollToCast(_rollD20(), CombatRules.CritThreshold(caster),
            innate.LevelsFor(innate.Def.Potency), weapon.ResolvedManaCost);
        _events.Raise(GameEvent.Cast,
            new CastPayload(weapon, target, roll.Roll, roll.IsCrit, roll.IsFumble, roll.Levels, roll.ManaCost), caster, target);
        RunHeldDeaths();
    }

    /// <summary>
    /// The one place an area cast lands (§1.4). The targets are chosen at the
    /// cast — everyone the shape catches from where the caster stands, nearest
    /// first, so nothing the first hit sets off can hide a body from it. The
    /// caster rolls once, in its own crit window, and that roll settles the
    /// cast (a crit halves the mana, a fumble doubles it) and every hit alike
    /// (a crit doubles them all with Block skipped, a natural 1 halves them
    /// all): one roll per cast, shared by every target, because the mana rule
    /// is per cast. Cast then runs the enchantment chain once, the caster
    /// standing in as its own target — an area cast is aimed at a point, not
    /// an actor — where the element pays its trigger once for the whole shape
    /// and types the cast, and the applier spends the mana. Then each hit
    /// resolves through <see cref="ResolveAttackOn"/> as a swing does, one
    /// after another, carrying the settled type into the chart at (3,1) and,
    /// on an ally, the friendly-fire mark for (1,2); the cast stops short when
    /// the caster has fallen to a counter or the game is over.
    /// </summary>
    private void CastAreaWith(ActorState caster, (float X, float Y) aim)
    {
        var weapon = caster.EquippedWeapon!;   // the callers gated on it: a cast is with what the caster holds
        var targets = AreaTargets(caster, aim);
        var innate = weapon.Innate!;   // a wand's element: instantiation refuses a wand without one
        var roll = CombatRules.RollToCast(_rollD20(), CombatRules.CritThreshold(caster),
            innate.LevelsFor(innate.Def.Potency), weapon.ResolvedManaCost);
        var settled = _events.Raise(GameEvent.Cast,
            new CastPayload(weapon, caster, roll.Roll, roll.IsCrit, roll.IsFumble, roll.Levels, roll.ManaCost), caster, caster);
        RunHeldDeaths();

        var side = _sides[caster];
        foreach (var target in targets)
        {
            if (Phase == TurnPhase.GameOver || !caster.Alive) return;
            ResolveAttackOn(caster, weapon, target, type: settled.Type, roll: roll.Roll, onAlly: _sides[target] == side);
        }
    }

    /// <summary>
    /// The Cast applier, the single world-write for a cast. Lands the settled
    /// status applications on the target, telling the target's typed feed
    /// (CharacterBuffed or EnemyBuffed, fixed when the roster was typed, never
    /// by asking the target its kind) and <see cref="ActorStatusApplied"/> of
    /// each, then queues the one ManaSpent record of the cast: the cast's own
    /// mana plus the triggers its enchantments paid, spent by that event's
    /// applier and never past the pool (a fumble's doubled cost empties it, it
    /// does not overdraw it). A cast whose parties are no longer both standing
    /// lands on nothing.
    /// </summary>
    private void ApplyCast(CastPayload settled, ActorState caster, ActorState target, EventTable table)
    {
        if (!caster.Alive || !target.Alive) return;

        if (!settled.ApplyToTarget.IsDefaultOrEmpty)
        {
            if (!_statusFeeds.TryGetValue(target, out var applied))
                throw new InvalidOperationException("Cast on an actor that is not on this turn system's roster.");
            foreach (var status in settled.ApplyToTarget)
            {
                var effect = target.ApplyStatus(status.Type, status.Element, status.Levels);
                applied(effect);
                ActorStatusApplied?.Invoke(target, effect, caster);
            }
        }

        int wanted = settled.ManaCost + settled.ManaToSpend;
        table.Enqueue(GameEvent.ManaSpent, new ManaPayload(wanted, Math.Min(wanted, caster.Mana), settled.Weapon.Id), caster, target);
    }

    /// <summary>
    /// The ManaSpent applier: take what the chain settled as spent off the
    /// actor's pool (every actor carries one, so no feed is needed), never
    /// below zero, and nothing for a spend settled at zero.
    /// </summary>
    private void ApplyManaSpent(ManaPayload settled, ActorState self, ActorState other, EventTable table)
    {
        if (settled.Spent <= 0) return;
        self.Mana = Math.Max(0, self.Mana - settled.Spent);
    }

    /// <summary>
    /// The DamageTaken applier — the single world-write for a hit. Takes
    /// <see cref="DamagePayload.Taken"/> off the target's HP, spends the Ward
    /// levels that swallowed the rest, lands the settled status applications
    /// on both sides (riders on the defender, BlockWeaken on the attacker),
    /// hands the projected resolution to the target's typed hit feed
    /// (CharacterHit or EnemyHit, fixed when the roster was typed at
    /// construction — never by asking the target its kind), then, on a
    /// survivor, answers a successful block with the defender's Riposte and
    /// performs the settled displacement tile by tile (step 8), and queues
    /// the follow-ups for the table to drain after it returns, in this order:
    /// Killed on a death, DamageDealt always, Crit on a crit. The order is a
    /// ruling of the decomposition (1b rule 5), pinned by DamagePipelineTests,
    /// not an accident of this method. Death consequences are held for the
    /// outermost raise. A queued hit whose parties are no longer both standing
    /// — a second brace on a mover the first one killed — lands on nothing: a
    /// corpse neither swings nor is struck.
    /// </summary>
    private void ApplyDamage(DamagePayload settled, ActorState attacker, ActorState target, EventTable table)
    {
        if (!attacker.Alive || !target.Alive) return;

        target.Hp = Math.Max(0, target.Hp - settled.Taken);
        if (settled.WardSpent > 0)
        {
            int perLevel = Math.Max(1, StatusRules.EffectPerLevel(StatusEffectType.Ward));
            target.AdjustStatus(StatusEffectType.Ward, null, -((settled.WardSpent + perLevel - 1) / perLevel));
        }
        if (!settled.ApplyToDefender.IsDefaultOrEmpty)
            foreach (var status in settled.ApplyToDefender)
                target.ApplyStatus(status.Type, status.Element, status.Levels);
        if (!settled.ApplyToAttacker.IsDefaultOrEmpty)
            foreach (var status in settled.ApplyToAttacker)
                attacker.ApplyStatus(status.Type, status.Element, status.Levels);

        if (!_hitFeeds.TryGetValue(target, out var hitFeed))
            throw new InvalidOperationException("Hit an actor that is not on this turn system's roster.");
        hitFeed(CombatRules.Project(settled));

        if (target.Hp <= 0)
        {
            target.Alive = false;
            target.StatusEffects.Clear();   // the dead shed everything
            table.Enqueue(GameEvent.Killed, new KillPayload(settled.Weapon, settled.Dealt, settled.Taken), attacker, target);
            _heldDeaths.Add(target);
        }
        else
        {
            // You absorb the hit, then you answer it — from where you stood
            // when it landed; then the blow moves you.
            if (settled.Blocked)
                TryRiposte(target, attacker, table);
            if (settled.Displace is { Tiles: > 0 } shove)
                ApplyDisplacement(target, shove);
        }
        table.Enqueue(GameEvent.DamageDealt, settled, attacker, target);
        if (settled.IsCrit)
            table.Enqueue(GameEvent.Crit, new CritPayload(settled.Weapon, settled.Roll), attacker, target);
    }

    /// <summary>
    /// The riposte hook: a successful block — never a crit, which a block
    /// never happens to — grants the defender a free counter-swing at the
    /// attacker, up to its Riposte value a turn, provided it can reach: the
    /// counter is a strike with the defender's own weapon, so a shield-bearer
    /// blocking an arrow from across the room has nothing to answer with.
    /// Queued as a DamageTaken from defender to attacker, so the counter is a
    /// hit like any other: it can be blocked, riposted back within the
    /// attacker's own budget, and it shoves if the blade does.
    /// </summary>
    private void TryRiposte(ActorState defender, ActorState attacker, EventTable table)
    {
        int counters = defender.Value(ModifierType.Riposte);
        if (counters <= 0 || _ripostes.Uses(defender, ModifierType.Riposte) >= counters) return;
        var weapon = defender.EquippedWeapon;
        if (weapon == null || weapon.IsCaster || !EnemyAi.CanHit(defender, attacker, weapon, _grid)) return;

        _ripostes.Spend(defender, ModifierType.Riposte);
        RiposteTriggered?.Invoke(defender);
        table.Enqueue(GameEvent.DamageTaken,
            DamagePayload.Initial(weapon, _rollD20(), CombatRules.SurfaceDistanceUnits(defender, attacker)), defender, attacker);
    }

    /// <summary>
    /// Step 8, applied once: move the target the settled number of tiles, one
    /// at a time, setting it down on each tile's centre and sending every step
    /// through the move path as a forced move — so the zones it is shoved
    /// into fire (their braces queue behind the shove and resolve once it has
    /// finished), the zones it is shoved out of do not, and it is never
    /// teleported past either. A wall or an occupied tile stops it short.
    /// </summary>
    private void ApplyDisplacement(ActorState target, Displacement shove)
    {
        int moved = 0;
        for (int i = 0; i < shove.Tiles && target.Alive; i++)
        {
            var next = Displacer.NextTile(target, shove, _grid, OccupiedTilesExcept(target));
            if (next == null) break;

            var (x, y) = Displacer.CentreOf(next.Value.R, next.Value.C);
            target.X = x;
            target.Y = y;
            moved++;
            NotifyActorMoved(target, MoveKind.Forced);
        }
        if (moved > 0)
            ActorDisplaced?.Invoke(target, moved);
    }

    /// <summary>What a party member's death means, however it came: the bank is lost, the scene is told, and the last one falling ends the game.</summary>
    private void OnCharacterDied(PartyMemberState target)
    {
        target.SavedMovement = 0;
        CharacterDied?.Invoke(target);

        if (_party.All(c => !c.Alive))
        {
            Phase = TurnPhase.GameOver;
            _timer = GameOverPauseSeconds;
            GameOver?.Invoke();
        }
    }

    /// <summary>What an enemy's death means, however it came: the defeat turn the resurrection timer counts from, and the scene is told.</summary>
    private void OnEnemyDefeated(EnemyState target)
    {
        target.DefeatedAtTurn = TurnCount;
        EnemyDefeated?.Invoke(target);
    }

    private static float Dist2(ActorState a, ActorState b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
