using System.Text.Json;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.7's <c>enchantments.json</c> as a file (PHASE5-137, -138, -148, re-pinning
/// -141 and -142): the catalogue and the unique souls, every field of an entry
/// under the record's name, the effect named and never written — so a new entry
/// of an existing kind needs no code, and a kind that does not exist stops the
/// game — the catalogue's own rules checked when the file loads, and a number
/// <c>tuning.json</c> owns marked in the file as the dial's. Every case reads
/// text; the shipped file is read from the test output's own linked copy.
/// </summary>
public class EnchantmentsJsonTests
{
    private static readonly string Shipped = DataTexts.Shipped.Enchantments!;

    private static readonly string[] Fields =
        ["id", "name", "effect", "targets", "lock", "trigger", "potency", "applyPercent", "applies", "damageType", "unique"];

    private static ContentLoad Load(string enchantments, string? restricted = null)
        => ContentFiles.Load(DataTexts.Shipped with { Enchantments = enchantments, Restricted = restricted ?? DataTexts.Shipped.Restricted });

    /// <summary>The shipped file with <paramref name="from"/> replaced by <paramref name="to"/> inside vampiric's entry alone.</summary>
    private static string InVampiric(string from, string to)
    {
        int start = Shipped.IndexOf("\"id\": \"vampiric\"", StringComparison.Ordinal);
        int end = Shipped.IndexOf('}', start);
        return string.Concat(Shipped.AsSpan(0, start), DataTexts.Edit(Shipped[start..end], from, to), Shipped.AsSpan(end));
    }

    [Fact]
    public void EveryFieldOfSectionFivePointSeven()
    {
        // PHASE5-137: "lock, trigger cost, condition, base potency, effect
        // reference, unique flag, and a damage type where the entry is elemental" —
        // the shipped file reads to exactly the compiled catalogue, entry for entry
        // and field for field.
        string text = File.ReadAllText(ShippedDataTests.Shipped(EnchantmentsJson.FileName));
        var read = EnchantmentsJson.Read(text);
        Assert.Equal(DataTexts.Catalogue, read.Enchantments);

        // Every entry names all eleven fields, the record's parameters camel-cased,
        // in the record's order; the file's one member is the list.
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        Assert.Equal(["enchantments"], document.RootElement.EnumerateObject().Select(member => member.Name));
        var entries = document.RootElement.GetProperty("enchantments").EnumerateArray().ToList();
        Assert.Equal(DataTexts.Catalogue.Count, entries.Count);
        Assert.All(entries, entry => Assert.Equal(Fields, entry.EnumerateObject().Select(member => member.Name)));

        // The unique flag is on the souls, and only on them — the ids neverRolled lists.
        Assert.Equal(ShippedContent.Restricted.NeverRolled.Order(StringComparer.Ordinal),
            read.Enchantments.Where(e => e.Unique).Select(e => e.Id).Order(StringComparer.Ordinal));

        // An applier quotes no flat trigger (null), a rule does; an element carries
        // its damage type by name, and nothing else carries one.
        JsonElement Entry(string id) => entries.Single(entry => entry.GetProperty("id").GetString() == id);
        Assert.Equal(JsonValueKind.Null, Entry("regeneration").GetProperty("trigger").ValueKind);
        Assert.Equal("Regeneration", Entry("regeneration").GetProperty("applies").GetString());
        Assert.Equal(2, Entry("vampiric").GetProperty("trigger").GetInt32());
        Assert.Equal("Flaming", Entry("flaming").GetProperty("damageType").GetString());
        Assert.Equal(JsonValueKind.Null, Entry("vampiric").GetProperty("damageType").ValueKind);
        Assert.Equal("ElementalDamage", Entry("flaming").GetProperty("effect").GetString());
        Assert.True(Entry("serrated").GetProperty("unique").GetBoolean());

        // There is no condition field (bend (c)): when an entry fires is its kind's,
        // so a condition column would be a second answer. A file that writes one is
        // refused, pointing at the field.
        var condition = Assert.Throws<ContentException>(() => EnchantmentsJson.Read(
            DataTexts.Edit(Shipped, "\"id\": \"vampiric\",", "\"id\": \"vampiric\",\n      \"condition\": \"OnHit\",")));
        Assert.Equal(("vampiric", ContentValidator.RuleFileShape), (condition.EntryId, condition.Rule));
        Assert.StartsWith("enchantments.json: enchantments[8].condition: is not a member here", condition.Detail);

        // The rest of the shape, named by its path: a field left out, a value of the
        // wrong kind, an enum by a name it does not have or in another case.
        (string From, string To, string Detail)[] cases =
        [
            ("\"lock\": 20,\n", "", "enchantments.json: enchantments[8].lock: is missing"),
            ("\"trigger\": 2,", "\"trigger\": \"2\",", "enchantments.json: enchantments[8].trigger: is the text 2, not a whole number"),
            ("\"trigger\": 2,", "\"trigger\": 2.5,", "enchantments.json: enchantments[8].trigger: is 2.5, not a whole number"),
            ("\"targets\": \"Ally\",", "\"targets\": \"ally\",", "enchantments.json: enchantments[8].targets: is the text ally, which names no TargetSide - did you mean Ally?"),
            ("\"applies\": null,", "\"applies\": \"Bleed\",", "enchantments.json: enchantments[8].applies: is the text Bleed, which names no StatusEffectType - the names are "
                + string.Join(", ", Enum.GetNames<StatusEffectType>())),
            ("\"unique\": false", "\"unique\": \"no\"", "enchantments.json: enchantments[8].unique: is the text no, not true or false"),
        ];
        foreach (var (from, to, detail) in cases)
        {
            var thrown = Assert.Throws<ContentException>(() => EnchantmentsJson.Read(InVampiric(from, to)));
            Assert.Equal(("vampiric", ContentValidator.RuleFileShape, detail), (thrown.EntryId, thrown.Rule, thrown.Detail));
        }

        // An entry whose id cannot be read is named by its place in the list.
        var noId = Assert.Throws<ContentException>(() => EnchantmentsJson.Read(DataTexts.Edit(Shipped, "\"id\": \"vampiric\",", "\"id\": 9,")));
        Assert.Equal(("enchantments[8]", "enchantments.json: enchantments[8].id: is 9, not an id in quotes"), (noId.EntryId, noId.Detail));
    }

    [Fact]
    public void TheEffectIsNamedNeverWritten()
    {
        // PHASE5-138, -148: "the effect itself stays code" — an entry names an
        // EffectKind — and "every effect reference resolves to an EffectKind": a
        // typo is not a silent no-op. It is refused under its own rule, naming the
        // entry, the path, and the kind it is nearest.
        string typo = DataTexts.Edit(Shipped, "\"effect\": \"Vampiric\"", "\"effect\": \"Vampric\"");
        var thrown = Assert.Throws<ContentException>(() => EnchantmentsJson.Read(typo));
        Assert.Equal(("vampiric", ContentValidator.RuleUnknownEffect), (thrown.EntryId, thrown.Rule));
        Assert.Equal("enchantments.json: enchantments[8].effect: is the text Vampric, which names no EffectKind - did you mean Vampiric?", thrown.Detail);

        var error = Load(typo).Error!;
        Assert.Equal(("vampiric", ContentValidator.RuleUnknownEffect, EnchantmentsJson.FileName, false), (error.EntryId, error.Rule, error.File, error.Compiled));

        // A number is no name, even the kind's ordinal; nor is another case.
        foreach (string wrong in new[] { "2", "\"vampiric\"", "null" })
        {
            var refused = Assert.Throws<ContentException>(() => EnchantmentsJson.Read(DataTexts.Edit(Shipped, "\"effect\": \"Vampiric\"", $"\"effect\": {wrong}")));
            Assert.Equal(("vampiric", ContentValidator.RuleUnknownEffect), (refused.EntryId, refused.Rule));
        }

        // And a new entry naming an existing kind needs no code: a second drinking
        // entry is a file edit, loads, and is the Vampiric behaviour — rollable,
        // since it is no unique soul.
        var leech = new EnchantmentDef("leech", "Leech", EffectKind.Vampiric, TargetSide.Ally, Lock: 25, Trigger: 3, Potency: 2,
            ApplyPercent: 100, Applies: null, DamageType: null, Unique: false);
        var loaded = Load(DataTexts.Enchantments(DataTexts.Catalogue.Append(leech)));
        Assert.Empty(loaded.Notices);
        Assert.Equal(leech, loaded.Content!.Enchantments["leech"]);
        Assert.True(EnchantmentBehaviours.HealsTheWielder(loaded.Content.Enchantments["leech"].Effect));
        Assert.Contains(loaded.Content.Enchantments.Rollable, e => e.Id == "leech");
    }

    [Fact]
    public void AZeroTriggerInAFileAbortsStartup()
    {
        // Re-pinning PHASE5-142 through the file: "no enchantment fires free, ever"
        // — the check to enforce hardest — so a trigger of 0 in enchantments.json
        // stops the game at the enchantments' stage, and so does a negative one.
        foreach (int trigger in new[] { 0, -3 })
        {
            var error = Load(DataTexts.Enchantments("vampiric", e => e with { Trigger = trigger })).Error!;
            Assert.Equal(("vampiric", ContentValidator.RuleZeroTrigger, EnchantmentsJson.FileName, false), (error.EntryId, error.Rule, error.File, error.Compiled));
            Assert.Equal($"trigger {trigger}", error.Detail);
        }

        // An applier's null is not a zero: it prices off the levels it lands.
        Assert.NotNull(Load(Shipped).Content);
    }

    [Fact]
    public void AUniqueMissingFromNeverRolledAbortsStartup_AcrossTwoFiles()
    {
        // Re-pinning PHASE5-141 through the files: the unique flag lives in
        // enchantments.json and roll-eligibility in restricted.json's neverRolled,
        // and "the loader cross-checks the two and aborts if they disagree". A soul
        // declared unique and forgotten in restricted.json stops the game at the
        // enchantments' stage, the detail naming the other file.
        var ember = new EnchantmentDef("ember_heart", "Ember Heart", EffectKind.Siphon, TargetSide.Ally, Lock: 20, Trigger: 5, Potency: 10,
            ApplyPercent: 100, Applies: null, DamageType: null, Unique: true);
        string withEmber = DataTexts.Enchantments(DataTexts.Catalogue.Append(ember));
        var forgotten = Load(withEmber).Error!;
        Assert.Equal(("ember_heart", ContentValidator.RuleUniqueNeverRolled, EnchantmentsJson.FileName), (forgotten.EntryId, forgotten.Rule, forgotten.File));
        Assert.Equal("unique, but absent from neverRolled in restricted.json", forgotten.Detail);

        // Declared in both, it loads: one question, one answer — never rolled.
        string listed = DataTexts.Edit(DataTexts.Shipped.Restricted!, ", \"burning\"]", ", \"burning\", \"ember_heart\"]");
        var both = Load(withEmber, listed);
        Assert.Empty(both.Notices);
        Assert.Contains("ember_heart", both.Content!.Enchantments.NeverRolled);
        Assert.DoesNotContain(both.Content.Enchantments.Rollable, e => e.Id == "ember_heart");

        // And the other way: neverRolled naming an entry that is not a unique soul.
        var notUnique = Load(Shipped, DataTexts.Edit(DataTexts.Shipped.Restricted!, ", \"burning\"]", ", \"burning\", \"vampiric\"]")).Error!;
        Assert.Equal(("vampiric", ContentValidator.RuleUniqueNeverRolled, EnchantmentsJson.FileName), (notUnique.EntryId, notUnique.Rule, notUnique.File));
        Assert.Contains(RestrictedJson.FileName, notUnique.Detail);
    }

    [Fact]
    public void ADialledFieldSaysWhichKeySetsIt()
    {
        // 1h: one owner for a dialled number — the dial. The written file says, after
        // each field a tuning.json dial owns, which key sets it: ApplyPercent's five
        // ids and Arcane's potency, and no other field.
        string text = EnchantmentsJson.Write(ShippedContent.Enchantments, ShippedContent.Tuning);
        var noted = new List<(string Id, string Line)>();
        string? id = null;
        foreach (string line in text.Split('\n').Select(line => line.Trim()))
        {
            if (line.StartsWith("\"id\": ", StringComparison.Ordinal))
                id = line["\"id\": ".Length..].Trim('"', ',');
            if (line.EndsWith("sets this */", StringComparison.Ordinal))
                noted.Add((id!, line));
        }
        Assert.Equal(
        [
            ("flaming", "\"applyPercent\": 20, /* tuning.json ApplyPercent.flaming sets this */"),
            ("cold", "\"applyPercent\": 15, /* tuning.json ApplyPercent.cold sets this */"),
            ("shocking", "\"applyPercent\": 25, /* tuning.json ApplyPercent.shocking sets this */"),
            ("acidic", "\"applyPercent\": 15, /* tuning.json ApplyPercent.acidic sets this */"),
            ("arcane", "\"potency\": 4, /* tuning.json ArcanePotency sets this */"),
            ("serrated", "\"applyPercent\": 20, /* tuning.json ApplyPercent.serrated sets this */"),
        ], noted);

        // The note is a comment: the file reads back to the same records, and the
        // shipped file is this text.
        Assert.Equal(DataTexts.Catalogue, EnchantmentsJson.Read(text).Enchantments);
        Assert.Equal(Shipped, text);

        // A dial a tuning adds is noted too, on the row it owns.
        var dialled = ShippedContent.Tuning with
        {
            ApplyPercent = new Dictionary<string, int>(ShippedContent.Tuning.ApplyPercent, StringComparer.Ordinal) { ["vampiric"] = 50 },
        };
        Assert.Contains("\"applyPercent\": 100, /* tuning.json ApplyPercent.vampiric sets this */", EnchantmentsJson.Write(ShippedContent.Enchantments, dialled));
    }
}
