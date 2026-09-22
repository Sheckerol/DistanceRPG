using System.Collections.Immutable;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The threat-zone group on <see cref="GameEvent.ThreatZoneEntered"/> (§1.2,
/// §1.7), raised once per reactor for each edge of its zone a mover crosses,
/// with the reactor as <c>self</c> and the mover as <c>other</c>. Each
/// handler reads the edge and the kind of move and appends a
/// <see cref="Reaction"/> — who fires, with what, on which modifier's
/// per-turn budget — and the turn system's applier spends the use and queues
/// the free attack. Brace fires on entry however the move came about, in
/// either side's phase; a held Overwatch shot on entry likewise, however the
/// move came about, but in the reactor's opponent's phase only — it is banked
/// for the other side's turn; and Opportunist on a chosen exit only: forced movement
/// is movement into a zone, never a disengagement out of one, so a Rout
/// cannot detonate its wielder's own opportunity attacks.
/// </summary>
public static class ReactionBehaviours
{
    public static readonly HandlerPriority BracePriority = new(1, 0);
    public static readonly HandlerPriority OverwatchPriority = new(1, 1);
    public static readonly HandlerPriority OpportunistPriority = new(2, 0);

    /// <summary>Register the three on <paramref name="table"/>: the entry edge's reactions first, then the exit edge's.</summary>
    public static void Register(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.On<ThreatPayload>(GameEvent.ThreatZoneEntered, BracePriority, "Brace", Brace);
        table.On<ThreatPayload>(GameEvent.ThreatZoneEntered, OverwatchPriority, "Overwatch", Overwatch);
        table.On<ThreatPayload>(GameEvent.ThreatZoneEntered, OpportunistPriority, "Opportunist", Opportunist);
    }

    /// <summary>(1,0): a mover entering the reactor's reach — walking, shoved or dragged — earns a retaliation, up to the reactor's Brace value a turn.</summary>
    public static ThreatPayload Brace(ThreatPayload payload, ActorState self, ActorState other)
    {
        if (payload.Edge != ZoneEdge.Enter || self.Value(ModifierType.Brace) <= 0) return payload;
        return Append(payload, self, ModifierType.Brace);
    }

    /// <summary>
    /// (1,1): a mover entering the reach of a ranged reactor holding fire is
    /// shot for free, up to the shots held — in the reactor's opponent's
    /// phase: "if an enemy enters line of sight during the enemy turn, it
    /// fires for free" (§1.2). Walked or shoved alike, so a brace's or a
    /// counter's shove on the enemy turn carries a body into the reach as
    /// surely as its own feet (settled: forced movement triggers the zones);
    /// but a body the reactor's own side shoves in on its own turn is not the
    /// enemy turn, and the shot stays held for the turn it was banked for. A
    /// ranged mirror of Brace, on the same edge; without a shot held the reach
    /// is no zone at all.
    /// </summary>
    public static ThreatPayload Overwatch(ThreatPayload payload, ActorState self, ActorState other)
    {
        if (payload.Edge != ZoneEdge.Enter || payload.OnReactorsTurn) return payload;
        if (self.HeldShots <= 0 || self.Value(ModifierType.Overwatch) <= 0) return payload;
        if (self.EquippedWeapon?.Kind != WeaponKind.Ranged) return payload;
        return Append(payload, self, ModifierType.Overwatch);
    }

    /// <summary>
    /// (2,0): a mover that chooses to leave the reactor's reach is hit on the
    /// way out, up to the reactor's Opportunist value a turn. Voluntary
    /// movement only — a target shoved out has not disengaged; it has been moved.
    /// </summary>
    public static ThreatPayload Opportunist(ThreatPayload payload, ActorState self, ActorState other)
    {
        if (payload.Edge != ZoneEdge.Exit || payload.Kind != MoveKind.Voluntary) return payload;
        if (self.Value(ModifierType.Opportunist) <= 0) return payload;
        return Append(payload, self, ModifierType.Opportunist);
    }

    private static ThreatPayload Append(ThreatPayload payload, ActorState reactor, ModifierType source)
    {
        var weapon = reactor.EquippedWeapon;
        if (weapon == null || weapon.IsCaster) return payload;   // a reaction is a strike; a caster casts, it cannot strike
        var reactions = payload.Reactions.IsDefault ? ImmutableArray<Reaction>.Empty : payload.Reactions;
        return payload with { Reactions = reactions.Add(new Reaction(reactor, weapon, source)) };
    }
}
