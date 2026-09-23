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

    /// <summary>
    /// The odds a non-caster drop arrives carrying an enchantment (§3.1): a
    /// staff's is fixed by its variant and a wand's is its element, so this is
    /// the martial roll and nothing else. Rolled as
    /// <c>NextInt(0, 999) &lt; DropEnchantChancePercent × 10</c>, an integer draw
    /// for the reason <see cref="FarmLadder.UniqueChancePermille"/> gives.
    /// <para>
    /// <strong>Provisional.</strong> §3.1 says "an uncommon roll on the loot
    /// stream" and names no number anywhere, so 10 is picked here to stop the
    /// drop path blocking on it and is expected to move after play — one of the
    /// two figures in Phase 3 that is a guess rather than a doc's.
    /// </para>
    /// </summary>
    public int DropEnchantChancePercent { get; init; } = 10;

    /// <summary>
    /// The one entry <see cref="ArcanePotency"/> is the dial for. The overlay is
    /// keyed by this <em>id</em> and never by <see cref="EffectKind.BonusDamage"/>,
    /// which is the rule <see cref="ApplyPercent"/> already follows: a dial names
    /// an entry, while which entries carry a kind is content's to say. A second
    /// bonus-damage row — a greater Arcane, a soul that adds untyped damage —
    /// therefore keeps the magnitude its content row authored, exactly as an
    /// element this file's percentage table does not name keeps its own.
    /// </summary>
    public const string ArcaneId = "arcane";

    /// <summary>
    /// Arcane's base potency: the untyped damage it adds to a hit, before its
    /// tier multiplies it through <see cref="Enchantment.LevelsFor"/> (§3.3). At
    /// <c>ApplyPercent</c> 100 a tier-1 Arcane adds 4 for a trigger of 8 and a
    /// tier-6 adds 24 for a lock of 180. It lays over the row of
    /// <see cref="ArcaneId"/> and no other.
    /// <para>
    /// <strong>Provisional.</strong> §3.3's starting-set table prices Arcane's
    /// lock and trigger and gives no potency column at all, so this is the second
    /// of Phase 3's two guessed figures. It sits here rather than in the
    /// catalogue row so a playtest can turn it without a content edit.
    /// </para>
    /// </summary>
    public int ArcanePotency { get; init; } = 4;

    // ---- Farming (§3.2) ----
    // The repeat-kill ladder's knobs. They are read through FarmLadder, which is
    // the only place the arithmetic over them lives.

    /// <summary>
    /// Turns a dummy at <c>DefeatCount</c> 0 takes to resurrect:
    /// <c>resurrectTurns(n) = max(ResurrectTurnsFloor, ResurrectTurnsBase − n)</c>
    /// (§3.2). Replaces the compiled <c>DummyResurrectTurns</c>, which was this
    /// same 10 with no ladder under it. settled.md keeps the front-loaded slope
    /// as designed and records <c>10 − n/2</c> as the fallback if the front half
    /// of a farm ever feels too samey.
    /// </summary>
    public int ResurrectTurnsBase { get; init; } = 10;

    /// <summary>
    /// The shortest a revival ever gets (§3.2). A floor rather than a curve
    /// because "a two-turn revival is not a fight, it is a treadmill": at three
    /// there is still room to reposition, swing at something else, or leave.
    /// Coupled to <see cref="ResurrectTurnsBase"/> — lowering either makes deep
    /// farming untenable long before the statline does.
    /// <see cref="ContentValidator.ValidateTuning"/> refuses a value below 1, or
    /// a dummy could revive the turn it died.
    /// </summary>
    public int ResurrectTurnsFloor { get; init; } = 3;

    /// <summary>
    /// The chance, as a percentage, that one farm cycle buys the drop a stack
    /// (§3.2). Flat rather than decaying, which settled.md accepts as designed:
    /// what ends a farm is the per-modifier <c>forged + 5</c> ceiling, so a
    /// falling rate would only make the same ending slower to reach.
    /// </summary>
    public int DefeatStackChance { get; init; } = 50;

    /// <summary>
    /// What a clean kill — a killing blow dealing at least the target's max HP —
    /// advances <c>DefeatCount</c> by (settled.md). It needs no counterweight
    /// because <c>DefeatCount</c> is already both reward and threat: it buys two
    /// cycles of drop quality <em>and</em> hands the dummy two cycles of statline.
    /// <see cref="ContentValidator.ValidateTuning"/> refuses a value below 1:
    /// <see cref="FarmLadder.DefeatAdvance"/> returns this figure verbatim, so a 0
    /// would leave a clean kill advancing nothing — strictly worse than the
    /// ordinary kill it is supposed to be worth double.
    /// </summary>
    public int CleanKillBonus { get; init; } = 2;

    /// <summary>
    /// The percentage of its own current damage or max HP a dummy gains per
    /// revival (§3.2; settled.md overrides the flat +5). Proportional, so a
    /// dagger dummy and an axe dummy grow at the same relative rate, and
    /// compounding, so the curve accelerates the longer a farm runs.
    /// <para>
    /// <strong>Provisional by the doc's own admission</strong> — "still a
    /// placeholder pending play"; what carries over from the flat version is the
    /// shape, not the number. At 15 an 18-damage axe dummy reaches 37 damage in
    /// five Damage rolls: 18, 21, 24, 28, 32, 37.
    /// </para>
    /// </summary>
    public int ReviveStepPercent { get; init; } = 15;

    // ---- Uniques (§3.2) ----
    // The logistic's three constants, all knobs: the ceiling sets how much
    // grinding can ever be worth, the midpoint moves the hot zone, and K
    // controls how sharply it arrives.

    /// <summary>
    /// The asymptote the unique chance climbs toward, as a percentage (§3.2). It
    /// is what keeps the deep farm a gamble rather than a long safe purchase:
    /// "no amount of grinding *guarantees* a unique". The asymptote does the work
    /// a hard cap used to.
    /// </summary>
    public int UniqueChanceCeilingPercent { get; init; } = 50;

    /// <summary>
    /// The <c>DefeatCount</c> the curve is steepest at, where it passes half the
    /// ceiling (§3.2). settled.md keeps it at 20 pending real numbers from
    /// <see cref="ReviveStepPercent"/> play, and fixes the direction of the
    /// coupling: if a twenty-times-revived dummy proves unsurvivable or trivial
    /// the midpoint moves to match, never the other way round.
    /// </summary>
    public int UniqueChanceMidpoint { get; init; } = 20;

    /// <summary>
    /// How sharply the curve arrives, as a percentage: the doc's
    /// <c>K = 0.26</c>, "tuned so DefeatCount 5 lands on ~1%". Percent-named
    /// because §5.3 makes every rate an integer with its units in its name rather
    /// than a bare float.
    /// </summary>
    public int UniqueChanceKPercent { get; init; } = 26;

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
    /// from Phase 6a — a weapon's wear capacity alike, so the 30 is never a bare
    /// literal in three places. A point costs <c>currentMax / governingStat</c>
    /// from here, which is why a CON 1 wizard and a CON 4 fighter start level and
    /// diverge rather than starting apart. Below 1 there is no step to take, and
    /// <see cref="ContentValidator.ValidateTuning"/> refuses it at load.
    /// <para>
    /// <strong>A caster's pool opens here too, on 30 and not on 160.</strong>
    /// settled.md had both figures live; the 160 in §1.3's derivations is the
    /// <em>movement</em> budget — <c>(160 - 40) / (8 + 3)</c> divides a staff's
    /// 40-unit movement cost out of <see cref="GameConstants.MaxDistance"/>,
    /// which is what <see cref="MovementUnitsPerMana"/> above is derived from —
    /// so there is no second starting-mana key and this one number is every
    /// pool's bar. Recorded under settled.md's Phase 2 implementation pass, with
    /// the consequence it accepts: a fresh caster gets one cast from a full
    /// pool. The pool an actor with no progression carries is
    /// <see cref="GameConstants.MaxMana"/>, and is a different number.
    /// </para>
    /// </summary>
    public int StartingPool { get; init; } = 30;

    /// <summary>
    /// The weapon ladder's bar per level (§2.2): the doc's
    /// <c>xpToNext(L) = 100 * L / governingStat</c> is
    /// <c>XpToNext(WeaponXpPerLevel * L, stat)</c>, so the ladder and the pools
    /// run on one function. Marked "tune later" where it is written and left
    /// unnamed there; this is the name.
    /// </summary>
    public int WeaponXpPerLevel { get; init; } = 100;
}
