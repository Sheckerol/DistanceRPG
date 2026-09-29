using System.Globalization;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What a slot holds, as the listing read it (<see cref="SaveListing.Read"/>):
/// nothing; a save that reads; a save that cannot be read beside a backup that
/// can — or no save at all beside one — which a load resumes from the backup; or
/// files none of which reads.
/// </summary>
public enum SlotState
{
    /// <summary>No save and no backup: nothing to load, and a slot free to save into.</summary>
    Empty,

    /// <summary>The save reads: a load would try it first.</summary>
    Readable,

    /// <summary>The save cannot be read, or is not there, and the backup reads: a load would set the save aside and resume the backup, one save older.</summary>
    FromBackup,

    /// <summary>Something is there and nothing of it reads: a load would set it aside and bring nothing back.</summary>
    Unreadable,
}

/// <summary>
/// What a slot's save says of itself, read off its document by the codec and
/// never restored (a run in progress would regenerate its floors): when it was
/// saved, the runs counted, and — mid-run — the dungeon, the floor and the turn.
/// Enough to choose a slot by.
/// </summary>
/// <param name="SavedAt">When it was saved (<see cref="SaveFile.SavedAt"/>), or null for a save that records no time, or none that is one.</param>
/// <param name="RunCounter">The runs that have counted.</param>
/// <param name="DungeonId">The dungeon a run in progress is in, by its content id; null between runs.</param>
/// <param name="Floor">The floor number the party stands on, counting from 1; null between runs.</param>
/// <param name="Turn">The visit's turn; null between runs.</param>
public sealed record SlotHeader(DateTimeOffset? SavedAt, int RunCounter, string? DungeonId, int? Floor, int? Turn);

/// <summary>
/// One slot of the listing (<see cref="SaveListing"/>).
/// </summary>
/// <param name="Number">The slot's number: 1 up, or 0 the old autosave, the save folder itself.</param>
/// <param name="State">What it holds.</param>
/// <param name="Header">The header of the file a load would bring back — the save's, or the backup's when the slot is <see cref="SlotState.FromBackup"/> — or null when nothing reads.</param>
/// <param name="Unfinished">Its resume marker is there: the last load of it never finished, and the next sets the save it guarded aside as crashed.</param>
/// <param name="Beyond">A slot above <c>tuning.json</c>'s <c>SaveSlots</c>: kept, listed, loaded, and saved into by the campaign played from it; never offered to NEW CAMPAIGN.</param>
/// <param name="AheadOfClock">Its time is more than a day ahead of the clock the listing was read at: shown, and counted as no time when the newest save is chosen.</param>
public sealed record SlotEntry(int Number, SlotState State, SlotHeader? Header, bool Unfinished, bool Beyond, bool AheadOfClock);

/// <summary>
/// Every slot of the save folder and what each holds (the user's rulings of
/// 2026-09-25 on the save model, R2, and on the save slots, R5): the old autosave
/// while the save folder itself holds a save or a backup — one the launch could
/// not move into a slot (<see cref="OldAutosave.MoveIntoASlot"/>) — the slots up to
/// <c>tuning.json</c>'s count — the empty ones included — and any above it that
/// hold a save, in number order; and what the listing could not say in a row.
/// <list type="bullet">
/// <item><strong>It reads and never moves</strong>: each slot's files are read
/// and their documents parsed for a header — never restored, never moved, never
/// deleted, never written. A save that reads but will not restore (content gone,
/// a contradiction) lists as <see cref="SlotState.Readable"/>, and the load sets
/// it aside.</item>
/// <item><strong>The most recent save is chosen by its own time</strong>
/// (<see cref="Newest"/>): the latest <see cref="SaveFile.SavedAt"/> among the
/// slots that would load something; a save with no time is older than any with
/// one; a time more than a day ahead of the clock counts as none, so a hand edit
/// or a clock once set wrong can never pin the choice on one slot through every
/// later autosave; and a tie goes to the lower number — the old autosave's, 0,
/// the lowest while it is listed — by number, never by any folder's order. File
/// times are never read: a copy or a sync rewrites them, and a test cannot fix
/// them.</item>
/// </list>
/// Public, as <see cref="ResumeView"/> is: a scene's constructor and a host's
/// interface hand it across. Its reader stays internal, as the stores are.
/// </summary>
/// <param name="Slots">Every slot listed, in number order: the old autosave first, while it is listed.</param>
/// <param name="Notices">What the rows cannot say: a file the file system would not let the game read, and why.</param>
public sealed record SaveListing(IReadOnlyList<SlotEntry> Slots, IReadOnlyList<string> Notices)
{
    /// <summary>How far ahead of the clock a save's time may be and still count: a day, for a save carried from a machine whose clock runs a little fast.</summary>
    public static readonly TimeSpan ClockSlack = TimeSpan.FromDays(1);

    /// <summary>
    /// The most recent save — what CONTINUE loads — or null when no slot would load
    /// anything: the latest time among the slots whose save, or failing that whose
    /// backup, reads; a save with no time, or one ahead of the clock, older than any
    /// with one; a tie to the lower number. A slot beyond the count is chosen like
    /// any other, and so is the old autosave while it is listed.
    /// </summary>
    public SlotEntry? Newest
    {
        get
        {
            SlotEntry? newest = null;
            foreach (var slot in Slots.OrderBy(slot => slot.Number))
            {
                if (slot.State is not (SlotState.Readable or SlotState.FromBackup))
                    continue;
                if (newest is null || Compare(TimeOf(slot), TimeOf(newest)) > 0)
                    newest = slot;
            }
            return newest;
        }
    }

    /// <summary>
    /// The slot a new campaign is offered first — where NEW CAMPAIGN's list starts,
    /// and where <c>--new-campaign</c> begins one, asking nothing: the lowest
    /// <see cref="SlotState.Empty"/> slot from 1 to the count, so an ENTER pressed
    /// without looking sets nothing aside while a slot is free; with none empty,
    /// slot 1. Never the old autosave, and never a slot beyond the count (the user's
    /// ruling of 2026-09-25 on the save slots, R5). A slot holding only files set
    /// aside is empty: nothing of it would load.
    /// </summary>
    public int NewCampaignSlot
        => Slots.Where(slot => slot.Number != SaveSlots.OldAutosave && !slot.Beyond && slot.State == SlotState.Empty)
            .Select(slot => slot.Number)
            .DefaultIfEmpty(FirstSlot)
            .Min();

    /// <summary>The first slot a campaign can begin in: slot 1.</summary>
    private const int FirstSlot = 1;

    /// <summary>The time a slot's choice is made by: its save's, unless there is none or it is ahead of the clock.</summary>
    private static DateTimeOffset? TimeOf(SlotEntry slot) => slot.AheadOfClock ? null : slot.Header?.SavedAt;

    /// <summary>Times compared as instants, no time the oldest of all.</summary>
    private static int Compare(DateTimeOffset? a, DateTimeOffset? b)
        => (a, b) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            ({ } x, { } y) => x.UtcDateTime.CompareTo(y.UtcDateTime),
        };

    /// <summary>
    /// Read every slot of <paramref name="slots"/>: the old autosave — the save
    /// folder itself, where a build before the user's ruling on the save slots
    /// (R5) autosaved — only while it holds a save or a backup, which the launch
    /// could not move into a slot (<see cref="OldAutosave.MoveIntoASlot"/>, run
    /// before any listing, a move a crash cut off finished first); slots 1 to the
    /// count; and those beyond it that hold a save (<see cref="SaveSlots.Beyond"/>).
    /// Each slot's save and backup are read and parsed for their headers, never
    /// restored; a file the file system will not let the game read is listed as
    /// not reading, and said in <see cref="Notices"/>. Nothing is moved, deleted
    /// or written.
    /// </summary>
    /// <param name="slots">The save folder's slots.</param>
    /// <param name="now">The clock's time, the edge's: a save's time more than <see cref="ClockSlack"/> past it counts as none.</param>
    /// <param name="report">Where the log lines go: what each slot holding anything holds, and every file that could not be read.</param>
    internal static SaveListing Read(SaveSlots slots, DateTimeOffset now, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var notices = new List<string>();
        var root = slots.Store(SaveSlots.OldAutosave);
        IEnumerable<int> numbers = root.HasSave || root.HasBackup ? [SaveSlots.OldAutosave] : [];
        var entries = numbers.Concat(Enumerable.Range(1, slots.Count)).Concat(slots.Beyond())
            .Select(number => ReadSlot(slots, number, notices, report))
            .Select(slot => new SlotEntry(
                slot.Number, slot.State, slot.Header, slot.Unfinished,
                Beyond: slot.Number > slots.Count,
                AheadOfClock: slot.Header?.SavedAt is { } at && at.UtcDateTime > (now + ClockSlack).UtcDateTime))
            .ToList();
        foreach (var entry in entries.Where(entry => entry.State != SlotState.Empty))
            report?.Invoke($"[Save] {SaveSlots.Label(entry.Number)}: {Describe(entry)}");
        return new SaveListing(entries, notices);
    }

    /// <summary>A slot as read: what it holds, the header of what would load, and whether its last load finished.</summary>
    private sealed record Found(int Number, SlotState State, SlotHeader? Header, bool Unfinished);

    private static Found ReadSlot(SaveSlots slots, int number, List<string> notices, Action<string>? report)
    {
        var store = slots.Store(number);
        bool unfinished = store.WasResuming;
        bool saveThere = store.HasSave, backupThere = store.HasBackup;
        if (!saveThere && !backupThere)
            return new Found(number, SlotState.Empty, null, unfinished);

        string? save = Text(store, number, backup: false, notices, report);
        if (save is not null && HeaderOf(save) is { } header)
            return new Found(number, SlotState.Readable, header, unfinished);

        string? backup = Text(store, number, backup: true, notices, report);
        if (backup is not null && HeaderOf(backup) is { } older)
            return new Found(number, SlotState.FromBackup, older, unfinished);
        return new Found(number, SlotState.Unreadable, null, unfinished);
    }

    /// <summary>The save's, or the backup's, text; null when it is not there, or when the file system will not let it be read — which is said, since no row can say why.</summary>
    private static string? Text(SaveStore store, int number, bool backup, List<string> notices, Action<string>? report)
    {
        try
        {
            return backup ? store.ReadBackup() : store.Read();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            string file = backup ? SaveStore.BackupName : SaveStore.FileName;
            report?.Invoke($"[Save] {(backup ? store.BackupPath : store.SavePath)} could not be read to list it: {error}");
            notices.Add($"{SaveSlots.Label(number)}: {file.ToUpperInvariant()} COULD NOT BE READ ({SaveStore.Why(error, store.Folder, writing: false).ToUpperInvariant()})");
            return null;
        }
    }

    /// <summary>The header of a save's text, or null when the codec refuses it. A time that is no time is no time here; the load refuses the save for it.</summary>
    private static SlotHeader? HeaderOf(string text)
    {
        SaveFile file;
        try
        {
            file = SaveCodec.Read(text);
        }
        catch (Exception)
        {
            return null;   // no save this build reads: a malformed text, a newer build's
        }

        DateTimeOffset? savedAt;
        try
        {
            savedAt = SaveGame.SavedAtOf(file);
        }
        catch (SaveFormatException)
        {
            savedAt = null;
        }
        var visit = file.Campaign.Visit;
        int? floor = visit is { FloorIndex: >= 0 and < int.MaxValue } ? visit.FloorIndex + 1 : null;
        return new SlotHeader(savedAt, file.Campaign.RunCounter, visit?.DungeonId, floor, visit?.Turn);
    }

    /// <summary>A slot, for the log: what it holds, when it was saved and where the campaign stands.</summary>
    private static string Describe(SlotEntry entry)
    {
        string state = entry.State switch
        {
            SlotState.Readable => "a save",
            SlotState.FromBackup => "a save that cannot be read, beside a backup that can",
            _ => "files none of which can be read",
        };
        string where = entry.Header is not { } header
            ? ""
            : $", saved {(header.SavedAt is { } at ? SaveGame.TimeText(at) : "at no recorded time")}{(entry.AheadOfClock ? " (ahead of this clock)" : "")}, run counter {header.RunCounter.ToString(CultureInfo.InvariantCulture)}"
                + (header.DungeonId is { } dungeon ? $", a run in progress in {dungeon}, floor {header.Floor?.ToString(CultureInfo.InvariantCulture) ?? "?"}, turn {header.Turn?.ToString(CultureInfo.InvariantCulture) ?? "?"}" : ", between runs");
        return state + where
            + (entry.Unfinished ? " - its last load did not finish" : "")
            + (entry.Beyond ? " - beyond the slot count, kept, and saved into by its own campaign" : "");
    }
}
