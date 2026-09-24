using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using static GameEngine.DistanceRPG.Tests.TestIl;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The save's edge (1e, 1f): one campaign in one folder, written atomically so a
/// failed write never costs the save before it, a save that cannot be played set
/// aside where it is never deleted or written over, the resume marker, and the
/// dev overrides' slot, the only folder ever emptied. The folder is always handed
/// in — there is no default one — so these read and write temporary folders of
/// their own and delete them after, and the folder a launch plays in is worked
/// out from strings alone, with no environment read and no disk touched.
/// </summary>
public class SaveStoreTests
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

    /// <summary>The names of the files directly in <paramref name="folder"/>, in ordinal order.</summary>
    private static string[] Files(string folder)
        => Directory.GetFiles(folder).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void AWriteIsAtomic_AndKeepsTheLastAsTheBackup()
    {
        InTempFolder(folder =>
        {
            // A folder that is not there yet: nothing to read, and nothing made
            // until the first write.
            string saves = Path.Combine(folder, "saves");
            var store = new SaveStore(saves);
            Assert.Equal(Path.GetFullPath(saves), store.Folder);
            Assert.Equal(Path.Combine(store.Folder, "campaign.json"), store.SavePath);
            Assert.Equal(Path.Combine(store.Folder, "campaign.bak.json"), store.BackupPath);
            Assert.Null(store.Read());
            Assert.Null(store.ReadBackup());
            Assert.False(Directory.Exists(saves));

            // The first write moves its temp file into place: UTF-8 with no byte
            // order mark, the text exactly as given, and no backup, there being
            // nothing before it.
            const string first = "{ \"campaign\": \"the first, é\" }\n";
            store.Write(first);
            Assert.Equal(new UTF8Encoding(false).GetBytes(first), File.ReadAllBytes(store.SavePath));
            Assert.False(store.HasBackup);
            Assert.Equal([SaveStore.FileName], Files(saves));

            // Every write after it replaces the save in one step, and the save it
            // replaces becomes the backup: one save back, never more.
            store.Write("second");
            Assert.Equal(("second", first), (store.Read(), store.ReadBackup()));
            store.Write("third");
            Assert.Equal(("third", "second"), (store.Read(), store.ReadBackup()));
            Assert.Equal([SaveStore.BackupName, SaveStore.FileName], Files(saves));   // no temp file left behind

            // A write that fails leaves both exactly as they were: here a folder
            // stands where the temp file goes, so the write fails before the
            // replace — and the replace is the only step that touches the save.
            Directory.CreateDirectory(store.TempPath);
            Assert.ThrowsAny<Exception>(() => store.Write("fourth"));
            Assert.Equal(("third", "second"), (store.Read(), store.ReadBackup()));
            Directory.Delete(store.TempPath);
            store.Write("fourth");
            Assert.Equal(("fourth", "third"), (store.Read(), store.ReadBackup()));

            // A store that refuses writes — a save it could not read or move is in
            // the way (CampaignBoot) — writes nothing at all, and says why.
            store.RefuseWrites("campaign.json is in the way");
            Assert.Equal("campaign.json is in the way", Assert.Throws<IOException>(() => store.Write("fifth")).Message);
            Assert.Equal(("fourth", "third"), (store.Read(), store.ReadBackup()));
        });
    }

    [Fact]
    public void AnUnreadableSaveIsSetAside_NeverOverwritten()
    {
        InTempFolder(folder =>
        {
            // Set aside by the first free number, as campaign.<reason>-N.json: the
            // file moved whole, and its full path handed back for the notice.
            var store = new SaveStore(folder);
            store.Write("first");
            string first = store.SetAside("unreadable");
            Assert.Equal(Path.Combine(store.Folder, "campaign.unreadable-1.json"), first);
            Assert.Equal("first", File.ReadAllText(first));
            Assert.False(store.HasSave);

            // The next takes the next number; the first is never written over.
            store.Write("second");
            Assert.Equal(Path.Combine(store.Folder, "campaign.unreadable-2.json"), store.SetAside("unreadable"));
            Assert.Equal("first", File.ReadAllText(first));

            // A number already taken — a player's own copy, even a folder of that
            // name — is passed over, and what holds it is left alone.
            string copy = Path.Combine(store.Folder, "campaign.unreadable-3.json");
            File.WriteAllText(copy, "a copy the player made");
            Directory.CreateDirectory(Path.Combine(store.Folder, "campaign.unreadable-4.json"));
            store.Write("third");
            Assert.Equal(Path.Combine(store.Folder, "campaign.unreadable-5.json"), store.SetAside("unreadable"));
            Assert.Equal("a copy the player made", File.ReadAllText(copy));

            // The backup is set aside the same way, and each reason counts its own.
            store.Write("fourth");
            store.Write("fifth");
            string backup = store.SetAside("crashed", backup: true);
            Assert.Equal(Path.Combine(store.Folder, "campaign.crashed-1.json"), backup);
            Assert.Equal("fourth", File.ReadAllText(backup));
            Assert.Equal((true, false, "fifth"), (store.HasSave, store.HasBackup, store.Read()));
            Assert.Equal(Path.Combine(store.Folder, "campaign.abandoned-1.json"), store.SetAside("abandoned"));

            // Nothing there is nothing to set aside, and nothing is made; a reason
            // is one lower-case word, since it names a file.
            Assert.Throws<FileNotFoundException>(() => store.SetAside("crashed"));
            Assert.Throws<FileNotFoundException>(() => store.SetAside("crashed", backup: true));
            store.Write("sixth");
            foreach (string reason in new[] { "", " ", "Unreadable", "not read", "../unreadable", "crashed-1" })
                Assert.ThrowsAny<ArgumentException>(() => store.SetAside(reason));
            Assert.Equal("sixth", store.Read());

            // Every file set aside is still there, as it was.
            Assert.Equal(
                ["campaign.abandoned-1.json", "campaign.crashed-1.json", "campaign.json", "campaign.unreadable-1.json", "campaign.unreadable-2.json",
                 "campaign.unreadable-3.json", "campaign.unreadable-5.json"],
                Files(folder));
        });
    }

    [Fact]
    public void TheResumeMarkerIsAFileOfItsOwn()
    {
        InTempFolder(folder =>
        {
            // The marker says a resume is in flight: set before the resume, cleared
            // once the resumed screen is up. It is no save, and neither setting nor
            // clearing it touches one.
            var store = new SaveStore(Path.Combine(folder, "saves"));
            Assert.False(store.WasResuming);
            store.ClearResuming();   // nothing there: nothing to do
            store.MarkResuming();
            Assert.True(store.WasResuming);
            Assert.Equal(Path.Combine(store.Folder, "campaign.resuming"), store.ResumingPath);
            store.Write("the campaign");
            store.MarkResuming();
            Assert.True(store.WasResuming);
            store.ClearResuming();
            Assert.False(store.WasResuming);
            Assert.Equal([SaveStore.FileName], Files(store.Folder));
            Assert.Equal("the campaign", store.Read());
        });
    }

    /// <summary>What reading the machine looks like from IL: a file or a folder, the environment's variables and folders, the executable's own folder.</summary>
    private static bool TouchesTheMachine(MethodBase target)
    {
        var type = target.DeclaringType;
        if (type is null) return false;
        if (type == typeof(File) || type == typeof(Directory) || type == typeof(FileInfo) || type == typeof(DirectoryInfo) || type == typeof(FileStream))
            return true;
        if (type == typeof(Environment))
            return target.Name is nameof(Environment.GetEnvironmentVariable) or nameof(Environment.GetFolderPath) or "get_" + nameof(Environment.CurrentDirectory);
        if (type == typeof(Path))
            return target.Name == nameof(Path.GetFullPath);   // reads the working folder for a relative path
        return type == typeof(AppContext) && target.Name == "get_" + nameof(AppContext.BaseDirectory);
    }

    [Fact]
    public void TheFolderIsAPureFunctionOfWhatIsHandedIn()
    {
        // The saves live in the player's data folder (1e), not beside the
        // executable: <local app data>/DistanceRPG/saves, or wherever
        // DISTANCERPG_SAVE_DIR says. The functions that say so take the variable's
        // value and the data folder as strings, and hand a string back.
        string local = Path.Combine("home", "someone", ".local", "share");
        string mine = Path.Combine(local, "DistanceRPG", "saves");
        Assert.Equal("DISTANCERPG_SAVE_DIR", SaveStore.DirectoryVariable);
        Assert.Equal(mine, SaveStore.DefaultDirectory(local));
        Assert.Equal(mine, SaveStore.SaveDirectory(null, local));
        Assert.Equal(mine, SaveStore.SaveDirectory("", local));
        Assert.Equal(mine, SaveStore.SaveDirectory("  \t", local));
        Assert.Equal("D:/scratch", SaveStore.SaveDirectory("D:/scratch", local));
        Assert.Equal("D:/scratch", SaveStore.SaveDirectory("  D:/scratch\n", local));

        // A dev-override launch plays in the slot inside whichever folder that is.
        Assert.Equal(Path.Combine("D:/scratch", "override"), SaveStore.SlotDirectory("D:/scratch", devOverride: true));
        Assert.Equal("D:/scratch", SaveStore.SlotDirectory("D:/scratch", devOverride: false));
        Assert.Equal(Path.Combine(mine, "override"), SaveStore.SlotDirectory(SaveStore.SaveDirectory(null, local), devOverride: true));

        // Strings in, strings out: none of the three reads the environment or the
        // disk, by their IL. The caller reads the environment.
        const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (string name in new[] { nameof(SaveStore.DefaultDirectory), nameof(SaveStore.SaveDirectory), nameof(SaveStore.SlotDirectory) })
        {
            var method = typeof(SaveStore).GetMethod(name, Statics)!;
            var called = Instructions(Body(method)!)
                .Where(i => i.Op == OpCodes.Call || i.Op == OpCodes.Callvirt || i.Op == OpCodes.Newobj)
                .Select(i => Resolve(method, i.Token))
                .OfType<MethodBase>()
                .ToArray();
            Assert.NotEmpty(called);   // the scan reads something
            Assert.DoesNotContain(called, TouchesTheMachine);
        }

        // And no store is made without a folder: there is no default to fall back
        // on, so nothing can reach the player's real saves by accident.
        Assert.Null(typeof(SaveStore).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes));
        Assert.ThrowsAny<ArgumentException>(() => new SaveStore(" "));

        // The environment is read by the game alone, at the edge — never by the
        // save's own types.
        var readers = Calls(typeof(SaveStore).Assembly)
            .Where(c => c.Target.DeclaringType == typeof(Environment)
                && c.Target.Name is nameof(Environment.GetEnvironmentVariable) or nameof(Environment.GetFolderPath))
            .Select(c => Root(c.From.DeclaringType)!.Name)
            .Distinct()
            .ToArray();
        Assert.Equal(["DistanceRpgGame"], readers);
    }

    [Fact]
    public void EmptyingRefusesAnythingButTheOverrideSlot()
    {
        InTempFolder(folder =>
        {
            // The campaign's own folder is never emptied: refused, and nothing in it touched.
            var campaign = new SaveStore(folder);
            campaign.Write("the campaign");
            campaign.Write("the campaign, a run on");
            Assert.Throws<InvalidOperationException>(campaign.Empty);
            Assert.Equal(("the campaign, a run on", "the campaign"), (campaign.Read(), campaign.ReadBackup()));

            // Nor is a folder whose name only looks like the slot, or one under it.
            foreach (string near in new[] { "override-copy", "overrides", Path.Combine("override", "deeper") })
            {
                var store = new SaveStore(Path.Combine(folder, near));
                store.Write("kept");
                Assert.Throws<InvalidOperationException>(store.Empty);
                Assert.Equal("kept", store.Read());
            }

            // The slot itself: every file in it goes, whatever it is, and nothing
            // below it — a folder the slot holds is left as it is. A slot that is
            // not there yet is already empty.
            var slot = new SaveStore(SaveStore.SlotDirectory(folder, devOverride: true));
            Directory.Delete(slot.Folder, recursive: true);
            slot.Empty();
            Assert.False(Directory.Exists(slot.Folder));
            slot.Write("a scratch campaign");
            slot.Write("a scratch campaign, a run on");
            slot.MarkResuming();
            File.WriteAllText(Path.Combine(slot.Folder, "notes.txt"), "anything");
            Directory.CreateDirectory(Path.Combine(slot.Folder, "deeper"));
            slot.Empty();
            Assert.Empty(Files(slot.Folder));
            Assert.True(Directory.Exists(Path.Combine(slot.Folder, "deeper")));

            // The campaign beside it is untouched throughout.
            Assert.Equal(("the campaign, a run on", "the campaign"), (campaign.Read(), campaign.ReadBackup()));
        });
    }
}
