using System.Text.Json;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.6's <c>weapons.json</c> as a file (PHASE5-122, -123, -124, -133): the
/// thirty-two variants and the uniques, "statline, forged spread, area shape,
/// innate enchantment id", a row for a weapon and never a class baseline (bend
/// (b)); read strictly, with a shape's zero dimensions and a tier of 1 left out;
/// and a <c>restricted.json</c> edit that makes a weapon illegal stopping the game
/// naming both — the weapon, and the relation that refused it, asked of the one
/// predicate every graft asks (<see cref="ModifierRules.Allowed"/>) and explained
/// by <see cref="ContentValidator.WhyNotAllowed"/>. Every case reads text or
/// builds rules it never makes current; the shipped file is read from the test
/// output's own linked copy.
/// </summary>
public class WeaponsJsonTests
{
    private static readonly string Shipped = DataTexts.Shipped.Weapons!;

    private static readonly string ShippedRestricted = DataTexts.Shipped.Restricted!;

    private static readonly string[] Fields =
        ["id", "name", "class", "role", "range", "damage", "cost", "manaCost", "forged", "enchantments", "shape", "unique", "derivedFrom"];

    private static ContentLoad Load(string? weapons = null, string? restricted = null)
        => ContentFiles.Load(DataTexts.Shipped with { Weapons = weapons ?? Shipped, Restricted = restricted ?? ShippedRestricted });

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
        // enchantment id". The shipped file reads to exactly the compiled
        // catalogue, row for row and in its order.
        string text = File.ReadAllText(ShippedDataTests.Shipped(WeaponsJson.FileName));
        var read = WeaponsJson.Read(text).Weapons;
        var compiled = ShippedContent.Weapons.Weapons;
        Assert.Equal(compiled.Count, read.Count);
        for (int i = 0; i < compiled.Count; i++)
            Same(compiled[i], read[i]);
        Assert.Equal(32, read.Count(w => !w.Unique));
        Assert.Equal(11, read.Count(w => w.Unique));

        // The file's one member is the list — no baselines section (bend (b),
        // USER DECISION 4) — and every row names all thirteen fields, the record's
        // parameters camel-cased, in the record's order.
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        Assert.Equal(["weapons"], document.RootElement.EnumerateObject().Select(member => member.Name));
        var rows = document.RootElement.GetProperty("weapons").EnumerateArray().ToList();
        Assert.All(rows, row => Assert.Equal(Fields, row.EnumerateObject().Select(member => member.Name)));

        // The statline by field; the forged spread by modifier name; a wand's shape;
        // the enchantments by id, with a tier where it is not 1.
        JsonElement Row(string id) => rows.Single(row => row.GetProperty("id").GetString() == id);
        var knife = Row("flensing_knife");
        Assert.Equal(("Dagger", "Efficiency", 40, 15, 30, 0), (knife.GetProperty("class").GetString(), knife.GetProperty("role").GetString(),
            knife.GetProperty("range").GetInt32(), knife.GetProperty("damage").GetInt32(), knife.GetProperty("cost").GetInt32(), knife.GetProperty("manaCost").GetInt32()));
        Assert.Equal(["CritWindow", "CritMultiplier", "Light"], knife.GetProperty("forged").EnumerateObject().Select(member => member.Name));
        Assert.Equal("{ \"kind\": \"Blast\", \"radius\": 48, \"targetReach\": 160 }", Row("wand_of_the_blast").GetProperty("shape").GetRawText());
        Assert.Equal(JsonValueKind.Null, knife.GetProperty("shape").ValueKind);
        Assert.Equal("[{ \"id\": \"serrated\" }, { \"id\": \"vampiric\", \"tier\": 3 }, { \"id\": \"overheal\" }]",
            Row("flensing_knife_unique").GetProperty("enchantments").GetRawText());
        Assert.Equal("[{ \"id\": \"regeneration\" }]", Row("staff_of_renewal").GetProperty("enchantments").GetRawText());
        Assert.Equal("flensing_knife", Row("flensing_knife_unique").GetProperty("derivedFrom").GetString());
        Assert.Equal(JsonValueKind.Null, Row("flensing_knife_unique").GetProperty("role").ValueKind);

        // The baselines are derived, never written: what a class's four variants
        // share is its signature and one other modifier, as RuleClassBaseline
        // checks — and the file loads, so it checked.
        var baselines = new Dictionary<WeaponClass, (ModifierType, ModifierType)>
        {
            [WeaponClass.Dagger] = (CritWindow, CritMultiplier),
            [WeaponClass.Sword] = (Block, Push),
            [WeaponClass.Spear] = (Brace, Longshot),
            [WeaponClass.Axe] = (Cleave, Opportunist),
            [WeaponClass.Ranged] = (Longshot, CritWindow),
            [WeaponClass.Throwing] = (Charges, CritMultiplier),
        };
        foreach (var (cls, (signature, second)) in baselines)
        {
            var variants = read.Where(w => w.Class == cls && !w.Unique).ToList();
            Assert.Equal(4, variants.Count);
            var shared = Enum.GetValues<ModifierType>().Where(t => variants.All(v => v.Forged.GetValueOrDefault(t) > 0)).ToHashSet();
            Assert.Equal(new HashSet<ModifierType> { signature, second }, shared);
            Assert.Equal(signature, ContentValidator.Signature[cls]);
        }
        Assert.Null(Load(text).Error);
    }

    [Fact]
    public void ADefaultShapeDimensionOrTierIsOmitted_AndReadsBack()
    {
        // 1h: a shape's zero dimensions and an enchantment's tier of 1 are left out
        // when written and read back as left out: a Nova names its radius alone,
        // a Beam its length and width, a tier-1 soul its id alone.
        Assert.Contains("\"shape\": { \"kind\": \"Nova\", \"radius\": 96 },", Shipped);
        Assert.Contains("\"shape\": { \"kind\": \"Beam\", \"length\": 224, \"width\": 32 },", Shipped);
        Assert.Contains("\"shape\": { \"kind\": \"Cone\", \"length\": 128, \"angleDegrees\": 90 },", Shipped);
        var read = WeaponsJson.Read(Shipped).Weapons;
        Assert.Equal(AreaShape.Nova(radius: 96), read.Single(w => w.Id == "wand_of_the_nova").Shape);
        Assert.Equal(AreaShape.Cone(angleDegrees: 90, length: 128), read.Single(w => w.Id == "wand_of_the_cone").Shape);
        Assert.Equal([new EnchantmentRef("serrated", 1), new EnchantmentRef("vampiric", 3), new EnchantmentRef("overheal", 1)],
            read.Single(w => w.Id == "flensing_knife_unique").Enchantments);

        // Written out, a default is the same number as left out: a Nova's width,
        // which it does not measure, as the 0 it is, and a soul's tier as 1.
        string spelled = DataTexts.Edit(DataTexts.InRow(Shipped, "wand_of_the_nova",
            "\"shape\": { \"kind\": \"Nova\", \"radius\": 96 },", "\"shape\": { \"kind\": \"Nova\", \"radius\": 96, \"width\": 0 },"),
            "[{ \"id\": \"serrated\" }, { \"id\": \"vampiric\", \"tier\": 3 }", "[{ \"id\": \"serrated\", \"tier\": 1 }, { \"id\": \"vampiric\", \"tier\": 3 }");
        Assert.Equal(Shipped, WeaponsJson.Write(WeaponsJson.Read(spelled)));

        // A dimension its kind does not measure is a number nothing reads, and is
        // refused naming the path and what the kind does measure.
        var stray = Assert.Throws<ContentException>(() => WeaponsJson.Read(DataTexts.InRow(Shipped, "wand_of_the_nova",
            "\"shape\": { \"kind\": \"Nova\", \"radius\": 96 },", "\"shape\": { \"kind\": \"Nova\", \"radius\": 96, \"length\": 64 },")));
        Assert.Equal(("wand_of_the_nova", ContentValidator.RuleFileShape, "weapons.json: weapons[31].shape.length: is not a dimension of a Nova - a Nova measures radius"),
            (stray.EntryId, stray.Rule, stray.Detail));
        var kind = Assert.Throws<ContentException>(() => WeaponsJson.Read(DataTexts.InRow(Shipped, "wand_of_the_nova", "\"kind\": \"Nova\"", "\"kind\": \"Ring\"")));
        Assert.Equal(("wand_of_the_nova", ContentValidator.RuleFileShape), (kind.EntryId, kind.Rule));
        Assert.StartsWith("weapons.json: weapons[31].shape.kind: is the text Ring, which names no AreaShapeKind", kind.Detail);
    }

    [Fact]
    public void AnUnknownMemberOrAWrongTypeIsRefusedNamingThePath()
    {
        // Content is strict (1h): a member the shape does not have, a value of the
        // wrong kind, a field left out — each refused under the file's shape,
        // naming the weapon and the path. A forged modifier that is no modifier is
        // an id that resolves to nothing, pointing at the one it is nearest.
        (string Text, string Entry, string Rule, string Detail)[] cases =
        [
            (DataTexts.InRow(Shipped, "arming_sword", "\"forged\": { \"Block\": 1,", "\"forged\": { \"Blok\": 1,"), "arming_sword", ContentValidator.RuleUnknownId,
                "weapons.json: weapons[4].forged.Blok: is not a modifier - did you mean Block?"),
            (DataTexts.InRow(Shipped, "arming_sword", "\"unique\": false,", "\"unique\": false,\n      \"baseline\": null,"), "arming_sword", ContentValidator.RuleFileShape,
                "weapons.json: weapons[4].baseline: is not a member here - the members are " + string.Join(", ", Fields)),
            (DataTexts.InRow(Shipped, "arming_sword", "\"class\": \"Sword\",", "\"class\": \"Swords\","), "arming_sword", ContentValidator.RuleFileShape,
                "weapons.json: weapons[4].class: is the text Swords, which names no WeaponClass - did you mean Sword?"),
            (DataTexts.InRow(Shipped, "arming_sword", "\"range\": 80,", "\"range\": \"80\","), "arming_sword", ContentValidator.RuleFileShape,
                "weapons.json: weapons[4].range: is the text 80, not a whole number"),
            (DataTexts.InRow(Shipped, "arming_sword", "\"damage\": 10,\n", ""), "arming_sword", ContentValidator.RuleFileShape,
                "weapons.json: weapons[4].damage: is missing"),
            (DataTexts.InRow(Shipped, "arming_sword", "\"forged\": { \"Block\": 1, \"Light\": 1, \"Push\": 1 }", "\"forged\": { \"Block\": 1, \"Block\": 2 }"), "arming_sword",
                ContentValidator.RuleFileShape, "weapons.json: weapons[4].forged.Block: is named twice"),
            (DataTexts.InRow(Shipped, "staff_of_renewal", "[{ \"id\": \"regeneration\" }]", "[\"regeneration\"]"), "staff_of_renewal", ContentValidator.RuleFileShape,
                "weapons.json: weapons[24].enchantments[0]: is the text regeneration, not an enchantment: a table of its id and, when it is not 1, its tier"),
            ("{ \"weapons\": {} }", WeaponsJson.FileName, ContentValidator.RuleFileShape, "weapons.json: weapons: is a table, not a list of weapons"),
        ];
        foreach (var (text, entry, rule, detail) in cases)
        {
            var thrown = Assert.Throws<ContentException>(() => WeaponsJson.Read(text));
            Assert.Equal((entry, rule, detail), (thrown.EntryId, thrown.Rule, thrown.Detail));

            // Through the loader, at the weapons' stage, stamped with the file.
            var error = Load(text).Error!;
            Assert.Equal((entry, rule, detail, WeaponsJson.FileName, false), (error.EntryId, error.Rule, error.Detail, error.File, error.Compiled));
        }

        // Comments and trailing commas are fine: a human edits the file.
        string relaxed = DataTexts.InRow(Shipped, "arming_sword", "\"derivedFrom\": null", "\"derivedFrom\": null, // the dummies' sword\n");
        Assert.Null(Load(relaxed).Error);
    }

    [Fact]
    public void ARelationsEditThatBreaksAWeaponNamesBoth()
    {
        // PHASE5-122: "relations load before content, so a relations edit that
        // invalidates an existing weapon aborts startup naming both" — "the weapon
        // that is now illegal and the rule that made it so". The weapon is the
        // entry, weapons.json the file; the detail ends with the restricted.json
        // relation that refused it, written as the file writes it (S:182: for
        // modifiers and enchantments alike). Each edit passes every restricted
        // and enchantment stage first, so it is the weapons' stage that breaks.
        ContentException Broken(string restricted)
        {
            var error = Load(restricted: restricted).Error!;
            Assert.Equal((WeaponsJson.FileName, false), (error.File, error.Compiled));
            return error;
        }

        // An exclusion group: Push and Block, which every sword forges together.
        var excluded = Broken(PushBlocks);
        Assert.Equal(("arming_sword", ContentValidator.RuleForgedNotAllowed), (excluded.EntryId, excluded.Rule));
        Assert.Equal("Block x1 on a Melee weapon beside Block x1, Light x1, Push x1 - restricted.json excludes [Push, Block]: Push is already forged here", excluded.Detail);

        // A dependency: Riposte needing Brace, which the Riposte Blade does not carry.
        var required = Broken(RiposteNeedsBrace);
        Assert.Equal(("riposte_blade", ContentValidator.RuleForgedNotAllowed), (required.EntryId, required.Rule));
        Assert.EndsWith("- restricted.json requires Riposte: [Brace], and this weapon has no Brace", required.Detail);

        // A weapon kind: Push kept to ranged weapons, and a sword is melee.
        var kind = Broken(PushIsRanged);
        Assert.Equal(("arming_sword", ContentValidator.RuleForgedNotAllowed), (kind.EntryId, kind.Rule));
        Assert.Equal("Push x1 on a Melee weapon beside Block x1, Light x1, Push x1 - restricted.json kind Push: ranged", kind.Detail);

        // And for enchantments (S:182): Regeneration and Overheal grouped, which
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
        string screen = string.Join(" ", StartupErrorScene.Lines(excluded, []).Select(row => row.Trim()));
        Assert.Contains("ENTRY: ARMING_SWORD", screen);
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

        // And every shipped spread is allowed as it is forged: nothing to name.
        foreach (var def in ShippedContent.Weapons.Weapons)
        {
            var spread = ModifierSet.Of(def.Forged.Select(entry => (entry.Key, entry.Value)).ToArray());
            foreach (var (t, _) in spread.Entries)
                Assert.Null(ContentValidator.WhyNotAllowed(t, Weapon.KindOf(def.Class), spread, shipped));
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
            (sword.EntryId, sword.Rule, sword.File, sword.Compiled));
        Assert.Equal("the code names it as what a dummy holds unless told otherwise (EnemyState.DefaultWeaponId), and weapons.json has no such weapon", sword.Detail);
        Assert.Equal(EnemyState.DefaultWeaponId, sword.EntryId);

        var staff = Load(DataTexts.WithoutRow(Shipped, "staff_of_renewal")).Error!;
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
