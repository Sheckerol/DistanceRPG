using System.Text.Json;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.6's <c>weapons.json</c> as a file (PHASE5-122, -123, -124, -133): the eight
/// classes, the thirty-two variants and the uniques, in three sections — "the
/// class baselines leave Weapons.cs" into the class rows (the content rework, 1d;
/// Phase 5 USER DECISION 4, ruled), a variant written as its class and the one
/// modifier it adds, a unique as its variant and what it raises; read strictly,
/// with a shape's zero dimensions and a tier of 1 left out; and a
/// <c>restricted.json</c> edit that makes a weapon illegal stopping the game
/// naming both — the weapon, or its class where the relation refuses the class's
/// baseline, and the relation that refused it, asked of the one
/// predicate every graft asks (<see cref="ModifierRules.Allowed"/>) and explained
/// by <see cref="ContentValidator.WhyNotAllowed"/>. Every case reads text or
/// builds rules it never makes current; the shipped file is the master copy the
/// build carries (<see cref="ShippedData.Texts"/>).
/// </summary>
public class WeaponsJsonTests
{
    private static readonly string Shipped = DataTexts.Shipped.Weapons!;

    private static readonly string ShippedRestricted = DataTexts.Shipped.Restricted!;

    private static readonly string[] ClassFields = ["kind", "stats", "signature", "baseline", "range", "damage", "cost", "manaCost"];

    /// <summary>A variant's fields: the seven every variant names, then the statline it may name as its own.</summary>
    private static readonly string[] VariantFields = ["id", "name", "class", "role", "adds", "enchantments", "shape", "range", "damage", "cost", "manaCost"];

    private static readonly string[] UniqueFields = ["id", "name", "derivedFrom", "raises", "enchantments"];

    private static ContentLoad Load(string? weapons = null, string? restricted = null)
        => ShippedData.Load(DataTexts.Shipped with { Weapons = weapons ?? Shipped, Restricted = restricted ?? ShippedRestricted });

    /// <summary>The four relations edits the section's tests make, as restricted.json texts, each passing every restricted and enchantment stage first.</summary>
    private static readonly string PushBlocks = DataTexts.Edit(ShippedRestricted, "[\"Light\", \"Resonant\"],", "[\"Light\", \"Resonant\"],\n    [\"Push\", \"Block\"],");
    private static readonly string RiposteNeedsBrace = DataTexts.Edit(ShippedRestricted, "\"Riposte\": [\"Block\"]", "\"Riposte\": [\"Brace\"]");
    private static readonly string PushIsRanged = DataTexts.Edit(ShippedRestricted, "\"Overwatch\": \"ranged\"", "\"Overwatch\": \"ranged\",\n    \"Push\": \"ranged\"");
    private static readonly string RegenerationExcludesOverheal = DataTexts.Edit(ShippedRestricted, "[\"shocking\", \"acidic\"]", "[\"shocking\", \"acidic\"],\n    [\"regeneration\", \"overheal\"]");
    private static readonly string SerratedNeedsVampiric = DataTexts.Edit(ShippedRestricted, "\"burning\": [\"flaming\"]", "\"burning\": [\"flaming\"],\n    \"serrated\": [\"vampiric\"]");

    /// <summary>Two weapon rows the same, field for field — the spread and the enchantments by content, since the record holds them by reference.</summary>
    internal static void Same(WeaponDef expected, WeaponDef actual)
    {
        Assert.Equal(
            (expected.Id, expected.Name, expected.Class, expected.Role, expected.Range, expected.Damage, expected.Cost, expected.ManaCost, expected.Shape, expected.Unique, expected.DerivedFrom),
            (actual.Id, actual.Name, actual.Class, actual.Role, actual.Range, actual.Damage, actual.Cost, actual.ManaCost, actual.Shape, actual.Unique, actual.DerivedFrom));
        Assert.Equal(expected.Forged.OrderBy(entry => entry.Key), actual.Forged.OrderBy(entry => entry.Key));
        Assert.Equal(expected.Enchantments, actual.Enchantments);
    }

    [Fact]
    public void TheFileIsTheCatalogue_ThirtyTwoVariantsAndTheUniques()
    {
        // PHASE5-123, -124: "the 32 weapons, the uniques, and the class baselines
        // leave Weapons.cs" — "statline, forged spread, area shape, innate
        // enchantment id". The shipped file is the master copy of every weapon
        // and every class: no second record of them is kept to hold it to.
        string text = ShippedData.Texts.Weapons!;
        var data = WeaponsJson.Read(text);
        var read = data.Weapons;
        Assert.Equal(32, read.Count(w => !w.Unique));
        Assert.Equal(11, read.Count(w => w.Unique));

        // Three sections (the content rework, 1d; Phase 5 USER DECISION 4, ruled):
        // the classes, by their C# names, every one in the enum's order, each
        // naming all eight of a class's fields in the record's order — and a
        // caster's its drop order after them (the rework's audit); the
        // variants, each naming the seven every variant names, and a statline
        // field only where it plays its own; the uniques, each naming its five.
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        Assert.Equal(["classes", "variants", "uniques"], document.RootElement.EnumerateObject().Select(member => member.Name));
        var classes = document.RootElement.GetProperty("classes").EnumerateObject().ToList();
        Assert.Equal(Enum.GetNames<WeaponClass>(), classes.Select(member => member.Name));
        Assert.All(classes, member => Assert.Equal(
            data.ClassOf(Enum.Parse<WeaponClass>(member.Name))!.Kind == WeaponKind.Caster ? [.. ClassFields, "dropOrder"] : ClassFields,
            member.Value.EnumerateObject().Select(field => field.Name)));
        var variantRows = document.RootElement.GetProperty("variants").EnumerateArray().ToList();
        var uniqueRows = document.RootElement.GetProperty("uniques").EnumerateArray().ToList();
        Assert.All(variantRows, row => Assert.Equal(VariantFields.Take(7), row.EnumerateObject().Select(field => field.Name).Where(name => VariantFields.Take(7).Contains(name))));
        Assert.All(variantRows, row => Assert.Subset(VariantFields.ToHashSet(), row.EnumerateObject().Select(field => field.Name).ToHashSet()));
        Assert.All(uniqueRows, row => Assert.Equal(UniqueFields, row.EnumerateObject().Select(field => field.Name)));

        // The statline is the class's, and a variant's own where it names one; the
        // forged spread the class baseline plus what the variant adds, by modifier
        // name; a wand's shape; the enchantments by id, with a tier where it is not 1.
        JsonElement Row(string id) => variantRows.Concat(uniqueRows).Single(row => row.GetProperty("id").GetString() == id);
        WeaponDef Played(string id) => read.Single(w => w.Id == id);
        var knife = Row("flensing_knife");
        Assert.Equal(("Dagger", "Efficiency"), (knife.GetProperty("class").GetString(), knife.GetProperty("role").GetString()));
        var dagger = data.ClassOf(WeaponClass.Dagger)!;
        int Own(string field, int classValue) => knife.TryGetProperty(field, out var value) ? value.GetInt32() : classValue;
        Assert.Equal((Own("range", dagger.Range), Own("damage", dagger.Damage), Own("cost", dagger.Cost), Own("manaCost", dagger.ManaCost)),
            (Played("flensing_knife").Range, Played("flensing_knife").Damage, Played("flensing_knife").Cost, Played("flensing_knife").ManaCost));
        Assert.Equal(["Light"], knife.GetProperty("adds").EnumerateObject().Select(member => member.Name));
        Assert.Equal([CritWindow, CritMultiplier, Light], Played("flensing_knife").Forged.Keys);
        var mire = Row("staff_of_mire");
        Assert.Equal(["manaCost"], mire.EnumerateObject().Select(field => field.Name).Except(VariantFields.Take(7)));
        Assert.Equal((data.ClassOf(WeaponClass.Staff)!.Range, mire.GetProperty("manaCost").GetInt32()), (Played("staff_of_mire").Range, Played("staff_of_mire").ManaCost));
        var blast = Played("wand_of_the_blast").Shape!;
        Assert.Equal($"{{ \"kind\": \"Blast\", \"radius\": {blast.Radius}, \"targetReach\": {blast.TargetReach} }}", Row("wand_of_the_blast").GetProperty("shape").GetRawText());
        Assert.Equal(JsonValueKind.Null, knife.GetProperty("shape").ValueKind);
        Assert.Equal("[{ \"id\": \"serrated\" }, { \"id\": \"vampiric\", \"tier\": 3 }, { \"id\": \"overheal\" }]",
            Row("flensing_knife_unique").GetProperty("enchantments").GetRawText());
        Assert.Equal("[{ \"id\": \"regeneration\" }]", Row("staff_of_renewal").GetProperty("enchantments").GetRawText());
        Assert.Equal("flensing_knife", Row("flensing_knife_unique").GetProperty("derivedFrom").GetString());
        Assert.Null(Played("flensing_knife_unique").Role);

        // The baselines are the rows': each martial class names its signature and a
        // baseline of it and one other modifier, as RuleClassBaseline checks; a
        // caster no signature, as RuleCasterForged checks, and the Resonant x1 its
        // row forges (§1.3, §1.4, the rows' design rather than any rule's). Each is
        // exactly what the class's four variants share. The file loads, so it checked.
        var baselines = new Dictionary<WeaponClass, (ModifierType? Signature, ModifierType[] Baseline)>
        {
            [WeaponClass.Dagger] = (CritWindow, [CritWindow, CritMultiplier]),
            [WeaponClass.Sword] = (Block, [Block, Push]),
            [WeaponClass.Spear] = (Brace, [Brace, Longshot]),
            [WeaponClass.Axe] = (Cleave, [Cleave, Opportunist]),
            [WeaponClass.Ranged] = (Longshot, [Longshot, CritWindow]),
            [WeaponClass.Throwing] = (Charges, [Charges, CritMultiplier]),
            [WeaponClass.Staff] = (null, [Resonant]),
            [WeaponClass.Wand] = (null, [Resonant]),
        };
        foreach (var (cls, (signature, baseline)) in baselines)
        {
            var row = data.ClassOf(cls)!;
            Assert.Equal(signature, row.Signature);
            Assert.Equal(baseline.ToHashSet(), row.Baseline.Keys.ToHashSet());
            Assert.All(row.Baseline.Values, stacks => Assert.Equal(1, stacks));

            var variants = read.Where(w => w.Class == cls && !w.Unique).ToList();
            Assert.Equal(4, variants.Count);
            var shared = Enum.GetValues<ModifierType>().Where(t => variants.All(v => v.Forged.GetValueOrDefault(t) > 0)).ToHashSet();
            Assert.Equal(baseline.ToHashSet(), shared);
        }
        Assert.Null(Load(text).Error);
    }

    [Fact]
    public void ADefaultShapeDimensionOrTierIsOmitted_AndReadsBack()
    {
        // 1h: a shape's zero dimensions and an enchantment's tier of 1 may be left
        // out, and read as the number left out: a Nova names its radius alone, a
        // Beam its length and width, a tier-1 soul its id alone. The dimensions
        // are the master's (WandShapes_MatchTheTable pins them to §1.4's table).
        var read = WeaponsJson.Read(Shipped).Weapons;
        AreaShape ShapeOf(string id) => read.Single(w => w.Id == id).Shape!;
        var (nova, beam, cone) = (ShapeOf("wand_of_the_nova"), ShapeOf("wand_of_the_beam"), ShapeOf("wand_of_the_cone"));
        string novaText = $"\"shape\": {{ \"kind\": \"Nova\", \"radius\": {nova.Radius} }}";
        Assert.Contains(novaText, Shipped);
        Assert.Contains($"\"shape\": {{ \"kind\": \"Beam\", \"length\": {beam.Length}, \"width\": {beam.Width} }}", Shipped);
        Assert.Contains($"\"shape\": {{ \"kind\": \"Cone\", \"length\": {cone.Length}, \"angleDegrees\": {cone.AngleDegrees} }}", Shipped);
        Assert.Equal(AreaShape.Nova(radius: nova.Radius), nova);
        Assert.Equal(AreaShape.Cone(angleDegrees: cone.AngleDegrees, length: cone.Length), cone);
        Assert.Equal([new EnchantmentRef("serrated", 1), new EnchantmentRef("vampiric", 3), new EnchantmentRef("overheal", 1)],
            read.Single(w => w.Id == "flensing_knife_unique").Enchantments);

        // Written out, a default is the same number as left out: a Nova's width,
        // which it does not measure, as the 0 it is, and a soul's tier as 1.
        string spelled = DataTexts.Edit(DataTexts.InRow(Shipped, "wand_of_the_nova",
            novaText, novaText[..^2] + ", \"width\": 0 }"),
            "[{ \"id\": \"serrated\" }, { \"id\": \"vampiric\", \"tier\": 3 }", "[{ \"id\": \"serrated\", \"tier\": 1 }, { \"id\": \"vampiric\", \"tier\": 3 }");
        var spelledOut = WeaponsJson.Read(spelled).Weapons;
        Assert.Equal(read.Count, spelledOut.Count);
        foreach (var (left, written) in read.Zip(spelledOut))
            Same(left, written);

        // A dimension its kind does not measure is a number nothing reads, and is
        // refused naming the path and what the kind does measure.
        var stray = Assert.Throws<ContentException>(() => WeaponsJson.Read(DataTexts.InRow(Shipped, "wand_of_the_nova",
            novaText, novaText[..^2] + ", \"length\": 64 }")));
        Assert.Equal(("wand_of_the_nova", ContentValidator.RuleFileShape, "weapons.json: variants[31].shape.length: is not a dimension of a Nova - a Nova measures radius"),
            (stray.EntryId, stray.Rule, stray.Detail));
        var kind = Assert.Throws<ContentException>(() => WeaponsJson.Read(DataTexts.InRow(Shipped, "wand_of_the_nova", "\"kind\": \"Nova\"", "\"kind\": \"Ring\"")));
        Assert.Equal(("wand_of_the_nova", ContentValidator.RuleFileShape), (kind.EntryId, kind.Rule));
        Assert.StartsWith("weapons.json: variants[31].shape.kind: is the text Ring, which names no AreaShapeKind", kind.Detail);
    }

    [Fact]
    public void AnUnknownMemberOrAWrongTypeIsRefusedNamingThePath()
    {
        // Content is strict (1h): a member the shape does not have, a value of the
        // wrong kind, a field left out — each refused under the file's shape,
        // naming the weapon and the path. A modifier that is no modifier is an id
        // that resolves to nothing, pointing at the one it is nearest. The paths
        // are the three sections' (the content rework, 1d): the Arming Sword is
        // variants[4], the Staff of Renewal variants[24].
        (string Text, string Entry, string Rule, string Detail)[] cases =
        [
            (DataTexts.InRow(Shipped, "arming_sword", "\"adds\": { \"Light\": 1 }", "\"adds\": { \"Blok\": 1 }"), "arming_sword", ContentValidator.RuleUnknownId,
                "weapons.json: variants[4].adds.Blok: is not a modifier - did you mean Block?"),
            // A class's field on a variant: the baseline is its class row's to name.
            (DataTexts.InRow(Shipped, "arming_sword", "\"shape\": null", "\"shape\": null,\n      \"baseline\": null"), "arming_sword", ContentValidator.RuleFileShape,
                "weapons.json: variants[4].baseline: is not a member here - the members are " + string.Join(", ", VariantFields)),
            (DataTexts.InRow(Shipped, "arming_sword", "\"class\": \"Sword\",", "\"class\": \"Swords\","), "arming_sword", ContentValidator.RuleFileShape,
                "weapons.json: variants[4].class: is the text Swords, which names no WeaponClass - did you mean Sword?"),
            // A statline the variant names as its own is a whole number, like the class's.
            (DataTexts.InRow(Shipped, "arming_sword", "\"adds\": { \"Light\": 1 },", "\"adds\": { \"Light\": 1 }, \"range\": \"80\","), "arming_sword", ContentValidator.RuleFileShape,
                "weapons.json: variants[4].range: is the text 80, not a whole number"),
            (DataTexts.InRow(Shipped, "arming_sword", "\"role\": \"Efficiency\", ", ""), "arming_sword", ContentValidator.RuleFileShape,
                "weapons.json: variants[4].role: is missing"),
            (DataTexts.InRow(Shipped, "arming_sword", "\"adds\": { \"Light\": 1 }", "\"adds\": { \"Light\": 1, \"Light\": 2 }"), "arming_sword",
                ContentValidator.RuleFileShape, "weapons.json: variants[4].adds.Light: is named twice"),
            (DataTexts.InRow(Shipped, "staff_of_renewal", "[{ \"id\": \"regeneration\" }]", "[\"regeneration\"]"), "staff_of_renewal", ContentValidator.RuleFileShape,
                "weapons.json: variants[24].enchantments[0]: is the text regeneration, not an enchantment: a table of its id and, when it is not 1, its tier"),
            ("{ \"classes\": [], \"variants\": [], \"uniques\": [] }", WeaponsJson.FileName, ContentValidator.RuleFileShape,
                "weapons.json: classes: is a list, not a table of each class to its row"),
        ];
        foreach (var (text, entry, rule, detail) in cases)
        {
            var thrown = Assert.Throws<ContentException>(() => WeaponsJson.Read(text));
            Assert.Equal((entry, rule, detail), (thrown.EntryId, thrown.Rule, thrown.Detail));

            // Through the loader, at the weapons' stage, stamped with the file.
            var error = Load(text).Error!;
            Assert.Equal((entry, rule, detail, WeaponsJson.FileName, false), (error.EntryId, error.Rule, error.Detail, error.File, error.BuiltIn));
        }

        // Comments and trailing commas are fine: a human edits the file.
        string relaxed = DataTexts.InRow(Shipped, "arming_sword", "\"shape\": null", "\"shape\": null, // the dummies' sword\n");
        Assert.Null(Load(relaxed).Error);
    }

    [Fact]
    public void ARelationsEditThatBreaksAWeaponNamesBoth()
    {
        // PHASE5-122: "relations load before content, so a relations edit that
        // invalidates an existing weapon aborts startup naming both" — "the weapon
        // that is now illegal and the rule that made it so", so that "the error
        // tells you which of the two you meant to change". The weapon is the
        // entry, weapons.json the file — or, where the relation refuses a class's
        // baseline, the class: its row is where the baseline is written, and every
        // weapon of it is forged with it, while a variant's row holds only what it
        // adds. The detail ends with the restricted.json relation that refused it,
        // written as the file writes it (P5:255: for modifiers and enchantments
        // alike). Each edit passes every restricted and enchantment stage first, so
        // it is the weapons' stage that breaks.
        ContentException Broken(string restricted)
        {
            var error = Load(restricted: restricted).Error!;
            Assert.Equal((WeaponsJson.FileName, false), (error.File, error.BuiltIn));
            return error;
        }

        // An exclusion group: Push and Block, which every sword forges together —
        // the Sword baseline.
        var excluded = Broken(PushBlocks);
        Assert.Equal(("Sword", ContentValidator.RuleForgedNotAllowed), (excluded.EntryId, excluded.Rule));
        Assert.Equal("baseline Block x1 on a Melee weapon beside Block x1, Push x1 - restricted.json excludes [Push, Block]: Push is already forged here", excluded.Detail);

        // A dependency: Riposte needing Brace, which the Riposte Blade does not carry.
        var required = Broken(RiposteNeedsBrace);
        Assert.Equal(("riposte_blade", ContentValidator.RuleForgedNotAllowed), (required.EntryId, required.Rule));
        Assert.EndsWith("- restricted.json requires Riposte: [Brace], and this weapon has no Brace", required.Detail);

        // A weapon kind: Push kept to ranged weapons, and the Sword baseline forges
        // it on a melee class.
        var kind = Broken(PushIsRanged);
        Assert.Equal(("Sword", ContentValidator.RuleForgedNotAllowed), (kind.EntryId, kind.Rule));
        Assert.Equal("baseline Push x1 on a Melee weapon beside Block x1, Push x1 - restricted.json kind Push: ranged", kind.Detail);

        // And for enchantments (P5:255): Regeneration and Overheal grouped, which
        // the Nameless Renewal carries together...
        var souls = Broken(RegenerationExcludesOverheal);
        Assert.Equal(("staff_of_renewal_unique", ContentValidator.RuleEnchantmentsExcluded), (souls.EntryId, souls.Rule));
        Assert.Equal("'regeneration' and 'overheal' - restricted.json excludes [regeneration, overheal]", souls.Detail);

        // ...and Serrated needing Vampiric ahead of it, which the Nameless Knife
        // carries behind it.
        var dependency = Broken(SerratedNeedsVampiric);
        Assert.Equal(("flensing_knife_unique", ContentValidator.RuleEnchantmentDependency), (dependency.EntryId, dependency.Rule));
        Assert.Equal("'serrated' needs 'vampiric' ahead of it - restricted.json requires serrated: [vampiric]", dependency.Detail);

        // The shipped relations' own rows are named the same way — the existing
        // fragments first, the relation after them.
        var frostfire = Load(DataTexts.InRow(Shipped, "the_long_candle", "{ \"id\": \"shocking\" }, { \"id\": \"flaming\" }", "{ \"id\": \"cold\" }, { \"id\": \"flaming\" }")).Error!;
        Assert.Equal(("the_long_candle", ContentValidator.RuleEnchantmentsExcluded), (frostfire.EntryId, frostfire.Rule));
        Assert.Equal("'cold' and 'flaming' - restricted.json excludes [flaming, cold]", frostfire.Detail);

        // A forgedOnly id no weapon is forged with names the file it is read from:
        // Pin kept forged-only, and both weapons that forge it re-forged Softening.
        var unused = Load(restricted: DataTexts.Edit(ShippedRestricted, "\"forgedOnly\": [\"Charges\"]", "\"forgedOnly\": [\"Charges\", \"Pin\"]"), weapons:
            DataTexts.InRow(DataTexts.InRow(Shipped, "pinning_lance", "\"Pin\": 1", "\"Softening\": 1"), "pinning_bow", "\"Pin\": 1", "\"Softening\": 1")).Error!;
        Assert.Equal(("Pin", ContentValidator.RuleForgedOnlyUnused, WeaponsJson.FileName), (unused.EntryId, unused.Rule, unused.File));
        Assert.Equal("restricted.json forgedOnly Pin, and no weapon in weapons.json is forged with it", unused.Detail);

        // On the screen that stops the game, the relation is on the detail's rows.
        string screen = string.Join(" ", StartupErrorScene.Lines(excluded, [], []).Select(row => row.Trim()));
        Assert.Contains("ENTRY: SWORD", screen);
        Assert.Contains("RESTRICTED.JSON EXCLUDES [PUSH, BLOCK]", screen);
    }

    [Fact]
    public void WhyNotAllowedAnswersExactlyWhenAllowedRefuses()
    {
        // PHASE5-133: "§1.7 already lists 'weapon data validation' as a caller of
        // Allowed. This is that caller, and the point is that no new validation
        // code exists." The relation named is an explanation of Allowed's answer,
        // never a second check: over every modifier, every kind, every shipped
        // spread — and each with one modifier taken out, so a dependency or a
        // forged-only modifier can refuse — under the shipped relations and each
        // edit above, it is named exactly when Allowed refuses.
        var spreads = new List<ModifierSet> { ModifierSet.Empty };
        foreach (var def in ShippedContent.Weapons.Weapons)
        {
            spreads.Add(ModifierSet.Of(def.Forged.Select(entry => (entry.Key, entry.Value)).ToArray()));
            foreach (var left in def.Forged.Keys)
                spreads.Add(ModifierSet.Of(def.Forged.Where(entry => entry.Key != left).Select(entry => (entry.Key, entry.Value)).ToArray()));
        }

        var relations = new[] { ShippedRestricted, PushBlocks, RiposteNeedsBrace, PushIsRanged, RegenerationExcludesOverheal, SerratedNeedsVampiric }
            .Select(RestrictedJson.Read).ToList();
        var failures = new List<string>();
        int refusals = 0, asked = 0;
        foreach (var restricted in relations)
        {
            var rules = new ModifierRules(ShippedContent.Tuning, restricted);
            foreach (var t in Enum.GetValues<ModifierType>())
                foreach (var kind in new[] { WeaponKind.Melee, WeaponKind.Ranged, WeaponKind.Caster, WeaponKind.Any })
                    foreach (var present in spreads)
                    {
                        asked++;
                        bool allowed = rules.Allowed(t, kind, present);
                        string? why = ContentValidator.WhyNotAllowed(t, kind, present, restricted);
                        if (!allowed) refusals++;
                        if (allowed != (why is null))
                            failures.Add($"{t} on {kind} beside {present}: Allowed {allowed}, named '{why}'");
                    }
        }
        Assert.Empty(failures);
        Assert.True(refusals > 1000 && asked - refusals > 1000, $"{refusals} refusals of {asked}: the sweep is not reaching both answers");

        // When several clauses refuse, the one named is the first Allowed asks:
        // outside the table, then the kind, forgedOnly, excludes, requires.
        var shipped = ShippedContent.Restricted;
        Assert.Equal("Momentum is not in the modifier table", ContentValidator.WhyNotAllowed(Momentum, WeaponKind.Melee, ModifierSet.Empty, shipped));
        Assert.Equal("restricted.json kind Brace: melee", ContentValidator.WhyNotAllowed(Brace, WeaponKind.Ranged, ModifierSet.Of((Opportunist, 1)), shipped));
        Assert.Equal("restricted.json forgedOnly Charges", ContentValidator.WhyNotAllowed(Charges, WeaponKind.Melee, ModifierSet.Empty, shipped));
        Assert.Equal("restricted.json excludes [Push, Drag, Rout]: Push is already forged here",
            ContentValidator.WhyNotAllowed(Rout, WeaponKind.Melee, ModifierSet.Of((Push, 1)), shipped));
        Assert.Equal("restricted.json requires Rout: [Cleave], and this weapon has no Cleave",
            ContentValidator.WhyNotAllowed(Rout, WeaponKind.Melee, ModifierSet.Of((Block, 1)), shipped));
        Assert.Null(ContentValidator.WhyNotAllowed(Rout, WeaponKind.Melee, ModifierSet.Of((Cleave, 1)), shipped));

        // And every shipped spread is allowed as it is forged, on the kind its
        // class row gives it: nothing to name.
        foreach (var def in ShippedContent.Weapons.Weapons)
        {
            var spread = ModifierSet.Of(def.Forged.Select(entry => (entry.Key, entry.Value)).ToArray());
            foreach (var (t, _) in spread.Entries)
                Assert.Null(ContentValidator.WhyNotAllowed(t, ShippedContent.Weapons.ClassOf(def.Class)!.Kind, spread, shipped));
        }
    }

    [Fact]
    public void AWeaponTheCodeNamesMustExist()
    {
        // Two weapons are named by the code, not the content: what a dummy holds
        // unless told otherwise (EnemyState.DefaultWeaponId) and the staff a
        // crowded room's healer carries (EnemyPlacer.HealerWeaponId). A weapons.json
        // without either would otherwise crash one construction deep, so the load
        // refuses it, naming the weapon, the rule and what the code names it as.
        var sword = Load(DataTexts.WithoutRow(Shipped, "arming_sword")).Error!;
        Assert.Equal(("arming_sword", ContentValidator.RuleCodeNamedWeaponsExist, WeaponsJson.FileName, false),
            (sword.EntryId, sword.Rule, sword.File, sword.BuiltIn));
        Assert.Equal("the code names it as what a dummy holds unless told otherwise (EnemyState.DefaultWeaponId), and weapons.json has no such weapon", sword.Detail);
        Assert.Equal(EnemyState.DefaultWeaponId, sword.EntryId);

        // The staff goes with the unique made from it, which the reader would
        // otherwise refuse first for deriving from nothing.
        var staff = Load(DataTexts.WithoutRow(DataTexts.WithoutRow(Shipped, "staff_of_renewal_unique"), "staff_of_renewal")).Error!;
        Assert.Equal(("staff_of_renewal", ContentValidator.RuleCodeNamedWeaponsExist, WeaponsJson.FileName), (staff.EntryId, staff.Rule, staff.File));
        Assert.Contains("EnemyPlacer.HealerWeaponId", staff.Detail);
        Assert.Equal(EnemyPlacer.HealerWeaponId, staff.EntryId);

        // A row renamed is a row gone, as far as the code is concerned.
        var renamed = Load(DataTexts.Edit(Shipped, "\"id\": \"arming_sword\",", "\"id\": \"broadsword\",")).Error!;
        Assert.Equal(("arming_sword", ContentValidator.RuleCodeNamedWeaponsExist), (renamed.EntryId, renamed.Rule));

        // A weapon the code does not name is the file's to rename: the Halberd, which
        // no unique, no member and no drop order names, loads under another id.
        var bardiche = Load(DataTexts.Edit(Shipped, "\"id\": \"halberd\",", "\"id\": \"bardiche\","));
        Assert.Null(bardiche.Error);
        Assert.True(bardiche.Content!.Weapons.TryGet("bardiche", out _));
    }
}
