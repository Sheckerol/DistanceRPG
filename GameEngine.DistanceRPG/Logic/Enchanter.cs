namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What a deposit at the enchanter buys (§6.2, §6.4, §6.7): one of two services,
/// chosen as the weapon goes in. Whichever it is, the deposit cashes the weapon's
/// whole pool and grows its capacity alike (R4), and it takes the weapon's count
/// plus one runs (<see cref="Wear.ServiceRuns"/>).
/// </summary>
public enum Service
{
    /// <summary>A chosen enchantment, one the campaign has met, attached last at tier 1 when the service finishes: the enchanter sells breadth, never a tier (§6.2).</summary>
    Enchant,

    /// <summary>Nothing attached: the deposit is the wear it cashes, which grows the capacity as any deposit's does, and opens the slots along it. Declining an enchantment is always this (§6.2).</summary>
    Refine,
}

/// <summary>
/// One deposit in the shop (§6.7's record, extended — PHASE 6a DECISION 13): the
/// weapon, out of the party's hands until its service is done or it is taken back;
/// the service; the wear the deposit cashed; the runs the service takes, fixed as
/// it went in — the count cannot change while the weapon is in the shop — and how
/// many of them the runs that counted have worked. The ordinal is the campaign's
/// count of deposits as it went in (<see cref="CampaignState.DepositsMade"/>),
/// never reused: the enchanter works the newest, the highest ordinals.
/// <para>
/// A class, not a record: <see cref="RunsWorked"/> moves, so a deposit is the one
/// it is by reference — as the shop, the clock and the hub's rows tell them apart
/// — and never equal to another by its values.
/// </para>
/// </summary>
/// <param name="ordinal">The deposit's place in the campaign's deposits, from 0: the higher, the newer.</param>
/// <param name="weapon">The weapon in the shop.</param>
/// <param name="service">What the deposit bought.</param>
/// <param name="wearAtDeposit">The wear the deposit cashed: the weapon's whole pool as it went in.</param>
/// <param name="runs">The runs the service takes: the weapon's count plus one, as it went in.</param>
/// <param name="enchantmentId">The enchantment an Enchant attaches, by id; null for a refinement.</param>
public sealed class Deposit(int ordinal, Weapon weapon, Service service, int wearAtDeposit, int runs, string? enchantmentId = null)
{
    /// <summary>The deposit's place in the campaign's deposits, from 0: the higher, the newer.</summary>
    public int Ordinal { get; } = ordinal;

    /// <summary>The weapon in the shop.</summary>
    public Weapon Weapon { get; } = weapon;

    /// <summary>What the deposit bought.</summary>
    public Service Service { get; } = service;

    /// <summary>The wear the deposit cashed: the weapon's whole pool as it went in.</summary>
    public int WearAtDeposit { get; } = wearAtDeposit;

    /// <summary>The runs the service takes: the weapon's count plus one, as it went in.</summary>
    public int Runs { get; } = runs;

    /// <summary>The enchantment an Enchant attaches, by id; null for a refinement.</summary>
    public string? EnchantmentId { get; } = enchantmentId;

    /// <summary>The runs of the service the runs that counted have worked: 0 as it goes in, and below <see cref="Runs"/> while it is in the shop — the tick that works the last one brings it back.</summary>
    public int RunsWorked { get; internal set; }
}

/// <summary>
/// What <see cref="Enchanter.Deposit"/> did, for the screen that asked it to log
/// it (Logic never logs): the entry now in the shop, the weapon's capacity before
/// and after the deposit's cash, and the weapon a hand the deposit emptied took up
/// from the bag, or null.
/// </summary>
public sealed record DepositMade(Deposit Entry, PoolProgress Before, PoolProgress After, Weapon? Armed);

/// <summary>
/// One enchantment the enchanter offers a weapon (§6.2, "you pick the
/// enchantment"; <see cref="Enchanter.Offers"/>): an id the campaign has met, why
/// the enchanter would refuse to attach it to that weapon — or null when it would
/// — and what the screen says beside it, or null. The two are asked apart, so an
/// offer may carry both. A refused offer is listed with its reason and never
/// hidden (PHASE 6a DECISION 12).
/// </summary>
/// <param name="Id">The enchantment, by id.</param>
/// <param name="Refusal">Why it would not go on the weapon, in the HUD's font and naming the entry first — <c>COLD - CANNOT SIT WITH FLAMING</c> — or null.</param>
/// <param name="Note">What the screen says beside the entry, whether or not it is refused — <c>NEVER FIRES ON A SWORD</c> — or null.</param>
public sealed record Offer(string Id, string? Refusal, string? Note);

/// <summary>
/// The enchanter (§6.2–§6.4, §6.7; Phase 6a.3a): where wear is spent, a weapon's
/// capacity grows, and enchantments are bought — slowly, in runs that count.
/// Engine-free and deterministic, like everything under <c>Logic/</c>, and the one
/// place wear is spent (P6:860–861).
/// <list type="bullet">
/// <item><strong>The offer</strong> (<see cref="Offers"/>, Phase 6a.3b) is what an
/// Enchant may buy: what the campaign has met, less the unique souls and the
/// kinds that do nothing yet, each with the reason the enchanter would refuse it
/// on the weapon — the relations, a status onto a wand, and a status for the
/// other side onto a staff — or none.</item>
/// <item><strong>A deposit</strong> (<see cref="Deposit"/>), between runs only,
/// takes a weapon out of the party's hands and into the shop, cashes its whole
/// pool — the capacity growing on the cash, a point a full bar, whatever the
/// service (R4) — and queues it for its count plus one runs. The gates are asked
/// first (<see cref="Refusal"/>), and nothing is attached or rolled yet (PHASE 6a
/// DECISION 3).</item>
/// <item><strong>The clock</strong> (<see cref="Tick"/>) is the run counter's
/// tick: every run's end asks it, the visit already gone. A run that counts works
/// the newest <see cref="Tuning.EnchanterWorksOn"/> deposits a run on — every
/// other deposit stalls — and a service whose last run is worked finishes: an
/// Enchant attaches its enchantment last, at tier 1 with no XP, and the weapon
/// goes to the end of the stable (PHASE 6a DECISION 7). A run that does not count
/// works nothing.</item>
/// <item><strong>A take-back</strong> (<see cref="TakeBack"/>), between runs,
/// returns a queued weapon to the stable with its service forfeit, and reverses
/// the deposit's cash exactly — the wear back in the pool, the capacity as before
/// (PHASE 6a DECISION 8).</item>
/// </list>
/// Every number is <c>tuning.json</c>'s (<see cref="Wear"/>); the service's
/// <c>+ 1</c> is a rule (PHASE 6a DECISION 6).
/// </summary>
public static class Enchanter
{
    /// <summary>The tier an enchantment is attached at: the enchanter sells breadth and never a tier (§6.2, S:33–36), and "can cut a circle", no more (S:1097–1100).</summary>
    public const int AttachedTier = 1;

    // ── The offer ─────────────────────────────────────────────────────────────

    /// <summary>
    /// What the enchanter offers to attach to <paramref name="weapon"/> (§6.2:
    /// "you pick the enchantment, from everything your campaign has seen"): every
    /// enchantment the campaign has met, in <see cref="CampaignState.SeenInOrder"/>'s
    /// order, less the two sorts it never offers —
    /// <list type="bullet">
    /// <item>an id <c>restricted.json</c> says no roll grants and no service
    /// copies (<see cref="EnchantmentCatalogue.NeverRolled"/>: the unique souls, as
    /// shipped, "transferable but never catalogued", S:279–281);</item>
    /// <item>an entry of a kind declared and inert (<see cref="LootTable.IsInert"/>),
    /// the live-behaviour test a drop's pool applies: so <c>echoing</c> is never
    /// offered, a lock reserved for nothing (open-questions.md, the Echoing entry).
    /// <c>aegis</c> passes, but nothing puts it among what a campaign has met
    /// (PHASE 6a DECISION 4).</item>
    /// </list>
    /// Nothing else is left out (PHASE 6a DECISION 12). Each offer says why the
    /// enchanter would refuse to attach it to this weapon, the first to apply, or
    /// nothing:
    /// <list type="number">
    /// <item>it and an entry the weapon carries are an exclusion group of
    /// <c>restricted.json</c>'s (<see cref="EnchantmentCatalogue.Excludes"/>), the
    /// weapon's asked in attachment order, so the pair named is the same every
    /// time (<see cref="CannotSitWith"/>);</item>
    /// <item>a prerequisite of its is not on the weapon ahead of where it would
    /// land, last — the question content validation asks of every weapon
    /// (<see cref="EnchantmentCatalogue.LacksPrerequisiteAhead"/>;
    /// <see cref="NeedsAhead"/>);</item>
    /// <item>it is a staff's kind, a status its cast lands, and the weapon is a
    /// wand, whose area cast would land it on the caster — the content rule
    /// <see cref="ContentValidator.RuleWandNoStatusApplier"/>'s question
    /// (<see cref="ContentValidator.NoWandCarries"/>; <see cref="WandCastsNoStatus"/>).
    /// This bends §3.3's "any enchantment goes on any weapon" (PHASE 6a DECISION
    /// 12);</item>
    /// <item>it is a staff's kind for one side, and the weapon a staff whose cast
    /// is for the other: the cast lands every status its entries apply on the one
    /// body its first entry chose, so Mire on a Staff of Renewal would mire the
    /// ally it casts on (<see cref="StaffCastsItOn"/>). The same bend, for the same
    /// hazard — a status landing on the wrong body (PHASE 6a DECISION 12).</item>
    /// </list>
    /// An entry that fires on no event the weapon raises is offered all the same,
    /// with a note (<see cref="NeverFiresOn"/>): the events are its class's, as a
    /// drop's pool reads them, but a staff's are its own — it casts on one body and
    /// never strikes, so nothing that rides a strike or a kill fires on it. A
    /// defender's step, which fires whenever the wielder is hit, is never that
    /// (<see cref="EnchantmentBehaviours.DefendsTheWielder"/>). The same enchantment
    /// again is offered — only opposed enchantments exclude each other (S:352–354)
    /// — and a lock the wielder could not afford is never asked: the entry is
    /// dormant (S:41–42), so where the weapon is, and who carries it, change nothing.
    /// </summary>
    /// <param name="campaign">The campaign whose met enchantments are offered.</param>
    /// <param name="weapon">The weapon they would be attached to, last.</param>
    public static IReadOnlyList<Offer> Offers(CampaignState campaign, Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(weapon);
        var catalogue = GameContent.Current.Enchantments;
        var offers = new List<Offer>();
        foreach (string id in campaign.SeenInOrder)
        {
            var def = catalogue[id];   // what a campaign has met is the content's: a load resolves every id
            if (NeverOffered(def) is null)
                offers.Add(OfferOf(weapon, def));
        }
        return offers;
    }

    /// <summary>
    /// Why <paramref name="def"/> is never offered, whatever the weapon: an id
    /// <c>restricted.json</c> never copies (<see cref="NeverCopied"/>), or a kind
    /// that does nothing yet (<see cref="DoesNothingYet"/>); null for every other.
    /// </summary>
    private static string? NeverOffered(EnchantmentDef def)
        => GameContent.Current.Enchantments.NeverRolled.Contains(def.Id) ? NeverCopied(def)
            : LootTable.IsInert(def) ? DoesNothingYet(def)
            : null;

    /// <summary>The offer of <paramref name="def"/> to <paramref name="weapon"/>: why the enchanter would refuse to attach it there, last, and whether it would never fire there (<see cref="Offers"/>) — each asked on its own.</summary>
    private static Offer OfferOf(Weapon weapon, EnchantmentDef def)
        => new(def.Id, OfferRefusal(weapon, def), FiresOn(weapon, def) ? null : NeverFiresOn(weapon.Class));

    /// <summary>The relations, the wand and the staff's side, asked of <paramref name="def"/> attached last to <paramref name="weapon"/>, in <see cref="Offers"/>' order; null when none refuses it.</summary>
    private static string? OfferRefusal(Weapon weapon, EnchantmentDef def)
    {
        var catalogue = GameContent.Current.Enchantments;
        foreach (var entry in weapon.Enchantments)
            if (catalogue.Excludes(def.Id, entry.Id))
                return CannotSitWith(def, entry.Def);
        if (catalogue.LacksPrerequisiteAhead(def.Id, weapon.Enchantments.Select(entry => entry.Id), out var need))
            return NeedsAhead(def, catalogue.TryGet(need, out var needed) ? needed.Name : need);
        if (weapon.Class == WeaponClass.Wand && ContentValidator.NoWandCarries(def))
            return WandCastsNoStatus(def);
        // A staff's cast: its first entry picks the side (TurnSystem.CanCastOn), and
        // every status its entries apply lands on that one body (ApplyCast).
        if (weapon.Innate is { Def.Applies: not null } cast && def.Effect == EffectKind.ApplyStatus
            && def.Targets != TargetSide.Any && def.Targets != cast.Def.Targets)
            return StaffCastsItOn(def, cast.Def.Targets);
        return null;
    }

    /// <summary>Whether <paramref name="def"/> ever fires on <paramref name="weapon"/>: a behaviour on an event it raises (<see cref="Raises"/>), or a step on its wielder when hit.</summary>
    private static bool FiresOn(Weapon weapon, EnchantmentDef def)
        => EnchantmentBehaviours.DefendsTheWielder(def.Effect)
            || Raises(weapon).Any(raised => EnchantmentBehaviours.FiresOn(raised, def.Effect));

    /// <summary>
    /// The events whose loops run <paramref name="weapon"/>'s entries. A staff — a
    /// caster with no area shape — casts on one body and never strikes: a caster
    /// cannot swing, and neither a counter nor a reaction takes one, so no strike
    /// and no kill runs its entries; it casts, and its wielder can be healed past
    /// full.
    /// Every other weapon raises what its class does (<see cref="LootTable.RaisedBy"/>,
    /// the drop pool's test, which counts a staff as striking — harmless there, since
    /// a staff's drop draws no entry).
    /// </summary>
    private static GameEvent[] Raises(Weapon weapon)
        => weapon.IsCaster && weapon.AreaShape is null
            ? [GameEvent.Cast, GameEvent.HealingAboveFull]
            : LootTable.RaisedBy(weapon.Class);

    /// <summary>An offer <c>restricted.json</c> excludes beside an entry the weapon carries (PHASE 6a DECISION 12): <c>COLD - CANNOT SIT WITH FLAMING</c>.</summary>
    public static string CannotSitWith(EnchantmentDef offered, EnchantmentDef carried)
    {
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(carried);
        return $"{offered.Name.ToUpperInvariant()} - CANNOT SIT WITH {carried.Name.ToUpperInvariant()}";
    }

    /// <summary>An offer whose prerequisite is not on the weapon ahead of it (PHASE 6a DECISION 12): <c>BURNING - NEEDS FLAMING AHEAD OF IT</c>.</summary>
    public static string NeedsAhead(EnchantmentDef offered, string needed)
    {
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(needed);
        return $"{offered.Name.ToUpperInvariant()} - NEEDS {needed.ToUpperInvariant()} AHEAD OF IT";
    }

    /// <summary>A status-applying offer to a wand (PHASE 6a DECISION 12): <c>REGENERATION - A WAND CASTS NO STATUS</c>.</summary>
    public static string WandCastsNoStatus(EnchantmentDef offered)
    {
        ArgumentNullException.ThrowIfNull(offered);
        return $"{offered.Name.ToUpperInvariant()} - A WAND CASTS NO STATUS";
    }

    /// <summary>A status for one side offered to a staff whose cast is for the other (PHASE 6a DECISION 12), naming the side <paramref name="casts"/> it would land on: <c>MIRE - THIS STAFF WOULD CAST IT ON AN ALLY</c>.</summary>
    public static string StaffCastsItOn(EnchantmentDef offered, TargetSide casts)
    {
        ArgumentNullException.ThrowIfNull(offered);
        string side = casts switch
        {
            TargetSide.Ally => "AN ALLY",
            TargetSide.Enemy => "AN ENEMY",
            _ => throw new ArgumentOutOfRangeException(nameof(casts), casts, "A staff casts on allies or on enemies, never either (ContentValidator.RuleCasterInnate)."),
        };
        return $"{offered.Name.ToUpperInvariant()} - THIS STAFF WOULD CAST IT ON {side}";
    }

    /// <summary>
    /// The note on an offer that fires on no event a <paramref name="cls"/> raises,
    /// offered all the same (PHASE 6a DECISION 12): <c>NEVER FIRES ON A SWORD</c>,
    /// <c>NEVER FIRES ON AN AXE</c>, <c>NEVER FIRES ON A RANGED WEAPON</c> — the
    /// class as the HUD names it, made a noun where its name is not one.
    /// </summary>
    public static string NeverFiresOn(WeaponClass cls)
    {
        string name = cls.ToString().ToUpperInvariant();
        string noun = cls is WeaponClass.Ranged or WeaponClass.Throwing ? $"{name} WEAPON" : name;
        return $"NEVER FIRES ON {("AEIOU".Contains(noun[0]) ? "AN" : "A")} {noun}";
    }

    /// <summary>An Enchant with nothing left to choose (R-none): every offer refused on the weapon, or none left. <c>NOTHING TO OFFER - NO ENCHANTMENT MET YET CAN GO ON IT</c>.</summary>
    public const string NothingToOffer = "NOTHING TO OFFER - NO ENCHANTMENT MET YET CAN GO ON IT";

    /// <summary>An enchantment <c>restricted.json</c> never copies, met on a unique as shipped: <c>SERRATED IS NEVER COPIED - THE ENCHANTER DOES NOT OFFER IT</c>. The screen lists only offers, so only a caller that names one meets it.</summary>
    public static string NeverCopied(EnchantmentDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        return $"{def.Name.ToUpperInvariant()} IS NEVER COPIED - THE ENCHANTER DOES NOT OFFER IT";
    }

    /// <summary>An enchantment of a kind declared and inert: <c>ECHOING DOES NOTHING YET - THE ENCHANTER DOES NOT OFFER IT</c>. The screen lists only offers, so only a caller that names one meets it.</summary>
    public static string DoesNothingYet(EnchantmentDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        return $"{def.Name.ToUpperInvariant()} DOES NOTHING YET - THE ENCHANTER DOES NOT OFFER IT";
    }

    // ── The gates ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Why the enchanter would refuse <paramref name="weapon"/> for
    /// <paramref name="service"/> now, or null when it would take it — the gates
    /// in the order they are asked, the first to fail the reason, each in the HUD's
    /// font for the screen to say (§1g's strings). First the service's own, which
    /// name no enchantment (<see cref="ServiceRefusal"/>'s, all but the last); then,
    /// for an Enchant, the enchantment it names:
    /// <list type="number">
    /// <item>one the campaign has met (§6.2's offer, <see cref="CampaignState.HasSeen"/>;
    /// <see cref="Unseen"/>);</item>
    /// <item>one the enchanter offers at all — not an id <c>restricted.json</c>
    /// never copies, nor a kind that does nothing yet (<see cref="Offers"/>;
    /// <see cref="NeverCopied"/>, <see cref="DoesNothingYet"/>);</item>
    /// <item>and its offer to this weapon not refused: an id whose offer is
    /// refused is refused with the offer's own reason (the relations, a status
    /// onto a wand, a status for the other side onto a staff).</item>
    /// </list>
    /// <see cref="ServiceRefusal"/>'s last gate, <see cref="NothingToOffer"/>, is
    /// not asked: an id that passes these three is itself an offer the enchanter
    /// would attach, so something is left to choose, and an id that fails one is
    /// refused with its own reason, which says more. The screen lists only offers,
    /// so the first two meet only a caller that names another. A lock the wielder
    /// could not afford is never a refusal: the enchantment is dormant (S:41–42).
    /// </summary>
    /// <param name="campaign">The campaign between its runs.</param>
    /// <param name="weapon">A weapon the party owns: in a member's bag, in the stable, or in the shop.</param>
    /// <param name="service">What the deposit would buy.</param>
    /// <param name="enchantmentId">The enchantment an Enchant would attach; null for a refinement.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="weapon"/> is none the party owns; an Enchant names no
    /// enchantment, or a refinement names one.
    /// </exception>
    public static string? Refusal(CampaignState campaign, Weapon weapon, Service service, string? enchantmentId = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(weapon);
        switch (service)
        {
            case Service.Enchant when enchantmentId is null:
                throw new ArgumentException("An Enchant names the enchantment it buys.", nameof(enchantmentId));
            case Service.Refine when enchantmentId is not null:
                throw new ArgumentException($"A refinement attaches nothing, and '{enchantmentId}' is named.", nameof(enchantmentId));
            case not (Service.Enchant or Service.Refine):
                throw new ArgumentOutOfRangeException(nameof(service), service, "Not a Service.");
        }

        if (Gates(campaign, weapon, service) is { } refused)
            return refused;
        if (service == Service.Refine)
            return null;
        if (!campaign.HasSeen(enchantmentId!))
            return Unseen(enchantmentId!);
        var def = GameContent.Current.Enchantments[enchantmentId!];   // met, so the content's
        return NeverOffered(def) ?? OfferRefusal(weapon, def);
    }

    /// <summary>
    /// Why the enchanter would refuse <paramref name="weapon"/> for
    /// <paramref name="service"/> whatever enchantment it named, or null — the gates
    /// that come before the enchantment, in the order they are asked, the first to
    /// fail the reason: what a screen dims a service by before anything is chosen.
    /// <list type="number">
    /// <item><strong>Closed</strong>: the tutorial is not beaten, and the enchanter
    /// is not open until it is (§6.3; S:411–419).</item>
    /// <item><strong>In the shop</strong> already: a weapon is in one place.</item>
    /// <item><strong>The last weapon</strong> a member carries: no deposit leaves a
    /// member carrying none (PHASE 6a DECISION 7).</item>
    /// <item>An <strong>Enchant</strong> of enchantment #n, n the count plus one,
    /// needs every one of: a slot left (<see cref="Tuning.EnchantmentSlots"/>),
    /// slot n open at the capacity the weapon is deposited at — before this
    /// deposit's cash (<see cref="Wear.SlotOpensAt"/>) — a deposit of at least its
    /// price (<see cref="Wear.Price"/>), and something to choose: an offer the
    /// enchanter would attach (<see cref="Offers"/>; <see cref="NothingToOffer"/>
    /// when every one is refused, or none is left). Capacity above the price is
    /// breathing room (R4).</item>
    /// <item>A <strong>Refine</strong> has no gate of its own: an unworn weapon may
    /// be refined, and comes back as it went (§6.4: "a fresh drop refines for
    /// nothing").</item>
    /// </list>
    /// </summary>
    /// <param name="campaign">The campaign between its runs.</param>
    /// <param name="weapon">A weapon the party owns: in a member's bag, in the stable, or in the shop.</param>
    /// <param name="service">What the deposit would buy.</param>
    /// <exception cref="ArgumentException"><paramref name="weapon"/> is none the party owns.</exception>
    public static string? ServiceRefusal(CampaignState campaign, Weapon weapon, Service service)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(weapon);
        if (service is not (Service.Enchant or Service.Refine))
            throw new ArgumentOutOfRangeException(nameof(service), service, "Not a Service.");
        if (Gates(campaign, weapon, service) is { } refused)
            return refused;
        return service == Service.Enchant && !Offers(campaign, weapon).Any(offer => offer.Refusal is null) ? NothingToOffer : null;
    }

    /// <summary>
    /// <see cref="ServiceRefusal"/>'s gates bar its last (<see cref="NothingToOffer"/>),
    /// in its order: the ones <see cref="Refusal"/> asks before the enchantment it
    /// names, which answers the last for itself.
    /// </summary>
    private static string? Gates(CampaignState campaign, Weapon weapon, Service service)
    {
        var content = GameContent.Current;
        var tutorial = content.Dungeons.Tutorial;
        if (!campaign.IsRetired(tutorial.Id))
            return Closed(tutorial);

        if (campaign.AtTheEnchanter.Any(deposit => ReferenceEquals(deposit.Weapon, weapon)))
            return InTheShop(weapon);
        var carrier = campaign.Party.FirstOrDefault(member => Array.FindIndex(member.Inventory, carried => ReferenceEquals(carried, weapon)) >= 0);
        if (carrier is null && !campaign.Stable.Any(kept => ReferenceEquals(kept, weapon)))
            throw new ArgumentException($"{weapon.Name} ({weapon.Id}) is in no bag, not in the stable and not in the shop: the enchanter takes only a weapon the party owns.", nameof(weapon));
        if (carrier is not null && carrier.Inventory.Count(carried => carried is not null) == 1)
            return WouldCarryNone(carrier.Id);

        if (service == Service.Refine)
            return null;

        var tuning = content.Tuning;
        int n = weapon.Enchantments.Count + 1;
        if (n > tuning.EnchantmentSlots)
            return AllFilled(tuning.EnchantmentSlots);
        int capacity = weapon.WearCapacity;
        long opensAt = Wear.SlotOpensAt(n);
        if (opensAt > capacity)
            return NoFreeSlot(n, opensAt, capacity);
        long price = Wear.Price(n);
        if (weapon.Wear < price)
            return Short(n, price, weapon.Wear);
        return null;
    }

    /// <summary>The enchanter's gate before the tutorial is beaten (§6.3), naming the tutorial as the hub's gate does: <c>THE ENCHANTER OPENS WHEN THE TUTORIAL IS BEATEN</c>. Also what the WEAPONS page heads its shop with until then.</summary>
    public static string Closed(DungeonDef tutorial)
    {
        ArgumentNullException.ThrowIfNull(tutorial);
        return $"THE ENCHANTER OPENS WHEN {tutorial.Name.ToUpperInvariant()} IS BEATEN";
    }

    /// <summary>A weapon deposited already: <c>ARMING SWORD IS IN THE SHOP ALREADY</c>, named bare as the summary's <c>… IS BACK FROM THE ENCHANTER</c> names it. The screen never offers it.</summary>
    public static string InTheShop(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return $"{weapon.Name.ToUpperInvariant()} IS IN THE SHOP ALREADY";
    }

    /// <summary>A member's last weapon (PHASE 6a DECISION 7): <c>[A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST</c>.</summary>
    public static string WouldCarryNone(string memberId) => $"[{memberId}] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST";

    /// <summary>A weapon holding every slot there is: <c>ALL 5 SLOTS ARE FILLED</c>. Refining is left.</summary>
    public static string AllFilled(int slots) => $"ALL {slots} SLOTS ARE FILLED";

    /// <summary>Enchantment #n's slot not open yet (R4, "a free slot is read literally"): <c>NO FREE SLOT - SLOT 2 OPENS AT CAPACITY 35 - IT IS 33</c>.</summary>
    public static string NoFreeSlot(int n, long opensAt, int capacity) => $"NO FREE SLOT - SLOT {n} OPENS AT CAPACITY {opensAt} - IT IS {capacity}";

    /// <summary>A deposit short of enchantment #n's price: <c>ENCHANTMENT 2 NEEDS A DEPOSIT OF 35 WEAR - IT CARRIES 20</c>.</summary>
    public static string Short(int n, long price, int wear) => $"ENCHANTMENT {n} NEEDS A DEPOSIT OF {price} WEAR - IT CARRIES {wear}";

    /// <summary>An enchantment the campaign has not met (§6.2: the offer is what the campaign has seen): <c>FLAMING HAS NOT BEEN MET - ONLY WHAT THE PARTY HAS CARRIED IS OFFERED</c>. The screen lists only what it has met, so only a caller that names another meets it.</summary>
    public static string Unseen(string enchantmentId)
    {
        ArgumentNullException.ThrowIfNull(enchantmentId);
        string name = GameContent.Current.Enchantments.TryGet(enchantmentId, out var def) ? def.Name : enchantmentId;
        return $"{name.ToUpperInvariant()} HAS NOT BEEN MET - ONLY WHAT THE PARTY HAS CARRIED IS OFFERED";
    }

    // ── A deposit ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Deposit the weapon at <paramref name="from"/> for <paramref name="service"/>
    /// (§6.2, §6.7: one call for every service), between runs. The gates are asked
    /// first (<see cref="Refusal"/>), and a refusal throws: the screen never asks
    /// for a deposit it would be refused. Then, in this order:
    /// <list type="number">
    /// <item>the runs the service takes, off the count as it goes in
    /// (<see cref="Wear.ServiceRuns"/>);</item>
    /// <item><strong>the whole pool cashed</strong> (<see cref="Weapon.CashWear"/>):
    /// the pool empties now, and the capacity grows on the cash now, identically
    /// whatever the service (R4: "+1 per full bar … cashed in", "the pool itself
    /// still empties at every deposit");</item>
    /// <item>the weapon taken out of its place and queued, the campaign's count of
    /// deposits its ordinal (<see cref="CampaignState.Enqueue"/>) — a hand it
    /// empties armed from the bag, as a move's is.</item>
    /// </list>
    /// Nothing is attached and nothing is rolled here: a service does that as it
    /// finishes (PHASE 6a DECISION 3).
    /// </summary>
    /// <returns>The deposit made, and the capacity before and after its cash, for the screen's log.</returns>
    /// <exception cref="InvalidOperationException">A visit is in progress, nothing lies at <paramref name="from"/>, or the enchanter refuses the deposit — the reason in the message.</exception>
    /// <exception cref="ArgumentException">An Enchant names no enchantment, or a refinement names one; or a member the place names is not in the party.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The place is not one the party or the stable has.</exception>
    public static DepositMade Deposit(CampaignState campaign, WeaponPlace from, Service service, string? enchantmentId = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (campaign.Visit is { } visit)
            throw new InvalidOperationException(
                $"A visit to '{visit.Def.Id}' is in progress: the enchanter takes deposits between runs only.");
        var weapon = campaign.WeaponAt(from) ?? throw new InvalidOperationException($"Nothing lies at {from} to deposit.");
        if (Refusal(campaign, weapon, service, enchantmentId) is { } refused)
            throw new InvalidOperationException($"The enchanter refuses {weapon.Name} ({weapon.Id}) for {service}: {refused}");

        int runs = Wear.ServiceRuns(weapon.Enchantments.Count);
        var before = weapon.WearPool;
        int cashed = weapon.CashWear();
        var entry = new Deposit(campaign.DepositsMade, weapon, service, cashed, runs, enchantmentId);
        var armed = campaign.Enqueue(from, entry);
        return new DepositMade(entry, before, weapon.WearPool, armed);
    }

    // ── A take-back ───────────────────────────────────────────────────────────

    /// <summary>
    /// Take <paramref name="deposit"/> back out of the shop, between runs (PHASE 6a
    /// DECISION 8): the weapon goes to the end of the stable, nothing attached and
    /// nothing rolled, the runs worked lost — and the deposit's cash reversed
    /// exactly (<see cref="Weapon.RefundWear"/>), the wear back in the pool and off
    /// what the weapon has cashed, so its capacity and the progress toward the next
    /// point are as they were before it went in. A take-back is an undo whose cost
    /// is the runs lost, never a way to grow a capacity with no downtime. The
    /// ordinal is not reused.
    /// </summary>
    /// <exception cref="InvalidOperationException">A visit is in progress, or <paramref name="deposit"/> is not in the shop.</exception>
    public static void TakeBack(CampaignState campaign, Deposit deposit)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(deposit);
        campaign.TakeOut(deposit);   // the hub's door: between runs, and only a deposit in the shop
        deposit.Weapon.RefundWear(deposit.WearAtDeposit);
    }

    // ── The clock ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The deposits the next run that counts works (§6.2: "the two most recently
    /// deposited"): the <see cref="Tuning.EnchanterWorksOn"/> with the highest
    /// ordinals, in deposit order. Every other deposit in <paramref name="queue"/>
    /// is stalled until one of these comes back. The queue has no length of its own:
    /// "dumping the whole stable achieves nothing" (P6:291–292).
    /// </summary>
    public static IReadOnlyList<Deposit> Worked(IReadOnlyList<Deposit> queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        int works = GameContent.Current.Tuning.EnchanterWorksOn;
        return queue.OrderByDescending(deposit => deposit.Ordinal).Take(works).OrderBy(deposit => deposit.Ordinal).ToArray();
    }

    /// <summary>
    /// The service clock (§6.3, "the tick advances the service clock and nothing
    /// else"), asked by the campaign's run's end on every run, the visit already
    /// gone (<see cref="CampaignState.EndRun"/>, its one caller).
    /// <list type="bullet">
    /// <item><strong>A run that counted</strong> works each deposit
    /// <see cref="Worked"/> names a run on, in deposit order, and finishes each
    /// whose last run that was: an Enchant attaches its enchantment last, at
    /// <see cref="AttachedTier"/> with no XP — the deposit asked the offer and the
    /// ceiling, and the weapon has been in the shop since — a refinement nothing,
    /// and the weapon goes to the end of the stable
    /// (<see cref="CampaignState.Return"/>). Every other deposit is reported
    /// stalled.</item>
    /// <item><strong>A run that did not count</strong> moves nothing, and reports
    /// every deposit stalled with its runs as they were — a fresh one's at 0 — so
    /// the summary can say the enchanter worked nothing.</item>
    /// </list>
    /// </summary>
    /// <param name="campaign">The campaign whose run has just ended.</param>
    /// <param name="counted">Whether the run counted (<see cref="RunResult.ServiceTick"/>).</param>
    /// <returns>A report for every deposit in the shop as the run ended, in deposit order; empty for an empty shop.</returns>
    internal static IReadOnlyList<ServiceReport> Tick(CampaignState campaign, bool counted)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var queue = campaign.AtTheEnchanter.ToArray();   // a copy: a finished service leaves the shop as the tick goes
        if (queue.Length == 0)
            return [];

        var worked = new HashSet<Deposit>(counted ? Worked(queue) : [], ReferenceEqualityComparer.Instance);
        var reports = new ServiceReport[queue.Length];
        for (int i = 0; i < queue.Length; i++)
        {
            var entry = queue[i];
            if (!worked.Contains(entry))
            {
                reports[i] = Report(entry, stalled: true);
                continue;
            }

            entry.RunsWorked++;
            reports[i] = Report(entry, stalled: false);
            if (entry.RunsWorked >= entry.Runs)
            {
                Complete(entry);
                campaign.Return(entry);
            }
        }
        return reports;
    }

    /// <summary>What the tick says of <paramref name="entry"/>: its weapon by id, its service, its runs as they stand, whether it was left unworked, and its enchantment.</summary>
    private static ServiceReport Report(Deposit entry, bool stalled)
        => new(entry.Weapon.Id, entry.Service, entry.RunsWorked, entry.Runs, stalled, entry.EnchantmentId);

    /// <summary>
    /// A service's last run worked: an Enchant attaches its enchantment — the
    /// content's own entry, by id — last, at <see cref="AttachedTier"/> with no XP
    /// (<see cref="Weapon.Attach"/>); a refinement attaches nothing.
    /// </summary>
    private static void Complete(Deposit entry)
    {
        switch (entry.Service)
        {
            case Service.Enchant:
                string id = entry.EnchantmentId
                    ?? throw new InvalidOperationException($"Deposit {entry.Ordinal}, an Enchant of {entry.Weapon.Name}, names no enchantment.");
                entry.Weapon.Attach(new Enchantment(GameContent.Current.Enchantments[id], AttachedTier));
                break;
            case Service.Refine:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entry), entry.Service, "Not a Service.");
        }
    }
}
