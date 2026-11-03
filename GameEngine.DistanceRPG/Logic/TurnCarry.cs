namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Every per-turn counter a <see cref="TurnSystem"/> keeps, handed from one
/// floor's system to the next when the party takes the stairs (§4.2): transit
/// keeps the turn, <em>all</em> of it. The attacks each actor has chosen (the
/// Charges cap), each reactor's threat-zone uses (Brace, Opportunist,
/// Overwatch), each actor's Riposte-style reaction uses, and the enemies a
/// member has laid eyes on this turn (which arm their zones and put a swap at
/// its combat price).
/// <para>
/// <strong>Why it has to travel.</strong> A fresh system zeroes all of it, and a
/// down-and-up hop is free — the party arrives back where it left — so without
/// the carry one stair hop would hand a thrower a fresh Charges allowance and
/// every reactor fresh uses inside a single turn. The carry is what makes the
/// stairs a place rather than a reset button.
/// </para>
/// <para>
/// Keyed by reference, like the counters it copies: an actor is the object on
/// a roster, never an equal-looking one. A carry belongs to one turn
/// (<see cref="Turn"/>): a system imports it only when it opens on that same
/// turn, and the visit keeps one carry for the turn in progress, merged across
/// every floor the party has stood on in it (<see cref="MergedWith"/>), so a
/// same-turn return finds its own enemies' counters too. A carry from an
/// earlier turn is dropped rather than imported — it would starve the next
/// turn's throws.
/// </para>
/// Engine-free and deterministic, like everything else under <c>Logic/</c>.
/// </summary>
/// <param name="Turn">The visit's turn these counters were spent in.</param>
/// <param name="Attacks">Chosen attacks per actor this turn — what Charges caps; reactions count for nothing.</param>
/// <param name="ZoneUses">Threat-zone uses per reactor this turn, either side's: a reactor fights on one side only, so one map holds both zones.</param>
/// <param name="ReactionUses">Uses of the reactions that answer being hit (Riposte) per actor and modifier this turn.</param>
/// <param name="Seen">The enemies a party member has laid eyes on this turn.</param>
public sealed record TurnCarry(
    int Turn,
    IReadOnlyDictionary<ActorState, int> Attacks,
    IReadOnlyDictionary<ActorState, int> ZoneUses,
    IReadOnlyDictionary<(ActorState Actor, ModifierType Type), int> ReactionUses,
    IReadOnlySet<EnemyState> Seen)
{
    /// <summary>
    /// This carry with <paramref name="later"/> laid over it, per actor: the
    /// counters of one turn across every floor visited in it. When
    /// <paramref name="later"/> is from another turn it is the whole answer —
    /// nothing of an earlier turn survives into a later one. Otherwise every
    /// entry <paramref name="later"/> holds wins (the system that exported it
    /// imported this carry's entries for its own roster first, so its figures
    /// are the current ones), and every entry it does not hold — another floor's
    /// enemies — is kept, so a return to that floor later in the same turn finds
    /// them as they were.
    /// </summary>
    public TurnCarry MergedWith(TurnCarry later)
    {
        ArgumentNullException.ThrowIfNull(later);
        if (later.Turn != Turn) return later;

        return new TurnCarry(
            Turn,
            Merge(Attacks, later.Attacks, ReferenceEqualityComparer.Instance),
            Merge(ZoneUses, later.ZoneUses, ReferenceEqualityComparer.Instance),
            Merge(ReactionUses, later.ReactionUses, EqualityComparer<(ActorState Actor, ModifierType Type)>.Default),
            new HashSet<EnemyState>(Seen.Concat(later.Seen), ReferenceEqualityComparer.Instance));
    }

    private static Dictionary<TKey, int> Merge<TKey>(IReadOnlyDictionary<TKey, int> earlier, IReadOnlyDictionary<TKey, int> later,
        IEqualityComparer<TKey> comparer)
        where TKey : notnull
    {
        var merged = new Dictionary<TKey, int>(comparer);
        foreach (var (key, count) in earlier)
            merged[key] = count;
        foreach (var (key, count) in later)
            merged[key] = count;
        return merged;
    }
}
