using System.Globalization;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// A save slot as the lists draw it (the user's ruling of 2026-09-25 on the save
/// model, R2(d): "the slot list shows, per slot, enough to choose by"): the title's
/// LOAD GAME list and CONTINUE's row, and the title's NEW CAMPAIGN list
/// (<see cref="SlotMenuMode.NewCampaign"/>). Saving by hand lists nothing: SAVE
/// GAME saves the campaign's own slot (the user's ruling of 2026-09-25 on the save
/// slots, R5). Every string comes from here and is tested (<c>TitleTests</c>); the
/// drawing is the screens' own.
/// <list type="bullet">
/// <item><strong>A slot is up to two rows, at the list's one scale</strong>
/// (<see cref="Scale"/>): its own — the label, then where the campaign stands (the
/// dungeon's name and floor, or <see cref="BetweenRuns"/>), the runs counted and
/// the time it was saved, in the player's own zone — or <see cref="Empty"/>, or
/// <see cref="CannotBeRead"/>; and under it, only when one applies, its markers:
/// what a load of it would do that the row cannot say. One row of both would run
/// past a 1280-pixel window at any scale the list can be read at.</item>
/// <item><strong>Every ask is two rows</strong> — what it does, then what the keys
/// do — and only ENTER says yes, as each second row says: NEW CAMPAIGN's, over a
/// slot in use (<see cref="TitleText.NewCampaignAsk"/>).</item>
/// <item><strong>Nothing drawn is taken on trust from the file</strong> (R3): a
/// dungeon is named by the content's own name for its id — an id the content does
/// not have is <see cref="UnknownDungeon"/>, never the raw text — and a time that
/// cannot be shown in the player's zone is shown in UTC, so a hand-edited save
/// draws in the HUD's font and never throws.</item>
/// </list>
/// </summary>
internal static class SlotText
{
    /// <summary>The list's one scale, the title's two lists alike: every slot row and marker row is drawn at it.</summary>
    internal const float Scale = 1.4f;

    /// <summary>A slot that holds nothing.</summary>
    internal const string Empty = "EMPTY";

    /// <summary>A slot that holds files none of which reads: a load would set them aside and bring nothing back.</summary>
    internal const string CannotBeRead = "CANNOT BE READ";

    /// <summary>Where a campaign between its runs stands.</summary>
    internal const string BetweenRuns = "BETWEEN RUNS";

    /// <summary>A run in progress in a dungeon the content does not have (a load refuses such a save): its id is raw text, and never drawn.</summary>
    internal const string UnknownDungeon = "AN UNKNOWN DUNGEON";

    /// <summary>The marker of a slot whose save cannot be read and whose backup can: the row is the backup's, which a load brings back.</summary>
    internal const string FromItsBackup = "FROM ITS BACKUP";

    /// <summary>The marker of a slot whose resume marker stands: the last load of it never finished, and the next sets the save it guarded aside as crashed.</summary>
    internal const string LastLoadDidNotFinish = "ITS LAST LOAD DID NOT FINISH";

    /// <summary>The marker of a time more than a day ahead of this computer's clock: shown, and counted as no time when the most recent save is chosen.</summary>
    internal const string AheadOfThisClock = "AHEAD OF THIS CLOCK";

    /// <summary>The marker of a save that records no time: one from before the slots, the oldest of all to CONTINUE.</summary>
    internal const string NoTimeRecorded = "NO TIME RECORDED";

    /// <summary>What the load list says when ENTER finds nothing to load: every slot is empty.</summary>
    internal const string NothingToLoad = "EVERY SLOT IS EMPTY";

    /// <summary>How the parts of a row, and the markers of a markers' row, are joined.</summary>
    private const string Joint = " - ";

    /// <summary>The time format a row shows: to the minute, in the player's own zone.</summary>
    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// The rows <paramref name="entry"/> is drawn as: its own row
    /// (<see cref="Row"/>), then — only when one applies — its markers' row, the
    /// markers in this order: <see cref="FromItsBackup"/>,
    /// <see cref="LastLoadDidNotFinish"/>, <see cref="AheadOfThisClock"/> or
    /// <see cref="NoTimeRecorded"/>, and <see cref="Beyond"/>. An empty slot has
    /// no markers: nothing of it would load.
    /// </summary>
    /// <param name="entry">The slot, as the listing read it.</param>
    /// <param name="count">How many manual slots there are (<c>tuning.json</c>'s <c>SaveSlots</c>), which a slot beyond it names.</param>
    /// <param name="dungeons">The content's dungeons, whose names the row reads.</param>
    /// <param name="zone">The player's time zone, which the time is shown in.</param>
    internal static IReadOnlyList<string> Rows(SlotEntry entry, int count, DungeonCatalogue dungeons, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var rows = new List<string> { Row(entry, dungeons, zone) };
        var markers = Markers(entry, count);
        if (markers.Count > 0)
            rows.Add(string.Join(Joint, markers));
        return rows;
    }

    /// <summary>
    /// A slot's own row: <c>SLOT 2 - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32</c>,
    /// the time left off when the save records none; <c>SLOT 2 - EMPTY</c>;
    /// <c>SLOT 2 - CANNOT BE READ</c>. The CONTINUE option is this row of the most
    /// recent save (<see cref="TitleText.Continue"/>).
    /// </summary>
    internal static string Row(SlotEntry entry, DungeonCatalogue dungeons, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string row = $"{SaveSlots.Label(entry.Number)}{Joint}{Summary(entry, dungeons)}";
        return entry.Header?.SavedAt is { } at ? $"{row}{Joint}{When(at, zone)}" : row;
    }

    /// <summary>
    /// What a slot holds, in the words an ask names it by: where its campaign
    /// stands and the runs counted — <c>THE SPEAR HALL FLOOR 4 - 3 RUNS</c>,
    /// <c>BETWEEN RUNS - 1 RUN</c> — or <see cref="Empty"/>, or
    /// <see cref="CannotBeRead"/>.
    /// </summary>
    internal static string Summary(SlotEntry entry, DungeonCatalogue dungeons)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Header is not { } header)
            return entry.State == SlotState.Empty ? Empty : CannotBeRead;
        return $"{Place(entry, dungeons)}{Joint}{DungeonHud.Runs(header.RunCounter)}";
    }

    /// <summary>
    /// Where a slot's campaign stands: mid-run the dungeon's name — the content's,
    /// by the saved id — and the floor the party stands on
    /// (<c>THE SPEAR HALL FLOOR 4</c>); between runs <see cref="BetweenRuns"/>; and
    /// <see cref="Empty"/> or <see cref="CannotBeRead"/> for a slot with nothing
    /// that reads. A dungeon the content does not have is
    /// <see cref="UnknownDungeon"/>, and a floor the header could not place is left
    /// off: both are raw fields, and the load refuses such a save.
    /// </summary>
    internal static string Place(SlotEntry entry, DungeonCatalogue dungeons)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(dungeons);
        if (entry.Header is not { } header)
            return entry.State == SlotState.Empty ? Empty : CannotBeRead;
        if (header.DungeonId is not { } id)
            return BetweenRuns;
        string name = dungeons.TryGet(id, out var def) ? def.Name.ToUpperInvariant() : UnknownDungeon;
        return header.Floor is { } floor ? $"{name} FLOOR {floor.ToString(CultureInfo.InvariantCulture)}" : name;
    }

    /// <summary>
    /// When a save was written, to the minute, in <paramref name="zone"/> —
    /// <c>2026-09-25 14:32</c> — or <see cref="NoTimeRecorded"/> for none. A time
    /// the zone cannot hold (a hand edit at the very ends of the calendar) is shown
    /// in UTC rather than throw.
    /// </summary>
    internal static string When(DateTimeOffset? at, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (at is not { } time)
            return NoTimeRecorded;
        DateTimeOffset shown;
        try
        {
            shown = TimeZoneInfo.ConvertTime(time, zone);
        }
        catch (ArgumentException)
        {
            shown = time.ToUniversalTime();
        }
        return shown.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A slot above the count: kept, listed, loaded, and saved into by the campaign
    /// played from it, since loading a slot is choosing it (the user's ruling of
    /// 2026-09-25 on the save slots, R5) — <c>BEYOND THE 3 SLOTS</c>, or <c>BEYOND
    /// THE 1 SLOT</c>. NEW CAMPAIGN never offers it.
    /// </summary>
    internal static string Beyond(int count)
        => count == 1 ? "BEYOND THE 1 SLOT" : $"BEYOND THE {count.ToString(CultureInfo.InvariantCulture)} SLOTS";

    /// <summary>What a digit or ENTER on a slot that holds nothing says: <c>SLOT 2 IS EMPTY</c>, <c>AUTOSAVE IS EMPTY</c>.</summary>
    internal static string IsEmpty(int number) => $"{SaveSlots.Label(number)} IS EMPTY";

    /// <summary>What a digit above the count says on NEW CAMPAIGN's list, which never offers such a slot: <c>SLOT 7 IS BEYOND THE 3 SLOTS</c>, <c>... THE 1 SLOT</c>.</summary>
    internal static string NotOffered(int number, int count)
        => count == 1
            ? $"{SaveSlots.Label(number)} IS BEYOND THE 1 SLOT"
            : $"{SaveSlots.Label(number)} IS BEYOND THE {count.ToString(CultureInfo.InvariantCulture)} SLOTS";

    /// <summary>The markers that apply to <paramref name="entry"/>, in the order they are drawn.</summary>
    private static List<string> Markers(SlotEntry entry, int count)
    {
        var markers = new List<string>();
        if (entry.State == SlotState.Empty)
            return markers;
        if (entry.State == SlotState.FromBackup)
            markers.Add(FromItsBackup);
        if (entry.Unfinished)
            markers.Add(LastLoadDidNotFinish);
        if (entry.Header is { SavedAt: null })
            markers.Add(NoTimeRecorded);
        else if (entry.Header is not null && entry.AheadOfClock)
            markers.Add(AheadOfThisClock);
        if (entry.Beyond)
            markers.Add(Beyond(count));
        return markers;
    }
}
