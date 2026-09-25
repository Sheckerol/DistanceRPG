using System.Text.Json;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.8's <c>dungeons.json</c> as a file (PHASE5-150, -151, -153, -155, -157,
/// -161, -164, -165, re-pinning -169): name, theme, attunement, the boss's floor
/// range, the boss — "statline plus innate modifiers" — and the tutorial's flag,
/// under the doc's own names; no drop table, written or read, because the table
/// is computed from the theme and the relations, so an edit to
/// <c>restricted.json</c> moves it with no edit here. Every case reads text and
/// loads content it never makes current; the shipped file is the master copy the
/// build carries (<see cref="ShippedData.Texts"/>).
/// </summary>
public class DungeonsJsonTests
{
    private static readonly string Shipped = DataTexts.Shipped.Dungeons!;

    private static readonly string[] Fields = ["id", "name", "theme", "attunement", "bossFloorMin", "bossFloorMax", "boss", "retiresOnClear"];

    private static readonly string[] BossFields = ["id", "name", "maxHp", "movementBudget", "innate"];

    private static ContentLoad Load(string dungeons, string? restricted = null)
        => ShippedData.Load(DataTexts.Shipped with { Dungeons = dungeons, Restricted = restricted ?? DataTexts.Shipped.Restricted });

    /// <summary>Two dungeon rows the same, field for field — the boss's innate modifiers by content, since the record holds them by reference.</summary>
    internal static void Same(DungeonDef expected, DungeonDef actual)
    {
        Assert.Equal((expected.Id, expected.Name, expected.Theme, expected.Attunement, expected.BossFloorMin, expected.BossFloorMax, expected.RetiresOnClear),
            (actual.Id, actual.Name, actual.Theme, actual.Attunement, actual.BossFloorMin, actual.BossFloorMax, actual.RetiresOnClear));
        Assert.Equal((expected.Boss.Id, expected.Boss.Name, expected.Boss.MaxHp, expected.Boss.MovementBudget),
            (actual.Boss.Id, actual.Boss.Name, actual.Boss.MaxHp, actual.Boss.MovementBudget));
        Assert.Equal(expected.Boss.Innate.OrderBy(entry => entry.Key), actual.Boss.Innate.OrderBy(entry => entry.Key));
    }

    [Fact]
    public void TheFieldsAreSectionFivePointEights()
    {
        // PHASE5-150: "name, theme modifier, attunement, boss definition, floor
        // range, and the flags the tutorial needs". The shipped file is the master
        // copy of every dungeon, in the order the hub lists them: no second record
        // of them is kept to hold it to.
        string text = ShippedData.Texts.Dungeons!;
        var read = DungeonsJson.Read(text).Dungeons;

        // §5.8's names verbatim, every row naming all eight and every boss all five.
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        Assert.Equal(["dungeons"], document.RootElement.EnumerateObject().Select(member => member.Name));
        var rows = document.RootElement.GetProperty("dungeons").EnumerateArray().ToList();
        Assert.All(rows, row => Assert.Equal(Fields, row.EnumerateObject().Select(member => member.Name)));
        Assert.All(rows, row => Assert.Equal(BossFields, row.GetProperty("boss").EnumerateObject().Select(member => member.Name)));
        JsonElement Row(string id) => rows.Single(row => row.GetProperty("id").GetString() == id);

        // PHASE5-151: theme, a ModifierType by its name.
        Assert.Equal(["Block", "Brace", "Longshot", "CritWindow", "Charges"], rows.Select(row => row.GetProperty("theme").GetString()));

        // PHASE5-153: attunement, a damage type or none — none on every shipped
        // dungeon (settled: its own field, and none of the five sets it)...
        Assert.All(rows, row => Assert.Equal(JsonValueKind.Null, row.GetProperty("attunement").ValueKind));

        // PHASE5-155: bossFloorMin/bossFloorMax — the tutorial pins both to 2, and
        // the rest leave them null, which reads the tuning's range.
        var tutorial = Row("tutorial");
        Assert.Equal((2, 2), (tutorial.GetProperty("bossFloorMin").GetInt32(), tutorial.GetProperty("bossFloorMax").GetInt32()));
        Assert.All(rows.Where(row => row.GetProperty("id").GetString() != "tutorial"), row =>
            Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (row.GetProperty("bossFloorMin").ValueKind, row.GetProperty("bossFloorMax").ValueKind)));
        var content = Load(text).Content!;
        Assert.Equal((content.Tuning.BossFloorMin, content.Tuning.BossFloorMax), content.Dungeons.BossFloorRange(content.Dungeons["spear_hall"]));
        Assert.Equal((2, 2), content.Dungeons.BossFloorRange(content.Dungeons.Tutorial));

        // PHASE5-157: boss, "statline plus innate modifiers" — the golem's HP, its
        // movement budget and its Block x3, read as written (the golem's own
        // numbers are S:377's, pinned in DungeonContentTests.TheTutorialIsTheShippedDungeon).
        Assert.Equal("{ \"Block\": 3 }", tutorial.GetProperty("boss").GetProperty("innate").GetRawText());
        var golem = read.Single(d => d.Id == "tutorial").Boss;
        Assert.Equal((golem.MaxHp, golem.MovementBudget), (tutorial.GetProperty("boss").GetProperty("maxHp").GetInt32(), tutorial.GetProperty("boss").GetProperty("movementBudget").GetInt32()));

        // PHASE5-161: retiresOnClear, the tutorial's alone.
        Assert.Equal([true, false, false, false, false], rows.Select(row => row.GetProperty("retiresOnClear").GetBoolean()));

        // ...and attuning one is a one-field edit (settled: "attuning one is a
        // one-field content edit"): the Spear Hall attuned to Cold loads, attuned.
        var attuned = Load(DataTexts.InRow(Shipped, "spear_hall", "\"attunement\": null", "\"attunement\": \"Cold\""));
        Assert.Null(attuned.Error);
        Assert.Equal(DamageType.Cold, attuned.Content!.Dungeons["spear_hall"].Attunement);

        // A theme is a modifier's name — one that is none is an id that resolves to
        // nothing — and an attunement a damage type's; each is named by its path.
        var theme = Assert.Throws<ContentException>(() => DungeonsJson.Read(DataTexts.InRow(Shipped, "spear_hall", "\"theme\": \"Brace\"", "\"theme\": \"Brase\"")));
        Assert.Equal(("spear_hall", ContentValidator.RuleUnknownId, "dungeons.json: dungeons[1].theme: is the text Brase, which names no ModifierType - did you mean Brace?"),
            (theme.EntryId, theme.Rule, theme.Detail));
        var element = Assert.Throws<ContentException>(() => DungeonsJson.Read(DataTexts.InRow(Shipped, "spear_hall", "\"attunement\": null", "\"attunement\": \"Frost\"")));
        Assert.Equal(("spear_hall", ContentValidator.RuleFileShape), (element.EntryId, element.Rule));
        Assert.StartsWith("dungeons.json: dungeons[1].attunement: is the text Frost, which names no DamageType", element.Detail);
        var innate = Assert.Throws<ContentException>(() => DungeonsJson.Read(DataTexts.InRow(Shipped, "tutorial", "\"innate\": { \"Block\": 3 }", "\"innate\": { \"Block\": \"3\" }")));
        Assert.Equal(("tutorial", ContentValidator.RuleFileShape, "dungeons.json: dungeons[0].boss.innate.Block: is the text 3, not a whole number"),
            (innate.EntryId, innate.Rule, innate.Detail));
        var bossField = Assert.Throws<ContentException>(() => DungeonsJson.Read(DataTexts.InRow(Shipped, "tutorial", $"\"maxHp\": {golem.MaxHp},", $"\"hp\": {golem.MaxHp},")));
        Assert.Equal(("tutorial", "dungeons.json: dungeons[0].boss.hp: is not a member here - the members are id, name, maxHp, movementBudget, innate"),
            (bossField.EntryId, bossField.Detail));
    }

    [Fact]
    public void NoDropTableIsWrittenOrRead()
    {
        // PHASE5-164: no drop-table field — "authoring it by hand would let the two
        // disagree, and the disagreement would look exactly like a drop-rate bug".
        // The written file has none, and a file that adds one is refused as a
        // member the shape does not have, stopping the game at the dungeons' stage.
        using (var document = JsonDocument.Parse(Shipped, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }))
            Assert.All(document.RootElement.GetProperty("dungeons").EnumerateArray(),
                row => Assert.DoesNotContain(row.EnumerateObject(), member => member.Name.Contains("drop", StringComparison.OrdinalIgnoreCase)));

        string authored = DataTexts.InRow(Shipped, "spear_hall", "\"retiresOnClear\": false", "\"retiresOnClear\": false,\n      \"dropTable\": [\"Dagger\", \"Sword\", \"Spear\"]");
        var refused = Assert.Throws<ContentException>(() => DungeonsJson.Read(authored));
        Assert.Equal(("spear_hall", ContentValidator.RuleFileShape), (refused.EntryId, refused.Rule));
        Assert.StartsWith("dungeons.json: dungeons[1].dropTable: is not a member here", refused.Detail);
        var error = Load(authored).Error!;
        Assert.Equal((DungeonsJson.FileName, false, "spear_hall", ContentValidator.RuleFileShape), (error.File, error.BuiltIn, error.EntryId, error.Rule));
    }

    [Fact]
    public void ARestrictedEditMovesTheDropTableWithNoDungeonEdit()
    {
        // PHASE5-165: "a restricted.json edit that makes Brace legal on axes
        // changes the Brace dungeon's drop table with no other edit anywhere —
        // because the table was never written down". Through the files: the
        // shipped Spear Hall drops daggers, swords and spears, since every axe
        // forges Opportunist and the threat zone excludes Brace beside it; take
        // Brace out of the threat-zone group and the axe is in the table, with
        // dungeons.json the very same text.
        var shipped = Load(Shipped).Content!;
        var hall = shipped.Dungeons["spear_hall"];
        Assert.Equal([WeaponClass.Dagger, WeaponClass.Sword, WeaponClass.Spear], shipped.Dungeons.ThemeClasses(hall.Theme));

        string ungrouped = DataTexts.Edit(DataTexts.Shipped.Restricted!, "[\"Brace\", \"Opportunist\", \"Overwatch\"]", "[\"Opportunist\", \"Overwatch\"]");
        var edited = Load(Shipped, ungrouped);
        Assert.Null(edited.Error);
        Assert.Empty(edited.Notices);
        Assert.Equal([WeaponClass.Dagger, WeaponClass.Sword, WeaponClass.Spear, WeaponClass.Axe], edited.Content!.Dungeons.ThemeClasses(ModifierType.Brace));
        Assert.Equal(shipped.Dungeons.All.Select(d => d.Id), edited.Content.Dungeons.All.Select(d => d.Id));
        foreach (var (before, after) in shipped.Dungeons.All.Zip(edited.Content.Dungeons.All))
            Same(before, after);

        // The boss's hands follow the same table, narrowed to what an enemy fields.
        Assert.Contains(WeaponClass.Axe, edited.Content.Dungeons.WieldClasses(edited.Content.Dungeons["spear_hall"]));
        Assert.DoesNotContain(WeaponClass.Axe, shipped.Dungeons.WieldClasses(hall));
    }

    [Fact]
    public void TwoRetiringDungeonsInAFileAbortStartup()
    {
        // Re-pinning PHASE5-169 through the file: "only one dungeon retires on
        // clear" — "two un-repeatable dungeons is a content decision nobody has
        // made". A dungeons.json retiring the Spear Hall as well (its floor pinned,
        // as a retiring dungeon's must be) stops the game at the dungeons' stage,
        // naming the second; one retiring none names the file.
        string two = DataTexts.InRow(DataTexts.InRow(Shipped, "spear_hall", "\"bossFloorMin\": null,\n      \"bossFloorMax\": null,", "\"bossFloorMin\": 5,\n      \"bossFloorMax\": 5,"),
            "spear_hall", "\"retiresOnClear\": false", "\"retiresOnClear\": true");
        var error = Load(two).Error!;
        Assert.Equal(("spear_hall", ContentValidator.RuleOneRetiringDungeon, DungeonsJson.FileName, false), (error.EntryId, error.Rule, error.File, error.BuiltIn));
        Assert.Equal("2 dungeons retire on clear", error.Detail);

        var none = Load(DataTexts.InRow(Shipped, "tutorial", "\"retiresOnClear\": true", "\"retiresOnClear\": false")).Error!;
        Assert.Equal(("dungeons", ContentValidator.RuleOneRetiringDungeon, DungeonsJson.FileName), (none.EntryId, none.Rule, none.File));

        // A second retiring dungeon that leaves its floor to the tuning is refused
        // for that first, as it is in code: a retiring dungeon pins its floor.
        var unpinned = Load(DataTexts.InRow(Shipped, "spear_hall", "\"retiresOnClear\": false", "\"retiresOnClear\": true")).Error!;
        Assert.Equal(("spear_hall", ContentValidator.RuleRetiringDungeonPinsItsFloor), (unpinned.EntryId, unpinned.Rule));
    }
}
