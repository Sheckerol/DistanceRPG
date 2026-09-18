using System.Collections.Immutable;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The compiled steps of the §1.6 damage pipeline that are no status's own
/// behaviour — the roll, the two dividers, and Block — and the attack-shape
/// modifiers that change what a swing <em>is</em> at the step they act on:
/// Longshot pricing the distance into the base at (1,1), Splitting folded into
/// Block's arithmetic, Pin and Softening riding any hit at (7,2) and (7,3) —
/// plus the healing pipeline's one bookkeeping step. Each is a
/// <see cref="Handler{TPayload}"/> at its step number, so the chain
/// <see cref="EventTable.HandlersFor"/> prints reads like the doc: (1,0) roll →
/// (1,1) Longshot → (2) Weakened → (3) Sundered → (3,9) the weapon's share fixed
/// → (4) enchantments → (5,0) Block → (6) Ward → (6,9) the amount reaching HP
/// fixed → (7) crit riders, BlockWeaken, Pin, Softening → (8) displacement. On
/// DamageTaken <c>self</c> is the attacker and <c>other</c> the defender; on
/// HealingReceived <c>self</c> is the actor healed.
/// </summary>
public static class CombatBehaviours
{
    public static readonly HandlerPriority RollToBasePriority = new(1, 0);
    public static readonly HandlerPriority LongshotPriority = new(1, 1);
    public static readonly HandlerPriority FixWeaponSharePriority = new(3, 9);
    public static readonly HandlerPriority BlockPriority = new(5, 0);
    public static readonly HandlerPriority FixTakenPriority = new(6, 9);
    public static readonly HandlerPriority PinPriority = new(7, 2);
    public static readonly HandlerPriority SofteningPriority = new(7, 3);
    public static readonly HandlerPriority CapToMissingHpPriority = new(1, 0);

    /// <summary>Register the compiled steps on <paramref name="table"/>, in step order.</summary>
    public static void Register(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.On<DamagePayload>(GameEvent.DamageTaken, RollToBasePriority, "RollToBase", Step1_RollToBase);
        table.On<DamagePayload>(GameEvent.DamageTaken, LongshotPriority, "Longshot", Longshot);
        table.On<DamagePayload>(GameEvent.DamageTaken, FixWeaponSharePriority, "FixWeaponShare", Step3_FixWeaponShare);
        table.On<DamagePayload>(GameEvent.DamageTaken, BlockPriority, "Block", Step5_Block);
        table.On<DamagePayload>(GameEvent.DamageTaken, FixTakenPriority, "FixTaken", Step6_FixTaken);
        table.On<DamagePayload>(GameEvent.DamageTaken, PinPriority, "Pin", Pin);
        table.On<DamagePayload>(GameEvent.DamageTaken, SofteningPriority, "Softening", Softening);
        table.On<HealPayload>(GameEvent.HealingReceived, CapToMissingHpPriority, "CapToMissingHp", CapToMissingHp);
    }

    /// <summary>
    /// Step 1: the natural roll — made before the chain, so this is pure —
    /// becomes base damage. In the attacker's crit window (20 less their
    /// <see cref="CritWindow"/>; a natural 20 always) it is multiplied by their
    /// <see cref="CritMultiplier"/>, whose Offset carries the base x2; a natural
    /// 1 halves it, floored at 1. Weapon plus innate, resolved together.
    /// <see cref="DamagePayload.WeaponShare"/> shadows <see cref="DamagePayload.Amount"/>
    /// until step 3 closes it.
    /// </summary>
    public static DamagePayload Step1_RollToBase(DamagePayload payload, ActorState self, ActorState other)
    {
        var roll = CombatRules.RollToBase(
            payload.Roll, payload.Weapon.Damage, CombatRules.CritThreshold(self), self.Value(CritMultiplier));
        return payload with
        {
            Amount = roll.Damage,
            WeaponShare = roll.Damage,
            IsCrit = roll.Outcome == RollOutcome.Crit,
            Outcome = roll.Outcome,
        };
    }

    /// <summary>
    /// Step 1.1: the attacker's <see cref="ModifierType.Longshot"/> prices the
    /// distance into the base — its value per tile the hit travelled beyond
    /// the free tiles (<see cref="Tuning.LongshotFreeTiles"/>), the surface
    /// distance counted in whole tiles rounded up — and step 1 is re-taken
    /// over that base, so the crit multiplier and the natural-1 halving act on
    /// the resolved number: whatever distance has already made it. Nothing
    /// within the free tiles. On a four-tile spear only the final tile
    /// qualifies — fight at full extension and every stack pays, step in and
    /// none do; on a bow it is the curve the whole room is measured on. A
    /// reaction carries the distance it was earned at, so a brace at reach
    /// pays the same way a swing at reach does.
    /// </summary>
    public static DamagePayload Longshot(DamagePayload payload, ActorState self, ActorState other)
    {
        int bonus = LongshotBonus(self, payload.DistanceUnits);
        if (bonus <= 0) return payload;
        var roll = CombatRules.RollToBase(
            payload.Roll, payload.Weapon.Damage + bonus, CombatRules.CritThreshold(self), self.Value(CritMultiplier));
        return payload with { Amount = roll.Damage, WeaponShare = roll.Damage };
    }

    /// <summary>
    /// What Longshot adds to <paramref name="attacker"/>'s base at
    /// <paramref name="distanceUnits"/> (surface to surface, logic units): its
    /// value per tile beyond the free tiles, and zero within them or without it.
    /// </summary>
    public static int LongshotBonus(ActorState attacker, int distanceUnits)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        int perTile = attacker.Value(ModifierType.Longshot);
        if (perTile <= 0) return 0;
        int paying = Math.Max(0, CombatRules.TilesSpanned(distanceUnits) - GameContent.Current.Tuning.LongshotFreeTiles);
        return perTile * paying;
    }

    /// <summary>
    /// The divider after step 3 — "this is the WEAPON's damage, and only this".
    /// Closes the weapon's share, so step 4 adds beside it rather than to it and
    /// step 5 blocks off the weapon's share alone. Bookkeeping at (3,9): Sundered
    /// at (3,0) and the type chart at (3,1) run in front of it.
    /// </summary>
    public static DamagePayload Step3_FixWeaponShare(DamagePayload payload, ActorState self, ActorState other)
        => payload with { WeaponShare = payload.Amount };

    /// <summary>
    /// Step 5: the defender's <see cref="Block"/> — what is left of it against
    /// this swing, see <see cref="BlockAgainst"/> — absorbs up to its value off
    /// the weapon's share — never the enchantment share, never below 1 of the
    /// weapon's share — and is skipped entirely on a crit: not reduced, not
    /// halved, skipped, and skipped before the minimum-1 clamp rather than
    /// after. It sets the one <see cref="DamagePayload.Blocked"/> flag Riposte
    /// and BlockWeaken read, and fixes <see cref="DamagePayload.Dealt"/>: Block
    /// is the last thing that reduces damage.
    /// </summary>
    public static DamagePayload Step5_Block(DamagePayload payload, ActorState self, ActorState other)
    {
        int absorbed = 0;
        if (!payload.IsCrit)
        {
            int block = BlockAgainst(self, other);
            if (block > 0)
                absorbed = Math.Max(0, Math.Min(payload.WeaponShare - 1, block));
        }

        return payload with
        {
            Absorbed = absorbed,
            Blocked = !payload.IsCrit && absorbed > 0,
            Dealt = payload.WeaponShare + payload.EnchantmentShare - absorbed,
        };
    }

    /// <summary>
    /// The Block <paramref name="defender"/> has against <paramref name="attacker"/>'s
    /// swing: its value, less what the attacker's <see cref="Splitting"/>
    /// ignores — the axe splitting the shield: <em>this</em> weapon's swing goes
    /// through — and less what the <see cref="StatusEffectType.Softened"/> on the
    /// defender has stripped — the javelin's mark: <em>everyone's</em> swing goes
    /// through until the round ends and the status resets. Same per-stack
    /// value, opposite beneficiary. Never below zero.
    /// </summary>
    public static int BlockAgainst(ActorState attacker, ActorState defender)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        int stripped = defender.StatusLevel(StatusEffectType.Softened) * StatusRules.EffectPerLevel(StatusEffectType.Softened);
        return Math.Max(0, defender.Value(Block) - attacker.Value(Splitting) - stripped);
    }

    /// <summary>
    /// The divider after step 6 — "the amount reaching HP is fixed here".
    /// Closes <see cref="DamagePayload.Taken"/> = Dealt − WardSpent; bookkeeping
    /// at (6,9), so Ward at (6,0) and Sturdy at (6,1) slot in front of it.
    /// </summary>
    public static DamagePayload Step6_FixTaken(DamagePayload payload, ActorState self, ActorState other)
        => payload with { Taken = payload.Dealt - payload.WardSpent };

    /// <summary>
    /// Step 7.2: the attacker's <see cref="ModifierType.Pin"/> lands
    /// <see cref="StatusEffectType.Mire"/> on the defender on any hit — a level
    /// per stack, however the hit was delivered: the lance's brace, the bow's
    /// shot, a counter. The existing Mire, not a status of its own; it rides the
    /// hit and changes nothing about it. Refuses to let contact break.
    /// </summary>
    public static DamagePayload Pin(DamagePayload payload, ActorState self, ActorState other)
    {
        int levels = self.Value(ModifierType.Pin);
        if (levels <= 0) return payload;
        return payload with
        {
            ApplyToDefender = OrEmpty(payload.ApplyToDefender).Add(new StatusApplication(StatusEffectType.Mire, null, levels)),
        };
    }

    /// <summary>
    /// Step 7.3: the attacker's <see cref="ModifierType.Softening"/> lands
    /// <see cref="StatusEffectType.Softened"/> on the defender on any hit — its
    /// value in levels, three Block stripped per stack — which
    /// <see cref="BlockAgainst"/> takes off the defender's Block for everyone's
    /// swings until the round ends and the status resets. It lands after this
    /// hit's own Block step: the throw that marks the target is blocked as
    /// usual; the swings after it are not.
    /// </summary>
    public static DamagePayload Softening(DamagePayload payload, ActorState self, ActorState other)
    {
        int levels = self.Value(ModifierType.Softening);
        if (levels <= 0) return payload;
        return payload with
        {
            ApplyToDefender = OrEmpty(payload.ApplyToDefender).Add(new StatusApplication(StatusEffectType.Softened, null, levels)),
        };
    }

    /// <summary>
    /// The healing pipeline's one compiled step, at (1,0) on HealingReceived:
    /// what of the amount the healed actor can take — never past full — is
    /// <see cref="HealPayload.Applied"/>, and the rest is the
    /// <see cref="HealPayload.Overflow"/> that HealingAboveFull carries to
    /// whatever banks surplus. Anything reacting to the heal itself runs after.
    /// </summary>
    public static HealPayload CapToMissingHp(HealPayload payload, ActorState self, ActorState other)
    {
        int applied = Math.Clamp(payload.Amount, 0, Math.Max(0, self.MaxHp - self.Hp));
        return payload with { Applied = applied, Overflow = payload.Amount - applied };
    }

    private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array)
        => array.IsDefault ? ImmutableArray<T>.Empty : array;
}
