using System.Collections.Immutable;
using static GameEngine.DistanceRPG.Logic.StatusEffectType;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The behaviours of the status table (§1.5, §1.7): what a level of each
/// status does, on the event its <see cref="StatusRules"/> row names, each a
/// pure <see cref="Handler{TPayload}"/> at its declared priority. On
/// <see cref="GameEvent.DamageTaken"/> the pipeline's own steps — Weakened (2),
/// Sundered (3), Ward (6), the crit riders (7) and BlockWeaken (7,1) — settle
/// the payload the turn system's applier writes. On
/// <see cref="GameEvent.TurnEnd"/> the ticks: a dead actor sheds (0,0), the
/// damage-over-time statuses tick (1,0) and the heal-over-time ticks (1,1),
/// each settling a <see cref="StatusTick"/>. On <see cref="GameEvent.RoundEnd"/>
/// the universal decay: the Reset rows go to zero (1,0), and everything that
/// did not tick at its own turn's end loses one level (9,0). On
/// <see cref="GameEvent.HealingAboveFull"/> the hidden pool converts (1,0).
/// Mire is the one status read where budgets are set rather than on an event
/// (<see cref="MiredBudget"/>). This is the old status tick as table rows and
/// handlers: no branch on a status's type anywhere in it, only on the row.
/// </summary>
public static class StatusBehaviours
{
    /// <summary>Percentages are integers out of this.</summary>
    private const int Percent = 100;

    // DamageTaken — the §1.6 step numbers.
    public static readonly HandlerPriority WeakenedPriority = new(2, 0);
    public static readonly HandlerPriority SunderedPriority = new(3, 0);
    public static readonly HandlerPriority WardPriority = new(6, 0);
    public static readonly HandlerPriority CritRidersPriority = new(7, 0);
    public static readonly HandlerPriority BlockWeakenPriority = new(7, 1);

    // TurnEnd and RoundEnd — the dead shed first, then the ticks, then the sweep.
    public static readonly HandlerPriority ShedPriority = new(0, 0);
    public static readonly HandlerPriority DoTTickPriority = new(1, 0);
    public static readonly HandlerPriority RegenTickPriority = new(1, 1);
    public static readonly HandlerPriority SoftenedResetPriority = new(1, 0);
    public static readonly HandlerPriority DecayPriority = new(9, 0);

    // HealingAboveFull — after the enchantment loop at (0,0), which is what grants the pool its levels.
    public static readonly HandlerPriority OverhealPoolPriority = new(1, 0);

    /// <summary>Register every status behaviour on <paramref name="table"/>.</summary>
    public static void Register(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.On<DamagePayload>(GameEvent.DamageTaken, WeakenedPriority, "Weakened", Weakened);
        table.On<DamagePayload>(GameEvent.DamageTaken, SunderedPriority, "Sundered", Sundered);
        table.On<DamagePayload>(GameEvent.DamageTaken, WardPriority, "Ward", Ward);
        table.On<DamagePayload>(GameEvent.DamageTaken, CritRidersPriority, "CritRiders", CritRiders);
        table.On<DamagePayload>(GameEvent.DamageTaken, BlockWeakenPriority, "BlockWeaken", BlockWeaken);

        table.On<TurnPayload>(GameEvent.TurnEnd, ShedPriority, "Shed", Shed);
        table.On<TurnPayload>(GameEvent.TurnEnd, DoTTickPriority, "DoTTick", DoTTick);
        table.On<TurnPayload>(GameEvent.TurnEnd, RegenTickPriority, "RegenTick", RegenTick);

        table.On<TurnPayload>(GameEvent.RoundEnd, ShedPriority, "Shed", Shed);
        table.On<TurnPayload>(GameEvent.RoundEnd, SoftenedResetPriority, "SoftenedReset", SoftenedReset);
        table.On<TurnPayload>(GameEvent.RoundEnd, DecayPriority, "Decay", Decay);

        table.On<HealPayload>(GameEvent.HealingAboveFull, OverhealPoolPriority, "OverhealPool", OverhealPool);
    }

    // ── DamageTaken: self is the attacker, other the defender ────────────────

    /// <summary>
    /// Step 2: the attacker's Weakened subtracts one per level from the weapon
    /// figure step 1 produced — after the multiplier or the halving — floored
    /// at 1 here, before Sundered adds: however deep it goes, a swing still
    /// lands for something.
    /// </summary>
    public static DamagePayload Weakened(DamagePayload payload, ActorState self, ActorState other)
    {
        int levels = self.StatusLevel(StatusEffectType.Weakened);
        if (levels <= 0) return payload;
        int amount = Math.Max(1, payload.Amount - levels * StatusRules.EffectPerLevel(StatusEffectType.Weakened));
        return payload with { Amount = amount };
    }

    /// <summary>
    /// Step 3: the defender's Sundered adds one per level to the weapon's
    /// figure — a flat add after the multiplier, never a multiplier, so it
    /// cannot compound with a crit; and to the weapon's share only, which step
    /// 3's divider then closes.
    /// </summary>
    public static DamagePayload Sundered(DamagePayload payload, ActorState self, ActorState other)
    {
        int levels = other.StatusLevel(StatusEffectType.Sundered);
        if (levels <= 0) return payload;
        return payload with { Amount = payload.Amount + levels * StatusRules.EffectPerLevel(StatusEffectType.Sundered) };
    }

    /// <summary>
    /// Step 6: the defender's Ward takes what was dealt, one point a level, up
    /// to its levels — temporary hit points, not armour, so a crit does not
    /// skip it and it may take a hit all the way to zero. What it swallowed
    /// was dealt; only the rest reaches HP, which the (6,9) divider closes.
    /// The applier spends the levels.
    /// </summary>
    public static DamagePayload Ward(DamagePayload payload, ActorState self, ActorState other)
    {
        int levels = other.StatusLevel(StatusEffectType.Ward);
        if (levels <= 0 || payload.Dealt <= 0) return payload;
        int capacity = levels * StatusRules.EffectPerLevel(StatusEffectType.Ward);
        return payload with { WardSpent = Math.Min(capacity, payload.Dealt) };
    }

    /// <summary>
    /// Step 7: on a crit — and only on a crit — the attacker's CritWeaken and
    /// CritSunder stacks land as Weakened and Sundered levels on the defender,
    /// one per stack, whatever Ward swallowed. The riders ride: they change
    /// nothing about the hit that applied them.
    /// </summary>
    public static DamagePayload CritRiders(DamagePayload payload, ActorState self, ActorState other)
    {
        if (!payload.IsCrit) return payload;
        var riders = OrEmpty(payload.ApplyToDefender);
        int weaken = self.Value(ModifierType.CritWeaken);
        if (weaken > 0) riders = riders.Add(new StatusApplication(StatusEffectType.Weakened, null, weaken));
        int sunder = self.Value(ModifierType.CritSunder);
        if (sunder > 0) riders = riders.Add(new StatusApplication(StatusEffectType.Sundered, null, sunder));
        return payload with { ApplyToDefender = riders };
    }

    /// <summary>
    /// Step 7.1: a successful block — Block absorbed something, which a crit
    /// never lets it — applies the defender's BlockWeaken stacks as Weakened
    /// levels on the attacker, one per stack: the same status the dagger's
    /// crit applies, on the shield's trigger.
    /// </summary>
    public static DamagePayload BlockWeaken(DamagePayload payload, ActorState self, ActorState other)
    {
        if (!payload.Blocked) return payload;
        int levels = other.Value(ModifierType.BlockWeaken);
        if (levels <= 0) return payload;
        return payload with
        {
            ApplyToAttacker = OrEmpty(payload.ApplyToAttacker).Add(new StatusApplication(StatusEffectType.Weakened, null, levels)),
        };
    }

    // ── TurnEnd and RoundEnd: self is the actor whose turn or round ended ────

    /// <summary>
    /// (0,0) on TurnEnd and RoundEnd: a dead actor sheds every status — regen
    /// cannot resurrect and poison does not tick on a corpse. The living pass
    /// through untouched; the handlers after this one only mean the living.
    /// </summary>
    public static TurnPayload Shed(TurnPayload payload, ActorState self, ActorState other)
    {
        if (self.Alive || self.StatusEffects.Count == 0) return payload;
        var ticks = OrEmpty(payload.Ticks).ToBuilder();
        foreach (var e in self.StatusEffects)
            if (!Settled(ticks, e))
                ticks.Add(new StatusTick(e.Type, e.Element, Damage: 0, Healing: 0, LevelsDelta: -e.Levels));
        return payload with { Ticks = ticks.ToImmutable() };
    }

    /// <summary>
    /// (1,0) on TurnEnd: every damage-over-time on the actor — the rows that
    /// tick at its turn's end — deals damage equal to its level, then loses
    /// one. The tick is the decay, so five levels are 5, 4, 3, 2, 1 and gone.
    /// </summary>
    public static TurnPayload DoTTick(TurnPayload payload, ActorState self, ActorState other)
        => TurnEndTick(payload, self, restoresHp: false);

    /// <summary>
    /// (1,1) on TurnEnd: the heal-over-time — the shipped Regeneration, the
    /// one row whose level restores HP instead of removing it — heals its
    /// level, then loses one. The heal is queued as HealingReceived, so what
    /// it cannot apply reaches HealingAboveFull like any other surplus.
    /// </summary>
    public static TurnPayload RegenTick(TurnPayload payload, ActorState self, ActorState other)
        => TurnEndTick(payload, self, restoresHp: true);

    /// <summary>
    /// (1,0) on RoundEnd: the rows that reset at the round's end go to zero
    /// whatever their level — Block stripped for a round is stripped for
    /// exactly one.
    /// </summary>
    public static TurnPayload SoftenedReset(TurnPayload payload, ActorState self, ActorState other)
    {
        if (!self.Alive) return payload;
        var ticks = OrEmpty(payload.Ticks).ToBuilder();
        foreach (var e in self.StatusEffects)
        {
            var rule = StatusRules.Of(e.Type);
            if (rule.Trigger != StatusTrigger.RoundEnd || rule.OnTrigger != OnTrigger.Reset || Settled(ticks, e)) continue;
            ticks.Add(new StatusTick(e.Type, e.Element, Damage: 0, Healing: 0, LevelsDelta: -e.Levels));
        }
        return payload with { Ticks = ticks.ToImmutable() };
    }

    /// <summary>
    /// (9,0) on RoundEnd: the universal decay. Every status loses one level a
    /// round; the ones that tick at their own turn's end already took theirs
    /// as that tick's decrement, so this sweep is for everything else, and it
    /// skips anything a handler before it settled this round.
    /// </summary>
    public static TurnPayload Decay(TurnPayload payload, ActorState self, ActorState other)
    {
        if (!self.Alive) return payload;
        var ticks = OrEmpty(payload.Ticks).ToBuilder();
        foreach (var e in self.StatusEffects)
        {
            if (!StatusRules.DecaysAtRoundEnd(e.Type) || Settled(ticks, e)) continue;
            ticks.Add(new StatusTick(e.Type, e.Element, Damage: 0, Healing: 0, LevelsDelta: -1));
        }
        return payload with { Ticks = ticks.ToImmutable() };
    }

    // ── HealingAboveFull: self is the actor healed past full ─────────────────

    /// <summary>
    /// (1,0) on HealingAboveFull: the rows that convert on surplus healing —
    /// the hidden overheal pool. Every <see cref="Tuning.OverhealPerWard"/>
    /// surplus points it holds, banked or granted earlier in this chain,
    /// become one Ward level, and the pool goes back to zero: it converts
    /// rather than damaging and resets rather than decrementing, and the
    /// remainder is lost. Without a pool the surplus is simply discarded.
    /// </summary>
    public static HealPayload OverhealPool(HealPayload payload, ActorState self, ActorState other)
    {
        if (!self.Alive) return payload;
        var ticks = OrEmpty(payload.Ticks);
        int perWard = Math.Max(1, GameContent.Current.Tuning.OverhealPerWard);

        foreach (var type in StatusRules.Table.Keys)
        {
            var rule = StatusRules.Of(type);
            if (rule.Trigger != StatusTrigger.ReceivingHealing || rule.OnTrigger != OnTrigger.ConvertAndReset) continue;

            int banked = self.StatusLevel(type);
            int granted = ticks.Where(t => t.Type == type).Sum(t => t.LevelsDelta);
            if (banked + granted <= 0) continue;

            var builder = ticks.RemoveAll(t => t.Type == type).ToBuilder();
            if (banked > 0)
                builder.Add(new StatusTick(type, null, Damage: 0, Healing: 0, LevelsDelta: -banked));
            int ward = (banked + granted) * StatusRules.EffectPerLevel(type) / perWard;
            if (ward > 0)
                builder.Add(new StatusTick(StatusEffectType.Ward, null, Damage: 0, Healing: 0, LevelsDelta: ward));
            ticks = builder.ToImmutable();
        }

        return payload with { Ticks = ticks };
    }

    // ── Read where budgets are set ───────────────────────────────────────────

    /// <summary>
    /// A movement budget after the actor's Mire: 10 percent gone per level and
    /// nothing left at ten or more — paralysis is the same counter crossing
    /// ten, released by the same decay. Read by the party member's turn start
    /// and the enemy's action, not on an event.
    /// </summary>
    public static float MiredBudget(ActorState actor, float budget)
    {
        ArgumentNullException.ThrowIfNull(actor);
        int cut = StatusRules.EffectPerLevel(Mire) * actor.StatusLevel(Mire);
        return budget * Math.Max(0, Percent - cut) / Percent;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>The shipped heal-over-time is the one table row whose level restores HP; every other tick removes it.</summary>
    private static bool RestoresHp(StatusEffectType type) => type == Regeneration;

    private static TurnPayload TurnEndTick(TurnPayload payload, ActorState self, bool restoresHp)
    {
        if (!self.Alive) return payload;
        var ticks = OrEmpty(payload.Ticks).ToBuilder();
        foreach (var e in self.StatusEffects)
        {
            var rule = StatusRules.Of(e.Type);
            if (rule.Trigger != StatusTrigger.TurnEnd || rule.OnTrigger != OnTrigger.TickAndDecrement) continue;
            if (RestoresHp(e.Type) != restoresHp || Settled(ticks, e)) continue;
            int effect = e.Levels * StatusRules.EffectPerLevel(e.Type);
            ticks.Add(new StatusTick(e.Type, e.Element,
                Damage: restoresHp ? 0 : effect, Healing: restoresHp ? effect : 0, LevelsDelta: -1));
        }
        return payload with { Ticks = ticks.ToImmutable() };
    }

    private static bool Settled(IEnumerable<StatusTick> ticks, StatusEffect e)
        => ticks.Any(t => t.Type == e.Type && t.Element == e.Element);

    private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array)
        => array.IsDefault ? ImmutableArray<T>.Empty : array;
}
