using System.Text.RegularExpressions;
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

    /// <summary>Every file the generator writes, by name, in the load order.</summary>
    private static IEnumerable<(string Name, string Text)> Generated()
    {
        var texts = ContentFiles.WriteDefaults();
        string?[] all = [texts.Tuning, texts.Restricted, texts.Weapons, texts.Dungeons];
        for (int i = 0; i < all.Length; i++)
            if (all[i] is { } text)
                yield return (ContentFiles.FileNames[i], text);
    }

    [Fact]
    public void TheShippedFilesAreTheGeneratorsOutput()
    {
        // PHASE5-080: the file that puts every number on one screen is the one
        // the game ships, and it is exactly what the generator writes from the
        // compiled content — so the two can never tell different stories. All
        // four: the tuning and the three content files (the enchantment catalogue
        // and the starting party are code, and ship no file).
        var generated = Generated().ToList();
        Assert.Equal([TuningJson.FileName, RestrictedJson.FileName, WeaponsJson.FileName, DungeonsJson.FileName],
            generated.Select(file => file.Name));
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
        Assert.Equal(TuningJson.Hash(ShippedContent.Tuning), TuningJson.Hash(read.Tuning));

        // Read beside the other shipped data files, as the game reads it, so any
        // notice would be the tuning's.
        var load = ContentFiles.Load(DataFiles.Read(ShippedFolder));
        Assert.Empty(load.Notices);
        Assert.Equal(TuningJson.Hash(ShippedContent.Tuning), TuningJson.Hash(load.Content!.Tuning));
    }

    [Fact]
    public void TheGeneratorWritesWhatTheGameReads()
    {
        // The generator's four files, loaded as the game loads them, are the
        // compiled content with nothing to say: no file missing, no number fitted
        // or overridden, no failure — and each reads back to the record it was
        // written from, which writes out again as the very same text (1h: "read
        // back to records that re-write identically").
        var texts = ContentFiles.WriteDefaults();
        var load = ContentFiles.Load(texts);
        Assert.Null(load.Error);
        Assert.Empty(load.Notices);
        Assert.Empty(load.TuningOverrides);

        Assert.Equal(texts.Restricted, RestrictedJson.Write(RestrictedJson.Read(texts.Restricted!)));
        Assert.Equal(texts.Weapons, WeaponsJson.Write(WeaponsJson.Read(texts.Weapons!)));
        Assert.Equal(texts.Dungeons, DungeonsJson.Write(DungeonsJson.Read(texts.Dungeons!)));
        Assert.Equal(texts.Tuning, TuningJson.Write(TuningJson.Read(texts.Tuning).Tuning));

        // The content the files load is the compiled content, catalogue for
        // catalogue: the same rows, in the same order, written the same way — and
        // beside it the game's own catalogue and starting party, the code's rows.
        var content = load.Content!;
        Assert.Equal(WeaponsJson.Write(ShippedContent.Weapons), WeaponsJson.Write(ShippedContent.Weapons with { Weapons = content.Weapons.All }));
        Assert.Equal(DungeonsJson.Write(ShippedContent.Dungeons), DungeonsJson.Write(new DungeonsData(content.Dungeons.All)));
        Assert.Equal(StartingParty.Data.Members, content.Party.All);
        Assert.Equal(EnchantmentDefinitions.Data.Enchantments, content.Enchantments.All);
        Assert.Equal(TuningJson.Hash(ShippedContent.Tuning), TuningJson.Hash(content.Tuning));
    }

    [Fact]
    public void AProvisionalRowSaysSo()
    {
        // A designer tuning the copy beside the game can tell a guessed row from
        // one the docs fixed, as tuning.json's "Provisional:" descriptions mark a
        // number: every id ContentDefaults marks provisional — the three unnamed
        // uniques, the four dungeons beside the tutorial and their bosses — is
        // followed by /* provisional */ in its file, and no other row is. The note
        // is a comment: every file still reads back to its record
        // (TheGeneratorWritesWhatTheGameReads). (The catalogue's own guesses — the
        // shielding entry's id, Burning's lock — are code, and say so in their
        // comments in EnchantmentDefinitions.)
        var texts = ContentFiles.WriteDefaults();
        var marked = new List<string>();
        var rows = new List<string>();
        foreach (string? text in new[] { texts.Restricted, texts.Weapons, texts.Dungeons })
        {
            foreach (Match row in Regex.Matches(text!, "\"id\": \"([^\"]+)\",?( /\\* provisional \\*/)?\n"))
            {
                rows.Add(row.Groups[1].Value);
                if (row.Groups[2].Success)
                    marked.Add(row.Groups[1].Value);
            }
            // The note appears after an id and nowhere else in the file.
            Assert.Equal(Regex.Matches(text!, Regex.Escape(JsonText.ProvisionalNote)).Count,
                Regex.Matches(text!, "\"id\": \"[^\"]+\",? " + Regex.Escape(JsonText.ProvisionalNote)).Count);
        }

        Assert.Equal(ContentDefaults.ProvisionalIds.Order(StringComparer.Ordinal), marked.Order(StringComparer.Ordinal));

        // Every provisional id is a row some file writes, so the set cannot go
        // stale behind a renamed row; and the docs' own rows are not marked.
        Assert.Subset(rows.ToHashSet(StringComparer.Ordinal), ContentDefaults.ProvisionalIds.ToHashSet(StringComparer.Ordinal));
        foreach (string fixedByTheDocs in new[] { "tutorial", "stone_golem", "widowmaker", "arming_sword", "regeneration", "A", "D" })
            Assert.DoesNotContain(fixedByTheDocs, marked);

        // A row an edit adds is marked only when ContentDefaults says so: the
        // writer asks the set, never the row.
        string renamed = WeaponsJson.Write(ShippedContent.Weapons with { Weapons = ShippedContent.Weapons.Weapons.Select(w => w.Id == "widowmaker" ? w with { Id = "widowmaker_2" } : w).ToList() });
        Assert.Contains("\"id\": \"widowmaker_2\",\n", renamed);
    }
}
