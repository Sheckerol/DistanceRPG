namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What a deposit at the enchanter buys (§6.2, §6.4, §6.5, §6.7): one of its
/// services, chosen as the weapon goes in. Whichever it is, the deposit cashes the
/// weapon's whole pool and grows its capacity alike (R4). An Enchant and a Refine
/// take the weapon's count plus one runs (<see cref="Wear.ServiceRuns"/>); a
/// Transfer the runs of attaching the soul it moves (<see cref="Enchanter.TransferRuns"/>).
/// </summary>
public enum Service
{
    /// <summary>A chosen enchantment, one the campaign has met, attached last at tier 1 when the service finishes: the enchanter sells breadth, never a tier (§6.2).</summary>
    Enchant,

    /// <summary>Nothing attached: the deposit is the wear it cashes, which grows the capacity as any deposit's does, and opens the slots along it. Declining an enchantment is always this (§6.2).</summary>
    Refine,

    /// <summary>
    /// An enchantment the party owns moved onto the weapon (§6.5; Phase 6a.6): taken
    /// off another weapon — its source, which waits in the shop beside it as the one
    /// deposit — or off the weapon itself, a reorder, and attached last a tier down,
    /// tier 1 the floor. An Enchant whose soul comes from a weapon rather than the
    /// catalogue: the weapon needs the free slot and the deposit of attaching it
    /// (PHASE 6a DECISIONS 11 and 13).
    /// </summary>
    Transfer,
}

/// <summary>
/// One deposit in the shop (§6.7's record, extended — PHASE 6a DECISION 13): the
/// weapon, out of the party's hands until its service is done or it is taken back;
/// the service; the wear the deposit cashed; the runs the service takes, fixed as
/// it went in — the count cannot change while the weapon is in the shop — and how
/// many of them the runs that counted have worked. The ordinal is the campaign's
/// count of deposits as it went in (<see cref="CampaignState.DepositsMade"/>),
/// never reused: the enchanter works the newest, the highest ordinals. A transfer
/// (Phase 6a.6) names the soul it moves by where it lies: its source — another
/// weapon, in the shop beside this one as the same deposit, or none for a reorder
/// of the weapon's own — and the soul's place on it.
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
/// <param name="runs">The runs the service takes, as it went in: the weapon's count plus one, or a transfer's (<see cref="Enchanter.TransferRuns"/>).</param>
/// <param name="enchantmentId">The enchantment an Enchant attaches, by id; null for any other service.</param>
/// <param name="source">A transfer's source, the weapon its soul comes off, in the shop with this one; null for a reorder and for any other service.</param>
/// <param name="sourceIndex">A transfer's soul, by its place on its source — or on the weapon, for a reorder; null for any other service.</param>
public sealed class Deposit(int ordinal, Weapon weapon, Service service, int wearAtDeposit, int runs, string? enchantmentId = null,
    Weapon? source = null, int? sourceIndex = null)
{
    /// <summary>The deposit's place in the campaign's deposits, from 0: the higher, the newer.</summary>
    public int Ordinal { get; } = ordinal;

    /// <summary>The weapon in the shop.</summary>
    public Weapon Weapon { get; } = weapon;

    /// <summary>What the deposit bought.</summary>
    public Service Service { get; } = service;

    /// <summary>The wear the deposit cashed: the weapon's whole pool as it went in.</summary>
    public int WearAtDeposit { get; } = wearAtDeposit;

    /// <summary>The runs the service takes, as it went in: the weapon's count plus one, or a transfer's (<see cref="Enchanter.TransferRuns"/>).</summary>
    public int Runs { get; } = runs;

    /// <summary>The enchantment an Enchant attaches, by id; null for any other service.</summary>
    public string? EnchantmentId { get; } = enchantmentId;

    /// <summary>A transfer's source (§6.5): the weapon its soul comes off, held in the shop beside this one until the service is done or taken back, as the same deposit. Null for a reorder — the soul is the weapon's own — and for any other service.</summary>
    public Weapon? Source { get; } = source;

    /// <summary>A transfer's soul, by its place on <see cref="Source"/> — on <see cref="Weapon"/> for a reorder — as it went in: neither weapon changes in the shop. Null for any other service.</summary>
    public int? SourceIndex { get; } = sourceIndex;

    /// <summary>The enchantment a transfer moves, where it lies while the deposit is in the shop: <see cref="SourceIndex"/> on its source, or on the weapon for a reorder. Null for any other service, and for an index nothing lies at.</summary>
    public Enchantment? Soul => Service == Service.Transfer && SourceIndex is { } index && index >= 0 && index < (Source ?? Weapon).Enchantments.Count
        ? (Source ?? Weapon).Enchantments[index]
        : null;

    /// <summary>The runs of the service the runs that counted have worked: 0 as it goes in, and below <see cref="Runs"/> while it is in the shop — the tick that works the last one brings it back.</summary>
    public int RunsWorked { get; internal set; }
}

/// <summary>
/// What a service's improvement roll did when it landed (§6.4's two outcomes, "nothing"
/// being the roll that did not land): deepened a modifier the weapon carried by a
/// stack, or grafted a first stack of one it never carried.
/// </summary>
public enum ImprovementKind
{
    /// <summary>+1 stack on a modifier the weapon already carries — the common outcome, biased toward the class signature.</summary>
    Deepen,

    /// <summary>+1 stack of a modifier the weapon has never carried — the rare one, and the surprise: acquired, so it caps at the acquired headroom.</summary>
    Graft,
}

/// <summary>What a landed improvement roll did to the weapon (§6.4): the outcome, and the modifier it gave a stack of.</summary>
/// <param name="Kind">Deepened, or grafted.</param>
/// <param name="Modifier">The modifier that gained the stack.</param>
public sealed record Improvement(ImprovementKind Kind, ModifierType Modifier);

/// <summary>
/// A service's improvement roll as it was drawn, for the log alone (Logic never
/// logs): the first draw off the roll's stream, 0 to <see cref="Denominator"/> − 1,
/// against the odds' numerator — it landed exactly when the draw is below it
/// (<see cref="Wear.ImproveOdds"/>). Its two keys are in the log where they were
/// made: the run's map seed as the run begins, and the deposit's ordinal where the
/// deposit was made (<c>deposit #3</c>, Phase 6a.5) — in the session that made it,
/// which may not be the one the service finishes in; and once the service has
/// finished no save holds the ordinal. A save made during the run holds both — the
/// visit's seed and the queued deposit — and a load of it replays the roll. Never
/// saved: a report a load brings back has none.
/// </summary>
/// <param name="Draw">The draw, 0 to <paramref name="Denominator"/> − 1.</param>
/// <param name="Denominator">The odds' denominator: 100 × StartingPool.</param>
/// <param name="Numerator">The odds' numerator: the roll lands when the draw is below it.</param>
public sealed record ImproveRoll(long Draw, long Denominator, long Numerator)
{
    /// <summary>Whether the roll landed: the draw below the numerator.</summary>
    public bool Landed => Draw < Numerator;
}

/// <summary>
/// What <see cref="Enchanter.Deposit"/> did, for the screen that asked it to log
/// it (Logic never logs): the entry now in the shop, the weapon's capacity before
/// and after the deposit's cash, and the weapon a hand the deposit emptied took up
/// from the bag, or null — the weapon's hand, and a transfer's source's.
/// </summary>
public sealed record DepositMade(Deposit Entry, PoolProgress Before, PoolProgress After, Weapon? Armed, Weapon? SourceArmed = null);

/// <summary>
/// What <see cref="Enchanter.TakeBack"/> did, for the screen that asked it to log
/// it (Logic never logs), as <see cref="DepositMade"/> is a deposit's: the deposit
/// taken back — a transfer's source with it, its <see cref="Deposit.Source"/>, whose
/// wear was never cashed — and the weapon's capacity before and after its cash was
/// reversed, taken here, on either side of the reversal, so no caller can take them
/// in the wrong order.
/// </summary>
public sealed record TakenBack(Deposit Entry, PoolProgress Before, PoolProgress After);

/// <summary>
/// One soul the enchanter could move onto a weapon (§6.5, "MOVE AN ENCHANTMENT
/// ONTO IT"; <see cref="Enchanter.TransferOffers"/>): where it lies — on another
/// weapon the party owns, or on the weapon itself, a reorder — the tier it arrives
/// at and the runs the move takes, why the enchanter would refuse it — or null when
/// it would take it — and what the screen says beside it, or null. A refused one is
/// listed with its reason and never hidden (PHASE 6a DECISION 12).
/// </summary>
/// <param name="From">Where the source lies, a member's slot or the stable's; null for the weapon's own entry.</param>
/// <param name="Source">The weapon the soul is on; null for the weapon's own entry.</param>
/// <param name="Index">The soul's place on its source, or on the weapon.</param>
/// <param name="Soul">The soul as it lies, its tier and its XP.</param>
/// <param name="ArrivesAt">The tier it lands at (<see cref="Enchantment.Transferred"/>).</param>
/// <param name="Runs">The runs the move takes (<see cref="Enchanter.TransferRuns"/>).</param>
/// <param name="Refusal">Why the enchanter would refuse it, not naming the soul — <c>BURNING NEEDS FLAMING AHEAD OF IT</c> — or null (<see cref="Enchanter.TransferRefusal"/>).</param>
/// <param name="Note">What the screen says beside it whether or not it is refused — <c>NEVER FIRES ON A SWORD</c> — or null.</param>
public sealed record TransferOffer(WeaponPlace? From, Weapon? Source, int Index, Enchantment Soul, int ArrivesAt, int Runs, string? Refusal, string? Note);

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
/// <item><strong>A transfer</strong> (Phase 6a.6; <see cref="TransferOffers"/>,
/// <see cref="TransferRefusal"/>) is a deposit whose soul comes off a weapon the
/// party owns rather than from the offer: the weapon needs the free slot and the
/// deposit of attaching it, and both weapons wait in the shop as one deposit, the
/// source's wear left in its pool. It is refused where it would leave a member
/// unarmed, where the relations refuse the soul on the weapon, where taking it off
/// would strand an entry behind it, and where a staff or a wand would be left
/// casting nothing (PHASE 6a DECISIONS 11–13).</item>
/// <item><strong>The clock</strong> (<see cref="Tick"/>) is the run counter's
/// tick: every run's end asks it, the visit already gone. A run that counts works
/// the newest <see cref="Tuning.EnchanterWorksOn"/> deposits a run on — every
/// other deposit stalls, a transfer counting as one — and a service whose last run
/// is worked finishes: an Enchant attaches its enchantment last, at tier 1 with no
/// XP, a Transfer moves its soul last a tier down, the improvement roll is made
/// (<see cref="Improve"/>, Phase 6a.4), and the weapon goes to the end of the
/// stable, a transfer's source after it (PHASE 6a DECISION 7). A run that does not
/// count works nothing.</item>
/// <item><strong>The improvement roll</strong> (<see cref="Improve"/>) is §6.4's
/// gamble: on its own stream, keyed by the finishing run's map seed and the
/// deposit's ordinal, it lands on the service's odds (<see cref="Wear.ImproveOdds"/>)
/// — never when the deposit cashed no wear — and a landed roll deepens a modifier
/// the weapon carries or, more rarely, grafts one it never carried (PHASE 6a
/// DECISION 5).</item>
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
        => new(def.Id, OfferRefusal(weapon, weapon.Enchantments, def) is { } why ? Named(def, why) : null, FiresOn(weapon, def) ? null : NeverFiresOn(weapon.Class));

    /// <summary>
    /// The relations, the wand and the staff's side, asked of <paramref name="def"/>
    /// attached last to <paramref name="weapon"/> behind <paramref name="ahead"/> —
    /// its entries, or, for a reorder, its entries less the one moving — in
    /// <see cref="Offers"/>' order; the reason without the entry's name, which the
    /// caller puts to it (<c>CANNOT SIT WITH FLAMING</c>), or null when none refuses it.
    /// </summary>
    private static string? OfferRefusal(Weapon weapon, IReadOnlyList<Enchantment> ahead, EnchantmentDef def)
    {
        var catalogue = GameContent.Current.Enchantments;
        foreach (var entry in ahead)
            if (catalogue.Excludes(def.Id, entry.Id))
                return CannotSitWith(entry.Def);
        if (catalogue.LacksPrerequisiteAhead(def.Id, ahead.Select(entry => entry.Id), out var need))
            return NeedsAhead(catalogue.TryGet(need, out var needed) ? needed.Name : need);
        if (weapon.Class == WeaponClass.Wand && ContentValidator.NoWandCarries(def))
            return WandCastsNoStatusWhy;
        // A staff's cast: its first entry picks the side (TurnSystem.CanCastOn), and
        // every status its entries apply lands on that one body (ApplyCast).
        if (weapon.IsCaster && ahead.Count > 0 && ahead[0] is { Def.Applies: not null } cast && def.Effect == EffectKind.ApplyStatus
            && def.Targets != TargetSide.Any && def.Targets != cast.Def.Targets)
            return StaffCastsItOn(cast.Def.Targets);
        return null;
    }

    /// <summary>A reason put to the entry it refuses, as the offer list says it: <c>COLD - CANNOT SIT WITH FLAMING</c>.</summary>
    private static string Named(EnchantmentDef def, string why) => $"{def.Name.ToUpperInvariant()} - {why}";

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
        return Named(offered, CannotSitWith(carried));
    }

    /// <summary><see cref="CannotSitWith(EnchantmentDef, EnchantmentDef)"/>'s reason, not naming the entry refused — how a transfer's list says it, after the soul's place: <c>CANNOT SIT WITH FLAMING</c>.</summary>
    public static string CannotSitWith(EnchantmentDef carried)
    {
        ArgumentNullException.ThrowIfNull(carried);
        return $"CANNOT SIT WITH {carried.Name.ToUpperInvariant()}";
    }

    /// <summary>An offer whose prerequisite is not on the weapon ahead of it (PHASE 6a DECISION 12): <c>BURNING - NEEDS FLAMING AHEAD OF IT</c>.</summary>
    public static string NeedsAhead(EnchantmentDef offered, string needed)
    {
        ArgumentNullException.ThrowIfNull(offered);
        return Named(offered, NeedsAhead(needed));
    }

    /// <summary><see cref="NeedsAhead(EnchantmentDef, string)"/>'s reason, not naming the entry refused: <c>NEEDS FLAMING AHEAD OF IT</c>.</summary>
    public static string NeedsAhead(string needed)
    {
        ArgumentNullException.ThrowIfNull(needed);
        return $"NEEDS {needed.ToUpperInvariant()} AHEAD OF IT";
    }

    /// <summary><see cref="WandCastsNoStatus"/>'s reason, not naming the entry refused.</summary>
    public const string WandCastsNoStatusWhy = "A WAND CASTS NO STATUS";

    /// <summary>A status-applying offer to a wand (PHASE 6a DECISION 12): <c>REGENERATION - A WAND CASTS NO STATUS</c>.</summary>
    public static string WandCastsNoStatus(EnchantmentDef offered)
    {
        ArgumentNullException.ThrowIfNull(offered);
        return Named(offered, WandCastsNoStatusWhy);
    }

    /// <summary>A status for one side offered to a staff whose cast is for the other (PHASE 6a DECISION 12), naming the side <paramref name="casts"/> it would land on: <c>MIRE - THIS STAFF WOULD CAST IT ON AN ALLY</c>.</summary>
    public static string StaffCastsItOn(EnchantmentDef offered, TargetSide casts)
    {
        ArgumentNullException.ThrowIfNull(offered);
        return Named(offered, StaffCastsItOn(casts));
    }

    /// <summary><see cref="StaffCastsItOn(EnchantmentDef, TargetSide)"/>'s reason, not naming the entry refused: <c>THIS STAFF WOULD CAST IT ON AN ALLY</c>.</summary>
    public static string StaffCastsItOn(TargetSide casts)
    {
        string side = casts switch
        {
            TargetSide.Ally => "AN ALLY",
            TargetSide.Enemy => "AN ENEMY",
            _ => throw new ArgumentOutOfRangeException(nameof(casts), casts, "A staff casts on allies or on enemies, never either (ContentValidator.RuleCasterInnate)."),
        };
        return $"THIS STAFF WOULD CAST IT ON {side}";
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
    /// enchantment, or a refinement names one; or a Transfer is asked here, which
    /// names its soul by where it lies (<see cref="TransferRefusal"/>).
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
            case Service.Transfer:
                throw new ArgumentException("A transfer names the soul it moves by where it lies, and is asked of TransferRefusal.", nameof(service));
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
        return NeverOffered(def) ?? (OfferRefusal(weapon, weapon.Enchantments, def) is { } why ? Named(def, why) : null);
    }

    /// <summary>
    /// Why the enchanter would refuse <paramref name="weapon"/> for
    /// <paramref name="service"/> whatever enchantment it named, or null — the gates
    /// that come before the enchantment, in the order they are asked, the first to
    /// fail the reason: what a screen dims a service by before anything is chosen.
    /// <list type="number">
    /// <item><strong>Closed</strong>: the tutorial is not beaten, and the enchanter
    /// is not open until it is (§6.3; S:411–419).</item>
    /// <item><strong>In the shop</strong> already, deposited or held as a
    /// transfer's source: a weapon is in one place.</item>
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
    /// <item>A <strong>Transfer</strong> (Phase 6a.6) needs a soul to move that the
    /// enchanter would take (<see cref="TransferOffers"/>): with none, the reason an
    /// Enchant of the next enchantment is refused — the slot, or the price, which
    /// every soul from another weapon must meet — or <see cref="NothingToMove"/>. A
    /// reorder of the weapon's own is asked at its count, so a weapon whose every
    /// slot is filled may still reorder (PHASE 6a DECISION 11).</item>
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
        if (service is not (Service.Enchant or Service.Refine or Service.Transfer))
            throw new ArgumentOutOfRangeException(nameof(service), service, "Not a Service.");
        if (Gates(campaign, weapon, service) is { } refused)
            return refused;
        return service switch
        {
            Service.Enchant when !Offers(campaign, weapon).Any(offer => offer.Refusal is null) => NothingToOffer,
            Service.Transfer when !TransferOffers(campaign, weapon).Any(offer => offer.Refusal is null)
                => Attaching(weapon, weapon.Enchantments.Count + 1) ?? NothingToMove,
            _ => null,
        };
    }

    /// <summary>
    /// <see cref="ServiceRefusal"/>'s gates bar its last (<see cref="NothingToOffer"/>,
    /// <see cref="NothingToMove"/>), in its order: the ones <see cref="Refusal"/> asks
    /// before the enchantment it names, which answers the last for itself. A
    /// transfer's are the weapon's own alone — closed, in the shop, its member's last
    /// — since the rest are asked of the soul it moves (<see cref="TransferRefusal"/>).
    /// </summary>
    private static string? Gates(CampaignState campaign, Weapon weapon, Service service)
    {
        if (!IsOpen(campaign))
            return Closed(GameContent.Current.Dungeons.Tutorial);

        if (InShop(campaign, weapon))
            return InTheShop(weapon);
        if (Carrier(campaign, weapon) is { } carrier && carrier.Inventory.Count(carried => carried is not null) == 1)
            return WouldCarryNone(carrier.Id);

        return service == Service.Enchant ? Attaching(weapon, weapon.Enchantments.Count + 1) : null;
    }

    /// <summary>
    /// Why enchantment #<paramref name="n"/> would not go on <paramref name="weapon"/>
    /// now, or null (R4): a slot left (<see cref="AllFilled"/>), slot n open at the
    /// capacity the weapon is deposited at, before the deposit's cash
    /// (<see cref="NoFreeSlot"/>), and a deposit of its price (<see cref="Short"/>) —
    /// in that order. An Enchant asks it at the count plus one; a transfer at the
    /// place its soul lands (<see cref="TransferRefusal"/>).
    /// </summary>
    private static string? Attaching(Weapon weapon, int n)
    {
        var tuning = GameContent.Current.Tuning;
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

    /// <summary>Whether <paramref name="weapon"/> is in the shop: deposited, or held there as a transfer's source.</summary>
    private static bool InShop(CampaignState campaign, Weapon weapon)
        => campaign.AtTheEnchanter.Any(deposit => ReferenceEquals(deposit.Weapon, weapon) || ReferenceEquals(deposit.Source, weapon));

    /// <summary>The member carrying <paramref name="weapon"/>, or null for a weapon in the stable.</summary>
    /// <exception cref="ArgumentException"><paramref name="weapon"/> is in no bag and not in the stable: none the party owns out of the shop.</exception>
    private static PartyMemberState? Carrier(CampaignState campaign, Weapon weapon)
    {
        var carrier = campaign.Party.FirstOrDefault(member => Array.FindIndex(member.Inventory, carried => ReferenceEquals(carried, weapon)) >= 0);
        if (carrier is null && !campaign.Stable.Any(kept => ReferenceEquals(kept, weapon)))
            throw new ArgumentException($"{weapon.Name} ({weapon.Id}) is in no bag, not in the stable and not in the shop: the enchanter takes only a weapon the party owns.", nameof(weapon));
        return carrier;
    }

    // ── A transfer (§6.5, Phase 6a.6) ─────────────────────────────────────────

    /// <summary>
    /// The runs moving a soul onto <paramref name="weapon"/> takes (§6.5: "one
    /// attachment's service time"): the runs of attaching it as the weapon's next —
    /// its count plus one (<see cref="Wear.ServiceRuns"/>), as an Enchant's — or, for
    /// a <paramref name="reorder"/> of its own, of attaching it again as the last of
    /// the same count: the count (PHASE 6a DECISION 11).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A reorder of a weapon carrying nothing to reorder.</exception>
    public static int TransferRuns(Weapon weapon, bool reorder)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        int count = weapon.Enchantments.Count;
        if (reorder)
            ArgumentOutOfRangeException.ThrowIfLessThan(count, 1, nameof(weapon));
        return Wear.ServiceRuns(reorder ? count - 1 : count);
    }

    /// <summary>
    /// What the enchanter could move onto <paramref name="weapon"/> (§6.5): every
    /// soul on every other weapon the party owns out of the shop — each member's, in
    /// roster order and slot order, then the stable's in its order — then the
    /// weapon's own, a reorder. Each with the tier it arrives at, the runs the move
    /// takes, why the enchanter would refuse it (<see cref="TransferRefusal"/>) or
    /// nothing, and the note an offer carries when it would never fire on the weapon
    /// (<see cref="NeverFiresOn"/>). A refused one is listed with its reason, never
    /// hidden (PHASE 6a DECISION 12).
    /// </summary>
    /// <param name="campaign">The campaign between its runs.</param>
    /// <param name="weapon">The weapon the soul would land on, last.</param>
    public static IReadOnlyList<TransferOffer> TransferOffers(CampaignState campaign, Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(weapon);
        var offers = new List<TransferOffer>();
        void Each(WeaponPlace? from, Weapon? source)
        {
            var giver = source ?? weapon;
            for (int index = 0; index < giver.Enchantments.Count; index++)
            {
                var soul = giver.Enchantments[index];
                offers.Add(new TransferOffer(from, source, index, soul, soul.Transferred().Tier, TransferRuns(weapon, reorder: source is null),
                    TransferRefusal(campaign, weapon, source, index), FiresOn(weapon, soul.Def) ? null : NeverFiresOn(weapon.Class)));
            }
        }

        foreach (var member in campaign.Party)
            for (int slot = 0; slot < member.Inventory.Length; slot++)
                if (member.Inventory[slot] is { } carried && !ReferenceEquals(carried, weapon))
                    Each(new WeaponPlace(member.Id, slot), carried);
        for (int i = 0; i < campaign.Stable.Count; i++)
            if (!ReferenceEquals(campaign.Stable[i], weapon))
                Each(WeaponPlace.InTheStable(i), campaign.Stable[i]);
        Each(null, null);
        return offers;
    }

    /// <summary>
    /// Why the enchanter would refuse to move the soul at <paramref name="index"/> on
    /// <paramref name="source"/> — or, <paramref name="source"/> null, on
    /// <paramref name="weapon"/> itself, a reorder — onto <paramref name="weapon"/>,
    /// last, or null when it would take it (§6.5; PHASE 6a DECISIONS 11–13). The
    /// reason names neither the soul nor where it lies, which the screen says before
    /// it: <c>VAMPIRIC T3 ON THE NAMELESS KNIFE - …</c>. Asked in this order, the
    /// first to fail the reason:
    /// <list type="number">
    /// <item><strong>Closed</strong>, and either weapon <strong>in the shop</strong>
    /// already, as every deposit asks (<see cref="ServiceRefusal"/>).</item>
    /// <item><strong>The last weapon</strong> a member carries, asked with both the
    /// weapon and the source gone: a member carrying only the two is refused, the
    /// weapon's member named first (PHASE 6a DECISION 7).</item>
    /// <item>A reorder of the entry already <strong>last</strong>, which would move
    /// nothing (<see cref="LastAlready"/>; PHASE 6a DECISION 11).</item>
    /// <item>The soul's <strong>place on the weapon</strong>, #n (R4): a slot left,
    /// slot n open at the capacity before the deposit's cash, and a deposit of its
    /// price — n the count plus one from another weapon, and the count for a reorder,
    /// which attaches it again as the last of the same count, so a reorder passes at
    /// five (PHASE 6a DECISION 11).</item>
    /// <item><strong>The soul on the weapon</strong>, as the offer asks an enchantment
    /// (<see cref="Offers"/>): an exclusion group beside an entry the weapon keeps,
    /// a prerequisite not ahead of where it lands, a status onto a wand, a status for
    /// the other side onto a staff. What the offer never lists — a unique soul, a kind
    /// that does nothing yet — is not asked: the soul is the party's already, and the
    /// enchanter moves it, never copies it (S:279–281).</item>
    /// <item><strong>What it leaves behind</strong> (S:176–181: the enchanter
    /// "refuses a transfer that would strand a unique's lingering effect"): an entry
    /// on the weapon it comes off — the source, or the weapon itself reordered — whose
    /// prerequisite was ahead of it and no longer is (<see cref="Strands"/>).</item>
    /// <item><strong>What each would cast</strong> (PHASE 6a DECISION 12's caster
    /// clause): a staff or a wand, the weapon or the source, left with a first entry
    /// it cannot cast, or none (<see cref="Weapon.CastsWith"/>; <see cref="WouldCastNothing"/>).
    /// This narrows settled.md's "that effect can be transferred onto a dagger"
    /// (S:339–342): a staff arrives with one soul, so it gives its effect away once
    /// it has a second status to cast.</item>
    /// </list>
    /// A lock the new wielder could not afford is never a refusal: the soul sleeps
    /// there (S:41–42).
    /// </summary>
    /// <param name="campaign">The campaign between its runs.</param>
    /// <param name="weapon">The weapon the soul would land on: in a member's bag or in the stable.</param>
    /// <param name="source">The weapon the soul is on, another the party owns; null for the weapon's own, a reorder.</param>
    /// <param name="index">The soul's place on <paramref name="source"/>, or on <paramref name="weapon"/>.</param>
    /// <exception cref="ArgumentException">Either weapon is none the party owns, or the source is the weapon itself — a reorder names none.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No soul lies at <paramref name="index"/>.</exception>
    public static string? TransferRefusal(CampaignState campaign, Weapon weapon, Weapon? source, int index)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(weapon);
        if (ReferenceEquals(source, weapon))
            throw new ArgumentException($"{weapon.Name} ({weapon.Id}) is named as its own source: a reorder names none.", nameof(source));
        var giver = source ?? weapon;
        if ((uint)index >= (uint)giver.Enchantments.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{giver.Name} ({giver.Id}) carries {giver.Enchantments.Count} enchantments.");

        if (!IsOpen(campaign))
            return Closed(GameContent.Current.Dungeons.Tutorial);
        if (InShop(campaign, weapon))
            return InTheShop(weapon);
        if (source is not null && InShop(campaign, source))
            return InTheShop(source);
        PartyMemberState?[] carriers = [Carrier(campaign, weapon), source is null ? null : Carrier(campaign, source)];
        foreach (var member in carriers.OfType<PartyMemberState>().Distinct())
            if (member.Inventory.All(carried => carried is null || ReferenceEquals(carried, weapon) || ReferenceEquals(carried, source)))
                return WouldCarryNone(member.Id);

        bool reorder = source is null;
        int count = weapon.Enchantments.Count;
        if (reorder && index == count - 1)
            return LastAlready;
        if (Attaching(weapon, reorder ? count : count + 1) is { } gate)
            return gate;

        var soul = giver.Enchantments[index];
        IReadOnlyList<Enchantment> ahead = reorder ? Without(weapon.Enchantments, index) : weapon.Enchantments;
        if (OfferRefusal(weapon, ahead, soul.Def) is { } why)
            return why;

        // What the giver keeps, in its order — the source less the soul, or the
        // weapon with the soul moved to its end — and where each of those stood.
        IReadOnlyList<Enchantment> left = reorder ? [.. ahead, soul] : Without(giver.Enchantments, index);
        int[] stood = [.. Enumerable.Range(0, giver.Enchantments.Count).Where(i => i != index), .. reorder ? [index] : Array.Empty<int>()];
        if (Stranded(giver.Enchantments, left, stood) is { } stranded)
            return stranded;

        IReadOnlyList<Enchantment> landed = [.. ahead, soul.Transferred()];
        if (!weapon.CastsWith(landed[0].Def))
            return WouldCastNothing(weapon);
        if (source is not null && !source.CastsWith(left.Count > 0 ? left[0].Def : null))
            return WouldCastNothing(source);
        return null;
    }

    /// <summary><paramref name="entries"/> less the one at <paramref name="index"/>, the rest in their order.</summary>
    private static Enchantment[] Without(IReadOnlyList<Enchantment> entries, int index)
        => [.. entries.Where((_, i) => i != index)];

    /// <summary>
    /// The first entry of <paramref name="after"/> whose prerequisite was ahead of it
    /// in <paramref name="before"/> — where it stood, at <paramref name="stood"/> —
    /// and is not ahead of it now, said as <see cref="Strands"/> says it; or null. An
    /// entry that lacked one already is not this transfer's doing.
    /// </summary>
    private static string? Stranded(IReadOnlyList<Enchantment> before, IReadOnlyList<Enchantment> after, int[] stood)
    {
        var catalogue = GameContent.Current.Enchantments;
        for (int k = 0; k < after.Count; k++)
        {
            if (!catalogue.LacksPrerequisiteAhead(after[k].Id, after.Take(k).Select(entry => entry.Id), out var need))
                continue;
            if (catalogue.LacksPrerequisiteAhead(after[k].Id, before.Take(stood[k]).Select(entry => entry.Id), out _))
                continue;
            return Strands(after[k].Def, catalogue.TryGet(need, out var needed) ? needed.Name : need);
        }
        return null;
    }

    /// <summary>A reorder of the entry already last, which would move nothing and only cost it a tier (PHASE 6a DECISION 11).</summary>
    public const string LastAlready = "IT IS LAST ALREADY";

    /// <summary>A transfer with nothing to move: every soul the party owns refused on the weapon, or none there. <c>NOTHING TO MOVE - NO ENCHANTMENT THE PARTY OWNS CAN GO ON IT</c>.</summary>
    public const string NothingToMove = "NOTHING TO MOVE - NO ENCHANTMENT THE PARTY OWNS CAN GO ON IT";

    /// <summary>
    /// A transfer that would strand an entry on the weapon the soul comes off
    /// (S:176–181: "refuses a transfer that would strand a unique's lingering
    /// effect"), naming the entry left and what it needs ahead of it: <c>BURNING
    /// NEEDS FLAMING AHEAD OF IT</c>.
    /// </summary>
    public static string Strands(EnchantmentDef stranded, string needed)
    {
        ArgumentNullException.ThrowIfNull(stranded);
        ArgumentNullException.ThrowIfNull(needed);
        return $"{stranded.Name.ToUpperInvariant()} NEEDS {needed.ToUpperInvariant()} AHEAD OF IT";
    }

    /// <summary>A transfer that would leave a staff or a wand with no first entry it can cast (PHASE 6a DECISION 12's caster clause): <c>THE ROTWOOD WOULD CAST NOTHING</c>.</summary>
    public static string WouldCastNothing(Weapon caster)
    {
        ArgumentNullException.ThrowIfNull(caster);
        string name = caster.Name.ToUpperInvariant();
        return $"{(name.StartsWith("THE ", StringComparison.Ordinal) ? name : $"THE {name}")} WOULD CAST NOTHING";
    }

    /// <summary>
    /// Whether the enchanter takes deposits (§6.3; S:411–419): the tutorial beaten —
    /// the first gate every deposit asks (<see cref="Closed"/> says why not), and
    /// what the WEAPONS page asks before it opens the deposit panel at all.
    /// </summary>
    public static bool IsOpen(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        return campaign.IsRetired(GameContent.Current.Dungeons.Tutorial.Id);
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
    /// first (<see cref="Refusal"/>, or a transfer's <see cref="TransferRefusal"/>),
    /// and a refusal throws: the screen never asks for a deposit it would be refused.
    /// Then, in this order:
    /// <list type="number">
    /// <item>the runs the service takes, off the count as it goes in
    /// (<see cref="Wear.ServiceRuns"/>; a transfer's <see cref="TransferRuns"/>);</item>
    /// <item><strong>the whole pool cashed</strong> (<see cref="Weapon.CashWear"/>):
    /// the pool empties now, and the capacity grows on the cash now, identically
    /// whatever the service (R4: "+1 per full bar … cashed in", "the pool itself
    /// still empties at every deposit"). A transfer's source is not cashed: its
    /// wear stays in its pool (PHASE 6a DECISION 13);</item>
    /// <item>the weapon taken out of its place and queued, the campaign's count of
    /// deposits its ordinal (<see cref="CampaignState.Enqueue"/>) — a transfer's
    /// source taken out with it, into the shop as the same deposit — a hand either
    /// empties armed from the bag, as a move's is.</item>
    /// </list>
    /// Nothing is attached, moved or rolled here: a service does that as it
    /// finishes (PHASE 6a DECISION 3; <see cref="Tick"/>, <see cref="Improve"/>).
    /// </summary>
    /// <param name="campaign">The campaign between its runs.</param>
    /// <param name="from">Where the weapon lies.</param>
    /// <param name="service">What the deposit buys.</param>
    /// <param name="enchantmentId">The enchantment an Enchant attaches; null for any other service.</param>
    /// <param name="source">Where a transfer's source lies — another weapon the party owns — or null for a reorder of the weapon's own, and for any other service.</param>
    /// <param name="sourceIndex">A transfer's soul, by its place on its source, or on the weapon for a reorder; null for any other service.</param>
    /// <returns>The deposit made, and the capacity before and after its cash, for the screen's log.</returns>
    /// <exception cref="InvalidOperationException">A visit is in progress, nothing lies at <paramref name="from"/> or <paramref name="source"/>, or the enchanter refuses the deposit — the reason in the message.</exception>
    /// <exception cref="ArgumentException">An Enchant names no enchantment, or another service names one; a transfer names no soul, or another service names a source; the source is the weapon itself; or a member a place names is not in the party.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A place is not one the party or the stable has, or no soul lies at <paramref name="sourceIndex"/>.</exception>
    public static DepositMade Deposit(CampaignState campaign, WeaponPlace from, Service service, string? enchantmentId = null,
        WeaponPlace? source = null, int? sourceIndex = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (campaign.Visit is { } visit)
            throw new InvalidOperationException(
                $"A visit to '{visit.Def.Id}' is in progress: the enchanter takes deposits between runs only.");
        var weapon = campaign.WeaponAt(from) ?? throw new InvalidOperationException($"Nothing lies at {from} to deposit.");
        Weapon? giver = null;
        string? refused;
        if (service == Service.Transfer)
        {
            if (enchantmentId is not null)
                throw new ArgumentException($"A transfer names the soul it moves by where it lies, and '{enchantmentId}' is named.", nameof(enchantmentId));
            if (sourceIndex is not { } index)
                throw new ArgumentException("A transfer names the soul it moves: its place on the source, or on the weapon.", nameof(sourceIndex));
            giver = source is { } at ? campaign.WeaponAt(at) ?? throw new InvalidOperationException($"Nothing lies at {at} to take a soul from.") : null;
            refused = TransferRefusal(campaign, weapon, giver, index);
        }
        else
        {
            if (source is not null || sourceIndex is not null)
                throw new ArgumentException($"Only a transfer names a source, and a {service} names one.", nameof(source));
            refused = Refusal(campaign, weapon, service, enchantmentId);
        }
        if (refused is not null)
            throw new InvalidOperationException($"The enchanter refuses {weapon.Name} ({weapon.Id}) for {service}: {refused}");

        int runs = service == Service.Transfer ? TransferRuns(weapon, reorder: giver is null) : Wear.ServiceRuns(weapon.Enchantments.Count);
        var before = weapon.WearPool;
        int cashed = weapon.CashWear();
        var entry = new Deposit(campaign.DepositsMade, weapon, service, cashed, runs, enchantmentId, giver, sourceIndex);
        var (armed, sourceArmed) = campaign.Enqueue(from, entry, giver is null ? null : source);
        return new DepositMade(entry, before, weapon.WearPool, armed, sourceArmed);
    }

    // ── A take-back ───────────────────────────────────────────────────────────

    /// <summary>
    /// Take <paramref name="deposit"/> back out of the shop, between runs (PHASE 6a
    /// DECISION 8): the weapon goes to the end of the stable — a transfer's source
    /// after it, its soul still on it and its wear, never cashed, still in its pool
    /// — nothing attached, moved or rolled, the runs worked lost; and the deposit's
    /// cash reversed exactly (<see cref="Weapon.RefundWear"/>), the wear back in the
    /// pool and off what the weapon has cashed, so its capacity and the progress
    /// toward the next point are as they were before it went in. A take-back is an
    /// undo whose cost is the runs lost, never a way to grow a capacity with no
    /// downtime. The ordinal is not reused.
    /// </summary>
    /// <returns>The deposit taken back, and the capacity before and after its cash was reversed, for the screen's log.</returns>
    /// <exception cref="InvalidOperationException">A visit is in progress, or <paramref name="deposit"/> is not in the shop.</exception>
    public static TakenBack TakeBack(CampaignState campaign, Deposit deposit)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(deposit);
        campaign.TakeOut(deposit);   // the hub's door: between runs, and only a deposit in the shop
        var before = deposit.Weapon.WearPool;
        deposit.Weapon.RefundWear(deposit.WearAtDeposit);
        return new TakenBack(deposit, before, deposit.Weapon.WearPool);
    }

    // ── The clock ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The deposits the next run that counts works (§6.2: "the two most recently
    /// deposited"): the <see cref="Tuning.EnchanterWorksOn"/> with the highest
    /// ordinals, in deposit order — a transfer one of them, its two weapons being one
    /// deposit (PHASE 6a DECISION 13). Every other deposit in <paramref name="queue"/>
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
    /// ceiling, and the weapon has been in the shop since — a Transfer moves its soul
    /// off its source, or off the weapon, and on last a tier down, and a refinement
    /// attaches nothing; then the improvement roll is made on the weapon as it now
    /// stands (<see cref="Improve"/>, on <paramref name="mapSeed"/> and the deposit's
    /// ordinal), and the weapon goes to the end of the stable, a transfer's source
    /// after it (<see cref="CampaignState.Return"/>). Every other deposit is
    /// reported stalled.</item>
    /// <item><strong>A run that did not count</strong> moves nothing and rolls
    /// nothing, and reports every deposit stalled with its runs as they were — a
    /// fresh one's at 0 — so the summary can say the enchanter worked nothing.</item>
    /// </list>
    /// </summary>
    /// <param name="campaign">The campaign whose run has just ended.</param>
    /// <param name="mapSeed">The map seed the run was entered on, which its save holds: what a finishing service's improvement roll is keyed by, with the deposit's ordinal, so a reload that replays the run's end replays the roll (R1).</param>
    /// <param name="counted">Whether the run counted (<see cref="RunResult.ServiceTick"/>).</param>
    /// <returns>A report for every deposit in the shop as the run ended, in deposit order; empty for an empty shop.</returns>
    internal static IReadOnlyList<ServiceReport> Tick(CampaignState campaign, long mapSeed, bool counted)
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
                var moved = Complete(entry);
                var (improved, roll) = Improve(entry.Weapon, entry.Service, entry.WearAtDeposit, mapSeed, entry.Ordinal);
                reports[i] = reports[i] with { Improved = improved, Roll = roll, Tier = moved?.Tier };
                campaign.Return(entry);
            }
        }
        return reports;
    }

    /// <summary>What the tick says of <paramref name="entry"/>: its weapon by id, its service, its runs as they stand, whether it was left unworked, and its enchantment — for a transfer, the soul it moves, and its source by id.</summary>
    private static ServiceReport Report(Deposit entry, bool stalled)
        => new(entry.Weapon.Id, entry.Service, entry.RunsWorked, entry.Runs, stalled,
            entry.Service == Service.Transfer ? entry.Soul?.Id : entry.EnchantmentId, SourceId: entry.Source?.Id);

    /// <summary>
    /// A service's last run worked: an Enchant attaches its enchantment — the
    /// content's own entry, by id — last, at <see cref="AttachedTier"/> with no XP
    /// (<see cref="Weapon.Attach"/>); a Transfer takes its soul off its source, or
    /// off the weapon for a reorder (<see cref="Weapon.Detach"/>), and attaches it
    /// last a tier down (<see cref="Enchantment.Transferred"/>) — the deposit asked
    /// every gate, and both weapons have been in the shop since; a refinement
    /// attaches nothing.
    /// </summary>
    /// <returns>A transfer's soul as it landed; null for any other service.</returns>
    private static Enchantment? Complete(Deposit entry)
    {
        switch (entry.Service)
        {
            case Service.Enchant:
                string id = entry.EnchantmentId
                    ?? throw new InvalidOperationException($"Deposit {entry.Ordinal}, an Enchant of {entry.Weapon.Name}, names no enchantment.");
                entry.Weapon.Attach(new Enchantment(GameContent.Current.Enchantments[id], AttachedTier));
                return null;
            case Service.Refine:
                return null;
            case Service.Transfer:
                int index = entry.SourceIndex
                    ?? throw new InvalidOperationException($"Deposit {entry.Ordinal}, a Transfer onto {entry.Weapon.Name}, names no soul.");
                var moved = (entry.Source ?? entry.Weapon).Detach(index).Transferred();
                entry.Weapon.Attach(moved);
                return moved;
            default:
                throw new ArgumentOutOfRangeException(nameof(entry), entry.Service, "Not a Service.");
        }
    }

    // ── The improvement roll ──────────────────────────────────────────────────

    /// <summary>
    /// The improvement roll's splitter (§6.4; ROADMAP: new randomness gets "its own
    /// stream derived from the map seed … never a continuation"): odd, 32-bit —
    /// murmur3's first mixing constant — and distinct from every other salt and mix
    /// in <c>Logic/</c>. A compiled constant like every salt: it decides what a seed
    /// rolls, so it is parity's and never a tuning key.
    /// </summary>
    public const long ServiceSalt = 0xCC9E2D51;

    /// <summary>
    /// The per-deposit term of <see cref="ImproveStream"/>, multiplied by the
    /// deposit's ordinal: odd — murmur3's second mixing constant — so no two of the
    /// first 2³² deposits share a composed seed, and each deposit's roll is its own
    /// seeded stream rather than a position in a running one.
    /// </summary>
    internal const long ServiceMix = 0x1B873593;

    /// <summary>
    /// The stream the deposit <paramref name="ordinal"/> finishing on a run entered
    /// on <paramref name="mapSeed"/> rolls its improvement on:
    /// <c>SplitMix32(mapSeed ^ ServiceSalt ^ ordinal × ServiceMix)</c>, finalized as
    /// every counter-keyed stream here is (<see cref="FarmLadder.SplitMix32"/>),
    /// since the roll's first draw — whether it lands — is the load-bearing one.
    /// Both keys are saved while the run is under way — the run's seed with its
    /// visit, the ordinal with its queued deposit — so a reload that replays the same
    /// run's end replays the same roll,
    /// and a bad roll is never reloaded away (R1).
    /// </summary>
    /// <param name="mapSeed">The seed the finishing run was entered on.</param>
    /// <param name="ordinal">The deposit's ordinal: the campaign's count of deposits as it went in.</param>
    internal static Mulberry32 ImproveStream(long mapSeed, int ordinal)
        => new(FarmLadder.SplitMix32(mapSeed ^ ServiceSalt ^ (ordinal * ServiceMix)));

    /// <summary>Percentages are integers out of this.</summary>
    private const int Percent = 100;

    /// <summary>
    /// A finished service's improvement roll (§6.4), made on
    /// <paramref name="weapon"/> as it now stands — its enchantment attached, for an
    /// Enchant — and drawn in this order off <see cref="ImproveStream"/>:
    /// <list type="number">
    /// <item><strong>Whether it lands</strong>: <c>NextInt(0, denominator − 1) &lt;
    /// numerator</c>, on the service's odds (<see cref="Wear.ImproveOdds"/>) — the
    /// one draw refinement moves, since it "improves whether the roll lands, not
    /// what it lands on" (S:782–784). A deposit that cashed no wear draws nothing
    /// and rolls nothing: "no wear, no roll" (P6:501–503), which only a refinement
    /// of an unworn weapon meets, since an enchantment's price is a wear at
    /// least.</item>
    /// <item><strong>Which outcome</strong>: a graft when <c>NextInt(0, 99) &lt;
    /// </c><see cref="Tuning.GraftSharePercent"/>, a deepen otherwise — "deepen stays
    /// the common result and a graft stays the surprise" (P6:547–551).</item>
    /// <item><strong>Which modifier</strong>, one draw over that outcome's
    /// candidates (<see cref="DeepenCandidates"/>, weighted toward the class
    /// signature; <see cref="GraftCandidates"/>, alike), and a stack of it acquired
    /// (<see cref="Weapon.Acquire"/>, held to forged plus the acquired headroom — a
    /// graft, forged at 0, caps at the headroom). An outcome with no candidate falls
    /// through to the other, and with neither the landed roll changes nothing:
    /// "a weapon with every modifier capped rolls nothing at all" (P6:592–593),
    /// read as every modifier it may hold, since a weapon whose carried modifiers
    /// are all capped "can still gain breadth through grafts" (P6:598–600). The
    /// split, the weight and the fall-through are PHASE 6a DECISION 5.</item>
    /// </list>
    /// </summary>
    /// <param name="weapon">The weapon coming back.</param>
    /// <param name="service">What its deposit bought.</param>
    /// <param name="wearAtDeposit">The wear its deposit cashed.</param>
    /// <param name="mapSeed">The seed the finishing run was entered on.</param>
    /// <param name="ordinal">The deposit's ordinal.</param>
    /// <returns>What the roll did, or null when it did not land or found nothing to improve; and the roll as drawn, for the log, or null when nothing was rolled.</returns>
    internal static (Improvement? Improved, ImproveRoll? Roll) Improve(Weapon weapon, Service service, int wearAtDeposit, long mapSeed, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentOutOfRangeException.ThrowIfNegative(wearAtDeposit);
        if (wearAtDeposit == 0)
            return (null, null);   // no wear, no roll: nothing to repair

        var stream = ImproveStream(mapSeed, ordinal);
        var (numerator, denominator) = Wear.ImproveOdds(service, wearAtDeposit);
        var roll = new ImproveRoll(stream.NextInt(0, checked((int)denominator) - 1), denominator, numerator);
        if (!roll.Landed)
            return (null, roll);

        bool graft = stream.NextInt(0, Percent - 1) < GameContent.Current.Tuning.GraftSharePercent;
        var deepens = DeepenCandidates(weapon);
        var grafts = GraftCandidates(weapon);
        bool None() => graft ? grafts.Count == 0 : deepens.Count == 0;
        if (None())
            graft = !graft;   // nothing that outcome may take: the other's
        if (None())
            return (null, roll);   // nothing to deepen and nothing to graft

        var modifier = graft
            ? grafts[stream.NextInt(0, grafts.Count - 1)]
            : Weighted(deepens, stream.NextInt(0, deepens.Sum(candidate => candidate.Weight) - 1));
        weapon.Acquire(modifier, 1);
        return (new Improvement(graft ? ImprovementKind.Graft : ImprovementKind.Deepen, modifier), roll);
    }

    /// <summary>The candidate <paramref name="pick"/> lands on, walking <paramref name="candidates"/> in their order, each as wide as its weight.</summary>
    private static ModifierType Weighted(IReadOnlyList<(ModifierType Modifier, int Weight)> candidates, int pick)
    {
        foreach (var (modifier, weight) in candidates)
        {
            if (pick < weight)
                return modifier;
            pick -= weight;
        }
        throw new ArgumentOutOfRangeException(nameof(pick), pick, "The pick is past the candidates' whole weight.");
    }

    /// <summary>
    /// What a deepen may land on (§6.4): every modifier <paramref name="weapon"/>
    /// carries, forged or acquired, still below its ceiling — forged plus the
    /// acquired headroom (<see cref="ModifierRules.Cap"/>), the one budget farming
    /// spends too — and one the relations still allow beside what it carries
    /// (<see cref="ModifierRules.Allowed"/>), in <see cref="ModifierType"/> order.
    /// Each weighs 1, and the class's signature
    /// (<see cref="WeaponClassDef.Signature"/>; a caster has none)
    /// <see cref="Tuning.DeepenSignatureWeight"/>: "biased toward the class
    /// signature".
    /// </summary>
    internal static IReadOnlyList<(ModifierType Modifier, int Weight)> DeepenCandidates(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        var content = GameContent.Current;
        var rules = content.Modifiers;
        var signature = content.Weapons.Class(weapon.Class).Signature;
        var candidates = new List<(ModifierType, int)>();
        foreach (var (modifier, stacks) in weapon.Modifiers.Entries)
            if (stacks < rules.Cap(modifier, weapon.Forged.Stacks(modifier)) && rules.Allowed(modifier, weapon.Kind, weapon.Modifiers))
                candidates.Add((modifier, modifier == signature ? content.Tuning.DeepenSignatureWeight : 1));
        return candidates;
    }

    /// <summary>
    /// What a graft may land on (§6.4): every modifier <paramref name="weapon"/> has
    /// never carried that the relations let it take as an acquired first stack
    /// (<see cref="ModifierRules.CanGraft"/>, asked as the enchanter's graft: the
    /// kind, the exclusions, the prerequisites — so a Block grafted first makes a
    /// Riposte possible later — and never a forged-only one, so <c>Charges</c>, the
    /// one negative graft, is never grafted), less those no weapon stacks
    /// (<see cref="ModifierRules.NeverStacked"/>), in <see cref="ModifierType"/>
    /// order, each alike.
    /// </summary>
    internal static IReadOnlyList<ModifierType> GraftCandidates(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        var rules = GameContent.Current.Modifiers;
        return Enum.GetValues<ModifierType>()
            .Where(modifier => weapon.Stacks(modifier) == 0 && !ModifierRules.NeverStacked.Contains(modifier)
                && rules.CanGraft(modifier, weapon.Kind, weapon.Modifiers, weapon.Forged, asForged: false))
            .ToArray();
    }
}
