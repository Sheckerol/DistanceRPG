using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The §5.3 <c>tuning.json</c> scalars Phase 1 reads, with their compiled
/// fallbacks as the initialisers: a partial file is valid key by key and the
/// game runs with no file at all. Read once at startup, never hot-reloaded.
/// Ratios are integer divisors whose names say what they convert. Nothing the
/// golden tests pin belongs here.
/// </summary>
public sealed record Tuning
{
    // ---- Modifiers (§1.1) ----

    /// <summary>Acquired stacks a modifier may gain above its forged count: <c>cap(t) = forged(t) + AcquiredHeadroom</c>.</summary>
    public int AcquiredHeadroom { get; init; } = 5;

    /// <summary>What one stack is worth, per modifier — the §1.1 table. First-pass targets, not tuned values.</summary>
    public IReadOnlyDictionary<ModifierType, int> PerStack { get; init; } = new Dictionary<ModifierType, int>
    {
        [Brace] = 1,           // +1 retaliation
        [Block] = 3,           // +3 absorbed; flat by deliberate exception
        [CritWindow] = 1,      // +1 to the crit window
        [CritMultiplier] = 1,  // +1 to the multiplier (the base x2 is its Offset)
        [Cleave] = 1,          // +1 extra target
        [Charges] = 1,         // +1 throw per turn, on top of the 1 in its Offset
        [Longshot] = 1,        // +1 damage per tile beyond LongshotFreeTiles
        [Light] = 10,          // -10% of the weapon's cost, additive not compounding
        [Riposte] = 1,         // +1 counter per turn
        [Push] = 1,            // +1 tile displaced
        [Drag] = 1,            // +1 tile displaced, toward the wielder
        [Splitting] = 3,       // +3 of the target's Block ignored
        [Overwatch] = 1,       // +1 held shot
        [Opportunist] = 1,     // +1 free attack when a target leaves reach
        [Rout] = 1,            // +1 tile displaced, on everything a cleave caught
        [Pin] = 1,             // +1 Mire level on the target
        [Softening] = 3,       // +3 of the target's Block stripped for a round
        [CritWeaken] = 1,      // +1 Weakened level on a crit
        [CritSunder] = 1,      // +1 Sundered level on a crit
        [BlockWeaken] = 1,     // +1 Weakened level on a successful block
        [OnHitPoison] = 0,     // reserved: no behaviour in Phase 1
        [Resonant] = 10,       // -10% of all mana the weapon spends
        [Momentum] = 0,        // enchantment-only: never a modifier stack
    };

    /// <summary>What a modifier grants before its first stack; absent means 0.</summary>
    public IReadOnlyDictionary<ModifierType, int> Offset { get; init; } = new Dictionary<ModifierType, int>
    {
        [CritMultiplier] = 2,  // the base x2
        [Charges] = 1,         // the first throw
    };

    /// <summary>
    /// Forge-limit overrides; absent means 3 (a unique raises one modifier to x3
    /// and nothing is forged past it). The currency pair shares the limit of 1
    /// because they share a shape and exclude each other.
    /// </summary>
    public IReadOnlyDictionary<ModifierType, int> MaxForged { get; init; } = new Dictionary<ModifierType, int>
    {
        [Light] = 1,     // Light ceilings at x6, a 60% discount
        [Resonant] = 1,  // same shape, same limit
    };

    // ---- Statuses (§1.5) ----

    /// <summary>The constant effect of one level of each status. No decay constants: one level a round, universally.</summary>
    public IReadOnlyDictionary<StatusEffectType, int> EffectPerLevel { get; init; } = new Dictionary<StatusEffectType, int>
    {
        [StatusEffectType.Regeneration] = 1,   // 1 HP restored per level at the target's turn end
    };

    /// <summary>Surplus healing points that convert into one Ward level.</summary>
    public int OverhealPerWard { get; init; } = 5;

    /// <summary>
    /// Expected damage-over-time points bought per point of trigger mana: the
    /// §1.5 <c>DotManaPerDamage = 1/3</c>, stored as the integer divisor §5.3 asks
    /// for (<c>triggerCost = max(1, expectedTotal / DotDamagePerMana)</c>).
    /// </summary>
    public int DotDamagePerMana { get; init; } = 3;

    // ---- Appliers (§1.5) ----

    /// <summary>
    /// Per-enchantment percentage of the source number that becomes status
    /// levels, keyed by enchantment id; an entry here overrides the catalogue's.
    /// The four elements sit at different points of the 10–25% band so their
    /// burns are not identically paced.
    /// </summary>
    public IReadOnlyDictionary<string, int> ApplyPercent { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["flaming"] = 20,
        ["cold"] = 15,
        ["shocking"] = 25,
        ["acidic"] = 15,
    };

    // ---- Economy (§1.2, §1.3) ----

    /// <summary>
    /// Banked movement (logic units, 32 per tile) that buys one mana at end of
    /// turn: <c>manaRegained = unspentMovement / MovementUnitsPerMana</c>. Derived
    /// so a caster at Resonant x6 sustains one cast a round standing still.
    /// </summary>
    public int MovementUnitsPerMana { get; init; } = 10;

    /// <summary>Movement a weapon swap costs once a live enemy has been seen this turn; free otherwise.</summary>
    public int WeaponSwapCost { get; init; } = 20;

    /// <summary>Tiles a Longshot shot travels before it starts paying out.</summary>
    public int LongshotFreeTiles { get; init; } = 3;

    /// <summary>Whether area casts hit allies. Off until the kiting AI and the wand placement scorer exist.</summary>
    public bool FriendlyFireEnabled { get; init; } = false;

    /// <summary>Percentage of an area cast's damage an ally takes when friendly fire is on.</summary>
    public int FriendlyFireAllyPercent { get; init; } = 50;
}
