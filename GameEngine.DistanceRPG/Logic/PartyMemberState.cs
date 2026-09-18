namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Gameplay state of one party member, ported from the prototype's per-char
/// object. Position is the circle centre in logic space (pixels, y-down).
/// Position, HP, radius and status effects are the shared <see cref="ActorState"/>.
/// </summary>
public sealed class PartyMemberState : ActorState
{
    public required string Id { get; init; }
    public required int ColorIndex { get; init; }

    public PartyMemberState()
    {
        Hp = GameConstants.PlayerHp;
    }

    public override int MaxHp => GameConstants.PlayerHp;

    /// <summary>Three slots; slot 0 is the equipped weapon.</summary>
    public Weapon?[] Inventory { get; } = new Weapon?[3];

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
