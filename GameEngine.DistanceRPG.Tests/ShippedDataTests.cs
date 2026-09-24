using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The shipped data files are the generator's output (USER DECISION 3):
/// <see cref="ContentDefaults"/> is the one source, <c>--write-data</c> writes the
/// files from it, and the committed <c>GameEngine.DistanceRPG/Data/*.json</c> are
/// held here to that output byte for byte (line ends aside: a checkout may turn
/// them). A change worth keeping goes into the compiled content and is
/// regenerated; an edit made to the committed file alone fails here, naming the
/// file. Read from the test output's own linked copies in <c>Data\</c>, by that
/// explicit path — never from any folder a game or a test reads by default.
/// </summary>
public class ShippedDataTests
{
    /// <summary>The test output's own linked copies of the shipped files, read by this explicit path.</summary>
    internal static string ShippedFolder => Path.Combine(AppContext.BaseDirectory, "Data");

    internal static string Shipped(string name) => Path.Combine(ShippedFolder, name);

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Every file the generator writes, by name, in §5.4's order.</summary>
    private static IEnumerable<(string Name, string Text)> Generated()
    {
        var texts = ContentFiles.WriteDefaults();
        string?[] all = [texts.Tuning, texts.Restricted, texts.Enchantments, texts.Weapons, texts.Dungeons, texts.Party];
        for (int i = 0; i < all.Length; i++)
            if (all[i] is { } text)
                yield return (ContentFiles.FileNames[i], text);
    }

    [Fact]
    public void TheShippedFilesAreTheGeneratorsOutput()
    {
        // PHASE5-080: the file that puts every number on one screen is the one
        // the game ships, and it is exactly what the generator writes from the
        // compiled content — so the two can never tell different stories.
        var generated = Generated().ToList();
        Assert.Equal([TuningJson.FileName, RestrictedJson.FileName, EnchantmentsJson.FileName], generated.Select(file => file.Name));
        foreach (var (name, text) in generated)
        {
            string path = Shipped(name);
            Assert.True(File.Exists(path), $"{name} is not shipped: run DistanceRPG --write-data GameEngine.DistanceRPG/Data");
            Assert.True(Normalized(File.ReadAllText(path)) == text,
                $"{name} is not the generator's output: edit ContentDefaults or Tuning.cs and run DistanceRPG --write-data GameEngine.DistanceRPG/Data");
        }

        // And nothing is shipped that the generator does not write.
        var shipped = Directory.GetFiles(ShippedFolder, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(generated.Select(file => file.Name).Order(StringComparer.Ordinal), shipped);
    }

    [Fact]
    public void TheShippedTuningIsTheCompiledTuning()
    {
        // The shipped tuning.json reads with nothing to say, changes no number,
        // and hashes as the compiled tuning does: a game started beside it plays
        // exactly what a game with no file plays.
        var read = TuningJson.Read(File.ReadAllText(Shipped(TuningJson.FileName)));
        Assert.Empty(read.Warnings);
        Assert.Empty(read.Overrides);
        Assert.Equal(TuningJson.Hash(ContentDefaults.Tuning), TuningJson.Hash(read.Tuning));

        // Read beside the other shipped data files, as the game reads it, so any
        // notice would be the tuning's.
        var load = ContentFiles.Load(DataFiles.Read(ShippedFolder));
        Assert.Empty(load.Notices);
        Assert.Equal(TuningJson.Hash(ContentDefaults.Tuning), TuningJson.Hash(load.Content!.Tuning));
    }
}
