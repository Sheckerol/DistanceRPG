using System.Text;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The data files' edge (§5.3, §5.4; 1e): the one place their text is read from
/// the disk or written to it. <c>Logic/</c> takes the text and never touches a
/// file (<see cref="ContentFiles.Load"/>). Engine-free — <c>System.IO</c> only —
/// and handed its folder every time: there is no default folder here, so nothing
/// can read or write the real one by accident. The game hands it the
/// executable's own folder (the files sit beside the exe, S:43), and
/// <c>--write-data</c> whatever folder it names. What the build itself carries
/// is read by <see cref="ShippedData"/>, never here.
/// <para>
/// Every file read, looked for and not found, or found and not readable, is
/// reported with its full path. That is how a playtester finds the copy to edit,
/// and how a log says which file a game ran under. So is a file the game no
/// longer reads that is found there anyway — <c>enchantments.json</c>,
/// <c>party.json</c> (<see cref="ContentFiles.Retired"/>) — which is looked for,
/// never read.
/// </para>
/// </summary>
internal static class DataFiles
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// The data files in <paramref name="directory"/>, in the load order. Each is
    /// null when it is not there, or when it is there and could not be read. An
    /// unreadable file is also listed in <see cref="ContentTexts.Unreadable"/>
    /// with the reason (<see cref="Why"/>), so the screen can say what happened
    /// instead of calling it missing. Each file read, missed or unreadable is
    /// reported to <paramref name="report"/> with its full path, and an
    /// unreadable one with the whole error too: all four — <c>tuning.json</c>,
    /// then the three content files, <c>restricted.json</c>, <c>weapons.json</c>
    /// and <c>dungeons.json</c>.
    /// <para>
    /// Then the files the game no longer reads (<see cref="ContentFiles.Retired"/>)
    /// are looked for, in ordinal order, and never opened: each one there is listed
    /// in <see cref="ContentTexts.Retired"/>, for the load to say, and reported
    /// with its full path. One that is not there is not reported at all — the game
    /// has not looked for it since it stopped being read.
    /// </para>
    /// </summary>
    public static ContentTexts Read(string directory, Action<string>? report = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var unreadable = new Dictionary<string, string>(StringComparer.Ordinal);
        string? tuning = ReadOne(directory, TuningJson.FileName, unreadable, report);
        string? restricted = ReadOne(directory, RestrictedJson.FileName, unreadable, report);
        string? weapons = ReadOne(directory, WeaponsJson.FileName, unreadable, report);
        string? dungeons = ReadOne(directory, DungeonsJson.FileName, unreadable, report);

        var retired = new List<string>();
        foreach (var (name, notice) in ContentFiles.Retired.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path))
                continue;
            report?.Invoke($"[Data] {path}: {notice}");
            retired.Add(name);
        }

        return new ContentTexts(Tuning: tuning, Restricted: restricted, Weapons: weapons, Dungeons: dungeons,
            Unreadable: unreadable.Count == 0 ? null : unreadable,
            Retired: retired.Count == 0 ? null : retired);
    }

    /// <summary>
    /// Write every text in <paramref name="texts"/> to its file in
    /// <paramref name="directory"/>, creating the folder: UTF-8 without a byte
    /// order mark, the text exactly as given — the built-in copies, byte for
    /// byte, when <c>--write-data</c> hands them in. Each file written is
    /// reported with its full path; a file with no text is not written. What
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
    /// and every number it changed from the built-in copy's, as clamped and fitted —
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

    /// <summary>Each file's name and text, in the load order.</summary>
    private static IEnumerable<(string Name, string? Text)> Files(ContentTexts texts)
    {
        var names = ContentFiles.FileNames;
        yield return (names[0], texts.Tuning);
        yield return (names[1], texts.Restricted);
        yield return (names[2], texts.Weapons);
        yield return (names[3], texts.Dungeons);
    }

    /// <summary>
    /// One file's text, or null when it is not there or cannot be read. Each case
    /// is reported as what it is, and the game plays the built-in one. An
    /// unreadable file goes into <paramref name="unreadable"/> by name, with
    /// <see cref="Why"/>.
    /// </summary>
    private static string? ReadOne(string directory, string name, IDictionary<string, string> unreadable, Action<string>? report)
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
            unreadable[name] = Why(error, path, name);
            return null;
        }
    }

    /// <summary>
    /// Why a file that is there could not be read, in a few words a notice can
    /// show. Two cases are named: another program holding it open, as an
    /// editor does, and no permission to read it. Any other error gives the
    /// system's own message with the file's name in place of its full path,
    /// which the log line already has.
    /// </summary>
    internal static string Why(Exception error, string path, string name)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error switch
        {
            UnauthorizedAccessException => "no permission to read it",
            IOException io when IsHeldOpen(io) => "another program has it open",
            _ => error.Message.Replace(path, name, StringComparison.OrdinalIgnoreCase).Trim().TrimEnd('.'),
        };
    }

    /// <summary>
    /// A sharing or lock violation (Windows errors 32 and 33): the file is open
    /// in another program that will not share it. Windows only, because on Unix
    /// the error's code is the system's errno.
    /// </summary>
    private static bool IsHeldOpen(IOException error)
        => OperatingSystem.IsWindows() && ((error.HResult & 0xFFFF) is 32 or 33);
}
