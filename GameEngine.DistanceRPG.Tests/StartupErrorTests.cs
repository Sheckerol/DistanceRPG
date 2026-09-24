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
        var load = ContentFiles.Load(shipped with
        {
            Tuning = """{ "MovementBudget": 200 }""",
            Restricted = DataTexts.Edit(shipped.Restricted!, "\"Riposte\": [\"Block\"]", "\"Riposte\": [\"Blok\"]"),
        });
        var lines = StartupErrorScene.Lines(load.Error!, load.TuningOverrides);
        Assert.Equal(
        [
            "THE GAME CANNOT START",
            "FILE: RESTRICTED.JSON",
            "ENTRY: BLOK",
            "RULE: ID DOES NOT RESOLVE TO A MODIFIER OR AN ENCHANTMENT",
            "DETAIL: NOT A MODIFIER, NOR AN ENCHANTMENT IN ENCHANTMENTS.JSON",
            "  - DID YOU MEAN BLOCK?",
            "TUNING.JSON CHANGES 1 NUMBER: MOVEMENTBUDGET 200",
            "FIX THE FILE AND START AGAIN - ESC QUITS",
        ], lines);

        // Every row is upper-cased — the HUD's font is capitals — and under the width
        // the screens' lines wrap at.
        Assert.All(lines, row => Assert.Equal(row.ToUpperInvariant(), row));
        Assert.All(lines, row => Assert.InRange(row.Length, 1, NoticeText.LineChars));

        // The lines come from the properties, never the message: a failure stamped
        // as compiled says so beside its file; one no stage stamped names no file;
        // one with no detail has no detail row; a tuning that changed nothing says so.
        var compiled = new ContentException("arming_sword", ContentValidator.RuleForgedNotAllowed, "Block x1 on a Melee weapon beside Block x1, Push x1, Light x1")
            .In(ContentFiles.WeaponsFileName, compiled: true);
        var compiledLines = StartupErrorScene.Lines(compiled, ["MovementBudget 200", "PerStack.Block 4"]);
        Assert.Equal("FILE: WEAPONS.JSON (COMPILED)", compiledLines[1]);
        Assert.Equal("ENTRY: ARMING_SWORD", compiledLines[2]);
        Assert.Contains("TUNING.JSON CHANGES 2 NUMBERS: MOVEMENTBUDGET 200, PERSTACK.BLOCK 4", string.Join(" ", compiledLines.Select(row => row.Trim())));
        Assert.Equal(StartupErrorScene.KeysLine, compiledLines[^1]);
        Assert.Equal(
            ["THE GAME CANNOT START", "ENTRY: X", "RULE: A RULE", "TUNING.JSON CHANGES NO NUMBERS", "FIX THE FILE AND START AGAIN - ESC QUITS"],
            StartupErrorScene.Lines(new ContentException("x", "a rule"), []));

        // A long detail wraps under the width, its rows indented under its first,
        // every word kept and in order.
        string detail = string.Join(" ", Enumerable.Repeat("restricted.json excludes [Push, Block]", 5));
        var wrapped = StartupErrorScene.Lines(new ContentException("tower_guard", "a rule", detail), []);
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
        // load's notices — here, that no enchantments.json was found, which is what
        // the relations beside it broke: the built-in elements, against a
        // restricted.json that opposes shocking to flaming as well as cold.
        var shipped = DataTexts.Shipped;
        var load = ContentFiles.Load(shipped with
        {
            Enchantments = null,
            Restricted = DataTexts.Edit(shipped.Restricted!, "[\"flaming\", \"cold\"]", "[\"flaming\", \"cold\", \"shocking\"]"),
        });
        var scene = new StartupErrorScene(game: null!, load.Error!, load.TuningOverrides, load.Notices);
        Assert.Equal(StartupErrorScene.Lines(load.Error!, load.TuningOverrides), scene.Rows);
        Assert.Equal("FILE: ENCHANTMENTS.JSON (COMPILED)", scene.Rows[1]);
        Assert.Equal("ENTRY: FLAMING", scene.Rows[2]);
        Assert.Contains("IN RESTRICTED.JSON EXCLUDES", string.Join(" ", scene.Rows.Select(row => row.Trim())));
        Assert.Equal(["enchantments.json is missing - the built-in enchantments are playing"], load.Notices);
        Assert.Equal(NoticeText.Rows(load.Notices), scene.NoticeRows);
        Assert.Equal(["ENCHANTMENTS.JSON IS MISSING - THE BUILT-IN ENCHANTMENTS ARE", "  PLAYING"], scene.NoticeRows);
        Assert.Empty(new StartupErrorScene(game: null!, load.Error!, load.TuningOverrides).NoticeRows);
    }
}
