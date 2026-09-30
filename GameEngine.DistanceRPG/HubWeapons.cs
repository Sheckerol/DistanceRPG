using GameEngine.DistanceRPG.Logic;
using Run = GameEngine.DistanceRPG.DungeonHud.Run;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The WEAPONS page between runs (§6.2, §6.7; Phase 6a.2): every member's hand
/// and bag, then the stable, then the enchanter's shop (Phase 6a.3a), a row a
/// weapon — rebuilt from the campaign each time it is asked
/// (<see cref="Rows(CampaignState)"/>), so a row never shows a weapon where it
/// no longer is — and the page's own state: the row chosen, the first row the
/// window shows, and a weapon carried.
/// <para>
/// <strong>A carry is the page's alone, and moves nothing.</strong> ENTER on a
/// weapon takes it up (<see cref="TakeUp"/>): the choice then goes over the
/// places it can be put down alone — a bag slot, the hand among them, a member's
/// room, a weapon in the stable, the stable's room — and ENTER puts it down
/// there, which is the one moment the campaign changes
/// (<see cref="CampaignState.Move"/>, which the scene calls). ESC puts it back,
/// and nothing has moved. So a quit, the window's close or a crash mid-carry
/// loses nothing, and a save never sees a carry.
/// </para>
/// Every string of the page's rows is built here, <c>internal static</c> where it
/// can be, and tested (<c>BetweenRunsHudTests</c>); the HUD draws them
/// (<see cref="BetweenRunsHud"/>).
/// </summary>
internal sealed class HubWeapons
{
    /// <summary>What a row of the page is.</summary>
    internal enum RowKind
    {
        /// <summary>A heading over a part of the page: read, never chosen.</summary>
        Heading,

        /// <summary>A member's name over its slots: read, never chosen.</summary>
        Member,

        /// <summary>A weapon in a member's slot or in the stable: chosen, taken up, and put down on — a swap.</summary>
        Weapon,

        /// <summary>Somewhere a weapon can go that holds none — a member's empty hand, the room in its bag, the stable's end: chosen, and put down on.</summary>
        Room,

        /// <summary>A weapon in the enchanter's shop, its deposit working or stalled (Phase 6a.3a): chosen, to be read, but neither taken up nor put down on — it is out of the party's hands.</summary>
        Shop,
    }

    /// <summary>
    /// One row of the page: what it is, its label — the whole text of a heading,
    /// a member or a room, and what comes before a weapon's name — where it
    /// points, and the weapon there.
    /// </summary>
    /// <param name="Kind">What the row is.</param>
    /// <param name="Label">A heading's, a member's or a room's whole text; a weapon row's text before the weapon's name, its indent and its slot, or on a shop row whether it is worked.</param>
    /// <param name="Place">Where a weapon or a room is — what a weapon carried is put down on — or null for a heading, a member or the shop.</param>
    /// <param name="Weapon">The weapon on a weapon row or a shop row, or null.</param>
    /// <param name="Deposit">The deposit on a shop row, or null.</param>
    /// <param name="Ahead">On a shop row, how many newer deposits are in the shop: a stalled one waits on them.</param>
    internal sealed record Row(RowKind Kind, string Label, WeaponPlace? Place = null, Weapon? Weapon = null, Deposit? Deposit = null, int Ahead = 0)
    {
        /// <summary>
        /// The row's text: the label, and on a weapon row the weapon's name and its
        /// wear (<see cref="DungeonHud.WearReadout"/>), on a shop row its name and its
        /// runs (<see cref="ShopRuns"/>).
        /// </summary>
        public string Text => (Weapon, Deposit) switch
        {
            ({ } weapon, { } deposit) => $"{Label}{Name(weapon)} - {ShopRuns(deposit)}",
            ({ } weapon, null) => $"{Label}{Name(weapon)} - {DungeonHud.WearReadout(weapon)}",
            _ => Label,
        };

        /// <summary>Whether this shop row's deposit waits: the next run that counts leaves it unworked.</summary>
        public bool Stalled => Kind == RowKind.Shop && Label == StalledLabel;

        /// <summary>Whether W/S stops on it while browsing: a weapon, a room, or a weapon in the shop.</summary>
        public bool Choosable => Kind is RowKind.Weapon or RowKind.Room or RowKind.Shop;

        /// <summary>Whether a weapon carried can be put down on it: a weapon, which it swaps with, or a room.</summary>
        public bool Target => Kind is RowKind.Weapon or RowKind.Room;
    }

    // ── The rows ─────────────────────────────────────────────────────────────

    /// <summary>Over the members and their slots.</summary>
    internal const string PartyHeading = "THE PARTY AND THE STABLE";

    /// <summary>A weapon in hand's row starts so: indented, and the slot said.</summary>
    internal const string HandLabel = "  HAND  ";

    /// <summary>A weapon in the bag's row starts so, the slot word padded to the hand's, so the names line up.</summary>
    internal const string BagLabel = "  BAG   ";

    /// <summary>A weapon in the stable's row starts so: indented under the stable's heading.</summary>
    internal const string StableLabel = "  ";

    /// <summary>A member's empty hand: somewhere a weapon can be put.</summary>
    internal const string EmptyHand = HandLabel + "- EMPTY -";

    /// <summary>The stable's end: where a weapon put in the stable goes.</summary>
    internal const string StableRoom = StableLabel + "- ROOM -";

    /// <summary>A shop row whose deposit the next run that counts works, as the stable's and the bag's rows start: indented, and what it is said.</summary>
    internal const string WorkingLabel = "  WORKING  ";

    /// <summary>A shop row whose deposit waits on the newer ones.</summary>
    internal const string StalledLabel = "  STALLED  ";

    /// <summary>Over the shop once the tutorial is beaten and nothing is in it.</summary>
    internal const string NothingInTheShop = "THE ENCHANTER - NOTHING IN THE SHOP";

    /// <summary>What the chosen row's indent becomes, and how it ends: the dungeon list's marks.</summary>
    private const string ChosenMark = "> ";
    private const string ChosenEnd = " <";

    /// <summary>A weapon's name as the page says it: upper-cased, as the HUD's font is.</summary>
    internal static string Name(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return weapon.Name.ToUpperInvariant();
    }

    /// <summary>A weapon named with its article, once: <c>THE ARMING SWORD</c>, and <c>THE BULWARK</c> for a name that has its own.</summary>
    internal static string The(Weapon weapon) => The(Name(weapon));

    /// <summary>A weapon's name, as the page says it, with its article once: <c>THE ROTWOOD</c>, and <c>THE BULWARK</c> as it is.</summary>
    internal static string The(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.StartsWith("THE ", StringComparison.Ordinal) ? name : $"THE {name}";
    }

    /// <summary>A member as the page names it, as the dungeon's readouts do: <c>[A]</c>.</summary>
    internal static string MemberLine(string memberId) => $"[{memberId}]";

    /// <summary>The room left in a member's bag, one row however many slots are empty: <c>  BAG   - ROOM FOR 4 MORE -</c>.</summary>
    internal static string BagRoom(int empty) => $"{BagLabel}- ROOM FOR {empty} MORE -";

    /// <summary>The stable's heading, with what it holds: <c>THE STABLE - 3 WEAPONS</c>, <c>THE STABLE - 1 WEAPON</c>, <c>THE STABLE - EMPTY</c>.</summary>
    internal static string StableHeading(int count)
        => count switch
        {
            0 => "THE STABLE - EMPTY",
            1 => "THE STABLE - 1 WEAPON",
            _ => $"THE STABLE - {count} WEAPONS",
        };

    /// <summary>
    /// Over the shop while something is in it (§6.2): how many deposits the
    /// enchanter works on at once, the newest, <c>THE ENCHANTER - WORKS ON THE 2
    /// NEWEST</c> (<see cref="Tuning.EnchanterWorksOn"/>) — so a stalled row under it
    /// reads as waiting, never as lost.
    /// </summary>
    internal static string EnchanterHeading(int worksOn)
        => worksOn == 1 ? "THE ENCHANTER - WORKS ON THE NEWEST" : $"THE ENCHANTER - WORKS ON THE {worksOn} NEWEST";

    /// <summary>A deposit's runs as a shop row reads them, the runs worked of the runs it takes: <c>1 OF 2 RUNS</c>, <c>0 OF 1 RUN</c>.</summary>
    internal static string ShopRuns(Deposit deposit)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        return $"{deposit.RunsWorked} OF {DungeonHud.Runs(deposit.Runs)}";
    }

    /// <summary>
    /// Why a weapon cannot be put down where it was carried to (PHASE 6a DECISION
    /// 7): the member it would leave carrying none, <c>EACH MEMBER KEEPS A WEAPON -
    /// [A] WOULD CARRY NONE</c>. Said in the refusal row, over the keys, until the
    /// next key; the weapon stays taken up.
    /// </summary>
    internal static string KeepsAWeapon(string memberId) => $"EACH MEMBER KEEPS A WEAPON - {MemberLine(memberId)} WOULD CARRY NONE";

    /// <summary>
    /// The page's rows, top to bottom (§6.2): the party's heading, then each member
    /// in roster order — its name, its hand, each weapon in its bag in slot order,
    /// and one row for the room its bag has left, or none when it is full — then
    /// the stable's heading, each weapon in the stable in its order, and the
    /// stable's room, which is always there: the stable is unbounded (PHASE 6a
    /// DECISION 7). An empty hand is a row of its own, somewhere a weapon can go.
    /// Last the enchanter (Phase 6a.3a): before the tutorial is beaten, that it
    /// opens then (§6.3); after, how many deposits it works on, and each weapon in
    /// the shop, the newest first — <c>WORKING</c> for the few the next run that
    /// counts works, <c>STALLED</c> for the rest (§6.2: "working on these two"
    /// reads at a glance, and a stalled weapon is never silent) — or that nothing
    /// is in it.
    /// </summary>
    internal static IReadOnlyList<Row> Rows(CampaignState campaign) => [.. PartyRows(campaign), .. ShopRows(campaign)];

    /// <summary>
    /// The page's rows down to the stable's room (<see cref="Rows(CampaignState)"/>):
    /// the party and the stable, every place a weapon can be taken up from or put
    /// down on — the page's first rows, so a row's index is the same here as on the
    /// whole page. Read off the party and the stable alone, nothing of the content,
    /// so a carry put back finds its weapon's row with nothing but the campaign
    /// (<see cref="PutBack"/>, which ESC reaches).
    /// </summary>
    private static List<Row> PartyRows(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var rows = new List<Row> { new(RowKind.Heading, PartyHeading) };
        foreach (var member in campaign.Party)
        {
            rows.Add(new Row(RowKind.Member, MemberLine(member.Id)));
            var bag = member.Inventory;
            rows.Add(bag[0] is { } hand
                ? new Row(RowKind.Weapon, HandLabel, new WeaponPlace(member.Id, 0), hand)
                : new Row(RowKind.Room, EmptyHand, new WeaponPlace(member.Id, 0)));
            int firstEmpty = -1, empty = 0;
            for (int slot = 1; slot < bag.Length; slot++)
            {
                if (bag[slot] is { } carried)
                {
                    rows.Add(new Row(RowKind.Weapon, BagLabel, new WeaponPlace(member.Id, slot), carried));
                    continue;
                }
                empty++;
                if (firstEmpty < 0)
                    firstEmpty = slot;
            }
            if (empty > 0)
                rows.Add(new Row(RowKind.Room, BagRoom(empty), new WeaponPlace(member.Id, firstEmpty)));   // a weapon put here takes the first empty slot
        }

        var stable = campaign.Stable;
        rows.Add(new Row(RowKind.Heading, StableHeading(stable.Count)));
        for (int i = 0; i < stable.Count; i++)
            rows.Add(new Row(RowKind.Weapon, StableLabel, WeaponPlace.InTheStable(i), stable[i]));
        rows.Add(new Row(RowKind.Room, StableRoom, WeaponPlace.InTheStable(stable.Count)));
        return rows;
    }

    /// <summary>The page's last rows (<see cref="Rows(CampaignState)"/>): the enchanter's heading, and each weapon in the shop, the newest first.</summary>
    private static List<Row> ShopRows(CampaignState campaign)
    {
        var rows = new List<Row>();
        var content = GameContent.Current;
        var shop = campaign.AtTheEnchanter;
        if (!campaign.IsRetired(content.Dungeons.Tutorial.Id))
            rows.Add(new Row(RowKind.Heading, Enchanter.Closed(content.Dungeons.Tutorial)));
        else if (shop.Count == 0)
            rows.Add(new Row(RowKind.Heading, NothingInTheShop));
        else
        {
            rows.Add(new Row(RowKind.Heading, EnchanterHeading(content.Tuning.EnchanterWorksOn)));
            var worked = Enchanter.Worked(shop);
            for (int i = shop.Count - 1; i >= 0; i--)   // the newest first: the worked few head it
            {
                var deposit = shop[i];
                bool working = worked.Any(entry => ReferenceEquals(entry, deposit));
                rows.Add(new Row(RowKind.Shop, working ? WorkingLabel : StalledLabel, Weapon: deposit.Weapon, Deposit: deposit,
                    Ahead: shop.Count(entry => entry.Ordinal > deposit.Ordinal)));
            }
        }
        return rows;
    }

    /// <summary>
    /// A row as the page draws it, in its colours: a heading and a member in grey,
    /// a room in dark grey, a weapon in white — its name in a unique's orange, its
    /// wear green once full (<see cref="DungeonHud.WearColor"/>), as on every
    /// weapon line — and a weapon taken up, where it still lies, in cyan. A weapon
    /// in the shop reads cyan while it is worked, as the enchantments do, and yellow
    /// while it is stalled, a unique's name orange. The row chosen is yellow, its
    /// indent marked <c>&gt;</c> and its end <c>&lt;</c>, as the dungeon list marks
    /// its choice.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="chosen">Whether it is the row chosen.</param>
    /// <param name="carried">Whether its weapon is the one taken up.</param>
    internal static IReadOnlyList<Run> Runs(Row row, bool chosen = false, bool carried = false)
    {
        ArgumentNullException.ThrowIfNull(row);
        switch (row.Kind)
        {
            case RowKind.Heading:
            case RowKind.Member:
                return [new Run(row.Label, DungeonHud.Grey)];

            case RowKind.Room:
                return [chosen ? new Run(Marked(row.Label) + ChosenEnd, DungeonHud.Yellow) : new Run(row.Label, DungeonHud.DarkGrey)];

            case RowKind.Shop:
            {
                var weapon = row.Weapon!;
                var line = chosen || row.Stalled ? DungeonHud.Yellow : DungeonHud.Cyan;
                var runs = new List<Run>
                {
                    new(chosen ? Marked(row.Label) : row.Label, line),
                    new(Name(weapon), weapon.Unique ? DungeonHud.Orange : line),
                    new($" - {ShopRuns(row.Deposit!)}", line),
                };
                if (chosen)
                    runs.Add(new Run(ChosenEnd, line));
                return runs;
            }

            default:
            {
                var weapon = row.Weapon!;
                var line = chosen ? DungeonHud.Yellow : carried ? DungeonHud.Cyan : DungeonHud.White;
                var runs = new List<Run>
                {
                    new(chosen ? Marked(row.Label) : row.Label, line),
                    new(Name(weapon), weapon.Unique ? DungeonHud.Orange : line),
                    new(" - ", line),
                    new(DungeonHud.WearReadout(weapon), DungeonHud.WearColor(weapon, line)),
                };
                if (chosen)
                    runs.Add(new Run(ChosenEnd, line));
                return runs;
            }
        }
    }

    /// <summary>A chosen row's label: its indent's first two characters become the mark.</summary>
    private static string Marked(string label) => ChosenMark + label[ChosenMark.Length..];

    /// <summary>
    /// Where a window of <paramref name="fit"/> rows starts over <paramref name="count"/>
    /// rows so that <paramref name="chosen"/> is in view: where it started,
    /// <paramref name="top"/>, when the choice is in view there, and otherwise
    /// moved just far enough — up to put the choice first, down to put it last —
    /// never past the last row's window or above the first row.
    /// </summary>
    internal static int ScrollTop(int top, int chosen, int count, int fit)
    {
        if (fit <= 0 || count <= fit)
            return 0;
        top = Math.Clamp(top, 0, count - fit);
        if (chosen < top)
            return Math.Max(0, chosen);
        if (chosen >= top + fit)
            return Math.Min(count - fit, chosen - fit + 1);
        return top;
    }

    // ── The page's state ─────────────────────────────────────────────────────

    private readonly CampaignState _campaign;

    // The row chosen, as an index into the rows as they stand; the first row the
    // window shows; and the weapon taken up, with where it was taken from — the
    // page's alone, never the campaign's, which changes only when it is put down.
    private int _chosen;
    private int _top;
    private (WeaponPlace From, Weapon Weapon)? _carried;

    /// <param name="campaign">The campaign between its runs, whose party and stable the page lists.</param>
    public HubWeapons(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        _campaign = campaign;
        _chosen = Next(Rows(campaign), -1, +1, row => row.Choosable);
    }

    /// <summary>The page's rows as the campaign stands now (<see cref="Rows(CampaignState)"/>).</summary>
    public IReadOnlyList<Row> Rows() => Rows(_campaign);

    /// <summary>The index of the row chosen.</summary>
    public int Chosen => _chosen;

    /// <summary>The row chosen, as the campaign stands now.</summary>
    public Row ChosenRow
    {
        get
        {
            var rows = Rows();
            return rows[Math.Clamp(_chosen, 0, rows.Count - 1)];   // the stable's room is always there: never an empty page
        }
    }

    /// <summary>The weapon taken up and where it was taken from, or null when none is.</summary>
    public (WeaponPlace From, Weapon Weapon)? Carried => _carried;

    /// <summary>
    /// The first row a window of <paramref name="fit"/> rows shows, with the choice
    /// in it (<see cref="ScrollTop"/>): kept from one frame to the next, so the
    /// window moves only as far as the choice asks, and W/S onto a row already in
    /// view never move it. A choice that is the last row any choice stops on asks
    /// for the rows under it that none stops on — the enchanter's heading under the
    /// stable's room, with nothing in the shop — where the window has room, so the
    /// page's end is read. And a choice that is the first row under a member's name
    /// or a heading — the first weapon of a member, of the stable or of the shop, or
    /// the room where there is none — asks for the rows over it that no choice stops
    /// on too, where the window has room, so it is never read without whose it is.
    /// </summary>
    public int Window(int fit)
    {
        var rows = Rows();
        int chosen = Math.Clamp(_chosen, 0, rows.Count - 1);
        int top = ScrollTop(_top, chosen, rows.Count, fit);
        if (fit > 0 && rows.Skip(chosen + 1).All(row => !row.Choosable))
            top = Math.Max(top, Math.Min(chosen, rows.Count - fit));   // the rows under the last choice: the page's end in view
        int over = chosen;   // the first of the rows no choice stops on just over the choice, or the choice itself when there are none
        while (over > 0 && !rows[over - 1].Choosable)
            over--;
        if (over < top)
            top = Math.Max(over, chosen - fit + 1);
        _top = top;
        return top;
    }

    /// <summary>
    /// W/S: the choice to the next row up (<paramref name="step"/> −1) or down (+1)
    /// it can stop on, round the ends of the page — while browsing, a weapon or a
    /// room; while carrying, a place the weapon can be put down on. Headings and
    /// members are passed over.
    /// </summary>
    public void Step(int step)
    {
        Func<Row, bool> stops = _carried is null ? row => row.Choosable : row => row.Target;
        _chosen = Next(Rows(), _chosen, step, stops);
    }

    /// <summary>
    /// ENTER while browsing: the weapon chosen taken up — the page's alone, nothing
    /// moves — and the choice left on it, where putting it down again moves
    /// nothing. On anything but a weapon, nothing.
    /// </summary>
    /// <returns>The weapon taken up and where from, or null when nothing was.</returns>
    public (WeaponPlace From, Weapon Weapon)? TakeUp()
    {
        if (_carried is not null || ChosenRow is not { Kind: RowKind.Weapon, Place: { } from, Weapon: { } weapon })
            return null;
        _carried = (from, weapon);
        return _carried;
    }

    /// <summary>ESC while carrying: the weapon put back — it never left — and the choice on it again.</summary>
    /// <returns>The weapon put back, or null when none was taken up.</returns>
    public Weapon? PutBack()
    {
        if (_carried is not { } carried)
            return null;
        _carried = null;
        ChooseRowOf(carried.Weapon);
        return carried.Weapon;
    }

    /// <summary>The weapon taken up has been put down (<see cref="CampaignState.Move"/>): the carry is over, and the choice follows it to where it went.</summary>
    public void PutDown(Weapon moved)
    {
        ArgumentNullException.ThrowIfNull(moved);
        _carried = null;
        ChooseRowOf(moved);
    }

    /// <summary>
    /// The choice onto <paramref name="weapon"/>'s row, wherever it is now; or, gone
    /// from the page, onto the first row it can stop on. A weapon carried is in the
    /// party or the stable, never the shop, and so are those rows, which the page
    /// opens with (<see cref="PartyRows"/>).
    /// </summary>
    private void ChooseRowOf(Weapon weapon)
    {
        var rows = PartyRows(_campaign);
        int at = -1;
        for (int i = 0; i < rows.Count && at < 0; i++)
            if (ReferenceEquals(rows[i].Weapon, weapon))
                at = i;
        _chosen = at >= 0 ? at : Next(rows, -1, +1, row => row.Choosable);
    }

    /// <summary>The next row from <paramref name="from"/>, a step at a time round the ends, that <paramref name="stops"/> takes; <paramref name="from"/> itself when no other is.</summary>
    private static int Next(IReadOnlyList<Row> rows, int from, int step, Func<Row, bool> stops)
    {
        int count = rows.Count;
        int direction = Math.Sign(step);
        if (count == 0 || direction == 0)
            return from;
        int at = from;
        for (int moved = 0; moved < count; moved++)
        {
            at = ((at + direction) % count + count) % count;
            if (stops(rows[at]))
                return at;
        }
        return from;
    }
}
