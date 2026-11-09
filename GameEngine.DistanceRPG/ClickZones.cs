using GameEngine.Core;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The menus' mouse (after play, 2026-10: "make all the menus clickable"): the
/// places on screen a left click acts on, laid down by a HUD as it draws a frame,
/// so a click reads exactly what was drawn last. Two kinds:
/// <list type="bullet">
/// <item><strong>A key's own words</strong> on a keys line or a menu row —
/// <c>ESC MENU</c>, <c>2  SAVE GAME</c>, <c>E OR ENTER: YES</c> — click as that key
/// pressed (<see cref="KeySpans"/>, <see cref="Press"/>), through every handler the
/// key reaches, so a click never does what its key would not. A pair, <c>W/S</c> or
/// <c>A/D</c>, presses its first key on its left half and its second on its right.</item>
/// <item><strong>A list's row</strong> — a slot, an option, a dungeon, a weapon —
/// clicks as the screen says (<see cref="Add"/>): each screen's own method, which
/// asks the list's own rules.</item>
/// </list>
/// The zone under the mouse is lit as it is laid down, under the text, so what a
/// click would take reads before it is made. The last zone laid down over a point
/// is the one clicked: what is drawn over the rest.
/// </summary>
internal sealed class ClickZones : IDisposable
{
    /// <summary>A place a click acts on, in window pixels, y down.</summary>
    internal readonly record struct Zone(float X, float Y, float Width, float Height, Action OnClick)
    {
        public bool Contains(Vector2 at) => at.X >= X && at.X < X + Width && at.Y >= Y && at.Y < Y + Height;
    }

    /// <summary>A key's words on a line: the characters from <paramref name="Start"/>, <paramref name="Length"/> long, that click as <paramref name="Key"/>.</summary>
    internal readonly record struct KeySpan(int Start, int Length, Keys Key);

    /// <summary>How far a zone reaches past its text's glyphs, every side: a 1.2-scale keys line is only 8 pixels tall.</summary>
    internal const float Pad = 3f;

    /// <summary>The zone under the mouse, lit: a white that only lifts the black.</summary>
    internal static readonly Vector4 Lit = new(1f, 1f, 1f, 0.12f);

    private readonly Func<InputEventManager> _input;
    private readonly List<Zone> _zones = [];
    private readonly HudRects _rects = new();
    private ShaderManager? _shaders;
    private int _width;
    private int _height;

    /// <summary>Zones whose keys press through <paramref name="input"/>, the game's own input, asked for only when a key is pressed: a HUD tested for its words has no game.</summary>
    public ClickZones(Func<InputEventManager> input) => _input = input;

    /// <summary>The mouse, in window pixels: the zone it is over is lit as it is laid down.</summary>
    public Vector2 Mouse { get; set; }

    /// <summary>The zones laid down in the last frame, in the order they were.</summary>
    public IReadOnlyList<Zone> Zones => _zones;

    /// <summary>
    /// A frame begins: the last frame's zones go, and the zone under the mouse is
    /// lit through <paramref name="shaders"/> in a window <paramref name="width"/> by
    /// <paramref name="height"/>. GL thread, a context current.
    /// </summary>
    public void BeginFrame(ShaderManager shaders, int width, int height)
    {
        _zones.Clear();
        _rects.Initialize();
        _shaders = shaders;
        _width = width;
        _height = height;
    }

    /// <summary>A zone: <paramref name="onClick"/> when a click lands in it. Lit while the mouse is over it, in a frame begun (<see cref="BeginFrame"/>).</summary>
    public void Add(float x, float y, float width, float height, Action onClick)
    {
        ArgumentNullException.ThrowIfNull(onClick);
        var zone = new Zone(x, y, width, height, onClick);
        _zones.Add(zone);
        if (_shaders is not null && zone.Contains(Mouse))
            _rects.Fill(_shaders, _width, _height, x, y, width, height, Lit);
    }

    /// <summary>A zone over a row of text drawn from (<paramref name="x"/>, <paramref name="y"/>), <paramref name="chars"/> characters at <paramref name="scale"/>, reaching <see cref="Pad"/> past it.</summary>
    public void AddText(float x, float y, int chars, float scale, Action onClick)
        => Add(x - Pad, y - Pad, chars * TitleHud.GlyphAdvance * scale + 2f * Pad, TitleHud.GlyphHeight * scale + 2f * Pad, onClick);

    /// <summary>Every key's words on <paramref name="text"/>, drawn from (<paramref name="x"/>, <paramref name="y"/>) at <paramref name="scale"/>, a zone that presses its key (<see cref="KeySpans"/>); with <paramref name="leadOnly"/>, a menu row's, only the key it opens with.</summary>
    public void AddKeys(string text, float x, float y, float scale, bool leadOnly = false)
    {
        foreach (var span in KeySpans(text, leadOnly))
        {
            var key = span.Key;
            AddText(x + span.Start * TitleHud.GlyphAdvance * scale, y, span.Length, scale, () => Press(key));
        }
    }

    /// <summary>A click at <paramref name="at"/>: the last zone laid down over it acts, and true; false over none.</summary>
    public bool Click(Vector2 at)
    {
        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            if (!_zones[i].Contains(at))
                continue;
            _zones[i].OnClick();
            return true;
        }
        return false;
    }

    /// <summary>
    /// <paramref name="key"/> pressed and let go, through the engine's input as a key
    /// on the keyboard is, so every handler it reaches answers it — the game's ESC
    /// among them — and a key held for movement is not left down.
    /// </summary>
    public void Press(Keys key)
    {
        var input = _input();
        input.RaiseKeyPressed(new KeyPressedEvent { Key = key });
        input.RaiseKeyReleased(new KeyReleasedEvent { Key = key });
    }

    /// <summary>The separator a keys line puts between its keys: <c>SPACE END TURN - E STAIRS</c>.</summary>
    private const string Joint = " - ";

    /// <summary>
    /// The keys <paramref name="line"/> names, each with where its words are: every
    /// part between the joints (<c> - </c>) that opens with a key — a letter or a
    /// digit alone, <c>ENTER</c>, <c>ESC</c>, <c>SPACE</c> or <c>TAB</c>, a colon after
    /// it or not — is that key's. A part that asks (<c>LEAVE THE DUNGEON? E OR ENTER:
    /// YES</c>) is read from after its question. A pair of keys (<c>W/S</c>,
    /// <c>A/D</c>) is split down the middle, its first key's on the left; a key with
    /// something not a key after its slash (<c>TAB/1-4</c>) is the key's alone. A key
    /// in a list (<c>1, 2 OR 3</c>) and a part opening with a word (<c>WASD MOVE</c>,
    /// <c>SCROLL ZOOM</c>, <c>CLICK ENEMY</c>) are no key's. A chosen row's marker
    /// (<c>&gt; 2  REFINE ... &lt;</c>) is read past.
    /// <para>
    /// With <paramref name="leadOnly"/> — a menu's row, an option led by its number
    /// (<c>1  ENCHANT - A CHOSEN ENCHANTMENT</c>) — only the first part is read, so a
    /// word of what the option says is never taken for a key: the whole row is the
    /// key's.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<KeySpan> KeySpans(string line, bool leadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(line);
        var spans = new List<KeySpan>();
        if (leadOnly)
        {
            AddPart(line, 0, line.Length, spans);
            return spans;
        }
        int start = 0;
        while (start <= line.Length)
        {
            int end = line.IndexOf(Joint, start, StringComparison.Ordinal);
            if (end < 0) end = line.Length;
            AddPart(line, start, end, spans);
            start = end + Joint.Length;
        }
        return spans;
    }

    private static void AddPart(string line, int start, int end, List<KeySpan> spans)
    {
        if (line.AsSpan(start, end - start).StartsWith("> ", StringComparison.Ordinal))
            start += 2;
        int asked = end > start ? line.LastIndexOf("? ", end - 1, end - start, StringComparison.Ordinal) : -1;
        if (asked >= 0) start = asked + 2;
        if (end <= start) return;

        string part = line[start..end];
        string token = part.Split(' ')[0];
        if (token.EndsWith(',')) return;   // one of a list: the list's own line names them
        string[] pair = token.Split('/');
        if (pair.Length == 2 && KeyOf(pair[0]) is { } first && KeyOf(pair[1]) is { } second)
        {
            int half = part.Length / 2;
            spans.Add(new KeySpan(start, half, first));
            spans.Add(new KeySpan(start + half, part.Length - half, second));
        }
        else if (KeyOf(pair[0]) is { } key)
        {
            spans.Add(new KeySpan(start, part.Length, key));
        }
    }

    /// <summary>The key <paramref name="token"/> names, a colon after it or not; null for a word.</summary>
    internal static Keys? KeyOf(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        token = token.TrimEnd(':');
        if (token.Length == 1)
            return token[0] switch
            {
                >= 'A' and <= 'Z' => Keys.A + (token[0] - 'A'),
                >= '0' and <= '9' => Keys.D0 + (token[0] - '0'),
                _ => null,
            };
        return token switch
        {
            "ENTER" => Keys.Enter,
            "ESC" => Keys.Escape,
            "SPACE" => Keys.Space,
            "TAB" => Keys.Tab,
            _ => null,
        };
    }

    public void Dispose() => _rects.Dispose();
}
