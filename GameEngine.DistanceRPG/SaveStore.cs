using System.Text;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The save's edge (1e, 1f): the one place a campaign's text is read from the
/// disk or written to it. <c>Logic/</c> turns a campaign into text and back
/// (<see cref="Logic.SaveCodec"/>, <see cref="Logic.SaveGame"/>) and never touches
/// a file. Engine-free — <c>System.IO</c> only — and handed its folder every
/// time: there is no parameterless constructor and no default folder here, so
/// nothing can read or write the player's real saves by accident. One store is
/// one slot (the user's ruling of 2026-09-25 on the save model): the game hands
/// it the player's save folder (<see cref="SaveDirectory"/>), which is the
/// autosave slot, a manual slot's folder inside it (<see cref="SaveSlots"/>), or
/// the save folder's dev-override slot (<see cref="SlotDirectory"/>); a test hands
/// it a temporary folder.
/// <para>
/// <strong>What lives in the folder.</strong> <see cref="FileName"/> is the
/// campaign. <see cref="BackupName"/> is the save before it, kept by every
/// write's atomic replace. <see cref="ResumingName"/> is there while a resume is
/// in flight, so a load that finds it knows the last resume never finished.
/// <see cref="UnsavedName"/> is there when the last time play was left the
/// campaign could not be saved here, so the title can say that what the slot
/// holds is from before then. And <c>campaign.&lt;reason&gt;-N.json</c> is a save
/// set aside — unreadable, crashed or abandoned, or, by a build before the user's
/// ruling on the save slots, replaced to make room for a manual slot's campaign —
/// which nothing here ever deletes or writes over; the one move back is
/// <see cref="PutBack"/>'s, of a file set aside for a new campaign that never
/// began.
/// </para>
/// <para>
/// <strong>A write never costs the save before it.</strong> The text goes to
/// <see cref="TempName"/> first, flushed to the disk. Only then does it replace
/// the save in one step, the old save becoming the backup. A write that fails at
/// any point leaves <see cref="FileName"/> exactly as it was. The one delete is
/// <see cref="Empty"/>, which refuses any folder but a dev-override slot. The two
/// markers, which are no saves, are the only other files this class removes.
/// </para>
/// <para>
/// <strong>What this session knows the files hold</strong>
/// (<see cref="SaveVouched"/>, <see cref="BackupVouched"/>): a flag per file,
/// true for a text this session wrote there or read and found a save
/// (<see cref="Vouch"/>), carried to the backup by the replace. The write guard
/// (<c>CampaignBoot.Save</c>) reads a file before a write only when its flag is
/// down, so a session that writes its own saves reads nothing back.
/// </para>
/// </summary>
internal sealed class SaveStore
{
    /// <summary>The campaign's save.</summary>
    public const string FileName = "campaign.json";

    /// <summary>The save before the last one, kept by the atomic replace.</summary>
    public const string BackupName = "campaign.bak.json";

    /// <summary>There while a resume is in flight: a load that finds it knows the resume before it never finished.</summary>
    public const string ResumingName = "campaign.resuming";

    /// <summary>There when the last time play was left the campaign could not be saved in this slot: the save beside it is from before then, and the title says so and removes it.</summary>
    public const string UnsavedName = "campaign.unsaved";

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

    /// <summary>The unsaved-quit marker, in full.</summary>
    public string UnsavedPath => Path.Combine(Folder, UnsavedName);

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

    /// <summary>Whether this session knows <see cref="FileName"/> holds a save: it wrote the text there, or read it and found one (<see cref="Vouch"/>). Down for a file set aside, and for one never read.</summary>
    internal bool SaveVouched { get; private set; }

    /// <summary>Whether this session knows <see cref="BackupName"/> holds a save: a save it knew, which a write's replace made the backup, or one it read and found a save (<see cref="Vouch"/>).</summary>
    internal bool BackupVouched { get; private set; }

    /// <summary>
    /// This session has read the save — or, with <paramref name="backup"/>, the
    /// backup — whole and found a save there, so a write need not read it again.
    /// Only a caller that parsed the text says so: a read alone never does, since
    /// a text read is no save until it is parsed.
    /// </summary>
    internal void Vouch(bool backup)
    {
        if (backup)
            BackupVouched = true;
        else
            SaveVouched = true;
    }

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
    /// given. The save is then one this session knows (<see cref="SaveVouched"/>),
    /// and the backup is known exactly when the save it was is.
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
        {
            File.Replace(TempPath, SavePath, BackupPath, ignoreMetadataErrors: true);
            BackupVouched = SaveVouched;   // the save it replaced is the backup now
        }
        else
        {
            File.Move(TempPath, SavePath);
        }
        SaveVouched = true;
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
            if (backup)
                BackupVouched = false;   // nothing is there now
            else
                SaveVouched = false;
            return target;
        }
    }

    /// <summary>
    /// Put a file set aside back where it was — as the backup with
    /// <paramref name="backup"/>, and otherwise as the save — when what it was set
    /// aside for never happened: a new campaign whose first save did not go
    /// through, or whose setting aside stopped at a file that would not move
    /// (<c>CampaignBoot.PutBack</c>). A move, never a copy, and it refuses a name
    /// that is taken, so nothing is deleted or written over. What this session
    /// knew of the file went when it was set aside and does not come back: the next
    /// write reads it first, as it reads any file it does not know.
    /// </summary>
    /// <param name="setAside">The file's full path: one set aside in this folder.</param>
    /// <param name="backup">Put it back as the backup rather than the save.</param>
    /// <exception cref="ArgumentException"><paramref name="setAside"/> is not in this folder: a file is only ever put back where it was set aside.</exception>
    /// <exception cref="IOException">The name is taken, or the move failed; the file stays where it was set aside.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission to move it; it stays where it was set aside.</exception>
    public void PutBack(string setAside, bool backup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setAside);
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(setAside)), Path.TrimEndingDirectorySeparator(Folder), StringComparison.Ordinal))
            throw new ArgumentException($"{setAside} is not in {Folder}: a file is put back only where it was set aside.", nameof(setAside));
        File.Move(setAside, backup ? BackupPath : SavePath, overwrite: false);
        if (backup)
            BackupVouched = false;   // a file this session has not read
        else
            SaveVouched = false;
    }

    /// <summary>Whether the resume marker is there: the last load of this slot began a resume that never finished.</summary>
    public bool WasResuming => File.Exists(ResumingPath);

    /// <summary>Write the resume marker: a resume is in flight until <see cref="ClearResuming"/>.</summary>
    public void MarkResuming()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(ResumingPath,
            "A resume is in flight. A load that finds this file sets the save it was resuming aside as crashed.\n", Utf8);
    }

    /// <summary>Remove the resume marker, if it is there: the resume finished, or nothing is being resumed.</summary>
    public void ClearResuming()
    {
        if (File.Exists(ResumingPath))
            File.Delete(ResumingPath);
    }

    /// <summary>What the last quit wrote in the unsaved-quit marker (<see cref="MarkUnsaved"/>), or null when there is none: that quit saved, or none has left one here. A marker that is there and cannot be read throws, as the file system says why.</summary>
    public string? ReadUnsaved() => ReadIfThere(UnsavedPath);

    /// <summary>
    /// Write the unsaved-quit marker: leaving play could not save the campaign in
    /// this slot, for <paramref name="why"/>, so what the slot holds is from before
    /// then, and the title says so the next time it opens — before anything is
    /// loaded — and removes this file. A marker, never a save: it is written even
    /// while writes are refused (<see cref="RefuseWrites"/>), since it can take no
    /// save's place.
    /// </summary>
    public void MarkUnsaved(string why)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(why);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(UnsavedPath,
            $"Leaving play could not save the campaign in this folder: {why}\nWhat this folder holds is from before that. The title says so and removes this file.\n", Utf8);
    }

    /// <summary>Remove the unsaved-quit marker, if it is there: the title has read it, or NEW CAMPAIGN has set aside the campaign it spoke of.</summary>
    public void ClearUnsaved()
    {
        if (File.Exists(UnsavedPath))
            File.Delete(UnsavedPath);
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
        SaveVouched = BackupVouched = false;
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
