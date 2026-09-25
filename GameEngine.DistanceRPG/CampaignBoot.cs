using System.Diagnostics.CodeAnalysis;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// What a launch was asked for: the dev overrides as the game parsed them — a
/// dungeon to boot straight into, a seed for the first run, each null when
/// unset or not valid — and <c>--new-campaign</c>.
/// </summary>
/// <param name="DungeonId">The <c>DISTANCERPG_DUNGEON</c> override, when it names a dungeon.</param>
/// <param name="Seed">The <c>DISTANCERPG_SEED</c> override, when it is a map seed.</param>
/// <param name="NewCampaign">The launch sets the campaign aside and starts fresh (<c>--new-campaign</c>).</param>
internal sealed record BootRequest(string? DungeonId = null, long? Seed = null, bool NewCampaign = false)
{
    /// <summary>Whether this is a dev-override launch: a fresh campaign in a slot of its own, which never reads or writes the campaign's save.</summary>
    public bool DevOverride => DungeonId is not null || Seed is not null;
}

/// <summary>
/// The campaign a launch plays, and what to say about it (1f): a dev-override
/// launch's scratch campaign (<see cref="CampaignBoot.BootOverride"/>), the one
/// <c>--new-campaign</c> begins (<see cref="CampaignBoot.Abandon"/>), or — put
/// together from the autosave's load (<see cref="CampaignBoot.ResumeOrBegin"/>) —
/// the campaign the load brought back, or the new one begun when it brought
/// nothing; the view its save carried; for a dev-override launch,
/// the dungeon to boot straight into and the first run's seed; and the notices
/// the first screen shows, in this order and whole: that the campaign resumed or
/// a new one begins, that the numbers changed since it was saved, and that a save
/// was set aside, where, and how to try it again.
/// </summary>
internal sealed record BootResult(CampaignState Campaign, bool Resumed, ViewDto? View, string? StraightInto, long? FirstSeed,
    IReadOnlyList<string> Notices);

/// <summary>
/// What loading one slot brought back (<see cref="CampaignBoot.Load"/>): the
/// campaign and the view its save carried — or neither, when the slot held
/// nothing that would load — and what to say, in order and whole: that the
/// campaign resumed, from which slot or its backup; that the last quit could not
/// write it; that the numbers changed since it was saved; and every file set
/// aside on the way, where, and how to try it again. A load never makes a
/// campaign: what follows one that brings nothing back is the caller's to decide
/// (the launch's: <see cref="CampaignBoot.ResumeOrBegin"/>).
/// </summary>
internal sealed record SlotLoad(CampaignState? Campaign, ViewDto? View, IReadOnlyList<string> Notices)
{
    /// <summary>Whether a campaign came back.</summary>
    [MemberNotNullWhen(true, nameof(Campaign))]
    public bool Loaded => Campaign is not null;
}

/// <summary>
/// The campaign's life on the disk, between the edge (<see cref="SaveStore"/>,
/// one slot each, and <see cref="SaveSlots"/>) and <c>Logic/</c>'s save document
/// (<see cref="SaveGame"/>, <see cref="SaveCodec"/>): a slot's load
/// (<see cref="Load"/>), what the launch plays from the autosave's load
/// (<see cref="ResumeOrBegin"/>), the two launches that load nothing — a dev
/// override's scratch campaign (<see cref="BootOverride"/>) and
/// <c>--new-campaign</c> (<see cref="Abandon"/>) — a save that never throws and
/// never costs a file
/// (<see cref="Save"/>), and the save a quit takes mid-run
/// (<see cref="SaveOnQuit"/>). Engine-free, so every case is tested on a
/// temporary folder with no window; what it has to say goes to the caller as log
/// lines (<c>report</c>) and as notices, and the game draws and logs them.
/// <list type="bullet">
/// <item><strong>Loading is per slot, and resumes</strong> (1f; the user's ruling
/// of 2026-09-25 on the save model, R2): a slot's <see cref="SaveStore.FileName"/>
/// is resumed, and the load says so and from which slot — between runs, or in the
/// run in progress it holds, which the game resumes in the dungeon at the saved
/// floor and turn (Phase 5.8). A save that cannot be read — malformed, written by
/// a newer build, naming content that is gone, holding a count past what play
/// reaches, contradicting itself (R3(b)) or keeping a time that is no time — is
/// set aside, never deleted or written over, and the backup is tried. A save
/// whose load crashed the game last time — the resume marker left behind — is set
/// aside the same way. Every set-aside notice names the file's full path and ends
/// <see cref="TryAgain"/>. A slot that brings nothing back makes no campaign: the
/// caller decides what follows — the launch begins one, saying so ahead of what
/// was set aside (<see cref="ResumeOrBegin"/>).</item>
/// <item><strong>No write costs a file</strong> (1f): a slot can be written without
/// being loaded, so before any write a <see cref="SaveStore.FileName"/> there
/// that no save can be read from is set aside first, and so is such a backup the
/// replace would otherwise rotate away — the codec's refusal decides, never a
/// listing's word on the slot. And a write clears its slot's resume marker: the
/// file it guarded is gone.</item>
/// <item><strong>A quit that could not save is said at the next launch</strong>
/// (1f): no screen follows a quit, so the quit's own door leaves
/// <see cref="SaveStore.UnsavedName"/> beside the save when it cannot save
/// (<see cref="SaveOnQuit"/>), and the load that finds it says, under the
/// resumed line, that what resumes is from before that quit
/// (<see cref="QuitUnsaved"/>).</item>
/// <item><strong>A different tuning is said, and plays on</strong> (§5.3,
/// PHASE5-084): "silently changing a saved game's rules underneath the player is
/// the failure to avoid", so a campaign saved under other numbers resumes with
/// <see cref="TuningChanged"/> on its first screen.</item>
/// <item><strong>The dev overrides play in their own slot</strong>: a launch
/// with <c>DISTANCERPG_DUNGEON</c> or <c>DISTANCERPG_SEED</c> empties
/// <see cref="SaveStore.OverrideSlot"/> and plays a fresh campaign there, so it
/// is as repeatable as it ever was and the campaign — the autosave and every
/// manual slot — is untouched.</item>
/// </list>
/// </summary>
internal static class CampaignBoot
{
    /// <summary>A resume from the autosave's backup, one save older than the campaign's own file.</summary>
    internal const string ResumedFromBackup = BetweenRunsHud.Resumed + " FROM ITS BACKUP, ONE SAVE OLDER";

    /// <summary>A save was there and could not be resumed, or was set aside for a new campaign.</summary>
    internal const string NewCampaignBegins = "A NEW CAMPAIGN BEGINS";

    /// <summary>PHASE5-084: the save's tuning hash is not the loaded tuning's.</summary>
    internal const string TuningChanged = "THE NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS ON UNDER THE NEW ONES";

    /// <summary>How every set-aside notice ends: how to try the file again.</summary>
    internal const string TryAgain = "RENAME IT TO CAMPAIGN.JSON TO TRY AGAIN";

    /// <summary>The last quit could not save the campaign (<see cref="SaveStore.UnsavedName"/>): what resumes is a save from before it.</summary>
    internal const string QuitUnsaved = "THE CAMPAIGN COULD NOT BE SAVED AS THE GAME LAST QUIT - IT RESUMES FROM AN EARLIER SAVE, BEHIND WHERE YOU LEFT IT";

    /// <summary>A dev-override launch's word on the campaign it plays.</summary>
    internal const string OverrideNotice = "A DEV OVERRIDE LAUNCH - A SCRATCH CAMPAIGN, SAVED APART - YOUR CAMPAIGN IS UNTOUCHED";

    /// <summary>The store a launch plays on: the dev-override slot inside <paramref name="saveDirectory"/> for an override launch, and the save folder itself otherwise.</summary>
    internal static SaveStore StoreFor(string saveDirectory, BootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SaveStore(SaveStore.SlotDirectory(saveDirectory, request.DevOverride));
    }

    /// <summary>
    /// What loading a slot says first, in green: <c>CAMPAIGN RESUMED</c> for the
    /// autosave, <c>CAMPAIGN RESUMED FROM SLOT 2</c> for a manual one; and from a
    /// backup, <see cref="ResumedFromBackup"/>, or <c>CAMPAIGN RESUMED FROM THE
    /// BACKUP OF SLOT 2, ONE SAVE OLDER</c>. Each starts with
    /// <see cref="BetweenRunsHud.Resumed"/>, so each is news, not a warning.
    /// </summary>
    internal static string Resumed(int number, bool backup)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        if (number == SaveSlots.Autosave)
            return backup ? ResumedFromBackup : BetweenRunsHud.Resumed;
        return backup
            ? $"{BetweenRunsHud.Resumed} FROM THE BACKUP OF {SaveSlots.Label(number)}, ONE SAVE OLDER"
            : $"{BetweenRunsHud.Resumed} FROM {SaveSlots.Label(number)}";
    }

    /// <summary>
    /// Load slot <paramref name="number"/> (<paramref name="slot"/>), Boot's resume
    /// as it always was, in this order:
    /// <list type="number">
    /// <item>A marker the last quit left because it could not save
    /// (<see cref="SaveStore.UnsavedName"/>) is read and cleared, so it is said
    /// once: a campaign resumed says, under the resumed line, that it is from
    /// before that quit (<see cref="QuitUnsaved"/>). One set aside has nothing to
    /// say it of.</item>
    /// <item>A resume marker left behind means the last load crashed the game: the
    /// save it guarded — the save, or the backup when there was no save — is set
    /// aside as <c>crashed</c>.</item>
    /// <item>The save is resumed; failing that it is set aside as
    /// <c>unreadable</c>, and the backup is resumed; failing that it is set aside
    /// too, and nothing is brought back. With no save there at all but a backup —
    /// a save set aside by an earlier load and nothing written since, or one
    /// deleted by hand — the backup is resumed, so a campaign is never lost to a
    /// missing file while its backup stands. The resume marker is written before
    /// each resume is tried and left for the game to clear once the resumed screen
    /// is up (<see cref="SaveStore.ClearResuming"/>).</item>
    /// </list>
    /// A restore that throws anything — a <see cref="SaveFormatException"/> or a
    /// fault — is logged whole and treated as unreadable, so a player is never
    /// stuck on a crash. A campaign resumed with a run in progress comes back with
    /// its visit (<see cref="CampaignState.Visit"/>), and the game plays it on in
    /// the dungeon; a crash there is the marker's to catch. A file that could not
    /// be read and cannot be moved either is left where it is, and the store
    /// refuses every write this session (<see cref="SaveStore.RefuseWrites"/>), so
    /// nothing takes its place. The file a load resumes is one this session knows
    /// holds a save (<see cref="SaveStore.Vouch"/>).
    /// <para>
    /// <strong>It never makes a campaign.</strong> A slot that brings nothing back
    /// comes back with no campaign and the notices of what was set aside — none for
    /// a slot that held nothing — and what follows is the caller's.
    /// </para>
    /// </summary>
    /// <param name="slot">The slot's store (<see cref="SaveSlots.Store"/>).</param>
    /// <param name="content">The content loaded, which must be <see cref="GameContent.Current"/>: a campaign is restored under it.</param>
    /// <param name="report">Where the log lines go: every file read, set aside or found wanting, with the whole error.</param>
    /// <param name="number">The slot's number, for what the load says: <see cref="SaveSlots.Autosave"/>, or a manual slot's.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    internal static SlotLoad Load(SaveStore slot, GameContent content, Action<string>? report = null, int number = SaveSlots.Autosave)
    {
        ArgumentNullException.ThrowIfNull(slot);
        RequireCurrent(content);
        ArgumentOutOfRangeException.ThrowIfNegative(number);

        bool quitUnsaved = TakeUnsaved(slot, report);
        var setAside = new List<string>();
        if (slot.WasResuming)
        {
            // The save it guarded is the one a resume reads first: the save when
            // there is one, and the backup when the resume had only that to read.
            bool save = slot.HasSave;
            if (save || slot.HasBackup)
            {
                report?.Invoke($"[Save] {slot.ResumingPath} was left behind: resuming {(save ? slot.SavePath : slot.BackupPath)} crashed the game last time");
                if (!SetAside(slot, backup: !save, "crashed", "crashed the game the last time it was resumed", setAside, report))
                    return new SlotLoad(null, null, setAside);
            }
            Unmark(slot, report);
        }

        foreach (bool backup in new[] { false, true })
        {
            if (!(backup ? slot.HasBackup : slot.HasSave))
                continue;
            Mark(slot, report);   // before the restore, so even a restore that takes the process down is caught next time
            if (TryResume(slot, backup, content, report, out var loaded, out string why))
            {
                slot.Vouch(backup);

                // What plays first — and that it is from before the last quit, when
                // that quit could not save — then the numbers it plays under
                // (PHASE5-084 wants that on the screen, whatever else there is to
                // say), then what became of any save set aside on the way.
                var notices = new List<string> { Resumed(number, backup) };
                if (quitUnsaved)
                    notices.Add(QuitUnsaved);
                if (loaded.TuningChanged)
                    notices.Add(TuningChanged);
                notices.AddRange(setAside);
                return new SlotLoad(loaded.Campaign, loaded.View, notices);
            }
            if (!SetAside(slot, backup, "unreadable", $"could not be read ({why})", setAside, report))
                break;   // stuck where it is: writes are refused, and nothing else is resumed
        }

        Unmark(slot, report);
        report?.Invoke(setAside.Count == 0
            ? $"[Save] No campaign to load in {slot.Folder}"
            : $"[Save] Nothing could be loaded from {slot.Folder}");
        return new SlotLoad(null, null, setAside);
    }

    /// <summary>
    /// What the launch plays from the autosave's load (1f's last bullet: the launch
    /// as it always was, until the title replaces it): the campaign
    /// <paramref name="load"/> brought back, resumed, with the view its save
    /// carried and the load's word as the load said it; or, when it brought
    /// nothing back, a new campaign (<see cref="CampaignState.Start"/>) — a load
    /// makes none of its own — whose word is <see cref="NewCampaignBegins"/>
    /// followed by every file the load set aside, in the load's order, or nothing
    /// at all when the slot held nothing, the first launch ever. Reads, writes and
    /// moves no file: the load has done all of that.
    /// </summary>
    /// <param name="load">What <see cref="Load"/> brought back from the autosave slot.</param>
    /// <param name="content">The content loaded, which must be <see cref="GameContent.Current"/>: a new campaign is built under it.</param>
    /// <param name="report">Where the log line goes when a new campaign begins.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    internal static BootResult ResumeOrBegin(SlotLoad load, GameContent content, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(load);
        RequireCurrent(content);
        if (load.Campaign is { } loaded)
            return new BootResult(loaded, Resumed: true, load.View, StraightInto: null, FirstSeed: null, load.Notices);

        report?.Invoke(load.Notices.Count == 0
            ? "[Save] Nothing saved yet: a new campaign begins"
            : "[Save] Nothing could be loaded: a new campaign begins");
        IReadOnlyList<string> said = load.Notices.Count == 0 ? [] : [NewCampaignBegins, .. load.Notices];
        return new BootResult(CampaignState.Start(content), Resumed: false, View: null, StraightInto: null, FirstSeed: null, said);
    }

    /// <summary>
    /// A dev-override launch: its slot (<paramref name="store"/>, from
    /// <see cref="StoreFor"/>) emptied, a fresh campaign, straight into the dungeon
    /// when one is named, its first run on the seed when one is. The campaign's own
    /// saves — the autosave and every manual slot — are never opened.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="request"/> is no dev override, or <paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="store"/> is not a dev-override slot: it is never emptied.</exception>
    internal static BootResult BootOverride(SaveStore store, GameContent content, BootRequest request, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        RequireCurrent(content);
        ArgumentNullException.ThrowIfNull(request);
        if (!request.DevOverride)
            throw new ArgumentException("A launch with no dev override plays the campaign's own saves, never the override's slot.", nameof(request));
        try
        {
            store.Empty();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] The dev override's slot {store.Folder} could not be emptied - its campaign is fresh all the same: {error}");
        }

        if (request.NewCampaign)
            report?.Invoke($"[Save] {Program.NewCampaignOption} does nothing on a dev-override launch: its campaign is fresh every time, and the campaign's own save is never touched");
        report?.Invoke($"[Save] A dev-override launch: a fresh campaign in its own slot, {store.Folder}"
            + (request.DungeonId is { } dungeon ? $", straight into {dungeon}" : "")
            + (request.Seed is { } seed ? $", its first run on seed {seed}" : "")
            + " - the campaign's own save is not read or written");
        return new BootResult(CampaignState.Start(content), Resumed: false, View: null, request.DungeonId, request.Seed, [OverrideNotice]);
    }

    /// <summary>
    /// <c>--new-campaign</c>: the slot's backup and then its save are set aside as
    /// <c>abandoned</c> — never deleted — and a new campaign begins, which the game
    /// saves at once (<c>DistanceRpgGame.BootCampaign</c>). Both go. A backup left
    /// behind would be resumed as though it were the new campaign's own the first
    /// time no save stood beside it: after a quit mid-run before the new campaign
    /// was ever saved, or a crash, or a first save that cannot be played. The
    /// backup goes first, so the later save takes the later number. A file that
    /// will not move stops it there: the store refuses every write this session
    /// (<see cref="SetAside"/>), and what is left of the old campaign — its save,
    /// when that is still in place — is what the next launch resumes. The old
    /// campaign's unsaved-quit marker goes with it, logged and never said: there is
    /// nothing left to say it of.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    internal static BootResult Abandon(SaveStore store, GameContent content, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        RequireCurrent(content);
        TakeUnsaved(store, report);

        string what = $"was left for a new campaign ({Program.NewCampaignOption})";
        var setAside = new List<string>();
        foreach (bool backup in new[] { true, false })
        {
            if (!(backup ? store.HasBackup : store.HasSave))
                continue;
            if (!SetAside(store, backup, "abandoned", what, setAside, report))
            {
                report?.Invoke($"[Save] {Program.NewCampaignOption} stops there: nothing more is set aside and nothing is saved this session, so the next launch resumes the campaign still in {store.Folder}");
                break;
            }
        }
        Unmark(store, report);   // nothing is being resumed
        report?.Invoke($"[Save] {Program.NewCampaignOption}: a new campaign begins in {store.Folder}");
        return new BootResult(CampaignState.Start(content), Resumed: false, View: null, StraightInto: null, FirstSeed: null, [NewCampaignBegins, .. setAside]);
    }

    /// <summary>A campaign is built and restored under the current content alone.</summary>
    private static void RequireCurrent(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!ReferenceEquals(content, GameContent.Current))
            throw new ArgumentException(
                "A campaign is loaded or begun under the current content: it is built and restored under GameContent.Current, so the content handed in must be that one.",
                nameof(content));
    }

    /// <summary>
    /// Read and restore the save, or the backup: true with the campaign, or false
    /// with why not in a few words (<see cref="WhyUnreadable"/>) — the whole error
    /// in the log. A tuning that changed since is logged with both hashes. A
    /// campaign that comes back with a run in progress is resumed like any other:
    /// the game plays its visit on in the dungeon (Phase 5.8), and the log says
    /// where — the dungeon, the floor and the turn.
    /// </summary>
    private static bool TryResume(SaveStore store, bool backup, GameContent content, Action<string>? report,
        [NotNullWhen(true)] out LoadedCampaign? loaded, out string why)
    {
        string path = backup ? store.BackupPath : store.SavePath;
        try
        {
            string text = (backup ? store.ReadBackup() : store.Read()) ?? throw new FileNotFoundException("The file went missing while it was being read.", path);
            var file = SaveCodec.Read(text);
            loaded = SaveGame.Restore(file, content);
            report?.Invoke($"[Save] Resumed {path}: run counter {loaded.Campaign.RunCounter}"
                + (loaded.Campaign.Visit is { } visit
                    ? $", a run in progress in {visit.Def.Id} on map seed {visit.MapSeed}, floor {visit.FloorIndex + 1}, turn {visit.Turn}"
                    : loaded.Campaign.LastResult is { } last ? $", the last run {last.Outcome} in {last.DungeonId}" : ", no run played yet")
                + (file.SavedAt is { } savedAt ? $", saved {savedAt}" : ", no time recorded"));
            if (loaded.TuningChanged)
                report?.Invoke($"[Save] {path} was saved under tuning {file.TuningHash} and the game plays under {TuningJson.Hash(content.Tuning)}: the numbers have changed, and the campaign plays on under the new ones");
            why = "";
            return true;
        }
        catch (Exception error)
        {
            report?.Invoke($"[Save] {path} could not be resumed: {error}");
            loaded = null;
            why = WhyUnreadable(error, store);
            return false;
        }
    }

    /// <summary>
    /// Set the save, or the backup, aside as <paramref name="reason"/>, saying so
    /// in <paramref name="notices"/> by its new full path. False when it could not
    /// be moved: then it stays where it is, the store refuses every write this
    /// session so nothing takes its place, and the notice says that instead.
    /// </summary>
    private static bool SetAside(SaveStore store, bool backup, string reason, string what, List<string> notices, Action<string>? report)
    {
        string file = backup ? SaveStore.BackupName : SaveStore.FileName;
        string from = backup ? store.BackupPath : store.SavePath;
        try
        {
            string path = store.SetAside(reason, backup);
            report?.Invoke($"[Save] {from} {what} - set aside as {path}, never deleted or written over");
            notices.Add(SetAsideNotice(file, what, path));
            return true;
        }
        catch (FileNotFoundException)
        {
            report?.Invoke($"[Save] {from} {what}, and is gone from the folder already: nothing to set aside");
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            string why = SaveStore.Why(error, store.Folder, writing: true);
            report?.Invoke($"[Save] {from} {what} and could not be set aside - nothing is saved this session, so it is never written over: {error}");
            store.RefuseWrites($"{file} {what} and could not be set aside ({why})");
            notices.Add(StuckNotice(file, what, why));
            return false;
        }
    }

    /// <summary>Write the resume marker; a marker that cannot be written only goes unguarded, and the log says so.</summary>
    private static void Mark(SaveStore store, Action<string>? report)
    {
        try
        {
            store.MarkResuming();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {store.ResumingPath} could not be written - this resume goes unguarded: {error}");
        }
    }

    /// <summary>Clear the resume marker: nothing is being resumed.</summary>
    private static void Unmark(SaveStore store, Action<string>? report)
    {
        try
        {
            store.ClearResuming();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {store.ResumingPath} could not be cleared: {error}");
        }
    }

    /// <summary>
    /// Whether the last quit here could not save the campaign: its marker
    /// (<see cref="SaveStore.MarkUnsaved"/>) read, logged with what that quit wrote
    /// in it, and cleared, so it is said once. A marker that is there and cannot be
    /// read is one all the same. One that will not go is said again next launch,
    /// and the log says why.
    /// </summary>
    private static bool TakeUnsaved(SaveStore store, Action<string>? report)
    {
        string? wrote;
        try
        {
            wrote = store.ReadUnsaved();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            wrote = $"(it could not be read: {error.Message})";
        }
        if (wrote is null)
            return false;

        report?.Invoke($"[Save] {store.UnsavedPath} was left by the last quit, which could not save the campaign - what resumes is from before it: {wrote.ReplaceLineEndings(" ").Trim()}");
        try
        {
            store.ClearUnsaved();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {store.UnsavedPath} could not be cleared - the next launch says this again: {error}");
        }
        return true;
    }

    /// <summary>
    /// Leave the unsaved-quit marker for the next launch (<see cref="SaveStore.MarkUnsaved"/>):
    /// this quit could not save the campaign, for <paramref name="why"/>. One that
    /// cannot be written either leaves the log alone to say so.
    /// </summary>
    private static void MarkUnsaved(SaveStore store, string why, Action<string>? report)
    {
        try
        {
            store.MarkUnsaved(why);
            report?.Invoke($"[Save] {store.UnsavedPath} written: the next launch says that what it resumes is from before this quit");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {store.UnsavedPath} could not be written either - the next launch cannot say this quit went unsaved: {error}");
        }
    }

    /// <summary>
    /// Save <paramref name="campaign"/> to <paramref name="store"/> at a safe point
    /// (<see cref="SaveGame.IsSafePoint"/>) — between runs, or with the run in
    /// progress in it (Phase 5.8): right after the entry, or in the player phase
    /// with nothing in flight — with the tuning's hash, the scene's
    /// <paramref name="view"/> and the time, atomically. A save of the campaign the
    /// slot already holds, whatever its time, is not written again
    /// (<see cref="Holds"/>), so the backup stays the save before it and the save
    /// keeps the time its state was first written. Never throws: a write that
    /// fails — a full disk, a folder with no rights, a locked file, a campaign that
    /// cannot be captured, the enemies mid-turn — is logged whole and comes back as
    /// the notice to show, and the save on the disk is what it was.
    /// <para>
    /// <strong>No write costs a file</strong> (1f, the write guard): before the
    /// write, a <see cref="SaveStore.FileName"/> whose text the codec refuses —
    /// malformed, or a newer build's, which is never written over — is set aside as
    /// <c>unreadable</c>, so the write moves into place and the backup beside it
    /// stays untouched; and while a save stands for the replace to rotate, a
    /// <see cref="SaveStore.BackupName"/> the codec refuses is set aside the same
    /// way. Only a text the codec refuses counts: a file the file system will not
    /// let the game read right now is left to the write, which fails and says so. A
    /// file this session wrote or read whole is not read again
    /// (<see cref="SaveStore.SaveVouched"/>). What the guard sets aside is said in
    /// <paramref name="said"/>, a fact about a file that no later save withdraws;
    /// one that will not move refuses the store's writes, and its notice comes back
    /// as the failure. And a write clears the slot's resume marker, since the file
    /// it guarded is gone.
    /// </para>
    /// </summary>
    /// <param name="store">The slot.</param>
    /// <param name="campaign">The campaign, at a safe point.</param>
    /// <param name="view">What the scene shows, carried for it; null for nothing to say.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <param name="savedAt">When it is saved, the edge's clock: written as the save's time. Null records none.</param>
    /// <param name="said">Where the notices of what the guard set aside go, a list the caller hands in; null to leave them to the log.</param>
    /// <returns>Null when saved, or the notice a failed write shows.</returns>
    internal static string? Save(SaveStore store, CampaignState campaign, ViewDto? view = null, Action<string>? report = null,
        DateTimeOffset? savedAt = null, List<string>? said = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(campaign);
        try
        {
            string text = SaveCodec.Write(SaveGame.Capture(campaign, view, savedAt));
            if (Holds(store, text))
            {
                report?.Invoke($"[Save] {store.SavePath} already holds this campaign - nothing written, the backup kept");
                return null;
            }
            if (store.WritesRefused is null && MakeWay(store, report, said) is { } stuck)
                return stuck;
            store.Write(text);
            report?.Invoke($"[Save] Wrote {store.SavePath}");
            Written(store, report);
            return null;
        }
        catch (Exception error)
        {
            report?.Invoke($"[Save] Could not write {store.SavePath} - the save there is kept: {error}");
            return WriteFailed(store.WritesRefused ?? SaveStore.Why(error, store.Folder, writing: true));
        }
    }

    /// <summary>
    /// The write guard: the save, then — while a save stands for the replace to
    /// rotate into its place — the backup, each set aside as <c>unreadable</c> when
    /// the codec refuses its text and it is not one this session knows
    /// (<see cref="SaveStore.SaveVouched"/>). Silent unless it acts.
    /// </summary>
    /// <returns>Null to go on and write; or the stuck notice, when a file the codec refuses would not move — the store refuses its writes from then on.</returns>
    private static string? MakeWay(SaveStore store, Action<string>? report, List<string>? said)
    {
        foreach (bool backup in new[] { false, true })
        {
            if (backup && !store.HasSave)
                break;   // the write moves into place, and the backup beside it is untouched
            if ((backup ? store.BackupVouched : store.SaveVouched) || Refusal(store, backup, report) is not { } why)
                continue;
            var notices = new List<string>();
            if (!SetAside(store, backup, "unreadable", $"could not be read ({why})", notices, report))
                return notices[^1];
            said?.AddRange(notices);
        }
        return null;
    }

    /// <summary>
    /// Why the codec refuses the save's — or the backup's — text, in a notice's few
    /// words (<see cref="WhyUnreadable"/>); or null when it reads — and is then one
    /// this session knows (<see cref="SaveStore.Vouch"/>) — or is not there, or the
    /// file system will not let it be read right now, which is left to the write.
    /// </summary>
    private static string? Refusal(SaveStore store, bool backup, Action<string>? report)
    {
        string? text;
        try
        {
            text = backup ? store.ReadBackup() : store.Read();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        if (text is null)
            return null;

        try
        {
            SaveCodec.Read(text);
        }
        catch (Exception error)
        {
            report?.Invoke($"[Save] {(backup ? store.BackupPath : store.SavePath)} is no save this build reads, and a write would rotate it away: {error}");
            return WhyUnreadable(error, store);
        }
        store.Vouch(backup);
        return null;
    }

    /// <summary>
    /// A write replaced the slot's save, so a resume marker there guarded a file
    /// that is gone, and goes too: left standing, it would set the new save aside as
    /// crashed at the next load and resume the backup — the very save that crashed.
    /// No write reaches a slot while its own load is in flight
    /// (<c>DistanceRpgGame.WriteRequestedSave</c> waits for the loaded screen), so a
    /// marker is only ever cleared here once the file it guarded is gone.
    /// </summary>
    private static void Written(SaveStore store, Action<string>? report)
    {
        if (!store.WasResuming)
            return;
        try
        {
            store.ClearResuming();
            report?.Invoke($"[Save] {store.ResumingPath} cleared: the save it guarded has been written over");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {store.ResumingPath} could not be cleared - the next load sets the new save aside as crashed: {error}");
        }
    }

    /// <summary>
    /// The save a quit takes (1f): the campaign settled to its next safe point,
    /// then saved (<see cref="Save"/>) with the view as the scene has it once
    /// settled. Every quit arrives here mid-run — the pause menu's
    /// <c>2  SAVE AND QUIT</c> and the window's close button alike. A quit that
    /// finds the enemies taking their turn — its banner, their walks, their beats
    /// — plays it out to the next player turn first, one
    /// <paramref name="step"/> at a time (<see cref="SaveGame.SettleToSafePoint"/>),
    /// so what is saved is the turn the player would have met. A settle in which
    /// the enemies wipe the party ends the run in <c>Logic/</c>, and the campaign
    /// is saved between runs, the run's summary with it.
    /// <para>
    /// <strong>No screen follows a quit</strong>, so a quit that cannot save — the
    /// write fails, or the enemies' turn cannot be played out — leaves
    /// <see cref="SaveStore.UnsavedName"/> beside the save it keeps, and the next
    /// launch says that what it resumes is from before this quit
    /// (<see cref="QuitUnsaved"/>). What the write guard sets aside on the way is
    /// logged: no screen follows to say it.
    /// </para>
    /// <para>
    /// <strong>The step is the playing scene's own</strong>
    /// (<c>DungeonScene.StepLogic</c>): one fixed logic step of the turn system
    /// plus the enemy-phase sighting the scene feeds back, which decides whose
    /// zones may react — the step live play takes, so the settle takes exactly
    /// the steps play would have. <c>Logic/</c>'s own default step feeds back no
    /// sighting and is a test's, never the game's: with no step, a campaign off a
    /// safe point is not settled and not saved, and the last save stands.
    /// </para>
    /// </summary>
    /// <param name="store">The slot the quit saves to: the autosave's.</param>
    /// <param name="campaign">The campaign being quit.</param>
    /// <param name="view">What the playing scene shows once the settle is done — read then, since a turn's start re-decides the march and may hand the lead to a living member — or null for nothing to say.</param>
    /// <param name="step">The playing scene's logic step; null when no scene is playing the visit, which leaves nothing to settle.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <param name="savedAt">When the quit saves, the edge's clock; null records no time.</param>
    /// <returns>True once the campaign stood at a safe point and the save was made — a write that fails is logged, never thrown, the save on the disk kept and the marker left; false when it never got to one, nothing was written, and the marker is left.</returns>
    internal static bool SaveOnQuit(SaveStore store, CampaignState campaign, Func<ViewDto?>? view, Action? step, Action<string>? report = null,
        DateTimeOffset? savedAt = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(campaign);
        if (!SaveGame.IsSafePoint(campaign))
        {
            var visit = campaign.Visit!;   // off a safe point there is a visit, and a system open on it
            if (step is null)
            {
                report?.Invoke($"[Save] Quitting {visit.Def.Name} during the enemies' turn ({visit.Turns!.Phase}) with no scene to play it out with - nothing is written, and {store.SavePath} keeps the last save");
                MarkUnsaved(store, $"the quit came during the enemies' turn in {visit.Def.Name}, with no scene to play it out with", report);
                return false;
            }

            report?.Invoke($"[Save] Quitting {visit.Def.Name} during the enemies' turn ({visit.Turns!.Phase}): playing it out to the next player turn before saving");
            int steps = 0;
            if (!SaveGame.SettleToSafePoint(campaign, () => { steps++; step(); }))
            {
                report?.Invoke($"[Save] The enemies' turn did not come round to the party's in {steps} steps - nothing is written, and {store.SavePath} keeps the last save");
                MarkUnsaved(store, $"the enemies' turn in {visit.Def.Name} did not come round to the party's in {steps} steps", report);
                return false;
            }
            report?.Invoke(campaign.Visit is { } settled
                ? $"[Save] Played out in {steps} steps: player turn {settled.Turn} on floor {settled.FloorIndex + 1}"
                : $"[Save] Played out in {steps} steps: the enemies wiped the party, and the run ended ({campaign.LastResult?.Outcome})");
        }

        // A view is the run's: a campaign between runs — a quit on the summary, or
        // a settle that ended the run — has none to carry.
        var viewNow = campaign.Visit is null ? null : view?.Invoke();
        if (Save(store, campaign, viewNow, report, savedAt) is { } failed)
        {
            report?.Invoke($"[Save] {failed}");
            MarkUnsaved(store, failed, report);
        }
        return true;
    }

    /// <summary>
    /// Whether the save on the disk is already <paramref name="text"/> but for its
    /// time (<see cref="SaveCodec.Untimed"/>): then nothing is written, the backup
    /// stays the save before it, and the save keeps the time its state was first
    /// written. A save that cannot be read never holds it, and the write guard
    /// deals with it first (<see cref="MakeWay"/>).
    /// </summary>
    private static bool Holds(SaveStore store, string text)
    {
        try
        {
            return store.WritesRefused is null && store.Read() is { } saved && SaveCodec.Untimed(saved) == SaveCodec.Untimed(text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // ── What the screen says ─────────────────────────────────────────────────

    /// <summary>A save set aside: which file, what became of it, the full path it has now, and <see cref="TryAgain"/>.</summary>
    internal static string SetAsideNotice(string file, string what, string path)
        => $"{file.ToUpperInvariant()} {what.ToUpperInvariant()} - SET ASIDE AS {path} - {TryAgain}";

    /// <summary>A save that could not be read and could not be moved: it stays, and nothing is saved over it.</summary>
    internal static string StuckNotice(string file, string what, string why)
        => $"{file.ToUpperInvariant()} {what.ToUpperInvariant()} AND COULD NOT BE SET ASIDE ({why.ToUpperInvariant()}) - NOTHING IS SAVED THIS SESSION, SO IT IS NEVER WRITTEN OVER";

    /// <summary>A write that failed, and why, in a notice: the save before it is kept.</summary>
    internal static string WriteFailed(string why)
        => $"THE CAMPAIGN COULD NOT BE SAVED ({why.ToUpperInvariant()}) - THE LAST SAVE IS KEPT - SEE THE LOG";

    /// <summary>
    /// Why a save could not be resumed, in a few words: the path in the document
    /// and the first clause of the problem for a save the format refuses
    /// (<c>version: 2 is newer than this build reads (1)</c>), the file system's
    /// reason for a file that could not be read, and otherwise that the game
    /// failed reading it — the log has the whole error.
    /// </summary>
    internal static string WhyUnreadable(Exception error, SaveStore store)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(store);
        return error switch
        {
            SaveFormatException format => $"{format.Where}: {FirstClause(format.Problem)}",
            IOException or UnauthorizedAccessException => SaveStore.Why(error, store.Folder, writing: false),
            _ => "the game failed reading it - see the log",
        };
    }

    /// <summary>
    /// A problem up to the end of its first clause or sentence, whichever comes
    /// first: <c>2 is newer than this build reads (1)</c> of the sentence that goes
    /// on to say why that matters, or the first of a JSON reader's two sentences.
    /// The log has the rest.
    /// </summary>
    private static string FirstClause(string problem)
    {
        int cut = new[] { problem.IndexOf(": ", StringComparison.Ordinal), problem.IndexOf(". ", StringComparison.Ordinal) }
            .Where(at => at > 0)
            .DefaultIfEmpty(problem.Length)
            .Min();
        return problem[..cut].Trim().TrimEnd('.');
    }
}
