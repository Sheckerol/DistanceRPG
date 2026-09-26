namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The slots a test hands the save's loads, saves and words when it has no
/// <see cref="SaveSlots"/> to ask (<see cref="SaveSlots.Slot"/> is the game's
/// way, and a test's whenever it has one): every one of them takes a
/// <see cref="SaveSlot"/>, its store and its name together.
/// </summary>
internal static class TestSlots
{
    /// <summary>
    /// Slot <paramref name="number"/> — or the scratch slot, for null — over a
    /// store no test reads or writes: its folder is never made. For a test of the
    /// words alone, which read the slot's name and never its files.
    /// </summary>
    internal static SaveSlot Named(int? number)
        => new(new SaveStore(Path.Combine(Path.GetTempPath(), "distancerpg-words-only")), number);

    /// <summary>
    /// <paramref name="store"/> as the autosave slot, number
    /// <see cref="SaveSlots.Autosave"/>: the save folder's own store, as most of
    /// the save's tests make it, or a store in a slot folder whose test asserts
    /// the words the autosave's saves say, not a manual slot's.
    /// </summary>
    internal static SaveSlot AsAutosave(SaveStore store) => new(store, SaveSlots.Autosave);
}
