using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What reading <c>tuning.json</c> made: the tuning — every key the file named,
/// clamped, over the compiled numbers — the warnings it cost, each one line a
/// playtester can act on, and the numbers the result changed from the compiled
/// ones (<c>MovementBudget 200</c>, <c>PerStack.Block 4</c>).
/// </summary>
public sealed record TuningRead(Tuning Tuning, IReadOnlyList<string> Warnings, IReadOnlyList<string> Overrides);

/// <summary>
/// <c>tuning.json</c> (§5.3): the numbers, read once at startup from the text a
/// caller hands in — <c>Logic/</c> never touches the disk — clamped rather than
/// refused, written from <see cref="TuningSchema"/> with every key grouped and
/// commented, and hashed so a save can ask "are these the same numbers?".
/// <list type="bullet">
/// <item><strong>Lenient, because a human edits it</strong> (S:62-74): comments
/// and trailing commas are allowed; a missing key keeps its compiled default, so
/// a partial file is valid; a table merges per entry onto the compiled one; an
/// unknown key or table member warns and is ignored, so a constant added later
/// never invalidates a file; a value that is not a whole number — a fraction, a
/// text, a list, a null — warns and keeps the default, since ratios are integer
/// divisors; a value out of range clamps to the bound and warns. Nothing here
/// throws on anything a file can say: malformed JSON plays the compiled numbers
/// and says which line.</item>
/// <item><strong>Consistent within itself</strong>: three relational clamps run
/// after the keys, in this order — <see cref="Tuning.BossFloorMax"/> is raised to
/// <see cref="Tuning.BossFloorMin"/>; <see cref="Tuning.PartyCarrySlots"/> is
/// rounded down to share evenly among the party; <see cref="Tuning.BossStackOffset"/>
/// is raised until the tuning's deepest boss pays less than a well-farmed weapon
/// banks — so what comes back always passes <see cref="ContentValidator.ValidateTuning"/>,
/// which stays the refusal for a <see cref="Tuning"/> built in code. Where the
/// tuning meets the content it bends to it, never the reverse
/// (<see cref="TuningFit"/>, run by <see cref="ContentFiles.Load"/>).</item>
/// </list>
/// </summary>
public static class TuningJson
{
    public const string FileName = "tuning.json";

    /// <summary>What every warning about the file starts with, so a notice says which file it is about.</summary>
    internal const string Prefix = FileName + ": ";

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Read a <c>tuning.json</c>'s text. Null — no file — and a file with nothing
    /// in it are the compiled numbers, with no warning (the missing-file notice is
    /// <see cref="ContentFiles"/>'s, which every data file gets alike). Never throws.
    /// </summary>
    public static TuningRead Read(string? text)
    {
        var compiled = ContentDefaults.Tuning;
        if (string.IsNullOrWhiteSpace(text))
            return new TuningRead(compiled, [], []);

        var warnings = new List<string>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, Options);
        }
        catch (JsonException error)
        {
            warnings.Add(Malformed(error));
            return new TuningRead(compiled, warnings, []);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                warnings.Add($"{Prefix}the file is {Describe(root)}, not a table of keys - the built-in numbers are playing");
                return new TuningRead(compiled, warnings, []);
            }

            var tuning = compiled;
            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!named.Add(property.Name))
                    warnings.Add($"{Prefix}{property.Name} is named twice - read in order, so the later one wins where they differ");
                tuning = TuningSchema.Find(property.Name) switch
                {
                    IntKey key => ReadInt(tuning, key, property.Value, warnings),
                    BoolKey key => ReadBool(tuning, key, property.Value, warnings),
                    TableKey key => ReadTable(tuning, key, property.Value, warnings),
                    _ => Unknown(tuning, property.Name, warnings),
                };
            }

            tuning = Relate(tuning, warnings);
            return new TuningRead(tuning, warnings, Overrides(tuning));
        }
    }

    /// <summary>
    /// The file for <paramref name="tuning"/>: a header saying what the file is
    /// and how it is read, then every key in <see cref="TuningSchema.Keys"/>' order
    /// under its §5.3 group's heading — the two groups whose keys wait for a later
    /// phase as headings naming them — each key under its description, every
    /// table member written out with the number the game reads for it. Line ends
    /// are <c>"\n"</c> whatever the platform, so one tuning is one text.
    /// </summary>
    public static string Write(Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var text = new StringBuilder();
        foreach (string line in Header)
            text.Append(line).Append('\n');
        text.Append("{\n");

        var last = TuningSchema.Keys[^1];
        foreach (var group in Enum.GetValues<TuningGroup>())
        {
            if (TuningSchema.Deferred.TryGetValue(group, out var deferred))
            {
                text.Append($"  /* ---- {group} - {deferred.Phase}: {string.Join(", ", deferred.Keys)} ---- */\n");
                Comment(text, $"No key yet: these arrive with the {deferred.Phase} code that reads them, since a key nothing reads would be a dial wired to nothing.", Indent);
                text.Append('\n');
                continue;
            }

            var keys = TuningSchema.Keys.Where(k => k.Group == group).ToList();
            if (keys.Count == 0) continue;
            text.Append($"  /* ---- {group} ---- */\n\n");
            foreach (var key in keys)
            {
                bool isLast = ReferenceEquals(key, last);
                Comment(text, key.Description, Indent);
                WriteKey(text, key, tuning, isLast);
                if (!isLast)
                    text.Append('\n');
            }
        }

        text.Append("}\n");
        return text.ToString();
    }

    /// <summary>
    /// Whether two tunings are the same numbers (S:99-107): the first 16 lowercase
    /// hex digits of SHA-256 over the effective values as canonical text — one
    /// <c>Key=value</c> line per scalar and per table entry (every member of an
    /// enum table, as the game reads it; every id <c>ApplyPercent</c> names),
    /// invariant integers, <c>true</c>/<c>false</c>, lines sorted ordinally. So a
    /// file's formatting, key order and comments never move it, and any value
    /// does — a compiled default a new build changed included. A save stores this,
    /// never the values.
    /// </summary>
    public static string Hash(Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var lines = Values(tuning).Select(v => $"{v.Name}={v.Value}").Order(StringComparer.Ordinal);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        return Convert.ToHexStringLower(digest, 0, 8);
    }

    /// <summary>
    /// Every number <paramref name="tuning"/> holds that the compiled tuning does
    /// not, in the file's order, as <c>MovementBudget 200</c> or
    /// <c>PerStack.Block 4</c>: what a file changed, for the log. A key set to its
    /// compiled value is not a change.
    /// </summary>
    internal static IReadOnlyList<string> Overrides(Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var compiled = Values(ContentDefaults.Tuning).ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        return Values(tuning)
            .Where(v => !compiled.TryGetValue(v.Name, out string? was) || was != v.Value)
            .Select(v => $"{v.Name} {v.Value}")
            .ToList();
    }

    /// <summary>Every effective value, keyed as the hash and the overrides name it: <c>MovementBudget</c>, <c>PerStack.Block</c>.</summary>
    private static IEnumerable<(string Name, string Value)> Values(Tuning tuning)
    {
        foreach (var key in TuningSchema.Keys)
        {
            switch (key)
            {
                case IntKey k:
                    yield return (k.Name, Number(k.Get(tuning)));
                    break;
                case BoolKey k:
                    yield return (k.Name, k.Get(tuning) ? "true" : "false");
                    break;
                case TableKey k:
                {
                    var table = k.Get(tuning);
                    foreach (string member in MembersOf(k, table))
                        yield return ($"{k.Name}.{member}", Number(table[member]));
                    break;
                }
            }
        }
    }

    /// <summary>A table's members in the order the file writes them: an enum's in the enum's order, enchantment ids ordinally.</summary>
    private static IReadOnlyList<string> MembersOf(TableKey key, IReadOnlyDictionary<string, int> table)
        => key.Members ?? table.Keys.Order(StringComparer.Ordinal).ToList();

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    // ── Reading one key ──────────────────────────────────────────────────────

    private static Tuning ReadInt(Tuning tuning, IntKey key, JsonElement value, List<string> warnings)
    {
        if (Whole(value, key.Name, $"keeping {Number(key.Get(tuning))}", warnings) is not { } whole)
            return tuning;
        return key.Set(tuning, Clamp(whole, key.Min, key.Max, key.Name, warnings));
    }

    private static Tuning ReadBool(Tuning tuning, BoolKey key, JsonElement value, List<string> warnings)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return key.Set(tuning, value.GetBoolean());
        warnings.Add($"{Prefix}{key.Name} is {Describe(value)}, not true or false - keeping {(key.Get(tuning) ? "true" : "false")}");
        return tuning;
    }

    /// <summary>
    /// A table: an object naming members, each merged over the table the tuning
    /// already has (the compiled one, unless the file named this table twice).
    /// A member the table does not have warns and is skipped — for
    /// <c>ApplyPercent</c>, any id is taken here and checked once the catalogue
    /// is loaded (<see cref="ContentFiles.Load"/>).
    /// </summary>
    private static Tuning ReadTable(Tuning tuning, TableKey key, JsonElement value, List<string> warnings)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            warnings.Add(value.ValueKind == JsonValueKind.Array
                ? $"{Prefix}{key.Name} is a list - a table names each {key.MemberNoun} with its number, never a list - keeping the built-in table"
                : $"{Prefix}{key.Name} is {Describe(value)}, not a table of whole numbers - keeping the built-in table");
            return tuning;
        }

        var merged = new Dictionary<string, int>(key.Get(tuning), StringComparer.Ordinal);
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in value.EnumerateObject())
        {
            string label = $"{key.Name}.{entry.Name}";
            if (!named.Add(entry.Name))
                warnings.Add($"{Prefix}{label} is named twice - read in order, so the later one wins");
            if (key.Members is { } members ? !members.Contains(entry.Name) : entry.Name.Trim().Length == 0)
            {
                string? near = key.Members is { } known ? TuningSchema.Nearest(entry.Name, known) : null;
                warnings.Add($"{Prefix}{label} is not a {key.MemberNoun} - ignored" + (near is null ? "" : $"; did you mean {near}?"));
                continue;
            }

            string keeping = merged.TryGetValue(entry.Name, out int current) ? $"keeping {Number(current)}" : "ignored";
            if (Whole(entry.Value, label, keeping, warnings) is not { } whole)
                continue;
            merged[entry.Name] = Clamp(whole, key.Min, key.Max, label, warnings);
        }
        return key.Set(tuning, merged);
    }

    /// <summary>
    /// A name no key has: ignored, with a warning that points at the key the
    /// reader meant (1g) — the phase that brings one of §5.3's keys still to
    /// come, the code's name for a doc's spelling and why, or the key a typo is
    /// nearest — and never read as that key.
    /// </summary>
    private static Tuning Unknown(Tuning tuning, string name, List<string> warnings)
    {
        if (TuningSchema.DeferredPhase(name) is { } phase)
            warnings.Add($"{Prefix}{name} is not a key yet - it arrives with the {phase} code that reads it - ignored");
        else if (TuningSchema.Alias(name) is { } alias)
            warnings.Add($"{Prefix}{name} is not a key - ignored; the key is {alias.Key}" + (alias.Why is { } why ? $", {why}" : ""));
        else if (TuningSchema.Suggest(name) is { } near)
            warnings.Add($"{Prefix}{name} is not a key - ignored; did you mean {near}?");
        else
            warnings.Add($"{Prefix}{name} is not a key - ignored");
        return tuning;
    }

    /// <summary>
    /// A value as a whole number, or null with a warning when it is not one: a
    /// number written with a fraction or an exponent, a text, true or false, a
    /// null, a list or a table. A whole number past any range there is comes back
    /// as the end it lies past, for the clamp to name.
    /// </summary>
    private static (long Value, string Written)? Whole(JsonElement value, string label, string keeping, List<string> warnings)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            string raw = value.GetRawText();
            if (raw.IndexOfAny(['.', 'e', 'E']) >= 0)
            {
                warnings.Add($"{Prefix}{label} {raw} is not a whole number - {keeping}");
                return null;
            }
            long whole = long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : raw.StartsWith('-') ? long.MinValue : long.MaxValue;
            return (whole, raw);
        }

        warnings.Add(value.ValueKind == JsonValueKind.Array
            ? $"{Prefix}{label} is a list - a key holds one value, never a list - {keeping}"
            : $"{Prefix}{label} is {Describe(value)}, not a whole number - {keeping}");
        return null;
    }

    /// <summary>A whole number brought into [<paramref name="min"/>, <paramref name="max"/>], saying so when it had to be.</summary>
    private static int Clamp((long Value, string Written) whole, int min, int max, string label, List<string> warnings)
    {
        if (whole.Value < min)
        {
            warnings.Add($"{Prefix}{label} {whole.Written} is below {min} - using {min}");
            return min;
        }
        if (whole.Value > max)
        {
            warnings.Add($"{Prefix}{label} {whole.Written} is above {max} - using {max}");
            return max;
        }
        return (int)whole.Value;
    }

    /// <summary>
    /// The three clamps between keys, in this order, each with a warning: the
    /// boss floor range is never inverted; the carry shares evenly among the
    /// party; the deepest boss of the tuning's range pays less than a well-farmed
    /// two-modifier weapon banks (<see cref="ContentValidator.FarmBanks"/>). Each
    /// lands inside its key's clamp range: the offset needed is at most
    /// <c>99 - 2</c>, since the deepest floor is 99 and a farm banks at least 2.
    /// </summary>
    private static Tuning Relate(Tuning tuning, List<string> warnings)
    {
        if (tuning.BossFloorMax < tuning.BossFloorMin)
        {
            warnings.Add($"{Prefix}{nameof(Tuning.BossFloorMax)} {tuning.BossFloorMax} is shallower than {nameof(Tuning.BossFloorMin)} {tuning.BossFloorMin} - using {tuning.BossFloorMin}");
            tuning = tuning with { BossFloorMax = tuning.BossFloorMin };
        }

        int carry = tuning.PartyCarrySlots;
        int even = carry / GameConstants.PartySize * GameConstants.PartySize;
        if (even != carry)
        {
            warnings.Add($"{Prefix}{nameof(Tuning.PartyCarrySlots)} {carry} does not share evenly among {GameConstants.PartySize} members - using {even}");
            tuning = tuning with { PartyCarrySlots = even };
        }

        int banks = ContentValidator.FarmBanks(tuning.AcquiredHeadroom);
        int pays = DungeonState.PromisedStacksFor(tuning.BossFloorMax, tuning);
        if (pays >= banks)
        {
            int offset = tuning.BossFloorMax - banks + 1;
            warnings.Add($"{Prefix}{nameof(Tuning.BossStackOffset)} {tuning.BossStackOffset} lets a floor-{tuning.BossFloorMax} boss pay {pays} stacks, and a well-farmed weapon banks {banks} - using {offset}");
            tuning = tuning with { BossStackOffset = offset };
        }
        return tuning;
    }

    /// <summary>What a value is, in the words a warning uses: its number, the text it holds, true, false, null, a list, a table.</summary>
    private static string Describe(JsonElement value) => value.ValueKind switch
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

    /// <summary>A file that is not JSON at all: where the reader stopped, and why, in words a notice can show.</summary>
    private static string Malformed(JsonException error)
    {
        long line = (error.LineNumber ?? 0) + 1;
        long column = (error.BytePositionInLine ?? 0) + 1;
        string reason = error.Message;
        int where = reason.IndexOf(" LineNumber:", StringComparison.Ordinal);   // the reader appends the position, said above in words
        if (where >= 0)
            reason = reason[..where];
        reason = reason.Replace("'", "", StringComparison.Ordinal).Replace("\"", "", StringComparison.Ordinal).Trim().TrimEnd('.');
        return $"{Prefix}line {line}, column {column} is not valid JSON ({reason}) - the built-in numbers are playing";
    }

    // ── Writing ──────────────────────────────────────────────────────────────

    private static readonly string[] Header =
    [
        "/*",
        " * tuning.json - every number DistanceRPG is tuned by, on one screen.",
        " *",
        " * Read once, when the game starts: edit a number, save the file, and start the game again to play",
        " * under it. A key left out keeps its built-in number, so a file may name only what it changes. A key",
        " * the game does not know, a value out of range, or one that is not a whole number costs a warning on",
        " * screen and a line in the log - never a crash: the game clamps it and plays on. Every value is a",
        " * whole number, true or false, or a table of whole numbers by name: a ratio is an integer divisor",
        " * with its units in its name, and a percentage says Percent.",
        " *",
        " * The copy to edit is the one beside the game (bin/Debug/net10.0/tuning.json - the log prints its",
        " * full path). GameEngine.DistanceRPG/Data/tuning.json is generated from the compiled numbers",
        " * (DistanceRPG --write-data <folder>) and pinned by the tests: a number worth keeping goes into",
        " * Logic/Content/Tuning.cs, and the file is generated again.",
        " *",
        " * Nothing the golden tests pin - the map generator, the random streams, the pathfinder - is here.",
        " */",
    ];

    private const string Indent = "  ";

    /// <summary>How wide a comment runs before it wraps, indentation included.</summary>
    private const int CommentWidth = 100;

    /// <summary>One key's line — or, for a table, its block — with the comma JSON wants after every key but the last.</summary>
    private static void WriteKey(StringBuilder text, TuningKey key, Tuning tuning, bool isLast)
    {
        string comma = isLast ? "" : ",";
        switch (key)
        {
            case IntKey k:
                text.Append($"{Indent}{Quote(k.Name)}: {Number(k.Get(tuning))}{comma}\n");
                break;
            case BoolKey k:
                text.Append($"{Indent}{Quote(k.Name)}: {(k.Get(tuning) ? "true" : "false")}{comma}\n");
                break;
            case TableKey k:
            {
                var table = k.Get(tuning);
                var members = MembersOf(k, table);
                text.Append($"{Indent}{Quote(k.Name)}: {{\n");
                for (int i = 0; i < members.Count; i++)
                {
                    string member = members[i];
                    if (k.MemberNotes.TryGetValue(member, out string? note))
                        Comment(text, note, Indent + Indent);
                    text.Append($"{Indent}{Indent}{Quote(member)}: {Number(table[member])}{(i < members.Count - 1 ? "," : "")}\n");
                }
                text.Append($"{Indent}}}{comma}\n");
                break;
            }
        }
    }

    /// <summary>A name as a JSON string.</summary>
    private static string Quote(string name) => $"\"{JsonEncodedText.Encode(name)}\"";

    /// <summary>A comment above a key or a member, wrapped at <see cref="CommentWidth"/>, its continuation lines aligned under its first word.</summary>
    private static void Comment(StringBuilder text, string words, string indent)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (string word in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && indent.Length + 3 + line.Length + 1 + word.Length > CommentWidth)
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
}
