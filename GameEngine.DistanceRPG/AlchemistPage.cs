using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;
using ListRow = GameEngine.DistanceRPG.BetweenRunsHud.ListRow;
using Run = GameEngine.DistanceRPG.DungeonHud.Run;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The ALCHEMIST page between runs (§6.6; Phase 6b.4), the hub's third TAB page:
/// where the farm's batch is split into potions. Before the alchemist arrives — the
/// first clear of a dungeon besides the tutorial (<see cref="Farm.AlchemistHasArrived"/>)
/// — it says when he does, and nothing on it is chosen (PHASE 6b DECISION 10).
/// After, down the left: what the farm yields a run that counts, the batch and the
/// units left of it, that what is left unmixed spoils on the descent, and the
/// quality A/D set (<see cref="Lines"/>); under them, each member's row — how much
/// room its bag has — and each potion in its bag, the rows the choice goes over,
/// in a window that scrolls with it (<see cref="Rows(CampaignState)"/>). On the
/// right, the chosen row said whole (<see cref="DetailRows"/>): for a member, what
/// a health and a mana potion at the quality would restore it now; for a potion,
/// what it restores and what pouring it back gives the batch. The page shows
/// numbers, never a person: the presentation is an art question, kept outside the
/// game (§6.6).
/// <para>
/// <strong>Its rows are read off the campaign each time</strong>, as the WEAPONS
/// page's are, and nothing is kept of the batch: it is the last run's yield less
/// what the bags hold (<see cref="Farm.Batch"/>). H or M mixes a potion at the
/// quality into the chosen member's bag — on a potion's row, its member's — and T on
/// a potion pours it back (<see cref="Alchemist.Mix"/>,
/// <see cref="Alchemist.PourBack"/>), which the scene calls.
/// </para>
/// <para>
/// <strong>The choice is kept by place</strong> — a member, or a member's bag slot
/// (<see cref="Place"/>) — never by a bare index, so a row added or taken above it
/// never moves it: after a mix it stays on the row it was on, and after a pour-back
/// it goes to the next potion in that member's bag, or to the member's row
/// (<see cref="AfterMix"/>, <see cref="AfterPourBack"/>). The quality starts at 1 on
/// every showing and is held to the batch: 1 to the units left, and 1 when none is.
/// </para>
/// Every string of the page is built here, <c>internal static</c>, and tested
/// (<c>AlchemistPageTests</c>); the HUD draws them (<see cref="BetweenRunsHud"/>).
/// </summary>
internal sealed class AlchemistPage
{
    /// <summary>What a row the choice goes over is.</summary>
    internal enum RowKind
    {
        /// <summary>A member: where H and M mix a potion into, and how much room its bag has.</summary>
        Member,

        /// <summary>A potion in a member's bag: T pours it back, and H and M mix into its member's bag.</summary>
        Potion,
    }

    /// <summary>
    /// Where a row points, which is what the choice keeps: a member's own row — slot
    /// 0, the hand, which never holds a potion, so 0 names no potion — or a potion in
    /// its bag, by its slot.
    /// </summary>
    /// <param name="MemberId">The member, by roster id.</param>
    /// <param name="Slot">0 for the member's own row; a bag slot, 1 and up, for a potion.</param>
    internal readonly record struct Place(string MemberId, int Slot)
    {
        /// <summary>Whether it is the member's own row rather than a potion's.</summary>
        public bool IsMember => Slot == 0;
    }

    /// <summary>One row the choice goes over: what it is, where it points, its text, and the potion on a potion's row.</summary>
    /// <param name="Kind">A member or a potion.</param>
    /// <param name="Place">Where it points: the member's row, or the potion's bag slot.</param>
    /// <param name="Text">The row's text, unmarked (<see cref="MemberText"/>, <see cref="PotionText"/>).</param>
    /// <param name="Potion">The potion on a potion's row, or null.</param>
    internal sealed record Row(RowKind Kind, Place Place, string Text, Potion? Potion = null);

    // ── The lines over the rows ──────────────────────────────────────────────

    /// <summary>
    /// What the farm yields a run that counts, at the workers it has now (§6.6): <c>THE
    /// FARM - 2 DUNGEONS BEATEN - 14 UNITS A COUNTED RUN</c>, <c>1 DUNGEON</c> and
    /// <c>1 UNIT</c> singular — or, under a <see cref="Tuning.FarmYieldPerClear"/> of 0,
    /// <c>THE FARM - 2 DUNGEONS BEATEN - IT YIELDS NOTHING</c>.
    /// </summary>
    /// <param name="clears">The farm's workers (<see cref="Farm.Clears"/>).</param>
    /// <param name="perRun">What a run that counts yields at them (<see cref="Farm.Yield"/>).</param>
    internal static string FarmLine(int clears, int perRun)
    {
        string beaten = $"THE FARM - {clears} {(clears == 1 ? "DUNGEON" : "DUNGEONS")} BEATEN";
        return perRun > 0 ? $"{beaten} - {BetweenRunsHud.Units(perRun)} A COUNTED RUN" : $"{beaten} - IT YIELDS NOTHING";
    }

    /// <summary>
    /// The batch (§6.6), off the last run's yield and what the bags hold
    /// (<see cref="BatchLine(int, int)"/>) — and with no batch, why
    /// (<see cref="Alchemist.NoBatch"/>): <c>NO BATCH - THE LAST RUN DID NOT
    /// COUNT</c>, and the rest.
    /// </summary>
    internal static string BatchLine(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        int yielded = campaign.LastResult?.FarmYield ?? 0;
        return yielded > 0 ? BatchLine(Farm.Batch(campaign), yielded) : Alchemist.NoBatch(campaign);
    }

    /// <summary>
    /// The batch, <paramref name="left"/> of the <paramref name="yielded"/> units the
    /// last run's end yielded: <c>THE BATCH - 9 OF 14 UNITS LEFT</c>, and with none
    /// left, <c>THE BATCH - ALL 14 UNITS MIXED</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Nothing yielded — that line is why there is no batch (<see cref="Alchemist.NoBatch"/>) — or less than none left.</exception>
    internal static string BatchLine(int left, int yielded)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(yielded, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(left);
        return left > 0 ? $"THE BATCH - {left} OF {BetweenRunsHud.Units(yielded)} LEFT" : $"THE BATCH - ALL {BetweenRunsHud.Units(yielded)} MIXED";
    }

    /// <summary>Under the batch: what becomes of what is left of it, and of a potion (§6.6: "a flow, not a stock").</summary>
    internal const string SpoilNote = "UNMIXED UNITS SPOIL ON THE DESCENT - A POTION KEEPS ONE TRIP";

    /// <summary>The quality H and M mix at, and what it costs: <c>QUALITY 3 - 3 UNITS A POTION - A/D CHANGES IT</c>.</summary>
    internal static string QualityLine(int quality) => $"QUALITY {quality} - {BetweenRunsHud.Units(quality)} A POTION - A/D CHANGES IT";

    /// <summary>
    /// The lines over the rows, top to bottom, as the HUD draws them down the left: the
    /// farm in grey, the batch in cyan, what spoils in grey, and the quality in yellow —
    /// read, never chosen.
    /// </summary>
    internal static IReadOnlyList<ListRow> Lines(CampaignState campaign, int quality)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        int clears = Farm.Clears(campaign);
        return
        [
            Line(FarmLine(clears, Farm.Yield(clears, counted: true)), DungeonHud.Grey),
            Line(BatchLine(campaign), DungeonHud.Cyan),
            Line(SpoilNote, DungeonHud.Grey),
            Line(QualityLine(quality), DungeonHud.Yellow),
        ];

        static ListRow Line(string text, Vector4 color) => new(text, color, BetweenRunsHud.WeaponRowScale, BetweenRunsHud.WeaponRowPitch);
    }

    // ── The rows ─────────────────────────────────────────────────────────────

    /// <summary>A member's row: its name and the room its bag has — <c>[A] - 2 BAG SLOTS FREE</c>, <c>[A] - 1 BAG SLOT FREE</c>, <c>[A] - THE BAG IS FULL</c>.</summary>
    internal static string MemberText(PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(member);
        int free = member.EmptyBagSlots.Count();
        string room = free switch
        {
            0 => "THE BAG IS FULL",
            1 => "1 BAG SLOT FREE",
            _ => $"{free} BAG SLOTS FREE",
        };
        return $"{HubWeapons.MemberLine(member.Id)} - {room}";
    }

    /// <summary>A potion's row under its member, its slot numbered as the dungeon's bag numbers it — the hand 1: <c>  BAG 4  HEALTH POTION Q3</c>.</summary>
    internal static string PotionText(int slot, Potion potion)
    {
        ArgumentNullException.ThrowIfNull(potion);
        return $"  BAG {slot + 1}  {potion.ShownName}";
    }

    /// <summary>
    /// The rows the choice goes over, top to bottom: each member in roster order, and
    /// under it each potion in its bag in slot order. Read off the party alone, afresh
    /// each time, so a row never shows a potion where it no longer is.
    /// </summary>
    internal static IReadOnlyList<Row> Rows(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var rows = new List<Row>();
        foreach (var member in campaign.Party)
        {
            rows.Add(new Row(RowKind.Member, new Place(member.Id, 0), MemberText(member)));
            foreach (var (slot, potion) in member.Potions)
                rows.Add(new Row(RowKind.Potion, new Place(member.Id, slot), PotionText(slot, potion), potion));
        }
        return rows;
    }

    /// <summary>What the chosen row's indent becomes, and how it ends: the dungeon list's and the WEAPONS page's marks.</summary>
    private const string ChosenMark = "> ";
    private const string ChosenEnd = " <";

    /// <summary>
    /// A row as the page draws it: a member in white, a potion in its own colour —
    /// green for health, cyan for mana (<see cref="DungeonHud.PotionColor"/>) — and the
    /// row chosen in yellow, marked as the WEAPONS page marks it, <c>&gt;</c> … <c>&lt;</c>:
    /// a potion's indent becomes the mark, and a member's row is led by it.
    /// </summary>
    internal static IReadOnlyList<Run> Runs(Row row, bool chosen = false)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!chosen)
            return [new Run(row.Text, row.Potion is { } potion ? DungeonHud.PotionColor(potion.Type) : DungeonHud.White)];
        string marked = row.Kind == RowKind.Potion ? ChosenMark + row.Text[ChosenMark.Length..] : ChosenMark + row.Text;
        return [new Run(marked + ChosenEnd, DungeonHud.Yellow)];
    }

    // ── The detail, on the right ─────────────────────────────────────────────

    /// <summary>Over a member's detail: <c>[A] - MAX HP 30 - MAX MANA 30</c> — the maxima a potion is a share of, mana's the pool it has earned (PHASE 6b DECISION 3).</summary>
    internal static string MemberHeading(PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return $"{HubWeapons.MemberLine(member.Id)} - MAX HP {member.MaxHp} - MAX MANA {member.MaxMana}";
    }

    /// <summary>
    /// What H or M would mix, and what it would restore the member now, in the bag's
    /// own words (<see cref="DungeonHud.PotionDetail"/>): <c>H  HEALTH POTION Q3 -
    /// RESTORES 18 OF 30 HP</c>, <c>M  MANA POTION Q3 - RESTORES ALL 30 MANA - 6
    /// OVER</c> — one line, or, where that would run past the column, the potion's
    /// line and what it restores indented under it.
    /// </summary>
    internal static IReadOnlyList<string> MixLines(PotionType type, int quality, PartyMemberState member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var potion = new Potion(type, quality);
        string head = $"{(type == PotionType.Health ? "H" : "M")}  {potion.ShownName}";
        string restores = DungeonHud.PotionDetail(potion, member);
        string whole = $"{head} - {restores}";
        return whole.Length <= BetweenRunsHud.DetailChars ? [whole] : [head, $"   {restores}"];
    }

    /// <summary>
    /// The quality that restores a whole pool from nothing (<see cref="Alchemist.QualityForAll"/>),
    /// past which more is overshoot: <c>QUALITY 5 RESTORES A WHOLE POOL</c> — or, with
    /// the two percentages apart, <c>HEALTH IS WHOLE AT QUALITY 5 - MANA AT 4</c>.
    /// </summary>
    internal static string WholeLine()
    {
        int health = Alchemist.QualityForAll(PotionType.Health), mana = Alchemist.QualityForAll(PotionType.Mana);
        return health == mana ? $"QUALITY {health} RESTORES A WHOLE POOL" : $"HEALTH IS WHOLE AT QUALITY {health} - MANA AT {mana}";
    }

    /// <summary>What a drink costs where it is drunk (PHASE 6b DECISION 2): <c>A DRINK COSTS 40 MOVE IN A DUNGEON</c>, or <c>DRINKING IS FREE IN A DUNGEON</c> at 0.</summary>
    internal static string DrinkLine(int cost) => cost > 0 ? $"A DRINK COSTS {cost} MOVE IN A DUNGEON" : "DRINKING IS FREE IN A DUNGEON";

    /// <summary>Over a potion's detail: what it is and where, <c>HEALTH POTION Q3 - BAG SLOT 4 OF [A]</c>, its slot numbered as its row numbers it.</summary>
    internal static string PotionHeading(Potion potion, Place place)
    {
        ArgumentNullException.ThrowIfNull(potion);
        return $"{potion.ShownName} - BAG SLOT {place.Slot + 1} OF {HubWeapons.MemberLine(place.MemberId)}";
    }

    /// <summary>What T does to a potion: <c>T POURS IT BACK - 3 UNITS RETURN TO THE BATCH</c>, <c>... - 1 UNIT RETURNS ...</c>.</summary>
    internal static string PourLine(int quality)
        => $"T POURS IT BACK - {BetweenRunsHud.Units(quality)} {(quality == 1 ? "RETURNS" : "RETURN")} TO THE BATCH";

    /// <summary>
    /// The chosen row said whole, top to bottom, on the right. For a member: its
    /// maxima (<see cref="MemberHeading"/>), what a health and a mana potion at
    /// <paramref name="quality"/> would restore it now (<see cref="MixLines"/>), in
    /// their colours, the quality for a whole pool (<see cref="WholeLine"/>) and what
    /// a drink costs (<see cref="DrinkLine"/>). For a potion: what it is and where
    /// (<see cref="PotionHeading"/>), what it restores its member now
    /// (<see cref="DungeonHud.PotionDetail"/>), in its colour, and what T gives back
    /// (<see cref="PourLine"/>). Nothing for no row, or a member gone from the party.
    /// </summary>
    internal static IReadOnlyList<ListRow> DetailRows(CampaignState campaign, Row? row, int quality)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var rows = new List<ListRow>();
        if (row is null || campaign.Party.FirstOrDefault(m => m.Id == row.Place.MemberId) is not { } member)
            return rows;

        if (row.Potion is { } potion)
        {
            var color = DungeonHud.PotionColor(potion.Type);
            rows.Add(new ListRow(PotionHeading(potion, row.Place), color, BetweenRunsHud.DetailNameScale, BetweenRunsHud.DetailNamePitch));
            rows.Add(new ListRow(DungeonHud.PotionDetail(potion, member), color, BetweenRunsHud.DetailScale, BetweenRunsHud.DetailPitch));
            rows.Add(new ListRow(PourLine(potion.Quality), DungeonHud.Grey, BetweenRunsHud.DetailScale, BetweenRunsHud.DetailPitch));
            return rows;
        }

        rows.Add(new ListRow(MemberHeading(member), DungeonHud.White, BetweenRunsHud.DetailNameScale, BetweenRunsHud.DetailNamePitch));
        foreach (var type in new[] { PotionType.Health, PotionType.Mana })
            foreach (string line in MixLines(type, quality, member))
                rows.Add(new ListRow(line, DungeonHud.PotionColor(type), BetweenRunsHud.DetailScale, BetweenRunsHud.DetailPitch));
        rows.Add(new ListRow(WholeLine(), DungeonHud.Grey, BetweenRunsHud.DetailScale, BetweenRunsHud.DetailPitch));
        rows.Add(new ListRow(DrinkLine(GameContent.Current.Tuning.PotionUseCost), DungeonHud.Grey, BetweenRunsHud.DetailScale, BetweenRunsHud.DetailPitch));
        return rows;
    }

    // ── The page's state ─────────────────────────────────────────────────────

    private readonly CampaignState _campaign;

    // The choice, kept by place — a member, or a member's bag slot — so a row added
    // or taken above it never moves it; the first row the window shows; and the
    // quality H and M mix at, which starts at 1 on every showing.
    private Place? _chosen;
    private int _top;
    private int _quality = 1;

    /// <param name="campaign">The campaign between its runs, whose farm, batch and bags the page shows.</param>
    public AlchemistPage(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        _campaign = campaign;
        _chosen = campaign.Party.Count > 0 ? new Place(campaign.Party[0].Id, 0) : null;
    }

    /// <summary>Whether the alchemist has arrived (<see cref="Farm.AlchemistHasArrived"/>): the page opens and closes on it, asked as the campaign stands (PHASE 6b DECISION 11).</summary>
    public bool Open => Farm.AlchemistHasArrived(_campaign);

    /// <summary>What the page says while he has not arrived: <see cref="Alchemist.NotYet"/>, naming the content's tutorial.</summary>
    public static string ClosedLine => Alchemist.NotYet(GameContent.Current.Dungeons.Tutorial);

    /// <summary>The rows as the campaign stands now (<see cref="Rows(CampaignState)"/>).</summary>
    public IReadOnlyList<Row> Rows() => Rows(_campaign);

    /// <summary>The lines over the rows as the campaign stands now, at the quality (<see cref="Lines(CampaignState, int)"/>).</summary>
    public IReadOnlyList<ListRow> Lines() => Lines(_campaign, Quality);

    /// <summary>The chosen row said whole as the campaign stands now, at the quality (<see cref="DetailRows"/>).</summary>
    public IReadOnlyList<ListRow> Detail() => DetailRows(_campaign, ChosenRow, Quality);

    /// <summary>
    /// The quality H and M mix at: 1 to the batch's units left — 1 when none is left,
    /// since a potion is a unit at least — held there as the batch changes.
    /// </summary>
    public int Quality => Math.Clamp(_quality, 1, MostQuality);

    /// <summary>The most the quality is held to: the batch's units left, and 1 when none is.</summary>
    private int MostQuality => Math.Max(1, Farm.Batch(_campaign));

    /// <summary>The index of the chosen row among the rows as they stand, or -1 when there are none (<see cref="Locate"/>).</summary>
    public int Chosen => Locate(Rows());

    /// <summary>The chosen row as the campaign stands now, or null when there are none.</summary>
    public Row? ChosenRow
    {
        get
        {
            var rows = Rows();
            int at = Locate(rows);
            return at >= 0 ? rows[at] : null;
        }
    }

    /// <summary>
    /// Where the choice is among <paramref name="rows"/>: the row at its place — or,
    /// should that place be gone from under it, its member's row; failing that, the
    /// first row. -1 for no rows.
    /// </summary>
    private int Locate(IReadOnlyList<Row> rows)
    {
        if (rows.Count == 0 || _chosen is not { } chosen)
            return rows.Count == 0 ? -1 : 0;
        int at = IndexOf(rows, chosen);
        if (at < 0)
            at = IndexOf(rows, chosen with { Slot = 0 });
        return Math.Max(at, 0);
    }

    private static int IndexOf(IReadOnlyList<Row> rows, Place place)
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Place == place)
                return i;
        return -1;
    }

    /// <summary>W/S: the choice to the next row up (<paramref name="step"/> −1) or down (+1), round the ends of the page — every row is a member or a potion, and each is chosen.</summary>
    public void Step(int step)
    {
        var rows = Rows();
        if (rows.Count == 0 || step == 0)
            return;
        int at = ((Locate(rows) + Math.Sign(step)) % rows.Count + rows.Count) % rows.Count;
        _chosen = rows[at].Place;
    }

    /// <summary>A/D: the quality a unit down (<paramref name="step"/> −1) or up (+1), held to 1 and the batch's units left.</summary>
    public void Shift(int step) => _quality = Math.Clamp(Quality + Math.Sign(step), 1, MostQuality);

    /// <summary>A potion has been mixed (<see cref="Alchemist.Mix"/>): the choice stays on the row it was on — the member, or the potion it was on, which has not moved — and the quality is held to what is left.</summary>
    public void AfterMix(Mixed mixed)
    {
        ArgumentNullException.ThrowIfNull(mixed);
        _quality = Quality;
    }

    /// <summary>
    /// A potion has been poured back (<see cref="Alchemist.PourBack"/>): the choice
    /// goes to the next potion in that member's bag, below where it lay, or else to the
    /// member's own row; the quality is kept, the batch having grown under it.
    /// </summary>
    public void AfterPourBack(PouredBack poured)
    {
        ArgumentNullException.ThrowIfNull(poured);
        var member = _campaign.Party.FirstOrDefault(m => m.Id == poured.MemberId);
        int next = member?.Potions.Select(carried => carried.Slot).Where(slot => slot > poured.Slot).DefaultIfEmpty(0).First() ?? 0;
        _chosen = new Place(poured.MemberId, next);
        _quality = Quality;
    }

    /// <summary>
    /// The first row a window of <paramref name="fit"/> rows shows, with the choice in
    /// it (<see cref="HubWeapons.ScrollTop"/>): kept from one frame to the next, so the
    /// window moves only as far as the choice asks; and a potion chosen asks for its
    /// member's row over it too, where the window has room, so it is never read
    /// without whose it is.
    /// </summary>
    public int Window(int fit)
    {
        var rows = Rows();
        int chosen = Math.Max(0, Locate(rows));
        int top = HubWeapons.ScrollTop(_top, chosen, rows.Count, fit);
        int over = chosen;
        while (over > 0 && rows[over].Kind == RowKind.Potion)
            over--;
        if (fit > 0 && over < top)
            top = Math.Max(over, chosen - fit + 1);
        _top = top;
        return top;
    }
}
