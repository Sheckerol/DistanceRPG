using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Every number of a content file has a range (the user's ruling of 2026-09-25,
/// R3(a): "since we are storing in raw json we should probably not trust the
/// fields"). Each number a content record holds has a row of
/// <see cref="ContentBounds"/>, with its reason; every number the masters ship
/// and the design docs fix lies inside its row; and one step outside a row stops
/// the game naming the file, the entry, the field, the number and the whole range
/// — under <see cref="ContentValidator.RuleNumberInRange"/>, or under the rule
/// that bounded the field before the ruling, for the end it keeps. The save reads
/// the content's own ceiling on a boss floor. Every edit is made the way a
/// playtester makes it, in the text beside the executable, and loaded as the game
/// loads it. In the content collection: the save's case starts a campaign under
/// the current content and restores under it.
/// </summary>
[Collection(TestContent.Collection)]
public class ContentBoundsTests
{
    private static readonly ContentTexts Shipped = DataTexts.Shipped;

    /// <summary>The HUD's glyphs, upper-cased: what the screen that stops the game can draw — no apostrophe, quote, brace, backslash or semicolon.</summary>
    private const string Font = @"^[A-Z0-9 .,:\-_!?()/<>\[\]+=#*]+$";

    private static (string? File, bool BuiltIn, string Entry, string Rule) Stamp(ContentException error)
        => (error.File, error.BuiltIn, error.EntryId, error.Rule);

    /// <summary>The detail a number outside its row is refused with: the field, the number, the whole range, and why.</summary>
    private static string Outside(NumberBound row, string field, int value)
        => $"{field} {value} is outside {row.Min} to {row.Max} - {row.Why}";

    /// <summary>Why the game does not start on <paramref name="texts"/>: the load's error, never an exception out of it.</summary>
    private static ContentException Fails(ContentTexts texts)
    {
        var load = ShippedData.Load(texts);
        Assert.Null(load.Content);
        return load.Error ?? throw new Xunit.Sdk.XunitException("the load has neither content nor an error");
    }

    /// <summary>Whether <paramref name="type"/> is a table of whole numbers by name: a forged spread, a boss's innate stacks.</summary>
    private static bool IsTableOfInts(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) && type.GetGenericArguments()[1] == typeof(int);

    private static bool IsNumber(Type type) => type == typeof(int) || type == typeof(int?) || IsTableOfInts(type);

    /// <summary>A shape's dimension by the name the file writes it under.</summary>
    private static int Dimension(AreaShape shape, string name) => name switch
    {
        "radius" => shape.Radius,
        "length" => shape.Length,
        "width" => shape.Width,
        "angleDegrees" => shape.AngleDegrees,
        "targetReach" => shape.TargetReach,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No dimension of a shape is called that."),
    };

    /// <summary>The place of <paramref name="id"/> among the file's rows: a variant's index in <c>variants</c>, which the reader's paths name.</summary>
    private static int IndexOf(string id) => ShippedContent.Weapons.Weapons.Select(w => w.Id).ToList().IndexOf(id);

    [Fact]
    public void EveryNumberOfAContentRecordHasABound()
    {
        // Every number a content record holds — an int, an int? or a table of ints —
        // is named by a row of ContentBounds.All, by where the file writes it: the
        // record's place in the file, then the field's own name camel-cased, a
        // table's members M. The one number the reader composes rather than reads,
        // a weapon's forged spread — a variant's class baseline plus its adds, a
        // unique's raises set over its variant — is named as the validator names
        // it, forged.M. So a number added to a content record later fails here
        // until it has a bound.
        (Type Record, string Place)[] records =
        [
            (typeof(WeaponClassDef), "classes.X."),
            (typeof(WeaponDef), "variants[i]."),
            (typeof(EnchantmentRef), "enchantments[j]."),
            (typeof(AreaShape), "variants[i].shape."),
            (typeof(DungeonDef), ""),
            (typeof(BossDef), "boss."),
        ];
        var rows = ContentBounds.All.ToDictionary(row => row.Field, StringComparer.Ordinal);   // one range a field
        foreach (var (record, place) in records)
        {
            var numbers = record.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => IsNumber(p.PropertyType)).ToList();
            Assert.NotEmpty(numbers);
            foreach (var property in numbers)
            {
                string field = record == typeof(WeaponDef) && property.Name == nameof(WeaponDef.Forged)
                    ? "forged.M"
                    : place + char.ToLowerInvariant(property.Name[0]) + property.Name[1..] + (IsTableOfInts(property.PropertyType) ? ".M" : "");
                Assert.True(rows.ContainsKey(field), $"{record.Name}.{property.Name} has no bound: no row of ContentBounds.All is {field}");
            }
        }

        // restricted.json holds no number: its relations are ids and groups of ids.
        Assert.DoesNotContain(typeof(RestrictedData).GetProperties(BindingFlags.Public | BindingFlags.Instance), p => IsNumber(p.PropertyType));

        // A row an older rule keeps an end of names that rule — the forge's rules a
        // forged count's, the boss floor range's a pinned floor's, the statline's a
        // boss's and the innate rule its stacks' — and every other row is the range
        // rule's alone. Each row is a range, in a content file, and its reason is
        // in the font of the screen that shows it.
        var kept = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["classes.X.baseline.M"] = ContentValidator.RuleForgedStackCount,
            ["forged.M"] = ContentValidator.RuleForgedStackCount,
            ["bossFloorMin"] = ContentValidator.RuleBossFloorRange,
            ["bossFloorMax"] = ContentValidator.RuleBossFloorRange,
            ["boss.maxHp"] = ContentValidator.RuleBossStatline,
            ["boss.movementBudget"] = ContentValidator.RuleBossStatline,
            ["boss.innate.M"] = ContentValidator.RuleBossInnateStackable,
        };
        foreach (var row in ContentBounds.All)
        {
            Assert.Equal(kept.GetValueOrDefault(row.Field), row.KeptBy);
            Assert.True(row.Min <= row.Max, $"{row.Field}: {row.Min} to {row.Max}");
            Assert.Contains(row.File, new[] { WeaponsJson.FileName, DungeonsJson.FileName });
            Assert.Matches(Font, row.Why.ToUpperInvariant());
        }

        // No bound is a master copy's value: every ceiling is a tuning.json clamp's,
        // read off the schema, so a clamp moved there moves the bound with it.
        static int Top(string key) => TuningSchema.Find(key) switch
        {
            IntKey k => k.Max,
            TableKey k => k.Max,
            _ => throw new Xunit.Sdk.XunitException($"the schema has no number key {key}"),
        };
        Assert.Equal(Top(nameof(Tuning.MaxForged)), ContentBounds.DeepestForge);
        Assert.Equal(Top(nameof(Tuning.MaxForged)) + Top(nameof(Tuning.AcquiredHeadroom)), ContentBounds.DeepestStack);
        Assert.Equal(Top(nameof(Tuning.BossFloorMax)), ContentBounds.DeepestBossFloor);
        Assert.Equal(Top(nameof(Tuning.PerStack)), ContentBounds.Magnitude);
        Assert.Equal(Top(nameof(Tuning.MovementBudget)), ContentBounds.Distance);
        Assert.Equal(Top(nameof(Tuning.StartingPool)), ContentBounds.Bar);
    }

    /// <summary>Every number <paramref name="weapons"/> and <paramref name="dungeons"/> hold, each with the row it is held to and where it is.</summary>
    private static IEnumerable<(NumberBound Row, string Where, int Value)> Numbers(WeaponsData weapons, DungeonsData dungeons)
    {
        foreach (var row in weapons.Classes)
        {
            string at = $"classes.{row.Class}";
            yield return (ContentBounds.ClassRange, $"{at}.range", row.Range);
            yield return (ContentBounds.ClassDamage, $"{at}.damage", row.Damage);
            yield return (ContentBounds.ClassCost, $"{at}.cost", row.Cost);
            yield return (ContentBounds.ClassManaCost, $"{at}.manaCost", row.ManaCost);
            foreach (var (t, n) in row.Baseline)
                yield return (ContentBounds.Baseline, $"{at}.baseline.{t}", n);
        }
        foreach (var def in weapons.Weapons)
        {
            yield return (ContentBounds.VariantRange, $"{def.Id}.range", def.Range);
            yield return (ContentBounds.VariantDamage, $"{def.Id}.damage", def.Damage);
            yield return (ContentBounds.VariantCost, $"{def.Id}.cost", def.Cost);
            yield return (ContentBounds.VariantManaCost, $"{def.Id}.manaCost", def.ManaCost);
            foreach (var (t, n) in def.Forged)
                yield return (ContentBounds.Forged, $"{def.Id}.forged.{t}", n);
            for (int j = 0; j < def.Enchantments.Count; j++)
                yield return (ContentBounds.Tier, $"{def.Id}.enchantments[{j}].tier", def.Enchantments[j].Tier);
            if (def.Shape is { } shape)
                foreach (string name in WeaponsJson.Dimensions[shape.Kind])
                    yield return (ContentBounds.Dimension(name), $"{def.Id}.shape.{name}", Dimension(shape, name));
        }
        foreach (var def in dungeons.Dungeons)
        {
            if (def.BossFloorMin is { } min)
                yield return (ContentBounds.BossFloorMin, $"{def.Id}.bossFloorMin", min);
            if (def.BossFloorMax is { } max)
                yield return (ContentBounds.BossFloorMax, $"{def.Id}.bossFloorMax", max);
            yield return (ContentBounds.BossMaxHp, $"{def.Id}.boss.maxHp", def.Boss.MaxHp);
            yield return (ContentBounds.BossMovementBudget, $"{def.Id}.boss.movementBudget", def.Boss.MovementBudget);
            foreach (var (t, n) in def.Boss.Innate)
                yield return (ContentBounds.BossInnate, $"{def.Id}.boss.innate.{t}", n);
        }
    }

    /// <summary>Every stack a variant of <paramref name="weapons"/>, a <c>weapons.json</c> text, adds, as the file writes it — before the reader sums it onto a baseline.</summary>
    private static List<(string Where, int Value)> Adds(string weapons)
    {
        using var document = JsonDocument.Parse(weapons, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var adds = new List<(string, int)>();
        int i = 0;
        foreach (var variant in document.RootElement.GetProperty("variants").EnumerateArray())
        {
            foreach (var add in variant.GetProperty("adds").EnumerateObject())
                adds.Add(($"variants[{i}].adds.{add.Name}", add.Value.GetInt32()));
            i++;
        }
        return adds;
    }

    [Fact]
    public void EveryBoundAdmitsTheShippedAndTheDocsValues()
    {
        // Every number the masters ship lies in its row, and so does every number a
        // design doc fixes — the premise's §1.2-§1.4 statlines, the staves' own
        // mana, the wands' shapes, and the golem's 80 HP and movement of 40 — and
        // every stack the master's variants add, read from its text as the reader
        // reads it. No bound refuses a shipped or a doc-fixed value, and every row
        // is asked of something the game ships.
        var asked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (source, weapons, dungeons) in new[] { ("the masters", ShippedContent.Weapons, ShippedContent.Dungeons), ("the docs", Premise.Weapons, Premise.Dungeons) })
            foreach (var (row, where, value) in Numbers(weapons, dungeons))
            {
                Assert.True(value >= row.Min && value <= row.Max, $"{source}: {where} is {value}, outside {row.Min} to {row.Max} ({row.Field})");
                asked.Add(row.Field);
            }
        var adds = Adds(Shipped.Weapons!);
        Assert.NotEmpty(adds);
        foreach (var (where, value) in adds)
        {
            Assert.True(value >= ContentBounds.Adds.Min && value <= ContentBounds.Adds.Max, $"{where} is {value}");
            asked.Add(ContentBounds.Adds.Field);
        }
        Assert.Equal(ContentBounds.All.Select(row => row.Field).Order(StringComparer.Ordinal), asked.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// One row's case as a playtester makes it: the entry an edit is made in — one
    /// where the row's extremes break nothing else — the field as the refusal names
    /// it, and the shipped texts with that one number set to a value. A number the
    /// reader asks is refused with its path in the file, before the file's name.
    /// </summary>
    private sealed record Case(NumberBound Row, string Entry, string Field, Func<int, ContentTexts> At, bool Read = false);

    /// <summary>The tutorial pinned to <paramref name="min"/>..<paramref name="max"/>, its master's pin read off the master.</summary>
    private static string TutorialPinned(int min, int max)
    {
        var tutorial = ShippedContent.Dungeons.Dungeons.Single(d => d.Id == "tutorial");
        string text = DataTexts.InRow(Shipped.Dungeons!, "tutorial", $"\"bossFloorMin\": {tutorial.BossFloorMin},", $"\"bossFloorMin\": {min},");
        return DataTexts.InRow(text, "tutorial", $"\"bossFloorMax\": {tutorial.BossFloorMax},", $"\"bossFloorMax\": {max},");
    }

    /// <summary>The Spear Hall, which pins neither end in the master, pinned at <paramref name="min"/> and <paramref name="max"/>: an end left null reads the tuning's, as the master's both do.</summary>
    private static string SpearHallPinned(int? min, int? max)
    {
        string text = Shipped.Dungeons!;
        if (min is { } shallower)
            text = DataTexts.InRow(text, "spear_hall", "\"bossFloorMin\": null,", $"\"bossFloorMin\": {shallower},");
        if (max is { } deeper)
            text = DataTexts.InRow(text, "spear_hall", "\"bossFloorMax\": null,", $"\"bossFloorMax\": {deeper},");
        return text;
    }

    /// <summary>A case for each numbered row: every number is read off the master, never written here.</summary>
    private static IEnumerable<Case> Cases()
    {
        var weapons = ShippedContent.Weapons;
        string text = Shipped.Weapons!;
        ContentTexts WithWeapons(string edited) => Shipped with { Weapons = edited };
        ContentTexts WithDungeons(string edited) => Shipped with { Dungeons = edited };

        // Rows 1-4: the Dagger class row's statline.
        var dagger = weapons.ClassOf(Dagger)!;
        foreach (var (row, field, shipped) in new[]
        {
            (ContentBounds.ClassRange, "range", dagger.Range),
            (ContentBounds.ClassDamage, "damage", dagger.Damage),
            (ContentBounds.ClassCost, "cost", dagger.Cost),
            (ContentBounds.ClassManaCost, "manaCost", dagger.ManaCost),
        })
            yield return new(row, nameof(Dagger), field, v => WithWeapons(DataTexts.InClass(text, Dagger, $"\"{field}\": {shipped}", $"\"{field}\": {v}")));

        // Row 5: a variant's own statline, which the Hatchet names beside its id.
        foreach (var (row, field) in new[]
        {
            (ContentBounds.VariantRange, "range"),
            (ContentBounds.VariantDamage, "damage"),
            (ContentBounds.VariantCost, "cost"),
            (ContentBounds.VariantManaCost, "manaCost"),
        })
            yield return new(row, "hatchet", field, v => WithWeapons(DataTexts.Edit(text, "\"id\": \"hatchet\",", $"\"id\": \"hatchet\", \"{field}\": {v},")));

        // Row 6: what the Riposte Blade adds, which the reader asks: 1 to the deepest forge.
        var blade = weapons.Weapons[IndexOf("riposte_blade")];
        int riposte = blade.Forged[Riposte] - weapons.ClassOf(blade.Class)!.Baseline.GetValueOrDefault(Riposte);
        yield return new(ContentBounds.Adds, "riposte_blade", $"variants[{IndexOf("riposte_blade")}].adds.Riposte",
            v => WithWeapons(DataTexts.InRow(text, "riposte_blade", $"\"Riposte\": {riposte}", $"\"Riposte\": {v}")), Read: true);

        // Row 7: the tier the Staff of Renewal's regeneration arrives at, and the
        // Nameless Knife's serrated, a unique soul, which plays at its pin.
        yield return new(ContentBounds.Tier, "staff_of_renewal", "enchantments[0].tier",
            v => WithWeapons(DataTexts.InRow(text, "staff_of_renewal", "{ \"id\": \"regeneration\" }", $"{{ \"id\": \"regeneration\", \"tier\": {v} }}")));
        yield return new(ContentBounds.Tier, "flensing_knife_unique", "enchantments[0].tier",
            v => WithWeapons(DataTexts.InRow(text, "flensing_knife_unique", "{ \"id\": \"serrated\" }", $"{{ \"id\": \"serrated\", \"tier\": {v} }}")));

        // Rows 8 and 9: each wand's own dimensions, every one its kind measures.
        foreach (var wand in weapons.Weapons.Where(w => w.Class == Wand && !w.Unique))
            foreach (string name in WeaponsJson.Dimensions[wand.Shape!.Kind])
            {
                int shipped = Dimension(wand.Shape, name);
                yield return new(ContentBounds.Dimension(name), wand.Id, $"shape.{name}",
                    v => WithWeapons(DataTexts.InRow(text, wand.Id, $"\"{name}\": {shipped}", $"\"{name}\": {v}")));
            }

        // Row 10: the tutorial pinned v..v — its floor RuleBossFloorRange's, its top
        // the range rule's, asked of the shallower end first; and the deeper end
        // alone, the Spear Hall pinned from the first floor to v — its floor the
        // boss floor range's too, never a range that inverts.
        yield return new(ContentBounds.BossFloorMin, "tutorial", "bossFloorMin", v => WithDungeons(TutorialPinned(v, v)));
        yield return new(ContentBounds.BossFloorMax, "spear_hall", "bossFloorMax", v => WithDungeons(SpearHallPinned(ContentBounds.BossFloorMin.Min, v)));

        // Rows 11-13: the golem's statline and its innate Block, each floor the rule's that kept it.
        string dungeons = Shipped.Dungeons!;
        var golem = ShippedContent.Dungeons.Dungeons.Single(d => d.Id == "tutorial").Boss;
        yield return new(ContentBounds.BossMaxHp, "tutorial", "boss.maxHp",
            v => WithDungeons(DataTexts.InRow(dungeons, "tutorial", $"\"maxHp\": {golem.MaxHp},", $"\"maxHp\": {v},")));
        yield return new(ContentBounds.BossMovementBudget, "tutorial", "boss.movementBudget",
            v => WithDungeons(DataTexts.InRow(dungeons, "tutorial", $"\"movementBudget\": {golem.MovementBudget},", $"\"movementBudget\": {v},")));
        yield return new(ContentBounds.BossInnate, "tutorial", "boss.innate.Block",
            v => WithDungeons(DataTexts.InRow(dungeons, "tutorial", $"\"Block\": {golem.Innate[Block]}", $"\"Block\": {v}")));
    }

    [Fact]
    public void ANumberOneStepOutsideItsBoundStopsTheGame_AtItsBoundItPlays()
    {
        // Every numbered row, edited in the text beside the game and loaded as the
        // game loads it — so tuning.json is fitted to it first: at its least and its
        // most the game plays; one below and one above, it does not start, naming
        // the file, the entry, the field, the number and the whole range — under the
        // range rule, or, for the end an older rule keeps, under that rule, its detail
        // in the same form.
        // Every row has its case here but two: the forged counts', both ends of
        // which an older rule keeps (TheBaselineAndTheRaisesKeepTheirRules).
        var cases = Cases().ToList();
        Assert.Equal(ContentBounds.All.Except([ContentBounds.Baseline, ContentBounds.Forged]).Select(row => row.Field).Order(StringComparer.Ordinal),
            cases.Select(c => c.Row.Field).Distinct().Order(StringComparer.Ordinal));
        foreach (var c in cases)
        {
            foreach (int v in new[] { c.Row.Min, c.Row.Max })
            {
                var load = ShippedData.Load(c.At(v));
                Assert.True(load.Error is null, $"{c.Entry} {c.Field} {v}: {load.Error?.Message}");
            }
            foreach (var (v, rule) in new[] { (c.Row.Min - 1, c.Row.KeptBy ?? ContentValidator.RuleNumberInRange), (c.Row.Max + 1, ContentValidator.RuleNumberInRange) })
            {
                var refused = Fails(c.At(v));
                Assert.Equal((c.Row.File, false, c.Entry, rule), Stamp(refused));
                Assert.Equal(c.Read ? $"{c.Row.File}: {c.Field}: {v} is outside {c.Row.Min} to {c.Row.Max} - {c.Row.Why}" : Outside(c.Row, c.Field, v), refused.Detail);
            }
        }

        // A soul written at the top of the row plays at its pin, as a soul written
        // anywhere does.
        var soul = ShippedData.Load(Shipped with
        {
            Weapons = DataTexts.InRow(Shipped.Weapons!, "flensing_knife_unique", "{ \"id\": \"serrated\" }", $"{{ \"id\": \"serrated\", \"tier\": {ContentBounds.Tier.Max} }}"),
        });
        Assert.Equal(Enchantment.UniqueTier, soul.Content!.Weapons.Instantiate("flensing_knife_unique").Enchantments[0].Tier);

        // One end pinned and the other the tuning's: the Spear Hall pinning its
        // bossFloorMax alone, or its bossFloorMin alone. At the deepest it plays; one
        // floor outside the range, the end it pins is named out of range with the
        // whole range — below the first floor the boss floor range's rule, past the
        // deepest the range rule — and never as a range inverted against the
        // tuning's other end.
        var tuning = ShippedContent.Tuning;
        Assert.Null(ShippedData.Load(Shipped with { Dungeons = SpearHallPinned(null, ContentBounds.BossFloorMax.Max) }).Error);
        foreach (var (row, pinned, v, rule) in new[]
        {
            (ContentBounds.BossFloorMax, SpearHallPinned(null, ContentBounds.BossFloorMax.Max + 1), ContentBounds.BossFloorMax.Max + 1, ContentValidator.RuleNumberInRange),
            (ContentBounds.BossFloorMax, SpearHallPinned(null, ContentBounds.BossFloorMax.Min - 1), ContentBounds.BossFloorMax.Min - 1, ContentValidator.RuleBossFloorRange),
            (ContentBounds.BossFloorMin, SpearHallPinned(ContentBounds.BossFloorMin.Max + 1, null), ContentBounds.BossFloorMin.Max + 1, ContentValidator.RuleNumberInRange),
        })
        {
            var outside = Fails(Shipped with { Dungeons = pinned });
            Assert.Equal((DungeonsJson.FileName, false, "spear_hall", rule), Stamp(outside));
            Assert.Equal(Outside(row, row.Field, v), outside.Detail);
        }

        // Both ends pinned, the deeper below the first floor: out of range as well,
        // never a range that inverts.
        var belowTheFirst = Fails(Shipped with { Dungeons = TutorialPinned(ContentBounds.BossFloorMin.Min, ContentBounds.BossFloorMax.Min - 1) });
        Assert.Equal((DungeonsJson.FileName, false, "tutorial", ContentValidator.RuleBossFloorRange), Stamp(belowTheFirst));
        Assert.Equal(Outside(ContentBounds.BossFloorMax, "bossFloorMax", ContentBounds.BossFloorMax.Min - 1), belowTheFirst.Detail);

        // An inverted range is two floors in range the wrong way round, and the boss
        // floor range's rule names it so, an end the dungeon leaves to the tuning
        // named as its key: both ends pinned; the shallower pinned past the tuning's
        // deeper; the deeper pinned short of a tuning's shallower.
        int first = ContentBounds.BossFloorMin.Min;
        foreach (var (entry, texts, detail) in new[]
        {
            ("tutorial", Shipped with { Dungeons = TutorialPinned(first + 1, first) }, $"bossFloorMax {first} is below bossFloorMin {first + 1}"),
            ("spear_hall", Shipped with { Dungeons = SpearHallPinned(tuning.BossFloorMax + 1, null) },
                $"tuning.json BossFloorMax {tuning.BossFloorMax} is below bossFloorMin {tuning.BossFloorMax + 1}"),
            ("spear_hall", Shipped with { Tuning = $$"""{ "{{nameof(Tuning.BossFloorMin)}}": {{first + 1}} }""", Dungeons = SpearHallPinned(null, first) },
                $"bossFloorMax {first} is below tuning.json BossFloorMin {first + 1}"),
        })
        {
            var inverted = Fails(texts);
            Assert.Equal((DungeonsJson.FileName, false, entry, ContentValidator.RuleBossFloorRange), Stamp(inverted));
            Assert.Equal(detail, inverted.Detail);
        }

        // Row 10's top holds the fit to its reason: at AcquiredHeadroom's clamp
        // floor a farm banks FarmBanks of it, and BossStackOffset's clamp reaches
        // far enough to keep a boss on the deepest floor short of that — asserted
        // off the schema, so a clamp moved later fails here rather than in play.
        var offsetKey = (IntKey)TuningSchema.Find(nameof(Tuning.BossStackOffset))!;
        var headroomKey = (IntKey)TuningSchema.Find(nameof(Tuning.AcquiredHeadroom))!;
        int needed = ContentBounds.DeepestBossFloor - ContentValidator.FarmBanks(headroomKey.Min) + 1;
        Assert.True(offsetKey.Max >= needed, $"{nameof(Tuning.BossStackOffset)} clamps at {offsetKey.Max}, and floor {ContentBounds.DeepestBossFloor} needs {needed}");

        // So the tutorial pinned there under that tuning plays, the fit raising the
        // offset to it and saying so, naming the tutorial and its floor...
        string shallowFarm = $$"""{ "{{nameof(Tuning.AcquiredHeadroom)}}": {{headroomKey.Min}} }""";
        var deepest = ShippedData.Load(Shipped with { Tuning = shallowFarm, Dungeons = TutorialPinned(ContentBounds.DeepestBossFloor, ContentBounds.DeepestBossFloor) });
        Assert.Null(deepest.Error);
        Assert.Equal(needed, deepest.Content!.Tuning.BossStackOffset);
        Assert.Contains(deepest.Notices, notice => notice.StartsWith($"tuning.json: {nameof(Tuning.BossStackOffset)} ", StringComparison.Ordinal)
            && notice.Contains($"the tutorial boss on floor {ContentBounds.DeepestBossFloor} ", StringComparison.Ordinal)
            && notice.EndsWith($"- using {needed}", StringComparison.Ordinal));

        // ...and one floor deeper stops the game on the range rule, and the fit says
        // nothing of the tutorial: it raised no offset for a file it could not serve.
        var past = ShippedData.Load(Shipped with { Tuning = shallowFarm, Dungeons = TutorialPinned(ContentBounds.DeepestBossFloor + 1, ContentBounds.DeepestBossFloor + 1) });
        Assert.Equal((DungeonsJson.FileName, false, "tutorial", ContentValidator.RuleNumberInRange), Stamp(past.Error!));
        Assert.Equal(Outside(ContentBounds.BossFloorMin, "bossFloorMin", ContentBounds.DeepestBossFloor + 1), past.Error!.Detail);
        Assert.DoesNotContain(past.Notices, notice => notice.Contains("tutorial", StringComparison.Ordinal));
        var pastShipped = ShippedData.Load(Shipped with { Dungeons = TutorialPinned(ContentBounds.DeepestBossFloor + 1, ContentBounds.DeepestBossFloor + 1) });
        Assert.NotNull(pastShipped.Error);
        Assert.DoesNotContain(pastShipped.Notices, notice => notice.Contains(nameof(Tuning.BossStackOffset), StringComparison.Ordinal));
    }

    [Fact]
    public void AnAddIsCheckedAsItIsRead()
    {
        // What a variant adds is asked as the reader reads it, before it is summed
        // onto its class's baseline: summed, a -1 on a baseline modifier would read
        // as a stack one short and a 0 as the baseline's own, and the largest whole
        // number would wrap below zero. So the refusal names the number the file
        // wrote, at its path — never a composed or a wrapped count.
        string text = Shipped.Weapons!;
        var weapons = ShippedContent.Weapons;
        var blade = weapons.Weapons[IndexOf("riposte_blade")];
        int riposte = blade.Forged[Riposte] - weapons.ClassOf(blade.Class)!.Baseline.GetValueOrDefault(Riposte);
        var fang = weapons.Weapons[IndexOf("assassins_fang")];
        int window = fang.Forged[CritWindow] - weapons.ClassOf(fang.Class)!.Baseline[CritWindow];   // the Purity variant deepens its signature
        var bound = ContentBounds.Adds;

        (string Id, string From, ModifierType Modifier, int Value)[] cases =
        [
            ("riposte_blade", $"\"Riposte\": {riposte}", Riposte, -1),
            ("riposte_blade", $"\"Riposte\": {riposte}", Riposte, 0),
            ("riposte_blade", $"\"Riposte\": {riposte}", Riposte, bound.Max + 1),
            ("riposte_blade", $"\"Riposte\": {riposte}", Riposte, int.MaxValue),
            ("assassins_fang", $"\"CritWindow\": {window}", CritWindow, -1),
            ("assassins_fang", $"\"CritWindow\": {window}", CritWindow, 0),
            ("assassins_fang", $"\"CritWindow\": {window}", CritWindow, int.MaxValue),
        ];
        foreach (var (id, from, modifier, value) in cases)
        {
            string edited = DataTexts.InRow(text, id, from, $"\"{modifier}\": {value}");
            var read = Assert.Throws<ContentException>(() => WeaponsJson.Read(edited));
            Assert.Equal((id, ContentValidator.RuleNumberInRange), (read.EntryId, read.Rule));
            Assert.Equal($"weapons.json: variants[{IndexOf(id)}].adds.{modifier}: {value} is outside {bound.Min} to {bound.Max} - {bound.Why}", read.Detail);

            // Through the load it is weapons.json's shape stage that stops the game.
            Assert.Equal((WeaponsJson.FileName, false, id, ContentValidator.RuleNumberInRange), Stamp(Fails(Shipped with { Weapons = edited })));
        }

        // The ends of the row read: the Riposte Blade adding as deep as any forge goes.
        var deepest = WeaponsJson.Read(DataTexts.InRow(text, "riposte_blade", $"\"Riposte\": {riposte}", $"\"Riposte\": {bound.Max}"));
        Assert.Equal(bound.Max, deepest.Weapons.Single(w => w.Id == "riposte_blade").Forged[Riposte]);
    }

    [Fact]
    public void ARecordBuiltInCodeIsHeldToTheSameBounds()
    {
        // Records built in code bypass the reader and meet the same ranges in the
        // validator: a class row's reach of 0, a variant's cost of 0, a Blast of no
        // radius, an enchantment arriving at tier 4 — a staff's innate, and a soul —
        // and a boss with more hit points than any bar opens on. Each is refused at
        // its stage, naming the entry, the field and the whole range.
        var weapons = ShippedContent.Weapons;
        WeaponsData Editing(string id, Func<WeaponDef, WeaponDef> edit)
            => weapons with { Weapons = weapons.Weapons.Select(w => w.Id == id ? edit(w) : w).ToList() };
        int tooDeep = ContentBounds.Tier.Max + 1;

        (Func<GameContent> Load, string File, string Entry, string Detail)[] cases =
        [
            (() => TestContent.Load(weapons: weapons with { Classes = weapons.Classes.Select(row => row.Class == Dagger ? row with { Range = 0 } : row).ToList() }),
                WeaponsJson.FileName, nameof(Dagger), Outside(ContentBounds.ClassRange, "range", 0)),
            (() => TestContent.Load(weapons: Editing("hatchet", w => w with { Cost = 0 })),
                WeaponsJson.FileName, "hatchet", Outside(ContentBounds.VariantCost, "cost", 0)),
            (() => TestContent.Load(weapons: Editing("wand_of_the_blast", w => w with { Shape = w.Shape! with { Radius = 0 } })),
                WeaponsJson.FileName, "wand_of_the_blast", Outside(ContentBounds.Radius, "shape.radius", 0)),
            (() => TestContent.Load(weapons: Editing("staff_of_renewal", w => w with { Enchantments = [new EnchantmentRef("regeneration", tooDeep)] })),
                WeaponsJson.FileName, "staff_of_renewal", Outside(ContentBounds.Tier, "enchantments[0].tier", tooDeep)),
            (() => TestContent.Load(weapons: Editing("flensing_knife_unique", w => w with { Enchantments = [new EnchantmentRef("serrated", tooDeep), .. w.Enchantments.Skip(1)] })),
                WeaponsJson.FileName, "flensing_knife_unique", Outside(ContentBounds.Tier, "enchantments[0].tier", tooDeep)),
            (() => TestContent.Load(dungeons: new DungeonsData(ShippedContent.Dungeons.Dungeons
                    .Select(d => d.Id == "tutorial" ? d with { Boss = d.Boss with { MaxHp = ContentBounds.Bar + 1 } } : d).ToList())),
                DungeonsJson.FileName, "tutorial", Outside(ContentBounds.BossMaxHp, "boss.maxHp", ContentBounds.Bar + 1)),
        ];
        foreach (var (load, file, entry, detail) in cases)
        {
            var refused = Assert.Throws<ContentException>(load);
            Assert.Equal((file, false, entry, ContentValidator.RuleNumberInRange), Stamp(refused));
            Assert.Equal(detail, refused.Detail);
        }
    }

    [Fact]
    public void TheBaselineAndTheRaisesKeepTheirRules()
    {
        // A forged count was bounded at both ends before the ruling, by the forge's
        // own rules — a stack at least (RuleForgedStackCount), and none deeper than
        // its modifier's limit (RuleMaxForged) — and they keep both: the same rules
        // and entries, each detail now naming the field and the whole range, the top
        // the tuned limit of the modifier. A class row's baseline says it is the
        // baseline's; a unique's raises, set rather than summed, is its composed
        // spread's number, named as the file writes it (raises.CritWindow). Past the deepest forge there is, no tuning can be fitted
        // to it.
        string text = Shipped.Weapons!;
        var tuning = ShippedContent.Tuning;
        int pastAnyForge = ContentBounds.DeepestForge + 1;

        var dagger = ShippedContent.Weapons.ClassOf(Dagger)!;
        ContentException Baseline(int v)
            => Fails(Shipped with { Weapons = DataTexts.InClass(text, Dagger, $"\"CritMultiplier\": {dagger.Baseline[CritMultiplier]}", $"\"CritMultiplier\": {v}") });
        int multiplier = tuning.MaxForged[CritMultiplier];
        var zero = Baseline(0);
        Assert.Equal((WeaponsJson.FileName, false, nameof(Dagger), ContentValidator.RuleForgedStackCount), Stamp(zero));
        Assert.Equal($"baseline.CritMultiplier 0 is outside 1 to {multiplier} - {ContentBounds.Baseline.Why}", zero.Detail);
        var deep = Baseline(pastAnyForge);
        Assert.Equal((WeaponsJson.FileName, false, nameof(Dagger), ContentValidator.RuleMaxForged), Stamp(deep));
        Assert.Equal($"baseline.CritMultiplier {pastAnyForge} is outside 1 to {multiplier} - {ContentBounds.Baseline.Why}", deep.Detail);

        var widowmaker = ShippedContent.Weapons.Weapons.Single(w => w.Id == "widowmaker");
        ContentException Raises(int v)
            => Fails(Shipped with { Weapons = DataTexts.InRow(text, "widowmaker", $"\"CritWindow\": {widowmaker.Forged[CritWindow]}", $"\"CritWindow\": {v}") });
        int window = tuning.MaxForged[CritWindow];
        var none = Raises(0);
        Assert.Equal((WeaponsJson.FileName, false, "widowmaker", ContentValidator.RuleForgedStackCount), Stamp(none));
        Assert.Equal($"raises.CritWindow 0 is outside 1 to {window} - {ContentBounds.Forged.Why}", none.Detail);
        var raised = Raises(pastAnyForge);
        Assert.Equal((WeaponsJson.FileName, false, "widowmaker", ContentValidator.RuleMaxForged), Stamp(raised));
        Assert.Equal($"raises.CritWindow {pastAnyForge} is outside 1 to {window} - {ContentBounds.Forged.Why}", raised.Detail);

        // Every one of them is drawn in the font of the screen that stops the game.
        foreach (var error in new[] { zero, deep, none, raised })
            Assert.All(StartupErrorScene.Lines(error, [], [WeaponsJson.FileName]), row => Assert.Matches(Font, row.Trim()));
    }

    [Fact]
    public void TheSaveReadsTheDeepestBossFloorTheContentDoes()
    {
        // The save's ceiling on a boss floor is the content's: ContentBounds.DeepestBossFloor,
        // the deepest floor any tuning names and any dungeon may pin. A save naming a
        // deeper boss names a dungeon no content could make, and is refused at its
        // path before a load builds a floor of it; one at the ceiling loads, every
        // floor below the party generated from the seed.
        var campaign = CampaignState.Start(GameContent.Current);
        campaign.Enter("spear_hall", mapSeed: 4);
        string text = SaveCodec.Write(SaveGame.Capture(campaign));
        SaveFile BossOn(int floor)
        {
            var tree = JsonNode.Parse(text)!.AsObject();
            tree["campaign"]!["visit"]!["bossFloor"] = floor;
            return SaveCodec.Read(tree.ToJsonString());
        }

        int deepest = ContentBounds.DeepestBossFloor;
        var refused = Assert.Throws<SaveFormatException>(() => SaveGame.Restore(BossOn(deepest + 1), GameContent.Current));
        Assert.Equal("campaign.visit.bossFloor", refused.Where);
        Assert.Equal($"{deepest + 1} is no boss floor: a boss waits on floor 1 to {deepest}", refused.Problem);

        var visit = SaveGame.Restore(BossOn(deepest), GameContent.Current).Campaign.Visit!;
        Assert.Equal(deepest, visit.BossFloor);
        Assert.Equal(deepest, visit.Floors.Length);

        // Both saves the pre-rework code wrote load, their boss floors inside it.
        foreach (string name in new[] { "pre-rework-mid-run.json", "pre-rework-between-runs.json" })
        {
            var file = SaveCodec.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "saves", name)));
            var restored = SaveGame.Restore(file, GameContent.Current).Campaign;
            if (restored.Visit is { } saved)
                Assert.InRange(saved.BossFloor, 1, deepest);
        }
    }
}
