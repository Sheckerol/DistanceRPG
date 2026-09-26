namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The slots a test hands the save's loads, saves and words when it does not
/// ask a <see cref="SaveSlots"/> for one (<see cref="SaveSlots.Slot"/>, the
/// game's way): every one of them takes a <see cref="SaveSlot"/>, its store and
/// its name together. A store in a slot folder that a test saves into or loads
/// goes in as its own slot, <see cref="SaveSlots.Slot"/>; a store in a folder of
/// the test's own goes in as slot 1 (<see cref="AsSlot1"/>), whose words are the
/// ones a player sees of a numbered slot — or, made the save folder, as the old
/// autosave (<see cref="AsOldAutosave"/>).
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
    /// <paramref name="store"/>, in a folder of the test's own, as slot 1: loaded
    /// from and saved into as a slot is, and named as a player sees a numbered
    /// slot named — <c>CAMPAIGN RESUMED FROM SLOT 1</c>, <c>SLOT 1 COULD NOT BE SAVED
    /// (...)</c> — whatever folder the store is in.
    /// </summary>
    internal static SaveSlot AsSlot1(SaveStore store) => new(store, 1);

    /// <summary>
    /// <paramref name="store"/> — a folder of the test's own, made the save folder
    /// — as the old autosave, number <see cref="SaveSlots.OldAutosave"/>: the slot
    /// <see cref="SaveSlots.Slot"/> gives for the save folder's own store.
    /// </summary>
    internal static SaveSlot AsOldAutosave(SaveStore store) => new(store, SaveSlots.OldAutosave);
}
