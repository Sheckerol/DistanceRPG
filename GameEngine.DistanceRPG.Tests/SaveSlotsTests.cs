using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Tests.TestIl;
using static GameEngine.DistanceRPG.Tests.TestSlots;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The save folder's slots and a save's time (the user's ruling of 2026-09-25 on
/// the save model, R2; Phase 5.S5). The save folder is the autosave slot —
/// <c>campaign.json</c> where it always was, so a save written before the slots is
/// the autosave and nothing moves — and <c>slot-n</c> inside it the manual slots,
/// each with its own backup, markers and set-aside files; <c>tuning.json</c>'s
/// <c>SaveSlots</c> says how many, and a slot above a lowered count keeps its
/// save. Every save carries the time it was written, handed in by the edge; the
/// listing reads every slot's header and never moves a file; the most recent
/// save is chosen by its own time, deterministically. A slot carries its own
/// name: every load, save and word takes the slot, its store and its name
/// together. A load is per slot and never makes a campaign, and the campaign it
/// brings back saves into that slot alone (the user's ruling of 2026-09-25 on the
/// save slots, R5), above the count too. No write into any
/// slot rotates away a file that cannot be read, and a write clears its slot's
/// resume marker. A dev-override session never touches a slot. Every case runs
/// on a temporary folder of its own, deleted after, under the numbers the runs
/// were written against (<see cref="Premise"/>), so the content collection holds
/// it.
/// </summary>
[Collection(TestContent.Collection)]
public sealed class SaveSlotsTests : IDisposable
{
    private readonly IDisposable _premise = TestContent.UsePremise();

    public void Dispose() => _premise.Dispose();

    /// <summary>The clock the listings in these tests are read at: the edge's, fixed.</summary>
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Three times before <see cref="Now"/>, oldest first.</summary>
    private static readonly DateTimeOffset T1 = Now.AddHours(-3), T2 = Now.AddHours(-2), T3 = Now.AddHours(-1);

    /// <summary>The HUD's glyphs, upper-cased: what a drawn row can hold — no apostrophe, quote, brace, backslash or semicolon.</summary>
    private const string Font = @"^[A-Z0-9 .,:\-_!?()/<>\[\]+=#*]+$";

    private const BindingFlags Declared = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

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

    /// <summary>A campaign one run on: the tutorial walked out of on its first floor, which counts nothing and leaves a summary.</summary>
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
    private static string Text(CampaignState campaign, DateTimeOffset? at = null) => SaveCodec.Write(SaveGame.Capture(campaign, savedAt: at));

    /// <summary>Save <paramref name="campaign"/> into <paramref name="slot"/> at <paramref name="at"/>, and it saved: a save that goes through says nothing.</summary>
    private static void Saved(SaveSlot slot, CampaignState campaign, DateTimeOffset? at = null) => Assert.Null(CampaignBoot.Save(slot, campaign, savedAt: at));

    /// <summary><paramref name="text"/> as a build before the slots wrote it: no time member at all.</summary>
    private static string BeforeTheSlots(string text)
    {
        string old = text.Replace(",\n  \"savedAt\": null\n}", "\n}", StringComparison.Ordinal);
        Assert.NotEqual(text, old);
        Assert.DoesNotContain("savedAt", old, StringComparison.Ordinal);
        return old;
    }

    /// <summary><paramref name="text"/> stamped with a version newer than this build reads.</summary>
    private static string Newer(string text)
    {
        var tree = JsonNode.Parse(text)!.AsObject();
        tree["version"] = SaveCodec.CurrentVersion + 1;
        return tree.ToJsonString();
    }

    /// <summary>Write <paramref name="text"/> as <paramref name="name"/> in <paramref name="slot"/>'s folder, behind the store's back, as a hand or another build would.</summary>
    private static void Put(SaveStore slot, string name, string text)
    {
        Directory.CreateDirectory(slot.Folder);
        File.WriteAllText(Path.Combine(slot.Folder, name), text);
    }

    /// <summary>The names of the files directly in <paramref name="folder"/>, in ordinal order.</summary>
    private static string[] Files(string folder)
        => Directory.GetFiles(folder).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Every file under <paramref name="folder"/>, by its path inside it, with its bytes: what "nothing moved" is checked against.</summary>
    private static Dictionary<string, string> Snapshot(string folder, Func<string, bool>? keep = null)
        => Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folder, path))
            .Where(path => keep?.Invoke(path) ?? true)
            .ToDictionary(path => path, path => Convert.ToBase64String(File.ReadAllBytes(Path.Combine(folder, path))), StringComparer.Ordinal);

    /// <summary>A set-aside notice: the file, what became of it, its new full path, and how to try it again.</summary>
    private static void SaysSetAside(string notice, string file, string what, string path)
    {
        Assert.Equal(CampaignBoot.SetAsideNotice(file, what, path), notice);
        Assert.True(Path.IsPathFullyQualified(path), path);
        Assert.EndsWith(CampaignBoot.TryAgain, notice, StringComparison.Ordinal);
    }

    /// <summary>Why the codec refuses <paramref name="text"/>, in the notice's few words.</summary>
    private static string WhyOf(string text, SaveStore slot)
        => CampaignBoot.WhyUnreadable(Assert.Throws<SaveFormatException>(() => SaveCodec.Read(text)), slot);

    private static bool Same(MethodBase? a, MethodBase b) => a is not null && a.Module == b.Module && a.MetadataToken == b.MetadataToken;

    [Fact]
    public void TheAutosaveIsTheSaveFolder_EachSlotAFolderInIt()
    {
        InTempFolder(folder =>
        {
            // The autosave slot is the save folder itself; manual slot n its slot-n
            // folder, numbered to SaveSlots' ceiling. Asking for a store makes no
            // folder, and each slot has one store for the session.
            var slots = new SaveSlots(folder, 3);
            Assert.Equal((Path.GetFullPath(folder), 3), (slots.Folder, slots.Count));
            var autosave = slots.Store(SaveSlots.Autosave);
            Assert.Equal(slots.Folder, autosave.Folder);
            Assert.Equal(Path.Combine(slots.Folder, SaveStore.FileName), autosave.SavePath);
            for (int n = 1; n <= SaveSlots.Most; n++)
                Assert.Equal(Path.Combine(slots.Folder, $"slot-{n}"), slots.Store(n).Folder);
            Assert.Same(autosave, slots.Store(SaveSlots.Autosave));
            Assert.Same(slots.Store(2), slots.Store(2));
            Assert.Empty(Directory.GetDirectories(folder));
            Assert.Equal(("AUTOSAVE", "SLOT 1", "SLOT 9"), (SaveSlots.Label(SaveSlots.Autosave), SaveSlots.Label(1), SaveSlots.Label(9)));

            // No slot past the ceiling, and no count outside SaveSlots' clamp.
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.Store(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.Store(SaveSlots.Most + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveSlots(folder, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveSlots(folder, SaveSlots.Most + 1));

            // Slots above the count that hold a save or a backup are beyond it: kept
            // and listed. An empty slot folder holds nothing, and is not.
            var campaign = NewCampaign();
            Saved(slots.Slot(4), campaign, T1);
            Saved(slots.Slot(9), campaign, T2);
            Put(slots.Store(6), SaveStore.BackupName, Text(campaign, T1));
            Directory.CreateDirectory(slots.Store(5).Folder);
            Assert.Equal([4, 6, 9], slots.Beyond());

            // A folder named any other way is no slot, whatever it holds — here the
            // newest save of all: never a store, never listed, never read.
            string[] strays = ["slot-01", "slot-+1", "slot-10", "slot-x", "slot-", "slots-1"];
            string newest = Text(Cleared(), Now);
            foreach (string stray in strays)
                Put(new SaveStore(Path.Combine(folder, stray)), SaveStore.FileName, newest);
            var named = Enumerable.Range(0, SaveSlots.Most + 1).Select(n => Path.GetFileName(slots.Store(n).Folder)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(strays, named.Contains);
            Assert.Equal([4, 6, 9], slots.Beyond());

            var listing = SaveListing.Read(slots, Now);
            Assert.Equal([0, 1, 2, 3, 4, 6, 9], listing.Slots.Select(slot => slot.Number));
            Assert.Equal([false, false, false, false, true, true, true], listing.Slots.Select(slot => slot.Beyond));
            Assert.Equal([SlotState.Empty, SlotState.Empty, SlotState.Empty, SlotState.Empty, SlotState.Readable, SlotState.FromBackup, SlotState.Readable],
                listing.Slots.Select(slot => slot.State));
            Assert.Equal(9, listing.Newest!.Number);   // T2, the latest a slot holds: never a stray's
            Assert.Empty(listing.Notices);
        });
    }

    [Fact]
    public void ASlotNamesItselfInEveryWord()
    {
        InTempFolder(folder =>
        {
            // A slot is its store and its name together (SaveSlot), and the save
            // folder's slots hand each the session's one store for its number: the
            // same store every ask, so a refusal and what the store knows of its
            // files go wherever the slot goes. No number outside 0 to the ceiling,
            // and no slot without a store.
            var slots = new SaveSlots(folder, 3);
            for (int n = SaveSlots.Autosave; n <= SaveSlots.Most; n++)
            {
                var slot = slots.Slot(n);
                Assert.Same(slots.Store(n), slot.Store);
                Assert.Equal((int?)n, slot.Number);
                Assert.Equal(slot, slots.Slot(n));
                Assert.Matches("^[A-Z0-9 ]+$", slot.Name);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.Slot(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.Slot(SaveSlots.Most + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveSlot(slots.Store(1), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveSlot(slots.Store(1), SaveSlots.Most + 1));
            Assert.Throws<ArgumentNullException>(() => new SaveSlot(null!, 1));
            Assert.Empty(Directory.GetDirectories(folder));

            // Its name: the autosave, a manual slot by its number, and a dev
            // override's scratch slot, which has none.
            var scratch = SaveSlot.Scratch(CampaignBoot.StoreFor(folder, new BootRequest("spear_hall")));
            Assert.Null(scratch.Number);
            Assert.Equal(("THE AUTOSAVE", "SLOT 2", "SLOT 9", "THE SCRATCH SLOT", "THE SCRATCH SLOT"),
                (slots.Slot(SaveSlots.Autosave).Name, slots.Slot(2).Name, slots.Slot(9).Name, scratch.Name, Named(null).Name));

            // Every word the saves say of a slot, built from the slot: the autosave's
            // and a manual slot's exactly as they have always read, but for SAVED TO
            // below. The scratch slot is said the autosave's way wherever the words
            // name no slot — a load's and a failed save's — and by its own name where
            // they do.
            Assert.Equal("CAMPAIGN RESUMED", CampaignBoot.Resumed(Named(SaveSlots.Autosave), backup: false));
            Assert.Equal("CAMPAIGN RESUMED FROM ITS BACKUP, ONE SAVE OLDER", CampaignBoot.Resumed(Named(SaveSlots.Autosave), backup: true));
            Assert.Equal("CAMPAIGN RESUMED FROM SLOT 2", CampaignBoot.Resumed(Named(2), backup: false));
            Assert.Equal("CAMPAIGN RESUMED FROM THE BACKUP OF SLOT 2, ONE SAVE OLDER", CampaignBoot.Resumed(Named(2), backup: true));
            foreach (bool backup in new[] { false, true })
                Assert.Equal(CampaignBoot.Resumed(Named(SaveSlots.Autosave), backup), CampaignBoot.Resumed(scratch, backup));

            Assert.Equal("NOTHING COULD BE LOADED FROM THE AUTOSAVE", CampaignBoot.NothingLoaded(Named(SaveSlots.Autosave)));
            Assert.Equal("NOTHING COULD BE LOADED FROM SLOT 2", CampaignBoot.NothingLoaded(Named(2)));
            Assert.Equal("NOTHING COULD BE LOADED FROM THE SCRATCH SLOT", CampaignBoot.NothingLoaded(scratch));
            var nothing = new SlotLoad(null, null, ["WHAT THE LOAD SET ASIDE"]);
            Assert.Equal(["NOTHING COULD BE LOADED FROM SLOT 7", "WHAT THE LOAD SET ASIDE"], CampaignBoot.NothingLoadedWord(nothing, Named(7)));

            Assert.Equal("THE CAMPAIGN COULD NOT BE SAVED (THE DISK IS FULL) - THE LAST SAVE IS KEPT - SEE THE LOG",
                CampaignBoot.NotSaved(Named(SaveSlots.Autosave), "the disk is full"));
            Assert.Equal("SLOT 2 COULD NOT BE SAVED (ANOTHER PROGRAM HAS IT OPEN) - WHAT IT HELD IS KEPT - SEE THE LOG",
                CampaignBoot.NotSaved(Named(2), "another program has it open"));
            Assert.Equal(CampaignBoot.NotSaved(Named(SaveSlots.Autosave), "the disk is full"), CampaignBoot.NotSaved(scratch, "the disk is full"));

            // A save by hand names the slot it went into as every word names it: THE
            // AUTOSAVE and THE SCRATCH SLOT too, where the autosave's once read SAVED
            // TO AUTOSAVE, the list's label. No player sees either yet: a save by hand
            // into them is refused outright (below).
            Assert.Equal(("SAVED TO SLOT 1", "SAVED TO SLOT 9"), (CampaignBoot.SavedTo(Named(1)), CampaignBoot.SavedTo(Named(9))));
            Assert.Equal(("SAVED TO THE AUTOSAVE", "SAVED TO THE SCRATCH SLOT"), (CampaignBoot.SavedTo(Named(SaveSlots.Autosave)), CampaignBoot.SavedTo(scratch)));

            // The name goes where the slot goes: one store refusing its writes, handed
            // in as slot 3 and as the autosave, fails in each one's words — and
            // touches no file either way. A save by hand is refused outright into the
            // autosave and the scratch slot, which are written by the game alone.
            var refusing = slots.Store(3);
            refusing.RefuseWrites("campaign.json is in the way");
            var campaign = Retreated();
            Assert.Equal(CampaignBoot.NotSaved(slots.Slot(3), "campaign.json is in the way"), CampaignBoot.Save(slots.Slot(3), campaign, savedAt: Now));
            Assert.StartsWith("SLOT 3 COULD NOT BE SAVED (CAMPAIGN.JSON IS IN THE WAY)", CampaignBoot.Save(slots.Slot(3), campaign), StringComparison.Ordinal);
            Assert.Equal(CampaignBoot.NotSaved(Named(SaveSlots.Autosave), "campaign.json is in the way"), CampaignBoot.Save(AsAutosave(refusing), campaign));
            Assert.Throws<ArgumentOutOfRangeException>(() => CampaignBoot.SaveTo(slots.Slot(SaveSlots.Autosave), campaign, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => CampaignBoot.SaveTo(scratch, campaign, null));
            Assert.Empty(Directory.GetFileSystemEntries(folder));
        });
    }

    [Fact]
    public void ASaveFromBeforeTheSlotsIsTheAutosave()
    {
        InTempFolder(folder =>
        {
            // campaign.json and its backup as a build before the slots wrote them,
            // in the save folder, with no time member at all.
            var campaign = NewCampaign();
            string older = BeforeTheSlots(Text(campaign));
            CampaignRuns.Clear(campaign, "tutorial", CampaignRuns.GoldenSeed);
            string newer = BeforeTheSlots(Text(campaign));
            File.WriteAllText(Path.Combine(folder, SaveStore.BackupName), older);
            File.WriteAllText(Path.Combine(folder, SaveStore.FileName), newer);
            var seeded = Snapshot(folder);

            // The listing finds it where it is: the autosave, a save that reads, no
            // time recorded, nothing unfinished — the most recent save there is,
            // every manual slot empty. Listing it moves nothing.
            var slots = new SaveSlots(folder, 3);
            var listing = SaveListing.Read(slots, Now);
            var autosave = listing.Autosave;
            Assert.Equal((SaveSlots.Autosave, SlotState.Readable), (autosave.Number, autosave.State));
            Assert.Equal((false, false, false), (autosave.Unfinished, autosave.Beyond, autosave.AheadOfClock));
            Assert.Equal(new SlotHeader(null, 1, null, null, null), autosave.Header);
            Assert.Same(autosave, listing.Newest);
            Assert.Equal([SlotState.Empty, SlotState.Empty, SlotState.Empty], listing.Slots.Skip(1).Select(slot => slot.State));
            Assert.Equal(seeded, Snapshot(folder));

            // Loaded, it resumes, saying so, and nothing moved: no migration.
            var store = slots.Store(SaveSlots.Autosave);
            var load = CampaignBoot.Load(slots.Slot(SaveSlots.Autosave), GameContent.Current);
            Assert.True(load.Loaded);
            Assert.Equal([BetweenRunsHud.Resumed], load.Notices);
            StateAssert.Equivalent(campaign, load.Campaign!, "campaign");
            store.ClearResuming();
            Assert.Equal(seeded, Snapshot(folder));

            // Saved again as it stands, whatever the time now, nothing is written: the
            // save keeps the time its state was first written — none — and the backup
            // stays the save before it.
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.Autosave), load.Campaign!, savedAt: Now));
            Assert.Equal(seeded, Snapshot(folder));

            // Played on, it saves as every save does from now on: with its time, the
            // save it was the backup.
            CampaignRuns.DeepRetreat(load.Campaign!, "spear_hall");
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.Autosave), load.Campaign!, savedAt: Now));
            Assert.Equal((Text(load.Campaign!, Now), newer), (store.Read(), store.ReadBackup()));
            Assert.Equal(Now, SaveListing.Read(slots, Now).Autosave.Header!.SavedAt);
        });
    }

    [Fact]
    public void ContinuePicksTheNewest()
    {
        InTempFolder(folder =>
        {
            int case_ = 0;
            SaveSlots Fresh(int count = 3) => new(Path.Combine(folder, $"case-{case_++}"), count);
            var a = NewCampaign();
            var b = Retreated();
            var c = Cleared();

            // The latest time wins, whichever slot holds it — every order of three
            // times across the autosave and slots 1 and 2.
            DateTimeOffset[] times = [T1, T2, T3];
            int[][] orders = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
            foreach (int[] order in orders)
            {
                var slots = Fresh();
                Saved(slots.Slot(0), a, times[order[0]]);
                Saved(slots.Slot(1), b, times[order[1]]);
                Saved(slots.Slot(2), c, times[order[2]]);
                var listing = SaveListing.Read(slots, Now);
                Assert.Equal(Array.IndexOf(order, 2), listing.Newest!.Number);
                Assert.Equal(T3, listing.Newest.Header!.SavedAt);
            }

            // A tie goes to the autosave, then the lower slot — by number, the same
            // whichever order the folders were made in.
            foreach (int[] made in new[] { new[] { 0, 1, 2 }, new[] { 2, 1, 0 } })
            {
                var tied = Fresh();
                foreach (int n in made)
                    Saved(tied.Slot(n), n == 0 ? a : n == 1 ? b : c, T2);
                Assert.Equal(0, SaveListing.Read(tied, Now).Newest!.Number);

                var manual = Fresh();
                foreach (int n in made.Where(n => n != 0))
                    Saved(manual.Slot(n), n == 1 ? b : c, T2);
                Assert.Equal(1, SaveListing.Read(manual, Now).Newest!.Number);
            }

            // A save with no time — one from before the slots — is older than any
            // with one; with none that has one, the tie rule decides.
            var untimed = Fresh();
            Saved(untimed.Slot(1), b);
            Saved(untimed.Slot(2), c, T1);
            Assert.Equal(2, SaveListing.Read(untimed, Now).Newest!.Number);
            Saved(untimed.Slot(2), a);   // another campaign, with no time
            Assert.Equal(1, SaveListing.Read(untimed, Now).Newest!.Number);

            // A save that cannot be read beside a backup that can: the backup's time,
            // since the backup is what a load brings back. Neither reading: never chosen.
            var broken = Fresh();
            Saved(broken.Slot(1), a, T1);
            Saved(broken.Slot(1), b, T3);
            File.WriteAllText(broken.Store(1).SavePath, "not a save");
            Saved(broken.Slot(0), c, T2);
            var fromBackup = SaveListing.Read(broken, Now);
            Assert.Equal((SlotState.FromBackup, (DateTimeOffset?)T1), (fromBackup.Slots[1].State, fromBackup.Slots[1].Header!.SavedAt));
            Assert.Equal(0, fromBackup.Newest!.Number);
            File.WriteAllText(broken.Store(0).SavePath, "not a save either");   // the autosave has no backup: nothing of it reads now
            var autosaveGone = SaveListing.Read(broken, Now);
            Assert.Equal(SlotState.Unreadable, autosaveGone.Autosave.State);
            Assert.Equal(1, autosaveGone.Newest!.Number);
            File.WriteAllText(broken.Store(1).BackupPath, "[1, 2, 3]");
            var neither = SaveListing.Read(broken, Now);
            Assert.Equal(SlotState.Unreadable, neither.Slots[1].State);
            Assert.Null(neither.Slots[1].Header);
            Assert.Null(neither.Newest);

            // A slot beyond the count is chosen when it is the newest.
            var lowered = Fresh();
            Saved(lowered.Slot(0), a, T2);
            Saved(lowered.Slot(5), b, T3);
            var beyond = SaveListing.Read(lowered, Now);
            Assert.True(beyond.Slots.Single(slot => slot.Number == 5).Beyond);
            Assert.Equal(5, beyond.Newest!.Number);

            // A time more than a day ahead of the clock counts as none: a year ahead
            // loses to an hour before, and is listed as ahead of the clock. An hour
            // ahead is inside the day's slack, and wins.
            var ahead = Fresh();
            Saved(ahead.Slot(0), a, Now.AddDays(365));
            Saved(ahead.Slot(1), b, Now.AddHours(-1));
            var skewed = SaveListing.Read(ahead, Now);
            Assert.True(skewed.Autosave.AheadOfClock);
            Assert.Equal(Now.AddDays(365), skewed.Autosave.Header!.SavedAt);   // still listed with its time
            Assert.False(skewed.Slots[1].AheadOfClock);
            Assert.Equal(1, skewed.Newest!.Number);
            Saved(ahead.Slot(2), c, Now.AddHours(1));
            var slack = SaveListing.Read(ahead, Now);
            Assert.False(slack.Slots[2].AheadOfClock);
            Assert.Equal(2, slack.Newest!.Number);
            Assert.True(SaveListing.Read(ahead, Now.AddDays(365)).Slots.All(slot => !slot.AheadOfClock));   // by a clock a year on, it is no longer ahead

            // A folder that is no slot never wins, however new what it holds.
            var stray = Fresh();
            Saved(stray.Slot(0), a, T1);
            Put(new SaveStore(Path.Combine(stray.Folder, "slot-01")), SaveStore.FileName, Text(b, Now));
            var withStray = SaveListing.Read(stray, Now);
            Assert.Equal(0, withStray.Newest!.Number);
            Assert.Equal([0, 1, 2, 3], withStray.Slots.Select(slot => slot.Number));
        });
    }

    [Fact]
    public void ALoadedSlotIsTheOnlyOneItsCampaignWritesInto()
    {
        InTempFolder(folder =>
        {
            // The user's ruling of 2026-09-25 on the save slots (R5): "you choose a save
            // slot and any saving happens on that save slot". Four campaigns — the
            // autosave's and slots 1 to 3, each saved twice, so each has a backup —
            // and slot 2 loaded, as the title loads it. Its campaign's saves, the
            // game's own at every safe point and the quit's, all go into slot 2: the
            // autosave and slots 1 and 3 are byte for byte as they were, and nothing is
            // set aside anywhere.
            var slots = new SaveSlots(folder, 3);
            CampaignState[] campaigns = [NewCampaign(), Retreated(), Cleared(), Retreated()];
            for (int n = SaveSlots.Autosave; n <= 3; n++)
            {
                Saved(slots.Slot(n), n == SaveSlots.Autosave ? Cleared() : NewCampaign(), T1.AddMinutes(n));
                Saved(slots.Slot(n), campaigns[n], T2.AddMinutes(n));
            }
            static bool NotSlot2(string path) => !path.StartsWith("slot-2", StringComparison.Ordinal);
            var others = Snapshot(folder, NotSlot2);
            Assert.Equal(8, Snapshot(folder).Count);

            var load = CampaignBoot.Load(slots.Slot(2), GameContent.Current);
            Assert.True(load.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 2"], load.Notices);
            slots.Store(2).ClearResuming();   // the loaded screen is up
            var played = load.Campaign!;

            // A run entered, a floor gone down, and the quit: three saves, each into
            // slot 2, the backup always the save before.
            var script = VisitScript.Enter(played, "spear_hall", CampaignRuns.FiveDeep);
            Assert.Null(CampaignBoot.Save(slots.Slot(2), played, savedAt: T3));
            string entered = slots.Store(2).Read()!;
            script.Down();
            Assert.Null(CampaignBoot.Save(slots.Slot(2), played, new ViewDto("A"), savedAt: T3.AddMinutes(1)));
            string down = slots.Store(2).Read()!;
            Assert.Equal(entered, slots.Store(2).ReadBackup());
            CampaignRuns.Exit(played);
            Assert.True(CampaignBoot.SaveOnQuit(slots.Slot(2), played, view: null, step: null, savedAt: T3.AddMinutes(2)));
            Assert.Equal((Text(played, T3.AddMinutes(2)), down), (slots.Store(2).Read(), slots.Store(2).ReadBackup()));

            Assert.Equal(others, Snapshot(folder, NotSlot2));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName], Files(slots.Store(2).Folder));
            Assert.All(Snapshot(folder).Keys, path => Assert.Contains(Path.GetFileName(path), new[] { SaveStore.FileName, SaveStore.BackupName }));

            // The next load of slot 2 brings back the campaign as it was left; the
            // autosave still holds its own.
            var again = CampaignBoot.Load(slots.Slot(2), GameContent.Current);
            StateAssert.Equivalent(played, again.Campaign!, "campaign");
            slots.Store(2).ClearResuming();
            Assert.Equal(Text(campaigns[0], T2), slots.Store(SaveSlots.Autosave).Read());
        });
    }

    [Fact]
    public void ALoweredCountLeavesEverySaveWhereItIs()
    {
        InTempFolder(folder =>
        {
            // Five slots saved, at a count of five.
            var five = new SaveSlots(folder, 5);
            CampaignState[] campaigns = [NewCampaign(), Retreated(), Cleared(), Retreated(), Cleared()];
            for (int n = 1; n <= 5; n++)
                Saved(five.Slot(n), campaigns[n - 1], T1.AddMinutes(n));
            Assert.Empty(five.Beyond());
            var seeded = Snapshot(folder);

            // At three, slots 4 and 5 are beyond the count: kept, listed, their every
            // byte and file name as they were.
            var three = new SaveSlots(folder, 3);
            Assert.Equal([4, 5], three.Beyond());
            var listing = SaveListing.Read(three, Now);
            Assert.Equal([0, 1, 2, 3, 4, 5], listing.Slots.Select(slot => slot.Number));
            Assert.Equal([false, false, false, false, true, true], listing.Slots.Select(slot => slot.Beyond));
            Assert.All(listing.Slots.Skip(1), slot => Assert.Equal(SlotState.Readable, slot.State));
            Assert.Equal(5, listing.Newest!.Number);
            Assert.Equal(seeded, Snapshot(folder));

            // Marked as beyond the count, and nothing more: it is no less a slot to load.
            Assert.Equal("BEYOND THE 3 SLOTS", SlotText.Rows(listing.Slots.Single(slot => slot.Number == 5), 3, GameContent.Current.Dungeons, TimeZoneInfo.Utc)[^1]);

            // And one beyond the count still loads.
            var load = CampaignBoot.Load(three.Slot(5), GameContent.Current);
            Assert.True(load.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 5"], load.Notices);
            StateAssert.Equivalent(campaigns[4], load.Campaign!, "campaign");
            three.Store(5).ClearResuming();
            Assert.Equal(seeded, Snapshot(folder));

            // Loaded, it is chosen, so it saves into itself (the user's ruling of
            // 2026-09-25 on the save slots, R5): played on and saved, then quit, slot 5
            // holds the campaign as it was left, its save before it the backup, and
            // every other slot's every byte as it was — nothing deleted, nothing set
            // aside, and the count still three.
            var played = load.Campaign!;
            CampaignRuns.ShallowRetreat(played, "spear_hall");
            Assert.Null(CampaignBoot.Save(three.Slot(5), played, savedAt: T2));
            string second = three.Store(5).Read()!;
            CampaignRuns.DeepRetreat(played, "spear_hall");
            Assert.True(CampaignBoot.SaveOnQuit(three.Slot(5), played, view: null, step: null, savedAt: T3));
            Assert.Equal((Text(played, T3), second), (three.Store(5).Read(), three.Store(5).ReadBackup()));
            static bool OtherThanFive(string path) => !path.StartsWith("slot-5", StringComparison.Ordinal);
            Assert.Equal(seeded.Where(file => OtherThanFive(file.Key)).ToDictionary(file => file.Key, file => file.Value, StringComparer.Ordinal), Snapshot(folder, OtherThanFive));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName], Files(three.Store(5).Folder));
            Assert.Equal([4, 5], three.Beyond());

            // Raised to five again, they are ordinary slots.
            var again = new SaveSlots(folder, 5);
            Assert.Empty(again.Beyond());
            Assert.All(SaveListing.Read(again, Now).Slots, slot => Assert.False(slot.Beyond));
        });
    }

    [Fact]
    public void EachSlotRoundTrips_AndSetsAsideInItsOwnFolder()
    {
        InTempFolder(folder =>
        {
            var slots = new SaveSlots(folder, 3);
            var autosave = slots.Store(SaveSlots.Autosave);
            Saved(slots.Slot(SaveSlots.Autosave), Retreated(), T1);
            var autosaved = Snapshot(folder, path => !path.StartsWith("slot-", StringComparison.Ordinal));

            // Between runs: saved to slot 2 and loaded back, the same text, time and all.
            var slot2 = slots.Store(2);
            var campaign = Cleared();
            Saved(slots.Slot(2), campaign, T2);
            string between = slot2.Read()!;
            Assert.Equal(Path.Combine(folder, "slot-2", SaveStore.FileName), slot2.SavePath);
            var load = CampaignBoot.Load(slots.Slot(2), GameContent.Current);
            Assert.True(load.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 2"], load.Notices);
            Assert.Equal(CampaignBoot.Resumed(slots.Slot(2), backup: false), load.Notices[0]);
            Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(NoticeText.Rows(load.Notices)[0]));
            Assert.Equal(between, Text(load.Campaign!, T2));
            slot2.ClearResuming();

            // Mid-run with a view: the run in progress and the view come back, the text whole.
            var script = VisitScript.Enter(load.Campaign!, "spear_hall", CampaignRuns.FiveDeep);
            script.Down();
            var view = new ViewDto("C", MarchingThisTurn: true);
            Assert.Null(CampaignBoot.Save(slots.Slot(2), script.Campaign, view, savedAt: T3));
            string midRun = slot2.Read()!;
            var back = CampaignBoot.Load(slots.Slot(2), GameContent.Current);
            Assert.True(back.Loaded);
            Assert.Equal(view, back.View);
            Assert.Equal(("spear_hall", 1), (back.Campaign!.Visit!.Def.Id, back.Campaign.Visit.FloorIndex));
            Assert.Equal(midRun, SaveCodec.Write(SaveGame.Capture(back.Campaign, back.View, T3)));
            slot2.ClearResuming();

            // Its save broken: set aside in slot 2's own folder, and slot 2's backup —
            // the save between runs — resumes, one save older, and says which slot.
            File.WriteAllText(slot2.SavePath, "not a save");
            var fromBackup = CampaignBoot.Load(slots.Slot(2), GameContent.Current);
            Assert.True(fromBackup.Loaded);
            string aside = Path.Combine(folder, "slot-2", "campaign.unreadable-1.json");
            Assert.Equal("not a save", File.ReadAllText(aside));
            Assert.Collection(fromBackup.Notices,
                notice => Assert.Equal("CAMPAIGN RESUMED FROM THE BACKUP OF SLOT 2, ONE SAVE OLDER", notice),
                notice => SaysSetAside(notice, SaveStore.FileName, $"could not be read ({WhyOf("not a save", slot2)})", aside));
            Assert.Equal(CampaignBoot.Resumed(slots.Slot(2), backup: true), fromBackup.Notices[0]);
            Assert.Equal(DungeonHud.Green, BetweenRunsHud.NoticeColor(NoticeText.Rows(fromBackup.Notices)[0]));
            Assert.Equal(between, Text(fromBackup.Campaign!, T2));
            slot2.ClearResuming();
            Assert.Equal([SaveStore.BackupName, "campaign.unreadable-1.json"], Files(slot2.Folder));

            // The autosave, beside it, untouched throughout: nothing set aside there.
            Assert.Equal(autosaved, Snapshot(folder, path => !path.StartsWith("slot-", StringComparison.Ordinal)));
            Assert.Equal([SaveStore.FileName], Files(folder));
        });
    }

    [Fact]
    public void TheListingReadsAndNeverMoves()
    {
        InTempFolder(folder =>
        {
            // A slot of every kind: the autosave a save; slot 1 a save that is not
            // one; slot 2 a save whose last load never finished; slot 3 a newer
            // build's save beside a backup this build reads; slot 5, beyond the
            // count, a newer build's save alone.
            var slots = new SaveSlots(folder, 3);
            var campaign = Cleared();
            Saved(slots.Slot(0), campaign, T1);
            Put(slots.Store(1), SaveStore.FileName, "{ \"version\": 1, \"campaign\": ");
            Saved(slots.Slot(2), campaign, T2);
            slots.Store(2).MarkResuming();
            Put(slots.Store(3), SaveStore.BackupName, Text(campaign, T3));
            Put(slots.Store(3), SaveStore.FileName, Newer(Text(campaign, Now)));
            Put(slots.Store(5), SaveStore.FileName, Newer(Text(campaign, Now)));
            var seeded = Snapshot(folder);

            var log = new List<string>();
            var listing = SaveListing.Read(slots, Now, log.Add);
            Assert.Equal([0, 1, 2, 3, 5], listing.Slots.Select(slot => slot.Number));
            Assert.Equal([SlotState.Readable, SlotState.Unreadable, SlotState.Readable, SlotState.FromBackup, SlotState.Unreadable],
                listing.Slots.Select(slot => slot.State));
            Assert.Equal([false, false, true, false, false], listing.Slots.Select(slot => slot.Unfinished));
            Assert.Equal([false, false, false, false, true], listing.Slots.Select(slot => slot.Beyond));
            Assert.Equal(new SlotHeader(T3, campaign.RunCounter, null, null, null), listing.Slots[3].Header);   // the backup's, which a load would bring back
            Assert.Null(listing.Slots[1].Header);
            Assert.Equal(3, listing.Newest!.Number);
            Assert.Empty(listing.Notices);
            Assert.Equal(5, log.Count);   // a line for each slot holding anything
            Assert.All(log, line => Assert.StartsWith("[Save] ", line, StringComparison.Ordinal));

            // Not one file moved, deleted or written — the resume marker included —
            // and a second reading reads the same.
            Assert.Equal(seeded, Snapshot(folder));
            Assert.Equal(listing.Slots, SaveListing.Read(slots, Now).Slots);
            Assert.Equal(seeded, Snapshot(folder));

            // A save another program holds open cannot be read: listed as not reading,
            // and said, since no row can say why — still moving nothing.
            if (OperatingSystem.IsWindows())
            {
                SaveListing held;
                using (new FileStream(slots.Store(0).SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    held = SaveListing.Read(slots, Now);
                Assert.Equal(SlotState.Unreadable, held.Autosave.State);
                Assert.Equal(["AUTOSAVE: CAMPAIGN.JSON COULD NOT BE READ (ANOTHER PROGRAM HAS IT OPEN)"], held.Notices);
                Assert.All(held.Notices.SelectMany(NoticeText.Wrap), row => Assert.Matches(Font, row));
                Assert.Equal(seeded, Snapshot(folder));
            }
        });
    }

    [Fact]
    public void ASaveCarriesTheTimeItWasTaken()
    {
        InTempFolder(folder =>
        {
            // The time is handed in — an offset one written as the UTC instant, round
            // trip — and written last; read back, it is the same instant.
            var campaign = Cleared();
            var at = new DateTimeOffset(2026, 9, 25, 14, 32, 0, TimeSpan.FromHours(2));
            var file = SaveGame.Capture(campaign, savedAt: at);
            Assert.Equal("2026-09-25T12:32:00.0000000Z", file.SavedAt);
            string text = SaveCodec.Write(file);
            Assert.EndsWith(",\n  \"savedAt\": \"2026-09-25T12:32:00.0000000Z\"\n}\n", text, StringComparison.Ordinal);
            var read = SaveCodec.Read(text);
            Assert.Equal(at.UtcTicks, SaveGame.SavedAtOf(read)!.Value.UtcTicks);
            Assert.Equal(text, SaveCodec.Write(read));

            // With none handed in, the member says so: null, never left out.
            string untimed = Text(campaign);
            Assert.EndsWith(",\n  \"savedAt\": null\n}\n", untimed, StringComparison.Ordinal);
            Assert.Null(SaveGame.SavedAtOf(SaveCodec.Read(untimed)));

            // Two saves of one campaign at two times are one text but for the time,
            // and so is a save from before the member existed; anything else is itself.
            Assert.Equal(SaveCodec.Untimed(text), SaveCodec.Untimed(Text(campaign, at.AddDays(3))));
            Assert.Equal(SaveCodec.Untimed(text), SaveCodec.Untimed(untimed));
            Assert.Equal(SaveCodec.Untimed(text), BeforeTheSlots(untimed));
            Assert.NotEqual(SaveCodec.Untimed(text), SaveCodec.Untimed(Text(Retreated(), at)));
            foreach (string other in new[] { "not a save", "", "{ \"savedAt\": null }", text.Replace("\n", "\r\n", StringComparison.Ordinal) })
                Assert.Equal(other, SaveCodec.Untimed(other));

            // A time that is no round-trip time is refused where it is, before anything
            // is built — its problem in words the notice can draw.
            foreach (var (written, problem) in new[]
            {
                ("yesterday", "yesterday is no time"),
                ("2026-09-25", "2026-09-25 is no time"),
                ("2026-09-25T14:32:00Z", "2026-09-25T14:32:00Z is no time"),
                ("", "a blank is no time"),
            })
            {
                var tree = JsonNode.Parse(text)!.AsObject();
                tree["savedAt"] = written;
                var bad = SaveCodec.Read(tree.ToJsonString());
                var refused = Assert.Throws<SaveFormatException>(() => SaveGame.Restore(bad, GameContent.Current));
                Assert.Equal((SaveCodec.SavedAtMember, problem), (refused.Where, refused.Problem));
            }

            // Loaded, such a save is set aside as one that cannot be read, the notice
            // naming the member — every row of it in the HUD's font — and the backup resumes.
            var slots = new SaveSlots(folder, 3);
            var store = slots.Store(SaveSlots.Autosave);
            Saved(slots.Slot(SaveSlots.Autosave), NewCampaign(), T1);
            Saved(slots.Slot(SaveSlots.Autosave), campaign, T2);
            var yesterday = JsonNode.Parse(store.Read()!)!.AsObject();
            yesterday["savedAt"] = "yesterday";
            File.WriteAllText(store.SavePath, yesterday.ToJsonString());
            Assert.Equal(SlotState.Readable, SaveListing.Read(slots, Now).Autosave.State);   // the codec reads it: only a load refuses it
            Assert.Null(SaveListing.Read(slots, Now).Autosave.Header!.SavedAt);
            var load = CampaignBoot.Load(slots.Slot(SaveSlots.Autosave), GameContent.Current);
            Assert.True(load.Loaded);
            string aside = Path.Combine(store.Folder, "campaign.unreadable-1.json");
            Assert.Collection(load.Notices,
                notice => Assert.Equal(CampaignBoot.ResumedFromBackup, notice),
                notice =>
                {
                    SaysSetAside(notice, SaveStore.FileName, "could not be read (savedAt: yesterday is no time)", aside);
                    Assert.StartsWith("CAMPAIGN.JSON COULD NOT BE READ (SAVEDAT: YESTERDAY IS NO TIME) - SET ASIDE AS ", notice, StringComparison.Ordinal);
                    Assert.All(NoticeText.Wrap(notice.Replace(aside, "", StringComparison.Ordinal)), row => Assert.Matches(Font, row));
                });
            store.ClearResuming();

            // The same campaign saved at a later time writes nothing: the save keeps
            // its time, and the backup stays the save before it.
            Saved(slots.Slot(SaveSlots.Autosave), campaign, T2);
            string saved = store.Read()!, backup = store.ReadBackup()!;
            var log = new List<string>();
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.Autosave), campaign, report: log.Add, savedAt: T3));
            Assert.Equal((saved, backup), (store.Read(), store.ReadBackup()));
            Assert.Equal(T2, SaveGame.SavedAtOf(SaveCodec.Read(store.Read()!)));
            Assert.Equal([$"[Save] {store.SavePath} already holds this campaign - nothing written, the backup kept"], log);

            // The clock is the edge's: the game reads it, and nothing in Logic/ does.
            static bool ReadsTheClock(MethodBase target)
                => (target.DeclaringType == typeof(DateTime) || target.DeclaringType == typeof(DateTimeOffset)) && target.Name is "get_Now" or "get_UtcNow" or "get_Today";
            var readers = Calls(typeof(DistanceRpgGame).Assembly).Where(call => ReadsTheClock(call.Target))
                .Select(call => Root(call.From.DeclaringType)!.FullName!).Distinct().ToArray();
            Assert.Equal([typeof(DistanceRpgGame).FullName!], readers);
        });
    }

    [Fact]
    public void AWriteNeverRotatesAwayAFileThatCannotBeRead()
    {
        InTempFolder(folder =>
        {
            var slots = new SaveSlots(folder, 3);
            var a = NewCampaign();
            var b = Retreated();
            var c = Cleared();

            // A newer build's save in the autosave slot, never loaded, saved into
            // twice: its bytes set aside before the first write, which moves into
            // place, and untouched by either — said once, never rotated away.
            var autosave = slots.Store(SaveSlots.Autosave);
            string newer = Newer(Text(c, T1));
            Put(autosave, SaveStore.FileName, newer);
            var said = new List<string>();
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.Autosave), a, savedAt: T2, said: said));
            string aside = Path.Combine(autosave.Folder, "campaign.unreadable-1.json");
            Assert.Equal(newer, File.ReadAllText(aside));
            Assert.Collection(said, notice => SaysSetAside(notice, SaveStore.FileName,
                $"could not be read (version: {SaveCodec.CurrentVersion + 1} is newer than this build reads ({SaveCodec.CurrentVersion}))", aside));
            Assert.False(autosave.HasBackup);
            Assert.Null(CampaignBoot.Save(slots.Slot(SaveSlots.Autosave), b, savedAt: T3, said: said));
            Assert.Single(said);
            Assert.Equal(newer, File.ReadAllText(aside));
            Assert.Equal((Text(b, T3), Text(a, T2)), (autosave.Read(), autosave.ReadBackup()));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName, "campaign.unreadable-1.json"], Files(autosave.Folder));

            // A save that is not one over a backup that reads: the save set aside, the
            // write moved into place, the backup's bytes as they were. (A second write
            // rotates that backup as any backup rotates: the guard's promise is the
            // unreadable file's.)
            var slot1 = slots.Store(1);
            string kept = Text(a, T1);
            Put(slot1, SaveStore.BackupName, kept);
            Put(slot1, SaveStore.FileName, "not a save");
            said.Clear();
            Assert.Null(CampaignBoot.Save(slots.Slot(1), b, savedAt: T2, said: said));
            string garbage = Path.Combine(slot1.Folder, "campaign.unreadable-1.json");
            Assert.Equal("not a save", File.ReadAllText(garbage));
            Assert.Collection(said, notice => SaysSetAside(notice, SaveStore.FileName, $"could not be read ({WhyOf("not a save", slot1)})", garbage));
            Assert.Equal((Text(b, T2), kept), (slot1.Read(), slot1.ReadBackup()));
            Assert.Null(CampaignBoot.Save(slots.Slot(1), c, savedAt: T3));
            Assert.Equal((Text(c, T3), Text(b, T2)), (slot1.Read(), slot1.ReadBackup()));

            // A save that reads over a newer build's backup: the backup set aside before
            // the replace would put the save in its place.
            var slot2 = slots.Store(2);
            string newerBackup = Newer(Text(c, T1));
            Put(slot2, SaveStore.FileName, Text(a, T1));
            Put(slot2, SaveStore.BackupName, newerBackup);
            said.Clear();
            Assert.Null(CampaignBoot.Save(slots.Slot(2), b, savedAt: T2, said: said));
            string backupAside = Path.Combine(slot2.Folder, "campaign.unreadable-1.json");
            Assert.Equal(newerBackup, File.ReadAllText(backupAside));
            Assert.Collection(said, notice => SaysSetAside(notice, SaveStore.BackupName,
                $"could not be read (version: {SaveCodec.CurrentVersion + 1} is newer than this build reads ({SaveCodec.CurrentVersion}))", backupAside));
            Assert.Equal((Text(b, T2), Text(a, T1)), (slot2.Read(), slot2.ReadBackup()));

            // After the store refuses its writes, no guard runs: a refusal, and nothing
            // set aside or written.
            var slot3 = slots.Store(3);
            Put(slot3, SaveStore.FileName, "not a save");
            slot3.RefuseWrites("campaign.json is in the way");
            said.Clear();
            Assert.Equal(CampaignBoot.NotSaved(Named(SaveSlots.Autosave), "campaign.json is in the way"), CampaignBoot.Save(AsAutosave(slot3), a, said: said));
            Assert.Empty(said);
            Assert.Equal([SaveStore.FileName], Files(slot3.Folder));
            Assert.Equal("not a save", File.ReadAllText(slot3.SavePath));

            // The common case — a save this session did not write, which reads, and
            // then its own — says nothing but that it wrote.
            var reread = new SaveSlots(folder, 3).Store(SaveSlots.Autosave);   // a fresh session: nothing known of the files
            var log = new List<string>();
            Assert.Null(CampaignBoot.Save(AsAutosave(reread), c, report: log.Add, savedAt: Now));
            Assert.Null(CampaignBoot.Save(AsAutosave(reread), a, report: log.Add, savedAt: Now.AddMinutes(1)));
            Assert.Equal([$"[Save] Wrote {reread.SavePath}", $"[Save] Wrote {reread.SavePath}"], log);

            if (!OperatingSystem.IsWindows())
                return;

            // A save another program holds open cannot be read right now: it is left
            // to the write, which fails as ever — nothing set aside, writes not refused
            // — and goes through once it is freed.
            var held = new SaveSlots(folder, 3).Store(1);
            string before = File.ReadAllText(held.SavePath);
            string? failed;
            said.Clear();
            using (new FileStream(held.SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                failed = CampaignBoot.Save(AsAutosave(held), a, savedAt: Now, said: said);
            Assert.StartsWith("THE CAMPAIGN COULD NOT BE SAVED (", failed, StringComparison.Ordinal);
            Assert.Empty(said);
            Assert.Null(held.WritesRefused);
            Assert.Equal(before, File.ReadAllText(held.SavePath));
            Assert.DoesNotContain(Files(held.Folder), name => name.StartsWith("campaign.unreadable-2", StringComparison.Ordinal));
            Assert.Null(CampaignBoot.Save(AsAutosave(held), a, savedAt: Now));

            // One it can read and cannot move: the stuck notice is the failure, writes
            // are refused from then on, and the file is exactly as it was.
            var stuck = new SaveSlots(folder, 3).Store(2);
            Put(stuck, SaveStore.FileName, "not a save");
            said.Clear();
            using (new FileStream(stuck.SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                failed = CampaignBoot.Save(AsAutosave(stuck), a, said: said);
            Assert.Equal(CampaignBoot.StuckNotice(SaveStore.FileName, $"could not be read ({WhyOf("not a save", stuck)})", "another program has it open"), failed);
            Assert.Empty(said);
            Assert.NotNull(stuck.WritesRefused);
            Assert.Equal("not a save", File.ReadAllText(stuck.SavePath));
            Assert.NotNull(CampaignBoot.Save(AsAutosave(stuck), a));
            Assert.Equal("not a save", File.ReadAllText(stuck.SavePath));
        });
    }

    [Fact]
    public void AWriteClearsItsSlotsResumeMarker()
    {
        InTempFolder(folder =>
        {
            // A slot whose last load crashed the game: its marker stands.
            var slots = new SaveSlots(folder, 3);
            var slot1 = slots.Store(1);
            Saved(slots.Slot(1), NewCampaign(), T1);
            Assert.True(CampaignBoot.Load(slots.Slot(1), GameContent.Current).Loaded);
            Assert.True(slot1.WasResuming);

            // Another campaign written into it — a save by hand into the slot, or the
            // autosave after a load of another — replaces the file the marker guarded,
            // so the marker goes with it, and the log says so.
            var other = Cleared();
            var log = new List<string>();
            Assert.Null(CampaignBoot.Save(slots.Slot(1), other, report: log.Add, savedAt: T2));
            Assert.False(slot1.WasResuming);
            Assert.Equal([$"[Save] Wrote {slot1.SavePath}", $"[Save] {slot1.ResumingPath} cleared: the save it guarded has been written over"], log);

            // So the next load brings the new save back, with nothing set aside as crashed.
            var next = CampaignBoot.Load(slots.Slot(1), GameContent.Current);
            Assert.True(next.Loaded);
            Assert.Equal(["CAMPAIGN RESUMED FROM SLOT 1"], next.Notices);
            StateAssert.Equivalent(other, next.Campaign!, "campaign");
            Assert.DoesNotContain(Files(slot1.Folder), name => name.Contains("crashed", StringComparison.Ordinal));

            // A save that writes nothing — the slot already holds the campaign —
            // replaces nothing, and the marker stands; so does a write that fails.
            Assert.True(slot1.WasResuming);
            Assert.Null(CampaignBoot.Save(slots.Slot(1), other, savedAt: T3));
            Assert.True(slot1.WasResuming);
            Directory.CreateDirectory(slot1.TempPath);
            Assert.NotNull(CampaignBoot.Save(slots.Slot(1), Retreated(), savedAt: T3));
            Assert.True(slot1.WasResuming);
            Directory.Delete(slot1.TempPath);
            slot1.ClearResuming();
        });
    }

    [Fact]
    public void ADevOverrideSessionNeverTouchesTheSlots()
    {
        InTempFolder(folder =>
        {
            // The save folder, seeded: the autosave and its backup, and slot 1.
            var slots = new SaveSlots(folder, 3);
            Saved(slots.Slot(SaveSlots.Autosave), NewCampaign(), T1);
            Saved(slots.Slot(SaveSlots.Autosave), Cleared(), T2);
            Saved(slots.Slot(1), Retreated(), T3);
            static bool Campaigns(string path) => !path.StartsWith(SaveStore.OverrideSlot, StringComparison.Ordinal);
            var seeded = Snapshot(folder, Campaigns);

            // A dev override's session, in its own slot: begun, entered, saved mid-run
            // and between runs, and quit.
            var request = new BootRequest("spear_hall", 7);
            var scratch = SaveSlot.Scratch(CampaignBoot.StoreFor(folder, request));
            Assert.Equal(Path.Combine(slots.Folder, SaveStore.OverrideSlot), scratch.Store.Folder);
            var boot = CampaignBoot.BootOverride(scratch, GameContent.Current, request);
            var visit = boot.Campaign.Enter(boot.StraightInto!, boot.FirstSeed!.Value);
            Assert.Null(CampaignBoot.Save(scratch, boot.Campaign, savedAt: Now));
            visit.OpenTurns(boot.Campaign.Party, CampaignRuns.Ten);
            Assert.Null(CampaignBoot.Save(scratch, boot.Campaign, new ViewDto("A"), savedAt: Now.AddMinutes(1)));
            CampaignRuns.Exit(boot.Campaign);
            Assert.True(CampaignBoot.SaveOnQuit(scratch, boot.Campaign, view: null, step: null, savedAt: Now.AddMinutes(2)));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName], Files(scratch.Store.Folder));

            // The campaign's own saves — the autosave, its backup and slot 1 — byte for
            // byte as they were.
            Assert.Equal(seeded, Snapshot(folder, Campaigns));

            // And the game's launch keeps it so: its override branch makes the
            // session's store the override's (CampaignBoot.StoreFor), its slot the
            // scratch slot (SaveSlot.Scratch), and binds the scratch campaign to it
            // (Adopt) once it is begun, asking the slots for nothing before — the
            // slots are made only on the other branch, after it, which opens on the
            // title.
            var bootCampaign = typeof(DistanceRpgGame).GetMethod("BootCampaign", Declared)!;
            byte[] il = Body(bootCampaign)!;
            var instructions = InstructionsAt(il).ToList();
            MethodBase? Called((OpCode Op, int Token, int At) i) => IsCall(i.Op) ? Resolve(bootCampaign, i.Token) : null;
            var slotsField = typeof(DistanceRpgGame).GetField("_slots", Declared)!;
            bool Stores((OpCode Op, int Token, int At) i, FieldInfo field)
                => i.Op == OpCodes.Stfld && BitConverter.ToInt32(il, i.At) is int token && bootCampaign.Module.ResolveField(token) is { } stored
                    && stored.Module == field.Module && stored.MetadataToken == field.MetadataToken;
            var storeFor = typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.StoreFor), Declared)!;
            var scratchMade = typeof(SaveSlot).GetMethod(nameof(SaveSlot.Scratch), Declared)!;
            var bootOverride = typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.BootOverride), Declared)!;
            var adopt = typeof(DistanceRpgGame).GetMethod("Adopt", Declared)!;
            var slotStore = typeof(SaveSlots).GetMethod(nameof(SaveSlots.Store), Declared)!;
            var slotSlot = typeof(SaveSlots).GetMethod(nameof(SaveSlots.Slot), Declared)!;
            var slotsMade = typeof(SaveSlots).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();

            int atStoreFor = instructions.FindIndex(i => Same(Called(i), storeFor));
            int atScratch = instructions.FindIndex(i => Same(Called(i), scratchMade));
            int atOverride = instructions.FindIndex(i => Same(Called(i), bootOverride));
            int atAdopt = instructions.FindIndex(i => Same(Called(i), adopt));
            Assert.True(atStoreFor >= 0 && atStoreFor < atScratch && atScratch < atOverride && atOverride < atAdopt,
                "BootCampaign does not make its scratch slot from StoreFor's store, boot the override and then bind its campaign to it, in that order");
            Assert.Single(instructions, i => Same(Called(i), adopt));
            Assert.DoesNotContain(instructions.Take(atAdopt + 1), i => Same(Called(i), slotStore) || Same(Called(i), slotSlot) || Same(Called(i), slotsMade) || Stores(i, slotsField));
            Assert.Contains(instructions.Skip(atAdopt), i => Same(Called(i), slotsMade));
            Assert.Equal(["DistanceRpgGame.BootCampaign"], Calls(typeof(DistanceRpgGame).Assembly)
                .Where(call => Same(call.Target, slotsMade)).Select(call => $"{Root(call.From.DeclaringType)!.Name}.{call.From.Name}").Distinct());

            // A load makes no campaign: one that brings nothing back goes back to the
            // title (S6; CampaignBoot.NothingLoadedWord, tested in CampaignBootTests).
            // A campaign is begun by NEW CAMPAIGN — the title's, or --new-campaign
            // (CampaignBoot.Abandon) — and by a dev override (BootOverride), and by
            // nothing else in the game.
            var start = typeof(CampaignState).GetMethod(nameof(CampaignState.Start), Declared)!;
            var load = typeof(CampaignBoot).GetMethod(nameof(CampaignBoot.Load), Declared)!;
            Assert.DoesNotContain(Reachable(load), method => CallsIn(method).Any(called => Same(called, start)));
            Assert.Equal(["CampaignBoot.Abandon", "CampaignBoot.BootOverride"], Calls(typeof(DistanceRpgGame).Assembly)
                .Where(call => Same(call.Target, start)).Select(call => $"{Root(call.From.DeclaringType)!.Name}.{call.From.Name}").Distinct().Order(StringComparer.Ordinal));
        });
    }
}
