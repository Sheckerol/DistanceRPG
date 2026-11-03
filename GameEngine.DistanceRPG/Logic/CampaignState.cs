namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What the campaign has met, as against what a run is carrying: today exactly
/// one thing, the set of enchantment ids a weapon has brought into the party's
/// hands (§3.3). It is save data (§5.1) and it is the one collection that only
/// ever grows — §6.4's enchanter offers "what you have seen", and this is the
/// list it will be reading.
/// <para>
/// <strong>Pickup is the trigger, not sight.</strong> An entry is met when the
/// weapon carrying it enters an inventory, which is what "enters your inventory"
/// says; watching a dummy swing something meets nothing, because the party never
/// held it.
/// </para>
/// <para>
/// <strong>Unique souls go in.</strong> The set is what the campaign has met, and
/// whether an entry may then be copied is a different question with an answer
/// already in the content: <see cref="EnchantmentCatalogue.NeverRolled"/>. Filtering
/// here instead would make the set answer a question §6.4 has not asked yet.
/// </para>
/// <para>
/// <strong>And it is never handed out as a <see cref="HashSet{T}"/>.</strong> .NET
/// randomises string hashing per process, so a set's enumeration order is not
/// stable between two launches of the game even for identical contents — a draw
/// over it would be irreproducible from the same seed and the same save, and
/// §5.1's serialisation would churn for nothing. The set is kept for lookups and
/// <see cref="SeenInOrder"/> is the only view anyone iterates, draws over or
/// saves.
/// </para>
/// Engine-free and deterministic, like everything else under <c>Logic/</c>.
/// </summary>
public sealed class CampaignState
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>
    /// Every enchantment id the campaign has met, sorted with
    /// <see cref="StringComparer.Ordinal"/>: the same order on every machine and
    /// on every launch, which is what an offer draw and a save both need.
    /// </summary>
    public IReadOnlyList<string> SeenInOrder
    {
        get
        {
            var ids = _seen.ToArray();
            Array.Sort(ids, StringComparer.Ordinal);
            return ids;
        }
    }

    /// <summary>Whether <paramref name="id"/> has been met — the lookup the set itself is kept for.</summary>
    public bool HasSeen(string id) => _seen.Contains(id);

    /// <summary>
    /// A weapon entering the party's hands: every entry it carries has now been
    /// met, its innate and its souls alike. Nothing is ever removed, and meeting
    /// the same entry twice is not an event.
    /// </summary>
    public void See(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        foreach (var entry in weapon.Enchantments)
            _seen.Add(entry.Id);
    }

    /// <summary>
    /// Everything <paramref name="member"/> is carrying, met at once — the same
    /// trigger as a pickup, read honestly.
    /// <para>
    /// A party does not find its first weapons on the floor: it is handed them by
    /// <see cref="PartyMemberState.From"/> off the roster. Without this the only
    /// entries the campaign would ever meet are the ones taken off a corpse, and a
    /// party starting with <c>staff_of_renewal</c>, <c>wand_of_the_nova</c> or the
    /// Efficiency dagger would reach §6.4's enchanter to be told it cannot offer
    /// <c>regeneration</c>, an element or <c>vampiric</c> — entries it has been
    /// carrying since turn 0. They are in a bag; how they got there is not this
    /// set's question.
    /// </para>
    /// </summary>
    public void See(PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(member);
        foreach (var weapon in member.Inventory)
            if (weapon is not null)
                See(weapon);
    }
}
