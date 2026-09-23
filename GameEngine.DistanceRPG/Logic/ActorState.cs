namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What every actor on the map shares, party member or enemy: a position (the
/// circle centre in logic space — pixels, y-down, 32 per tile), hit points, a
/// reach radius, the weapon it fights with, and the status effects it carries.
/// Status effects live here so a buff or debuff can land on either side through
/// one mechanism, and the turn system's attack resolver, status ticks and threat
/// zones are written against this type rather than against a side.
/// </summary>
public abstract class ActorState
{
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>
    /// Current hit points; full at spawn — HP nobody has written reads as this
    /// actor's own <see cref="MaxHp"/>, the same shape <see cref="Mana"/> has.
    /// A kind whose maximum is computed from its own state (a party member's
    /// earned points, §2.1) therefore spawns at whatever that computes to, with
    /// no construction-order problem: the maximum is asked for when it is
    /// wanted, not copied at the moment the actor is made.
    /// </summary>
    public int Hp
    {
        get => _hp ?? MaxHp;
        set => _hp = value;
    }

    private int? _hp;

    /// <summary>Full hit points for this kind of actor.</summary>
    public abstract int MaxHp { get; }

    public bool Alive { get; set; } = true;

    /// <summary>Collision and reach radius, in logic units.</summary>
    public abstract float Radius { get; }

    /// <summary>The weapon this actor fights with right now; null when unarmed.</summary>
    public abstract Weapon? EquippedWeapon { get; }

    /// <summary>
    /// Modifiers the actor carries whatever it is holding — the golem's Block
    /// (§4.3). Added to the weapon's at resolution time, never to any weapon's
    /// cap: a weapon's ceiling comes from its own forged spread alone.
    /// </summary>
    public ModifierSet Innate { get; set; } = ModifierSet.Empty;

    /// <summary>Stacks of <paramref name="t"/> in play for this actor: the equipped weapon's plus the innate ones.</summary>
    public int Stacks(ModifierType t) => (EquippedWeapon?.Modifiers.Stacks(t) ?? 0) + Innate.Stacks(t);

    /// <summary>
    /// The §1.1 value of <paramref name="t"/> for this actor. Weapon and innate
    /// stacks resolve together, so a modifier's offset applies once.
    /// </summary>
    public int Value(ModifierType t) => GameContent.Current.Modifiers.Resolve(t, Stacks(t));

    /// <summary>
    /// The level this actor wields <paramref name="weapon"/> at: its proficiency
    /// in that weapon's class (§2.2). Declared here, and virtual, so a rule that
    /// reads a wielder's level — the damage bonus and the movement discount —
    /// asks whoever is holding the thing rather than asking what kind of actor
    /// it is.
    /// <para>
    /// <strong>Serrated does not read this.</strong> settled.md carried two
    /// formulas for its magnitude — an earlier
    /// <c>BleedPercent × (base damage + proficiency level)</c> and a later
    /// "Serrated reads the weapon's share too" — and the later one is the rule
    /// (recorded under the Phase 2 implementation pass). It loses nothing by it:
    /// the weapon's share of what was dealt already carries this level, because
    /// the damage bonus is part of the weapon's own damage before anything else
    /// touches it.
    /// </para>
    /// <para>
    /// The base is <see cref="Progression.StartingLevel"/>, whatever is held:
    /// proficiency is a party member's, and an actor with no pools wields
    /// everything at the level a class nobody has trained is wielded at. Both
    /// level effects are no-ops there, so an enemy swings for exactly its
    /// weapon's own numbers.
    /// </para>
    /// </summary>
    public virtual int WeaponLevel(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return Progression.StartingLevel;
    }

    /// <summary>
    /// Damage this actor adds to whatever it is holding, however it came by it:
    /// a farmed dummy's accumulated revival bonus (§3.2). Zero for an actor with
    /// no such history, which is everyone else.
    /// <para>
    /// <strong>It lands on the actor and never on the weapon</strong> — "a dummy
    /// hitting for +15 does not drop a weapon with +15 on it; drop quality comes
    /// from the ladder above and nothing else" (§3.2). Otherwise farming would
    /// pay twice for one investment, and the bonus would leak into a modifier
    /// system with no place to put it. Declared here and read in exactly one
    /// place, <see cref="CombatRules.BaseDamage"/>, which is the one place a
    /// wielder's base damage is computed: a bonus added at a swing's step 1
    /// alone would be silently dropped by Longshot's retake and by a wand's
    /// cast, both of which re-derive the base through that same call.
    /// </para>
    /// </summary>
    public virtual int BonusDamage => 0;

    /// <summary>
    /// The movement one swing or cast of <paramref name="weapon"/> costs this
    /// actor: the weapon's own resolved cost — Light's discount already in it —
    /// less the wielder's proficiency discount, never below
    /// <see cref="Progression.MinimumMovementCost"/> (§2.2).
    /// <para>
    /// The discount is the wielder's and the cost is the weapon's, so the two
    /// meet here and never on <see cref="Weapon.ResolvedCost"/>, which knows no
    /// wielder and reads the same for an item in the bag as for one in hand.
    /// Every gate and every charge in the turn system asks this, so what a swing
    /// is refused for and what it is billed are one number.
    /// </para>
    /// </summary>
    public int MovementCost(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return Progression.MovementCost(weapon.ResolvedCost, WeaponLevel(weapon));
    }

    /// <summary>
    /// The damage type this actor is attuned to, or null: what a typed hit
    /// resolves against on the chart (§1.4) — the same type halved, the
    /// opposed one x1.5, anything else unchanged. Attunement is the dungeon's,
    /// not the individual's: a themed floor (§4.3, Phase 4) sets it on every
    /// enemy it spawns and an unthemed one on none, and a party member never
    /// carries one. Declared here so the chart's handler reads it off whoever
    /// was hit without asking what kind of actor that is.
    /// </summary>
    public DamageType? Attunement { get; set; }

    /// <summary>
    /// Mana pool for casting. Every trigger costs mana on both sides, so an
    /// enemy caster carries one too; full at spawn — a pool nobody has written
    /// reads as this actor's own <see cref="UsableMaxMana"/>, so a kind that
    /// overrides the maximum spawns full as well.
    /// <para>
    /// Full is the <em>spendable</em> ceiling and not the earned one (§3.3): a
    /// wielder whose equipped entries reserve part of the pool never holds mana
    /// it could not spend, so the very first cast of a freshly spawned caster
    /// pays out of the same remainder every later one does.
    /// </para>
    /// </summary>
    public int Mana
    {
        get => _mana ?? UsableMaxMana;
        set => _mana = value;
    }

    private int? _mana;

    /// <summary>
    /// The pool this actor has earned (§2.2): what XP grows and what a save
    /// stores. It is <em>not</em> what a spend reads — an equipped enchantment
    /// reserves part of it (<see cref="PaidLocks"/>) and
    /// <see cref="UsableMaxMana"/> is the remainder.
    /// </summary>
    public virtual int MaxMana => GameConstants.MaxMana;

    /// <summary>
    /// Mana the equipped weapon's enchantments actually reserve (§3.3): the
    /// entries are walked in attachment order and each lock is paid while it
    /// fits, so the sum can never exceed <see cref="MaxMana"/> — which is the
    /// whole of "the sum of equipped locks may not exceed max mana", enforced by
    /// dormancy rather than by refusing to equip.
    /// <para>
    /// <strong>Locks are the wielder's, whoever the wielder is</strong>, so they
    /// live here and not on <see cref="PartyMemberState"/>: an enemy healer
    /// carrying a lock-15 staff has 85 usable of its flat 100, and no handler
    /// asks an actor its kind. Nothing is stored — the walk is redone on read,
    /// so growing the pool, crediting a tier or swapping the weapon re-prices
    /// the reservation with no event to subscribe to (§3.5).
    /// </para>
    /// </summary>
    public int PaidLocks => Locks().Paid;

    /// <summary>
    /// What every spend and every regen reads: <c>MaxMana − Σ paid locks</c>
    /// (§3.5). Unequipping returns the ceiling in full and refunds no points,
    /// because nothing was taken from the pool — only reserved out of it.
    /// </summary>
    public int UsableMaxMana => Math.Max(0, MaxMana - PaidLocks);

    /// <summary>
    /// Whether the entry at <paramref name="index"/> of the equipped weapon's
    /// list is asleep: its lock went unpaid, so it does not fire and pays
    /// nothing — the same non-event as a trigger this actor cannot afford
    /// (§3.1). Derived, never stored: the same shared, immutable
    /// <see cref="Enchantment"/> is dormant in one wielder's hands and awake in
    /// another's, and wakes on its own the moment the pool grows to cover it.
    /// <para>
    /// The remainder after the first unaffordable entry sleeps <em>even if a
    /// later one would fit</em> — the opposite of partial firing, which does
    /// pass the remainder down the list. A lock is a reservation the whole list
    /// competes for once; a trigger is a purchase each entry makes in turn.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Nothing is equipped, or nothing is attached at <paramref name="index"/>.</exception>
    public bool IsDormant(int index)
    {
        var weapon = EquippedWeapon;
        if (weapon == null || (uint)index >= (uint)weapon.Enchantments.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"The equipped weapon carries {weapon?.Enchantments.Count ?? 0} enchantments.");
        return index >= Locks().Awake;
    }

    /// <summary>
    /// The one walk both members read: pay each equipped entry's
    /// <see cref="Enchantment.EffectiveLock"/> in attachment order while the
    /// earned pool covers it, and stop at the first that does not fit. Returns
    /// what was reserved and how many entries, from the front, are awake.
    /// </summary>
    private (int Paid, int Awake) Locks()
    {
        var weapon = EquippedWeapon;
        if (weapon == null) return (0, 0);

        int budget = MaxMana;
        int paid = 0;
        var entries = weapon.Enchantments;
        for (int i = 0; i < entries.Count; i++)
        {
            int reserved = entries[i].EffectiveLock;
            if (paid + reserved > budget) return (paid, i);
            paid += reserved;
        }
        return (paid, entries.Count);
    }

    /// <summary>
    /// Convert movement left unspent at the end of the actor's action into
    /// mana, the only route mana comes back by (§1.3): every
    /// <see cref="Tuning.MovementUnitsPerMana"/> units bank one point, the
    /// remainder is lost, and the pool never passes
    /// <see cref="UsableMaxMana"/> — the spendable ceiling, not the earned one,
    /// so movement is never converted into mana the locks have already reserved
    /// (§3.3). A party member banks what is left of its budget at its turn's
    /// end, an enemy what its action left; casting spends movement, so it eats
    /// into this the same way walking does: the more you cast, the less mana you
    /// can spend. Returns the mana actually regained; nothing for the dead.
    /// </summary>
    public int RegenManaFromUnusedMovement(float unspentMovement)
    {
        int ceiling = UsableMaxMana;
        if (!Alive || Mana >= ceiling) return 0;
        int unitsPerMana = Math.Max(1, GameContent.Current.Tuning.MovementUnitsPerMana);
        int regained = (int)(MathF.Max(0f, unspentMovement) / unitsPerMana);
        int before = Mana;
        Mana = Math.Min(ceiling, Mana + regained);
        return Mana - before;
    }

    /// <summary>
    /// Overwatch's banked shots: holding fire this turn arms the actor's
    /// Overwatch value here, which makes its ranged reach a threat zone until
    /// the turn ends — a target entering it is shot for free, one shot per
    /// held stack. Counts down as they fire, so it reads as the shots still
    /// held: zero when nothing is, and cleared when the next player turn
    /// starts or the weapon that held them is swapped away.
    /// </summary>
    public int HeldShots { get; set; }

    /// <summary>
    /// The statuses on this actor: one immutable <see cref="StatusEffect"/> per
    /// (Type, Element), in the order they first landed, each replaced whole
    /// when its levels change. Read freely; written through
    /// <see cref="ApplyStatus"/> and <see cref="AdjustStatus"/>.
    /// </summary>
    public List<StatusEffect> StatusEffects { get; } = new();

    /// <summary>
    /// Land <paramref name="levels"/> of a status: re-application accumulates
    /// onto the entry already there rather than refreshing anything, because
    /// levels are magnitude and duration at once (§1.5, §1.6). The applier
    /// decided how many; this only adds them. Returns the entry now on the actor.
    /// </summary>
    public StatusEffect ApplyStatus(StatusEffectType type, DamageType? element, int levels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(levels, 1);
        return AdjustStatus(type, element, levels)!;
    }

    /// <summary>
    /// Change the levels of (<paramref name="type"/>, <paramref name="element"/>)
    /// by <paramref name="delta"/> — positive accumulates, negative is a tick's
    /// decrement, a decay, a spend or a reset — and drop the entry once it
    /// reaches zero: a status at level 0 is removed, never kept empty. Returns
    /// the entry now on the actor, or null when it is gone. The one write path.
    /// </summary>
    public StatusEffect? AdjustStatus(StatusEffectType type, DamageType? element, int delta)
    {
        if (StatusRules.KeysOnElement(type))
        {
            if (element is null or DamageType.None)
                throw new ArgumentException($"{type} is keyed on the element that lit it; give one.", nameof(element));
        }
        else if (element != null)
        {
            throw new ArgumentException($"{type} carries no element.", nameof(element));
        }

        int i = StatusEffects.FindIndex(e => e.Type == type && e.Element == element);
        int levels = (i >= 0 ? StatusEffects[i].Levels : 0) + delta;
        if (levels <= 0)
        {
            if (i >= 0) StatusEffects.RemoveAt(i);
            return null;
        }

        var entry = new StatusEffect(type, element, levels);
        if (i >= 0) StatusEffects[i] = entry;
        else StatusEffects.Add(entry);
        return entry;
    }

    /// <summary>
    /// Levels of <paramref name="type"/> on the actor, 0 if absent. With an
    /// <paramref name="element"/>, that entry alone; without one, every entry of
    /// the type together — which for an element-keyed status is the sum of its
    /// burns, and for any other is its one entry.
    /// </summary>
    public int StatusLevel(StatusEffectType type, DamageType? element = null)
        => element is null
            ? StatusEffects.Where(e => e.Type == type).Sum(e => e.Levels)
            : StatusEffects.FirstOrDefault(e => e.Type == type && e.Element == element)?.Levels ?? 0;
}
