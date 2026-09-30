using System.Globalization;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The deposit panel between runs (§6.2, §6.7; Phase 6a.5): what D opens on a
/// weapon on the WEAPONS page, and what T opens on a weapon in the shop — the
/// enchanter's two questions, asked over the dimmed page. Its state machine, and
/// every string it says, are here, pure and tested (<c>DepositPanelTests</c>); the
/// scene routes the keys to it and does what it answers
/// (<see cref="BetweenRunsScene"/>), and the HUD draws it
/// (<see cref="BetweenRunsHud.PanelRows"/>).
/// <list type="bullet">
/// <item><strong>Services</strong>: <c>DEPOSIT THE ARMING SWORD</c>, the weapon's
/// wear, enchantments, open slots and runs in the shop, and the two services on
/// their number keys — <c>1  ENCHANT</c> and <c>2  REFINE</c> — each with the odds
/// its return improves the weapon anyway (<see cref="Wear.ImproveOdds"/>). One the
/// enchanter would refuse is dimmed, its reason in its odds' place, and its key
/// says the reason (PHASE 6a DECISION 12); an unworn weapon's refinement is not
/// refused — it says it will come back as it went (§6.4: "a fresh drop refines for
/// nothing").</item>
/// <item><strong>Offer</strong>: ENCHANT's list, every enchantment the campaign has
/// met that the enchanter offers (<see cref="Enchanter.Offers"/>), a refused one
/// dimmed with its reason and never hidden, one that would never fire on the
/// weapon noted — in a window that scrolls with the choice, since what a campaign
/// has met only grows. W/S choose among those not refused, ENTER chooses.</item>
/// <item><strong>Confirm</strong>: the panel's last line becomes the ask —
/// <c>DEPOSIT THE ARMING SWORD TO ENCHANT FLAMING - 2 RUNS - ITS 36 WEAR IS
/// SPENT</c> — and only ENTER deposits.</item>
/// <item><strong>Ask</strong>: a take-back's own question, on a weapon in the shop —
/// <c>TAKE BACK THE ARMING SWORD - ITS SERVICE IS LOST - ITS 36 WEAR COMES BACK</c>
/// (PHASE 6a DECISION 8: the deposit's cash is reversed) — and only ENTER takes it
/// back.</item>
/// </list>
/// ESC goes back a step at a time (<see cref="Back"/>), and from the services, or
/// on the ask, closes the panel with nothing done. The panel reads the campaign
/// once, as it opens: nothing changes it while the panel is up, since only its own
/// ENTER does — and that ENTER asks again before it acts (<see cref="RefusalNow"/>),
/// so a panel left up over a change, should a later hub action make one, says why
/// and does nothing.
/// </summary>
internal sealed class DepositPanel
{
    /// <summary>Which of its questions the panel is asking.</summary>
    internal enum Step
    {
        /// <summary>Which service: ENCHANT or REFINE, on their number keys.</summary>
        Services,

        /// <summary>Which enchantment an ENCHANT buys, off the offer list.</summary>
        Offer,

        /// <summary>Whether to deposit, the choice made: only ENTER says yes.</summary>
        Confirm,

        /// <summary>Whether to take a weapon in the shop back: only ENTER says yes.</summary>
        Ask,
    }

    /// <summary>
    /// One service the panel offers, on its number key: what it does, and either
    /// the odds its return improves the weapon anyway, what it says in their place,
    /// or why the enchanter would refuse it — which dims it.
    /// </summary>
    /// <param name="Key">The number key that picks it.</param>
    /// <param name="Service">The service.</param>
    /// <param name="Does">What it buys, in a few words: <c>A CHOSEN ENCHANTMENT</c>, <c>NOTHING ATTACHED</c>.</param>
    /// <param name="Refusal">Why the enchanter would refuse the weapon for it (<see cref="Enchanter.ServiceRefusal"/>), or null.</param>
    /// <param name="Odds">The odds its return improves the weapon anyway, <c>6.6 IN 100 TO IMPROVE</c>, or null for none: a deposit that cashes no wear rolls nothing.</param>
    /// <param name="Note">What it says in the odds' place, not a refusal: <see cref="NothingToRepair"/> on an unworn refinement; or null.</param>
    internal sealed record Option(int Key, Service Service, string Does, string? Refusal, string? Odds, string? Note)
    {
        /// <summary>The line: <c>2  REFINE - NOTHING ATTACHED - 6.6 IN 100 TO IMPROVE</c>, or dimmed, <c>1  ENCHANT - NO FREE SLOT - SLOT 2 OPENS AT CAPACITY 35 - IT IS 31</c>.</summary>
        public string Text => Refusal is { } refusal
            ? $"{Key}  {ServiceName(Service)} - {refusal}"
            : $"{Key}  {ServiceName(Service)} - {Does} - {Odds ?? Note}";

        /// <summary>Whether it is refused: drawn dimmed, and its key says why.</summary>
        public bool Dimmed => Refusal is not null;
    }

    // ── What it says ─────────────────────────────────────────────────────────

    /// <summary>What an ENCHANT buys, on its line.</summary>
    internal const string EnchantDoes = "A CHOSEN ENCHANTMENT";

    /// <summary>What a REFINE buys, on its line: the deposit is the wear it cashes, and the capacity grows on it.</summary>
    internal const string RefineDoes = "NOTHING ATTACHED";

    /// <summary>
    /// An unworn weapon's refinement, in its odds' place — a note, never a
    /// refusal (§6.4: "a fresh drop refines for nothing … no wear, no roll"): it
    /// costs its runs, grows nothing and rolls nothing.
    /// </summary>
    internal const string NothingToRepair = "NOTHING TO REPAIR - IT WILL COME BACK AS IT WENT";

    /// <summary>
    /// A deposit's weapon gone from where the panel found it, as the confirm's ENTER
    /// finds it (<see cref="RefusalNow"/>): <c>THE TOWER GUARD HAS MOVED - NOTHING
    /// DEPOSITED</c>. Nothing moves a weapon while a panel is up yet, so it is not
    /// seen in play.
    /// </summary>
    internal static string Moved(Weapon weapon) => $"{HubWeapons.The(weapon)} HAS MOVED - NOTHING DEPOSITED";

    /// <summary>
    /// A take-back's deposit gone from the shop, as the ask's ENTER finds it
    /// (<see cref="RefusalNow"/>): <c>THE TOWER GUARD IS NOT IN THE SHOP - NOTHING
    /// TAKEN BACK</c>. Nothing empties the shop while the ask is up yet, so it is not
    /// seen in play.
    /// </summary>
    internal static string NotInTheShop(Weapon weapon) => $"{HubWeapons.The(weapon)} IS NOT IN THE SHOP - NOTHING TAKEN BACK";

    /// <summary>The keys while the services are up: a number picks one, ESC closes the panel.</summary>
    internal const string ServicesKeys = "1 OR 2 CHOOSES - ESC BACK";

    /// <summary>The keys while the offer list is up.</summary>
    internal const string OfferKeys = "W/S CHOOSE - ENTER CHOOSES - ESC BACK";

    /// <summary>The keys while a deposit is asked: only ENTER deposits.</summary>
    internal const string ConfirmKeys = "ENTER: DEPOSIT - ESC: BACK";

    /// <summary>The keys while a take-back is asked: only ENTER takes it back.</summary>
    internal const string AskKeys = "ENTER: TAKE IT BACK - ESC: NO";

    /// <summary>A service as the panel names it: <c>ENCHANT</c>, <c>REFINE</c>.</summary>
    internal static string ServiceName(Service service) => service switch
    {
        Service.Enchant => "ENCHANT",
        Service.Refine => "REFINE",
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Not a Service."),
    };

    /// <summary>The panel's heading: <c>DEPOSIT THE ARMING SWORD</c>.</summary>
    internal static string Heading(Weapon weapon) => $"DEPOSIT {HubWeapons.The(weapon)}";

    /// <summary>
    /// The weapon as a deposit meets it (R4): its wear over its capacity — what the
    /// deposit cashes — its enchantments of the most there are, the slots its
    /// capacity has opened, and the runs any service takes, its count plus one:
    /// <c>WEAR 36 OF 36 - ENCHANTMENTS 1 OF 5 - SLOTS OPEN 2 - 2 RUNS IN THE SHOP</c>.
    /// </summary>
    internal static string StateLine(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        int count = weapon.Enchantments.Count;
        return $"WEAR {weapon.Wear} OF {weapon.WearCapacity} - ENCHANTMENTS {count} OF {GameContent.Current.Tuning.EnchantmentSlots}"
            + $" - SLOTS OPEN {Wear.SlotsOpen(weapon.WearCapacity)} - {DungeonHud.Runs(Wear.ServiceRuns(count))} IN THE SHOP";
    }

    /// <summary>
    /// A chance out of 100 as the panel says it (the engine's font has no percent
    /// sign): whole when it is exact, and otherwise to one place, cut short rather
    /// than rounded, so it never reads better than it is — <c>6</c>, <c>6.6</c>,
    /// <c>6.1</c> — and <c>100</c> at or past certain.
    /// </summary>
    /// <param name="numerator">The odds' numerator (<see cref="Wear.ImproveOdds"/>).</param>
    /// <param name="denominator">The odds' denominator, at least 1.</param>
    internal static string Chance(long numerator, long denominator)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(numerator);
        ArgumentOutOfRangeException.ThrowIfLessThan(denominator, 1);
        if (numerator >= denominator)
            return "100";
        long hundredths = numerator * 100;
        if (hundredths % denominator == 0)
            return (hundredths / denominator).ToString(CultureInfo.InvariantCulture);
        long tenths = numerator * 1000 / denominator;
        return $"{tenths / 10}.{tenths % 10}";
    }

    /// <summary>The odds a service's return improves the weapon anyway, on the wear it would cash (§6.4; <see cref="Wear.ImproveOdds"/>): <c>3 IN 100 TO IMPROVE</c>, <c>6.6 IN 100 TO IMPROVE</c>.</summary>
    internal static string Odds(Service service, int wear)
    {
        var (numerator, denominator) = Wear.ImproveOdds(service, wear);
        return $"{Chance(numerator, denominator)} IN 100 TO IMPROVE";
    }

    /// <summary>The offer list's heading: which enchantment the ENCHANT attaches and its runs, <c>ENCHANT THE ARMING SWORD - ENCHANTMENT 2 - 2 RUNS IN THE SHOP</c>.</summary>
    internal static string OfferHeading(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        int count = weapon.Enchantments.Count;
        return $"ENCHANT {HubWeapons.The(weapon)} - ENCHANTMENT {count + 1} - {DungeonHud.Runs(Wear.ServiceRuns(count))} IN THE SHOP";
    }

    /// <summary>
    /// One offer's line: the enchantment and the mana it locks at the tier it is
    /// attached at, <c>FLAMING - LOCK 20</c> — or, refused, its reason, which names
    /// it first (<c>COLD - CANNOT SIT WITH FLAMING</c>) — and then its note, refused
    /// or not: <c>REGENERATION - LOCK 15 - NEVER FIRES ON A SWORD</c>.
    /// </summary>
    internal static string OfferLine(Offer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        string head = offer.Refusal
            ?? $"{BetweenRunsHud.EnchantmentName(offer.Id)} - LOCK {Enchantment.LockAt(GameContent.Current.Enchantments[offer.Id], Enchanter.AttachedTier)}";
        return offer.Note is { } note ? $"{head} - {note}" : head;
    }

    /// <summary>
    /// The confirm's ask, the weapon as it stands: what the deposit buys, its runs,
    /// and the wear it spends — <c>DEPOSIT THE ARMING SWORD TO ENCHANT FLAMING - 2
    /// RUNS - ITS 36 WEAR IS SPENT</c>, <c>… TO REFINE - …</c> — or, unworn,
    /// <c>DEPOSIT THE WEAKSPOT STILETTO TO REFINE - 1 RUN - IT CARRIES NO WEAR</c>.
    /// </summary>
    internal static string DepositAsk(Weapon weapon, Service service, string? enchantmentId)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        string buys = service == Service.Enchant && enchantmentId is { } id ? $"ENCHANT {BetweenRunsHud.EnchantmentName(id)}" : ServiceName(service);
        string spent = weapon.Wear > 0 ? $"ITS {weapon.Wear} WEAR IS SPENT" : "IT CARRIES NO WEAR";
        return $"DEPOSIT {HubWeapons.The(weapon)} TO {buys} - {DungeonHud.Runs(Wear.ServiceRuns(weapon.Enchantments.Count))} - {spent}";
    }

    /// <summary>
    /// A take-back's ask (PHASE 6a DECISION 8): the service forfeit, and the wear
    /// the deposit cashed coming back — <c>TAKE BACK THE ARMING SWORD - ITS SERVICE IS
    /// LOST - ITS 36 WEAR COMES BACK</c> — or, where it cashed none, <c>… - IT CARRIED
    /// NO WEAR</c>. Its capacity goes back with the wear. Named as the weapon is, so
    /// it reads nothing of the content.
    /// </summary>
    internal static string TakeBackAsk(Deposit deposit)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        string wear = deposit.WearAtDeposit > 0 ? $"ITS {deposit.WearAtDeposit} WEAR COMES BACK" : "IT CARRIED NO WEAR";
        return $"TAKE BACK {HubWeapons.The(deposit.Weapon)} - ITS SERVICE IS LOST - {wear}";
    }

    // ── The panel's state ────────────────────────────────────────────────────

    private readonly Option[] _services;
    private readonly Offer[] _offers;

    // Which question is up; the service chosen, and the offer chosen and the first
    // the offer list's window shows — the panel's alone: nothing is deposited
    // until the confirm's ENTER.
    private Step _step;
    private Service _service;
    private int _chosen;
    private int _top;

    private DepositPanel(Weapon weapon, WeaponPlace? from, Deposit? deposit, Option[] services, Offer[] offers, Step step)
    {
        Weapon = weapon;
        From = from;
        TakingBack = deposit;
        _services = services;
        _offers = offers;
        _step = step;
        _chosen = Next(-1, +1);
    }

    /// <summary>
    /// D on the weapon at <paramref name="from"/>: the panel on its services, each
    /// asked of the enchanter as the campaign stands (<see cref="Enchanter.ServiceRefusal"/>),
    /// with the offer an ENCHANT would choose from (<see cref="Enchanter.Offers"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing lies at <paramref name="from"/>.</exception>
    public static DepositPanel Open(CampaignState campaign, WeaponPlace from)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var weapon = campaign.WeaponAt(from) ?? throw new InvalidOperationException($"Nothing lies at {from} to deposit.");
        bool worn = weapon.Wear > 0;
        Option[] services =
        [
            new(1, Service.Enchant, EnchantDoes, Enchanter.ServiceRefusal(campaign, weapon, Service.Enchant), Odds(Service.Enchant, weapon.Wear), null),
            new(2, Service.Refine, RefineDoes, Enchanter.ServiceRefusal(campaign, weapon, Service.Refine),
                worn ? Odds(Service.Refine, weapon.Wear) : null, worn ? null : NothingToRepair),
        ];
        return new DepositPanel(weapon, from, null, services, [.. Enchanter.Offers(campaign, weapon)], Step.Services);
    }

    /// <summary>T on a weapon in the shop: the panel asking whether to take <paramref name="deposit"/> back (PHASE 6a DECISION 8).</summary>
    public static DepositPanel Asking(Deposit deposit)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        return new DepositPanel(deposit.Weapon, null, deposit, [], [], Step.Ask);
    }

    /// <summary>The weapon the panel is about: the one to deposit, or the one in the shop to take back.</summary>
    public Weapon Weapon { get; }

    /// <summary>Where the weapon to deposit lay as the panel opened; null on a take-back's ask, which deposits nothing.</summary>
    public WeaponPlace? From { get; }

    /// <summary>The deposit a take-back's ask is about, or null for a deposit.</summary>
    public Deposit? TakingBack { get; }

    /// <summary>Which question is up.</summary>
    public Step Current => _step;

    /// <summary>The services, on their number keys.</summary>
    public IReadOnlyList<Option> Services => _services;

    /// <summary>What an ENCHANT chooses from, refused ones among them: sorted by id, as <see cref="CampaignState.SeenInOrder"/> hands them out (the same on every machine), not in the order the campaign met them.</summary>
    public IReadOnlyList<Offer> Offers => _offers;

    /// <summary>The index of the offer chosen, or -1 when none can be.</summary>
    public int Chosen => _chosen;

    /// <summary>The service the confirm deposits for.</summary>
    public Service Service => _service;

    /// <summary>The enchantment the confirm's ENCHANT attaches, or null for a refinement.</summary>
    public string? EnchantmentId => _service == Service.Enchant && _chosen >= 0 ? _offers[_chosen].Id : null;

    /// <summary>The ask on the panel's last line: the confirm's or the take-back's; null while a choice is still to be made.</summary>
    public string? Ask => _step switch
    {
        Step.Confirm => DepositAsk(Weapon, _service, EnchantmentId),
        Step.Ask => TakeBackAsk(TakingBack!),
        _ => null,
    };

    /// <summary>The keys the panel answers now, which the keys line along the bottom says in the hub's keys' place.</summary>
    public string KeysLine => _step switch
    {
        Step.Services => ServicesKeys,
        Step.Offer => OfferKeys,
        Step.Confirm => ConfirmKeys,
        _ => AskKeys,
    };

    /// <summary>
    /// A number key on the services: the service on it picked — ENCHANT to the offer
    /// list, the choice on the first offer not refused; REFINE straight to the
    /// confirm — or, dimmed, not picked, and its reason handed back for the screen
    /// to say. Anywhere else, or a key no service is on, nothing.
    /// </summary>
    /// <returns>Why the service on the key is refused, or null.</returns>
    public string? Pick(int digit)
    {
        if (_step != Step.Services || _services.FirstOrDefault(option => option.Key == digit) is not { } option)
            return null;
        if (option.Refusal is { } refused)
            return refused;
        _service = option.Service;
        if (option.Service == Service.Enchant)
        {
            _chosen = Next(-1, +1);
            if (_chosen >= 0)   // the service is refused with nothing to offer, so there is always one
                _step = Step.Offer;
        }
        else
        {
            _step = Step.Confirm;
        }
        return null;
    }

    /// <summary>W/S on the offer list: the choice to the next offer up (<paramref name="step"/> −1) or down (+1) not refused, round the ends. Anywhere else, nothing.</summary>
    public void Move(int step)
    {
        if (_step == Step.Offer)
            _chosen = Next(_chosen, step);
    }

    /// <summary>ENTER on the offer list: the offer chosen is the one, and the confirm asks. Anywhere else, nothing.</summary>
    public void ChooseOffer()
    {
        if (_step == Step.Offer && _chosen >= 0)
            _step = Step.Confirm;
    }

    /// <summary>
    /// ESC: back a step — from the confirm to the question it answered (the offer
    /// list for an ENCHANT, the services for a REFINE), from the offer list to the
    /// services — and from the services, or on a take-back's ask, the panel is done
    /// with, nothing deposited and nothing taken back. Reads nothing of the content.
    /// </summary>
    /// <returns>True when the panel closes.</returns>
    public bool Back()
    {
        switch (_step)
        {
            case Step.Confirm:
                _step = _service == Service.Enchant ? Step.Offer : Step.Services;
                return false;
            case Step.Offer:
                _step = Step.Services;
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// Why the ENTER that answers the confirm or the ask cannot be done now, or null
    /// when it can — asked of the campaign as it stands at the ENTER, not as the
    /// panel read it when it opened: for a deposit, the weapon still where it lay
    /// (<see cref="Moved"/>) and the enchanter still taking it for the service and
    /// the enchantment chosen (<see cref="Enchanter.Refusal"/>, the gates
    /// <see cref="Enchanter.Deposit"/> asks and throws on); for a take-back, the
    /// deposit still in the shop (<see cref="NotInTheShop"/>). Nothing changes the
    /// campaign while a panel is up yet, so in play it answers null: it is there so
    /// a panel left up over a change says why and does nothing, rather than
    /// throwing through the enchanter's doors.
    /// </summary>
    /// <exception cref="InvalidOperationException">Neither the confirm nor the ask is up: no ENTER answers the panel yet.</exception>
    public string? RefusalNow(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        switch (_step)
        {
            case Step.Ask:
                return campaign.AtTheEnchanter.Any(queued => ReferenceEquals(queued, TakingBack)) ? null : NotInTheShop(Weapon);
            case Step.Confirm:
                if (From is not { } from || !Lies(campaign, from, Weapon))
                    return Moved(Weapon);
                return Enchanter.Refusal(campaign, Weapon, _service, EnchantmentId);
            default:
                throw new InvalidOperationException($"The panel is on its {_step}: only the confirm and the ask are answered with an ENTER.");
        }
    }

    /// <summary>Whether <paramref name="weapon"/> lies at <paramref name="place"/> now — asked without the throws of <see cref="CampaignState.WeaponAt"/>, since a place the panel read may no longer be one the campaign has.</summary>
    private static bool Lies(CampaignState campaign, WeaponPlace place, Weapon weapon)
    {
        if (place.IsStable)
            return place.Index >= 0 && place.Index < campaign.Stable.Count && ReferenceEquals(campaign.Stable[place.Index], weapon);
        var member = campaign.Party.FirstOrDefault(member => member.Id == place.MemberId);
        return member is not null && place.Index >= 0 && place.Index < member.Inventory.Length && ReferenceEquals(member.Inventory[place.Index], weapon);
    }

    /// <summary>
    /// The first offer a window of <paramref name="fit"/> lines shows, with the
    /// choice in it (<see cref="HubWeapons.ScrollTop"/>): kept from one frame to the
    /// next, so the window moves only as far as the choice asks.
    /// </summary>
    public int Window(int fit)
    {
        _top = HubWeapons.ScrollTop(_top, Math.Max(0, _chosen), _offers.Length, fit);
        return _top;
    }

    /// <summary>The next offer from <paramref name="from"/>, a step at a time round the ends, that is not refused; <paramref name="from"/> itself when no other is, and -1 when none is.</summary>
    private int Next(int from, int step)
    {
        int count = _offers.Length;
        int direction = Math.Sign(step);
        if (count == 0 || direction == 0)
            return from;
        int at = from;
        for (int moved = 0; moved < count; moved++)
        {
            at = ((at + direction) % count + count) % count;
            if (_offers[at].Refusal is null)
                return at;
        }
        return from;
    }
}
