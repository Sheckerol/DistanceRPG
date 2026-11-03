namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay state of the training-dummy enemy, ported from the prototype.
/// Position is the circle centre in logic space.
/// </summary>
public sealed class EnemyState : ActorState
{
    /// <summary>What a dummy holds unless told otherwise: the Efficiency sword — Block x1, Push x1, Light x1.</summary>
    public const string DefaultWeaponId = "arming_sword";

    public EnemyState()
    {
        Hp = GameConstants.DummyHp;
    }

    public override int MaxHp => GameConstants.DummyHp;

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
}
