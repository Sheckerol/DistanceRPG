namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay state of the training-dummy enemy, ported from the prototype.
/// Position is the circle centre in logic space.
/// </summary>
public sealed class EnemyState : ActorState
{
    /// <summary>What a dummy holds unless told otherwise: the Efficiency sword — Block x1, Push x1, Light x1.</summary>
    public const string DefaultWeaponId = "arming_sword";

    /// <summary>
    /// A dummy's HP is the flat constant plus whatever its revivals have added
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
    public override int MaxHp => GameConstants.DummyHp + RevivalMaxHp;

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
    /// a blow that dealt at least this dummy's <see cref="MaxHp"/>.
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
    /// above already fixes it, so a floor entry re-derives it through
    /// <see cref="LootTable.Roll"/> and gets the identical weapon back
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
