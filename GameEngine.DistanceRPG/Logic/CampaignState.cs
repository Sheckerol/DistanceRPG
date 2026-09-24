namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The campaign (§6.7): what lives <em>outside</em> any one dungeon and outlasts
/// every visit — the party, the permanent half of the game — and the visit in
/// progress, the renewable half, which is null between runs. §6.7's "a save with
/// no dungeon in progress is now a valid state" is exactly a campaign whose
/// <see cref="Visit"/> is null.
/// <para>
/// The wiring runs one way, campaign to visit: the campaign builds the
/// <see cref="DungeonState"/> and drops it on leaving, and the visit never holds
/// the campaign. Floors, rows, fog and the turn clock are the visit's and go
/// with it (§4.1: "leaving the dungeon discards all of it"); the members, what
/// they carry and what they have met are the campaign's and stay.
/// </para>
/// <para>
/// And what the campaign has met, as against what a run is carrying: the set of
/// enchantment ids a weapon has brought into the party's hands (§3.3). It is
/// save data (§5.1) and it is the one collection that only ever grows — §6.4's
/// enchanter offers "what you have seen", and this is the list it will be
/// reading. It is campaign-wide, never per floor or per visit: an entry met on
/// floor 2 is still met after the party walks out, and on every visit after.
/// </para>
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
/// <see cref="SeenInOrder"/> is the only view of it anyone iterates, draws over
/// or saves. The party is handed out the same way, as a list in roster order
/// (<see cref="Party"/>) whose order is the roster file's and never a hash's.
/// </para>
/// Engine-free and deterministic, like everything else under <c>Logic/</c>: it
/// rolls nothing — a visit's seed is handed to <see cref="Enter"/> by whoever
/// enters.
/// </summary>
public sealed class CampaignState
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<PartyMemberState> _party;

    /// <summary>A campaign with no party: what the seen set alone is tested on. It holds nothing to send into a dungeon.</summary>
    public CampaignState()
        : this([])
    {
    }

    /// <summary>
    /// A campaign holding <paramref name="party"/>, in roster order: what a test
    /// builds around its fixture members, and what Phase 5's load restores. It
    /// meets nothing on its own — a loaded campaign's seen set is the saved one;
    /// <see cref="Start"/> is where a new campaign meets its loadout.
    /// </summary>
    /// <exception cref="ArgumentException">The party names a null member.</exception>
    public CampaignState(IReadOnlyList<PartyMemberState> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.Any(member => member is null))
            throw new ArgumentException("The party names a null member.", nameof(party));
        _party = Array.AsReadOnly(party.ToArray());
    }

    /// <summary>
    /// A new campaign (§2.1, §5.9): the roster of <paramref name="content"/> built
    /// once, in file order — each member by <see cref="PartyMemberState.From"/>,
    /// colour by position — and everything they carry met at once (§3.3: the
    /// loadout enters the party's hands the moment it is handed over). Built
    /// once for the whole campaign: every visit is made by these same members,
    /// who carry home whatever they carry. They stand nowhere yet; entering a
    /// dungeon places them.
    /// </summary>
    /// <param name="content">The content whose roster starts the campaign — in the game, the current content, whose catalogue <see cref="PartyMemberState.From"/> instantiates the weapons from.</param>
    public static CampaignState Start(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var roster = content.Party.All;
        var members = new PartyMemberState[roster.Count];
        for (int i = 0; i < roster.Count; i++)
            members[i] = PartyMemberState.From(roster[i], colorIndex: i, x: 0f, y: 0f);

        var campaign = new CampaignState(members);
        foreach (var member in members)
            campaign.See(member);
        return campaign;
    }

    /// <summary>The party, in roster order: the permanent half of the game, the same members on every visit.</summary>
    public IReadOnlyList<PartyMemberState> Party => _party;

    /// <summary>The visit in progress — the renewable half — or null between runs.</summary>
    public DungeonState? Visit { get; private set; }

    /// <summary>
    /// Send the party into the dungeon <paramref name="dungeonId"/>, entered on
    /// <paramref name="mapSeed"/>: every member rests first
    /// (<see cref="PartyMemberState.Rest"/> — alive, full, shed, a fresh budget),
    /// then the visit is built (<see cref="DungeonState.Enter"/>) — every floor
    /// generated afresh, the party at the first floor's arrival — and held as
    /// <see cref="Visit"/>.
    /// <para>
    /// Rolls nothing itself: the seed is the caller's, rolled per entry, which is
    /// what re-rolls a failed dungeon's depth, and the same seed is the same
    /// dungeon. One visit at a time — the campaign never holds two, so the one in
    /// progress has to be left first.
    /// </para>
    /// </summary>
    /// <returns>The new visit, which is also <see cref="Visit"/>.</returns>
    /// <exception cref="InvalidOperationException">A visit is already in progress, or the campaign has no party to send.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="dungeonId"/> names no dungeon in the current content.</exception>
    public DungeonState Enter(string dungeonId, long mapSeed)
    {
        ArgumentNullException.ThrowIfNull(dungeonId);
        if (Visit is not null)
            throw new InvalidOperationException(
                $"A visit to '{Visit.Def.Id}' is in progress: the campaign holds one visit at a time, and it has to be left before another begins.");
        if (_party.Count == 0)
            throw new InvalidOperationException("The campaign has no party to send into a dungeon.");

        var def = GameContent.Current.Dungeons[dungeonId];
        foreach (var member in _party)
            member.Rest();

        Visit = DungeonState.Enter(def, mapSeed, _party);
        return Visit;
    }

    /// <summary>
    /// Leave the dungeon and discard the visit whole (§4.1): its floors, their
    /// rows and every <see cref="EnemyState.DefeatCount"/> farmed on them, their
    /// fog, its clock. The next entry regenerates every floor fresh, so a farm
    /// never outlives the visit it was built in. What the party carries and what
    /// the campaign has met stay. Nothing to do between runs.
    /// </summary>
    public void Abandon() => Visit = null;

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
