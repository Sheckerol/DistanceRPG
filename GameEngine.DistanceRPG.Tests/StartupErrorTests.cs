using System.Reflection;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// A bad content file on screen (1h, 1i; PHASE5-096): the game does not start,
/// and the screen it opens on instead names the file, the entry, the rule and its
/// detail, says what <c>tuning.json</c> changed, and says what to do — every line
/// built from the failure's properties, never from its message, and wrapped by
/// the one wrapper every notice goes through. The screen is laid out with no
/// window, as the HUD tests lay theirs out; only the drawing is untested.
/// </summary>
public class StartupErrorTests
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
    public void TheScreenNamesTheFileTheEntryTheRule_AndWhatTuningChanged()
    {
        // The playtest's line (1i): a typo in restricted.json, played beside a tuned
        // budget, opens the game on THE GAME CANNOT START - RESTRICTED.JSON - BLOK -
        // ID DOES NOT RESOLVE TO A MODIFIER OR AN ENCHANTMENT, with the id it is
        // nearest, what the tuning changed, and the way out.
        var shipped = DataTexts.Shipped;
        var load = ShippedData.Load(shipped with
        {
            Tuning = """{ "MovementBudget": 200 }""",
            Restricted = DataTexts.Edit(shipped.Restricted!, "\"Riposte\": [\"Block\"]", "\"Riposte\": [\"Blok\"]"),
        });
        var lines = StartupErrorScene.Lines(load.Error!, load.TuningOverrides, load.ReadBeside);
        Assert.Equal(
        [
            "THE GAME CANNOT START",
            "FILE: RESTRICTED.JSON",
            "ENTRY: BLOK",
            "RULE: ID DOES NOT RESOLVE TO A MODIFIER OR AN ENCHANTMENT",
            "DETAIL: NOT A MODIFIER, NOR AN ENCHANTMENT IN THE ENCHANTMENT",
            "  CATALOGUE - DID YOU MEAN BLOCK?",
            "TUNING.JSON CHANGES 1 NUMBER: MOVEMENTBUDGET 200",
            "FIX THE FILE AND START AGAIN - ESC QUITS",
        ], lines);

        // Every row is upper-cased — the HUD's font is capitals — and under the width
        // the screens' lines wrap at.
        Assert.All(lines, row => Assert.Equal(row.ToUpperInvariant(), row));
        Assert.All(lines, row => Assert.InRange(row.Length, 1, NoticeText.LineChars));

        // And no row holds an apostrophe, which that font cannot draw (it would
        // leave a gap): the detail names the enchantment catalogue without one.
        Assert.All(lines, row => Assert.DoesNotContain("'", row));

        // The lines come from the properties, never the message: a failure stamped
        // as built-in says so beside its file; one no stage stamped names no file;
        // one with no detail has no detail row; a tuning that changed nothing says so.
        var builtIn = new ContentException("arming_sword", ContentValidator.RuleForgedNotAllowed, "Block x1 on a Melee weapon beside Block x1, Push x1, Light x1")
            .In(ContentFiles.WeaponsFileName, builtIn: true);
        var builtInLines = StartupErrorScene.Lines(builtIn, ["MovementBudget 200", "PerStack.Block 4"], [RestrictedJson.FileName]);
        Assert.Equal("FILE: WEAPONS.JSON (BUILT-IN)", builtInLines[1]);
        Assert.Equal("ENTRY: ARMING_SWORD", builtInLines[2]);
        Assert.Contains("TUNING.JSON CHANGES 2 NUMBERS: MOVEMENTBUDGET 200, PERSTACK.BLOCK 4", string.Join(" ", builtInLines.Select(row => row.Trim())));
        Assert.Equal(StartupErrorScene.KeysLine, builtInLines[^1]);
        Assert.Equal(
            ["THE GAME CANNOT START", "ENTRY: X", "RULE: A RULE", "TUNING.JSON CHANGES NO NUMBERS", "FIX THE FILE AND START AGAIN - ESC QUITS"],
            StartupErrorScene.Lines(new ContentException("x", "a rule"), [], []));

        // A long detail wraps under the width, its rows indented under its first,
        // every word kept and in order.
        string detail = string.Join(" ", Enumerable.Repeat("restricted.json excludes [Push, Block]", 5));
        var wrapped = StartupErrorScene.Lines(new ContentException("tower_guard", "a rule", detail), [], []);
        var detailRows = wrapped.SkipWhile(row => !row.StartsWith("DETAIL: ", StringComparison.Ordinal)).TakeWhile(row => !row.StartsWith("TUNING.JSON", StringComparison.Ordinal)).ToList();
        Assert.True(detailRows.Count > 1);
        Assert.All(detailRows.Skip(1), row => Assert.StartsWith("  ", row));
        Assert.All(detailRows, row => Assert.InRange(row.Length, 1, NoticeText.LineChars));
        Assert.Equal($"DETAIL: {detail}".ToUpperInvariant(), string.Join(" ", detailRows.Select(row => row.Trim())));
    }

    [Fact]
    public void ABrokenFileOpensTheGameOnTheErrorScreen()
    {
        // The game's startup builds the screen when the content does not load, and
        // nothing else builds it; the log is written from the same lines the screen
        // draws (1h: "the log has the same lines").
        var constructor = typeof(StartupErrorScene).GetConstructors().Single();
        var lines = typeof(StartupErrorScene).GetMethod(nameof(StartupErrorScene.Lines), BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(["DistanceRpgGame.LoadData"], Callers(constructor));
        Assert.Equal(["DistanceRpgGame.LoadData", "StartupErrorScene..ctor"], Callers(lines));

        // And the screen it builds holds the failure's lines and, under them, the
        // load's notices — here, that no weapons.json was found, which is what the
        // relations beside it broke: the built-in weapons, against a
        // restricted.json that groups Push with Block.
        var shipped = DataTexts.Shipped;
        var load = ShippedData.Load(shipped with
        {
            Weapons = null,
            Restricted = DataTexts.Edit(shipped.Restricted!, "[\"Light\", \"Resonant\"],", "[\"Light\", \"Resonant\"],\n    [\"Push\", \"Block\"],"),
        });
        var scene = new StartupErrorScene(game: null!, load.Error!, load.TuningOverrides, load.ReadBeside, load.Notices);
        Assert.Equal(StartupErrorScene.Lines(load.Error!, load.TuningOverrides, load.ReadBeside), scene.Rows);
        Assert.Equal("FILE: WEAPONS.JSON (BUILT-IN)", scene.Rows[1]);
        Assert.Equal("ENTRY: ARMING_SWORD", scene.Rows[2]);
        Assert.Contains("RESTRICTED.JSON EXCLUDES [PUSH, BLOCK]", string.Join(" ", scene.Rows.Select(row => row.Trim())));
        Assert.Equal(["weapons.json is missing - the built-in weapons are playing"], load.Notices);
        Assert.Equal(NoticeText.Rows(load.Notices), scene.NoticeRows);
        Assert.Equal(["WEAPONS.JSON IS MISSING - THE BUILT-IN WEAPONS ARE PLAYING"], scene.NoticeRows);
        Assert.Empty(new StartupErrorScene(game: null!, load.Error!, load.TuningOverrides, load.ReadBeside).NoticeRows);
    }

    [Fact]
    public void ABrokenMasterSaysToFixTheMaster()
    {
        // The content rework, 1h: (BUILT-IN) marks two situations, and the closing
        // line tells them apart. A built-in copy that broke with nothing beside the
        // executable involved can only be fixed in its master, and the game
        // rebuilt: the screen says which file. A master that is not JSON — edited
        // and never tested — is its own failure, whatever sits beside the
        // executable: here nothing does, and then the playtest's case, a
        // restricted.json and a dungeons.json there and no weapons.json.
        const string FixTheMaster = "THE BUILT-IN WEAPONS.JSON IS BROKEN - FIX GAMEENGINE.DISTANCERPG/DATA/WEAPONS.JSON AND REBUILD - ESC QUITS";
        var shipped = DataTexts.Shipped;
        var brokenMaster = shipped with { Weapons = DataTexts.Edit(shipped.Weapons!, "\"id\": \"halberd\",", "\"id\": \"halberd\"") };
        var alone = ContentFiles.Load(new ContentTexts(), brokenMaster);
        Assert.Empty(alone.ReadBeside);
        var lines = StartupErrorScene.Lines(alone.Error!, alone.TuningOverrides, alone.ReadBeside);
        Assert.Equal("FILE: WEAPONS.JSON (BUILT-IN)", lines[1]);
        Assert.Equal(FixTheMaster, lines[^1]);
        Assert.Equal(lines[^1], new StartupErrorScene(game: null!, alone.Error!, alone.TuningOverrides, alone.ReadBeside, alone.Notices).Rows[^1]);

        var withOthersBeside = ContentFiles.Load(shipped with { Weapons = null }, brokenMaster);
        Assert.Equal([RestrictedJson.FileName, DungeonsJson.FileName], withOthersBeside.ReadBeside);
        Assert.Equal((WeaponsJson.FileName, true, ContentValidator.RuleFileShape),
            (withOthersBeside.Error!.File, withOthersBeside.Error.BuiltIn, withOthersBeside.Error.Rule));
        Assert.Equal(FixTheMaster, StartupErrorScene.Lines(withOthersBeside.Error!, withOthersBeside.TuningOverrides, withOthersBeside.ReadBeside)[^1]);

        // A built-in copy the build does not carry is the master's too.
        var notCarried = new ContentException(WeaponsJson.FileName, ContentValidator.RuleBuiltInCopy, "weapons.json (built-in): the game was built without it")
            .In(WeaponsJson.FileName, builtIn: true);
        Assert.Equal(FixTheMaster, StartupErrorScene.ClosingLine(notCarried, [RestrictedJson.FileName]));

        // A sound built-in weapons.json that a restricted.json beside the executable
        // makes illegal — Push grouped with Block, which every sword forges — fails at
        // the built-in weapons' stage, and it is that file beside it that broke it:
        // the line stays the one for a file.
        var pushBlocks = DataTexts.Edit(shipped.Restricted!, "[\"Light\", \"Resonant\"],", "[\"Light\", \"Resonant\"],\n    [\"Push\", \"Block\"],");
        var besideAFile = ContentFiles.Load(new ContentTexts(Restricted: pushBlocks), shipped);
        Assert.Equal([RestrictedJson.FileName], besideAFile.ReadBeside);
        Assert.Equal((WeaponsJson.FileName, true, ContentValidator.RuleForgedNotAllowed),
            (besideAFile.Error!.File, besideAFile.Error.BuiltIn, besideAFile.Error.Rule));
        Assert.Equal(StartupErrorScene.KeysLine, StartupErrorScene.Lines(besideAFile.Error!, besideAFile.TuningOverrides, besideAFile.ReadBeside)[^1]);

        // The same rule broken with nothing beside the executable — a master weapon
        // forged Light x0 — is the master's own.
        var zeroStack = shipped with { Weapons = DataTexts.InRow(shipped.Weapons!, "flensing_knife", "\"Light\": 1", "\"Light\": 0") };
        var masterRule = ContentFiles.Load(new ContentTexts(), zeroStack);
        Assert.Equal((WeaponsJson.FileName, true, ContentValidator.RuleForgedStackCount),
            (masterRule.Error!.File, masterRule.Error.BuiltIn, masterRule.Error.Rule));
        Assert.Equal(FixTheMaster, StartupErrorScene.Lines(masterRule.Error!, masterRule.TuningOverrides, masterRule.ReadBeside)[^1]);

        // A built-in tuning.json that fails always names the master, whatever was
        // read beside it: every copy beside the game is read over it, and none can
        // break it.
        var tuning = new ContentException(nameof(Tuning.MovementBudget), ContentValidator.RuleFileShape, "tuning.json (built-in): MovementBudget: is missing")
            .In(TuningJson.FileName, builtIn: true);
        Assert.Equal("THE BUILT-IN TUNING.JSON IS BROKEN - FIX GAMEENGINE.DISTANCERPG/DATA/TUNING.JSON AND REBUILD - ESC QUITS",
            StartupErrorScene.Lines(tuning, [], [RestrictedJson.FileName, WeaponsJson.FileName, DungeonsJson.FileName])[^1]);

        // And a failure of a file beside the executable, or of none, keeps the line
        // for a file; no closing line holds an apostrophe the screen cannot draw.
        Assert.Equal(StartupErrorScene.KeysLine, StartupErrorScene.ClosingLine(new ContentException("x", "a rule").In(WeaponsJson.FileName, builtIn: false), []));
        Assert.Equal(StartupErrorScene.KeysLine, StartupErrorScene.ClosingLine(new ContentException("x", "a rule"), []));
        Assert.All(lines, row => Assert.DoesNotContain("'", row));
    }

    [Fact]
    public void ABrokenBuiltInTuningReachesTheScreen()
    {
        // A built-in tuning.json without MovementBudget stops the load before any
        // number exists: no tuning, none changed. The screen still renders — the
        // file, the key, the rule — and says the tuning changed nothing, without
        // touching a tuning that is not there.
        var shipped = DataTexts.Shipped;
        var gap = shipped with { Tuning = DataTexts.WithoutKey(shipped.Tuning!, nameof(Tuning.MovementBudget)) };
        var load = ContentFiles.Load(shipped, gap);
        Assert.Null(load.Content);
        Assert.Null(load.Tuning);
        Assert.Empty(load.TuningOverrides);
        Assert.Empty(load.ReadBeside);

        var lines = StartupErrorScene.Lines(load.Error!, load.TuningOverrides, load.ReadBeside);
        Assert.Equal("FILE: TUNING.JSON (BUILT-IN)", lines[1]);
        Assert.Equal("ENTRY: MOVEMENTBUDGET", lines[2]);
        Assert.Contains("TUNING.JSON CHANGES NO NUMBERS", lines);
        Assert.StartsWith("THE BUILT-IN TUNING.JSON IS BROKEN", lines[^1], StringComparison.Ordinal);
        var scene = new StartupErrorScene(game: null!, load.Error!, load.TuningOverrides, load.ReadBeside, load.Notices);
        Assert.Equal(lines, scene.Rows);
    }
}
