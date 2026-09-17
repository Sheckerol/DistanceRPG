namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What every actor on the map shares, party member or enemy: a position (the
/// circle centre in logic space — pixels, y-down, 32 per tile), hit points, a
/// reach radius, the weapon it fights with, and the status effects it carries.
/// Status effects live here so a buff or debuff can land on either side through
/// one mechanism, and the turn system's attack resolver, status tick and threat
/// zones are written against this type rather than against a side.
/// </summary>
public abstract class ActorState
{
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>Current hit points; each kind of actor starts at its own full value.</summary>
    public int Hp { get; set; }

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
    /// Mana pool for casting. Every trigger costs mana on both sides, so an
    /// enemy caster carries one too; full at spawn — a pool nobody has written
    /// reads as this actor's own <see cref="MaxMana"/>, so a kind that overrides
    /// the maximum spawns full as well.
    /// </summary>
    public int Mana
    {
        get => _mana ?? MaxMana;
        set => _mana = value;
    }

    private int? _mana;

    public virtual int MaxMana => GameConstants.MaxMana;

    /// <summary>Active heal-over-time and other ongoing effects.</summary>
    public List<StatusEffect> StatusEffects { get; } = new();

    /// <summary>
    /// Add a status effect, stacking its level onto any existing effect of the
    /// same type. Returns the (possibly merged) effect now on the actor.
    /// </summary>
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

    /// <summary>Level of the given effect currently on the actor, 0 if absent.</summary>
    public int StatusLevel(StatusEffectType type)
        => StatusEffects.FirstOrDefault(e => e.Type == type)?.Level ?? 0;
}
