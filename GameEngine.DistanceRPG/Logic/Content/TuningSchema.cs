namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The groups <c>tuning.json</c> is written under: §5.3's ten, in §5.3's order
/// (S:80-91), then the three groups of keys its starting set does not list, which
/// sit beside it. <see cref="Enchanter"/> and <see cref="Consumables"/> hold no key
/// yet: their keys arrive with the readers Phases 6a and 6b build
/// (<see cref="TuningSchema.Deferred"/>), and the file writes each as a heading
/// naming them, so §5.3's one screen stays whole.
/// </summary>
public enum TuningGroup
{
    Economy,
    Statuses,
    Appliers,
    Modifiers,
    Farming,
    Uniques,
    Dungeon,
    Enchanter,
    Inventory,
    Consumables,
    Progression,
    Combat,
    Drops,
}

/// <summary>
/// One key of <c>tuning.json</c>: a <see cref="Tuning"/> property by its name,
/// verbatim — the one naming source, so the file, the record and a save's hash
/// can never disagree about what a key is called — its §5.3 group, and the
/// one-line description the file writes above it. A provisional figure's
/// description starts <c>Provisional:</c>, the marker <see cref="Tuning"/>
/// otherwise carries only in C# comments.
/// </summary>
public abstract record TuningKey(string Name, TuningGroup Group, string Description);

/// <summary>A whole-number key and its clamp range: a value outside [<paramref name="Min"/>, <paramref name="Max"/>] is brought to the bound, with a warning.</summary>
public sealed record IntKey(string Name, TuningGroup Group, string Description, int Min, int Max,
    Func<Tuning, int> Get, Func<Tuning, int, Tuning> Set) : TuningKey(Name, Group, Description);

/// <summary>A true-or-false key.</summary>
public sealed record BoolKey(string Name, TuningGroup Group, string Description,
    Func<Tuning, bool> Get, Func<Tuning, bool, Tuning> Set) : TuningKey(Name, Group, Description);

/// <summary>
/// A table of whole numbers by name — namespaced scalars, never a list and never
/// nested, so "scalars only" (S:60) holds leaf by leaf (A4). A file names only
/// the entries it changes and the rest keep their compiled values: the
/// <see cref="Tuning.Lookup"/> overlay rule.
/// </summary>
/// <param name="Members">The enum's member names, in the enum's order; null for <c>ApplyPercent</c>, whose members are enchantment ids.</param>
/// <param name="Fallback">What the game's own reader falls back to for a member neither the tuning's table nor the compiled one names, so the file and the hash state what the game reads.</param>
/// <param name="Get">The effective table: every member through <see cref="Tuning.Lookup"/> with <paramref name="Fallback"/>; for <c>ApplyPercent</c>, every id the tuning's or the compiled table names.</param>
/// <param name="Set">The tuning with this table replaced by exactly the entries given.</param>
public sealed record TableKey(string Name, TuningGroup Group, string Description, int Min, int Max,
    IReadOnlyList<string>? Members,
    int Fallback,
    Func<Tuning, IReadOnlyDictionary<string, int>> Get,
    Func<Tuning, IReadOnlyDictionary<string, int>, Tuning> Set) : TuningKey(Name, Group, Description)
{
    /// <summary>What one member is, for a warning about one that is not: <c>modifier</c>, <c>status</c>, <c>enchantment</c>.</summary>
    public string MemberNoun { get; init; } = "member";

    /// <summary>A note the file writes beside one member's line — a provisional row says so, as a provisional key's description does.</summary>
    public IReadOnlyDictionary<string, string> MemberNotes { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Every <c>tuning.json</c> key (§5.3), as a compiled table: one entry per
/// <see cref="Tuning"/> property, in the order the file writes them, each with
/// its group, its clamp range and its description, and typed get/set delegates
/// — no reflection at runtime. The reader (<see cref="TuningJson.Read"/>)
/// clamps against it, the writer lays the file out from it, the hash walks it,
/// and a test holds it to the record key for key, so a property added to
/// <see cref="Tuning"/> without a key here fails the build's tests.
/// <para>
/// <strong>What it never holds</strong>: anything the golden tests pin — the
/// map generator's, the random streams' and the pathfinder's constants stay
/// compiled "permanently and without exception" (S:48-51), which a test asserts
/// against a hardcoded list — and nothing structural (S:57-60): the relations
/// are <c>restricted.json</c>'s, the classes and variants content's. And no
/// decay constant: a status loses one level a round, universally (S:83).
/// </para>
/// </summary>
public static class TuningSchema
{
    /// <summary>Every key, once, in the file's order: §5.3's groups in §5.3's order, then Progression, Combat and Drops.</summary>
    public static IReadOnlyList<TuningKey> Keys { get; } = BuildKeys();

    /// <summary>
    /// The two §5.3 groups whose keys wait for the phase that reads them — a key
    /// with no reader is a dial wired to nothing (bend (e)) — each with the phase
    /// and the keys it will bring. The file writes both as headings naming them.
    /// <c>EnchantmentWearCost</c> sits on open-questions.md's deposit question,
    /// which Phase 6a decides; nothing here decides it.
    /// </summary>
    public static IReadOnlyDictionary<TuningGroup, (string Phase, IReadOnlyList<string> Keys)> Deferred { get; } =
        new Dictionary<TuningGroup, (string Phase, IReadOnlyList<string> Keys)>
        {
            [TuningGroup.Enchanter] = ("Phase 6a", ["WearPerHit", "WearCapacityLadder", "EnchantmentWearCost", "GraftBaseChance"]),
            [TuningGroup.Consumables] = ("Phase 6b", ["PotionUseCost", "HealthPotionPercent", "ManaPotionPercent", "FarmYieldPerClear"]),
        };

    private static readonly IReadOnlyDictionary<string, TuningKey> ByName =
        Keys.ToDictionary(k => k.Name, StringComparer.Ordinal);

    /// <summary>
    /// The spellings the docs use for a key the code names otherwise, and why
    /// the code's is the key: never aliases — two answers to one question — but
    /// the warning's pointer at the key the reader meant (1g). §5.3's own
    /// <c>DotManaPerDamage</c>, <c>UniqueChanceCeiling</c> and <c>UniqueChanceK</c>
    /// and the rejected float form are the ones a designer reading the doc types.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Key, string? Why)> Aliases =
        new Dictionary<string, (string Key, string? Why)>(StringComparer.OrdinalIgnoreCase)
        {
            ["DotManaPerDamage"] = (nameof(Tuning.DotDamagePerMana), "its integer reciprocal: 3 means a third of a mana for each point of damage"),
            ["UniqueChanceCeiling"] = (nameof(Tuning.UniqueChanceCeilingPercent), "a percentage says so in its name"),
            ["UniqueChanceK"] = (nameof(Tuning.UniqueChanceKPercent), "a percentage says so in its name: K 0.26 is 26"),
            ["ManaPerMovementUnit"] = (nameof(Tuning.MovementUnitsPerMana), "a ratio is an integer divisor, never a fraction"),
            ["OverhealPerWardLevel"] = (nameof(Tuning.OverhealPerWard), null),
            ["StartingWearCapacity"] = (nameof(Tuning.StartingPool), "the one bar for HP, mana and wear capacity alike"),
            ["MaxDistance"] = (nameof(Tuning.MovementBudget), null),
            ["DummyResurrectTurns"] = (nameof(Tuning.ResurrectTurnsBase), null),
        };

    /// <summary>The key named <paramref name="name"/> exactly (ordinal: a key's case is part of its name), or null.</summary>
    public static TuningKey? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ByName.GetValueOrDefault(name);
    }

    /// <summary>
    /// The key a name that is not one points at, or null: a doc's spelling of a
    /// key the code names otherwise (<c>DotManaPerDamage</c> → <c>DotDamagePerMana</c>),
    /// a key in the wrong case, or a typo a quarter of the name away or closer
    /// (<c>MovmentBudget</c> → <c>MovementBudget</c>).
    /// </summary>
    public static string? Suggest(string unknown)
    {
        ArgumentNullException.ThrowIfNull(unknown);
        if (Alias(unknown) is { } alias)
            return alias.Key;
        return Nearest(unknown, Keys.Select(k => k.Name));
    }

    /// <summary>The key a doc's spelling points at and why the code's is the key, or null for a name no doc spells.</summary>
    internal static (string Key, string? Why)? Alias(string unknown)
        => Aliases.TryGetValue(unknown, out var alias) ? alias : null;

    /// <summary>The phase that brings <paramref name="name"/>, when it is one of <see cref="Deferred"/>'s keys; null otherwise.</summary>
    internal static string? DeferredPhase(string name)
    {
        foreach (var (phase, keys) in Deferred.Values)
            if (keys.Contains(name, StringComparer.OrdinalIgnoreCase))
                return phase;
        return null;
    }

    /// <summary>
    /// The candidate <paramref name="unknown"/> is a typo of: equal but for case,
    /// or within a quarter of the shorter name's length in edits (at least one).
    /// The nearest wins, the first in <paramref name="candidates"/>' order on a
    /// tie; null when nothing is that near.
    /// </summary>
    internal static string? Nearest(string unknown, IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(unknown);
        ArgumentNullException.ThrowIfNull(candidates);
        string lower = unknown.ToLowerInvariant();
        string? best = null;
        int bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (string.Equals(candidate, unknown, StringComparison.OrdinalIgnoreCase))
                return candidate;
            int distance = Edits(lower, candidate.ToLowerInvariant());
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        if (best is null) return null;
        int allowed = Math.Max(1, Math.Min(unknown.Length, best.Length) / 4);
        return bestDistance <= allowed ? best : null;
    }

    /// <summary>Levenshtein distance: the single-character inserts, deletes and substitutions between two strings.</summary>
    private static int Edits(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int substitute = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitute, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    // ── The keys, in the file's order ────────────────────────────────────────

    private static IReadOnlyList<TuningKey> BuildKeys() =>
    [
        // Economy (§1.2, §1.3)
        Int(nameof(Tuning.MovementUnitsPerMana), TuningGroup.Economy, 1, 1000, t => t.MovementUnitsPerMana, (t, v) => t with { MovementUnitsPerMana = v },
            "Banked movement, in logic units (32 a tile), that buys one mana at a turn's end: mana regained = unspent movement / "
            + "MovementUnitsPerMana - an integer divisor, never a rate. Derived rather than picked: a caster at Resonant x6 with a tier-1 "
            + "enchantment sustains one cast a round standing still, (MovementBudget 160 - a staff's 40) / (8 + 3 at Resonant x6) = 10.9 -> 10. "
            + "It does not follow MovementBudget or a cast cost on its own: when either moves, re-derive it by hand."),
        Int(nameof(Tuning.MovementBudget), TuningGroup.Economy, 1, 10000, t => t.MovementBudget, (t, v) => t with { MovementBudget = v },
            "How far each party member moves in a turn, in logic units (32 a tile): what every swing, cast and swap is priced against, "
            + "what a turn start refills and Mire cuts, the half of it a turn may bank, and what scales an enemy's costs (its 100 against "
            + "this). MovementUnitsPerMana does not follow it - re-derive the divisor by hand, (MovementBudget 160 - a staff's 40) / "
            + "(8 + 3 at Resonant x6) = 10.9 -> 10; left alone, a longer budget also banks more mana a turn."),
        Int(nameof(Tuning.StartingPool), TuningGroup.Economy, 1, 10000, t => t.StartingPool, (t, v) => t with { StartingPool = v },
            "The bar every pool opens on: max HP, max mana and (from Phase 6a) a weapon's wear capacity alike. A point then costs "
            + "currentMax / governing stat in XP, so every member starts level and diverges through play. 30 admits two Regeneration "
            + "locks, or exactly one Arcane."),

        // Statuses (§1.5)
        EnumTable<StatusEffectType>(nameof(Tuning.EffectPerLevel), TuningGroup.Statuses, 0, 1000, StatusRules.EffectFallback, "status",
            t => t.EffectPerLevel, (t, v) => t with { EffectPerLevel = v },
            "The effect of one level of each status, the only dial a status has: 1 damage a level for Poison, Bleeding and Searing, "
            + "1 HP for Regeneration, 1 point absorbed for Ward, 10 percent of the movement budget for Mire (ten levels paralyse), 1 a hit "
            + "for Sundered and Weakened, 1 Block for Softened. No decay dial: a status loses one level a round, and a damage-over-time's "
            + "tick is its decay - Searing burns for its level, then loses one - so a burn lasts as many rounds as it has levels, against "
            + "however long the fight runs."),
        Int(nameof(Tuning.OverhealPerWard), TuningGroup.Statuses, 1, 1000, t => t.OverhealPerWard, (t, v) => t with { OverhealPerWard = v },
            "Surplus healing points that make one Ward level: Overheal banks healing above full HP at this many points to one level - "
            + "a salvage, lossy on purpose, and it teaches no HP."),
        Int(nameof(Tuning.DotDamagePerMana), TuningGroup.Statuses, 1, 1000, t => t.DotDamagePerMana, (t, v) => t with { DotDamagePerMana = v },
            "Expected damage-over-time points one point of trigger mana buys: a status applier's trigger = max(1, its expected total / "
            + "DotDamagePerMana). The docs' DotManaPerDamage of 1/3, written as its integer reciprocal 3 because a ratio is an integer "
            + "divisor - there is no DotManaPerDamage key."),

        // Appliers (§1.5)
        ApplyPercentTable(),

        // Modifiers (§1.1)
        Int(nameof(Tuning.AcquiredHeadroom), TuningGroup.Modifiers, 1, 50, t => t.AcquiredHeadroom, (t, v) => t with { AcquiredHeadroom = v },
            "Acquired stacks a modifier may gain above its forging: the farm allowance, cap = forged + AcquiredHeadroom (forged + 5). "
            + "A well-farmed two-modifier weapon banks twice it, and a boss always pays less than that: BossStackOffset is raised to keep "
            + "it so."),
        EnumTable<ModifierType>(nameof(Tuning.PerStack), TuningGroup.Modifiers, 0, 1000, 0, "modifier",
            t => t.PerStack, (t, v) => t with { PerStack = v },
            "What one stack of each modifier is worth - the modifier table: Block absorbs 3 a stack, Light and Resonant take 10 percent "
            + "off, Splitting and Softening strip 3 Block, the rest count one a stack. Momentum and OnHitPoison never stack on a weapon."),
        EnumTable<ModifierType>(nameof(Tuning.Offset), TuningGroup.Modifiers, 0, 1000, ModifierRules.DefaultOffset, "modifier",
            t => t.Offset, (t, v) => t with { Offset = v },
            "What a modifier grants before its first stack: CritMultiplier's base x2 and Charges' first throw; nothing for the rest."),
        EnumTable<ModifierType>(nameof(Tuning.MaxForged), TuningGroup.Modifiers, 1, 3, ModifierRules.DefaultMaxForged, "modifier",
            t => t.MaxForged, (t, v) => t with { MaxForged = v },
            "The deepest a weapon may be forged in each modifier: 3, a unique's one raised modifier, and 1 for the currency pair Light "
            + "and Resonant. At most 3, and never below the deepest forging the loaded weapons carry: a lower limit is raised to it, with "
            + "a warning."),

        // Farming (§3.2)
        Int(nameof(Tuning.ResurrectTurnsBase), TuningGroup.Farming, 0, 1000, t => t.ResurrectTurnsBase, (t, v) => t with { ResurrectTurnsBase = v },
            "Turns a dummy at DefeatCount 0 takes to get up: resurrectTurns(n) = max(ResurrectTurnsFloor, ResurrectTurnsBase - n), so "
            + "each death brings the next revival a turn sooner."),
        Int(nameof(Tuning.ResurrectTurnsFloor), TuningGroup.Farming, 1, 1000, t => t.ResurrectTurnsFloor, (t, v) => t with { ResurrectTurnsFloor = v },
            "The shortest a revival ever gets - a two-turn revival is a treadmill, not a fight. At least 1, or a dummy could get up the "
            + "turn it died."),
        Int(nameof(Tuning.DefeatStackChance), TuningGroup.Farming, 0, 100, t => t.DefeatStackChance, (t, v) => t with { DefeatStackChance = v },
            "The chance, as a percentage, that one farm cycle buys the drop a stack. Flat on purpose: what ends a farm is the "
            + "forged + AcquiredHeadroom ceiling."),
        Int(nameof(Tuning.CleanKillBonus), TuningGroup.Farming, 1, 100, t => t.CleanKillBonus, (t, v) => t with { CleanKillBonus = v },
            "What a clean kill - a killing blow of at least the target's max HP - advances DefeatCount by: two cycles of drop quality, "
            + "and two of the dummy's statline."),
        Int(nameof(Tuning.ReviveStepPercent), TuningGroup.Farming, 0, 1000, t => t.ReviveStepPercent, (t, v) => t with { ReviveStepPercent = v },
            "Provisional: the percentage of its own damage or max HP a dummy gains per revival - proportional and compounding. At 15 an "
            + "18-damage axe dummy reaches 37 in five damage rolls."),

        // Uniques (§3.2)
        Int(nameof(Tuning.UniqueChanceCeilingPercent), TuningGroup.Uniques, 0, 100, t => t.UniqueChanceCeilingPercent, (t, v) => t with { UniqueChanceCeilingPercent = v },
            "The percentage the unique chance climbs toward and never reaches: no amount of grinding guarantees a unique."),
        Int(nameof(Tuning.UniqueChanceMidpoint), TuningGroup.Uniques, 0, 1000, t => t.UniqueChanceMidpoint, (t, v) => t with { UniqueChanceMidpoint = v },
            "The DefeatCount the unique curve is steepest at, where it passes half the ceiling. If a twenty-times-revived dummy proves "
            + "unsurvivable or trivial, this moves to match ReviveStepPercent, never the other way round."),
        Int(nameof(Tuning.UniqueChanceKPercent), TuningGroup.Uniques, 0, 1000, t => t.UniqueChanceKPercent, (t, v) => t with { UniqueChanceKPercent = v },
            "How sharply the unique curve arrives, as a percentage: the docs' K = 0.26 is 26, tuned so DefeatCount 5 lands near "
            + "1 percent."),

        // Dungeon (§4.3, §4.4)
        Int(nameof(Tuning.BossFloorMin), TuningGroup.Dungeon, 1, 99, t => t.BossFloorMin, (t, v) => t with { BossFloorMin = v },
            "The shallowest floor number a boss is rolled onto at entry, counting floors from 1. A dungeon that pins its own range - "
            + "the tutorial's floor 2 - ignores it."),
        Int(nameof(Tuning.BossFloorMax), TuningGroup.Dungeon, 1, 99, t => t.BossFloorMax, (t, v) => t with { BossFloorMax = v },
            "The deepest floor number a boss is rolled onto. Never shallower than BossFloorMin: raised to it, with a warning."),
        Int(nameof(Tuning.BossStackOffset), TuningGroup.Dungeon, 0, 99, t => t.BossStackOffset, (t, v) => t with { BossStackOffset = v },
            "The floors a boss's drop pays nothing for: its guaranteed stacks are max(0, boss floor - BossStackOffset), 1 at floor 5 and "
            + "6 at floor 10. Raised, with a warning, until the deepest boss - the range above, and every dungeon's pinned floor - pays "
            + "less than a well-farmed weapon banks (2 x AcquiredHeadroom)."),
        Int(nameof(Tuning.CooldownRunsPerFloor), TuningGroup.Dungeon, 0, 99, t => t.CooldownRunsPerFloor, (t, v) => t with { CooldownRunsPerFloor = v },
            "Runs a clear locks its dungeon for, per floor its boss was beaten on: the lock is boss floor x this, counted on the run "
            + "counter."),
        Int(nameof(Tuning.MercyFloor), TuningGroup.Dungeon, 1, 99, t => t.MercyFloor, (t, v) => t with { MercyFloor = v },
            "The floor number a failed run must have reached for the run to count (the mercy tick)."),
        Int(nameof(Tuning.DeathXpLossPercent), TuningGroup.Dungeon, 0, 100, t => t.DeathXpLossPercent, (t, v) => t with { DeathXpLossPercent = v },
            "Provisional: the percentage of this run's gain in each XP pool a wipe claws back. 100 erases the run's progress; 0 makes "
            + "death a pure loss of loot."),
        Int(nameof(Tuning.StairGatherTiles), TuningGroup.Dungeon, 1, 50, t => t.StairGatherTiles, (t, v) => t with { StairGatherTiles = v },
            "How near the stairs, in whole tiles, every living member must stand for the party to take them."),

        // Inventory (§4.4)
        Int(nameof(Tuning.PartyCarrySlots), TuningGroup.Inventory, GameConstants.PartySize, GameConstants.PartySize * GameConstants.MaxInventorySlots,
            t => t.PartyCarrySlots, (t, v) => t with { PartyCarrySlots = v },
            "The party's whole carry in weapon slots, shared evenly by its four members - 6 each at 24: slot 1 is the hand, the rest the "
            + "bag, loot and weapons alike. Rounded down to a multiple of four, at most 36 (nine a member, as far as the number keys "
            + "reach), and never less than the roster's starting loadouts need."),

        // Progression (§2.2) — not in §5.3's list
        Int(nameof(Tuning.WeaponXpPerLevel), TuningGroup.Progression, 1, 100000, t => t.WeaponXpPerLevel, (t, v) => t with { WeaponXpPerLevel = v },
            "The weapon ladder's bar per level: XP to the next level = WeaponXpPerLevel x level / governing stat."),

        // Combat (§1.2, §1.4) — not in §5.3's list
        Int(nameof(Tuning.WeaponSwapCost), TuningGroup.Combat, 0, 10000, t => t.WeaponSwapCost, (t, v) => t with { WeaponSwapCost = v },
            "Movement a weapon swap costs once a live enemy has been seen this turn; free otherwise."),
        Int(nameof(Tuning.LongshotFreeTiles), TuningGroup.Combat, 0, 100, t => t.LongshotFreeTiles, (t, v) => t with { LongshotFreeTiles = v },
            "Tiles a Longshot shot travels before its bonus damage starts paying out."),
        new BoolKey(nameof(Tuning.FriendlyFireEnabled), TuningGroup.Combat,
            "Whether area casts hit allies: off until the kiting AI and the wand placement scorer exist.",
            t => t.FriendlyFireEnabled, (t, v) => t with { FriendlyFireEnabled = v }),
        Int(nameof(Tuning.FriendlyFireAllyPercent), TuningGroup.Combat, 0, 100, t => t.FriendlyFireAllyPercent, (t, v) => t with { FriendlyFireAllyPercent = v },
            "The percentage of an area cast's damage an ally takes while friendly fire is on."),

        // Drops (§3.1, §3.3) — not in §5.3's list: Phase 3's two guessed figures
        Int(nameof(Tuning.DropEnchantChancePercent), TuningGroup.Drops, 0, 100, t => t.DropEnchantChancePercent, (t, v) => t with { DropEnchantChancePercent = v },
            "Provisional: the chance, as a percentage, that a martial drop arrives carrying an enchantment - a caster's is fixed by its "
            + "variant. The docs name no number."),
        Int(nameof(Tuning.ArcanePotency), TuningGroup.Drops, 0, 1000, t => t.ArcanePotency, (t, v) => t with { ArcanePotency = v },
            "Provisional: the untyped damage a tier-1 Arcane adds to a hit, before its tier multiplies it; it replaces the arcane "
            + "entry's own potency. The docs price Arcane's lock and trigger and give it no potency."),
    ];

    private static IntKey Int(string name, TuningGroup group, int min, int max, Func<Tuning, int> get, Func<Tuning, int, Tuning> set, string description)
        => new(name, group, description, min, max, get, set);

    /// <summary>A table keyed by an enum's members: every member, in the enum's order, read as the game reads it.</summary>
    private static TableKey EnumTable<TEnum>(string name, TuningGroup group, int min, int max, int fallback, string noun,
        Func<Tuning, IReadOnlyDictionary<TEnum, int>> table, Func<Tuning, IReadOnlyDictionary<TEnum, int>, Tuning> with, string description)
        where TEnum : struct, Enum
    {
        var members = Enum.GetValues<TEnum>();
        return new TableKey(name, group, description, min, max, Array.AsReadOnly(members.Select(m => m.ToString()).ToArray()), fallback,
            Get: t =>
            {
                var effective = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var member in members)
                    effective[member.ToString()] = t.Lookup(table, member, fallback);
                return effective;
            },
            Set: (t, values) =>
            {
                var typed = new Dictionary<TEnum, int>();
                foreach (var (member, value) in values)
                    typed[ParseMember<TEnum>(name, member)] = value;
                return with(t, typed);
            })
        {
            MemberNoun = noun,
        };
    }

    /// <summary><paramref name="member"/> as a member of <typeparamref name="TEnum"/>, by its exact name: never a number, never another case.</summary>
    private static TEnum ParseMember<TEnum>(string table, string member) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(member, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) && parsed.ToString() == member
            ? parsed
            : throw new ArgumentException($"'{member}' is not a {typeof(TEnum).Name} member, so it has no row in {table}.", nameof(member));

    /// <summary>
    /// <c>ApplyPercent</c>, keyed by enchantment id: the catalogue lays each
    /// entry the table names over that entry's own percentage, so the dial owns
    /// the number for every id it names (S:84, S:125). The effective table is
    /// every id the tuning's own table or the compiled one names.
    /// </summary>
    private static TableKey ApplyPercentTable() => new(
        nameof(Tuning.ApplyPercent), TuningGroup.Appliers,
        "Per enchantment id: the percentage of the source number (a hit's damage) that becomes status levels - the one number that "
        + "decides how many levels a source grants. An id named here sets that enchantment's percentage, whatever its own entry says. "
        + "The elements sit at different points of the 10-25 band so their burns are paced apart; an element's percentage is also the "
        + "burn its lingering unique feeds.",
        Min: 0, Max: 1000, Members: null, Fallback: 0,
        Get: t =>
        {
            var ids = new SortedSet<string>(t.ApplyPercent.Keys, StringComparer.Ordinal);
            ids.UnionWith(ContentDefaults.Tuning.ApplyPercent.Keys);
            var effective = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in ids)
                effective[id] = t.Lookup(x => x.ApplyPercent, id, 0);
            return effective;
        },
        Set: (t, values) => t with { ApplyPercent = new Dictionary<string, int>(values, StringComparer.Ordinal) })
    {
        MemberNoun = "enchantment",
        MemberNotes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["serrated"] = "Provisional: the docs' BleedPercent, the share of the weapon's own damage a hit leaves as Bleeding - picked in the elements' band, fixed by no doc.",
        },
    };
}
