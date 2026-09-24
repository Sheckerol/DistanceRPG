using System.Globalization;
using System.Text.RegularExpressions;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.3's <c>tuning.json</c>, as a reader, a writer and a hash (PHASE5-031–045,
/// -080): "a file a human edits by hand" — a partial file is valid, an unknown
/// key or a value out of range costs a warning and never a crash, a ratio is an
/// integer — written with every number on one screen, grouped and commented, and
/// hashed on its values alone. Every case reads text: nothing here touches a
/// file. One case makes each tuning it loads current for a moment, to price every
/// shipped weapon under it (<see cref="NegativeCosts"/>), so the class sits in the
/// content collection.
/// </summary>
[Collection(TestContent.Collection)]
public class TuningJsonTests
{
    private static readonly Tuning Compiled = ContentDefaults.Tuning;

    private static TuningKey Key(string name) => TuningSchema.Find(name) ?? throw new KeyNotFoundException(name);

    private static IReadOnlyDictionary<string, int> Table(Tuning tuning, string name) => ((TableKey)Key(name)).Get(tuning);

    [Fact]
    public void NoFileAndAnEmptyFileAreTheCompiledNumbers()
    {
        // PHASE5-041: no file at all is the compiled numbers, and so is a file that
        // names nothing — empty, blank, or an empty table with a comment on it.
        // Nothing to warn about in any of them: the missing file's notice is the
        // loader's, which every data file gets alike.
        foreach (string? text in new[] { null, "", "  \n", "{}", "/* nothing yet */ { }" })
        {
            var read = TuningJson.Read(text);
            Assert.Empty(read.Warnings);
            Assert.Empty(read.Overrides);
            TuningValues.Same(Compiled, read.Tuning);
        }

        // The loader adds the missing file's notice — and only for a missing one.
        // The other data files sit beside it, as they ship, so any notice is the
        // tuning's (an absent content file has a notice of its own).
        var others = ContentFiles.WriteDefaults() with { Tuning = null };
        Assert.Equal(["tuning.json is missing - the built-in numbers are playing"], ContentFiles.Load(others).Notices);
        Assert.Empty(ContentFiles.Load(others with { Tuning = "{}" }).Notices);

        // A file that is there and could not be read is not a missing file. The
        // edge says why, the notice says so, and the built-in numbers play. A
        // text that was read wins over any reason given beside it.
        var unreadable = others with { Unreadable = new Dictionary<string, string> { [TuningJson.FileName] = "another program has it open" } };
        var locked = ContentFiles.Load(unreadable);
        Assert.Equal(["tuning.json could not be read (another program has it open) - the built-in numbers are playing"], locked.Notices);
        TuningValues.Same(Compiled, locked.Content!.Tuning);
        Assert.Empty(ContentFiles.Load(unreadable with { Tuning = "{}" }).Notices);
    }

    [Fact]
    public void AFileNamesOnlyWhatItChanges_TablesMergePerEntry()
    {
        // PHASE5-041, -049, -053, -055, -056: a partial file changes what it names
        // and nothing else, and a table merges entry by entry onto the compiled
        // one — the other twenty-two stacks keep their worth.
        void OnlyMoves(string json, string name, string value)
        {
            var read = TuningJson.Read(json);
            Assert.Empty(read.Warnings);
            Assert.Equal([name], TuningValues.Moved(Compiled, read.Tuning));
            Assert.Equal(value, TuningValues.Of(read.Tuning)[name]);
        }

        OnlyMoves("""{ "PerStack": { "Block": 4 } }""", "PerStack.Block", "4");
        OnlyMoves("""{ "EffectPerLevel": { "Mire": 20 } }""", "EffectPerLevel.Mire", "20");
        OnlyMoves("""{ "ApplyPercent": { "cold": 30 } }""", "ApplyPercent.cold", "30");
        OnlyMoves("""{ "MaxForged": { "Light": 2 } }""", "MaxForged.Light", "2");
        OnlyMoves("""{ "MovementBudget": 200 }""", "MovementBudget", "200");
        OnlyMoves("""{ "FriendlyFireEnabled": true }""", "FriendlyFireEnabled", "true");

        // And the game reads the merged table so: Block is worth 4 a stack, Brace
        // still its compiled 1.
        var rules = new ModifierRules(TuningJson.Read("""{ "PerStack": { "Block": 4 } }""").Tuning, ContentDefaults.Restricted);
        Assert.Equal((4, 1), (rules.PerStack(ModifierType.Block), rules.PerStack(ModifierType.Brace)));
    }

    [Fact]
    public void AnUnknownKeyWarnsAndIsIgnored_PointingAtTheKey()
    {
        // PHASE5-042, -043: an unknown key never breaks a file — a constant added
        // later is optional, and one removed is ignored — and it is never read as
        // the key it resembles: one warning, pointing at the key the reader meant,
        // and nothing moved. §5.3's own spellings of three keys the code names by
        // their units (bend (a); settled L163-166) and the rejected float form
        // point at the code's names; a typo points at the nearest key.
        void Ignored(string json, params string[] said)
        {
            var read = TuningJson.Read(json);
            string warning = Assert.Single(read.Warnings);
            Assert.StartsWith("tuning.json: ", warning);
            foreach (string fragment in said)
                Assert.Contains(fragment, warning);
            Assert.Empty(TuningValues.Moved(Compiled, read.Tuning));
        }

        Ignored("""{ "MovmentBudget": 200 }""", "MovmentBudget is not a key", "did you mean MovementBudget?");
        Ignored("""{ "DotManaPerDamage": 3 }""", "DotManaPerDamage is not a key", "the key is DotDamagePerMana", "integer reciprocal");
        Ignored("""{ "UniqueChanceCeiling": 80 }""", "UniqueChanceCeiling is not a key", "the key is UniqueChanceCeilingPercent");
        Ignored("""{ "UniqueChanceK": 52 }""", "UniqueChanceK is not a key", "the key is UniqueChanceKPercent");
        Ignored("""{ "ManaPerMovementUnit": 0.1 }""", "ManaPerMovementUnit is not a key", "the key is MovementUnitsPerMana", "integer divisor");
        Ignored("""{ "PerStack": { "Blok": 4 } }""", "PerStack.Blok is not a modifier", "did you mean Block?");
        Ignored("""{ "movementBudget": 200 }""", "movementBudget is not a key", "did you mean MovementBudget?");   // a key's case is part of its name
        Ignored("""{ "Wibble": 1 }""", "Wibble is not a key - ignored");

        // One of §5.3's keys still to come says which phase brings it.
        Ignored("""{ "GraftBaseChance": 3 }""", "GraftBaseChance is not a key yet", "Phase 6a");
        Ignored("""{ "HealthPotionPercent": 20 }""", "HealthPotionPercent is not a key yet", "Phase 6b");
    }

    /// <summary>Every whole-number key and every table's first member, one below its range and one above it.</summary>
    public static TheoryData<string, string?, long, int> OutOfRange()
    {
        var data = new TheoryData<string, string?, long, int>();
        foreach (var key in TuningSchema.Keys)
        {
            switch (key)
            {
                case IntKey k:
                    data.Add(k.Name, null, (long)k.Min - 1, k.Min);
                    data.Add(k.Name, null, (long)k.Max + 1, k.Max);
                    break;
                case TableKey k:
                    string member = k.Members?[0] ?? Table(Compiled, k.Name).Keys.Order(StringComparer.Ordinal).First();
                    data.Add(k.Name, member, (long)k.Min - 1, k.Min);
                    data.Add(k.Name, member, (long)k.Max + 1, k.Max);
                    break;
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public void OutOfRangeClampsAndWarns_ItNeverThrows(string name, string? member, long value, int bound)
    {
        // PHASE5-045: "out-of-range values clamp and warn rather than throwing" —
        // "a typo should cost a log line, not a session". The value lands on the
        // bound, with one warning naming the key, the value and the bound. A key
        // other keys are measured against may be moved on by a clamp between keys
        // (a floor range, the boss offset), which says so in a warning of its own;
        // either way, what comes back passes the validator.
        string json = member is null ? $$"""{ "{{name}}": {{value}} }""" : $$"""{ "{{name}}": { "{{member}}": {{value}} } }""";
        var read = TuningJson.Read(json);

        string label = member is null ? name : $"{name}.{member}";
        string clamp = Assert.Single(read.Warnings, w => w.StartsWith($"tuning.json: {label} {value} is ", StringComparison.Ordinal));
        Assert.EndsWith(value < bound ? $"is below {bound} - using {bound}" : $"is above {bound} - using {bound}", clamp);

        // Only the key's own clamp names the value. A clamp between keys can then
        // move two keys past their bound, and no others: BossFloorMax (raised to
        // BossFloorMin) and BossStackOffset (raised over the deepest boss). When
        // it does, the key lands on the number that clamp's warning says it is
        // using. Every other key lands on the bound, and so do those two when
        // nothing moved them. BossFloorMin and AcquiredHeadroom can make the
        // offset move, but they stay on their own bound.
        Assert.All(read.Warnings.Where(w => w != clamp), w => Assert.DoesNotContain($"{label} {value}", w));
        int landed = member is null ? ((IntKey)Key(name)).Get(read.Tuning) : Table(read.Tuning, name)[member];
        string? moved = read.Warnings.SingleOrDefault(w => w != clamp && w.StartsWith($"tuning.json: {label} ", StringComparison.Ordinal));
        if (moved is null)
        {
            Assert.Equal(bound, landed);
        }
        else
        {
            Assert.Contains(label, MovedByARelation);
            Assert.Equal(Using(moved), landed);
        }
        ContentValidator.ValidateTuning(read.Tuning);
    }

    /// <summary>The keys a clamp between keys may move after their own clamp: the deep end of the boss floor range, and the boss offset.</summary>
    private static readonly string[] MovedByARelation = [nameof(Tuning.BossFloorMax), nameof(Tuning.BossStackOffset)];

    /// <summary>The number a warning says the game is using: <c>... - using 5</c>.</summary>
    private static int Using(string warning)
    {
        var match = Regex.Match(warning, @" - using (\d+)$");
        Assert.True(match.Success, $"'{warning}' does not say what it is using");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void EveryTuningAFileCanMakeLoadsWithTheShippedContent()
    {
        // The property the clamps and the fit exist for (1g): whatever a file says,
        // the game starts. Every whole-number key alone, and every table with all
        // its members at once, at the ends of an int and around zero; the boolean
        // both ways; and the cases where keys meet each other or the content —
        // floors inverted, carries the party cannot share or the roster cannot fit,
        // the deepest boss against the shallowest farm, every forge limit at 1. Each
        // loads through the loader with the shipped content (in Phase 5.1, the
        // compiled content), and the tuning it plays still passes the validator,
        // which stays the refusal for a tuning built in code. And under it no
        // shipped weapon's swing or cast costs less than nothing, however deep
        // its Light or Resonant runs: a discount past 100 percent takes the
        // whole cost and no more (PerStack.Resonant 20 is 120 percent at x6).
        long[] ends = [int.MinValue, -1, 0, 1, int.MaxValue];
        var files = new List<string>();
        foreach (var key in TuningSchema.Keys)
        {
            switch (key)
            {
                case IntKey k:
                    files.AddRange(ends.Select(v => $$"""{ "{{k.Name}}": {{v}} }"""));
                    break;
                case BoolKey k:
                    files.Add($$"""{ "{{k.Name}}": true }""");
                    files.Add($$"""{ "{{k.Name}}": false }""");
                    break;
                case TableKey k:
                    var members = k.Members ?? Table(Compiled, k.Name).Keys.ToList();
                    files.AddRange(ends.Select(v => $$"""{ "{{k.Name}}": { {{string.Join(", ", members.Select(m => $"\"{m}\": {v}"))}} } }"""));
                    break;
            }
        }
        files.AddRange(
        [
            """{ "BossFloorMin": 8, "BossFloorMax": 3 }""",
            """{ "PartyCarrySlots": 4 }""",
            """{ "PartyCarrySlots": 25 }""",
            """{ "PartyCarrySlots": 40 }""",
            """{ "PartyCarrySlots": 0 }""",
            """{ "BossFloorMax": 99, "BossStackOffset": 0, "AcquiredHeadroom": 1 }""",
            """{ "BossFloorMin": 1, "BossFloorMax": 1, "BossStackOffset": 0, "AcquiredHeadroom": 1 }""",
            $$"""{ "MaxForged": { {{string.Join(", ", Enum.GetNames<ModifierType>().Select(m => $"\"{m}\": 1"))}} } }""",
            """{ "PerStack": { "Light": 20, "Resonant": 20 } }""",
            """{ "Offset": { "Light": 150, "Resonant": 150 } }""",
        ]);
        Assert.True(files.Count > 170, $"{files.Count} files: the sweep is not reading the schema");

        var failures = new List<string>();
        foreach (string file in files)
        {
            var load = ContentFiles.Load(new ContentTexts(Tuning: file));
            if (load.Content is not { } content)
            {
                failures.Add($"{file} -> {load.Error?.Message ?? "no content"}");
                continue;
            }
            try
            {
                ContentValidator.ValidateTuning(content.Tuning);
            }
            catch (ContentException error)
            {
                failures.Add($"{file} -> plays a tuning the validator refuses: {error.Message}");
            }
            failures.AddRange(NegativeCosts(content).Select(cost => $"{file} -> {cost}"));
        }
        Assert.Empty(failures);
    }

    /// <summary>
    /// Every shipped weapon that would cost less than nothing to swing or cast
    /// under <paramref name="content"/>, made current for the pricing and put back
    /// after. Each weapon is priced at the deepest Light, and then the deepest
    /// Resonant, that any weapon can carry: forged to that modifier's limit and
    /// farmed to its cap (forged + AcquiredHeadroom), with the other discount of
    /// the pair taken off, since the two exclude each other.
    /// </summary>
    private static List<string> NegativeCosts(GameContent content)
    {
        var previous = GameContent.Current;
        GameContent.Use(content);
        try
        {
            var found = new List<string>();
            foreach (var def in content.Weapons.All)
            {
                foreach (var (discount, other) in new[] { (ModifierType.Light, ModifierType.Resonant), (ModifierType.Resonant, ModifierType.Light) })
                {
                    var spread = def.Forged.Where(entry => entry.Key != other).ToDictionary(entry => entry.Key, entry => entry.Value);
                    spread[discount] = content.Modifiers.MaxForged(discount);
                    var weapon = new Weapon(def with { Forged = spread }, []);
                    if (content.Modifiers.Allowed(discount, weapon.Kind, weapon.Modifiers))
                        weapon.Acquire(discount, content.Modifiers.AcquiredHeadroom);
                    if (weapon.ResolvedCost < 0 || weapon.ResolvedManaCost < 0)
                        found.Add($"{def.Id} at {discount} x{weapon.Stacks(discount)} costs {weapon.ResolvedCost} movement and {weapon.ResolvedManaCost} mana");
                }
            }
            return found;
        }
        finally
        {
            GameContent.Use(previous);
        }
    }

    [Fact]
    public void ARatioIsAnInteger()
    {
        // PHASE5-043, -044: "Ratios are integer divisors, not floats" — a fraction,
        // a number written as text, a boolean or a null where the divisor belongs
        // warns and keeps the compiled 10; a whole number is the divisor.
        foreach (string value in new[] { "10.5", "\"10\"", "true", "null", "1e1", "10.0" })
        {
            var read = TuningJson.Read($$"""{ "MovementUnitsPerMana": {{value}} }""");
            string warning = Assert.Single(read.Warnings);
            Assert.StartsWith("tuning.json: MovementUnitsPerMana ", warning);
            Assert.EndsWith("keeping 10", warning);
            Assert.Equal(10, read.Tuning.MovementUnitsPerMana);
        }

        var whole = TuningJson.Read("""{ "MovementUnitsPerMana": 12 }""");
        Assert.Empty(whole.Warnings);
        Assert.Equal(12, whole.Tuning.MovementUnitsPerMana);

        // A whole number past any range there is is still a whole number: it clamps.
        var huge = TuningJson.Read("""{ "MovementUnitsPerMana": 99999999999999999999999 }""");
        Assert.Equal(1000, huge.Tuning.MovementUnitsPerMana);
        Assert.EndsWith("99999999999999999999999 is above 1000 - using 1000", Assert.Single(huge.Warnings));
    }

    [Fact]
    public void OnlyScalars_AnArrayIsNeverAValue()
    {
        // PHASE5-038, -039: "tuning.json holds scalars only" — a table is named
        // scalars, and a list is never a value: not as a table, not as a key, not
        // as a table's entry, and not §5.3's ladder, whose key waits for Phase 6a.
        var table = TuningJson.Read("""{ "PerStack": [1] }""");
        Assert.Contains("PerStack is a list", Assert.Single(table.Warnings));
        Assert.Empty(TuningValues.Moved(Compiled, table.Tuning));

        var ladder = TuningJson.Read("""{ "WearCapacityLadder": [30, 40, 55] }""");
        Assert.Contains("WearCapacityLadder is not a key yet", Assert.Single(ladder.Warnings));
        Assert.Empty(TuningValues.Moved(Compiled, ladder.Tuning));

        var key = TuningJson.Read("""{ "MovementBudget": [160] }""");
        Assert.Contains("MovementBudget is a list", Assert.Single(key.Warnings));

        var entry = TuningJson.Read("""{ "PerStack": { "Block": [4] } }""");
        Assert.Contains("PerStack.Block is a list", Assert.Single(entry.Warnings));
        Assert.Empty(TuningValues.Moved(Compiled, entry.Tuning));

        // Nor is a table nested: an entry that is a table is not a whole number.
        var nested = TuningJson.Read("""{ "PerStack": { "Block": { "x": 1 } } }""");
        Assert.Contains("PerStack.Block is a table, not a whole number", Assert.Single(nested.Warnings));
    }

    [Fact]
    public void AHandEditedFileMayCommentAndTrailAComma()
    {
        // A human edits it (S:73-74): line and block comments anywhere, and the
        // trailing comma an edit leaves behind, are no mistake.
        const string file = """
            // the numbers for tonight's playtest
            {
              /* further */ "MovementBudget": 200, // a longer walk
              "PerStack": { "Block": 4, },
            }
            """;
        var read = TuningJson.Read(file);
        Assert.Empty(read.Warnings);
        Assert.Equal(["MovementBudget", "PerStack.Block"], TuningValues.Moved(Compiled, read.Tuning));

        // A key named twice — an edit that forgot the first — is read in order,
        // and said: the later number plays.
        var twice = TuningJson.Read("""{ "MovementBudget": 200, "MovementBudget": 220 }""");
        Assert.Equal(220, twice.Tuning.MovementBudget);
        Assert.Contains("MovementBudget is named twice", Assert.Single(twice.Warnings));
    }

    [Fact]
    public void AMalformedFileRunsTheCompiledNumbers_AndSaysWhichLine()
    {
        // A file that is not JSON at all costs the whole file — the compiled
        // numbers play — and one warning saying which line broke, never a crash.
        const string file = "{\n  \"MovementBudget\": 200\n  \"StartingPool\": 40\n}";
        var read = TuningJson.Read(file);
        string warning = Assert.Single(read.Warnings);
        Assert.StartsWith("tuning.json: line 3, column ", warning);
        Assert.Contains("is not valid JSON", warning);
        Assert.EndsWith("the built-in numbers are playing", warning);
        TuningValues.Same(Compiled, read.Tuning);

        // A file that is JSON but not a table of keys likewise.
        var list = TuningJson.Read("[1, 2, 3]");
        Assert.Equal(["tuning.json: the file is a list, not a table of keys - the built-in numbers are playing"], list.Warnings);
        TuningValues.Same(Compiled, list.Tuning);
    }

    [Fact]
    public void TheWriterWritesEveryKeyGroupedAndCommented_AndReadsBackToItself()
    {
        // PHASE5-080: every knob "gathered in one place ... seeing them on one
        // screen is how those get resolved". The file names every key once, under
        // §5.3's ten groups in §5.3's order (S:80-91) — the two whose keys wait for
        // Phase 6 as headings naming them — then the three beside it, each key
        // under its description; and it reads back to the numbers it was written
        // from, with nothing to warn about, whatever those numbers are.
        string text = TuningJson.Write(Compiled);
        var read = TuningJson.Read(text);
        Assert.Empty(read.Warnings);
        TuningValues.Same(Compiled, read.Tuning);
        Assert.DoesNotContain('\r', text);

        var turned = Compiled with
        {
            MovementBudget = 200,
            FriendlyFireEnabled = true,
            PerStack = new Dictionary<ModifierType, int> { [ModifierType.Block] = 4 },
            ApplyPercent = new Dictionary<string, int>(StringComparer.Ordinal) { ["arcane"] = 60 },
        };
        var back = TuningJson.Read(TuningJson.Write(turned));
        Assert.Empty(back.Warnings);
        TuningValues.Same(turned, back.Tuning);

        // Every key once, as a key of the file (a member line is indented deeper).
        foreach (var key in TuningSchema.Keys)
            Assert.Single(Regex.Matches(text, $"\n  \"{key.Name}\":"));

        // The groups in §5.3's order, then Progression, Combat, Drops; the two
        // deferred ones naming their keys and the phase that brings them.
        var headings = Enum.GetValues<TuningGroup>().Select(group => TuningSchema.Deferred.TryGetValue(group, out var deferred)
            ? $"/* ---- {group} - {deferred.Phase}: {string.Join(", ", deferred.Keys)} ---- */"
            : $"/* ---- {group} ---- */").ToArray();
        Assert.Equal(
            ["Economy", "Statuses", "Appliers", "Modifiers", "Farming", "Uniques", "Dungeon", "Enchanter", "Inventory", "Consumables", "Progression", "Combat", "Drops"],
            Enum.GetNames<TuningGroup>());
        Assert.Contains("/* ---- Enchanter - Phase 6a: WearPerHit, WearCapacityLadder, EnchantmentWearCost, GraftBaseChance ---- */", headings);
        Assert.Contains("/* ---- Consumables - Phase 6b: PotionUseCost, HealthPotionPercent, ManaPotionPercent, FarmYieldPerClear ---- */", headings);
        var at = headings.Select(heading => text.IndexOf(heading, StringComparison.Ordinal)).ToArray();
        Assert.All(at, index => Assert.True(index >= 0));
        Assert.Equal(at.Order(), at);

        // Each key sits under its own group's heading, with its description over it.
        foreach (var key in TuningSchema.Keys)
        {
            int line = text.IndexOf($"\n  \"{key.Name}\":", StringComparison.Ordinal);
            int group = (int)key.Group;
            Assert.True(line > at[group] && (group + 1 == at.Length || line < at[group + 1]), $"{key.Name} is outside {key.Group}");
            int comment = text.LastIndexOf("  /* ", line, StringComparison.Ordinal);
            Assert.StartsWith(key.Description[..40], text[(comment + 5)..]);
        }

        // The comparisons §5.3 wants on one screen (S:93-97): the mana divisor
        // against the budget and the cast costs, re-derived by hand; the farm
        // allowance against forged + 5; Searing's decay against the fight.
        const string derivation = "(MovementBudget 160 - a staff's 40) / (8 + 3 at Resonant x6) = 10.9 -> 10";
        foreach (string name in new[] { nameof(Tuning.MovementBudget), nameof(Tuning.MovementUnitsPerMana) })
        {
            Assert.Contains(derivation, Key(name).Description);
            Assert.Contains("re-derive", Key(name).Description);
        }
        Assert.Contains("forged + AcquiredHeadroom", Key(nameof(Tuning.AcquiredHeadroom)).Description);
        Assert.Contains("Searing burns for its level, then loses one", Key(nameof(Tuning.EffectPerLevel)).Description);

        // A provisional figure says so, and only a provisional one.
        string[] provisional =
            [nameof(Tuning.ReviveStepPercent), nameof(Tuning.DeathXpLossPercent), nameof(Tuning.DropEnchantChancePercent), nameof(Tuning.ArcanePotency)];
        Assert.Equal(provisional.Order(StringComparer.Ordinal),
            TuningSchema.Keys.Where(k => k.Description.StartsWith("Provisional:", StringComparison.Ordinal)).Select(k => k.Name).Order(StringComparer.Ordinal));
        var serrated = ((TableKey)Key(nameof(Tuning.ApplyPercent))).MemberNotes;
        Assert.Equal(["serrated"], serrated.Keys);
        Assert.StartsWith("Provisional:", serrated["serrated"]);
        Assert.Equal(provisional.Length + 1, Regex.Matches(text, @"/\* Provisional:").Count);
    }

    [Fact]
    public void TheHashIsOfTheValuesNotTheText()
    {
        // PHASE5-081 (A10): a save asks one question — "are these the same
        // numbers?" — so the hash is of the values the game plays: formatting,
        // order, comments, and naming a key at its default never move it; any one
        // number moved by one does, every such move to a hash of its own.
        string compiled = TuningJson.Hash(Compiled);
        Assert.Matches("^[0-9a-f]{16}$", compiled);

        foreach (string file in new[]
        {
            TuningJson.Write(Compiled),
            "{}",
            """{ "MovementBudget": 160 }""",
            "/* hi */ {\n\n  \"PerStack\": { \"Block\": 3 },\n    \"AcquiredHeadroom\" :5,\n}",
            """{ "AcquiredHeadroom": 5, "PerStack": { "Block": 3 } }""",
        })
            Assert.Equal(compiled, TuningJson.Hash(TuningJson.Read(file).Tuning));

        var hashes = new List<string> { compiled };
        foreach (var key in TuningSchema.Keys)
        {
            switch (key)
            {
                case IntKey k:
                    int value = k.Get(Compiled);
                    hashes.Add(TuningJson.Hash(k.Set(Compiled, value + 1 <= k.Max ? value + 1 : value - 1)));
                    break;
                case BoolKey k:
                    hashes.Add(TuningJson.Hash(k.Set(Compiled, !k.Get(Compiled))));
                    break;
                case TableKey k:
                    foreach (var (member, entry) in k.Get(Compiled))
                    {
                        var moved = new Dictionary<string, int>(k.Get(Compiled)) { [member] = entry + 1 <= k.Max ? entry + 1 : entry - 1 };
                        hashes.Add(TuningJson.Hash(k.Set(Compiled, moved)));
                    }
                    break;
            }
        }
        Assert.True(hashes.Count > 100);
        Assert.Equal(hashes.Count, hashes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TheOverridesAreWhatTheFileChanged()
    {
        // What the log names: every number the file changed from the compiled
        // ones, in the file's order — never a key set to its default.
        var read = TuningJson.Read("""{ "StartingPool": 30, "PerStack": { "Block": 4, "Brace": 1 }, "MovementBudget": 200 }""");
        Assert.Empty(read.Warnings);
        Assert.Equal(["MovementBudget 200", "PerStack.Block 4"], read.Overrides);

        // The load reports the numbers the game plays: a value clamped is what it
        // became, and the fit's raise is a change like any other.
        var load = ContentFiles.Load(new ContentTexts(Tuning: """{ "MovementUnitsPerMana": 0, "PartyCarrySlots": 4 }"""));
        Assert.Equal(["MovementUnitsPerMana 1", "PartyCarrySlots 8"], load.TuningOverrides);
    }

    [Fact]
    public void AnApplyPercentIdTheCatalogueLacksWarnsOnceItIsLoaded()
    {
        // 1g: ApplyPercent is keyed by enchantment id, which the reader cannot
        // check on its own — any id reads. Once the catalogue is known the loader
        // says an id it lacks names nothing, points at the id it resembles, and
        // drops it, so the tuning the game hashes holds only numbers something reads.
        var read = TuningJson.Read("""{ "ApplyPercent": { "flamming": 30 } }""");
        Assert.Empty(read.Warnings);

        var load = ContentFiles.Load(ContentFiles.WriteDefaults() with { Tuning = """{ "ApplyPercent": { "flamming": 30 } }""" });   // beside the shipped content files
        Assert.Equal(["tuning.json: ApplyPercent.flamming names no enchantment - ignored; did you mean flaming?"], load.Notices);
        Assert.False(load.Content!.Tuning.ApplyPercent.ContainsKey("flamming"));
        Assert.Equal(TuningJson.Hash(Compiled), TuningJson.Hash(load.Content.Tuning));
        Assert.Equal(20, load.Content.Enchantments["flaming"].ApplyPercent);
    }
}
