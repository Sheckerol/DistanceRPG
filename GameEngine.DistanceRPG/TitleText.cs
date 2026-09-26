using System.Globalization;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The title screen's words (the user's ruling of 2026-09-25 on the save model,
/// R2(a)): the game's name, the four options in the ruling's order — CONTINUE,
/// NEW CAMPAIGN, LOAD GAME, QUIT — the keys, and NEW CAMPAIGN's ask. Every one is here
/// and tested (<c>TitleTests</c>), in the HUD's font: capitals, digits and
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

    /// <summary>
    /// NEW CAMPAIGN's ask, when the autosave slot holds a save or a backup (USER
    /// DECISION 3): what it sets aside — <c>NEW CAMPAIGN SETS THE AUTOSAVE ASIDE -
    /// THE SPEAR HALL FLOOR 4 - 3 RUNS</c> — and that it is never deleted and the
    /// manual slots are kept: <c>AS FILES, NEVER DELETED - SLOTS 1 TO 3 ARE KEPT -
    /// ENTER: YES - ESC: NO</c>.
    /// </summary>
    /// <param name="place">What the autosave holds (<see cref="SlotText.Summary"/>).</param>
    /// <param name="count">How many manual slots there are, every one of which is kept.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is below 1: there is always a manual slot.</exception>
    internal static (string What, string Keys) NewCampaignAsk(string place, int count)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        string kept = count == 1 ? "SLOT 1 IS KEPT" : $"SLOTS 1 TO {count.ToString(CultureInfo.InvariantCulture)} ARE KEPT";
        return ($"NEW CAMPAIGN SETS THE AUTOSAVE ASIDE{Joint}{place}", $"AS FILES, NEVER DELETED{Joint}{kept}{Joint}{EnterOrEsc}");
    }
}
