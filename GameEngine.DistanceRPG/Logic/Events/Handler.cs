namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One behaviour on one event (§1.7): apply this effect to the payload and
/// hand the result back so the next handler can act on it — a chain, not a
/// broadcast. A handler never writes to the world; it returns a changed
/// payload and the dispatcher applies the settled outcome once, at the end.
/// <paramref name="self"/> is the actor whose behaviour this is — on
/// <see cref="GameEvent.DamageTaken"/> the attacker, whose roll, crit window
/// and weapon the chain resolves — and <paramref name="other"/> the actor on
/// the far side of it — the defender, whose Block and Ward answer.
/// </summary>
public delegate TPayload Handler<TPayload>(TPayload payload, ActorState self, ActorState other);
