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

    [Fact]
    public void ReadsWhatIsBesideTheExecutable_AndNothingThatIsAbsent()
    {
        InTempFolder(folder =>
        {
            string path = Path.Combine(folder, TuningJson.FileName);
            var report = new List<string>();

            // Nothing there: no text, and the log says where it looked.
            var none = DataFiles.Read(folder, report.Add);
            Assert.Null(none.Tuning);
            Assert.Equal([$"[Data] {path} is missing - the built-in one plays"], report);

            // A tuning.json alone: read whole, reported by its path in that folder,
            // and nothing else read — the content files are the compiled records in
            // Phase 5.1.
            File.WriteAllText(path, """{ "MovementBudget": 200 }""");
            report.Clear();
            var texts = DataFiles.Read(folder, report.Add);
            Assert.Equal(new ContentTexts(Tuning: """{ "MovementBudget": 200 }"""), texts);
            Assert.Equal([$"[Data] Read {path}"], report);
            Assert.StartsWith(folder, path, StringComparison.Ordinal);

            // And the game plays what it read.
            Assert.Equal(200, ContentFiles.Load(texts).Content!.Tuning.MovementBudget);
        });
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
            Assert.Equal([$"[Data] Wrote {path}"], report);
            Assert.Equal(Encoding.UTF8.GetBytes(written.Tuning!), File.ReadAllBytes(path));
            Assert.Equal(written, DataFiles.Read(target));
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
