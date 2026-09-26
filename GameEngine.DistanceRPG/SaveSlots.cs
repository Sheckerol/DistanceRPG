using System.Globalization;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The save folder's slots (the user's rulings of 2026-09-25 on the save model,
/// R2, and on the save slots, R5): slot 1 to <see cref="Count"/>, each a
/// <see cref="SaveStore"/> of its own — its own save, backup, markers and set-aside
/// files, by the same names in every slot — and each holding one campaign, which
/// saves only there.
/// <list type="bullet">
/// <item><strong>Slot n is the folder <c>slot-n</c> inside the save
/// folder</strong>, n from 1 to <see cref="Count"/>: <c>tuning.json</c>'s
/// <c>SaveSlots</c>, at most <see cref="Most"/>, so a digit picks each. The names
/// are built from the numbers and the save folder is never enumerated, so a folder
/// named any other way — <c>slot-01</c>, <c>slot-10</c>, <c>slot-x</c> — is no
/// slot: never read, listed or written, and no folder's spelling or order reaches
/// the list.</item>
/// <item><strong>The save folder itself is only the old autosave</strong>
/// (<see cref="OldAutosave"/>): the <c>campaign.json</c> a build before R5 saved
/// there, as the autosave. It is listed while it holds a save or a backup
/// (<see cref="SaveListing.Read"/>), loaded, and saved into by its own campaign;
/// NEW CAMPAIGN never begins there, so nothing new is ever saved in it.</item>
/// <item><strong>A count lowered below a slot that holds a save deletes
/// nothing</strong>: the slots above the count up to <see cref="Most"/> that hold a
/// save or a backup are <see cref="Beyond"/> it — listed, loaded, and saved into
/// by the campaign played from each, since a campaign saves into the slot it came
/// from (R5), but never offered to NEW CAMPAIGN — and a count raised again makes
/// them ordinary slots.</item>
/// <item><strong>One store per slot for the session</strong>
/// (<see cref="Store"/>): made on its first ask and handed back after, so a store
/// that refused its writes over a file that would not move
/// (<see cref="SaveStore.RefuseWrites"/>) goes on refusing through the load and
/// every save after it, and what it knows of its files
/// (<see cref="SaveStore.SaveVouched"/>) is known once.</item>
/// </list>
/// Engine-free, <c>System.IO</c> through the stores alone, and handed the folder:
/// a test hands it a temporary one. A dev-override launch makes none — its
/// scratch campaign saves in its own slot (<see cref="CampaignBoot.StoreFor"/>),
/// never here.
/// </summary>
internal sealed class SaveSlots
{
    /// <summary>The old autosave's number: the save folder itself, which a build before the user's ruling on the save slots (R5) saved into as the autosave.</summary>
    public const int OldAutosave = 0;

    /// <summary>How a slot's folder is named, before its number: <c>slot-2</c>.</summary>
    public const string SlotFolderPrefix = "slot-";

    /// <summary>What the old autosave is called on screen.</summary>
    public const string OldAutosaveLabel = "OLD AUTOSAVE";

    private readonly Dictionary<int, SaveStore> _stores = new();

    /// <param name="saveDirectory">The save folder (<see cref="SaveStore.SaveDirectory"/>), made absolute here; it need not exist yet.</param>
    /// <param name="count">How many save slots there are: <c>tuning.json</c>'s <c>SaveSlots</c>, which the reader keeps from 1 to <see cref="Most"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is outside 1 to <see cref="Most"/>.</exception>
    public SaveSlots(string saveDirectory, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Most);
        Folder = Path.GetFullPath(saveDirectory);
        Count = count;
    }

    /// <summary>
    /// The most save slots there can be, and so the highest slot number:
    /// <c>SaveSlots</c>' clamp ceiling, read off its key, so the two cannot drift
    /// apart. A slot above <see cref="Count"/> and at most this is kept, listed and
    /// played.
    /// </summary>
    public static int Most { get; } = ((IntKey)TuningSchema.Find(nameof(Tuning.SaveSlots))!).Max;

    /// <summary>The save folder, as a full path: the slots' parent, and the old autosave's folder.</summary>
    public string Folder { get; }

    /// <summary>How many save slots are offered: slots 1 to this.</summary>
    public int Count { get; }

    /// <summary>
    /// Slot <paramref name="number"/>'s store — the save folder's <c>slot-n</c>,
    /// above the count included, or the save folder itself for
    /// <see cref="OldAutosave"/> — one per slot for the session: the same store
    /// every ask.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is outside 0 to <see cref="Most"/>.</exception>
    public SaveStore Store(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Most);
        if (!_stores.TryGetValue(number, out var store))
        {
            store = new SaveStore(number == OldAutosave ? Folder : Path.Combine(Folder, SlotFolderPrefix + number.ToString(CultureInfo.InvariantCulture)));
            _stores[number] = store;
        }
        return store;
    }

    /// <summary>Slot <paramref name="number"/> — its store (<see cref="Store"/>), the same one every ask, and its name — for every load, save and word of it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is outside 0 to <see cref="Most"/>.</exception>
    public SaveSlot Slot(int number) => new(Store(number), number);

    /// <summary>The slots above <see cref="Count"/> — up to <see cref="Most"/> — that hold a save or a backup, in number order: kept where they are, listed, and saved into only by the campaign played from each.</summary>
    public IReadOnlyList<int> Beyond()
        => Enumerable.Range(Count + 1, Most - Count).Where(number => Store(number) is { } store && (store.HasSave || store.HasBackup)).ToList();

    /// <summary>What a slot is called on screen: <c>SLOT 2</c>, or <c>OLD AUTOSAVE</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is negative.</exception>
    public static string Label(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        return number == OldAutosave ? OldAutosaveLabel : $"SLOT {number.ToString(CultureInfo.InvariantCulture)}";
    }
}
