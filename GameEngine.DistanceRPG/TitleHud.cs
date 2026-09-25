using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The title screen's text (the user's ruling of 2026-09-25 on the save model,
/// R2(a)), drawn with the engine's bitmap <see cref="TextRenderer"/> as the other
/// screens are: <c>DISTANCE RPG</c> across the top, the notices under it, the four
/// options — or the load list in their place — and the keys along the bottom, or
/// an ask in their place.
/// <list type="bullet">
/// <item><strong>The options</strong> sit centred under the notices, the chosen
/// one marked <c>&gt; ... &lt;</c> in yellow, CONTINUE dimmed while there is
/// nothing to continue.</item>
/// <item><strong>The load list stands in the options' place, and the notices'
/// </strong>, so it never contends with the save's word for height: a slot a row,
/// with its markers' row under it in grey when any apply (<see cref="SlotText"/>),
/// at the list's one scale, left-aligned and centred on its widest row as the
/// screen a broken file stops the game on lays its block. The chosen slot is
/// marked <c>&gt; ... &lt;</c> in yellow; an empty slot, or one beyond the count,
/// is dimmed. Ten slots, each with its markers' row, fit a 720-pixel window.</item>
/// <item><strong>An ask is drawn where the keys stand</strong>, two rows in
/// yellow — what it does, then what the keys do — and what the list said about a
/// key (<c>SLOT 2 IS EMPTY</c>) sits just above the keys, in orange.</item>
/// </list>
/// Every row comes from <see cref="Layout"/>, a pure function of the menu, the
/// notices and the window, which the tests read; only the drawing is not.
/// </summary>
public sealed class TitleHud : IDisposable
{
    /// <summary>Pixels a character advances at scale 1: the 5-px glyph and its 1-px gap.</summary>
    internal const float GlyphAdvance = 6f;

    /// <summary>A glyph's height at scale 1: the font is 5 x 7.</summary>
    internal const float GlyphHeight = 7f;

    internal const float TitleY = 44f;
    internal const float TitleScale = 6f;

    /// <summary>The notices under the title, a row a pitch; and the load list's heading, in their place while it is up.</summary>
    internal const float NoticeTop = 104f;
    internal const float NoticePitch = 16f;
    internal const float NoticeScale = 1.4f;

    /// <summary>How far down the window the options sit with nothing above them to clear, and the gap they keep under the notices.</summary>
    internal const float OptionsShare = 0.42f;
    internal const float OptionsGap = 24f;
    internal const float OptionScale = 2.2f;
    internal const float OptionPitch = 40f;

    /// <summary>The load list: its heading, then a slot row a pitch — a markers' row just under its slot's, and the next slot a pitch under that.</summary>
    internal const float ListHeadingScale = 2f;
    internal const float ListTop = 136f;
    internal const float SlotPitch = 24f;
    internal const float MarkerOffset = 15f;

    /// <summary>The keys line's distance up from the bottom, its scale, and the pitch the row above it — an ask's first row, or what the list said — stands at.</summary>
    internal const float KeysFromBottom = 30f;
    internal const float KeysScale = 1.6f;
    internal const float BottomPitch = 22f;

    /// <summary>The least room kept either side of a left-aligned block.</summary>
    internal const float SideMargin = 20f;

    /// <summary>How a slot row is marked when it is not the one chosen, so every slot's label starts in the same column as the chosen one's after its <c>&gt; </c>.</summary>
    private const string Unmarked = "  ";

    /// <summary>How a slot's markers' row is indented under its slot's row.</summary>
    private const string MarkerIndent = "    ";

    /// <summary>One row as the screen draws it: its text, how far down, its scale and colour, and where its left edge is — null for a row centred on the window.</summary>
    internal readonly record struct TitleRow(string Text, float Y, float Scale, Vector4 Color, float? Left = null)
    {
        /// <summary>The row's width in pixels.</summary>
        public float Width => Text.Length * GlyphAdvance * Scale;

        /// <summary>Where the row's glyphs end, downward.</summary>
        public float Bottom => Y + GlyphHeight * Scale;
    }

    private readonly Game _game;
    private readonly TextRenderer _text = new();
    private bool _glReady;

    public TitleHud(Game game)
    {
        _game = game;
    }

    /// <summary>
    /// Every row the screen draws, top to bottom, in a window
    /// <paramref name="width"/> by <paramref name="height"/>: the title; then the
    /// notices and the options, or — while the load list is up — its heading and
    /// the slots in their place; then, along the bottom, the ask's two rows, or the
    /// keys with what the list said just above them.
    /// </summary>
    /// <param name="menu">The title's choices, and where they stand.</param>
    /// <param name="notices">The notices as the screen lays them out (<see cref="TitleScene.NoticeRows"/>).</param>
    /// <param name="width">The window's width, in pixels.</param>
    /// <param name="height">The window's height, in pixels.</param>
    /// <param name="dungeons">The content's dungeons, whose names the slot rows read.</param>
    /// <param name="zone">The player's time zone, which a save's time is shown in.</param>
    internal static IReadOnlyList<TitleRow> Layout(TitleMenu menu, IReadOnlyList<string> notices, float width, float height,
        DungeonCatalogue dungeons, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentNullException.ThrowIfNull(dungeons);
        ArgumentNullException.ThrowIfNull(zone);

        var rows = new List<TitleRow> { new(TitleText.Title, TitleY, TitleScale, DungeonHud.Cyan) };
        if (menu.Loading is { } list)
            rows.AddRange(ListRows(list, width, dungeons, zone));
        else
            rows.AddRange(OptionRows(menu, notices, height, dungeons, zone));

        float keysY = height - KeysFromBottom;
        if (menu.Asking)
        {
            var (what, keys) = AskRows(menu, dungeons);
            rows.Add(new TitleRow(what, keysY - BottomPitch, KeysScale, DungeonHud.Yellow));
            rows.Add(new TitleRow(keys, keysY, KeysScale, DungeonHud.Yellow));
            return rows;
        }
        if (menu.Loading?.Said is { } said)
            rows.Add(new TitleRow(said, keysY - BottomPitch, KeysScale, DungeonHud.Orange));
        rows.Add(new TitleRow(menu.Loading is null ? TitleText.KeysLine : TitleText.LoadKeysLine, keysY, KeysScale, DungeonHud.Grey));
        return rows;
    }

    /// <summary>
    /// The ask being put, as its two rows: NEW CAMPAIGN's names what the autosave
    /// holds and how many slots are kept; a load's names the slot and what the
    /// autosave holds — promising no set-aside for a slot listed
    /// <see cref="SlotText.CannotBeRead"/> (<see cref="TitleText.LoadAsk"/>). Each
    /// names the autosave by what it holds (<see cref="SlotText.Summary"/>).
    /// </summary>
    internal static (string What, string Keys) AskRows(TitleMenu menu, DungeonCatalogue dungeons)
    {
        ArgumentNullException.ThrowIfNull(menu);
        string autosave = SlotText.Summary(menu.Listing.Autosave, dungeons);
        return menu.Ask switch
        {
            TitleAsk.NewCampaign => TitleText.NewCampaignAsk(autosave, menu.Count),
            TitleAsk.Load => TitleText.LoadAsk(menu.AskSlot, autosave,
                unreadable: menu.Listing.Slots.Any(slot => slot.Number == menu.AskSlot && slot.State == SlotState.Unreadable)),
            _ => throw new InvalidOperationException("No ask is being put."),
        };
    }

    /// <summary>An option as its row says it: CONTINUE naming the most recent save's own row, or that there is none yet; the rest by name.</summary>
    internal static string OptionText(TitleOption option, TitleMenu menu, DungeonCatalogue dungeons, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return option switch
        {
            TitleOption.Continue => menu.Listing.Newest is { } newest ? TitleText.Continue(SlotText.Row(newest, dungeons, zone)) : TitleText.NoSaveYet,
            TitleOption.NewCampaign => TitleText.NewCampaign,
            TitleOption.LoadGame => TitleText.LoadGame,
            _ => TitleText.Quit,
        };
    }

    /// <summary>The notices, then the options under them — as low as the notices push them, and never higher than their share of the window.</summary>
    private static IEnumerable<TitleRow> OptionRows(TitleMenu menu, IReadOnlyList<string> notices, float height, DungeonCatalogue dungeons, TimeZoneInfo zone)
    {
        for (int i = 0; i < notices.Count; i++)
            yield return new TitleRow(notices[i], NoticeTop + i * NoticePitch, NoticeScale, BetweenRunsHud.NoticeColor(notices[i]));

        float top = MathF.Max(height * OptionsShare, notices.Count == 0 ? 0f : NoticeTop + notices.Count * NoticePitch + OptionsGap);
        for (int i = 0; i < TitleMenu.Options.Count; i++)
        {
            var option = TitleMenu.Options[i];
            string text = OptionText(option, menu, dungeons, zone);
            bool chosen = i == menu.Cursor;
            var color = chosen ? DungeonHud.Yellow
                : option == TitleOption.Continue && !menu.ContinueOn ? DungeonHud.DarkGrey
                : DungeonHud.White;
            yield return new TitleRow(chosen ? $"> {text} <" : text, top + i * OptionPitch, OptionScale, color);
        }
    }

    /// <summary>
    /// The load list: its heading where the notices stand, then each slot's rows
    /// (<see cref="SlotText.Rows"/>) as one left-aligned block centred on its
    /// widest row — the chosen slot's row marked and in yellow, an empty slot or
    /// one beyond the count dimmed, every markers' row in grey and indented under
    /// its slot.
    /// </summary>
    private static IEnumerable<TitleRow> ListRows(SlotMenu list, float width, DungeonCatalogue dungeons, TimeZoneInfo zone)
    {
        yield return new TitleRow(TitleText.LoadGame, NoticeTop, ListHeadingScale, DungeonHud.Grey);

        var block = new List<(string Text, Vector4 Color, bool Marker)>();
        for (int i = 0; i < list.Rows.Count; i++)
        {
            var entry = list.Rows[i];
            var texts = SlotText.Rows(entry, list.Count, dungeons, zone);
            bool chosen = i == list.Cursor;
            var color = chosen ? DungeonHud.Yellow
                : entry.State == SlotState.Empty || entry.Beyond ? DungeonHud.DarkGrey
                : DungeonHud.White;
            block.Add((chosen ? $"> {texts[0]} <" : Unmarked + texts[0], color, false));
            foreach (string marker in texts.Skip(1))
                block.Add((MarkerIndent + marker, DungeonHud.Grey, true));
        }

        int widest = block.Select(row => row.Text.Length).DefaultIfEmpty(0).Max();
        float left = MathF.Max(SideMargin, width / 2f - widest * GlyphAdvance * SlotText.Scale / 2f);
        float y = ListTop;
        for (int i = 0; i < block.Count; i++)
        {
            var row = block[i];
            yield return new TitleRow(row.Text, y, SlotText.Scale, row.Color, left);
            bool markerNext = i + 1 < block.Count && block[i + 1].Marker;
            y += markerNext ? MarkerOffset : SlotPitch;
        }
    }

    /// <summary>The screen: every row <see cref="Layout"/> lays out, over the black the game clears to.</summary>
    public void Render(TitleScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!_glReady)
        {
            _text.Initialize();
            _glReady = true;
        }

        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;
        foreach (var row in Layout(scene.Menu, scene.NoticeRows, w, h, GameContent.Current.Dungeons, TimeZoneInfo.Local))
            _text.DrawText(row.Text, row.Left ?? w / 2f - row.Width / 2f, row.Y, row.Scale, row.Color);
        _text.Flush(_game.ShaderManager);
    }

    public void Dispose() => _text.Dispose();
}
