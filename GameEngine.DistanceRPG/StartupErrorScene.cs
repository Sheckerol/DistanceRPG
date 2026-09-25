using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The screen a broken content file stops the game on (1h, §5.4: "invalid
/// content aborts startup, naming the entry and the rule it broke"). The game
/// opens on it instead of the screen between runs when the data files do not
/// load (<see cref="ContentFiles.Load"/> came back with an error): <c>THE GAME
/// CANNOT START</c>, then the file whose stage broke (marked <c>(BUILT-IN)</c>
/// when that stage ran the copy the game carries built in), the entry, the rule
/// and its detail, what <c>tuning.json</c> changed from the built-in numbers,
/// and what to do: <c>FIX THE FILE AND START AGAIN - ESC QUITS</c>, or — when
/// the broken copy is a built-in one and nothing beside the executable is
/// involved — which master copy to fix, and that the game must be rebuilt
/// (<see cref="ClosingLine"/>). The log has the same lines. Under them, the
/// load's notices, wrapped and capped as everywhere else — a file missing may be
/// what the broken one broke against.
/// <para>
/// A bad <em>tuning</em> value never gets here: it clamps, the tuning is fitted
/// to the content, and it shows as a notice where the game opens — the contrast
/// §5.4 draws between a bad balance number and a broken invariant. No campaign
/// exists behind this screen, so there is nothing to save; ESC is not consumed
/// here, so the game quits (<c>DistanceRpgGame</c>).
/// </para>
/// <para>
/// Every line comes from <see cref="Lines"/>, built from the failure's
/// <see cref="ContentException.File"/>, <see cref="ContentException.EntryId"/>,
/// <see cref="ContentException.Rule"/> and <see cref="ContentException.Detail"/>
/// — never by taking its message apart — and each is wrapped by the one
/// wrapper every notice goes through (<see cref="NoticeText.Wrap"/>), so the
/// strings are tested and only the drawing is not.
/// </para>
/// </summary>
public sealed class StartupErrorScene : Scene
{
    /// <summary>The screen's first line.</summary>
    internal const string Title = "THE GAME CANNOT START";

    /// <summary>The screen's last line: what to do, and the one key it answers.</summary>
    internal const string KeysLine = "FIX THE FILE AND START AGAIN - ESC QUITS";

    /// <summary>Pixels a character advances at scale 1: the 5-px glyph and its 1-px gap.</summary>
    private const float GlyphAdvance = 6f;

    private const float TitleY = 48f;
    private const float TitleScale = 4f;
    private const float BodyTop = 116f;
    private const float BodyScale = 1.8f;
    private const float BodyPitch = 22f;
    private const float NoticeGap = 18f;
    private const float NoticeScale = 1.4f;
    private const float NoticePitch = 16f;
    private const float KeysFromBottom = 40f;
    private const float KeysScale = 1.8f;

    /// <summary>The least room kept either side of the text, in pixels, when a narrow window shrinks it.</summary>
    private const float SideMargin = 20f;

    /// <summary>The gap between the failure's rows and the line saying what the tuning changed, which is context rather than the failure.</summary>
    private const float TuningGap = 10f;

    private readonly Game _game;
    private readonly TextRenderer _text = new();
    private readonly int _tuningRows;
    private bool _glReady;

    /// <param name="game">The game window: its size and its shaders, for the text.</param>
    /// <param name="error">Why the content does not load: the first stage to break, stamped with its file.</param>
    /// <param name="tuningOverrides">Every number the tuning changed from the built-in copy's (<see cref="ContentLoad.TuningOverrides"/>).</param>
    /// <param name="readBeside">The content files read from beside the executable (<see cref="ContentLoad.ReadBeside"/>): whether one of them may be what broke a built-in copy.</param>
    /// <param name="notices">What else the load raised, or none.</param>
    public StartupErrorScene(Game game, ContentException error, IReadOnlyList<string> tuningOverrides, IReadOnlyList<string> readBeside,
        IReadOnlyList<string>? notices = null)
        : base("StartupErrorScene")
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(tuningOverrides);
        ArgumentNullException.ThrowIfNull(readBeside);
        _game = game;
        Rows = Lines(error, tuningOverrides, readBeside);
        NoticeRows = NoticeText.Rows(notices ?? []);
        _tuningRows = NoticeText.Wrap(TuningLine(tuningOverrides)).Count;
    }

    /// <summary>The screen's lines as it draws them (<see cref="Lines"/>): the title first, the keys line last.</summary>
    internal IReadOnlyList<string> Rows { get; }

    /// <summary>The load's notices as the screen draws them under the failure (<see cref="NoticeText.Rows"/>): wrapped, capped, or none.</summary>
    internal IReadOnlyList<string> NoticeRows { get; }

    /// <summary>
    /// The screen's lines, top to bottom, upper-cased and wrapped under
    /// <see cref="NoticeText.LineChars"/>: <see cref="Title"/>; the file — with
    /// <c>(BUILT-IN)</c> when its stage ran the built-in copy — when the failure
    /// names one; the entry; the rule; the detail, when there is one; what
    /// <c>tuning.json</c> changed from the built-in numbers; and the closing line
    /// (<see cref="ClosingLine"/>), one line however long. The log writes the
    /// same lines.
    /// </summary>
    internal static IReadOnlyList<string> Lines(ContentException error, IReadOnlyList<string> tuningOverrides, IReadOnlyList<string> readBeside)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(tuningOverrides);
        ArgumentNullException.ThrowIfNull(readBeside);
        var lines = new List<string> { Title };
        if (error.File is { } file)
            lines.AddRange(NoticeText.Wrap($"FILE: {file}{(error.BuiltIn ? " (built-in)" : "")}"));
        lines.AddRange(NoticeText.Wrap($"ENTRY: {error.EntryId}"));
        lines.AddRange(NoticeText.Wrap($"RULE: {error.Rule}"));
        if (error.Detail is { } detail)
            lines.AddRange(NoticeText.Wrap($"DETAIL: {detail}"));
        lines.AddRange(NoticeText.Wrap(TuningLine(tuningOverrides)));
        lines.Add(ClosingLine(error, readBeside));
        return lines;
    }

    /// <summary>
    /// What to do, and the one key the screen answers. When the copy that broke
    /// is a built-in one and nothing beside the executable is involved, the only
    /// fix is the master copy the game was built with:
    /// <c>THE BUILT-IN WEAPONS.JSON IS BROKEN - FIX GAMEENGINE.DISTANCERPG/DATA/WEAPONS.JSON AND REBUILD - ESC QUITS</c>.
    /// Nothing beside it is involved when the copy is the built-in
    /// <c>tuning.json</c>, which every read stands on; when the copy's own text is
    /// what failed — it is not JSON of its shape
    /// (<see cref="ContentValidator.RuleFileShape"/>, which only reading a file
    /// raises), a number in it is outside its range
    /// (<see cref="ContentValidator.RuleNumberInRange"/>: a range is the game's,
    /// never another file's, so a number is always its own file's), or the build
    /// does not carry it (<see cref="ContentValidator.RuleBuiltInCopy"/>) — since
    /// none of those reads anything else; and when no content file was read from
    /// beside the executable at all. Otherwise a file beside the executable broke
    /// it, or may have — a <c>restricted.json</c> there refusing a built-in weapon
    /// — and the line is <see cref="KeysLine"/>.
    /// </summary>
    internal static string ClosingLine(ContentException error, IReadOnlyList<string> readBeside)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(readBeside);
        if (!error.BuiltIn || error.File is not { } file)
            return KeysLine;
        bool ownText = error.Rule is ContentValidator.RuleFileShape or ContentValidator.RuleNumberInRange or ContentValidator.RuleBuiltInCopy;
        if (file != TuningJson.FileName && !ownText && readBeside.Count > 0)
            return KeysLine;
        string name = file.ToUpperInvariant();
        return $"THE BUILT-IN {name} IS BROKEN - FIX GAMEENGINE.DISTANCERPG/DATA/{name} AND REBUILD - ESC QUITS";
    }

    /// <summary>What <c>tuning.json</c> changed from the built-in numbers, as clamped and fitted — or that it changed none.</summary>
    internal static string TuningLine(IReadOnlyList<string> tuningOverrides)
    {
        ArgumentNullException.ThrowIfNull(tuningOverrides);
        return tuningOverrides.Count == 0
            ? $"{TuningJson.FileName} changes no numbers"
            : $"{TuningJson.FileName} changes {tuningOverrides.Count} {(tuningOverrides.Count == 1 ? "number" : "numbers")}: {string.Join(", ", tuningOverrides)}";
    }

    public override void Render(Camera camera)
    {
        base.Render(camera);
        if (!_glReady)
        {
            _text.Initialize();
            _glReady = true;
        }

        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;

        DrawCentered(w, TitleY, Rows[0], TitleScale, DungeonHud.Red);

        // The failure's rows, then what the tuning changed, as one block:
        // left-aligned so a wrapped line's indented rows read as its own, and the
        // block centred on the widest row. The tuning's rows sit a little apart,
        // in grey: context, not the failure. The closing line is one line however
        // long — a master copy's path, say — so the block is fitted with it, and
        // it is drawn at the block's scale.
        var body = Rows.Skip(1).Take(Rows.Count - 2).ToList();
        int failureRows = body.Count - _tuningRows;
        float scale = FitScale(w, body.Concat(NoticeRows).Append(Rows[^1]), BodyScale);
        float left = BlockLeft(w, body, scale);
        float y = BodyTop;
        for (int i = 0; i < body.Count; i++)
        {
            if (i == failureRows)
                y += TuningGap;
            _text.DrawText(body[i], left, y, scale, i < failureRows ? DungeonHud.White : DungeonHud.Grey);
            y += BodyPitch * scale / BodyScale;
        }

        if (NoticeRows.Count > 0)
        {
            y += NoticeGap;
            float noticeScale = scale * NoticeScale / BodyScale;
            float noticeLeft = BlockLeft(w, NoticeRows, noticeScale);
            foreach (string row in NoticeRows)
            {
                _text.DrawText(row, noticeLeft, y, noticeScale, DungeonHud.Orange);
                y += NoticePitch * scale / BodyScale;
            }
        }

        DrawCentered(w, h - KeysFromBottom, Rows[^1], MathF.Min(KeysScale, scale), DungeonHud.Yellow);
        _text.Flush(_game.ShaderManager);
    }

    public override void Shutdown()
    {
        _text.Dispose();
        base.Shutdown();
    }

    /// <summary>The body's scale: <paramref name="preferred"/>, or smaller when the widest row would not fit the window with a margin either side.</summary>
    private static float FitScale(int viewportW, IEnumerable<string> rows, float preferred)
    {
        int widest = rows.Select(row => row.Length).DefaultIfEmpty(0).Max();
        if (widest == 0) return preferred;
        return MathF.Min(preferred, (viewportW - 2 * SideMargin) / (widest * GlyphAdvance));
    }

    /// <summary>Where a left-aligned block starts so its widest row is centred.</summary>
    private static float BlockLeft(int viewportW, IReadOnlyCollection<string> rows, float scale)
    {
        int widest = rows.Select(row => row.Length).DefaultIfEmpty(0).Max();
        return MathF.Max(SideMargin, viewportW / 2f - widest * GlyphAdvance * scale / 2f);
    }

    private void DrawCentered(int viewportW, float y, string text, float scale, Vector4 color)
    {
        float width = text.Length * GlyphAdvance * scale;
        _text.DrawText(text, viewportW / 2f - width / 2f, y, scale, color);
    }
}
