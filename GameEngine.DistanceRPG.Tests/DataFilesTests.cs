using System.Text;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The data files' edge (1e): the data sits beside the executable (S:43,
/// PHASE5-033), read from the folder handed in — there is no default one, so
/// these read and write a temporary folder of their own and nothing else, and
/// delete it after — with every file read or missed reported by its full path.
/// </summary>
public class DataFilesTests
{
    /// <summary>A fresh empty folder under the system's temp folder, deleted with everything in it once <paramref name="use"/> is done.</summary>
    private static void InTempFolder(Action<string> use)
    {
        var folder = Directory.CreateTempSubdirectory("distancerpg-");
        try
        {
            use(folder.FullName);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>The data files the edge reads, in the load order: the tuning, then the three content files — the relations, the weapons, the dungeons.</summary>
    private static readonly string[] FilesRead =
        [TuningJson.FileName, RestrictedJson.FileName, WeaponsJson.FileName, DungeonsJson.FileName];

    [Fact]
    public void ReadsWhatIsBesideTheExecutable_AndNothingThatIsAbsent()
    {
        InTempFolder(folder =>
        {
            string path = Path.Combine(folder, TuningJson.FileName);
            var report = new List<string>();

            // Nothing there: no text, and the log says where it looked for each file.
            var none = DataFiles.Read(folder, report.Add);
            Assert.Equal(new ContentTexts(), none);
            Assert.Equal(FilesRead.Select(name => $"[Data] {Path.Combine(folder, name)} is missing - the built-in one plays"), report);

            // A tuning.json alone: read whole, reported by its path in that folder,
            // and the three content files looked for and reported missing — the
            // compiled records play for them.
            File.WriteAllText(path, """{ "MovementBudget": 200 }""");
            report.Clear();
            var texts = DataFiles.Read(folder, report.Add);
            Assert.Equal(new ContentTexts(Tuning: """{ "MovementBudget": 200 }"""), texts);
            Assert.Equal([$"[Data] Read {path}", .. FilesRead.Skip(1).Select(name => $"[Data] {Path.Combine(folder, name)} is missing - the built-in one plays")], report);
            Assert.StartsWith(folder, path, StringComparison.Ordinal);

            // And the game plays what it read.
            Assert.Equal(200, ContentFiles.Load(texts).Content!.Tuning.MovementBudget);
        });
    }

    [Fact]
    public void AFileThatIsThereButCannotBeReadSaysSo_NeverThatItIsMissing()
    {
        // An editor holding tuning.json open without sharing it: the file is plainly
        // there, and the game cannot read it. The built-in numbers play, as for a
        // missing file. The screen says the file could not be read, and why, and
        // never that it is missing. The log has the path and the whole error.
        InTempFolder(folder =>
        {
            // The other data files sit beside it, as they ship, so any notice is the tuning's.
            DataFiles.Write(folder, ContentFiles.WriteDefaults() with { Tuning = null });
            string path = Path.Combine(folder, TuningJson.FileName);
            File.WriteAllText(path, """{ "MovementBudget": 200 }""");
            var report = new List<string>();
            ContentTexts texts;
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                texts = DataFiles.Read(folder, report.Add);

            Assert.Null(texts.Tuning);
            string why = Assert.Single(texts.Unreadable!, entry => entry.Key == TuningJson.FileName).Value;
            if (OperatingSystem.IsWindows())
                Assert.Equal("another program has it open", why);
            Assert.DoesNotContain(folder, why, StringComparison.OrdinalIgnoreCase);   // the path is the log's; the screen names the file
            Assert.Collection(report,
                line => Assert.StartsWith($"[Data] {path} could not be read (", line, StringComparison.Ordinal),
                line => Assert.Equal($"[Data] Read {Path.Combine(folder, RestrictedJson.FileName)}", line),
                line => Assert.Equal($"[Data] Read {Path.Combine(folder, WeaponsJson.FileName)}", line),
                line => Assert.Equal($"[Data] Read {Path.Combine(folder, DungeonsJson.FileName)}", line));

            var load = ContentFiles.Load(texts);
            Assert.Equal([$"tuning.json could not be read ({why}) - the built-in numbers are playing"], load.Notices);
            Assert.DoesNotContain(load.Notices, notice => notice.Contains("missing", StringComparison.Ordinal));
            Assert.Equal(TuningJson.Hash(ShippedContent.Tuning), TuningJson.Hash(load.Content!.Tuning));

            // Once it is free again, it is read like any other, beside the shipped
            // files it sits with: every text read whole, and every file reported.
            report.Clear();
            var free = DataFiles.Read(folder, report.Add);
            Assert.Equal(ContentFiles.WriteDefaults() with { Tuning = """{ "MovementBudget": 200 }""" }, free);
            Assert.Equal(FilesRead.Select(name => $"[Data] Read {Path.Combine(folder, name)}"), report);
        });
    }

    [Fact]
    public void WhyAFileCouldNotBeReadIsAFewWords()
    {
        // The reason a notice shows: the two cases a playtester meets named plainly,
        // and anything else the system's own words with the file's name in place of
        // its full path, which the log line already carries.
        string path = Path.Combine(Path.GetTempPath(), "beside-the-exe", TuningJson.FileName);
        Assert.Equal("no permission to read it",
            DataFiles.Why(new UnauthorizedAccessException($"Access to the path '{path}' is denied."), path, TuningJson.FileName));
        Assert.Equal("The file 'tuning.json' is on a disk that has gone",
            DataFiles.Why(new IOException($"The file '{path}' is on a disk that has gone."), path, TuningJson.FileName));
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("another program has it open",
                DataFiles.Why(new IOException("in use", unchecked((int)0x80070020)), path, TuningJson.FileName));   // ERROR_SHARING_VIOLATION
            Assert.Equal("another program has it open",
                DataFiles.Why(new IOException("locked", unchecked((int)0x80070021)), path, TuningJson.FileName));   // ERROR_LOCK_VIOLATION
        }
    }

    [Fact]
    public void WritesTheGeneratorsFiles_AndReadsThemBack()
    {
        // What --write-data does: the generator's texts into the folder, created if
        // need be, UTF-8 with no byte order mark and the text's own "\n" line ends,
        // each file reported by its path — and they read back as written.
        InTempFolder(folder =>
        {
            string target = Path.Combine(folder, "data");
            var report = new List<string>();
            var written = ContentFiles.WriteDefaults();
            DataFiles.Write(target, written, report.Add);

            string path = Path.Combine(target, TuningJson.FileName);
            Assert.Equal(FilesRead.Select(name => $"[Data] Wrote {Path.Combine(target, name)}"), report);
            Assert.Equal(Encoding.UTF8.GetBytes(written.Tuning!), File.ReadAllBytes(path));
            Assert.Equal(Encoding.UTF8.GetBytes(written.Restricted!), File.ReadAllBytes(Path.Combine(target, RestrictedJson.FileName)));
            Assert.Equal(Encoding.UTF8.GetBytes(written.Weapons!), File.ReadAllBytes(Path.Combine(target, WeaponsJson.FileName)));
            Assert.Equal(Encoding.UTF8.GetBytes(written.Dungeons!), File.ReadAllBytes(Path.Combine(target, DungeonsJson.FileName)));
            Assert.Equal(written, DataFiles.Read(target));
        });
    }

    [Fact]
    public void ARetiredFileBesideTheGameIsSaid_AndNotRead()
    {
        // The content rework (1c): the starting party and the enchantment catalogue
        // are code, by the user's ruling of 2026-09-25, so a party.json left beside
        // the game — a playtester's edited copy, say — is never read. The edge
        // names it, and the log gives its path; the load says it is no longer read,
        // and where the party lives now; and the party that plays is the code's.
        InTempFolder(folder =>
        {
            DataFiles.Write(folder, ContentFiles.WriteDefaults());
            string party = Path.Combine(folder, "party.json");
            File.WriteAllText(party, """{ "members": [{ "id": "A", "name": "Edited", "stats": { "STR": 4, "DEX": 3, "CON": 2, "INT": 1 } }] }""");
            var report = new List<string>();
            var texts = DataFiles.Read(folder, report.Add);

            Assert.Equal(["party.json"], texts.Retired);
            Assert.Equal(
            [
                .. FilesRead.Select(name => $"[Data] Read {Path.Combine(folder, name)}"),
                $"[Data] {party}: party.json is no longer read - the starting party is in the code (Logic/Content/StartingParty.cs)",
            ], report);

            var load = ContentFiles.Load(texts);
            Assert.Null(load.Error);
            Assert.Equal(["party.json is no longer read - the starting party is in the code (Logic/Content/StartingParty.cs)"], load.Notices);
            Assert.Equal("PARTY.JSON IS NO LONGER READ - THE STARTING PARTY IS IN THE CODE (LOGIC/CONTENT/STARTINGPARTY.CS)",
                string.Join(" ", NoticeText.Rows(load.Notices).Select(row => row.Trim())));
            Assert.Equal(StartingParty.Data.Members, load.Content!.Party.All);
            Assert.Empty(load.TuningOverrides);

            // Never read, so never unreadable: a copy an editor holds open is named
            // all the same. The catalogue's retired file is said the same way, and
            // both in the order the edge looks, beside the four it reads.
            File.WriteAllText(Path.Combine(folder, "enchantments.json"), "not even JSON");
            report.Clear();
            using (new FileStream(party, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                texts = DataFiles.Read(folder, report.Add);
            Assert.Equal(["enchantments.json", "party.json"], texts.Retired);
            Assert.Null(texts.Unreadable);
            Assert.Equal(FilesRead.Length + 2, report.Count);
            Assert.Equal(
            [
                "enchantments.json is no longer read - the enchantment catalogue is in the code (Logic/Enchantments/EnchantmentDefinitions.cs)",
                "party.json is no longer read - the starting party is in the code (Logic/Content/StartingParty.cs)",
            ], ContentFiles.Load(texts).Notices);

            // And with neither there, nothing is said of them at all.
            File.Delete(party);
            File.Delete(Path.Combine(folder, "enchantments.json"));
            report.Clear();
            Assert.Null(DataFiles.Read(folder, report.Add).Retired);
            Assert.Equal(FilesRead.Select(name => $"[Data] Read {Path.Combine(folder, name)}"), report);
        });
    }

    [Fact]
    public void TheLogNamesTheFileAndEveryNumberItChanged()
    {
        // The log's line for the tuning played: its full path, and every number it
        // changed from the compiled ones.
        string folder = Path.Combine(Path.GetTempPath(), "beside-the-exe");
        string path = Path.Combine(folder, TuningJson.FileName);
        Assert.Equal($"[Data] {path} changes 2 numbers: MovementBudget 200, PerStack.Block 4",
            DataFiles.TuningSummary(folder, ["MovementBudget 200", "PerStack.Block 4"]));
        Assert.Equal($"[Data] {path} changes 1 number: MovementBudget 200", DataFiles.TuningSummary(folder, ["MovementBudget 200"]));
        Assert.Equal($"[Data] {path} changes no numbers - the built-in tuning plays", DataFiles.TuningSummary(folder, []));
    }
}
