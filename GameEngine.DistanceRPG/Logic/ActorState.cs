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
