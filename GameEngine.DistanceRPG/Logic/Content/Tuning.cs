using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The §5.3 <c>tuning.json</c> scalars Phase 1 reads, with their compiled
/// fallbacks as the initialisers: a partial file is valid key by key and the
/// game runs with no file at all. Read once at startup, never hot-reloaded.
/// Ratios are integer divisors whose names say what they convert. Nothing the
/// golden tests pin belongs here. The tables are per-key overlays on the
/// compiled instance: readers go through <see cref="Lookup"/>, so a file that
/// names one entry leaves the rest at their compiled values and a deserialiser
/// may replace a table wholesale without merging it.
/// </summary>
public sealed record Tuning
{
    /// <summary>
    /// Read <paramref name="key"/> from one of this instance's tables, falling
    /// back per key to the compiled table (§5.3: a missing key means the compiled
    /// default) and then to <paramref name="fallback"/> for a key in neither.
    /// </summary>
    public int Lookup<TKey>(Func<Tuning, IReadOnlyDictionary<TKey, int>> table, TKey key, int fallback)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(table);
        return table(this).TryGetValue(key, out var value)
            ? value
            : table(ContentDefaults.Tuning).GetValueOrDefault(key, fallback);
    }

    // ---- Modifiers (§1.1) ----

    /// <summary>Acquired stacks a modifier may gain above its forged count: <c>cap(t) = forged(t) + AcquiredHeadroom</c>.</summary>
    public int AcquiredHeadroom { get; init; } = 5;

    /// <summary>What one stack is worth, per modifier — the §1.1 table. First-pass targets, not tuned values. Complete: every member has a row, so no stack is ever worth nothing by omission.</summary>
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

    /// <summary>What a modifier grants before its first stack; absent falls back to this compiled table, then 0.</summary>
    public IReadOnlyDictionary<ModifierType, int> Offset { get; init; } = new Dictionary<ModifierType, int>
    {
        [CritMultiplier] = 2,  // the base x2
        [Charges] = 1,         // the first throw
    };

    /// <summary>
    /// Forge-limit overrides; absent falls back to this compiled table, then 3
    /// (a unique raises one modifier to x3 and nothing is forged past it). The
    /// currency pair shares the limit of 1 because they share a shape and
    /// exclude each other.
    /// </summary>
    public IReadOnlyDictionary<ModifierType, int> MaxForged { get; init; } = new Dictionary<ModifierType, int>
    {
        [Light] = 1,     // Light ceilings at x6, a 60% discount
        [Resonant] = 1,  // same shape, same limit
    };

    // ---- Statuses (§1.5) ----

    /// <summary>
    /// The constant effect of one level of each status — the §1.5 table's
    /// third column, the only dial a status has. Complete: every member has a
    /// row. No decay constants: one level a round, universally.
    /// </summary>
    public IReadOnlyDictionary<StatusEffectType, int> EffectPerLevel { get; init; } = new Dictionary<StatusEffectType, int>
    {
        [StatusEffectType.Regeneration] = 1,   // 1 HP restored per level at the target's turn end
        [StatusEffectType.Ward] = 1,           // 1 point absorbed per level, spent as it absorbs
        [StatusEffectType.Poison] = 1,         // 1 damage per level at the target's turn end
        [StatusEffectType.Mire] = 10,          // 10 percent of the movement budget cut per level; ten levels is all of it
        [StatusEffectType.Sundered] = 1,       // +1 taken per level from every weapon hit
        [StatusEffectType.Weakened] = 1,       // -1 dealt per level, floored at 1
        [StatusEffectType.Bleeding] = 1,       // 1 damage per level at the target's turn end
        [StatusEffectType.OverhealPool] = 1,   // 1 surplus healing point per level; OverhealPerWard of them make one Ward level
        [StatusEffectType.Searing] = 1,        // 1 damage per level at the target's turn end, whatever element lit it
        [StatusEffectType.Softened] = 1,       // 1 Block stripped per level for the round
    };

    /// <summary>Surplus healing points that convert into one Ward level.</summary>
    public int OverhealPerWard { get; init; } = 5;

    /// <summary>
    /// Expected damage-over-time points bought per point of trigger mana. This is
    /// the docs' <c>DotManaPerDamage = 1/3</c> (§1.5; settled.md fixes the value)
    /// stored as its integer reciprocal, because §5.3 makes every ratio an integer
    /// divisor named by its units: <c>triggerCost = max(1, expectedTotal / DotDamagePerMana)</c>.
    /// Loader note: the <c>tuning.json</c> key is <c>DotDamagePerMana</c>, this
    /// property's name; there is no <c>DotManaPerDamage</c> key, since 1/3 cannot be
    /// written as an integer under that name.
    /// </summary>
    public int DotDamagePerMana { get; init; } = 3;

    // ---- Appliers (§1.5) ----

    /// <summary>
    /// Per-enchantment percentage of the source number that becomes status
    /// levels, keyed by enchantment id; an entry here overrides the catalogue's.
    /// The four elements sit at different points of the 10–25% band so their
    /// burns are not identically paced; an element's percentage is also the
    /// burn its lingering unique feeds (§1.5). Serrated's row is the docs'
    /// <c>BleedPercent</c>: the share of the weapon's own damage a hit leaves
    /// as Bleeding levels — provisional, in the same band as the elements.
    /// </summary>
    public IReadOnlyDictionary<string, int> ApplyPercent { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["flaming"] = 20,
        ["cold"] = 15,
        ["shocking"] = 25,
        ["acidic"] = 15,
        ["serrated"] = 20,
    };

    // ---- Economy (§1.2, §1.3) ----

    /// <summary>
    /// Banked movement (logic units, 32 per tile) that buys one mana at end of
    /// turn: <c>manaRegained = unspentMovement / MovementUnitsPerMana</c>, an
    /// integer divisor rather than a fractional rate, so the file reads correctly
    /// and nothing drifts ("1 per point" would be 32 mana a tile). Derived from
    /// one calibration target rather than picked (§1.3), so it re-derives when
    /// anything it depends on moves: a caster at Resonant x6 carrying a tier-1
    /// enchantment sustains one cast a round while standing still:
    /// <c>(movementBudget - castCost) / (castMana + triggerMana, at x6)
    /// = (160 - 40) / (8 + 3) = 120 / 11 ~ 10.9 -> 10</c>.
    /// A fully banked turn is 16 mana; a turn spent on one cast banks 120 and
    /// pays 12, against the 11 spent: a hair above break-even, forever.
    /// Nothing below x6 sustains, and that cliff is deliberate.
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

    // ---- Progression (§2.1, §2.2) ----
    // StartingPool is §5.3's own key, in its Economy group. WeaponXpPerLevel is
    // named here, because §5.3 names the weapon ladder's 100 nowhere at all; the
    // Economy group is where it lands when Phase 5 writes tuning.json. They are
    // the two numbers a playtest would actually turn; the shape of a level's
    // effect (Progression.DamagePerLevels and the rest) stays compiled.

    /// <summary>
    /// The bar every point-growing pool opens on (§2.1): max HP, max mana and —
    /// from Phase 6a — a weapon's wear capacity alike, so the 25 is never a bare
    /// literal in three places. A point costs <c>currentMax / governingStat</c>
    /// from here, which is why a CON 1 wizard and a CON 4 fighter start level and
    /// diverge rather than starting apart. Below 1 there is no step to take, and
    /// <see cref="ContentValidator.ValidateTuning"/> refuses it at load.
    /// </summary>
    public int StartingPool { get; init; } = 25;

    /// <summary>
    /// The weapon ladder's bar per level (§2.2): the doc's
    /// <c>xpToNext(L) = 100 * L / governingStat</c> is
    /// <c>XpToNext(WeaponXpPerLevel * L, stat)</c>, so the ladder and the pools
    /// run on one function. Marked "tune later" where it is written and left
    /// unnamed there; this is the name.
    /// </summary>
    public int WeaponXpPerLevel { get; init; } = 100;
}
