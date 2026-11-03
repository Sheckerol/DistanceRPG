using System.Text.Json;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.5's <c>restricted.json</c> as a file (PHASE5-089, -092, -096, -105–111):
/// the relations for modifiers and enchantments alike, under the doc's five
/// keys, read strictly — an unknown member or a value of the wrong kind stops the
/// game naming the file and the path — and checked before anything reads them,
/// so a typo in a relation aborts startup rather than "a rule that silently does
/// not apply". Every case reads text; the shipped file is the master copy the
/// build carries (<see cref="ShippedData.Texts"/>).
/// </summary>
public class RestrictedJsonTests
{
    private static readonly string Shipped = DataTexts.Shipped.Restricted!;

    private static ContentLoad Load(string restricted) => ShippedData.Load(DataTexts.Shipped with { Restricted = restricted });

    [Fact]
    public void TheFileIsSectionFivePointFivesKeys()
    {
        // PHASE5-105–110: the shipped file holds §5.5's five keys verbatim, in
        // §5.5's order. (It is the master copy: the relations are its alone, so
        // there is no second record to hold it to.)
        string text = ShippedData.Texts.Restricted!;
        var read = RestrictedJson.Read(text);

        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = document.RootElement;
        Assert.Equal(["excludes", "requires", "kind", "forgedOnly", "neverRolled"], root.EnumerateObject().Select(member => member.Name));

        // excludes: arrays of ids, every member excluding every other — the four
        // modifier groups, the currency pair, and the opposed damage types, which
        // are enchantment ids: modifiers and enchantments alike, in one file.
        Assert.Equal(JsonValueKind.Array, root.GetProperty("excludes").ValueKind);
        Assert.Equal(["Brace", "Opportunist", "Overwatch"], read.Excludes[0]);
        Assert.Contains(read.Excludes, group => group.SequenceEqual(["flaming", "cold"]));
        Assert.Contains(read.Excludes, group => group.SequenceEqual(["shocking", "acidic"]));

        // requires: an id to the ids it needs — Riposte needs Block, a lingering
        // element needs its element.
        Assert.Equal(JsonValueKind.Object, root.GetProperty("requires").ValueKind);
        Assert.Equal(["Block"], read.Requires["Riposte"]);
        Assert.Equal(["flaming"], read.Requires["burning"]);

        // kind: an id to melee, ranged or caster — Brace melee, Overwatch ranged.
        Assert.Equal("melee", read.Kind["Brace"]);
        Assert.Equal("ranged", read.Kind["Overwatch"]);

        // forgedOnly and neverRolled: arrays of ids, and kept apart — a safety rule
        // and an economy rule. The shipped neverRolled is the nine unique souls:
        // the file's own choice, which the load no longer holds to the catalogue's
        // unique flag (the rework's audit: the relation is this file's alone). A
        // relation is no number, so a test that states one fails where it states
        // it when an edit moves it (REWORK DECISION 3); here, as a set.
        Assert.Equal(["Charges"], read.ForgedOnly);
        string[] souls = ["siphon", "weightless", "sturdy", "momentum", "overheal", "serrated", "immovable", "piercing", "burning"];
        Assert.Equal(souls.Order(StringComparer.Ordinal), read.NeverRolled.Order(StringComparer.Ordinal));
        Assert.Empty(read.ForgedOnly.Intersect(read.NeverRolled));
    }

    [Fact]
    public void AGroupIsOneLine_TheLoaderClosesItBothWays()
    {
        // PHASE5-111 (and the shipped Symmetric, PHASE5-112/113): "groups are
        // arrays rather than pairs" — the threat zone is one line of three, and the
        // loader expands it to the six directed exclusions.
        var read = RestrictedJson.Read("""
            { "excludes": [["Brace", "Opportunist", "Overwatch"]], "requires": {}, "kind": {}, "forgedOnly": [], "neverRolled": [] }
            """);
        var rules = new ModifierRules(ShippedContent.Tuning, read);
        var pairs = rules.Excludes.SelectMany(entry => entry.Value.Select(other => (entry.Key, other))).ToList();
        (ModifierType, ModifierType)[] six =
            [(Brace, Opportunist), (Brace, Overwatch), (Opportunist, Brace), (Opportunist, Overwatch), (Overwatch, Brace), (Overwatch, Opportunist)];
        Assert.Equal(six.Order(), pairs.Order());

        // Through the loader and the shipped file, the same closure: every member
        // of every group excludes every other, both ways.
        var content = ShippedData.Load(DataTexts.Shipped).Content!;
        foreach (var group in RestrictedJson.Read(Shipped).Excludes.Where(group => group.All(ModifierRules.ModifierIds.Contains)))
            foreach (var a in group)
                foreach (var b in group.Where(b => b != a))
                    Assert.Contains(Enum.Parse<ModifierType>(b), content.Modifiers.Excludes[Enum.Parse<ModifierType>(a)]);
    }

    [Fact]
    public void KindIsLowercaseAndExact()
    {
        // PHASE5-108: kind's values are melee, ranged and caster, exactly. Anything
        // else is text the file may hold, and the relations' own check refuses it
        // naming the id — at restricted.json's stage, stamped with the file.
        foreach (string wrong in new[] { "Melee", "MELEE", "melee ", "mele", "any" })
        {
            var load = Load(DataTexts.Edit(Shipped, "\"Brace\": \"melee\"", $"\"Brace\": \"{wrong}\""));
            Assert.Null(load.Content);
            Assert.Equal(("Brace", ContentValidator.RuleKindValue, RestrictedJson.FileName, false),
                (load.Error!.EntryId, load.Error.Rule, load.Error.File, load.Error.BuiltIn));
        }

        // A kind that is not text at all is the file's shape, named by its path.
        var number = Load(DataTexts.Edit(Shipped, "\"Brace\": \"melee\"", "\"Brace\": 1")).Error!;
        Assert.Equal(("Brace", ContentValidator.RuleFileShape), (number.EntryId, number.Rule));
        Assert.Equal("restricted.json: kind.Brace: is 1, not melee, ranged or caster in quotes", number.Detail);

        // Each of the three reads as its kind.
        var read = RestrictedJson.Read("""
            { "excludes": [], "requires": {}, "kind": { "Brace": "melee", "Overwatch": "ranged", "Resonant": "caster" }, "forgedOnly": [], "neverRolled": [] }
            """);
        var rules = new ModifierRules(ShippedContent.Tuning, read);
        Assert.Equal(WeaponKind.Melee, rules.RequiresKind[Brace]);
        Assert.Equal(WeaponKind.Ranged, rules.RequiresKind[Overwatch]);
        Assert.Equal(WeaponKind.Caster, rules.RequiresKind[Resonant]);
    }

    [Fact]
    public void AnUnknownMemberOrAWrongTypeIsRefusedNamingTheFileAndThePath()
    {
        // PHASE5-096: content is strict where tuning is lenient — a mistyped field
        // is "a rule that silently does not apply" — so an unknown member, a value
        // of the wrong kind, a key left out or named twice, and a file that is not
        // JSON at all are each refused under the file's shape, naming the entry, the
        // file and the path. Read on its own, the reader throws; through the loader,
        // the failure comes back at restricted.json's stage, stamped with the file.
        string both = """{ "excludes": [], "requires": {}, "kind": {}, "forgedOnly": [], "neverRolled": [] }""";
        (string Text, string Entry, string Detail)[] cases =
        [
            (DataTexts.Edit(Shipped, "\"excludes\":", "\"exclude\":"), "restricted.json",
                "restricted.json: exclude: is not a member here - did you mean excludes?"),
            (DataTexts.Edit(Shipped, "\"neverRolled\":", "\"rollable\":"), "restricted.json",
                "restricted.json: rollable: is not a member here - the members are excludes, requires, kind, forgedOnly, neverRolled"),
            (DataTexts.Edit(Shipped, "[\"Riposte\", \"BlockWeaken\"]", "[\"Riposte\", 7]"), "excludes",
                "restricted.json: excludes[2][1]: is 7, not an id in quotes"),
            (DataTexts.Edit(Shipped, "[\"Riposte\", \"BlockWeaken\"]", "\"Riposte, BlockWeaken\""), "excludes",
                "restricted.json: excludes[2]: is the text Riposte, BlockWeaken, not a group: a list of ids"),
            (DataTexts.Edit(Shipped, "\"Riposte\": [\"Block\"]", "\"Riposte\": \"Block\""), "Riposte",
                "restricted.json: requires.Riposte: is the text Block, not a list of the ids it needs"),
            (DataTexts.Edit(Shipped, "\"forgedOnly\": [\"Charges\"]", "\"forgedOnly\": \"Charges\""), "forgedOnly",
                "restricted.json: forgedOnly: is the text Charges, not a list of ids"),
            (DataTexts.Edit(Shipped, "[\"Light\", \"Resonant\"]", "[\"Light\", \"\"]"), "excludes",
                "restricted.json: excludes[4][1]: is an empty id - an id names its entry"),
            (both.Replace("\"kind\": {}, ", "", StringComparison.Ordinal), "kind", "restricted.json: kind: is missing"),
            (both.Replace("\"kind\": {}", "\"kind\": {}, \"kind\": {}", StringComparison.Ordinal), "restricted.json", "restricted.json: kind: is named twice"),
            (both.Replace("\"requires\": {}", "\"requires\": { \"Riposte\": [\"Block\"], \"Riposte\": [] }", StringComparison.Ordinal), "Riposte",
                "restricted.json: requires.Riposte: is named twice"),
            ("[]", "restricted.json", "restricted.json: the top level: is a list, not a table of the five relations"),
            ("  \n", "restricted.json", "restricted.json: the top level: the file is empty"),
        ];
        foreach (var (text, entry, detail) in cases)
        {
            var thrown = Assert.Throws<ContentException>(() => RestrictedJson.Read(text));
            Assert.Equal((entry, ContentValidator.RuleFileShape, detail), (thrown.EntryId, thrown.Rule, thrown.Detail));

            var error = Load(text).Error!;
            Assert.Equal((entry, ContentValidator.RuleFileShape, detail, RestrictedJson.FileName, false),
                (error.EntryId, error.Rule, error.Detail, error.File, error.BuiltIn));
        }

        // A file that is not JSON says the line and the column, in words.
        var syntax = Assert.Throws<ContentException>(() => RestrictedJson.Read(DataTexts.Edit(Shipped, "[\"Push\", \"Drag\", \"Rout\"],", "[\"Push\", \"Drag\", \"Rout\"]")));
        Assert.Equal((RestrictedJson.FileName, ContentValidator.RuleFileShape), (syntax.EntryId, syntax.Rule));
        Assert.Matches(@"^restricted\.json: line \d+, column \d+: is not valid JSON \(.+\)$", syntax.Detail);

        // Comments and trailing commas are fine: a human edits the file.
        var relaxed = RestrictedJson.Read("""
            /* the crit riders are out, for a playtest */
            { "excludes": [["Push", "Drag",],], "requires": { "Riposte": ["Block"], }, "kind": {}, "forgedOnly": [], "neverRolled": [], }
            """);
        Assert.Equal(["Push", "Drag"], Assert.Single(relaxed.Excludes));
    }

    [Fact]
    public void ATypoInARelationAbortsStartupNamingIt()
    {
        // PHASE5-089, -092, re-pinning -115: the relations are checked too, not
        // frozen — "every id resolves to a real modifier or enchantment", since a
        // typo in a relation is a rule that silently does not apply. So "Blok"
        // stops the game, naming the id, the rule and the file, and pointing at the
        // id it is nearest (1i: RESTRICTED.JSON - BLOK - ID DOES NOT RESOLVE ...).
        var error = Load(DataTexts.Edit(Shipped, "\"Riposte\": [\"Block\"]", "\"Riposte\": [\"Blok\"]")).Error!;
        Assert.Equal(("Blok", ContentValidator.RuleUnknownId, RestrictedJson.FileName, false), (error.EntryId, error.Rule, error.File, error.BuiltIn));
        Assert.Equal("not a modifier, nor an enchantment in the enchantment catalogue - did you mean Block?", error.Detail);

        // Wherever the typo sits — a group, a row's key, a kind, forgedOnly, an
        // enchantment id in neverRolled — the same rule names it.
        (string From, string To, string Entry)[] typos =
        [
            ("[\"Push\", \"Drag\", \"Rout\"]", "[\"Push\", \"Dragg\", \"Rout\"]", "Dragg"),
            ("\"Rout\": [\"Cleave\"]", "\"Rour\": [\"Cleave\"]", "Rour"),
            ("\"Overwatch\": \"ranged\"", "\"Overwach\": \"ranged\"", "Overwach"),
            ("\"forgedOnly\": [\"Charges\"]", "\"forgedOnly\": [\"Charge\"]", "Charge"),
            (", \"burning\"]", ", \"buring\"]", "buring"),
        ];
        foreach (var (from, to, entry) in typos)
        {
            var typo = Load(DataTexts.Edit(Shipped, from, to)).Error!;
            Assert.Equal((entry, ContentValidator.RuleUnknownId, RestrictedJson.FileName), (typo.EntryId, typo.Rule, typo.File));
        }
        Assert.EndsWith("did you mean burning?", Load(DataTexts.Edit(Shipped, ", \"burning\"]", ", \"buring\"]")).Error!.Detail);
    }

    [Fact]
    public void AnEnchantmentInKindOrForgedOnlyAbortsStartupNamingIt()
    {
        // PHASE5-092, -108, -109: kind keeps a modifier to a kind of weapon (Brace
        // melee) and forgedOnly keeps one from being added from zero (Charges) —
        // relations only modifiers have, which the modifier rules read and nothing
        // else does. An enchantment named in either resolves, and then no rule
        // reads it: "a rule that silently does not apply". So the edit stops the
        // game at restricted.json's stage, naming the id, the key and the file,
        // rather than loading as though it said something.
        var kind = Load(DataTexts.Edit(Shipped, "\"Brace\": \"melee\",", "\"Brace\": \"melee\",\n    \"flaming\": \"melee\",")).Error!;
        Assert.Equal(("flaming", ContentValidator.RuleModifierRelationIds, RestrictedJson.FileName, false),
            (kind.EntryId, kind.Rule, kind.File, kind.BuiltIn));
        Assert.Equal("'flaming' is an enchantment, and kind limits modifiers alone - restricted.json kind flaming: melee", kind.Detail);

        var forgedOnly = Load(DataTexts.Edit(Shipped, "\"forgedOnly\": [\"Charges\"]", "\"forgedOnly\": [\"Charges\", \"burning\"]")).Error!;
        Assert.Equal(("burning", ContentValidator.RuleModifierRelationIds, RestrictedJson.FileName, false),
            (forgedOnly.EntryId, forgedOnly.Rule, forgedOnly.File, forgedOnly.BuiltIn));
        Assert.Equal("'burning' is an enchantment, and forgedOnly lists modifiers alone - restricted.json forgedOnly burning", forgedOnly.Detail);

        // A modifier added to kind is the relation it always was, and loads: the
        // riposte blade, a sword, is the one weapon forged with Riposte.
        var counter = Load(DataTexts.Edit(Shipped, "\"Opportunist\": \"melee\",", "\"Opportunist\": \"melee\",\n    \"Riposte\": \"melee\","));
        Assert.Null(counter.Error);
        Assert.Equal(WeaponKind.Melee, counter.Content!.Modifiers.RequiresKind[Riposte]);
        Assert.False(counter.Content.Modifiers.Allowed(Riposte, WeaponKind.Ranged, ModifierSet.Of((Block, 1))));
    }
}
