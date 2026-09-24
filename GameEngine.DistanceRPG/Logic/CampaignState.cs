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
/// (<see cref="Party"/>) whose order is the roster file's and never a hash's,
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
/// party kept. The dungeon itself is discarded whole.
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
    // anyone iterates or saves. Only a run's end writes them, and RunCounter:
    // there is no restore path yet. Phase 5's load (§5.1) adds one of its own,
    // an internal restore constructor or factory taking the counter and the
    // three sorted views, never a public setter or a public Retire.
    private readonly Dictionary<string, int> _lockedUntilRun = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cleared = new(StringComparer.Ordinal);
    private readonly HashSet<string> _retired = new(StringComparer.Ordinal);

    // The subscription Enter made on the visit in progress's Wiped, taken off
    // again at the run's end, so a discarded visit keeps nothing of the campaign.
    private Action? _onWiped;

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
    /// hub's gate — the tutorial alone until it is beaten — is the between-runs
    /// screen's to present; entering asks only whether the dungeon is locked or
    /// retired.
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

        _onWiped = () =>
        {
            if (ReferenceEquals(Visit, visit))
                EndRun(RunEnding.Wiped);
        };
        visit.Wiped += _onWiped;
        Visit = visit;
        return visit;
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

        // The visit goes, and nothing of the campaign stays wired to it.
        visit.Wiped -= _onWiped;
        _onWiped = null;
        Visit = null;

        var result = new RunResult(def.Id,
            ending == RunEnding.Wiped ? RunOutcome.Death : bossDown ? RunOutcome.BossKill : RunOutcome.Retreat,
            bossDown, deepest, deepest >= visit.BossFloor ? visit.BossFloor : null,
            ServiceTick: tick, MercyTick: mercy, RunCounter, lockedUntil, retired,
            kept, lost, clawbacks);
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
    /// run counter. <strong>Provisional</strong>: it changes settled.md's "clearing
    /// a dungeon locks it for <c>bossFloor</c> runs" in this one corner, and
    /// open-questions.md holds the decision for the user.
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
    /// has a dungeon it can enter.
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
