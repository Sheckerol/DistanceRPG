using System.Reflection;
using System.Text.Json;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.TuningGroup;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The schema <c>tuning.json</c> is read and hashed from (§5.3; 1g): one key per
/// <see cref="Tuning"/> property, named verbatim; tables read exactly as the game
/// reads them, and complete — a table built in code names every member of its
/// kind, since no number in code stands under a gap; nothing the golden tests pin
/// and nothing that is not a scalar; no decay dial; the §5.3 starting set present
/// or waiting for the phase that reads it; and every divisor at least one.
/// </summary>
public class TuningSchemaTests
{
    /// <summary>The built-in tuning: the master copy's numbers, as the game plays them.</summary>
    private static Tuning BuiltIn => ShippedContent.Tuning;

    private static TuningKey Key(string name) => TuningSchema.Find(name) ?? throw new KeyNotFoundException(name);

    private static TableKey Table(string name) => (TableKey)Key(name);

    [Fact]
    public void TheSchemaIsTheTuningRecord_KeyForKey()
    {
        // PHASE5-034: the file's keys are the record's properties — the one
        // naming source — so a property added without a key, or a key for nothing,
        // fails here. Each key reads its property and writes it on a copy, and
        // writing one key moves that key alone.
        var properties = typeof(Tuning).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        var names = TuningSchema.Keys.Select(k => k.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(properties, names.Order(StringComparer.Ordinal));
        Assert.Contains(nameof(Tuning.MovementBudget), names);

        foreach (var key in TuningSchema.Keys)
        {
            Assert.Same(key, TuningSchema.Find(key.Name));
            Assert.Null(TuningSchema.Find(key.Name.ToUpperInvariant()));   // ordinal: the case is the name's
            var property = typeof(Tuning).GetProperty(key.Name)!;
            Tuning written;
            switch (key)
            {
                case IntKey k:
                {
                    int value = k.Get(BuiltIn);
                    Assert.Equal(property.GetValue(BuiltIn), value);
                    Assert.InRange(value, k.Min, k.Max);   // the shipped number is inside its own range
                    int moved = value + 1 <= k.Max ? value + 1 : value - 1;
                    written = k.Set(BuiltIn, moved);
                    Assert.Equal((moved, moved), (k.Get(written), (int)property.GetValue(written)!));
                    break;
                }
                case BoolKey k:
                    Assert.Equal(property.GetValue(BuiltIn), k.Get(BuiltIn));
                    written = k.Set(BuiltIn, !k.Get(BuiltIn));
                    Assert.Equal(!k.Get(BuiltIn), (bool)property.GetValue(written)!);
                    break;
                case TableKey k:
                {
                    var table = k.Get(BuiltIn);
                    Assert.All(table.Values, value => Assert.InRange(value, k.Min, k.Max));
                    string first = table.Keys.Order(StringComparer.Ordinal).First();
                    var moved = new Dictionary<string, int>(table) { [first] = table[first] + 1 <= k.Max ? table[first] + 1 : table[first] - 1 };
                    written = k.Set(BuiltIn, moved);
                    Assert.Equal(moved.OrderBy(e => e.Key, StringComparer.Ordinal), k.Get(written).OrderBy(e => e.Key, StringComparer.Ordinal));
                    Assert.NotSame(property.GetValue(BuiltIn), property.GetValue(written));
                    break;
                }
                default:
                    throw new InvalidOperationException($"{key.Name} is a {key.GetType().Name}, which the schema never makes");
            }

            var movedNames = TuningValues.Moved(BuiltIn, written);
            Assert.NotEmpty(movedNames);
            Assert.All(movedNames, name => Assert.True(name == key.Name || name.StartsWith(key.Name + ".", StringComparison.Ordinal), $"{key.Name} moved {name}"));
        }
    }

    [Fact]
    public void EveryTableReadsAsTheGameReadsIt()
    {
        // What the hash and the log read is what the game reads: under a tuning
        // with a member of every table moved, each table's entry is the game's own
        // reader's answer — the modifier rules' for a stack, an offset and a forge
        // limit, the status table's for a level, the catalogue's percentage for
        // every id the table names. Every table stays complete, as every tuning the
        // game plays is (RuleTableNamesEveryMember): a member moves, and the rest
        // keep the built-in numbers.
        var tuning = TuningValues.With(BuiltIn,
            ("PerStack.Block", 7), ("Offset.Push", 1), ("MaxForged.Pin", 2), ("EffectPerLevel.Mire", 5),
            ("ApplyPercent.cold", 40), ("ApplyPercent.arcane", 60));
        var content = TestContent.Load(tuning, ShippedContent.Restricted, ShippedContent.Enchantments, ShippedContent.Weapons);   // built, never made current
        var rules = content.Modifiers;

        foreach (var type in Enum.GetValues<ModifierType>())
        {
            string name = type.ToString();
            Assert.Equal(rules.PerStack(type), Table(nameof(Tuning.PerStack)).Get(tuning)[name]);
            Assert.Equal(rules.Offset(type), Table(nameof(Tuning.Offset)).Get(tuning)[name]);
            Assert.Equal(rules.MaxForged(type), Table(nameof(Tuning.MaxForged)).Get(tuning)[name]);
        }
        foreach (var status in Enum.GetValues<StatusEffectType>())
            Assert.Equal(StatusRules.EffectPerLevel(status, tuning), Table(nameof(Tuning.EffectPerLevel)).Get(tuning)[status.ToString()]);

        var apply = Table(nameof(Tuning.ApplyPercent)).Get(tuning);
        Assert.Equal(["acidic", "arcane", "cold", "flaming", "serrated", "shocking"], apply.Keys.Order(StringComparer.Ordinal));
        foreach (var (id, percent) in apply)
            Assert.Equal(content.Enchantments[id].ApplyPercent, percent);

        // The pieces this rests on: the moved entry is what the game reads, and
        // every other keeps the built-in number.
        Assert.Equal((7, BuiltIn.PerStack[ModifierType.Splitting]), (rules.PerStack(ModifierType.Block), rules.PerStack(ModifierType.Splitting)));
        Assert.Equal((1, BuiltIn.Offset[ModifierType.Block]), (rules.Offset(ModifierType.Push), rules.Offset(ModifierType.Block)));
        Assert.Equal((2, BuiltIn.MaxForged[ModifierType.Brace]), (rules.MaxForged(ModifierType.Pin), rules.MaxForged(ModifierType.Brace)));
        Assert.Equal((5, BuiltIn.EffectPerLevel[StatusEffectType.Ward]), (StatusRules.EffectPerLevel(StatusEffectType.Mire, tuning), StatusRules.EffectPerLevel(StatusEffectType.Ward, tuning)));
        Assert.Null(Table(nameof(Tuning.ApplyPercent)).Members);
        Assert.Equal(Enum.GetNames<ModifierType>(), Table(nameof(Tuning.PerStack)).Members!.ToArray());
        Assert.Equal(Enum.GetNames<StatusEffectType>(), Table(nameof(Tuning.EffectPerLevel)).Members!.ToArray());
    }

    /// <summary>
    /// The names the golden tests pin (PHASE5-035–037, A34): what decides a map, a
    /// fog box, a path or a stream — the three classes S:48-49 names and their
    /// constants, every stream's salt and mix (the dice's among them, which a later
    /// sub-step adds), what the placer, the stairs and the boss draw decide by, the
    /// tile, and every top-level key of the golden file itself. Hardcoded, so a
    /// key added by accident cannot be vouched for by the thing it breaks.
    /// </summary>
    private static readonly string[] ParityNames =
    [
        "MapGenerator", "Cols", "Rows", "MinRoom", "MaxRoom", "Attempts",
        "Mulberry32",
        "Pathfinder", "Dirs", "DiagonalCost",
        "SeedSalt", "FloorSalt", "StairSalt", "BossSalt", "LootSalt", "ReviveSalt", "BossDropSalt", "DiceSalt",
        "SpawnMix", "CycleMix", "DiceMix",
        "EnemyPlacer", "MaxEnemiesPerRoom", "PlacedClasses", "VariantsPerClass", "HealerWeaponId",
        "StairPlacer", "MilliStepsPerCardinal", "MilliStepsPerDiagonal",
        "MaxBossClassDraws", "MaxClassDraws",
        "Tile", "LogicUnitsPerTile",
        "mapRows", "mapCols", "rngSamples", "mapSeed", "rooms", "corridors", "expandedCorridors", "playerStart", "enemyStart",
        "gridRows", "unionFogBoxes", "fogBoxes", "paths",
    ];

    [Fact]
    public void NothingTheGoldenTestsPinIsAKey()
    {
        // PHASE5-035–037: "nothing the golden tests pin" — "a tunable RNG constant
        // is a tunable parity constant, and the golden file stops meaning anything
        // the moment one is editable". The schema's names, its table-qualified
        // names and the keys still to come are disjoint from the parity names,
        // whatever the case.
        var parity = new HashSet<string>(ParityNames, StringComparer.OrdinalIgnoreCase);
        var tuningNames = new List<string>();
        foreach (var key in TuningSchema.Keys)
        {
            tuningNames.Add(key.Name);
            if (key is TableKey table)
                tuningNames.AddRange(table.Get(BuiltIn).Keys.Select(member => $"{key.Name}.{member}"));
        }
        tuningNames.AddRange(TuningSchema.Deferred.Values.SelectMany(d => d.Keys));
        Assert.Equal([], tuningNames.Where(parity.Contains).ToArray());

        // And the list is complete, so a constant added where parity lives cannot
        // slip past a stale one: every constant and static readonly field the
        // parity classes declare, every salt and mix anywhere in Logic, and every
        // top-level key of the golden file.
        var listed = new HashSet<string>(ParityNames, StringComparer.Ordinal);
        Type[] parityClasses = [typeof(MapGenerator), typeof(Mulberry32), typeof(Pathfinder), typeof(EnemyPlacer), typeof(StairPlacer)];
        foreach (var type in parityClasses)
        {
            Assert.Contains(type.Name, listed);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if ((field.IsLiteral || field.IsInitOnly) && !field.Name.StartsWith('<'))
                    Assert.True(listed.Contains(field.Name), $"{type.Name}.{field.Name} decides parity and is not on the list");
        }

        var logic = TestIl.Types(typeof(Floor).Assembly).Where(t => t.Namespace == typeof(Floor).Namespace);
        var streams = logic.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(f => f.Name.EndsWith("Salt", StringComparison.Ordinal) || f.Name.EndsWith("Mix", StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToArray();
        Assert.Contains("SeedSalt", streams);   // the scan finds the salts it guards
        Assert.All(streams, name => Assert.Contains(name, listed));

        using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "distancerpg-golden.json")));
        var goldenKeys = golden.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Contains("mapSeed", goldenKeys);
        Assert.All(goldenKeys, name => Assert.Contains(name, listed));
    }

    [Fact]
    public void ScalarsOnly_IntegersBoolsAndTablesOfIntegers()
    {
        // PHASE5-038, -039, -043: "tuning.json holds scalars only", and a ratio is
        // an integer. Every property is a whole number, a boolean, or a table of
        // whole numbers by name — never a float, never a list, never nested — and
        // every key is one of the three shapes the reader clamps.
        Type[] allowed =
        [
            typeof(int), typeof(bool),
            typeof(IReadOnlyDictionary<ModifierType, int>), typeof(IReadOnlyDictionary<StatusEffectType, int>), typeof(IReadOnlyDictionary<string, int>),
        ];
        Assert.All(typeof(Tuning).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => Assert.Contains(property.PropertyType, allowed));
        Assert.All(TuningSchema.Keys, key => Assert.True(key is IntKey or BoolKey or TableKey, key.Name));
        Assert.All(TuningSchema.Keys.OfType<IntKey>(), key => Assert.True(key.Min <= key.Max, key.Name));
        Assert.All(TuningSchema.Keys.OfType<TableKey>(), key => Assert.True(key.Min <= key.Max, key.Name));
    }

    [Fact]
    public void NoDecayConstant()
    {
        // PHASE5-052: "no decay constants — one level a round, universally". No
        // key, member or key still to come names one, and the dial the unified
        // status model retired (SearDecayPerTurn, settled's superseding pass) is no
        // key: a file naming it is told so and changes nothing.
        var names = TuningSchema.Keys.Select(k => k.Name)
            .Concat(TuningSchema.Keys.OfType<TableKey>().SelectMany(t => t.Get(BuiltIn).Keys))
            .Concat(TuningSchema.Deferred.Values.SelectMany(d => d.Keys));
        Assert.DoesNotContain(names, name => name.Contains("Decay", StringComparison.OrdinalIgnoreCase));

        var read = TuningJson.Read("""{ "SearDecayPerTurn": 2 }""", BuiltIn);
        Assert.Contains("SearDecayPerTurn is not a key", Assert.Single(read.Warnings));
        Assert.Empty(TuningValues.Moved(BuiltIn, read.Tuning));
    }

    [Fact]
    public void TheStartingSetIsHere_OrDeferredWithItsReader()
    {
        // PHASE5-046–070, -075: §5.3's starting set, each by its code spelling
        // (the units-in-the-name rule, bend (a)), in its §5.3 group; "per-stack
        // values" are PerStack and "MaxForged overrides" MaxForged; and the ten
        // groups in §5.3's order, then the three beside it. The Enchanter's are
        // what a deposit costs, as the user ruled on 2026-09-25 (§5.3's row).
        (TuningGroup Group, string[] Keys)[] starting =
        [
            (Economy, [nameof(Tuning.MovementUnitsPerMana), nameof(Tuning.MovementBudget), nameof(Tuning.StartingPool)]),
            (Statuses, [nameof(Tuning.EffectPerLevel), nameof(Tuning.OverhealPerWard), nameof(Tuning.DotDamagePerMana)]),
            (Appliers, [nameof(Tuning.ApplyPercent)]),
            (Modifiers, [nameof(Tuning.AcquiredHeadroom), nameof(Tuning.PerStack), nameof(Tuning.MaxForged)]),
            (Farming, [nameof(Tuning.DefeatStackChance), nameof(Tuning.ReviveStepPercent), nameof(Tuning.ResurrectTurnsBase),
                nameof(Tuning.ResurrectTurnsFloor), nameof(Tuning.CleanKillBonus)]),
            (Uniques, [nameof(Tuning.UniqueChanceCeilingPercent), nameof(Tuning.UniqueChanceMidpoint), nameof(Tuning.UniqueChanceKPercent)]),
            (Dungeon, [nameof(Tuning.BossFloorMin), nameof(Tuning.BossFloorMax), nameof(Tuning.BossStackOffset),
                nameof(Tuning.CooldownRunsPerFloor), nameof(Tuning.MercyFloor), nameof(Tuning.DeathXpLossPercent)]),
            (TuningGroup.Enchanter, [nameof(Tuning.WearPerHit), .. RulingsEnchanterKeys]),
            (Inventory, [nameof(Tuning.PartyCarrySlots)]),
        ];
        foreach (var (group, keys) in starting)
            foreach (string name in keys)
                Assert.Equal(group, Key(name).Group);

        Assert.Equal([Economy, Statuses, Appliers, Modifiers, Farming, Uniques, Dungeon, TuningGroup.Enchanter, Inventory, Consumables, TuningGroup.Progression, Combat, Drops, Saves],
            Enum.GetValues<TuningGroup>());
        var groups = TuningSchema.Keys.Select(k => k.Group).ToArray();
        Assert.Equal(groups.Order(), groups);   // the file's order is the groups' order

        // The five 6a/6b keys still to come wait for their readers (bend (e)): no
        // key and no property, listed under their group with the phase that
        // brings them. WearPerHit came with its reader in Phase 6a.1, and
        // EnchanterWorksOn with its reader in 6a.3a. The Enchanter's six the
        // ruling made keys ahead of their reader are each a property, and a rule
        // plays each since Phase 6a reads it: the capacity a bar adds from 6a.1,
        // the other five from 6a.3a.
        Assert.Equal("Phase 6a", TuningSchema.Deferred[TuningGroup.Enchanter].Phase);
        Assert.Equal(["GraftBaseChance"], TuningSchema.Deferred[TuningGroup.Enchanter].Keys.ToArray());
        Assert.Equal("Phase 6b", TuningSchema.Deferred[Consumables].Phase);
        Assert.Equal(["PotionUseCost", "HealthPotionPercent", "ManaPotionPercent", "FarmYieldPerClear"], TuningSchema.Deferred[Consumables].Keys.ToArray());
        Assert.Equal([TuningGroup.Enchanter, Consumables], TuningSchema.Deferred.Keys.Order());
        foreach (string name in TuningSchema.Deferred.Values.SelectMany(d => d.Keys))
        {
            Assert.Null(TuningSchema.Find(name));
            Assert.Null(typeof(Tuning).GetProperty(name));
        }
        foreach (string name in RulingsEnchanterKeys)
        {
            Assert.NotNull(typeof(Tuning).GetProperty(name));
            Assert.True(Key(name).Played, $"{name} is not played");
        }
        Assert.NotNull(typeof(Tuning).GetProperty(nameof(Tuning.WearPerHit)));
        Assert.True(Key(nameof(Tuning.WearPerHit)).Played);
        Assert.DoesNotContain(TuningSchema.Keys, k => k.Group is Consumables);

        // What each key is set to is tuning.json's to say: the one derivation a
        // doc fixes — the divisor off the budget, W:774 — is pinned where its
        // test is (MovementBudgetTests; ShippedDataTests lists every such pin),
        // and no other number of the master is pinned here.
    }

    /// <summary>
    /// What a deposit costs, as the user ruled on 2026-09-25, in the file's order:
    /// the capacity a full bar of cashed wear adds, the capacity the first slot
    /// opens at and the step to the next, the most enchantments a weapon holds,
    /// and the wear attaching the first needs and the step to the next — and then
    /// how many deposits the enchanter works on at once, the doc's two (S:97–98),
    /// a key by the user's standing rule (Phase 6a.3a). A weapon's capacity opens
    /// on StartingPool, which is not among them.
    /// </summary>
    private static readonly string[] RulingsEnchanterKeys =
    [
        nameof(Tuning.WearCapacityPerBar), nameof(Tuning.FirstSlotCapacity), nameof(Tuning.SlotCapacityStep),
        nameof(Tuning.EnchantmentSlots), nameof(Tuning.FirstEnchantmentWear), nameof(Tuning.EnchantmentWearStep),
        nameof(Tuning.EnchanterWorksOn),
    ];

    [Fact]
    public void TheEnchanterNumbersAreKeys_PlayedFromVersion2()
    {
        // R4, the user's ruling of 2026-09-25: every number of what a deposit
        // costs is a tuning.json key of the Enchanter group, each its own, so any
        // one can move alone — declared ahead of Phase 6a's enchanter, which reads
        // them — and WearPerHit, what a hit wears, heads the group, arrived with
        // its reader, and EnchanterWorksOn, the enchanter's worked few, closes it
        // (Phase 6a.3a). Each is played since a Phase 6a rule reads it, and a
        // save's hash covers each from version 2 (HashedFrom, PHASE 6a DECISION 9).
        // Each is a key like any other: a property, the only number its Set moves,
        // the master's number inside its clamp, and the clamp the plan's (1f). What
        // the master sets each to is its own to say (REWORK DECISION 3), so no value
        // is asserted here.
        var enchanter = TuningSchema.Keys.Where(k => k.Group == TuningGroup.Enchanter).ToArray();
        Assert.Equal([nameof(Tuning.WearPerHit), .. RulingsEnchanterKeys], enchanter.Select(k => k.Name));
        var clamps = new Dictionary<string, (int Min, int Max)>
        {
            [nameof(Tuning.WearPerHit)] = (0, 1000),   // 0 wears nothing, a dial; 1000 where a magnitude clamps
            [nameof(Tuning.WearCapacityPerBar)] = (0, 1000),
            [nameof(Tuning.FirstSlotCapacity)] = (1, 10000),   // a capacity's range is StartingPool's
            [nameof(Tuning.SlotCapacityStep)] = (0, 10000),   // 0 opens every slot at once
            [nameof(Tuning.EnchantmentSlots)] = (ContentValidator.MostArrivingEnchantments, 10),
            [nameof(Tuning.FirstEnchantmentWear)] = (1, 10000),
            [nameof(Tuning.EnchantmentWearStep)] = (0, 10000),   // 0 prices every one as the first
            [nameof(Tuning.EnchanterWorksOn)] = (1, 10),   // 0 would freeze every service; no more than a weapon holds
        };
        foreach (var key in enchanter)
        {
            var k = Assert.IsType<IntKey>(key);
            Assert.True(k.Played, $"{k.Name} is not played");
            Assert.Equal(2, k.HashedFrom);
            Assert.Equal(clamps[k.Name], (k.Min, k.Max));
            int value = k.Get(BuiltIn);
            Assert.InRange(value, k.Min, k.Max);
            Assert.Equal([k.Name], TuningValues.Moved(BuiltIn, k.Set(BuiltIn, value < k.Max ? value + 1 : value - 1)));
        }

        // Every key outside the group is hashed from version 1, as it was: only
        // what Phase 6a plays is new to version 2's hash.
        Assert.All(TuningSchema.Keys.Where(k => k.Group != TuningGroup.Enchanter), k => Assert.Equal(1, k.HashedFrom));

        // The Enchanter's other number that is not the ruling's still waits with
        // its Phase 6a reader, and WearPerHit no longer does; the two placeholders
        // the ruling replaced are no longer keys to come but point at the keys that
        // took their place, each why in words the HUD's font can draw (the whole
        // warning around it is held to the font by TuningJsonTests' sweep of every
        // name warning); and the graft formula's StartingWearCapacity is still
        // StartingPool, the bar a capacity opens on.
        Assert.Equal(((string?)null, "Phase 6a"), (TuningSchema.DeferredPhase("WearPerHit"), TuningSchema.DeferredPhase("GraftBaseChance")));
        foreach (var (placeholder, key) in new[] { ("WearCapacityLadder", nameof(Tuning.WearCapacityPerBar)), ("EnchantmentWearCost", nameof(Tuning.FirstEnchantmentWear)) })
        {
            Assert.Null(TuningSchema.DeferredPhase(placeholder));
            var alias = TuningSchema.Alias(placeholder);
            Assert.Equal(key, alias?.Key);
            Assert.Matches(@"^[A-Za-z0-9 .,:_!?()/<>\[\]+=#*-]+$", alias?.Why ?? "");
        }
        Assert.Equal(nameof(Tuning.StartingPool), TuningSchema.Suggest("StartingWearCapacity"));

        // EnchantmentSlots never holds a weapon to fewer slots than it arrives
        // filling: its clamp starts at the most any weapon arrives carrying, read
        // off the validator's own constant, so the two cannot drift apart.
        Assert.Equal(ContentValidator.MostArrivingEnchantments, ((IntKey)Key(nameof(Tuning.EnchantmentSlots))).Min);
    }

    [Fact]
    public void TheSlotCountIsAKeyNoRulePlays()
    {
        // R2(c), the user's ruling of 2026-09-25 on the save model: "the number of
        // saves should live on the tuning.json so that we don't need to fix the code
        // every time I change my mind". SaveSlots is the one key of a group of its
        // own, the file's last, clamped 1 to 9 — a slot at least, so saving by hand
        // always has somewhere to go, and a digit to pick each — and read by the
        // launcher (the save folder's slots), never by a rule of play: Played is
        // false, so a save's hash leaves it out. What the master sets it to is its
        // own to say, so no value is asserted here.
        var key = Assert.IsType<IntKey>(Key(nameof(Tuning.SaveSlots)));
        Assert.Equal((Saves, 1, 9), (key.Group, key.Min, key.Max));
        Assert.False(key.Played);
        Assert.Equal([key], TuningSchema.Keys.Where(k => k.Group == Saves));
        Assert.Same(key, TuningSchema.Keys[^1]);
        Assert.InRange(key.Get(BuiltIn), key.Min, key.Max);
        Assert.Equal(key.Max, SaveSlots.Most);   // the highest slot number is the clamp's ceiling, read off it

        // A file asking for twelve plays nine and says so, as every clamp does — in
        // the HUD's font, since it is drawn — and the log names the number it
        // changed; the tuning's hash does not move.
        var read = TuningJson.Read("""{ "SaveSlots": 12 }""", BuiltIn);
        Assert.Equal(["tuning.json: SaveSlots 12 is above 9 - using 9"], read.Warnings);
        Assert.Equal(9, read.Tuning.SaveSlots);
        string[] changed = BuiltIn.SaveSlots == 9 ? [] : ["SaveSlots 9"];
        Assert.Equal(changed, read.Overrides);
        Assert.Equal(TuningJson.Hash(BuiltIn), TuningJson.Hash(read.Tuning));
        var load = ShippedData.Load(ShippedData.Texts with { Tuning = """{ "SaveSlots": 12 }""" });
        Assert.Null(load.Error);
        Assert.Equal(9, load.Content!.Tuning.SaveSlots);
        string warning = Assert.Single(load.Notices);
        Assert.All(NoticeText.Wrap(warning), row => Assert.Matches(@"^[A-Z0-9 .,:_!?()/<>\[\]+=#*-]+$", row));
        Assert.Equal(TuningJson.Hash(BuiltIn), TuningJson.Hash(load.Content.Tuning));

        // Every count a file can make opens the slots: one to nine of them.
        foreach (int count in new[] { key.Min, key.Max })
        {
            var slots = new SaveSlots(Path.Combine(Path.GetTempPath(), "never-made"), count);
            Assert.Equal(count, slots.Count);
        }
    }

    [Fact]
    public void ATuningBuiltInCodeNamesEveryMember()
    {
        // RuleTableNamesEveryMember (the content rework, 1f): the game reads every
        // member of the four tables keyed by an enum directly, and no number in
        // code stands under a member a table leaves out. A file cannot leave one
        // — the built-in copy is refused for a gap as it is read, and a copy beside
        // it merges over it — so a tuning built in code with a table short of a
        // member is refused: by the validator, and by the modifier rules for
        // their three, naming the first member missing, in the enum's order.
        (string Table, Tuning Short, string Missing)[] shortOfOne =
        [
            (nameof(Tuning.EffectPerLevel), BuiltIn with { EffectPerLevel = new Dictionary<StatusEffectType, int> { [StatusEffectType.Mire] = 5 } }, "Regeneration"),
            (nameof(Tuning.PerStack), BuiltIn with { PerStack = new Dictionary<ModifierType, int> { [ModifierType.Brace] = 1 } }, "Block"),
            (nameof(Tuning.Offset), BuiltIn with { Offset = new Dictionary<ModifierType, int>() }, "Brace"),
            (nameof(Tuning.MaxForged), BuiltIn with { MaxForged = new Dictionary<ModifierType, int> { [ModifierType.Light] = 1 } }, "Brace"),
        ];
        foreach (var (table, shortTuning, missing) in shortOfOne)
        {
            var refused = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(shortTuning));
            Assert.Equal(($"{table}.{missing}", ContentValidator.RuleTableNamesEveryMember), (refused.EntryId, refused.Rule));

            // Through the load it is refused before anything else is read, and — a
            // tuning built in code, no file's — stamped with no file.
            var loaded = Assert.Throws<ContentException>(() => TestContent.Load(shortTuning));
            Assert.Equal((null, $"{table}.{missing}", ContentValidator.RuleTableNamesEveryMember), (loaded.File, loaded.EntryId, loaded.Rule));

            if (table != nameof(Tuning.EffectPerLevel))
            {
                var rules = Assert.Throws<ContentException>(() => new ModifierRules(shortTuning, ShippedContent.Restricted));
                Assert.Equal(($"{table}.{missing}", ContentValidator.RuleTableNamesEveryMember), (rules.EntryId, rules.Rule));
            }
        }

        // The blank tuning the built-in copy is read over names nothing, and is
        // never played; the same table with a member moved — complete — loads.
        Assert.Equal(ContentValidator.RuleTableNamesEveryMember, Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(Tuning.Blank)).Rule);
        var moved = TuningValues.With(BuiltIn, ("PerStack.Block", 4));
        ContentValidator.ValidateTuning(moved);
        Assert.Equal(4, TestContent.Load(moved).Modifiers.PerStack(ModifierType.Block));
    }

    [Fact]
    public void EveryDivisorIsAtLeastOne()
    {
        // PHASE5-043: a ratio is an integer divisor, and a divisor is at least 1 —
        // the file cannot get one below it (its range starts at 1), and a tuning
        // built in code with one at 0 is refused at load under its own rule.
        string[] divisors = [nameof(Tuning.MovementUnitsPerMana), nameof(Tuning.MovementBudget), nameof(Tuning.OverhealPerWard), nameof(Tuning.DotDamagePerMana)];
        foreach (string name in divisors)
        {
            var key = (IntKey)Key(name);
            Assert.True(key.Min >= 1, name);

            var refused = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(key.Set(BuiltIn, 0)));
            Assert.Equal((name, ContentValidator.RuleDivisorIsPositive), (refused.EntryId, refused.Rule));

            var read = TuningJson.Read($$"""{ "{{name}}": 0 }""", BuiltIn);
            Assert.Equal(1, key.Get(read.Tuning));
            ContentValidator.ValidateTuning(read.Tuning);
        }
    }
}
