using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The compiled steps of the §1.6 damage pipeline that are no modifier's or
/// status's own behaviour: the roll, the two dividers, and Block — plus the
/// healing pipeline's one bookkeeping step. Each is a
/// <see cref="Handler{TPayload}"/> at its step number, so the chain
/// <see cref="EventTable.HandlersFor"/> prints reads like the doc: (1,0) roll →
/// (2) Weakened → (3) Sundered → (3,9) the weapon's share fixed → (4)
/// enchantments → (5,0) Block → (6) Ward → (6,9) the amount reaching HP fixed →
/// (7) crit riders → (8) displacement. On DamageTaken <c>self</c> is the
/// attacker and <c>other</c> the defender; on HealingReceived <c>self</c> is
/// the actor healed.
/// </summary>
public static class CombatBehaviours
{
    public static readonly HandlerPriority RollToBasePriority = new(1, 0);
    public static readonly HandlerPriority FixWeaponSharePriority = new(3, 9);
    public static readonly HandlerPriority BlockPriority = new(5, 0);
    public static readonly HandlerPriority FixTakenPriority = new(6, 9);
    public static readonly HandlerPriority CapToMissingHpPriority = new(1, 0);

    /// <summary>Register the compiled steps on <paramref name="table"/>, in step order.</summary>
    public static void Register(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.On<DamagePayload>(GameEvent.DamageTaken, RollToBasePriority, "RollToBase", Step1_RollToBase);
        table.On<DamagePayload>(GameEvent.DamageTaken, FixWeaponSharePriority, "FixWeaponShare", Step3_FixWeaponShare);
        table.On<DamagePayload>(GameEvent.DamageTaken, BlockPriority, "Block", Step5_Block);
        table.On<DamagePayload>(GameEvent.DamageTaken, FixTakenPriority, "FixTaken", Step6_FixTaken);
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
    /// The divider after step 3 — "this is the WEAPON's damage, and only this".
    /// Closes the weapon's share, so step 4 adds beside it rather than to it and
    /// step 5 blocks off the weapon's share alone. Bookkeeping at (3,9): Sundered
    /// at (3,0) and the type chart at (3,1) run in front of it.
    /// </summary>
    public static DamagePayload Step3_FixWeaponShare(DamagePayload payload, ActorState self, ActorState other)
        => payload with { WeaponShare = payload.Amount };

    /// <summary>
    /// Step 5: the defender's <see cref="Block"/> absorbs up to its value off the
    /// weapon's share — never the enchantment share, never below 1 of the
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
            int block = other.Value(Block);
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
    /// The divider after step 6 — "the amount reaching HP is fixed here".
    /// Closes <see cref="DamagePayload.Taken"/> = Dealt − WardSpent; bookkeeping
    /// at (6,9), so Ward at (6,0) and Sturdy at (6,1) slot in front of it.
    /// </summary>
    public static DamagePayload Step6_FixTaken(DamagePayload payload, ActorState self, ActorState other)
        => payload with { Taken = payload.Dealt - payload.WardSpent };

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
}
