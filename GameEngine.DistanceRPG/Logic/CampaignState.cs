namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One line of the hub's dungeon list (§4.5, <see cref="CampaignState.Listings"/>):
/// a dungeon as the between-runs screen offers it. Plain numbers read off the
/// campaign, so the screen states them and decides nothing.
/// </summary>
/// <param name="Def">The dungeon: its name and its theme are what the line names.</param>
/// <param name="Selectable">Whether it can be entered now: <see cref="CampaignState.Enter"/> would take it between runs, its lock lifted or never written, or it is the early offer.</param>
/// <param name="RunsUntilAvailable">
/// How many runs that count (§6.3) until its lock lifts:
/// <c>LockedUntilRun − RunCounter</c> for a locked dungeon — "available in 4
/// runs" — and 0 for one that can be entered now, the early offer included.
/// </param>
/// <param name="Early">
/// Whether it is the early offer (<see cref="CampaignState.EarlyOffer"/>): every
/// dungeon is locked, and this one, whose lock lifts soonest, can be entered
/// anyway. Provisional, as that offer is.
/// </param>
public sealed record DungeonListing(DungeonDef Def, bool Selectable, int RunsUntilAvailable, bool Early);

/// <summary>
/// Where a weapon the party owns is, between runs (§6.2, Phase 6a.2): a member's
/// slot — <see cref="MemberId"/> and the slot, 0 the hand and the rest the bag —
/// or the stable, <see cref="MemberId"/> null and <see cref="Index"/> its place in
/// <see cref="CampaignState.Stable"/>, where <c>Stable.Count</c> is the stable's
/// end: where a weapon put there goes. What the hub's moves name
/// (<see cref="CampaignState.Move"/>).
/// </summary>
/// <param name="MemberId">The member whose slot it is, by roster id, or null for the stable.</param>
/// <param name="Index">The slot, or the place in the stable.</param>
public readonly record struct WeaponPlace(string? MemberId, int Index)
{
    /// <summary>A place in the stable: <c>Stable.Count</c> is its end.</summary>
    public static WeaponPlace InTheStable(int index) => new(null, index);

    /// <summary>Whether it is the stable's, rather than a member's slot.</summary>
    public bool IsStable => MemberId is null;
}

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
/// save data (§5.1) and it is the one collection that only ever grows — §6.2's
/// enchanter offers from it (<see cref="Enchanter.Offers"/>), less the souls and
/// the kinds that do nothing yet. It is campaign-wide, never per floor or per
/// visit: an entry met on floor 2 is still met after the party walks out, and on
/// every visit after.
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
/// here instead would make the set answer the enchanter's question for it.
/// </para>
/// <para>
/// <strong>And it is never handed out as a <see cref="HashSet{T}"/>.</strong> .NET
/// randomises string hashing per process, so a set's enumeration order is not
/// stable between two launches of the game even for identical contents — a draw
/// over it would be irreproducible from the same seed and the same save, and
/// §5.1's serialisation would churn for nothing. The set is kept for lookups and
/// <see cref="SeenInOrder"/> is the only view of it anyone iterates, draws over
/// or saves. The party is handed out the same way, as a list in roster order
/// (<see cref="Party"/>) whose order is the roster's and never a hash's,
/// and so are the run's-end records below — the locks, the cleared set and the
/// retired set — each as an ordinal-sorted view (<see cref="LockedInOrder"/>,
/// <see cref="ClearedInOrder"/>, <see cref="RetiredInOrder"/>).
/// </para>
/// <para>
/// <strong>What a run carries out</strong> (§4.4, §4.5, §6.3): a run ends by the
/// way out or by a wipe (<see cref="EndRun"/>), and all that outlives it is
/// numbers — the run counter a tick advances (<see cref="RunCounter"/>), the lock
/// a clear writes against that counter (<see cref="LockedUntilRun"/>), the fact
/// of the clear (<see cref="HasCleared"/>), the tutorial's retirement
/// (<see cref="IsRetired"/>) — plus whatever is in the bags, and the XP the
/// party kept. The dungeon itself is discarded whole. Between runs, the hub's
/// dungeon list is read off those numbers (<see cref="Listings"/>).
/// </para>
/// <para>
/// <strong>And the stable</strong> (§6.2, Phase 6a.2; PHASE 6a DECISION 7): the
/// weapons the party owns and is not carrying, in the order they were put there
/// (<see cref="Stable"/>), and unbounded. It changes by hand only between runs,
/// through the hub's one door (<see cref="Move"/>), which never leaves a member
/// carrying no weapon and arms a hand it empties from the bag; a load brings it
/// back through <see cref="Restore"/>. A new campaign's is empty, and so is a
/// save's from before it.
/// </para>
/// <para>
/// <strong>And the enchanter's queue</strong> (§6.2, §6.7, Phase 6a.3a): the
/// deposits in the shop, in deposit order (<see cref="AtTheEnchanter"/>), and how
/// many deposits the campaign has ever made (<see cref="DepositsMade"/>), the next
/// one's ordinal. A deposit and a take-back change it by hand, between runs,
/// through the hub's doors (<see cref="Enqueue"/>, <see cref="TakeOut"/>), which
/// the enchanter calls (<see cref="Enchanter"/>); every run's end works it, the
/// visit gone, and brings a finished service's weapon back to the end of the stable
/// (<see cref="Return"/>) — a transfer's two, its source after it (Phase 6a.6); a
/// load brings it back through <see cref="Restore"/>.
/// </para>
/// Engine-free and deterministic, like everything else under <c>Logic/</c>: it
/// rolls nothing — a visit's seed is handed to <see cref="Enter"/> by whoever
/// enters.
/// </summary>
public sealed class CampaignState
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<PartyMemberState> _party;

    // The run's-end records (§4.5, §6.7), keyed by dungeon id: the run each
    // cleared dungeon is locked until, the dungeons cleared at least once, and
    // the dungeons retired for good. Lookups only; the sorted views are what
    // anyone iterates or saves. Only a run's end writes them, and RunCounter —
    // and a load, which brings back what a run's end wrote through its own
    // internal door (Restore, Phase 5.4), never a public setter or a public
    // Retire.
    private readonly Dictionary<string, int> _lockedUntilRun = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cleared = new(StringComparer.Ordinal);
    private readonly HashSet<string> _retired = new(StringComparer.Ordinal);

    // The stable (§6.2): the weapons owned and not carried, in the order they
    // were put there. Written by the hub's moves between runs (Move), by the
    // enchanter's doors — a deposit takes a weapon out (Enqueue), a take-back and a
    // finished service put one at its end (TakeOut, Return) — and by a load
    // (Restore) alone.
    private readonly List<Weapon> _stable = new();

    // The enchanter's queue (§6.2): the deposits in the shop, in deposit order.
    // Written by the hub's doors between runs (Enqueue, TakeOut), by the run's
    // end's tick (Return) and by a load (Restore) alone.
    private readonly List<Deposit> _enchanter = new();

    // The subscription Enter (or a load, through Restore) made on the visit in
    // progress's Wiped, taken off again at the run's end, so a discarded visit
    // keeps nothing of the campaign.
    private Action? _onWiped;

    /// <summary>A campaign with no party: what the seen set alone is tested on. It holds nothing to send into a dungeon.</summary>
    public CampaignState()
        : this([])
    {
    }

    /// <summary>
    /// A campaign holding <paramref name="party"/>, in roster order: what a test
    /// builds around its fixture members, and what a load builds its campaign on
    /// (<see cref="Restore"/>). It meets nothing on its own — a loaded campaign's
    /// seen set is the saved one; <see cref="Start"/> is where a new campaign
    /// meets its loadout.
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
    /// once, in roster order — each member by <see cref="PartyMemberState.From"/>,
    /// colour by position — and everything they carry met at once (§3.3: the
    /// loadout enters the party's hands the moment it is handed over). Built
    /// once for the whole campaign: every visit is made by these same members,
    /// who carry home whatever they carry. They stand nowhere yet; entering a
    /// dungeon places them.
    /// </summary>
    /// <param name="content">
    /// The content whose roster starts the campaign, which must be
    /// <see cref="GameContent.Current"/>: a member is built under the current
    /// content — <see cref="PartyMemberState.From"/> instantiates its weapons
    /// from the current catalogue and sizes its bag by the current tuning — so a
    /// roster read from any other content would come out as rows of one content
    /// carrying another's weapons.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    public static CampaignState Start(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!ReferenceEquals(content, GameContent.Current))
            throw new ArgumentException(
                "A campaign starts under the current content: its members' weapons come from GameContent.Current's catalogue and their bags from its tuning, so the roster must be the current content's too.",
                nameof(content));

        var roster = content.Party.All;
        var members = new PartyMemberState[roster.Count];
        for (int i = 0; i < roster.Count; i++)
            members[i] = PartyMemberState.From(roster[i], colorIndex: i, x: 0f, y: 0f);

        var campaign = new CampaignState(members);
        foreach (var member in members)
            campaign.See(member);
        return campaign;
    }

    /// <summary>
    /// A campaign as a save describes it (§5.1, §6.7): the party as the load
    /// rebuilt it, what it has met, the run counter, every lock a clear wrote — an
    /// early offer's rewrite as it stands — the dungeons cleared and retired, how
    /// the last run ended, the visit in progress, or none between runs, the
    /// stable, in its order, and the enchanter's queue in deposit order with how
    /// many deposits were ever made — each filled here, before the visit is held,
    /// never through the hub's doors (<see cref="Move"/>, <see cref="Enqueue"/>), so
    /// a mid-run save with a stable and a queue comes back whole though those doors
    /// are shut until its run ends. The
    /// one door the run's-end records come back through: internal, so there is
    /// still no public setter on the counter and no public <c>Retire</c> (a run's
    /// end is the only other writer). Nothing is re-derived and nothing is met: a
    /// loaded campaign has met what it had met, which is not what its bags hold now.
    /// <para>
    /// <strong>A visit comes back held as <see cref="Enter"/> holds one</strong>:
    /// its wipe is wired to the run's end (<see cref="DungeonState.Wiped"/> →
    /// <see cref="EndRun"/>), so a party that falls after a load ends its run in
    /// Logic exactly as one that never left. It is the load's to have built
    /// (<see cref="DungeonState.Restore"/>), on these members.
    /// </para>
    /// <para>
    /// The ids are the load's to have checked against the content
    /// (<see cref="SaveGame.Restore"/>): this takes them as they are, and refuses
    /// only what no campaign can hold — a negative counter or lock, a dungeon
    /// locked twice, a visit with nobody to make it, a stable or a queue that
    /// holds a null or one weapon twice between them, and a queue out of deposit
    /// order or holding an ordinal the deposits made have not reached.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">The party names a null member, an id is null, a dungeon is locked twice, a visit is handed in with no party, the stable or the queue holds a null or a weapon twice between them, or the queue is out of deposit order or holds an ordinal not below the deposits made.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The counter, a lock or the deposits made is negative.</exception>
    internal static CampaignState Restore(IReadOnlyList<PartyMemberState> party, IReadOnlyList<string> seen, int runCounter,
        IReadOnlyList<(string DungeonId, int LockedUntilRun)> locks, IReadOnlyList<string> cleared, IReadOnlyList<string> retired,
        RunResult? lastResult, DungeonState? visit, IReadOnlyList<Weapon> stable, IReadOnlyList<Deposit>? enchanter = null, int depositsMade = 0)
    {
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(locks);
        ArgumentNullException.ThrowIfNull(cleared);
        ArgumentNullException.ThrowIfNull(retired);
        ArgumentNullException.ThrowIfNull(stable);
        ArgumentOutOfRangeException.ThrowIfNegative(runCounter);
        if (seen.Concat(cleared).Concat(retired).Any(id => id is null))
            throw new ArgumentException("A campaign's records name their entries by id, and one is null.");
        if (stable.Any(weapon => weapon is null))
            throw new ArgumentException("The stable holds weapons, and one is null.", nameof(stable));
        if (stable.Distinct(ReferenceEqualityComparer.Instance).Count() != stable.Count)
            throw new ArgumentException("The stable holds a weapon twice: a weapon is in one place.", nameof(stable));
        enchanter ??= [];
        ArgumentOutOfRangeException.ThrowIfNegative(depositsMade);
        if (enchanter.Any(deposit => deposit?.Weapon is null))
            throw new ArgumentException("The queue holds deposits of weapons, and one is null.", nameof(enchanter));
        var queued = enchanter.SelectMany(deposit => deposit.Source is { } source ? new[] { deposit.Weapon, source } : [deposit.Weapon]).ToArray();
        if (stable.Concat(queued).Distinct(ReferenceEqualityComparer.Instance).Count() != stable.Count + queued.Length)
            throw new ArgumentException("The stable and the queue hold a weapon twice between them: a weapon is in one place.", nameof(enchanter));
        for (int i = 0; i < enchanter.Count; i++)
            if (enchanter[i].Ordinal < (i == 0 ? 0 : enchanter[i - 1].Ordinal + 1) || enchanter[i].Ordinal >= depositsMade)
                throw new ArgumentException($"Deposit {i}'s ordinal, {enchanter[i].Ordinal}, is out of deposit order or not below the {depositsMade} deposits made.", nameof(enchanter));

        var campaign = new CampaignState(party)
        {
            RunCounter = runCounter,
            LastResult = lastResult,
        };
        campaign._seen.UnionWith(seen);
        foreach (var (dungeonId, until) in locks)
        {
            ArgumentNullException.ThrowIfNull(dungeonId, nameof(locks));
            ArgumentOutOfRangeException.ThrowIfNegative(until, nameof(locks));
            if (!campaign._lockedUntilRun.TryAdd(dungeonId, until))
                throw new ArgumentException($"'{dungeonId}' is locked twice: a dungeon has one lock, the last a clear wrote.", nameof(locks));
        }
        campaign._cleared.UnionWith(cleared);
        campaign._retired.UnionWith(retired);
        campaign._stable.AddRange(stable);   // not through Move: the hub's door is the player's, and shut while a visit is held
        campaign._enchanter.AddRange(enchanter);   // nor through Enqueue, for the same reason: before the visit is held
        campaign.DepositsMade = depositsMade;
        if (visit is not null)
        {
            if (campaign._party.Count == 0)
                throw new ArgumentException("A visit in progress needs a party to be making it, and the campaign has none.", nameof(visit));
            campaign.Hold(visit);
        }
        return campaign;
    }

    /// <summary>The party, in roster order: the permanent half of the game, the same members on every visit.</summary>
    public IReadOnlyList<PartyMemberState> Party => _party;

    /// <summary>The visit in progress — the renewable half — or null between runs.</summary>
    public DungeonState? Visit { get; private set; }

    /// <summary>
    /// The campaign's run counter (§6.3): how many runs have <em>counted</em> —
    /// a boss kill's success tick, or a failure's mercy tick for having got
    /// <see cref="Tuning.MercyFloor"/> floors down. Entering counts nothing, and
    /// failing shallow counts nothing. It is the one clock every cooldown is
    /// counted on (§4.3: "counted by the same events that tick the enchanter,
    /// so there is no second clock"), and the one the service clocks of §6.3
    /// will consume.
    /// </summary>
    public int RunCounter { get; private set; }

    /// <summary>How the last run ended — what the run's summary reads — or null before any has.</summary>
    public RunResult? LastResult { get; private set; }

    /// <summary>
    /// A run has ended (§6.7's run-outcome signal): raised once per run, by
    /// <see cref="EndRun"/>, after everything the run carried out is written and
    /// the visit is gone. Whoever plays the run shows its summary from it —
    /// whichever way it ended, by the way out or by the wipe that ended it in
    /// Logic.
    /// </summary>
    public event Action<RunResult>? RunEnded;

    /// <summary>
    /// Send the party into the dungeon <paramref name="dungeonId"/>, entered on
    /// <paramref name="mapSeed"/>: every member rests first
    /// (<see cref="PartyMemberState.Rest"/> — alive, full, shed, a fresh budget,
    /// an empty hand armed from the bag), then the visit is built
    /// (<see cref="DungeonState.Enter"/>) — every floor generated afresh, the
    /// party at the first floor's arrival, its XP taken at the door — and held as
    /// <see cref="Visit"/>, its wipe wired to the run's end
    /// (<see cref="DungeonState.Wiped"/> → <see cref="EndRun"/>).
    /// <para>
    /// Rolls nothing itself, and ticks nothing (§6.3: entering the dungeon ticks
    /// nothing): the seed is the caller's, rolled per entry, which is what
    /// re-rolls a failed dungeon's depth, and the same seed is the same dungeon.
    /// One visit at a time — the campaign never holds two, so the one in progress
    /// has to end first.
    /// </para>
    /// <para>
    /// <strong>A locked or retired dungeon is refused</strong> (§4.3): a cleared
    /// dungeon until its lock lifts (<see cref="IsSelectable"/>), the tutorial for
    /// good once beaten. The one exception is the early offer
    /// (<see cref="EarlyOffer"/>) — with every dungeon locked, the one whose lock
    /// lifts soonest may be entered, and entering it sets its lock to the run
    /// counter now, so it stands as a lifted lock and nothing else moves. The
    /// hub's gate — the tutorial alone until it is beaten — is what the dungeon
    /// list offers (<see cref="Listings"/>), which the between-runs screen
    /// presents; entering asks only whether the dungeon is locked or retired.
    /// </para>
    /// </summary>
    /// <returns>The new visit, which is also <see cref="Visit"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// A visit is already in progress, the campaign has no party to send, or the
    /// dungeon is retired, or locked and not the early offer. Nothing is rested and
    /// nothing is entered.
    /// </exception>
    /// <exception cref="KeyNotFoundException"><paramref name="dungeonId"/> names no dungeon in the current content.</exception>
    public DungeonState Enter(string dungeonId, long mapSeed)
    {
        ArgumentNullException.ThrowIfNull(dungeonId);
        if (Visit is not null)
            throw new InvalidOperationException(
                $"A visit to '{Visit.Def.Id}' is in progress: the campaign holds one visit at a time, and its run has to end before another begins.");
        if (_party.Count == 0)
            throw new InvalidOperationException("The campaign has no party to send into a dungeon.");

        var def = GameContent.Current.Dungeons[dungeonId];
        if (IsRetired(def.Id))
            throw new InvalidOperationException(
                $"'{def.Id}' is retired: it was beaten and is gone for good (§4.3), and cannot be entered again.");
        bool early = false;
        if (!IsSelectable(def))
        {
            if (EarlyOffer() != def.Id)
                throw new InvalidOperationException(
                    $"'{def.Id}' is locked until run {LockedUntilRun(def.Id)} and the run counter stands at {RunCounter}: a cleared dungeon comes back after its cooldown (§4.3).");
            early = true;
        }

        foreach (var member in _party)
            member.Rest();

        var visit = DungeonState.Enter(def, mapSeed, _party);
        if (early)
            _lockedUntilRun[def.Id] = RunCounter;   // the lock lifted now: the entry stays, and nothing else moves

        Hold(visit);
        return visit;
    }

    /// <summary>
    /// Hold <paramref name="visit"/> as the visit in progress, its wipe wired to
    /// the run's end: what <see cref="Enter"/> does with a visit it has just built,
    /// and what a load does with one it has restored (<see cref="Restore"/>).
    /// </summary>
    private void Hold(DungeonState visit)
    {
        _onWiped = () =>
        {
            if (ReferenceEquals(Visit, visit))
                EndRun(RunEnding.Wiped);
        };
        visit.Wiped += _onWiped;
        Visit = visit;
    }

    /// <summary>
    /// End the run in progress (§4.4's three outcomes) and carry out of it what
    /// outlives it; the visit is discarded whole — every floor, every row and
    /// every farm on it, its fog, its clock, its snapshot (§4.1) — and
    /// <see cref="RunEnded"/> is raised with the result.
    /// <list type="table">
    /// <listheader><term>Ending</term><description>What it means</description></listheader>
    /// <item><term>Out, the boss dead</term><description>
    /// <see cref="RunOutcome.BossKill"/>: the spoils in the bags are kept, the XP
    /// is kept, the run ticks once — at any depth (§4.3: the tick rewards the
    /// achievement, the drop the depth).</description></item>
    /// <item><term>Out, the boss standing</term><description>
    /// <see cref="RunOutcome.Retreat"/>: nothing dropped to keep (nothing drops
    /// while the dungeon resurrects), the XP kept, and a mercy tick only from
    /// <see cref="Tuning.MercyFloor"/> down.</description></item>
    /// <item><term>Wiped</term><description>
    /// <see cref="RunOutcome.Death"/>: the spoils still in a bag are taken out of
    /// it and lost, each XP pool loses <see cref="Tuning.DeathXpLossPercent"/> of
    /// its gain since the entry snapshot, and the run ticks as a boss kill would
    /// if the boss was down, or as a failure otherwise.</description></item>
    /// </list>
    /// <para>
    /// <strong>The clear.</strong> A run with the boss dead — out or wiped (§6.3:
    /// "killed the boss, died on the way out" still ticks) — is a clear: its tick
    /// lands first, then the dungeon is recorded cleared for good
    /// (<see cref="HasCleared"/>) and locked until
    /// <c>RunCounter + LockRuns</c> (<see cref="DungeonState.LockRuns"/>: as many
    /// runs as the floor its boss was beaten on), so "available in <em>n</em>
    /// runs" is the lock less the counter and a floor-5 clear is back after five
    /// more ticking runs. A dungeon that retires on its clear — the tutorial — is
    /// retired instead and never written to the locks (§4.3). Failing locks
    /// nothing.
    /// </para>
    /// <para>
    /// <strong>The enchanter's clock</strong> (§6.3): once the visit is gone, the
    /// run's end works the shop (<see cref="Enchanter.Tick"/>) — a run that counted
    /// works the newest deposits a run on and brings back what they finish, each
    /// with its improvement roll made on the run's map seed, one that did not works
    /// nothing — and the result carries what it did
    /// (<see cref="RunResult.Services"/>).
    /// </para>
    /// <para>
    /// <strong>Spoils, not gear</strong> (§4.4): only the run's spoils
    /// (<see cref="DungeonState.Spoils"/>) still in some member's bag count,
    /// found by reference — what the party carried in is never taken, and a spoil
    /// put down in the dungeon is lost with it either way. Nobody is rested here:
    /// the next entry rests them, so a defeat still shows the fallen as fallen.
    /// </para>
    /// </summary>
    /// <param name="ending">How it ended.</param>
    /// <returns>The run's result, which is also <see cref="LastResult"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// No run is in progress; or <see cref="RunEnding.Exited"/> while the party
    /// could not take the first floor's way out — below the first floor (there is
    /// no shortcut exit, §4.3), outside its own phase, or not gathered; or
    /// <see cref="RunEnding.Wiped"/> with a member still standing.
    /// </exception>
    public RunResult EndRun(RunEnding ending)
    {
        var visit = Visit ?? throw new InvalidOperationException("No run is in progress: there is nothing to end.");
        RequireEnding(visit, ending);

        var tuning = GameContent.Current.Tuning;
        var def = visit.Def;
        bool bossDown = visit.BossDefeated;
        int deepest = visit.DeepestFloorIndex + 1;

        // The tick first (§6.3): a win ticks once at any depth; a failure only deep.
        bool mercy = !bossDown && deepest >= tuning.MercyFloor;
        bool tick = bossDown || mercy;
        if (tick)
            RunCounter++;

        // Then the clear, on the counter the tick has already moved.
        int? lockedUntil = null;
        bool retired = false;
        if (bossDown)
        {
            _cleared.Add(def.Id);
            if (def.RetiresOnClear)
            {
                Retire(def.Id);
                retired = true;
            }
            else
            {
                lockedUntil = RunCounter + visit.LockRuns;
                _lockedUntilRun[def.Id] = lockedUntil.Value;
            }
        }

        // The spoils still in a bag: kept on the way out, taken out of the bags by a wipe.
        var carried = SpoilsInBags(visit);
        IReadOnlyList<Weapon> kept = [], lost = [];
        if (ending == RunEnding.Exited)
        {
            kept = carried.Select(c => c.Spoil).ToArray();
        }
        else
        {
            foreach (var (member, slot, _) in carried)
                member.Inventory[slot] = null;
            lost = carried.Select(c => c.Spoil).ToArray();
        }

        IReadOnlyList<XpClawback> clawbacks = ending == RunEnding.Wiped
            ? visit.EntrySnapshot.ClawBack(_party, tuning.DeathXpLossPercent)
            : [];

        // The visit goes, and nothing of the campaign stays wired to it — but its
        // map seed, which a finishing service's improvement roll is keyed by.
        long mapSeed = visit.MapSeed;
        visit.Wiped -= _onWiped;
        _onWiped = null;
        Visit = null;

        // The enchanter's clock (§6.3), on every run's end with the visit gone, so
        // a finished service's weapon goes back to the stable through the campaign's
        // own path: a run that counted works the newest deposits a run on, and a
        // service it finishes rolls its improvement on the run's seed; one that did
        // not works nothing, and says so.
        var services = Enchanter.Tick(this, mapSeed, counted: tick);

        var result = new RunResult(def.Id,
            ending == RunEnding.Wiped ? RunOutcome.Death : bossDown ? RunOutcome.BossKill : RunOutcome.Retreat,
            bossDown, deepest, deepest >= visit.BossFloor ? visit.BossFloor : null,
            ServiceTick: tick, MercyTick: mercy, RunCounter, lockedUntil, retired,
            kept, lost, clawbacks, services.Count > 0 ? services : null);
        LastResult = result;
        RunEnded?.Invoke(result);
        return result;
    }

    /// <summary>The door the run is ending by has to be one it can end by: the first floor's way out, taken as the stairs are, or a wipe with nobody standing.</summary>
    private void RequireEnding(DungeonState visit, RunEnding ending)
    {
        switch (ending)
        {
            case RunEnding.Exited:
                if (visit.FloorIndex != 0)
                    throw new InvalidOperationException(
                        $"The party is on floor {visit.FloorIndex + 1}: there is no shortcut exit (§4.3), and the only way out is the first floor's up-stairs.");
                if (visit.CanUse(new StairUse(Stair.Exit, visit.Current.UpStairs), _party) is { } refusal)
                    throw new InvalidOperationException($"The party cannot take the way out now: {refusal}.");
                return;

            case RunEnding.Wiped:
                if (_party.Any(member => member.Alive))
                    throw new InvalidOperationException("A wipe is the whole party down, and a member is still standing.");
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(ending), ending, "Not a RunEnding.");
        }
    }

    /// <summary>
    /// Every spoil of <paramref name="visit"/> still in some member's bag, in the
    /// order it was taken, with where it sits — found by reference, so a swap
    /// between slots moves nothing that matters and a spoil that has left every
    /// bag is simply not found.
    /// </summary>
    private List<(PartyMemberState Member, int Slot, Weapon Spoil)> SpoilsInBags(DungeonState visit)
    {
        var found = new List<(PartyMemberState, int, Weapon)>();
        foreach (var spoil in visit.Spoils)
        {
            foreach (var member in _party)
            {
                int slot = Array.FindIndex(member.Inventory, carried => ReferenceEquals(carried, spoil));
                if (slot < 0) continue;
                found.Add((member, slot, spoil));
                break;
            }
        }
        return found;
    }

    /// <summary>
    /// The run <paramref name="dungeonId"/> is locked until (§4.5): a clear
    /// writes <c>RunCounter + LockRuns</c> here, and the dungeon is selectable
    /// again once the run counter reaches it. <strong>Absent reads 0</strong> —
    /// available — which is every dungeon never cleared and every dungeon that
    /// retires rather than locks (the tutorial is never written here).
    /// </summary>
    public int LockedUntilRun(string dungeonId)
    {
        ArgumentNullException.ThrowIfNull(dungeonId);
        return _lockedUntilRun.GetValueOrDefault(dungeonId);
    }

    /// <summary>
    /// Every lock a clear has written, as (dungeon id, run it is locked until),
    /// sorted by id with <see cref="StringComparer.Ordinal"/>: the view a save
    /// stores and anything iterates. A lock that has lifted stays, reading a run
    /// already reached.
    /// </summary>
    public IReadOnlyList<(string DungeonId, int LockedUntilRun)> LockedInOrder
        => _lockedUntilRun
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => (DungeonId: entry.Key, LockedUntilRun: entry.Value))
            .ToArray();

    /// <summary>
    /// Whether <paramref name="dungeonId"/> has been cleared at least once — its
    /// boss killed on some run (§6.7: "the set of dungeons cleared at least
    /// once"). Its own record, never read back off the locks: a lock lifts and
    /// the early offer rewrites one, and neither un-clears anything. The tutorial
    /// is in it once beaten; what counts it is for its readers to decide.
    /// </summary>
    public bool HasCleared(string dungeonId)
    {
        ArgumentNullException.ThrowIfNull(dungeonId);
        return _cleared.Contains(dungeonId);
    }

    /// <summary>Every dungeon cleared at least once, sorted with <see cref="StringComparer.Ordinal"/>.</summary>
    public IReadOnlyList<string> ClearedInOrder => Sorted(_cleared);

    /// <summary>
    /// Whether <paramref name="dungeonId"/> is retired: beaten, and gone for good
    /// (§4.3 — the tutorial, "available exactly once"). A flag and never a lock.
    /// The hub opens when the tutorial is retired.
    /// </summary>
    public bool IsRetired(string dungeonId)
    {
        ArgumentNullException.ThrowIfNull(dungeonId);
        return _retired.Contains(dungeonId);
    }

    /// <summary>Every retired dungeon, sorted with <see cref="StringComparer.Ordinal"/>.</summary>
    public IReadOnlyList<string> RetiredInOrder => Sorted(_retired);

    /// <summary>
    /// Retire <paramref name="dungeonId"/> for good (§4.3): what a clear does to a
    /// dungeon that retires on its clear, in place of a lock — a flag, never a
    /// cooldown entry, so it is never written to <see cref="LockedUntilRun"/>.
    /// </summary>
    private void Retire(string dungeonId) => _retired.Add(dungeonId);

    /// <summary>
    /// Whether <paramref name="def"/> can be picked now (§4.5): not retired, and
    /// <c>RunCounter ≥ LockedUntilRun</c> — absent reads 0, so a dungeon never
    /// cleared is always selectable, and a failed one straight away (§4.3:
    /// failing locks nothing).
    /// </summary>
    public bool IsSelectable(DungeonDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        return !IsRetired(def.Id) && RunCounter >= LockedUntilRun(def.Id);
    }

    /// <summary>
    /// The dungeon offered early when there is nothing else to enter, or null.
    /// <para>
    /// With every dungeon that is not retired locked, no run can be entered, so no
    /// run can tick and no lock can lift: a dead end. Four straight clears reach
    /// it for certain with the shipped four dungeons (locks of five runs or more
    /// outlast the three ticks after the first). So the dungeon whose lock lifts
    /// soonest is offered early — ties in the content's file order, never a
    /// dictionary's — and entering it (<see cref="Enter"/>) sets its lock to the
    /// run counter. It changes settled.md's "clearing a dungeon locks it for
    /// <c>bossFloor</c> runs" in this one corner, and the user kept it
    /// (settled.md, Phase 4 implementation pass).
    /// </para>
    /// Null whenever some dungeon not retired is selectable — the tutorial, until
    /// it is beaten, always is — and when nothing is left that is not retired.
    /// </summary>
    public string? EarlyOffer()
    {
        DungeonDef? soonest = null;
        foreach (var def in GameContent.Current.Dungeons.All)
        {
            if (IsRetired(def.Id)) continue;
            if (IsSelectable(def)) return null;
            if (soonest is null || LockedUntilRun(def.Id) < LockedUntilRun(soonest.Id))
                soonest = def;   // strictly sooner only: a tie keeps the earlier in file order
        }
        return soonest?.Id;
    }

    /// <summary>
    /// The dungeon the next run goes into when nobody chooses one: the tutorial
    /// until it is beaten, then the first selectable dungeon in the content's
    /// file order, and with every dungeon locked, the early offer
    /// (<see cref="EarlyOffer"/>). Never null: whoever begins the next run always
    /// has a dungeon it can enter. It is where the between-runs screen's choice
    /// starts, and it is always one of the <see cref="Listings"/> that can be
    /// entered.
    /// </summary>
    /// <exception cref="InvalidOperationException">No dungeon is left that is not retired: content with nothing but the tutorial, beaten.</exception>
    public string DefaultNext()
    {
        var dungeons = GameContent.Current.Dungeons;
        if (!IsRetired(dungeons.Tutorial.Id))
            return dungeons.Tutorial.Id;
        foreach (var def in dungeons.All)
            if (IsSelectable(def))
                return def.Id;
        return EarlyOffer()
            ?? throw new InvalidOperationException("No dungeon is left to enter: every dungeon in the content is retired.");
    }

    /// <summary>
    /// The hub's dungeon list (§4.5, §6.7): what the between-runs screen shows,
    /// one line a dungeon, in the content's file order — never a dictionary's.
    /// <list type="bullet">
    /// <item><strong>Before the tutorial is beaten, the tutorial alone</strong>
    /// (§4.3): it is the thing that opens the hub, so there is nowhere else to go
    /// yet, and failing it leaves it where it was.</item>
    /// <item><strong>After, every dungeon not retired</strong> — the tutorial is
    /// gone for good — each with whether it can be entered now and, when it
    /// cannot, how many runs that count until its lock lifts. <strong>A locked
    /// dungeon is listed, never hidden</strong> (§4.5: "a dungeon that silently
    /// vanishes reads as a bug and makes the rotation impossible to think
    /// about"), counting down on the same run counter the lock was written
    /// against.</item>
    /// <item><strong>With every dungeon locked</strong>, the one whose lock lifts
    /// soonest is listed as the early offer (<see cref="EarlyOffer"/>) and can be
    /// entered; every other line goes on counting down.</item>
    /// </list>
    /// A line can be entered exactly when <see cref="Enter"/> would take its
    /// dungeon between runs: its lock has lifted, or it is the early offer.
    /// Nothing here says how deep a dungeon goes: the depth is rolled at each
    /// entry and learned by reaching it (§4.3), so the list has none to give.
    /// Empty only when content leaves nothing that is not retired.
    /// </summary>
    public IReadOnlyList<DungeonListing> Listings()
    {
        var dungeons = GameContent.Current.Dungeons;
        var tutorial = dungeons.Tutorial;
        if (!IsRetired(tutorial.Id))
            return [Listing(tutorial, early: false)];   // the hub opens behind it: never locked, it retires instead

        string? early = EarlyOffer();
        var listings = new List<DungeonListing>();
        foreach (var def in dungeons.All)
            if (!IsRetired(def.Id))   // a retired dungeon is gone for good
                listings.Add(Listing(def, early: def.Id == early));
        return listings;
    }

    /// <summary><paramref name="def"/>'s line: the early offer can be entered with its lock standing; any other line can be entered once its lock has lifted, and counts down until it has.</summary>
    private DungeonListing Listing(DungeonDef def, bool early)
    {
        if (early)
            return new DungeonListing(def, Selectable: true, RunsUntilAvailable: 0, Early: true);
        bool selectable = IsSelectable(def);
        return new DungeonListing(def, selectable, selectable ? 0 : LockedUntilRun(def.Id) - RunCounter, Early: false);
    }

    private static IReadOnlyList<string> Sorted(HashSet<string> ids)
    {
        var sorted = ids.ToArray();
        Array.Sort(sorted, StringComparer.Ordinal);
        return sorted;
    }

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
    /// Efficiency dagger would reach §6.2's enchanter to be told it cannot offer
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

    // ── The stable (§6.2, Phase 6a.2) ────────────────────────────────────────

    /// <summary>
    /// The stable (§6.2): every weapon the party owns and is not carrying, in the
    /// order they were put there — a list, never a set, so the view a save stores
    /// and the hub lists is that order. Unbounded (PHASE 6a DECISION 7). Empty on
    /// a new campaign.
    /// </summary>
    public IReadOnlyList<Weapon> Stable => _stable.AsReadOnly();

    /// <summary>
    /// The weapon at <paramref name="place"/>: a member's slot's, which may be
    /// empty (null), or the stable's — its end holding nothing.
    /// </summary>
    /// <exception cref="ArgumentException">No member of the party has the id.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is not one the member has, or the place is past the stable's end.</exception>
    public Weapon? WeaponAt(WeaponPlace place)
    {
        if (place.IsStable)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(place.Index, nameof(place));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(place.Index, _stable.Count, nameof(place));
            return place.Index < _stable.Count ? _stable[place.Index] : null;
        }
        var bag = MemberOf(place).Inventory;
        ArgumentOutOfRangeException.ThrowIfNegative(place.Index, nameof(place));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(place.Index, bag.Length, nameof(place));
        return bag[place.Index];
    }

    /// <summary>
    /// The member a move of the weapon at <paramref name="from"/> to
    /// <paramref name="to"/> would leave carrying no weapon (PHASE 6a DECISION 7),
    /// or null when it leaves every member armed. Only the member it is taken
    /// from can be left with none, and only when nothing comes back in its place
    /// — the weapon put down where nothing lies, in another member's bag or at
    /// the stable's end — and it was the one weapon that member carried. A swap
    /// always hands one back, and a weapon put down in its own member's bag stays
    /// carried.
    /// </summary>
    /// <exception cref="ArgumentException">A member the places name is not in the party.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A place is not one the party or the stable has.</exception>
    public PartyMemberState? WouldDisarm(WeaponPlace from, WeaponPlace to)
    {
        if (from.IsStable || WeaponAt(from) is null || WeaponAt(to) is not null)
            return null;
        if (!to.IsStable && to.MemberId == from.MemberId)
            return null;
        var member = MemberOf(from);
        return member.Inventory.Count(carried => carried is not null) == 1 ? member : null;
    }

    /// <summary>
    /// Move a weapon the party owns, by hand between runs (§6.2; PHASE 6a
    /// DECISION 7): the hub's one door onto the stable and the bags. The weapon at
    /// <paramref name="from"/> goes to <paramref name="to"/>, and whatever lay
    /// there comes back to <paramref name="from"/> in its place — a swap: into a
    /// member's hand is how a weapon is equipped, from the stable or another bag.
    /// Put down where nothing lies — a member's empty slot, or the stable's end —
    /// it leaves its old place empty: a bag slot is left empty, and the stable
    /// closes up, so a weapon put at the stable's end from the stable moves there.
    /// A hand the move empties is armed from its bag, as the rest before a
    /// dungeon arms it (<see cref="PartyMemberState.ArmFromBag"/>). A move onto its
    /// own place moves nothing.
    /// <para>
    /// Refused, and nothing moves: during a visit — the stable and the bags are
    /// changed by hand only between runs — and a move that would leave a member
    /// carrying no weapon (<see cref="WouldDisarm"/>), which the hub asks first and
    /// says why. Nothing is met: every weapon moved was the party's already.
    /// </para>
    /// </summary>
    /// <returns>The weapon moved; the weapon it was swapped with, or null; and the weapon a hand the move emptied took up from the bag, or null.</returns>
    /// <exception cref="InvalidOperationException">A visit is in progress, <paramref name="from"/> holds no weapon, or the move would leave a member carrying none.</exception>
    /// <exception cref="ArgumentException">A member the places name is not in the party.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A place is not one the party or the stable has.</exception>
    internal (Weapon Moved, Weapon? Swapped, Weapon? Armed) Move(WeaponPlace from, WeaponPlace to)
    {
        if (Visit is { } visit)
            throw new InvalidOperationException(
                $"A visit to '{visit.Def.Id}' is in progress: the stable and the bags are changed by hand only between runs.");
        var moved = WeaponAt(from) ?? throw new InvalidOperationException($"Nothing lies at {from} to move.");
        var swapped = WeaponAt(to);
        if (WouldDisarm(from, to) is { } unarmed)
            throw new InvalidOperationException($"The move would leave {unarmed.Id} carrying no weapon (PHASE 6a DECISION 7).");
        if (from == to)
            return (moved, null, null);

        if (swapped is not null)
        {
            Put(to, moved);
            Put(from, swapped);
        }
        else if (to.IsStable)
        {
            Take(from);
            _stable.Add(moved);   // the stable's end, after the stable has closed up behind it
        }
        else
        {
            Take(from);
            Put(to, moved);
        }

        var armed = !from.IsStable && from.Index == 0 ? MemberOf(from).ArmFromBag() : null;   // null for a hand the move filled again
        return (moved, swapped, armed);
    }

    /// <summary>The member a place in the party names.</summary>
    /// <exception cref="ArgumentException">No member of the party has the id.</exception>
    private PartyMemberState MemberOf(WeaponPlace place)
        => _party.FirstOrDefault(member => member.Id == place.MemberId)
            ?? throw new ArgumentException($"'{place.MemberId}' is no member of the party.", nameof(place));

    /// <summary>Lay <paramref name="weapon"/> at <paramref name="place"/>, over what is there: a slot, or a place in the stable.</summary>
    private void Put(WeaponPlace place, Weapon weapon)
    {
        if (place.IsStable)
            _stable[place.Index] = weapon;
        else
            MemberOf(place).Inventory[place.Index] = weapon;
    }

    /// <summary>Take what is at <paramref name="place"/> away: a slot is left empty, and the stable closes up behind it.</summary>
    private void Take(WeaponPlace place)
    {
        if (place.IsStable)
            _stable.RemoveAt(place.Index);
        else
            MemberOf(place).Inventory[place.Index] = null;
    }

    // ── The enchanter's queue (§6.2, Phase 6a.3a) ────────────────────────────

    /// <summary>
    /// The deposits in the shop (§6.2), in deposit order — the ordinals rising — a
    /// list, never a set, so the view a save stores and the hub lists is that
    /// order. Each weapon here is out of the party's hands: in no bag and not in
    /// the stable. No length of its own (P6:302–303). Empty on a new campaign.
    /// </summary>
    public IReadOnlyList<Deposit> AtTheEnchanter => _enchanter.AsReadOnly();

    /// <summary>How many deposits the campaign has ever made: the next deposit's ordinal (<see cref="Deposit.Ordinal"/>). Never lowered — a take-back or a finished service reuses no ordinal.</summary>
    public int DepositsMade { get; private set; }

    /// <summary>
    /// Queue <paramref name="entry"/>, the deposit of the weapon at
    /// <paramref name="from"/>, by hand between runs: the hub's door into the shop,
    /// which the enchanter calls once it has cashed the weapon's wear
    /// (<see cref="Enchanter.Deposit"/>, its one caller). The weapon leaves its
    /// place — a bag slot is left empty, and the stable closes up behind it — and so
    /// does a transfer's source, from <paramref name="source"/>: both wait in the
    /// shop as the one deposit (PHASE 6a DECISION 13). A hand either empties is armed
    /// from the bag as a move's is (<see cref="PartyMemberState.ArmFromBag"/>), once
    /// both are gone, and the count of deposits made moves past its ordinal.
    /// </summary>
    /// <param name="from">Where the deposit's weapon lies.</param>
    /// <param name="entry">The deposit.</param>
    /// <param name="source">Where a transfer's source lies, when it is another weapon; null for every other deposit, and for a reorder.</param>
    /// <returns>The weapon a hand the weapon left took up from the bag, and the one a hand the source left took up; each null when no hand was emptied.</returns>
    /// <exception cref="InvalidOperationException">A visit is in progress, or the deposit would leave a member carrying no weapon (PHASE 6a DECISION 7) — a transfer's asked with both its weapons gone.</exception>
    /// <exception cref="ArgumentException"><paramref name="entry"/> is not the deposit of the weapon at <paramref name="from"/>, or of the source at <paramref name="source"/>; or its ordinal is not the next.</exception>
    internal (Weapon? Armed, Weapon? SourceArmed) Enqueue(WeaponPlace from, Deposit entry, WeaponPlace? source = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (Visit is { } visit)
            throw new InvalidOperationException(
                $"A visit to '{visit.Def.Id}' is in progress: the shop is changed by hand only between runs.");
        if (!ReferenceEquals(WeaponAt(from), entry.Weapon))
            throw new ArgumentException($"Deposit {entry.Ordinal} is of {entry.Weapon.Name}, and {from} holds another: a deposit takes the weapon where it lies.", nameof(entry));
        if ((source is null) != (entry.Source is null) || (source is { } at && !ReferenceEquals(WeaponAt(at), entry.Source)))
            throw new ArgumentException($"Deposit {entry.Ordinal}'s source is {entry.Source?.Name ?? "none"}, and {source?.ToString() ?? "no place"} is named for it: a transfer takes its source where it lies.", nameof(source));
        if (entry.Ordinal != DepositsMade)
            throw new ArgumentException($"Deposit {entry.Ordinal} is not the next: {DepositsMade} deposits have been made.", nameof(entry));
        WeaponPlace[] places = source is { } given ? [from, given] : [from];
        foreach (var member in places.Where(place => !place.IsStable).Select(MemberOf).Distinct())
            if (member.Inventory.Count(carried => carried is not null) == places.Count(place => place.MemberId == member.Id))
                throw new InvalidOperationException($"The deposit would leave {member.Id} carrying no weapon (PHASE 6a DECISION 7).");

        foreach (var place in places.OrderByDescending(place => place.IsStable ? place.Index : -1))
            Take(place);   // the stable's later place first, so the earlier one has not moved
        _enchanter.Add(entry);
        DepositsMade++;
        var armed = !from.IsStable && from.Index == 0 ? MemberOf(from).ArmFromBag() : null;
        var sourceArmed = source is { IsStable: false, Index: 0 } emptied ? MemberOf(emptied).ArmFromBag() : null;
        return (armed, sourceArmed);
    }

    /// <summary>
    /// Take <paramref name="deposit"/> out of the shop by hand, between runs: the
    /// hub's door for a take-back (<see cref="Enchanter.TakeBack"/>, its one
    /// caller), its weapon to the end of the stable, and a transfer's source after
    /// it.
    /// </summary>
    /// <exception cref="InvalidOperationException">A visit is in progress, or <paramref name="deposit"/> is not in the shop.</exception>
    internal void TakeOut(Deposit deposit)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        if (Visit is { } visit)
            throw new InvalidOperationException(
                $"A visit to '{visit.Def.Id}' is in progress: the shop is changed by hand only between runs.");
        Release(deposit);
    }

    /// <summary>
    /// A service finished at the run's end (§6.2): <paramref name="finished"/> leaves
    /// the shop and its weapon goes to the end of the stable, a transfer's source
    /// after it (PHASE 6a DECISIONS 7 and 13).
    /// The run's end's own path — the visit already gone — never the hub's doors,
    /// and asked by the enchanter's clock alone (<see cref="Enchanter.Tick"/>,
    /// which <see cref="EndRun"/> alone asks).
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="finished"/> is not in the shop.</exception>
    internal void Return(Deposit finished)
    {
        ArgumentNullException.ThrowIfNull(finished);
        Release(finished);
    }

    /// <summary>A deposit out of the shop, by reference, and its weapon to the end of the stable, a transfer's source after it: what a take-back and a finished service share.</summary>
    private void Release(Deposit deposit)
    {
        int at = _enchanter.FindIndex(queued => ReferenceEquals(queued, deposit));
        if (at < 0)
            throw new InvalidOperationException($"Deposit {deposit.Ordinal}, of {deposit.Weapon.Name}, is not in the shop.");
        _enchanter.RemoveAt(at);
        _stable.Add(deposit.Weapon);
        if (deposit.Source is { } source)
            _stable.Add(source);
    }
}
