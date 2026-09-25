using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>What a slot list is for: the title's LOAD GAME list. (Saving by hand, which the ruling asks for too, adds its own mode when it arrives.)</summary>
internal enum SlotMenuMode
{
    /// <summary>Loading: every slot listed, a slot that holds something chosen to be loaded.</summary>
    Load,
}

/// <summary>What one key press did to a slot list (<see cref="SlotMenu.Press"/>).</summary>
internal enum SlotStep
{
    /// <summary>Nothing: a key the list does not answer, or a move with nowhere else to go.</summary>
    None,

    /// <summary>The choice moved to another slot.</summary>
    Moved,

    /// <summary>A slot was chosen (<see cref="SlotMenu.Chosen"/>): the list's owner loads it, or asks first.</summary>
    Chosen,

    /// <summary>A slot that holds nothing was asked for, and the list says so (<see cref="SlotMenu.Said"/>).</summary>
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
/// holds, in number order — <c>AUTOSAVE</c>, <c>SLOT 1</c> to the count, then any
/// slot beyond the count that holds a save. W/S or the arrows move over the rows
/// that hold something, round the ends; a digit takes its slot (0 the autosave);
/// ENTER takes the one chosen; a slot that holds nothing is refused with
/// <c>SLOT 2 IS EMPTY</c>, which stays until the next key; ESC goes back. A slot
/// that holds files none of which reads can be chosen: its load sets them aside
/// and brings nothing back, and the title says so. The list decides nothing about
/// the autosave: whether loading a slot sets it aside first is its owner's ask.
/// </para>
/// </summary>
internal sealed class SlotMenu
{
    /// <param name="mode">What the list is for.</param>
    /// <param name="listing">The slots, as the listing read them.</param>
    /// <param name="count">How many manual slots there are (<c>tuning.json</c>'s <c>SaveSlots</c>), which a slot beyond it names.</param>
    public SlotMenu(SlotMenuMode mode, SaveListing listing, int count)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        Mode = mode;
        Count = count;
        Rows = listing.Slots.OrderBy(slot => slot.Number).ToList();

        // The choice starts on the most recent save, and failing that on the first
        // slot that holds anything; with nothing anywhere, on none.
        int newest = listing.Newest is { } found ? IndexOf(found.Number) : -1;
        Cursor = newest >= 0 ? newest : Next(-1, +1);
    }

    /// <summary>What the list is for.</summary>
    public SlotMenuMode Mode { get; }

    /// <summary>How many manual slots there are.</summary>
    public int Count { get; }

    /// <summary>The slots, top to bottom: the autosave, the manual slots to the count, then any beyond it that hold a save.</summary>
    public IReadOnlyList<SlotEntry> Rows { get; }

    /// <summary>The index of the row chosen now — always one that holds something — or -1 when no slot holds anything.</summary>
    public int Cursor { get; private set; }

    /// <summary>The slot the last key chose, by number, or null: set by a <see cref="SlotStep.Chosen"/> step, cleared by the next key.</summary>
    public int? Chosen { get; private set; }

    /// <summary>What the list says under itself — <c>SLOT 2 IS EMPTY</c> — until the next key; null for nothing.</summary>
    public string? Said { get; private set; }

    /// <summary>
    /// One key press, once: W/S and the arrows move, a digit or ENTER chooses,
    /// ESC goes back (<see cref="SlotStep"/>). Every key first clears what the list
    /// said and what it chose.
    /// </summary>
    public SlotStep Press(Keys key)
    {
        Said = null;
        Chosen = null;
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
        Chosen = Rows[row].Number;
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

    /// <summary>The next row from <paramref name="from"/> in <paramref name="step"/>'s direction that holds something, round the ends; <paramref name="from"/> itself when no other does; -1 when none does.</summary>
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
            if (Rows[at].State != SlotState.Empty)
                return at;
        }
        return from >= 0 && from < count && Rows[from].State != SlotState.Empty ? from : -1;
    }
}
