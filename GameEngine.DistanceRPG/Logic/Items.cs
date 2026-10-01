namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What a bag slot holds (§3.4's "third item category", §3.5: <c>Inventory</c>
/// "needs to hold something other than a <see cref="Weapon"/>"): a weapon or a
/// potion. A slot holds one thing, because there is one array
/// (<see cref="PartyMemberState.Inventory"/>) and each slot of it is one item or
/// none — so the type carries "one potion a slot, no stacking" (§3.4) and no
/// writer has to keep a second list in step with the first.
/// <para>
/// The hand holds a weapon by rule (PHASE 6a DECISION 7), never a potion: a
/// potion lives in a member's bag, slots 1 and up, and nowhere else (PHASE 6b
/// DECISION 1). Every door that moves, counts or reads a slot as a weapon asks
/// <c>is Weapon</c>, never "is anything there".
/// </para>
/// </summary>
public interface IItem
{
    /// <summary>The item's name as every screen says it: in capitals, with no apostrophe (<see cref="Weapon.Shown"/>).</summary>
    string ShownName { get; }
}

/// <summary>
/// The two potions there are (§3.4): health and mana. That is the whole list —
/// no buff, cure or utility potion — and a save names one by this enum's name.
/// </summary>
public enum PotionType
{
    Health,
    Mana,
}

/// <summary>
/// A potion (§3.5: "<c>(PotionType Type, int Quality)</c> and nothing else"): its
/// type, and the units of the farm's yield it was mixed from (§6.6). What it
/// restores is worked out when it is drunk, from the drinker's maximum at that
/// moment, so a potion mixed early rides the ladders with the member who carries
/// it.
/// <para>
/// <strong>A value, not an object with an identity.</strong> Two potions of the
/// same type and quality are equal, and nothing finds a potion by reference: a
/// slot says which potion is meant. A potion is in a member's bag (PHASE 6b
/// DECISION 1) and leaves it through one door,
/// <see cref="PartyMemberState.TakePotion"/>.
/// </para>
/// </summary>
/// <param name="Type">Health or mana.</param>
/// <param name="Quality">The units it was mixed from: 1 at least, with no ceiling — past a whole pool, more is overshoot (§6.6).</param>
public sealed record Potion(PotionType Type, int Quality) : IItem
{
    /// <summary>Health or mana, and nothing else (§3.4).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a <see cref="PotionType"/>.</exception>
    public PotionType Type { get; } = Enum.IsDefined(Type)
        ? Type
        : throw new ArgumentOutOfRangeException(nameof(Type), Type, "There are two potions, health and mana, and that is the whole list.");

    /// <summary>One unit makes an ordinary potion (§6.6), and a quality is a count of units: never below 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Below 1.</exception>
    public int Quality { get; } = Quality >= 1
        ? Quality
        : throw new ArgumentOutOfRangeException(nameof(Quality), Quality, "One unit makes an ordinary potion, and a quality is a count of units: it is 1 at least.");

    /// <summary>The potion as the screen says it: <c>HEALTH POTION Q2</c>, <c>MANA POTION Q1</c>.</summary>
    public string ShownName => $"{(Type == PotionType.Health ? "HEALTH" : "MANA")} POTION Q{Quality}";
}

/// <summary>
/// One potion drunk (§3.4), as <see cref="TurnSystem.TryDrink"/> reports it and
/// <see cref="TurnSystem.PotionDrunk"/> hands the scene for its beat and its log
/// line: what was drunk and from which slot, what it offered, what the pool
/// gained, and the movement it cost.
/// </summary>
/// <param name="Potion">The potion, gone from the bag.</param>
/// <param name="Slot">The bag slot it was drunk from, 0 being the hand.</param>
/// <param name="Share">What it offered the drinker's pool: the share of its maximum, overshoot and all (<see cref="Alchemist.Share(Potion, ActorState)"/>).</param>
/// <param name="Restored">What the pool gained: HP after less HP before, or the mana the pool took — never the overshoot, whatever Overheal banked of it.</param>
/// <param name="Cost">The movement it cost (<see cref="TurnSystem.PotionUseCost"/>).</param>
public sealed record PotionDrink(Potion Potion, int Slot, int Share, int Restored, int Cost);
