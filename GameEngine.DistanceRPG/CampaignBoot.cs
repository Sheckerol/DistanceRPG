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
    IReadOnlyList<string> Notices)
{
    /// <summary>
    /// What NEW CAMPAIGN set aside (<see cref="CampaignBoot.Abandon"/>), in the
    /// order it moved them: each file's path now, whether it was the backup, and
    /// the notice that says so — what <see cref="CampaignBoot.PutBack"/> gives back
    /// should the new campaign never begin. None for a dev override's campaign.
    /// </summary>
    public IReadOnlyList<(string Path, bool Backup, string Notice)> Abandoned { get; init; } = [];

    /// <summary>
    /// Whether NEW CAMPAIGN took the slot's unsaved-quit marker, without a word
    /// (<see cref="CampaignBoot.TakeUnsaved"/>) — <c>--new-campaign</c> does, before
    /// any title has said it — which <see cref="CampaignBoot.PutBack"/> says should
    /// the new campaign never begin.
    /// </summary>
    public bool TookUnsaved { get; init; }
}

/// <summary>
/// What loading one slot brought back (<see cref="CampaignBoot.Load"/>): the
/// campaign and the view its save carried — or neither, when the slot held
/// nothing that would load — and what to say, in order and whole: that the
/// campaign resumed, from which slot or its backup; that the numbers changed
/// since it was saved; and every file set aside on the way, where, and how to try
/// it again. A load never makes a campaign: one that brings nothing back goes
/// back to the title, which says so first (<see cref="CampaignBoot.NothingLoadedWord"/>);
/// and a slot that takes no saves this session brings nothing back and says why
/// (<see cref="CampaignBoot.TakesNoSaves"/>).
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
/// (<see cref="NothingLoadedWord"/>); the two ways a campaign is begun rather
/// than loaded — a dev override's scratch campaign (<see cref="BootOverride"/>)
/// and NEW CAMPAIGN (<see cref="Abandon"/>, and <see cref="PutBack"/> when it
/// never begins); a save that never throws and never costs a file
/// (<see cref="Save"/>), a save by hand among them, and what the menu answers it
/// (<see cref="SaveAnswer"/>); and the save leaving play takes mid-run
/// (<see cref="SaveOnQuit"/>). Engine-free, so every
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
/// <see cref="TryAgain"/> — but NEW CAMPAIGN's, which end <see cref="MoveToPlay"/>.
/// A slot that brings nothing back makes no campaign: the
/// game goes back to the title, which says so first, then what was set aside
/// (<see cref="NothingLoadedWord"/>).</item>
/// <item><strong>A campaign saves into the slot it came from</strong> (the user's
/// ruling of 2026-09-25 on the save slots, R5: "you choose a save slot and any
/// saving happens on that save slot"): the slot it was loaded from, or the one
/// NEW CAMPAIGN began it in — chosen on the title, 1 to the count — takes every
/// save of it — the game's own, the quit's and SAVE GAME's — and no load sets
/// another slot's files aside, nor does NEW CAMPAIGN, which sets aside what its
/// own slot held alone.</item>
/// <item><strong>A slot that takes no saves brings no campaign into
/// play</strong>: a refusal holds for the session, and a campaign played in a
/// slot that refuses its writes could never be saved, so its load loads nothing
/// and touches nothing (<see cref="TakesNoSaves"/>), and NEW CAMPAIGN does not
/// begin there — nor where its first save fails, what it set aside put back
/// (<see cref="PutBack"/>, <see cref="NotBegunWord"/>).</item>
/// <item><strong>A save by hand goes into the campaign's own slot, asking
/// nothing</strong> (the user's ruling of 2026-09-25 on the save slots, R5:
/// "instead of choosing a save slot everytime you save"): through
/// <see cref="Save"/>, the one door, at a safe point only — the game asks it
/// (R2(d): never while the enemies are moving) — and a save of the campaign the
/// slot holds already stamps its time, the backup kept. What the menu answers is
/// <see cref="SaveAnswer"/>.</item>
/// <item><strong>No write costs a file</strong> (1f): before any write, a
/// <see cref="SaveStore.FileName"/> there that this session does not know and no
/// save can be read from is set aside first, and so is such a backup the
/// replace would otherwise rotate away — the codec's refusal decides, never a
/// listing's word on the slot. And a write clears its slot's resume marker: the
/// file it guarded is gone.</item>
/// <item><strong>Leaving play without saving is said on the title</strong> (1f):
/// no screen of the campaign follows a quit — the window closes, or the title
/// opens — so the quit's own door leaves <see cref="SaveStore.UnsavedName"/>
/// beside the save of the slot it could not save into (<see cref="SaveOnQuit"/>),
/// and the title takes every slot's (<see cref="TakeEveryUnsaved"/>) and says,
/// before anything is loaded, that what that slot holds is from before then
/// (<see cref="QuitUnsaved"/>) — at once, after QUIT TO TITLE. A quit that does
/// save tells its caller so, which takes back what an earlier failed save
/// said.</item>
/// <item><strong>A different tuning is said, and plays on</strong> (§5.3,
/// PHASE5-084): "silently changing a saved game's rules underneath the player is
/// the failure to avoid", so a campaign saved under other numbers resumes with
/// <see cref="TuningChanged"/> on its first screen.</item>
/// <item><strong>The dev overrides play in their own slot</strong>: a launch
/// with <c>DISTANCERPG_DUNGEON</c> or <c>DISTANCERPG_SEED</c> empties
/// <see cref="SaveStore.OverrideSlot"/> and plays a fresh campaign there, so it
/// is as repeatable as it ever was and the campaign — every slot, and the old
/// autosave — is untouched.</item>
/// </list>
/// </summary>
internal static class CampaignBoot
{
    /// <summary>NEW CAMPAIGN began one — on the title, or <c>--new-campaign</c> at launch — whatever was set aside for it.</summary>
    internal const string NewCampaignBegins = "A NEW CAMPAIGN BEGINS";

    /// <summary>PHASE5-084: the save's tuning hash is not the loaded tuning's.</summary>
    internal const string TuningChanged = "THE NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS ON UNDER THE NEW ONES";

    /// <summary>How every set-aside notice ends but NEW CAMPAIGN's: how to try the file again, where it lies.</summary>
    internal const string TryAgain = "RENAME IT TO CAMPAIGN.JSON TO TRY AGAIN";

    /// <summary>
    /// How NEW CAMPAIGN's set-aside notices end (<see cref="Abandon"/>): how to play
    /// the old campaign again. Renamed back where it lies it would take the name of
    /// the new campaign's save, which is written into that same folder at once, so
    /// it is moved into an empty slot folder instead — a slot folder holding only
    /// files set aside lists as empty — and loads from there like any slot.
    /// </summary>
    internal const string MoveToPlay = "TO PLAY IT AGAIN, MOVE IT INTO AN EMPTY SLOT FOLDER AS CAMPAIGN.JSON";

    /// <summary>A dev-override launch's word on the campaign it plays.</summary>
    internal const string OverrideNotice = "A DEV OVERRIDE LAUNCH - A SCRATCH CAMPAIGN, SAVED APART - YOUR CAMPAIGN IS UNTOUCHED";

    /// <summary>The store a launch plays on: the dev-override slot inside <paramref name="saveDirectory"/> for an override launch, and the save folder itself otherwise.</summary>
    internal static SaveStore StoreFor(string saveDirectory, BootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SaveStore(SaveStore.SlotDirectory(saveDirectory, request.DevOverride));
    }

    /// <summary>
    /// What loading a slot says first, in green, the slot by its name
    /// (<see cref="SaveSlot.Name"/>): <c>CAMPAIGN RESUMED FROM SLOT 2</c>, or
    /// <c>... FROM THE OLD AUTOSAVE</c>; and from a backup, <c>CAMPAIGN RESUMED FROM
    /// THE BACKUP OF SLOT 2, ONE SAVE OLDER</c>, or — for a slot whose name is no
    /// number, which "the backup of" would push past one notice row —
    /// <c>CAMPAIGN RESUMED FROM THE OLD AUTOSAVE BACKUP, ONE SAVE OLDER</c>. Each
    /// starts with <see cref="BetweenRunsHud.Resumed"/>, so each is news, not a
    /// warning, and each is one notice row (<see cref="NoticeText.LineChars"/>).
    /// The scratch slot, which no title loads, is named the same way.
    /// </summary>
    internal static string Resumed(SaveSlot slot, bool backup)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (!backup)
            return $"{BetweenRunsHud.Resumed} FROM {slot.Name}";
        return slot.Number is > SaveSlots.OldAutosave
            ? $"{BetweenRunsHud.Resumed} FROM THE BACKUP OF {slot.Name}, ONE SAVE OLDER"
            : $"{BetweenRunsHud.Resumed} FROM {slot.Name} BACKUP, ONE SAVE OLDER";
    }

    /// <summary>What the title says first when a load brought nothing back: <c>NOTHING COULD BE LOADED FROM SLOT 2</c>, or <c>... FROM THE OLD AUTOSAVE</c> — the slot by its name (<see cref="SaveSlot.Name"/>).</summary>
    internal static string NothingLoaded(SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return $"NOTHING COULD BE LOADED FROM {slot.Name}";
    }

    /// <summary>
    /// The title's word on a load of <paramref name="slot"/> that brought
    /// nothing back (R2): <see cref="NothingLoaded"/> first, then every file the
    /// load set aside, whole and in the load's order — the save, then its backup —
    /// or the file it could neither read nor move, or that the slot takes no saves
    /// this session (<see cref="TakesNoSaves"/>). The game goes back to the title
    /// with it, the listing read again, and nothing else is touched: a load makes no
    /// campaign, and sets no other slot's files aside. Reads, writes and moves no
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
    /// <paramref name="slot"/> refused its writes earlier this session, so no
    /// campaign comes into play there: why, and what to do —
    /// <c>SLOT 2 TAKES NO SAVES THIS SESSION: EARLIER, CAMPAIGN.JSON COULD NOT BE READ
    /// (ANOTHER PROGRAM HAS IT OPEN) AND COULD NOT BE SET ASIDE (ANOTHER PROGRAM HAS IT
    /// OPEN) - LAUNCH THE GAME AGAIN TO PLAY IN IT</c>.
    /// <para>
    /// <strong>A refusal holds for the session</strong> (<see cref="SaveStore.RefuseWrites"/>,
    /// 1f): a file that could be neither read nor moved is never written over while
    /// the game runs, even once another program has let go of it. Every save of a
    /// campaign goes into its own slot (R5), so a campaign played in a slot that
    /// refuses its writes could never be saved, and what was played would be lost
    /// at the quit; not beginning it loses nothing. So a load of such a slot loads
    /// nothing (<see cref="Load"/>), and NEW CAMPAIGN does not begin there
    /// (<see cref="NotBegunWord"/>); the player chooses another slot, or launches
    /// again, which is the retry. Letting a retry clear the refusal would change
    /// 1f's contract, and is the user's call, not taken here (S6's review).
    /// </para>
    /// </summary>
    /// <param name="slot">The slot, by its name.</param>
    /// <param name="why">Why it refuses its writes (<see cref="SaveStore.WritesRefused"/>).</param>
    internal static string TakesNoSaves(SaveSlot slot, string why)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentException.ThrowIfNullOrWhiteSpace(why);
        return $"{slot.Name} TAKES NO SAVES THIS SESSION: EARLIER, {why.ToUpperInvariant()} - LAUNCH THE GAME AGAIN TO PLAY IN IT";
    }

    /// <summary>What the title says first when NEW CAMPAIGN did not begin one: <c>NO NEW CAMPAIGN BEGAN IN SLOT 2</c> — the slot by its name (<see cref="SaveSlot.Name"/>), 1 to the count (NEW CAMPAIGN never begins in the old autosave).</summary>
    internal static string NoNewCampaignIn(SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return $"NO NEW CAMPAIGN BEGAN IN {slot.Name}";
    }

    /// <summary>
    /// The title's word on a NEW CAMPAIGN that did not begin in
    /// <paramref name="slot"/>: <see cref="NoNewCampaignIn"/> first, then
    /// <paramref name="said"/>, whole and in order — why: the slot takes no saves
    /// this session (<see cref="TakesNoSaves"/>); or a file it would not move as it
    /// was set aside, by its stuck notice alone; or the new campaign's first save,
    /// which failed (<see cref="NotSaved"/>) — then any file that would not go back
    /// where it was (<see cref="PutBack"/>). The game goes back to the title with
    /// it, with no campaign in play. Reads, writes and moves no file.
    /// </summary>
    /// <param name="slot">The slot NEW CAMPAIGN was to begin in.</param>
    /// <param name="said">Why it did not, then what stays set aside.</param>
    internal static IReadOnlyList<string> NotBegunWord(SaveSlot slot, IReadOnlyList<string> said)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(said);
        return [NoNewCampaignIn(slot), .. said];
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
    /// slot's unsaved-quit marker is not the load's: the title has taken it before
    /// anything is loaded (<see cref="TakeEveryUnsaved"/>).
    /// </para>
    /// <para>
    /// <strong>A slot that takes no saves this session loads nothing, and touches
    /// nothing</strong> (R5): the campaign it brought back would save into it, and
    /// every save would fail. So a store that refuses its writes
    /// (<see cref="SaveStore.WritesRefused"/>) writes no marker, reads, vouches for
    /// and sets aside nothing, and comes back with no campaign and
    /// <see cref="TakesNoSaves"/> alone.
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
        if (store.WritesRefused is { } refused)
        {
            report?.Invoke($"[Save] Nothing is loaded from {store.Folder}, and nothing there is touched: it takes no saves this session, so the campaign it holds could not be saved - {refused}");
            return new SlotLoad(null, null, [TakesNoSaves(slot, refused)]);
        }

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
    /// campaign's own saves — every slot, and the old autosave — are never opened.
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
    /// NEW CAMPAIGN — on the title, or <c>--new-campaign</c> at launch: the slot the
    /// new campaign begins in has its backup and then its save set aside as
    /// <c>abandoned</c> — never deleted — each notice ending <see cref="MoveToPlay"/>,
    /// and a new campaign is handed back, which the game saves into the slot before
    /// it plays it (<c>DistanceRpgGame.BeginNewCampaign</c>; SLOT DECISION 2). Both
    /// go. A backup left behind would be resumed as though it were the new
    /// campaign's own the first time no save stood beside it: after a quit mid-run
    /// before the new campaign was ever saved, or a crash, or a first save that
    /// cannot be played. The backup goes first, so the later save takes the later
    /// number. What was moved is listed (<see cref="BootResult.Abandoned"/>), so a
    /// new campaign that never begins gives it back (<see cref="PutBack"/>). A file
    /// that will not move stops it there: the store refuses every write this
    /// session (<see cref="SetAside"/>), so no campaign begins in the slot, and
    /// what is left of the old campaign — its save, still in place — is what
    /// CONTINUE finds next time. Every other slot is never touched. The old
    /// campaign's unsaved-quit marker goes with it, logged and not said — there is
    /// nothing left to say it of, unless the new campaign never begins
    /// (<see cref="BootResult.TookUnsaved"/>). Its resume marker is left where it
    /// is: the new campaign's first save clears it as it goes through
    /// (<see cref="Save"/>), and a new campaign that never begins leaves it
    /// guarding the save put back, as it guarded it before.
    /// </summary>
    /// <param name="slot">The slot the new campaign begins in: the one chosen on the title's NEW CAMPAIGN list, or, for <c>--new-campaign</c>, the one that list would start on (<see cref="SaveListing.NewCampaignSlot"/>) — 1 to the count, never the old autosave.</param>
    /// <param name="content">The content loaded, which must be <see cref="GameContent.Current"/>: the new campaign is built under it.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <param name="how">What asked for it, which each set-aside notice names: <c>--new-campaign</c> by default; null for NEW CAMPAIGN on the title, which the player has just chosen and the notice need not name.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not <see cref="GameContent.Current"/>.</exception>
    internal static BootResult Abandon(SaveSlot slot, GameContent content, Action<string>? report = null, string? how = Program.NewCampaignOption)
    {
        ArgumentNullException.ThrowIfNull(slot);
        RequireCurrent(content);
        var store = slot.Store;
        bool tookUnsaved = TakeUnsaved(store, report);

        string asked = how ?? "NEW CAMPAIGN";
        string what = how is null ? "was left for a new campaign" : $"was left for a new campaign ({how})";
        var setAside = new List<string>();
        var moved = new List<(string Path, bool Backup, string Notice)>();
        bool stuck = false;
        foreach (bool backup in new[] { true, false })
        {
            if (!(backup ? store.HasBackup : store.HasSave))
                continue;
            if (!SetAside(store, backup, "abandoned", what, setAside, report, moved, MoveToPlay))
            {
                report?.Invoke($"[Save] {asked} stops there: nothing more is set aside, and {store.Folder} takes no saves this session - no new campaign begins there, and the campaign still in it is what CONTINUE finds next time");
                stuck = true;
                break;
            }
        }
        if (!stuck)
            report?.Invoke($"[Save] {asked}: a new campaign begins in {store.Folder}");
        return new BootResult(CampaignState.Start(content), Resumed: false, View: null, StraightInto: null, FirstSeed: null, [NewCampaignBegins, .. setAside])
        {
            Abandoned = moved,
            TookUnsaved = tookUnsaved,
        };
    }

    /// <summary>
    /// A new campaign that never began in <paramref name="slot"/> gives back what
    /// NEW CAMPAIGN set aside for it (<paramref name="abandoned"/>'s
    /// <see cref="BootResult.Abandoned"/>): its first save did not go through, or
    /// setting the old campaign aside stopped at a file that would not move. Each
    /// file goes back where it was, the save before the backup — the reverse of
    /// the order they left in — by a move that refuses a name that is taken
    /// (<see cref="SaveStore.PutBack"/>), so nothing is deleted or written over and
    /// the slot is as it was, its backup one save deep, and the resume marker
    /// <see cref="Abandon"/> left there guarding it as before. A file that will not
    /// go back stays set aside, and the notice that said where is said again. The
    /// unsaved-quit marker Abandon took without a word
    /// (<see cref="BootResult.TookUnsaved"/>) is said now, once, as the title would
    /// have said it (<see cref="QuitUnsaved"/>), since what the slot holds again is
    /// from before that quit.
    /// </summary>
    /// <param name="slot">The slot NEW CAMPAIGN was to begin in.</param>
    /// <param name="abandoned">What <see cref="Abandon"/> handed back for it.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <returns>The set-aside notice of each file that would not go back, in the order they were set aside, then the unsaved-quit word Abandon took, when the slot holds the old campaign again; none when all went back and there was no such word.</returns>
    internal static IReadOnlyList<string> PutBack(SaveSlot slot, BootResult abandoned, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(abandoned);
        var store = slot.Store;
        var kept = new List<string>();
        foreach (var (path, backup, notice) in abandoned.Abandoned.Reverse())
        {
            string to = backup ? store.BackupPath : store.SavePath;
            try
            {
                store.PutBack(path, backup);
                report?.Invoke($"[Save] {path} put back as {to}: no new campaign began there");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                report?.Invoke($"[Save] {path} could not be put back as {to} - it stays set aside, never deleted or written over: {error}");
                kept.Insert(0, notice);
            }
        }
        if (abandoned.TookUnsaved && (store.HasSave || store.HasBackup))
        {
            report?.Invoke($"[Save] The unsaved-quit marker taken from {store.Folder} is said after all: no new campaign began there, and what it holds is from before that quit");
            kept.Add(QuitUnsaved(slot));
        }
        return kept;
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
    /// in <paramref name="notices"/> by its new full path, ending
    /// <paramref name="tail"/> — and in <paramref name="moved"/>, when handed one,
    /// what went where. False when it could not be moved: then it stays where it
    /// is, the store refuses every write this session so nothing takes its place,
    /// and the notice says that instead.
    /// </summary>
    private static bool SetAside(SaveStore store, bool backup, string reason, string what, List<string> notices, Action<string>? report,
        List<(string Path, bool Backup, string Notice)>? moved = null, string tail = TryAgain)
    {
        string file = backup ? SaveStore.BackupName : SaveStore.FileName;
        string from = backup ? store.BackupPath : store.SavePath;
        try
        {
            string path = store.SetAside(reason, backup);
            report?.Invoke($"[Save] {from} {what} - set aside as {path}, never deleted or written over");
            string notice = SetAsideNotice(file, what, path, tail);
            notices.Add(notice);
            moved?.Add((path, backup, notice));
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
            report?.Invoke($"[Save] {from} {what} and could not be set aside - its slot takes no saves this session, so it is never written over: {error}");
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
    /// The title's word on every slot whose campaign could not be saved the last
    /// time play was left: each slot's unsaved-quit marker taken
    /// (<see cref="TakeUnsaved"/>) — the save folder's, then slot 1 to
    /// <see cref="SaveSlots.Most"/>, above the count too, since a campaign saves
    /// into whichever slot it came from (R5) — and each said once, in slot order
    /// (<see cref="QuitUnsaved"/>). The title takes them when it opens, before the
    /// slots are listed and anything is loaded.
    /// </summary>
    /// <param name="slots">The save folder's slots.</param>
    /// <param name="report">Where the log lines go: each marker, with what its quit wrote in it.</param>
    internal static IReadOnlyList<string> TakeEveryUnsaved(SaveSlots slots, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var said = new List<string>();
        for (int number = SaveSlots.OldAutosave; number <= SaveSlots.Most; number++)
        {
            var slot = slots.Slot(number);
            if (TakeUnsaved(slot.Store, report))
                said.Add(QuitUnsaved(slot));
        }
        return said;
    }

    /// <summary>
    /// Whether the last time play was left here the campaign could not be saved:
    /// its marker (<see cref="SaveStore.MarkUnsaved"/>) read, logged with what that
    /// quit wrote in it, and cleared, so it is said once. The title takes every
    /// slot's when it opens, before anything is loaded, and says each
    /// (<see cref="TakeEveryUnsaved"/>); NEW CAMPAIGN takes its slot's as it sets
    /// the campaign it spoke of aside, and says nothing — unless the new campaign
    /// never begins, and the old one is put back (<see cref="PutBack"/>). A marker
    /// that is there and cannot be read is one all the same. One that will not go
    /// is said again the next time, and the log says why.
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

        report?.Invoke($"[Save] {store.UnsavedPath} was left by the last quit, which could not save the campaign there - what the slot holds is from before then: {wrote.ReplaceLineEndings(" ").Trim()}");
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
            report?.Invoke($"[Save] {store.UnsavedPath} written: the title says that what the slot holds is from before this quit");
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
    /// keeps the time its state was first written — but for a save by hand
    /// (<paramref name="byHand"/>), which stamps the save's time as old games' saves
    /// stamp their file (the user's ruling of 2026-09-25 on the save slots, R5): its
    /// time alone is rewritten, and the backup is not rotated
    /// (<see cref="SaveStore.Restamp"/>), so one save deep holds, and the slot just
    /// saved by hand is the one CONTINUE names. Never throws: a write that
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
    /// one that will not move refuses the store's writes, and the failure's words
    /// say why. And a write clears the slot's resume marker, since the file it
    /// guarded is gone.
    /// </para>
    /// </summary>
    /// <param name="slot">The slot: its store, and its name, which a failure's words name (<see cref="NotSaved"/>) — the slot the campaign came from, where a file that would not move is said the same way, by what refuses the slot's writes.</param>
    /// <param name="campaign">The campaign, at a safe point.</param>
    /// <param name="view">What the scene shows, carried for it; null for nothing to say.</param>
    /// <param name="report">Where the log lines go.</param>
    /// <param name="savedAt">When it is saved, the edge's clock: written as the save's time. Null records none.</param>
    /// <param name="said">Where the notices of what the guard set aside go, a list the caller hands in; null to leave them to the log.</param>
    /// <param name="byHand">A save by hand — SAVE GAME on either menu — which restamps a save that holds the campaign already, rather than write nothing; the game's own saves and the quit's never do.</param>
    /// <returns>Null when saved, or the notice a failed write shows.</returns>
    internal static string? Save(SaveSlot slot, CampaignState campaign, ViewDto? view = null, Action<string>? report = null,
        DateTimeOffset? savedAt = null, List<string>? said = null, bool byHand = false)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(campaign);
        var store = slot.Store;
        try
        {
            string text = SaveCodec.Write(SaveGame.Capture(campaign, view, savedAt));
            if (Holds(store, text))
            {
                if (!byHand)
                {
                    report?.Invoke($"[Save] {store.SavePath} already holds this campaign - nothing written, the backup kept");
                    return null;
                }
                store.Restamp(text);
                report?.Invoke($"[Save] {store.SavePath} already held this campaign - saved by hand, so its time alone is rewritten, and the backup kept");
                Written(store, report);
                return null;
            }
            if (store.WritesRefused is null && MakeWay(store, report, said) is { } stuck)
                return NotSaved(slot, store.WritesRefused ?? stuck);
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
    /// says, when it next opens (at once, after QUIT TO TITLE), that what the slot
    /// holds is from before this quit (<see cref="QuitUnsaved"/>). What the
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
    /// <param name="slot">The slot the quit saves to: the one the campaign came from, or a dev override's scratch slot.</param>
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
    /// written — but for a save by hand, which rewrites the time alone, the backup
    /// still kept (<see cref="Save"/>, <see cref="SaveStore.Restamp"/>). A save that
    /// cannot be read never holds it, and the write guard deals with it first
    /// (<see cref="MakeWay"/>).
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

    /// <summary>A save set aside: which file, what became of it, the full path it has now, and how to play it again — <see cref="TryAgain"/>, or NEW CAMPAIGN's <see cref="MoveToPlay"/>.</summary>
    internal static string SetAsideNotice(string file, string what, string path, string tail = TryAgain)
        => $"{file.ToUpperInvariant()} {what.ToUpperInvariant()} - SET ASIDE AS {path} - {tail}";

    /// <summary>
    /// How every stuck notice ends (<see cref="StuckNotice"/>): its slot takes no
    /// more saves this session — the slot's alone, and the word that leads the
    /// notice names it (<see cref="NothingLoaded"/>, <see cref="NoNewCampaignIn"/>,
    /// <see cref="NotSaved"/>), so the player goes on in another.
    /// </summary>
    private const string SlotTakesNoSaves = "THIS SLOT TAKES NO SAVES THIS SESSION, SO IT IS NEVER WRITTEN OVER";

    /// <summary>A save that could not be read and could not be moved: it stays, and nothing is saved over it.</summary>
    internal static string StuckNotice(string file, string what, string why)
        => $"{file.ToUpperInvariant()} {what.ToUpperInvariant()} AND COULD NOT BE SET ASIDE ({why.ToUpperInvariant()}) - {SlotTakesNoSaves}";

    /// <summary>Whether <paramref name="notice"/> is a stuck notice (<see cref="StuckNotice"/>): what the title says of a NEW CAMPAIGN it stopped, alone.</summary>
    internal static bool SaysStuck(string notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        return notice.EndsWith(SlotTakesNoSaves, StringComparison.Ordinal);
    }

    /// <summary>
    /// The last time play was left, the campaign could not be saved into
    /// <paramref name="slot"/> (<see cref="SaveStore.UnsavedName"/>): what the slot
    /// holds is from before then. The title says it, before anything is loaded —
    /// true of the window's close, and of leaving play for the title, alike:
    /// <c>SLOT 2 COULD NOT BE SAVED WHEN YOU LAST LEFT PLAY - WHAT IT HOLDS IS FROM
    /// BEFORE THAT, BEHIND WHERE YOU LEFT IT</c>, or <c>THE OLD AUTOSAVE COULD NOT BE
    /// SAVED WHEN ...</c> — the slot by its name (<see cref="SaveSlot.Name"/>). Two
    /// notice rows, at slot 9 and the old autosave too.
    /// </summary>
    internal static string QuitUnsaved(SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return $"{slot.Name} COULD NOT BE SAVED WHEN YOU LAST LEFT PLAY - WHAT IT HOLDS IS FROM BEFORE THAT, BEHIND WHERE YOU LEFT IT";
    }

    /// <summary>
    /// A write into <paramref name="slot"/> that failed, and why, in a notice:
    /// what the slot held before is kept. The slot is named
    /// (<see cref="SaveSlot.Name"/>) — whichever save it was, the game's own, the
    /// quit's, or one by hand — <c>SLOT 2 COULD NOT BE SAVED (ANOTHER PROGRAM HAS IT
    /// OPEN) - WHAT IT HELD IS KEPT - SEE THE LOG</c>, <c>THE OLD AUTOSAVE COULD NOT
    /// BE SAVED (THE DISK IS FULL) - ...</c>, <c>THE SCRATCH SLOT COULD NOT BE SAVED
    /// (...) - ...</c>. A blank why is taken as it is: <see cref="Save"/>, which
    /// never throws, hands it the file system's.
    /// </summary>
    internal static string NotSaved(SaveSlot slot, string why)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(why);
        return $"{slot.Name} COULD NOT BE SAVED ({why.ToUpperInvariant()}) - WHAT IT HELD IS KEPT - SEE THE LOG";
    }

    /// <summary>How a save by hand that went through starts (<see cref="SavedTo"/>): the menu draws it green.</summary>
    internal const string SavedPrefix = "SAVED TO ";

    /// <summary>A save by hand that went through: <c>SAVED TO SLOT 2</c>, <c>SAVED TO THE OLD AUTOSAVE</c>, <c>SAVED TO THE SCRATCH SLOT</c> — the slot by its name (<see cref="SaveSlot.Name"/>).</summary>
    internal static string SavedTo(SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return $"{SavedPrefix}{slot.Name}";
    }

    /// <summary>
    /// What the menu answers a save by hand into <paramref name="slot"/> — the
    /// campaign's own, the only one SAVE GAME saves (the user's ruling of 2026-09-25
    /// on the save slots, R5) — drawn inside it until the next key: <see cref="SavedTo"/>
    /// when it went through, in green, or why not (<see cref="NotSaved"/>, the words
    /// <see cref="Save"/> handed back), in orange; then each file the write guard set
    /// aside on the way, where, and how to try it again — true of the disk whichever
    /// way the save went.
    /// </summary>
    /// <param name="slot">The campaign's slot, saved into.</param>
    /// <param name="failed">What <see cref="Save"/> handed back: null when the save went through, and otherwise its word.</param>
    /// <param name="setAside">The notices of what the write guard set aside first (<see cref="Save"/>'s <c>said</c>), in order.</param>
    internal static string SaveAnswer(SaveSlot slot, string? failed, IReadOnlyList<string> setAside)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(setAside);
        return string.Join(" - ", [failed ?? SavedTo(slot), .. setAside]);
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
