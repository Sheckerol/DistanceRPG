namespace GameEngine.DistanceRPG;

/// <summary>
/// The title screen's words (the user's ruling of 2026-09-25 on the save model,
/// R2(a)): the game's name, the four options in the ruling's order — CONTINUE,
/// NEW CAMPAIGN, LOAD GAME, QUIT — the keys, the heading and keys of NEW
/// CAMPAIGN's slot list, and its ask. Every one is here and tested (<c>TitleTests</c>), in the HUD's font: capitals, digits and
/// <c>. , : - ( ) / &gt; &lt;</c>, never an apostrophe, a quote or a brace, which
/// the engine's font cannot draw.
/// <para>
/// <strong>Every ask is two rows</strong>: what it does, then what the keys do,
/// drawn where the keys stand. <strong>Only ENTER says yes</strong>, as each ask's
/// second row says, and every other key — N and Y among them — says no, so a
/// player pressing N to mean no keeps what the ask named.
/// </para>
/// </summary>
internal static class TitleText
{
    /// <summary>The game's name, across the top.</summary>
    internal const string Title = "DISTANCE RPG";

    /// <summary>The keys the options answer, along the bottom.</summary>
    internal const string KeysLine = "W/S OR ARROWS CHOOSE - ENTER SELECTS - ESC QUITS";

    /// <summary>
    /// The keys the load list answers, along the bottom while it is up: W/S and the
    /// arrows only move, and a digit loads its slot at once, as ENTER loads the one
    /// chosen (S6's review: a digit never only chooses). And what a load means,
    /// said where the choice is made: the campaign it brings back saves into the
    /// slot it came from, every save of it (the user's ruling of 2026-09-25 on the
    /// save slots, R5) — so a slot kept as a checkpoint is played forward, and
    /// written over, from its first load.
    /// </summary>
    internal const string LoadKeysLine = "W/S OR ARROWS CHOOSE - ENTER OR 0-9 LOADS - PLAY SAVES INTO THE SLOT YOU LOAD - ESC BACK";

    /// <summary>CONTINUE with no save to load: drawn dimmed, and passed over.</summary>
    internal const string NoSaveYet = "CONTINUE - NO SAVE YET";

    /// <summary>The second option: a campaign begun afresh, with the starting party.</summary>
    internal const string NewCampaign = "NEW CAMPAIGN";

    /// <summary>The third option, and the load list's heading while it is up.</summary>
    internal const string LoadGame = "LOAD GAME";

    /// <summary>The last option: the game closes.</summary>
    internal const string Quit = "QUIT";

    /// <summary>How the parts of a row are joined, as the slot rows join theirs.</summary>
    private const string Joint = " - ";

    /// <summary>What the second row of every ask ends with: ENTER alone says yes.</summary>
    private const string EnterOrEsc = "ENTER: YES - ESC: NO";

    /// <summary>CONTINUE, naming what it loads: the most recent save's own row (<see cref="SlotText.Row"/>) — <c>CONTINUE - AUTOSAVE - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32</c>.</summary>
    internal static string Continue(string row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return $"CONTINUE{Joint}{row}";
    }

    /// <summary>NEW CAMPAIGN's list's heading, where the notices stand while it is up: the slot the new campaign begins in, and saves into, is chosen there (the user's ruling of 2026-09-25 on the save slots, R5).</summary>
    internal const string NewCampaignHeading = "NEW CAMPAIGN - CHOOSE A SLOT";

    /// <summary>
    /// The keys NEW CAMPAIGN's list answers, along the bottom while it is up: W/S
    /// and the arrows only move, and a digit begins in its slot at once, as ENTER
    /// begins in the one chosen — each asking first over a slot that holds anything.
    /// No 0: the autosave is never offered.
    /// </summary>
    internal const string NewCampaignKeysLine = "W/S OR ARROWS CHOOSE - ENTER OR 1-9 BEGINS - ESC BACK";

    /// <summary>
    /// NEW CAMPAIGN's ask, over a slot that holds a save, a backup, or files none of
    /// which reads (SLOT DECISION 2): the campaign in the slot is the subject, as the
    /// old games ask whether to overwrite a file — <c>SLOT 2 HOLDS THE SPEAR HALL
    /// FLOOR 4 - 3 RUNS</c>, or <c>SLOT 2 CANNOT BE READ</c> — then that beginning
    /// there takes it off LOAD GAME, set aside and never deleted: <c>BEGIN HERE -
    /// WHAT IT HOLDS LEAVES LOAD GAME, KEPT AS FILES, NEVER DELETED - ENTER: YES -
    /// ESC: NO</c>.
    /// </summary>
    /// <param name="slot">The slot the new campaign would begin in.</param>
    /// <param name="place">What it holds (<see cref="SlotText.Summary"/>); unread when <paramref name="unreadable"/>.</param>
    /// <param name="unreadable">Nothing in it reads (<see cref="SlotState.Unreadable"/>): there is no campaign to name.</param>
    internal static (string What, string Keys) NewCampaignAsk(int slot, string place, bool unreadable = false)
    {
        ArgumentNullException.ThrowIfNull(place);
        string what = unreadable ? $"{SaveSlots.Label(slot)} {SlotText.CannotBeRead}" : $"{SaveSlots.Label(slot)} HOLDS {place}";
        return (what, $"BEGIN HERE{Joint}WHAT IT HOLDS LEAVES LOAD GAME, KEPT AS FILES, NEVER DELETED{Joint}{EnterOrEsc}");
    }
}
