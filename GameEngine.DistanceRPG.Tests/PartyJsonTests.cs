using System.Text.Json;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §5.9's <c>party.json</c> as a file (PHASE5-170, -171, -172, -174, -177, -178,
/// re-pinning -173 and -175): "the four-character starting roster" under the
/// doc's own names — <c>id</c>, <c>name</c>, <c>stats</c> in capitals,
/// <c>startingWeaponId</c> — and the bag beside them; "a starting loadout is
/// content, not a constant", so a campaign starts with whoever the file names. In
/// the content collection: the roster test makes the parsed roster current
/// (<see cref="TestContent.Use"/>), since a campaign starts under the current
/// content; every other case reads text and loads content it never makes current.
/// </summary>
[Collection(TestContent.Collection)]
public class PartyJsonTests
{
    private static readonly string Shipped = DataTexts.Shipped.Party!;

    private static readonly string[] Fields = ["id", "name", "stats", "startingWeaponId", "bagWeaponIds"];

    private static ContentLoad Load(string party) => ContentFiles.Load(DataTexts.Shipped with { Party = party });

    [Fact]
    public void TheFieldsAreSectionFivePointNines_StatsInCapitals()
    {
        // PHASE5-170: the four-character starting roster, read from the shipped
        // file to exactly the compiled one, member for member and in spawn order.
        string text = File.ReadAllText(ShippedDataTests.Shipped(PartyJson.FileName));
        var read = PartyJson.Read(text).Members;
        var compiled = ContentDefaults.Party.Members;
        Assert.Equal(compiled.Count, read.Count);
        for (int i = 0; i < read.Count; i++)
        {
            Assert.Equal((compiled[i].Id, compiled[i].Name, compiled[i].Stats, compiled[i].StartingWeaponId), (read[i].Id, read[i].Name, read[i].Stats, read[i].StartingWeaponId));
            Assert.Equal(compiled[i].BagWeaponIds, read[i].BagWeaponIds);
        }

        // PHASE5-171, -172, -174: id and name, stats as { STR, DEX, CON, INT } in
        // the doc's capitals, startingWeaponId a weapons.json id — and the bag, the
        // record's fourth field beyond §5.9's three.
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        Assert.Equal(["members"], document.RootElement.EnumerateObject().Select(member => member.Name));
        var rows = document.RootElement.GetProperty("members").EnumerateArray().ToList();
        Assert.All(rows, row => Assert.Equal(Fields, row.EnumerateObject().Select(member => member.Name)));
        Assert.All(rows, row => Assert.Equal(["STR", "DEX", "CON", "INT"], row.GetProperty("stats").EnumerateObject().Select(member => member.Name)));
        Assert.Equal(["A", "B", "C", "D"], rows.Select(row => row.GetProperty("id").GetString()));
        Assert.Equal("{ \"STR\": 1, \"DEX\": 4, \"CON\": 2, \"INT\": 3 }", rows[0].GetProperty("stats").GetRawText());
        var weapons = ContentDefaults.Weapons.Weapons.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(rows, row => Assert.Contains(row.GetProperty("startingWeaponId").GetString()!, weapons));

        // A stat is its capital name, exactly: a lower-case one is not a member,
        // and pointed at the name it is; one left out is missing.
        var lower = Assert.Throws<ContentException>(() => PartyJson.Read(DataTexts.InRow(Shipped, "A", "\"STR\": 1", "\"str\": 1")));
        Assert.Equal(("A", ContentValidator.RuleFileShape, "party.json: members[0].stats.str: is not a member here - did you mean STR?"), (lower.EntryId, lower.Rule, lower.Detail));
        var missing = Assert.Throws<ContentException>(() => PartyJson.Read(DataTexts.InRow(Shipped, "A", "\"STR\": 1, ", "")));
        Assert.Equal(("A", "party.json: members[0].stats.STR: is missing"), (missing.EntryId, missing.Detail));
        var text4 = Assert.Throws<ContentException>(() => PartyJson.Read(DataTexts.InRow(Shipped, "A", "\"bagWeaponIds\": [\"staff_of_renewal\"]", "\"bagWeaponIds\": \"staff_of_renewal\"")));
        Assert.Equal(("A", "party.json: members[0].bagWeaponIds: is the text staff_of_renewal, not a list of weapon ids, in slot order"), (text4.EntryId, text4.Detail));

        // Through the loader, a member's shape stops the game at the roster's stage.
        var error = Load(DataTexts.InRow(Shipped, "A", "\"STR\": 1", "\"str\": 1")).Error!;
        Assert.Equal((PartyJson.FileName, false, "A", ContentValidator.RuleFileShape), (error.File, error.Compiled, error.EntryId, error.Rule));
    }

    [Fact]
    public void TheRosterIsTheFilesToEdit()
    {
        // PHASE5-177, -178: "Party B/C/D's own identities (which pair splits which
        // stats, which debuff staff D carries) are decided in this file, not
        // hardcoded" — "a starting loadout is content, not a constant". A party.json
        // swapping A's and C's spreads, and handing D Blight for Mire, starts a
        // campaign with them swapped and D carrying Blight: a data edit, no rebuild.
        string edited = DataTexts.InRow(
            DataTexts.InRow(
                DataTexts.InRow(Shipped, "A", "\"stats\": { \"STR\": 1, \"DEX\": 4, \"CON\": 2, \"INT\": 3 }", "\"stats\": { \"STR\": 4, \"DEX\": 2, \"CON\": 3, \"INT\": 1 }"),
                "C", "\"stats\": { \"STR\": 4, \"DEX\": 2, \"CON\": 3, \"INT\": 1 }", "\"stats\": { \"STR\": 1, \"DEX\": 4, \"CON\": 2, \"INT\": 3 }"),
            "D", "\"bagWeaponIds\": [\"staff_of_mire\"]", "\"bagWeaponIds\": [\"staff_of_blight\"]");
        Assert.Null(Load(edited).Error);

        using (TestContent.Use(party: PartyJson.Read(edited)))
        {
            var campaign = CampaignState.Start(GameContent.Current);
            var party = campaign.Party.ToDictionary(member => member.Id);
            Assert.Equal(["A", "B", "C", "D"], campaign.Party.Select(member => member.Id));
            Assert.Equal(new InnateStats(STR: 4, DEX: 2, CON: 3, INT: 1), party["A"].Stats);
            Assert.Equal(new InnateStats(STR: 1, DEX: 4, CON: 2, INT: 3), party["C"].Stats);
            Assert.Equal(new InnateStats(STR: 3, DEX: 1, CON: 4, INT: 2), party["B"].Stats);   // untouched
            Assert.Equal("weakspot_stiletto", party["A"].Inventory[0]!.Id);                      // A keeps the dagger: only the spread moved
            Assert.Equal("staff_of_blight", party["D"].Inventory[1]!.Id);
        }

        // And the shipped file starts the shipped party.
        using (TestContent.Use(party: PartyJson.Read(Shipped)))
        {
            var campaign = CampaignState.Start(GameContent.Current);
            Assert.Equal(ContentDefaults.Party.Members.Select(m => m.Stats), campaign.Party.Select(member => member.Stats));
            Assert.Equal("staff_of_mire", campaign.Party.Single(member => member.Id == "D").Inventory[1]!.Id);
        }
    }

    [Fact]
    public void AnUnknownStartingWeaponAbortsStartup()
    {
        // Re-pinning PHASE5-175 through the file: startingWeaponId is "a
        // weapons.json id" that "must exist". A typo stops the game at the roster's
        // stage, naming the member, the rule and the id; so does a bag weapon.
        var error = Load(DataTexts.InRow(Shipped, "B", "\"startingWeaponId\": \"tower_guard\"", "\"startingWeaponId\": \"tower_gaurd\"")).Error!;
        Assert.Equal(("B", ContentValidator.RulePartyWeaponExists, PartyJson.FileName, false), (error.EntryId, error.Rule, error.File, error.Compiled));
        Assert.Equal("tower_gaurd", error.Detail);

        var bag = Load(DataTexts.InRow(Shipped, "D", "\"bagWeaponIds\": [\"staff_of_mire\"]", "\"bagWeaponIds\": [\"staff_of_mud\"]")).Error!;
        Assert.Equal(("D", ContentValidator.RulePartyWeaponExists, "staff_of_mud"), (bag.EntryId, bag.Rule, bag.Detail));

        // A weapon the weapons.json beside it names is a weapon the roster may
        // start with: the Halberd renamed there, and handed to C here.
        string renamed = DataTexts.Edit(DataTexts.Shipped.Weapons!, "\"id\": \"halberd\",", "\"id\": \"bardiche\",");
        var bardiche = ContentFiles.Load(DataTexts.Shipped with
        {
            Weapons = renamed,
            Party = DataTexts.InRow(Shipped, "C", "\"startingWeaponId\": \"great_axe\"", "\"startingWeaponId\": \"bardiche\""),
        });
        Assert.Null(bardiche.Error);
        Assert.Equal("bardiche", bardiche.Content!.Party["C"].StartingWeaponId);
    }

    [Fact]
    public void ANonPermutationAbortsStartup()
    {
        // Re-pinning PHASE5-173 through the file: stats are "one permutation of
        // 1-4". A spread of 1, 1, 4, 4 — the same total, better endowed twice —
        // stops the game at the roster's stage, naming the member and the spread.
        var error = Load(DataTexts.InRow(Shipped, "A", "\"stats\": { \"STR\": 1, \"DEX\": 4, \"CON\": 2, \"INT\": 3 }", "\"stats\": { \"STR\": 1, \"DEX\": 4, \"CON\": 1, \"INT\": 4 }")).Error!;
        Assert.Equal(("A", ContentValidator.RulePartyStatPermutation, PartyJson.FileName, false), (error.EntryId, error.Rule, error.File, error.Compiled));

        // Five of anything is a whole number, and not a stat: refused the same way.
        var five = Load(DataTexts.InRow(Shipped, "D", "\"INT\": 4", "\"INT\": 5")).Error!;
        Assert.Equal(("D", ContentValidator.RulePartyStatPermutation), (five.EntryId, five.Rule));
    }
}
