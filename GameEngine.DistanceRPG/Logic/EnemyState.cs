namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay state of the training-dummy enemy, ported from the prototype.
/// Position is the circle centre in logic space.
/// </summary>
public sealed class EnemyState : ActorState
{
    public EnemyState()
    {
        Hp = GameConstants.DummyHp;
    }

    public override int MaxHp => GameConstants.DummyHp;

    public Weapon Weapon { get; set; } = GameConstants.Weapons[1]; // Sword

    /// <summary>The weapon this enemy fights with is <see cref="Weapon"/>, which is never null.</summary>
    public override Weapon? EquippedWeapon => Weapon;

    /// <summary>True for a staff-wielding support enemy that heals its allies.</summary>
    public bool IsHealer => Weapon.IsCaster;

    public override float Radius => (GameConstants.Tile - 4f) / 2f;

    /// <summary>Turn index when the dummy was defeated; -1 while alive.</summary>
    public int DefeatedAtTurn { get; set; } = -1;

    /// <summary>
    /// Turns since a party member last saw the dummy. Starts at 2 so it stays
    /// passive until spotted; at 2+ it skips its movement phase.
    /// </summary>
    public int TurnsSinceSeen { get; set; } = 2;
}
