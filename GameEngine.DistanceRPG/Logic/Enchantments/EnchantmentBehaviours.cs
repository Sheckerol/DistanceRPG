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
/// loop runs on <see cref="GameEvent.Cast"/> here, where a staff's innate
/// applies its status (§1.3 "a cast is a hit"); the hit-side events take the
/// loop with the kinds that fire on them — the elements, the souls.
/// <see cref="EventTable.HandlersFor"/> prints the loop as one row per attached
/// entry, so attachment order reads as the priority it is.
/// </summary>
public static class EnchantmentBehaviours
{
    /// <summary>The loop's name in every chain it runs in.</summary>
    public const string LoopName = "Enchantments";

    /// <summary>On Cast the loop is the chain: the innate's application, at the front.</summary>
    public static readonly HandlerPriority CastLoopPriority = new(0, 0);

    /// <summary>
    /// What firing one entry does to a cast: hands back the payload with its
    /// effect appended, scaled to what it could pay, and its payment added —
    /// or untouched, when it could pay for nothing. <paramref name="manaLeft"/>
    /// is what the cast and the entries before it left of the caster's pool.
    /// </summary>
    public delegate CastPayload CastBehaviour(CastPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other);

    /// <summary>The per-kind behaviours on Cast. A kind absent here does nothing on a cast — the elements and the souls fire on hits.</summary>
    private static readonly IReadOnlyDictionary<EffectKind, CastBehaviour> OnCast = new Dictionary<EffectKind, CastBehaviour>
    {
        [EffectKind.ApplyStatus] = ApplyStatus,
    };

    /// <summary>Register the loop on <paramref name="table"/>, expanded for printing into the actor's attached entries.</summary>
    public static void Register(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.On<CastPayload>(GameEvent.Cast, CastLoopPriority, LoopName, CastLoop, expand: Attached);
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
