namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Per-turn uses of the reactions that answer being hit rather than being
/// approached — Riposte — keyed by actor and modifier and reset when the
/// player turn starts, so "+1 counter per turn" is a stack's worth of uses a
/// round on either side. The threat-zone reactions keep their pool inside
/// the zone that watches for them. Keyed by reference identity.
/// </summary>
internal sealed class ReactionPool
{
    private readonly Dictionary<(ActorState Actor, ModifierType Type), int> _uses = new();

    /// <summary>Uses of <paramref name="type"/> this actor has spent since the last <see cref="Reset"/>.</summary>
    public int Uses(ActorState actor, ModifierType type)
        => _uses.TryGetValue((actor, type), out int used) ? used : 0;

    /// <summary>Spend one use.</summary>
    public void Spend(ActorState actor, ModifierType type) => _uses[(actor, type)] = Uses(actor, type) + 1;

    /// <summary>A new turn: every reaction's uses are restored.</summary>
    public void Reset() => _uses.Clear();

    /// <summary>Every (actor, reaction) that has spent a use since the last <see cref="Reset"/>, and how many: what a floor's turn system hands on when the party takes the stairs (<see cref="TurnCarry"/>).</summary>
    public IEnumerable<KeyValuePair<(ActorState Actor, ModifierType Type), int>> SpentUses => _uses;

    /// <summary>An actor's uses of <paramref name="type"/> as a carry brought them from another floor this same turn: what it spent stays spent.</summary>
    public void RestoreUses(ActorState actor, ModifierType type, int used) => _uses[(actor, type)] = used;
}
