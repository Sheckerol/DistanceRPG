using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>What a slot list is for: the title's LOAD GAME list, or the menus' save list, which saving by hand opens.</summary>
internal enum SlotMenuMode
{
    /// <summary>Loading: every slot listed, a slot that holds something chosen to be loaded.</summary>
    Load,

    /// <summary>Saving by hand: the campaign's own slot and the autosave shown and never chosen, the other manual slots to the count chosen to be saved into, asking before one that holds anything.</summary>
    Save,
}

/// <summary>What one key press did to a slot list (<see cref="SlotMenu.Press"/>).</summary>
internal enum SlotStep
{
    /// <summary>Nothing: a key the list does not answer, a move with nowhere else to go, or a key that answered an ask no.</summary>
    None,

    /// <summary>The choice moved to another slot.</summary>
    Moved,

    /// <summary>A slot that holds something was chosen on the save list, which asks first (<see cref="SlotMenu.Asking"/>): the next key is the ask's alone.</summary>
    Asking,

    /// <summary>A slot was chosen (<see cref="SlotMenu.Chosen"/>): the list's owner loads it, or saves into it.</summary>
    Chosen,

    /// <summary>A slot the list cannot take was asked for, and the list says so (<see cref="SlotMenu.Said"/>).</summary>
    Refused,

    /// <summary>ESC: the list goes, and its owner's options come back.</summary>
    Back,
}

/// <summary>
/// A list of the save folder's slots to choose one from (the user's ruling of
/// 2026-09-25 on the save model, R2(d)): pure — keys in, a step out — so every
/// key is tested (<c>TitleTests</c>, <c>SaveMenuTests</c>), and only the drawing
/// is the screen's.
/// <para>
/// <strong>Load mode</strong>, the title's LOAD GAME: every slot the listing
/// holds, in number order — <c>AUTOSAVE</c>, <c>SLOT 1</c> to the count, then any
/// slot beyond the count that holds a save. W/S or the arrows move over the rows
/// that hold something, round the ends; a digit takes its slot (0 the autosave);
/// ENTER takes the one chosen; a slot that holds nothing is refused with
/// <c>SLOT 2 IS EMPTY</c>, which stays until the next key; ESC goes back. A slot
/// that holds files none of which reads can be chosen: its load sets them aside
/// and brings nothing back, and the title says so. A slot is loaded at once: the
/// campaign it brings back saves into it from then on (the user's ruling of
/// 2026-09-25 on the save slots, R5), and no other slot is touched.
/// </para>
/// <para>
/// <strong>Save mode</strong>, the menus' SAVE GAME: the autosave's row and the
/// campaign's own slot (<see cref="Own"/>), each shown and never chosen — the
/// campaign's own slot ends <c>SAVES ITSELF</c>, since every save of the campaign
/// goes there already (R5) — then the other manual slots 1 to the count; a slot
/// beyond the count is never offered here. W/S or the arrows move over the slots
/// that can be chosen, round the ends; a digit takes its slot and ENTER the one
/// chosen. An empty slot is chosen at once. One that holds anything asks first
/// (<see cref="Asking"/>, R2(d): "saving into an occupied slot asks before
/// overwriting") — over a save that reads, whether to save over it; over one that
/// cannot be read, whether to set it aside and save there — and <strong>only
/// ENTER says yes</strong>: the slot's digit again, N, ESC and every other key say
/// no and are spent, as on the title. The campaign's own slot's digit says it
/// saves itself, a digit above the count is refused, saying why, and 0 — for a
/// campaign that saves elsewhere — does nothing. The choice starts on the first
/// empty slot, so an ENTER pressed without looking saves over nothing; with none
/// empty, on the first that can be chosen.
/// </para>
/// </summary>
internal sealed class SlotMenu
{
    /// <param name="mode">What the list is for.</param>
    /// <param name="listing">The slots, as the listing read them.</param>
    /// <param name="count">How many manual slots there are (<c>tuning.json</c>'s <c>SaveSlots</c>), which a slot beyond it names.</param>
    /// <param name="own">Saving: the slot the campaign in play saves into (<see cref="ISaveHost.OwnSlot"/>), shown and never chosen. The autosave's by default.</param>
    public SlotMenu(SlotMenuMode mode, SaveListing listing, int count, int own = SaveSlots.Autosave)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(own);
        Mode = mode;
        Count = count;
        Own = own;
        Rows = listing.Slots
            .Where(slot => mode == SlotMenuMode.Load || !slot.Beyond)
            .OrderBy(slot => slot.Number)
            .ToList();

        if (mode == SlotMenuMode.Save)
        {
            // The first empty slot, so an ENTER pressed without looking saves over
            // nothing; with none, the first slot that can be saved into.
            int empty = FirstRow(row => Choosable(row) && row.State == SlotState.Empty);
            Cursor = empty >= 0 ? empty : FirstRow(Choosable);
            return;
        }

        // The choice starts on the most recent save, and failing that on the first
        // slot that holds anything; with nothing anywhere, on none.
        int newest = listing.Newest is { } found ? IndexOf(found.Number) : -1;
        Cursor = newest >= 0 ? newest : Next(-1, +1);
    }

    /// <summary>
    /// How many manual slots <paramref name="listing"/> offers: the ones it lists up
    /// to the count — every manual slot to the count is listed, the empty ones
    /// included — never fewer than one.
    /// </summary>
    internal static int CountOf(SaveListing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        return Math.Max(1, listing.Slots.Count(slot => slot.Number != SaveSlots.Autosave && !slot.Beyond));
    }

    /// <summary>What the list is for.</summary>
    public SlotMenuMode Mode { get; }

    /// <summary>How many manual slots there are.</summary>
    public int Count { get; }

    /// <summary>Saving: the slot the campaign in play saves into, which the list shows and never offers.</summary>
    public int Own { get; }

    /// <summary>The slots, top to bottom: the autosave, the manual slots to the count, then — loading — any beyond it that hold a save.</summary>
    public IReadOnlyList<SlotEntry> Rows { get; }

    /// <summary>The index of the row chosen now — always one a key can take (<see cref="Choosable"/>) — or -1 when there is none.</summary>
    public int Cursor { get; private set; }

    /// <summary>The slot the last key chose, by number, or null: set by a <see cref="SlotStep.Chosen"/> step, cleared by the next key.</summary>
    public int? Chosen { get; private set; }

    /// <summary>What the list says under itself — <c>SLOT 2 IS EMPTY</c> — until the next key; null for nothing.</summary>
    public string? Said { get; private set; }

    /// <summary>The slot the save list is asking about — whether to save over it, or set it aside and save there — until the next key, which answers; null for no ask.</summary>
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
                return Cursor >= 0 ? Take(Cursor) : Refuse(SlotText.NothingToLoad);
        }

        if (Digit(key) is not { } digit)
            return SlotStep.None;
        int row = IndexOf(digit);
        if (Mode == SlotMenuMode.Save)
        {
            if (digit == Own)
                return Refuse(SlotText.SlotSavesItself(Own));
            if (digit == SaveSlots.Autosave)
                return SlotStep.None;   // never offered, and not the campaign's: nothing to say
            return row >= 0 ? Take(row) : Refuse(SlotText.NotOffered(digit, Count));
        }
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

    /// <summary>Whether a key can take <paramref name="row"/>: loading, one that holds something; saving, a manual slot other than the campaign's own, the empty ones included. A row no key can take is drawn dimmed.</summary>
    internal bool Choosable(SlotEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Mode == SlotMenuMode.Save ? row.Number != SaveSlots.Autosave && row.Number != Own : row.State != SlotState.Empty;
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
        if (Mode == SlotMenuMode.Save && entry.State != SlotState.Empty)
        {
            Asking = entry;   // it holds something: the next key answers
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

    /// <summary>The first row <paramref name="match"/> takes, or -1.</summary>
    private int FirstRow(Func<SlotEntry, bool> match)
    {
        for (int i = 0; i < Rows.Count; i++)
            if (match(Rows[i]))
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
