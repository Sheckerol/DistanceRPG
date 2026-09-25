namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The texts of the data files, as the edge found them beside the executable
/// (<c>DataFiles</c>): null for a file with no text, whether it is not there or
/// it is there and could not be read. <c>Logic/</c> takes strings and never
/// touches the disk, so every reader here is tested on text. There is no text
/// for the enchantment catalogue or the starting party: both are code
/// (<see cref="EnchantmentDefinitions"/>, <see cref="StartingParty"/>).
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
/// <param name="Retired">
/// The files the game once read and no longer does that the edge found beside
/// it anyway (<see cref="ContentFiles.Retired"/>: <c>enchantments.json</c>,
/// <c>party.json</c>), by name, in the order it looked; null when it found none.
/// Never read — only said, so an edit a playtester makes in one never passes
/// as an edit that plays.
/// </param>
public sealed record ContentTexts(string? Tuning = null, string? Restricted = null, string? Weapons = null, string? Dungeons = null,
    IReadOnlyDictionary<string, string>? Unreadable = null, IReadOnlyList<string>? Retired = null);

/// <summary>
/// What loading the data files made: the content the game runs under, or the
/// <see cref="ContentException"/> that stops it — never both — and, either way,
/// every notice the load raised (a file missing, a file no longer read found
/// beside the game, a tuning value clamped or fitted, one line each, for the
/// screen and the log) and every number the tuning the game runs under changed
/// from the compiled one.
/// </summary>
public sealed record ContentLoad(GameContent? Content, ContentException? Error, IReadOnlyList<string> Notices, IReadOnlyList<string> TuningOverrides);

/// <summary>
/// The data files (§5.3, §5.4): read in the load order, fitted and validated
/// into the content the game runs under, and written from the compiled content
/// by the generator (<c>DistanceRPG --write-data &lt;dir&gt;</c>), whose output
/// is the shipped files, pinned byte for byte by the tests.
/// <para>
/// <strong>All four are read</strong>: <c>tuning.json</c>, then the three
/// content files (§5.4) — <c>restricted.json</c>, <c>weapons.json</c> and
/// <c>dungeons.json</c>. The enchantment catalogue and the starting party are
/// reference data in code (the user's ruling of 2026-09-25:
/// <see cref="EnchantmentDefinitions"/>, <see cref="StartingParty"/>), handed to
/// the load beside them and checked like any content: the catalogue first, on
/// its own, and then against the relations; the party last, against the
/// weapons. The tuning is read first (clamped, never refused); then every
/// present content file is parsed, its shape failure recorded rather than
/// thrown; then the tuning is fitted to the weapons and dungeons that parsed
/// and to the starting party (<see cref="TuningFit"/>); then the staged load
/// validates them in order and reports the first stage to break
/// (<see cref="GameContent"/>'s staged <c>Load</c>, 1h).
/// </para>
/// <para>
/// <strong>A file that is not there is its compiled record, and says so</strong>:
/// a notice (<c>weapons.json is missing - the built-in weapons are playing</c>),
/// and the edge logs the path it looked in. A file that is there is read whole
/// — never merged entry by entry with the compiled one, as the tuning's keys
/// are — and records from files and compiled records validate together like any
/// content.
/// </para>
/// <para>
/// <strong>A file the game no longer reads is said, never read</strong>
/// (<see cref="Retired"/>): an <c>enchantments.json</c> or a <c>party.json</c>
/// left beside the game gets a notice naming where what it held lives now, so
/// an edit made in one is never taken for an edit that plays.
/// </para>
/// </summary>
public static class ContentFiles
{
    /// <summary>The weapons' file (§5.6): <see cref="WeaponsJson.FileName"/>, by the name the staged load stamps its stage with.</summary>
    internal const string WeaponsFileName = WeaponsJson.FileName;

    /// <summary>The dungeons' file (§5.8): <see cref="DungeonsJson.FileName"/>, by the name the staged load stamps its stage with.</summary>
    internal const string DungeonsFileName = DungeonsJson.FileName;

    /// <summary>Every data file, in the load order: the tuning, then the relations, the weapons, the dungeons.</summary>
    public static IReadOnlyList<string> FileNames { get; } =
        [TuningJson.FileName, RestrictedJson.FileName, WeaponsFileName, DungeonsFileName];

    /// <summary>
    /// The data files the game read once and no longer does (the content rework,
    /// 1c): each by name, with the notice a copy found beside the game gets —
    /// that it is no longer read, and where what it held lives now. The edge
    /// looks for each (<c>DataFiles</c>) and never reads one; the load says each
    /// it was told of.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Retired { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["enchantments.json"] = "enchantments.json is no longer read - the enchantment catalogue is in the code (Logic/Enchantments/EnchantmentDefinitions.cs)",
        ["party.json"] = "party.json is no longer read - the starting party is in the code (Logic/Content/StartingParty.cs)",
    };

    /// <summary>
    /// Load the content from <paramref name="texts"/>: the tuning read, then the
    /// three content files parsed in the load order — a file with no text is its
    /// compiled record, with a notice that tells a missing file from one there and
    /// unreadable — then every retired file the edge found said, then the tuning
    /// fitted to the weapons, dungeons and starting party it will run beside (a
    /// file that did not parse is fitted to nothing), any <c>ApplyPercent</c> id
    /// the game's enchantment catalogue does not have dropped with a notice, and
    /// then the staged load, handed the code's catalogue and party beside the
    /// files. Never throws a <see cref="ContentException"/>: the first stage to
    /// break comes back as <see cref="ContentLoad.Error"/>, stamped with its file,
    /// beside every notice raised before it.
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
        var weapons = Parse(texts, texts.Weapons, WeaponsJson.FileName, "weapons", WeaponsJson.Read, ContentDefaults.Weapons, notices);
        var dungeons = Parse(texts, texts.Dungeons, DungeonsJson.FileName, "dungeons", DungeonsJson.Read, ContentDefaults.Dungeons, notices);
        foreach (string name in texts.Retired ?? [])
            notices.Add(RetiredNotice(name));

        // The catalogue and the party are the code's: handed to the load, never read.
        var enchantments = EnchantmentDefinitions.Data;
        var party = StartingParty.Data;

        var tuning = TuningFit.ToContent(read.Tuning, weapons.Data, dungeons.Data, party, notices);
        tuning = DialsForNoEnchantment(tuning, enchantments, notices);
        var overrides = TuningJson.Overrides(tuning);

        try
        {
            var content = GameContent.Load(tuning, enchantments, restricted, weapons, dungeons, party);
            return new ContentLoad(content, null, notices, overrides);
        }
        catch (ContentException error)
        {
            return new ContentLoad(null, error, notices, overrides);
        }
    }

    /// <summary>
    /// The generator (USER DECISION 3): the compiled content as the four data files
    /// it ships as, text for text — what <c>--write-data</c> writes and what the
    /// committed files are pinned to. <see cref="ContentDefaults"/> stays their one
    /// source; each file reads back to the record it was written from, and every
    /// row the docs did not fix says it is provisional. The enchantment catalogue
    /// and the starting party are code, and have no file.
    /// </summary>
    public static ContentTexts WriteDefaults() => new(
        Tuning: TuningJson.Write(ContentDefaults.Tuning),
        Restricted: RestrictedJson.Write(ContentDefaults.Restricted),
        Weapons: WeaponsJson.Write(ContentDefaults.Weapons),
        Dungeons: DungeonsJson.Write(ContentDefaults.Dungeons));

    /// <summary>The notice a data file that was not there gets: the compiled record played in its place, and said so, so a renamed or deleted file never passes unseen.</summary>
    internal static string Missing(string file, string what) => $"{file} is missing - the built-in {what} are playing";

    /// <summary>The notice a data file that is there but could not be read gets: why, in the edge's words, and the compiled record played in its place.</summary>
    internal static string Unreadable(string file, string why, string what) => $"{file} could not be read ({why}) - the built-in {what} are playing";

    /// <summary>The notice a retired file found beside the game gets (<see cref="Retired"/>); a name the table does not know is still said as no longer read.</summary>
    private static string RetiredNotice(string file)
        => Retired.TryGetValue(file, out var notice) ? notice : $"{file} is no longer read";

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
    /// An <c>ApplyPercent</c> id the game's enchantment catalogue does not have
    /// warns (1g) and is dropped, so the tuning the game hashes holds only
    /// numbers something reads. The compiled table's ids are the catalogue's own
    /// and are never the file's to lose. The catalogue is code, so it is always
    /// there to ask.
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
}
