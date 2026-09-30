using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestSlots;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// What loading a slot brings back, and a save that never throws (1f; the user's
/// ruling of 2026-09-25 on the save model; PHASE5-084). Loading is resuming: the
/// save is resumed and says so; one that cannot be read, or whose resume crashed
/// the game, is set aside — never deleted, never written over — with a notice
/// naming its full path, and the backup is tried, then nothing is brought back,
/// a load making no campaign of its own — the title says so first, then what was
/// set aside; a slot that refuses its writes since an earlier try brings no
/// campaign into play, and NEW CAMPAIGN that cannot save into its slot begins
/// nothing and puts back what it set aside (the user's ruling of 2026-09-25 on the
/// save slots, R5: every save of a campaign goes into its own slot);
/// NEW CAMPAIGN — the title's, or
/// <c>--new-campaign</c> — sets the save and its backup aside before anything is
/// read, so neither can come back as the new campaign's; the save's word is shown
/// whole, the numbers ahead of any file set aside; a quit that cannot save is said
/// on the title, by its slot; a dev-override launch plays a fresh campaign in a slot of its own
/// and never touches the campaign; a tuning changed since the save is said on
/// screen; and a write that fails is a notice, never an exception, and never
/// costs the save before it. Every case runs on a temporary folder of its own,
/// deleted after, whose store is slot 1's, named as a player sees it. The runs
/// behind the saves stand on the numbers they were written against — the seeds'
/// depths, the mercy floor (<see cref="Premise"/>) — made current for each test,
/// and a campaign is built and restored under it and some cases change the numbers
/// over it, so the content collection holds it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class CampaignBootTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>A fresh empty folder under the system's temp folder, deleted with everything in it once <paramref name="use"/> is done.</summary>
    private static void InTempFolder(Action<string> use)
    {
        var folder = Directory.CreateTempSubdirectory("distancerpg-");
        try
        {
            use(folder.FullName);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private static CampaignState NewCampaign() => CampaignState.Start(GameContent.Current);

    /// <summary>What loading <paramref name="store"/> — slot 1 — brings back, under the current content.</summary>
    private static SlotLoad Load(SaveStore store, List<string>? log = null)
        => CampaignBoot.Load(AsSlot1(store), GameContent.Current, log is null ? null : log.Add);

    /// <summary>Save <paramref name="campaign"/> into <paramref name="store"/> — slot 1 — and it saved: a save that goes through says nothing.</summary>
    private static void Saved(SaveStore store, CampaignState campaign) => Assert.Null(CampaignBoot.Save(AsSlot1(store), campaign));

    /// <summary>The text <paramref name="campaign"/> saves as.</summary>
    private static string Text(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    /// <summary>The names of the files directly in <paramref name="folder"/>, in ordinal order.</summary>
    private static string[] Files(string folder)
        => Directory.GetFiles(folder).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// A set-aside notice: the file, what became of it, its new full path, and how
    /// to play it again — every one ends so: <c>RENAME IT TO CAMPAIGN.JSON TO TRY
    /// AGAIN</c>, or, for what NEW CAMPAIGN set aside (<paramref name="abandoned"/>),
    /// whose name the new campaign's save takes, <c>TO PLAY IT AGAIN, MOVE IT INTO
    /// AN EMPTY SLOT FOLDER AS CAMPAIGN.JSON</c>.
    /// </summary>
    private static void SaysSetAside(string notice, string file, string what, string path, bool abandoned = false)
    {
        string tail = abandoned ? CampaignBoot.MoveToPlay : CampaignBoot.TryAgain;
        Assert.Equal(CampaignBoot.SetAsideNotice(file, what, path, tail), notice);
        Assert.StartsWith($"{file.ToUpperInvariant()} {what.ToUpperInvariant()}", notice, StringComparison.Ordinal);
        Assert.Contains($" SET ASIDE AS {path} ", notice, StringComparison.Ordinal);
        Assert.True(Path.IsPathFullyQualified(path), path);
        Assert.EndsWith(abandoned ? "TO PLAY IT AGAIN, MOVE IT INTO AN EMPTY SLOT FOLDER AS CAMPAIGN.JSON" : "RENAME IT TO CAMPAIGN.JSON TO TRY AGAIN",
            notice, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptySlotLoadsNothing()
    {
        InTempFolder(folder =>
        {
            // Nothing in the folder — the first launch ever — loads nothing: no
            // campaign, no view, nothing to say, and nothing written, not even the
            // folder. A load never makes a campaign: what follows is the caller's
            // (the title's, which goes back to itself saying so, S6).
            var store = new SaveStore(Path.Combine(folder, "saves"));
            var load = Load(store);
            Assert.False(load.Loaded);
            Assert.Null(load.Campaign);
            Assert.Empty(load.Notices);
            Assert.Null(load.View);
            Assert.False(Directory.Exists(store.Folder));

            // A campaign's first save makes the folder and the save, and the next load resumes it.
            var campaign = NewCampaign();
            Saved(store, campaign);
            Assert.Equal([SaveStore.FileName], Files(store.Folder));
            Assert.True(Load(store).Loaded);
            store.ClearResuming();

            // A save gone while its backup stands — deleted by hand, say — is no
            // reason to lose the campaign: the backup resumes, one save older, and
            // says so. Only with neither does a load bring nothing back.
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            File.Delete(store.SavePath);
            var fromBackup = Load(store);
            Assert.True(fromBackup.Loaded);
            Assert.Equal([CampaignBoot.Resumed(Named(1), backup: true)], fromBackup.Notices);
            Assert.Equal(0, fromBackup.Campaign!.RunCounter);
            store.ClearResuming();
        });
    }

    [Fact]
    public void ASavedCampaignBetweenRunsResumes_AndSaysSo()
    {
        InTempFolder(folder =>
        {
            // A campaign some way in: the tutorial beaten and gone, the Spear Hall
            // cleared and locked. Saved, and the next launch plays it on.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            CampaignRuns.Clear(campaign, "spear_hall", CampaignRuns.FiveDeep);
            Saved(store, campaign);

            var log = new List<string>();
            var boot = Load(store, log: log);
            Assert.True(boot.Loaded);
            StateAssert.Equivalent(campaign, boot.Campaign!, "campaign");
            Assert.Equal(2, boot.Campaign!.RunCounter);
            Assert.Equal(campaign.LockedInOrder, boot.Campaign!.LockedInOrder);
            Assert.Contains(("spear_hall", campaign.LockedUntilRun("spear_hall")), boot.Campaign!.LockedInOrder);
            Assert.Equal(["tutorial"], boot.Campaign!.RetiredInOrder);
            Assert.Equal(["spear_hall", "tutorial"], boot.Campaign!.ClearedInOrder);

            // The screen between runs it opens on is the one the campaign left: the
            // last run's summary over the list, the same list, the same choice.
            Assert.Equal(BetweenRunsHud.SummaryLines(campaign.LastResult!), BetweenRunsHud.SummaryLines(boot.Campaign!.LastResult!));
            Assert.Equal(campaign.Listings().Select(l => (l.Def.Id, l.Selectable, l.RunsUntilAvailable, l.Early)),
                boot.Campaign!.Listings().Select(l => (l.Def.Id, l.Selectable, l.RunsUntilAvailable, l.Early)));
            Assert.Equal(campaign.DefaultNext(), boot.Campaign!.DefaultNext());

            // And it says so, first under the title, in green: news, not a warning.
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], boot.Notices);
            var rows = NoticeText.Rows(boot.Notices);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], rows);
            Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(rows[0]));
            Assert.Contains(log, line => line.StartsWith($"[Save] Resumed {store.SavePath}", StringComparison.Ordinal));

            // The resume is guarded until the game clears the marker, once the
            // resumed screen is up; and the campaign saves as the very text it was
            // resumed from — nothing lost, nothing re-derived.
            Assert.True(store.WasResuming);
            Assert.Equal(store.Read(), Text(boot.Campaign!));
            store.ClearResuming();

            // It plays on: the next run goes in and ends as it would have.
            Assert.Equal(RunOutcome.Retreat, CampaignRuns.DeepRetreat(boot.Campaign!, "long_gallery").Outcome);
            Assert.Equal(3, boot.Campaign!.RunCounter);
        });
    }

    [Fact]
    public void AnUnreadableSaveIsSetAside_TheBackupTried_ThenNothingLoads()
    {
        InTempFolder(folder =>
        {
            // Two saves: the backup a campaign with the tutorial beaten, the save a
            // run further on.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);
            Assert.Equal((2, true), (campaign.RunCounter, store.HasBackup));

            // The save is broken — cut off mid-document. It is set aside, its bytes
            // as they were, and the backup resumes: one save older, and said so.
            const string broken = "{ \"version\": 1, \"tuningHash\": \"0000\", \"campaign\": ";
            File.WriteAllText(store.SavePath, broken);
            var log = new List<string>();
            var boot = Load(store, log: log);
            Assert.True(boot.Loaded);
            Assert.Equal(1, boot.Campaign!.RunCounter);
            Assert.Equal(["tutorial"], boot.Campaign!.RetiredInOrder);
            string first = Path.Combine(store.Folder, "campaign.unreadable-1.json");
            Assert.Equal(broken, File.ReadAllText(first));
            Assert.False(store.HasSave);
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.Resumed(Named(1), backup: true), notice),
                notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf(broken, store)})", first));
            Assert.Contains(log, line => line.Contains(store.SavePath, StringComparison.Ordinal) && line.Contains("could not be resumed", StringComparison.Ordinal));
            store.ClearResuming();

            // The campaign played from the backup saves as ever: its first write
            // moves into place, and the file set aside is not touched by it.
            Saved(store, boot.Campaign!);
            Assert.Equal(Text(boot.Campaign!), store.Read());
            Assert.Equal(broken, File.ReadAllText(first));

            // Both unreadable — the save is not a save, the backup not even an
            // object: each is set aside by the next free number, and nothing is
            // brought back. What follows is the caller's, so the load says what it
            // set aside and nothing else: NOTHING COULD BE LOADED is the title's word.
            File.WriteAllText(store.SavePath, "not a save");
            File.WriteAllText(store.BackupPath, "[1, 2, 3]");
            var nothing = Load(store);
            Assert.False(nothing.Loaded);
            Assert.Null(nothing.Campaign);
            Assert.Null(nothing.View);
            string second = Path.Combine(store.Folder, "campaign.unreadable-2.json");
            string third = Path.Combine(store.Folder, "campaign.unreadable-3.json");
            Assert.Collection(nothing.Notices,
                notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf("not a save", store)})", second),
                notice => SaysSetAside(notice, "campaign.bak.json", "could not be read ($: the document is a JSON array)", third));
            Assert.Equal(("not a save", "[1, 2, 3]"), (File.ReadAllText(second), File.ReadAllText(third)));
            Assert.Equal((false, false, false), (store.HasSave, store.HasBackup, store.WasResuming));
            Assert.Equal(broken, File.ReadAllText(first));

            // A save naming content that is gone is unreadable the same way, and
            // says where in the document.
            var wrong = JsonNode.Parse(Text(campaign))!.AsObject();
            wrong["campaign"]!["retired"]![0] = "no_such_dungeon";
            File.WriteAllText(store.SavePath, wrong.ToJsonString());
            var gone = Load(store);
            Assert.False(gone.Loaded);
            SaysSetAside(Assert.Single(gone.Notices), "campaign.json", "could not be read (campaign.retired[0]: 'no_such_dungeon' is no dungeon in the content)",
                Path.Combine(store.Folder, "campaign.unreadable-4.json"));
        });
    }

    /// <summary>Why a notice says <paramref name="text"/> could not be read: the reader's own refusal of it, in the few words a notice gives.</summary>
    private static string WhyOf(string text, SaveStore store)
        => CampaignBoot.WhyUnreadable(Assert.Throws<SaveFormatException>(() => SaveCodec.Read(text)), store);

    [Fact]
    public void ANewerSaveIsSetAside_NotOverwritten()
    {
        InTempFolder(folder =>
        {
            // A save a newer build wrote: this build cannot know what it meant, so it
            // is never read, and never written over — set aside, the reason said.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            var tree = JsonNode.Parse(Text(campaign))!.AsObject();
            tree["version"] = SaveCodec.CurrentVersion + 1;
            string newer = tree.ToJsonString();
            File.WriteAllText(store.SavePath, newer);

            var boot = Load(store);
            Assert.False(boot.Loaded);
            string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");
            Assert.Collection(boot.Notices,
                notice => SaysSetAside(notice, "campaign.json", $"could not be read (version: {SaveCodec.CurrentVersion + 1} is newer than this build reads ({SaveCodec.CurrentVersion}))", aside));

            // A new campaign's saves into the slot — the first, and one after a run,
            // which replaces it — leave the newer save's bytes exactly as they were.
            var fresh = NewCampaign();
            Saved(store, fresh);
            CampaignRuns.ShallowRetreat(fresh, "tutorial");
            Saved(store, fresh);
            Assert.Equal(newer, File.ReadAllText(aside));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName, "campaign.unreadable-1.json"], Files(folder));

            // Renamed back, it is read again, and refused again: set aside once more,
            // under the next free number, and still whole.
            File.Copy(aside, store.SavePath, overwrite: true);
            var again = Load(store);
            Assert.True(again.Loaded);   // the backup, the new campaign's first save
            Assert.Equal(newer, File.ReadAllText(Path.Combine(store.Folder, "campaign.unreadable-2.json")));
            Assert.Equal(newer, File.ReadAllText(aside));
            store.ClearResuming();
        });
    }

    [Fact]
    public void ALoadThatRestoresNothingSaysSoFirst()
    {
        InTempFolder(folder =>
        {
            // S6 (the user's ruling of 2026-09-25 on the save model, R2): a load makes
            // no campaign, and one that brings nothing back goes back to the title,
            // whose word is put together from the load (CampaignBoot.NothingLoadedWord,
            // which DistanceRpgGame.LoadSlot hands the title): NOTHING COULD BE LOADED
            // FROM ... first, then every file the load set aside, whole, in the load's
            // order. Each case is one the launch's word pinned (S5's review) before the
            // title took the launch's place — where A NEW CAMPAIGN BEGINS led and a
            // campaign began. Every case is a slot of one save folder whose old autosave
            // holds a campaign, which a load of another slot that brings nothing back
            // leaves exactly as it was.
            Assert.Equal("NOTHING COULD BE LOADED FROM SLOT 2", CampaignBoot.NothingLoaded(Named(2)));
            Assert.Equal("NOTHING COULD BE LOADED FROM THE OLD AUTOSAVE", CampaignBoot.NothingLoaded(Named(SaveSlots.OldAutosave)));
            Assert.All(Enumerable.Range(0, SaveSlots.Most + 1).Select(n => CampaignBoot.NothingLoaded(Named(n))), word => Assert.Matches("^[A-Z0-9 ]+$", word));

            var slots = new SaveSlots(folder, SaveSlots.Most);
            var autosave = slots.Store(SaveSlots.OldAutosave);
            var kept = NewCampaign();
            CampaignRuns.Clear(kept, "tutorial", CampaignRuns.GoldenSeed);
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.OldAutosave), kept));
            CampaignRuns.DeepRetreat(kept, "spear_hall");
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.OldAutosave), kept));
            (string Save, string Backup) autosaved = (autosave.Read()!, autosave.ReadBackup()!);
            void TheAutosaveUntouched()
            {
                Assert.Equal(autosaved, (autosave.Read()!, autosave.ReadBackup()!));
                Assert.Equal([SaveStore.BackupName, SaveStore.FileName], Files(folder));
            }

            SlotLoad LoadSlot(int number) => CampaignBoot.Load(slots.Slot(number), GameContent.Current);
            IReadOnlyList<string> Word(int number, SlotLoad load)
            {
                Assert.False(load.Loaded);
                Assert.Null(load.View);
                var word = CampaignBoot.NothingLoadedWord(load, Named(number));
                Assert.Equal([CampaignBoot.NothingLoaded(Named(number)), .. load.Notices], word);

                // On the title whole: the save's word leads there, every row of it shown.
                Assert.Equal(word.SelectMany(NoticeText.Wrap), NoticeText.Rows(word, whole: word.Count));
                return word;
            }

            // A slot emptied since the title listed it: nothing to set aside, and the
            // title says only that nothing could be loaded. Nothing is written, not
            // even the slot's folder.
            var emptied = Word(1, LoadSlot(1));
            Assert.Equal([CampaignBoot.NothingLoaded(slots.Slot(1))], emptied);
            Assert.False(Directory.Exists(slots.Store(1).Folder));
            TheAutosaveUntouched();

            // A slot that loads is played, and its word is its own: the helper is for
            // a load that brought nothing back, and refuses one that brought a campaign.
            var resumes = slots.Store(2);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            var script = VisitScript.Enter(campaign, "spear_hall", CampaignRuns.FiveDeep);
            script.Down();
            Assert.Null(CampaignBoot.Save(slots.Slot(2), campaign, new ViewDto("B")));   // a player turn's start
            script.EndTurn(SaveGame.LogicStepSeconds);
            Assert.Null(CampaignBoot.Save(slots.Slot(2), campaign, new ViewDto("B")));   // the next one: the save before it is the backup
            File.WriteAllText(resumes.SavePath, "not a save");
            var load = LoadSlot(2);
            Assert.True(load.Loaded);
            Assert.Equal(new ViewDto("B"), load.View);
            Assert.Equal(2, load.Notices.Count);
            Assert.Equal(CampaignBoot.Resumed(slots.Slot(2), backup: true), load.Notices[0]);
            Assert.Throws<ArgumentException>(() => CampaignBoot.NothingLoadedWord(load, slots.Slot(2)));
            resumes.ClearResuming();
            TheAutosaveUntouched();

            // Nothing that loads — the save not a save, the backup not even an object:
            // the title's word first, then what was set aside, in the load's order —
            // the save, then its backup — in the slot's own folder.
            var broken = slots.Store(3);
            Directory.CreateDirectory(broken.Folder);
            File.WriteAllText(broken.SavePath, "not a save");
            File.WriteAllText(broken.BackupPath, "[1, 2, 3]");
            Assert.Collection(Word(3, LoadSlot(3)),
                notice => Assert.Equal("NOTHING COULD BE LOADED FROM SLOT 3", notice),
                notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf("not a save", broken)})",
                    Path.Combine(broken.Folder, "campaign.unreadable-1.json")),
                notice => SaysSetAside(notice, "campaign.bak.json", "could not be read ($: the document is a JSON array)",
                    Path.Combine(broken.Folder, "campaign.unreadable-2.json")));
            TheAutosaveUntouched();

            // A newer build's save, set aside and never written over: the same.
            var newer = slots.Store(4);
            var tree = JsonNode.Parse(Text(NewCampaign()))!.AsObject();
            tree["version"] = SaveCodec.CurrentVersion + 1;
            Directory.CreateDirectory(newer.Folder);
            File.WriteAllText(newer.SavePath, tree.ToJsonString());
            Assert.Collection(Word(4, LoadSlot(4)),
                notice => Assert.Equal("NOTHING COULD BE LOADED FROM SLOT 4", notice),
                notice => SaysSetAside(notice, "campaign.json",
                    $"could not be read (version: {SaveCodec.CurrentVersion + 1} is newer than this build reads ({SaveCodec.CurrentVersion}))",
                    Path.Combine(newer.Folder, "campaign.unreadable-1.json")));
            TheAutosaveUntouched();

            // A save whose resume crashed the game, with no backup to fall back on: set
            // aside as crashed, and the same again — the marker gone with it.
            var crashes = slots.Store(5);
            Assert.Null(CampaignBoot.Save(slots.Slot(5), NewCampaign()));
            Assert.True(LoadSlot(5).Loaded);   // and the game goes down before the loaded screen is up: the marker stands
            Assert.Collection(Word(5, LoadSlot(5)),
                notice => Assert.Equal("NOTHING COULD BE LOADED FROM SLOT 5", notice),
                notice => SaysSetAside(notice, "campaign.json", "crashed the game the last time it was resumed",
                    Path.Combine(crashes.Folder, "campaign.crashed-1.json")));
            Assert.False(crashes.WasResuming);
            TheAutosaveUntouched();

            // The old autosave itself, when nothing of it loads: the title says so
            // of the old autosave, by its name, then what was set aside.
            var autosaveBroken = new SaveSlots(Path.Combine(folder, "autosave-broken"), 3);
            var itsAutosave = autosaveBroken.Store(SaveSlots.OldAutosave);
            Directory.CreateDirectory(itsAutosave.Folder);
            File.WriteAllText(itsAutosave.SavePath, "not a save");
            Assert.Collection(Word(SaveSlots.OldAutosave, CampaignBoot.Load(autosaveBroken.Slot(SaveSlots.OldAutosave), GameContent.Current)),
                notice => Assert.Equal("NOTHING COULD BE LOADED FROM THE OLD AUTOSAVE", notice),
                notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf("not a save", itsAutosave)})",
                    Path.Combine(itsAutosave.Folder, "campaign.unreadable-1.json")));

            // Windows only: a save another program holds open can be neither read nor
            // moved. It stays, the slot refuses its writes, and the title says so after
            // its own word.
            if (!OperatingSystem.IsWindows())
                return;
            var held = slots.Store(6);
            Assert.Null(CampaignBoot.Save(slots.Slot(6), NewCampaign()));
            SlotLoad heldLoad;
            using (new FileStream(held.SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                heldLoad = LoadSlot(6);
            Assert.Collection(Word(6, heldLoad),
                notice => Assert.Equal("NOTHING COULD BE LOADED FROM SLOT 6", notice),
                notice => Assert.Equal(CampaignBoot.StuckNotice("campaign.json", "could not be read (another program has it open)", "another program has it open"), notice));
            Assert.NotNull(held.WritesRefused);
            TheAutosaveUntouched();
        });
    }

    [Fact]
    public void ANewCampaignSetsOnlyItsChosenSlotAside()
    {
        InTempFolder(folder =>
        {
            // The user's ruling of 2026-09-25 on the save slots (R5): NEW CAMPAIGN on the
            // title begins in the slot chosen on its list, and SLOT DECISION 2: confirmed
            // over a slot that holds a campaign, that slot's backup and save are set
            // aside as abandoned — never deleted, bytes intact, the backup first so the
            // later save takes the later number, each in the slot's own folder — a
            // campaign begun with the starting party and saved there at once, and the
            // next load of the slot bringing it back. The save folder itself and every
            // other slot are never touched. Its notices name no command line — the
            // player chose NEW CAMPAIGN, and the ask said what it does — and end by
            // saying how to play the old campaign again: moved into an empty slot
            // folder, since renamed back where it lies it would take the new campaign's
            // save's name. Moved so, it loads, and plays.
            var slots = new SaveSlots(folder, 3);
            var chosen = slots.Slot(2);
            var old = NewCampaign();
            CampaignRuns.Clear(old, "tutorial", CampaignRuns.GoldenSeed);
            Assert.Null(CampaignBoot.Save(chosen, old));
            CampaignRuns.DeepRetreat(old, "spear_hall");
            Assert.Null(CampaignBoot.Save(chosen, old));
            byte[] save = File.ReadAllBytes(chosen.Store.SavePath);
            byte[] backup = File.ReadAllBytes(chosen.Store.BackupPath);
            var inAutosave = NewCampaign();
            CampaignRuns.Clear(inAutosave, "tutorial", CampaignRuns.GoldenSeed);
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.OldAutosave), inAutosave));
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.OldAutosave), old));
            var inSlot1 = NewCampaign();
            CampaignRuns.ShallowRetreat(inSlot1, "tutorial");
            Assert.Null(CampaignBoot.Save(slots.Slot(1), inSlot1));
            static bool NotSlot2(string path) => !path.StartsWith("slot-2", StringComparison.Ordinal);
            Dictionary<string, string> Others()
                => Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(folder, path))
                    .Where(NotSlot2)
                    .ToDictionary(path => path, path => Convert.ToBase64String(File.ReadAllBytes(Path.Combine(folder, path))), StringComparer.Ordinal);
            var others = Others();
            Assert.Equal(3, others.Count);   // the save folder's save and backup, and slot 1's save

            var log = new List<string>();
            var begun = CampaignBoot.Abandon(chosen, GameContent.Current, log.Add, how: null);

            // A new campaign: the roster's four, nothing met but their loadout, nothing
            // played, every dungeon but the tutorial behind it — and nothing of a save
            // or a dev override: no view, no dungeon to boot into, no seed.
            Assert.False(begun.Resumed);
            Assert.Null(begun.View);
            Assert.Null(begun.StraightInto);
            Assert.Null(begun.FirstSeed);
            Assert.Equal(GameContent.Current.Party.All.Select(member => member.Id), begun.Campaign.Party.Select(member => member.Id));
            StateAssert.Equivalent(NewCampaign(), begun.Campaign, "campaign");
            Assert.Equal(0, begun.Campaign.RunCounter);
            Assert.Null(begun.Campaign.LastResult);

            // Slot 2's files set aside in its own folder, never deleted, and said so
            // whole, each ending with how to play it again.
            string backupAside = Path.Combine(chosen.Store.Folder, "campaign.abandoned-1.json");
            string aside = Path.Combine(chosen.Store.Folder, "campaign.abandoned-2.json");
            Assert.Equal(Path.Combine(folder, "slot-2", "campaign.abandoned-2.json"), aside);
            Assert.Equal(backup, File.ReadAllBytes(backupAside));
            Assert.Equal(save, File.ReadAllBytes(aside));
            Assert.Equal((false, false, false), (chosen.Store.HasSave, chosen.Store.HasBackup, chosen.Store.WasResuming));
            Assert.Collection(begun.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
                notice => SaysSetAside(notice, "campaign.bak.json", "was left for a new campaign", backupAside, abandoned: true),
                notice => SaysSetAside(notice, "campaign.json", "was left for a new campaign", aside, abandoned: true));
            Assert.All(begun.Notices.Skip(1), notice => Assert.DoesNotContain(CampaignBoot.TryAgain, notice, StringComparison.Ordinal));
            Assert.DoesNotContain(begun.Notices, notice => notice.Contains(Program.NewCampaignOption, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(log, line => line.StartsWith($"[Save] NEW CAMPAIGN: a new campaign begins in {chosen.Store.Folder}", StringComparison.Ordinal));
            Assert.Equal(begun.Notices.SelectMany(NoticeText.Wrap), NoticeText.Rows(begun.Notices, whole: begun.Notices.Count));
            Assert.Equal(others, Others());

            // Saved at once, as the game saves it: the next load of slot 2 is the new
            // campaign — never the old one's backup — and says so alone.
            Assert.Null(CampaignBoot.Save(chosen, begun.Campaign));
            Assert.False(chosen.Store.HasBackup);
            var next = CampaignBoot.Load(chosen, GameContent.Current);
            Assert.True(next.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 2"], next.Notices);
            StateAssert.Equivalent(NewCampaign(), next.Campaign!, "campaign");
            chosen.Store.ClearResuming();

            // The old campaign's bytes whole where they were set aside, and the save
            // folder and every other slot byte for byte as they were.
            Assert.Equal(backup, File.ReadAllBytes(backupAside));
            Assert.Equal(save, File.ReadAllBytes(aside));
            Assert.Equal(others, Others());

            // As its notice says: moved into an empty slot folder as campaign.json, the
            // old campaign is a slot LOAD GAME lists, and it loads and plays on.
            Assert.Equal(SlotState.Empty, SaveListing.Read(slots, DateTimeOffset.UtcNow).Slots.Single(slot => slot.Number == 3).State);
            Directory.CreateDirectory(slots.Store(3).Folder);
            File.Move(aside, slots.Store(3).SavePath);
            Assert.Equal(SlotState.Readable, SaveListing.Read(slots, DateTimeOffset.UtcNow).Slots.Single(slot => slot.Number == 3).State);
            var again = CampaignBoot.Load(slots.Slot(3), GameContent.Current);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 3"], again.Notices);
            StateAssert.Equivalent(old, again.Campaign!, "campaign");
            slots.Store(3).ClearResuming();
            Assert.Equal(save, File.ReadAllBytes(slots.Store(3).SavePath));

            // --new-campaign's words still name it (the launch's developer tooling),
            // and end the same way — the number moved out free again — and again, no
            // other slot is touched.
            var moved = Others();
            var launched = CampaignBoot.Abandon(chosen, GameContent.Current);
            SaysSetAside(launched.Notices[1], "campaign.json", "was left for a new campaign (--new-campaign)",
                Path.Combine(chosen.Store.Folder, "campaign.abandoned-2.json"), abandoned: true);
            Assert.Equal(moved, Others());
        });
    }

    [Fact]
    public void ASlotThatTakesNoSavesBringsNoCampaignIntoPlay()
    {
        InTempFolder(folder =>
        {
            // The user's ruling of 2026-09-25 on the save slots (R5): every save of a
            // campaign goes into the slot it came from. A refusal holds for the session
            // (1f), so a campaign played in a slot that refuses its writes could never
            // be saved, and what was played would be lost at the quit; not beginning it
            // loses nothing. So a load of such a slot loads nothing and touches nothing,
            // and says why; NEW CAMPAIGN there begins nothing; a NEW CAMPAIGN whose
            // setting aside sticks, or whose first save fails, is not begun either, and
            // what it set aside goes back. A refusal is its slot's alone.
            var slots = new SaveSlots(folder, 3);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Assert.Null(CampaignBoot.Save(slots.Slot(1), campaign));
            var slot2 = slots.Slot(2);
            Assert.Null(CampaignBoot.Save(slot2, NewCampaign()));
            Assert.Null(CampaignBoot.Save(slot2, campaign));
            static Dictionary<string, string> Bytes(SaveStore store)
                => Directory.GetFiles(store.Folder).ToDictionary(path => Path.GetFileName(path)!, path => Convert.ToBase64String(File.ReadAllBytes(path)), StringComparer.Ordinal);
            var before = Bytes(slot2.Store);

            // Refused from an earlier try: the load writes no marker, sets nothing
            // aside, leaves every byte, and says why — in the slot's own words.
            const string why = "campaign.json could not be read (another program has it open) and could not be set aside (another program has it open)";
            slot2.Store.RefuseWrites(why);
            var log = new List<string>();
            var load = CampaignBoot.Load(slot2, GameContent.Current, log.Add);
            Assert.False(load.Loaded);
            Assert.Null(load.View);
            Assert.Equal([CampaignBoot.TakesNoSaves(slot2, why)], load.Notices);
            Assert.Equal("SLOT 2 TAKES NO SAVES THIS SESSION: EARLIER, CAMPAIGN.JSON COULD NOT BE READ (ANOTHER PROGRAM HAS IT OPEN)"
                + " AND COULD NOT BE SET ASIDE (ANOTHER PROGRAM HAS IT OPEN) - LAUNCH THE GAME AGAIN TO PLAY IN IT", load.Notices[0]);
            Assert.False(slot2.Store.WasResuming);
            Assert.Equal(before, Bytes(slot2.Store));
            Assert.Contains(log, line => line.StartsWith($"[Save] Nothing is loaded from {slot2.Store.Folder}", StringComparison.Ordinal));

            // The title's word: nothing loaded from the slot, by its name, then why —
            // whole where the title shows it, in the font, in the warning's orange.
            var word = CampaignBoot.NothingLoadedWord(load, slot2);
            Assert.Equal(["NOTHING COULD BE LOADED FROM SLOT 2", load.Notices[0]], word);
            var rows = NoticeText.Rows(word, whole: word.Count);
            Assert.Equal(word.SelectMany(NoticeText.Wrap), rows);
            Assert.All(rows, row => Assert.Matches("^[A-Z0-9 :,./()-]+$", row));
            Assert.All(rows, row => Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor(row)));

            // NEW CAMPAIGN there is not begun — the host reads the refusal before
            // anything is set aside — and says so by the slot's name, then why.
            Assert.Equal(["NO NEW CAMPAIGN BEGAN IN SLOT 2", load.Notices[0]], CampaignBoot.NotBegunWord(slot2, [CampaignBoot.TakesNoSaves(slot2, why)]));
            Assert.Equal("NO NEW CAMPAIGN BEGAN IN THE OLD AUTOSAVE", CampaignBoot.NoNewCampaignIn(slots.Slot(SaveSlots.OldAutosave)));
            Assert.Equal(before, Bytes(slot2.Store));

            // Slot 1, in the same session, loads and says nothing of slot 2's refusal.
            var one = CampaignBoot.Load(slots.Slot(1), GameContent.Current);
            Assert.True(one.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], one.Notices);
            slots.Store(1).ClearResuming();

            // A NEW CAMPAIGN whose first save fails — a folder in the way of the write's
            // temp file — is not begun: what NEW CAMPAIGN set aside, as it listed it,
            // goes back where it was, the save before the backup, byte for byte, and the
            // word is the slot's name, then the failed save. The old campaign loads as
            // ever, its backup one save deep.
            var slot3 = slots.Slot(3);
            Assert.Null(CampaignBoot.Save(slot3, NewCampaign()));
            Assert.Null(CampaignBoot.Save(slot3, campaign));
            var three = Bytes(slot3.Store);
            var abandoned = CampaignBoot.Abandon(slot3, GameContent.Current, how: null);
            Assert.Equal([true, false], abandoned.Abandoned.Select(file => file.Backup));
            Assert.Equal(abandoned.Notices.Skip(1), abandoned.Abandoned.Select(file => file.Notice));
            Assert.Equal((false, false), (slot3.Store.HasSave, slot3.Store.HasBackup));
            Directory.CreateDirectory(slot3.Store.TempPath);
            string failed = Assert.IsType<string>(CampaignBoot.Save(slot3, abandoned.Campaign));
            Assert.StartsWith("SLOT 3 COULD NOT BE SAVED (", failed, StringComparison.Ordinal);
            Assert.EndsWith(") - WHAT IT HELD IS KEPT - SEE THE LOG", failed, StringComparison.Ordinal);
            var putBackLog = new List<string>();
            Assert.Empty(CampaignBoot.PutBack(slot3, abandoned, putBackLog.Add));
            const string PutBackAs = " put back as ", NoneBegan = ": no new campaign began there";
            Assert.Equal([slot3.Store.SavePath, slot3.Store.BackupPath],   // the save first: the reverse of the order the two left in
                putBackLog.Where(line => line.EndsWith(NoneBegan, StringComparison.Ordinal))
                    .Select(line => line[(line.IndexOf(PutBackAs, StringComparison.Ordinal) + PutBackAs.Length)..^NoneBegan.Length]));
            Directory.Delete(slot3.Store.TempPath);
            Assert.Equal(three, Bytes(slot3.Store));
            Assert.Equal(["NO NEW CAMPAIGN BEGAN IN SLOT 3", failed], CampaignBoot.NotBegunWord(slot3, [failed]));
            var kept = CampaignBoot.Load(slot3, GameContent.Current);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 3"], kept.Notices);
            StateAssert.Equivalent(campaign, kept.Campaign!, "campaign");
            slot3.Store.ClearResuming();
            Assert.Equal(three, Bytes(slot3.Store));

            // A file that will not go back — its name taken since — stays set aside,
            // never deleted or written over, and its notice is said again; the other
            // goes back all the same. A file is put back only where it was set aside.
            var again = CampaignBoot.Abandon(slot3, GameContent.Current, how: null);
            Directory.CreateDirectory(slot3.Store.BackupPath);
            Assert.Equal([again.Abandoned[0].Notice], CampaignBoot.PutBack(slot3, again));
            Assert.True(File.Exists(again.Abandoned[0].Path));
            Assert.True(slot3.Store.HasSave);
            Directory.Delete(slot3.Store.BackupPath);
            Assert.Throws<ArgumentException>(() => slot3.Store.PutBack(Path.Combine(slots.Store(1).Folder, SaveStore.FileName), backup: false));

            // And the other way round: the save's name taken since, the save stays set
            // aside and its notice is said again, and the backup goes back all the
            // same — so the slot lists FROM ITS BACKUP, one save older, which is what
            // its next load would resume.
            File.Move(again.Abandoned[0].Path, slot3.Store.BackupPath);   // the slot as it was before that NEW CAMPAIGN
            var mirror = CampaignBoot.Abandon(slot3, GameContent.Current, how: null);
            Directory.CreateDirectory(slot3.Store.SavePath);
            Assert.Equal([mirror.Abandoned[1].Notice], CampaignBoot.PutBack(slot3, mirror));
            Assert.True(File.Exists(mirror.Abandoned[1].Path));
            Assert.True(slot3.Store.HasBackup);
            Assert.Equal(SlotState.FromBackup, SaveListing.Read(slots, DateTimeOffset.UtcNow).Slots.Single(slot => slot.Number == 3).State);
            Directory.Delete(slot3.Store.SavePath);

            // The markers stay the slot's (the T2 review): a NEW CAMPAIGN that is not
            // begun leaves the marker of a load that never finished where it was —
            // the new campaign's first save is what clears it — so it still guards the
            // save put back, which the next load sets aside as crashed, as it would
            // have; and the quit's unsaved marker, which --new-campaign takes before any
            // title has said it, is said with the word that nothing began.
            var markedSlots = new SaveSlots(Path.Combine(folder, "marked"), 3);
            var marked = markedSlots.Slot(1);
            Assert.Null(CampaignBoot.Save(marked, NewCampaign()));
            Assert.Null(CampaignBoot.Save(marked, campaign));
            marked.Store.MarkResuming();                    // its last load crashed the game
            marked.Store.MarkUnsaved("the disk is full");   // and before that, a quit could not save it
            var markedBytes = Bytes(marked.Store);
            var markedAside = CampaignBoot.Abandon(marked, GameContent.Current);
            Assert.True(markedAside.TookUnsaved);
            Assert.True(marked.Store.WasResuming);
            Directory.CreateDirectory(marked.Store.TempPath);
            Assert.NotNull(CampaignBoot.Save(marked, markedAside.Campaign));
            Assert.Equal([CampaignBoot.QuitUnsaved(marked)], CampaignBoot.PutBack(marked, markedAside));
            Directory.Delete(marked.Store.TempPath);
            markedBytes.Remove(SaveStore.UnsavedName);
            Assert.Equal(markedBytes, Bytes(marked.Store));   // the save, the backup and the resume marker as they were
            Assert.True(SaveListing.Read(markedSlots, DateTimeOffset.UtcNow).Slots.Single(slot => slot.Number == 1).Unfinished);
            var crashedAgain = CampaignBoot.Load(marked, GameContent.Current);
            Assert.Equal(CampaignBoot.Resumed(marked, backup: true), crashedAgain.Notices[0]);
            SaysSetAside(crashedAgain.Notices[1], "campaign.json", "crashed the game the last time it was resumed",
                Path.Combine(marked.Store.Folder, "campaign.crashed-1.json"));
            marked.Store.ClearResuming();

            // Begun, its first save clears the marker as it goes through, so the new
            // campaign's next load is never set aside as crashed; and the title took
            // the unsaved marker before NEW CAMPAIGN, so there is none to take.
            marked.Store.MarkResuming();
            var begun = CampaignBoot.Abandon(marked, GameContent.Current, how: null);
            Assert.False(begun.TookUnsaved);
            Assert.True(marked.Store.WasResuming);
            Assert.Null(CampaignBoot.Save(marked, begun.Campaign));
            Assert.False(marked.Store.WasResuming);

            // Windows only: a stuck NEW CAMPAIGN. Another program holds the save, so it
            // will not move once the backup has; the slot takes no saves from then on,
            // and the stuck notice says so of this slot alone. The backup goes back, so
            // the slot's files are exactly as they were, its backup one save deep, and
            // the word is the stuck notice alone. The slot loads nothing and takes no
            // save for the rest of the session.
            if (!OperatingSystem.IsWindows())
                return;
            var heldSlots = new SaveSlots(Path.Combine(folder, "held"), 3);
            var held = heldSlots.Slot(2);
            Assert.Null(CampaignBoot.Save(held, NewCampaign()));
            Assert.Null(CampaignBoot.Save(held, campaign));
            var heldBytes = Bytes(held.Store);
            BootResult stuck;
            using (new FileStream(held.Store.SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stuck = CampaignBoot.Abandon(held, GameContent.Current, how: null);
            Assert.NotNull(held.Store.WritesRefused);
            Assert.Equal([true], stuck.Abandoned.Select(file => file.Backup));
            string stuckNotice = CampaignBoot.StuckNotice("campaign.json", "was left for a new campaign", "another program has it open");
            Assert.Equal(stuckNotice, stuck.Notices[^1]);
            Assert.EndsWith(" - THIS SLOT TAKES NO SAVES THIS SESSION, SO IT IS NEVER WRITTEN OVER", stuckNotice, StringComparison.Ordinal);
            Assert.Equal([stuckNotice], stuck.Notices.Where(CampaignBoot.SaysStuck));
            Assert.Empty(CampaignBoot.PutBack(held, stuck));
            Assert.Equal(heldBytes, Bytes(held.Store));
            Assert.Equal(["NO NEW CAMPAIGN BEGAN IN SLOT 2", stuckNotice], CampaignBoot.NotBegunWord(held, [.. stuck.Notices.Where(CampaignBoot.SaysStuck)]));
            Assert.NotNull(CampaignBoot.Save(held, stuck.Campaign));
            Assert.False(CampaignBoot.Load(held, GameContent.Current).Loaded);
            Assert.Equal(heldBytes, Bytes(held.Store));
        });
    }

    [Fact]
    public void AVisitInProgressResumesInTheDungeon()
    {
        InTempFolder(folder =>
        {
            // 1f, Phase 5.8 (Phase 5.6's review's ARunInProgressIsSetAside_UntilTheGameCanResumeIt,
            // its assertion reversed): the game saves the run in progress at every safe
            // point, and a campaign.json holding one resumes with it — the dungeon, the
            // floor and the turn the save's — for the game to play on in the dungeon.
            // Nothing is set aside, and the run is never lost to a launch.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);                             // between runs
            campaign.Enter("spear_hall", CampaignRuns.FiveDeep);
            Assert.True(SaveGame.IsSafePoint(campaign));        // right after the entry, no floor open yet
            Saved(store, campaign);                             // the entry's own save, as BeginRun asks for it
            string entered = store.Read()!;
            var script = new VisitScript(campaign);
            script.Open();
            Assert.Equal(5, script.Visit.BossFloor);
            script.Down();
            script.EndTurn(SaveGame.LogicStepSeconds);          // a turn played out on the logic clock's own step
            var view = new ViewDto("C", MarchingThisTurn: true);
            Assert.Null(CampaignBoot.Save(AsSlot1(store), campaign, view));   // a player turn's start: the game writes it
            string midRun = store.Read()!;
            Assert.NotNull(JsonNode.Parse(midRun)!["campaign"]!["visit"]);
            Assert.Equal(entered, store.ReadBackup());

            var log = new List<string>();
            var boot = Load(store, log: log);
            Assert.True(boot.Loaded);
            var resumed = boot.Campaign!.Visit;
            Assert.NotNull(resumed);
            Assert.Equal(("spear_hall", CampaignRuns.FiveDeep, 1, script.Visit.Turn), (resumed.Def.Id, resumed.MapSeed, resumed.FloorIndex, resumed.Turn));
            Assert.Equal(1, script.Visit.Turn);
            Assert.Null(resumed.Turns);   // the scene that plays it opens the floor, as an arrival does
            Assert.Equal(view, boot.View);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], boot.Notices);
            Assert.Equal(midRun, SaveCodec.Write(SaveGame.Capture(boot.Campaign!, boot.View)));   // nothing lost, nothing re-derived
            Assert.Equal(1, boot.Campaign!.RunCounter);
            Assert.Equal(["tutorial"], boot.Campaign!.RetiredInOrder);
            Assert.Contains(log, line => line.StartsWith($"[Save] Resumed {store.SavePath}", StringComparison.Ordinal)
                && line.Contains("a run in progress in spear_hall", StringComparison.Ordinal) && line.Contains("floor 2, turn 1", StringComparison.Ordinal));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName, SaveStore.ResumingName], Files(folder));   // nothing set aside
            Assert.True(store.WasResuming);   // cleared by the game once the resumed floor is up
            store.ClearResuming();

            // It plays on: the floor opens caught up, as an arrival opens it, and the
            // run goes on to its end like any other.
            resumed.OpenTurns(boot.Campaign!.Party, CampaignRuns.Ten);
            Assert.Equal(RunOutcome.Retreat, CampaignRuns.Exit(boot.Campaign!).Outcome);
            Assert.Null(boot.Campaign!.Visit);

            // A save that cannot be read beside a backup holding a run: the save is set
            // aside as ever, and the backup resumes in the dungeon — here the entry's
            // own save, taken before the first floor was opened, so it carries no view
            // and its floor is arrived on afresh.
            File.WriteAllText(store.SavePath, "not a save");
            var fromBackup = Load(store);
            Assert.True(fromBackup.Loaded);
            Assert.Equal(("spear_hall", 0, 0, (TurnSystem?)null), (fromBackup.Campaign!.Visit!.Def.Id, fromBackup.Campaign!.Visit.FloorIndex,
                fromBackup.Campaign!.Visit.Turn, fromBackup.Campaign!.Visit.Turns));
            Assert.Null(fromBackup.View);
            Assert.Equal(entered, SaveCodec.Write(SaveGame.Capture(fromBackup.Campaign!)));
            string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");
            Assert.Collection(fromBackup.Notices,
                notice => Assert.Equal(CampaignBoot.Resumed(Named(1), backup: true), notice),
                notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf("not a save", store)})", aside));
            store.ClearResuming();
        });
    }

    [Fact]
    public void ABackupResumedInTheDungeonSaysTheSavesWordWhole()
    {
        InTempFolder(folder =>
        {
            // S8's review. A save that cannot be read may sit beside a backup that
            // holds a run, which is common now that the run saves at every turn's
            // start. The save is set aside and the backup resumes in the dungeon. The
            // floor it lands on says what became of campaign.json whole, as the screen
            // between runs would: the full path it was set aside as, and how to try it
            // again. What the data files raised after it is capped, as on every screen.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            var script = VisitScript.Enter(campaign, "spear_hall", CampaignRuns.FiveDeep);
            script.Down();
            Assert.Null(CampaignBoot.Save(AsSlot1(store), campaign, new ViewDto("B")));   // a player turn's start
            script.EndTurn(SaveGame.LogicStepSeconds);
            Assert.Null(CampaignBoot.Save(AsSlot1(store), campaign, new ViewDto("B")));   // the next one: the save before it is the backup
            File.WriteAllText(store.SavePath, "not a save");

            var boot = Load(store);
            Assert.True(boot.Loaded);
            Assert.Equal(("spear_hall", 1), (boot.Campaign!.Visit!.Def.Id, boot.Campaign!.Visit.FloorIndex));
            Assert.Equal(new ViewDto("B"), boot.View);
            string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.Resumed(Named(1), backup: true), notice),
                notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf("not a save", store)})", aside));
            store.ClearResuming();

            // The game hands them to the resumed floor this way (DistanceRpgGame.ResumeRun):
            // the save's word leads, the data files' notices are held after it, and the
            // count of what leads is read before the take.
            string[] data = [.. Enumerable.Range(1, 5).Select(i => $"tuning.json: Key{i} 0 is below 1 - using 1")];
            var held = new StartupNotices();
            held.Hold(data);
            held.Lead(boot.Notices);
            int leading = held.Leading;
            var hud = new DungeonHud(game: null!);
            hud.ShowResume(boot.Campaign!.Visit, held.Take(), leading);
            Assert.Equal(2, leading);
            var saysWhole = boot.Notices.SelectMany(NoticeText.Wrap).ToList();   // every word of both, the path and TRY AGAIN among them
            Assert.InRange(saysWhole.Count, NoticeText.MaxRows + 1, NoticeText.MaxWholeRows);
            Assert.Equal([.. saysWhole, .. NoticeText.Rows(data).Take(NoticeText.MaxRows), NoticeText.More(2)], hud.NoticeRows);
            Assert.Contains(aside.Replace('\\', '/').ToUpperInvariant(), string.Concat(hud.NoticeRows.Select(row => row.Trim())), StringComparison.Ordinal);
            Assert.Contains(CampaignBoot.TryAgain, string.Join(" ", hud.NoticeRows.Select(row => row.Trim())), StringComparison.Ordinal);

            // S8 first capped every notice here. The floor then showed the resumed
            // line, a row or two of the notice and a count of the rest: never the
            // whole path, and never how to try it again.
            var capped = NoticeText.Rows([.. boot.Notices, .. data]);
            Assert.Equal(NoticeText.MaxRows + 1, capped.Count);
            Assert.DoesNotContain(CampaignBoot.TryAgain, string.Join(" ", capped.Select(row => row.Trim())), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void QuittingMidEnemyTurnSavesTheNextPlayerTurn()
    {
        InTempFolder(folder =>
        {
            // 1f: a quit that finds the enemies taking their turn — the pause menu
            // opened during it, or the window closed — plays it out to the next player
            // turn on the playing scene's own step, then saves: the turn the player
            // would have met. Here the lance is mid-stride towards A when the quit
            // arrives, and its walk ends in blows, so dice are rolled on the way.
            var store = new SaveStore(folder);
            var view = new ViewDto("A", MarchingThisTurn: false);

            // Live play, stepped at the logic clock's own step to the next player turn.
            var live = ReloadDeterminismTests.MidWalk();
            int rolledMidStride = live.Visit.DiceRolled;
            int stepsToPlay = 0;
            while (live.Turns.Phase != TurnPhase.Player && stepsToPlay < 200_000)
            {
                live.Turns.Update(SaveGame.LogicStepSeconds);
                stepsToPlay++;
            }
            Assert.Equal(TurnPhase.Player, live.Turns.Phase);
            Assert.True(live.Visit.DiceRolled > rolledMidStride);   // the lance's blows rolled the visit's own die on the way
            string reached = SaveCodec.Write(SaveGame.Capture(live.Campaign, view));

            // The quit: off a safe point nothing can be written as it stands — a notice,
            // never a throw — so the quit's own door settles it with the step it is
            // handed, calling it exactly as often as play stepped, reads the view once
            // the turn has come round, and saves what live play reached.
            var quitting = ReloadDeterminismTests.MidWalk();
            Assert.False(SaveGame.IsSafePoint(quitting.Campaign));
            Assert.NotNull(CampaignBoot.Save(AsSlot1(store), quitting.Campaign));
            Assert.False(store.HasSave);
            int calls = 0;
            var log = new List<string>();
            Assert.True(CampaignBoot.SaveOnQuit(AsSlot1(store), quitting.Campaign,
                () =>
                {
                    Assert.Equal(TurnPhase.Player, quitting.Turns.Phase);   // the view is the settled turn's
                    return view;
                },
                () =>
                {
                    calls++;
                    quitting.Turns.Update(SaveGame.LogicStepSeconds);
                }, log.Add));
            Assert.Equal(stepsToPlay, calls);
            Assert.Equal(reached, store.Read());
            Assert.Contains(log, line => line.Contains($"Played out in {stepsToPlay} steps", StringComparison.Ordinal));

            // The reboot is at that player turn, the view with it.
            var boot = Load(store);
            Assert.True(boot.Loaded);
            Assert.Equal(view, boot.View);
            Assert.Equal(live.Visit.Turn, boot.Campaign!.Visit!.Turn);
            Assert.Equal(reached, SaveCodec.Write(SaveGame.Capture(boot.Campaign!, boot.View)));
            store.ClearResuming();

            // With no step to play it out with — no scene playing the run — nothing
            // is settled and nothing written: the last save stands. Logic's own
            // default step is a test's, never the game's.
            var unplayed = ReloadDeterminismTests.MidWalk();
            Assert.False(CampaignBoot.SaveOnQuit(AsSlot1(store), unplayed.Campaign, () => view, step: null));
            Assert.Equal(reached, store.Read());
            Assert.Equal(TurnPhase.EnemyMoving, unplayed.Turns.Phase);

            // A step that never brings the turn round is given up on, and nothing is
            // written either.
            var stuck = ReloadDeterminismTests.MidWalk();
            Assert.False(CampaignBoot.SaveOnQuit(AsSlot1(store), stuck.Campaign, () => view, () => { }));
            Assert.Equal(reached, store.Read());

            // At a safe point already the quit takes no step and saves as it stands;
            // a campaign between runs carries no view.
            Assert.True(CampaignBoot.SaveOnQuit(AsSlot1(store), live.Campaign, () => view with { ActiveMemberId = "B" },
                () => throw new Xunit.Sdk.XunitException("a player turn needs no step")));
            Assert.Equal("B", SaveCodec.Read(store.Read()!).View!.ActiveMemberId);
            var between = NewCampaign();
            Assert.True(CampaignBoot.SaveOnQuit(AsSlot1(store), between, () => throw new Xunit.Sdk.XunitException("a campaign between runs has no view"), step: null));
            Assert.Null(SaveCodec.Read(store.Read()!).View);
        });
    }

    [Fact]
    public void ASettleThatWipesThePartySavesTheCampaignBetweenRuns()
    {
        InTempFolder(folder =>
        {
            // A quit during the enemies' turn that ends with the party dead: the settle
            // ends the run in Logic, as play would, and what is saved is the campaign
            // between runs with the run's summary — no visit, and no view.
            var store = new SaveStore(folder);
            var script = VisitScript.Enter(NewCampaign(), "spear_hall", CampaignRuns.FiveDeep, visitsOwnDie: true);
            var lance = script.Row(10, "pinning_lance");
            script.Walk("A", script.Beside(lance));
            foreach (var member in script.Party)
            {
                member.Hp = 1;
                if (member.Id != "A")
                    member.ApplyStatus(StatusEffectType.Poison, null, 5);
            }
            var turns = script.Turns;
            turns.EndTurn();
            Assert.False(SaveGame.IsSafePoint(script.Campaign));

            Assert.True(CampaignBoot.SaveOnQuit(AsSlot1(store), script.Campaign,
                () => throw new Xunit.Sdk.XunitException("a run that ended has no view"),
                () => turns.Update(SaveGame.LogicStepSeconds)));
            Assert.Null(script.Campaign.Visit);
            var boot = Load(store);
            Assert.True(boot.Loaded);
            Assert.Null(boot.Campaign!.Visit);
            Assert.Null(boot.View);
            Assert.Equal(("spear_hall", RunOutcome.Death), (boot.Campaign!.LastResult!.DungeonId, boot.Campaign!.LastResult.Outcome));
            store.ClearResuming();
        });
    }

    [Fact]
    public void TheViewComesBack()
    {
        InTempFolder(folder =>
        {
            // 1b, 1d: the view is what the scene decided and Logic never reads — the
            // active member, and whether the turn was granted the march — and a save
            // carries it back to the scene that resumes the turn.
            var store = new SaveStore(folder);
            var script = VisitScript.Enter(NewCampaign(), "spear_hall", CampaignRuns.FiveDeep);
            foreach (var view in new[] { new ViewDto("C", MarchingThisTurn: true), new ViewDto("B"), new ViewDto() })
            {
                Assert.Null(CampaignBoot.Save(AsSlot1(store), script.Campaign, view));
                var boot = Load(store);
                Assert.Equal(view, boot.View);
                store.ClearResuming();
            }

            // A march granted stays granted: a turn has just begun, and nothing has
            // been seen in it yet. One revoked stays revoked: the grant comes back from
            // the view, and the sighting that revoked it from the turn's own counters,
            // so the scene's march — granted and nothing live seen — is off again
            // after the reload.
            script.EndTurn(SaveGame.LogicStepSeconds);
            var granted = new ViewDto("A", MarchingThisTurn: true);
            Assert.False(script.Turns.AnyLiveEnemySeenThisTurn);   // the turn's start: nobody seen yet
            Assert.Null(CampaignBoot.Save(AsSlot1(store), script.Campaign, granted));
            var calm = Load(store);
            store.ClearResuming();
            Assert.True(calm.View!.MarchingThisTurn);
            Assert.False(calm.Campaign!.Visit!.OpenTurns(calm.Campaign!.Party, CampaignRuns.Ten).AnyLiveEnemySeenThisTurn);

            var sighted = script.Floor.Enemies.First(row => row.Alive);
            script.Turns.NotifyEnemyVisible(sighted, true);   // a live enemy in sight, as the scene tells it: the march is revoked this turn
            Assert.True(script.Turns.AnyLiveEnemySeenThisTurn);
            Assert.Null(CampaignBoot.Save(AsSlot1(store), script.Campaign, granted));
            var revoked = Load(store);
            store.ClearResuming();
            Assert.True(revoked.View!.MarchingThisTurn);
            Assert.True(revoked.Campaign!.Visit!.OpenTurns(revoked.Campaign!.Party, CampaignRuns.Ten).AnyLiveEnemySeenThisTurn);

            // The member the view names leads while it stands; a name no member has, a
            // fallen member, or none, hands the lead to the first living one, as a
            // turn's start does.
            var party = revoked.Campaign!.Party;
            Assert.Equal(2, DungeonScene.ActiveIndexFor(party, "C"));
            Assert.Equal(0, DungeonScene.ActiveIndexFor(party, null));
            Assert.Equal(0, DungeonScene.ActiveIndexFor(party, "Z"));
            party[0].Alive = false;
            party[2].Alive = false;
            Assert.Equal(1, DungeonScene.ActiveIndexFor(party, "C"));
            Assert.Equal(1, DungeonScene.ActiveIndexFor(party, null));
        });
    }

    [Fact]
    public void AQuitWithTheViewALoadBroughtBack_WritesNothing()
    {
        InTempFolder(folder =>
        {
            // T6's review: a close during a loaded run's fade in, before its screen is
            // up, has no scene to read a view off, so the game hands the quit's save
            // the view the load brought back (DistanceRpgGame.SaveOnQuit, pinned in
            // GameStartupTests). With it, the campaign as loaded is the save on the
            // disk already: nothing is written, the backup stays one save deep, and
            // the next load comes back with the view. Handed none, as the game did
            // before, the quit writes the campaign again without it, and the save that
            // had it goes to the backup: the next load arrives on the floor afresh,
            // its active member and its march lost.
            var store = new SaveStore(folder);
            var slot = AsSlot1(store);
            var script = VisitScript.Enter(NewCampaign(), "spear_hall", CampaignRuns.FiveDeep);
            var view = new ViewDto("C", MarchingThisTurn: true);
            Assert.Null(CampaignBoot.Save(slot, script.Campaign, new ViewDto("A")));
            Assert.Null(CampaignBoot.Save(slot, script.Campaign, view));
            string save = File.ReadAllText(store.SavePath);
            string backup = File.ReadAllText(store.BackupPath);

            var load = Load(store);
            Assert.Equal(view, load.View);
            Assert.True(CampaignBoot.SaveOnQuit(slot, load.Campaign!, () => load.View, step: null));
            Assert.Equal(save, File.ReadAllText(store.SavePath));
            Assert.Equal(backup, File.ReadAllText(store.BackupPath));
            store.ClearResuming();   // the game's, once the quit has saved
            Assert.Equal(view, Load(store).View);
            store.ClearResuming();

            var viewless = Load(store);
            Assert.True(CampaignBoot.SaveOnQuit(slot, viewless.Campaign!, () => null, step: null));
            store.ClearResuming();
            Assert.Null(SaveCodec.Read(store.Read()!).View);
            Assert.Equal(save, File.ReadAllText(store.BackupPath));
            Assert.Null(Load(store).View);
            store.ClearResuming();
        });
    }

    [Fact]
    public void AResumeKeepsTheTurnsSight_AStairArrivalResetsIt()
    {
        InTempFolder(folder =>
        {
            // 1b: what the party can see this turn is every box a living member has
            // stood in since the turn began, and the pick and the march read it. A
            // resume keeps it — the lance's room lit behind A, who has walked out into
            // the corridor — and adds the boxes the party stands in; a stairs arrival
            // lights only where the party stands.
            var store = new SaveStore(folder);
            var script = VisitScript.Enter(NewCampaign(), "spear_hall", CampaignRuns.FiveDeep);
            var lance = script.Row(10, "pinning_lance");
            var (r, c) = Displacer.TileOf(lance);
            script.Walk("A", (8, 40));
            script.Look();
            script.Walk("A", (10, 24));   // out of the room: a fog box's edge crossed
            script.Look();
            Assert.True(script.Floor.Fog.Visible[r, c]);
            Assert.Null(CampaignBoot.Save(AsSlot1(store), script.Campaign, new ViewDto("A")));

            (int R, int C)[] Living(CampaignState campaign) => campaign.Party.Where(member => member.Alive).Select(Displacer.TileOf).ToArray();
            bool[] Lit(FogState fog) => fog.Visible.Cast<bool>().ToArray();

            var kept = Load(store);
            store.ClearResuming();
            var floor = kept.Campaign!.Visit!.Current;
            var saved = Lit(floor.Fog);
            var seen = floor.Fog.Seen.Cast<bool>().ToArray();
            Assert.Equal(Lit(script.Floor.Fog), saved);
            DungeonScene.LightArrival(floor.Fog, floor.FogBoxes.FogBoxes, Living(kept.Campaign!), keepSight: true);
            Assert.Equal(saved, Lit(floor.Fog));   // every tile the turn had lit, and the party's boxes were among them
            Assert.True(floor.Fog.Visible[r, c]);
            Assert.Equal(seen, floor.Fog.Seen.Cast<bool>());

            // Only where the party stands, on a stairs arrival: the lance's room dark
            // again, the explored memory as it was.
            var fresh = new FogState(floor.Fog.Rows, floor.Fog.Cols);
            foreach (var (tr, tc) in Living(kept.Campaign!))
                fresh.RevealAt(tr, tc, floor.FogBoxes.FogBoxes);
            var reset = Load(store);
            store.ClearResuming();
            var again = reset.Campaign!.Visit!.Current;
            DungeonScene.LightArrival(again.Fog, again.FogBoxes.FogBoxes, Living(reset.Campaign!), keepSight: false);
            Assert.Equal(Lit(fresh), Lit(again.Fog));
            Assert.False(again.Fog.Visible[r, c]);
            Assert.Equal(seen, again.Fog.Seen.Cast<bool>());

            // A floor keeping its sight still lights a box a member stands in that was
            // dark: the boxes the party stands in are added.
            var dark = Load(store);
            store.ClearResuming();
            var unlit = dark.Campaign!.Visit!.Current;
            unlit.Fog.ResetVisibility();
            DungeonScene.LightArrival(unlit.Fog, unlit.FogBoxes.FogBoxes, Living(dark.Campaign!), keepSight: true);
            Assert.Equal(Lit(fresh), Lit(unlit.Fog));
        });
    }

    [Fact]
    public void AChangedTuningIsANotice()
    {
        InTempFolder(folder =>
        {
            // PHASE5-084, §5.3: "loading under different numbers warns rather than
            // refuses ... silently changing a saved game's rules underneath the
            // player is the failure to avoid". A campaign saved under the numbers it
            // was played under, resumed under others, says so on the screen it opens on.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], Load(store).Notices);   // the same numbers: nothing to say
            store.ClearResuming();

            var played = GameContent.Current.Tuning;
            var tuned = played with { MovementBudget = 200 };
            using (TestContent.UsePremise(tuning: _ => tuned))
            {
                var log = new List<string>();
                var boot = Load(store, log: log);
                Assert.True(boot.Loaded);
                Assert.Equal("THE NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS ON UNDER THE NEW ONES", CampaignBoot.TuningChanged);
                Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1", CampaignBoot.TuningChanged], boot.Notices);

                // On screen whole, under the resumed line and ahead of what the data
                // files raised: the save's word leads, shown in full, and the file's
                // notice follows it.
                var held = new StartupNotices();
                held.Hold(["tuning.json: MovementBudget 20000 is above 10000 - using 10000"]);
                held.Lead(boot.Notices);
                int whole = held.Leading;
                var rows = NoticeText.Rows(held.Take(), whole);
                Assert.Equal(2, whole);
                Assert.Equal("CAMPAIGN RESUMED FROM SLOT 1", rows[0]);
                Assert.Equal(CampaignBoot.TuningChanged, string.Join(" ", rows.Skip(1).Take(2).Select(row => row.Trim())));
                Assert.Equal(["TUNING.JSON: MOVEMENTBUDGET 20000 IS ABOVE 10000 - USING 10000"], rows.Skip(3));
                Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor(rows[1]));

                // The log names both hashes; the campaign is the one saved, playing on.
                Assert.Contains(log, line => line.Contains(TuningJson.Hash(played), StringComparison.Ordinal)
                    && line.Contains(TuningJson.Hash(tuned), StringComparison.Ordinal));
                Assert.Equal(campaign.LockedInOrder, boot.Campaign!.LockedInOrder);
                Assert.Equal(1, boot.Campaign!.RunCounter);
                store.ClearResuming();

                // The next save records the new hash, so the change is said once.
                Saved(store, boot.Campaign!);
                Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], Load(store).Notices);
                store.ClearResuming();
            }

            // Back under the numbers it was first played under, they have changed again, and it says so again.
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1", CampaignBoot.TuningChanged], Load(store).Notices);
            store.ClearResuming();
        });
    }

    [Fact]
    public void TheNumbersChangedLineHashesAtTheSavesVersion()
    {
        InTempFolder(folder =>
        {
            // PHASE 6a DECISION 9: a save is compared on the keys of the version its
            // hash is of, and the log's numbers-changed line prints the game's hash
            // on those same keys, naming the version — two hashes over one key set,
            // never a version-1 save's hash beside the current version's. A campaign
            // saved as a version-1 document would be: its hash on version 1's keys.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            var played = GameContent.Current.Tuning;
            var tree = JsonNode.Parse(Text(campaign))!.AsObject();
            tree["tuningHash"] = TuningJson.Hash(played, version: 1);
            tree["tuningHashVersion"] = 1;
            Directory.CreateDirectory(store.Folder);
            File.WriteAllText(store.SavePath, tree.ToJsonString());

            // Under a tuning that moved only what version 2 began to play, nothing
            // it was played under moved: it resumes with nothing to say.
            var wearOnly = played with { WearPerHit = played.WearPerHit + 1 };
            using (TestContent.UsePremise(tuning: _ => wearOnly))
            {
                var log = new List<string>();
                Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], Load(store, log: log).Notices);
                Assert.DoesNotContain(log, line => line.Contains("the numbers have changed", StringComparison.Ordinal));
                store.ClearResuming();
            }

            // Under one that moved a number it was played under, it says so, and the
            // log names both hashes on version 1's keys, and the version.
            var tuned = played with { MovementBudget = 200 };
            using (TestContent.UsePremise(tuning: _ => tuned))
            {
                var log = new List<string>();
                Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1", CampaignBoot.TuningChanged], Load(store, log: log).Notices);
                string line = Assert.Single(log, line => line.Contains("the numbers have changed", StringComparison.Ordinal));
                Assert.Contains($"was saved under tuning {TuningJson.Hash(played, version: 1)} (version 1's keys) and the game plays under {TuningJson.Hash(tuned, version: 1)} on them", line, StringComparison.Ordinal);
                Assert.DoesNotContain(TuningJson.Hash(tuned), line, StringComparison.Ordinal);
                Assert.NotEqual(TuningJson.Hash(tuned), TuningJson.Hash(tuned, version: 1));
                store.ClearResuming();
            }
        });
    }

    [Fact]
    public void TheSavesWordIsOnScreenWhole_TheNumbersAheadOfWhatWasSetAside()
    {
        InTempFolder(folder =>
        {
            // The busiest resume there is: the save cannot be read and is set aside,
            // the backup resumes, and the numbers have changed since it was saved.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);
            File.WriteAllText(store.SavePath, "not a save");

            using (TestContent.UsePremise(tuning: t => t with { MovementBudget = 200 }))
            {
                var boot = Load(store);
                Assert.True(boot.Loaded);
                string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");

                // What plays, then the numbers it plays under — PHASE5-084 wants that
                // on screen, whatever else there is to say — then the file set aside.
                Assert.Collection(boot.Notices,
                    notice => Assert.Equal(CampaignBoot.Resumed(Named(1), backup: true), notice),
                    notice => Assert.Equal(CampaignBoot.TuningChanged, notice),
                    notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf("not a save", store)})", aside));
                store.ClearResuming();

                // The screen between runs shows the save's word whole — the set-aside
                // file's full path and how to try it again among it — and what the
                // data files raised after it, capped as ever.
                var held = new StartupNotices();
                held.Hold(["tuning.json: MovementBudget 200 differs from the built-in 160"]);
                held.Lead(boot.Notices);
                int whole = held.Leading;
                var rows = NoticeText.Rows(held.Take(), whole);
                Assert.Equal(3, whole);
                var saysWhole = boot.Notices.SelectMany(NoticeText.Wrap).ToList();   // every word of every notice, the path and TRY AGAIN among them
                Assert.InRange(saysWhole.Count, 6, NoticeText.MaxWholeRows);           // more than the cap of three alone would show
                Assert.Equal([.. saysWhole, "TUNING.JSON: MOVEMENTBUDGET 200 DIFFERS FROM THE BUILT-IN 160"], rows);
                Assert.Equal(CampaignBoot.Resumed(Named(1), backup: true), rows[0]);
                Assert.Equal(CampaignBoot.TuningChanged, string.Join(" ", rows.Skip(1).Take(2).Select(row => row.Trim())));
                Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(rows[0]));
                Assert.All(rows.Skip(1), row => Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor(row)));

                // The cap alone — every notice capped, as the other screens cap theirs
                // — would have shown the resumed line and the numbers, and nothing of
                // the file set aside but how many more are in the log.
                Assert.Equal([.. saysWhole.Take(3), NoticeText.More(2)], NoticeText.Rows([.. boot.Notices, "tuning.json: MovementBudget 200 differs from the built-in 160"]));
            }
        });
    }

    [Fact]
    public void ADevOverrideLaunchPlaysInItsOwnSlot_AndNeverTouchesTheCampaign()
    {
        InTempFolder(folder =>
        {
            // The campaign, saved: the tutorial beaten.
            var own = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(own, campaign);
            byte[] saved = File.ReadAllBytes(own.SavePath);

            // DISTANCERPG_DUNGEON=spear_hall and DISTANCERPG_SEED=7: a fresh campaign
            // straight into the Spear Hall, its first run on seed 7 — as repeatable
            // as ever — in the override slot, never the campaign's folder.
            var request = new BootRequest("spear_hall", 7);
            Assert.True(request.DevOverride);
            var slot = CampaignBoot.StoreFor(folder, request);
            Assert.Equal(Path.Combine(own.Folder, SaveStore.OverrideSlot), slot.Folder);
            var bootLog = new List<string>();
            var boot = CampaignBoot.BootOverride(SaveSlot.Scratch(slot), GameContent.Current, request, bootLog.Add);
            Assert.False(boot.Resumed);
            Assert.Equal("spear_hall", boot.StraightInto);
            // The log names what it never touches as the ruling on the save slots has
            // it: no slot, nor the old autosave — never "the campaign's own save".
            Assert.Equal([$"[Save] A dev-override launch: a fresh campaign in its own slot, {slot.Folder}, straight into spear_hall, its first run on seed 7 - no slot, nor the old autosave, is read or written"], bootLog);
            Assert.Equal(7L, boot.FirstSeed);
            Assert.Equal([CampaignBoot.OverrideNotice], boot.Notices);
            StateAssert.Equivalent(NewCampaign(), boot.Campaign, "campaign");

            // It plays like any campaign, and saves into its slot.
            var visit = boot.Campaign.Enter(boot.StraightInto!, boot.FirstSeed!.Value);
            Assert.Equal(7L, visit.MapSeed);
            visit.OpenTurns(boot.Campaign.Party, CampaignRuns.Ten);
            CampaignRuns.Exit(boot.Campaign);
            Assert.Null(CampaignBoot.Save(SaveSlot.Scratch(slot), boot.Campaign));
            Assert.Equal([SaveStore.FileName], Files(slot.Folder));

            // The campaign's own save: the same bytes, and nothing beside it but the slot.
            Assert.Equal(saved, File.ReadAllBytes(own.SavePath));
            Assert.Equal([SaveStore.FileName], Files(folder));
            Assert.Equal([SaveStore.OverrideSlot], Directory.GetDirectories(folder).Select(path => Path.GetFileName(path)!));

            // A second such launch starts afresh: the slot emptied, nothing resumed.
            var again = CampaignBoot.BootOverride(SaveSlot.Scratch(slot), GameContent.Current, request);
            Assert.False(again.Resumed);
            Assert.Null(again.Campaign.LastResult);
            Assert.Empty(Files(slot.Folder));

            // A seed alone is a dev override too: its own slot, the screen between runs first.
            var seeded = new BootRequest(Seed: 7);
            var bySeed = CampaignBoot.BootOverride(SaveSlot.Scratch(CampaignBoot.StoreFor(folder, seeded)), GameContent.Current, seeded);
            Assert.False(bySeed.Resumed);
            Assert.Null(bySeed.StraightInto);
            Assert.Equal(7L, bySeed.FirstSeed);

            // --new-campaign beside an override touches nothing of the campaign's
            // either, and an override on the campaign's own folder is refused rather
            // than emptying it.
            var both = new BootRequest("spear_hall", NewCampaign: true);
            var bothLog = new List<string>();
            Assert.False(CampaignBoot.BootOverride(SaveSlot.Scratch(CampaignBoot.StoreFor(folder, both)), GameContent.Current, both, bothLog.Add).Resumed);
            Assert.Contains($"[Save] {Program.NewCampaignOption} does nothing on a dev-override launch: its campaign is fresh every time, and no slot, nor the old autosave, is touched", bothLog);
            Assert.Throws<InvalidOperationException>(() => CampaignBoot.BootOverride(SaveSlot.Scratch(own), GameContent.Current, request));
            Assert.Equal(saved, File.ReadAllBytes(own.SavePath));
            Assert.Equal([SaveStore.FileName], Files(folder));

            // And a launch without one resumes the campaign as it was left.
            var resumed = Load(own);
            Assert.True(resumed.Loaded);
            Assert.Equal(1, resumed.Campaign!.RunCounter);
            Assert.Equal(["tutorial"], resumed.Campaign!.RetiredInOrder);
            own.ClearResuming();
        });
    }

    [Fact]
    public void NewCampaignAtLaunchSetsTheSaveAside()
    {
        InTempFolder(folder =>
        {
            // DistanceRPG --new-campaign: it begins in the slot NEW CAMPAIGN's list
            // would start on (SaveListing.NewCampaignSlot) — here slot 1, the one
            // slot there is, and in use — whose save and backup are set aside as
            // abandoned before anything is read — never deleted — and a fresh
            // campaign begins. The backup goes first, so the later save takes the
            // later number.
            var slots = new SaveSlots(folder, 1);
            var slot = slots.Slot(1);
            var store = slot.Store;
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Assert.Null(CampaignBoot.Save(slot, campaign));
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Assert.Null(CampaignBoot.Save(slot, campaign));
            byte[] saved = File.ReadAllBytes(store.SavePath);
            byte[] backup = File.ReadAllBytes(store.BackupPath);
            Assert.Equal(1, SaveListing.Read(slots, DateTimeOffset.UtcNow).NewCampaignSlot);

            var boot = CampaignBoot.Abandon(slot, GameContent.Current);
            Assert.False(boot.Resumed);
            StateAssert.Equivalent(NewCampaign(), boot.Campaign, "campaign");
            string backupAside = Path.Combine(store.Folder, "campaign.abandoned-1.json");
            string aside = Path.Combine(store.Folder, "campaign.abandoned-2.json");
            Assert.Equal(backup, File.ReadAllBytes(backupAside));
            Assert.Equal(saved, File.ReadAllBytes(aside));
            Assert.Equal((false, false), (store.HasSave, store.HasBackup));   // nothing of the old campaign's is left to resume
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
                notice => SaysSetAside(notice, "campaign.bak.json", "was left for a new campaign (--new-campaign)", backupAside, abandoned: true),
                notice => SaysSetAside(notice, "campaign.json", "was left for a new campaign (--new-campaign)", aside, abandoned: true));
            Assert.False(store.WasResuming);

            // All three on the screen between runs, whole — the save's word is — for
            // all that the two set-aside notices name full paths.
            var rows = NoticeText.Rows(boot.Notices, whole: boot.Notices.Count);
            Assert.Equal(boot.Notices.SelectMany(NoticeText.Wrap), rows);
            Assert.InRange(rows.Count, 1, NoticeText.MaxWholeRows);

            // Its saves go where the old one was — the game saves it at once — and
            // the files set aside stay whole.
            void BothStillWhole()
            {
                Assert.Equal(backup, File.ReadAllBytes(backupAside));
                Assert.Equal(saved, File.ReadAllBytes(aside));
            }
            Assert.Null(CampaignBoot.Save(slot, boot.Campaign));
            Assert.Equal(["campaign.abandoned-1.json", "campaign.abandoned-2.json", SaveStore.FileName], Files(store.Folder));
            BothStillWhole();

            // Again: the next number, and the first two untouched. The new
            // campaign's first save had no backup to keep, so there is none to set aside.
            var again = CampaignBoot.Abandon(slot, GameContent.Current);
            Assert.Equal(Text(boot.Campaign), File.ReadAllText(Path.Combine(store.Folder, "campaign.abandoned-3.json")));
            BothStillWhole();
            Assert.Equal(2, again.Notices.Count);

            // With neither there is nothing to set aside, and a new campaign all the same.
            Assert.Equal([CampaignBoot.NewCampaignBegins], CampaignBoot.Abandon(slot, GameContent.Current).Notices);

            // A backup alone — its save deleted by hand — is set aside the same way.
            File.Copy(backupAside, store.BackupPath);
            var alone = CampaignBoot.Abandon(slot, GameContent.Current);
            string fourth = Path.Combine(store.Folder, "campaign.abandoned-4.json");
            Assert.Equal(backup, File.ReadAllBytes(fourth));
            Assert.Collection(alone.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
                notice => SaysSetAside(notice, "campaign.bak.json", "was left for a new campaign (--new-campaign)", fourth, abandoned: true));
            Assert.Equal((false, false), (store.HasSave, store.HasBackup));
        });
    }

    [Fact]
    public void NewCampaignAtLaunchNeverBringsTheOldCampaignBack()
    {
        InTempFolder(folder =>
        {
            // A campaign two saves in, in the one slot there is: the save and its backup
            // both the old campaign's.
            var slots = new SaveSlots(folder, 1);
            var slot = slots.Slot(1);
            var store = slot.Store;
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Assert.Null(CampaignBoot.Save(slot, campaign));
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Assert.Null(CampaignBoot.Save(slot, campaign));
            SlotLoad LoadIt() => CampaignBoot.Load(slot, GameContent.Current);

            // --new-campaign, in the slot NEW CAMPAIGN's list would start on — slot 1,
            // in use — and nothing saved before the game goes down: a crash in the new
            // campaign's first run before any save was written. The next load brings
            // nothing back — never the old campaign's backup, as though it were the new
            // one's — and says nothing.
            Assert.Equal(1, SaveListing.Read(slots, DateTimeOffset.UtcNow).NewCampaignSlot);
            var abandoned = CampaignBoot.Abandon(slot, GameContent.Current);
            Assert.False(abandoned.Resumed);
            abandoned.Campaign.Enter("tutorial", CampaignRuns.GoldenSeed);   // its first run begun, then down: nothing saved
            var next = LoadIt();
            Assert.False(next.Loaded);
            Assert.Null(next.Campaign);
            Assert.Empty(next.Notices);
            Assert.Equal((false, false), (store.HasSave, store.HasBackup));

            // The game saves the new campaign at once (DistanceRpgGame.BeginNewCampaign),
            // so after the same crash the next load resumes that campaign — the new
            // one, as it stood when its run began.
            var fresh = CampaignBoot.Abandon(slot, GameContent.Current);
            Assert.Null(CampaignBoot.Save(slot, fresh.Campaign));
            fresh.Campaign.Enter("tutorial", CampaignRuns.GoldenSeed);
            var resumed = LoadIt();
            Assert.True(resumed.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], resumed.Notices);
            StateAssert.Equivalent(NewCampaign(), resumed.Campaign!, "campaign");
            Assert.False(store.HasBackup);
            store.ClearResuming();

            // And a first save that cannot be played has no backup of the old
            // campaign's to fall back on: it is set aside, and nothing is brought back.
            File.WriteAllText(store.SavePath, "not a save");
            var fallback = LoadIt();
            Assert.False(fallback.Loaded);
            Assert.Null(fallback.Campaign);
            SaysSetAside(Assert.Single(fallback.Notices), "campaign.json", $"could not be read ({WhyOf("not a save", store)})",
                Path.Combine(store.Folder, "campaign.unreadable-1.json"));
        });
    }

    [Fact]
    public void NewCampaignAtLaunchStopsAtAFileThatWillNotMove_AndTheOldCampaignStays()
    {
        // Windows only: another program holding the backup open without sharing it
        // makes it unmovable. --new-campaign stops there: the save is not touched,
        // nothing is saved this session, and the next launch resumes the campaign
        // as it stands — never its backup in place of its save.
        if (!OperatingSystem.IsWindows())
            return;

        InTempFolder(folder =>
        {
            // The one slot there is, in use: the slot --new-campaign begins in
            // (SaveListing.NewCampaignSlot).
            var slots = new SaveSlots(folder, 1);
            var slot = slots.Slot(1);
            var store = slot.Store;
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Assert.Null(CampaignBoot.Save(slot, campaign));
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Assert.Null(CampaignBoot.Save(slot, campaign));
            string saved = store.Read()!;
            Assert.Equal(1, SaveListing.Read(slots, DateTimeOffset.UtcNow).NewCampaignSlot);

            BootResult boot;
            using (new FileStream(store.BackupPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                boot = CampaignBoot.Abandon(slot, GameContent.Current);

            Assert.False(boot.Resumed);
            Assert.Collection(boot.Notices,
                line => Assert.Equal(CampaignBoot.NewCampaignBegins, line),
                line => Assert.Equal(CampaignBoot.StuckNotice("campaign.bak.json", "was left for a new campaign (--new-campaign)", "another program has it open"), line));
            Assert.NotNull(store.WritesRefused);
            Assert.NotNull(CampaignBoot.Save(slot, boot.Campaign));
            Assert.Equal(saved, store.Read());
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName], Files(store.Folder));

            var nextLaunch = new SaveSlots(folder, 1).Slot(1);
            var next = CampaignBoot.Load(nextLaunch, GameContent.Current);
            Assert.True(next.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], next.Notices);
            Assert.Equal(2, next.Campaign!.RunCounter);
            nextLaunch.Store.ClearResuming();
        });
    }

    [Fact]
    public void AResumeThatNeverFinishedSetsTheSaveAsideNextLaunch()
    {
        InTempFolder(folder =>
        {
            // Two saves, the backup one run older.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);
            string guarded = store.Read()!;
            string older = store.ReadBackup()!;

            // A launch resumes it, marker written first — and the game goes down
            // before the resumed screen is up, so the marker is never cleared.
            Assert.True(Load(store).Loaded);
            Assert.True(store.WasResuming);

            // The next launch finds the marker: the save it guarded crashed the game,
            // so it is set aside as crashed, and the backup resumes — guarded in turn.
            var boot = Load(store);
            Assert.True(boot.Loaded);
            Assert.Equal(1, boot.Campaign!.RunCounter);
            string crashed = Path.Combine(store.Folder, "campaign.crashed-1.json");
            Assert.Equal(guarded, File.ReadAllText(crashed));
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.Resumed(Named(1), backup: true), notice),
                notice => SaysSetAside(notice, "campaign.json", "crashed the game the last time it was resumed", crashed));
            Assert.True(store.WasResuming);

            // That one crashed too: the backup is what it guarded, set aside the same
            // way, and nothing is brought back. What follows is the caller's, so the
            // load says what it set aside and nothing else: NOTHING COULD BE LOADED
            // is the title's word.
            var fresh = Load(store);
            Assert.False(fresh.Loaded);
            Assert.Null(fresh.Campaign);
            string crashedBackup = Path.Combine(store.Folder, "campaign.crashed-2.json");
            Assert.Equal(older, File.ReadAllText(crashedBackup));
            Assert.Collection(fresh.Notices,
                notice => SaysSetAside(notice, "campaign.bak.json", "crashed the game the last time it was resumed", crashedBackup));
            Assert.Equal((false, false, false), (store.HasSave, store.HasBackup, store.WasResuming));

            // A marker with nothing left to guard is only cleared, and says nothing.
            store.MarkResuming();
            Assert.Empty(Load(store).Notices);
            Assert.False(store.WasResuming);
            Assert.Equal(["campaign.crashed-1.json", "campaign.crashed-2.json"], Files(folder));
        });
    }

    [Fact]
    public void AWriteThatFailsIsANotice_AndKeepsThePreviousSave()
    {
        InTempFolder(folder =>
        {
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            Saved(store, campaign);
            string previous = store.Read()!;
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);

            // A folder where the write's temp file goes: the write fails before it
            // can replace anything. The save returns the notice to show, throws
            // nothing, and the save before it is exactly what it was.
            Directory.CreateDirectory(store.TempPath);
            var log = new List<string>();
            string? notice = CampaignBoot.Save(AsSlot1(store), campaign, report: log.Add);
            Assert.NotNull(notice);
            Assert.StartsWith("SLOT 1 COULD NOT BE SAVED (", notice, StringComparison.Ordinal);
            Assert.EndsWith(") - WHAT IT HELD IS KEPT - SEE THE LOG", notice, StringComparison.Ordinal);
            Assert.Equal(previous, store.Read());
            Assert.False(store.HasBackup);
            Assert.Contains(log, line => line.StartsWith($"[Save] Could not write {store.SavePath}", StringComparison.Ordinal));

            // Freed, the same save goes through, and the one before it is the backup.
            Directory.Delete(store.TempPath);
            log.Clear();
            Assert.Null(CampaignBoot.Save(AsSlot1(store), campaign, report: log.Add));
            Assert.Equal((Text(campaign), previous), (store.Read(), store.ReadBackup()));
            Assert.Equal([$"[Save] Wrote {store.SavePath}"], log);

            // A campaign mid-run is saved at a safe point (Phase 5.8), and asked off
            // one — the turn ended, the enemies' turn under way — it is a notice and
            // never a throw, and the save between runs is kept.
            string between = store.Read()!;
            campaign.Enter("spear_hall", CampaignRuns.FiveDeep).OpenTurns(campaign.Party, CampaignRuns.Ten).EndTurn();
            Assert.False(SaveGame.IsSafePoint(campaign));
            Assert.NotNull(CampaignBoot.Save(AsSlot1(store), campaign));
            Assert.Equal((between, previous), (store.Read(), store.ReadBackup()));
        });
    }

    [Fact]
    public void AQuitThatCannotSaveIsSaidAtTheNextLaunch()
    {
        InTempFolder(folder =>
        {
            // The audit's pass (1f): no screen follows a quit, so a quit that cannot
            // save — here a folder in the way of the write's temp file — leaves
            // campaign.unsaved beside the save it keeps, and the title says, the next
            // time it opens and before anything is loaded (CampaignBoot.TakeEveryUnsaved,
            // which DistanceRpgGame.ShowTitle takes), that what the slot holds is from
            // before that quit. A player is never resumed behind where they left the
            // game unwarned. Since the user's ruling on the save slots (R5) the quit
            // saves into the campaign's own slot — here slot 2, which it was loaded
            // from — so the marker is that slot's, and the title names it.
            var slots = new SaveSlots(folder, 3);
            var slot = slots.Slot(2);
            var store = slot.Store;
            var campaign = NewCampaign();
            Assert.Null(CampaignBoot.Save(slot, campaign));
            string before = store.Read()!;
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);   // played on since: the quit has something to save

            Directory.CreateDirectory(store.TempPath);
            var log = new List<string>();
            Assert.True(CampaignBoot.SaveOnQuit(slot, campaign, view: null, step: null, log.Add));
            Assert.Equal(before, store.Read());
            string marker = Assert.IsType<string>(store.ReadUnsaved());
            Assert.StartsWith("Leaving play could not save the campaign in this folder: SLOT 2 COULD NOT BE SAVED (", marker, StringComparison.Ordinal);
            Assert.Contains("\nWhat this folder holds is from before that. The title says so and removes this file.\n", marker, StringComparison.Ordinal);
            Assert.Contains(log, line => line.StartsWith($"[Save] {store.UnsavedPath} written", StringComparison.Ordinal));
            Assert.Equal([SaveStore.FileName, SaveStore.UnsavedName], Files(store.Folder));
            Assert.Equal([SaveSlots.SlotFolderPrefix + "2"], Directory.GetFileSystemEntries(folder).Select(Path.GetFileName));   // nothing beside the other slots' saves
            Directory.Delete(store.TempPath);

            // The title takes the marker, before any load, with what the quit wrote in
            // the log; the marker goes as it is read, so it is said once. The load after
            // it resumes the save the quit could not replace, and says that alone.
            var titleLog = new List<string>();
            Assert.True(CampaignBoot.TakeUnsaved(store, titleLog.Add));
            Assert.Null(store.ReadUnsaved());
            Assert.Contains(titleLog, line => line.StartsWith($"[Save] {store.UnsavedPath} was left by the last quit", StringComparison.Ordinal)
                && line.Contains("SLOT 2 COULD NOT BE SAVED (", StringComparison.Ordinal));
            Assert.False(CampaignBoot.TakeUnsaved(store, titleLog.Add));
            var boot = CampaignBoot.Load(slot, GameContent.Current);
            Assert.True(boot.Loaded);
            Assert.Equal(0, boot.Campaign!.RunCounter);   // the campaign as the last save left it, not as it was quit
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 2"], boot.Notices);
            store.ClearResuming();
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 2"], CampaignBoot.Load(slot, GameContent.Current).Notices);
            store.ClearResuming();

            // On the title whole, in the warning's orange: it is the save's word, which
            // leads, and takes two rows — naming the slot, in words true of the window's
            // close and of leaving play for the title alike. Two rows at slot 9 too, and
            // for the old autosave, named as every slot is named.
            Assert.Equal("SLOT 2 COULD NOT BE SAVED WHEN YOU LAST LEFT PLAY - WHAT IT HOLDS IS FROM BEFORE THAT, BEHIND WHERE YOU LEFT IT", CampaignBoot.QuitUnsaved(slot));
            Assert.Equal("THE OLD AUTOSAVE COULD NOT BE SAVED WHEN YOU LAST LEFT PLAY - WHAT IT HOLDS IS FROM BEFORE THAT, BEHIND WHERE YOU LEFT IT",
                CampaignBoot.QuitUnsaved(slots.Slot(SaveSlots.OldAutosave)));
            foreach (var said in new[] { slot, slots.Slot(SaveSlots.Most), slots.Slot(SaveSlots.OldAutosave) }.Select(CampaignBoot.QuitUnsaved))
            {
                string[] title = [said];
                var rows = NoticeText.Rows(title, whole: title.Length);
                Assert.Equal(title.SelectMany(NoticeText.Wrap), rows);
                Assert.Equal(2, NoticeText.Wrap(said).Count);
                Assert.All(rows, row => Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor(row)));
                Assert.Matches("^[A-Z0-9 ,-]+$", said);   // the HUD's font: no apostrophe
            }

            // A quit that saves leaves no marker.
            Assert.True(CampaignBoot.SaveOnQuit(slot, campaign, view: null, step: null));
            Assert.Equal(Text(campaign), store.Read());
            Assert.Null(store.ReadUnsaved());

            // Nor does a quit whose save the disk already holds: nothing needed writing.
            Directory.CreateDirectory(store.TempPath);
            Assert.True(CampaignBoot.SaveOnQuit(slot, campaign, view: null, step: null));
            Assert.Null(store.ReadUnsaved());
            Directory.Delete(store.TempPath);

            // A quit during the enemies' turn that cannot be played out writes nothing
            // and leaves the marker as well. The title says it, and the busiest load
            // there is after it — the save unreadable and set aside, the backup resumed
            // under other numbers — says what resumed, the numbers it plays under and
            // what was set aside, and the save's word still fits the rows it is shown
            // whole in (NoticeText.MaxWholeRows).
            var stuck = ReloadDeterminismTests.MidWalk();
            Assert.False(CampaignBoot.SaveOnQuit(slot, stuck.Campaign, () => new ViewDto("A"), () => { }));
            Assert.Contains("did not come round to the party's", store.ReadUnsaved()!, StringComparison.Ordinal);
            File.WriteAllText(store.SavePath, "not a save");
            using (TestContent.UsePremise(tuning: t => t with { MovementBudget = 200 }))
            {
                Assert.True(CampaignBoot.TakeUnsaved(store, report: null));
                var busiest = CampaignBoot.Load(slot, GameContent.Current);
                Assert.True(busiest.Loaded);
                Assert.Collection(busiest.Notices,
                    notice => Assert.Equal("CAMPAIGN RESUMED FROM THE BACKUP OF SLOT 2, ONE SAVE OLDER", notice),
                    notice => Assert.Equal(CampaignBoot.TuningChanged, notice),
                    notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf("not a save", store)})",
                        Path.Combine(store.Folder, "campaign.unreadable-1.json")));
                Assert.InRange(busiest.Notices.SelectMany(NoticeText.Wrap).Count(), NoticeText.MaxRows + 1, NoticeText.MaxWholeRows);
            }
            store.ClearResuming();
            Assert.Null(store.ReadUnsaved());

            // Left for a new campaign, the campaign it spoke of is set aside: the
            // marker goes, and nothing is said of it.
            var unplayed = ReloadDeterminismTests.MidWalk();
            Assert.False(CampaignBoot.SaveOnQuit(slot, unplayed.Campaign, () => new ViewDto("A"), step: null));
            Assert.Contains("with no scene to play it out with", store.ReadUnsaved()!, StringComparison.Ordinal);
            var abandoned = CampaignBoot.Abandon(slot, GameContent.Current);
            Assert.DoesNotContain(CampaignBoot.QuitUnsaved(slot), abandoned.Notices);
            Assert.Null(store.ReadUnsaved());

            // A marker that cannot be written either — a folder in its way — leaves the
            // log alone to say so, and the quit is not stopped.
            Directory.CreateDirectory(store.UnsavedPath);
            Directory.CreateDirectory(store.TempPath);
            log.Clear();
            Assert.True(CampaignBoot.SaveOnQuit(slot, campaign, view: null, step: null, log.Add));
            Assert.Contains(log, line => line.StartsWith($"[Save] {store.UnsavedPath} could not be written either", StringComparison.Ordinal));
            Assert.Null(store.ReadUnsaved());
        });
    }

    [Fact]
    public void TheTitleTakesEverySlotsUnsavedMarker()
    {
        InTempFolder(folder =>
        {
            // The quit's marker is its campaign's slot's (R5), so the title takes every
            // slot's before it lists them (CampaignBoot.TakeEveryUnsaved, which
            // DistanceRpgGame.ShowTitle calls first): the old autosave's, slot 2's, and slot
            // 5's above the count — a campaign loaded from there saves there too — each
            // beside the campaign's save, or its backup alone, which a load resumes; each
            // said once, in slot order, by the slot's name, logged with what its quit
            // wrote, and cleared.
            var slots = new SaveSlots(folder, 3);
            var cleared = NewCampaign();
            CampaignRuns.Clear(cleared, "tutorial", CampaignRuns.GoldenSeed);
            foreach (int number in new[] { SaveSlots.OldAutosave, 1, 2, 3, 5 })
                Assert.Null(CampaignBoot.Save(slots.Slot(number), NewCampaign()));
            Assert.Null(CampaignBoot.Save(slots.Slot(5), cleared));
            File.Delete(slots.Store(5).SavePath);   // its backup alone
            string Holding(SaveStore store) => string.Join(",", Directory.GetFileSystemEntries(store.Folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            var held = Enumerable.Range(SaveSlots.OldAutosave, SaveSlots.Most + 1).ToDictionary(number => number, number => Directory.Exists(slots.Store(number).Folder) ? Holding(slots.Store(number)) : null);
            foreach (int number in new[] { 5, SaveSlots.OldAutosave, 2 })
                slots.Store(number).MarkUnsaved("the disk is full");
            var log = new List<string>();
            Assert.Equal([CampaignBoot.QuitUnsaved(slots.Slot(SaveSlots.OldAutosave)), CampaignBoot.QuitUnsaved(slots.Slot(2)), CampaignBoot.QuitUnsaved(slots.Slot(5))],
                CampaignBoot.TakeEveryUnsaved(slots, log.Add));
            var took = log.Where(line => line.Contains(" was left by the last quit", StringComparison.Ordinal)).ToList();
            Assert.Equal([slots.Store(SaveSlots.OldAutosave).UnsavedPath, slots.Store(2).UnsavedPath, slots.Store(5).UnsavedPath],
                took.Select(line => line["[Save] ".Length..line.IndexOf(" was left", StringComparison.Ordinal)]));
            Assert.All(took, line => Assert.Contains("the disk is full", line, StringComparison.Ordinal));
            Assert.All(Enumerable.Range(SaveSlots.OldAutosave, SaveSlots.Most + 1), number => Assert.Null(slots.Store(number).ReadUnsaved()));
            Assert.Empty(CampaignBoot.TakeEveryUnsaved(slots));   // said once
            Assert.Equal(held, Enumerable.Range(SaveSlots.OldAutosave, SaveSlots.Most + 1).ToDictionary(number => number, number => Directory.Exists(slots.Store(number).Folder) ? Holding(slots.Store(number)) : null));   // nothing else touched

            // A marker beside no save and no backup — what it spoke of moved or deleted
            // by hand since — is taken, logged, and not said: its WHAT IT HOLDS would
            // name nothing, and the old autosave is not even listed then (the audit of
            // the ruling on the save slots). A slot that holds only files set aside
            // holds no campaign either.
            var lone = new SaveSlots(Path.Combine(folder, "lone"), 3);
            foreach (int number in new[] { SaveSlots.OldAutosave, 2 })
                lone.Store(number).MarkUnsaved("the disk is full");
            Directory.CreateDirectory(lone.Store(3).Folder);
            File.WriteAllText(Path.Combine(lone.Store(3).Folder, "campaign.abandoned-1.json"), Text(NewCampaign()));
            lone.Store(3).MarkUnsaved("the disk is full");
            var loneLog = new List<string>();
            Assert.Empty(CampaignBoot.TakeEveryUnsaved(lone, loneLog.Add));
            Assert.All(new[] { SaveSlots.OldAutosave, 2, 3 }, number => Assert.Null(lone.Store(number).ReadUnsaved()));
            Assert.Equal([lone.Store(SaveSlots.OldAutosave).Folder, lone.Store(2).Folder, lone.Store(3).Folder],
                loneLog.Where(line => line.EndsWith(" holds no save and no backup beside that marker, so there is nothing of the campaign to say it of - the title says nothing", StringComparison.Ordinal))
                    .Select(line => line["[Save] ".Length..line.IndexOf(" holds no save", StringComparison.Ordinal)]));
            Assert.DoesNotContain(SaveListing.Read(lone, DateTimeOffset.UtcNow).Slots, slot => slot.Number == SaveSlots.OldAutosave);

            // One that cannot be cleared — Windows only: another program holds it open
            // for reading — is said, and said again the next time; the others go.
            if (!OperatingSystem.IsWindows())
                return;
            slots.Store(1).MarkUnsaved("the disk is full");
            slots.Store(3).MarkUnsaved("the disk is full");
            using (new FileStream(slots.Store(1).UnsavedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Equal([CampaignBoot.QuitUnsaved(slots.Slot(1)), CampaignBoot.QuitUnsaved(slots.Slot(3))], CampaignBoot.TakeEveryUnsaved(slots));
            Assert.Null(slots.Store(3).ReadUnsaved());
            Assert.Equal([CampaignBoot.QuitUnsaved(slots.Slot(1))], CampaignBoot.TakeEveryUnsaved(slots));
            Assert.Empty(CampaignBoot.TakeEveryUnsaved(slots));
        });
    }

    [Fact]
    public void SavingWhatIsSavedWritesNothing_SoTheBackupStaysTheSaveBefore()
    {
        InTempFolder(folder =>
        {
            // A run's end saves, and quitting right after it saves the same campaign
            // again. Written twice, the backup would be the save it sits beside, and
            // a save whose resume crashed would fall back to the same state. So a
            // text the save already holds is not written again.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            Saved(store, campaign);
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            string before = store.ReadBackup()!;
            var log = new List<string>();
            Assert.Null(CampaignBoot.Save(AsSlot1(store), campaign, report: log.Add));
            Assert.Equal((Text(campaign), before), (store.Read(), store.ReadBackup()));
            Assert.Equal([$"[Save] {store.SavePath} already holds this campaign - nothing written, the backup kept"], log);

            // Anything that changed is written as ever.
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);
            Assert.NotEqual(before, store.ReadBackup());
        });
    }

    [Fact]
    public void ASaveByHandOfAnUnchangedCampaignStampsItsTime_KeepingTheBackup()
    {
        InTempFolder(folder =>
        {
            // The user's ruling of 2026-09-25 on the save slots (R5): SAVE GAME saves
            // the campaign's own slot, which the game's own saves keep up to date, so
            // a save by hand mostly finds the slot holding the campaign as it stands.
            // It stamps the save's time all the same, as old games' saves stamp their
            // file — so the slot just saved by hand is the one CONTINUE names — and
            // rewrites the time alone: the backup is not rotated, so it stays the save
            // before, a different state, one save deep.
            var at = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
            var slots = new SaveSlots(folder, 3);
            var slot2 = slots.Slot(2);
            var campaign = NewCampaign();
            Assert.Null(CampaignBoot.Save(slot2, campaign, savedAt: at));                // A, at 10:00
            CampaignRuns.ShallowRetreat(campaign, "tutorial");
            Assert.Null(CampaignBoot.Save(slot2, campaign, savedAt: at.AddHours(2)));    // B, at 12:00: A is the backup
            byte[] backup = File.ReadAllBytes(slot2.Store.BackupPath);
            var other = NewCampaign();
            CampaignRuns.Clear(other, "tutorial", CampaignRuns.GoldenSeed);
            Assert.Null(CampaignBoot.Save(slots.Slot(1), other, savedAt: at.AddHours(2.5)));   // another campaign, saved later
            Assert.Equal(1, SaveListing.Read(slots, at.AddHours(3)).Newest!.Number);

            // B by hand at 13:00: its time rewritten, the rest of it as it was, the
            // backup A byte for byte, nothing left in the temp file's place. The file
            // is replaced, so a resume marker standing in the slot is cleared, as
            // every write clears it (the T4 review): in play none stands by then — no
            // save reaches a slot before its loaded screen is up — but one whose
            // clearing failed once that screen was up, which this takes away.
            slot2.Store.MarkResuming();
            var log = new List<string>();
            Assert.Null(CampaignBoot.Save(slot2, campaign, report: log.Add, savedAt: at.AddHours(3), byHand: true));
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(campaign, savedAt: at.AddHours(3))), slot2.Store.Read());
            Assert.Equal(backup, File.ReadAllBytes(slot2.Store.BackupPath));
            Assert.Equal([$"[Save] {slot2.Store.SavePath} already held this campaign - saved by hand, so its time alone is rewritten, and the backup kept",
                $"[Save] {slot2.Store.ResumingPath} cleared: the save it guarded has been written over"], log);
            Assert.False(slot2.Store.WasResuming);
            Assert.False(File.Exists(slot2.Store.TempPath));
            Assert.Equal(2, SaveListing.Read(slots, at.AddHours(3)).Newest!.Number);   // CONTINUE names the slot just saved by hand
            string stamped = slot2.Store.Read()!;

            // The game's own save of it, unchanged, writes nothing, as ever: the time
            // stays the hand save's, and the backup A.
            Assert.Null(CampaignBoot.Save(slot2, campaign, savedAt: at.AddHours(4)));
            Assert.Equal(stamped, slot2.Store.Read());
            Assert.Equal(backup, File.ReadAllBytes(slot2.Store.BackupPath));

            // A slot with no backup — a campaign's first save alone — is restamped
            // with none made.
            var slot3 = slots.Slot(3);
            Assert.Null(CampaignBoot.Save(slot3, other, savedAt: at));
            Assert.Null(CampaignBoot.Save(slot3, other, savedAt: at.AddHours(4), byHand: true));
            Assert.Equal(SaveCodec.Write(SaveGame.Capture(other, savedAt: at.AddHours(4))), slot3.Store.Read());
            Assert.Equal([SaveStore.FileName], Files(slot3.Store.Folder));

            // Windows only: a restamp that fails — the save held open by another
            // program that does not share its deletion — says so, and the save and
            // its backup are what they were, each under its own name: what the
            // answer says is kept is kept (the T4 review).
            if (OperatingSystem.IsWindows())
            {
                string? held;
                using (new FileStream(slot2.Store.SavePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    held = CampaignBoot.Save(slot2, campaign, savedAt: at.AddHours(4.5), byHand: true);
                Assert.StartsWith("SLOT 2 COULD NOT BE SAVED (", held, StringComparison.Ordinal);
                Assert.EndsWith(" - WHAT IT HELD IS KEPT - SEE THE LOG", held, StringComparison.Ordinal);
                Assert.Equal(stamped, slot2.Store.Read());
                Assert.Equal(backup, File.ReadAllBytes(slot2.Store.BackupPath));
            }

            // A store that refuses its writes this session restamps nothing, and says
            // why.
            var refusing = new SaveSlots(folder, 3);
            refusing.Store(2).RefuseWrites("campaign.json is in the way");
            Assert.Equal(CampaignBoot.NotSaved(refusing.Slot(2), "campaign.json is in the way"),
                CampaignBoot.Save(refusing.Slot(2), campaign, savedAt: at.AddHours(5), byHand: true));
            Assert.Equal(stamped, slot2.Store.Read());
            Assert.Equal(backup, File.ReadAllBytes(slot2.Store.BackupPath));

            // A campaign that changed is written by hand as ever, the save before it
            // becoming the backup.
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Assert.Null(CampaignBoot.Save(slot2, campaign, savedAt: at.AddHours(6), byHand: true));
            Assert.Equal(stamped, slot2.Store.ReadBackup());
        });
    }

    [Fact]
    public void ASaveThatCanBeNeitherReadNorMovedIsNeverWrittenOver()
    {
        // Windows only: another program holding the save open without sharing it
        // makes it unreadable and unmovable at once. It cannot be set aside, so it
        // stays where it is, and nothing is saved this session — a new campaign's
        // saves would otherwise take its place once it is freed.
        if (!OperatingSystem.IsWindows())
            return;

        InTempFolder(folder =>
        {
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            string saved = store.Read()!;

            // The load brings nothing back, and a new campaign — NEW CAMPAIGN's, say —
            // is saved into the slot while the file is still held.
            SlotLoad load;
            string? notice;
            var fresh = NewCampaign();
            using (new FileStream(store.SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                load = Load(store);
                notice = CampaignBoot.Save(AsSlot1(store), fresh);
            }

            Assert.False(load.Loaded);
            Assert.Collection(load.Notices,
                line => Assert.Equal(CampaignBoot.StuckNotice("campaign.json", "could not be read (another program has it open)", "another program has it open"), line));
            Assert.NotNull(store.WritesRefused);
            Assert.StartsWith("SLOT 1 COULD NOT BE SAVED (CAMPAIGN.JSON COULD NOT BE READ", notice, StringComparison.Ordinal);

            // Freed, this session still saves nothing over it: it is the save, byte
            // for byte, and nothing was set aside or written beside it. The next
            // launch reads it as ever.
            Assert.NotNull(CampaignBoot.Save(AsSlot1(store), fresh));
            Assert.Equal(saved, store.Read());
            Assert.Equal([SaveStore.FileName], Files(folder));
            Assert.True(Load(new SaveStore(folder)).Loaded);
        });
    }
}
