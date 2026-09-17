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
}
