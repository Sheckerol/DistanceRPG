using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The old autosave's move into a slot of its own (the user's ruling of 2026-09-25
/// on the save slots, R5; SLOT DECISION 4; <see cref="OldAutosave.MoveIntoASlot"/>):
/// the campaign a build before the ruling autosaved in the save folder itself —
/// its save, backup and two markers, the save first — moved byte for byte into the
/// lowest slot free for it, within the count or above it, a slot holding a stray
/// marker or a name taken by a folder passed over and never cleared; with no slot
/// free, left as the old autosave and played in place; a move a crash cut off
/// finished into the slot its marker names, a stale or a garbage marker dropped; a
/// try that stops undone and the next slot tried, a save folder every try fails at
/// refused its writes for the session, and a move left in two halves loading
/// neither; a marker that cannot be written, read or cleared refusing the save
/// folder's writes, saying so, and one that cannot be read kept; and a file renamed
/// back into the save folder moved at the next launch.
/// Nothing is ever deleted or written over. Every case runs on a temporary folder
/// of its own, deleted after, under the numbers the runs were written against
/// (<see cref="Premise"/>), so the content collection holds it. The cases that hold
/// a file open, as another program would, run on Windows alone.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class OldAutosaveTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>The clock the listings here are read at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Three times before <see cref="Now"/>, oldest first.</summary>
    private static readonly DateTimeOffset T1 = Now.AddHours(-3), T2 = Now.AddHours(-2), T3 = Now.AddHours(-1);

    /// <summary>The files of a campaign the move takes, in the order it takes them.</summary>
    private static readonly string[] Four = [SaveStore.FileName, SaveStore.BackupName, SaveStore.ResumingName, SaveStore.UnsavedName];

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

    /// <summary>A campaign one run on: the tutorial walked out of on its first floor.</summary>
    private static CampaignState Retreated()
    {
        var campaign = NewCampaign();
        CampaignRuns.ShallowRetreat(campaign, "tutorial");
        return campaign;
    }

    /// <summary>A campaign with the tutorial beaten and gone.</summary>
    private static CampaignState Cleared()
    {
        var campaign = NewCampaign();
        CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
        return campaign;
    }

    /// <summary>The text <paramref name="campaign"/> saves as, at <paramref name="at"/>.</summary>
    private static string Text(CampaignState campaign, DateTimeOffset at) => SaveCodec.Write(SaveGame.Capture(campaign, savedAt: at));

    /// <summary>Write <paramref name="text"/> as <paramref name="name"/> in <paramref name="store"/>'s folder, behind the store's back, as a build before the ruling, a hand or another program would.</summary>
    private static void Put(SaveStore store, string name, string text)
    {
        Directory.CreateDirectory(store.Folder);
        File.WriteAllText(Path.Combine(store.Folder, name), text);
    }

    /// <summary>The save folder as a build before the ruling left it: the autosave's save and backup in the folder itself, each at its own time.</summary>
    private static void Autosaved(SaveSlots slots)
    {
        var root = slots.Store(SaveSlots.OldAutosave);
        Put(root, SaveStore.BackupName, Text(NewCampaign(), T1));
        Put(root, SaveStore.FileName, Text(Retreated(), T3));
    }

    /// <summary>Every file under <paramref name="folder"/>, by its path inside it, with its bytes: what "nothing moved", and "moved byte for byte", are checked against.</summary>
    private static Dictionary<string, string> Snapshot(string folder)
        => Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folder, path))
            .ToDictionary(path => path, path => Convert.ToBase64String(File.ReadAllBytes(Path.Combine(folder, path))), StringComparer.Ordinal);

    /// <summary><paramref name="before"/> with the campaign's four files moved from the save folder into slot <paramref name="number"/>, and nothing else changed.</summary>
    private static Dictionary<string, string> MovedInto(Dictionary<string, string> before, int number)
        => before.ToDictionary(file => Four.Contains(file.Key) ? Path.Combine($"slot-{number}", file.Key) : file.Key, file => file.Value, StringComparer.Ordinal);

    /// <summary>The names of the files directly in <paramref name="folder"/>, in ordinal order; none for a folder that is not there.</summary>
    private static string[] Files(string folder)
        => Directory.Exists(folder) ? Directory.GetFiles(folder).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray() : [];

    [Fact]
    public void TheAutosaveMovesIntoTheLowestFreeSlot_ByteForByte()
    {
        InTempFolder(folder =>
        {
            // The save folder as a build before the ruling left it: the autosave's
            // save and backup, the marker of a resume that never finished and of a
            // quit that could not save, a file set aside, a write's temp file, and
            // the dev override's slot. Slot 1 holds a save; slot 2 only files set
            // aside, which are no obstacle.
            var slots = new SaveSlots(folder, 3);
            var root = slots.Store(SaveSlots.OldAutosave);
            Autosaved(slots);
            Put(root, SaveStore.ResumingName, "A resume is in flight.");
            Put(root, SaveStore.UnsavedName, "Leaving play could not save the campaign in this folder: the disk is full");
            Put(root, "campaign.unreadable-1.json", "not a save");
            Put(root, SaveStore.TempName, "a write cut off");
            Put(new SaveStore(Path.Combine(folder, SaveStore.OverrideSlot)), SaveStore.FileName, Text(Cleared(), T2));
            Assert.Null(CampaignBoot.Save(slots.Slot(1), Cleared(), savedAt: T2));
            Put(slots.Store(2), "campaign.abandoned-1.json", Text(NewCampaign(), T1));
            var before = Snapshot(folder);

            // The launch moves the four into slot 2, the lowest free: under their own
            // names, byte for byte, the save first; the file set aside, the temp file
            // and the dev override's slot stay; nothing is written, and nothing is
            // deleted but the marker of the move itself — which names slot 2 on the
            // disk before the first file moves, read as the save's move is logged.
            var log = new List<string>();
            int? marked = null;
            void Hook(string line)
            {
                log.Add(line);
                if (line == $"[Save] {root.SavePath} moved into {slots.Store(2).Folder}")
                    marked = root.ReadMoving();
            }
            var said = OldAutosave.MoveIntoASlot(slots, Hook);
            Assert.Equal(["THE AUTOSAVE MOVED INTO SLOT 2 - ITS CAMPAIGN SAVES THERE FROM NOW ON"], said);
            Assert.Equal(2, marked);
            Assert.Equal(MovedInto(before, 2), Snapshot(folder));
            var movedAt = Four.Select(name => log.FindIndex(line => line == $"[Save] {Path.Combine(root.Folder, name)} moved into {slots.Store(2).Folder}")).ToList();
            Assert.DoesNotContain(-1, movedAt);
            Assert.Equal(movedAt.Order(), movedAt);
            Assert.False(root.WasMoving);
            Assert.Equal(["campaign.json.tmp", "campaign.unreadable-1.json"], Files(folder));

            // The listing has no old autosave now: slot 2 holds the campaign, its last
            // load unfinished — the marker went with the save it guards — and the most
            // recent save. The title says the move, then the quit's word, which moved
            // with it and now speaks of slot 2.
            var listing = SaveListing.Read(slots, Now);
            Assert.Equal([1, 2, 3], listing.Slots.Select(slot => slot.Number));
            var two = listing.Slots[1];
            Assert.Equal((SlotState.Readable, true, false, T3), (two.State, two.Unfinished, two.Beyond, two.Header!.SavedAt));
            Assert.Same(two, listing.Newest);
            Assert.Equal([CampaignBoot.QuitUnsaved(slots.Slot(2))], CampaignBoot.TakeEveryUnsaved(slots));
            Assert.Equal("SLOT 2 COULD NOT BE SAVED WHEN YOU LAST LEFT PLAY - WHAT IT HOLDS IS FROM BEFORE THAT, BEHIND WHERE YOU LEFT IT", CampaignBoot.QuitUnsaved(slots.Slot(2)));

            // Slot 2's load does what the marker asked of the save folder's: the save
            // it guarded is set aside as crashed — in slot 2's own folder — and the
            // backup resumed, as a load of it always did.
            var load = CampaignBoot.Load(slots.Slot(2), GameContent.Current);
            Assert.True(load.Loaded);
            Assert.Equal(CampaignBoot.Resumed(slots.Slot(2), backup: true), load.Notices[0]);
            Assert.Contains(Path.Combine(slots.Store(2).Folder, "campaign.crashed-1.json"), load.Notices[1], StringComparison.Ordinal);
            slots.Store(2).ClearResuming();

            // Moved, it is moved: a second run, and a later launch, move nothing and
            // say nothing.
            var settled = Snapshot(folder);
            Assert.Empty(OldAutosave.MoveIntoASlot(slots));
            Assert.Empty(OldAutosave.MoveIntoASlot(new SaveSlots(folder, 3)));
            Assert.Equal(settled, Snapshot(folder));
        });
    }

    [Fact]
    public void WithTheSlotsToTheCountInUseItMovesAboveIt()
    {
        InTempFolder(folder =>
        {
            // Slots 1 to 3, the count, all in use — the folder an S7 build left with the
            // autosave and three manual slots full: the old autosave moves above the
            // count, into slot 4, which is listed beyond it, and loaded, and saves into
            // itself like any slot (SLOT DECISION 1).
            var slots = new SaveSlots(folder, 3);
            Autosaved(slots);
            for (int number = 1; number <= 3; number++)
                Assert.Null(CampaignBoot.Save(slots.Slot(number), Cleared(), savedAt: T2));
            var before = Snapshot(folder);
            Assert.Equal([CampaignBoot.AutosaveMoved(4)], OldAutosave.MoveIntoASlot(slots));
            Assert.Equal(MovedInto(before, 4), Snapshot(folder));
            Assert.Equal([4], slots.Beyond());

            var listing = SaveListing.Read(slots, Now);
            Assert.Equal([1, 2, 3, 4], listing.Slots.Select(slot => slot.Number));
            var four = listing.Slots[3];
            Assert.Equal((SlotState.Readable, true), (four.State, four.Beyond));
            Assert.Same(four, listing.Newest);
            Assert.Equal("BEYOND THE 3 SLOTS", SlotText.Rows(four, 3, GameContent.Current.Dungeons, TimeZoneInfo.Utc)[1]);

            // Loaded, it plays on and saves into slot 4 alone.
            var load = CampaignBoot.Load(slots.Slot(4), GameContent.Current);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 4"], load.Notices);
            slots.Store(4).ClearResuming();
            var others = Snapshot(folder).Where(file => !file.Key.StartsWith("slot-4", StringComparison.Ordinal)).ToList();
            string moved = slots.Store(4).Read()!;
            CampaignRuns.DeepRetreat(load.Campaign!, "spear_hall");
            Assert.Null(CampaignBoot.Save(slots.Slot(4), load.Campaign!, savedAt: Now));
            Assert.Equal((Text(load.Campaign!, Now), moved), (slots.Store(4).Read(), slots.Store(4).ReadBackup()));
            Assert.Equal(others, Snapshot(folder).Where(file => !file.Key.StartsWith("slot-4", StringComparison.Ordinal)).ToList());
        });
    }

    [Fact]
    public void WithNoRoomItStaysAsTheOldAutosave_PlayedInPlace()
    {
        InTempFolder(folder =>
        {
            // Slots 1 to 9 all hold something — saves, a backup alone, a stray marker,
            // a name taken by a folder — so no slot is free: nothing is tried, nothing
            // moves, and nothing is refused. The log says why; there is no word.
            var slots = new SaveSlots(folder, 3);
            Autosaved(slots);
            foreach (int number in new[] { 1, 2, 3, 8, 9 })
                Assert.Null(CampaignBoot.Save(slots.Slot(number), Cleared(), savedAt: T2));
            Put(slots.Store(4), SaveStore.BackupName, Text(Cleared(), T1));
            Put(slots.Store(5), SaveStore.ResumingName, "a stray marker");
            Put(slots.Store(6), SaveStore.UnsavedName, "a stray marker");
            Directory.CreateDirectory(Path.Combine(slots.Store(7).Folder, SaveStore.FileName));
            var root = slots.Store(SaveSlots.OldAutosave);
            var before = Snapshot(folder);
            var log = new List<string>();
            Assert.Empty(OldAutosave.MoveIntoASlot(slots, log.Add));
            Assert.Equal(before, Snapshot(folder));
            Assert.Contains($"[Save] The autosave has no empty slot to move into (slots 1 to 9 all hold something) - it stays in {root.Folder}, listed as OLD AUTOSAVE", log);
            Assert.DoesNotContain(log, line => line.Contains(" moved into ", StringComparison.Ordinal));
            Assert.Null(root.WritesRefused);
            Assert.False(root.WasMoving);

            // It is the old autosave, first on the list, marked as not yet moved.
            var listing = SaveListing.Read(slots, Now);
            var old = listing.Slots[0];
            Assert.Equal((SaveSlots.OldAutosave, SlotState.Readable), (old.Number, old.State));
            Assert.Equal(["OLD AUTOSAVE - BETWEEN RUNS - 0 RUNS - 2026-09-25 11:00", "NOT YET MOVED INTO A SLOT"], SlotText.Rows(old, 3, GameContent.Current.Dungeons, TimeZoneInfo.Utc));

            // And it is played in place: it loads, and saves back into the save folder,
            // every slot untouched.
            var load = CampaignBoot.Load(slots.Slot(SaveSlots.OldAutosave), GameContent.Current);
            Assert.Equal(["CAMPAIGN RESUMED FROM THE OLD AUTOSAVE"], load.Notices);
            root.ClearResuming();
            string saved = root.Read()!;
            CampaignRuns.DeepRetreat(load.Campaign!, "spear_hall");
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.OldAutosave), load.Campaign!, savedAt: Now));
            Assert.Equal((Text(load.Campaign!, Now), saved), (root.Read(), root.ReadBackup()));
            Assert.Equal(before.Where(file => file.Key.StartsWith("slot-", StringComparison.Ordinal)),
                Snapshot(folder).Where(file => file.Key.StartsWith("slot-", StringComparison.Ordinal)));
        });
    }

    [Fact]
    public void ASlotWithAStrayMarkerOrAFolderInTheWayIsSkipped_NeverCleared()
    {
        InTempFolder(folder =>
        {
            // Slot 1 holds a stray unsaved-quit marker, and slot 2 a folder named
            // campaign.json: neither is free — the marker would speak of a campaign
            // not its own, and the folder would stop the save's move — so the move
            // goes into slot 3, and both are left as they were. The listing calls
            // both empty, as nothing of them loads; the move sees more than it does.
            var slots = new SaveSlots(folder, 3);
            Autosaved(slots);
            Put(slots.Store(1), SaveStore.UnsavedName, "a stray marker");
            string inTheWay = Path.Combine(slots.Store(2).Folder, SaveStore.FileName);
            Directory.CreateDirectory(inTheWay);
            var before = Snapshot(folder);
            Assert.Equal([SlotState.Empty, SlotState.Empty, SlotState.Empty], SaveListing.Read(slots, Now).Slots.Skip(1).Select(slot => slot.State));
            Assert.Equal([CampaignBoot.AutosaveMoved(3)], OldAutosave.MoveIntoASlot(slots));
            Assert.Equal(MovedInto(before, 3), Snapshot(folder));
            Assert.Equal("a stray marker", File.ReadAllText(slots.Store(1).UnsavedPath));
            Assert.True(Directory.Exists(inTheWay));

            // A file where a slot's folder goes is in the way too, and stays.
            string other = Path.Combine(folder, "other");
            var otherSlots = new SaveSlots(other, 3);
            Autosaved(otherSlots);
            File.WriteAllText(otherSlots.Store(1).Folder, "a file named slot-1");
            var otherBefore = Snapshot(other);
            Assert.Equal([CampaignBoot.AutosaveMoved(2)], OldAutosave.MoveIntoASlot(otherSlots));
            Assert.Equal(MovedInto(otherBefore, 2), Snapshot(other));
            Assert.Equal("a file named slot-1", File.ReadAllText(otherSlots.Store(1).Folder));
        });
    }

    [Fact]
    public void AMoveCutOffByACrashFinishesIntoTheSameSlot()
    {
        InTempFolder(folder =>
        {
            // A crash cut the move into slot 3 off after the save had moved: the save
            // folder still holds the backup and both markers, and the marker of the
            // move names 3. The launch finishes it there — though slot 1 is free —
            // and the listing, read after, sees one campaign, in slot 3.
            var slots = new SaveSlots(Path.Combine(folder, "cut"), 3);
            var root = slots.Store(SaveSlots.OldAutosave);
            Autosaved(slots);
            Put(root, SaveStore.ResumingName, "A resume is in flight.");
            Put(root, SaveStore.UnsavedName, "Leaving play could not save the campaign in this folder: the disk is full");
            var whole = Snapshot(slots.Folder);
            Directory.CreateDirectory(slots.Store(3).Folder);
            File.Move(root.SavePath, slots.Store(3).SavePath);
            File.WriteAllText(root.MovingPath, "3");
            var log = new List<string>();
            Assert.Equal([CampaignBoot.AutosaveMoved(3)], OldAutosave.MoveIntoASlot(slots, log.Add));
            Assert.Equal(MovedInto(whole, 3), Snapshot(slots.Folder));
            Assert.False(root.WasMoving);
            Assert.Contains(log, line => line.StartsWith($"[Save] A move of the autosave into {slots.Store(3).Folder} was cut off: finishing it", StringComparison.Ordinal));
            var listing = SaveListing.Read(slots, Now);
            Assert.Equal([1, 2, 3], listing.Slots.Select(slot => slot.Number));
            Assert.Equal((3, true), (listing.Newest!.Number, listing.Newest.Unfinished));

            // The crash came after both files had moved: only the markers are left.
            // The quit's goes with them, since it speaks of their campaign; the resume
            // marker, with no save beside it to guard, is left and logged — and so is
            // it at every launch after.
            var late = new SaveSlots(Path.Combine(folder, "late"), 3);
            var lateRoot = late.Store(SaveSlots.OldAutosave);
            Autosaved(late);
            Directory.CreateDirectory(late.Store(2).Folder);
            File.Move(lateRoot.SavePath, late.Store(2).SavePath);
            File.Move(lateRoot.BackupPath, late.Store(2).BackupPath);
            Put(lateRoot, SaveStore.ResumingName, "A resume is in flight.");
            Put(lateRoot, SaveStore.UnsavedName, "Leaving play could not save the campaign in this folder: the disk is full");
            File.WriteAllText(lateRoot.MovingPath, "2");
            log.Clear();
            Assert.Equal([CampaignBoot.AutosaveMoved(2)], OldAutosave.MoveIntoASlot(late, log.Add));
            Assert.Equal([SaveStore.ResumingName], Files(late.Folder));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName, SaveStore.UnsavedName], Files(late.Store(2).Folder));
            Assert.Contains($"[Save] {lateRoot.ResumingPath} stands alone, beside no save or backup - it guards nothing, and is left where it is", log);
            Assert.Equal([CampaignBoot.QuitUnsaved(late.Slot(2))], CampaignBoot.TakeEveryUnsaved(late));
            Assert.Empty(OldAutosave.MoveIntoASlot(late));
            Assert.Equal([SaveStore.ResumingName], Files(late.Folder));

            // A stale marker: it names slot 3, but slot 3 has taken the save's name
            // since — a build without the move ran in between and saved there. It is
            // dropped, and the move goes afresh into the lowest free slot; slot 3 is
            // left as it was.
            var stale = new SaveSlots(Path.Combine(folder, "stale"), 3);
            var staleRoot = stale.Store(SaveSlots.OldAutosave);
            Autosaved(stale);
            Assert.Null(CampaignBoot.Save(stale.Slot(3), Cleared(), savedAt: T2));
            File.WriteAllText(staleRoot.MovingPath, "3");
            var staleBefore = Snapshot(stale.Folder);
            staleBefore.Remove(SaveStore.MovingName);
            log.Clear();
            Assert.Equal([CampaignBoot.AutosaveMoved(1)], OldAutosave.MoveIntoASlot(stale, log.Add));
            Assert.Equal(MovedInto(staleBefore, 1), Snapshot(stale.Folder));
            Assert.Contains(log, line => line.Contains("the marker is stale, dropped, and a slot is picked afresh", StringComparison.Ordinal));

            // A garbage marker names no slot: it is dropped, and the log names what
            // the save folder holds and every slot holding a lone save or a lone
            // backup — the halves a person could join by hand — before the move goes
            // afresh.
            var garbage = new SaveSlots(Path.Combine(folder, "garbage"), 3);
            var garbageRoot = garbage.Store(SaveSlots.OldAutosave);
            Autosaved(garbage);
            Put(garbage.Store(2), SaveStore.BackupName, Text(NewCampaign(), T1));
            Assert.Null(CampaignBoot.Save(garbage.Slot(5), Cleared(), savedAt: T2));
            File.WriteAllText(garbageRoot.MovingPath, "slot three");
            log.Clear();
            Assert.Equal([CampaignBoot.AutosaveMoved(1)], OldAutosave.MoveIntoASlot(garbage, log.Add));
            string dropped = Assert.Single(log, line => line.StartsWith($"[Save] {garbageRoot.MovingPath} names no slot", StringComparison.Ordinal));
            Assert.Contains($"holds {SaveStore.FileName}, {SaveStore.BackupName};", dropped, StringComparison.Ordinal);
            Assert.Contains($"{garbage.Store(2).Folder}, {garbage.Store(5).Folder}", dropped, StringComparison.Ordinal);
            Assert.False(garbageRoot.WasMoving);
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName], Files(garbage.Store(1).Folder));

            // A crash that came after the marker and before any file moved: it names
            // slot 2, which holds nothing of the campaign — and where a file named
            // slot-2 stands now, so the finish cannot reach it either. The save folder
            // is whole, so there is no split to keep: the finish is a move that
            // stopped, the marker is dropped, and the move goes afresh, past slot 1's
            // save and the file, into slot 3. Nothing is refused.
            var early = new SaveSlots(Path.Combine(folder, "early"), 3);
            var earlyRoot = early.Store(SaveSlots.OldAutosave);
            Autosaved(early);
            Assert.Null(CampaignBoot.Save(early.Slot(1), Cleared(), savedAt: T2));
            File.WriteAllText(early.Store(2).Folder, "a file named slot-2");
            File.WriteAllText(earlyRoot.MovingPath, "2");
            var earlyBefore = Snapshot(early.Folder);
            earlyBefore.Remove(SaveStore.MovingName);
            log.Clear();
            Assert.Equal([CampaignBoot.AutosaveMoved(3)], OldAutosave.MoveIntoASlot(early, log.Add));
            Assert.Equal(MovedInto(earlyBefore, 3), Snapshot(early.Folder));
            Assert.Contains(log, line => line.StartsWith($"[Save] The move into {early.Store(2).Folder} that a crash cut off had moved nothing there", StringComparison.Ordinal));
            Assert.False(earlyRoot.WasMoving);
            Assert.Null(earlyRoot.WritesRefused);
            Assert.Null(early.Store(2).WritesRefused);
        });
    }

    [Fact]
    public void AFileInTheWayIsUndone_AndTheNextSlotTried()
    {
        InTempFolder(folder =>
        {
            // Between the test of slot 1 and the move of the backup into it, a file
            // takes the backup's name there — the log line of the save's move is the
            // hook. The try is undone — the save goes back, nothing is deleted — and
            // the move goes into slot 2, the file in slot 1 left untouched. The marker
            // named slot 1 on the disk before the save moved there, so a crash in the
            // try would have been finished into the slot it left a half in.
            var slots = new SaveSlots(Path.Combine(folder, "planted"), 3);
            var root = slots.Store(SaveSlots.OldAutosave);
            Autosaved(slots);
            Put(root, SaveStore.ResumingName, "A resume is in flight.");
            var before = Snapshot(slots.Folder);
            var slot1 = slots.Store(1);
            var log = new List<string>();
            int? marked = null;
            void Hook(string line)
            {
                log.Add(line);
                if (line == $"[Save] {root.SavePath} moved into {slot1.Folder}")
                {
                    marked = root.ReadMoving();
                    Put(slot1, SaveStore.BackupName, "planted");
                }
            }
            Assert.Equal([CampaignBoot.AutosaveMoved(2)], OldAutosave.MoveIntoASlot(slots, Hook));
            Assert.Equal(1, marked);
            var expected = MovedInto(before, 2);
            expected[Path.Combine("slot-1", SaveStore.BackupName)] = Convert.ToBase64String("planted"u8.ToArray());
            Assert.Equal(expected, Snapshot(slots.Folder));
            Assert.Contains(log, line => line == $"[Save] {slot1.SavePath} moved back into {root.Folder}");
            Assert.Contains(log, line => line.StartsWith($"[Save] The move into {slot1.Folder} was undone", StringComparison.Ordinal));
            Assert.False(root.WasMoving);
            Assert.Null(root.WritesRefused);

            // Windows only: the save folder's own save held by another program, which
            // shares nothing. The save moves first, so every free slot's try fails
            // before anything has moved: nothing moves, no slot gains a row, no marker
            // is left, and the save folder takes no saves this session — listed as the
            // old autosave, it loads nothing and writes nothing, and says why.
            if (!OperatingSystem.IsWindows())
                return;
            var held = new SaveSlots(Path.Combine(folder, "held"), 3);
            var heldRoot = held.Store(SaveSlots.OldAutosave);
            Autosaved(held);
            var heldBefore = Snapshot(held.Folder);
            IReadOnlyList<string> said;
            using (new FileStream(heldRoot.SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                said = OldAutosave.MoveIntoASlot(held);
                Assert.Equal(["THE AUTOSAVE COULD NOT MOVE INTO A SLOT (ANOTHER PROGRAM HAS IT OPEN) - IT STAYS AS THE OLD AUTOSAVE AND TAKES NO SAVES THIS SESSION - LAUNCH THE GAME AGAIN TO MOVE IT"], said);
                Assert.Equal("campaign.json could not be moved into a slot (another program has it open)", heldRoot.WritesRefused);
                var load = CampaignBoot.Load(held.Slot(SaveSlots.OldAutosave), GameContent.Current);
                Assert.False(load.Loaded);
                Assert.Equal(["NOTHING COULD BE LOADED FROM THE OLD AUTOSAVE",
                    "THE OLD AUTOSAVE TAKES NO SAVES THIS SESSION: EARLIER, CAMPAIGN.JSON COULD NOT BE MOVED INTO A SLOT (ANOTHER PROGRAM HAS IT OPEN) - LAUNCH THE GAME AGAIN TO PLAY IN IT"],
                    CampaignBoot.NothingLoadedWord(load, held.Slot(SaveSlots.OldAutosave)));
            }
            Assert.Equal(heldBefore, Snapshot(held.Folder));
            Assert.False(heldRoot.WasMoving);
            var listing = SaveListing.Read(held, Now);
            Assert.Equal([0, 1, 2, 3], listing.Slots.Select(slot => slot.Number));
            Assert.All(listing.Slots.Skip(1), slot => Assert.Equal(SlotState.Empty, slot.State));
            Assert.Empty(held.Beyond());
            Assert.StartsWith("THE OLD AUTOSAVE COULD NOT BE SAVED (CAMPAIGN.JSON COULD NOT BE MOVED INTO A SLOT", CampaignBoot.Save(held.Slot(SaveSlots.OldAutosave), Cleared(), savedAt: Now), StringComparison.Ordinal);
            Assert.False(CampaignBoot.Load(held.Slot(SaveSlots.OldAutosave), GameContent.Current).Loaded);   // released, still refused this session
            Assert.Equal(heldBefore, Snapshot(held.Folder));

            // Released, the next launch moves it.
            var relaunched = new SaveSlots(held.Folder, 3);
            Assert.Equal([CampaignBoot.AutosaveMoved(1)], OldAutosave.MoveIntoASlot(relaunched));
            Assert.Equal(MovedInto(heldBefore, 1), Snapshot(held.Folder));
            Assert.Null(relaunched.Store(SaveSlots.OldAutosave).WritesRefused);
        });
    }

    [Fact]
    public void ASplitThatCannotBeUndoneLoadsNeitherHalf()
    {
        // Windows only: a half held by another program is what keeps a move from
        // being finished or undone.
        if (!OperatingSystem.IsWindows())
            return;
        InTempFolder(folder =>
        {
            // A crash cut the move into slot 3 off after the save had moved, and the
            // backup still in the save folder is held by another program, so the
            // finish stops at it. The campaign stands in two halves: neither takes
            // saves this session, so neither loads and neither is written, and the
            // marker stays for the next launch.
            var slots = new SaveSlots(folder, 3);
            var root = slots.Store(SaveSlots.OldAutosave);
            Autosaved(slots);
            Put(root, SaveStore.ResumingName, "A resume is in flight.");
            var whole = Snapshot(folder);
            Directory.CreateDirectory(slots.Store(3).Folder);
            File.Move(root.SavePath, slots.Store(3).SavePath);
            File.WriteAllText(root.MovingPath, "3");
            var split = Snapshot(folder);
            using (new FileStream(root.BackupPath, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Equal(["THE AUTOSAVE IS HALF MOVED INTO SLOT 3 (ANOTHER PROGRAM HAS IT OPEN) - NEITHER LOADS THIS SESSION - LAUNCH THE GAME AGAIN TO FINISH IT"],
                    OldAutosave.MoveIntoASlot(slots));
            Assert.Equal(split, Snapshot(folder));
            Assert.True(root.WasMoving);
            const string refused = "the autosave was left half moved into slot 3 (another program has it open)";
            Assert.Equal((refused, refused), (root.WritesRefused, slots.Store(3).WritesRefused));
            foreach (int number in new[] { SaveSlots.OldAutosave, 3 })
            {
                var slot = slots.Slot(number);
                var load = CampaignBoot.Load(slot, GameContent.Current);
                Assert.Equal([CampaignBoot.TakesNoSaves(slot, refused)], load.Notices);
                Assert.False(load.Loaded);
                Assert.Equal(CampaignBoot.NotSaved(slot, refused), CampaignBoot.Save(slot, Cleared(), savedAt: Now));
                Assert.Equal(CampaignBoot.NotSaved(slot, refused), CampaignBoot.Save(slot, Cleared(), savedAt: Now, byHand: true));
            }
            Assert.Equal(split, Snapshot(folder));

            // The next launch, the backup let go, finishes the move into slot 3.
            var relaunched = new SaveSlots(folder, 3);
            Assert.Equal([CampaignBoot.AutosaveMoved(3)], OldAutosave.MoveIntoASlot(relaunched));
            Assert.Equal(MovedInto(whole, 3), Snapshot(folder));
            Assert.False(root.WasMoving);

            // A fresh move can be left in two halves too: the save has moved into slot
            // 1 when another program takes hold of both it and the backup still in the
            // save folder — the log line of the save's move is the hook — so the
            // backup will not follow and the save will not go back.
            string fresh = Path.Combine(folder, "fresh");
            var freshSlots = new SaveSlots(fresh, 3);
            var freshRoot = freshSlots.Store(SaveSlots.OldAutosave);
            Autosaved(freshSlots);
            var freshWhole = Snapshot(fresh);
            var holds = new List<FileStream>();
            void Hook(string line)
            {
                if (line == $"[Save] {freshRoot.SavePath} moved into {freshSlots.Store(1).Folder}")
                {
                    holds.Add(new FileStream(freshSlots.Store(1).SavePath, FileMode.Open, FileAccess.Read, FileShare.None));
                    holds.Add(new FileStream(freshRoot.BackupPath, FileMode.Open, FileAccess.Read, FileShare.None));
                }
            }
            try
            {
                Assert.Equal([CampaignBoot.AutosaveSplit(1, "another program has it open")], OldAutosave.MoveIntoASlot(freshSlots, Hook));
            }
            finally
            {
                holds.ForEach(held => held.Dispose());
            }
            Assert.True(freshRoot.WasMoving);
            Assert.NotNull(freshRoot.WritesRefused);
            Assert.NotNull(freshSlots.Store(1).WritesRefused);
            Assert.False(CampaignBoot.Load(freshSlots.Slot(1), GameContent.Current).Loaded);
            Assert.False(CampaignBoot.Load(freshSlots.Slot(SaveSlots.OldAutosave), GameContent.Current).Loaded);
            Assert.Equal([CampaignBoot.AutosaveMoved(1)], OldAutosave.MoveIntoASlot(new SaveSlots(fresh, 3)));
            Assert.Equal(MovedInto(freshWhole, 1), Snapshot(fresh));

            // A crash that came after the marker and before any file moved, and the
            // save folder's own save held at the next launch: slot 3 holds nothing of
            // the campaign, so there is no split to keep. The finish is a move that
            // stopped, and goes afresh: every slot's try fails at the held save, the
            // marker is dropped, and the save folder alone takes no saves this
            // session — no slot is refused, nothing moved.
            string early = Path.Combine(folder, "early");
            var earlySlots = new SaveSlots(early, 3);
            var earlyRoot = earlySlots.Store(SaveSlots.OldAutosave);
            Autosaved(earlySlots);
            var earlyWhole = Snapshot(early);
            File.WriteAllText(earlyRoot.MovingPath, "3");
            using (new FileStream(earlyRoot.SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Equal([CampaignBoot.AutosaveNotMoved("another program has it open")], OldAutosave.MoveIntoASlot(earlySlots));
            Assert.Equal(earlyWhole, Snapshot(early));
            Assert.False(earlyRoot.WasMoving);
            Assert.NotNull(earlyRoot.WritesRefused);
            Assert.All(Enumerable.Range(1, SaveSlots.Most), number => Assert.Null(earlySlots.Store(number).WritesRefused));
        });
    }

    [Fact]
    public void AMarkerThatFailsStopsTheMove_TheSaveFolderTakingNoSaves()
    {
        InTempFolder(folder =>
        {
            // A folder named campaign.moving stands in the save folder, so the marker
            // of the move cannot be written: nothing moves — no slot's folder is even
            // made — and the save folder takes no saves this session, its refusal
            // saying it was the marker that failed, and what the marker is, never
            // that a file of the campaign's would not move. The folder is left; once
            // it is gone, the next launch moves the autosave.
            var slots = new SaveSlots(Path.Combine(folder, "unwritten"), 3);
            var root = slots.Store(SaveSlots.OldAutosave);
            Autosaved(slots);
            Directory.CreateDirectory(root.MovingPath);
            var before = Snapshot(slots.Folder);
            var said = OldAutosave.MoveIntoASlot(slots);
            const string unwritten = "campaign.moving, the marker of its move, could not be written (";
            string refused = root.WritesRefused!;
            Assert.StartsWith(unwritten, refused, StringComparison.Ordinal);
            string why = refused[unwritten.Length..^1];
            if (OperatingSystem.IsWindows())
                Assert.Equal("no permission to write there", why);
            Assert.Equal([CampaignBoot.AutosaveNotMoved(why)], said);
            Assert.Equal(before, Snapshot(slots.Folder));
            Assert.True(Directory.Exists(root.MovingPath));
            Assert.All(Enumerable.Range(1, SaveSlots.Most), number => Assert.False(Directory.Exists(slots.Store(number).Folder)));
            var zero = slots.Slot(SaveSlots.OldAutosave);
            var load = CampaignBoot.Load(zero, GameContent.Current);
            Assert.False(load.Loaded);
            Assert.Equal(["NOTHING COULD BE LOADED FROM THE OLD AUTOSAVE",
                $"THE OLD AUTOSAVE TAKES NO SAVES THIS SESSION: EARLIER, CAMPAIGN.MOVING, THE MARKER OF ITS MOVE, COULD NOT BE WRITTEN ({why.ToUpperInvariant()}) - LAUNCH THE GAME AGAIN TO PLAY IN IT"],
                CampaignBoot.NothingLoadedWord(load, zero));
            Assert.Equal(before, Snapshot(slots.Folder));
            Directory.Delete(root.MovingPath);
            Assert.Equal([CampaignBoot.AutosaveMoved(1)], OldAutosave.MoveIntoASlot(new SaveSlots(slots.Folder, 3)));
            Assert.Equal(MovedInto(before, 1), Snapshot(slots.Folder));

            // Windows only from here: a marker held by another program.
            if (!OperatingSystem.IsWindows())
                return;

            // A crash left the move into slot 3 in two halves — the save there, the
            // backup and the resume marker here — and another program holds the
            // marker, sharing it for deleting alone, so it cannot be read. That is no
            // marker naming no slot: it may name the other half, so it is kept and
            // nothing moves — moved afresh, the backup would go into slot 1, and the
            // campaign stand split across two slots with nothing left to join it. The
            // save folder takes no saves this session, and the log names the lone
            // halves. Let go, the next launch finishes the move into slot 3.
            var split = new SaveSlots(Path.Combine(folder, "unread"), 3);
            var splitRoot = split.Store(SaveSlots.OldAutosave);
            Autosaved(split);
            Put(splitRoot, SaveStore.ResumingName, "A resume is in flight.");
            var whole = Snapshot(split.Folder);
            Directory.CreateDirectory(split.Store(3).Folder);
            File.Move(splitRoot.SavePath, split.Store(3).SavePath);
            File.WriteAllText(splitRoot.MovingPath, "3");
            var halves = Snapshot(split.Folder);
            var log = new List<string>();
            using (new FileStream(splitRoot.MovingPath, FileMode.Open, FileAccess.Read, FileShare.Delete))
                Assert.Equal([CampaignBoot.AutosaveNotMoved("another program has it open")], OldAutosave.MoveIntoASlot(split, log.Add));
            Assert.Equal(halves, Snapshot(split.Folder));
            Assert.Equal("campaign.moving, the marker of its move, could not be read (another program has it open)", splitRoot.WritesRefused);
            string unread = Assert.Single(log, line => line.StartsWith($"[Save] {splitRoot.MovingPath} could not be read", StringComparison.Ordinal));
            Assert.Contains($"a lone save or backup stands in {split.Store(3).Folder}:", unread, StringComparison.Ordinal);
            Assert.Equal(["THE OLD AUTOSAVE TAKES NO SAVES THIS SESSION: EARLIER, CAMPAIGN.MOVING, THE MARKER OF ITS MOVE, COULD NOT BE READ (ANOTHER PROGRAM HAS IT OPEN) - LAUNCH THE GAME AGAIN TO PLAY IN IT"],
                CampaignBoot.Load(split.Slot(SaveSlots.OldAutosave), GameContent.Current).Notices);
            Assert.Equal(halves, Snapshot(split.Folder));
            Assert.Equal([CampaignBoot.AutosaveMoved(3)], OldAutosave.MoveIntoASlot(new SaveSlots(split.Folder, 3)));
            Assert.Equal(MovedInto(whole, 3), Snapshot(split.Folder));

            // With the save and the backup moved before the crash, a marker that
            // cannot be read has nothing left here to split: it is kept all the same,
            // and nothing is refused or said. Let go, the next launch finishes.
            var late = new SaveSlots(Path.Combine(folder, "late"), 3);
            var lateRoot = late.Store(SaveSlots.OldAutosave);
            Autosaved(late);
            Directory.CreateDirectory(late.Store(2).Folder);
            File.Move(lateRoot.SavePath, late.Store(2).SavePath);
            File.Move(lateRoot.BackupPath, late.Store(2).BackupPath);
            File.WriteAllText(lateRoot.MovingPath, "2");
            var lateBefore = Snapshot(late.Folder);
            using (new FileStream(lateRoot.MovingPath, FileMode.Open, FileAccess.Read, FileShare.Delete))
                Assert.Empty(OldAutosave.MoveIntoASlot(late));
            Assert.Equal(lateBefore, Snapshot(late.Folder));
            Assert.Null(lateRoot.WritesRefused);
            Assert.Equal([CampaignBoot.AutosaveMoved(2)], OldAutosave.MoveIntoASlot(new SaveSlots(late.Folder, 3)));
            Assert.False(lateRoot.WasMoving);

            // A marker naming no slot, held by another program sharing it for reading
            // alone: it reads, but cannot be dropped, and no fresh marker could be
            // written over it, so nothing moves, and the save folder takes no saves
            // this session. Let go, the next launch drops it and moves the autosave.
            var kept = new SaveSlots(Path.Combine(folder, "undropped"), 3);
            var keptRoot = kept.Store(SaveSlots.OldAutosave);
            Autosaved(kept);
            File.WriteAllText(keptRoot.MovingPath, "slot three");
            var keptBefore = Snapshot(kept.Folder);
            using (new FileStream(keptRoot.MovingPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Equal([CampaignBoot.AutosaveNotMoved("another program has it open")], OldAutosave.MoveIntoASlot(kept));
            Assert.Equal(keptBefore, Snapshot(kept.Folder));
            Assert.Equal("campaign.moving, the marker of its move, could not be cleared (another program has it open)", keptRoot.WritesRefused);
            keptBefore.Remove(SaveStore.MovingName);
            Assert.Equal([CampaignBoot.AutosaveMoved(1)], OldAutosave.MoveIntoASlot(new SaveSlots(kept.Folder, 3)));
            Assert.Equal(MovedInto(keptBefore, 1), Snapshot(kept.Folder));

            // A try undone — a file planted in slot 1 as the save moved there, as in
            // AFileInTheWayIsUndone_AndTheNextSlotTried — whose marker another program
            // then holds, sharing it for reading alone, so it cannot be cleared: the
            // save folder is whole again, and takes no saves this session, its
            // refusal saying which file stopped the try and that the marker stayed.
            // Let go, the next launch finds the marker stale — slot 1 has the
            // backup's name — drops it, and moves the autosave into slot 2.
            var stuck = new SaveSlots(Path.Combine(folder, "uncleared"), 3);
            var stuckRoot = stuck.Store(SaveSlots.OldAutosave);
            Autosaved(stuck);
            var stuckBefore = Snapshot(stuck.Folder);
            var one = stuck.Store(1);
            var holds = new List<FileStream>();
            void Hook(string line)
            {
                if (line == $"[Save] {stuckRoot.SavePath} moved into {one.Folder}")
                    Put(one, SaveStore.BackupName, "planted");
                else if (line.StartsWith($"[Save] {stuckRoot.BackupPath} could not be moved into {one.Folder}", StringComparison.Ordinal))
                    holds.Add(new FileStream(stuckRoot.MovingPath, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            try
            {
                Assert.Equal([CampaignBoot.AutosaveNotMoved("another program has it open")], OldAutosave.MoveIntoASlot(stuck, Hook));
            }
            finally
            {
                holds.ForEach(held => held.Dispose());
            }
            Assert.Single(holds);
            Assert.Equal("campaign.moving, the marker of its move, could not be cleared after campaign.bak.json would not move (another program has it open)", stuckRoot.WritesRefused);
            string planted = Convert.ToBase64String("planted"u8.ToArray());
            var undone = new Dictionary<string, string>(stuckBefore, StringComparer.Ordinal)
            {
                [SaveStore.MovingName] = Convert.ToBase64String("1"u8.ToArray()),
                [Path.Combine("slot-1", SaveStore.BackupName)] = planted,
            };
            Assert.Equal(undone, Snapshot(stuck.Folder));
            Assert.Equal([CampaignBoot.AutosaveMoved(2)], OldAutosave.MoveIntoASlot(new SaveSlots(stuck.Folder, 3)));
            var moved = MovedInto(stuckBefore, 2);
            moved[Path.Combine("slot-1", SaveStore.BackupName)] = planted;
            Assert.Equal(moved, Snapshot(stuck.Folder));
        });
    }

    [Fact]
    public void ASaveRenamedBackIntoTheSaveFolderMovesAtTheNextLaunch()
    {
        InTempFolder(folder =>
        {
            // The save folder holds the autosave and a save set aside there once, as
            // campaign.unreadable-1.json. The launch moves the autosave into slot 1;
            // the file set aside stays, as every such file does.
            var slots = new SaveSlots(folder, 3);
            var root = slots.Store(SaveSlots.OldAutosave);
            Autosaved(slots);
            string setAside = Path.Combine(root.Folder, "campaign.unreadable-1.json");
            File.WriteAllText(setAside, Text(Cleared(), T2));
            Assert.Equal([CampaignBoot.AutosaveMoved(1)], OldAutosave.MoveIntoASlot(slots));
            Assert.Equal(["campaign.unreadable-1.json"], Files(folder));

            // Renamed back to campaign.json, as its notice said, it is the old
            // autosave for the rest of the session — listed first, not yet moved, and
            // played in place — since the move runs at launch alone.
            File.Move(setAside, root.SavePath);
            var listing = SaveListing.Read(slots, Now);
            Assert.Equal([0, 1, 2, 3], listing.Slots.Select(slot => slot.Number));
            Assert.Equal(SlotText.NotMovedYet, SlotText.Rows(listing.Slots[0], 3, GameContent.Current.Dungeons, TimeZoneInfo.Utc)[1]);
            var before = Snapshot(folder);

            // The next launch moves it into the lowest free slot, byte for byte.
            Assert.Equal([CampaignBoot.AutosaveMoved(2)], OldAutosave.MoveIntoASlot(new SaveSlots(folder, 3)));
            Assert.Equal(MovedInto(before, 2), Snapshot(folder));
            Assert.Empty(Files(folder));
        });
    }
}
