namespace GameEngine.DistanceRPG;

/// <summary>
/// The old autosave's move into a slot of its own (the user's ruling of 2026-09-25
/// on the save slots, R5; SLOT DECISION 4): the campaign a build before the ruling
/// autosaved in the save folder itself is moved, at launch and without asking,
/// into the lowest slot free for it, so that every campaign is in a slot and saves
/// only there. Where it cannot be, it stays as the old autosave, listed as such.
/// <list type="bullet">
/// <item><strong>At every launch, before the title</strong>
/// (<c>DistanceRpgGame.BootCampaign</c>): a file set aside and later renamed back
/// into the save folder, as every set-aside notice there says to, is moved at the
/// next launch. Never on a dev-override launch, which makes no slots, and never
/// mid-session, so a campaign in play is never moved from under it.</item>
/// <item><strong>Only the four files of the campaign, the save first</strong>:
/// <see cref="SaveStore.FileName"/>, <see cref="SaveStore.BackupName"/>,
/// <see cref="SaveStore.ResumingName"/> and <see cref="SaveStore.UnsavedName"/>, in
/// that order, each only if it is there, each by a move under its own name that
/// refuses a name taken (<see cref="SaveStore.MoveInto"/>). Nothing is read,
/// rewritten or deleted, and the save document carries no slot, so no text
/// changes. The save goes first, since it is the file another program is likeliest
/// to hold: held, it stops the move before anything has moved. The resume marker
/// goes with the save it guards, so the slot lists <c>ITS LAST LOAD DID NOT
/// FINISH</c> and its next load sets that save aside as crashed; the unsaved-quit
/// marker goes so the title says it as the slot's. Files set aside, the temp file
/// and the dev override's slot stay where they are.</item>
/// <item><strong>Into the lowest slot free for it</strong>, 1 to the count, then
/// above it to <see cref="SaveSlots.Most"/>: one where none of the four names is
/// taken, by a file or a folder, and whose folder is no file. A slot holding a
/// stray marker is passed over, never cleared; files set aside are no obstacle. A
/// slot above the count is then listed beyond it, and played like any.</item>
/// <item><strong>All or nothing</strong>: before the first file moves, the save
/// folder gets <see cref="SaveStore.MovingName"/>, naming the slot, flushed to the
/// disk. A move that stops at any file is undone — what moved goes back, in
/// reverse, and the slot folder the try made goes too, while nothing is in it —
/// and the next slot is tried, since the failure may be the slot's. When
/// every slot's try fails, the failure is the save folder's own — its save held
/// by another program, or no rights there — and every write there would fail too,
/// so it takes no saves this session: listed as the old autosave, it loads
/// nothing until a launch can move it. A move that can be neither finished nor
/// undone leaves the campaign in two halves, and neither takes saves this session,
/// so no load runs on a split and no save forks it.</item>
/// <item><strong>A move a crash cut off is finished</strong> into the slot its
/// marker names, before anything is listed or loaded — unless that slot has since
/// taken one of the names still in the save folder, which makes the marker stale,
/// or the marker names no slot: each is dropped and logged, and a slot is picked
/// afresh. A marker that cannot be read at all is kept, since it may name the slot
/// the other half is in, and the save folder takes no saves this session. A finish
/// that stops is undone as a move is, and the halves the crash left stand; a crash
/// that came before any file moved left none, so that finish is a move that
/// stopped, and goes afresh.</item>
/// <item><strong>With no slot free</strong> — slots 1 to 9 all hold something —
/// nothing is tried, so nothing is known to fail: the old autosave stays, listed
/// and played in place, and the log says why.</item>
/// </list>
/// Engine-free, <c>System.IO</c> through the stores alone, and handed the slots: a
/// test hands it a temporary folder. What it has to say goes to the caller as log
/// lines and as the title's word.
/// </summary>
internal static class OldAutosave
{
    /// <summary>The files of the old autosave's campaign that move, in the order they move: the save first.</summary>
    private static readonly string[] Files = [SaveStore.FileName, SaveStore.BackupName, SaveStore.ResumingName, SaveStore.UnsavedName];

    /// <summary>
    /// Move the old autosave — the save folder's own <c>campaign.json</c> or
    /// <c>campaign.bak.json</c>, with its markers — into a slot of its own, or
    /// finish a move a crash cut off, and say what became of it: the title's word,
    /// led before the rest. A move that went through says
    /// <see cref="CampaignBoot.AutosaveMoved"/>; one every slot refused, or whose
    /// marker could not be written, read or cleared,
    /// <see cref="CampaignBoot.AutosaveNotMoved"/>, the save folder then taking no
    /// saves this session; one left in two halves,
    /// <see cref="CampaignBoot.AutosaveSplit"/>, both halves then taking none. No
    /// old autosave, and none that has room to move, say nothing: the listing shows
    /// what there is, and the log says why.
    /// </summary>
    /// <param name="slots">The save folder's slots, whose stores the move goes through: the session's own, so what a move refuses holds for the session, and what a store knew of a file moved goes with it.</param>
    /// <param name="report">Where the log lines go: every file moved, every try and why it failed.</param>
    internal static IReadOnlyList<string> MoveIntoASlot(SaveSlots slots, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var root = slots.Store(SaveSlots.OldAutosave);

        if (root.WasMoving && Finish(slots, root, report) is { } finished)
            return finished;

        if (!(root.HasSave || root.HasBackup))
        {
            LoneResumeLeft(root, report);
            return [];
        }

        var free = Enumerable.Range(1, SaveSlots.Most).Where(number => Free(slots.Store(number))).ToList();
        if (free.Count == 0)
        {
            report?.Invoke($"[Save] The autosave has no empty slot to move into (slots 1 to {SaveSlots.Most} all hold something) - it stays in {root.Folder}, listed as {SaveSlots.OldAutosaveLabel}");
            return [];
        }

        (string File, string Why)? failed = null;
        foreach (int number in free)
        {
            var slot = slots.Store(number);
            bool made = !slot.FolderExists;   // the try makes it, and an undone try takes it back
            try
            {
                root.MarkMoving(number);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                string why = SaveStore.Why(error, root.Folder, writing: true);
                report?.Invoke($"[Save] {root.MovingPath} could not be written, so nothing moves - {root.Folder} takes no saves this session: {error}");
                return [NotMoved(root, ItsMarker("written"), why, report)];
            }

            var moved = new List<string>();
            if (MoveEach(root, slot, Files.Where(root.Has).ToList(), moved, report) is not { } stopped)
                return Moved(root, slot, number, report);

            if (MoveBack(root, slot, moved, report) is { } stuck)
                return [Split(root, slot, number, stuck, report)];
            Unmade(slot, made, report);
            try
            {
                root.ClearMoving();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                string why = SaveStore.Why(error, root.Folder, writing: true);
                report?.Invoke($"[Save] {root.MovingPath} could not be cleared after the move into {slot.Folder} was undone - the next launch moves the autosave there; {root.Folder} takes no saves this session: {error}");
                return [NotMoved(root, ItsMarker($"cleared after {stopped.File} would not move"), why, report)];
            }
            report?.Invoke($"[Save] The move into {slot.Folder} was undone, nothing kept there: {stopped.File} would not move ({stopped.Why}) - the next slot is tried");
            failed = stopped;
        }

        // Every slot's try failed at the save folder's own files: whatever stopped
        // them stops a write there too, so its campaign takes no saves this session.
        var (file, last) = failed ?? throw new InvalidOperationException("No slot was tried, though one was free.");
        report?.Invoke($"[Save] The autosave could not move into any of slots {string.Join(", ", free)} - it stays in {root.Folder} and takes no saves this session; the next launch tries again");
        return [NotMoved(root, $"{file} could not be moved into a slot", last, report)];
    }

    /// <summary>A slot the old autosave may move into: none of its four names taken there, by a file or a folder, and no file where the slot's folder goes. A stray marker makes it unfit, and is left where it is.</summary>
    private static bool Free(SaveStore slot) => !slot.FolderIsAFile && !Files.Any(slot.Taken);

    /// <summary>
    /// Finish a move a crash cut off, before anything is listed or loaded: into the
    /// slot its marker names, what is left in the save folder, in the move's order.
    /// Null to go on and move afresh — the marker named no slot, or a stale one, or
    /// one the crash had moved nothing into and the finish could not move into
    /// either, and was dropped — and otherwise what to say. A marker that cannot be
    /// read is no marker that names no slot: it may name the slot the other half of
    /// a split is in, so it is kept for a launch that can read it, and the save
    /// folder, while it holds a save or a backup, takes no saves this session —
    /// were it moved afresh, a split would stand in two slots with nothing to join
    /// it.
    /// </summary>
    private static IReadOnlyList<string>? Finish(SaveSlots slots, SaveStore root, Action<string>? report)
    {
        int? named;
        try
        {
            named = root.ReadMoving();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {root.MovingPath} could not be read, so a move cut off cannot be finished by it - it is kept for a launch that can read it, and nothing moves. The save folder holds {Holding(root)}; "
                + $"a lone save or backup stands in {LoneHalves(slots)}: {error}");
            if (!(root.HasSave || root.HasBackup))
                return [];   // nothing left here to move, or to split: nothing to refuse, and nothing to say
            return [NotMoved(root, ItsMarker("read"), SaveStore.Why(error, root.Folder, writing: false), report)];
        }
        if (named is not { } number)
        {
            report?.Invoke($"[Save] {root.MovingPath} names no slot, so a move cut off cannot be finished by it - it is dropped, and a slot is picked afresh. The save folder holds {Holding(root)}; "
                + $"a lone save or backup stands in {LoneHalves(slots)} - one of them may be the other half of the autosave, to join by hand");
            return Drop(root, report);
        }

        var slot = slots.Store(number);
        if (!(root.HasSave || root.HasBackup))
        {
            // The save and the backup moved before the crash: what is left of the
            // move is a marker at most. The quit's speaks of the slot's campaign; the
            // resume marker, with no save beside it to guard, is left (M4).
            if (root.Has(SaveStore.UnsavedName) && !slot.Taken(SaveStore.UnsavedName))
                MoveEach(root, slot, [SaveStore.UnsavedName], [], report);
            LoneResumeLeft(root, report);
            Unmark(root, report);
            report?.Invoke($"[Save] The move into {slot.Folder} that a crash cut off had moved the save and its backup already: it is finished");
            return slot.HasSave || slot.HasBackup ? [CampaignBoot.AutosaveMoved(number)] : [];
        }

        var left = Files.Where(root.Has).ToList();
        if (left.Where(slot.Taken).ToList() is [_, ..] taken)
        {
            report?.Invoke($"[Save] {root.MovingPath} names {slot.Folder}, which holds {string.Join(", ", taken)} since - the marker is stale, dropped, and a slot is picked afresh");
            return Drop(root, report);
        }

        report?.Invoke($"[Save] A move of the autosave into {slot.Folder} was cut off: finishing it with {string.Join(", ", left)}");
        bool made = !slot.FolderExists;
        var moved = new List<string>();
        if (MoveEach(root, slot, left, moved, report) is { } stopped)
        {
            // What this launch moved goes back; the halves the crash left stand. A
            // crash that came before any file moved left no halves: the save folder
            // is whole and the slot holds nothing of it, so this is a move that
            // stopped like any other, and it goes afresh — the next slot tried, and
            // the save folder refused its writes when every try fails.
            string? stuck = MoveBack(root, slot, moved, report);
            if (stuck is null && !Files.Any(slot.Has))
            {
                Unmade(slot, made, report);
                report?.Invoke($"[Save] The move into {slot.Folder} that a crash cut off had moved nothing there, and stopped again: {stopped.File} would not move ({stopped.Why}) - the marker is dropped, and a slot is picked afresh");
                return Drop(root, report);
            }
            return [Split(root, slot, number, stuck ?? stopped.Why, report)];
        }
        return Moved(root, slot, number, report);
    }

    /// <summary>The move into slot <paramref name="number"/> went through: its marker cleared, the log told, and the title's word.</summary>
    private static IReadOnlyList<string> Moved(SaveStore root, SaveStore slot, int number, Action<string>? report)
    {
        Unmark(root, report);
        report?.Invoke($"[Save] The autosave moved into {slot.Folder}: its campaign saves in {SaveSlots.Label(number)} from now on");
        return [CampaignBoot.AutosaveMoved(number)];
    }

    /// <summary>
    /// A try undone takes back the slot folder it made (<see cref="SaveStore.MoveInto"/>
    /// makes it before a file moves), when nothing is in it, so the try leaves the
    /// save folder as it found it: a save folder whose own save another program
    /// holds fails every slot's try, and would otherwise leave slot-1 to slot-9
    /// behind, empty. A folder that was there before the try, or holds anything, is
    /// left as it is (<see cref="SaveStore.RemoveIfEmpty"/>: never recursive, so no
    /// file goes).
    /// </summary>
    private static void Unmade(SaveStore slot, bool made, Action<string>? report)
    {
        if (made && slot.RemoveIfEmpty())
            report?.Invoke($"[Save] {slot.Folder}, which the try made, is empty once it is undone, and is removed");
    }

    /// <summary>A resume marker standing alone in the save folder, beside no save or backup, guards nothing: it is left where it is, and logged.</summary>
    private static void LoneResumeLeft(SaveStore root, Action<string>? report)
    {
        if (root.WasResuming)
            report?.Invoke($"[Save] {root.ResumingPath} stands alone, beside no save or backup - it guards nothing, and is left where it is");
    }

    /// <summary>
    /// Move <paramref name="names"/> out of the save folder into
    /// <paramref name="slot"/>, in order, each added to <paramref name="moved"/> as
    /// it goes. Null when every one moved; otherwise the one that would not, and why
    /// in a notice's few words, and nothing after it is tried.
    /// </summary>
    private static (string File, string Why)? MoveEach(SaveStore root, SaveStore slot, IReadOnlyList<string> names, List<string> moved, Action<string>? report)
    {
        foreach (string name in names)
        {
            try
            {
                root.MoveInto(slot, name);
                moved.Add(name);
                report?.Invoke($"[Save] {Path.Combine(root.Folder, name)} moved into {slot.Folder}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                report?.Invoke($"[Save] {Path.Combine(root.Folder, name)} could not be moved into {slot.Folder} - it stays where it is: {error}");
                return (name, SaveStore.Why(error, root.Folder, writing: true));
            }
        }
        return null;
    }

    /// <summary>
    /// Undo a move that stopped: what <paramref name="moved"/> names goes back from
    /// <paramref name="slot"/> into the save folder, the last first, each by a move
    /// that refuses a name taken — deleting nothing. Every one is tried, so as much
    /// as can go back does. Null when all went back; otherwise why the first that
    /// would not, which leaves the campaign in two halves.
    /// </summary>
    private static string? MoveBack(SaveStore root, SaveStore slot, List<string> moved, Action<string>? report)
    {
        string? stuck = null;
        for (int i = moved.Count - 1; i >= 0; i--)
        {
            string name = moved[i];
            try
            {
                slot.MoveInto(root, name);
                report?.Invoke($"[Save] {Path.Combine(slot.Folder, name)} moved back into {root.Folder}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                report?.Invoke($"[Save] {Path.Combine(slot.Folder, name)} could not be moved back into {root.Folder} - it stays where it is: {error}");
                stuck ??= SaveStore.Why(error, slot.Folder, writing: true);
            }
        }
        return stuck;
    }

    /// <summary>
    /// The save folder's campaign would not move: it stays, and the save folder takes
    /// no saves this session — whatever stopped the move stops its writes — so its
    /// load, per a slot that refuses its writes, loads nothing and says why:
    /// <paramref name="what"/> failed, a file of the campaign's that would not move or
    /// the move's own marker (<see cref="ItsMarker"/>), for <paramref name="why"/>.
    /// </summary>
    private static string NotMoved(SaveStore root, string what, string why, Action<string>? report)
    {
        root.RefuseWrites($"{what} ({why})");
        report?.Invoke($"[Save] {root.Folder} takes no saves this session: {root.WritesRefused}");
        return CampaignBoot.AutosaveNotMoved(why);
    }

    /// <summary>
    /// The campaign stands in two halves, the save folder and <paramref name="slot"/>:
    /// the marker stays for the next launch to finish the move, and neither half
    /// takes saves this session, so neither loads.
    /// </summary>
    private static string Split(SaveStore root, SaveStore slot, int number, string why, Action<string>? report)
    {
        string refusal = $"the autosave was left half moved into {SaveSlots.Label(number).ToLowerInvariant()} ({why})";
        root.RefuseWrites(refusal);
        slot.RefuseWrites(refusal);
        report?.Invoke($"[Save] The autosave stands half in {root.Folder} and half in {slot.Folder} - neither takes saves this session, and {root.MovingPath} stays for the next launch to finish the move");
        return CampaignBoot.AutosaveSplit(number, why);
    }

    /// <summary>Drop a marker that cannot be followed: null to go on and move afresh; or, when it will not go, the word that the save folder stays and takes no saves, since no fresh marker could be written either.</summary>
    private static IReadOnlyList<string>? Drop(SaveStore root, Action<string>? report)
    {
        try
        {
            root.ClearMoving();
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {root.MovingPath} could not be dropped, so nothing moves: {error}");
            return [NotMoved(root, ItsMarker("cleared"), SaveStore.Why(error, root.Folder, writing: true), report)];
        }
    }

    /// <summary>What failed, when the move's own marker did, in the words of the save folder's refusal: <c>campaign.moving, the marker of its move, could not be written</c> — a file the player never made, so it says what it is.</summary>
    private static string ItsMarker(string failed) => $"{SaveStore.MovingName}, the marker of its move, could not be {failed}";

    /// <summary>Clear the marker of a move that went through; one that will not go is logged, and the next launch finds nothing left to move.</summary>
    private static void Unmark(SaveStore root, Action<string>? report)
    {
        try
        {
            root.ClearMoving();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {root.MovingPath} could not be cleared - the next launch finds the move done: {error}");
        }
    }

    /// <summary>What of the campaign's four files the save folder holds, for the log.</summary>
    private static string Holding(SaveStore root)
        => Files.Where(root.Has).ToList() is [_, ..] held ? string.Join(", ", held) : "none of the campaign's files";

    /// <summary>Every slot that holds a save with no backup, or a backup with no save — the halves a move cut off can leave — for the log.</summary>
    private static string LoneHalves(SaveSlots slots)
        => Enumerable.Range(1, SaveSlots.Most).Where(number => slots.Store(number) is var slot && slot.HasSave != slot.HasBackup)
            .Select(number => slots.Store(number).Folder).ToList() is [_, ..] lone ? string.Join(", ", lone) : "no slot";
}
