namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What an enchantment does when it fires (§3.3): the name an entry gives its
/// behaviour, which stays code while the entry is data (§5.7). The two innate
/// kinds are a staff's effect (<see cref="ApplyStatus"/>) and a wand's element
/// (<see cref="ElementalDamage"/>); <see cref="Vampiric"/> is the catalogue
/// entry the Efficiency dagger arrives with; the rest are the souls the
/// uniques carry (§1.5). Each kind is an entry in the per-event tables of
/// <see cref="EnchantmentBehaviours"/> — or, for the two a defender carries,
/// a compiled step of the damage pipeline — never a branch in a loop.
/// </summary>
public enum EffectKind
{
    /// <summary>Apply <see cref="EnchantmentDef.Applies"/> at <see cref="Enchantment.LevelsFor"/>(<see cref="EnchantmentDef.Potency"/>) levels: the staff innates.</summary>
    ApplyStatus,

    /// <summary>The hit carries <see cref="EnchantmentDef.DamageType"/> into the type chart: the wand innates.</summary>
    ElementalDamage,

    /// <summary>On damage dealt, heal the wielder a flat potency per tier, per instance.</summary>
    Vampiric,

    /// <summary>On damage dealt, leave <see cref="EnchantmentDef.Applies"/> (Bleeding) at the entry's percentage of the weapon's share of the hit.</summary>
    Serrated,

    /// <summary>On healing above full, bank the surplus in the hidden pool, which folds it into Ward.</summary>
    Overheal,

    /// <summary>On a shove at the wielder in its opponent's phase, negate it entirely (a defender's soul, compiled at (8,3)).</summary>
    Immovable,

    /// <summary>On damage dealt, carry the shot to the next body in line on a fresh roll.</summary>
    Piercing,

    /// <summary>The element named by <see cref="EnchantmentDef.DamageType"/> lingers as <see cref="EnchantmentDef.Applies"/> at the element's percentage of its damage, paid once per cast.</summary>
    LingeringElement,

    /// <summary>On lethal damage to the wielder, leave it at 1 HP instead (a defender's soul, compiled at (6,1)).</summary>
    Sturdy,

    /// <summary>On a kill, restore <see cref="EnchantmentDef.Potency"/> mana.</summary>
    Siphon,

    /// <summary>Attacks cost less movement — declared, inert until the amount is fixed (§3.3).</summary>
    Weightless,

    /// <summary>A kill refunds part of the swing's movement — declared, inert until the fraction is fixed (§3.3).</summary>
    Momentum,
}

/// <summary>Who an enchantment's effect may land on.</summary>
public enum TargetSide
{
    Ally,
    Enemy,
    Any,
}

/// <summary>
/// One row of §5.7 <c>enchantments.json</c>. An entry quotes a flat
/// <see cref="Trigger"/> cost or applies a status (<see cref="Applies"/>),
/// never both: a status applier prices its trigger off the levels it applies,
/// so a flat cost would decay into free. <see cref="Unique"/> says one thing
/// only — the tier is pinned at 1 — and its roll-eligibility lives in
/// <c>restricted.json</c> under <c>neverRolled</c>.
/// </summary>
/// <param name="Id">The stable string id content refers to it by.</param>
/// <param name="Name">The display name.</param>
/// <param name="Effect">What firing does.</param>
/// <param name="Targets">Who it may land on.</param>
/// <param name="Lock">Mana reserved while equipped (§3.3; read in Phase 3).</param>
/// <param name="Trigger">Flat mana per fire, or null for a status applier priced off its levels.</param>
/// <param name="Potency">The base source number an applier scales: a staff's levels per cast.</param>
/// <param name="ApplyPercent">Percentage of the source number that becomes levels; <see cref="Tuning.ApplyPercent"/> overrides it per id.</param>
/// <param name="Applies">The status an applier lands, or null.</param>
/// <param name="DamageType">The element an <see cref="EffectKind.ElementalDamage"/> entry carries, or null.</param>
/// <param name="Unique">Tier pinned at 1.</param>
public sealed record EnchantmentDef(
    string Id, string Name, EffectKind Effect, TargetSide Targets,
    int Lock, int? Trigger,
    int Potency, int ApplyPercent, StatusEffectType? Applies,
    DamageType? DamageType, bool Unique);

/// <summary>
/// An enchantment attached to a weapon: a catalogue entry at a tier. Immutable
/// and shared; a weapon's list of these is in attachment order, which is
/// gameplay (§1.7) and is never reordered.
/// </summary>
public sealed record Enchantment(EnchantmentDef Def, int Tier)
{
    /// <summary>Percentages are integers out of this.</summary>
    private const int Percent = 100;

    /// <summary>The tier a unique enchantment is pinned at, whatever it was attached at (§3.3).</summary>
    public const int UniqueTier = 1;

    private readonly int _tier = Def.Unique ? UniqueTier : Tier;

    /// <summary>
    /// The tier attached at — and <see cref="UniqueTier"/> for a unique
    /// whatever was asked (§3.3). The pin is applied where the tier is stored
    /// and again where it is read, so no path lifts it: not a content row
    /// asking for tier 3, not a <c>with</c> copy, not a later transfer or
    /// service. It is one of the two doors the <see cref="Unique"/> flag
    /// closes, and the only one on this type; the other is <c>neverRolled</c>,
    /// beside the catalogue (<see cref="EnchantmentCatalogue.Rollable"/>).
    /// </summary>
    public int Tier
    {
        get => Def.Unique ? UniqueTier : _tier;
        init => _tier = Def.Unique ? UniqueTier : value;
    }

    public string Id => Def.Id;

    /// <summary>Tier pinned at 1: a unique's magnitude reads off its source instead (§1.5).</summary>
    public bool Unique => Def.Unique;

    /// <summary>
    /// Mana this entry has spent into the tier it is on now — the remainder, not
    /// a cumulative total (§3.3: "mana it spends is its XP").
    /// <para>
    /// <strong>Tier is stored and XP is the remainder into it, which is a ruling
    /// against §3.5's wording rather than a reading of it.</strong> §3.5 says
    /// "Tier is derived from XP, not stored", exactly as <c>WeaponXp</c>,
    /// <c>HpXp</c> and <c>ManaXp</c> work. Those pools can replay from a
    /// cumulative total because a member's stat never moves; an enchantment's
    /// divisor is <em>whoever is holding it</em>, so replaying from cumulative XP
    /// would read a wizard's tier-4 soul as tier 2 in a fighter's hand and back
    /// again — depth would become a property of the wielder, which contradicts
    /// depth being "the circle burned into this weapon". Storing the tier and
    /// crediting at the stat that was actually spending keeps the ladder the
    /// weapon's, and makes a saved instance two ints.
    /// </para>
    /// </summary>
    public int Xp { get; init; }

    /// <summary>
    /// The mana this entry reserves while equipped: <c>Lock × Tier</c> (§3.3,
    /// "lock and potency both scale linearly with tier", so a tier-3 Arcane locks
    /// 90 and a tier-6 locks 180). Nothing clamps it — the tier needs no ceiling
    /// because max mana already is one, continuously and painfully.
    /// <para>
    /// There is deliberately no <c>EffectivePotency</c> beside it. <c>Tier</c> is
    /// already a factor inside <see cref="LevelsFor"/>, and every behaviour that
    /// wants "this entry's potency at its tier" passes
    /// <see cref="EnchantmentDef.Potency"/> through that one call; a second idiom
    /// for the same idea would invite feeding one into the other and silently
    /// squaring the tier.
    /// </para>
    /// </summary>
    public int EffectiveLock => Def.Lock * Tier;

    /// <summary>
    /// What leaving this tier costs (§3.3): <c>xpToNextTier = currentTierCost /
    /// INT</c>, where "the tier's current cost" is the pool it locks — the only
    /// cost a tier has. It is <see cref="Progression.XpToNext"/> again, a fifth
    /// application of the one threshold function, so INT divides a threshold and
    /// never multiplies a credit (§2.3: "there is no rate multiplier anywhere").
    /// A wizard therefore tiers an entry roughly four times as fast as a fighter,
    /// and the tier is what lifts lock and potency.
    /// </summary>
    /// <param name="stat">The wielder's INT, 1..4; a flat 1 for a thing with no nature.</param>
    public int XpToNextTier(int stat) => Progression.XpToNext(EffectiveLock, stat);

    /// <summary>
    /// This entry after <paramref name="manaSpent"/> more mana has gone through
    /// it (§3.3). The credit is the mana <em>spent</em> — the same figure §2.2
    /// already credits to max mana, one number and two ladders — and the replay
    /// is the definition: each tier costs the bar it just made, so the climb
    /// self-slows with no curve authored anywhere, exactly as
    /// <see cref="Progression.Pool"/> does.
    /// <para>
    /// A unique banks the spend and buys nothing (§3.3): it is pinned at tier 1,
    /// "they still accrue mana spent, since every trigger still costs; it simply
    /// buys nothing". It is not a dead stack, because the same spend still grows
    /// the wielder's max mana.
    /// </para>
    /// </summary>
    /// <param name="manaSpent">Mana this entry actually paid; never negative.</param>
    /// <param name="stat">The INT that was doing the spending, 1..4; a flat 1 for a thing with no nature, which is what the farm's grant and an enemy's spend both use.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="manaSpent"/> is negative, or <paramref name="stat"/> is outside 1..4.</exception>
    public Enchantment WithXp(int manaSpent, int stat)
    {
        if (manaSpent < 0)
            throw new ArgumentOutOfRangeException(nameof(manaSpent), manaSpent,
                "Mana spent is never negative: an enchantment's ladder is only ever credited.");
        if (stat < InnateStats.Low || stat > InnateStats.High)
            throw new ArgumentOutOfRangeException(nameof(stat), stat,
                $"A governing stat runs {InnateStats.Low}..{InnateStats.High}; a thing with no nature divides by {InnateStats.Low}.");

        if (Unique)
            return this with { Xp = Xp + manaSpent };

        int tier = Tier;
        int xp = Xp + manaSpent;
        int cost = StepFor(tier, stat);
        while (xp >= cost)
        {
            xp -= cost;
            tier++;
            cost = StepFor(tier, stat);
        }
        return this with { Tier = tier, Xp = xp };
    }

    /// <summary>
    /// The cost of leaving <paramref name="tier"/>, floored at 1. The floor lives
    /// here and never inside <see cref="XpToNextTier"/>, which stays the doc's
    /// line — the same split <see cref="Progression"/> already makes, for the same
    /// reason: this loop is the only caller a free step could hang.
    /// <see cref="ContentValidator.ValidateEnchantments"/> refuses a lock below 1
    /// at load, so the floor is a second door on a closed one.
    /// </summary>
    private int StepFor(int tier, int stat) => Math.Max(1, Progression.XpToNext(Def.Lock * tier, stat));

    /// <summary>
    /// Levels one application grants from <paramref name="sourceNumber"/> —
    /// a staff's <see cref="EnchantmentDef.Potency"/>, a hit's damage, healing
    /// overflowed — as §3.5 prices it: <c>ApplyPercent x source x Tier</c>,
    /// truncated; a unique reads its scale off the source alone
    /// (<see cref="LevelsOffSource"/>).
    /// </summary>
    public int LevelsFor(int sourceNumber) => Unique
        ? LevelsOffSource(sourceNumber)
        : Def.ApplyPercent * sourceNumber * Tier / Percent;

    /// <summary>
    /// Levels off <paramref name="sourceNumber"/> at this entry's percentage
    /// and nothing else — <c>ApplyPercent x source</c>, truncated, whatever
    /// the tier: the unique path of <see cref="LevelsFor"/>, for a magnitude
    /// that reads its scale off its source ("Serrated, and every element",
    /// phase-3-loot.md:1258-1261). A lingering element's burn is one: its
    /// levels are the element's percentage of the element damage, so the
    /// element's tier deepens the burn only through the damage it builds,
    /// never as a second factor on the levels — a tier-6 Flaming on a wand
    /// dealing 30 applies 3 levels at 10%, 6 damage over 3 turns (§1.5).
    /// </summary>
    public int LevelsOffSource(int sourceNumber) => Def.ApplyPercent * sourceNumber / Percent;

    /// <summary>
    /// Mana one fire costs before the wielder's discount: the flat
    /// <see cref="EnchantmentDef.Trigger"/> when the entry quotes one, else
    /// the DoT ladder — <c>max(1, L(L+1)/2 / DotDamagePerMana)</c> — so a
    /// status applier's price rises with what it buys and never decays into
    /// free (§1.5, §3.5).
    /// </summary>
    public int TriggerCostFor(int levels)
    {
        if (Def.Trigger is int flat)
            return flat;
        int expectedTotal = levels * (levels + 1) / 2;
        return Math.Max(1, expectedTotal / GameContent.Current.Tuning.DotDamagePerMana);
    }

    /// <summary>
    /// <see cref="TriggerCostFor"/> after <paramref name="w"/>'s Resonant
    /// discount — the same -10% a stack that prices its cast (§1.3) — floored
    /// at 1, because no enchantment fires free, ever.
    /// </summary>
    public int ResolvedTriggerCost(Weapon w, int levels)
    {
        ArgumentNullException.ThrowIfNull(w);
        return Math.Max(1, TriggerCostFor(levels) * (Percent - w.Modifiers.Value(ModifierType.Resonant)) / Percent);
    }
}
