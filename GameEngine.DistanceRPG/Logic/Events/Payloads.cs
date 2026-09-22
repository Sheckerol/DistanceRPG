using System.Collections.Immutable;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The one payload of <see cref="GameEvent.DamageTaken"/> — and, once settled,
/// of <see cref="GameEvent.DamageDealt"/>: the §1.6 pipeline's working state
/// and its two outputs. Immutable; a handler returns <c>payload with { … }</c>.
/// Through the chain: <see cref="Amount"/> is the running weapon figure for
/// steps 1–3 and is closed into <see cref="WeaponShare"/> after step 3 ("this
/// is the WEAPON's damage, and only this"); step 4 fills
/// <see cref="EnchantmentShare"/> beside it; step 5 sets <see cref="Absorbed"/>
/// — off the weapon's share only, never below 1 of it, skipped entirely on a
/// crit — and fixes <see cref="Dealt"/> = WeaponShare + EnchantmentShare −
/// Absorbed; step 6 sets <see cref="WardSpent"/> and fixes <see cref="Taken"/>
/// = Dealt − WardSpent; everything after only appends to the Apply lists and
/// <see cref="Displace"/>. <see cref="Dealt"/> is never clamped to remaining
/// HP: the clean-kill test (§3.2) needs the unclamped figure.
/// </summary>
/// <param name="Amount">The running weapon figure through steps 1–3.</param>
/// <param name="Type">The hit's damage type; <see cref="DamageType.None"/> for a martial swing.</param>
/// <param name="IsCrit">The natural roll landed in the attacker's crit window.</param>
/// <param name="Dealt">After mitigation, before absorption: what the attacker did. Weapon XP, Serrated and the clean-kill test read this.</param>
/// <param name="Absorbed">What Block prevented — never dealt.</param>
/// <param name="Taken">What reached hit points. Death and Sturdy read this.</param>
/// <param name="WeaponShare">The weapon's own damage after step 3 — the only part that teaches the weapon.</param>
/// <param name="EnchantmentShare">What attached enchantments added at step 4, kept apart the whole way down.</param>
/// <param name="WardSpent">What Ward swallowed at step 6, in points (one level each): dealt, but landed on a pool that is not HP.</param>
/// <param name="Roll">The natural d20, rolled before the chain so step 1 is pure.</param>
/// <param name="Outcome">Crit, Weak (a natural 1) or Normal, decided at step 1.</param>
/// <param name="Weapon">The attacker's weapon.</param>
/// <param name="DistanceUnits">Surface-to-surface distance in logic units (32 per tile), rounded up, so a fraction past a tile boundary counts as the next tile.</param>
/// <param name="Blocked">The one blocked flag Riposte and BlockWeaken read: a successful block, which a crit never is.</param>
/// <param name="ApplyToDefender">Statuses to land on the defender (crit riders, Pin, Softening), applied once by the applier.</param>
/// <param name="ApplyToAttacker">Statuses to land on the attacker (BlockWeaken).</param>
/// <param name="Displace">Push, Drag or Rout settled here, applied once at step 8.</param>
/// <param name="ManaToSpend">The attacker's enchantment trigger payments accumulated in list order, spent once by the applier.</param>
/// <param name="FromCleave">True on a hit a cleave fanned out to beyond its primary target; Rout is Push applied to everything the cleave caught, the primary included, so it shoves either way.</param>
/// <param name="OnAlly">True on a hit that landed on the attacker's own side — an area cast catching an ally, which only happens while friendly fire is on — priced at <see cref="Tuning.FriendlyFireAllyPercent"/> at (1,2).</param>
/// <param name="DefenderManaToSpend">The defender's trigger payments — its Sturdy at (6,1), its Immovable at (8,3) — accumulated apart from the attacker's and spent once by the applier, off the defender's own pool.</param>
/// <param name="Spared">What Sturdy kept from reaching hit points — settled at (6,1) so that the wielder survives a lethal blow at the soul's potency in HP (one), taken off <see cref="Taken"/> by the (6,9) divider. <see cref="Dealt"/> is untouched: the blow was dealt, the wielder simply did not die of it.</param>
/// <param name="HealToAttacker">HP the entries that fire on damage dealt restore to the attacker — Vampiric's flat drink per instance — queued once by the DamageDealt applier as a HealingReceived, so a surplus reaches HealingAboveFull like any other.</param>
/// <param name="Pierces">Settled on DamageDealt by Piercing: the DamageDealt applier carries the shot to <see cref="NextInLine"/>, on a fresh roll.</param>
/// <param name="FromPierce">True on a hit the shot was carried to beyond its first body; it is not carried on again.</param>
/// <param name="CastFired">For a hit an area cast fanned out to: the ids of the cast's entries whose trigger the cast paid, in attachment order — what the hit-side halves of those entries (an element's share, a lingering element's burn) read to fire on the hit at no further cost. Default for a swing.</param>
/// <param name="OnDefendersTurn">True when the hit lands in the defender's own side's phase — a counter to its swing, a brace it walked into, a shot it drew by moving: the defender is acting, not holding a line. The souls that answer the opponent's phase (Immovable) stand aside. Set by the turn system from whose phase it is; false by default, the opponent's phase.</param>
/// <param name="NextInLine">On DamageDealt: the next body on the shot's line beyond the defender that the weapon reaches — the nearest living actor of the far side further along the attacker-to-defender ray, within a tile of it, in range and sight — read off the map by the DamageTaken applier at impact, before anything the hit sets off has moved. What an entry that carries the shot on (Piercing) reads before it pays; null when the line is clear, on a hit that was itself carried there, and throughout DamageTaken.</param>
public sealed record DamagePayload(
    int Amount, DamageType Type, bool IsCrit, int Dealt, int Absorbed,
    int Taken, int WeaponShare, int EnchantmentShare, int WardSpent,
    int Roll, RollOutcome Outcome, Weapon Weapon, int DistanceUnits,
    bool Blocked,
    ImmutableArray<StatusApplication> ApplyToDefender,
    ImmutableArray<StatusApplication> ApplyToAttacker,
    Displacement? Displace,
    int ManaToSpend,
    bool FromCleave = false,
    bool OnAlly = false,
    int DefenderManaToSpend = 0,
    int Spared = 0,
    int HealToAttacker = 0,
    bool Pierces = false,
    bool FromPierce = false,
    ImmutableArray<string> CastFired = default,
    bool OnDefendersTurn = false,
    ActorState? NextInLine = null)
{
    /// <summary>
    /// The payload as it enters the chain: only the inputs step 1 needs, with
    /// nothing computed and the lists empty rather than default.
    /// <paramref name="type"/> is the element the hit carries into the chart —
    /// a wand's, settled by its cast — and None for a martial swing;
    /// <paramref name="fromCleave"/> marks a hit the swing fanned out to beyond
    /// its primary target; <paramref name="onAlly"/> a hit on the attacker's
    /// own side; <paramref name="castFired"/> the entries the cast that fanned
    /// this hit out paid for; <paramref name="fromPierce"/> a hit a shot was
    /// carried to beyond its first body; <paramref name="onDefendersTurn"/> a
    /// hit landing in the defender's own side's phase.
    /// </summary>
    public static DamagePayload Initial(Weapon weapon, int roll, int distanceUnits, DamageType type = DamageType.None, bool fromCleave = false, bool onAlly = false,
        ImmutableArray<string> castFired = default, bool fromPierce = false, bool onDefendersTurn = false)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return new DamagePayload(
            Amount: 0, Type: type, IsCrit: false, Dealt: 0, Absorbed: 0,
            Taken: 0, WeaponShare: 0, EnchantmentShare: 0, WardSpent: 0,
            Roll: roll, Outcome: RollOutcome.Normal, Weapon: weapon, DistanceUnits: distanceUnits,
            Blocked: false,
            ApplyToDefender: ImmutableArray<StatusApplication>.Empty,
            ApplyToAttacker: ImmutableArray<StatusApplication>.Empty,
            Displace: null,
            ManaToSpend: 0,
            FromCleave: fromCleave,
            OnAlly: onAlly,
            CastFired: castFired.IsDefault ? ImmutableArray<string>.Empty : castFired,
            FromPierce: fromPierce,
            OnDefendersTurn: onDefendersTurn);
    }

    /// <summary>
    /// The weapon's share of <see cref="Dealt"/>: what Block, which comes off
    /// the weapon's share alone, left of the weapon's own damage. The number
    /// that measures the blow itself — weapon XP reads it (§2.2), and so does
    /// Serrated, since the wound is as deep as the blow that made it and not
    /// as deep as whatever rode the same swing (§1.6).
    /// </summary>
    public int WeaponDealt => WeaponShare - Absorbed;

    /// <summary>
    /// The hit as <see cref="GameEvent.DamageDealt"/> sees it: the settled
    /// outputs, with what the hit's own applier already wrote — the statuses
    /// it landed, the payments it took, the heal and the continuation nothing
    /// has settled yet — cleared, so the entries that fire on damage dealt
    /// accumulate their own and that event's applier writes them once.
    /// </summary>
    public DamagePayload AsDealt() => this with
    {
        ApplyToDefender = ImmutableArray<StatusApplication>.Empty,
        ApplyToAttacker = ImmutableArray<StatusApplication>.Empty,
        ManaToSpend = 0,
        DefenderManaToSpend = 0,
        HealToAttacker = 0,
        Pierces = false,
    };
}

/// <summary>A status to land on an actor once the chain settles: the type, its element (Searing keys on it) and the levels.</summary>
public sealed record StatusApplication(StatusEffectType Type, DamageType? Element, int Levels);

/// <summary>
/// One status's settled outcome at a trigger, written once by the raising
/// event's applier: the HP it removes (<paramref name="Damage"/>) or restores
/// (<paramref name="Healing"/>, which becomes a queued
/// <see cref="GameEvent.HealingReceived"/>), and the level change it takes —
/// negative for a tick's decrement, a decay, a shed or a reset, positive for a
/// grant such as the Ward the overheal pool converts into. A status handler
/// appends these to the boundary payloads instead of touching the actor.
/// </summary>
public sealed record StatusTick(StatusEffectType Type, DamageType? Element, int Damage, int Healing, int LevelsDelta);

/// <summary>
/// A settled shove: <paramref name="Tiles"/> steps of the 4-directional unit
/// (<paramref name="DirRow"/>, <paramref name="DirCol"/>) along the dominant
/// axis of attacker→target (reversed for Drag), applied one tile at a time and
/// stopping early at a wall or an occupied tile.
/// </summary>
public sealed record Displacement(int Tiles, int DirRow, int DirCol);

/// <summary><see cref="GameEvent.AttackDeclared"/>.</summary>
public sealed record AttackPayload(Weapon Weapon, ActorState Target, int DistanceUnits);

/// <summary>
/// <see cref="GameEvent.HealingReceived"/> and <see cref="GameEvent.HealingAboveFull"/>.
/// On both, <c>self</c> is the actor healed — whose behaviours react to the
/// surplus — and <c>other</c> the source, the same actor for a regeneration
/// tick. The raiser seeds <paramref name="Amount"/>; the chain settles what
/// was <paramref name="Applied"/> (never past full) and the
/// <paramref name="Overflow"/>, which HealingAboveFull then carries.
/// <paramref name="Ticks"/> holds the status changes the chain settled (the
/// hidden pool's conversion, the levels Overheal grants it), applied once by
/// the event's applier; <paramref name="ManaToSpend"/> the trigger payments
/// the healed actor's entries accumulated on HealingAboveFull, spent once.
/// </summary>
public sealed record HealPayload(int Amount, int Applied, int Overflow, string Source, ImmutableArray<StatusTick> Ticks = default, int ManaToSpend = 0);

/// <summary>
/// <see cref="GameEvent.Killed"/>: the weapon that did it — null when a status
/// tick did, in which case <c>self</c> and <c>other</c> are both the corpse
/// and there is no wielder to reward — and the killing hit's two outputs.
/// <paramref name="ManaToSpend"/> and <paramref name="ManaRestored"/> are what
/// the killer's entries settled on the kill (Siphon's trigger and its refund),
/// written once by the applier as one mana record.
/// </summary>
public sealed record KillPayload(Weapon? Weapon, int Dealt, int Taken, int ManaToSpend = 0, int ManaRestored = 0);

/// <summary><see cref="GameEvent.Crit"/>.</summary>
public sealed record CritPayload(Weapon Weapon, int Roll);

/// <summary>
/// <see cref="GameEvent.ManaSpent"/>: the one record of a spend — queued by the
/// Cast applier once per cast with the cast's own mana plus the triggers its
/// enchantments paid, <paramref name="Source"/> the weapon's id — whose applier
/// takes <paramref name="Spent"/> off the pool. <paramref name="Wanted"/> is what
/// the spend asked for; <paramref name="Spent"/> never exceeds the pool, so a
/// fumble's doubled cost empties it rather than overdrawing it. Enchantment XP
/// (§3.3) is counted on <paramref name="Spent"/>: the discounted mana actually paid.
/// <paramref name="Restored"/> is mana the same record hands back — Siphon's
/// refund on a kill — taken after the spend and never past the pool, so one
/// record says what a kill cost and what it returned.
/// </summary>
public sealed record ManaPayload(int Wanted, int Spent, string Source, int Restored = 0);

/// <summary><see cref="GameEvent.MovementSpent"/> — attack, cast and swap costs in Phase 1.</summary>
public sealed record MovementPayload(int Wanted, int Spent, string Source);

/// <summary>
/// <see cref="GameEvent.TurnStart"/>, <see cref="GameEvent.TurnEnd"/> and
/// <see cref="GameEvent.RoundEnd"/>, raised at the phase boundaries for every
/// actor on the roster — alive, dead, or sitting the phase out — with that
/// actor as both <c>self</c> and <c>other</c>. Each actor sees the three once
/// per round, in that order: the events say a phase opened or closed for the
/// actor, not that it acted, and a handler that only means the living checks
/// <see cref="ActorState.Alive"/>. <paramref name="Ticks"/> is what the status
/// handlers settled for the actor — its turn-end ticks, its round-end decay —
/// written once by the event's applier.
/// </summary>
public sealed record TurnPayload(int TurnCount, Side Side, ImmutableArray<StatusTick> Ticks = default);

/// <summary>
/// A reaction a threat-zone handler appends: who fires, with what, and on
/// which modifier's per-turn budget (<paramref name="Source"/>) — the
/// applier spends a use of it and queues the free attack.
/// </summary>
public sealed record Reaction(ActorState Reactor, Weapon Weapon, ModifierType Source);

/// <summary>
/// <see cref="GameEvent.ThreatZoneEntered"/>. The edge travels in the payload
/// rather than as a second event: §1.7 forbids casual members, and the exit
/// side is the same function watching the other edge.
/// </summary>
/// <param name="Mover">The actor whose move crossed the edge.</param>
/// <param name="Kind">Whether the mover chose the move or was shoved.</param>
/// <param name="Edge">Which edge of the reactor's zone the move crossed.</param>
/// <param name="Reactions">What the crossing earned: appended by the handlers, spent and fired by the applier.</param>
/// <param name="DistanceUnits">
/// Surface distance from the reactor to the mover at the crossing, in logic
/// units (32 per tile), rounded up. A reaction is earned at the crossing and
/// resolves at that distance: inside a running cascade it fires only once the
/// shove that caused it has finished, by which time the mover may stand deeper
/// in the zone or out the far side, and a distance-priced step (Longshot) must
/// see where the mover was struck, not where it landed.
/// </param>
public sealed record ThreatPayload(ActorState Mover, MoveKind Kind, ZoneEdge Edge, ImmutableArray<Reaction> Reactions, int DistanceUnits);

/// <summary>
/// <see cref="GameEvent.Cast"/>: a staff's hit (§1.3). <c>self</c> is the caster
/// and <c>other</c> the <paramref name="Target"/>. The raiser rolls the d20
/// before the chain and settles what the roll did to the cast — a crit doubles
/// <paramref name="Levels"/>, the innate's levels, and halves
/// <paramref name="ManaCost"/>, the weapon's resolved mana; a fumble doubles the
/// mana and leaves the levels (§1.6, settled) — so the chain is pure. The
/// enchantment loop then appends what each entry lands on the target to
/// <paramref name="ApplyToTarget"/> and what its trigger cost to
/// <paramref name="ManaToSpend"/>, in attachment order, each paying out of what
/// the cast and the entries before it left; the applier writes both once and
/// queues the one <see cref="ManaPayload"/> record of the cast. A wand's area
/// cast (§1.4) is aimed at a point rather than an actor, so its record names
/// the caster as its own <paramref name="Target"/>, the way a boundary event
/// names the actor twice, and the loop settles <paramref name="Type"/>: the
/// element every hit the shape fans out to carries into the chart, typed by
/// the element entry for one payment of its trigger — once per cast, not once
/// per target caught — and None for a staff, or when no element could fire.
/// For the same reason a status-applying cast entry (a staff's kind) has no
/// place on a wand — its status would land on the caster — and the content
/// validator refuses one (<see cref="ContentValidator.RuleWandNoStatusApplier"/>);
/// what such an entry should mean on an area cast is Phase 3's to decide.
/// <paramref name="Fired"/> lists the entries whose trigger this cast paid, in
/// attachment order — an element typing the cast or adding its share, a
/// lingering element — and travels into every hit the shape fans out to as
/// <see cref="DamagePayload.CastFired"/>: the cast pays once, the hits fire
/// their halves of those entries at no further cost, and an entry the cast
/// could not pay stays quiet on every hit.
/// </summary>
public sealed record CastPayload(Weapon Weapon, ActorState Target, int Roll, bool IsCrit, bool IsFumble, int Levels, int ManaCost,
    ImmutableArray<StatusApplication> ApplyToTarget = default, int ManaToSpend = 0, DamageType Type = DamageType.None,
    ImmutableArray<string> Fired = default);

/// <summary>Whether a move was chosen: Brace fires on entry either way, Opportunist only on a voluntary exit (§1.2).</summary>
public enum MoveKind { Voluntary, Forced }

/// <summary>Which edge of a threat zone a move crossed.</summary>
public enum ZoneEdge { Enter, Exit }

/// <summary>The side an actor fights on.</summary>
public enum Side { Party, Enemy }
