using System.Reflection;
using System.Reflection.Emit;
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
    public void TheStartupNoticesGoToTheFirstSceneAlone()
    {
        // What the data files raised reaches the screen through the first scene
        // the game opens on (1h, 1i). That is the screen between runs, or a run
        // that a dev override boots straight into. The load holds the notices.
        // Each of the two methods that show a scene takes them and passes them
        // straight into that scene's constructor, where the constructor's last
        // argument is the notices. Nothing else takes them.
        var hold = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Hold))!;
        var take = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Take))!;
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(hold));
        Assert.Equal(["DistanceRpgGame.BeginRun", "DistanceRpgGame.ShowBetweenRuns"], Callers(take));
        foreach (var (shows, scene) in new[] { ("ShowBetweenRuns", typeof(BetweenRunsScene)), ("BeginRun", typeof(DungeonScene)) })
        {
            var built = BuiltWithTheTake(shows, take);
            Assert.True(built is not null, $"DistanceRpgGame.{shows} does not hand what it takes straight to a constructor");
            Assert.Equal(scene, built.DeclaringType);
            Assert.Equal("notices", built.GetParameters()[^1].Name);
        }

        // Only once. The first scene is handed the notices, and every scene after
        // it (the next run, the screen between runs it returns to) gets none.
        const string clamped = "tuning.json: MovementUnitsPerMana 0 is below 1 - using 1";
        var notices = new StartupNotices();
        Assert.Empty(notices.Take());
        notices.Hold([clamped]);
        Assert.Equal([clamped], notices.Take());
        Assert.Empty(notices.Take());
        Assert.Empty(notices.Take());

        // Each scene lays out what it is handed, and its HUD draws that. The
        // screen between runs puts the rows under its title. A dungeon scene's
        // HUD puts them over the first floor; HudTextTests builds that scene and
        // reads its rows.
        var rows = typeof(NoticeText).GetMethod(nameof(NoticeText.Rows))!;
        var showNotices = typeof(DungeonHud).GetMethod(nameof(DungeonHud.ShowNotices))!;
        var noticeRows = typeof(BetweenRunsScene).GetProperty("NoticeRows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetMethod!;
        var drawNotices = typeof(DungeonHud).GetMethod("DrawNotices", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Contains("BetweenRunsScene..ctor", Callers(rows));
        Assert.Contains("DungeonScene..ctor", Callers(showNotices));
        Assert.Contains("BetweenRunsHud.Render", Callers(noticeRows));
        Assert.Equal(["DungeonHud.Render"], Callers(drawNotices));
    }

    /// <summary>
    /// The constructor that <c>DistanceRpgGame.<paramref name="method"/></c> calls
    /// with what <paramref name="take"/> returns. That is the constructor called by
    /// the instruction right after the take, so the value taken is its last
    /// argument and nothing else touches it first. Null when no constructor
    /// directly follows the take.
    /// </summary>
    private static MethodBase? BuiltWithTheTake(string method, MethodBase take)
    {
        var shows = typeof(DistanceRpgGame).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var instructions = Instructions(Body(shows)!).ToList();
        for (int i = 0; i + 1 < instructions.Count; i++)
        {
            var (op, token) = instructions[i];
            if ((op == OpCodes.Call || op == OpCodes.Callvirt) && Resolve(shows, token) is { } called && Same(called, take)
                && instructions[i + 1].Op == OpCodes.Newobj)
                return Resolve(shows, instructions[i + 1].Token);
        }
        return null;
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

    [Fact]
    public void TheWriteDataCommandWritesTheSixFiles()
    {
        // DistanceRPG --write-data <folder> regenerates everything the tests pin
        // (USER DECISION 3): the tuning and the five content files, each the
        // generator's text byte for byte — UTF-8, no byte order mark, "\n" line
        // ends — each reported by its full path, and nothing else in the folder.
        // Run into a temp folder of its own, so no real folder is touched; a game
        // started beside what it wrote plays the compiled content, saying nothing.
        var folder = Directory.CreateTempSubdirectory("distancerpg-");
        try
        {
            string target = Path.Combine(folder.FullName, "data");
            var report = new List<string>();
            Program.WriteData(target, report.Add);

            var generated = ContentFiles.WriteDefaults();
            string?[] texts = [generated.Tuning, generated.Restricted, generated.Enchantments, generated.Weapons, generated.Dungeons, generated.Party];
            Assert.Equal(6, ContentFiles.FileNames.Count);
            Assert.Equal(ContentFiles.FileNames.Select(name => $"[Data] Wrote {Path.Combine(target, name)}"), report);
            for (int i = 0; i < texts.Length; i++)
            {
                Assert.NotNull(texts[i]);
                Assert.Equal(System.Text.Encoding.UTF8.GetBytes(texts[i]!), File.ReadAllBytes(Path.Combine(target, ContentFiles.FileNames[i])));
            }
            Assert.Equal(ContentFiles.FileNames.Order(StringComparer.Ordinal),
                Directory.GetFiles(target).Select(Path.GetFileName).Order(StringComparer.Ordinal));

            var load = ContentFiles.Load(DataFiles.Read(target));
            Assert.Null(load.Error);
            Assert.Empty(load.Notices);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void TheArgumentsParse()
    {
        // DistanceRPG [--write-data <folder>] [--new-campaign], in either order.
        // Pure: the arguments in, the options out, and what was not understood as
        // warnings for the log.
        Assert.Equal("--new-campaign", Program.NewCampaignOption);
        var warnings = new List<string>();
        LaunchOptions Parsed(params string[] args) => Program.Parse(args, warnings);

        Assert.Equal(new LaunchOptions(null, false), Parsed());
        Assert.Equal(new LaunchOptions(null, true), Parsed("--new-campaign"));
        Assert.Equal(new LaunchOptions("out", false), Parsed("--write-data", "out"));
        Assert.Equal(new LaunchOptions("out", true), Parsed("--write-data", "out", "--new-campaign"));
        Assert.Equal(new LaunchOptions("out", true), Parsed("--new-campaign", "--write-data", "out"));
        Assert.Empty(warnings);

        // An argument the game does not know is ignored, with a warning naming it,
        // and everything else on the line still counts.
        Assert.Equal(new LaunchOptions(null, true), Parsed("--new-campain", "--new-campaign"));
        Assert.Contains("'--new-campain'", Assert.Single(warnings), StringComparison.Ordinal);
        warnings.Clear();

        // --write-data that names no folder names none, and says so.
        Assert.Equal(new LaunchOptions(null, true), Parsed("--write-data", "--new-campaign"));
        Assert.Contains("names no folder", Assert.Single(warnings), StringComparison.Ordinal);
        warnings.Clear();
        Assert.Equal(new LaunchOptions(null, false), Parsed("--write-data"));
        Assert.Single(warnings);

        // No warnings list is fine too, and the same line is the same options.
        Assert.Equal(Program.Parse(["--new-campaign"]), Program.Parse(["--new-campaign"]));

        // And the game is built with what was parsed: Main hands the options to it.
        var built = typeof(DistanceRpgGame).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, [typeof(LaunchOptions)])!;
        Assert.Contains("Program.Main", Callers(built));
        Assert.Contains("Program.Main", Callers(typeof(Program).GetMethod(nameof(Program.Parse), BindingFlags.Static | BindingFlags.NonPublic)!));
    }

    [Fact]
    public void TheCampaignIsSavedAtTheFramesEnd_OnANewCampaign_AndOnQuit()
    {
        // 1f: a save is written at the end of the frame a run's end asked for it
        // in — never inside the turn system's raise that ended the run — when NEW
        // CAMPAIGN is confirmed, and on quitting, which every close arrives at
        // (OnClosing). The campaign a launch plays is decided at load, once. Read
        // off the IL, so a save added anywhere else fails here.
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodBase Method(Type type, string name) => type.GetMethod(name, Any)!;

        Assert.Equal(["DistanceRpgGame.Save"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.Save))));
        Assert.Equal(["DistanceRpgGame.NewCampaign", "DistanceRpgGame.SaveOnQuit", "DistanceRpgGame.WriteRequestedSave"],
            Callers(Method(typeof(DistanceRpgGame), "Save")));
        Assert.Equal(["DistanceRpgGame.OnUpdateFrame"], Callers(Method(typeof(DistanceRpgGame), "WriteRequestedSave")));
        Assert.Equal(["DistanceRpgGame.OnClosing"], Callers(Method(typeof(DistanceRpgGame), "SaveOnQuit")));

        // A run's end only asks: the handler the campaign's RunEnded calls writes nothing.
        var asks = Method(typeof(DistanceRpgGame), "SaveAtTheRunsEnd");
        Assert.DoesNotContain(Instructions(Body(asks)!), i => (i.Op == OpCodes.Call || i.Op == OpCodes.Callvirt) && Resolve(asks, i.Token) is { } target
            && (target.DeclaringType == typeof(CampaignBoot) || target.DeclaringType == typeof(SaveStore)));

        // The disk is touched through the store alone, and the store only by the boot and the save.
        Assert.Equal(["CampaignBoot.Save"], Callers(Method(typeof(SaveStore), nameof(SaveStore.Write))));
        Assert.Equal(["CampaignBoot.SetAside"], Callers(Method(typeof(SaveStore), nameof(SaveStore.SetAside))));
        Assert.Equal(["CampaignBoot.BootOverride"], Callers(Method(typeof(SaveStore), nameof(SaveStore.Empty))));
        Assert.Equal(["CampaignBoot.StoreFor"], Callers(typeof(SaveStore).GetConstructor([typeof(string)])!));
        Assert.Equal(["DistanceRpgGame.BootCampaign"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.Boot))));
        Assert.Equal(["DistanceRpgGame.OnLoad"], Callers(Method(typeof(DistanceRpgGame), "BootCampaign")));
    }
}
