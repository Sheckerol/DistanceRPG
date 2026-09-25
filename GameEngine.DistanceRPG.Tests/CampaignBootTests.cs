using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// What loading a slot brings back, and a save that never throws (1f; the user's
/// ruling of 2026-09-25 on the save model; PHASE5-084). Loading is resuming: the
/// save is resumed and says so; one that cannot be read, or whose resume crashed
/// the game, is set aside — never deleted, never written over — with a notice
/// naming its full path, and the backup is tried, then nothing is brought back,
/// a load making no campaign of its own; <c>--new-campaign</c> sets the save and
/// its backup aside before anything is read, so neither can come back as the new
/// campaign's; the save's word is shown whole, the numbers ahead of any file set
/// aside; a dev-override launch plays a fresh campaign in a slot of its own and
/// never touches the campaign; a tuning changed since the save is said on
/// screen; and a write that fails is a notice, never an exception, and never
/// costs the save before it. Every case runs on a temporary folder of its own,
/// deleted after, whose store is the autosave slot's. The runs behind the saves
/// stand on the numbers they were written against — the seeds' depths, the mercy
/// floor (<see cref="Premise"/>) — made current for each test, and a campaign is
/// built and restored under it and some cases change the numbers over it, so the
/// content collection holds it.
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

    /// <summary>What loading <paramref name="store"/> — the autosave slot — brings back, under the current content.</summary>
    private static SlotLoad Load(SaveStore store, List<string>? log = null)
        => CampaignBoot.Load(store, GameContent.Current, log is null ? null : log.Add);

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
    public void AnEmptySlotLoadsNothing()
    {
        InTempFolder(folder =>
        {
            // Nothing in the folder — the first launch ever — loads nothing: no
            // campaign, no view, nothing to say, and nothing written, not even the
            // folder. A load never makes a campaign: what follows is the caller's
            // (the launch begins one, DistanceRpgGame.BootCampaign).
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
            Assert.Equal([CampaignBoot.ResumedFromBackup], fromBackup.Notices);
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
            Assert.Equal([BetweenRunsHud.Resumed], boot.Notices);
            var rows = NoticeText.Rows(boot.Notices);
            Assert.Equal(["CAMPAIGN RESUMED"], rows);
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
                notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
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
            // set aside and nothing else: A NEW CAMPAIGN BEGINS is the launch's word.
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
            Assert.Null(CampaignBoot.Save(store, campaign, view));   // a player turn's start: the game writes it
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
            Assert.Equal([BetweenRunsHud.Resumed], boot.Notices);
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
                notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
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
            Assert.Null(CampaignBoot.Save(store, campaign, new ViewDto("B")));   // a player turn's start
            script.EndTurn(SaveGame.LogicStepSeconds);
            Assert.Null(CampaignBoot.Save(store, campaign, new ViewDto("B")));   // the next one: the save before it is the backup
            File.WriteAllText(store.SavePath, "not a save");

            var boot = Load(store);
            Assert.True(boot.Loaded);
            Assert.Equal(("spear_hall", 1), (boot.Campaign!.Visit!.Def.Id, boot.Campaign!.Visit.FloorIndex));
            Assert.Equal(new ViewDto("B"), boot.View);
            string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
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
            Assert.NotNull(CampaignBoot.Save(store, quitting.Campaign));
            Assert.False(store.HasSave);
            int calls = 0;
            var log = new List<string>();
            Assert.True(CampaignBoot.SaveOnQuit(store, quitting.Campaign,
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
            Assert.False(CampaignBoot.SaveOnQuit(store, unplayed.Campaign, () => view, step: null));
            Assert.Equal(reached, store.Read());
            Assert.Equal(TurnPhase.EnemyMoving, unplayed.Turns.Phase);

            // A step that never brings the turn round is given up on, and nothing is
            // written either.
            var stuck = ReloadDeterminismTests.MidWalk();
            Assert.False(CampaignBoot.SaveOnQuit(store, stuck.Campaign, () => view, () => { }));
            Assert.Equal(reached, store.Read());

            // At a safe point already the quit takes no step and saves as it stands;
            // a campaign between runs carries no view.
            Assert.True(CampaignBoot.SaveOnQuit(store, live.Campaign, () => view with { ActiveMemberId = "B" },
                () => throw new Xunit.Sdk.XunitException("a player turn needs no step")));
            Assert.Equal("B", SaveCodec.Read(store.Read()!).View!.ActiveMemberId);
            var between = NewCampaign();
            Assert.True(CampaignBoot.SaveOnQuit(store, between, () => throw new Xunit.Sdk.XunitException("a campaign between runs has no view"), step: null));
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

            Assert.True(CampaignBoot.SaveOnQuit(store, script.Campaign,
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
                Assert.Null(CampaignBoot.Save(store, script.Campaign, view));
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
            Assert.Null(CampaignBoot.Save(store, script.Campaign, granted));
            var calm = Load(store);
            store.ClearResuming();
            Assert.True(calm.View!.MarchingThisTurn);
            Assert.False(calm.Campaign!.Visit!.OpenTurns(calm.Campaign!.Party, CampaignRuns.Ten).AnyLiveEnemySeenThisTurn);

            var sighted = script.Floor.Enemies.First(row => row.Alive);
            script.Turns.NotifyEnemyVisible(sighted, true);   // a live enemy in sight, as the scene tells it: the march is revoked this turn
            Assert.True(script.Turns.AnyLiveEnemySeenThisTurn);
            Assert.Null(CampaignBoot.Save(store, script.Campaign, granted));
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
            Assert.Null(CampaignBoot.Save(store, script.Campaign, new ViewDto("A")));

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
            Assert.Equal([BetweenRunsHud.Resumed], Load(store).Notices);   // the same numbers: nothing to say
            store.ClearResuming();

            var played = GameContent.Current.Tuning;
            var tuned = played with { MovementBudget = 200 };
            using (TestContent.UsePremise(tuning: _ => tuned))
            {
                var log = new List<string>();
                var boot = Load(store, log: log);
                Assert.True(boot.Loaded);
                Assert.Equal("THE NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS ON UNDER THE NEW ONES", CampaignBoot.TuningChanged);
                Assert.Equal([BetweenRunsHud.Resumed, CampaignBoot.TuningChanged], boot.Notices);

                // On screen whole, under the resumed line and ahead of what the data
                // files raised: the save's word leads, shown in full, and the file's
                // notice follows it.
                var held = new StartupNotices();
                held.Hold(["tuning.json: MovementBudget 20000 is above 10000 - using 10000"]);
                held.Lead(boot.Notices);
                int whole = held.Leading;
                var rows = NoticeText.Rows(held.Take(), whole);
                Assert.Equal(2, whole);
                Assert.Equal("CAMPAIGN RESUMED", rows[0]);
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
                Assert.Equal([BetweenRunsHud.Resumed], Load(store).Notices);
                store.ClearResuming();
            }

            // Back under the numbers it was first played under, they have changed again, and it says so again.
            Assert.Equal([BetweenRunsHud.Resumed, CampaignBoot.TuningChanged], Load(store).Notices);
            store.ClearResuming();
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
                    notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
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
                Assert.Equal(CampaignBoot.ResumedFromBackup, rows[0]);
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
            var boot = CampaignBoot.BootOverride(slot, GameContent.Current, request);
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
            var again = CampaignBoot.BootOverride(slot, GameContent.Current, request);
            Assert.False(again.Resumed);
            Assert.Null(again.Campaign.LastResult);
            Assert.Empty(Files(slot.Folder));

            // A seed alone is a dev override too: its own slot, the screen between runs first.
            var seeded = new BootRequest(Seed: 7);
            var bySeed = CampaignBoot.BootOverride(CampaignBoot.StoreFor(folder, seeded), GameContent.Current, seeded);
            Assert.False(bySeed.Resumed);
            Assert.Null(bySeed.StraightInto);
            Assert.Equal(7L, bySeed.FirstSeed);

            // --new-campaign beside an override touches nothing of the campaign's
            // either, and an override on the campaign's own folder is refused rather
            // than emptying it.
            var both = new BootRequest("spear_hall", NewCampaign: true);
            Assert.False(CampaignBoot.BootOverride(CampaignBoot.StoreFor(folder, both), GameContent.Current, both).Resumed);
            Assert.Throws<InvalidOperationException>(() => CampaignBoot.BootOverride(own, GameContent.Current, request));
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
            // DistanceRPG --new-campaign: the save and its backup are set aside as
            // abandoned before anything is read — never deleted — and a fresh
            // campaign begins. The backup goes first, so the later save takes the
            // later number.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);
            byte[] saved = File.ReadAllBytes(store.SavePath);
            byte[] backup = File.ReadAllBytes(store.BackupPath);

            var boot = CampaignBoot.Abandon(store, GameContent.Current);
            Assert.False(boot.Resumed);
            StateAssert.Equivalent(NewCampaign(), boot.Campaign, "campaign");
            string backupAside = Path.Combine(store.Folder, "campaign.abandoned-1.json");
            string aside = Path.Combine(store.Folder, "campaign.abandoned-2.json");
            Assert.Equal(backup, File.ReadAllBytes(backupAside));
            Assert.Equal(saved, File.ReadAllBytes(aside));
            Assert.Equal((false, false), (store.HasSave, store.HasBackup));   // nothing of the old campaign's is left to resume
            Assert.Collection(boot.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
                notice => SaysSetAside(notice, "campaign.bak.json", "was left for a new campaign (--new-campaign)", backupAside),
                notice => SaysSetAside(notice, "campaign.json", "was left for a new campaign (--new-campaign)", aside));
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
            Saved(store, boot.Campaign);
            Assert.Equal(["campaign.abandoned-1.json", "campaign.abandoned-2.json", SaveStore.FileName], Files(folder));
            BothStillWhole();

            // Again: the next number, and the first two untouched. The new
            // campaign's first save had no backup to keep, so there is none to set aside.
            var again = CampaignBoot.Abandon(store, GameContent.Current);
            Assert.Equal(Text(boot.Campaign), File.ReadAllText(Path.Combine(store.Folder, "campaign.abandoned-3.json")));
            BothStillWhole();
            Assert.Equal(2, again.Notices.Count);

            // With neither there is nothing to set aside, and a new campaign all the same.
            Assert.Equal([CampaignBoot.NewCampaignBegins], CampaignBoot.Abandon(store, GameContent.Current).Notices);

            // A backup alone — its save deleted by hand — is set aside the same way.
            File.Copy(backupAside, store.BackupPath);
            var alone = CampaignBoot.Abandon(store, GameContent.Current);
            string fourth = Path.Combine(store.Folder, "campaign.abandoned-4.json");
            Assert.Equal(backup, File.ReadAllBytes(fourth));
            Assert.Collection(alone.Notices,
                notice => Assert.Equal(CampaignBoot.NewCampaignBegins, notice),
                notice => SaysSetAside(notice, "campaign.bak.json", "was left for a new campaign (--new-campaign)", fourth));
            Assert.Equal((false, false), (store.HasSave, store.HasBackup));
        });
    }

    [Fact]
    public void NewCampaignAtLaunchNeverBringsTheOldCampaignBack()
    {
        InTempFolder(folder =>
        {
            // A campaign two saves in: the save and its backup both the old campaign's.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);

            // --new-campaign, and nothing saved before the game goes down: a crash
            // in the new campaign's first run before any save was written. The next
            // load brings nothing back — never the old campaign's backup, as though
            // it were the new one's — and says nothing.
            var abandoned = CampaignBoot.Abandon(store, GameContent.Current);
            Assert.False(abandoned.Resumed);
            abandoned.Campaign.Enter("tutorial", CampaignRuns.GoldenSeed);   // its first run begun, then down: nothing saved
            var next = Load(store);
            Assert.False(next.Loaded);
            Assert.Null(next.Campaign);
            Assert.Empty(next.Notices);
            Assert.Equal((false, false), (store.HasSave, store.HasBackup));

            // The game saves the new campaign at once (DistanceRpgGame.BootCampaign),
            // so after the same crash the next load resumes that campaign — the new
            // one, as it stood when its run began.
            var fresh = CampaignBoot.Abandon(store, GameContent.Current);
            Saved(store, fresh.Campaign);
            fresh.Campaign.Enter("tutorial", CampaignRuns.GoldenSeed);
            var resumed = Load(store);
            Assert.True(resumed.Loaded);
            Assert.Equal([BetweenRunsHud.Resumed], resumed.Notices);
            StateAssert.Equivalent(NewCampaign(), resumed.Campaign!, "campaign");
            Assert.False(store.HasBackup);
            store.ClearResuming();

            // And a first save that cannot be played has no backup of the old
            // campaign's to fall back on: it is set aside, and nothing is brought back.
            File.WriteAllText(store.SavePath, "not a save");
            var fallback = Load(store);
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
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            Saved(store, campaign);
            CampaignRuns.DeepRetreat(campaign, "spear_hall");
            Saved(store, campaign);
            string saved = store.Read()!;

            BootResult boot;
            using (new FileStream(store.BackupPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                boot = CampaignBoot.Abandon(store, GameContent.Current);

            Assert.False(boot.Resumed);
            Assert.Collection(boot.Notices,
                line => Assert.Equal(CampaignBoot.NewCampaignBegins, line),
                line => Assert.Equal(CampaignBoot.StuckNotice("campaign.bak.json", "was left for a new campaign (--new-campaign)", "another program has it open"), line));
            Assert.NotNull(store.WritesRefused);
            Assert.NotNull(CampaignBoot.Save(store, boot.Campaign));
            Assert.Equal(saved, store.Read());
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName], Files(folder));

            var next = Load(new SaveStore(folder));
            Assert.True(next.Loaded);
            Assert.Equal([BetweenRunsHud.Resumed], next.Notices);
            Assert.Equal(2, next.Campaign!.RunCounter);
            new SaveStore(folder).ClearResuming();
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
                notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
                notice => SaysSetAside(notice, "campaign.json", "crashed the game the last time it was resumed", crashed));
            Assert.True(store.WasResuming);

            // That one crashed too: the backup is what it guarded, set aside the same
            // way, and nothing is brought back. What follows is the caller's, so the
            // load says what it set aside and nothing else: A NEW CAMPAIGN BEGINS is
            // the launch's word.
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

            // A campaign mid-run is saved at a safe point (Phase 5.8), and asked off
            // one — the turn ended, the enemies' turn under way — it is a notice and
            // never a throw, and the save between runs is kept.
            string between = store.Read()!;
            campaign.Enter("spear_hall", CampaignRuns.FiveDeep).OpenTurns(campaign.Party, CampaignRuns.Ten).EndTurn();
            Assert.False(SaveGame.IsSafePoint(campaign));
            Assert.NotNull(CampaignBoot.Save(store, campaign));
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
            // campaign.unsaved beside the save it keeps, and the next launch says,
            // under the resumed line, that what resumes is from before that quit. A
            // player is never resumed behind where they left the game unwarned.
            var store = new SaveStore(folder);
            var campaign = NewCampaign();
            Saved(store, campaign);
            string before = store.Read()!;
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);   // played on since: the quit has something to save

            Directory.CreateDirectory(store.TempPath);
            var log = new List<string>();
            Assert.True(CampaignBoot.SaveOnQuit(store, campaign, view: null, step: null, log.Add));
            Assert.Equal(before, store.Read());
            string marker = Assert.IsType<string>(store.ReadUnsaved());
            Assert.StartsWith("The last quit could not save the campaign: THE CAMPAIGN COULD NOT BE SAVED (", marker, StringComparison.Ordinal);
            Assert.Contains(log, line => line.StartsWith($"[Save] {store.UnsavedPath} written", StringComparison.Ordinal));
            Assert.Equal([SaveStore.FileName, SaveStore.UnsavedName], Files(folder));
            Directory.Delete(store.TempPath);

            // The next launch resumes the save the quit could not replace and says so,
            // under the resumed line, with what the quit wrote in the log; the marker
            // goes as it is read, so it is said once.
            var bootLog = new List<string>();
            var boot = Load(store, log: bootLog);
            Assert.True(boot.Loaded);
            Assert.Equal(0, boot.Campaign!.RunCounter);   // the campaign as the last save left it, not as it was quit
            Assert.Equal([BetweenRunsHud.Resumed, CampaignBoot.QuitUnsaved], boot.Notices);
            Assert.Null(store.ReadUnsaved());
            Assert.Contains(bootLog, line => line.StartsWith($"[Save] {store.UnsavedPath} was left by the last quit", StringComparison.Ordinal)
                && line.Contains("THE CAMPAIGN COULD NOT BE SAVED (", StringComparison.Ordinal));
            store.ClearResuming();
            Assert.Equal([BetweenRunsHud.Resumed], Load(store).Notices);
            store.ClearResuming();

            // On screen whole, in the warning's orange: it is the save's word, which
            // leads, and takes two rows.
            var rows = NoticeText.Rows(boot.Notices, whole: boot.Notices.Count);
            Assert.Equal(boot.Notices.SelectMany(NoticeText.Wrap), rows);
            Assert.Equal(2, NoticeText.Wrap(CampaignBoot.QuitUnsaved).Count);
            Assert.All(rows.Skip(1), row => Assert.Equal(DungeonHud.Orange, BetweenRunsHud.NoticeColor(row)));
            Assert.Matches("^[A-Z0-9 ,-]+$", CampaignBoot.QuitUnsaved);   // the HUD's font: no apostrophe

            // A quit that saves leaves no marker.
            Assert.True(CampaignBoot.SaveOnQuit(store, campaign, view: null, step: null));
            Assert.Equal(Text(campaign), store.Read());
            Assert.Null(store.ReadUnsaved());

            // Nor does a quit whose save the disk already holds: nothing needed writing.
            Directory.CreateDirectory(store.TempPath);
            Assert.True(CampaignBoot.SaveOnQuit(store, campaign, view: null, step: null));
            Assert.Null(store.ReadUnsaved());
            Directory.Delete(store.TempPath);

            // A quit during the enemies' turn that cannot be played out writes nothing
            // and leaves the marker as well. The busiest resume there is — the save
            // unreadable and set aside, the backup resumed under other numbers — says
            // it between what resumed and the numbers it plays under, and the save's
            // word still fits the rows it is shown whole in (NoticeText.MaxWholeRows).
            var stuck = ReloadDeterminismTests.MidWalk();
            Assert.False(CampaignBoot.SaveOnQuit(store, stuck.Campaign, () => new ViewDto("A"), () => { }));
            Assert.Contains("did not come round to the party's", store.ReadUnsaved()!, StringComparison.Ordinal);
            File.WriteAllText(store.SavePath, "not a save");
            using (TestContent.UsePremise(tuning: t => t with { MovementBudget = 200 }))
            {
                var busiest = Load(store);
                Assert.True(busiest.Loaded);
                Assert.Collection(busiest.Notices,
                    notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
                    notice => Assert.Equal(CampaignBoot.QuitUnsaved, notice),
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
            Assert.False(CampaignBoot.SaveOnQuit(store, unplayed.Campaign, () => new ViewDto("A"), step: null));
            Assert.Contains("with no scene to play it out with", store.ReadUnsaved()!, StringComparison.Ordinal);
            var abandoned = CampaignBoot.Abandon(store, GameContent.Current);
            Assert.DoesNotContain(CampaignBoot.QuitUnsaved, abandoned.Notices);
            Assert.Null(store.ReadUnsaved());

            // A marker that cannot be written either — a folder in its way — leaves the
            // log alone to say so, and the quit is not stopped.
            Directory.CreateDirectory(store.UnsavedPath);
            Directory.CreateDirectory(store.TempPath);
            log.Clear();
            Assert.True(CampaignBoot.SaveOnQuit(store, campaign, view: null, step: null, log.Add));
            Assert.Contains(log, line => line.StartsWith($"[Save] {store.UnsavedPath} could not be written either", StringComparison.Ordinal));
            Assert.Null(store.ReadUnsaved());
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

            // The load brings nothing back, and a new campaign — the launch's, when
            // nothing loads — is saved into the slot while the file is still held.
            SlotLoad load;
            string? notice;
            var fresh = NewCampaign();
            using (new FileStream(store.SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                load = Load(store);
                notice = CampaignBoot.Save(store, fresh);
            }

            Assert.False(load.Loaded);
            Assert.Collection(load.Notices,
                line => Assert.Equal(CampaignBoot.StuckNotice("campaign.json", "could not be read (another program has it open)", "another program has it open"), line));
            Assert.NotNull(store.WritesRefused);
            Assert.StartsWith("THE CAMPAIGN COULD NOT BE SAVED (CAMPAIGN.JSON COULD NOT BE READ", notice, StringComparison.Ordinal);

            // Freed, this session still saves nothing over it: it is the save, byte
            // for byte, and nothing was set aside or written beside it. The next
            // launch reads it as ever.
            Assert.NotNull(CampaignBoot.Save(store, fresh));
            Assert.Equal(saved, store.Read());
            Assert.Equal([SaveStore.FileName], Files(folder));
            Assert.True(Load(new SaveStore(folder)).Loaded);
        });
    }
}
