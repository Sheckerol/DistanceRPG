namespace GameEngine.DistanceRPG.Logic;

public enum RollOutcome
{
    Normal,

    /// <summary>Natural roll in the crit window — damage times the attacker's CritMultiplier (x2 with no stacks).</summary>
    Crit,

    /// <summary>Natural 1 — half damage (minimum 1).</summary>
    Weak,
}

/// <summary>The raw d20 attack roll before the defender's mitigation.</summary>
public readonly record struct AttackRoll(int Roll, int Damage, RollOutcome Outcome);

/// <summary>
/// An attack as the HUD and tests see it, projected from the settled
/// <see cref="DamagePayload"/>: the roll, the pipeline's two outputs —
/// <see cref="Dealt"/> after mitigation and before absorption, <see cref="Taken"/>
/// what reached hit points — what Block prevented, what Ward swallowed, and
/// the riders that landed on the defender.
/// </summary>
public readonly record struct AttackResolution(
    AttackRoll Roll, int Dealt, int Taken, int Blocked, int WardSpent, IReadOnlyList<StatusApplication> Riders)
{
    /// <summary>What reached hit points — the number the floating text shows. An alias of <see cref="Taken"/>.</summary>
    public int Damage => Taken;
}

/// <summary>
/// Attack resolution as the §1.6 pipeline: a d20 roll made before the chain,
/// then <see cref="GameEvent.DamageTaken"/> through an <see cref="EventTable"/>
/// whose sorted handlers — the roll to base damage, the defender's Block,
/// and everything the later phases hang between them — each transform the
/// payload and return it. Resolving writes nothing: the table's applier does,
/// once, with the settled result. Distances are logic units, 32 per tile.
/// </summary>
public static class CombatRules
{
    /// <summary>The top of the die: a natural 20 crits for every weapon, whatever its CritWindow.</summary>
    public const int NaturalTwenty = 20;

    /// <summary>The compiled chain alone — no applier — for resolutions that only need the settled numbers.</summary>
    private static readonly Lazy<EventTable> PureTable = new(() =>
    {
        var table = new EventTable();
        Behaviours.RegisterAll(table);
        return table;
    });

    /// <summary>The natural roll <paramref name="attacker"/> needs to crit: 20 less their CritWindow, weapon plus innate.</summary>
    public static int CritThreshold(ActorState attacker)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        return NaturalTwenty - attacker.Value(ModifierType.CritWindow);
    }

    /// <summary>The natural roll <paramref name="weapon"/> alone needs to crit — no wielder, so no innate modifiers.</summary>
    public static int CritThreshold(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return NaturalTwenty - weapon.Modifiers.Value(ModifierType.CritWindow);
    }

    /// <summary>
    /// Step 1 as a pure function of its inputs: a natural roll at or above
    /// <paramref name="critThreshold"/> multiplies <paramref name="damage"/> by
    /// <paramref name="critMultiplier"/>; a natural 1 halves it, floored at 1
    /// (integer division, as shipped); anything else is the base damage.
    /// </summary>
    public static AttackRoll RollToBase(int roll, int damage, int critThreshold, int critMultiplier)
    {
        if (roll >= critThreshold)
            return new AttackRoll(roll, damage * critMultiplier, RollOutcome.Crit);
        if (roll == 1)
            return new AttackRoll(roll, Math.Max(1, damage / 2), RollOutcome.Weak);
        return new AttackRoll(roll, damage, RollOutcome.Normal);
    }

    /// <summary>
    /// Roll an attack with <paramref name="weapon"/> alone — no wielder, so the
    /// weapon's own CritWindow and CritMultiplier and nothing innate.
    /// </summary>
    /// <param name="rollD20">Returns a die roll in [1, 20]; injected for testability.</param>
    public static AttackRoll RollAttack(Weapon weapon, Func<int> rollD20)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(rollD20);
        return RollToBase(rollD20(), weapon.Damage, CritThreshold(weapon), weapon.Modifiers.Value(ModifierType.CritMultiplier));
    }

    /// <summary>
    /// Resolve <paramref name="attacker"/> hitting <paramref name="defender"/> with
    /// <paramref name="weapon"/> through the compiled chain alone: the settled
    /// numbers, with nothing written to either actor. The turn system resolves
    /// through its own table instead (<see cref="Resolve"/>), whose applier writes.
    /// </summary>
    public static AttackResolution ResolveAttack(ActorState attacker, ActorState defender, Weapon weapon, int distanceUnits, Func<int> rollD20)
        => Resolve(PureTable.Value, attacker, defender, weapon, distanceUnits, rollD20);

    /// <summary>
    /// Resolve through <paramref name="table"/>: roll the d20 first so step 1 is
    /// pure, seed the payload, raise <see cref="GameEvent.DamageTaken"/> with
    /// the attacker as <c>self</c> and the defender as <c>other</c>, and project
    /// the settled payload. Whatever the table's applier does with the result
    /// has happened by the time this returns.
    /// </summary>
    public static AttackResolution Resolve(EventTable table, ActorState attacker, ActorState defender, Weapon weapon, int distanceUnits, Func<int> rollD20)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(rollD20);

        var initial = DamagePayload.Initial(weapon, rollD20(), distanceUnits);
        var settled = table.Raise(GameEvent.DamageTaken, initial, attacker, defender);
        return Project(settled);
    }

    /// <summary>The HUD's view of a settled payload: the roll shows the weapon's own figure, before mitigation.</summary>
    public static AttackResolution Project(DamagePayload settled)
    {
        ArgumentNullException.ThrowIfNull(settled);
        return new AttackResolution(
            new AttackRoll(settled.Roll, settled.WeaponShare, settled.Outcome),
            settled.Dealt, settled.Taken, settled.Absorbed, settled.WardSpent,
            settled.ApplyToDefender.IsDefault ? Array.Empty<StatusApplication>() : settled.ApplyToDefender);
    }

    /// <summary>Centre-to-centre distance with both radii subtracted: how far apart two surfaces are, in logic units. Negative when overlapping.</summary>
    public static float SurfaceDistance(
        float attackerX, float attackerY, float attackerRadius,
        float targetX, float targetY, float targetRadius)
    {
        float dx = targetX - attackerX;
        float dy = targetY - attackerY;
        return MathF.Sqrt(dx * dx + dy * dy) - attackerRadius - targetRadius;
    }

    public static float SurfaceDistance(ActorState a, ActorState b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return SurfaceDistance(a.X, a.Y, a.Radius, b.X, b.Y, b.Radius);
    }

    /// <summary>
    /// The surface distance as the payload carries it: whole logic units,
    /// rounded up so a fraction past a tile boundary counts as the next tile,
    /// and never below zero.
    /// </summary>
    public static int SurfaceDistanceUnits(ActorState a, ActorState b)
        => Math.Max(0, (int)MathF.Ceiling(SurfaceDistance(a, b)));

    /// <summary>
    /// Center-to-center distance check with both radii subtracted, matching the
    /// original: an attack reaches if surface-to-surface distance ≤ weapon range.
    /// (Line of sight is checked separately by the scene, which owns the walls.)
    /// </summary>
    public static bool InAttackRange(
        float attackerX, float attackerY, float attackerRadius,
        float targetX, float targetY, float targetRadius,
        Weapon weapon)
        => SurfaceDistance(attackerX, attackerY, attackerRadius, targetX, targetY, targetRadius) <= weapon.Range;
}
