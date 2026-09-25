using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What reading a <c>tuning.json</c> made: the tuning — every key the file named,
/// clamped, over the numbers it was read over — the warnings it cost, each one
/// line a playtester can act on, and the numbers the result changed from the
/// ones it was read over (<c>MovementBudget 200</c>, <c>PerStack.Block 4</c>).
/// </summary>
public sealed record TuningRead(Tuning Tuning, IReadOnlyList<string> Warnings, IReadOnlyList<string> Overrides);

/// <summary>
/// <c>tuning.json</c> (§5.3): the numbers, read once at startup from the text a
/// caller hands in — <c>Logic/</c> never touches the disk — clamped rather than
/// refused, and hashed so a save can ask "are these the same numbers?". The
/// file is the master copy of every number (the content rework, 1f): the game
/// carries the shipped file built in, and no number has a second home in code.
/// <list type="bullet">
/// <item><strong>The built-in copy first, strictly</strong>
/// (<see cref="ReadBuiltIn"/>): it is read over <see cref="Tuning.Blank"/>, so
/// every key, and every member of a table keyed by an enum, must be there with
/// a number it can take — a gap is refused (the game stops, naming the key),
/// because there is no number under it.</item>
/// <item><strong>Then the copy beside the game, leniently, because a human
/// edits it</strong> (S:62-74; <see cref="Read"/>): comments and trailing commas
/// are allowed; a missing key keeps the built-in copy's number, so a partial file
/// is valid; a table merges per entry onto the built-in one; an unknown key or
/// table member warns and is ignored, so a constant added later never
/// invalidates a file; a value that is not a whole number — a fraction, a text,
/// a list, a null — warns and keeps the built-in number, since ratios are
/// integer divisors; a value out of range clamps to the bound and warns. Nothing
/// there throws on anything a file can say: malformed JSON plays the built-in
/// numbers and says which line, and a blank file plays them and says it is
/// empty.</item>
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

    /// <summary>What every warning about the file beside the game starts with, so a notice says which file it is about.</summary>
    internal const string Prefix = FileName + ": ";

    /// <summary>What every warning about the built-in copy starts with, and every refusal of it names in its detail.</summary>
    internal const string BuiltInPrefix = FileName + " (built-in): ";

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Read the built-in <c>tuning.json</c>'s text — the master copy the game
    /// carries — over <see cref="Tuning.Blank"/>, as <see cref="Read"/> reads a
    /// file, but refusing what would leave a key with no number under it: a text
    /// that is empty or not a table of keys, a key or a member of a table keyed by
    /// an enum that it does not name, and a value it cannot take (a fraction, a
    /// text, a list, a table where a number goes). An out-of-range value still
    /// clamps and warns, and a key or member it does not know still warns and is
    /// ignored, each a notice. It is the numbers every other read is measured
    /// against, so it overrides nothing.
    /// </summary>
    /// <exception cref="ContentException">The built-in copy leaves a number out (<see cref="ContentValidator.RuleFileShape"/>), naming the key — <c>MovementBudget</c>, <c>PerStack.Block</c> — and the detail <c>tuning.json (built-in): …</c>.</exception>
    public static TuningRead ReadBuiltIn(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var reading = new Reading(Tuning.Blank, strict: true);
        var tuning = reading.Read(text);
        return new TuningRead(tuning, reading.Warnings, []);
    }

    /// <summary>
    /// Read a <c>tuning.json</c>'s text over <paramref name="builtIn"/>, the
    /// built-in copy's tuning. Null — no file — is the built-in numbers, with no
    /// warning: the missing-file notice is <see cref="ContentFiles"/>'s, which
    /// every data file gets alike. A file with nothing in it — blank, as an
    /// editor that crashed while saving leaves one — is the built-in numbers too,
    /// and says so (<see cref="EmptyWarning"/>): a playtester whose numbers were wiped
    /// must not be told the file changes nothing, as if it were a copy with no
    /// edits. Never throws on anything the text says.
    /// </summary>
    public static TuningRead Read(string? text, Tuning builtIn)
    {
        ArgumentNullException.ThrowIfNull(builtIn);
        if (text is null)
            return new TuningRead(builtIn, [], []);
        var reading = new Reading(builtIn, strict: false);
        var tuning = reading.Read(text);
        return new TuningRead(tuning, reading.Warnings, Overrides(tuning, builtIn));
    }

    /// <summary>The warning a blank <c>tuning.json</c> beside the game raises: it plays the built-in numbers, as a missing one does, and is not missing.</summary>
    internal const string EmptyWarning = Prefix + "the file is empty - the built-in numbers are playing";

    /// <summary>
    /// Whether two tunings are the same numbers (S:99-107): the first 16 lowercase
    /// hex digits of SHA-256 over the effective values as canonical text — one
    /// <c>Key=value</c> line per scalar and per table entry (every member of an
    /// enum table, every id <c>ApplyPercent</c> names), invariant integers,
    /// <c>true</c>/<c>false</c>, lines sorted ordinally. So a file's formatting,
    /// key order and comments never move it, and any value does — a number the
    /// master copy ships, changed by a new build, included. A save stores this,
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
    /// Every number <paramref name="tuning"/> holds that <paramref name="builtIn"/>
    /// does not, in the file's order, as <c>MovementBudget 200</c> or
    /// <c>PerStack.Block 4</c>: what a file changed from the built-in copy, for
    /// the log. A key set to the built-in number is not a change.
    /// </summary>
    internal static IReadOnlyList<string> Overrides(Tuning tuning, Tuning builtIn)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(builtIn);
        var was = Values(builtIn).ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        return Values(tuning)
            .Where(v => !was.TryGetValue(v.Name, out string? before) || before != v.Value)
            .Select(v => $"{v.Name} {v.Value}")
            .ToList();
    }

    /// <summary>Every value the tuning holds, keyed as the hash and the overrides name it: <c>MovementBudget</c>, <c>PerStack.Block</c>.</summary>
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

    /// <summary>A table's members in the file's order: an enum's in the enum's order, enchantment ids ordinally — those the table names.</summary>
    private static IReadOnlyList<string> MembersOf(TableKey key, IReadOnlyDictionary<string, int> table)
        => key.Members?.Where(table.ContainsKey).ToList() ?? table.Keys.Order(StringComparer.Ordinal).ToList();

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>What a value is, in the words a warning uses — the content files' words too (<see cref="JsonShape.Describe"/>).</summary>
    private static string Describe(JsonElement value) => JsonShape.Describe(value);

    /// <summary>
    /// One reading of one text over one tuning: the warnings it raises, and —
    /// reading the built-in copy (<paramref name="strict"/>) — the refusal of a
    /// number it leaves out. The same walk for both, so the built-in copy is read
    /// exactly as a file beside the game is, but for what it may leave out.
    /// </summary>
    private sealed class Reading(Tuning under, bool strict)
    {
        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings => _warnings;

        private string Prefix => strict ? BuiltInPrefix : TuningJson.Prefix;

        public Tuning Read(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (strict)
                    throw Gap(FileName, JsonShape.Top, "the file is empty");
                _warnings.Add(EmptyWarning);
                return under;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(text, Options);
            }
            catch (JsonException error)
            {
                var (line, column, reason) = JsonShape.SyntaxError(error);
                if (strict)
                    throw Gap(FileName, $"line {line}, column {column}", $"is not valid JSON ({reason})");
                _warnings.Add($"{Prefix}line {line}, column {column} is not valid JSON ({reason}) - the built-in numbers are playing");
                return under;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    if (strict)
                        throw Gap(FileName, JsonShape.Top, $"is {Describe(root)}, not a table of keys");
                    _warnings.Add($"{Prefix}the file is {Describe(root)}, not a table of keys - the built-in numbers are playing");
                    return under;
                }

                var tuning = under;
                var named = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in root.EnumerateObject())
                {
                    if (!named.Add(property.Name))
                        _warnings.Add($"{Prefix}{property.Name} is named twice - read in order, so the later one wins where they differ");
                    tuning = TuningSchema.Find(property.Name) switch
                    {
                        IntKey key => ReadInt(tuning, key, property.Value),
                        BoolKey key => ReadBool(tuning, key, property.Value),
                        TableKey key => ReadTable(tuning, key, property.Value),
                        _ => Unknown(tuning, property.Name),
                    };
                }

                if (strict)
                    RequireEveryNumber(tuning, named);
                return Relate(tuning);
            }
        }

        /// <summary>
        /// The built-in copy names every key, and every member of every table
        /// keyed by an enum, in the file's order: the first it leaves out is
        /// refused, since nothing stands under the built-in copy to fill it.
        /// <c>ApplyPercent</c>'s members are enchantment ids, which only the
        /// catalogue can answer for (<see cref="ContentValidator.ValidateDials"/>).
        /// </summary>
        private static void RequireEveryNumber(Tuning tuning, IReadOnlySet<string> named)
        {
            foreach (var key in TuningSchema.Keys)
            {
                if (!named.Contains(key.Name))
                    throw Gap(key.Name, key.Name, "is missing - the built-in copy names every key");
                if (key is TableKey { Members: { } members } table)
                {
                    var read = table.Get(tuning);
                    foreach (string member in members)
                        if (!read.ContainsKey(member))
                        {
                            string label = $"{table.Name}.{member}";
                            throw Gap(label, label, $"is missing - the built-in {table.Name} names every {table.MemberNoun}");
                        }
                }
            }
        }

        /// <summary>A number the built-in copy leaves out, or cannot give: the game stops, naming the key (<see cref="ContentValidator.RuleFileShape"/>).</summary>
        private static ContentException Gap(string entry, string path, string problem)
            => new(entry, ContentValidator.RuleFileShape, $"{BuiltInPrefix}{path}: {problem}");

        private Tuning ReadInt(Tuning tuning, IntKey key, JsonElement value)
        {
            if (Whole(value, key.Name, $"keeping {Number(key.Get(tuning))}") is not { } whole)
                return tuning;
            return key.Set(tuning, Clamp(whole, key.Min, key.Max, key.Name));
        }

        private Tuning ReadBool(Tuning tuning, BoolKey key, JsonElement value)
        {
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return key.Set(tuning, value.GetBoolean());
            if (strict)
                throw Gap(key.Name, key.Name, $"is {Describe(value)}, not true or false");
            _warnings.Add($"{Prefix}{key.Name} is {Describe(value)}, not true or false - keeping {(key.Get(tuning) ? "true" : "false")}");
            return tuning;
        }

        /// <summary>
        /// A table: an object naming members, each merged over the table the tuning
        /// already has (the built-in one, unless the file named this table twice).
        /// A member the table does not have warns and is skipped — for
        /// <c>ApplyPercent</c>, any id is taken here and checked once the catalogue
        /// is loaded (<see cref="ContentFiles.Load"/>).
        /// </summary>
        private Tuning ReadTable(Tuning tuning, TableKey key, JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                if (strict)
                    throw Gap(key.Name, key.Name, $"is {Describe(value)}, not a table naming each {key.MemberNoun} with its number");
                _warnings.Add(value.ValueKind == JsonValueKind.Array
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
                    _warnings.Add($"{Prefix}{label} is named twice - read in order, so the later one wins");
                if (key.Members is { } members ? !members.Contains(entry.Name) : entry.Name.Trim().Length == 0)
                {
                    string? near = key.Members is { } known ? TuningSchema.Nearest(entry.Name, known) : null;
                    _warnings.Add($"{Prefix}{label} is not a {key.MemberNoun} - ignored" + (near is null ? "" : $"; did you mean {near}?"));
                    continue;
                }

                string keeping = merged.TryGetValue(entry.Name, out int current) ? $"keeping {Number(current)}" : "ignored";
                if (Whole(entry.Value, label, keeping) is not { } whole)
                    continue;
                merged[entry.Name] = Clamp(whole, key.Min, key.Max, label);
            }
            return key.Set(tuning, merged);
        }

        /// <summary>
        /// A name no key has: ignored, with a warning that points at the key the
        /// reader meant (1g) — the phase that brings one of §5.3's keys still to
        /// come, the code's name for a doc's spelling and why, or the key a typo is
        /// nearest — and never read as that key.
        /// </summary>
        private Tuning Unknown(Tuning tuning, string name)
        {
            if (TuningSchema.DeferredPhase(name) is { } phase)
                _warnings.Add($"{Prefix}{name} is not a key yet - it arrives with the {phase} code that reads it - ignored");
            else if (TuningSchema.Alias(name) is { } alias)
                _warnings.Add($"{Prefix}{name} is not a key - ignored; the key is {alias.Key}" + (alias.Why is { } why ? $", {why}" : ""));
            else if (TuningSchema.Suggest(name) is { } near)
                _warnings.Add($"{Prefix}{name} is not a key - ignored; did you mean {near}?");
            else
                _warnings.Add($"{Prefix}{name} is not a key - ignored");
            return tuning;
        }

        /// <summary>
        /// A value as a whole number, or null with a warning when it is not one: a
        /// number written with a fraction or an exponent, a text, true or false, a
        /// null, a list or a table. A whole number past any range there is comes back
        /// as the end it lies past, for the clamp to name. Reading the built-in copy,
        /// a value that is not a whole number is refused: there is no number to keep.
        /// </summary>
        private (long Value, string Written)? Whole(JsonElement value, string label, string keeping)
        {
            if (value.ValueKind == JsonValueKind.Number)
            {
                string raw = value.GetRawText();
                if (raw.IndexOfAny(['.', 'e', 'E']) >= 0)
                {
                    if (strict)
                        throw Gap(label, label, $"is {raw}, not a whole number");
                    _warnings.Add($"{Prefix}{label} {raw} is not a whole number - {keeping}");
                    return null;
                }
                long whole = long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed)
                    ? parsed
                    : raw.StartsWith('-') ? long.MinValue : long.MaxValue;
                return (whole, raw);
            }

            if (strict)
                throw Gap(label, label, value.ValueKind == JsonValueKind.Array
                    ? "is a list - a key holds one value, never a list"
                    : $"is {Describe(value)}, not a whole number");
            _warnings.Add(value.ValueKind == JsonValueKind.Array
                ? $"{Prefix}{label} is a list - a key holds one value, never a list - {keeping}"
                : $"{Prefix}{label} is {Describe(value)}, not a whole number - {keeping}");
            return null;
        }

        /// <summary>A whole number brought into [<paramref name="min"/>, <paramref name="max"/>], saying so when it had to be.</summary>
        private int Clamp((long Value, string Written) whole, int min, int max, string label)
        {
            if (whole.Value < min)
            {
                _warnings.Add($"{Prefix}{label} {whole.Written} is below {min} - using {min}");
                return min;
            }
            if (whole.Value > max)
            {
                _warnings.Add($"{Prefix}{label} {whole.Written} is above {max} - using {max}");
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
        private Tuning Relate(Tuning tuning)
        {
            if (tuning.BossFloorMax < tuning.BossFloorMin)
            {
                _warnings.Add($"{Prefix}{nameof(Tuning.BossFloorMax)} {tuning.BossFloorMax} is shallower than {nameof(Tuning.BossFloorMin)} {tuning.BossFloorMin} - using {tuning.BossFloorMin}");
                tuning = tuning with { BossFloorMax = tuning.BossFloorMin };
            }

            int carry = tuning.PartyCarrySlots;
            int even = carry / GameConstants.PartySize * GameConstants.PartySize;
            if (even != carry)
            {
                _warnings.Add($"{Prefix}{nameof(Tuning.PartyCarrySlots)} {carry} does not share evenly among {GameConstants.PartySize} members - using {even}");
                tuning = tuning with { PartyCarrySlots = even };
            }

            int banks = ContentValidator.FarmBanks(tuning.AcquiredHeadroom);
            int pays = DungeonState.PromisedStacksFor(tuning.BossFloorMax, tuning);
            if (pays >= banks)
            {
                int offset = tuning.BossFloorMax - banks + 1;
                _warnings.Add($"{Prefix}{nameof(Tuning.BossStackOffset)} {tuning.BossStackOffset} lets a floor-{tuning.BossFloorMax} boss pay {pays} stacks, and a well-farmed weapon banks {banks} - using {offset}");
                tuning = tuning with { BossStackOffset = offset };
            }
            return tuning;
        }
    }
}
