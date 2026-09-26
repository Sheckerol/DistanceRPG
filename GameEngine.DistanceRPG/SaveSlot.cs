namespace GameEngine.DistanceRPG;

/// <summary>
/// One save slot: its store and its name, which travel together, so every load,
/// save and word takes the slot — never a store handed in beside a number that
/// could name another slot. The save folder's slots come from
/// <see cref="SaveSlots.Slot"/>, which hands each the session's one store for it,
/// so a refusal and what the store knows of its files hold wherever the slot
/// goes; a dev-override launch's scratch slot from <see cref="Scratch"/>.
/// Engine-free, and it touches no file: the store does.
/// </summary>
/// <param name="Store">The slot's store: the session's one for a slot of the save folder (<see cref="SaveSlots.Store"/>), or the dev override's (<see cref="CampaignBoot.StoreFor"/>).</param>
/// <param name="Number">The slot's number — 1 to <see cref="SaveSlots.Most"/> for <c>slot-n</c>, <see cref="SaveSlots.OldAutosave"/> for the save folder itself — or null for the dev override's scratch slot, which has none.</param>
internal sealed record SaveSlot(SaveStore Store, int? Number)
{
    /// <summary>What the words call the old autosave: the save folder itself, which a build before the user's ruling on the save slots (R5) saved into as the autosave.</summary>
    internal const string OldAutosaveName = "THE OLD AUTOSAVE";

    /// <summary>What the words call a dev-override launch's scratch slot.</summary>
    internal const string ScratchName = "THE SCRATCH SLOT";

    /// <summary>The slot's store.</summary>
    public SaveStore Store { get; } = Store ?? throw new ArgumentNullException(nameof(Store));

    /// <summary>The slot's number, or null for the scratch slot.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is outside 0 to <see cref="SaveSlots.Most"/>.</exception>
    public int? Number { get; } = Number is < SaveSlots.OldAutosave || Number > SaveSlots.Most
        ? throw new ArgumentOutOfRangeException(nameof(Number), Number, $"A slot is numbered from {SaveSlots.OldAutosave} to {SaveSlots.Most}, or has no number: the scratch slot.")
        : Number;

    /// <summary>What the words call the slot: <c>SLOT 2</c>, <see cref="OldAutosaveName"/>, or <see cref="ScratchName"/>. Every word of a slot is built from it.</summary>
    public string Name => Number switch
    {
        null => ScratchName,
        SaveSlots.OldAutosave => OldAutosaveName,
        { } number => SaveSlots.Label(number),
    };

    /// <summary>A dev-override launch's scratch slot: <paramref name="store"/> (<see cref="CampaignBoot.StoreFor"/>), with no number.</summary>
    internal static SaveSlot Scratch(SaveStore store) => new(store, null);
}
