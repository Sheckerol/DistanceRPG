using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>What a slot list is for: the title's LOAD GAME list, or its NEW CAMPAIGN list. Saving by hand has none: SAVE GAME saves the campaign's own slot, asking nothing (the user's ruling of 2026-09-25 on the save slots, R5).</summary>
internal enum SlotMenuMode
{
    /// <summary>Loading: every slot listed, a slot that holds something chosen to be loaded.</summary>
    Load,

    /// <summary>A new campaign: slots 1 to the count, every one chosen to begin the campaign in, asking before one that holds anything.</summary>
    NewCampaign,
}

/// <summary>What one key press did to a slot list (<see cref="SlotMenu.Press"/>).</summary>
internal enum SlotStep
{
    /// <summary>Nothing: a key the list does not answer, a move with nowhere else to go, or a key that answered an ask no.</summary>
    None,

    /// <summary>The choice moved to another slot.</summary>
    Moved,

    /// <summary>A slot that holds something was chosen on NEW CAMPAIGN's list, which asks first (<see cref="SlotMenu.Asking"/>): the next key is the ask's alone.</summary>
    Asking,

    /// <summary>A slot was chosen (<see cref="SlotMenu.Chosen"/>): the list's owner loads it, or begins a campaign in it.</summary>
    Chosen,

    /// <summary>A slot the list cannot take was asked for, and the list says so (<see cref="SlotMenu.Said"/>).</summary>
    Refused,

    /// <summary>ESC: the list goes, and its owner's options come back.</summary>
    Back,
}

/// <summary>
/// A list of the save folder's slots to choose one from (the user's ruling of
/// 2026-09-25 on the save model, R2(d)): pure — keys in, a step out — so every
/// key is tested (<c>TitleTests</c>), and only the drawing is the screen's.
/// <para>
/// <strong>Load mode</strong>, the title's LOAD GAME: every slot the listing
/// holds, in number order — the <c>OLD AUTOSAVE</c> while the listing holds it,
/// <c>SLOT 1</c> to the count, then any slot beyond the count that holds a save.
/// W/S or the arrows move over the rows that hold something, round the ends; a
/// digit takes its slot — 0 the old autosave while it is listed, and otherwise
/// nothing; ENTER takes the one chosen; a slot that holds nothing is refused with
/// <c>SLOT 2 IS EMPTY</c>, which stays until the next key; ESC goes back. A slot
/// that holds files none of which reads can be chosen: its load sets them aside
/// and brings nothing back, and the title says so. A slot is loaded at once: the
/// campaign it brings back saves into it from then on (the user's ruling of
/// 2026-09-25 on the save slots, R5), and no other slot is touched.
/// </para>
/// <para>
/// <strong>NEW CAMPAIGN mode</strong>, the title's NEW CAMPAIGN: the slot the new
/// campaign begins in, and saves into from then on, is chosen here (the user's
/// ruling of 2026-09-25 on the save slots, R5: "you choose a save slot"). Slots 1
/// to the count, every one a row a key can take — never the old autosave, and
/// never a slot beyond the count, so no campaign begins in either. The choice
/// starts on the lowest empty slot (<see cref="SaveListing.NewCampaignSlot"/>), so
/// an ENTER pressed without looking sets nothing aside while a slot is free. An
/// empty slot is chosen at once. One that holds anything — a save, a backup, or
/// files none of which reads — asks first (<see cref="Asking"/>) whether to begin
/// there, setting what it holds aside, and <strong>only ENTER says yes</strong>:
/// the slot's digit again, N, Y, the arrows, ESC and every other key say no and
/// are spent. A digit above the count is refused, saying why; 0 does nothing.
/// </para>
/// </summary>
internal sealed class SlotMenu
{
    /// <param name="mode">What the list is for.</param>
    /// <param name="listing">The slots, as the listing read them.</param>
    /// <param name="count">How many save slots there are (<c>tuning.json</c>'s <c>SaveSlots</c>), which a slot beyond it names.</param>
    public SlotMenu(SlotMenuMode mode, SaveListing listing, int count)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        Mode = mode;
        Count = count;
        Rows = listing.Slots
            .Where(slot => mode == SlotMenuMode.Load || !slot.Beyond)
            .Where(slot => mode != SlotMenuMode.NewCampaign || slot.Number != SaveSlots.OldAutosave)
            .OrderBy(slot => slot.Number)
            .ToList();

        if (mode == SlotMenuMode.NewCampaign)
        {
            // The lowest empty slot, so an ENTER pressed without looking sets
            // nothing aside; with none, slot 1.
            Cursor = IndexOf(listing.NewCampaignSlot);
            return;
        }

        // The choice starts on the most recent save, and failing that on the first
        // slot that holds anything; with nothing anywhere, on none.
        int newest = listing.Newest is { } found ? IndexOf(found.Number) : -1;
        Cursor = newest >= 0 ? newest : Next(-1, +1);
    }

    /// <summary>
    /// How many slots <paramref name="listing"/> offers: the ones it lists from 1 up
    /// to the count — every slot to the count is listed, the empty ones included —
    /// never fewer than one.
    /// </summary>
    internal static int CountOf(SaveListing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        return Math.Max(1, listing.Slots.Count(slot => slot.Number != SaveSlots.OldAutosave && !slot.Beyond));
    }

    /// <summary>What the list is for.</summary>
    public SlotMenuMode Mode { get; }

    /// <summary>How many save slots there are.</summary>
    public int Count { get; }

    /// <summary>The slots, top to bottom: the old autosave while it is listed (never on NEW CAMPAIGN's list), the slots to the count, then — loading — any beyond it that hold a save.</summary>
    public IReadOnlyList<SlotEntry> Rows { get; }

    /// <summary>The index of the row chosen now — always one a key can take (<see cref="Choosable"/>) — or -1 when there is none.</summary>
    public int Cursor { get; private set; }

    /// <summary>The slot the last key chose, by number, or null: set by a <see cref="SlotStep.Chosen"/> step, cleared by the next key.</summary>
    public int? Chosen { get; private set; }

    /// <summary>What the list says under itself — <c>SLOT 2 IS EMPTY</c> — until the next key; null for nothing.</summary>
    public string? Said { get; private set; }

    /// <summary>The slot NEW CAMPAIGN's list is asking about — whether to begin there, setting what it holds aside — until the next key, which answers; null for no ask.</summary>
    public SlotEntry? Asking { get; private set; }

    /// <summary>
    /// One key press, once. An ask takes it first: ENTER says yes and chooses the
    /// slot asked about; any other key says no and is spent. Then W/S and the
    /// arrows move, a digit or ENTER chooses, ESC goes back (<see cref="SlotStep"/>).
    /// Every key first clears what the list said and what it chose.
    /// </summary>
    public SlotStep Press(Keys key)
    {
        Said = null;
        Chosen = null;
        if (Asking is { } asked)
        {
            Asking = null;
            if (key is not (Keys.Enter or Keys.KeyPadEnter))
                return SlotStep.None;   // no, and spent
            Chosen = asked.Number;
            return SlotStep.Chosen;
        }

        switch (key)
        {
            case Keys.Escape:
                return SlotStep.Back;
            case Keys.W or Keys.Up:
                return Move(-1);
            case Keys.S or Keys.Down:
                return Move(+1);
            case Keys.Enter or Keys.KeyPadEnter:
                if (Cursor >= 0)
                    return Take(Cursor);
                // Nothing a key can take: loading, every slot is empty. (NEW
                // CAMPAIGN's list always has a row: slot 1 at the least.)
                return Mode == SlotMenuMode.Load ? Refuse(SlotText.NothingToLoad) : SlotStep.None;
        }

        if (Digit(key) is not { } digit)
            return SlotStep.None;
        int row = IndexOf(digit);
        if (digit == SaveSlots.OldAutosave && (Mode == SlotMenuMode.NewCampaign || row < 0))
            return SlotStep.None;   // no campaign begins in the old autosave, and none is loaded from it unless it is listed
        if (Mode == SlotMenuMode.NewCampaign)
            return row >= 0 ? Take(row) : Refuse(SlotText.NotOffered(digit, Count));
        return row >= 0 && Rows[row].State != SlotState.Empty ? Take(row) : Refuse(SlotText.IsEmpty(digit));
    }

    /// <summary>
    /// The slot number a digit key names — the number row's 0 to 9, as the bag's
    /// digits are read in the dungeon — or null for any other key. The keypad's are
    /// not digits here: with NumLock off its 2, 4, 6 and 8 are arrows to the
    /// player, and the engine names the physical key, so a keypad 2 meant as down
    /// would otherwise load slot 2.
    /// </summary>
    internal static int? Digit(Keys key) => key is >= Keys.D0 and <= Keys.D9 ? key - Keys.D0 : null;

    /// <summary>Whether a key can take <paramref name="row"/>: loading, one that holds something; beginning a new campaign, every row there is. A row no key can take is drawn dimmed.</summary>
    internal bool Choosable(SlotEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Mode == SlotMenuMode.NewCampaign || row.State != SlotState.Empty;
    }

    private SlotStep Move(int step)
    {
        int next = Next(Cursor, step);
        if (next < 0 || next == Cursor)
            return SlotStep.None;
        Cursor = next;
        return SlotStep.Moved;
    }

    private SlotStep Take(int row)
    {
        Cursor = row;
        var entry = Rows[row];
        if (Mode != SlotMenuMode.Load && entry.State != SlotState.Empty)
        {
            Asking = entry;   // beginning there: it holds something, and the next key answers
            return SlotStep.Asking;
        }
        Chosen = entry.Number;
        return SlotStep.Chosen;
    }

    private SlotStep Refuse(string said)
    {
        Said = said;
        return SlotStep.Refused;
    }

    private int IndexOf(int number)
    {
        for (int i = 0; i < Rows.Count; i++)
            if (Rows[i].Number == number)
                return i;
        return -1;
    }

    /// <summary>The next row from <paramref name="from"/> in <paramref name="step"/>'s direction that a key can take (<see cref="Choosable"/>), round the ends; <paramref name="from"/> itself when no other can; -1 when none can.</summary>
    private int Next(int from, int step)
    {
        int count = Rows.Count;
        if (count == 0)
            return -1;
        int direction = Math.Sign(step);
        int at = from;
        for (int moved = 0; moved < count; moved++)
        {
            at = ((at + direction) % count + count) % count;
            if (Choosable(Rows[at]))
                return at;
        }
        return from >= 0 && from < count && Choosable(Rows[from]) ? from : -1;
    }
}
