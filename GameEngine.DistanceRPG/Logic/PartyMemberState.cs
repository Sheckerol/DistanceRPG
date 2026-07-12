namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay state of one party member, ported from the prototype's per-char
/// object. Position is the circle centre in logic space (pixels, y-down).
/// </summary>
public sealed class PartyMemberState
{
    public required string Id { get; init; }
    public required int ColorIndex { get; init; }

    public float X { get; set; }
    public float Y { get; set; }

    public int Hp { get; set; } = GameConstants.PlayerHp;
    public int MaxHp { get; } = GameConstants.PlayerHp;
    public bool Alive { get; set; } = true;

    /// <summary>Mana pool for casting staves; persists across turns.</summary>
    public int Mana { get; set; } = GameConstants.MaxMana;
    public int MaxMana => GameConstants.MaxMana;

    /// <summary>Active heal-over-time and other ongoing effects.</summary>
    public List<StatusEffect> StatusEffects { get; } = new();

    /// <summary>Three slots; slot 0 is the equipped weapon.</summary>
    public Weapon?[] Inventory { get; } = new Weapon?[3];

    public Weapon? EquippedWeapon => Inventory[0];

    /// <summary>Movement budget left this turn, in logic units.</summary>
    public float DistLeft { get; set; } = GameConstants.MaxDistance;

    /// <summary>This turn's cap: base budget plus movement saved last turn.</summary>
    public float EffectiveMax { get; set; } = GameConstants.MaxDistance;

    /// <summary>Banked at end of turn (half the unspent budget, capped).</summary>
    public float SavedMovement { get; set; }

    public float Radius => GameConstants.PlayerHalf;

    /// <summary>
    /// Start-of-turn reset: cash in the saved movement bonus, refill the budget.
    /// </summary>
    public void StartTurn()
    {
        float bonus = SavedMovement;
        SavedMovement = 0;
        EffectiveMax = GameConstants.MaxDistance + bonus;
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

    /// <summary>
    /// Convert the movement left unspent this turn into mana: a fully idle turn
    /// (nothing moved) restores <c>MaxMana / <see cref="GameConstants.ManaRegenTurns"/></c>,
    /// scaling linearly with the unused fraction of the base budget. Casting a
    /// staff spends movement, so it eats into this the same way walking does.
    /// Returns the mana actually regained.
    /// </summary>
    public int RegenManaFromUnusedMovement()
    {
        if (!Alive || Mana >= MaxMana) return 0;
        float frac = Math.Clamp(DistLeft / GameConstants.MaxDistance, 0f, 1f);
        int regen = (int)MathF.Round(frac * MaxMana / GameConstants.ManaRegenTurns);
        int before = Mana;
        Mana = Math.Min(MaxMana, Mana + regen);
        return Mana - before;
    }

    /// <summary>
    /// Add a status effect, stacking its level onto any existing effect of the
    /// same type. Returns the (possibly merged) effect now on the member.
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

    /// <summary>Level of the given effect currently on the member, 0 if absent.</summary>
    public int StatusLevel(StatusEffectType type)
        => StatusEffects.FirstOrDefault(e => e.Type == type)?.Level ?? 0;
}
