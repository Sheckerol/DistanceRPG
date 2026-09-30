namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The groups <c>tuning.json</c> is laid out under: §5.3's ten, in §5.3's order
/// (S:80-91), then the four groups of keys its starting set does not list, which
/// sit beside it. <see cref="Consumables"/> holds no key yet: its keys arrive with
/// the readers Phase 6b builds (<see cref="TuningSchema.Deferred"/>).
/// <see cref="Enchanter"/> holds what a hit wears and what a deposit costs, the
/// user's ruling of 2026-09-25, whose keys were declared ahead of Phase 6a's
/// enchanter and are played since its rules read them
/// (<see cref="TuningKey.Played"/>), and how many deposits the enchanter works on;
/// its last key arrives with its Phase 6a reader, the improvement roll. <see cref="Saves"/> holds how many save slots there are, the user's
/// ruling of the same day on the save model: the launcher's number, never a rule
/// of play's. The file's heading for each group names the keys still to come,
/// so §5.3's one screen stays whole.
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
    Saves,
}

/// <summary>
/// One key of <c>tuning.json</c>: a <see cref="Tuning"/> property by its name,
/// verbatim — the one naming source, so the file, the record and a save's hash
/// can never disagree about what a key is called — and its §5.3 group. What a
/// key means, and whether its number is provisional, is said once, in the
/// comment above it in <c>tuning.json</c>, the master copy: a second copy of
/// that prose here would drift from it.
/// </summary>
public abstract record TuningKey(string Name, TuningGroup Group)
{
    /// <summary>
    /// Whether a rule of play reads this key. True for every key but two kinds: one
    /// declared ahead of its reader — a number the user has ruled and wants in the
    /// file before the code that plays it exists: what a deposit costs was, until
    /// Phase 6a's enchanter read each — and one that no rule of play reads at all:
    /// <c>SaveSlots</c>, how many save slots the launcher offers. Such a key
    /// is read exactly as every key — the built-in copy must name it with a whole
    /// number, a copy beside the game merges it, clamps it and warns — and the log
    /// names it among the numbers a file changed (<see cref="TuningJson.Read"/>).
    /// But a save's hash leaves it out (<see cref="TuningJson.Hash"/>): the hash
    /// asks "are these the numbers the campaign was played under?", and a number
    /// nothing plays is none it was played under, so turning one never says the
    /// numbers changed. The change that gives a rule of play one to read sets it
    /// true.
    /// </summary>
    public bool Played { get; init; } = true;

    /// <summary>
    /// The save version whose tuning hash first covers this key (PHASE 6a
    /// DECISION 9): 1 for every key a rule played when version 1 was the format,
    /// and the version a key starts to play with for one that starts later —
    /// Phase 6a's, which play from version 2. A save's hash is of the keys played
    /// at the version it records (<see cref="SaveFile.TuningHashVersion"/>), so a
    /// campaign saved before a key played is never told the numbers changed for a
    /// number it was never played under: it is compared on its own version's keys,
    /// and its next save is hashed on the current version's.
    /// </summary>
    public int HashedFrom { get; init; } = 1;
}

/// <summary>A whole-number key and its clamp range: a value outside [<paramref name="Min"/>, <paramref name="Max"/>] is brought to the bound, with a warning.</summary>
public sealed record IntKey(string Name, TuningGroup Group, int Min, int Max,
    Func<Tuning, int> Get, Func<Tuning, int, Tuning> Set) : TuningKey(Name, Group);

/// <summary>A true-or-false key.</summary>
public sealed record BoolKey(string Name, TuningGroup Group,
    Func<Tuning, bool> Get, Func<Tuning, bool, Tuning> Set) : TuningKey(Name, Group);

/// <summary>
/// A table of whole numbers by name — namespaced scalars, never a list and never
/// nested, so "scalars only" (S:60) holds leaf by leaf (A4). A file beside the
/// game names only the entries it changes, and the rest keep the built-in
/// copy's numbers: the read merges per entry (<see cref="TuningJson.Read"/>).
/// </summary>
/// <param name="Members">The enum's member names, in the enum's order; null for <c>ApplyPercent</c>, whose members are enchantment ids.</param>
/// <param name="Get">The table as the game reads it: every member the tuning's table names, in the enum's order — every member, once the table is complete, which a tuning the game plays always is (<see cref="ContentValidator.RuleTableNamesEveryMember"/>); for <c>ApplyPercent</c>, every id it names, ordinally.</param>
/// <param name="Set">The tuning with this table replaced by exactly the entries given.</param>
public sealed record TableKey(string Name, TuningGroup Group, int Min, int Max,
    IReadOnlyList<string>? Members,
    Func<Tuning, IReadOnlyDictionary<string, int>> Get,
    Func<Tuning, IReadOnlyDictionary<string, int>, Tuning> Set) : TuningKey(Name, Group)
{
    /// <summary>What one member is, for a warning about one that is not: <c>modifier</c>, <c>status</c>, <c>enchantment</c>.</summary>
    public string MemberNoun { get; init; } = "member";
}

/// <summary>
/// Every <c>tuning.json</c> key (§5.3), as a table in code: one entry per
/// <see cref="Tuning"/> property, in the order the file lays them out, each with
/// its group and its clamp range, and typed get/set delegates — no reflection at
/// runtime. It holds no number the game plays: those are the file's. The
/// readers (<see cref="TuningJson.ReadBuiltIn"/>, <see cref="TuningJson.Read"/>)
/// clamp against it, the hash walks the keys a rule of play reads
/// (<see cref="TuningKey.Played"/>), and a test holds it to the record key
/// for key, so a property added to <see cref="Tuning"/> without a key here
/// fails the build's tests.
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
    /// <summary>Every key, once, in the file's order: §5.3's groups in §5.3's order, then Progression, Combat, Drops and Saves.</summary>
    public static IReadOnlyList<TuningKey> Keys { get; } = BuildKeys();

    /// <summary>
    /// The §5.3 keys that wait for the phase that reads them — a key with no
    /// reader is a dial wired to nothing (bend (e)) — by group, each with the
    /// phase and the keys it will bring. The file's headings name them. What a
    /// deposit costs is the one exception: the user ruled its numbers on
    /// 2026-09-25 and keeps a number they may revisit in the file, so its six were
    /// <see cref="TuningGroup.Enchanter"/> keys before their reader, and each has
    /// played since a Phase 6a rule reads it (<see cref="TuningKey.Played"/>).
    /// <c>WearPerHit</c> and <c>EnchanterWorksOn</c> arrived with their readers in
    /// Phase 6a; <c>GraftBaseChance</c> is not the ruling's number either, and
    /// waits here with the improvement roll that reads it.
    /// </summary>
    public static IReadOnlyDictionary<TuningGroup, (string Phase, IReadOnlyList<string> Keys)> Deferred { get; } =
        new Dictionary<TuningGroup, (string Phase, IReadOnlyList<string> Keys)>
        {
            [TuningGroup.Enchanter] = ("Phase 6a", ["GraftBaseChance"]),
            [TuningGroup.Consumables] = ("Phase 6b", ["PotionUseCost", "HealthPotionPercent", "ManaPotionPercent", "FarmYieldPerClear"]),
        };

    private static readonly IReadOnlyDictionary<string, TuningKey> ByName =
        Keys.ToDictionary(k => k.Name, StringComparer.Ordinal);

    /// <summary>
    /// The spellings the docs use for a key the code names otherwise, and why
    /// the code's is the key: never aliases — two answers to one question — but
    /// the warning's pointer at the key the reader meant (1g). §5.3's own
    /// <c>DotManaPerDamage</c>, <c>UniqueChanceCeiling</c> and <c>UniqueChanceK</c>
    /// and the rejected float form are the ones a designer reading the doc types;
    /// <c>WearCapacityLadder</c> and <c>EnchantmentWearCost</c> are the two
    /// placeholders the user's ruling of 2026-09-25 on the deposit replaced, which
    /// the docs named before it. Each why is shown on screen, in the HUD's font,
    /// so none has an apostrophe, a quote or a semicolon.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (string Key, string? Why)> Aliases =
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
            ["WearCapacityLadder"] = (nameof(Tuning.WearCapacityPerBar), "one point a full bar, never a ladder"),
            ["EnchantmentWearCost"] = (nameof(Tuning.FirstEnchantmentWear), "attaching enchantment n needs FirstEnchantmentWear + EnchantmentWearStep x (n - 1)"),
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
        Int(nameof(Tuning.MovementUnitsPerMana), TuningGroup.Economy, 1, 1000, t => t.MovementUnitsPerMana, (t, v) => t with { MovementUnitsPerMana = v }),
        Int(nameof(Tuning.MovementBudget), TuningGroup.Economy, 1, 10000, t => t.MovementBudget, (t, v) => t with { MovementBudget = v }),
        Int(nameof(Tuning.StartingPool), TuningGroup.Economy, 1, 10000, t => t.StartingPool, (t, v) => t with { StartingPool = v }),

        // Statuses (§1.5)
        EnumTable<StatusEffectType>(nameof(Tuning.EffectPerLevel), TuningGroup.Statuses, 0, 1000, "status",
            t => t.EffectPerLevel, (t, v) => t with { EffectPerLevel = v }),
        Int(nameof(Tuning.OverhealPerWard), TuningGroup.Statuses, 1, 1000, t => t.OverhealPerWard, (t, v) => t with { OverhealPerWard = v }),
        Int(nameof(Tuning.DotDamagePerMana), TuningGroup.Statuses, 1, 1000, t => t.DotDamagePerMana, (t, v) => t with { DotDamagePerMana = v }),

        // Appliers (§1.5)
        ApplyPercentTable(),

        // Modifiers (§1.1)
        Int(nameof(Tuning.AcquiredHeadroom), TuningGroup.Modifiers, 1, 50, t => t.AcquiredHeadroom, (t, v) => t with { AcquiredHeadroom = v }),
        EnumTable<ModifierType>(nameof(Tuning.PerStack), TuningGroup.Modifiers, 0, 1000, "modifier",
            t => t.PerStack, (t, v) => t with { PerStack = v }),
        EnumTable<ModifierType>(nameof(Tuning.Offset), TuningGroup.Modifiers, 0, 1000, "modifier",
            t => t.Offset, (t, v) => t with { Offset = v }),
        EnumTable<ModifierType>(nameof(Tuning.MaxForged), TuningGroup.Modifiers, 1, 3, "modifier",
            t => t.MaxForged, (t, v) => t with { MaxForged = v }),

        // Farming (§3.2)
        Int(nameof(Tuning.ResurrectTurnsBase), TuningGroup.Farming, 0, 1000, t => t.ResurrectTurnsBase, (t, v) => t with { ResurrectTurnsBase = v }),
        Int(nameof(Tuning.ResurrectTurnsFloor), TuningGroup.Farming, 1, 1000, t => t.ResurrectTurnsFloor, (t, v) => t with { ResurrectTurnsFloor = v }),
        Int(nameof(Tuning.DefeatStackChance), TuningGroup.Farming, 0, 100, t => t.DefeatStackChance, (t, v) => t with { DefeatStackChance = v }),
        Int(nameof(Tuning.CleanKillBonus), TuningGroup.Farming, 1, 100, t => t.CleanKillBonus, (t, v) => t with { CleanKillBonus = v }),
        Int(nameof(Tuning.ReviveStepPercent), TuningGroup.Farming, 0, 1000, t => t.ReviveStepPercent, (t, v) => t with { ReviveStepPercent = v }),

        // Uniques (§3.2)
        Int(nameof(Tuning.UniqueChanceCeilingPercent), TuningGroup.Uniques, 0, 100, t => t.UniqueChanceCeilingPercent, (t, v) => t with { UniqueChanceCeilingPercent = v }),
        Int(nameof(Tuning.UniqueChanceMidpoint), TuningGroup.Uniques, 0, 1000, t => t.UniqueChanceMidpoint, (t, v) => t with { UniqueChanceMidpoint = v }),
        Int(nameof(Tuning.UniqueChanceKPercent), TuningGroup.Uniques, 0, 1000, t => t.UniqueChanceKPercent, (t, v) => t with { UniqueChanceKPercent = v }),

        // Dungeon (§4.3, §4.4)
        Int(nameof(Tuning.BossFloorMin), TuningGroup.Dungeon, 1, 99, t => t.BossFloorMin, (t, v) => t with { BossFloorMin = v }),
        Int(nameof(Tuning.BossFloorMax), TuningGroup.Dungeon, 1, 99, t => t.BossFloorMax, (t, v) => t with { BossFloorMax = v }),
        Int(nameof(Tuning.BossStackOffset), TuningGroup.Dungeon, 0, 99, t => t.BossStackOffset, (t, v) => t with { BossStackOffset = v }),
        Int(nameof(Tuning.CooldownRunsPerFloor), TuningGroup.Dungeon, 0, 99, t => t.CooldownRunsPerFloor, (t, v) => t with { CooldownRunsPerFloor = v }),
        Int(nameof(Tuning.MercyFloor), TuningGroup.Dungeon, 1, 99, t => t.MercyFloor, (t, v) => t with { MercyFloor = v }),
        Int(nameof(Tuning.DeathXpLossPercent), TuningGroup.Dungeon, 0, 100, t => t.DeathXpLossPercent, (t, v) => t with { DeathXpLossPercent = v }),
        Int(nameof(Tuning.StairGatherTiles), TuningGroup.Dungeon, 1, 50, t => t.StairGatherTiles, (t, v) => t with { StairGatherTiles = v }),

        // Enchanter (§6.1, §6.2) — what a hit wears, what a deposit costs, the
        // user's ruling of 2026-09-25, and how many deposits the enchanter works on:
        // the ruling's keys were declared ahead of their reader, Phase 6a's
        // enchanter, and each is played, hashed from the save's version 2, since a
        // rule of it reads it — the wear a hit banks and the capacity a bar adds from
        // Phase 6a.1, the slots, the price and the queue's worked few from 6a.3a. A
        // hit wears at most what a magnitude clamps at, and 0 wears nothing (a dial:
        // nothing can then be enchanted). A capacity's range is StartingPool's, the
        // bar it opens on; a step may be 0 (every slot open at once, every
        // enchantment priced as the first); a bar adds at most what a magnitude
        // clamps at. A weapon holds no fewer slots than it arrives filling, and at
        // most twice the docs' five. The enchanter works on one deposit at least — 0
        // would freeze every service — and on no more than the most enchantments a
        // weapon holds.
        PlayedFrom2(Int(nameof(Tuning.WearPerHit), TuningGroup.Enchanter, 0, 1000, t => t.WearPerHit, (t, v) => t with { WearPerHit = v })),
        PlayedFrom2(Int(nameof(Tuning.WearCapacityPerBar), TuningGroup.Enchanter, 0, 1000, t => t.WearCapacityPerBar, (t, v) => t with { WearCapacityPerBar = v })),
        PlayedFrom2(Int(nameof(Tuning.FirstSlotCapacity), TuningGroup.Enchanter, 1, 10000, t => t.FirstSlotCapacity, (t, v) => t with { FirstSlotCapacity = v })),
        PlayedFrom2(Int(nameof(Tuning.SlotCapacityStep), TuningGroup.Enchanter, 0, 10000, t => t.SlotCapacityStep, (t, v) => t with { SlotCapacityStep = v })),
        PlayedFrom2(Int(nameof(Tuning.EnchantmentSlots), TuningGroup.Enchanter, ContentValidator.MostArrivingEnchantments, 10,
            t => t.EnchantmentSlots, (t, v) => t with { EnchantmentSlots = v })),
        PlayedFrom2(Int(nameof(Tuning.FirstEnchantmentWear), TuningGroup.Enchanter, 1, 10000, t => t.FirstEnchantmentWear, (t, v) => t with { FirstEnchantmentWear = v })),
        PlayedFrom2(Int(nameof(Tuning.EnchantmentWearStep), TuningGroup.Enchanter, 0, 10000, t => t.EnchantmentWearStep, (t, v) => t with { EnchantmentWearStep = v })),
        PlayedFrom2(Int(nameof(Tuning.EnchanterWorksOn), TuningGroup.Enchanter, 1, 10, t => t.EnchanterWorksOn, (t, v) => t with { EnchanterWorksOn = v })),

        // Inventory (§4.4)
        Int(nameof(Tuning.PartyCarrySlots), TuningGroup.Inventory, GameConstants.PartySize, GameConstants.PartySize * GameConstants.MaxInventorySlots,
            t => t.PartyCarrySlots, (t, v) => t with { PartyCarrySlots = v }),

        // Progression (§2.2) — not in §5.3's list
        Int(nameof(Tuning.WeaponXpPerLevel), TuningGroup.Progression, 1, 100000, t => t.WeaponXpPerLevel, (t, v) => t with { WeaponXpPerLevel = v }),

        // Combat (§1.2, §1.4) — not in §5.3's list
        Int(nameof(Tuning.WeaponSwapCost), TuningGroup.Combat, 0, 10000, t => t.WeaponSwapCost, (t, v) => t with { WeaponSwapCost = v }),
        Int(nameof(Tuning.LongshotFreeTiles), TuningGroup.Combat, 0, 100, t => t.LongshotFreeTiles, (t, v) => t with { LongshotFreeTiles = v }),
        new BoolKey(nameof(Tuning.FriendlyFireEnabled), TuningGroup.Combat,
            t => t.FriendlyFireEnabled, (t, v) => t with { FriendlyFireEnabled = v }),
        Int(nameof(Tuning.FriendlyFireAllyPercent), TuningGroup.Combat, 0, 100, t => t.FriendlyFireAllyPercent, (t, v) => t with { FriendlyFireAllyPercent = v }),

        // Drops (§3.1, §3.3) — not in §5.3's list: Phase 3's two guessed figures
        Int(nameof(Tuning.DropEnchantChancePercent), TuningGroup.Drops, 0, 100, t => t.DropEnchantChancePercent, (t, v) => t with { DropEnchantChancePercent = v }),
        Int(nameof(Tuning.ArcanePotency), TuningGroup.Drops, 0, 1000, t => t.ArcanePotency, (t, v) => t with { ArcanePotency = v }),

        // Saves — not in §5.3's list: the user's ruling of 2026-09-25 on the save
        // model keeps the save slots' count here, so a change of mind is an edit.
        // The launcher reads it (SaveSlots), never a rule of play, so no save's hash
        // holds it. At least 1, so a campaign always has a slot to begin in; at most
        // 9, since a digit picks each slot, as the bag's are picked, and the number
        // row stops at 9.
        Unplayed(Int(nameof(Tuning.SaveSlots), TuningGroup.Saves, 1, 9, t => t.SaveSlots, (t, v) => t with { SaveSlots = v })),
    ];

    private static IntKey Int(string name, TuningGroup group, int min, int max, Func<Tuning, int> get, Func<Tuning, int, Tuning> set)
        => new(name, group, min, max, get, set);

    /// <summary><paramref name="key"/>, which no rule of play reads — declared ahead of its reader, or the launcher's: read and clamped as every key, outside a save's hash until a rule plays it (<see cref="TuningKey.Played"/>).</summary>
    private static IntKey Unplayed(IntKey key) => key with { Played = false };

    /// <summary>
    /// <paramref name="key"/>, which a rule of play reads from Phase 6a, with the
    /// save's version 2: a save's hash covers it from that version
    /// (<see cref="TuningKey.HashedFrom"/>), so a campaign saved at version 1 —
    /// never played under it — is compared without it.
    /// </summary>
    private static IntKey PlayedFrom2(IntKey key) => key with { HashedFrom = 2 };

    /// <summary>
    /// A table keyed by an enum's members, read as the game reads it: every
    /// member the tuning's table names, in the enum's order. A tuning the game
    /// plays names every one (<see cref="ContentValidator.RuleTableNamesEveryMember"/>);
    /// the one the built-in file is read over (<see cref="Tuning.Blank"/>) names
    /// none until the file does.
    /// </summary>
    private static TableKey EnumTable<TEnum>(string name, TuningGroup group, int min, int max, string noun,
        Func<Tuning, IReadOnlyDictionary<TEnum, int>> table, Func<Tuning, IReadOnlyDictionary<TEnum, int>, Tuning> with)
        where TEnum : struct, Enum
    {
        var members = Enum.GetValues<TEnum>();
        return new TableKey(name, group, min, max, Array.AsReadOnly(members.Select(m => m.ToString()).ToArray()),
            Get: t =>
            {
                var named = table(t);
                var effective = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var member in members)
                    if (named.TryGetValue(member, out int value))
                        effective[member.ToString()] = value;
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
    /// the number for every id it names (S:84, S:128) — and for the entries the
    /// catalogue leaves to it (<see cref="EnchantmentDefinitions.SetByTuning"/>),
    /// it is the only number there is. The table is every id the tuning names,
    /// in ordinal order.
    /// </summary>
    private static TableKey ApplyPercentTable() => new(
        nameof(Tuning.ApplyPercent), TuningGroup.Appliers,
        Min: 0, Max: 1000, Members: null,
        Get: t => t.ApplyPercent.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
        Set: (t, values) => t with { ApplyPercent = new Dictionary<string, int>(values, StringComparer.Ordinal) })
    {
        MemberNoun = "enchantment",
    };
}
