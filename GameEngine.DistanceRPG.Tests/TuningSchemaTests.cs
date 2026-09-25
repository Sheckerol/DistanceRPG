using System.Reflection;
using System.Text.Json;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.TuningGroup;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The schema <c>tuning.json</c> is read, written and hashed from (§5.3; 1g):
/// one key per <see cref="Tuning"/> property, named verbatim; tables read
/// exactly as the game reads them; nothing the golden tests pin and nothing that
/// is not a scalar; no decay dial; the §5.3 starting set present or waiting for
/// the phase that reads it; and every divisor at least one.
/// </summary>
public class TuningSchemaTests
{
    private static readonly Tuning Compiled = ShippedContent.Tuning;

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
                    int value = k.Get(Compiled);
                    Assert.Equal(property.GetValue(Compiled), value);
                    Assert.InRange(value, k.Min, k.Max);   // the compiled number is inside its own range
                    int moved = value + 1 <= k.Max ? value + 1 : value - 1;
                    written = k.Set(Compiled, moved);
                    Assert.Equal((moved, moved), (k.Get(written), (int)property.GetValue(written)!));
                    break;
                }
                case BoolKey k:
                    Assert.Equal(property.GetValue(Compiled), k.Get(Compiled));
                    written = k.Set(Compiled, !k.Get(Compiled));
                    Assert.Equal(!k.Get(Compiled), (bool)property.GetValue(written)!);
                    break;
                case TableKey k:
                {
                    var table = k.Get(Compiled);
                    Assert.All(table.Values, value => Assert.InRange(value, k.Min, k.Max));
                    string first = table.Keys.Order(StringComparer.Ordinal).First();
                    var moved = new Dictionary<string, int>(table) { [first] = table[first] + 1 <= k.Max ? table[first] + 1 : table[first] - 1 };
                    written = k.Set(Compiled, moved);
                    Assert.Equal(moved.OrderBy(e => e.Key, StringComparer.Ordinal), k.Get(written).OrderBy(e => e.Key, StringComparer.Ordinal));
                    Assert.NotSame(property.GetValue(Compiled), property.GetValue(written));
                    break;
                }
                default:
                    throw new InvalidOperationException($"{key.Name} is a {key.GetType().Name}, which the schema never makes");
            }

            var movedNames = TuningValues.Moved(Compiled, written);
            Assert.NotEmpty(movedNames);
            Assert.All(movedNames, name => Assert.True(name == key.Name || name.StartsWith(key.Name + ".", StringComparison.Ordinal), $"{key.Name} moved {name}"));
        }
    }

    [Fact]
    public void EveryTableReadsAsTheGameReadsIt()
    {
        // What the file writes and the hash reads is what the game reads: under a
        // tuning whose tables name some members and not others, each table's
        // effective entry is the game's own reader's answer — the modifier rules'
        // for a stack, an offset and a forge limit, the status table's for a
        // level, the catalogue's percentage for every id the table names. So a
        // member neither table names reads the reader's own fallback.
        var tuning = Compiled with
        {
            PerStack = new Dictionary<ModifierType, int> { [ModifierType.Block] = 7 },
            Offset = new Dictionary<ModifierType, int> { [ModifierType.Push] = 1 },
            MaxForged = new Dictionary<ModifierType, int> { [ModifierType.Pin] = 2 },
            EffectPerLevel = new Dictionary<StatusEffectType, int> { [StatusEffectType.Mire] = 5 },
            ApplyPercent = new Dictionary<string, int>(StringComparer.Ordinal) { ["cold"] = 40, ["arcane"] = 60 },
        };
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

        // The pieces this rests on: the tuning's entry wins, the compiled table
        // stands behind it, and each table's reader falls back to what its key says.
        Assert.Equal((7, 3, 0), (rules.PerStack(ModifierType.Block), rules.PerStack(ModifierType.Splitting), Table(nameof(Tuning.PerStack)).Fallback));
        Assert.Equal((ModifierRules.DefaultOffset, 0), (Table(nameof(Tuning.Offset)).Fallback, rules.Offset(ModifierType.Block)));
        Assert.Equal((ModifierRules.DefaultMaxForged, 3), (Table(nameof(Tuning.MaxForged)).Fallback, rules.MaxForged(ModifierType.Brace)));
        Assert.Equal(StatusRules.EffectFallback, Table(nameof(Tuning.EffectPerLevel)).Fallback);
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
                tuningNames.AddRange(table.Get(Compiled).Keys.Select(member => $"{key.Name}.{member}"));
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
            .Concat(TuningSchema.Keys.OfType<TableKey>().SelectMany(t => t.Get(Compiled).Keys))
            .Concat(TuningSchema.Deferred.Values.SelectMany(d => d.Keys));
        Assert.DoesNotContain(names, name => name.Contains("Decay", StringComparison.OrdinalIgnoreCase));

        var read = TuningJson.Read("""{ "SearDecayPerTurn": 2 }""");
        Assert.Contains("SearDecayPerTurn is not a key", Assert.Single(read.Warnings));
        Assert.Empty(TuningValues.Moved(Compiled, read.Tuning));
    }

    [Fact]
    public void TheStartingSetIsHere_OrDeferredWithItsReader()
    {
        // PHASE5-046–070, -075: §5.3's starting set, each by its code spelling
        // (the units-in-the-name rule, bend (a)), in its §5.3 group; "per-stack
        // values" are PerStack and "MaxForged overrides" MaxForged; and the ten
        // groups in §5.3's order, then the three beside it.
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
            (Inventory, [nameof(Tuning.PartyCarrySlots)]),
        ];
        foreach (var (group, keys) in starting)
            foreach (string name in keys)
                Assert.Equal(group, Key(name).Group);

        Assert.Equal([Economy, Statuses, Appliers, Modifiers, Farming, Uniques, Dungeon, Enchanter, Inventory, Consumables, TuningGroup.Progression, Combat, Drops],
            Enum.GetValues<TuningGroup>());
        var groups = TuningSchema.Keys.Select(k => k.Group).ToArray();
        Assert.Equal(groups.Order(), groups);   // the file's order is the groups' order

        // The eight 6a/6b keys wait for their readers (bend (e)): no key and no
        // property, listed under their group with the phase that brings them.
        Assert.Equal("Phase 6a", TuningSchema.Deferred[Enchanter].Phase);
        Assert.Equal(["WearPerHit", "WearCapacityLadder", "EnchantmentWearCost", "GraftBaseChance"], TuningSchema.Deferred[Enchanter].Keys.ToArray());
        Assert.Equal("Phase 6b", TuningSchema.Deferred[Consumables].Phase);
        Assert.Equal(["PotionUseCost", "HealthPotionPercent", "ManaPotionPercent", "FarmYieldPerClear"], TuningSchema.Deferred[Consumables].Keys.ToArray());
        Assert.Equal([Enchanter, Consumables], TuningSchema.Deferred.Keys.Order());
        foreach (string name in TuningSchema.Deferred.Values.SelectMany(d => d.Keys))
        {
            Assert.Null(TuningSchema.Find(name));
            Assert.Null(typeof(Tuning).GetProperty(name));
        }
        Assert.DoesNotContain(TuningSchema.Keys, k => k.Group is Enchanter or Consumables);

        // The numbers the docs and settled.md fix: every pool starts at 30, the
        // divisor is 10 on a budget of 160, DotDamagePerMana is 1/3 as 3, and the
        // statuses and elements are settled's.
        Assert.Equal((30, 10, 160, 3, 5), (Compiled.StartingPool, Compiled.MovementUnitsPerMana, Compiled.MovementBudget, Compiled.DotDamagePerMana, Compiled.OverhealPerWard));
        var effect = Table(nameof(Tuning.EffectPerLevel)).Get(Compiled);
        Assert.Equal((1, 10, 1), (effect["Searing"], effect["Mire"], effect["Ward"]));
        var apply = Table(nameof(Tuning.ApplyPercent)).Get(Compiled);
        Assert.Equal((20, 15, 25, 15), (apply["flaming"], apply["cold"], apply["shocking"], apply["acidic"]));
        Assert.Equal((20, 15, 50, 24), (Compiled.UniqueChanceMidpoint, Compiled.ReviveStepPercent, Compiled.DeathXpLossPercent, Compiled.PartyCarrySlots));
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

            var refused = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(key.Set(Compiled, 0)));
            Assert.Equal((name, ContentValidator.RuleDivisorIsPositive), (refused.EntryId, refused.Rule));

            var read = TuningJson.Read($$"""{ "{{name}}": 0 }""");
            Assert.Equal(1, key.Get(read.Tuning));
            ContentValidator.ValidateTuning(read.Tuning);
        }
    }
}
