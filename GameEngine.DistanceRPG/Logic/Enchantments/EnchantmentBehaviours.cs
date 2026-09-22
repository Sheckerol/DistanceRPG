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
/// its status (§1.3 "a cast is a hit"), a wand's element types the cast for
/// one payment of its trigger (§1.4) and a lingering element pays once for
/// every body the shape will catch; at step 4 of
/// <see cref="GameEvent.DamageTaken"/>, where a hit's enchantments add their
/// share beside the weapon's (§1.6) and the burn lands; on
/// <see cref="GameEvent.DamageDealt"/>, where the entries that ride a landed
/// hit fire — Serrated's wound, Vampiric's drink, Piercing's continuation; on
/// <see cref="GameEvent.Killed"/>, where Siphon refunds; and on
/// <see cref="GameEvent.HealingAboveFull"/>, where Overheal banks the surplus.
/// The two souls the <em>defender</em> carries — Sturdy, Immovable — are
/// compiled handlers at their own pipeline steps, since the loop is the
/// attacker's; each reads its own kind off the defender's weapon and pays
/// out of the defender's pool. A loop's step is its own on its event:
/// <see cref="EventTable.HandlersFor"/> prints the loop as one row per
/// attached entry at (step, attachment index), so attachment order reads as
/// the priority it is, and <see cref="EventTable.On{TPayload}"/> refuses a
/// compiled handler beside it on the step.
/// </summary>
public static class EnchantmentBehaviours
{
    /// <summary>The loop's name in every chain it runs in.</summary>
    public const string LoopName = "Enchantments";

    /// <summary>On Cast the loop is the chain: the innate's application, at the front.</summary>
    public static readonly HandlerPriority CastLoopPriority = new(0, 0);

    /// <summary>On DamageTaken the loop is pipeline step 4: after the weapon's share is closed at (3,9), before Block at (5,0).</summary>
    public static readonly HandlerPriority DamageTakenLoopPriority = new(4, 0);

    /// <summary>On DamageDealt, Killed and HealingAboveFull the loop is the front of the chain, ahead of the compiled steps that fold what it settled (the pool at (1,0)).</summary>
    public static readonly HandlerPriority DamageDealtLoopPriority = new(0, 0);
    public static readonly HandlerPriority KilledLoopPriority = new(0, 0);
    public static readonly HandlerPriority HealingAboveFullLoopPriority = new(0, 0);

    /// <summary>Sturdy at (6,1): after Ward at (6,0) has taken its share, before the (6,9) divider fixes what reaches HP.</summary>
    public static readonly HandlerPriority SturdyPriority = new(6, 1);

    /// <summary>Immovable at (8,3): after Push, Drag and Rout have settled the shove it refuses.</summary>
    public static readonly HandlerPriority ImmovablePriority = new(8, 3);

    /// <summary>
    /// What firing one entry does to a cast: hands back the payload with its
    /// effect appended, scaled to what it could pay, and its payment added —
    /// or untouched, when it could pay for nothing. <paramref name="manaLeft"/>
    /// is what the cast and the entries before it left of the caster's pool.
    /// </summary>
    public delegate CastPayload CastBehaviour(CastPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other);

    /// <summary>
    /// What firing one entry does to a hit — at step 4, where what it adds goes
    /// in <see cref="DamagePayload.EnchantmentShare"/> beside the weapon's share,
    /// never into it; on DamageDealt, where it settles what the landed hit sets
    /// off — scaled to what it could pay, with its payment added; or untouched,
    /// when it could pay for nothing. <paramref name="manaLeft"/> is what the
    /// entries before it left of the attacker's pool.
    /// </summary>
    public delegate DamagePayload DamageBehaviour(DamagePayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other);

    /// <summary>What firing one entry does to a kill: the mana it settles for the killer, paid and refunded once by the applier.</summary>
    public delegate KillPayload KillBehaviour(KillPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other);

    /// <summary>What firing one entry does to healing past full: the status ticks it grants the healed actor, and its payment.</summary>
    public delegate HealPayload HealBehaviour(HealPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other);

    /// <summary>The per-kind behaviours on Cast: a staff's effect applies its status, a wand's element types the cast, a lingering element pays for the burn once. A kind absent here does nothing on a cast.</summary>
    private static readonly IReadOnlyDictionary<EffectKind, CastBehaviour> OnCast = new Dictionary<EffectKind, CastBehaviour>
    {
        [EffectKind.ApplyStatus] = ApplyStatus,
        [EffectKind.ElementalDamage] = ElementOnCast,
        [EffectKind.LingeringElement] = LingerOnCast,
    };

    /// <summary>
    /// The per-kind behaviours at step 4 of a hit: the wands' element adds its
    /// own contribution beside the weapon's share, the lingering element lands
    /// its burn. A kind absent here adds nothing to a hit — the staff innates
    /// cast, and the souls that ride a landed hit fire on DamageDealt.
    /// </summary>
    private static readonly IReadOnlyDictionary<EffectKind, DamageBehaviour> OnDamageTaken = new Dictionary<EffectKind, DamageBehaviour>
    {
        [EffectKind.ElementalDamage] = ElementalDamage,
        [EffectKind.LingeringElement] = LingerOnHit,
    };

    /// <summary>The per-kind behaviours on DamageDealt — what rides a landed hit: Vampiric's drink and two souls. Weightless is declared and inert: what a cheaper attack costs is not fixed yet (§3.3), and AttackDeclared, the event its "fires on attack" names, is raised nowhere in Phase 1.</summary>
    private static readonly IReadOnlyDictionary<EffectKind, DamageBehaviour> OnDamageDealt = new Dictionary<EffectKind, DamageBehaviour>
    {
        [EffectKind.Vampiric] = Vampiric,
        [EffectKind.Serrated] = Serrated,
        [EffectKind.Piercing] = Piercing,
        [EffectKind.Weightless] = Inert,
    };

    /// <summary>The per-kind behaviours on Killed. Momentum is declared and inert: what fraction of the swing comes back is not fixed yet.</summary>
    private static readonly IReadOnlyDictionary<EffectKind, KillBehaviour> OnKilled = new Dictionary<EffectKind, KillBehaviour>
    {
        [EffectKind.Siphon] = Siphon,
        [EffectKind.Momentum] = InertOnKill,
    };

    /// <summary>The per-kind behaviours on HealingAboveFull: what banks the surplus.</summary>
    private static readonly IReadOnlyDictionary<EffectKind, HealBehaviour> OnHealingAboveFull = new Dictionary<EffectKind, HealBehaviour>
    {
        [EffectKind.Overheal] = Overheal,
    };

    /// <summary>
    /// The kinds whose behaviour heals the wielder — what the content
    /// validator asks, beside the status rows that restore HP, when it checks
    /// that Overheal sits beside a source of surplus healing (§1.5). A table
    /// beside the behaviour tables: Vampiric's drink is the one entry that
    /// heals, and a second is a row here as its behaviour is a row in
    /// <see cref="OnDamageDealt"/>.
    /// </summary>
    private static readonly IReadOnlySet<EffectKind> WielderHealers = new HashSet<EffectKind>
    {
        EffectKind.Vampiric,
    };

    /// <summary>Whether <paramref name="kind"/>'s behaviour heals its wielder (<see cref="WielderHealers"/>).</summary>
    public static bool HealsTheWielder(EffectKind kind) => WielderHealers.Contains(kind);

    /// <summary>
    /// The kinds whose behaviour lands the entry's own
    /// <see cref="EnchantmentDef.Applies"/> status — a staff's cast, Serrated's
    /// wound, a lingering element's burn — and so the kinds content must name
    /// a status for: what the validator asks instead of listing kinds. A new
    /// status-landing kind is a row here as its behaviour is a row in its
    /// event's table.
    /// </summary>
    private static readonly IReadOnlySet<EffectKind> StatusAppliers = new HashSet<EffectKind>
    {
        EffectKind.ApplyStatus,
        EffectKind.Serrated,
        EffectKind.LingeringElement,
    };

    /// <summary>Whether <paramref name="kind"/>'s behaviour lands the entry's <see cref="EnchantmentDef.Applies"/> status (<see cref="StatusAppliers"/>).</summary>
    public static bool AppliesStatus(EffectKind kind) => StatusAppliers.Contains(kind);

    /// <summary>
    /// The kinds whose behaviour reads an element entry on the same weapon —
    /// the one carrying the entry's <see cref="EnchantmentDef.DamageType"/>
    /// (<see cref="ElementFor"/>) — to say how deep it goes: the lingering
    /// element. Content must name an element that exists for these.
    /// </summary>
    private static readonly IReadOnlySet<EffectKind> ElementLingerers = new HashSet<EffectKind>
    {
        EffectKind.LingeringElement,
    };

    /// <summary>Whether <paramref name="kind"/>'s behaviour lingers the element its entry names (<see cref="ElementLingerers"/>).</summary>
    public static bool LingersAnElement(EffectKind kind) => ElementLingerers.Contains(kind);

    /// <summary>
    /// Whether <paramref name="kind"/> has a behaviour in the loop's table for
    /// <paramref name="evt"/>: what content validation asks of an entry — does
    /// it convert healing above full, say — without naming the kind. False for
    /// an event the loop does not run on.
    /// </summary>
    public static bool FiresOn(GameEvent evt, EffectKind kind) => evt switch
    {
        GameEvent.Cast => OnCast.ContainsKey(kind),
        GameEvent.DamageTaken => OnDamageTaken.ContainsKey(kind),
        GameEvent.DamageDealt => OnDamageDealt.ContainsKey(kind),
        GameEvent.Killed => OnKilled.ContainsKey(kind),
        GameEvent.HealingAboveFull => OnHealingAboveFull.ContainsKey(kind),
        _ => false,
    };

    /// <summary>A type is whole or nothing: the one "level" an element's cast fires at, so its flat trigger is paid in full or not at all.</summary>
    private const int OneType = 1;

    /// <summary>The one "level" the rules that fire once fire at: a shot carried on, a kill refunded, a life spared, a shove refused — whole or nothing, like a type. (Overheal, a rule too, grants levels and scales them to what it could pay.)</summary>
    private const int Once = 1;

    /// <summary>Register the loops on <paramref name="table"/>, on each event they run on, expanded for printing into the actor's attached entries; and the defender's two souls at their steps.</summary>
    public static void Register(EventTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.On<CastPayload>(GameEvent.Cast, CastLoopPriority, LoopName, CastLoop, expand: Attached);
        table.On<DamagePayload>(GameEvent.DamageTaken, DamageTakenLoopPriority, LoopName, DamageTakenLoop, expand: Attached);
        table.On<DamagePayload>(GameEvent.DamageDealt, DamageDealtLoopPriority, LoopName, DamageDealtLoop, expand: Attached);
        table.On<KillPayload>(GameEvent.Killed, KilledLoopPriority, LoopName, KilledLoop, expand: Attached);
        table.On<HealPayload>(GameEvent.HealingAboveFull, HealingAboveFullLoopPriority, LoopName, HealingAboveFullLoop, expand: Attached);

        table.On<DamagePayload>(GameEvent.DamageTaken, SturdyPriority, "Sturdy", Sturdy);
        table.On<DamagePayload>(GameEvent.DamageTaken, ImmovablePriority, "Immovable", Immovable);
    }

    /// <summary>The names the loop expands to for <paramref name="self"/>: its weapon's enchantments, in attachment order.</summary>
    public static IEnumerable<string> Attached(ActorState self)
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.EquippedWeapon?.Enchantments.Select(e => e.Id) ?? [];
    }

    // ── The loops ────────────────────────────────────────────────────────────

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
    /// The loop on DamageDealt: <c>self</c> is the attacker whose hit landed,
    /// <c>other</c> the defender it landed on. The hit's own payments were
    /// taken before this event was drained, so each entry sees the pool as it
    /// stands less what the entries before it settled here.
    /// </summary>
    public static DamagePayload DamageDealtLoop(DamagePayload payload, ActorState self, ActorState other)
    {
        var weapon = self.EquippedWeapon;
        if (weapon == null) return payload;
        foreach (var enchantment in weapon.Enchantments)
        {
            if (!OnDamageDealt.TryGetValue(enchantment.Def.Effect, out var fire)) continue;
            int manaLeft = Math.Max(0, self.Mana - payload.ManaToSpend);
            payload = fire(payload, enchantment, weapon, manaLeft, self, other);
        }
        return payload;
    }

    /// <summary>The loop on Killed: <c>self</c> is the killer, <c>other</c> the dead — or both the corpse, for a tick death, which the entries read off the null weapon.</summary>
    public static KillPayload KilledLoop(KillPayload payload, ActorState self, ActorState other)
    {
        var weapon = self.EquippedWeapon;
        if (weapon == null) return payload;
        foreach (var enchantment in weapon.Enchantments)
        {
            if (!OnKilled.TryGetValue(enchantment.Def.Effect, out var fire)) continue;
            int manaLeft = Math.Max(0, self.Mana - payload.ManaToSpend);
            payload = fire(payload, enchantment, weapon, manaLeft, self, other);
        }
        return payload;
    }

    /// <summary>The loop on HealingAboveFull: <c>self</c> is the actor healed past full, whose weapon's entries react to its surplus; <c>other</c> the source.</summary>
    public static HealPayload HealingAboveFullLoop(HealPayload payload, ActorState self, ActorState other)
    {
        var weapon = self.EquippedWeapon;
        if (weapon == null) return payload;
        foreach (var enchantment in weapon.Enchantments)
        {
            if (!OnHealingAboveFull.TryGetValue(enchantment.Def.Effect, out var fire)) continue;
            int manaLeft = Math.Max(0, self.Mana - payload.ManaToSpend);
            payload = fire(payload, enchantment, weapon, manaLeft, self, other);
        }
        return payload;
    }

    // ── Cast ─────────────────────────────────────────────────────────────────

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
            Fired = OrEmpty(payload.Fired).Add(enchantment.Id),
        };
    }

    /// <summary>
    /// The wand innates' behaviour on a cast (§1.4): the element pays its
    /// trigger once, for the whole cast and not once per target caught
    /// (settled): a six-target Nova pays what a one-target Blast pays. A type
    /// is whole or nothing, so the trigger is paid in full or the element is a
    /// non-event that types nothing, adds nothing and pays nothing
    /// (<see cref="PartialFire"/> over the one type). A hit carries exactly one
    /// type: the first element that fires types the cast — every hit the
    /// shape fans out to carries it into the attunement chart — and a second
    /// element on the same weapon (the Long Candle, §1.5) pays the same way
    /// for its own share at the hits, where it resolves against the chart by
    /// its own type. What fired is recorded for the hits to read.
    /// </summary>
    public static CastPayload ElementOnCast(CastPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        var type = enchantment.Def.DamageType ?? DamageType.None;
        if (type == DamageType.None)
            throw new InvalidOperationException($"'{enchantment.Id}' carries no damage type; the content validator lets no such element through.");

        var (fired, paid) = PartialFire(enchantment, weapon, OneType, manaLeft);
        if (fired <= 0) return payload;
        return payload with
        {
            Type = payload.Type == DamageType.None ? type : payload.Type,
            ManaToSpend = payload.ManaToSpend + paid,
            Fired = OrEmpty(payload.Fired).Add(enchantment.Id),
        };
    }

    /// <summary>
    /// A lingering element's behaviour on a cast (§1.5, the wand unique's own
    /// shape): the burn its element leaves on every body the shape catches is
    /// paid for once here, like the element's trigger, never once per target.
    /// The levels are the element's — its percentage of the element damage a
    /// hit of this cast carries, as the cast can foresee it: the wand's figure
    /// under the cast's roll when the element typed the cast, plus the
    /// element's own share — and the price is this entry's ladder over them,
    /// paid whole or not at all (<see cref="FireWhole"/>). Nothing lingers
    /// without its element on the weapon ahead of it, or when the cast could
    /// not pay the element: the burn is quiet and costs nothing.
    /// </summary>
    public static CastPayload LingerOnCast(CastPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        var element = ElementFor(enchantment, weapon);
        if (element == null || !Fired(payload.Fired, element.Id)) return payload;

        int levels = element.LevelsFor(ElementDamageOfCast(payload, element, weapon, self));
        var (fired, paid) = FireWhole(enchantment, weapon, levels, manaLeft);
        if (fired <= 0) return payload;
        return payload with
        {
            ManaToSpend = payload.ManaToSpend + paid,
            Fired = OrEmpty(payload.Fired).Add(enchantment.Id),
        };
    }

    // ── DamageTaken, step 4 ──────────────────────────────────────────────────

    /// <summary>
    /// The wand innates' behaviour on a hit, at step 4: what the element adds
    /// beside the weapon's share — its potency's levels, which Phase 1 prices
    /// at nothing (the tier's contribution and INT scaling are §3.3's) — goes
    /// into <see cref="DamagePayload.EnchantmentShare"/>, tracked apart from
    /// the weapon's the whole way down, and resolved against the target's
    /// attunement by the element's <em>own</em> type: on a two-element wand
    /// each element answers the chart separately, the one that typed the cast
    /// having already multiplied the weapon's share at (3,1). It pays nothing
    /// here: its trigger is the cast's, paid once for every hit the shape
    /// caught, and an element the cast could not pay — or a swing that never
    /// cast — adds nothing.
    /// </summary>
    public static DamagePayload ElementalDamage(DamagePayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        if (!Fired(payload.CastFired, enchantment.Id)) return payload;
        int levels = enchantment.LevelsFor(enchantment.Def.Potency);
        if (levels <= 0) return payload;
        int added = CombatBehaviours.AgainstAttunement(levels, enchantment.Def.DamageType ?? DamageType.None, other.Attunement);
        return added <= 0 ? payload : payload with { EnchantmentShare = payload.EnchantmentShare + added };
    }

    /// <summary>
    /// A lingering element's behaviour on a hit, at step 4: the burn lands.
    /// Its levels are the element's percentage of this hit's element damage —
    /// the weapon's share when the element typed the hit, plus the element's
    /// own share, each already answered by the chart — and the status it
    /// leaves is the entry's, keyed on the element when the status is (the
    /// row's flag): Burning leaves Searing/Flaming. It pays nothing here: the
    /// cast paid once, and a hit whose cast did not pay it lands no burn.
    /// </summary>
    public static DamagePayload LingerOnHit(DamagePayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        if (!Fired(payload.CastFired, enchantment.Id)) return payload;
        var element = ElementFor(enchantment, weapon);
        if (element == null) return payload;

        var type = element.Def.DamageType ?? DamageType.None;
        int share = element.LevelsFor(element.Def.Potency);
        int elementDamage = (payload.Type == type ? payload.WeaponShare : 0)
            + (share > 0 ? CombatBehaviours.AgainstAttunement(share, type, other.Attunement) : 0);
        int levels = element.LevelsFor(elementDamage);
        if (levels <= 0) return payload;

        var status = enchantment.Def.Applies
            ?? throw new InvalidOperationException($"'{enchantment.Id}' leaves no status; the content validator lets no such lingering element through.");
        var keyed = StatusRules.KeysOnElement(status) ? type : (DamageType?)null;
        return payload with
        {
            ApplyToDefender = OrEmpty(payload.ApplyToDefender).Add(new StatusApplication(status, keyed, levels)),
        };
    }

    // ── DamageDealt ──────────────────────────────────────────────────────────

    /// <summary>
    /// Vampiric fires on damage dealt and heals the wielder a flat amount per
    /// instance — its potency (one point) per tier, whatever the hit was worth
    /// (settled) — for its flat trigger, scaled to what it could pay. The heal
    /// is settled here and queued by the applier as a HealingReceived, so a
    /// surplus reaches HealingAboveFull and whatever banks it. A tick is not
    /// an instance: the status deals it, on the enemy's turn, and nothing
    /// fires on it.
    /// </summary>
    public static DamagePayload Vampiric(DamagePayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        if (payload.Dealt <= 0) return payload;
        var (heal, paid) = PartialFire(enchantment, weapon, enchantment.LevelsFor(enchantment.Def.Potency), manaLeft);
        if (heal <= 0) return payload;
        return payload with { HealToAttacker = payload.HealToAttacker + heal, ManaToSpend = payload.ManaToSpend + paid };
    }

    /// <summary>
    /// Serrated fires on damage dealt and leaves its status — Bleeding — on
    /// the defender, at its percentage (the docs' <c>BleedPercent</c>, the
    /// entry's row of <see cref="Tuning.ApplyPercent"/>) of the
    /// <em>weapon's</em> share of what was dealt (<see cref="DamagePayload.WeaponDealt"/>):
    /// it reads <c>Dealt</c>, after Block and before Ward, and of that the
    /// weapon's part alone — the wound is as deep as the blow that made it, and
    /// what an enchantment added beside the blade cut nobody deeper (§1.5,
    /// §1.6). A unique with no ladder of its own, it takes the weapon's — the
    /// share already carries the item and the wielder — and its price rides
    /// the same ladder: the DoT cost over the levels it lands, scaled to what
    /// it could pay. A hit that killed leaves no one to bleed, and costs nothing.
    /// </summary>
    public static DamagePayload Serrated(DamagePayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        if (!other.Alive) return payload;
        var status = enchantment.Def.Applies
            ?? throw new InvalidOperationException($"'{enchantment.Id}' applies no status; the content validator lets no such entry through.");
        var (levels, paid) = PartialFire(enchantment, weapon, enchantment.LevelsFor(payload.WeaponDealt), manaLeft);
        if (levels <= 0) return payload;

        var keyed = StatusRules.KeysOnElement(status) ? enchantment.Def.DamageType : null;
        return payload with
        {
            ApplyToDefender = OrEmpty(payload.ApplyToDefender).Add(new StatusApplication(status, keyed, levels)),
            ManaToSpend = payload.ManaToSpend + paid,
        };
    }

    /// <summary>
    /// Piercing fires on a hit and carries the shot to the next body in line
    /// beyond the one it struck, rolling the same hit again: settled here as
    /// <see cref="DamagePayload.Pierces"/>, for its flat trigger paid whole or
    /// not at all, and performed by the applier. Where the shot would go was
    /// read off the map at impact (<see cref="DamagePayload.NextInLine"/>), so
    /// the entry knows before it pays: a shot with nobody on its line — a lone
    /// target, bodies off the line or past the weapon's reach — has nothing to
    /// carry on to, and like any effect that scales to nothing it does not
    /// fire and pays nothing. The body the shot was carried to does not carry
    /// it on again.
    /// </summary>
    public static DamagePayload Piercing(DamagePayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        if (payload.FromPierce || payload.NextInLine is not { Alive: true }) return payload;
        var (fired, paid) = PartialFire(enchantment, weapon, Once, manaLeft);
        if (fired <= 0) return payload;
        return payload with { Pierces = true, ManaToSpend = payload.ManaToSpend + paid };
    }

    /// <summary>A declared soul with no behaviour yet: the hit passes through and nothing is paid, since nothing fired.</summary>
    public static DamagePayload Inert(DamagePayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
        => payload;

    // ── Killed ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Siphon fires on a kill and restores mana — its potency, fifteen — for
    /// its flat trigger of five, paid whole or not at all: net ten a kill,
    /// which is what makes the enchanted dagger the build that does not run
    /// dry. A tick death names no weapon and no wielder, and refunds nothing.
    /// </summary>
    public static KillPayload Siphon(KillPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        if (payload.Weapon == null) return payload;
        var (fired, paid) = PartialFire(enchantment, weapon, Once, manaLeft);
        if (fired <= 0) return payload;
        return payload with
        {
            ManaToSpend = payload.ManaToSpend + paid,
            ManaRestored = payload.ManaRestored + enchantment.LevelsFor(enchantment.Def.Potency),
        };
    }

    /// <summary>A declared soul with no behaviour yet: the kill passes through and nothing is paid.</summary>
    public static KillPayload InertOnKill(KillPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
        => payload;

    // ── HealingAboveFull ─────────────────────────────────────────────────────

    /// <summary>
    /// Overheal fires on healing past full and banks the surplus: it grants
    /// the hidden pool — every status row that converts on surplus healing —
    /// the overflow's points as levels, for its flat trigger scaled to what it
    /// could pay, and the pool's own handler after it in the chain folds the
    /// grant, with whatever was banked, into Ward and resets. The grant is a
    /// tick on the payload like any other: this never writes the pool itself.
    /// </summary>
    public static HealPayload Overheal(HealPayload payload, Enchantment enchantment, Weapon weapon, int manaLeft, ActorState self, ActorState other)
    {
        if (payload.Overflow <= 0) return payload;
        var (granted, paid) = PartialFire(enchantment, weapon, enchantment.LevelsFor(payload.Overflow), manaLeft);
        if (granted <= 0) return payload;

        var ticks = OrEmpty(payload.Ticks);
        foreach (var type in Enum.GetValues<StatusEffectType>())
        {
            var rule = StatusRules.Of(type);
            if (rule.Trigger != StatusTrigger.ReceivingHealing || rule.OnTrigger != OnTrigger.ConvertAndReset) continue;
            ticks = ticks.Add(new StatusTick(type, null, Damage: 0, Healing: 0, LevelsDelta: granted));
        }
        return payload with { Ticks = ticks, ManaToSpend = payload.ManaToSpend + paid };
    }

    // ── The defender's souls, compiled at their steps ────────────────────────

    /// <summary>
    /// Step 6.1: Sturdy on the defender refuses death. When what the hit would
    /// take — dealt, less what Ward swallowed at (6,0) — is lethal, it spares
    /// enough of it that the wielder survives at the entry's potency in HP
    /// (one), for its trigger of forty out of the defender's own pool, paid
    /// whole or not at all; the (6,9) divider takes the spared points off what
    /// reaches HP. It reads what would be <c>Taken</c> — only HP kills you —
    /// and leaves <c>Dealt</c> standing: the blow landed, the wielder simply
    /// did not die of it. Unaffordable, the wielder dies as anyone would.
    /// </summary>
    public static DamagePayload Sturdy(DamagePayload payload, ActorState self, ActorState other)
    {
        if (!other.Alive) return payload;
        var (entry, weapon) = DefenderSoul(other, EffectKind.Sturdy);
        if (entry == null || weapon == null) return payload;

        int survivesAt = Math.Max(1, entry.LevelsFor(entry.Def.Potency));
        int wouldTake = payload.Dealt - payload.WardSpent - payload.Spared;
        int spared = wouldTake - Math.Max(0, other.Hp - survivesAt);
        if (spared <= 0 || other.Hp - wouldTake > 0) return payload;   // not lethal: the shield takes it as any shield would

        var (fired, paid) = PartialFire(entry, weapon, Once, Math.Max(0, other.Mana - payload.DefenderManaToSpend));
        if (fired <= 0) return payload;
        return payload with { Spared = payload.Spared + spared, DefenderManaToSpend = payload.DefenderManaToSpend + paid };
    }

    /// <summary>
    /// Step 8.3: Immovable on the defender negates the shove Push, Drag or
    /// Rout settled at (8,0)–(8,2) — entirely, not by a tile — for its trigger
    /// of ten out of the defender's own pool, paid whole or not at all. It
    /// answers an attempt on the wielder in its opponent's phase (§1.5: "on
    /// the wielder's own turn's opponent-phase"): a line that holds when it is
    /// charged, as long as it can pay to hold. In its own side's phase — a
    /// counter to its swing, a brace it walked into — it is the one moving,
    /// and the shove lands like any other. A blow that kills moves nobody, so
    /// there is nothing to refuse and nothing is paid.
    /// </summary>
    public static DamagePayload Immovable(DamagePayload payload, ActorState self, ActorState other)
    {
        if (payload.Displace is not { Tiles: > 0 } || payload.OnDefendersTurn) return payload;
        if (!other.Alive || other.Hp <= payload.Taken) return payload;
        var (entry, weapon) = DefenderSoul(other, EffectKind.Immovable);
        if (entry == null || weapon == null) return payload;

        var (fired, paid) = PartialFire(entry, weapon, Once, Math.Max(0, other.Mana - payload.DefenderManaToSpend));
        if (fired <= 0) return payload;
        return payload with { Displace = null, DefenderManaToSpend = payload.DefenderManaToSpend + paid };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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

    /// <summary>
    /// The payment for an entry priced on its ladder over <paramref name="levels"/>
    /// that cannot fire for a part of them — a lingering element's burn,
    /// settled once at the cast for every body the shape will catch: the
    /// resolved cost for the levels, paid in full or not at all. Returns the
    /// levels and the mana; (0, 0) when the pool falls short.
    /// </summary>
    public static (int Levels, int Paid) FireWhole(Enchantment enchantment, Weapon weapon, int levels, int manaLeft)
    {
        ArgumentNullException.ThrowIfNull(enchantment);
        ArgumentNullException.ThrowIfNull(weapon);
        if (levels <= 0) return (0, 0);
        int wanted = enchantment.ResolvedTriggerCost(weapon, levels);
        return manaLeft >= wanted ? (levels, wanted) : (0, 0);
    }

    /// <summary>The element entry on <paramref name="weapon"/> that <paramref name="lingering"/> lingers: the one carrying its damage type, or null when the weapon has none (a transfer took it off).</summary>
    public static Enchantment? ElementFor(Enchantment lingering, Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(lingering);
        ArgumentNullException.ThrowIfNull(weapon);
        var type = lingering.Def.DamageType;
        if (type is null or DamageType.None) return null;
        return weapon.Enchantments.FirstOrDefault(e => e.Def.Effect == EffectKind.ElementalDamage && e.Def.DamageType == type);
    }

    /// <summary>
    /// The element damage a hit of <paramref name="payload"/>'s cast carries,
    /// as the cast can foresee it before any target is known: the weapon's
    /// figure under the cast's roll when <paramref name="element"/> typed the
    /// cast, plus the element's own share.
    /// </summary>
    private static int ElementDamageOfCast(CastPayload payload, Enchantment element, Weapon weapon, ActorState caster)
    {
        int typed = payload.Type == element.Def.DamageType
            ? CombatRules.RollToBase(payload.Roll, weapon.Damage, CombatRules.CritThreshold(caster), caster.Value(ModifierType.CritMultiplier)).Damage
            : 0;
        return typed + element.LevelsFor(element.Def.Potency);
    }

    /// <summary>The soul of <paramref name="kind"/> the defender carries, and the weapon carrying it; nulls when it holds none.</summary>
    private static (Enchantment? Entry, Weapon? Weapon) DefenderSoul(ActorState defender, EffectKind kind)
    {
        var weapon = defender.EquippedWeapon;
        var entry = weapon?.Enchantments.FirstOrDefault(e => e.Def.Effect == kind);
        return (entry, weapon);
    }

    private static bool Fired(ImmutableArray<string> fired, string id)
        => !fired.IsDefaultOrEmpty && fired.Contains(id);

    private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array)
        => array.IsDefault ? ImmutableArray<T>.Empty : array;
}
