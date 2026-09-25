using System.Reflection;
using System.Reflection.Emit;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The game's startup, as claims about code (PHASE5-040; 1e; the content
/// rework, 1g): the data files are read once, when the game loads — never on an
/// update path, never hot-reloaded — by the game alone, from the folder beside
/// the executable, which nothing else in the game reads, over the copies the
/// build carries, which only the edge reads; nothing before the window reads the
/// content, and the built-in content loads only when something asks for it; and
/// the command that writes the data files writes the built-in copies. Read off
/// the game assembly's IL, so a second caller added anywhere fails here.
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
        // folder is read in one place: the game's LoadData, through the edge that
        // lays the files over the copies the build carries (ShippedData.Load).
        // ContentFiles.Load's one other caller is the load of the built-in copies
        // alone — the content GameContent.Current opens on.
        var use = typeof(GameContent).GetMethod(nameof(GameContent.Use))!;
        var shippedLoad = typeof(ShippedData).GetMethod(nameof(ShippedData.Load))!;
        var load = typeof(ContentFiles).GetMethod(nameof(ContentFiles.Load))!;
        var baseDirectory = typeof(AppContext).GetProperty(nameof(AppContext.BaseDirectory))!.GetMethod!;
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(use));
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(shippedLoad));
        Assert.Equal(["ShippedData.Load", "ShippedData.LoadBuiltIn"], Callers(load));
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(baseDirectory));

        // And LoadData runs from the game's OnLoad alone: once, at startup, never
        // on an update path.
        var loadData = typeof(DistanceRpgGame).GetMethod("LoadData", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(["DistanceRpgGame.OnLoad"], Callers(loadData));

        // The loader of the content Current opens on is handed in by the edge's
        // module initializer alone — as the game's assembly loads — and once: a
        // second hand-over is refused, and the loader there is left as it was.
        var openOn = typeof(GameContent).GetMethod(nameof(GameContent.OpenOn), Declared)!;
        var provide = typeof(ShippedData).GetMethod(nameof(ShippedData.Provide), Declared)!;
        Assert.Equal(["ShippedData.Provide"], Callers(openOn));
        Assert.NotNull(provide.GetCustomAttribute<System.Runtime.CompilerServices.ModuleInitializerAttribute>());
        var opened = typeof(GameContent).GetField("_builtIn", BindingFlags.NonPublic | BindingFlags.Static)!;
        var before = opened.GetValue(null);
        Assert.NotNull(before);
        Assert.Throws<InvalidOperationException>(() => GameContent.OpenOn(() => throw new Xunit.Sdk.XunitException("a second loader of the built-in content was run")));
        Assert.Same(before, opened.GetValue(null));
        Assert.Same(ShippedData.Content, ((Lazy<GameContent>)before!).Value);

        // What the build carries is read by the edge alone, and only by ShippedData.
        var readers = Calls(Game)
            .Where(c => c.Target.DeclaringType == typeof(Assembly) && c.Target.Name.StartsWith("GetManifestResource", StringComparison.Ordinal))
            .Select(c => Root(c.From.DeclaringType)!.Name)
            .Distinct()
            .ToArray();
        Assert.Equal([nameof(ShippedData)], readers);
    }

    [Fact]
    public void NothingBeforeTheWindowReadsTheContent()
    {
        // The game makes the loaded content current in LoadData, when the window
        // loads, before anything reads it; until then GameContent.Current would
        // open on the built-in content, and forcing that load before the window —
        // with a broken built-in copy, say — would be a crash, not a screen. So
        // nothing reachable from Program.Main or the game's constructors, type
        // initializers and delegates made along the way included, calls
        // GameContent.Current.
        var current = typeof(GameContent).GetProperty(nameof(GameContent.Current))!.GetMethod!;
        var main = typeof(Program).GetMethod("Main", Declared)!;
        var roots = typeof(DistanceRpgGame).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Cast<MethodBase>().Prepend(main).ToArray();
        var reached = Reachable(roots);
        Assert.Equal([], ReadersOf(current, reached));

        // The walk is not reading nothing: it reaches the command that writes the
        // data files, the edge that reads what the build carries, and the game's
        // constructor, and it does find a reader of the content where there is
        // one — the game's own OnLoad, once the window is up.
        Assert.Contains(reached, m => Same(m, typeof(Program).GetMethod(nameof(Program.WriteData), Declared)!));
        Assert.Contains(reached, m => Same(m, typeof(ShippedData).GetProperty(nameof(ShippedData.Texts))!.GetMethod!));
        Assert.NotEmpty(ReadersOf(current, Reachable(typeof(DistanceRpgGame).GetMethod("OnLoad", Declared)!)));
    }

    [Fact]
    public void TheBuiltInContentLoadsOnlyWhenAskedFor()
    {
        // The content rework, 1g: ShippedData and GameContent hold no eager static
        // state. Neither type initializer calls a method of the game's — a Lazy
        // handed a method to run later is a delegate made, not a call — so a broken
        // built-in copy can never surface as a type that failed to initialize.
        var game = typeof(ShippedData).Module;
        foreach (var type in new[] { typeof(ShippedData), typeof(GameContent) })
            if (type.TypeInitializer is { } initializer)
                Assert.Equal([], CallsIn(initializer).Where(target => target.Module == game).Select(target => $"{target.DeclaringType?.Name}.{target.Name}"));
        var deferred = Edges(Game).Where(e => IsTypeInitializer(e.From) && e.From.DeclaringType == typeof(ShippedData) && IsDelegate(e.Op))
            .Select(e => e.Target.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["LoadBuiltIn", "ReadBuiltInTexts"], deferred);   // the deferrals are there: the lazy holders' loaders

        // The module initializer hands GameContent the loader and does nothing else
        // of the game's: it reads nothing, and forces neither holder.
        var provide = typeof(ShippedData).GetMethod(nameof(ShippedData.Provide), Declared)!;
        var openOn = typeof(GameContent).GetMethod(nameof(GameContent.OpenOn), Declared)!;
        Assert.Equal([openOn.Name], CallsIn(provide).Where(target => target.Module == game).Select(target => target.Name));

        // The built-in content alone is the suite's door: nothing in the game asks
        // for it.
        var content = typeof(ShippedData).GetProperty(nameof(ShippedData.Content), Declared)!.GetMethod!;
        Assert.Empty(Callers(content));

        // And the game's own load never forces it: nothing reachable from LoadData
        // or from ShippedData.Load calls the built-in-only load or reads Current —
        // the load builds from its own locals, so it cannot re-enter the content it
        // is building, and a broken built-in copy reaches the error screen.
        var loadBuiltIn = typeof(ShippedData).GetMethod("LoadBuiltIn", Declared)!;
        var current = typeof(GameContent).GetProperty(nameof(GameContent.Current))!.GetMethod!;
        var fromLoad = Reachable(typeof(DistanceRpgGame).GetMethod("LoadData", Declared)!, typeof(ShippedData).GetMethod(nameof(ShippedData.Load))!);
        Assert.Contains(fromLoad, m => Same(m, typeof(ContentFiles).GetMethod(nameof(ContentFiles.Load))!));
        Assert.Equal([], ReadersOf(loadBuiltIn, fromLoad));
        Assert.Equal([], ReadersOf(current, fromLoad));
    }

    /// <summary>Every method of <paramref name="reached"/> that calls <paramref name="target"/>, as <c>Type.Method</c>.</summary>
    private static string[] ReadersOf(MethodBase target, IEnumerable<MethodBase> reached)
        => reached.Where(method => CallsIn(method).Any(called => Same(called, target)))
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void TheStartupNoticesGoToTheFirstSceneAlone()
    {
        // What the data files raised reaches the screen through the first scene
        // the game opens on (1h, 1i). That is the screen between runs, a run that
        // a dev override boots straight into, or — from Phase 5.8 — a run a save
        // held, resumed in the dungeon (ResumeRun). The load holds the notices.
        // Each of the methods that show a scene takes them and passes them
        // straight into what that scene is built with, where the last argument is
        // the notices. Nothing else takes them.
        var hold = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Hold))!;
        var take = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Take))!;
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(hold));
        Assert.Equal(["DistanceRpgGame.BeginRun", "DistanceRpgGame.ResumeRun", "DistanceRpgGame.ShowBetweenRuns"], Callers(take));
        foreach (var (shows, scene) in new[] { ("ShowBetweenRuns", typeof(BetweenRunsScene)), ("BeginRun", typeof(DungeonScene)) })
        {
            var built = BuiltWithTheTake(shows, take);
            Assert.True(built is not null, $"DistanceRpgGame.{shows} does not hand what it takes straight to a constructor");
            Assert.Equal(scene, built.DeclaringType);
            Assert.Equal("notices", built.GetParameters()[^1].Name);
        }

        // A resumed run's notices ride with the save's view (ResumeView), which the
        // scene's resuming constructor takes whole.
        var resumeView = BuiltWithTheTake("ResumeRun", take);
        Assert.True(resumeView is not null, "DistanceRpgGame.ResumeRun does not hand what it takes straight to a constructor");
        Assert.Equal(typeof(ResumeView), resumeView.DeclaringType);
        Assert.Equal("Notices", resumeView.GetParameters()[^1].Name);
        Assert.Contains(typeof(DungeonScene).GetConstructors(),
            ctor => ctor.GetParameters() is [.., { ParameterType: var last }] && last == typeof(ResumeView));
        var resumeRun = typeof(DistanceRpgGame).GetMethod("ResumeRun", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Contains(Calls(Game), c => Same(c.From, resumeRun) && c.Op == OpCodes.Newobj
            && c.Target.DeclaringType == typeof(DungeonScene) && c.Target.GetParameters()[^1].ParameterType == typeof(ResumeView));

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

    [Fact]
    public void TheScreenBetweenRunsIsToldWhichNoticesAreTheSavesWord()
    {
        // S5's review: the save's word on the campaign — resumed or begun, the
        // numbers changed, a file set aside and how to try it again — leads the
        // notices, and the screen between runs shows it whole, capping only what
        // the data files raised after it. So does a floor a saved run resumes on
        // (S8's review). The holder counts what was led. The two methods that open
        // those screens read that count, and nothing else does. Each reads it
        // before the take that hands the notices over.
        var notices = new StartupNotices();
        notices.Hold(["data one", "data two"]);
        notices.Lead(["a save that failed"]);
        notices.Lead(["resumed", "the numbers changed"]);
        Assert.Equal(3, notices.Leading);
        Assert.Equal(["resumed", "the numbers changed", "a save that failed", "data one", "data two"], notices.Take());
        Assert.Equal(0, notices.Leading);
        Assert.Empty(notices.Take());

        // Held after a lead, the data files' notices still follow it.
        notices.Lead(["resumed"]);
        notices.Hold(["data"]);
        Assert.Equal(1, notices.Leading);
        Assert.Equal(["resumed", "data"], notices.Take());

        var leading = typeof(StartupNotices).GetProperty(nameof(StartupNotices.Leading))!.GetMethod!;
        Assert.Equal(["DistanceRpgGame.ResumeRun", "DistanceRpgGame.ShowBetweenRuns"], Callers(leading));
        var built = typeof(BetweenRunsScene).GetConstructors().Single();
        Assert.Equal(["game", "campaign", "host", "shownWhole", "notices"], built.GetParameters().Select(p => p.Name));
        Assert.Equal(["View", "Leading", "Notices"], typeof(ResumeView).GetConstructors().Single().GetParameters().Select(p => p.Name));

        // Both read the count before the take, which empties the holder: read after
        // it, the count would be none, and the save's word would be capped.
        var take = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Take))!;
        foreach (string opens in new[] { "ShowBetweenRuns", "ResumeRun" })
        {
            var method = typeof(DistanceRpgGame).GetMethod(opens, BindingFlags.NonPublic | BindingFlags.Instance)!;
            var calls = Instructions(Body(method)!).Select(i => i.Token != 0 ? Resolve(method, i.Token) : null).ToList();
            int counted = calls.FindIndex(called => called is not null && Same(called, leading));
            int taken = calls.FindIndex(called => called is not null && Same(called, take));
            Assert.True(counted >= 0 && counted < taken, $"DistanceRpgGame.{opens} does not read the count before the take");
        }
    }

    [Fact]
    public void AFailedSaveIsSaidOnTheFloorAtOnce_AndOnTheNextScreenOnce()
    {
        // The audit's pass (1f): a write that fails is said at once over the floor
        // being played — not only on the screen between runs once the run is over —
        // and on that next screen once, however many times it failed, and only until
        // a later save goes through. The holder leads a notice once...
        var notices = new StartupNotices();
        notices.Hold(["data"]);
        string held = CampaignBoot.WriteFailed("another program has it open");
        notices.Lead([held]);
        notices.Lead([held]);   // every turn's start and every arrival fail with the same words
        notices.Lead([held, held]);
        Assert.Equal(1, notices.Leading);
        string full = CampaignBoot.WriteFailed("the disk is full");
        notices.Lead([full]);
        Assert.Equal(2, notices.Leading);
        Assert.Equal([full, held, "data"], notices.Take());

        // ...and takes it back once a save goes through, when it is no longer true;
        // what else leads is left as it was.
        notices.Hold(["data"]);
        notices.Lead([held]);
        notices.Lead(["resumed"]);
        notices.Withdraw(held);
        notices.Withdraw("never led");
        Assert.Equal(1, notices.Leading);
        Assert.Equal(["resumed", "data"], notices.Take());

        // Read off the IL: the game's one Save says a failure to the scene playing
        // the run and leads it for the next screen, and takes back what a later save
        // made untrue; the scene hands it to its HUD, which lays it among the rows.
        var save = typeof(DistanceRpgGame).GetMethod("Save", Declared)!;
        var saveFailed = typeof(DungeonScene).GetMethod(nameof(DungeonScene.SaveFailed), Declared)!;
        var lead = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Lead))!;
        Assert.Equal(["DistanceRpgGame.Save"], Callers(saveFailed));
        Assert.Equal(["DungeonScene.SaveFailed"], Callers(typeof(DungeonHud).GetMethod(nameof(DungeonHud.ShowSaveFailed))!));
        Assert.Equal(["DistanceRpgGame.Save"], Callers(typeof(StartupNotices).GetMethod(nameof(StartupNotices.Withdraw))!));
        Assert.Equal(["DistanceRpgGame.BootCampaign", "DistanceRpgGame.Save"], Callers(lead));
        Assert.Equal(1, Mentions(save, lead));
        Assert.Equal(1, Mentions(save, typeof(DistanceRpgGame).GetMethod("PlayingScene", Declared)!));
        Assert.Equal(1, Mentions(save, saveFailed));
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
    public void TheWriteDataCommandWritesTheBuiltInCopies()
    {
        // DistanceRPG --write-data <folder> writes the four copies the game carries
        // built in — the tuning and the three content files — into the folder, no
        // window opened: each byte for byte as the build carries it (UTF-8, no byte
        // order mark, "\n" line ends), each reported by its full path, and nothing
        // else in the folder — the enchantment catalogue and the starting party are
        // code, and have no file. The command's whole body, run into a temp folder of
        // its own, so no real folder is touched; a game started beside what it wrote
        // plays exactly the built-in content, saying nothing.
        var folder = Directory.CreateTempSubdirectory("distancerpg-");
        try
        {
            string target = Path.Combine(folder.FullName, "data");
            var report = new List<string>();
            Assert.True(Program.WriteData(target, report.Add));

            var builtIn = ShippedData.Texts;
            string?[] texts = [builtIn.Tuning, builtIn.Restricted, builtIn.Weapons, builtIn.Dungeons];
            Assert.Equal(4, ContentFiles.FileNames.Count);
            Assert.Equal(ContentFiles.FileNames.Select(name => $"[Data] Wrote {Path.Combine(target, name)}"), report);
            for (int i = 0; i < texts.Length; i++)
            {
                Assert.NotNull(texts[i]);
                Assert.Equal(System.Text.Encoding.UTF8.GetBytes(texts[i]!), File.ReadAllBytes(Path.Combine(target, ContentFiles.FileNames[i])));
                using var resource = typeof(ShippedData).Assembly.GetManifestResourceStream(ShippedData.ResourcePrefix + ContentFiles.FileNames[i])!;
                using var bytes = new MemoryStream();
                resource.CopyTo(bytes);
                Assert.Equal(bytes.ToArray(), File.ReadAllBytes(Path.Combine(target, ContentFiles.FileNames[i])));
            }
            Assert.Equal(ContentFiles.FileNames.Order(StringComparer.Ordinal),
                Directory.GetFiles(target).Select(Path.GetFileName).Order(StringComparer.Ordinal));

            var load = ShippedData.Load(DataFiles.Read(target));
            Assert.Null(load.Error);
            Assert.Empty(load.Notices);
            Assert.Empty(load.TuningOverrides);

            // It refuses the source Data folder — a folder named Data beside the game
            // project — which holds the master copies: the last build's copies are
            // never written over edits not built yet. Nothing is written there, and
            // the report says why.
            string project = Path.Combine(folder.FullName, "GameEngine.DistanceRPG");
            string source = Path.Combine(project, "Data");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(project, "GameEngine.DistanceRPG.csproj"), "<Project />");
            report.Clear();
            Assert.True(Program.IsSourceDataFolder(source));
            Assert.True(Program.IsSourceDataFolder(source + Path.DirectorySeparatorChar));
            Assert.False(Program.WriteData(source, report.Add));
            Assert.Empty(Directory.GetFiles(source));
            Assert.Contains("is the source Data folder", Assert.Single(report), StringComparison.Ordinal);

            // A Data folder with no game project beside it is any other folder.
            Assert.False(Program.IsSourceDataFolder(Path.Combine(folder.FullName, "Data")));
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
        // CAMPAIGN is confirmed, or --new-campaign begins a campaign at launch
        // (BootCampaign, S5's review), and on quitting, which every close arrives
        // at (OnClosing): through the quit's own door, which settles the enemies'
        // turn first when a run is in progress and then saves (CampaignBoot.SaveOnQuit,
        // Phase 5.8) — between runs as well since the audit's pass, so a quit that
        // cannot save leaves its marker for the next launch however it came. The
        // campaign a launch plays is decided at load, once. Read off the IL, so a
        // save added anywhere else fails here.
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodBase Method(Type type, string name) => type.GetMethod(name, Any)!;

        Assert.Equal(["CampaignBoot.SaveOnQuit", "DistanceRpgGame.Save"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.Save))));
        Assert.Equal(["DistanceRpgGame.SaveOnQuit"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.SaveOnQuit))));
        Assert.Equal(["DistanceRpgGame.BootCampaign", "DistanceRpgGame.NewCampaign", "DistanceRpgGame.WriteRequestedSave"],
            Callers(Method(typeof(DistanceRpgGame), "Save")));

        // The unsaved-quit marker is written by the quit's own door alone, and read
        // and cleared by the load alone — and by --new-campaign, which sets the
        // campaign it spoke of aside and says nothing of it.
        Assert.Equal(["CampaignBoot.MarkUnsaved"], Callers(Method(typeof(SaveStore), nameof(SaveStore.MarkUnsaved))));
        Assert.Equal(["CampaignBoot.SaveOnQuit"], Callers(Method(typeof(CampaignBoot), "MarkUnsaved")));
        Assert.Equal(["CampaignBoot.TakeUnsaved"], Callers(Method(typeof(SaveStore), nameof(SaveStore.ReadUnsaved))));
        Assert.Equal(["CampaignBoot.TakeUnsaved"], Callers(Method(typeof(SaveStore), nameof(SaveStore.ClearUnsaved))));
        Assert.Equal(["CampaignBoot.Abandon", "CampaignBoot.Load"], Callers(Method(typeof(CampaignBoot), "TakeUnsaved")));
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
        Assert.Equal(["CampaignBoot.StoreFor", "SaveSlots.Store"], Callers(typeof(SaveStore).GetConstructor([typeof(string)])!));
        Assert.Equal(["DistanceRpgGame.BootCampaign"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.Load))));
        Assert.Equal(["DistanceRpgGame.BootCampaign"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.BootOverride))));
        Assert.Equal(["DistanceRpgGame.BootCampaign"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.Abandon))));
        Assert.Equal(["DistanceRpgGame.OnLoad"], Callers(Method(typeof(DistanceRpgGame), "BootCampaign")));
    }

    private const BindingFlags Declared = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Every method-typed operand in <paramref name="method"/>'s body that names <paramref name="target"/> — a call, or a delegate made of it (<c>ldftn</c>).</summary>
    private static int Mentions(MethodBase method, MethodBase target)
        => Instructions(Body(method)!).Count(i => i.Token != 0 && Resolve(method, i.Token) is { } named && Same(named, target));

    [Fact]
    public void OnlyTheSceneStepDrivesTheTurnSystem()
    {
        // 1c, S8: the turn system runs on the scene's fixed logic clock. Outside
        // Logic/ it is stepped from one place — DungeonScene.StepLogic, one
        // SaveGame.LogicStepSeconds step with the enemy-phase sighting the scene
        // feeds back — and never on a frame's own time: the scene's Update steps the
        // clock (LogicClock) and calls StepLogic, never TurnSystem.Update itself.
        var update = typeof(TurnSystem).GetMethod(nameof(TurnSystem.Update))!;
        var stepLogic = typeof(DungeonScene).GetMethod(nameof(DungeonScene.StepLogic), Declared)!;
        var sceneUpdate = typeof(DungeonScene).GetMethod(nameof(DungeonScene.Update), Declared)!;
        var outsideLogic = Calls(Game)
            .Where(c => Same(c.Target, update) && Root(c.From.DeclaringType)!.Namespace != typeof(TurnSystem).Namespace)
            .Select(c => $"{Root(c.From.DeclaringType)!.Name}.{c.From.Name}")
            .Distinct()
            .ToArray();
        Assert.Equal(["DungeonScene.StepLogic"], outsideLogic);
        Assert.Equal(0, Mentions(sceneUpdate, update));
        Assert.Equal(1, Mentions(sceneUpdate, stepLogic));
        Assert.Equal(1, Mentions(sceneUpdate, typeof(LogicClock).GetMethod(nameof(LogicClock.Steps))!));

        // With the one step: the float StepLogic hands Update is LogicStepSeconds.
        var il = Body(stepLogic)!;
        var instructions = InstructionsAt(il).ToList();
        int call = instructions.FindIndex(i => i.Token != 0 && Resolve(stepLogic, i.Token) is { } target && Same(target, update));
        Assert.True(call > 0, "StepLogic does not call TurnSystem.Update");
        Assert.Equal(OpCodes.Ldc_R4, instructions[call - 1].Op);
        Assert.Equal(SaveGame.LogicStepSeconds, BitConverter.ToSingle(il, instructions[call - 1].At));

        // And the step a quit settles with is that same step (S7's review, check 3):
        // the game hands DungeonScene.StepLogic to the quit's own door, never Logic's
        // default step, which feeds back no sighting.
        var quit = typeof(DistanceRpgGame).GetMethod("SaveOnQuit", Declared)!;
        Assert.Equal(1, Mentions(quit, stepLogic));
        Assert.Equal(1, Mentions(quit, typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.SaveOnQuit), Declared)!));
        Assert.Equal(0, Mentions(quit, typeof(SaveGame).GetMethod(nameof(SaveGame.SettleToSafePoint))!));
        Assert.Equal(["DistanceRpgGame.SaveOnQuit"], Callers(typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.SaveOnQuit), Declared)!));
    }

    [Fact]
    public void TheSceneRollsTheVisitsOwnDie()
    {
        // USER DECISION 1 (ruled by the user, 2026-09-25: seeded by the visit and
        // saved), S7's review's check 1: every floor the scene opens rolls the
        // scene's d20, and the scene's d20 is the visit's own (DungeonState.RollD20),
        // whose position a save keeps — never Random.Shared, which would make a
        // reload a reroll.
        var sceneDie = typeof(DungeonScene).GetMethod("RollD20", Declared)!;
        var visitDie = typeof(DungeonState).GetMethod(nameof(DungeonState.RollD20))!;
        Assert.Equal(1, Mentions(sceneDie, visitDie));
        Assert.DoesNotContain(Instructions(Body(sceneDie)!), i => i.Token != 0 && Resolve(sceneDie, i.Token)?.DeclaringType == typeof(Random));
        Assert.Contains("DungeonScene.RollD20", Callers(visitDie));

        // Both OpenTurns the scene makes — the first floor's and every stairs
        // arrival's — hand it that die.
        var open = typeof(DungeonState).GetMethod(nameof(DungeonState.OpenTurns))!;
        foreach (string arrival in new[] { "Initialize", "TakeStairs" })
        {
            var method = typeof(DungeonScene).GetMethod(arrival, Declared)!;
            Assert.Equal(1, Mentions(method, open));
            Assert.Equal(1, Mentions(method, sceneDie));
        }
        Assert.Equal(["DungeonScene.Initialize", "DungeonScene.TakeStairs"], Callers(open).Where(caller => caller.StartsWith("DungeonScene.", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheRunAutosavesAtItsEntryItsTurnsAndItsArrivals()
    {
        // 1f: the run in progress is saved at every safe point a player would want
        // to come back to — when it is entered (BeginRun), at every player turn's
        // start and after every floor arrival, the first floor's included (the
        // scene asks its host, IRunHost.RequestSave) — and always only asked for:
        // the host writes it at the frame's end (WriteRequestedSave), never inside
        // the turn system's raise that asked. Read off the IL.
        var asks = typeof(IRunHost).GetMethod(nameof(IRunHost.RequestSave))!;
        Assert.Equal(["DungeonScene.Initialize", "DungeonScene.OnPlayerTurnStarted", "DungeonScene.TakeStairs"], Callers(asks));
        var wired = typeof(DungeonScene).GetMethod("WireTurnSystem", Declared)!;
        Assert.Equal(1, Mentions(wired, typeof(DungeonScene).GetMethod("OnPlayerTurnStarted", Declared)!));

        var game = typeof(DistanceRpgGame);
        var request = game.GetMethod(nameof(DistanceRpgGame.RequestSave))!;
        Assert.Equal(["DistanceRpgGame.BeginRun"], Callers(request));
        foreach (var asking in new[] { request, game.GetMethod("BeginRun", Declared)! })
            Assert.DoesNotContain(Instructions(Body(asking)!), i => i.Token != 0 && Resolve(asking, i.Token) is { } target
                && (target.DeclaringType == typeof(CampaignBoot) || target.DeclaringType == typeof(SaveStore) || target.Name == "Save"));

        // The pause menu's 2 closes the window, and the close is where the quit saves.
        // (The call binds to the window's original declaration of Close, which
        // reflection on the game's type would hand back as its override.)
        var close = typeof(GameEngine.Core.Game).GetMethod("Close", Type.EmptyTypes)!.GetBaseDefinition();
        Assert.Contains("DungeonScene.NumberKey", Callers(close));
    }
}
