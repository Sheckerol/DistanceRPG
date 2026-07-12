namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay state of the training-dummy enemy, ported from the prototype.
/// Position is the circle centre in logic space.
/// </summary>
public sealed class EnemyState
{
    public float X { get; set; }
    public float Y { get; set; }

    public int Hp { get; set; } = GameConstants.DummyHp;
    public int MaxHp { get; } = GameConstants.DummyHp;
    public bool Alive { get; set; } = true;

    public Weapon Weapon { get; set; } = GameConstants.Weapons[1]; // Sword

    /// <summary>True for a staff-wielding support enemy that heals its allies.</summary>
    public bool IsHealer => Weapon.IsCaster;

    /// <summary>Active status effects (e.g. a healer's Regeneration on this enemy).</summary>
    public List<StatusEffect> StatusEffects { get; } = new();

    public float Radius => (GameConstants.Tile - 4f) / 2f;

    /// <summary>Add a status effect, stacking onto any existing one of the same type.</summary>
    public StatusEffect ApplyStatusEffect(StatusEffectType type, int level)
    {
        var existing = StatusEffects.FirstOrDefault(e => e.Type == type);
        if (existing != null)
        {
            existing.Level += level;
            return existing;
        }
        var added = new StatusEffect { Type = type, Level = level };
        StatusEffects.Add(added);
        return added;
    }

    /// <summary>Level of the given effect currently on the enemy, 0 if absent.</summary>
    public int StatusLevel(StatusEffectType type)
        => StatusEffects.FirstOrDefault(e => e.Type == type)?.Level ?? 0;

    /// <summary>Turn index when the dummy was defeated; -1 while alive.</summary>
    public int DefeatedAtTurn { get; set; } = -1;

    /// <summary>
    /// Turns since a party member last saw the dummy. Starts at 2 so it stays
    /// passive until spotted; at 2+ it skips its movement phase.
    /// </summary>
    public int TurnsSinceSeen { get; set; } = 2;
}
