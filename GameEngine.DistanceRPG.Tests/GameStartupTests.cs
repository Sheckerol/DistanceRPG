using System.Reflection;
using System.Reflection.Emit;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;
using static GameEngine.DistanceRPG.Tests.TestSlots;

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
        // the game opens on (1h, 1i). That is the title (the user's ruling of
        // 2026-09-25 on the save model, R2(a); ShowTitle), and else the screen
        // between runs — a --new-campaign launch's, or a dev override's that names
        // only a seed — or a run that a dev override boots straight into; and once
        // the title has taken them, a campaign loaded from it opens on the screen
        // between runs or on a run a save held, resumed in the dungeon (ResumeRun),
        // with the load's word. The load holds the notices. Each of the methods that
        // show a scene takes them and passes them straight into what that scene is
        // built with, where the last argument is the notices. Nothing else takes them.
        var hold = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Hold))!;
        var take = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Take))!;
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(hold));
        Assert.Equal(["DistanceRpgGame.BeginRun", "DistanceRpgGame.ResumeRun", "DistanceRpgGame.ShowBetweenRuns", "DistanceRpgGame.ShowTitle"], Callers(take));
        foreach (var (shows, scene) in new[] { ("ShowBetweenRuns", typeof(BetweenRunsScene)), ("BeginRun", typeof(DungeonScene)), ("ShowTitle", typeof(TitleScene)) })
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
        // screen between runs and the title put the rows under their titles. A
        // dungeon scene's HUD puts them over the first floor; HudTextTests builds
        // that scene and reads its rows.
        var rows = typeof(NoticeText).GetMethod(nameof(NoticeText.Rows))!;
        var showNotices = typeof(DungeonHud).GetMethod(nameof(DungeonHud.ShowNotices))!;
        var noticeRows = typeof(BetweenRunsScene).GetProperty("NoticeRows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetMethod!;
        var titleRows = typeof(TitleScene).GetProperty("NoticeRows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetMethod!;
        var drawNotices = typeof(DungeonHud).GetMethod("DrawNotices", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Contains("BetweenRunsScene..ctor", Callers(rows));
        Assert.Contains("TitleScene..ctor", Callers(rows));
        Assert.Contains("DungeonScene..ctor", Callers(showNotices));
        Assert.Contains("BetweenRunsHud.Render", Callers(noticeRows));
        Assert.Contains("TitleHud.Render", Callers(titleRows));
        Assert.Equal(["DungeonHud.Render"], Callers(drawNotices));

        // The title is the first scene's door (S6): the launch shows it unless a dev
        // override or --new-campaign skips it, a load that brings nothing back shows
        // it again, a NEW CAMPAIGN that could not save into its slot shows it again
        // (the user's ruling on the save slots, R5 — --new-campaign's among them,
        // which then opens on it), and leaving play shows it (S7's QUIT TO TITLE, the
        // campaign's slot written first); nothing else does. (A title asked for
        // mid-fade is shown by a lambda of ShowTitle's own, once the fade is done.)
        var showTitle = typeof(DistanceRpgGame).GetMethod("ShowTitle", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(["DistanceRpgGame.BeginNewCampaign", "DistanceRpgGame.BootCampaign", "DistanceRpgGame.LoadSlot", "DistanceRpgGame.QuitToTitle"],
            Callers(showTitle).Where(caller => !caller.Contains('<')));
        var bootCampaign = typeof(DistanceRpgGame).GetMethod("BootCampaign", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(1, Mentions(bootCampaign, showTitle));
        Assert.Equal(["TitleScene..ctor"], Callers(typeof(TitleMenu).GetConstructor([typeof(SaveListing)])!));
        Assert.Equal(["DistanceRpgGame.ShowTitle"], Callers(typeof(TitleScene).GetConstructors().Single()));
    }

    [Fact]
    public void TheScreenBetweenRunsIsToldWhichNoticesAreTheSavesWord()
    {
        // S5's review: the save's word on the campaign — resumed or begun, the
        // numbers changed, a file set aside and how to try it again — leads the
        // notices, and the screen between runs shows it whole, capping only what
        // the data files raised after it. So does a floor a saved run resumes on
        // (S8's review), and so does the title (S6), whose word is what a load that
        // brought nothing back set aside, a slot a quit left unsaved, a file the
        // listing could not read. The holder counts what was led. The three methods
        // that open those screens read that count, and nothing else does. Each
        // reads it before the take that hands the notices over.
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
        Assert.Equal(["DistanceRpgGame.ResumeRun", "DistanceRpgGame.ShowBetweenRuns", "DistanceRpgGame.ShowTitle"], Callers(leading));
        var built = typeof(BetweenRunsScene).GetConstructors().Single();
        Assert.Equal(["game", "campaign", "host", "shownWhole", "notices"], built.GetParameters().Select(p => p.Name));
        Assert.Equal(["game", "host", "listing", "shownWhole", "notices"], typeof(TitleScene).GetConstructors().Single().GetParameters().Select(p => p.Name));
        Assert.Equal(["View", "Leading", "Notices"], typeof(ResumeView).GetConstructors().Single().GetParameters().Select(p => p.Name));

        // Each reads the count before the take, which empties the holder: read after
        // it, the count would be none, and the save's word would be capped.
        var take = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Take))!;
        foreach (string opens in new[] { "ShowBetweenRuns", "ResumeRun", "ShowTitle" })
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
        string held = CampaignBoot.NotSaved(Named(SaveSlots.OldAutosave), "another program has it open");
        notices.Lead([held]);
        notices.Lead([held]);   // every turn's start and every arrival fail with the same words
        notices.Lead([held, held]);
        Assert.Equal(1, notices.Leading);
        string full = CampaignBoot.NotSaved(Named(SaveSlots.OldAutosave), "the disk is full");
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
        // The taking back is one helper's, TakeBackFailedSaves, which Save calls and
        // the quit's own door is handed too (S7's review: a quit whose save went
        // through takes it back from the title; SaveMenuTests pins that door).
        var save = typeof(DistanceRpgGame).GetMethod("Save", Declared)!;
        var saveFailed = typeof(DungeonScene).GetMethod(nameof(DungeonScene.SaveFailed), Declared)!;
        var lead = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Lead))!;
        var takeBack = typeof(DistanceRpgGame).GetMethod("TakeBackFailedSaves", Declared)!;
        Assert.Equal(["DistanceRpgGame.Save"], Callers(saveFailed));
        Assert.Equal(["DungeonScene.SaveFailed"], Callers(typeof(DungeonHud).GetMethod(nameof(DungeonHud.ShowSaveFailed))!));
        Assert.Equal(["DistanceRpgGame.TakeBackFailedSaves"], Callers(typeof(StartupNotices).GetMethod(nameof(StartupNotices.Withdraw))!));
        Assert.Equal(["DistanceRpgGame.Save"], Callers(takeBack));
        Assert.Equal(1, Mentions(save, takeBack));
        Assert.Equal(["DistanceRpgGame.BeginNewCampaign", "DistanceRpgGame.BootCampaign", "DistanceRpgGame.LoadSlot", "DistanceRpgGame.Save", "DistanceRpgGame.ShowTitle"],
            Callers(lead));
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
        // CAMPAIGN begins one, on the title or by --new-campaign at launch
        // (BeginNewCampaign, S6), by hand — SAVE GAME, into the campaign's own slot,
        // through the frame's end's own door (the user's ruling on the save slots,
        // R5; S7's CampaignBoot.SaveTo, into another slot, is gone) — and on
        // leaving play, by the window's close (OnClosing) or the menus' QUIT
        // TO TITLE (S7): through the quit's own door, which settles the enemies'
        // turn first when a run is in progress and then saves (CampaignBoot.SaveOnQuit,
        // Phase 5.8) — between runs as well since the audit's pass, so a quit that
        // cannot save leaves its marker for the title however it came. The
        // campaign in play is decided by the title — a slot loaded, or a new one
        // begun — or by a dev override at launch. Read off the IL, so a save added
        // anywhere else fails here. NEW CAMPAIGN's first save is written straight
        // through CampaignBoot.Save, before the campaign is played (the user's ruling
        // on the save slots, R5), so the game's own Save is the frame's end's alone.
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodBase Method(Type type, string name) => type.GetMethod(name, Any)!;

        Assert.Equal(["CampaignBoot.SaveOnQuit", "DistanceRpgGame.BeginNewCampaign", "DistanceRpgGame.Save"],
            Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.Save))));
        Assert.Equal(["DistanceRpgGame.SaveOnQuit"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.SaveOnQuit))));
        Assert.Equal(["DistanceRpgGame.WriteRequestedSave"], Callers(Method(typeof(DistanceRpgGame), "Save")));

        // The unsaved-quit marker is written by the quit's own door alone, and read
        // and cleared by the title alone — every slot's, before the slots are listed
        // and anything is loaded — and by NEW CAMPAIGN, which sets the campaign it
        // spoke of aside and says nothing of it.
        Assert.Equal(["CampaignBoot.MarkUnsaved"], Callers(Method(typeof(SaveStore), nameof(SaveStore.MarkUnsaved))));
        Assert.Equal(["CampaignBoot.SaveOnQuit"], Callers(Method(typeof(CampaignBoot), "MarkUnsaved")));
        Assert.Equal(["CampaignBoot.TakeUnsaved"], Callers(Method(typeof(SaveStore), nameof(SaveStore.ReadUnsaved))));
        Assert.Equal(["CampaignBoot.TakeUnsaved"], Callers(Method(typeof(SaveStore), nameof(SaveStore.ClearUnsaved))));
        Assert.Equal(["CampaignBoot.Abandon", "CampaignBoot.TakeEveryUnsaved"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.TakeUnsaved))));
        var takeEvery = Method(typeof(CampaignBoot), nameof(CampaignBoot.TakeEveryUnsaved));
        Assert.Equal(["DistanceRpgGame.ShowTitle"], Callers(takeEvery));
        var inTitle = CallsIn(Method(typeof(DistanceRpgGame), "ShowTitle")).ToList();
        int taken = inTitle.FindIndex(called => Same(called, takeEvery));
        Assert.True(taken >= 0 && taken < inTitle.FindIndex(called => Same(called, Method(typeof(SaveListing), nameof(SaveListing.Read)))),
            "ShowTitle lists the slots before it has taken their unsaved-quit markers");
        Assert.Equal(["DistanceRpgGame.OnUpdateFrame"], Callers(Method(typeof(DistanceRpgGame), "WriteRequestedSave")));
        Assert.Equal(["DistanceRpgGame.OnClosing", "DistanceRpgGame.QuitToTitle"], Callers(Method(typeof(DistanceRpgGame), "SaveOnQuit")));

        // A run's end only asks: the handler the campaign's RunEnded calls writes nothing.
        var asks = Method(typeof(DistanceRpgGame), "SaveAtTheRunsEnd");
        Assert.DoesNotContain(Instructions(Body(asks)!), i => (i.Op == OpCodes.Call || i.Op == OpCodes.Callvirt) && Resolve(asks, i.Token) is { } target
            && (target.DeclaringType == typeof(CampaignBoot) || target.DeclaringType == typeof(SaveStore)));

        // The disk is touched through the store alone, and the store only by the boot and the save:
        // a save's write, or — a save by hand of the campaign the slot holds already — its restamp.
        Assert.Equal(["CampaignBoot.Save"], Callers(Method(typeof(SaveStore), nameof(SaveStore.Write))));
        Assert.Equal(["CampaignBoot.Save"], Callers(Method(typeof(SaveStore), nameof(SaveStore.Restamp))));
        Assert.Equal(["CampaignBoot.SetAside"], Callers(Method(typeof(SaveStore), nameof(SaveStore.SetAside))));
        Assert.Equal(["CampaignBoot.BootOverride"], Callers(Method(typeof(SaveStore), nameof(SaveStore.Empty))));
        Assert.Equal(["CampaignBoot.StoreFor", "SaveSlots.Store"], Callers(typeof(SaveStore).GetConstructor([typeof(string)])!));
        // The old autosave's move is OldAutosave's alone (the user's ruling on the
        // save slots, R5): every file it moves, its marker, and what it reads of the
        // folders to choose a slot — called, or handed on as a delegate, by nothing
        // else, but for the one move refusing a name taken there. And a slot's
        // writes are refused by what could not be set aside, or by a move of the old
        // autosave that failed or was left in two halves, and by nothing else.
        string[] MentionedBy(MethodBase target)
            => Edges(Game).Where(edge => Same(edge.Target, target)).Select(edge => Root(edge.From.DeclaringType)!.Name).Distinct().Order(StringComparer.Ordinal).ToArray();
        foreach (var moving in new[] { nameof(SaveStore.MoveInto), nameof(SaveStore.MarkMoving), nameof(SaveStore.ReadMoving), nameof(SaveStore.ClearMoving), nameof(SaveStore.Has),
            nameof(SaveStore.RemoveIfEmpty), "get_" + nameof(SaveStore.WasMoving), "get_" + nameof(SaveStore.FolderIsAFile), "get_" + nameof(SaveStore.FolderExists) })
            Assert.Equal(["OldAutosave"], MentionedBy(Method(typeof(SaveStore), moving)));
        Assert.Equal(["OldAutosave", "SaveStore"], MentionedBy(Method(typeof(SaveStore), nameof(SaveStore.Taken))));
        Assert.Equal(["SaveStore.MoveInto"], Callers(Method(typeof(SaveStore), nameof(SaveStore.Taken))).Where(caller => !caller.StartsWith("OldAutosave.", StringComparison.Ordinal)));
        Assert.Equal(["CampaignBoot.SetAside", "OldAutosave.NotMoved", "OldAutosave.Split"], Callers(Method(typeof(SaveStore), nameof(SaveStore.RefuseWrites))));
        // A slot is made only where its store is had — the save folder's by number,
        // the dev override's scratch — so no store goes about under another's name.
        Assert.Equal(["SaveSlot.Scratch", "SaveSlots.Slot"], Callers(typeof(SaveSlot).GetConstructor([typeof(SaveStore), typeof(int?)])!));
        Assert.Equal(["DistanceRpgGame.LoadSlot"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.Load))));
        Assert.Equal(["DistanceRpgGame.BootCampaign"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.BootOverride))));
        Assert.Equal(["DistanceRpgGame.BeginNewCampaign"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.Abandon))));
        Assert.Equal(["DistanceRpgGame.OnLoad"], Callers(Method(typeof(DistanceRpgGame), "BootCampaign")));

        // NEW CAMPAIGN begins in the slot chosen on the title's list, and
        // --new-campaign in the slot that list starts on (the user's ruling on the
        // save slots, R5): the listing's NewCampaignSlot, read by the list and by the
        // launch alone — the record's own printing aside — the launch reading it
        // before it begins the campaign.
        var newCampaignSlot = typeof(SaveListing).GetProperty(nameof(SaveListing.NewCampaignSlot))!.GetMethod!;
        Assert.Equal(["DistanceRpgGame.BootCampaign", "SaveListing.PrintMembers", "SlotMenu..ctor"], Callers(newCampaignSlot));
        var inBoot = CallsIn(Method(typeof(DistanceRpgGame), "BootCampaign")).ToList();
        int slotRead = inBoot.FindIndex(called => Same(called, newCampaignSlot));
        Assert.True(slotRead >= 0 && slotRead < inBoot.FindIndex(called => Same(called, Method(typeof(DistanceRpgGame), "BeginNewCampaign"))),
            "BootCampaign does not read the slot --new-campaign begins in before it begins the campaign");

        // What NEW CAMPAIGN set aside is put back by NEW CAMPAIGN alone, when the new
        // campaign never began, and only ever through the one door that moves a file
        // back where it was set aside, refusing a name that is taken.
        var putBack = Method(typeof(CampaignBoot), nameof(CampaignBoot.PutBack));
        Assert.Equal(["CampaignBoot.PutBack"], Callers(Method(typeof(SaveStore), nameof(SaveStore.PutBack))));
        Assert.Equal(["DistanceRpgGame.BeginNewCampaign"], Callers(putBack));

        // A campaign never comes into play in a slot that takes no saves (R5): NEW
        // CAMPAIGN reads the slot's refusal before it sets anything aside, writes the
        // new campaign's first save after it has set the old one aside and before it
        // binds the campaign to the slot (Adopt), and on the way that never binds it
        // puts back what it set aside and goes back to the title — so a close from
        // the title after a NEW CAMPAIGN that did not begin saves nothing.
        var begin = Method(typeof(DistanceRpgGame), "BeginNewCampaign");
        var inBegin = CallsIn(begin).ToList();
        var refusal = typeof(SaveStore).GetProperty(nameof(SaveStore.WritesRefused))!.GetMethod!;
        int refusedRead = inBegin.FindIndex(called => Same(called, refusal));
        int abandon = inBegin.FindIndex(called => Same(called, Method(typeof(CampaignBoot), nameof(CampaignBoot.Abandon))));
        int firstSave = inBegin.FindIndex(called => Same(called, Method(typeof(CampaignBoot), nameof(CampaignBoot.Save))));
        var showTitle = Method(typeof(DistanceRpgGame), "ShowTitle");
        int adopt = inBegin.FindIndex(called => Same(called, Method(typeof(DistanceRpgGame), "Adopt")));
        int titled = inBegin.FindIndex(called => Same(called, showTitle));
        Assert.True(refusedRead >= 0 && refusedRead < abandon && abandon < firstSave && firstSave < adopt,
            "BeginNewCampaign does not read the refusal, set the old campaign aside, save the new one and bind it, in that order");
        // The refusal is read twice: before anything is set aside, and again after,
        // since a setting aside that stuck refuses the slot's writes — which goes
        // back to the title before any first save is tried.
        var refusedReads = inBegin.Select((called, at) => (called, at)).Where(call => Same(call.called, refusal)).Select(call => call.at).ToList();
        Assert.Equal(2, refusedReads.Count);
        Assert.True(refusedReads[0] < abandon && abandon < refusedReads[1] && refusedReads[1] < firstSave,
            "BeginNewCampaign does not read the refusal again after setting the old campaign aside and before its first save");
        Assert.True(firstSave < titled && titled < adopt, "BeginNewCampaign does not go back to the title, on the way that never binds the campaign, after its first save");
        Assert.Equal(2, Mentions(begin, putBack));   // a setting aside that stuck, and a first save that failed
        Assert.All(inBegin.Select((called, at) => (called, at)).Where(call => Same(call.called, putBack)), call => Assert.InRange(call.at, abandon + 1, adopt - 1));
        Assert.Equal(1, Mentions(begin, Method(typeof(DistanceRpgGame), "Adopt")));
        Assert.Equal(1, Mentions(begin, showTitle));
        Assert.Equal(["DistanceRpgGame.BeginNewCampaign"], Callers(Method(typeof(CampaignBoot), nameof(CampaignBoot.NotBegunWord))));

        // And the way that never binds the campaign ends at the title: past the
        // ShowTitle call — over any nop, and any branch followed to where it goes —
        // the next instruction is the method's return, so nothing after it adopts a
        // campaign that did not begin.
        byte[] beginIl = Body(begin)!;
        var beginAt = InstructionsAt(beginIl).ToList();
        int next = beginAt.FindIndex(i => i.Token != 0 && Resolve(begin, i.Token) is { } called && Same(called, showTitle)) + 1;
        for (int hops = 0; beginAt[next].Op != OpCodes.Ret; hops++)
        {
            var (op, _, at) = beginAt[next];
            Assert.True(hops < beginAt.Count, "BeginNewCampaign's way back to the title runs in a loop");
            if (op == OpCodes.Nop)
            {
                next++;
                continue;
            }
            Assert.True(op == OpCodes.Br || op == OpCodes.Br_S, $"BeginNewCampaign goes on past ShowTitle ({op}) on the way that never binds the campaign");
            int target = op == OpCodes.Br_S ? at + 1 + (sbyte)beginIl[at] : at + 4 + BitConverter.ToInt32(beginIl, at);
            next = beginAt.FindIndex(i => i.At - i.Op.Size == target);
        }
    }

    [Fact]
    public void TheOldAutosaveMovesAtLaunchAlone_BeforeTheTitle()
    {
        // The user's ruling of 2026-09-25 on the save slots, R5 (SLOT DECISION 4):
        // the campaign a build before the ruling autosaved in the save folder itself
        // is moved into a slot of its own by the launch alone — at every launch, never
        // mid-session, so a campaign in play is never moved from under it — once the
        // slots are made and before anything reads them: before --new-campaign lists
        // them for its slot or begins a campaign, and before the title takes their
        // markers and lists them. Its word leads the title's. Never on a dev-override
        // launch: the call follows the override branch's own, as the slots are made
        // only after it (SaveSlotsTests.ADevOverrideSessionNeverTouchesTheSlots). Read
        // off the IL.
        var move = typeof(OldAutosave).GetMethod(nameof(OldAutosave.MoveIntoASlot), Declared)!;
        Assert.Equal(["DistanceRpgGame.BootCampaign"], Callers(move));
        var boot = typeof(DistanceRpgGame).GetMethod("BootCampaign", Declared)!;
        Assert.Equal(1, Mentions(boot, move));
        var inBoot = CallsIn(boot).ToList();
        int At(MethodBase target) => inBoot.FindIndex(called => Same(called, target));
        int moved = At(move);
        int slotsMade = At(typeof(SaveSlots).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single());
        int overridden = At(typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.BootOverride), Declared)!);
        int listed = At(typeof(SaveListing).GetMethod(nameof(SaveListing.Read), Declared)!);
        int begun = At(typeof(DistanceRpgGame).GetMethod("BeginNewCampaign", Declared)!);
        int titled = At(typeof(DistanceRpgGame).GetMethod("ShowTitle", Declared)!);
        Assert.True(overridden >= 0 && overridden < slotsMade && slotsMade < moved,
            "BootCampaign does not move the old autosave after the override branch and once the slots are made");
        Assert.True(moved < listed && moved < begun && moved < titled,
            "BootCampaign reads the slots, begins a campaign or shows the title before the old autosave has moved");

        // Its word reaches the screen, and first, as the move came first: what the
        // move hands back is kept in a local, and that local, as it is, is
        // ShowTitle's leading word, and BeginNewCampaign's earlier word for the
        // screen --new-campaign opens on. It is never led here: led before the new
        // campaign's word, it stood behind it, the last notice, and the one a long
        // save path cut (the audit of the ruling on the save slots). The one Lead
        // here is the override branch's own, before the move.
        var lead = typeof(StartupNotices).GetMethod(nameof(StartupNotices.Lead))!;
        var beginNew = typeof(DistanceRpgGame).GetMethod("BeginNewCampaign", Declared)!;
        Assert.Equal(1, Mentions(boot, lead));
        byte[] bootIl = Body(boot)!;
        var walked = InstructionsAt(bootIl).ToList();
        int Called(MethodBase target, int from) => walked.FindIndex(from, i => IsCall(i.Op) && Resolve(boot, i.Token) is { } called && Same(called, target));
        int moveAt = Called(move, 0);
        Assert.InRange(Called(lead, 0), 0, moveAt - 1);
        int? kept = Local(bootIl, walked[moveAt + 1], store: true);
        Assert.True(kept is not null, "BootCampaign does not keep what the move hands back in a local");
        Assert.Equal(kept, Local(bootIl, walked[Called(typeof(DistanceRpgGame).GetMethod("ShowTitle", Declared)!, moveAt) - 1], store: false));
        Assert.Equal(kept, Local(bootIl, walked[Called(beginNew, moveAt) - 1], store: false));
        Assert.Equal("earlier", beginNew.GetParameters()[^1].Name);

        // And BeginNewCampaign says that earlier word on either screen it opens: its
        // last argument is read once for the one word it leads for the screen between
        // runs, and once for the one word it hands the title, each after the call
        // before it — so no way out leaves it unsaid.
        var inBegin = InstructionsAt(Body(beginNew)!).ToList();
        Assert.Equal(3, beginNew.GetParameters().Length);   // number, how, earlier: after this, earlier is argument 3
        var says = inBegin.Select((i, at) => (i, at))
            .Where(call => IsCall(call.i.Op) && Resolve(beginNew, call.i.Token) is { } called
                && (Same(called, lead) || Same(called, typeof(DistanceRpgGame).GetMethod("ShowTitle", Declared)!)))
            .Select(call => call.at).ToList();
        Assert.Equal(2, says.Count);
        var readsEarlier = inBegin.Select((i, at) => (i, at)).Where(read => read.i.Op == OpCodes.Ldarg_3).Select(read => read.at).ToList();
        Assert.Equal(2, readsEarlier.Count);
        Assert.True(readsEarlier[0] < says[0] && says[0] < readsEarlier[1] && readsEarlier[1] < says[1],
            "BeginNewCampaign does not read its earlier word for each screen it opens, before it says what it has to say there");

        // Nothing else in the game reaches the move: not the title, a load, NEW
        // CAMPAIGN, a save or a quit.
        foreach (string later in new[] { "ShowTitle", "LoadSlot", "BeginNewCampaign", "WriteRequestedSave", "SaveOnQuit", "QuitToTitle", "OnUpdateFrame" })
        {
            var method = typeof(DistanceRpgGame).GetMethod(later, Declared)!;
            Assert.DoesNotContain(Reachable(method), reached => Same(reached, move));
        }
    }

    private const BindingFlags Declared = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Every method-typed operand in <paramref name="method"/>'s body that names <paramref name="target"/> — a call, or a delegate made of it (<c>ldftn</c>).</summary>
    private static int Mentions(MethodBase method, MethodBase target)
        => Instructions(Body(method)!).Count(i => i.Token != 0 && Resolve(method, i.Token) is { } named && Same(named, target));

    /// <summary>The local <paramref name="instruction"/> stores to — or, with <paramref name="store"/> false, loads — in any of the forms of <c>stloc</c> (<c>ldloc</c>); null for any other instruction.</summary>
    private static int? Local(byte[] il, (OpCode Op, int Token, int At) instruction, bool store)
    {
        OpCode[] numbered = store ? [OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3] : [OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3];
        if (Array.IndexOf(numbered, instruction.Op) is var index and >= 0)
            return index;
        if (instruction.Op == (store ? OpCodes.Stloc_S : OpCodes.Ldloc_S))
            return il[instruction.At];
        if (instruction.Op == (store ? OpCodes.Stloc : OpCodes.Ldloc))
            return BitConverter.ToUInt16(il, instruction.At);
        return null;
    }

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

        // A dev-override session's pause menu has no title to quit to: its 3  QUIT
        // GAME closes the window, and the close is where the quit saves, into the
        // session's scratch slot. (The call binds to the window's original declaration
        // of Close, which reflection on the game's type would hand back as its
        // override.)
        var close = typeof(GameEngine.Core.Game).GetMethod("Close", Type.EmptyTypes)!.GetBaseDefinition();
        Assert.Contains("DungeonScene.NumberKey", Callers(close));

        // Every other session's 3  QUIT TO TITLE (S7) — the pause menu's and the
        // menu's between runs — asks the host, which writes the autosave through
        // the close's own door before it shows the title.
        var quitToTitle = typeof(ISaveHost).GetMethod(nameof(ISaveHost.QuitToTitle))!;
        Assert.Equal(["BetweenRunsScene.MenuKey", "DungeonScene.NumberKey"], Callers(quitToTitle));
        var hostQuits = game.GetMethod(nameof(DistanceRpgGame.QuitToTitle))!;
        var inQuit = CallsIn(hostQuits).ToList();
        int savedOnQuit = inQuit.FindIndex(called => Same(called, game.GetMethod("SaveOnQuit", Declared)!));
        int titled = inQuit.FindIndex(called => Same(called, game.GetMethod("ShowTitle", Declared)!));
        Assert.True(savedOnQuit >= 0 && savedOnQuit < titled, "QuitToTitle shows the title before it writes the autosave");
    }

    [Fact]
    public void AnAutosaveWaitsForTheLoadedScreen_AndIsWrittenTheFrameItIsUp()
    {
        // S5's review (1f): a load's resume marker stands until the loaded
        // campaign's first screen is up, and a write clears its slot's marker. So an
        // autosave asked for before that screen is up — the resumed floor asks for
        // one as it is arrived on — must wait: written at once, it would clear the
        // marker, and a first screen that then crashed would leave nothing to set the
        // save aside with at the next launch. WriteRequestedSave reads _resuming
        // before it writes, and clears a request only past that read, so a request
        // that arrives while a load is resuming is kept, not dropped — the game's
        // own, and SAVE GAME's too (the user's ruling on the save slots, R5), which
        // the same door writes, once, into the same slot. And the frame's end clears
        // a resume whose screen is up before it writes a kept request, so the
        // request is written the frame the marker clears. Read off the IL: before
        // this pin, only the smoke launch covered it.
        var game = typeof(DistanceRpgGame);
        var write = game.GetMethod("WriteRequestedSave", Declared)!;
        var save = game.GetMethod("Save", Declared)!;
        var resuming = game.GetField("_resuming", Declared)!;
        var requested = game.GetField("_saveRequested", Declared)!;
        var byHand = game.GetField("_handSaveRequested", Declared)!;

        var walked = Walk(write);
        int read = walked.FindIndex(i => i.Op == OpCodes.Ldfld && SameField(i.Field, resuming));
        int firstWrite = walked.FindIndex(i => i.Called is { } called && Same(called, save));
        Assert.True(read >= 0, "WriteRequestedSave never reads _resuming");
        Assert.True(firstWrite > read, "WriteRequestedSave writes before it reads _resuming");
        Assert.Equal(1, Mentions(write, save));   // one write, whatever asked for it: past the read
        foreach (var request in new[] { requested, byHand })
        {
            var cleared = walked.Select((instruction, at) => (instruction, at))
                .Where(i => i.at > read && i.instruction.Op == OpCodes.Stfld && SameField(i.instruction.Field, request))
                .Select(i => i.at)
                .ToList();
            Assert.True(cleared.Count == 1 && cleared[0] < firstWrite && walked[cleared[0] - 1].Op == OpCodes.Ldc_I4_0,
                $"WriteRequestedSave does not clear {request.Name} exactly once, past its read of _resuming and before it writes");
        }
        Assert.DoesNotContain(walked.Take(read), i => i.Op == OpCodes.Ldfld && SameField(i.Field, byHand));   // a save by hand is taken only past the read: it waits too

        // The frame's end: the resume cleared once its screen is up, then a kept
        // request — either — written.
        var frame = game.GetMethod("OnUpdateFrame", Declared)!;
        var clearOnceUp = game.GetMethod("ClearResumeOnceUp", Declared)!;
        var frameWalked = Walk(frame);
        int clears = frameWalked.FindIndex(i => i.Called is { } called && Same(called, clearOnceUp));
        int writes = frameWalked.FindIndex(i => i.Called is { } called && Same(called, write));
        Assert.True(clears >= 0 && clears < writes, "OnUpdateFrame writes a kept request before it clears a resume whose screen is up");
        foreach (var request in new[] { requested, byHand })
        {
            int tested = frameWalked.FindIndex(i => i.Op == OpCodes.Ldfld && SameField(i.Field, request));
            Assert.True(clears < tested && tested < writes, $"OnUpdateFrame does not test {request.Name} before it writes");
        }
        Assert.Equal(1, Mentions(frame, write));
        Assert.Contains(Walk(clearOnceUp), i => i.Op == OpCodes.Stfld && SameField(i.Field, resuming));
        Assert.Equal(["DistanceRpgGame.OnUpdateFrame"], Callers(clearOnceUp));

        // A campaign let go, or one that comes into play, carries no request of the
        // one before it: DropCampaign, LoadSlot and BeginNewCampaign each store
        // false to both.
        foreach (string clearing in new[] { "DropCampaign", "LoadSlot", "BeginNewCampaign" })
        {
            var clearingWalk = Walk(game.GetMethod(clearing, Declared)!);
            foreach (var request in new[] { requested, byHand })
                Assert.True(clearingWalk.Select((instruction, at) => (instruction, at))
                        .Any(i => i.instruction.Op == OpCodes.Stfld && SameField(i.instruction.Field, request) && clearingWalk[i.at - 1].Op == OpCodes.Ldc_I4_0),
                    $"{clearing} does not store false to {request.Name}");
        }
    }

    [Fact]
    public void AQuitBeforeTheLoadedScreenIsUp_SavesTheViewTheLoadBroughtBack()
    {
        // T6's review: a close during a loaded run's fade in, before its screen is
        // up, finds no scene playing the run — the title is still the scene up for
        // the fade's first half — so there is no scene to read a view off, and the
        // quit's save wrote the campaign without the view the load brought back: the
        // slot's save rewritten less its active member and its march, the save that
        // had them pushed to the backup. So every load keeps the view it brought back
        // (_loadedView, stored from SlotLoad.View, by LoadSlot alone), and the quit's
        // door alone reads it — past its read of _resuming, before its save — into
        // what a view delegate it hands CampaignBoot.SaveOnQuit returns. Read off the
        // IL; CampaignBootTests.AQuitWithTheViewALoadBroughtBack_WritesNothing shows
        // what that keeps.
        var game = typeof(DistanceRpgGame);
        var loadedView = game.GetField("_loadedView", Declared);
        Assert.NotNull(loadedView);
        Assert.Equal(typeof(ViewDto), loadedView.FieldType);
        IEnumerable<string> Touching(OpCode op) => game.GetNestedTypes(Declared).Prepend(game)
            .SelectMany(type => type.GetMethods(Declared).Cast<MethodBase>())
            .Where(method => Body(method) is not null && Walk(method).Any(i => i.Op == op && SameField(i.Field, loadedView)))
            .Select(method => method.Name);

        var loading = Walk(game.GetMethod(nameof(DistanceRpgGame.LoadSlot))!);
        int kept = loading.FindIndex(i => i.Op == OpCodes.Stfld && SameField(i.Field, loadedView));
        Assert.True(kept > 0 && loading[kept - 1].Called is { } viewRead && Same(viewRead, typeof(SlotLoad).GetProperty(nameof(SlotLoad.View))!.GetMethod!),
            "LoadSlot does not keep the view its load brought back");
        Assert.Equal(["LoadSlot"], Touching(OpCodes.Stfld));
        Assert.Equal(["SaveOnQuit"], Touching(OpCodes.Ldfld));

        var quitting = Walk(game.GetMethod("SaveOnQuit", Declared)!);
        var resuming = game.GetField("_resuming", Declared)!;
        int gate = quitting.FindIndex(i => i.Op == OpCodes.Ldfld && SameField(i.Field, resuming));
        int read = quitting.FindIndex(i => i.Op == OpCodes.Ldfld && SameField(i.Field, loadedView));
        int door = quitting.FindIndex(i => i.Called is { } called && Same(called, typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.SaveOnQuit), Declared)!));
        Assert.True(gate >= 0 && gate < read && read < door, "SaveOnQuit does not read the loaded view past its read of _resuming, before its save");

        // What it read is captured, and a view delegate handed to the door returns it.
        int captured = quitting.FindIndex(read, i => i.Op == OpCodes.Stfld);
        Assert.True(captured > read && captured < door, "SaveOnQuit does not keep the loaded view for the delegate it hands the door");
        var keptIn = quitting[captured].Field!;
        var handed = quitting.Take(door)
            .Where(i => i.Op == OpCodes.Ldftn && i.Called is MethodInfo lambda && lambda.ReturnType == typeof(ViewDto))
            .Select(i => i.Called!)
            .ToList();
        Assert.Contains(handed, lambda => Walk(lambda).Any(i => i.Op == OpCodes.Ldfld && SameField(i.Field, keptIn)));
    }

    [Fact]
    public void ACampaignComesIntoPlayBoundToOneSlot_AndNothingRebindsIt()
    {
        // The user's ruling of 2026-09-25 on the save slots (R5): "you choose a save
        // slot and any saving happens on that save slot". The game holds the slot
        // the campaign in play is bound to in one field, _slot. It is set in one
        // place, Adopt — as a campaign comes into play: a slot loaded (LoadSlot), a
        // new campaign begun once its first save has gone through (BeginNewCampaign),
        // a dev override's scratch campaign (BootCampaign) — to the slot handed in,
        // and cleared in one, DropCampaign, as the campaign goes. The frame's end's
        // saves, the quit's, and the resume marker's clearing reach the slot through
        // that field alone and never ask the save folder's slots for one, so nothing
        // between a campaign's coming into play and its going can point its saves
        // anywhere else. Read off the IL.
        var game = typeof(DistanceRpgGame);
        var slotField = game.GetField("_slot", Declared)!;
        Assert.Equal(typeof(SaveSlot), slotField.FieldType);
        var adopt = game.GetMethod("Adopt", Declared)!;
        var drop = game.GetMethod("DropCampaign", Declared)!;

        // _slot is private, so only the game's own code — its methods, and the
        // lambdas and closures nested in it — can store it: each is read.
        var stores = new[] { game }.Concat(game.GetNestedTypes(Declared))
            .SelectMany(type => type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
            .Where(method => Body(method) is not null)
            .Where(method => Walk(method).Any(i => i.Op == OpCodes.Stfld && SameField(i.Field, slotField)))
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal);
        Assert.Equal(["Adopt", "DropCampaign"], stores);
        var adopting = Walk(adopt);
        int setAt = adopting.FindIndex(i => i.Op == OpCodes.Stfld && SameField(i.Field, slotField));
        Assert.Equal(OpCodes.Ldarg_2, adopting[setAt - 1].Op);   // the slot handed in, and nothing else
        Assert.Equal(1, adopting.Count(i => i.Op == OpCodes.Stfld && SameField(i.Field, slotField)));
        var dropping = Walk(drop);
        int clearedAt = dropping.FindIndex(i => i.Op == OpCodes.Stfld && SameField(i.Field, slotField));
        Assert.Equal(OpCodes.Ldnull, dropping[clearedAt - 1].Op);
        Assert.Equal(["DistanceRpgGame.BeginNewCampaign", "DistanceRpgGame.BootCampaign", "DistanceRpgGame.LoadSlot"], Callers(adopt));
        Assert.Equal(["DistanceRpgGame.QuitToTitle"], Callers(drop));

        // A load binds the one slot it asked the save folder for, and only once Load
        // has brought a campaign back: a load that brings nothing back binds nothing.
        var loadSlot = game.GetMethod(nameof(DistanceRpgGame.LoadSlot))!;
        var slotOf = typeof(SaveSlots).GetMethod(nameof(SaveSlots.Slot))!;
        var loading = CallsIn(loadSlot).ToList();
        Assert.Equal(1, Mentions(loadSlot, slotOf));
        int loaded = loading.FindIndex(called => Same(called, typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.Load), Declared)!));
        int bound = loading.FindIndex(called => Same(called, adopt));
        Assert.True(loading.FindIndex(called => Same(called, slotOf)) < loaded && loaded < bound, "LoadSlot binds a slot before its Load has brought a campaign back");
        Assert.Equal(1, Mentions(loadSlot, adopt));
        Assert.Equal(0, Mentions(loadSlot, typeof(SaveListing).GetMethod(nameof(SaveListing.Read), Declared)!));   // no listing decides anything of a load

        // A new campaign binds the one slot chosen for it (T3; the T3 review): the
        // title hands the host the slot its step names, and nothing else calls the
        // host's NEW CAMPAIGN; the host's title door hands that number on as it is;
        // and BeginNewCampaign refuses any number outside 1 to the count, then asks
        // the save folder for that slot alone, once, before it reads anything of it,
        // and has no other slot from anywhere — so the one it binds once the first
        // save has gone through is the one it was handed.
        var hostBegins = typeof(ITitleHost).GetMethod(nameof(ITitleHost.BeginNewCampaign))!;
        var onKey = typeof(TitleScene).GetMethod("OnKey", Declared)!;
        Assert.Equal(["TitleScene.OnKey"], Callers(hostBegins));
        Assert.Equal(1, Mentions(onKey, hostBegins));
        var keyed = Walk(onKey);
        int told = keyed.FindIndex(i => i.Called is { } called && Same(called, hostBegins));
        Assert.True(told > 0 && keyed[told - 1].Called is { } stepSlot && Same(stepSlot, typeof(TitleStep).GetProperty(nameof(TitleStep.Slot))!.GetMethod!),
            "The title does not hand the host's NEW CAMPAIGN the slot its step names");

        var begin = game.GetMethod("BeginNewCampaign", Declared)!;
        var map = game.GetInterfaceMap(typeof(ITitleHost));
        var door = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, hostBegins)];
        var opening = Walk(door);
        Assert.Equal(1, Mentions(door, begin));
        int handedOn = opening.FindIndex(i => i.Called is { } called && Same(called, begin));
        Assert.True(handedOn >= 3 && opening[handedOn - 3].Op == OpCodes.Ldarg_1 && opening[handedOn - 2].Op == OpCodes.Ldnull && opening[handedOn - 1].Op == OpCodes.Ldnull,
            "The host's NEW CAMPAIGN does not hand BeginNewCampaign the slot it was handed, naming no command line and no earlier word");

        var beginning = Walk(begin);
        Assert.Equal(1, Mentions(begin, slotOf));
        int asked = beginning.FindIndex(i => i.Called is { } called && Same(called, slotOf));
        Assert.Equal(OpCodes.Ldarg_1, beginning[asked - 1].Op);   // `number`, as it was handed
        bool Guard(MethodBase? called, string name) => called is MethodInfo { IsGenericMethod: true } guard
            && guard.DeclaringType == typeof(ArgumentOutOfRangeException) && guard.Name == name && guard.GetGenericArguments().SequenceEqual([typeof(int)]);
        int below = beginning.FindIndex(i => Guard(i.Called, nameof(ArgumentOutOfRangeException.ThrowIfLessThan)));
        int above = beginning.FindIndex(i => Guard(i.Called, nameof(ArgumentOutOfRangeException.ThrowIfGreaterThan)));
        Assert.True(below >= 3 && beginning[below - 3].Op == OpCodes.Ldarg_1 && beginning[below - 2].Op == OpCodes.Ldc_I4_1,
            "BeginNewCampaign does not refuse a number below 1");
        Assert.True(above >= 4 && beginning[above - 4].Op == OpCodes.Ldarg_1
            && beginning[above - 2].Called is { } counted && Same(counted, typeof(SaveSlots).GetProperty(nameof(SaveSlots.Count))!.GetMethod!),
            "BeginNewCampaign does not refuse a number above the count");
        var refusalRead = typeof(SaveStore).GetProperty(nameof(SaveStore.WritesRefused))!.GetMethod!;
        int firstRead = beginning.FindIndex(i => i.Called is { } called && Same(called, refusalRead));
        Assert.True(below < asked && above < asked && asked < firstRead,
            "BeginNewCampaign asks for its slot before it has refused a number outside 1 to the count, or after it has read the slot");
        Assert.DoesNotContain(beginning, i => i.Field?.FieldType == typeof(SaveSlot)
            || (i.Called is MethodInfo named && named.ReturnType == typeof(SaveSlot) && !Same(named, slotOf))
            || (i.Called is ConstructorInfo made && made.DeclaringType == typeof(SaveSlot)));

        // Every write of the campaign in play, and the marker's clearing, go through
        // _slot, and never ask the save folder's slots for another.
        var slotsField = game.GetField("_slots", Declared)!;
        foreach (string writes in new[] { "WriteRequestedSave", "SaveOnQuit", "ClearResumeMarker" })
        {
            var method = game.GetMethod(writes, Declared)!;
            var walked = Walk(method);
            Assert.Contains(walked, i => i.Op == OpCodes.Ldfld && SameField(i.Field, slotField));
            Assert.DoesNotContain(walked, i => i.Called?.DeclaringType == typeof(SaveSlots));
            Assert.DoesNotContain(walked, i => i.Op == OpCodes.Ldfld && SameField(i.Field, slotsField));
        }

        // And the frame's end's door (Save), which WriteRequestedSave hands _slot,
        // writes the slot it is handed and no other: it reads neither field, asks the
        // save folder's slots for nothing, makes no slot, and hands CampaignBoot.Save
        // its own `slot` argument, then the campaign.
        var save = game.GetMethod("Save", Declared)!;
        Assert.Equal(typeof(SaveSlot), save.GetParameters()[1].ParameterType);
        var saving = Walk(save);
        Assert.DoesNotContain(saving, i => i.Op == OpCodes.Ldfld && (SameField(i.Field, slotField) || SameField(i.Field, slotsField)));
        Assert.DoesNotContain(saving, i => i.Called?.DeclaringType == typeof(SaveSlots)
            || (i.Called is MethodInfo named && named.ReturnType == typeof(SaveSlot))
            || (i.Called is ConstructorInfo made && made.DeclaringType == typeof(SaveSlot)));
        int written = saving.FindIndex(i => i.Called is { } called && Same(called, typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.Save), Declared)!));
        Assert.True(written >= 0, "Save never calls CampaignBoot.Save");
        int handed = saving.FindLastIndex(written, i => i.Op == OpCodes.Ldarg_2);
        Assert.True(handed >= 0 && saving[handed + 1].Op == OpCodes.Ldarg_1, "Save does not hand CampaignBoot.Save the slot it was handed, then the campaign");
        Assert.Equal(1, saving.Count(i => i.Op == OpCodes.Ldarg_2));
    }

    /// <summary>Whether <paramref name="a"/> names the field <paramref name="b"/> is.</summary>
    private static bool SameField(FieldInfo? a, FieldInfo b) => a is not null && a.Module == b.Module && a.MetadataToken == b.MetadataToken;

    /// <summary>
    /// <paramref name="method"/>'s body, one instruction at a time and in order:
    /// each with the method it calls or names, and the field it loads or stores,
    /// when its operand is one.
    /// </summary>
    private static List<(OpCode Op, MethodBase? Called, FieldInfo? Field)> Walk(MethodBase method)
    {
        byte[] il = Body(method)!;
        return InstructionsAt(il)
            .Select(i => (i.Op,
                i.Token != 0 ? Resolve(method, i.Token) : null,
                i.Op.OperandType == OperandType.InlineField ? method.Module.ResolveField(BitConverter.ToInt32(il, i.At)) : null))
            .ToList();
    }
}
