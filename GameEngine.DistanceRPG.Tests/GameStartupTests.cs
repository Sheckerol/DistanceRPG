using System.Reflection;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The game's startup, as claims about code (PHASE5-040; 1e): the data files are
/// read once, when the game loads — never on an update path, never hot-reloaded
/// — by the game alone, from the folder beside the executable, which nothing
/// else in the game reads; and the command that writes them writes what the
/// generator makes. Read off the game assembly's IL, so a second caller added
/// anywhere fails here.
/// </summary>
public class GameStartupTests
{
    private static readonly Assembly Game = typeof(DistanceRpgGame).Assembly;

    private static bool Same(MethodBase a, MethodBase b) => a.Module == b.Module && a.MetadataToken == b.MetadataToken;

    /// <summary>Every method in the game assembly that calls <paramref name="target"/>, as <c>Type.Method</c> of its outermost type.</summary>
    private static string[] Callers(MethodBase target)
        => Calls(Game).Where(c => Same(c.Target, target))
            .Select(c => $"{Root(c.From.DeclaringType)!.Name}.{c.From.Name}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void TheDataIsReadOnceAtStartup()
    {
        // The content is made current, the files are loaded and the executable's
        // folder is read in one place: the game's LoadData.
        var use = typeof(GameContent).GetMethod(nameof(GameContent.Use))!;
        var load = typeof(ContentFiles).GetMethod(nameof(ContentFiles.Load))!;
        var baseDirectory = typeof(AppContext).GetProperty(nameof(AppContext.BaseDirectory))!.GetMethod!;
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(use));
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(load));
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(baseDirectory));

        // And LoadData runs from the game's OnLoad alone: once, at startup, never
        // on an update path.
        var loadData = typeof(DistanceRpgGame).GetMethod("LoadData", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(["DistanceRpgGame.OnLoad"], Callers(loadData));
    }

    [Fact]
    public void TheWriteDataCommandWritesWhatTheGeneratorWrites()
    {
        // DistanceRPG --write-data <folder> writes the generator's files into the
        // folder, no window opened: the command's whole body, run into a temp folder.
        var folder = Directory.CreateTempSubdirectory("distancerpg-");
        try
        {
            string target = Path.Combine(folder.FullName, "out");
            var report = new List<string>();
            Program.WriteData(target, report.Add);

            var generated = ContentFiles.WriteDefaults();
            string path = Path.Combine(target, TuningJson.FileName);
            Assert.Equal(generated.Tuning, File.ReadAllText(path));
            Assert.Contains($"[Data] Wrote {path}", report);
        }
        finally
        {
            folder.Delete(recursive: true);
        }

        // The folder is the argument after the option; without one there is none.
        Assert.Equal("Data", Program.WriteDataDirectory(["--write-data", "Data"]));
        Assert.Equal("out", Program.WriteDataDirectory(["--other", "--write-data", "out"]));
        Assert.Null(Program.WriteDataDirectory(["--write-data"]));
        Assert.Null(Program.WriteDataDirectory(["--write-data", "--new-campaign"]));
        Assert.Null(Program.WriteDataDirectory([]));
    }
}
