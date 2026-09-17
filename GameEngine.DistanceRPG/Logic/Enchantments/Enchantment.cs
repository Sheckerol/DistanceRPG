namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What an enchantment does when it fires (§3.3). The two innate kinds are
/// live in Phase 1: <see cref="ApplyStatus"/> is a staff's effect and
/// <see cref="ElementalDamage"/> a wand's element. The rest are the souls the
/// uniques carry; each is one handler in the enchantment behaviours.
/// </summary>
public enum EffectKind
{
    /// <summary>Apply <see cref="EnchantmentDef.Applies"/> at <see cref="Enchantment.LevelsFor"/>(<see cref="EnchantmentDef.Potency"/>) levels: the staff innates.</summary>
    ApplyStatus,

    /// <summary>The hit carries <see cref="EnchantmentDef.DamageType"/> into the type chart: the wand innates.</summary>
    ElementalDamage,

    Vampiric,
    Serrated,
    Overheal,
    Immovable,
    Piercing,
    LingeringElement,
    Sturdy,
    Siphon,
    Weightless,
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

    public string Id => Def.Id;

    /// <summary>Tier pinned at 1: a unique's magnitude reads off its source instead (§1.5).</summary>
    public bool Unique => Def.Unique;

    /// <summary>
    /// Levels one application grants from <paramref name="sourceNumber"/> —
    /// a staff's <see cref="EnchantmentDef.Potency"/>, a hit's damage, healing
    /// overflowed — as §3.5 prices it: <c>ApplyPercent x source x Tier</c>,
    /// truncated; a unique reads its scale off the source alone.
    /// </summary>
    public int LevelsFor(int sourceNumber) => Unique
        ? Def.ApplyPercent * sourceNumber / Percent
        : Def.ApplyPercent * sourceNumber * Tier / Percent;

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
