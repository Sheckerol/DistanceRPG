namespace GameEngine.DistanceRPG.Logic;

public enum RollOutcome
{
    Normal,

    /// <summary>Natural roll in the crit window — double damage.</summary>
    Crit,

    /// <summary>Natural 1 — half damage (minimum 1).</summary>
    Weak,
}

/// <summary>The raw d20 attack roll before the defender's mitigation.</summary>
public readonly record struct AttackRoll(int Roll, int Damage, RollOutcome Outcome);

/// <summary>An attack after the defender's block has been applied.</summary>
public readonly record struct AttackResolution(AttackRoll Roll, int Damage, int Blocked);

/// <summary>
/// Attack resolution ported from the Phaser prototype: d20 roll with a
/// crit window widened by the CritRange ability, a natural 1 dealing half
/// damage, and the defender's Block ability absorbing damage down to a
/// minimum of 1 taken.
/// </summary>
public static class CombatRules
{
    /// <summary>
    /// Roll an attack with <paramref name="weapon"/>.
    /// </summary>
    /// <param name="rollD20">Returns a die roll in [1, 20]; injected for testability.</param>
    public static AttackRoll RollAttack(Weapon weapon, Func<int> rollD20)
    {
        var crit = weapon.GetAbility(AbilityType.CritRange);
        int critAt = crit != null ? 20 - crit.Value : 20;
        int roll = rollD20();

        if (roll >= critAt)
            return new AttackRoll(roll, weapon.Damage * 2, RollOutcome.Crit);
        if (roll == 1)
            return new AttackRoll(roll, Math.Max(1, weapon.Damage / 2), RollOutcome.Weak);
        return new AttackRoll(roll, weapon.Damage, RollOutcome.Normal);
    }

    /// <summary>
    /// Roll an attack and apply the defender's Block ability. Block absorbs up
    /// to its value but always lets at least 1 damage through.
    /// </summary>
    public static AttackResolution ResolveAttack(Weapon attackerWeapon, Weapon? defenderWeapon, Func<int> rollD20)
    {
        var attackRoll = RollAttack(attackerWeapon, rollD20);

        int damage = attackRoll.Damage;
        int absorbed = 0;
        var block = defenderWeapon?.GetAbility(AbilityType.Block);
        if (block is { Value: > 0 })
        {
            absorbed = Math.Min(damage - 1, block.Value);
            damage -= absorbed;
        }

        return new AttackResolution(attackRoll, damage, absorbed);
    }

    /// <summary>
    /// Center-to-center distance check with both radii subtracted, matching the
    /// original: an attack reaches if surface-to-surface distance ≤ weapon range.
    /// (Line of sight is checked separately by the scene, which owns the walls.)
    /// </summary>
    public static bool InAttackRange(
        float attackerX, float attackerY, float attackerRadius,
        float targetX, float targetY, float targetRadius,
        Weapon weapon)
    {
        float dx = targetX - attackerX;
        float dy = targetY - attackerY;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        return d - attackerRadius - targetRadius <= weapon.Range;
    }
}
