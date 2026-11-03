namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Step 8 of the §1.6 pipeline: the displacement group. Push shoves the
/// defender away from the attacker, Drag pulls it toward the attacker, and
/// Rout is Push applied to everything a cleave caught (§1.2) — a tile per
/// stack, along the dominant axis between the two, settled on the payload as
/// a <see cref="Displacement"/> and applied once by the turn system, tile by
/// tile through the move path so every zone crossed fires (§1.7). They fire
/// on any hit the carrying weapon lands, however delivered — a swing, a
/// brace, a counter — and whether or not the block succeeded. One direction
/// per weapon: the group excludes itself, so at most one of the three settles
/// anything on a given hit.
/// </summary>
public static class DisplacementBehaviours
{
    public static readonly HandlerPriority PushPriority = new(8, 0);
    public static readonly HandlerPriority DragPriority = new(8, 1);
    public static readonly HandlerPriority RoutPriority = new(8, 2);

    /// <summary>Register the three on <paramref name="table"/>, in step order.</summary>
    public static void Register(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.On<DamagePayload>(GameEvent.DamageTaken, PushPriority, "Push", Push);
        table.On<DamagePayload>(GameEvent.DamageTaken, DragPriority, "Drag", Drag);
        table.On<DamagePayload>(GameEvent.DamageTaken, RoutPriority, "Rout", Rout);
    }

    /// <summary>(8,0): the attacker's Push shoves the defender a tile per stack away from the attacker.</summary>
    public static DamagePayload Push(DamagePayload payload, ActorState self, ActorState other)
        => Shove(payload, self, other, ModifierType.Push, towardAttacker: false);

    /// <summary>
    /// (8,1): the attacker's Drag pulls the defender a tile per stack toward
    /// the attacker — the exact inverse of Push — and never onto it: the
    /// attacker's own tile is occupied, and an occupied tile stops the pull.
    /// </summary>
    public static DamagePayload Drag(DamagePayload payload, ActorState self, ActorState other)
        => Shove(payload, self, other, ModifierType.Drag, towardAttacker: true);

    /// <summary>
    /// (8,2): the attacker's Rout is Push applied to everything the cleave
    /// caught — the primary target and each hit the cleave fanned out
    /// (<see cref="DamagePayload.FromCleave"/>) alike are shoved a tile per
    /// stack away from the attacker.
    /// </summary>
    public static DamagePayload Rout(DamagePayload payload, ActorState self, ActorState other)
        => Shove(payload, self, other, ModifierType.Rout, towardAttacker: false);

    private static DamagePayload Shove(DamagePayload payload, ActorState self, ActorState other, ModifierType type, bool towardAttacker)
    {
        int tiles = self.Value(type);
        if (tiles <= 0) return payload;

        var direction = Displacer.Direction(self, other);
        if (direction == null) return payload;   // standing on the same spot: no axis to shove along

        var (dr, dc) = direction.Value;
        if (towardAttacker)
        {
            dr = -dr;
            dc = -dc;
        }
        return payload with { Displace = new Displacement(tiles, dr, dc) };
    }
}
