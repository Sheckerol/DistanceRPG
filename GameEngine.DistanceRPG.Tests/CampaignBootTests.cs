using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Which campaign a launch plays, and a save that never throws (1f, USER
/// DECISION 2; PHASE5-084). Loading is resuming: the save is resumed and says
/// so; one that cannot be read, or whose resume crashed the game, is set aside —
/// never deleted, never written over — with a notice naming its full path, and
/// the backup is tried, then a new campaign; <c>--new-campaign</c> sets the save
/// aside before anything is read; a dev-override launch plays a fresh campaign
/// in a slot of its own and never touches the campaign; a tuning changed since
/// the save is said on screen; and a write that fails is a notice, never an
/// exception, and never costs the save before it. Every case runs on a temporary
/// folder of its own, deleted after. In the content collection, since a campaign
/// is built and restored under the current content and one case swaps it.
/// </summary>
[Collection(TestContent.Collection)]
public class CampaignBootTests
{
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

    /// <summary>The campaign this launch plays, from <paramref name="store"/>, under the current content.</summary>
    private static BootResult Boot(SaveStore store, BootRequest? request = null, List<string>? log = null)
        => CampaignBoot.Boot(store, GameContent.Current, request ?? new BootRequest(), log is null ? null : log.Add);

    /// <summary>Save <paramref name="campaign"/>, and it saved.</summary>
    private static void Saved(SaveStore store, CampaignState campaign) => Assert.Null(CampaignBoot.Save(store, campaign));

    /// <summary>The text <paramref name="campaign"/> saves as.</summary>
    private static string Text(CampaignState campaign) => SaveCodec.Write(SaveGame.Capture(campaign));

    /// <summary>The names of the files directly in <paramref name="folder"/>, in ordinal order.</summary>
    private static string[] Files(string folder)
        => Directory.GetFiles(folder).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray();

    /// <summary>A set-aside notice: the file, what became of it, its new full path, and how to try it again — every one ends so.</summary>
    private static void SaysSetAside(string notice, string file, string what, string path)
    {
        Assert.Equal(CampaignBoot.SetAsideNotice(file, what, path), notice);
        Assert.StartsWith($"{file.ToUpperInvariant()} {what.ToUpperInvariant()}", notice, StringComparison.Ordinal);
        Assert.Contains($" SET ASIDE AS {path} ", notice, StringComparison.Ordinal);
        Assert.True(Path.IsPathFullyQualified(path), path);
        Assert.EndsWith("RENAME IT TO CAMPAIGN.JSON TO TRY AGAIN", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSaveIsANewCampaign()
    {
        InTempFolder(folder =>
        {
            // Nothing in the folder — the first launch ever — is a new campaign: the
            // roster's four, nothing met but their loadout, nothing played, every
            // dungeon but the tutorial behind it. Nothing to say, and nothing written.
            var store = new SaveStore(Path.Combine(folder, "saves"));
            var boot = Boot(store);
            Assert.False(boot.Resumed);
            Assert.Empty(boot.Notices);
            Assert.Null(boot.View);
            Assert.Null(boot.StraightInto);
            Assert.Null(boot.FirstSeed);
            Assert.Equal(GameContent.Current.Party.All.Select(member => member.Id), boot.Campaign.Party.Select(member => member.Id));
            StateAssert.Equivalent(NewCampaign(), boot.Campaign, "campaign");
            Assert.Equal(0, boot.Campaign.RunCounter);
            Assert.Null(boot.Campaign.LastResult);
            Assert.False(Directory.Exists(store.Folder));

            // Its first save makes the folder and the save, and the next launch resumes it.
            Saved(store, boot.Campaign);
            Assert.Equal([SaveStore.FileName], Files(store.Folder));
            Assert.True(Boot(store).Resumed);
            store.ClearResuming();

            // A save gone while its backup stands — deleted by hand, say — is no
            // reason to lose the campaign: the backup resumes, one save older, and
            // says so. Only with neither is a launch a new campaign.
            CampaignRuns.Clear(boot.Campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, boot.Campaign);
            File.Delete(store.SavePath);
            var fromBackup = Boot(store);
            Assert.True(fromBackup.Resumed);
            Assert.Equal([CampaignBoot.ResumedFromBackup], fromBackup.Notices);
            Assert.Equal(0, fromBackup.Campaign.RunCounter);
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
            var boot = Boot(store, log: log);
            Assert.True(boot.Resumed);
            StateAssert.Equivalent(campaign, boot.Campaign, "campaign");
            Assert.Equal(2, boot.Campaign.RunCounter);
            Assert.Equal(campaign.LockedInOrder, boot.Campaign.LockedInOrder);
            Assert.Contains(("spear_hall", campaign.LockedUntilRun("spear_hall")), boot.Campaign.LockedInOrder);
            Assert.Equal(["tutorial"], boot.Campaign.RetiredInOrder);
            Assert.Equal(["spear_hall", "tutorial"], boot.Campaign.ClearedInOrder);

            // The screen between runs it opens on is the one the campaign left: the
            // last run's summary over the list, the same list, the same choice.
            Assert.Equal(BetweenRunsHud.SummaryLines(campaign.LastResult!), BetweenRunsHud.SummaryLines(boot.Campaign.LastResult!));
            Assert.Equal(campaign.Listings().Select(l => (l.Def.Id, l.Selectable, l.RunsUntilAvailable, l.Early)),
                boot.Campaign.Listings().Select(l => (l.Def.Id, l.Selectable, l.RunsUntilAvailable, l.Early)));
            Assert.Equal(campaign.DefaultNext(), boot.Campaign.DefaultNext());

            // And it says so, first under the title, in green: news, not a warning.
            Assert.Equal([BetweenRunsHud.Resumed], boot.Notices);
            var rows = NoticeText.Rows(boot.Notices);
            Assert.Equal(["CAMPAIGN RESUMED"], rows);
            Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(rows[0]));
            Assert.Contains(log, line => line.StartsWith($"[Save] Resumed {store.SavePath}", StringComparison.Ordinal));

            // The resume is guarded until the game clears the marker, once the
            // resumed screen is up; and the campaign saves as the very text it was
            // resumed from — nothing lost, nothing re-derived.
            Assert.True(store.WasResuming);
            Assert.Equal(store.Read(), Text(boot.Campaign));
            store.ClearResuming();

            // It plays on: the next run goes in and ends as it would have.
            Assert.Equal(RunOutcome.Retreat, CampaignRuns.DeepRetreat(boot.Campaign, "long_gallery").Outcome);
            Assert.Equal(3, boot.Campaign.RunCounter);
        });
    }

    [Fact]
    public void AnUnreadableSaveIsSetAside_TheBackupTried_ThenANewCampaign()
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
            var boot = Boot(store, log: log);
            Assert.True(boot.Resumed);
            Assert.Equal(1, boot.Campaign.RunCounter);
            Assert.Equal(["tutorial"], boot.Campaign.RetiredInOrder);
            string first = Path.Combine(store.Folder, "campaign.unreadable-1.json");
            Assert.Equal(broken, File.ReadAllText(first));
            Assert.False(store.HasSave);
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
                notice => SaysSetAside(notice, "campaign.json", $"could not be read ({WhyOf(broken, store)})", first));
            Assert.Contains(log, line => line.Contains(store.SavePath, StringComparison.Ordinal) && line.Contains("could not be resumed", StringComparison.Ordinal));
            store.ClearResuming();

            // The campaign played from the backup saves as ever: its first write
            // moves into place, and the file set aside is not touched by it.
            Saved(store, boot.Campaign);
            Assert.Equal(Text(boot.Campaign), store.Read());
            Assert.Equal(broken, File.ReadAllText(first));

            // Both unreadable — the save is not a save, the backup not even an
            // object: each is set aside by the next free number, and a new campaign
            // begins, saying so first.
            File.WriteAllText(store.SavePath, "not a save");
            File.WriteAllText(store.BackupPath, "[1, 2, 3]");
            boot = Boot(store);
            Assert.False(boot.Resumed);
            StateAssert.Equivalent(NewCampaign(), boot.Campaign, "campaign");
            string second = Path.Combine(store.Folder, "campaign.unreadable-2.json");
            string third = Path.Combine(store.Folder, "campaign.unreadable-3.json");
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
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
            boot = Boot(store);
            Assert.False(boot.Resumed);
            SaysSetAside(boot.Notices[1], "campaign.json", "could not be read (campaign.retired[0]: 'no_such_dungeon' is no dungeon in the content)",
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

            var boot = Boot(store);
            Assert.False(boot.Resumed);
            string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
                notice => SaysSetAside(notice, "campaign.json", $"could not be read (version: {SaveCodec.CurrentVersion + 1} is newer than this build reads ({SaveCodec.CurrentVersion}))", aside));

            // The new campaign's saves — the first, and one after a run, which
            // replaces it — leave the newer save's bytes exactly as they were.
            Saved(store, boot.Campaign);
            CampaignRuns.ShallowRetreat(boot.Campaign, "tutorial");
            Saved(store, boot.Campaign);
            Assert.Equal(newer, File.ReadAllText(aside));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName, "campaign.unreadable-1.json"], Files(folder));

            // Renamed back, it is read again, and refused again: set aside once more,
            // under the next free number, and still whole.
            File.Copy(aside, store.SavePath, overwrite: true);
            var again = Boot(store);
            Assert.True(again.Resumed);   // the backup, the new campaign's first save
            Assert.Equal(newer, File.ReadAllText(Path.Combine(store.Folder, "campaign.unreadable-2.json")));
            Assert.Equal(newer, File.ReadAllText(aside));
            store.ClearResuming();
        });
    }

    [Fact]
    public void AChangedTuningIsANotice()
    {
        InTempFolder(folder =>
        {
            // PHASE5-084, §5.3: "loading under different numbers warns rather than
            // refuses ... silently changing a saved game's rules underneath the
            // player is the failure to avoid". A campaign saved under the shipped
            // numbers, resumed under others, says so on the screen it opens on.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            Assert.Equal([BetweenRunsHud.Resumed], Boot(store).Notices);   // the same numbers: nothing to say
            store.ClearResuming();

            var tuned = ContentDefaults.Tuning with { MovementBudget = 200 };
            using (TestContent.Use(tuning: tuned))
            {
                var log = new List<string>();
                var boot = Boot(store, log: log);
                Assert.True(boot.Resumed);
                Assert.Equal("THE NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS ON UNDER THE NEW ONES", CampaignBoot.TuningChanged);
                Assert.Equal([BetweenRunsHud.Resumed, CampaignBoot.TuningChanged], boot.Notices);

                // On screen whole, under the resumed line, even ahead of what the data
                // files raised: the save's word leads, and the file's notice waits in the log.
                var held = new StartupNotices();
                held.Hold(["tuning.json: MovementBudget 20000 is above 10000 - using 10000"]);
                held.Lead(boot.Notices);
                var rows = NoticeText.Rows(held.Take());
                Assert.Equal("CAMPAIGN RESUMED", rows[0]);
                Assert.Equal(CampaignBoot.TuningChanged, string.Join(" ", rows.Skip(1).Take(2).Select(row => row.Trim())));
                Assert.Equal(NoticeText.More(1), rows[^1]);
                Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor(rows[1]));

                // The log names both hashes; the campaign is the one saved, playing on.
                Assert.Contains(log, line => line.Contains(TuningJson.Hash(ContentDefaults.Tuning), StringComparison.Ordinal)
                    && line.Contains(TuningJson.Hash(tuned), StringComparison.Ordinal));
                Assert.Equal(campaign.LockedInOrder, boot.Campaign.LockedInOrder);
                Assert.Equal(1, boot.Campaign.RunCounter);
                store.ClearResuming();

                // The next save records the new hash, so the change is said once.
                Saved(store, boot.Campaign);
                Assert.Equal([BetweenRunsHud.Resumed], Boot(store).Notices);
                store.ClearResuming();
            }

            // Back under the shipped numbers, they have changed again, and it says so again.
            Assert.Equal([BetweenRunsHud.Resumed, CampaignBoot.TuningChanged], Boot(store).Notices);
            store.ClearResuming();
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
            var boot = CampaignBoot.Boot(slot, GameContent.Current, request);
            Assert.False(boot.Resumed);
            Assert.Equal("spear_hall", boot.StraightInto);
            Assert.Equal(7L, boot.FirstSeed);
            Assert.Equal([CampaignBoot.OverrideNotice], boot.Notices);
            StateAssert.Equivalent(NewCampaign(), boot.Campaign, "campaign");

            // It plays like any campaign, and saves into its slot.
            var visit = boot.Campaign.Enter(boot.StraightInto!, boot.FirstSeed!.Value);
            Assert.Equal(7L, visit.MapSeed);
            visit.OpenTurns(boot.Campaign.Party, CampaignRuns.Ten);
            CampaignRuns.Exit(boot.Campaign);
            Saved(slot, boot.Campaign);
            Assert.Equal([SaveStore.FileName], Files(slot.Folder));

            // The campaign's own save: the same bytes, and nothing beside it but the slot.
            Assert.Equal(saved, File.ReadAllBytes(own.SavePath));
            Assert.Equal([SaveStore.FileName], Files(folder));
            Assert.Equal([SaveStore.OverrideSlot], Directory.GetDirectories(folder).Select(path => Path.GetFileName(path)!));

            // A second such launch starts afresh: the slot emptied, nothing resumed.
            var again = CampaignBoot.Boot(slot, GameContent.Current, request);
            Assert.False(again.Resumed);
            Assert.Null(again.Campaign.LastResult);
            Assert.Empty(Files(slot.Folder));

            // A seed alone is a dev override too: its own slot, the screen between runs first.
            var seeded = new BootRequest(Seed: 7);
            var bySeed = CampaignBoot.Boot(CampaignBoot.StoreFor(folder, seeded), GameContent.Current, seeded);
            Assert.False(bySeed.Resumed);
            Assert.Null(bySeed.StraightInto);
            Assert.Equal(7L, bySeed.FirstSeed);

            // --new-campaign beside an override touches nothing of the campaign's
            // either, and an override on the campaign's own folder is refused rather
            // than emptying it.
            var both = new BootRequest("spear_hall", NewCampaign: true);
            Assert.False(CampaignBoot.Boot(CampaignBoot.StoreFor(folder, both), GameContent.Current, both).Resumed);
            Assert.Throws<InvalidOperationException>(() => CampaignBoot.Boot(own, GameContent.Current, request));
            Assert.Equal(saved, File.ReadAllBytes(own.SavePath));
            Assert.Equal([SaveStore.FileName], Files(folder));

            // And a launch without one resumes the campaign as it was left.
            var resumed = Boot(own);
            Assert.True(resumed.Resumed);
            Assert.Equal(1, resumed.Campaign.RunCounter);
            Assert.Equal(["tutorial"], resumed.Campaign.RetiredInOrder);
            own.ClearResuming();
        });
    }

    [Fact]
    public void NewCampaignAtLaunchSetsTheSaveAside()
    {
        InTempFolder(folder =>
        {
            // DistanceRPG --new-campaign: the save is set aside as abandoned before
            // anything is read — never deleted — and a fresh campaign begins.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);
            byte[] saved = File.ReadAllBytes(store.SavePath);
            byte[] backup = File.ReadAllBytes(store.BackupPath);

            var boot = Boot(store, new BootRequest(NewCampaign: true));
            Assert.False(boot.Resumed);
            StateAssert.Equivalent(NewCampaign(), boot.Campaign, "campaign");
            string aside = Path.Combine(store.Folder, "campaign.abandoned-1.json");
            Assert.Equal(saved, File.ReadAllBytes(aside));
            Assert.False(store.HasSave);
            Assert.Equal(backup, File.ReadAllBytes(store.BackupPath));   // --new-campaign sets the save aside, and nothing else
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
                notice => SaysSetAside(notice, "campaign.json", "was left for a new campaign (--new-campaign)", aside));
            Assert.False(store.WasResuming);

            // Its saves go where the old one was, and the one set aside stays whole.
            Saved(store, boot.Campaign);
            Assert.Equal(saved, File.ReadAllBytes(aside));

            // Again: the next number, and the first untouched.
            var again = Boot(store, new BootRequest(NewCampaign: true));
            Assert.Equal(Text(boot.Campaign), File.ReadAllText(Path.Combine(store.Folder, "campaign.abandoned-2.json")));
            Assert.Equal(saved, File.ReadAllBytes(aside));
            Assert.Equal(2, again.Notices.Count);

            // With no save there is nothing to set aside, and a new campaign all the same.
            Assert.Equal([CampaignBoot.NewCampaignBegins], Boot(store, new BootRequest(NewCampaign: true)).Notices);
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
            Assert.True(Boot(store).Resumed);
            Assert.True(store.WasResuming);

            // The next launch finds the marker: the save it guarded crashed the game,
            // so it is set aside as crashed, and the backup resumes — guarded in turn.
            var boot = Boot(store);
            Assert.True(boot.Resumed);
            Assert.Equal(1, boot.Campaign.RunCounter);
            string crashed = Path.Combine(store.Folder, "campaign.crashed-1.json");
            Assert.Equal(guarded, File.ReadAllText(crashed));
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
                notice => SaysSetAside(notice, "campaign.json", "crashed the game the last time it was resumed", crashed));
            Assert.True(store.WasResuming);

            // That one crashed too: the backup is what it guarded, set aside the same
            // way, and a new campaign begins.
            var fresh = Boot(store);
            Assert.False(fresh.Resumed);
            string crashedBackup = Path.Combine(store.Folder, "campaign.crashed-2.json");
            Assert.Equal(older, File.ReadAllText(crashedBackup));
            Assert.Collection(fresh.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
                notice => SaysSetAside(notice, "campaign.bak.json", "crashed the game the last time it was resumed", crashedBackup));
            Assert.Equal((false, false, false), (store.HasSave, store.HasBackup, store.WasResuming));

            // A marker with nothing left to guard is only cleared, and says nothing.
            store.MarkResuming();
            Assert.Empty(Boot(store).Notices);
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
            string? notice = CampaignBoot.Save(store, campaign, report: log.Add);
            Assert.NotNull(notice);
            Assert.StartsWith("THE CAMPAIGN COULD NOT BE SAVED (", notice, StringComparison.Ordinal);
            Assert.EndsWith(") - THE LAST SAVE IS KEPT - SEE THE LOG", notice, StringComparison.Ordinal);
            Assert.Equal(previous, store.Read());
            Assert.False(store.HasBackup);
            Assert.Contains(log, line => line.StartsWith($"[Save] Could not write {store.SavePath}", StringComparison.Ordinal));

            // Freed, the same save goes through, and the one before it is the backup.
            Directory.Delete(store.TempPath);
            log.Clear();
            Assert.Null(CampaignBoot.Save(store, campaign, report: log.Add));
            Assert.Equal((Text(campaign), previous), (store.Read(), store.ReadBackup()));
            Assert.Equal([$"[Save] Wrote {store.SavePath}"], log);

            // A campaign mid-run is not saved yet (Phase 5.8): asked anyway, it is a
            // notice and never a throw, and the save between runs is kept.
            string between = store.Read()!;
            campaign.Enter("spear_hall", CampaignRuns.FiveDeep);
            Assert.NotNull(CampaignBoot.Save(store, campaign));
            Assert.Equal((between, previous), (store.Read(), store.ReadBackup()));
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
            Assert.Null(CampaignBoot.Save(store, campaign, report: log.Add));
            Assert.Equal((Text(campaign), before), (store.Read(), store.ReadBackup()));
            Assert.Equal([$"[Save] {store.SavePath} already holds this campaign - nothing written, the backup kept"], log);

            // Anything that changed is written as ever.
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);
            Assert.NotEqual(before, store.ReadBackup());
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

            BootResult boot;
            string? notice;
            using (new FileStream(store.SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                boot = Boot(store);
                notice = CampaignBoot.Save(store, boot.Campaign);
            }

            Assert.False(boot.Resumed);
            Assert.Collection(boot.Notices,
                line => Assert.Equal(CampaignBoot.NewCampaignBegins, line),
                line => Assert.Equal(CampaignBoot.StuckNotice("campaign.json", "could not be read (another program has it open)", "another program has it open"), line));
            Assert.NotNull(store.WritesRefused);
            Assert.StartsWith("THE CAMPAIGN COULD NOT BE SAVED (CAMPAIGN.JSON COULD NOT BE READ", notice, StringComparison.Ordinal);

            // Freed, this session still saves nothing over it: it is the save, byte
            // for byte, and nothing was set aside or written beside it. The next
            // launch reads it as ever.
            Assert.NotNull(CampaignBoot.Save(store, boot.Campaign));
            Assert.Equal(saved, store.Read());
            Assert.Equal([SaveStore.FileName], Files(folder));
            Assert.True(Boot(new SaveStore(folder)).Resumed);
        });
    }
}
