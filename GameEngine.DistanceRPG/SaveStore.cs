using System.Text;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The save's edge (1e, 1f): the one place a campaign's text is read from the
/// disk or written to it. <c>Logic/</c> turns a campaign into text and back
/// (<see cref="Logic.SaveCodec"/>, <see cref="Logic.SaveGame"/>) and never touches
/// a file. Engine-free — <c>System.IO</c> only — and handed its folder every
/// time: there is no parameterless constructor and no default folder here, so
/// nothing can read or write the player's real saves by accident. The game hands
/// it the player's save folder (<see cref="SaveDirectory"/>), or that folder's
/// dev-override slot (<see cref="SlotDirectory"/>); a test hands it a temporary
/// folder.
/// <para>
/// <strong>What lives in the folder.</strong> <see cref="FileName"/> is the
/// campaign. <see cref="BackupName"/> is the save before it, kept by every
/// write's atomic replace. <see cref="ResumingName"/> is there while a resume is
/// in flight, so a launch that finds it knows the last resume never finished.
/// And <c>campaign.&lt;reason&gt;-N.json</c> is a save set aside — unreadable,
/// crashed or abandoned — which nothing here ever deletes or writes over.
/// </para>
/// <para>
/// <strong>A write never costs the save before it.</strong> The text goes to
/// <see cref="TempName"/> first, flushed to the disk. Only then does it replace
/// the save in one step, the old save becoming the backup. A write that fails at
/// any point leaves <see cref="FileName"/> exactly as it was. The one delete is
/// <see cref="Empty"/>, which refuses any folder but a dev-override slot. The
/// resume marker, which is no save, is the only other file this class removes.
/// </para>
/// </summary>
internal sealed class SaveStore
{
    /// <summary>The campaign's save.</summary>
    public const string FileName = "campaign.json";

    /// <summary>The save before the last one, kept by the atomic replace.</summary>
    public const string BackupName = "campaign.bak.json";

    /// <summary>There while a resume is in flight: a launch that finds it knows the resume before it never finished.</summary>
    public const string ResumingName = "campaign.resuming";

    /// <summary>Set to a folder, the saves live there instead of the player's data folder, the dev-override slot inside it.</summary>
    public const string DirectoryVariable = "DISTANCERPG_SAVE_DIR";

    /// <summary>The folder inside the saves a dev-override launch plays in: emptied at every such launch, and never the campaign's.</summary>
    public const string OverrideSlot = "override";

    /// <summary>What a write goes through before it replaces the save: a folder of this name in the way is a write that fails.</summary>
    internal const string TempName = "campaign.json.tmp";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <param name="directory">The folder the saves live in, made absolute here; it need not exist yet — the first write makes it.</param>
    public SaveStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Folder = Path.GetFullPath(directory);
    }

    /// <summary>The folder, as a full path: every log line and every notice names a file in it by its full path.</summary>
    public string Folder { get; }

    /// <summary>The campaign's save, in full.</summary>
    public string SavePath => Path.Combine(Folder, FileName);

    /// <summary>The backup, in full.</summary>
    public string BackupPath => Path.Combine(Folder, BackupName);

    /// <summary>The resume marker, in full.</summary>
    public string ResumingPath => Path.Combine(Folder, ResumingName);

    /// <summary>The temp file a write goes through, in full.</summary>
    internal string TempPath => Path.Combine(Folder, TempName);

    /// <summary>Whether there is a save to resume.</summary>
    public bool HasSave => File.Exists(SavePath);

    /// <summary>Whether there is a backup.</summary>
    public bool HasBackup => File.Exists(BackupPath);

    /// <summary>
    /// Why this store will not write, or null while it will. Set when a save it
    /// could not read could not be set aside either (<see cref="RefuseWrites"/>),
    /// so that nothing written this session can take its place.
    /// </summary>
    public string? WritesRefused { get; private set; }

    /// <summary>The save's text, or null when there is none. A save that is there and cannot be read throws, as the file system says why.</summary>
    public string? Read() => ReadIfThere(SavePath);

    /// <summary>The backup's text, or null when there is none.</summary>
    public string? ReadBackup() => ReadIfThere(BackupPath);

    private static string? ReadIfThere(string path) => File.Exists(path) ? File.ReadAllText(path, Utf8) : null;

    /// <summary>
    /// Write <paramref name="text"/> as the save, atomically: into
    /// <see cref="TempName"/>, flushed to the disk, and then in place of the save
    /// in one step. The save it replaces becomes the backup. The first write has
    /// no save to replace, so the temp file is simply moved into place. The
    /// folder is made if need be. UTF-8, no byte order mark, the text exactly as
    /// given.
    /// </summary>
    /// <exception cref="IOException">The write failed — a full disk, a locked file, a folder in the way — or writes are refused; the save on the disk is what it was.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission to write there; the save on the disk is what it was.</exception>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (WritesRefused is { } refused)
            throw new IOException(refused);

        Directory.CreateDirectory(Folder);
        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(Utf8.GetBytes(text));
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(SavePath))
            File.Replace(TempPath, SavePath, BackupPath, ignoreMetadataErrors: true);
        else
            File.Move(TempPath, SavePath);
    }

    /// <summary>
    /// Move the save — or, with <paramref name="backup"/>, the backup — out of the
    /// way as <c>campaign.&lt;reason&gt;-N.json</c>, with N the first number from
    /// 1 no file or folder there has taken. Nothing is deleted and nothing is
    /// written over: the move refuses a name that is taken. Renaming the file back
    /// to <see cref="FileName"/> is how it is tried again.
    /// </summary>
    /// <param name="reason">Why, in one lower-case word: <c>unreadable</c>, <c>crashed</c>, <c>abandoned</c>.</param>
    /// <param name="backup">Set the backup aside rather than the save.</param>
    /// <returns>The full path it now has.</returns>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is not one lower-case word.</exception>
    /// <exception cref="FileNotFoundException">There is no such file to set aside.</exception>
    public string SetAside(string reason, bool backup = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!reason.All(char.IsAsciiLetterLower))
            throw new ArgumentException($"'{reason}' is not one lower-case word: a reason names a file.", nameof(reason));

        string source = backup ? BackupPath : SavePath;
        if (!File.Exists(source))
            throw new FileNotFoundException("There is nothing there to set aside.", source);

        for (int n = 1; ; n++)
        {
            string target = Path.Combine(Folder, $"campaign.{reason}-{n}.json");
            if (File.Exists(target) || Directory.Exists(target))
                continue;
            File.Move(source, target, overwrite: false);
            return target;
        }
    }

    /// <summary>Whether the resume marker is there: the last launch began a resume that never finished.</summary>
    public bool WasResuming => File.Exists(ResumingPath);

    /// <summary>Write the resume marker: a resume is in flight until <see cref="ClearResuming"/>.</summary>
    public void MarkResuming()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(ResumingPath,
            "A resume is in flight. A launch that finds this file sets the save it was resuming aside as crashed.\n", Utf8);
    }

    /// <summary>Remove the resume marker, if it is there: the resume finished, or nothing is being resumed.</summary>
    public void ClearResuming()
    {
        if (File.Exists(ResumingPath))
            File.Delete(ResumingPath);
    }

    /// <summary>
    /// Refuse every write from now on, saying why: a save that could not be read
    /// could not be moved out of the way either, so writing anything would put a
    /// new save in its place.
    /// </summary>
    public void RefuseWrites(string why)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(why);
        WritesRefused = why;
    }

    /// <summary>
    /// Delete every file in the folder, for a dev-override launch, which plays a
    /// fresh campaign every time. It refuses any folder not named
    /// <see cref="OverrideSlot"/>, so the campaign's own saves can never be
    /// emptied by a mistake. Nothing below the folder is touched, and a folder
    /// that does not exist yet is already empty.
    /// </summary>
    /// <exception cref="InvalidOperationException">The folder is not a dev-override slot.</exception>
    public void Empty()
    {
        if (!string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(Folder)), OverrideSlot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{Folder} is not a dev-override slot: only a folder named '{OverrideSlot}' is ever emptied, so a campaign's saves are never deleted.");
        if (!Directory.Exists(Folder))
            return;
        foreach (string file in Directory.GetFiles(Folder))
            File.Delete(file);
    }

    // ── Where the saves live: strings in, strings out ─────────────────────────

    /// <summary>The player's own save folder: <c>&lt;localAppData&gt;/DistanceRPG/saves</c> — on Windows under <c>%LOCALAPPDATA%</c>, on Linux under <c>~/.local/share</c>.</summary>
    public static string DefaultDirectory(string localAppData)
    {
        ArgumentNullException.ThrowIfNull(localAppData);
        return Path.Combine(localAppData, "DistanceRPG", "saves");
    }

    /// <summary>
    /// The save folder: <paramref name="overrideValue"/> — the
    /// <see cref="DirectoryVariable"/> variable, surrounding whitespace ignored —
    /// when it is set and not blank, and <see cref="DefaultDirectory"/> of
    /// <paramref name="localAppData"/> otherwise. A pure function of the two
    /// strings: the caller reads the environment, and nothing here touches it or
    /// the disk.
    /// </summary>
    internal static string SaveDirectory(string? overrideValue, string localAppData)
        => string.IsNullOrWhiteSpace(overrideValue) ? DefaultDirectory(localAppData) : overrideValue.Trim();

    /// <summary>The folder a launch plays in: the dev-override slot inside <paramref name="saveDirectory"/> for an override launch, and the save folder itself otherwise.</summary>
    internal static string SlotDirectory(string saveDirectory, bool devOverride)
    {
        ArgumentNullException.ThrowIfNull(saveDirectory);
        return devOverride ? Path.Combine(saveDirectory, OverrideSlot) : saveDirectory;
    }

    /// <summary>
    /// Why a file of the save's could not be read or written, in a few words a
    /// notice can show: another program holding it open, no permission, a full
    /// disk, and otherwise the system's own message with the folder's path taken
    /// out, since the log line has it.
    /// </summary>
    internal static string Why(Exception error, string folder, bool writing)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(folder);
        return error switch
        {
            UnauthorizedAccessException => writing ? "no permission to write there" : "no permission to read it",
            IOException io when IsHeldOpen(io) => "another program has it open",
            IOException io when IsDiskFull(io) => "the disk is full",
            _ => error.Message.Replace(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, "", StringComparison.OrdinalIgnoreCase)
                .Trim().TrimEnd('.'),
        };
    }

    /// <summary>A sharing or lock violation (Windows errors 32 and 33): another program has the file open and will not share it.</summary>
    private static bool IsHeldOpen(IOException error)
        => OperatingSystem.IsWindows() && ((error.HResult & 0xFFFF) is 32 or 33);

    /// <summary>A full disk (Windows errors 39 and 112).</summary>
    private static bool IsDiskFull(IOException error)
        => OperatingSystem.IsWindows() && ((error.HResult & 0xFFFF) is 39 or 112);
}
