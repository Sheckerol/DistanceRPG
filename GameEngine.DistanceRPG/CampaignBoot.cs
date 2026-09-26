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
/// A campaign begun rather than loaded, and what to say about it (1f): a
/// dev-override launch's scratch campaign (<see cref="CampaignBoot.BootOverride"/>),
/// with the dungeon to boot straight into and the first run's seed; or the one
/// NEW CAMPAIGN — on the title, or <c>--new-campaign</c> at launch — begins
/// (<see cref="CampaignBoot.Abandon"/>); and the notices the first screen shows,
/// in this order and whole: that a new campaign begins, and every file set aside
/// for it, where, and how to try it again. A campaign loaded from a slot is a
/// <see cref="SlotLoad"/>, so <see cref="Resumed"/> is false and
/// <see cref="View"/> null for every campaign here.
/// </summary>
internal sealed record BootResult(CampaignState Campaign, bool Resumed, ViewDto? View, string? StraightInto, long? FirstSeed,
    IReadOnlyList<string> Notices);

/// <summary>
/// What loading one slot brought back (<see cref="CampaignBoot.Load"/>): the
/// campaign and the view its save carried — or neither, when the slot held
/// nothing that would load — and what to say, in order and whole: that the
/// campaign resumed, from which slot or its backup; that the numbers changed
/// since it was saved; and every file set aside on the way, where, and how to try
/// it again. A load never makes a campaign: one that brings nothing back goes
/// back to the title, which says so first (<see cref="CampaignBoot.NothingLoadedWord"/>).
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
/// (<see cref="Load"/>), and the title's word when it brings nothing back
/// (<see cref="NothingLoadedWord"/>); the room a manual slot's campaign makes in
/// the autosave slot (<see cref="MakeRoom"/>); the two ways a campaign is begun
/// rather than loaded — a dev override's scratch campaign
/// (<see cref="BootOverride"/>) and NEW CAMPAIGN (<see cref="Abandon"/>); a save
/// that never throws and never costs a file (<see cref="Save"/>); a save by hand
/// into a manual slot, at a safe point alone (<see cref="SaveTo"/>); and the save
/// leaving play takes mid-run (<see cref="SaveOnQuit"/>). Engine-free, so every
/// case is tested on a temporary folder with no window; what it has to say goes to
/// the caller as log lines (<c>report</c>) and as notices, and the game draws and
/// logs them.
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
/// game goes back to the title, which says so first, then what was set aside
/// (<see cref="NothingLoadedWord"/>).</item>
/// <item><strong>A manual slot's campaign makes room in the autosave slot</strong>
/// (<see cref="MakeRoom"/>; USER DECISION 2): once loaded it autosaves there, so a
/// different campaign in the autosave slot is set aside — after the title asked,
/// once the load has brought a campaign back, before anything is written — never
/// deleted, and never lost to two autosaves.</item>
/// <item><strong>A campaign that will not be autosaved says so on its first
/// screen</strong> (<see cref="InPlayWord"/>): a refusal holds for the session, so
/// a file stuck on an earlier try from the title refuses the autosave for every
/// campaign loaded or begun after it, and each says so at once rather than leave
/// it to the first autosave to fail.</item>
/// <item><strong>A save by hand goes into a manual slot at a safe point
/// only</strong> (<see cref="SaveTo"/>; the user's ruling of 2026-09-25 on the save
/// model, R2(d)): never while the enemies are moving, and never into the autosave,
/// which saves itself.</item>
/// <item><strong>No write costs a file</strong> (1f): a slot can be written without
/// being loaded, so before any write a <see cref="SaveStore.FileName"/> there
/// that no save can be read from is set aside first, and so is such a backup the
/// replace would otherwise rotate away — the codec's refusal decides, never a
/// listing's word on the slot. And a write clears its slot's resume marker: the
/// file it guarded is gone.</item>
/// <item><strong>Leaving play without writing the autosave is said on the
/// title</strong> (1f): no screen of the campaign follows a quit — the window
/// closes, or the title opens — so the quit's own door leaves
/// <see cref="SaveStore.UnsavedName"/> beside the autosave when it cannot save
/// (<see cref="SaveOnQuit"/>), and the title takes it (<see cref="TakeUnsaved"/>)
/// and says, before anything is loaded, that what the autosave holds is from
/// before then (<see cref="QuitUnsaved"/>) — at once, after QUIT TO TITLE. A quit
/// that does save tells its caller so, which takes back what an earlier failed
/// save said.</item>
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

    /// <summary>NEW CAMPAIGN began one — on the title, or <c>--new-campaign</c> at launch — whatever was set aside for it.</summary>
    internal const string NewCampaignBegins = "A NEW CAMPAIGN BEGINS";

    /// <summary>PHASE5-084: the save's tuning hash is not the loaded tuning's.</summary>
    internal const string TuningChanged = "THE NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS ON UNDER THE NEW ONES";

    /// <summary>How every set-aside notice ends: how to try the file again.</summary>
    internal const string TryAgain = "RENAME IT TO CAMPAIGN.JSON TO TRY AGAIN";

    /// <summary>
    /// The last time play was left, the autosave could not be written
    /// (<see cref="SaveStore.UnsavedName"/>): what it holds is from before then. The
    /// title says it, before anything is loaded — true of the window's close, and
    /// of leaving play for the title, alike; and after a manual slot was loaded the
    /// quit was writing that slot's campaign into the autosave, so the words name
    /// the autosave and never a campaign.
    /// </summary>
    internal const string QuitUnsaved = "THE AUTOSAVE COULD NOT BE WRITTEN WHEN YOU LAST LEFT PLAY - WHAT IT HOLDS IS FROM BEFORE THAT, BEHIND WHERE YOU LEFT IT";

    /// <summary>A dev-override launch's word on the campaign it plays.</summary>
    internal const string OverrideNotice = "A DEV OVERRIDE LAUNCH - A SCRATCH CAMPAIGN, SAVED APART - YOUR CAMPAIGN IS UNTOUCHED";

    /// <summary>The store a launch plays on: the dev-override slot inside <paramref name="saveDirectory"/> for an override launch, and the save folder itself otherwise.</summary>
    internal static SaveStore StoreFor(string saveDirectory, BootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SaveStore(SaveStore.SlotDirectory(saveDirectory, request.DevOverride));
    }

    /// <summary>
    /// Whether the words speak of <paramref name="slot"/> as the campaign's own
    /// save, naming no slot: the autosave, and the dev override's scratch slot,
    /// whose saves have always been said the autosave's way. A manual slot is
    /// named.
    /// </summary>
    private static bool Unnamed(SaveSlot slot) => slot.Number is null or SaveSlots.Autosave;

    /// <summary>
    /// What loading a slot says first, in green: <c>CAMPAIGN RESUMED</c> for the
    /// autosave, <c>CAMPAIGN RESUMED FROM SLOT 2</c> for a manual one; and from a
    /// backup, <see cref="ResumedFromBackup"/>, or <c>CAMPAIGN RESUMED FROM THE
    /// BACKUP OF SLOT 2, ONE SAVE OLDER</c>. Each starts with
    /// <see cref="BetweenRunsHud.Resumed"/>, so each is news, not a warning. The
    /// scratch slot, which no title loads, is spoken of as the autosave is.
    /// </summary>
    internal static string Resumed(SaveSlot slot, bool backup)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (Unnamed(slot))
            return backup ? ResumedFromBackup : BetweenRunsHud.Resumed;
        return backup
            ? $"{BetweenRunsHud.Resumed} FROM THE BACKUP OF {slot.Name}, ONE SAVE OLDER"
            : $"{BetweenRunsHud.Resumed} FROM {slot.Name}";
    }

    /// <summary>What the title says first when a load brought nothing back: <c>NOTHING COULD BE LOADED FROM SLOT 2</c>, or <c>... FROM THE AUTOSAVE</c> — the slot by its name (<see cref="SaveSlot.Name"/>).</summary>
    internal static string NothingLoaded(SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return $"NOTHING COULD BE LOADED FROM {slot.Name}";
    }

    /// <summary>
    /// The title's word on a load of <paramref name="slot"/> that brought
    /// nothing back (R2): <see cref="NothingLoaded"/> first, then every file the
    /// load set aside, whole and in the load's order — the save, then its backup —
    /// or the file it could neither read nor move. The game goes back to the title
    /// with it, the listing read again, and nothing else is touched: a load makes no
    /// campaign, and the autosave is set aside for a manual slot's campaign only
    /// once one has come back (<see cref="MakeRoom"/>). Reads, writes and moves no
    /// file: the load has done all of that.
    /// </summary>
    /// <param name="load">What <see cref="Load"/> brought back from the slot: nothing.</param>
    /// <param name="slot">The slot loaded.</param>
    /// <exception cref="ArgumentException"><paramref name="load"/> brought a campaign back: its word is its own.</exception>
    internal static IReadOnlyList<string> NothingLoadedWord(SlotLoad load, SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(slot);
        if (load.Loaded)
            throw new ArgumentException("The load brought a campaign back: what it says is its own word, and the campaign is played.", nameof(load));
        return [NothingLoaded(slot), .. load.Notices];
    }

    /// <summary>
    /// The autosave refused its writes before the campaign now coming into play
    /// came into play (<see cref="InPlayWord"/>): that nothing is autosaved for the
    /// rest of the session, why, and what to do —
    /// <c>NOTHING IS AUTOSAVED THIS SESSION: EARLIER, CAMPAIGN.JSON COULD NOT BE READ
    /// (ANOTHER PROGRAM HAS IT OPEN) AND COULD NOT BE SET ASIDE (ANOTHER PROGRAM HAS IT
    /// OPEN) - LAUNCH THE GAME AGAIN TO SAVE</c>.
    /// </summary>
    /// <param name="why">Why the autosave refuses its writes (<see cref="SaveStore.WritesRefused"/>).</param>
    internal static string NotAutosaved(string why)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(why);
        return $"NOTHING IS AUTOSAVED THIS SESSION: EARLIER, {why.ToUpperInvariant()} - LAUNCH THE GAME AGAIN TO SAVE";
    }

    /// <summary>
    /// The save's word on a campaign coming into play — loaded from a slot, with
    /// the room it made in the autosave slot (<see cref="Load"/>,
    /// <see cref="MakeRoom"/>), or begun by NEW CAMPAIGN (<see cref="Abandon"/>) — as
    /// its first screen says it: <paramref name="said"/>, then
    /// <see cref="NotAutosaved"/> when <paramref name="autosave"/>, where the
    /// campaign autosaves, refuses its writes and nothing said yet says so.
    /// <para>
    /// <strong>A refusal holds for the session</strong> (<see cref="SaveStore.RefuseWrites"/>,
    /// 1f): a file that could be neither read nor moved is never written over while
    /// the game runs, even once another program has let go of it. The title lets
    /// the player try again, so a load that went back to it over a stuck autosave,
    /// or a NEW CAMPAIGN stopped by one, can be followed by a try that loads, or a
    /// NEW CAMPAIGN that goes through, with the refusal still standing: a campaign
    /// played and never autosaved. So its first screen says so, rather than leave it
    /// to the first autosave to fail; a relaunch is the retry. Letting a retry clear
    /// the refusal would change 1f's contract, and is the user's call, not taken
    /// here (S6's review). A refusal the load, the room it made or the new campaign
    /// met itself — a file that would not move as it was set aside — is said
    /// already, by its stuck notice (<see cref="StuckNotice"/>), and not twice.
    /// </para>
    /// Reads, writes and moves no file.
    /// </summary>
    /// <param name="said">What the load and the room it made, or the new campaign, say, in order.</param>
    /// <param name="autosave">The slot the campaign autosaves into: the autosave slot.</param>
    internal static IReadOnlyList<string> InPlayWord(IReadOnlyList<string> said, SaveSlot autosave)
    {
        ArgumentNullException.ThrowIfNull(said);
        ArgumentNullException.ThrowIfNull(autosave);
        if (autosave.Store.WritesRefused is not { } refused || said.Any(SaysStuck))
            return said;
        return [.. said, NotAutosaved(refused)];
    }

    /// <summary>
    /// Load <paramref name="slot"/>, Boot's resume as it always was, in this
    /// order:
    /// <list type="number">
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
    /// a slot that held nothing — and what follows is the caller's: the game goes
    /// back to the title, saying so (<see cref="NothingLoadedWord"/>). The
    /// autosave's unsaved-quit marker is not the load's: the title has taken it
    /// before anything is loaded (<see cref="TakeUnsaved"/>).
    /// </para>
    /// </summary>
    /// <param name="slot">The slot (<see cref="SaveSlots.Slot"/>): its store, and its name for what the load says.</param>
    /// <param name="content">The content loaded, which must be <see cref="GameContent.Current"/>: a campaign is restored under it.</param>
    /// <param name="report">Where the log lines go: every file read, set aside or found wanting, with the whole error.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    internal static SlotLoad Load(SaveSlot slot, GameContent content, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        RequireCurrent(content);
        var store = slot.Store;

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
                    return new SlotLoad(null, null, setAside);
            }
            Unmark(store, report);
        }

        foreach (bool backup in new[] { false, true })
        {
            if (!(backup ? store.HasBackup : store.HasSave))
                continue;
            Mark(store, report);   // before the restore, so even a restore that takes the process down is caught next time
            if (TryResume(store, backup, content, report, out var loaded, out string why))
            {
                store.Vouch(backup);

                // What plays first, then the numbers it plays under (PHASE5-084
                // wants that on the screen, whatever else there is to say), then
                // what became of any save set aside on the way.
                var notices = new List<string> { Resumed(slot, backup) };
                if (loaded.TuningChanged)
                    notices.Add(TuningChanged);
                notices.AddRange(setAside);
                return new SlotLoad(loaded.Campaign, loaded.View, notices);
            }
            if (!SetAside(store, backup, "unreadable", $"could not be read ({why})", setAside, report))
                break;   // stuck where it is: writes are refused, and nothing else is resumed
        }

        Unmark(store, report);
        report?.Invoke(setAside.Count == 0
            ? $"[Save] No campaign to load in {store.Folder}"
            : $"[Save] Nothing could be loaded from {store.Folder}");
        return new SlotLoad(null, null, setAside);
    }

    /// <summary>
    /// A dev-override launch: its scratch slot (<paramref name="scratch"/>, its
    /// store from <see cref="StoreFor"/>) emptied, a fresh campaign, straight into
    /// the dungeon when one is named, its first run on the seed when one is. The
    /// campaign's own saves — the autosave and every manual slot — are never opened.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="request"/> is no dev override, or <paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="scratch"/>'s store is not a dev-override slot: it is never emptied.</exception>
    internal static BootResult BootOverride(SaveSlot scratch, GameContent content, BootRequest request, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(scratch);
        RequireCurrent(content);
        ArgumentNullException.ThrowIfNull(request);
        var store = scratch.Store;
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
    /// NEW CAMPAIGN — on the title, or <c>--new-campaign</c> at launch: the
    /// autosave slot's backup and then its save are set aside as <c>abandoned</c> —
    /// never deleted — and a new campaign begins, which the game saves at once
    /// (<c>DistanceRpgGame.BeginNewCampaign</c>; USER DECISION 3). Both go. A backup
    /// left behind would be resumed as though it were the new campaign's own the
    /// first time no save stood beside it: after a quit mid-run before the new
    /// campaign was ever saved, or a crash, or a first save that cannot be played.
    /// The backup goes first, so the later save takes the later number. A file that
    /// will not move stops it there: the store refuses every write this session
    /// (<see cref="SetAside"/>), and what is left of the old campaign — its save,
    /// when that is still in place — is what CONTINUE finds next time. The manual
    /// slots are never touched. The old campaign's unsaved-quit marker goes with it,
    /// logged and never said: there is nothing left to say it of.
    /// </summary>
    /// <param name="slot">The autosave slot.</param>
    /// <param name="content">The content loaded, which must be <see cref="GameContent.Current"/>: the new campaign is built under it.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <param name="how">What asked for it, which each set-aside notice names: <c>--new-campaign</c> by default; null for NEW CAMPAIGN on the title, which the player has just chosen and the notice need not name.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    internal static BootResult Abandon(SaveSlot slot, GameContent content, Action<string>? report = null, string? how = Program.NewCampaignOption)
    {
        ArgumentNullException.ThrowIfNull(slot);
        RequireCurrent(content);
        var store = slot.Store;
        TakeUnsaved(store, report);

        string asked = how ?? "NEW CAMPAIGN";
        string what = how is null ? "was left for a new campaign" : $"was left for a new campaign ({how})";
        var setAside = new List<string>();
        foreach (bool backup in new[] { true, false })
        {
            if (!(backup ? store.HasBackup : store.HasSave))
                continue;
            if (!SetAside(store, backup, "abandoned", what, setAside, report))
            {
                report?.Invoke($"[Save] {asked} stops there: nothing more is set aside and nothing is saved this session, so the campaign still in {store.Folder} is what CONTINUE finds next time");
                break;
            }
        }
        Unmark(store, report);   // nothing is being resumed
        report?.Invoke($"[Save] {asked}: a new campaign begins in {store.Folder}");
        return new BootResult(CampaignState.Start(content), Resumed: false, View: null, StraightInto: null, FirstSeed: null, [NewCampaignBegins, .. setAside]);
    }

    /// <summary>
    /// Make room in the autosave slot for the campaign manual slot
    /// <paramref name="number"/> brought back (USER DECISION 2): the loaded campaign
    /// autosaves into the autosave slot, so a different campaign there would be the
    /// backup after one autosave and gone after the second — for a player who never
    /// saved by hand, their whole campaign. So its files are set aside first,
    /// through <see cref="SetAside"/> as every set-aside goes: the backup, then the
    /// save, so the later save takes the later number, each named for why — the
    /// one the resume marker guards (the save, or the backup when there is no save)
    /// <c>crashed</c>, one the codec refuses <c>unreadable</c>, anything else
    /// <c>replaced</c> — and never deleted; then the resume marker, which guarded a
    /// file that is gone, is cleared. The caller calls it only once a load has
    /// brought a campaign back, and before anything is written — so a load that
    /// restores nothing leaves the autosave exactly as it was — and only when the
    /// title asked (<see cref="SaveListing.NeedsRoom"/>). A file that will not move
    /// stops it there: the autosave refuses every write this session, and nothing
    /// is autosaved over what is left.
    /// </summary>
    /// <param name="autosave">The autosave slot.</param>
    /// <param name="number">The manual slot whose campaign was loaded, which the notices name.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <returns>What to say on the loaded screen, after the load's own word: each file set aside, where, and how to try it again — or the file that would not move.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is not a manual slot's.</exception>
    internal static IReadOnlyList<string> MakeRoom(SaveStore autosave, int number, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(autosave);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        string slot = SaveSlots.Label(number).ToLowerInvariant();
        var setAside = new List<string>();

        // The file the resume marker guards is the one a load of the autosave reads
        // first: the save when there is one, and the backup when there is only that.
        bool marked = autosave.WasResuming;
        bool saveThere = autosave.HasSave;
        foreach (bool backup in new[] { true, false })
        {
            if (!(backup ? autosave.HasBackup : autosave.HasSave))
                continue;
            string reason, what;
            if (marked && backup == !saveThere)
                (reason, what) = ("crashed", "crashed the game the last time it was resumed");
            else if (Refusal(autosave, backup, report) is { } why)
                (reason, what) = ("unreadable", $"could not be read ({why})");
            else
                (reason, what) = ("replaced", $"made way for {slot}");
            if (!SetAside(autosave, backup, reason, what, setAside, report, stuckWhat: $"was to make way for {slot}"))
            {
                report?.Invoke($"[Save] Making room for {slot} stops there: nothing is autosaved this session, so what is left in {autosave.Folder} is never written over");
                return setAside;
            }
        }
        Unmark(autosave, report);
        report?.Invoke(setAside.Count == 0
            ? $"[Save] The autosave slot holds nothing: nothing to set aside for {slot}"
            : $"[Save] The autosave slot is clear for {slot}'s campaign: its autosaves go into {autosave.Folder}");
        return setAside;
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
    /// session so nothing takes its place, and the notice says that instead — in
    /// <paramref name="stuckWhat"/>'s words when what happened to the file and what
    /// was to happen to it read differently (a file that "made way" did not, when
    /// it would not move).
    /// </summary>
    private static bool SetAside(SaveStore store, bool backup, string reason, string what, List<string> notices, Action<string>? report,
        string? stuckWhat = null)
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
            string stuck = stuckWhat ?? what;
            report?.Invoke($"[Save] {from} {stuck} and could not be set aside - nothing is saved this session, so it is never written over: {error}");
            store.RefuseWrites($"{file} {stuck} and could not be set aside ({why})");
            notices.Add(StuckNotice(file, stuck, why));
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
    /// Whether the last time play was left here the autosave could not be written:
    /// its marker (<see cref="SaveStore.MarkUnsaved"/>) read, logged with what that
    /// quit wrote in it, and cleared, so it is said once. The title takes the
    /// autosave's when it opens, before anything is loaded, and says it
    /// (<see cref="QuitUnsaved"/>); NEW CAMPAIGN takes it as it sets the campaign it
    /// spoke of aside, and says nothing. A marker that is there and cannot be read
    /// is one all the same. One that will not go is said again the next time, and
    /// the log says why.
    /// </summary>
    internal static bool TakeUnsaved(SaveStore store, Action<string>? report)
    {
        ArgumentNullException.ThrowIfNull(store);
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

        report?.Invoke($"[Save] {store.UnsavedPath} was left by the last quit, which could not write the autosave - what it holds is from before then: {wrote.ReplaceLineEndings(" ").Trim()}");
        try
        {
            store.ClearUnsaved();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {store.UnsavedPath} could not be cleared - the title says this again next time: {error}");
        }
        return true;
    }

    /// <summary>
    /// Leave the unsaved-quit marker for the title to say (<see cref="SaveStore.MarkUnsaved"/>):
    /// this quit could not save the campaign, for <paramref name="why"/>. One that
    /// cannot be written either leaves the log alone to say so.
    /// </summary>
    private static void MarkUnsaved(SaveStore store, string why, Action<string>? report)
    {
        try
        {
            store.MarkUnsaved(why);
            report?.Invoke($"[Save] {store.UnsavedPath} written: the title says that what the autosave holds is from before this quit");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            report?.Invoke($"[Save] {store.UnsavedPath} could not be written either - the title cannot say this quit went unsaved: {error}");
        }
    }

    /// <summary>
    /// Save <paramref name="campaign"/> to <paramref name="slot"/> at a safe point
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
    /// <param name="slot">The slot: its store, and its name, which a failure's words name (<see cref="NotSaved"/>) — the autosave's and the scratch slot's, or a manual slot's, saved by hand, where a file that would not move is said the same way, by what refuses the slot's writes.</param>
    /// <param name="campaign">The campaign, at a safe point.</param>
    /// <param name="view">What the scene shows, carried for it; null for nothing to say.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <param name="savedAt">When it is saved, the edge's clock: written as the save's time. Null records none.</param>
    /// <param name="said">Where the notices of what the guard set aside go, a list the caller hands in; null to leave them to the log.</param>
    /// <returns>Null when saved, or the notice a failed write shows.</returns>
    internal static string? Save(SaveSlot slot, CampaignState campaign, ViewDto? view = null, Action<string>? report = null,
        DateTimeOffset? savedAt = null, List<string>? said = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(campaign);
        var store = slot.Store;
        try
        {
            string text = SaveCodec.Write(SaveGame.Capture(campaign, view, savedAt));
            if (Holds(store, text))
            {
                report?.Invoke($"[Save] {store.SavePath} already holds this campaign - nothing written, the backup kept");
                return null;
            }
            if (store.WritesRefused is null && MakeWay(store, report, said) is { } stuck)
                return Unnamed(slot) ? stuck : NotSaved(slot, store.WritesRefused ?? stuck);
            store.Write(text);
            report?.Invoke($"[Save] Wrote {store.SavePath}");
            Written(store, report);
            return null;
        }
        catch (Exception error)
        {
            report?.Invoke($"[Save] Could not write {store.SavePath} - the save there is kept: {error}");
            string why = store.WritesRefused ?? SaveStore.Why(error, store.Folder, writing: true);
            return NotSaved(slot, why);
        }
    }

    /// <summary>
    /// A save by hand into a manual <paramref name="slot"/> — the save list's
    /// choice on the pause menu or the menu between runs (the user's ruling of
    /// 2026-09-25 on the save model, R2(d): "a manual save can be made at ANY SAFE
    /// POINT"). Refused off a safe point (<see cref="SaveGame.IsSafePoint"/>) —
    /// never while the enemies are moving — with the words the menu says there,
    /// writing nothing: the menu opens the list only on one, and this asks again
    /// when the write comes. Otherwise written
    /// through <see cref="Save"/>, the one door every write goes through, whose guard
    /// sets aside a <see cref="SaveStore.FileName"/> the codec refuses — and such a
    /// backup the replace would rotate away — <strong>whatever the list said of the
    /// slot</strong>: the files decide, never a listing read before them. So a slot
    /// whose save cannot be read and whose backup can has its save set aside, the
    /// write moved into place, and the backup untouched. A slot whose store refuses
    /// its writes this session answers with why.
    /// </summary>
    /// <param name="slot">The manual slot (<see cref="SaveSlots.Slot"/>): its store, and its name, which the words name.</param>
    /// <param name="campaign">The campaign in play.</param>
    /// <param name="view">What the playing scene shows, carried for it mid-run; null between runs.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <param name="savedAt">When it is saved, the edge's clock.</param>
    /// <returns>Whether it was saved, and what the menu says: <c>SAVED TO SLOT 2</c> — then each file the write set aside first, where, and how to try it again — or why not.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slot"/> is no manual slot: the autosave is written by the game alone, and the scratch slot by its dev override.</exception>
    internal static (bool Saved, string Notice) SaveTo(SaveSlot slot, CampaignState campaign, ViewDto? view,
        Action<string>? report = null, DateTimeOffset? savedAt = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(campaign);
        if (slot.Number is not > SaveSlots.Autosave)
            throw new ArgumentOutOfRangeException(nameof(slot), slot.Number, "A save by hand goes into a manual slot: the autosave is written by the game alone, and the scratch slot by its dev override.");
        if (!SaveGame.IsSafePoint(campaign))
        {
            report?.Invoke($"[Save] Not saving into {slot.Name}: the campaign is off a safe point ({campaign.Visit!.Turns!.Phase}) - nothing is written, and {slot.Store.SavePath} keeps what it holds");
            return (false, PauseMenu.SaveOnYourTurn);
        }

        var said = new List<string>();
        if (Save(slot, campaign, campaign.Visit is null ? null : view, report, savedAt, said) is { } failed)
            return (false, failed);
        return (true, string.Join(" - ", [SavedTo(slot), .. said]));
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
    /// The save leaving play takes (1f): the campaign settled to its next safe
    /// point, then saved (<see cref="Save"/>) with the view as the scene has it once
    /// settled. Every way out of play arrives here — the menus' <c>3  QUIT TO
    /// TITLE</c> (the user's ruling of 2026-09-25 on the save model, R2(e)) and the
    /// window's close button alike. A quit that finds the enemies taking their turn
    /// — its banner, their walks, their beats — plays it out to the next player turn
    /// first, one <paramref name="step"/> at a time
    /// (<see cref="SaveGame.SettleToSafePoint"/>), so what is saved is the turn the
    /// player would have met. A settle in which the enemies wipe the party ends the
    /// run in <c>Logic/</c>, and the campaign is saved between runs, the run's
    /// summary with it.
    /// <para>
    /// <strong>No screen of the campaign follows</strong> — the window closes, or
    /// the title opens with no campaign in play — so a quit that cannot save — the
    /// write fails, or the enemies' turn cannot be played out — leaves
    /// <see cref="SaveStore.UnsavedName"/> beside the save it keeps, and the title
    /// says, when it next opens (at once, after QUIT TO TITLE), that what the
    /// autosave holds is from before this quit (<see cref="QuitUnsaved"/>). What the
    /// write guard sets aside on the way is logged. And a quit that saves — the
    /// save written, or the disk holding it already — tells its caller
    /// (<paramref name="saved"/>), which takes back the word of an earlier save that
    /// failed: the disk holds the campaign again, so the title opened by QUIT TO
    /// TITLE never says the campaign could not be saved when the quit's own save
    /// went through (S7's review).
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
    /// <param name="slot">The slot the quit saves to: the autosave, or a dev override's scratch slot.</param>
    /// <param name="campaign">The campaign being quit.</param>
    /// <param name="view">What the playing scene shows once the settle is done — read then, since a turn's start re-decides the march and may hand the lead to a living member — or null for nothing to say.</param>
    /// <param name="step">The playing scene's logic step; null when no scene is playing the visit, which leaves nothing to settle.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <param name="savedAt">When the quit saves, the edge's clock; null records no time.</param>
    /// <param name="saved">Told, once, when the campaign stands on the disk as it was left — the save written, or held there already — and never when the quit could not save; null to tell nobody.</param>
    /// <returns>True once the campaign stood at a safe point and the save was made — a write that fails is logged, never thrown, the save on the disk kept and the marker left; false when it never got to one, nothing was written, and the marker is left.</returns>
    internal static bool SaveOnQuit(SaveSlot slot, CampaignState campaign, Func<ViewDto?>? view, Action? step, Action<string>? report = null,
        DateTimeOffset? savedAt = null, Action? saved = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(campaign);
        var store = slot.Store;
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
        if (Save(slot, campaign, viewNow, report, savedAt) is { } failed)
        {
            report?.Invoke($"[Save] {failed}");
            MarkUnsaved(store, failed, report);
        }
        else
        {
            saved?.Invoke();   // the disk holds the campaign as it was left
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

    /// <summary>How every stuck notice ends (<see cref="StuckNotice"/>): the slot takes no more saves this session.</summary>
    private const string NothingSavedThisSession = "NOTHING IS SAVED THIS SESSION, SO IT IS NEVER WRITTEN OVER";

    /// <summary>A save that could not be read and could not be moved: it stays, and nothing is saved over it.</summary>
    internal static string StuckNotice(string file, string what, string why)
        => $"{file.ToUpperInvariant()} {what.ToUpperInvariant()} AND COULD NOT BE SET ASIDE ({why.ToUpperInvariant()}) - {NothingSavedThisSession}";

    /// <summary>Whether <paramref name="notice"/> is a stuck notice (<see cref="StuckNotice"/>), which says already that nothing more is saved this session.</summary>
    private static bool SaysStuck(string notice) => notice.EndsWith(NothingSavedThisSession, StringComparison.Ordinal);

    /// <summary>
    /// A write into <paramref name="slot"/> that failed, and why, in a notice:
    /// what the slot held before is kept. A manual slot, saved by hand, is named —
    /// <c>SLOT 2 COULD NOT BE SAVED (ANOTHER PROGRAM HAS IT OPEN) - WHAT IT HELD IS
    /// KEPT - SEE THE LOG</c> — and the autosave and the scratch slot are the
    /// campaign's own save: <c>THE CAMPAIGN COULD NOT BE SAVED (THE DISK IS FULL) -
    /// THE LAST SAVE IS KEPT - SEE THE LOG</c>. A blank why is taken as it is:
    /// <see cref="Save"/>, which never throws, hands it the file system's.
    /// </summary>
    internal static string NotSaved(SaveSlot slot, string why)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(why);
        return Unnamed(slot)
            ? $"THE CAMPAIGN COULD NOT BE SAVED ({why.ToUpperInvariant()}) - THE LAST SAVE IS KEPT - SEE THE LOG"
            : $"{slot.Name} COULD NOT BE SAVED ({why.ToUpperInvariant()}) - WHAT IT HELD IS KEPT - SEE THE LOG";
    }

    /// <summary>How a save by hand that went through starts (<see cref="SavedTo"/>): the menu draws it green.</summary>
    internal const string SavedPrefix = "SAVED TO ";

    /// <summary>A save by hand that went through: <c>SAVED TO SLOT 2</c> — the slot by its name (<see cref="SaveSlot.Name"/>).</summary>
    internal static string SavedTo(SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return $"{SavedPrefix}{slot.Name}";
    }

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
