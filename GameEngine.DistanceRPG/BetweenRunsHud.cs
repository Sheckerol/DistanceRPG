using GameEngine.Core;
using GameEngine.Core.UI;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;
using Run = GameEngine.DistanceRPG.DungeonHud.Run;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The between-runs screen's text (§4.5, §6.2, §6.7), drawn with the engine's
/// bitmap <see cref="TextRenderer"/> as the dungeon's HUD is: on the DUNGEONS page
/// the last run's summary, the hub's dungeon list under it, and the keys that
/// choose from it; on the WEAPONS page (Phase 6a.2) the party's hands and bags,
/// the stable and the enchanter's shop down the left (<see cref="HubWeapons"/>), a window that scrolls with
/// the choice, the chosen weapon said whole on the right (<see cref="DetailRows"/>),
/// and a refused put-down over the keys — and, under
/// the title, the notices the game hands it (<see cref="NoticeText"/>): on the
/// first one a campaign opens on, the save's word on it, shown whole
/// (<see cref="Resumed"/>, the numbers changed since it was saved, a save set
/// aside and where), and then anything else, capped; on a later one, a save that
/// could not be written, and one of the hub's own that could not, at once. While
/// the menu ESC opens is up, the hub dims under it and
/// the menu is drawn over it as the dungeon's pause menu is
/// (<see cref="DungeonHud.PauseMenuLayout"/>) — its options, the slot the campaign
/// saves into, and what a save by hand came to — and the keys line along the
/// bottom gives way to the menu's (<see cref="BottomLine"/>).
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
/// less that panel's key, since here ENTER descends — and where the enchanter's
/// lines were cut short, the key that shows the rest (<see cref="HubSummaryLines"/>).</item>
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
    /// The keys the DUNGEONS page answers, along the bottom: W/S or the arrows move
    /// the choice, ENTER descends into it, TAB turns to the WEAPONS page, ESC opens
    /// the menu — the dungeon's pause menu, over the hub, where the campaign is
    /// saved by hand and play is left for the title (the user's ruling of
    /// 2026-09-25 on the save model, R2(d), R2(e)). NEW CAMPAIGN is the title's
    /// (R2(e)), so no key here begins one.
    /// </summary>
    internal const string KeysLine = "W/S OR ARROWS CHOOSE - ENTER DESCENDS - TAB WEAPONS - ESC MENU";

    /// <summary>The keys the WEAPONS page answers while browsing (§6.2, Phase 6a.2): W/S or the arrows move the choice, ENTER takes the weapon chosen up, TAB turns back to DUNGEONS, ESC opens the menu.</summary>
    internal const string WeaponsKeysLine = "W/S OR ARROWS CHOOSE - ENTER TAKES IT UP - TAB DUNGEONS - ESC MENU";

    /// <summary>
    /// The keys line with a weapon taken up, which names it: <c>CARRYING THE
    /// ARMING SWORD - W/S CHOOSE WHERE - ENTER PUTS IT DOWN - ESC PUTS IT BACK</c>.
    /// TAB is not named: it does nothing while a weapon is carried.
    /// </summary>
    internal static string CarryingKeysLine(Weapon carried) => $"CARRYING {HubWeapons.The(carried)} - W/S CHOOSE WHERE - ENTER PUTS IT DOWN - ESC PUTS IT BACK";

    /// <summary>How the menu's keys line ends, after its options: ESC closes the menu.</summary>
    private const string EscCloses = "ESC CLOSES";

    /// <summary>
    /// The keys line while the menu is up, in the hub's keys' place: the menu's
    /// options, each led by its key, then ESC — <c>1  CLOSE - 2  SAVE GAME - 3  QUIT TO
    /// TITLE - ESC CLOSES</c>, or <c>3  QUIT GAME</c> in a dev-override session.
    /// </summary>
    /// <param name="hasTitle">Whether the session has a title to quit to (<see cref="ISaveHost.HasTitle"/>).</param>
    internal static string MenuKeysLine(bool hasTitle) => string.Join(" - ", [.. PauseMenu.Options(hasTitle), EscCloses]);

    /// <summary>Under the title of the screen a loaded campaign opens on between runs, first among the notices (1f): the word of every load, <c>CAMPAIGN RESUMED FROM SLOT 2</c> and the rest, starts with it, and so is green.</summary>
    internal const string Resumed = "CAMPAIGN RESUMED";

    /// <summary>What the list says when nothing is left to enter: content whose only dungeon is the tutorial, beaten.</summary>
    internal const string NothingLeft = "NO DUNGEON IS LEFT TO ENTER";

    private readonly Game _game;
    private readonly TextRenderer _text = new();
    private readonly FullscreenFade _fade = new();
    private bool _glReady;

    /// <summary>How far the hub dims under the menu: further than the dungeon's floor does under its pause menu (0.65), since the hub is text on black, and its rows would otherwise read through the menu's.</summary>
    private const float MenuDim = 0.88f;

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

    /// <summary>How many of the enchanter's finished and worked services the hub's summary names before it says how many more (<see cref="MoreAtTheEnchanter"/>): the hub cuts a long summary short, so it can name more than the dungeon's end panel, which cannot.</summary>
    internal const int HubServiceLines = 3;

    /// <summary>How many weapons the summary's stalled line names before it says how many more.</summary>
    internal const int MostStalledNames = 3;

    /// <summary>What the summary says when a run that did not count ends with weapons in the shop (§6.3): the enchanter works only on a run that counts.</summary>
    internal const string WorkedNothing = "THE ENCHANTER WORKED NOTHING - THE RUN DID NOT COUNT";

    /// <summary>The head of the summary's line of stalled weapons, the names after it.</summary>
    internal const string StalledHeading = "STALLED AT THE ENCHANTER:";

    /// <summary>What the hub alone appends to <see cref="MoreAtTheEnchanter"/>: the page that shows the rest. The dungeon's end panel shares the summary, and TAB does nothing there.</summary>
    internal const string TabWeapons = " - TAB WEAPONS";

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
    /// <para>
    /// <strong>And the enchanter</strong> (§6.2, §6.3; Phase 6a.3a), after whether
    /// the run counted, when anything was in the shop: each service the run
    /// finished, back in the stable with what it attached, and each it worked a run
    /// on, at most <paramref name="mostServiceLines"/> of them and then how many
    /// more (<see cref="MoreAtTheEnchanter"/>); then the weapons it left stalled,
    /// wrapped as the spoils are, at most <see cref="MostStalledNames"/> named. A
    /// run that did not count worked nothing, and says so. It names no key: the
    /// dungeon's end panel shares these lines, and the hub adds its own
    /// (<see cref="HubSummaryLines"/>).
    /// </para>
    /// </summary>
    /// <param name="result">The run's end, as the campaign returned it.</param>
    /// <param name="mostServiceLines">How many finished and worked services to name before saying how many more: the hub's <see cref="HubServiceLines"/>, the dungeon's end panel fewer.</param>
    internal static IReadOnlyList<Run> SummaryLines(RunResult result, int mostServiceLines = HubServiceLines)
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

        // The boss, and the depth only where the party stood on it (§4.3) — and
        // the re-roll only where there is one: the tutorial is two floors deep on
        // every visit, so its standing golem promises no new depth next time.
        string reroll = def is not null && content.Dungeons.HasFixedDepth(def) ? "" : " - THE DEPTH RE-ROLLS NEXT TIME";
        if (result.BossDefeated)
            lines.Add(new Run(result.BossFloorRevealed is { } fell ? $"THE {boss} FELL ON FLOOR {fell}" : $"THE {boss} FELL", DungeonHud.Green));
        else if (result.BossFloorRevealed is { } waited)
            lines.Add(new Run($"THE {boss} WAITED ON FLOOR {waited}{reroll}", DungeonHud.Grey));
        else
            lines.Add(new Run($"THE {boss} STILL STANDS{reroll}", DungeonHud.Grey));

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

        // Whether the run counted (§6.3), and where the counter stands. The rule
        // for why not is said only where going deeper could have met it: in a
        // dungeon that never reaches the mercy floor — the tutorial's two floors
        // against its 3 — a failed run never counts, and pointing at a floor 3 it
        // does not have would send the player looking for one.
        bool mercyReachable = def is null || content.Dungeons.BossFloorRange(def).Max >= content.Tuning.MercyFloor;
        string counted = result.ServiceTick
            ? result.MercyTick ? "RUN COUNTED - A MERCY FOR FAILING DEEP" : "RUN COUNTED"
            : mercyReachable ? $"RUN NOT COUNTED - A FAILED RUN COUNTS FROM FLOOR {content.Tuning.MercyFloor}" : "RUN NOT COUNTED";
        lines.Add(new Run($"{counted} - {DungeonHud.Runs(result.RunCounter)} SO FAR", DungeonHud.Cyan));

        // What the enchanter did on the run's end, which the count decided.
        lines.AddRange(ServiceLines(result, mostServiceLines));

        // What the clear did to the dungeon.
        if (result.Retired)
            lines.Add(new Run($"{dungeon} IS CLOSED FOR GOOD", DungeonHud.Yellow));
        else if (result.LockedUntilRun is { } until)
            lines.Add(new Run($"{dungeon} IS LOCKED FOR {DungeonHud.Runs(until - result.RunCounter)}", DungeonHud.Yellow));

        return lines;
    }

    /// <summary>
    /// The summary's lines for the enchanter (§6.2, §6.3), off the run's reports
    /// (<see cref="RunResult.Services"/>), none for an empty shop: on a run that
    /// counted, each finished service (cyan) — <c>WAND OF THE CONE IS BACK FROM THE
    /// ENCHANTER - ATTACHED VAMPIRIC</c>, <c>RIPOSTE BLADE IS BACK FROM THE
    /// ENCHANTER - REFINED</c> — then each worked one — <c>THE ENCHANTER WORKED THE
    /// ROTWOOD - 1 OF 2 RUNS</c> — at most <paramref name="most"/> of those together
    /// and then <c>AND 2 MORE AT THE ENCHANTER</c>; then the stalled weapons in
    /// yellow, wrapped, <c>STALLED AT THE ENCHANTER: RIPOSTE BLADE</c>. On a run
    /// that did not count, the one grey line <see cref="WorkedNothing"/>. A weapon
    /// and an enchantment are named by the content's rows, and by the bare id where
    /// a row is gone, so the summary never throws while it draws.
    /// </summary>
    private static IEnumerable<Run> ServiceLines(RunResult result, int most)
    {
        var reports = result.Services ?? [];
        if (reports.Count == 0)
            yield break;
        if (!result.ServiceTick)
        {
            yield return new Run(WorkedNothing, DungeonHud.Grey);
            yield break;
        }

        var told = reports.Where(report => report.Finished).Concat(reports.Where(report => !report.Stalled && !report.Finished)).ToList();
        foreach (var report in told.Take(Math.Max(0, most)))
            yield return new Run(report.Finished ? BackFromTheEnchanter(report) : WorkedThe(report), DungeonHud.Cyan);
        if (told.Count > most)
            yield return new Run(MoreAtTheEnchanter(told.Count - Math.Max(0, most)), DungeonHud.Cyan);

        var stalled = reports.Where(report => report.Stalled).Select(report => WeaponName(report.WeaponId)).ToList();
        if (stalled.Count > 0)
        {
            var named = stalled.Take(MostStalledNames).ToList();
            if (stalled.Count > MostStalledNames)
                named.Add($"AND {stalled.Count - MostStalledNames} MORE");
            foreach (var line in Wrapped(StalledHeading, named, DungeonHud.Yellow))
                yield return line;
        }
    }

    /// <summary>A finished service's line: <c>WAND OF THE CONE IS BACK FROM THE ENCHANTER - ATTACHED VAMPIRIC</c>, or <c>- REFINED</c>.</summary>
    internal static string BackFromTheEnchanter(ServiceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        string what = report.Service == Service.Enchant && report.EnchantmentId is { } id ? $"ATTACHED {EnchantmentName(id)}" : "REFINED";
        return $"{WeaponName(report.WeaponId)} IS BACK FROM THE ENCHANTER - {what}";
    }

    /// <summary>A worked service's line, not finished yet: <c>THE ENCHANTER WORKED THE ROTWOOD - 1 OF 2 RUNS</c>.</summary>
    internal static string WorkedThe(ServiceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return $"THE ENCHANTER WORKED {HubWeapons.The(WeaponName(report.WeaponId))} - {report.RunsWorked} OF {DungeonHud.Runs(report.Runs)}";
    }

    /// <summary>How many more finished and worked services there are than the summary named: <c>AND 2 MORE AT THE ENCHANTER</c>.</summary>
    internal static string MoreAtTheEnchanter(int more) => $"AND {more} MORE AT THE ENCHANTER";

    /// <summary>
    /// The summary as the hub shows it over its list: <see cref="SummaryLines"/> at
    /// <see cref="HubServiceLines"/>, and the enchanter's line of how many more it did
    /// not name pointing at the page that shows them — <c>AND 2 MORE AT THE
    /// ENCHANTER - TAB WEAPONS</c>. Here, and never in the dungeon's end panel, where
    /// TAB does nothing.
    /// </summary>
    internal static IReadOnlyList<Run> HubSummaryLines(RunResult result)
    {
        var lines = SummaryLines(result, HubServiceLines).ToList();
        for (int i = 0; i < lines.Count; i++)
            if (lines[i].Text.StartsWith("AND ", StringComparison.Ordinal) && lines[i].Text.EndsWith(" MORE AT THE ENCHANTER", StringComparison.Ordinal))
                lines[i] = lines[i] with { Text = lines[i].Text + TabWeapons };
        return lines;
    }

    /// <summary>A weapon by its catalogue id, as the summary names it: the content's row's name, upper-cased — the bare id where the content has none.</summary>
    private static string WeaponName(string id)
        => (GameContent.Current.Weapons.TryGet(id, out var def) ? def.Name : id).ToUpperInvariant();

    /// <summary>An enchantment by its id, as the summary and the shop name it: the content's row's name, upper-cased — the bare id where the content has none.</summary>
    internal static string EnchantmentName(string id)
        => (GameContent.Current.Enchantments.TryGet(id, out var def) ? def.Name : id).ToUpperInvariant();

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

    /// <summary>A glyph's height at scale 1: the font is 5 x 7.</summary>
    private const float GlyphHeight = 7f;

    /// <summary>Where the title's glyphs end: the startup notices start under it.</summary>
    internal const float TitleBottom = TitleY + GlyphHeight * TitleScale;

    /// <summary>
    /// The startup notices (<see cref="NoticeText.Rows"/>) sit under the title, a
    /// row a pitch, and everything below them — the last run and the list — moves
    /// down by as many pitches as there are rows (<see cref="ScreenLayout.Shift"/>).
    /// </summary>
    internal const float NoticeTop = 72f;
    internal const float NoticePitch = 16f;
    private const float NoticeScale = 1.4f;

    /// <summary>The last run's heading, its headline under it, and the rest of its lines from the first row down.</summary>
    internal const float LastRunHeadingY = 88f;
    private const float HeadlineY = 106f;
    private const float HeadlineScale = 2.6f;
    private const float SummaryFirstRowY = 134f;
    private const float SummaryScale = 1.4f;
    private const float SummaryPitch = 17f;

    /// <summary>A summary row's own glyphs at <see cref="SummaryScale"/>, so the gap under the last row is measured from its bottom and not its top.</summary>
    private const float SummaryRowHeight = 10f;

    /// <summary>The keys line's distance up from the bottom, and the clearance the list keeps above it.</summary>
    internal const float KeysLineFromBottom = 28f;
    internal const float ListBottomMargin = 44f;

    /// <summary>The gap between the last summary row and the list's heading.</summary>
    private const float ListGap = 22f;

    /// <summary>How far down the window the list starts when there is no last run to show above it.</summary>
    private const float ListTopShareAlone = 0.3f;

    /// <summary>
    /// Where the list starts, how many of the summary's lines under the headline
    /// there is room for, and how far the startup notices push the last run and
    /// the list down from where they sit without them.
    /// </summary>
    internal readonly record struct ScreenLayout(float ListTop, int SummaryRows, float Shift = 0f);

    /// <summary>How far <paramref name="noticeRows"/> rows of notices push everything under the title down: a pitch a row, none for none.</summary>
    internal static float NoticeShift(int noticeRows) => noticeRows <= 0 ? 0f : noticeRows * NoticePitch;

    /// <summary>
    /// A notice row's colour: the campaign resuming, and the old autosave moved into
    /// a slot of its own (<see cref="CampaignBoot.AutosaveMoved"/>), in green — news,
    /// not a warning — and every other notice in the warning's orange.
    /// </summary>
    internal static Vector4 NoticeColor(string row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.StartsWith(Resumed, StringComparison.Ordinal) || row.StartsWith(CampaignBoot.MovedInto, StringComparison.Ordinal)
            ? DungeonHud.Green
            : DungeonHud.Orange;
    }

    /// <summary>
    /// The line along the bottom, and its colour, in grey: the keys the screen
    /// answers in what it is doing — the DUNGEONS page's (<see cref="KeysLine"/>),
    /// the WEAPONS page's (<see cref="WeaponsKeysLine"/>), a carry's, naming the
    /// weapon (<see cref="CarryingKeysLine"/>) — and while the menu is up, the
    /// menu's (<see cref="MenuKeysLine"/>) in their place.
    /// </summary>
    /// <param name="mode">What the screen is doing.</param>
    /// <param name="hasTitle">Whether the session has a title to quit to.</param>
    /// <param name="carried">The weapon taken up, while carrying.</param>
    internal static (string Text, Vector4 Color) BottomLine(BetweenRunsScene.HubMode mode = BetweenRunsScene.HubMode.Dungeons, bool hasTitle = true, Weapon? carried = null)
        => (mode switch
        {
            BetweenRunsScene.HubMode.Menu => MenuKeysLine(hasTitle),
            BetweenRunsScene.HubMode.Weapons => WeaponsKeysLine,
            BetweenRunsScene.HubMode.Carrying => carried is null ? WeaponsKeysLine : CarryingKeysLine(carried),
            _ => KeysLine,
        }, DungeonHud.Grey);

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
    /// <para>
    /// <paramref name="noticeRows"/> rows of startup notices under the title push
    /// the last run and the list down by their height (<see cref="NoticeShift"/>);
    /// the list still comes first, so the summary is what gives way to them.
    /// </para>
    /// </summary>
    internal static ScreenLayout Layout(float windowHeight, int summaryLines, float listHeight, int noticeRows = 0)
    {
        float shift = NoticeShift(noticeRows);
        float lowest = windowHeight - ListBottomMargin - listHeight;   // the lowest the list may start and still clear the keys line
        if (summaryLines <= 0)
            return new ScreenLayout(MathF.Max(HeadlineY + shift, MathF.Min(lowest, windowHeight * ListTopShareAlone)), 0, shift);

        float firstRow = SummaryFirstRowY + shift;
        float underTheSummary = firstRow + (summaryLines - 1) * SummaryPitch + SummaryRowHeight + ListGap;
        if (underTheSummary <= lowest)
            return new ScreenLayout(underTheSummary, summaryLines, shift);

        float listTop = MathF.Max(lowest, firstRow + SummaryRowHeight + ListGap);
        int room = (int)MathF.Floor((listTop - ListGap - SummaryRowHeight - firstRow) / SummaryPitch) + 1;
        return new ScreenLayout(listTop, Math.Clamp(room, 0, summaryLines), shift);
    }

    // ── The WEAPONS page (§6.2, Phase 6a.2) ─────────────────────────────────

    /// <summary>Where the WEAPONS page's two columns start, from the window's centre: the rows on the left, the chosen weapon's detail on the right — 40 and 660 across the shipped 1280.</summary>
    internal const float LeftColumn = -600f;
    internal const float RightColumn = 20f;

    /// <summary>How wide each column is: nothing drawn in one runs into the other, or off a 1280-wide window.</summary>
    internal const float ColumnWidth = 580f;

    /// <summary>How wide a line across the window may run — the keys, the refusal — with 20 px either side of the shipped 1280.</summary>
    internal const float FullWidth = 1240f;

    /// <summary>A row of the page: its scale and the height it takes before the next.</summary>
    internal const float WeaponRowScale = 1.5f;
    internal const float WeaponRowPitch = 19f;

    /// <summary>The detail panel's lines: where the weapon is, its name, then the rest.</summary>
    internal const float DetailWhereScale = 1.3f;
    private const float DetailWherePitch = 20f;
    internal const float DetailNameScale = 2f;
    private const float DetailNamePitch = 26f;
    internal const float DetailScale = 1.4f;
    private const float DetailPitch = 18f;

    /// <summary>
    /// The refusal row's distance up from the bottom: one row over the keys line,
    /// in the warning's orange — what a put-down was refused with, until the next
    /// key — and the clearance the page's rows keep above it.
    /// </summary>
    internal const float RefusalFromBottom = 50f;
    private const float RefusalClearance = 6f;

    /// <summary>The top of the WEAPONS page under the notices, the lowest a row's glyphs may reach over the refusal row, and how many rows fit between.</summary>
    internal readonly record struct WeaponsLayout(float Top, float Bottom, int Rows);

    /// <summary>
    /// Where the WEAPONS page goes in a window <paramref name="windowHeight"/>
    /// pixels tall: from where the last run's heading sits on DUNGEONS, pushed down
    /// by <paramref name="noticeRows"/> rows of notices (<see cref="NoticeShift"/>),
    /// to the refusal row's clearance over the keys line; the rows between are the
    /// window the choice scrolls (<see cref="HubWeapons.ScrollTop"/>). At least one,
    /// so the choice is always in view.
    /// </summary>
    internal static WeaponsLayout WeaponsPage(float windowHeight, int noticeRows = 0)
    {
        float top = LastRunHeadingY + NoticeShift(noticeRows);
        float bottom = windowHeight - RefusalFromBottom - RefusalClearance;
        int rows = (int)MathF.Floor((bottom - top - GlyphHeight * WeaponRowScale) / WeaponRowPitch) + 1;
        return new WeaponsLayout(top, bottom, Math.Max(1, rows));
    }

    /// <summary>How many characters a detail line runs to before a list on it wraps: the right column's width at <see cref="DetailScale"/>.</summary>
    internal static int DetailChars => (int)(ColumnWidth / (GlyphAdvance * DetailScale));

    /// <summary>
    /// A weapon's capacity said whole (R4, §6.1), for the detail panel: the
    /// capacity, and how far the wear cashed has come toward the next point of it —
    /// <c>CAPACITY 31 - 12 OF 31 CASHED TOWARD 32</c>. Under a tuning that freezes
    /// it (<see cref="Tuning.WearCapacityPerBar"/> 0), <c>CAPACITY 30 - IT DOES NOT
    /// GROW</c>.
    /// </summary>
    internal static string CapacityLine(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        var pool = weapon.WearPool;
        int perBar = GameContent.Current.Tuning.WearCapacityPerBar;
        return perBar > 0
            ? $"CAPACITY {pool.Max} - {pool.XpIntoNext} OF {pool.XpToNext} CASHED TOWARD {pool.Max + perBar}"
            : $"CAPACITY {pool.Max} - IT DOES NOT GROW";
    }

    /// <summary>Where a weapon row's weapon is, over its detail: <c>IN THE HAND OF [A]</c>, <c>IN THE BAG OF [A]</c>, <c>IN THE STABLE</c>.</summary>
    internal static string WhereLine(WeaponPlace place)
        => place.IsStable ? "IN THE STABLE"
            : place.Index == 0 ? $"IN THE HAND OF {HubWeapons.MemberLine(place.MemberId!)}"
            : $"IN THE BAG OF {HubWeapons.MemberLine(place.MemberId!)}";

    /// <summary>What a room says, over the detail panel: where it is.</summary>
    internal static string RoomLine(WeaponPlace place)
        => place.IsStable ? "THE END OF THE STABLE - IT KEEPS ANY NUMBER OF WEAPONS"
            : place.Index == 0 ? $"THE HAND OF {HubWeapons.MemberLine(place.MemberId!)} IS EMPTY"
            : $"ROOM IN THE BAG OF {HubWeapons.MemberLine(place.MemberId!)}";

    /// <summary>Under a room's line: how a weapon comes to be there.</summary>
    internal const string RoomHint = "TAKE A WEAPON UP - THEN PUT IT DOWN HERE";

    /// <summary>Under a caster's enchantments, in the warning's orange, when it casts nothing (<see cref="CastsNothing"/>): why.</summary>
    internal const string CastsNothingLine = "CASTS NOTHING - ITS FIRST ENCHANTMENT IS ITS CAST";

    /// <summary>
    /// Whether <paramref name="weapon"/> is a caster that casts nothing: a staff
    /// whose first enchantment applies no status, a wand whose first is no
    /// element — or either carrying none. The dungeon's own gates ask the same, the
    /// staff's cast (<c>TurnSystem.CanCastOn</c>) and the wand's
    /// (<see cref="TurnSystem.CanCastArea"/>), so such a weapon never casts, and
    /// cannot strike: a caster casts. No weapon the content ships is made so, and
    /// the enchanter is to leave none so (the plan's caster rule, Phase 6a.6); only
    /// a hand-edited save can put one on the WEAPONS page, whose detail then says why.
    /// </summary>
    internal static bool CastsNothing(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return weapon.IsCaster
            && (weapon.AreaShape is null
                ? weapon.Innate?.Def.Applies is null
                : weapon.Innate?.Def.DamageType is null or DamageType.None);
    }

    /// <summary>
    /// A weapon's enchantment slots said whole (R4, §6.2), for the detail panel: how
    /// many it holds, how many slots its capacity has opened, and the slot it waits
    /// on — the one its next enchantment needs, or the next to open when that one is
    /// open already — <c>ENCHANTMENTS 1 - SLOTS OPEN 1 OF 5 - SLOT 2 OPENS AT
    /// CAPACITY 35</c>; or <c>ENCHANTMENTS 2 - ALL 5 SLOTS OPEN</c>. What a weapon
    /// arrives with fills its slots from the first, so a unique carrying three at
    /// capacity 30 waits on slot 4.
    /// </summary>
    internal static string SlotsLine(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        int count = weapon.Enchantments.Count;
        int slots = GameContent.Current.Tuning.EnchantmentSlots;
        int open = Wear.SlotsOpen(weapon.WearCapacity);
        if (open >= slots)
            return $"ENCHANTMENTS {count} - ALL {slots} SLOTS OPEN";
        string line = $"ENCHANTMENTS {count} - SLOTS OPEN {open} OF {slots}";
        if (count >= slots)
            return line;
        int next = Math.Max(count + 1, open + 1);
        return $"{line} - SLOT {next} OPENS AT CAPACITY {Wear.SlotOpensAt(next)}";
    }

    /// <summary>
    /// What a weapon's next enchantment costs (R4, §6.2), for the detail panel: the
    /// deposit it needs and the runs it spends in the shop — <c>THE NEXT NEEDS A
    /// DEPOSIT OF 35 WEAR - 2 RUNS IN THE SHOP</c> — or, with every slot filled,
    /// <c>ALL 5 SLOTS ARE FILLED - REFINING IS LEFT</c>.
    /// </summary>
    internal static string PriceLine(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        int count = weapon.Enchantments.Count;
        int slots = GameContent.Current.Tuning.EnchantmentSlots;
        return count >= slots
            ? $"ALL {slots} SLOTS ARE FILLED - REFINING IS LEFT"
            : $"THE NEXT NEEDS A DEPOSIT OF {Wear.Price(count + 1)} WEAR - {DungeonHud.Runs(Wear.ServiceRuns(count))} IN THE SHOP";
    }

    /// <summary>Over a weapon in the shop's detail, where the others say where they are.</summary>
    internal const string AtTheEnchanterLine = "AT THE ENCHANTER";

    /// <summary>What a deposit bought, and its runs: <c>ENCHANT VAMPIRIC - 1 OF 2 RUNS</c>, <c>REFINE - 0 OF 1 RUN</c>.</summary>
    internal static string ServiceLine(Deposit deposit)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        string what = deposit.Service == Service.Enchant && deposit.EnchantmentId is { } id ? $"ENCHANT {EnchantmentName(id)}" : "REFINE";
        return $"{what} - {HubWeapons.ShopRuns(deposit)}";
    }

    /// <summary>A worked deposit's state: when it comes back, <c>WORKING - BACK AFTER 1 MORE RUN THAT COUNTS</c>.</summary>
    internal static string WorkingLine(Deposit deposit)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        int left = deposit.Runs - deposit.RunsWorked;
        return $"WORKING - BACK AFTER {left} MORE {(left == 1 ? "RUN THAT COUNTS" : "RUNS THAT COUNT")}";
    }

    /// <summary>A stalled deposit's state: what it waits on, <c>STALLED - 2 NEWER DEPOSITS ARE AHEAD OF IT</c>.</summary>
    internal static string StalledLine(int ahead)
        => $"STALLED - {ahead} NEWER {(ahead == 1 ? "DEPOSIT IS" : "DEPOSITS ARE")} AHEAD OF IT";

    /// <summary>
    /// The detail panel for the row chosen, top to bottom, on the right: for a
    /// weapon, where it is, its name — a unique's in orange — and the dungeon's
    /// readouts with no wielder (<see cref="DungeonHud.WeaponStats"/>, the
    /// modifiers and the enchantments, each list wrapped to the column), under a
    /// caster that casts nothing why, in orange (<see cref="CastsNothingLine"/>),
    /// then its wear said whole (<see cref="DungeonHud.WearLine"/>, green once
    /// full), its capacity (<see cref="CapacityLine"/>), its slots
    /// (<see cref="SlotsLine"/>) and what its next enchantment costs
    /// (<see cref="PriceLine"/>). For a weapon in the shop, that it is at the
    /// enchanter, its name, what its deposit bought and its runs
    /// (<see cref="ServiceLine"/>), whether it is worked or stalled and on what
    /// (<see cref="WorkingLine"/>, <see cref="StalledLine"/>), and the readouts, wear
    /// and capacity — no slots or price, which its service is changing. For a room,
    /// what it is and what it is for. Nothing for a row that is none of these.
    /// </summary>
    internal static IReadOnlyList<ListRow> DetailRows(HubWeapons.Row row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var rows = new List<ListRow>();
        if (row is { Kind: HubWeapons.RowKind.Room, Place: { } room })
        {
            rows.Add(new ListRow(RoomLine(room), DungeonHud.White, DetailScale, DetailPitch));
            rows.Add(new ListRow(RoomHint, DungeonHud.Grey, DetailScale, DetailPitch));
            return rows;
        }
        if (row is { Kind: HubWeapons.RowKind.Shop, Weapon: { } shopWeapon, Deposit: { } deposit })
        {
            rows.Add(new ListRow(AtTheEnchanterLine, DungeonHud.Grey, DetailWhereScale, DetailWherePitch));
            rows.Add(new ListRow(HubWeapons.Name(shopWeapon), shopWeapon.Unique ? DungeonHud.Orange : DungeonHud.White, DetailNameScale, DetailNamePitch));
            rows.Add(new ListRow(ServiceLine(deposit), DungeonHud.Cyan, DetailScale, DetailPitch));
            rows.Add(row.Stalled
                ? new ListRow(StalledLine(row.Ahead), DungeonHud.Yellow, DetailScale, DetailPitch)
                : new ListRow(WorkingLine(deposit), DungeonHud.Cyan, DetailScale, DetailPitch));
            Readouts(rows, shopWeapon);
            return rows;
        }
        if (row is not { Kind: HubWeapons.RowKind.Weapon, Place: { } place, Weapon: { } weapon })
            return rows;

        rows.Add(new ListRow(WhereLine(place), DungeonHud.Grey, DetailWhereScale, DetailWherePitch));
        rows.Add(new ListRow(HubWeapons.Name(weapon), weapon.Unique ? DungeonHud.Orange : DungeonHud.White, DetailNameScale, DetailNamePitch));
        Readouts(rows, weapon);
        rows.Add(new ListRow(SlotsLine(weapon), DungeonHud.Grey, DetailScale, DetailPitch));
        rows.Add(new ListRow(PriceLine(weapon), DungeonHud.Grey, DetailScale, DetailPitch));
        return rows;
    }

    /// <summary>What every weapon's detail says under its name: its statline, modifiers and enchantments with no wielder, why a caster casts nothing, its wear and its capacity.</summary>
    private static void Readouts(List<ListRow> rows, Weapon weapon)
    {
        rows.Add(new ListRow(DungeonHud.WeaponStats(weapon), DungeonHud.White, DetailScale, DetailPitch));
        foreach (string line in Listed("* ", DungeonHud.ModifierReadout(weapon), DetailChars))
            rows.Add(new ListRow(line, DungeonHud.White, DetailScale, DetailPitch));
        foreach (string line in Listed("* ", DungeonHud.EnchantmentReadout(weapon), DetailChars))
            rows.Add(new ListRow(line, DungeonHud.Cyan, DetailScale, DetailPitch));
        if (CastsNothing(weapon))
            rows.Add(new ListRow(CastsNothingLine, DungeonHud.Orange, DetailScale, DetailPitch));
        rows.Add(new ListRow(DungeonHud.WearLine(weapon), DungeonHud.WearColor(weapon, DungeonHud.White), DetailScale, DetailPitch));
        rows.Add(new ListRow(CapacityLine(weapon), DungeonHud.Grey, DetailScale, DetailPitch));
    }

    /// <summary>
    /// A readout's entries — separated by two spaces, as the dungeon's readouts
    /// separate them — as lines of at most <paramref name="chars"/> characters: the
    /// first led by <paramref name="lead"/>, the rest indented under it, an entry
    /// never split. None for an empty readout.
    /// </summary>
    internal static IEnumerable<string> Listed(string lead, string readout, int chars)
    {
        ArgumentNullException.ThrowIfNull(lead);
        ArgumentNullException.ThrowIfNull(readout);
        var entries = readout.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0)
            yield break;
        string indent = new(' ', lead.Length);
        var line = new System.Text.StringBuilder(lead).Append(entries[0]);
        foreach (string entry in entries.Skip(1))
        {
            if (line.Length + 2 + entry.Length > chars)
            {
                yield return line.ToString();
                line.Clear().Append(indent).Append(entry);
            }
            else
            {
                line.Append("  ").Append(entry);
            }
        }
        yield return line.ToString();
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The screen: its title, the startup notices under it (when there are any),
    /// the last run above (when there has been one), the list under it with the
    /// choice marked, and the keys along the bottom — and, while the menu is up,
    /// the hub dimmed under it and the menu over it (<see cref="DungeonHud.PauseMenuLayout"/>).
    /// </summary>
    public void Render(BetweenRunsScene scene)
    {
        if (!_glReady)
        {
            _text.Initialize();
            _fade.Initialize();
            _glReady = true;
        }

        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;

        DrawCentered(w, TitleY, Title, TitleScale, DungeonHud.Cyan);

        var notices = scene.NoticeRows;
        for (int i = 0; i < notices.Count; i++)
            DrawCentered(w, NoticeTop + i * NoticePitch, notices[i], NoticeScale, NoticeColor(notices[i]));

        if (scene.OnWeapons)
            DrawWeapons(scene, w, h, notices.Count);
        else
            DrawDungeons(scene, w, h, notices.Count);

        if (scene.MenuOpen)
        {
            // The hub dims under the menu, as the floor does under the pause menu,
            // and the menu is drawn over it.
            _text.Flush(_game.ShaderManager);
            _fade.Draw(_game.ShaderManager, MenuDim);
            foreach (var row in DungeonHud.PauseMenuLayout(w, h, PauseMenu.Options(scene.HasTitle), canSave: true, DungeonHud.PauseSavesLine(scene.SavesInto),
                         scene.MenuAnswer))
                _text.DrawText(row.Text, w / 2f - row.Width / 2f, row.Y, row.Scale, row.Color);
        }
        else if (scene.Refusal is { } refusal)
        {
            DrawCentered(w, h - RefusalFromBottom, refusal, KeysLineScale, DungeonHud.Orange);
        }

        var (bottom, bottomColor) = BottomLine(scene.Mode, scene.HasTitle, scene.Weapons.Carried?.Weapon);
        DrawCentered(w, h - KeysLineFromBottom, bottom, KeysLineScale, bottomColor);
        _text.Flush(_game.ShaderManager);
    }

    /// <summary>The DUNGEONS page: the last run above (when there has been one), and the list under it with the choice marked.</summary>
    private void DrawDungeons(BetweenRunsScene scene, int w, int h, int noticeRows)
    {
        var rows = ListRows(scene.Listings, scene.Chosen);
        IReadOnlyList<Run> summary = scene.LastResult is { } lastRun ? HubSummaryLines(lastRun) : [];
        var layout = Layout(h, Math.Max(0, summary.Count - 1), rows.Sum(row => row.Pitch), noticeRows);

        if (summary.Count > 0)
        {
            DrawCentered(w, LastRunHeadingY + layout.Shift, LastRunHeading, SummaryScale, DungeonHud.Grey);
            DrawCentered(w, HeadlineY + layout.Shift, summary[0].Text, HeadlineScale, summary[0].Color);
            float y = SummaryFirstRowY + layout.Shift;
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
    }

    /// <summary>
    /// The WEAPONS page (§6.2): the rows down the left, as many as the window
    /// holds, scrolled to keep the choice in view — the choice marked, a weapon
    /// taken up in cyan where it still lies — and the chosen row said whole on the
    /// right (<see cref="DetailRows"/>).
    /// </summary>
    private void DrawWeapons(BetweenRunsScene scene, int w, int h, int noticeRows)
    {
        var page = scene.Weapons;
        var rows = page.Rows();
        var layout = WeaponsPage(h, noticeRows);
        int top = page.Window(layout.Rows);
        var carried = page.Carried?.Weapon;
        float left = w / 2f + LeftColumn;
        for (int i = top; i < Math.Min(rows.Count, top + layout.Rows); i++)
        {
            var row = rows[i];
            bool isCarried = carried is not null && ReferenceEquals(row.Weapon, carried);
            DrawRuns(left, layout.Top + (i - top) * WeaponRowPitch, WeaponRowScale, HubWeapons.Runs(row, chosen: i == page.Chosen, isCarried));
        }

        float right = w / 2f + RightColumn;
        float y = layout.Top;
        foreach (var line in DetailRows(page.ChosenRow))
        {
            _text.DrawText(line.Text, right, y, line.Scale, line.Color);
            y += line.Pitch;
        }
    }

    /// <summary>Runs side by side from <paramref name="x"/>, each where the last ended: a row read as one line, in its colours.</summary>
    private void DrawRuns(float x, float y, float scale, IReadOnlyList<Run> runs)
    {
        foreach (var run in runs)
        {
            _text.DrawText(run.Text, x, y, scale, run.Color);
            x += run.Text.Length * GlyphAdvance * scale;
        }
    }

    /// <summary>The keys line's scale.</summary>
    internal const float KeysLineScale = 1.3f;

    private void DrawCentered(int viewportW, float y, string text, float scale, Vector4 color)
    {
        float width = text.Length * GlyphAdvance * scale;
        _text.DrawText(text, viewportW / 2f - width / 2f, y, scale, color);
    }

    public void Dispose()
    {
        _text.Dispose();
        _fade.Dispose();
    }
}
