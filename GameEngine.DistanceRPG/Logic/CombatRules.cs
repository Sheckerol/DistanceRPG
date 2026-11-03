using System.Collections.Immutable;

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

/// <summary>The raw d20 cast roll before the chain: what it did to the innate's levels and to the cast's mana.</summary>
public readonly record struct CastRoll(int Roll, bool IsCrit, bool IsFumble, int Levels, int ManaCost);

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
    /// The base damage <paramref name="self"/> swings <paramref name="weapon"/>
    /// for, before the roll multiplies it: the weapon's own damage, whatever the
    /// distance has already added (<paramref name="longshotBonus"/>), and
    /// <c>+floor(L / 2)</c> for the wielder's proficiency in the weapon's class
    /// (§2.2), plus whatever the wielder itself adds to anything it holds
    /// (<see cref="ActorState.BonusDamage"/> — a farmed dummy's revival ladder,
    /// §3.2). The one place a wielder's base is computed, because step 1 is
    /// taken in three: the roll at (1,0), Longshot re-taking it at (1,1) over a
    /// priced distance, and a wand's cast re-deriving it to size the burn its
    /// element leaves. A bonus added at one of them alone would be silently
    /// dropped at the others.
    /// <para>
    /// It enters here, ahead of the crit multiplier, because the proficiency
    /// bonus is the weapon's own damage rather than a rider on it: a wielder who
    /// has mastered a weapon hits harder, and a crit multiplies what they hit
    /// for. The flat-after-the-multiplier convention belongs to the temporary
    /// statuses (Sundered, Weakened), which must not compound with a crit.
    /// </para>
    /// </summary>
    public static int BaseDamage(ActorState self, Weapon weapon, int longshotBonus = 0)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(weapon);
        return weapon.Damage + longshotBonus + Progression.DamageBonus(self.WeaponLevel(weapon)) + self.BonusDamage;
    }

    /// <summary>
    /// Roll an attack with <paramref name="weapon"/> alone — no wielder, so the
    /// weapon's own CritWindow and CritMultiplier and nothing innate, and no
    /// proficiency bonus either: that is the wielder's, and there is none here.
    /// </summary>
    /// <param name="rollD20">Returns a die roll in [1, 20]; injected for testability.</param>
    public static AttackRoll RollAttack(Weapon weapon, Func<int> rollD20)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(rollD20);
        return RollToBase(rollD20(), weapon.Damage, CritThreshold(weapon), weapon.Modifiers.Value(ModifierType.CritMultiplier));
    }

    /// <summary>A cast crit doubles the effect level applied — the crit axis the caster classes keep (§1.6).</summary>
    public const int CastCritLevelMultiplier = 2;

    /// <summary>A cast crit halves that cast's mana, floored (settled): a cheap efficient burst.</summary>
    public const int CastCritManaDivisor = 2;

    /// <summary>A cast fumble — a natural 1 — doubles that cast's mana (settled): a real punish for fishing.</summary>
    public const int CastFumbleManaMultiplier = 2;

    /// <summary>The attunement chart's "Same" (§1.4): the hit is halved — it cancels, but never to nothing.</summary>
    public const int ResistedDamageDivisor = 2;

    /// <summary>The attunement chart's "Opposed" (§1.4): x1.5, as a percentage, truncated.</summary>
    public const int OpposedDamagePercent = 150;

    /// <summary>
    /// A cast's step 1 as a pure function of its inputs, the counterpart of
    /// <see cref="RollToBase"/>: staves and wands roll d20 like attacks, in the
    /// caster's own crit window. A natural roll at or above
    /// <paramref name="critThreshold"/> doubles <paramref name="levels"/> and
    /// halves <paramref name="manaCost"/>; a natural 1 doubles the mana and
    /// leaves the levels; anything else is the cast as priced.
    /// </summary>
    public static CastRoll RollToCast(int roll, int critThreshold, int levels, int manaCost)
    {
        if (roll >= critThreshold)
            return new CastRoll(roll, IsCrit: true, IsFumble: false, levels * CastCritLevelMultiplier, manaCost / CastCritManaDivisor);
        if (roll == 1)
            return new CastRoll(roll, IsCrit: false, IsFumble: true, levels, manaCost * CastFumbleManaMultiplier);
        return new CastRoll(roll, IsCrit: false, IsFumble: false, levels, manaCost);
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
    /// has happened by the time this returns. <paramref name="fromCleave"/>
    /// marks a hit the swing fanned out to beyond its primary target;
    /// <paramref name="type"/> is the element a wand's cast settled for its
    /// hits (None for a swing); <paramref name="onAlly"/> marks an area cast's
    /// hit on the attacker's own side; <paramref name="castFired"/> names the
    /// entries the cast paid for, which their hit-side halves read;
    /// <paramref name="fromPierce"/> marks a hit a shot was carried to;
    /// <paramref name="onDefendersTurn"/> marks a hit landing in the
    /// defender's own side's phase.
    /// </summary>
    public static AttackResolution Resolve(EventTable table, ActorState attacker, ActorState defender, Weapon weapon, int distanceUnits, Func<int> rollD20,
        bool fromCleave = false, DamageType type = DamageType.None, bool onAlly = false,
        ImmutableArray<string> castFired = default, bool fromPierce = false, bool onDefendersTurn = false)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(rollD20);

        var initial = DamagePayload.Initial(weapon, rollD20(), distanceUnits, type, fromCleave, onAlly, castFired, fromPierce, onDefendersTurn);
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
    /// A distance in logic units as the whole tiles it spans, rounded up —
    /// the conversion goes through <see cref="GameConstants.LogicUnitsPerTile"/>,
    /// the constant named for what it converts, never a bare rate — so a
    /// fraction past a tile boundary is the next tile: 96 units are three
    /// tiles and 97 are four. What Longshot prices; zero for nothing.
    /// </summary>
    public static int TilesSpanned(int distanceUnits)
        => (int)MathF.Ceiling(Math.Max(0, distanceUnits) / GameConstants.LogicUnitsPerTile);

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
