namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The texts of the data files: as the edge found them beside the executable
/// (<c>DataFiles</c>), or as the game carries them built in (<c>ShippedData</c>).
/// Null for a file with no text, whether it is not there or it is there and could
/// not be read. <c>Logic/</c> takes strings and never touches the disk — nor
/// reads what the build carries — so every reader here is tested on text. There
/// is no text for the enchantment catalogue or the starting party: both are code
/// (<see cref="EnchantmentDefinitions"/>, <see cref="StartingParty"/>).
/// </summary>
/// <param name="Unreadable">
/// The files that are there but could not be read, by file name
/// (<c>tuning.json</c>), each with the reason in a few words
/// (<c>another program has it open</c>). The reason comes from the edge, which
/// saw the error; the full error goes to the log. It is null when every file
/// that is there was read. The notice then says the file could not be read,
/// rather than that it is missing: a playtester looking at the file in its
/// folder must not be told it is not there. For the built-in texts, the copies
/// the build does not carry, or that could not be read out of it, with why.
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
/// from the built-in copy's.
/// </summary>
public sealed record ContentLoad(GameContent? Content, ContentException? Error, IReadOnlyList<string> Notices, IReadOnlyList<string> TuningOverrides)
{
    /// <summary>
    /// The tuning the load fitted and would play — what the game hashes and the
    /// log summarises — or null when there is none: the built-in
    /// <c>tuning.json</c> did not read, so no number stands under anything, and
    /// <see cref="TuningOverrides"/> is empty.
    /// </summary>
    public Tuning? Tuning { get; init; }

    /// <summary>
    /// The content files whose text came from beside the executable, in the load
    /// order — each one a playtester may have edited. None means every content
    /// file played its built-in copy, so a failure stamped built-in is the master
    /// copy's own, and the screen says to fix the master.
    /// </summary>
    public IReadOnlyList<string> ReadBeside { get; init; } = [];
}

/// <summary>
/// The data files (§5.3, §5.4): read in the load order, fitted and validated
/// into the content the game runs under. The shipped files in
/// <c>GameEngine.DistanceRPG/Data</c> are the master copy of every number,
/// relation, weapon and dungeon (the user's ruling of 2026-09-25); the game
/// carries them built in, and each is the fallback for its copy beside the
/// executable.
/// <para>
/// <strong>All four are read</strong>: <c>tuning.json</c>, then the three
/// content files (§5.4) — <c>restricted.json</c>, <c>weapons.json</c> and
/// <c>dungeons.json</c>. The enchantment catalogue and the starting party are
/// reference data in code (<see cref="EnchantmentDefinitions"/>,
/// <see cref="StartingParty"/>), handed to the load beside them and checked like
/// any content: the catalogue first, on its own, and then against the
/// relations; the party last, against the weapons. The built-in tuning is read
/// first, strictly — it is every number there is — and the one beside the game
/// over it, clamped, never refused; then every content file is parsed, its shape
/// failure recorded rather than thrown; then the tuning is fitted to the weapons
/// and dungeons that parsed and to the starting party (<see cref="TuningFit"/>);
/// then the staged load validates them in order and reports the first stage to
/// break (<see cref="GameContent"/>'s staged <c>Load</c>, 1h).
/// </para>
/// <para>
/// <strong>A file that is not there plays its built-in copy, and says so</strong>:
/// a notice (<c>weapons.json is missing - the built-in weapons are playing</c>),
/// and the edge logs the path it looked in. A file that is there is read whole
/// — never merged entry by entry with the built-in copy, as the tuning's keys
/// are — and a built-in copy is read only when it plays: a sound copy beside the
/// game covers a broken built-in one. A built-in copy that plays and is broken,
/// or that the build does not carry, stops the game like any broken file, marked
/// built-in.
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
    /// Load the content from <paramref name="files"/>, the texts found beside the
    /// game, over <paramref name="builtIn"/>, the texts the game carries (the
    /// staged load, 1h): the built-in <c>tuning.json</c> read strictly — a gap
    /// there is the one failure that comes back before anything else is read,
    /// with no tuning — and the one beside the game read over it; the three
    /// content files parsed in the load order, each from its text beside the
    /// game, else from its built-in text with a notice that tells a missing file
    /// from one there and unreadable; every retired file the edge found said; the
    /// tuning fitted to the weapons, dungeons and starting party it will run
    /// beside (a file that did not parse is fitted to nothing), any
    /// <c>ApplyPercent</c> id the game's enchantment catalogue does not have
    /// dropped with a notice; and then the staged load, handed the code's
    /// catalogue and party beside the files. Never throws a
    /// <see cref="ContentException"/>: the first stage to break comes back as
    /// <see cref="ContentLoad.Error"/>, stamped with its file, beside every notice
    /// raised before it.
    /// </summary>
    public static ContentLoad Load(ContentTexts files, ContentTexts builtIn)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(builtIn);
        var notices = new List<string>();

        // (1) The built-in tuning, strictly: it is every number the game has, so
        // a gap in it — or a copy the build lacks — stops the game at once, with
        // no tuning to compare anything with or to hash.
        Tuning under;
        try
        {
            var strict = TuningJson.ReadBuiltIn(builtIn.Tuning ?? throw NotCarried(builtIn, TuningJson.FileName));
            notices.AddRange(strict.Warnings);
            under = strict.Tuning;
        }
        catch (ContentException failure)
        {
            return new ContentLoad(null, failure.In(TuningJson.FileName, builtIn: true), notices, []);
        }

        // Then tuning.json beside the game, leniently over it: it never stops the game.
        if (files.Tuning is null)
            notices.Add(NoText(files, TuningJson.FileName, "numbers"));
        var read = TuningJson.Read(files.Tuning, under);
        notices.AddRange(read.Warnings);

        // (2) The content files, each parsed from beside the game or from its built-in copy.
        var readBeside = new List<string>();
        var restricted = Parse(files, builtIn, RestrictedJson.FileName, "relations", t => t.Restricted, RestrictedJson.Read, notices, readBeside);
        var weapons = Parse(files, builtIn, WeaponsJson.FileName, "weapons", t => t.Weapons, WeaponsJson.Read, notices, readBeside);
        var dungeons = Parse(files, builtIn, DungeonsJson.FileName, "dungeons", t => t.Dungeons, DungeonsJson.Read, notices, readBeside);
        foreach (string name in files.Retired ?? [])
            notices.Add(RetiredNotice(name));

        // The catalogue and the party are the code's: handed to the load, never read.
        var enchantments = EnchantmentDefinitions.Data;
        var party = StartingParty.Data;

        // (3) The fit.
        var tuning = TuningFit.ToContent(read.Tuning, weapons.Data, dungeons.Data, party, notices);
        tuning = DialsForNoEnchantment(tuning, enchantments, notices);
        var overrides = TuningJson.Overrides(tuning, under);

        // (4) The staged load.
        try
        {
            var content = GameContent.Load(tuning, enchantments, restricted, weapons, dungeons, party);
            return new ContentLoad(content, null, notices, overrides) { Tuning = tuning, ReadBeside = readBeside };
        }
        catch (ContentException error)
        {
            return new ContentLoad(null, error, notices, overrides) { Tuning = tuning, ReadBeside = readBeside };
        }
    }

    /// <summary>The notice a data file that was not there gets: its built-in copy played in its place, and said so, so a renamed or deleted file never passes unseen.</summary>
    internal static string Missing(string file, string what) => $"{file} is missing - the built-in {what} are playing";

    /// <summary>The notice a data file that is there but could not be read gets: why, in the edge's words, and its built-in copy played in its place.</summary>
    internal static string Unreadable(string file, string why, string what) => $"{file} could not be read ({why}) - the built-in {what} are playing";

    /// <summary>The notice a retired file found beside the game gets (<see cref="Retired"/>); a name the table does not know is still said as no longer read.</summary>
    private static string RetiredNotice(string file)
        => Retired.TryGetValue(file, out var notice) ? notice : $"{file} is no longer read";

    /// <summary>The notice for a data file the edge handed no text for: <see cref="Unreadable"/> when it said the file is there and why it could not be read, <see cref="Missing"/> otherwise.</summary>
    private static string NoText(ContentTexts texts, string file, string what)
        => texts.Unreadable?.GetValueOrDefault(file) is { } why ? Unreadable(file, why, what) : Missing(file, what);

    /// <summary>
    /// A built-in copy the load needs and the build does not carry — the texts
    /// handed in have none for it, and say why in their
    /// <see cref="ContentTexts.Unreadable"/>: that copy's failure
    /// (<see cref="ContentValidator.RuleBuiltInCopy"/>), naming the file.
    /// </summary>
    private static ContentException NotCarried(ContentTexts builtIn, string file)
        => new(file, ContentValidator.RuleBuiltInCopy,
            $"{file} (built-in): {builtIn.Unreadable?.GetValueOrDefault(file) ?? "the game was built without it"}");

    /// <summary>
    /// One content file as the staged load takes it: the record its text beside
    /// the game reads to, or the reason it does not — recorded, not thrown, to be
    /// reported at the file's own stage (1h). With no text beside the game, the
    /// notice every absent file gets and its built-in copy, read the same way and
    /// marked built-in; a built-in copy the build lacks is the stage's failure.
    /// A built-in text is read only here, when it plays.
    /// </summary>
    private static ContentSource<T> Parse<T>(ContentTexts files, ContentTexts builtIn, string file, string what,
        Func<ContentTexts, string?> textOf, Func<string, T> read, ICollection<string> notices, ICollection<string> readBeside) where T : class
    {
        if (textOf(files) is { } text)
        {
            readBeside.Add(file);
            return Read(file, text, read, isBuiltIn: false);
        }

        notices.Add(NoText(files, file, what));
        return textOf(builtIn) is { } builtInText
            ? Read(file, builtInText, read, isBuiltIn: true)
            : new ContentSource<T>(file, null, NotCarried(builtIn, file), BuiltIn: true);
    }

    /// <summary>The record <paramref name="text"/> reads to, or the reason it does not, for its stage to report.</summary>
    private static ContentSource<T> Read<T>(string file, string text, Func<string, T> read, bool isBuiltIn) where T : class
    {
        try
        {
            return new ContentSource<T>(file, read(text), null, isBuiltIn);
        }
        catch (ContentException failure)
        {
            return new ContentSource<T>(file, null, failure, isBuiltIn);
        }
    }

    /// <summary>
    /// An <c>ApplyPercent</c> id the game's enchantment catalogue does not have
    /// warns (1g) and is dropped, so the tuning the game hashes holds only
    /// numbers something reads — the built-in copy's own ids included: none is
    /// exempt. The catalogue is code, so it is always there to ask.
    /// </summary>
    private static Tuning DialsForNoEnchantment(Tuning tuning, EnchantmentsData enchantments, ICollection<string> notices)
    {
        var ids = enchantments.Enchantments.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var strays = tuning.ApplyPercent.Keys
            .Where(id => !ids.Contains(id))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (strays.Count == 0)
            return tuning;

        foreach (string id in strays)
        {
            string? near = TuningSchema.Nearest(id, ids.Order(StringComparer.Ordinal));
            notices.Add($"{TuningJson.Prefix}{nameof(Tuning.ApplyPercent)}.{id} names no enchantment - ignored" + (near is null ? "" : $" - did you mean {near}?"));
        }
        var kept = tuning.ApplyPercent.Where(entry => !strays.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return tuning with { ApplyPercent = kept };
    }
}
