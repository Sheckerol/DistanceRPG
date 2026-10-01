namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay state of one party member, ported from the prototype's per-char
/// object. Position is the circle centre in logic space (pixels, y-down).
/// Position, HP, radius and status effects are the shared <see cref="ActorState"/>.
/// <para>
/// Progression lives here and not on the base (§2.1): a member carries an
/// innate spread and three cumulative XP numbers, and every other progression
/// figure — points, maxima, levels — is replayed from those numbers on read. An
/// actor with no pools is not a member with empty ones; it simply has none,
/// which is why nothing in the damage pipeline asks an actor for a stat.
/// </para>
/// </summary>
public sealed class PartyMemberState : ActorState
{
    public required string Id { get; init; }
    public required int ColorIndex { get; init; }

    /// <summary>
    /// The member a roster entry describes (§2.1, §5.9), and the one place a
    /// <see cref="PartyMemberDef"/> becomes one — so the scene that spawns the
    /// party holds no construction rule of its own and an edit to the roster is
    /// an edit to what the player picks up.
    /// <para>
    /// The spread comes off the def, which is the whole point: a member built
    /// any other way carries <see cref="InnateStats.None"/> and divides every
    /// threshold by 1, so the §2.1 table would exist and do nothing. The
    /// starting weapon is slot 0 and the bag follows it in the def's order, each
    /// instantiated fresh — two members carrying the same id carry two weapons.
    /// </para>
    /// <para>
    /// The def is trusted here because <see cref="ContentValidator.ValidateParty"/>
    /// has already refused an unknown weapon id, a bag deeper than
    /// <see cref="InventorySlots"/> and a spread that is not a permutation: a
    /// broken roster is a startup failure, never half a party.
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">A weapon id that never went through the validator.</exception>
    public static PartyMemberState From(PartyMemberDef def, int colorIndex, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(def);

        var member = new PartyMemberState
        {
            Id = def.Id,
            ColorIndex = colorIndex,
            Stats = def.Stats,
            X = x,
            Y = y,
        };

        var catalogue = GameContent.Current.Weapons;
        member.Inventory[0] = catalogue.Instantiate(def.StartingWeaponId);
        for (int i = 0; i < def.BagWeaponIds.Count; i++)
            member.Inventory[i + 1] = catalogue.Instantiate(def.BagWeaponIds[i]);

        return member;
    }

    /// <summary>
    /// A member as a save describes it (§5.1): its id, its colour, the spread it
    /// was built with and the bag it was built with — <paramref name="slots"/>
    /// of them, whatever the current tuning's carry would give a member built
    /// now, since a member keeps the count it was built with and a changed
    /// <c>tuning.json</c> never resizes a bag. Everything else — the XP, the body,
    /// what the bag holds, the turn's movement — the load writes back after, as
    /// plain state. The one construction a load makes, beside
    /// <see cref="From"/>'s for a new campaign; internal, so a restore is never a
    /// public way to set a spread.
    /// <para>
    /// The spread is taken as saved and never asked the §2.1 permutation rule,
    /// which is the roster's: a member's spread is fixed at creation, and
    /// this is the same member coming back.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slots"/> is not a count a member can carry: at least the hand, at most <see cref="GameConstants.MaxInventorySlots"/>.</exception>
    internal static PartyMemberState Restore(string id, int colorIndex, InnateStats stats, int slots)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(slots, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(slots, GameConstants.MaxInventorySlots);

        return new PartyMemberState
        {
            Id = id,
            ColorIndex = colorIndex,
            Stats = stats,
            Inventory = new IItem?[slots],
        };
    }

    /// <summary>
    /// The permanent spread (§2.1): a permutation of 1-4, fixed at creation from
    /// the roster entry and never raised — no mechanic in any phase writes it,
    /// which is why it is <c>init</c>-only rather than merely left alone. A
    /// member built without one carries <see cref="InnateStats.None"/>, the
    /// neutral spread that divides every bar by 1.
    /// </summary>
    public InnateStats Stats { get; init; } = InnateStats.None;

    /// <summary>
    /// Weapon XP per class, for this member (§2.2). Keyed on the class, so any
    /// dagger this member picks up wields at their dagger level and loot never
    /// resets progress; per member, so one character's practice is never
    /// another's.
    /// </summary>
    public WeaponXpBook WeaponXp { get; } = new();

    /// <summary>Cumulative HP XP: the HP actually restored to this member. Plain and settable, because Phase 4's death rollback and Phase 5's save write it back.</summary>
    public int HpXp { get; set; }

    /// <summary>Cumulative mana XP: the mana this member actually spent. Plain and settable, for the same two reasons.</summary>
    public int ManaXp { get; set; }

    /// <summary>Where the health pool stands, replayed from <see cref="HpXp"/> against CON (§2.1). Derived on read and never cached: the starting bar is content, and a test may swap it.</summary>
    public PoolProgress HealthPool => Progression.Pool(HpXp, Stats.CON);

    /// <summary>Where the mana pool stands, replayed from <see cref="ManaXp"/> against INT.</summary>
    public PoolProgress ManaPool => Progression.Pool(ManaXp, Stats.INT);

    /// <summary>
    /// Max HP: the starting pool plus the points earned (§2.1), computed rather
    /// than the constant it used to be. Nobody starts with more of anything — a
    /// CON 1 wizard and a CON 4 fighter both open the game at
    /// <see cref="Tuning.StartingPool"/> and diverge only through growth, since
    /// the stat divides the threshold and never the value.
    /// </summary>
    public override int MaxHp => HealthPool.Max;

    /// <summary>Max mana, the same arithmetic against INT. The pool an actor with no progression carries is <see cref="GameConstants.MaxMana"/>; a member's is this.</summary>
    public override int MaxMana => ManaPool.Max;

    /// <summary>
    /// Where this member's ladder in <paramref name="cls"/> stands: the level
    /// they wield it at and how far into the next, replayed from the raw XP
    /// against the class's governing stat (§2.2). The whole progression figure,
    /// so a readout states it and computes nothing of its own.
    /// </summary>
    public LevelProgress Proficiency(WeaponClass cls)
        => Progression.Ladder(WeaponXp[cls], Progression.GoverningStat(cls, Stats));

    /// <summary>The level this member wields <paramref name="weapon"/> at: their ladder in its class, replayed from the raw XP against the class's governing stat (§2.2).</summary>
    public override int WeaponLevel(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return Proficiency(weapon.Class).Level;
    }

    /// <summary>
    /// Add a credit's raw XP to the pool it names and return the points — or,
    /// for a class, the levels — it bought. The one write path for progression:
    /// the appliers that credit XP (§2.2) state what happened, and nothing else
    /// decides what it was worth. The amount is used as it arrives; the stat is
    /// already in the threshold.
    /// <para>
    /// <strong>A gained HP point arrives filled; a gained mana point does not.</strong>
    /// Constitution grows by getting hurt and then healed, and the HP loop's only
    /// filler is a second healing event, so a point that arrived empty would end
    /// every fight one short of the new ceiling. Mana is credited by
    /// <em>spending</em>, so filling a gained point would hand back part of the
    /// cast that earned it — and mana has a filler that needs no second event
    /// (<see cref="ActorState.RegenManaFromUnusedMovement"/>), so an empty point
    /// costs nothing but a turn's regen. A pool nobody has written reads as its
    /// own ceiling, so the mana branch pins what the pool held before the bar
    /// moves and the rule holds whether or not anything has been spent yet. A
    /// weapon level fills nothing: a ladder has no pool to be full of.
    /// </para>
    /// </summary>
    /// <returns>Points or levels gained, 0 for a credit that crossed no bar.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative: XP is credited, never taken back.</exception>
    /// <exception cref="ArgumentException">A weapon credit names no class, or a pool credit names one.</exception>
    public int Credit(XpCredit credit)
    {
        if (credit.Amount < 0)
            throw new ArgumentOutOfRangeException(nameof(credit), credit.Amount, "XP is credited, never taken back: a refund is not a negative credit.");

        switch (credit.Pool)
        {
            case XpPool.Weapon:
            {
                if (credit.Class is not { } cls)
                    throw new ArgumentException("A weapon credit levels a class; name the class the weapon belongs to.", nameof(credit));
                int stat = Progression.GoverningStat(cls, Stats);
                int before = Progression.Ladder(WeaponXp[cls], stat).Level;
                WeaponXp[cls] += credit.Amount;
                return Progression.Ladder(WeaponXp[cls], stat).Level - before;
            }

            case XpPool.Health:
            {
                RequireNoClass(credit);
                int before = MaxHp;
                int hp = Hp;               // read against the bar before it moves: HP nobody has written is full, and full is what it stays
                HpXp += credit.Amount;
                int gained = MaxHp - before;
                if (gained > 0)
                    Hp = hp + gained;      // the point arrives filled
                return gained;
            }

            case XpPool.Mana:
            {
                RequireNoClass(credit);
                int before = MaxMana;
                int mana = Mana;           // read against the bar before it moves: a pool nobody has written reads as its ceiling, and would otherwise rise with it
                ManaXp += credit.Amount;
                int gained = MaxMana - before;
                if (gained > 0)
                    Mana = mana;           // the ceiling rises; what is in the pool does not
                return gained;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(credit), credit.Pool, "Not an XpPool.");
        }
    }

    /// <summary>The two pools that are the member's own level no class: a credit that names one is describing something else.</summary>
    private static void RequireNoClass(XpCredit credit)
    {
        if (credit.Class is not null)
            throw new ArgumentException($"{credit.Pool} is the member's own pool and levels no class; it named {credit.Class}.", nameof(credit));
    }

    /// <summary>
    /// Weapon slots a member carries: the party's carry
    /// (<see cref="Tuning.PartyCarrySlots"/>, §4.4's 24) shared evenly among its
    /// <see cref="GameConstants.PartySize"/> members — six each, replacing the
    /// three that were a testing remnant. Slot 0 is the weapon in hand; the rest
    /// are the bag, and loot and weapons share them — there is no haul bag, so
    /// every drop carried out is a weapon that could have been swung.
    /// <para>
    /// Read from the current content when a member is built, so a member keeps
    /// the count it was built with. A roster entry's starting weapon and bag are
    /// checked against the same number at load, so a bag too deep to spawn
    /// aborts startup.
    /// </para>
    /// </summary>
    public static int InventorySlots => SlotsFor(GameContent.Current.Tuning);

    /// <summary>
    /// The slots a member carries under <paramref name="tuning"/>: its carry over
    /// the party's size. <see cref="ContentValidator.ValidateTuning"/> refuses a
    /// carry that does not divide evenly, a slot or more each, so nothing is lost
    /// to the division, and one that shares out past
    /// <see cref="GameConstants.MaxInventorySlots"/>, so every slot has a key.
    /// Content being loaded passes its own tuning, since the content
    /// <see cref="InventorySlots"/> reads is not current yet.
    /// </summary>
    internal static int SlotsFor(Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        return tuning.PartyCarrySlots / GameConstants.PartySize;
    }

    /// <summary>
    /// The member's <see cref="InventorySlots"/> slots, six under the shipped
    /// tuning; slot 0 is the equipped weapon. The array is the member's for good:
    /// only construction sizes it — a restore (<see cref="Restore"/>) to the count
    /// the saved member was built with — and every write after is to a slot.
    /// <para>
    /// <strong>A slot holds a weapon or a potion</strong> (<see cref="IItem"/>,
    /// §3.4), one thing or nothing. The hand holds a weapon by rule (PHASE 6a
    /// DECISION 7); a potion lies in the bag, slots 1 and up (PHASE 6b DECISION 1).
    /// So a reader that wants a weapon asks for one (<see cref="WeaponIn"/>), and a
    /// count of what a member carries counts weapons
    /// (<see cref="WeaponsCarried"/>): a potion's slot is full, never room, and
    /// never a weapon.
    /// </para>
    /// </summary>
    public IItem?[] Inventory { get; private init; } = new IItem?[InventorySlots];

    /// <summary>The weapon in hand. Slot 0 holds a weapon or nothing — nothing ever puts a potion there — so a potion is never read as one.</summary>
    public override Weapon? EquippedWeapon => Inventory[0] as Weapon;

    /// <summary>The weapon in <paramref name="slot"/>, or null when the slot is empty or holds a potion.</summary>
    public Weapon? WeaponIn(int slot) => Inventory[slot] as Weapon;

    /// <summary>The potion in <paramref name="slot"/>, or null when the slot is empty or holds a weapon.</summary>
    public Potion? PotionIn(int slot) => Inventory[slot] as Potion;

    /// <summary>How many weapons the member carries, the hand's and the bag's: what every rule that keeps a member armed counts (PHASE 6a DECISION 7). A potion is never one.</summary>
    public int WeaponsCarried => Inventory.Count(item => item is Weapon);

    /// <summary>The potions in the bag, each with its slot, in slot order.</summary>
    public IEnumerable<(int Slot, Potion Potion)> Potions
    {
        get
        {
            for (int slot = 1; slot < Inventory.Length; slot++)
                if (Inventory[slot] is Potion potion)
                    yield return (slot, potion);
        }
    }

    /// <summary>
    /// Empty bag slot <paramref name="slot"/>, which holds a potion, and hand the
    /// potion back: <strong>the one way a potion leaves a bag</strong>. Its callers
    /// are pinned (<c>PotionSlotTests.APotionLeavesABagThroughOneDoor</c>): the
    /// pour-out in a dungeon (<c>DungeonScene.LeaveSlot</c>, PHASE 6b DECISION 1),
    /// the drink (<see cref="TurnSystem.TryDrink"/>, Phase 6b.2), the run's end,
    /// which spoils every potion carried (<see cref="Farm.Spoil"/>, Phase 6b.3), and
    /// from Phase 6b's next step a pour-back at the alchemist. A potion is never put
    /// down on a floor, moved to the stable or carried into another bag.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slot"/> is the hand (0), which keeps a weapon, or no slot the member carries.</exception>
    /// <exception cref="ArgumentException">The slot holds no potion: it is empty or holds a weapon.</exception>
    internal Potion TakePotion(int slot)
    {
        if (slot < 1 || slot >= Inventory.Length)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, slot == 0
                ? "Slot 0 is the hand, which keeps a weapon: a potion lies in the bag, slot 1 and up."
                : $"Member '{Id}' carries slots 0 to {Inventory.Length - 1}, the hand and its bag: there is no slot {slot}.");
        if (Inventory[slot] is not Potion potion)
            throw new ArgumentException(
                $"Slot {slot} of member '{Id}' holds {(Inventory[slot] is Weapon weapon ? weapon.Name : "nothing")}, not a potion: only a potion leaves a bag this way.",
                nameof(slot));
        Inventory[slot] = null;
        return potion;
    }

    /// <summary>
    /// The base movement a turn refills (§1.2): <see cref="Tuning.MovementBudget"/>
    /// under the current content, read on every use and never cached, so a
    /// member built after a tuning is loaded moves by that tuning's number.
    /// </summary>
    private static float Budget => GameContent.Current.Tuning.MovementBudget;

    /// <summary>Movement budget left this turn, in logic units.</summary>
    public float DistLeft { get; set; } = Budget;

    /// <summary>This turn's cap: base budget plus movement saved last turn, less what Mire cut.</summary>
    public float EffectiveMax { get; set; } = Budget;

    /// <summary>Banked at end of turn (half the unspent budget, capped).</summary>
    public float SavedMovement { get; set; }

    /// <summary>
    /// The whole this turn's cap was cut from: the base budget plus the bank
    /// <see cref="StartTurn"/> cashed in, before Mire took its share — the budget
    /// alone after a <see cref="Rest"/>, and for a member no turn has started for.
    /// What a Mire legend states beside the MOVE readout's cap (§1.5: 10% a level
    /// <em>of this</em>). Kept on the member and not beside it, because the bank
    /// it holds is spent by the time anyone reads it, and a save carries it
    /// (§5.1) so a reload reads the same whole.
    /// </summary>
    public float BudgetBeforeMire { get; set; } = Budget;

    public override float Radius => GameConstants.PlayerHalf;

    /// <summary>
    /// Start-of-turn reset: cash in the saved movement bonus, refill the budget,
    /// then take Mire's cut off the whole of it — 10% a level, and at ten
    /// levels the budget is gone, which is all paralysis is (§1.5). The whole it
    /// cut from is kept (<see cref="BudgetBeforeMire"/>).
    /// </summary>
    public void StartTurn()
    {
        float bonus = SavedMovement;
        SavedMovement = 0;
        BudgetBeforeMire = Budget + bonus;
        EffectiveMax = StatusBehaviours.MiredBudget(this, BudgetBeforeMire);
        DistLeft = EffectiveMax;
    }

    /// <summary>
    /// The rest before a dungeon (§4.1): the member walks in whole — alive, HP
    /// full, mana full to the spendable ceiling (<see cref="ActorState.UsableMaxMana"/>,
    /// the pool a spend reads, §3.3), every status shed, and the turn's budget
    /// as a turn with nothing banked starts it: the base movement, no saved
    /// bonus, no shot held. The permanent half is untouched — the weapons
    /// carried, the XP earned, the innate spread — because a rest is between
    /// runs and never a reset of the campaign.
    /// <para>
    /// <strong>And armed, if there is anything to arm it with</strong>
    /// (<see cref="ArmFromBag"/>). A wipe takes the run's spoils out of the bags
    /// (§4.4), and a spoil the member had taken up in hand leaves the hand empty;
    /// so an empty hand takes the first weapon carried in the bag before the
    /// member walks into the next dungeon.
    /// </para>
    /// <para>
    /// Taken at the next entry, not when a run ends, so the run's end still
    /// shows the fallen as fallen.
    /// </para>
    /// </summary>
    public void Rest()
    {
        ArmFromBag();

        Alive = true;
        StatusEffects.Clear();
        Hp = MaxHp;
        Mana = UsableMaxMana;
        HeldShots = 0;
        SavedMovement = 0;
        BudgetBeforeMire = Budget;
        EffectiveMax = Budget;
        DistLeft = Budget;
    }

    /// <summary>
    /// An empty hand takes the first weapon carried in the bag, moved rather than
    /// copied: what the rest before a dungeon does (<see cref="Rest"/>), and what a
    /// move between runs does to the hand it empties (PHASE 6a DECISION 7,
    /// <see cref="CampaignState.Move"/>). A hand already holding something is left
    /// as it is, and a member carrying no weapon stays unarmed. A potion is passed
    /// over wherever it lies: the hand keeps a weapon (PHASE 6b DECISION 1).
    /// </summary>
    /// <returns>The weapon taken into the hand, or null when none was.</returns>
    internal Weapon? ArmFromBag()
    {
        if (Inventory[0] is not null)
            return null;
        for (int slot = 1; slot < Inventory.Length; slot++)
        {
            if (Inventory[slot] is not Weapon carried) continue;
            Inventory[0] = carried;
            Inventory[slot] = null;
            return carried;
        }
        return null;
    }

    /// <summary>
    /// End-of-turn banking: save half the unspent budget (capped at half the
    /// base). Returns the amount saved.
    /// </summary>
    public float EndTurnSaveMovement()
    {
        if (!Alive)
        {
            SavedMovement = 0;
            return 0;
        }
        float save = Math.Min(MathF.Floor(DistLeft / 2f), Budget / 2f);
        SavedMovement = save;
        return save;
    }
}
