namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The bookkeeping behind a threat zone: which (bracer, mover) pairs currently
/// stand inside the bracer's reach, and how many free attacks each bracer has
/// spent this turn. A free attack is earned on the entry edge — the moment a
/// mover that was outside a bracer's reach is found inside it — so a pair marked
/// inside when the zone arms never fires until it is released. Both brace
/// directions (the party's spears against a walking enemy, spear enemies against
/// a walking member) run on an instance of this; the reach test, the trigger
/// event and the attack itself belong to the caller, which knows the side.
/// Everything is keyed by reference identity, like the sets it replaces.
/// </summary>
internal sealed class ThreatZone
{
    private readonly HashSet<(ActorState Bracer, ActorState Mover)> _inside = new();
    private readonly Dictionary<ActorState, int> _usesThisTurn = new();

    /// <summary>Forget every pair — the zone is about to be re-armed from a fresh snapshot.</summary>
    public void Clear() => _inside.Clear();

    /// <summary>A new turn: every bracer's free attacks are restored.</summary>
    public void ResetUses() => _usesThisTurn.Clear();

    /// <summary>Arming: <paramref name="mover"/> already stands inside <paramref name="bracer"/>'s reach.</summary>
    public void MarkInside(ActorState bracer, ActorState mover) => _inside.Add((bracer, mover));

    /// <summary>
    /// The mover is inside the bracer's reach right now. True only on the entry
    /// edge — false if the pair was already inside.
    /// </summary>
    public bool Enter(ActorState bracer, ActorState mover) => _inside.Add((bracer, mover));

    /// <summary>
    /// The mover is outside the bracer's reach right now: release the pair so a
    /// later entry counts again. True on the exit edge (the pair was inside) —
    /// the edge an opportunity attack would watch.
    /// </summary>
    public bool Leave(ActorState bracer, ActorState mover) => _inside.Remove((bracer, mover));

    /// <summary>Free attacks this bracer has spent since the last <see cref="ResetUses"/>.</summary>
    public int UsesThisTurn(ActorState bracer)
        => _usesThisTurn.TryGetValue(bracer, out int used) ? used : 0;

    /// <summary>Spend one free attack.</summary>
    public void Spend(ActorState bracer) => _usesThisTurn[bracer] = UsesThisTurn(bracer) + 1;
}
