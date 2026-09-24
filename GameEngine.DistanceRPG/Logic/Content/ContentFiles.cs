namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The texts of the data files, as the edge found them beside the executable
/// (<c>DataFiles</c>): null for a file with no text, whether it is not there or
/// it is there and could not be read. <c>Logic/</c> takes strings and never
/// touches the disk, so every reader here is tested on text.
/// </summary>
/// <param name="Unreadable">
/// The files that are there but could not be read, by file name
/// (<c>tuning.json</c>), each with the reason in a few words
/// (<c>another program has it open</c>). The reason comes from the edge, which
/// saw the error; the full error goes to the log. It is null when every file
/// that is there was read. The notice then says the file could not be read,
/// rather than that it is missing: a playtester looking at the file in its
/// folder must not be told it is not there.
/// </param>
public sealed record ContentTexts(string? Tuning = null, string? Restricted = null, string? Enchantments = null,
    string? Weapons = null, string? Dungeons = null, string? Party = null,
    IReadOnlyDictionary<string, string>? Unreadable = null);

/// <summary>
/// What loading the data files made: the content the game runs under, or the
/// <see cref="ContentException"/> that stops it — never both — and, either way,
/// every notice the load raised (a file missing, a tuning value clamped or
/// fitted, a dialled number edited in the wrong file, one line each, for the
/// screen and the log) and every number the tuning the game runs under changed
/// from the compiled one.
/// </summary>
public sealed record ContentLoad(GameContent? Content, ContentException? Error, IReadOnlyList<string> Notices, IReadOnlyList<string> TuningOverrides);

/// <summary>
/// The data files (§5.3, §5.4): read in §5.4's order, fitted and validated into
/// the content the game runs under, and written from the compiled content by
/// the generator (<c>DistanceRPG --write-data &lt;dir&gt;</c>), whose output is the
/// shipped files, pinned byte for byte by the tests.
/// <para>
/// <strong>All six are read</strong>: <c>tuning.json</c>, then the five content
/// files (§5.4) — <c>restricted.json</c>, <c>enchantments.json</c>,
/// <c>weapons.json</c>, <c>dungeons.json</c> and <c>party.json</c>. The tuning
/// is read first (clamped, never refused); then every present content file is
/// parsed, its shape failure recorded rather than thrown; then the tuning is
/// fitted to the weapons, dungeons and roster that parsed
/// (<see cref="TuningFit"/>); then the staged load validates them in §5.4's
/// order and reports the first stage to break (<see cref="GameContent"/>'s
/// staged <c>Load</c>, 1h).
/// </para>
/// <para>
/// <strong>A file that is not there is its compiled record, and says so</strong>:
/// a notice (<c>enchantments.json is missing - the built-in enchantments are
/// playing</c>), and the edge logs the path it looked in. A file that is there
/// is read whole — never merged entry by entry with the compiled one, as the
/// tuning's keys are — and records from files and compiled records validate
/// together like any content.
/// </para>
/// </summary>
public static class ContentFiles
{
    /// <summary>The weapons' file (§5.6): <see cref="WeaponsJson.FileName"/>, by the name the staged load stamps its stage with.</summary>
    internal const string WeaponsFileName = WeaponsJson.FileName;

    /// <summary>The dungeons' file (§5.8): <see cref="DungeonsJson.FileName"/>, by the name the staged load stamps its stage with.</summary>
    internal const string DungeonsFileName = DungeonsJson.FileName;

    /// <summary>The roster's file (§5.9): <see cref="PartyJson.FileName"/>, by the name the staged load stamps its stage with.</summary>
    internal const string PartyFileName = PartyJson.FileName;

    /// <summary>Every data file, in §5.4's load order: the tuning, then the relations, the enchantments, the weapons, the dungeons, the roster.</summary>
    public static IReadOnlyList<string> FileNames { get; } =
        [TuningJson.FileName, RestrictedJson.FileName, EnchantmentsJson.FileName, WeaponsFileName, DungeonsFileName, PartyFileName];

    /// <summary>
    /// Load the content from <paramref name="texts"/>: the tuning read, then the
    /// five content files parsed in §5.4's order — a file with no text is its
    /// compiled record, with a notice that tells a missing file from one there and
    /// unreadable — then the tuning fitted to the weapons, dungeons and roster it
    /// will run beside (a file that did not parse is fitted to nothing), any
    /// <c>ApplyPercent</c> id the loaded enchantments do not have dropped with a
    /// notice, a dialled number the enchantments' file edits to no effect said
    /// with a notice, and then the staged load. Never throws a
    /// <see cref="ContentException"/>: the first stage to break comes back as
    /// <see cref="ContentLoad.Error"/>, stamped with its file, beside every notice
    /// raised before it.
    /// </summary>
    public static ContentLoad Load(ContentTexts texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var notices = new List<string>();

        if (texts.Tuning is null)
            notices.Add(NoText(texts, TuningJson.FileName, "numbers"));
        var read = TuningJson.Read(texts.Tuning);
        notices.AddRange(read.Warnings);

        var restricted = Parse(texts, texts.Restricted, RestrictedJson.FileName, "relations", RestrictedJson.Read, ContentDefaults.Restricted, notices);
        var enchantments = Parse(texts, texts.Enchantments, EnchantmentsJson.FileName, "enchantments", EnchantmentsJson.Read, ContentDefaults.Enchantments, notices);
        var weapons = Parse(texts, texts.Weapons, WeaponsJson.FileName, "weapons", WeaponsJson.Read, ContentDefaults.Weapons, notices);
        var dungeons = Parse(texts, texts.Dungeons, DungeonsJson.FileName, "dungeons", DungeonsJson.Read, ContentDefaults.Dungeons, notices);
        var party = Parse(texts, texts.Party, PartyJson.FileName, "party members", PartyJson.Read, ContentDefaults.Party, notices);

        var tuning = TuningFit.ToContent(read.Tuning, weapons.Data, dungeons.Data, party.Data, notices);
        if (enchantments.Data is { } catalogue)
        {
            tuning = DialsForNoEnchantment(tuning, catalogue, notices);
            DialledEdits(tuning, catalogue, notices);
        }
        var overrides = TuningJson.Overrides(tuning);

        try
        {
            var content = GameContent.Load(tuning, restricted, enchantments, weapons, dungeons, party);
            return new ContentLoad(content, null, notices, overrides);
        }
        catch (ContentException error)
        {
            return new ContentLoad(null, error, notices, overrides);
        }
    }

    /// <summary>
    /// The generator (USER DECISION 3): the compiled content as the six data files
    /// it ships as, text for text — what <c>--write-data</c> writes and what the
    /// committed files are pinned to. <see cref="ContentDefaults"/> stays the one
    /// source; each file reads back to the record it was written from. The
    /// enchantments' dialled fields say which <c>tuning.json</c> key sets them, and
    /// every row the docs did not fix says it is provisional.
    /// </summary>
    public static ContentTexts WriteDefaults() => new(
        Tuning: TuningJson.Write(ContentDefaults.Tuning),
        Restricted: RestrictedJson.Write(ContentDefaults.Restricted),
        Enchantments: EnchantmentsJson.Write(ContentDefaults.Enchantments, ContentDefaults.Tuning),
        Weapons: WeaponsJson.Write(ContentDefaults.Weapons),
        Dungeons: DungeonsJson.Write(ContentDefaults.Dungeons),
        Party: PartyJson.Write(ContentDefaults.Party));

    /// <summary>The notice a data file that was not there gets: the compiled record played in its place, and said so, so a renamed or deleted file never passes unseen.</summary>
    internal static string Missing(string file, string what) => $"{file} is missing - the built-in {what} are playing";

    /// <summary>The notice a data file that is there but could not be read gets: why, in the edge's words, and the compiled record played in its place.</summary>
    internal static string Unreadable(string file, string why, string what) => $"{file} could not be read ({why}) - the built-in {what} are playing";

    /// <summary>
    /// The notice a present content file gets when it edits a number a
    /// <c>tuning.json</c> dial owns: the edit cannot take effect, so the screen
    /// names both files and the number that plays.
    /// </summary>
    internal static string Overridden(string file, string field, int value, string dial, int dialled)
        => $"{file} {field} {value} is overridden by {TuningJson.FileName} {dial} {dialled}";

    /// <summary>The notice for a data file the edge handed no text for: <see cref="Unreadable"/> when it said the file is there and why it could not be read, <see cref="Missing"/> otherwise.</summary>
    private static string NoText(ContentTexts texts, string file, string what)
        => texts.Unreadable?.GetValueOrDefault(file) is { } why ? Unreadable(file, why, what) : Missing(file, what);

    /// <summary>
    /// One content file as the staged load takes it: its compiled record, with the
    /// notice every absent file gets, when there is no text; otherwise the record
    /// its text reads to, or the reason it does not — recorded, not thrown, to be
    /// reported at the file's own stage (1h).
    /// </summary>
    private static ContentSource<T> Parse<T>(ContentTexts texts, string? text, string file, string what, Func<string, T> read, T compiled,
        ICollection<string> notices) where T : class
    {
        if (text is null)
        {
            notices.Add(NoText(texts, file, what));
            return ContentSource<T>.Built(file, compiled);
        }
        try
        {
            return new ContentSource<T>(file, read(text), null, Compiled: false);
        }
        catch (ContentException failure)
        {
            return new ContentSource<T>(file, null, failure, Compiled: false);
        }
    }

    /// <summary>
    /// An <c>ApplyPercent</c> id the enchantments do not have warns once the
    /// catalogue is known (1g) and is dropped, so the tuning the game hashes holds
    /// only numbers something reads. The compiled table's ids are the catalogue's
    /// own and are never the file's to lose. Asked of the enchantments the load
    /// has — the file's when it parsed — and not at all when it did not, since a
    /// catalogue that is not loaded has no ids to be asked about.
    /// </summary>
    private static Tuning DialsForNoEnchantment(Tuning tuning, EnchantmentsData enchantments, ICollection<string> notices)
    {
        var ids = enchantments.Enchantments.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var strays = tuning.ApplyPercent.Keys
            .Where(id => !ids.Contains(id) && !ContentDefaults.Tuning.ApplyPercent.ContainsKey(id))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (strays.Count == 0)
            return tuning;

        foreach (string id in strays)
        {
            string? near = TuningSchema.Nearest(id, ids.Order(StringComparer.Ordinal));
            notices.Add($"{TuningJson.Prefix}{nameof(Tuning.ApplyPercent)}.{id} names no enchantment - ignored" + (near is null ? "" : $"; did you mean {near}?"));
        }
        var kept = tuning.ApplyPercent.Where(entry => !strays.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return tuning with { ApplyPercent = kept };
    }

    /// <summary>
    /// One owner for a dialled number: the dial (1h). A row's <c>applyPercent</c>
    /// for an id <see cref="Tuning.ApplyPercent"/> names, and Arcane's
    /// <c>potency</c>, are <c>tuning.json</c>'s — the catalogue lays the dial over
    /// them — so an edit to one in <c>enchantments.json</c> cannot take effect. It
    /// is said, naming both files, when the row's number differs both from the
    /// dial that plays and from the generator's number for that row: an edit, and
    /// one that changes nothing. A dial turned in <c>tuning.json</c> with the row
    /// untouched says nothing, and neither does a row the dial agrees with.
    /// </summary>
    private static void DialledEdits(Tuning tuning, EnchantmentsData enchantments, ICollection<string> notices)
    {
        var generated = ContentDefaults.Enchantments.Enchantments.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var row in enchantments.Enchantments)
        {
            generated.TryGetValue(row.Id, out var shipped);
            if (EnchantmentsJson.ApplyPercentDial(tuning, row.Id) is { } percentDial)
            {
                int plays = tuning.Lookup(x => x.ApplyPercent, row.Id, row.ApplyPercent);
                if (row.ApplyPercent != plays && row.ApplyPercent != shipped?.ApplyPercent)
                    notices.Add(Overridden(EnchantmentsJson.FileName, $"{row.Id}.applyPercent", row.ApplyPercent, percentDial, plays));
            }
            if (EnchantmentsJson.PotencyDial(row.Id) is { } potencyDial
                && row.Potency != tuning.ArcanePotency && row.Potency != shipped?.Potency)
                notices.Add(Overridden(EnchantmentsJson.FileName, $"{row.Id}.potency", row.Potency, potencyDial, tuning.ArcanePotency));
        }
    }
}
