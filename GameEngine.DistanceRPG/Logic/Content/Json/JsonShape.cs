using System.Globalization;
using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The strict reader every content file shares (1h): one file's text as JSON,
/// and each value asked for as the documented shape says it is. Content is
/// strict where <c>tuning.json</c> is lenient — a mistyped field in content is
/// "a rule that silently does not apply" (P5:287) — so nothing here guesses,
/// defaults or skips: a member the shape does not know is refused, a member
/// named twice is refused, a missing member is refused, and a value of the wrong
/// kind is refused. Comments and trailing commas are allowed, since a human
/// edits the file.
/// <para>
/// Every failure is a <see cref="ContentException"/> under
/// <see cref="ContentValidator.RuleFileShape"/> (or the rule a caller names,
/// such as <see cref="ContentValidator.RuleUnknownId"/>), naming the entry
/// and carrying the detail <c>file: path: problem</c> — the path in the file's
/// own terms (<c>variants[4].adds.Light</c>) — so the failure says where it is
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

    /// <summary><see cref="Id"/>, or null for a JSON null: a reference the shape lets be empty.</summary>
    public string? IdOrNull(JsonElement value, string path, string entry)
        => value.ValueKind == JsonValueKind.Null ? null : Id(value, path, entry);

    /// <summary>A list of ids, each a non-empty string, in the file's order; <paramref name="what"/> says what the list is, for a failure.</summary>
    public string[] Ids(JsonElement value, string path, string entry, string what)
    {
        var items = Array(value, path, entry, what);
        var ids = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
            ids[i] = Id(items[i], $"{path}[{i}]", entry);
        return ids;
    }

    /// <summary>
    /// What a failure inside the row <paramref name="row"/> at <paramref name="path"/>
    /// is reported against: the row's id when it has one that reads, else its
    /// place in the file. The id's own failure comes in field order, like any other.
    /// </summary>
    public static string EntryOf(JsonElement row, string path)
        => row.ValueKind == JsonValueKind.Object && row.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(id.GetString())
            ? id.GetString()!
            : path;

    /// <summary>
    /// A table of modifier names to stack counts — a weapon's forged spread, a
    /// boss's innate modifiers — in the file's order. Each name is a
    /// <see cref="ModifierType"/>'s, exactly: a modifier is content's id, so a name
    /// that is none is an id that resolves to nothing
    /// (<see cref="ContentValidator.RuleUnknownId"/>), pointing at the name it is
    /// nearest. No name twice, and every count a whole number; whether a count is
    /// one the rules allow is the validator's.
    /// </summary>
    /// <param name="what">What the table is, for a failure: <c>a table of modifiers to stack counts</c>.</param>
    public IReadOnlyDictionary<ModifierType, int> Modifiers(JsonElement value, string path, string entry, string what)
    {
        var table = Object(value, path, entry, what);
        var stacks = new Dictionary<ModifierType, int>();
        foreach (var member in table.EnumerateObject())
        {
            string at = Member(path, member.Name);
            if (!ModifierRules.TryParseId(member.Name, out var type))
            {
                string? near = TuningSchema.Nearest(member.Name, System.Enum.GetNames<ModifierType>());
                throw Fail(entry, at, near is null
                    ? $"is not a modifier - the modifiers are {string.Join(", ", System.Enum.GetNames<ModifierType>())}"
                    : $"is not a modifier - did you mean {near}?", ContentValidator.RuleUnknownId);
            }
            if (stacks.ContainsKey(type))
                throw Fail(entry, at, "is named twice");
            stacks[type] = Int(member.Value, at, entry);
        }
        return stacks;
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

    /// <summary>The most characters of the text the reader stopped at that a reason quotes, before it is cut and marked with <see cref="Cut"/>.</summary>
    internal const int MaxQuoted = 24;

    /// <summary>What marks a quoted text as cut short.</summary>
    private const string Cut = "...";

    /// <summary>
    /// Where a text that is not JSON stopped the reader, 1-based, and why, in
    /// words a screen can show on one line: the reader's own sentence without the
    /// position it appends (said here as numbers), its quotes, or its full stop.
    /// <para>
    /// The reader quotes the text it stopped at, and for a word it does not know —
    /// <c>nul</c>, <c>tru</c>, <c>fals</c> — that quote runs to the end of the
    /// file, newlines and all: a <c>"shape": nul</c> early in <c>weapons.json</c>
    /// made a reason of every row after it, which the screen that stops the game
    /// drew past its bottom edge. So the quoted text is kept to the word the
    /// reader stopped at, at most <see cref="MaxQuoted"/> characters, and marked
    /// where it was cut — <c>nul... is an invalid JSON literal. Expected the literal
    /// null</c> — and whatever whitespace is left is one space, so the reason is
    /// one line whatever the reader says.
    /// </para>
    /// </summary>
    internal static (long Line, long Column, string Reason) SyntaxError(JsonException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        string reason = error.Message;
        int where = reason.IndexOf(" LineNumber:", StringComparison.Ordinal);
        if (where >= 0)
            reason = reason[..where];
        reason = OneLine(reason).Replace("'", "", StringComparison.Ordinal).Replace("\"", "", StringComparison.Ordinal);
        reason = OneLine(reason).TrimEnd('.');
        return ((error.LineNumber ?? 0) + 1, (error.BytePositionInLine ?? 0) + 1, reason);
    }

    /// <summary>
    /// A JSON reader's sentence as one short line, whatever file it read — a
    /// content file's (<see cref="SyntaxError"/>) or a save's
    /// (<c>SaveCodec</c>): the text it quotes kept to the word it stopped at
    /// (<see cref="ShortenQuoted"/>), and every run of whitespace — a line break
    /// included — one space.
    /// </summary>
    internal static string OneLine(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return string.Join(' ', ShortenQuoted(reason).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// <paramref name="reason"/>, the reader's sentence, with the text it quotes
    /// at its start — <c>'…' is …</c> — kept to its first word, at most
    /// <see cref="MaxQuoted"/> characters, and <see cref="Cut"/> where anything was
    /// left out. A reason that quotes nothing is returned as it is.
    /// </summary>
    private static string ShortenQuoted(string reason)
    {
        const string Closes = "' is ";
        if (!reason.StartsWith('\''))
            return reason;
        int close = reason.LastIndexOf(Closes, StringComparison.Ordinal);
        if (close <= 0)
            return reason;

        string quoted = reason[1..close];
        int end = quoted.IndexOfAny([' ', '\t', '\r', '\n', ',', ':', '}', ']']);   // the word ends where the text around it starts
        string word = end > 0 ? quoted[..end] : quoted;
        if (word.Length > MaxQuoted)
            word = word[..MaxQuoted];
        return word.Length == quoted.Length
            ? reason
            : $"'{word}{Cut}{reason[close..]}";
    }
}
