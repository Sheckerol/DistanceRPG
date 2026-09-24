using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What every data file's writer shares: a name as a JSON string, and a comment
/// wrapped to the file's width with its continuation lines aligned under its
/// first word. Line ends are <c>"\n"</c> whatever the platform, so one record
/// is one text and the shipped files can be pinned byte for byte.
/// </summary>
internal static class JsonText
{
    /// <summary>How wide a comment runs before it wraps, indentation included.</summary>
    public const int Width = 100;

    /// <summary><paramref name="name"/> as a JSON string.</summary>
    public static string Quote(string name) => $"\"{JsonEncodedText.Encode(name)}\"";

    /// <summary>A whole number as a JSON number, in the invariant culture.</summary>
    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A comment above what it describes, wrapped at <see cref="Width"/>, its continuation lines aligned under its first word.</summary>
    public static void Comment(StringBuilder text, string words, string indent)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(words);
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (string word in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && indent.Length + 3 + line.Length + 1 + word.Length > Width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }
            if (line.Length > 0)
                line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0)
            lines.Add(line.ToString());

        for (int i = 0; i < lines.Count; i++)
            text.Append(indent).Append(i == 0 ? "/* " : "   ").Append(lines[i]).Append(i == lines.Count - 1 ? " */" : "").Append('\n');
    }

    /// <summary>A file's header: a block comment of <paramref name="lines"/>, each line as given.</summary>
    public static void Header(StringBuilder text, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(lines);
        text.Append("/*\n");
        foreach (string line in lines)
            text.Append(line.Length == 0 ? " *" : $" * {line}").Append('\n');
        text.Append(" */\n");
    }
}

/// <summary>
/// The strict reader every content file shares (1h): one file's text as JSON,
/// and each value asked for as the documented shape says it is. Content is
/// strict where <c>tuning.json</c> is lenient — a mistyped field in content is
/// "a rule that silently does not apply" (S:213) — so nothing here guesses,
/// defaults or skips: a member the shape does not know is refused, a member
/// named twice is refused, a missing member is refused, and a value of the wrong
/// kind is refused. Comments and trailing commas are allowed, since a human
/// edits the file.
/// <para>
/// Every failure is a <see cref="ContentException"/> under
/// <see cref="ContentValidator.RuleFileShape"/> (or the rule a caller names,
/// such as <see cref="ContentValidator.RuleUnknownEffect"/>), naming the entry
/// and carrying the detail <c>file: path: problem</c> — the path in the file's
/// own terms (<c>enchantments[4].lock</c>) — so the failure says where it is
/// even before the load stamps its file on it. Enum values are the C# member
/// names, exactly (an ordinal, a case changed or a number are refused), and a
/// near miss says which name it is near.
/// </para>
/// </summary>
internal sealed class JsonShape
{
    /// <summary>The path a failure of the whole file names.</summary>
    public const string Top = "the top level";

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <param name="file">The file being read, as every failure names it: <c>restricted.json</c>.</param>
    public JsonShape(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        File = file;
    }

    /// <summary>The file being read.</summary>
    public string File { get; }

    /// <summary>
    /// The file's text as a JSON value, held on its own (nothing to dispose). An
    /// empty file is refused — a present file is read whole, and nothing is not
    /// content — and a syntax error names its line and column.
    /// </summary>
    public JsonElement Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            throw Fail(File, Top, "the file is empty");
        try
        {
            using var document = JsonDocument.Parse(text, Options);
            return document.RootElement.Clone();
        }
        catch (JsonException error)
        {
            var (line, column, reason) = SyntaxError(error);
            throw Fail(File, $"line {line}, column {column}", $"is not valid JSON ({reason})");
        }
    }

    /// <summary>A failure of this file: <paramref name="entry"/> broke <paramref name="rule"/>, and the detail is <c>file: path: problem</c>.</summary>
    public ContentException Fail(string entry, string path, string problem, string rule = ContentValidator.RuleFileShape)
        => new(entry, rule, $"{File}: {path}: {problem}");

    /// <summary><paramref name="obj"/>'s member <paramref name="name"/>, which the shape requires: refused when missing.</summary>
    public JsonElement Required(JsonElement obj, string name, string path, string entry)
        => obj.TryGetProperty(name, out var value) ? value : throw Fail(entry, Member(path, name), "is missing");

    /// <summary><paramref name="obj"/>'s member <paramref name="name"/>, or null when the shape lets it be left out.</summary>
    public JsonElement? Optional(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value : null;

    /// <summary>
    /// Refuse any member of <paramref name="obj"/> that is not one of
    /// <paramref name="known"/> — pointing at the known one it is nearest, or
    /// listing them — and any member named twice. Run before a member is read,
    /// so an unknown member is reported before a missing one it may be a typo of.
    /// </summary>
    /// <param name="known">The members the shape has, in the order the documentation lists them.</param>
    public void Only(JsonElement obj, IReadOnlyList<string> known, string path, string entry)
    {
        ArgumentNullException.ThrowIfNull(known);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in obj.EnumerateObject())
        {
            if (!known.Contains(member.Name, StringComparer.Ordinal))
            {
                string? near = TuningSchema.Nearest(member.Name, known);
                throw Fail(entry, Member(path, member.Name), near is null
                    ? $"is not a member here - the members are {string.Join(", ", known)}"
                    : $"is not a member here - did you mean {near}?");
            }
            if (!seen.Add(member.Name))
                throw Fail(entry, Member(path, member.Name), "is named twice");
        }
    }

    /// <summary>
    /// The members of <paramref name="obj"/>, an object keyed by id (a relation's
    /// id to what it holds), in the file's order: each name an id, and none named
    /// twice, since a second row for an id would silently replace the first.
    /// </summary>
    public IReadOnlyList<(string Id, JsonElement Value)> Members(JsonElement obj, string path, string entry)
    {
        var members = new List<(string, JsonElement)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in obj.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(member.Name))
                throw Fail(entry, Member(path, member.Name), "is an empty id - an id names what it is about");
            if (!seen.Add(member.Name))
                throw Fail(member.Name, Member(path, member.Name), "is named twice");
            members.Add((member.Name, member.Value));
        }
        return members;
    }

    /// <summary><paramref name="value"/> when it is an object; refused otherwise, saying what it is and what <paramref name="what"/> it should be.</summary>
    public JsonElement Object(JsonElement value, string path, string entry, string what)
        => value.ValueKind == JsonValueKind.Object ? value : throw Fail(entry, path, $"is {Describe(value)}, not {what}");

    /// <summary><paramref name="value"/>'s items when it is a list; refused otherwise.</summary>
    public IReadOnlyList<JsonElement> Array(JsonElement value, string path, string entry, string what)
        => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : throw Fail(entry, path, $"is {Describe(value)}, not {what}");

    /// <summary><paramref name="value"/> as text; refused when it is anything else, a null included.</summary>
    /// <param name="what">What the text is, for the failure: <c>a name in quotes</c>.</param>
    public string String(JsonElement value, string path, string entry, string what = "a text in quotes")
        => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Fail(entry, path, $"is {Describe(value)}, not {what}");

    /// <summary><paramref name="value"/> as an id: text, and not blank — an id is what everything names the entry by.</summary>
    public string Id(JsonElement value, string path, string entry)
    {
        string id = String(value, path, entry, "an id in quotes");
        return string.IsNullOrWhiteSpace(id) ? throw Fail(entry, path, "is an empty id - an id names its entry") : id;
    }

    /// <summary>
    /// <paramref name="value"/> as a whole number: a number written without a
    /// fraction or an exponent, within what a whole number holds. Anything else —
    /// a fraction, a text, true or false, a null, a list, a table — is refused.
    /// </summary>
    public int Int(JsonElement value, string path, string entry)
    {
        if (value.ValueKind != JsonValueKind.Number)
            throw Fail(entry, path, $"is {Describe(value)}, not a whole number");
        string raw = value.GetRawText();
        if (raw.IndexOfAny(['.', 'e', 'E']) >= 0)
            throw Fail(entry, path, $"is {raw}, not a whole number");
        if (!int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int whole))
            throw Fail(entry, path, $"is {raw}, past the largest whole number the game holds");
        return whole;
    }

    /// <summary><paramref name="value"/> as a whole number, or null for a JSON null: a field the shape lets be empty.</summary>
    public int? IntOrNull(JsonElement value, string path, string entry)
        => value.ValueKind == JsonValueKind.Null ? null : Int(value, path, entry);

    /// <summary><paramref name="value"/> as true or false; anything else, a text <c>"true"</c> included, is refused.</summary>
    public bool Bool(JsonElement value, string path, string entry)
        => value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw Fail(entry, path, $"is {Describe(value)}, not true or false");

    /// <summary>
    /// <paramref name="value"/> as a member of <typeparamref name="T"/> by its
    /// exact name — never a number, never another case — refused under
    /// <paramref name="rule"/> otherwise, pointing at the name it is nearest or
    /// listing the names there are.
    /// </summary>
    public T Enum<T>(JsonElement value, string path, string entry, string rule = ContentValidator.RuleFileShape) where T : struct, Enum
    {
        string[] names = System.Enum.GetNames<T>();
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is not null && names.Contains(text, StringComparer.Ordinal))
            return System.Enum.Parse<T>(text);

        string? near = text is null ? null : TuningSchema.Nearest(text, names);
        throw Fail(entry, path, $"is {Describe(value)}, which names no {typeof(T).Name}"
            + (near is null ? $" - the names are {string.Join(", ", names)}" : $" - did you mean {near}?"), rule);
    }

    /// <summary><see cref="Enum{T}"/>, or null for a JSON null: a field the shape lets be empty.</summary>
    public T? EnumOrNull<T>(JsonElement value, string path, string entry, string rule = ContentValidator.RuleFileShape) where T : struct, Enum
        => value.ValueKind == JsonValueKind.Null ? null : Enum<T>(value, path, entry, rule);

    /// <summary>A member's path under its object's: <c>requires.Riposte</c>, or the bare name at the top.</summary>
    public static string Member(string path, string name)
        => string.IsNullOrEmpty(path) || path == Top ? name : $"{path}.{name}";

    /// <summary>What a value is, in the words a failure uses: its number, the text it holds, true, false, null, a list, a table.</summary>
    internal static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.String => $"the text {value.GetString()}",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "a list",
        JsonValueKind.Object => "a table",
        _ => "nothing",
    };

    /// <summary>
    /// Where a text that is not JSON stopped the reader, 1-based, and why, in
    /// words a screen can show: the reader's own sentence without the position
    /// it appends (said here as numbers), its quotes, or its full stop.
    /// </summary>
    internal static (long Line, long Column, string Reason) SyntaxError(JsonException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        string reason = error.Message;
        int where = reason.IndexOf(" LineNumber:", StringComparison.Ordinal);
        if (where >= 0)
            reason = reason[..where];
        reason = reason.Replace("'", "", StringComparison.Ordinal).Replace("\"", "", StringComparison.Ordinal).Trim().TrimEnd('.');
        return ((error.LineNumber ?? 0) + 1, (error.BytePositionInLine ?? 0) + 1, reason);
    }
}
