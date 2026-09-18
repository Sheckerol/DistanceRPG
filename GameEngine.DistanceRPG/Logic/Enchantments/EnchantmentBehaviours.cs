using System.Collections.Immutable;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The enchantment group (§1.7, §3.3): one compiled handler, <see cref="LoopName"/>,
/// on each event enchantments fire on, iterating the acting actor's weapon's
/// enchantment list in attachment order — first on, first to fire, first to
/// be paid — and paying each trigger out of what the cast and the entries
/// before it left. What an entry does when it fires is its kind's behaviour,
/// looked up by <see cref="EffectKind"/> in the table of the event it fires
/// on: a new soul is one entry in one table, never an edit to the loop. The
/// loop runs on <see cref="GameEvent.Cast"/>, where a staff's innate applies
/// its status (§1.3 "a cast is a hit"), and at step 4 of
/// <see cref="GameEvent.DamageTaken"/>, where a hit's enchantments add their
/// share beside the weapon's (§1.6) — a table no kind is in yet: the wands'
/// element and the souls that ride a hit are entries in it when they land. A
/// loop's step is its own on its event: <see cref="EventTable.HandlersFor"/>
/// prints the loop as one row per attached entry at (step, attachment index),
/// so attachment order reads as the priority it is, and
/// <see cref="EventTable.On{TPayload}"/> refuses a compiled handler beside it
/// on the step.
/// </summary>
public static class EnchantmentBehaviours
{
    /// <summary>The loop's name in every chain it runs in.</summary>
    public const string LoopName = "Enchantments";

    /// <summary>On Cast the loop is the chain: the innate's application, at the front.</summary>
    public static readonly HandlerPriority CastLoopPriority = new(0, 0);

    /// <summary>On DamageTaken the loop is pipeline step 4: after the weapon's share is closed at (3,9), before Block at (5,0).</summary>
    public static readonly HandlerPriority DamageTakenLoopPriority = new(4, 0);

    /// <summary>
    /// What firing one entry does to a cast: hands back the payload with its
    /// effect appended, scaled to what it could pay, and its payment added —
    /// or untouched, when it could pay for nothing. <paramref name="manaLeft"/>
    /// is what the cast and the entries before it left of the caster's pool.
    /// </summary>
    public delegate CastPayload CastBehaviour(CastPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other);

    /// <summary>
    /// What firing one entry does to a hit at step 4: hands back the payload
    /// with what it adds put in <see cref="DamagePayload.EnchantmentShare"/> —
    /// beside the weapon's share, never into it — scaled to what it could pay,
    /// and its payment added; or untouched, when it could pay for nothing.
    /// <paramref name="manaLeft"/> is what the entries before it left of the
    /// attacker's pool.
    /// </summary>
    public delegate DamagePayload DamageBehaviour(DamagePayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other);

    /// <summary>The per-kind behaviours on Cast. A kind absent here does nothing on a cast — the elements and the souls fire on hits.</summary>
    private static readonly IReadOnlyDictionary<EffectKind, CastBehaviour> OnCast = new Dictionary<EffectKind, CastBehaviour>
    {
        [EffectKind.ApplyStatus] = ApplyStatus,
    };

    /// <summary>
    /// The per-kind behaviours at step 4 of a hit. No kind is in it yet: the
    /// wands' <see cref="EffectKind.ElementalDamage"/> and the souls that ride
    /// a hit are each an entry here when they land, and nothing in the loop.
    /// A kind absent here adds nothing to a hit — the staff innates cast.
    /// </summary>
    private static readonly IReadOnlyDictionary<EffectKind, DamageBehaviour> OnDamageTaken = new Dictionary<EffectKind, DamageBehaviour>();

    /// <summary>Register the loop on <paramref name="table"/>, on each event it runs on, expanded for printing into the actor's attached entries.</summary>
    public static void Register(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.On<CastPayload>(GameEvent.Cast, CastLoopPriority, LoopName, CastLoop, expand: Attached);
        table.On<DamagePayload>(GameEvent.DamageTaken, DamageTakenLoopPriority, LoopName, DamageTakenLoop, expand: Attached);
    }

    /// <summary>The names the loop expands to for <paramref name="self"/>: its weapon's enchantments, in attachment order.</summary>
    public static IEnumerable<string> Attached(ActorState self)
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.EquippedWeapon?.Enchantments.Select(e => e.Id) ?? [];
    }

    /// <summary>
    /// The loop on Cast: <c>self</c> is the caster, <c>other</c> the target.
    /// Each attached entry with a cast behaviour fires in list order, seeing
    /// what the cast's own mana and the entries before it left of the pool.
    /// </summary>
    public static CastPayload CastLoop(CastPayload payload, ActorState self, ActorState other)
    {
        var weapon = self.EquippedWeapon;
        if (weapon == null) return payload;
        foreach (var enchantment in weapon.Enchantments)
        {
            if (!OnCast.TryGetValue(enchantment.Def.Effect, out var fire)) continue;
            int manaLeft = Math.Max(0, self.Mana - payload.ManaCost - payload.ManaToSpend);
            payload = fire(payload, enchantment, weapon, manaLeft, self, other);
        }
        return payload;
    }

    /// <summary>
    /// The loop at step 4 of a hit: <c>self</c> is the attacker, <c>other</c>
    /// the defender. Each attached entry with a hit behaviour fires in list
    /// order, seeing what the entries before it left of the attacker's pool;
    /// the weapon's share is closed by the time it runs, so what an entry adds
    /// is tracked apart from it the whole way down. With no kind in the table
    /// the hit passes through untouched.
    /// </summary>
    public static DamagePayload DamageTakenLoop(DamagePayload payload, ActorState self, ActorState other)
    {
        var weapon = self.EquippedWeapon;
        if (weapon == null) return payload;
        foreach (var enchantment in weapon.Enchantments)
        {
            if (!OnDamageTaken.TryGetValue(enchantment.Def.Effect, out var fire)) continue;
            int manaLeft = Math.Max(0, self.Mana - payload.ManaToSpend);
            payload = fire(payload, enchantment, weapon, manaLeft, self, other);
        }
        return payload;
    }

    /// <summary>
    /// The staff innates' behaviour: apply the entry's status to the target at
    /// the levels its potency grants — doubled on a crit — as far as its
    /// trigger could be paid (<see cref="PartialFire"/>), carrying the entry's
    /// element when the status is keyed on one (the row's flag, not the type).
    /// </summary>
    public static CastPayload ApplyStatus(CastPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        var type = enchantment.Def.Applies
            ?? throw new InvalidOperationException($"'{enchantment.Id}' applies no status; the content validator lets no such applier through.");
        var (levels, paid) = PartialFire(enchantment, weapon, CastLevels(enchantment, payload.IsCrit), manaLeft);
        if (levels <= 0) return payload;

        var element = StatusRules.KeysOnElement(type) ? enchantment.Def.DamageType : null;
        return payload with
        {
            ApplyToTarget = OrEmpty(payload.ApplyToTarget).Add(new StatusApplication(type, element, levels)),
            ManaToSpend = payload.ManaToSpend + paid,
        };
    }

    /// <summary>The levels one cast of <paramref name="enchantment"/> applies: its potency's, doubled on a crit (§1.6).</summary>
    public static int CastLevels(Enchantment enchantment, bool isCrit)
    {
        ArgumentNullException.ThrowIfNull(enchantment);
        return enchantment.LevelsFor(enchantment.Def.Potency) * (isCrit ? CombatRules.CastCritLevelMultiplier : 1);
    }

    /// <summary>
    /// The trigger payment every kind on every event shares (settled): the
    /// entry wants its resolved trigger for <paramref name="levels"/> — the
    /// weapon's Resonant discount applied — and fires at
    /// <c>affordable / wanted</c> of full strength, paying exactly what it had,
    /// where <c>affordable</c> is what is left of the pool up to what it
    /// wanted. An effect that scales to nothing is a non-event: it does not
    /// fire, pays nothing, and passes the remainder down the list. Returns the
    /// levels to apply and the mana to pay; (0, 0) for a non-event.
    /// </summary>
    public static (int Levels, int Paid) PartialFire(Enchantment enchantment, Weapon weapon, int levels, int manaLeft)
    {
        ArgumentNullException.ThrowIfNull(enchantment);
        ArgumentNullException.ThrowIfNull(weapon);
        if (levels <= 0) return (0, 0);

        int wanted = enchantment.ResolvedTriggerCost(weapon, levels);   // never below 1: no enchantment fires free
        int affordable = Math.Clamp(manaLeft, 0, wanted);
        if (affordable <= 0) return (0, 0);

        int scaled = levels * affordable / wanted;
        return scaled <= 0 ? (0, 0) : (scaled, affordable);
    }

    private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array)
        => array.IsDefault ? ImmutableArray<T>.Empty : array;
}
