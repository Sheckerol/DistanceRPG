namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay state of an enemy: the training dummy ported from the prototype,
/// and — built to its dungeon's statline, with a name, a movement budget and
/// modifiers of its own (<see cref="DungeonState.BossRow"/>) — the boss waiting
/// on a dungeon's bottom floor (§4.3), which is a row like any dummy's in every
/// other respect. Position is the circle centre in logic space.
/// </summary>
public sealed class EnemyState : ActorState
{
    /// <summary>What a dummy holds unless told otherwise: the Efficiency sword — Block x1, Push x1, Light x1.</summary>
    public const string DefaultWeaponId = "arming_sword";

    /// <summary>
    /// An enemy's HP is its base (<see cref="BaseMaxHp"/> — a dummy's flat
    /// constant, a boss's statline) plus whatever its revivals have added
    /// (<see cref="RevivalMaxHp"/>, §3.2), and it spawns full: unwritten HP reads
    /// as this (<see cref="ActorState.Hp"/>), so no constructor copies it.
    /// <para>
    /// It is also the bar a clean kill has to clear, which is what puts the
    /// clean-kill bonus out of business by the most direct route available:
    /// every cycle that rolls Health lifts the very threshold the swing must
    /// beat, so a dummy you could clean-kill at <see cref="DefeatCount"/> 2 is
    /// one you cannot at 10.
    /// </para>
    /// </summary>
    public override int MaxHp => BaseMaxHp + RevivalMaxHp;

    /// <summary>
    /// The HP this enemy has before any revival adds to it: the flat
    /// <see cref="GameConstants.DummyHp"/> for rank and file, and its dungeon's
    /// statline for a boss (§5.8 — the golem's 80, <see cref="DungeonState.BossRow"/>).
    /// Settable like every field a row is restored from, though a save need not
    /// store a boss's: it re-derives from content (§5.1).
    /// </summary>
    public int BaseMaxHp { get; set; } = GameConstants.DummyHp;

    /// <summary>
    /// How far this enemy moves in a turn, in logic units (32 per tile): the
    /// standard <see cref="GameConstants.EnemyMove"/> for rank and file, and a
    /// boss's statline for a boss (§5.8 — the golem's 40, slow in a game whose
    /// party moves 160). Read wherever an enemy's turn budget is read — the walk
    /// it plans, the mana an idle turn banks, the Mire legend over it — and
    /// nowhere else: the attack-cost scale stays the standard budget's
    /// (<c>EnemyMove / MaxDistance</c>), which converts the party's costs into an
    /// enemy's, so a slower enemy buys fewer swings as well as fewer steps.
    /// </summary>
    public float MoveBudget { get; set; } = GameConstants.EnemyMove;

    /// <summary>
    /// The name its plate reads: a boss's (<see cref="BossDef.Name"/> — the Stone
    /// Golem). Null for rank and file, whose plate names what they are by what
    /// they hold: DUMMY, HEALER or CASTER.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The dungeon's theme (§4.3, <see cref="DungeonDef.Theme"/>), on every enemy
    /// the dungeon spawns — the boss's and every dummy's alike — and null on a
    /// row no dungeon placed (a fixture). It is what every drop in the dungeon
    /// <em>rolls for</em>: the theme joins the farm's roll pool as one more entry
    /// (<see cref="LootTable.Roll"/>), weighting it rather than handing it over, so
    /// an ordinary drop may grow it acquired and never forged. The boss's own drop
    /// is forged with it instead (<see cref="BossLoot.Roll"/>).
    /// <para>
    /// The dungeon's and never the individual's, like <see cref="ActorState.Attunement"/>:
    /// a save need not store it, since it re-derives from the dungeon (§5.1).
    /// </para>
    /// </summary>
    public ModifierType? Theme { get; set; }

    /// <summary>
    /// The guaranteed stacks this enemy's drop carries (§4.3: one per floor past
    /// four) — set on the boss row alone, by generation, from
    /// <see cref="DungeonState.PromisedStacks"/>, and null on every other row. It
    /// is how the boss floor "survives into the drop resolution": the count is
    /// on the row, so a drop re-derived later in the visit, or after a load that
    /// restored the row, pays exactly the stacks the visit promised at entry.
    /// <para>
    /// It is read by the boss's drop and decides nothing on its own: which table a
    /// corpse is rolled on is the roster's to say (<see cref="TurnSystem"/>'s
    /// drop feed for the actor it names as the boss), so a dummy handed a promise
    /// by hand still rolls the ordinary drop.
    /// </para>
    /// </summary>
    public int? PromisedStacks { get; set; }

    /// <summary>
    /// What this dummy adds to whatever it is holding: its accumulated revival
    /// damage, on the actor and never on the weapon (§3.2). Read by
    /// <see cref="CombatRules.BaseDamage"/> like anyone else's.
    /// </summary>
    public override int BonusDamage => RevivalDamage;

    /// <summary>A fresh instance per enemy: acquired stacks are per item, so two dummies never share one.</summary>
    public Weapon Weapon { get; set; } = GameContent.Current.Weapons.Instantiate(DefaultWeaponId);

    /// <summary>The weapon this enemy fights with is <see cref="Weapon"/>, which is never null.</summary>
    public override Weapon? EquippedWeapon => Weapon;

    /// <summary>
    /// True for a support caster (a staff whose innate effect lands on allies)
    /// that mends or shields its side rather than fighting. A caster that is
    /// not this is a debuff caster: an attacker whose swing is a cast.
    /// </summary>
    public bool IsSupportCaster => Weapon.Innate?.Def.Targets == TargetSide.Ally;

    /// <summary>
    /// True for a caster whose innate mends — its status restores HP, the
    /// status row's flag rather than its type. A support caster that is not
    /// this shields or buffs: the nameplate calls only a mender HEALER, and
    /// the AI picks a mender's target by wounds. The beat itself reads
    /// <see cref="IsSupportCaster"/>.
    /// </summary>
    public bool IsHealer => Weapon.Innate?.Def.Applies is { } type && StatusRules.Of(type).RestoresHp;

    public override float Radius => (GameConstants.Tile - 4f) / 2f;

    /// <summary>Turn index when the dummy was defeated; -1 while alive.</summary>
    public int DefeatedAtTurn { get; set; } = -1;

    /// <summary>
    /// Turns since a party member last saw the dummy. Starts at 2 so it stays
    /// passive until spotted; at 2+ it skips its movement phase.
    /// </summary>
    public int TurnsSinceSeen { get; set; } = 2;

    // ── The farm (§3.2) ──────────────────────────────────────────────────────
    // Five plain fields, every one of them settable, because together they are
    // the whole of a dummy's history: a save stores accumulated values rather
    // than a stream position, and a load hands them straight back (§5.1). There
    // is deliberately no `wasAtFullHp` here and none on the payload: the clean
    // kill is one comparison against MaxHp rather than a piece of tracked state.

    /// <summary>
    /// This dummy's index in the floor's spawn list, the only thing that makes a
    /// per-enemy stream possible: the revival ladder and (from §3.5) the drop are
    /// seeded from <c>mapSeed</c> and this, so the order the party walks a floor
    /// in cannot change what any one corpse is worth. Defaults to 0 for a dummy
    /// nobody placed — a fixture, or the one enemy of a test.
    /// </summary>
    public int SpawnIndex { get; set; }

    /// <summary>
    /// How many times this dummy has been put down: the quality of the drop it is
    /// carrying <em>and</em> how dangerous it has become, deliberately the same
    /// number (§3.2). Advanced by <see cref="FarmLadder.DefeatAdvance"/> when the
    /// Killed event settles — by 1, or by <see cref="Tuning.CleanKillBonus"/> for
    /// a blow that dealt at least this dummy's <see cref="MaxHp"/>. A boss's
    /// advances by exactly 1 at its one death, however clean the blow: nobody
    /// farms it, so the clean kill's bonus is not its to earn (the defeat feed
    /// <see cref="TurnSystem"/> fixes for the roster's boss).
    /// </summary>
    public int DefeatCount { get; set; }

    /// <summary>
    /// Cycles whose revival roll has already been applied. Never past
    /// <see cref="DefeatCount"/>: a clean kill leaves two cycles owing, and the
    /// resurrection resolves them one at a time and in order, so the second roll
    /// reads the statline the first produced.
    /// </summary>
    public int RevivalsRolled { get; set; }

    /// <summary>Damage the revival ladder has accumulated, absolute rather than a rate, so a save round-trips without replaying a roll.</summary>
    public int RevivalDamage { get; set; }

    /// <summary>Maximum HP the revival ladder has accumulated, on the same terms.</summary>
    public int RevivalMaxHp { get; set; }

    /// <summary>
    /// Whether the weapon this dummy was carrying has been taken off the floor
    /// (§3.5). A permanent kill leaves a ground item that nothing stores: the row
    /// above already fixes it, so a floor entry re-derives it through the table
    /// the roster fixed for it — <see cref="LootTable.Roll"/>, or the boss's
    /// <see cref="BossLoot.Roll"/> — and gets the identical weapon back
    /// (<see cref="TurnSystem.GroundItemOf"/>).
    /// <para>
    /// This one flag is what retires the offer. Without it a save taken after a
    /// pickup would hand the same weapon over again on the next load, and a save
    /// taken before one would have to choose between storing the item (a second
    /// source of truth) and losing it.
    /// </para>
    /// </summary>
    public bool DropTaken { get; set; }
}
