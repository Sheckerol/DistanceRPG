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
/// fitted, one line each, for the screen and the log) and every number the
/// tuning the game runs under changed from the compiled one.
/// </summary>
public sealed record ContentLoad(GameContent? Content, ContentException? Error, IReadOnlyList<string> Notices, IReadOnlyList<string> TuningOverrides);

/// <summary>
/// The data files (§5.3, §5.4): read in §5.4's order, fitted and validated into
/// the content the game runs under, and written from the compiled content by
/// the generator (<c>DistanceRPG --write-data &lt;dir&gt;</c>), whose output is the
/// shipped files, pinned byte for byte by the tests.
/// <para>
/// <strong>Phase 5.1 reads <c>tuning.json</c> alone</strong>; the five content
/// files are the compiled records (<see cref="ContentDefaults"/>) until the
/// sub-steps that ship them. The tuning is read (clamped, never refused), fitted
/// to the content it will run beside (<see cref="TuningFit"/>) and handed to
/// <see cref="GameContent.Load"/> with it.
/// </para>
/// </summary>
public static class ContentFiles
{
    /// <summary>Every data file, in §5.4's load order: the tuning, then the relations, the enchantments, the weapons, the dungeons, the roster.</summary>
    public static IReadOnlyList<string> FileNames { get; } =
        [TuningJson.FileName, "restricted.json", "enchantments.json", "weapons.json", "dungeons.json", "party.json"];

    /// <summary>
    /// Load the content from <paramref name="texts"/>: the tuning read, a notice
    /// when there was no text (the file missing, or there and unreadable, which
    /// the notice tells apart), the tuning fitted to the weapons, dungeons and
    /// roster it will run beside, any <c>ApplyPercent</c> id the enchantments do
    /// not have dropped with a notice, then <see cref="GameContent.Load"/>. Never
    /// throws a <see cref="ContentException"/>: one comes back as
    /// <see cref="ContentLoad.Error"/>, and in Phase 5.1 none can, since every
    /// tuning a file can make is fitted to the compiled content.
    /// </summary>
    public static ContentLoad Load(ContentTexts texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var notices = new List<string>();

        if (texts.Tuning is null)
            notices.Add(NoText(texts, TuningJson.FileName, "numbers"));
        var read = TuningJson.Read(texts.Tuning);
        notices.AddRange(read.Warnings);

        var restricted = ContentDefaults.Restricted;
        var enchantments = ContentDefaults.Enchantments;
        var weapons = ContentDefaults.Weapons;
        var dungeons = ContentDefaults.Dungeons;
        var party = ContentDefaults.Party;

        var tuning = TuningFit.ToContent(read.Tuning, weapons, dungeons, party, notices);
        tuning = DialsForNoEnchantment(tuning, enchantments, notices);
        var overrides = TuningJson.Overrides(tuning);

        try
        {
            var content = GameContent.Load(tuning, restricted, enchantments, weapons, party, dungeons);
            return new ContentLoad(content, null, notices, overrides);
        }
        catch (ContentException error)
        {
            return new ContentLoad(null, error, notices, overrides);
        }
    }

    /// <summary>
    /// The generator: the compiled content as the data files it ships as, text
    /// for text — what <c>--write-data</c> writes and what the committed files
    /// are pinned to. Phase 5.1 writes <c>tuning.json</c>.
    /// </summary>
    public static ContentTexts WriteDefaults() => new(Tuning: TuningJson.Write(ContentDefaults.Tuning));

    /// <summary>The notice a data file that was not there gets: the compiled record played in its place, and said so, so a renamed or deleted file never passes unseen.</summary>
    internal static string Missing(string file, string what) => $"{file} is missing - the built-in {what} are playing";

    /// <summary>The notice a data file that is there but could not be read gets: why, in the edge's words, and the compiled record played in its place.</summary>
    internal static string Unreadable(string file, string why, string what) => $"{file} could not be read ({why}) - the built-in {what} are playing";

    /// <summary>The notice for a data file the edge handed no text for: <see cref="Unreadable"/> when it said the file is there and why it could not be read, <see cref="Missing"/> otherwise.</summary>
    private static string NoText(ContentTexts texts, string file, string what)
        => texts.Unreadable?.GetValueOrDefault(file) is { } why ? Unreadable(file, why, what) : Missing(file, what);

    /// <summary>
    /// An <c>ApplyPercent</c> id the enchantments do not have warns once the
    /// catalogue is known (1g) and is dropped, so the tuning the game hashes holds
    /// only numbers something reads. The compiled table's ids are the catalogue's
    /// own and are never the file's to lose.
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
