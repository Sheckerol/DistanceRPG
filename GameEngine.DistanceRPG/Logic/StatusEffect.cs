namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Every status effect in the game, in the §1.5 table's order. A status is
/// three things and nothing else (§1.5, §1.7): a level count, a trigger and a
/// fixed effect per level. What one level is worth is the
/// <see cref="Tuning.EffectPerLevel"/> row; when it fires and what firing does
/// to the levels is its <see cref="StatusRules"/> row; and how many levels an
/// application grants belongs to the applier, never to the status.
/// </summary>
public enum StatusEffectType
{
    /// <summary>Heal-over-time: restores HP equal to its level at the target's turn end, then loses a level. Casting a Staff of Renewal stacks it.</summary>
    Regeneration,

    /// <summary>Temporary hit points: absorbs 1 per level of what a hit dealt, spending the levels it absorbs with. Not armour, so a crit does not skip it.</summary>
    Ward,

    /// <summary>Damage-over-time: damage equal to its level at the target's turn end, then one level fewer.</summary>
    Poison,

    /// <summary>Cuts the target's movement budget by 10% a level; at ten levels or more the target cannot move until decay brings it back under.</summary>
    Mire,

    /// <summary>The target takes +1 damage per level from every weapon hit — a crit rider, and the reason to focus one enemy.</summary>
    Sundered,

    /// <summary>The target deals 1 less per level, floored at 1 — the other crit rider, and what a successful block leaves on the attacker.</summary>
    Weakened,

    /// <summary>Damage-over-time from a serrated wound: the weapon's own DoT.</summary>
    Bleeding,

    /// <summary>Hidden: surplus healing points banked by Overheal, converting into Ward at <see cref="Tuning.OverhealPerWard"/> to one and resetting.</summary>
    OverhealPool,

    /// <summary>The lingering element: damage-over-time keyed on the <see cref="DamageType"/> that lit it, so a burn and a frostbite are one mechanism with two names and two entries.</summary>
    Searing,

    /// <summary>Block stripped, 1 per level, for the rest of the round.</summary>
    Softened,
}

/// <summary>
/// One status on one actor — the whole representation (§1.7): its type, the
/// element it is keyed on (<see cref="StatusEffectType.Searing"/> only; null
/// for every other type) and its levels, which are magnitude and duration at
/// once. Immutable: an actor's list holds one entry per (Type, Element),
/// replaced on change. No back-reference to what applied it — levels on a
/// target are levels, whatever happens to the source afterwards.
/// </summary>
public sealed record StatusEffect(StatusEffectType Type, DamageType? Element, int Levels);

/// <summary>When a status fires — the table column the dispatcher reads: which event carries its handler.</summary>
public enum StatusTrigger
{
    /// <summary>The end of the actor's own turn — <see cref="GameEvent.TurnEnd"/>.</summary>
    TurnEnd,

    /// <summary>Being hit — <see cref="GameEvent.DamageTaken"/>.</summary>
    TakingDamage,

    /// <summary>Healing past full — <see cref="GameEvent.HealingAboveFull"/>.</summary>
    ReceivingHealing,

    /// <summary>
    /// The end of the round — <see cref="GameEvent.RoundEnd"/>: what the Reset
    /// rows fire on, and the one event that moves a status nothing fires. Mire,
    /// Sundered and Weakened are read where they apply — a movement budget,
    /// pipeline steps 2 and 3 — and lose their levels only to the universal
    /// decay, so their rows name the decay's event with
    /// <see cref="OnTrigger.None"/> beside it: one convention for all three.
    /// </summary>
    RoundEnd,
}

/// <summary>What firing does to the levels — the table column the resolver reads.</summary>
public enum OnTrigger
{
    /// <summary>Apply the effect at the level, then one level fewer: the tick is the decay.</summary>
    TickAndDecrement,

    /// <summary>Spend as many levels as one event needs: being hit for 19 is nineteen points of one event.</summary>
    SpendMany,

    /// <summary>Convert the levels into something else and go back to zero.</summary>
    ConvertAndReset,

    /// <summary>Go back to zero.</summary>
    Reset,

    /// <summary>Nothing on the trigger: the levels are read where they apply and only the universal decay moves them.</summary>
    None,
}

/// <summary>
/// A status table row: the two behaviour columns of §1.7 — the third column,
/// the effect per level, is a tunable — and two flags the handlers read
/// instead of asking a type: whether a tick's effect restores HP rather than
/// removing it (<paramref name="RestoresHp"/>, the heal-over-time rows) and
/// whether entries key on the element that lit them
/// (<paramref name="KeyedOnElement"/>, the lingering element). A second
/// heal-over-time or a second element-keyed status is a row, not an edit.
/// </summary>
public sealed record StatusRule(StatusEffectType Type, StatusTrigger Trigger, OnTrigger OnTrigger,
    bool RestoresHp = false, bool KeyedOnElement = false);

/// <summary>
/// The compiled status table. Decay is not a column: every status loses one
/// level per round, and the rule that says how is keyed off the trigger — a
/// status that fires at <see cref="StatusTrigger.TurnEnd"/> takes that loss as
/// the decrement of its own tick and is skipped by the round-end sweep; every
/// other status loses its level at <see cref="GameEvent.RoundEnd"/>. There is
/// no per-status decay constant to add.
/// </summary>
public static class StatusRules
{
    public static IReadOnlyDictionary<StatusEffectType, StatusRule> Table { get; } = Build(
    [
        new(StatusEffectType.Regeneration, StatusTrigger.TurnEnd, OnTrigger.TickAndDecrement, RestoresHp: true),
        new(StatusEffectType.Ward, StatusTrigger.TakingDamage, OnTrigger.SpendMany),
        new(StatusEffectType.Poison, StatusTrigger.TurnEnd, OnTrigger.TickAndDecrement),
        new(StatusEffectType.Mire, StatusTrigger.RoundEnd, OnTrigger.None),          // nothing fires it: read where budgets are set, moved by the decay alone
        new(StatusEffectType.Sundered, StatusTrigger.RoundEnd, OnTrigger.None),      // nothing fires it: read at pipeline step 3, moved by the decay alone
        new(StatusEffectType.Weakened, StatusTrigger.RoundEnd, OnTrigger.None),      // nothing fires it: read at pipeline step 2, moved by the decay alone
        new(StatusEffectType.Bleeding, StatusTrigger.TurnEnd, OnTrigger.TickAndDecrement),
        new(StatusEffectType.OverhealPool, StatusTrigger.ReceivingHealing, OnTrigger.ConvertAndReset),
        new(StatusEffectType.Searing, StatusTrigger.TurnEnd, OnTrigger.TickAndDecrement, KeyedOnElement: true),
        new(StatusEffectType.Softened, StatusTrigger.RoundEnd, OnTrigger.Reset),
    ]);

    public static StatusRule Of(StatusEffectType type) => Table[type];

    /// <summary>The constant effect of one level, from the tuning table: 1 HP, 1 damage, 1 absorbed, 10 percent of a movement budget.</summary>
    public static int EffectPerLevel(StatusEffectType type)
        => GameContent.Current.Tuning.Lookup(t => t.EffectPerLevel, type, fallback: 0);

    /// <summary>Whether the round-end sweep takes this status's level: every status but those that tick at their own turn's end, whose tick is their decay.</summary>
    public static bool DecaysAtRoundEnd(StatusEffectType type) => Of(type).Trigger != StatusTrigger.TurnEnd;

    /// <summary>Whether entries of this type are keyed on an element — the row's flag: the lingering element carries the type that lit it; nothing else does.</summary>
    public static bool KeysOnElement(StatusEffectType type) => Of(type).KeyedOnElement;

    private static IReadOnlyDictionary<StatusEffectType, StatusRule> Build(StatusRule[] rows)
    {
        var table = rows.ToDictionary(r => r.Type);
        foreach (var type in Enum.GetValues<StatusEffectType>())
            if (!table.ContainsKey(type))
                throw new InvalidOperationException($"The status table has no row for {type}.");
        return table;
    }
}
