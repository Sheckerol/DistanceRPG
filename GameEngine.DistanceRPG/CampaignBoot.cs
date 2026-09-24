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
/// The campaign a launch plays, and what to say about it (1f): the campaign —
/// resumed, or new — and the view its save carried; for a dev-override launch,
/// the dungeon to boot straight into and the first run's seed; and the notices
/// the first screen shows, in this order and whole: that the campaign resumed or
/// a new one begins, that the numbers changed since it was saved, and that a save
/// was set aside, where, and how to try it again.
/// </summary>
internal sealed record BootResult(CampaignState Campaign, bool Resumed, ViewDto? View, string? StraightInto, long? FirstSeed,
    IReadOnlyList<string> Notices);

/// <summary>
/// The campaign's life on the disk, between the edge (<see cref="SaveStore"/>)
/// and <c>Logic/</c>'s save document (<see cref="SaveGame"/>, <see cref="SaveCodec"/>):
/// which campaign a launch plays (<see cref="Boot"/>), and a save that never
/// throws (<see cref="Save"/>). Engine-free, so every case is tested on a
/// temporary folder with no window; what it has to say goes to the caller as
/// log lines (<c>report</c>) and as notices, and the game draws and logs them.
/// <list type="bullet">
/// <item><strong>Loading is resuming</strong> (1f, USER DECISION 2): a launch
/// resumes <see cref="SaveStore.FileName"/>, and says so. A save that cannot be
/// read — malformed, written by a newer build, naming content that is gone — is
/// set aside, never deleted or written over, and the backup is tried, then a
/// new campaign. A save whose resume crashed the game last time — the resume
/// marker left behind — is set aside the same way. Every set-aside notice names
/// the file's full path and ends <see cref="TryAgain"/>.</item>
/// <item><strong>A different tuning is said, and plays on</strong> (§5.3,
/// PHASE5-084): "silently changing a saved game's rules underneath the player is
/// the failure to avoid", so a campaign saved under other numbers resumes with
/// <see cref="TuningChanged"/> on its first screen.</item>
/// <item><strong>The dev overrides play in their own slot</strong>: a launch
/// with <c>DISTANCERPG_DUNGEON</c> or <c>DISTANCERPG_SEED</c> empties
/// <see cref="SaveStore.OverrideSlot"/> and plays a fresh campaign there, so it
/// is as repeatable as it ever was and the campaign is untouched.</item>
/// </list>
/// </summary>
internal static class CampaignBoot
{
    /// <summary>A resume from the backup, one save older than the campaign's own file.</summary>
    internal const string ResumedFromBackup = BetweenRunsHud.Resumed + " FROM ITS BACKUP, ONE SAVE OLDER";

    /// <summary>A save was there and could not be resumed, or was set aside for a new campaign.</summary>
    internal const string NewCampaignBegins = "A NEW CAMPAIGN BEGINS";

    /// <summary>PHASE5-084: the save's tuning hash is not the loaded tuning's.</summary>
    internal const string TuningChanged = "THE NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS ON UNDER THE NEW ONES";

    /// <summary>How every set-aside notice ends: how to try the file again.</summary>
    internal const string TryAgain = "RENAME IT TO CAMPAIGN.JSON TO TRY AGAIN";

    /// <summary>A dev-override launch's word on the campaign it plays.</summary>
    internal const string OverrideNotice = "A DEV OVERRIDE LAUNCH - A SCRATCH CAMPAIGN, SAVED APART - YOUR CAMPAIGN IS UNTOUCHED";

    /// <summary>The store a launch plays on: the dev-override slot inside <paramref name="saveDirectory"/> for an override launch, and the save folder itself otherwise.</summary>
    internal static SaveStore StoreFor(string saveDirectory, BootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SaveStore(SaveStore.SlotDirectory(saveDirectory, request.DevOverride));
    }

    /// <summary>
    /// The campaign this launch plays, in this order:
    /// <list type="number">
    /// <item>A dev-override launch empties its slot (<paramref name="store"/> must
    /// be one) and plays a fresh campaign — straight into the dungeon it names,
    /// its first run on the seed it names. The campaign's own save is never
    /// opened.</item>
    /// <item><c>--new-campaign</c> sets the backup and the save aside as
    /// <c>abandoned</c> before anything is read, and starts fresh
    /// (<see cref="Abandon"/>).</item>
    /// <item>A resume marker left behind means the last resume crashed the game:
    /// the save it guarded — the save, or the backup when there was no save — is
    /// set aside as <c>crashed</c>.</item>
    /// <item>The save is resumed; failing that it is set aside as
    /// <c>unreadable</c>, and the backup is resumed; failing that it is set aside
    /// too, and a new campaign starts. With no save there at all but a backup — a
    /// save set aside by an earlier launch and nothing written since, or one
    /// deleted by hand — the backup is resumed, so a campaign is never lost to a
    /// missing file while its backup stands. The resume marker is written before
    /// each resume is tried and left for the game to clear once the resumed screen
    /// is up (<see cref="SaveStore.ClearResuming"/>).</item>
    /// </list>
    /// A restore that throws anything — a <see cref="SaveFormatException"/> or a
    /// fault — is logged whole and treated as unreadable, so a player is never
    /// stuck on a crash at launch. A file that could not be read and cannot be
    /// moved either is left where it is, and the store refuses every write this
    /// session (<see cref="SaveStore.RefuseWrites"/>), so nothing takes its place.
    /// </summary>
    /// <param name="store">The launch's slot (<see cref="StoreFor"/>).</param>
    /// <param name="content">The content loaded, which must be <see cref="GameContent.Current"/>: a campaign is built and restored under it.</param>
    /// <param name="request">What the launch was asked for.</param>
    /// <param name="report">Where the log lines go: every file read, set aside or found wanting, with the whole error.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    /// <exception cref="InvalidOperationException">A dev-override launch on a store that is not a dev-override slot.</exception>
    internal static BootResult Boot(SaveStore store, GameContent content, BootRequest request, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(request);
        if (!ReferenceEquals(content, GameContent.Current))
            throw new ArgumentException(
                "A campaign boots under the current content: it is built and restored under GameContent.Current, so the content handed in must be that one.",
                nameof(content));

        if (request.DevOverride)
            return BootOverride(store, content, request, report);
        if (request.NewCampaign)
            return Abandon(store, content, report);

        var setAside = new List<string>();
        if (store.WasResuming)
        {
            // The save it guarded is the one a resume reads first: the save when
            // there is one, and the backup when the resume had only that to read.
            bool save = store.HasSave;
            if (save || store.HasBackup)
            {
                report?.Invoke($"[Save] {store.ResumingPath} was left behind: resuming {(save ? store.SavePath : store.BackupPath)} crashed the game last time");
                if (!SetAside(store, backup: !save, "crashed", "crashed the game the last time it was resumed", setAside, report))
                    return New(content, [NewCampaignBegins, .. setAside]);
            }
            Unmark(store, report);
        }

        foreach (bool backup in new[] { false, true })
        {
            if (!(backup ? store.HasBackup : store.HasSave))
                continue;
            Mark(store, report);   // before the restore, so even a restore that takes the process down is caught next launch
            if (TryResume(store, backup, content, report, out var loaded, out string why))
            {
                // What plays first, then the numbers it plays under — PHASE5-084
                // wants that on the screen, whatever else there is to say — then
                // what became of any save set aside on the way.
                var notices = new List<string> { backup ? ResumedFromBackup : BetweenRunsHud.Resumed };
                if (loaded.TuningChanged)
                    notices.Add(TuningChanged);
                notices.AddRange(setAside);
                return new BootResult(loaded.Campaign, Resumed: true, loaded.View, StraightInto: null, FirstSeed: null, notices);
            }
            if (!SetAside(store, backup, "unreadable", $"could not be read ({why})", setAside, report))
                break;   // stuck where it is: writes are refused, and nothing else is resumed
        }

        Unmark(store, report);
        if (setAside.Count == 0)
        {
            report?.Invoke($"[Save] No campaign to resume in {store.Folder}: a new campaign begins");
            return New(content, []);
        }
        report?.Invoke("[Save] Nothing could be resumed: a new campaign begins");
        return New(content, [NewCampaignBegins, .. setAside]);
    }

    /// <summary>A dev-override launch: the slot emptied, a fresh campaign, straight into the dungeon when one is named.</summary>
    private static BootResult BootOverride(SaveStore store, GameContent content, BootRequest request, Action<string>? report)
    {
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
    /// <c>--new-campaign</c>: the campaign's backup and then its save are set
    /// aside as <c>abandoned</c> — never deleted — and a new campaign begins, which
    /// the game saves at once (<c>DistanceRpgGame.BootCampaign</c>). Both go. A
    /// backup left behind would be resumed as though it were the new campaign's
    /// own the first time no save stood beside it: after a quit mid-run before the
    /// new campaign was ever saved, or a crash, or a first save that cannot be
    /// played. The backup goes first, so the later save takes the later number. A
    /// file that will not move stops it there: the store refuses every write this
    /// session (<see cref="SetAside"/>), and what is left of the old campaign —
    /// its save, when that is still in place — is what the next launch resumes.
    /// </summary>
    private static BootResult Abandon(SaveStore store, GameContent content, Action<string>? report)
    {
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
        return New(content, [NewCampaignBegins, .. setAside]);
    }

    /// <summary>A campaign from the roster, saying <paramref name="notices"/>.</summary>
    private static BootResult New(GameContent content, IReadOnlyList<string> notices)
        => new(CampaignState.Start(content), Resumed: false, View: null, StraightInto: null, FirstSeed: null, notices);

    /// <summary>
    /// Read and restore the save, or the backup: true with the campaign, or false
    /// with why not in a few words (<see cref="WhyUnreadable"/>) — the whole error
    /// in the log. A tuning that changed since is logged with both hashes.
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
                + (loaded.Campaign.LastResult is { } last ? $", the last run {last.Outcome} in {last.DungeonId}" : ", no run played yet"));
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
    /// Save <paramref name="campaign"/> to <paramref name="store"/> — between runs,
    /// which is all the game saves until Phase 5.8 brings the run in progress in —
    /// with the tuning's hash, atomically. A text the save already holds is not
    /// written again, so the backup stays the save before it. Never throws: a write
    /// that fails — a full disk, a folder with no rights, a locked file, a campaign
    /// that cannot be captured, a run in progress — is logged whole and comes back
    /// as the notice to show, and the save on the disk is what it was.
    /// <para>
    /// <c>Logic/</c> can capture a visit at any safe point from Phase 5.6
    /// (<see cref="SaveGame.Capture"/>); the game does not write one yet, since
    /// nothing resumes it into the dungeon before 5.8, and a save holding a run the
    /// game could only reopen between runs would strand it there.
    /// </para>
    /// </summary>
    /// <returns>Null when saved, or the notice a failed write shows.</returns>
    internal static string? Save(SaveStore store, CampaignState campaign, ViewDto? view = null, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(campaign);
        try
        {
            if (campaign.Visit is { } visit)
                throw new InvalidOperationException(
                    $"A visit to '{visit.Def.Id}' is in progress: the game saves a campaign between runs, and the run in progress is not saved yet.");
            string text = SaveCodec.Write(SaveGame.Capture(campaign, view));
            if (Holds(store, text))
            {
                report?.Invoke($"[Save] {store.SavePath} already holds this campaign - nothing written, the backup kept");
                return null;
            }
            store.Write(text);
            report?.Invoke($"[Save] Wrote {store.SavePath}");
            return null;
        }
        catch (Exception error)
        {
            report?.Invoke($"[Save] Could not write {store.SavePath} - the save there is kept: {error}");
            return WriteFailed(store.WritesRefused ?? SaveStore.Why(error, store.Folder, writing: true));
        }
    }

    /// <summary>Whether the save on the disk is already <paramref name="text"/>; a save that cannot be read is written over as ever, through the replace that keeps it as the backup.</summary>
    private static bool Holds(SaveStore store, string text)
    {
        try
        {
            return store.WritesRefused is null && store.Read() == text;
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
