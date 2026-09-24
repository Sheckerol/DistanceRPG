using System.Text;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The data files' edge (§5.3, §5.4; 1e): the one place their text is read from
/// the disk or written to it. <c>Logic/</c> takes the text and never touches a
/// file (<see cref="ContentFiles.Load"/>). Engine-free — <c>System.IO</c> only —
/// and handed its folder every time: there is no default folder here, so nothing
/// can read or write the real one by accident. The game hands it the
/// executable's own folder (the files sit beside the exe, S:43) and the
/// generator whatever <c>--write-data</c> names.
/// <para>
/// Every file read, or looked for and not found, is reported with its full path
/// — which is how a playtester finds the copy to edit, and how a log says which
/// file a game ran under.
/// </para>
/// </summary>
internal static class DataFiles
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// The data files beside <paramref name="directory"/>, each null when it is
    /// not there; each one read or missed is reported to <paramref name="report"/>
    /// with its full path. Phase 5.1 reads <c>tuning.json</c>; the content files
    /// are the compiled records until the sub-steps that ship them.
    /// </summary>
    public static ContentTexts Read(string directory, Action<string>? report = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return new ContentTexts(Tuning: ReadOne(directory, TuningJson.FileName, report));
    }

    /// <summary>
    /// Write every text in <paramref name="texts"/> to its file in
    /// <paramref name="directory"/>, creating the folder: UTF-8 without a byte
    /// order mark, the text exactly as given (the generator's <c>"\n"</c> line
    /// ends). Each file written is reported with its full path. What
    /// <c>--write-data</c> runs; the game itself never writes beside the exe.
    /// </summary>
    public static void Write(string directory, ContentTexts texts, Action<string>? report = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(texts);
        Directory.CreateDirectory(directory);
        foreach (var (name, text) in Files(texts))
        {
            if (text is null) continue;
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, text, Utf8);
            report?.Invoke($"[Data] Wrote {path}");
        }
    }

    /// <summary>
    /// The log's line for the tuning the game runs under: the file's full path
    /// and every number it changed from the compiled ones, as clamped and fitted —
    /// <c>[Data] D:\...\net10.0\tuning.json changes 2 numbers: MovementBudget 200, PerStack.Block 4</c>.
    /// </summary>
    internal static string TuningSummary(string directory, IReadOnlyList<string> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        string path = Path.Combine(directory, TuningJson.FileName);
        return overrides.Count == 0
            ? $"[Data] {path} changes no numbers - the built-in tuning plays"
            : $"[Data] {path} changes {overrides.Count} {(overrides.Count == 1 ? "number" : "numbers")}: {string.Join(", ", overrides)}";
    }

    /// <summary>Each file's name and text, in §5.4's order.</summary>
    private static IEnumerable<(string Name, string? Text)> Files(ContentTexts texts)
    {
        var names = ContentFiles.FileNames;
        yield return (names[0], texts.Tuning);
        yield return (names[1], texts.Restricted);
        yield return (names[2], texts.Enchantments);
        yield return (names[3], texts.Weapons);
        yield return (names[4], texts.Dungeons);
        yield return (names[5], texts.Party);
    }

    /// <summary>One file's text, or null — not there, or not readable (reported as such: the game plays the built-in one).</summary>
    private static string? ReadOne(string directory, string name, Action<string>? report)
    {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path))
        {
            report?.Invoke($"[Data] {path} is missing - the built-in one plays");
            return null;
        }

        try
        {
            string text = File.ReadAllText(path);
            report?.Invoke($"[Data] Read {path}");
            return text;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Data] {path} could not be read ({error.Message}) - the built-in one plays");
            return null;
        }
    }
}
