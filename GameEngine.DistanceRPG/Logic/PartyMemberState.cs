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
    /// starting weapon is slot 0 and the bag follows it in file order, each
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

    /// <summary>Weapon slots a member carries. A roster entry's starting weapon and bag are checked against it at load, so a bag too deep to spawn aborts startup.</summary>
    public const int InventorySlots = 3;

    /// <summary>Three slots; slot 0 is the equipped weapon.</summary>
    public Weapon?[] Inventory { get; } = new Weapon?[InventorySlots];

    public override Weapon? EquippedWeapon => Inventory[0];

    /// <summary>Movement budget left this turn, in logic units.</summary>
    public float DistLeft { get; set; } = GameConstants.MaxDistance;

    /// <summary>This turn's cap: base budget plus movement saved last turn, less what Mire cut.</summary>
    public float EffectiveMax { get; set; } = GameConstants.MaxDistance;

    /// <summary>Banked at end of turn (half the unspent budget, capped).</summary>
    public float SavedMovement { get; set; }

    public override float Radius => GameConstants.PlayerHalf;

    /// <summary>
    /// Start-of-turn reset: cash in the saved movement bonus, refill the budget,
    /// then take Mire's cut off the whole of it — 10% a level, and at ten
    /// levels the budget is gone, which is all paralysis is (§1.5).
    /// </summary>
    public void StartTurn()
    {
        float bonus = SavedMovement;
        SavedMovement = 0;
        EffectiveMax = StatusBehaviours.MiredBudget(this, GameConstants.MaxDistance + bonus);
        DistLeft = EffectiveMax;
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
        float save = Math.Min(MathF.Floor(DistLeft / 2f), GameConstants.MaxDistance / 2f);
        SavedMovement = save;
        return save;
    }
}
