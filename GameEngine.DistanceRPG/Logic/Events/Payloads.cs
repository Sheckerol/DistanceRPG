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
/// <param name="ManaToSpend">Enchantment trigger payments accumulated in list order, spent once by the applier.</param>
/// <param name="FromCleave">True on a hit a cleave fanned out to beyond its primary target; Rout is Push applied to everything the cleave caught, the primary included, so it shoves either way.</param>
public sealed record DamagePayload(
    int Amount, DamageType Type, bool IsCrit, int Dealt, int Absorbed,
    int Taken, int WeaponShare, int EnchantmentShare, int WardSpent,
    int Roll, RollOutcome Outcome, Weapon Weapon, int DistanceUnits,
    bool Blocked,
    ImmutableArray<StatusApplication> ApplyToDefender,
    ImmutableArray<StatusApplication> ApplyToAttacker,
    Displacement? Displace,
    int ManaToSpend,
    bool FromCleave = false)
{
    /// <summary>
    /// The payload as it enters the chain: only the inputs step 1 needs, with
    /// nothing computed and the lists empty rather than default.
    /// <paramref name="fromCleave"/> marks a hit the swing fanned out to beyond
    /// its primary target.
    /// </summary>
    public static DamagePayload Initial(Weapon weapon, int roll, int distanceUnits, DamageType type = DamageType.None, bool fromCleave = false)
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
            FromCleave: fromCleave);
    }
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
/// hidden pool's conversion), applied once by the event's applier.
/// </summary>
public sealed record HealPayload(int Amount, int Applied, int Overflow, string Source, ImmutableArray<StatusTick> Ticks = default);

/// <summary><see cref="GameEvent.Killed"/>: the weapon that did it — null when a status tick did — and the killing hit's two outputs.</summary>
public sealed record KillPayload(Weapon? Weapon, int Dealt, int Taken);

/// <summary><see cref="GameEvent.Crit"/>.</summary>
public sealed record CritPayload(Weapon Weapon, int Roll);

/// <summary><see cref="GameEvent.ManaSpent"/>.</summary>
public sealed record ManaPayload(int Wanted, int Spent, string Source);

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

/// <summary><see cref="GameEvent.Cast"/>.</summary>
public sealed record CastPayload(Weapon Weapon, ActorState Target, int Roll, bool IsCrit, bool IsFumble, int Levels, int ManaCost);

/// <summary>Whether a move was chosen: Brace fires on entry either way, Opportunist only on a voluntary exit (§1.2).</summary>
public enum MoveKind { Voluntary, Forced }

/// <summary>Which edge of a threat zone a move crossed.</summary>
public enum ZoneEdge { Enter, Exit }

/// <summary>The side an actor fights on.</summary>
public enum Side { Party, Enemy }
