using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;
using Run = GameEngine.DistanceRPG.DungeonHud.Run;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The between-runs screen's text (§4.5, §6.7), drawn with the engine's bitmap
/// <see cref="TextRenderer"/> as the dungeon's HUD is: the last run's summary,
/// the hub's dungeon list under it, and the keys that choose from it.
/// <list type="bullet">
/// <item><strong>The list is the campaign's</strong>
/// (<see cref="CampaignState.Listings"/>), in the content's file order. Before
/// the tutorial is beaten it is the tutorial alone, with its warning under it:
/// the one dungeon the game is allowed to warn you about (§4.3), told before it
/// is committed to (<see cref="TutorialWarning"/>). After, it is every dungeon
/// left, each named with its theme. A locked one counts down in runs rather than
/// vanishing (§4.5), and with every one locked, the one offered early says so.</item>
/// <item><strong>Never the depth.</strong> The boss floor is rolled at each entry
/// and learned by reaching it (§4.3), so no line says how deep a dungeon goes —
/// not a range, and not even the tutorial's fixed two.</item>
/// <item><strong>The summary is the last run's own</strong>
/// (<see cref="SummaryLines"/>): the facts its end showed over the last floor,
/// less that panel's key, since here ENTER descends.</item>
/// </list>
/// Every string it draws comes from an <c>internal static</c> member and is
/// tested (<c>BetweenRunsHudTests</c>), and so is where the rows go
/// (<see cref="Layout"/>); only the drawing itself is not.
/// </summary>
public sealed class BetweenRunsHud : IDisposable
{
    /// <summary>Pixels a character advances at scale 1: the 5-px glyph and its 1-px gap.</summary>
    private const float GlyphAdvance = 6f;

    /// <summary>The screen's name, across the top.</summary>
    internal const string Title = "BETWEEN RUNS";

    /// <summary>Over the last run's summary.</summary>
    internal const string LastRunHeading = "THE LAST RUN";

    /// <summary>Over the dungeon list.</summary>
    internal const string ChooseHeading = "CHOOSE A DUNGEON";

    /// <summary>
    /// The keys the screen answers, along the bottom: W/S or the arrows move the
    /// choice, ENTER descends into it, ESC quits the game. This is the one screen
    /// where ESC quits outright. A run's end never offers it (<see cref="DungeonHud.ContinueHint"/>),
    /// and in a dungeon it opens the pause menu.
    /// </summary>
    internal const string KeysLine = "W/S OR ARROWS CHOOSE - ENTER DESCENDS - ESC QUITS";

    /// <summary>What the list says when nothing is left to enter: content whose only dungeon is the tutorial, beaten.</summary>
    internal const string NothingLeft = "NO DUNGEON IS LEFT TO ENTER";

    private readonly Game _game;
    private readonly TextRenderer _text = new();
    private bool _glReady;

    public BetweenRunsHud(Game game)
    {
        _game = game;
    }

    // ── What the list says ───────────────────────────────────────────────────

    /// <summary>
    /// One dungeon as the list names it: its name and the theme it is themed on
    /// (§4.3), which is why anyone picks one dungeon over another. Then, for a
    /// dungeon that cannot be entered yet, its countdown in runs that count (§4.5):
    /// <c>THE SPEAR HALL - THEMED ON BRACE - LOCKED - AVAILABLE IN 4 RUNS</c>. A
    /// countdown and never a gap in the list. And for the early offer, why it can
    /// be entered anyway. Nothing about depth, and so no number except a countdown.
    /// </summary>
    internal static string ListingLine(DungeonListing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        string line = $"{listing.Def.Name.ToUpperInvariant()} - THEMED ON {DungeonHud.ModifierName(listing.Def.Theme)}";
        if (listing.Early)
            return $"{line} - OFFERED EARLY - EVERY DUNGEON IS LOCKED";
        if (!listing.Selectable)
            return listing.RunsUntilAvailable > 0
                ? $"{line} - LOCKED - AVAILABLE IN {DungeonHud.Runs(listing.RunsUntilAvailable)}"
                : $"{line} - LOCKED";
        return line;
    }

    /// <summary>
    /// What the list says under the dungeon that is gone once it is beaten — the
    /// tutorial (§4.3: "the one dungeon the game is allowed to warn you about").
    /// A first-time player who loses the theme without knowing the rule has been
    /// cheated rather than taught, so it is said plainly, before the run is
    /// committed to: <c>BEATING THE STONE GOLEM CLOSES IT FOR GOOD - AND ITS BLOCK
    /// FARM WITH IT</c>. The boss and the theme are the dungeon's own, read off its
    /// row, so the warning cannot name a boss or a farm the dungeon does not have.
    /// </summary>
    /// <param name="dungeon">The dungeon that retires on its clear.</param>
    internal static string TutorialWarning(DungeonDef dungeon)
    {
        ArgumentNullException.ThrowIfNull(dungeon);
        return $"BEATING THE {dungeon.Boss.Name.ToUpperInvariant()} CLOSES IT FOR GOOD - AND ITS {DungeonHud.ModifierName(dungeon.Theme)} FARM WITH IT";
    }

    /// <summary>
    /// What the list says while the tutorial is all it offers (§4.3): the rest of
    /// the dungeons are behind it, and beating it is what opens them. That is why
    /// the list is one line long, and it is not a lock.
    /// </summary>
    internal static string HubGate(DungeonDef tutorial)
    {
        ArgumentNullException.ThrowIfNull(tutorial);
        return $"EVERY OTHER DUNGEON OPENS WHEN {tutorial.Name.ToUpperInvariant()} IS BEATEN";
    }

    /// <summary>
    /// What the list says under a countdown (§4.3, §6.3): a lock counts runs that
    /// count, the clock the enchanter will tick on too, and not entries. So a
    /// shallow retreat brings a locked dungeon no closer, and the rule is on
    /// screen next to the number it moves.
    /// </summary>
    internal static string LockRule(int mercyFloor)
        => $"A LOCK COUNTS DOWN ON RUNS THAT COUNT - A CLEAR, OR A FAILED RUN FROM FLOOR {mercyFloor}";

    // ── The last run ─────────────────────────────────────────────────────────

    /// <summary>How many characters a summary line runs to before a list on it wraps: a little over half the shipped window at the dungeon's summary scale.</summary>
    private const int SummaryLineChars = 64;

    /// <summary>
    /// A run's summary (§4.4), a line a fact, the headline first. It says what
    /// the run was — <c>CLEARED</c>, <c>RETREATED</c>, <c>DEFEATED</c> — and where
    /// and how deep it went. Then the boss, with its floor only where the party
    /// reached it (§4.3). Then the spoils kept or lost, by name, and the XP kept or
    /// what the wipe clawed back, member by member. Then whether the run counted
    /// and the counter it stands at (§6.3), and the lock or the closing a clear
    /// wrote. These are the run's own numbers, off its <see cref="RunResult"/> and
    /// nothing else. The dungeon shows them at the run's end, with its key under
    /// them (<see cref="DungeonHud.RunSummaryLines"/>). The between-runs screen
    /// shows them again above the list, where they are the context the next
    /// choice is made in.
    /// </summary>
    /// <param name="result">The run's end, as the campaign returned it.</param>
    internal static IReadOnlyList<Run> SummaryLines(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var content = GameContent.Current;
        var def = content.Dungeons.TryGet(result.DungeonId, out var found) ? found : null;
        string dungeon = (def?.Name ?? result.DungeonId).ToUpperInvariant();
        string boss = def?.Boss.Name.ToUpperInvariant() ?? "BOSS";

        var lines = new List<Run>
        {
            result.Outcome switch
            {
                RunOutcome.BossKill => new Run("CLEARED", DungeonHud.Green),
                RunOutcome.Retreat => new Run("RETREATED", DungeonHud.Yellow),
                _ => new Run(DungeonHud.DefeatHeadline, DungeonHud.Red),
            },
            new Run($"{dungeon} - DEEPEST FLOOR {result.DeepestFloor}", DungeonHud.Grey),
        };

        // The boss, and the depth only where the party stood on it (§4.3).
        if (result.BossDefeated)
            lines.Add(new Run(result.BossFloorRevealed is { } fell ? $"THE {boss} FELL ON FLOOR {fell}" : $"THE {boss} FELL", DungeonHud.Green));
        else if (result.BossFloorRevealed is { } waited)
            lines.Add(new Run($"THE {boss} WAITED ON FLOOR {waited} - THE DEPTH RE-ROLLS NEXT TIME", DungeonHud.Grey));
        else
            lines.Add(new Run($"THE {boss} STILL STANDS - THE DEPTH RE-ROLLS NEXT TIME", DungeonHud.Grey));

        // What was carried home, or lost with the party.
        if (result.SpoilsKept.Count > 0)
            lines.AddRange(Wrapped("SPOILS KEPT:", result.SpoilsKept.Select(w => w.Name.ToUpperInvariant()), DungeonHud.Orange));
        if (result.SpoilsLost.Count > 0)
            lines.AddRange(Wrapped("SPOILS LOST:", result.SpoilsLost.Select(w => w.Name.ToUpperInvariant()), DungeonHud.Red));
        if (result.SpoilsKept.Count == 0 && result.SpoilsLost.Count == 0)
            lines.Add(new Run(result.BossDefeated ? "NO SPOILS CARRIED" : "NO SPOILS - NOTHING DROPS WHILE THE BOSS STANDS", DungeonHud.Grey));

        // The XP: kept, or the share of each pool's gain a wipe took back, member by member.
        if (result.Outcome != RunOutcome.Death)
            lines.Add(new Run("XP KEPT", DungeonHud.Green));
        else if (result.Clawbacks.Count == 0)
            lines.Add(new Run("XP KEPT - NOTHING CLAWED BACK", DungeonHud.Green));   // nothing gained, or a share of 0
        else
        {
            lines.Add(new Run("XP CLAWED BACK:", DungeonHud.Red));
            foreach (var member in result.Clawbacks.GroupBy(c => c.MemberId))
                lines.AddRange(Wrapped($"{member.Key}:", member.Select(c => $"{c.Pool.ToUpperInvariant()} -{c.Before - c.After}"), DungeonHud.Red));
        }

        // Whether the run counted (§6.3), and where the counter stands.
        string counted = result.ServiceTick
            ? result.MercyTick ? "RUN COUNTED - A MERCY FOR FAILING DEEP" : "RUN COUNTED"
            : $"RUN NOT COUNTED - A FAILED RUN COUNTS FROM FLOOR {content.Tuning.MercyFloor}";
        lines.Add(new Run($"{counted} - {DungeonHud.Runs(result.RunCounter)} SO FAR", DungeonHud.Cyan));

        // What the clear did to the dungeon.
        if (result.Retired)
            lines.Add(new Run($"{dungeon} IS CLOSED FOR GOOD", DungeonHud.Yellow));
        else if (result.LockedUntilRun is { } until)
            lines.Add(new Run($"{dungeon} IS LOCKED FOR {DungeonHud.Runs(until - result.RunCounter)}", DungeonHud.Yellow));

        return lines;
    }

    /// <summary>A list under a heading, as many summary lines as it takes: the heading and the items on the first, the rest indented under it, comma-separated.</summary>
    private static IEnumerable<Run> Wrapped(string heading, IEnumerable<string> items, Vector4 color)
    {
        var line = new System.Text.StringBuilder(heading);
        bool first = true;
        foreach (string item in items)
        {
            string piece = first ? $" {item}" : $", {item}";
            if (!first && line.Length + piece.Length > SummaryLineChars)
            {
                yield return new Run(line.Append(',').ToString(), color);
                line.Clear().Append("  ").Append(item);
            }
            else
            {
                line.Append(piece);
            }
            first = false;
        }
        yield return new Run(line.ToString(), color);
    }

    // ── The list, row by row ─────────────────────────────────────────────────

    /// <summary>One row of the list as the screen draws it: the text, its colour, its scale, and the height it takes before the next row.</summary>
    internal readonly record struct ListRow(string Text, Vector4 Color, float Scale, float Pitch);

    // A listing's row, and the smaller rows said under it or under the list.
    private const float ListingScale = 1.8f;
    private const float ListingPitch = 26f;
    private const float NoteScale = 1.4f;
    private const float NotePitch = 20f;
    private const float HeadingScale = 1.5f;
    private const float HeadingPitch = 26f;

    /// <summary>
    /// The list block, top to bottom, as the screen draws it (§4.5). It starts
    /// with its heading. Then comes each listing (<see cref="ListingLine"/>), in
    /// the campaign's order. The one chosen now is marked <c>&gt; ... &lt;</c>, a
    /// locked one is dimmed, and the early offer is picked out. Right under the
    /// tutorial comes its warning (<see cref="TutorialWarning"/>): the list is
    /// where a run is committed to, so the warning is there. While the tutorial
    /// is all there is, the list says the rest opens behind it
    /// (<see cref="HubGate"/>). Under any countdown it says what a countdown
    /// counts (<see cref="LockRule"/>).
    /// </summary>
    /// <param name="listings">The campaign's list (<see cref="CampaignState.Listings"/>).</param>
    /// <param name="chosen">The index of the listing ENTER would descend into, or -1.</param>
    internal static IReadOnlyList<ListRow> ListRows(IReadOnlyList<DungeonListing> listings, int chosen)
    {
        ArgumentNullException.ThrowIfNull(listings);
        var rows = new List<ListRow> { new(ChooseHeading, DungeonHud.Grey, HeadingScale, HeadingPitch) };
        if (listings.Count == 0)
        {
            rows.Add(new ListRow(NothingLeft, DungeonHud.Grey, ListingScale, ListingPitch));
            return rows;
        }

        for (int i = 0; i < listings.Count; i++)
        {
            var listing = listings[i];
            string line = ListingLine(listing);
            var color = i == chosen ? DungeonHud.Yellow
                : !listing.Selectable ? DungeonHud.DarkGrey
                : listing.Early ? DungeonHud.Orange
                : DungeonHud.White;
            rows.Add(new ListRow(i == chosen ? $"> {line} <" : line, color, ListingScale, ListingPitch));
            if (listing.Def.RetiresOnClear)
                rows.Add(new ListRow(TutorialWarning(listing.Def), DungeonHud.Orange, NoteScale, NotePitch));
        }

        var content = GameContent.Current;
        if (listings is [{ Def.RetiresOnClear: true } only] && content.Dungeons.All.Count > 1)
            rows.Add(new ListRow(HubGate(only.Def), DungeonHud.Grey, NoteScale, NotePitch));
        if (listings.Any(listing => !listing.Selectable))
            rows.Add(new ListRow(LockRule(content.Tuning.MercyFloor), DungeonHud.Grey, NoteScale, NotePitch));
        return rows;
    }

    // ── Where it all goes ────────────────────────────────────────────────────

    private const float TitleY = 36f;
    private const float TitleScale = 4f;

    /// <summary>The last run's heading, its headline under it, and the rest of its lines from the first row down.</summary>
    private const float LastRunHeadingY = 88f;
    private const float HeadlineY = 106f;
    private const float HeadlineScale = 2.6f;
    private const float SummaryFirstRowY = 134f;
    private const float SummaryScale = 1.4f;
    private const float SummaryPitch = 17f;

    /// <summary>A summary row's own glyphs at <see cref="SummaryScale"/>, so the gap under the last row is measured from its bottom and not its top.</summary>
    private const float SummaryRowHeight = 10f;

    /// <summary>The keys line's distance up from the bottom, and the clearance the list keeps above it.</summary>
    private const float KeysLineFromBottom = 28f;
    internal const float ListBottomMargin = 44f;

    /// <summary>The gap between the last summary row and the list's heading.</summary>
    private const float ListGap = 22f;

    /// <summary>How far down the window the list starts when there is no last run to show above it.</summary>
    private const float ListTopShareAlone = 0.3f;

    /// <summary>Where the list starts, and how many of the summary's lines under the headline there is room for.</summary>
    internal readonly record struct ScreenLayout(float ListTop, int SummaryRows);

    /// <summary>
    /// Where the rows go in a window <paramref name="windowHeight"/> pixels tall.
    /// The list block is <paramref name="listHeight"/> tall. The last run's
    /// summary has <paramref name="summaryLines"/> lines under its headline (0
    /// before any run). The list follows the summary a gap below it.
    /// <strong>The list is never pushed off the screen</strong>, because it is what
    /// the screen is for. When the summary is too long to leave it room above the
    /// keys line, the list starts as low as it can and the summary shows as many
    /// lines as fit, the last of them <c>...</c>. That only happens after a wipe
    /// that claws back from every member and loses a full haul, and the dungeon's
    /// own summary showed all of it. With no run to show yet, the list sits in the
    /// upper third, so the upper half is not left empty.
    /// </summary>
    internal static ScreenLayout Layout(float windowHeight, int summaryLines, float listHeight)
    {
        float lowest = windowHeight - ListBottomMargin - listHeight;   // the lowest the list may start and still clear the keys line
        if (summaryLines <= 0)
            return new ScreenLayout(MathF.Max(HeadlineY, MathF.Min(lowest, windowHeight * ListTopShareAlone)), 0);

        float underTheSummary = SummaryFirstRowY + (summaryLines - 1) * SummaryPitch + SummaryRowHeight + ListGap;
        if (underTheSummary <= lowest)
            return new ScreenLayout(underTheSummary, summaryLines);

        float listTop = MathF.Max(lowest, SummaryFirstRowY + SummaryRowHeight + ListGap);
        int room = (int)MathF.Floor((listTop - ListGap - SummaryRowHeight - SummaryFirstRowY) / SummaryPitch) + 1;
        return new ScreenLayout(listTop, Math.Clamp(room, 0, summaryLines));
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The screen: its title, the last run above (when there has been one), the
    /// list under it with the choice marked, and the keys along the bottom.
    /// </summary>
    public void Render(BetweenRunsScene scene)
    {
        if (!_glReady)
        {
            _text.Initialize();
            _glReady = true;
        }

        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;

        DrawCentered(w, TitleY, Title, TitleScale, DungeonHud.Cyan);

        var rows = ListRows(scene.Listings, scene.Chosen);
        IReadOnlyList<Run> summary = scene.LastResult is { } lastRun ? SummaryLines(lastRun) : [];
        var layout = Layout(h, Math.Max(0, summary.Count - 1), rows.Sum(row => row.Pitch));

        if (summary.Count > 0)
        {
            DrawCentered(w, LastRunHeadingY, LastRunHeading, SummaryScale, DungeonHud.Grey);
            DrawCentered(w, HeadlineY, summary[0].Text, HeadlineScale, summary[0].Color);
            float y = SummaryFirstRowY;
            int shown = layout.SummaryRows;
            bool cut = shown < summary.Count - 1;
            for (int i = 0; i < shown; i++)
            {
                var line = cut && i == shown - 1 ? new Run("...", DungeonHud.Grey) : summary[i + 1];   // cut short: the last row that fits says so
                DrawCentered(w, y, line.Text, SummaryScale, line.Color);
                y += SummaryPitch;
            }
        }

        float rowY = layout.ListTop;
        foreach (var row in rows)
        {
            DrawCentered(w, rowY, row.Text, row.Scale, row.Color);
            rowY += row.Pitch;
        }

        DrawCentered(w, h - KeysLineFromBottom, KeysLine, 1.3f, DungeonHud.Grey);
        _text.Flush(_game.ShaderManager);
    }

    private void DrawCentered(int viewportW, float y, string text, float scale, Vector4 color)
    {
        float width = text.Length * GlyphAdvance * scale;
        _text.DrawText(text, viewportW / 2f - width / 2f, y, scale, color);
    }

    public void Dispose() => _text.Dispose();
}
