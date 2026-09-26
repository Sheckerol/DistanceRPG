namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The slots a test hands the save's loads, saves and words when it does not
/// ask a <see cref="SaveSlots"/> for one (<see cref="SaveSlots.Slot"/>, the
/// game's way): every one of them takes a <see cref="SaveSlot"/>, its store and
/// its name together. A store in a slot folder that a test saves into or loads
/// goes in as its own slot, <see cref="SaveSlots.Slot"/>, but for the few whose
/// tests assert the autosave's words of it (<see cref="AsAutosave"/>).
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
    /// the save's tests make it — the slot <see cref="SaveSlots.Slot"/> gives for
    /// it — or a store in a slot folder whose test asserts the words the
    /// autosave's saves say, not a manual slot's. No other store in a slot folder
    /// goes in as the autosave.
    /// </summary>
    internal static SaveSlot AsAutosave(SaveStore store) => new(store, SaveSlots.Autosave);
}
