namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The alchemist's arithmetic (§3.4, §6.6): what a potion restores, worked out
/// when it is drunk from the drinker's maximum at that moment, so a potion rides
/// the ladders the member is climbing rather than decaying into irrelevance as a
/// flat number would. Engine-free and stateless; the numbers are
/// <c>tuning.json</c>'s (<see cref="Tuning.HealthPotionPercent"/>,
/// <see cref="Tuning.ManaPotionPercent"/>).
/// <para>
/// <strong>A share, rounded down, of the pool's maximum</strong> (PHASE 6b
/// DECISION 3): health of <see cref="ActorState.MaxHp"/>; mana of
/// <see cref="ActorState.MaxMana"/>, the pool the member has earned, never of
/// what its locks leave spendable — what a drink can actually take is the
/// mana applier's to cap (<see cref="TurnSystem.TryDrink"/>). The share has no
/// minimum: quality 1 on a pool of 4 offers nothing.
/// </para>
/// <para>
/// <strong>No quality is refused.</strong> Past the quality that restores a whole
/// pool (<see cref="QualityForAll"/>) more quality is overshoot, which only
/// Overheal keeps (§6.6) — so a share is never capped, and is worked out wide
/// enough that no quality and no maximum a save can hold wraps it: it saturates
/// at the largest whole number instead.
/// </para>
/// </summary>
public static class Alchemist
{
    /// <summary>A percentage's whole.</summary>
    private const int FullPercent = 100;

    /// <summary>
    /// What a potion of <paramref name="type"/> mixed at <paramref name="quality"/>
    /// offers a pool whose maximum is <paramref name="max"/>:
    /// <c>floor(percent × quality × max / 100)</c>, held at the largest whole
    /// number. At 20 percent, quality 1 on 33 is 6 (6.6 rounded down), quality 3
    /// on 33 is 19, quality 5 on 33 is all 33, and quality 7 on 30 is 42 — a whole
    /// pool and 12 over.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A quality below 1, or a negative maximum.</exception>
    public static int Share(PotionType type, int quality, int max) => ShareAt(Percent(type), quality, max);

    /// <summary>
    /// <see cref="Share(PotionType, int, int)"/>'s arithmetic at <paramref name="percent"/>
    /// a unit: worked out wide, so no percent, quality or maximum wraps it, and
    /// held at the largest whole number.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A percent or a quality below 1, or a negative maximum.</exception>
    internal static int ShareAt(int percent, int quality, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        Int128 share = (Int128)percent * quality * max / FullPercent;
        return share >= int.MaxValue ? int.MaxValue : (int)share;
    }

    /// <summary>
    /// What <paramref name="potion"/> offers <paramref name="drinker"/> now: a
    /// share of its max HP for a health potion, of its max mana — the earned pool
    /// — for a mana potion (<see cref="Share(PotionType, int, int)"/>).
    /// </summary>
    public static int Share(Potion potion, ActorState drinker)
    {
        ArgumentNullException.ThrowIfNull(potion);
        ArgumentNullException.ThrowIfNull(drinker);
        return Share(potion.Type, potion.Quality, MaxOf(potion.Type, drinker));
    }

    /// <summary>
    /// The pool's maximum a potion of <paramref name="type"/> is a share of:
    /// <see cref="ActorState.MaxHp"/>, or <see cref="ActorState.MaxMana"/> — the
    /// earned pool, which the readouts beside the share name.
    /// </summary>
    public static int MaxOf(PotionType type, ActorState drinker)
    {
        ArgumentNullException.ThrowIfNull(drinker);
        return type == PotionType.Health ? drinker.MaxHp : drinker.MaxMana;
    }

    /// <summary>
    /// The quality that restores a whole pool from nothing:
    /// <c>ceil(100 / percent)</c> — 5 at 20 percent, the ceiling §6.6 names, "a
    /// potion that heals you completely from 1 HP". Rounding down never weakens
    /// it: at this quality the share is at least the whole maximum, at every
    /// percent from 1 to 100. Past it, more quality is overshoot.
    /// </summary>
    public static int QualityForAll(PotionType type) => QualityForAllAt(Percent(type));

    /// <summary><see cref="QualityForAll"/>'s arithmetic at <paramref name="percent"/> a unit: <c>ceil(100 / percent)</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A percent below 1.</exception>
    internal static int QualityForAllAt(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 1);
        return (FullPercent + percent - 1) / percent;
    }

    /// <summary>The share a unit of quality restores, as a percentage: the tuning's, for the type.</summary>
    private static int Percent(PotionType type)
    {
        var tuning = GameContent.Current.Tuning;
        return type switch
        {
            PotionType.Health => tuning.HealthPotionPercent,
            PotionType.Mana => tuning.ManaPotionPercent,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "There are two potions, health and mana."),
        };
    }
}
