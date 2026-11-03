using WearRules = GameEngine.DistanceRPG.Logic.Wear;   // inside Weapon the property Wear hides the class

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A weapon as an item: the statline and identity of its <see cref="WeaponDef"/>,
/// the modifier stacks it carries (§1.1) and the enchantments attached to it
/// (§3.3). Range and Cost are in logic units (32 per tile); Cost is subtracted
/// from the wielder's movement budget per swing or cast, ManaCost from the mana
/// pool per cast. A class rather than a record because <see cref="Modifiers"/>
/// is the live total: <see cref="Forged"/> is the identity spread, fixed at
/// construction and never written, and exists only to compute the ceiling;
/// <see cref="Modifiers"/> is forged plus acquired, and is what every
/// resolution site reads. Made by <see cref="WeaponCatalogue.Instantiate"/>,
/// one instance per item, and brought back by a load with both spreads as they
/// were saved (§5.1).
/// <para>
/// It also carries its wear (§6.1): the work it has done since it was last
/// serviced, banked a hit at a time up to its capacity, and the wear it has
/// ever cashed at the enchanter, which its capacity is replayed from
/// (<see cref="WearRules.Capacity"/>, PHASE 6a DECISION 2). Every new weapon
/// has neither: a drop, a boss's drop, an enemy's weapon and the starting kit
/// arrive at 0 of <see cref="Tuning.StartingPool"/>. Nothing in combat reads
/// either, so a full weapon plays as a fresh one does.
/// </para>
/// <para>
/// It is one of the two things a slot holds (<see cref="IItem"/>, Phase 6b), and
/// the only one a hand does.
/// </para>
/// </summary>
public sealed class Weapon : IItem
{
    /// <summary>Percentages are integers out of this.</summary>
    private const int Percent = 100;

    /// <summary>
    /// The attached entries, in attachment order — the weapon's own copy of what
    /// it was handed, and the only thing <see cref="CreditEnchantment"/> writes
    /// to, the enchanter appends to (<see cref="Attach"/>) and a transfer takes
    /// an entry off (<see cref="Detach"/>). Private, and
    /// <see cref="Enchantments"/> hands out a read-only view of it rather than the
    /// list itself: attachment order is gameplay (§1.7) and fixes the order
    /// entries fire in, so it is the weapon's to change and nobody else's.
    /// </summary>
    private readonly List<Enchantment> _enchantments;

    /// <param name="def">The row this item is stamped from.</param>
    /// <param name="enchantments">The entries attached, in attachment order: the forged ones first, anything acquired behind them.</param>
    /// <param name="forgedCount">
    /// How many of <paramref name="enchantments"/>, counted from the front, are
    /// forged. Defaults to what <paramref name="def"/> lists, so an entry handed
    /// in past that list is one something attached later;
    /// <see cref="WeaponCatalogue.Instantiate"/> passes its own count, because a
    /// wand's element is supplied at instantiation and is forged all the same
    /// (§1.4, §3.1) though no def lists it.
    /// </param>
    /// <param name="forgedGraft">
    /// A modifier grafted onto <paramref name="def"/>'s spread <em>as forged</em>,
    /// one stack of it, or null for none: the boss's theme on the boss's own drop
    /// (§4.3). It goes on here, at construction, because <see cref="Forged"/> is
    /// fixed here and never written again — a forged graft is part of what the
    /// weapon <em>is</em>, which is what puts its ceiling a stack deeper than an
    /// acquired one's (forged + 5, §1.1). It asks the one question every graft
    /// asks (<see cref="ModifierRules.CanGraft"/>, as forged): the relations
    /// beside the def's spread, and the forge limit.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="forgedCount"/> is not a count of <paramref name="enchantments"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="forgedGraft"/> cannot be forged onto <paramref name="def"/>:
    /// the relations refuse it beside the def's spread, or it would forge past
    /// <see cref="ModifierRules.MaxForged"/>. A source asks
    /// <see cref="ModifierRules.CanGraft"/> before it offers a graft — the boss's
    /// drop table is computed from that same question — so a refusal here is a
    /// source that skipped its gate: thrown rather than forged silently.
    /// </exception>
    public Weapon(WeaponDef def, IReadOnlyList<Enchantment> enchantments, int? forgedCount = null, ModifierType? forgedGraft = null)
        : this(def, enchantments)
    {
        Forged = Forge(def, forgedGraft);
        Modifiers = Forged;
        int forged = forgedCount ?? Math.Min(def.Enchantments.Count, _enchantments.Count);
        if (forged < 0 || forged > _enchantments.Count)
            throw new ArgumentOutOfRangeException(nameof(forgedCount), forgedCount,
                $"'{def.Id}' was handed {_enchantments.Count} entries; the forged ones are a prefix of them.");
        ForgedEnchantmentCount = forged;
        Resolve();
    }

    /// <summary>
    /// The item a save describes (§5.1), coming back: the statline, class and
    /// shape off <paramref name="def"/> — its id is all a save keeps of them, so
    /// an edit to the row reaches every saved copy — and what is the item's own
    /// as it was saved: <strong>both</strong> its forged and its live spreads, and
    /// its entries in attachment order with how many of them are forged. The
    /// forged spread is the one the item was made with (a boss drop's graft
    /// included), never re-read off the row, so its ceiling is where it was.
    /// Internal: a load's door, never a second way to make a weapon.
    /// <para>
    /// Nothing is re-asked of the relations or the forge limit: the item was
    /// legal when it was made, and a restore brings it back rather than making
    /// it again. Three things are refused, because no play can reach them: a live
    /// spread below the forged one — acquired stacks are only ever added — a
    /// forged count that is not a prefix of the entries, and a negative wear or
    /// wear cashed. Wear above today's capacity is not refused: capacity is
    /// today's tuning's, replayed, and a tuning that lowered it takes no wear
    /// away (<see cref="AccrueWear"/>); the load holds wear to a ceiling no tuning
    /// in range can pass instead (<c>SaveMapper</c>).
    /// </para>
    /// </summary>
    /// <param name="def">The catalogue row, by the saved id.</param>
    /// <param name="forged">The identity spread the item was made with.</param>
    /// <param name="modifiers">The live total: forged plus acquired.</param>
    /// <param name="enchantments">The attached entries, in attachment order, each at its tier with its XP.</param>
    /// <param name="forgedEnchantmentCount">How many of <paramref name="enchantments"/>, from the front, are forged.</param>
    /// <param name="wear">The wear banked since its last service (<see cref="Wear"/>).</param>
    /// <param name="wearCashed">The wear it has ever cashed at the enchanter (<see cref="WearCashed"/>), its capacity's history.</param>
    /// <exception cref="ArgumentException"><paramref name="modifiers"/> holds fewer stacks of some modifier than <paramref name="forged"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="forgedEnchantmentCount"/> is not a count of <paramref name="enchantments"/>, or <paramref name="wear"/> or <paramref name="wearCashed"/> is negative.</exception>
    internal Weapon(WeaponDef def, ModifierSet forged, ModifierSet modifiers, IReadOnlyList<Enchantment> enchantments, int forgedEnchantmentCount,
        int wear = 0, int wearCashed = 0)
        : this(def, enchantments)
    {
        ArgumentNullException.ThrowIfNull(forged);
        ArgumentNullException.ThrowIfNull(modifiers);
        foreach (var (type, stacks) in forged.Entries)
            if (modifiers.Stacks(type) < stacks)
                throw new ArgumentException(
                    $"'{def.Id}' forges {type} x{stacks} and holds {modifiers.Stacks(type)} live: the live spread is the forged one plus what was acquired, never less.",
                    nameof(modifiers));
        if (forgedEnchantmentCount < 0 || forgedEnchantmentCount > _enchantments.Count)
            throw new ArgumentOutOfRangeException(nameof(forgedEnchantmentCount), forgedEnchantmentCount,
                $"'{def.Id}' carries {_enchantments.Count} entries; the forged ones are a prefix of them.");
        ArgumentOutOfRangeException.ThrowIfNegative(wear);
        ArgumentOutOfRangeException.ThrowIfNegative(wearCashed);

        Forged = forged;
        Modifiers = modifiers;
        ForgedEnchantmentCount = forgedEnchantmentCount;
        Wear = wear;
        WearCashed = wearCashed;
        Resolve();
    }

    /// <summary>What every construction shares: the row's statline and identity, and the entries in attachment order. The spreads and the forged count are each constructor's own.</summary>
    private Weapon(WeaponDef def, IReadOnlyList<Enchantment> enchantments)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(enchantments);

        Id = def.Id;
        Name = def.Name;
        Class = def.Class;
        Role = def.Role;
        Range = def.Range;
        Damage = def.Damage;
        Cost = def.Cost;
        ManaCost = def.ManaCost;
        Forged = ModifierSet.Empty;
        Modifiers = ModifierSet.Empty;
        _enchantments = enchantments.ToList();   // attachment order, copied so nothing outside can reorder it
        Enchantments = _enchantments.AsReadOnly();   // a live view: a credited entry and an attached one show through, a write does not get in
        AreaShape = def.Shape;
        Unique = def.Unique;
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>The name as the screen says it (<see cref="Shown"/>): <c>SKIRMISHERS PIKE</c>. The log keeps <see cref="Name"/>.</summary>
    public string ShownName => Shown(Name);

    /// <summary>
    /// A weapon's name as every screen says it: in capitals, as the HUD's font draws
    /// them, and with no apostrophe, which the font has no glyph for — drawn, the gap
    /// read <c>SKIRMISHER S PIKE</c>. <c>Skirmisher's Pike</c> reads <c>SKIRMISHERS
    /// PIKE</c>. The one door every weapon name takes onto the screen, as a save's
    /// notice names an id bare.
    /// </summary>
    public static string Shown(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Replace("'", "", StringComparison.Ordinal).ToUpperInvariant();
    }

    public WeaponClass Class { get; }
    public VariantRole? Role { get; }
    public int Range { get; }
    public int Damage { get; }
    public int Cost { get; }
    public int ManaCost { get; }

    /// <summary>The identity spread — class baseline, variant role, unique spread, dungeon theme — fixed here and never written again.</summary>
    public ModifierSet Forged { get; }

    /// <summary>The live total, forged plus acquired: what every resolution site reads.</summary>
    public ModifierSet Modifiers { get; private set; }

    /// <summary>In attachment order, which is gameplay (§1.7): never normalised, sorted or deduped. An entry the enchanter attaches comes last (<see cref="Attach"/>), and one a transfer takes off leaves the rest in their order (<see cref="Detach"/>).</summary>
    public IReadOnlyList<Enchantment> Enchantments { get; }

    /// <summary>
    /// How many of <see cref="Enchantments"/>, from the front, are forged: part
    /// of what the weapon <em>is</em>, rather than what was attached to it later
    /// (§3.1). The same line <see cref="Forged"/> draws through the modifiers,
    /// drawn through the entries — fixed at construction, and moved only by a
    /// transfer taking a forged entry off (<see cref="Detach"/>, Phase 6a.6), which
    /// closes the list up behind it, so the forged ones stay a prefix — and put back
    /// by a reorder of a weapon forged whole, whose soul lands last still forged
    /// (<see cref="Attach"/>).
    /// <para>
    /// The distinction is not weapon-versus-magic: a staff's effect and a wand's
    /// element are forged, and they are the whole of what those weapons do — a
    /// wand with no forged entry would have no XP source at all (§2.2). It is
    /// what weapon proficiency is credited on, which is why it is a count here
    /// and not a convention at the credit site: a wizard's Arcane dagger levels
    /// daggers on the knife alone.
    /// </para>
    /// <para>
    /// A forged entry a transfer moves arrives on its new weapon acquired, last:
    /// the mark stays with the weapon that was forged with it (PHASE 6a DECISION 13).
    /// A reorder keeps it where the weapon is forged whole, and loses it where an
    /// acquired entry would stand ahead of it (PHASE 6a DECISION 11).
    /// </para>
    /// </summary>
    public int ForgedEnchantmentCount { get; private set; }

    /// <summary>Whether the entry at <paramref name="index"/> of <see cref="Enchantments"/> is forged (<see cref="ForgedEnchantmentCount"/>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Nothing is attached at <paramref name="index"/>.</exception>
    public bool IsForged(int index)
    {
        if ((uint)index >= (uint)Enchantments.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{Name} ({Id}) carries {Enchantments.Count} enchantments.");
        return index < ForgedEnchantmentCount;
    }

    /// <summary>A wand's geometry; null for everything else.</summary>
    public AreaShape? AreaShape { get; }

    public bool Unique { get; }

    /// <summary>
    /// The sort of weapon the relations file means by "kind" (§1.1): its class
    /// row's in <c>weapons.json</c> (<see cref="WeaponCatalogue.KindOf"/>) — staff
    /// and wand cast, bow and throwing are ranged, the rest melee, as the shipped
    /// rows say. Read off the current content every time rather than kept on the
    /// item, so a save holds nothing of it; the load itself builds no weapon, so
    /// it never asks.
    /// </summary>
    public WeaponKind Kind => GameContent.Current.Weapons.KindOf(Class);

    /// <summary>True for a staff or wand — a weapon that casts rather than strikes.</summary>
    public bool IsCaster => Kind == WeaponKind.Caster;

    /// <summary>A caster's innate enchantment — the effect a staff casts, the element a wand carries — first in attachment order; null on a martial weapon.</summary>
    public Enchantment? Innate => IsCaster && Enchantments.Count > 0 ? Enchantments[0] : null;

    // ── Wear (§6.1) ──────────────────────────────────────────────────────────

    /// <summary>
    /// The wear this weapon has banked since it was last serviced (§6.1): a
    /// count of the work it has done, one direction only — the game never says
    /// durability. 0 on every new weapon, never negative, and no more than
    /// <see cref="WearCapacity"/> whenever accrual wrote it
    /// (<see cref="AccrueWear"/>); a tuning that lowered the capacity since leaves
    /// it where it was. Nothing in combat reads it.
    /// </summary>
    public int Wear { get; private set; }

    /// <summary>
    /// The wear this weapon has ever cashed at the enchanter: its capacity's
    /// whole history, the one number <see cref="WearPool"/> is replayed from, as
    /// a member's maxima are from its XP (PHASE 6a DECISION 2). 0 on every new
    /// weapon.
    /// </summary>
    public int WearCashed { get; private set; }

    /// <summary>
    /// Where the capacity stands, replayed from <see cref="WearCashed"/> under the
    /// current tuning (<see cref="WearRules.Capacity"/>): <c>Max</c> is the
    /// capacity, <c>XpIntoNext</c> the wear cashed toward its next point and
    /// <c>XpToNext</c> the bar that point costs.
    /// </summary>
    public PoolProgress WearPool => WearRules.Capacity(WearCashed);

    /// <summary>The most wear this weapon banks: <see cref="Tuning.StartingPool"/> on a new weapon, grown by full bars cashed (§6.1).</summary>
    public int WearCapacity => WearPool.Max;

    /// <summary>Whether this weapon has banked all the wear it can: a hit banks nothing more until a deposit spends it. It still plays exactly as it did (§6.1: "a fully worn weapon is not broken").</summary>
    public bool WornFull => Wear >= WearCapacity;

    /// <summary>
    /// Bank <paramref name="n"/> wear, up to <see cref="WearCapacity"/>. A weapon
    /// at or over its capacity banks nothing, so a tuning that lowered the
    /// capacity never takes wear away and never adds past it. The one caller is
    /// <see cref="WearRules.Accrue"/>, the turn system's wear feed's door
    /// (<c>WearTests.WearAccruesInOnePlace</c>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative: wear is only ever banked here.</exception>
    internal void AccrueWear(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        int capacity = WearCapacity;
        if (Wear < capacity)
            Wear = (int)Math.Min(capacity, (long)Wear + n);
    }

    /// <summary>
    /// Cash the whole pool in at the enchanter (§6.1; R4): the wear banked goes
    /// to <see cref="WearCashed"/>, the capacity growing on it as it is replayed,
    /// and the pool empties — whatever the deposit bought, "the pool itself still
    /// empties at every deposit". What was cashed is saturated at
    /// <see cref="ContentBounds.MostCount"/>, a billion hits in, which play never
    /// reaches; since <see cref="Wear"/> is no more than that, what was cashed is
    /// never less than what this call returns. The one caller is
    /// <see cref="Enchanter.Deposit"/>: the enchanter is the single place wear is
    /// spent (P6:941–942; <c>EnchanterTests.WearIsSpentAndReturnedInOnePlace</c>).
    /// </summary>
    /// <returns>The wear cashed: the deposit's.</returns>
    internal int CashWear()
    {
        int cashed = Wear;
        Wear = 0;
        WearCashed = (int)Math.Min(ContentBounds.MostCount, (long)WearCashed + cashed);
        return cashed;
    }

    /// <summary>
    /// A deposit taken back (PHASE 6a DECISION 8): the exact inverse of
    /// <see cref="CashWear"/> — <paramref name="cashed"/> goes back into the pool
    /// and off <see cref="WearCashed"/>, so the capacity and its progress, replayed
    /// from what was cashed, are what they were before the deposit. A deposit and
    /// its take-back therefore leave a weapon as they found it, and a take-back is
    /// never a way to grow capacity with no downtime. The one inexact case is a
    /// <see cref="WearCashed"/> that saturated in the cash, which play never
    /// reaches. The one caller is <see cref="Enchanter.TakeBack"/>.
    /// </summary>
    /// <param name="cashed">What the deposit cashed (<see cref="Deposit.WearAtDeposit"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cashed"/> is negative, or more than the weapon has ever cashed.</exception>
    internal void RefundWear(int cashed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cashed);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cashed, WearCashed);
        Wear = (int)Math.Min(ContentBounds.MostCount, (long)Wear + cashed);
        WearCashed -= cashed;
    }

    /// <summary>
    /// Attach <paramref name="entry"/> as the weapon's last enchantment: what a
    /// finished Enchant service does (§6.2; <see cref="Enchanter"/>), and a finished
    /// transfer with the soul it moved (§6.5). Last, never sorted in: "a new
    /// enchantment always goes last" (§3.3). Acquired, never forged — but for the
    /// soul a reorder moves on a weapon forged whole, <paramref name="forged"/>, which
    /// keeps its mark behind the forged ones (PHASE 6a DECISION 11). The enchanter's
    /// own gates (<see cref="Enchanter.Refusal"/>, <see cref="Enchanter.TransferRefusal"/>)
    /// and the save's (check 29) keep a weapon below the most a weapon can hold, and
    /// this holds it there too, so no path builds a weapon the next load refuses.
    /// </summary>
    /// <param name="entry">The entry, attached last.</param>
    /// <param name="forged">Whether it lands forged: only behind entries every one forged, so the forged ones stay a prefix.</param>
    /// <exception cref="InvalidOperationException">The weapon already holds <see cref="ContentBounds.MostEnchantments"/>, or <paramref name="forged"/> behind an acquired entry.</exception>
    internal void Attach(Enchantment entry, bool forged = false)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_enchantments.Count >= ContentBounds.MostEnchantments)
            throw new InvalidOperationException(
                $"{Name} ({Id}) holds {_enchantments.Count} enchantments, the most a weapon can (EnchantmentSlots at its most): the enchanter never attaches past it.");
        if (forged && ForgedEnchantmentCount != _enchantments.Count)
            throw new InvalidOperationException(
                $"{Name} ({Id}) carries {_enchantments.Count - ForgedEnchantmentCount} acquired enchantments: a forged one cannot land behind them, since the forged ones are a prefix.");
        _enchantments.Add(entry);
        if (forged)
            ForgedEnchantmentCount++;
    }

    /// <summary>
    /// Take the entry at <paramref name="index"/> off the weapon: what a finished
    /// transfer does to the weapon it moves a soul from — another, or its own, for
    /// a reorder (§6.5; <see cref="Enchanter"/>). The entries behind it close up in
    /// their order, which is gameplay and never re-sorted, and a forged entry taken
    /// off takes a forged place with it (<see cref="ForgedEnchantmentCount"/>), so
    /// the forged ones stay a prefix. The one door an entry leaves a weapon by; its
    /// one caller is the enchanter's clock, finishing a transfer
    /// (<c>TransferTests.AForgedInnateArrivesAcquired_ItsForgedMarkLeavesWithIt</c>).
    /// </summary>
    /// <returns>The entry taken off, as it was: its tier and its XP.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Nothing is attached at <paramref name="index"/>.</exception>
    internal Enchantment Detach(int index)
    {
        if ((uint)index >= (uint)_enchantments.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{Name} ({Id}) carries {_enchantments.Count} enchantments.");
        var entry = _enchantments[index];
        _enchantments.RemoveAt(index);
        if (index < ForgedEnchantmentCount)
            ForgedEnchantmentCount--;
        return entry;
    }

    /// <summary>
    /// Whether this weapon, were <paramref name="first"/> its first enchantment,
    /// would cast: a staff one that applies a status — whom its cast lands on — and a
    /// wand one that is an element, which its area cast carries. The dungeon's own
    /// gates ask the same of a weapon's first entry, the staff's cast
    /// (<c>TurnSystem.CanCastOn</c>) and the wand's (<see cref="TurnSystem.CanCastArea"/>);
    /// a caster that casts nothing can neither cast nor strike, so it never earns
    /// wear again. True for every weapon that is no caster: it casts nothing, and
    /// needs nothing to. What the enchanter asks of what a transfer would leave
    /// (<see cref="Enchanter.TransferRefusal"/>), and what the WEAPONS page asks of a
    /// hand-edited caster.
    /// </summary>
    /// <param name="first">The entry that would be first, or null for none.</param>
    public bool CastsWith(EnchantmentDef? first)
        => !IsCaster || (AreaShape is null ? first?.Applies is not null : first?.DamageType is not (null or DamageType.None));

    /// <summary>
    /// What this item would reserve of a wielder's pool if every entry fitted:
    /// the sum of the attached entries' <see cref="Enchantment.EffectiveLock"/>
    /// (§3.3). It is the item's own figure and knows no wielder, exactly as
    /// <see cref="ResolvedCost"/> does; what is actually taken out of a pool is
    /// <see cref="ActorState.PaidLocks"/>, which stops at the first entry that
    /// does not fit and leaves the rest dormant.
    /// <para>
    /// Derived rather than cached, because a tier is: <see cref="CreditEnchantment"/>
    /// replaces an entry and the deeper lock has to show through the same read.
    /// </para>
    /// </summary>
    public int TotalLock
    {
        get
        {
            int total = 0;
            foreach (var entry in _enchantments)
                total += entry.EffectiveLock;
            return total;
        }
    }

    /// <summary>
    /// The movement a swing or cast costs after Light's discount:
    /// <c>Cost * (100 - Modifiers.Value(Light)) / 100</c>, truncated, and never
    /// below 0 (<see cref="Discounted"/>). Computed once and cached, so the HUD
    /// and the movement gate agree on one number.
    /// </summary>
    public int ResolvedCost { get; private set; }

    /// <summary>The mana a cast costs after Resonant's discount, the same shape against <see cref="ManaCost"/>, and never below 0 either.</summary>
    public int ResolvedManaCost { get; private set; }

    /// <summary>
    /// The one mutator: add <paramref name="n"/> acquired stacks of
    /// <paramref name="t"/>, clamped to forged plus the acquired headroom, and
    /// refresh the resolved costs. Farm depth, grafts and improvements are all
    /// this call, and the relations bind every one of them alike (§1.1: "the
    /// forge, a farm roll, a graft, and a boss theme"): a modifier
    /// <see cref="ModifierRules.Allowed"/> refuses beside what this weapon
    /// already holds — the wrong kind, an exclusion, a missing prerequisite, a
    /// forged-only modifier from zero, a member outside the table — is refused
    /// here too. A source gates its offers on the same predicate before it
    /// rolls (§1.7: the graft roll filters its candidates first), so a refusal
    /// here is a source that skipped its gate: a bug, thrown rather than a
    /// stack granted silently. Deepening what the weapon holds passes while
    /// nothing beside it excludes it, which is how a forged <c>Charges</c>
    /// deepens and a bow without one never gains any.
    /// </summary>
    /// <exception cref="InvalidOperationException">The relations refuse <paramref name="t"/> on this weapon beside its modifiers.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative: nothing is ever removed.</exception>
    public void Acquire(ModifierType t, int n)
    {
        if (!GameContent.Current.Modifiers.Allowed(t, Kind, Modifiers))
            throw new InvalidOperationException(
                $"{Name} ({Id}) cannot hold {t} beside {Modifiers}: the relations bind every source, so an offer is gated on ModifierRules.Allowed before it is made.");
        Modifiers = Modifiers.With(t, n, Forged);
        Resolve();
    }

    /// <summary>
    /// The second mutator, and the only one that touches an entry: credit
    /// <paramref name="xp"/> mana spent to the enchantment at
    /// <paramref name="index"/>, at the INT that was spending it (§3.3). The
    /// entry is a record and the climb is <see cref="Enchantment.WithXp"/>'s, so
    /// this replaces the element rather than mutating one — attachment order,
    /// the entry's identity and everything else about the weapon stay exactly
    /// where they were.
    /// <para>
    /// Every source of enchantment XP comes through here and hands over the same
    /// figure: the mana actually paid. A trigger the wielder paid for is one
    /// (§3.3), and the farm's grant is another — "a plain XP grant instead of a
    /// special case" (§3.5) — so the farm has no path of its own into a tier.
    /// </para>
    /// <para>
    /// The trigger's callers are the five appliers that write a mana record: the
    /// Cast, DamageTaken, DamageDealt, Killed and HealingAboveFull events each
    /// credit the entries their loop attributed a payment to
    /// (<see cref="EnchantmentPayment"/>). A handler never calls this — a handler
    /// never writes — and neither does a behaviour, which has no index.
    /// </para>
    /// </summary>
    /// <param name="index">The entry's position in <see cref="Enchantments"/>.</param>
    /// <param name="xp">Mana that entry paid; never negative.</param>
    /// <param name="stat">The INT that was doing the spending, 1..4; a flat 1 for a thing with no nature.</param>
    /// <exception cref="ArgumentOutOfRangeException">Nothing is attached at <paramref name="index"/>, <paramref name="xp"/> is negative, or <paramref name="stat"/> is outside 1..4.</exception>
    public void CreditEnchantment(int index, int xp, int stat)
    {
        if ((uint)index >= (uint)_enchantments.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{Name} ({Id}) carries {_enchantments.Count} enchantments.");
        _enchantments[index] = _enchantments[index].WithXp(xp, stat);
    }

    public int Stacks(ModifierType t) => Modifiers.Stacks(t);

    /// <summary>
    /// The identity spread <paramref name="def"/> is forged with, plus one forged
    /// stack of <paramref name="graft"/> when there is one — the same operation
    /// the enchanter's graft is (§6.4), asked as forged: the boss "only chooses
    /// which modifier instead of rolling it" (§4.3). No other path grafts a
    /// forged stack, so this is the one place the question is asked of a weapon.
    /// </summary>
    private static ModifierSet Forge(WeaponDef def, ModifierType? graft)
    {
        var spread = ModifierSet.Of(def.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());
        if (graft is not { } theme)
            return spread;

        if (!GameContent.Current.Modifiers.CanGraft(theme, GameContent.Current.Weapons.KindOf(def.Class), spread, spread, asForged: true))
            throw new ArgumentException(
                $"'{def.Id}' cannot be forged with {theme} beside {spread}: every graft asks ModifierRules.CanGraft first, and a forged one the forge limit too.",
                "forgedGraft");
        return ModifierSet.Of([.. spread.Entries, (theme, 1)]);
    }

    private void Resolve()
    {
        ResolvedCost = Discounted(Cost, ModifierType.Light);
        ResolvedManaCost = Discounted(ManaCost, ModifierType.Resonant);
    }

    /// <summary>
    /// <paramref name="cost"/> less <paramref name="discount"/>'s percentage off
    /// it, truncated. A discount takes at most the whole cost, so the result is
    /// never below 0. The shipped numbers peak at 60 percent (10 a stack, x6).
    /// A <c>tuning.json</c> can go well past 100: <c>PerStack.Resonant</c> 20 is
    /// 120 percent off at x6, and <c>AcquiredHeadroom</c> 10 lets Light reach x11,
    /// 110 percent at the shipped 10 a stack. Past 100 percent the cost is 0,
    /// never a negative number on the weapon line. A swing still costs
    /// <see cref="Progression.MinimumMovementCost"/> where it is paid, and a
    /// trigger still costs 1 (<see cref="Enchantment.ResolvedTriggerCost"/>).
    /// </summary>
    private int Discounted(int cost, ModifierType discount)
        => cost * Math.Max(0, Percent - Modifiers.Value(discount)) / Percent;

    public override string ToString() => $"{Name} ({Id}: {Modifiers})";
}
