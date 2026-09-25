using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The content the game loads from its shipped files is, field for field, the
/// content it shipped before the Phase 5 content rework (1i). While the rework
/// retypes the enchantment rows into code, deletes the compiled copies and
/// rewrites <c>weapons.json</c> into class rows, nothing it moves may change what
/// plays — and the per-feature suites pin names, classes, statlines and spreads,
/// not every field of every row: not the provisional bosses' numbers, the uniques'
/// soul tiers, the variants' enchantment lists, or the relations as whole sets.
/// So the content is dumped as one canonical text (<see cref="ContentDump"/>) and
/// held to <c>TestData/content/pre-rework-content.txt</c>, which the code at
/// <c>febe036</c> wrote once and nothing regenerates. Retired with the dump once
/// the rework is done (Phase 5.R5): from then on the JSON files are the master
/// copy, and a designer's edit to them must not fail a test.
/// </summary>
public class ContentEquivalenceTests
{
    private static string PreReworkPath => Path.Combine(AppContext.BaseDirectory, "TestData", "content", "pre-rework-content.txt");

    [Fact]
    public void TheShippedContentIsThePreReworkContent()
    {
        // The content the game loads from the shipped files, as it loads them, and
        // the relations its restricted.json holds, as read — dumped as the
        // pre-rework content was.
        var load = ContentFiles.Load(DataFiles.Read(ShippedDataTests.ShippedFolder));
        Assert.Null(load.Error);
        var relations = RestrictedJson.Read(File.ReadAllText(ShippedDataTests.Shipped(RestrictedJson.FileName)));
        string now = ContentDump.Of(load.Content!, relations);
        string before = File.ReadAllText(PreReworkPath);

        // The first line that moved, named, so a drift reads as the field it is...
        string[] beforeLines = before.Split('\n');
        string[] nowLines = now.Split('\n');
        for (int i = 0; i < Math.Min(beforeLines.Length, nowLines.Length); i++)
            if (beforeLines[i] != nowLines[i])
                Assert.Fail($"line {i + 1} of the pre-rework content is '{beforeLines[i]}', and the shipped content's is '{nowLines[i]}'");

        // ...and then the whole text: no line lost or added at the end.
        Assert.Equal(before, now);
    }
}
