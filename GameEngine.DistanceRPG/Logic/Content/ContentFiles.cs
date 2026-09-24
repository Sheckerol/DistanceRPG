namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The texts of the data files, as the edge found them beside the executable
/// (<c>DataFiles</c>): null for a file that is not there. <c>Logic/</c> takes
/// strings and never touches the disk, so every reader here is tested on text.
/// </summary>
public sealed record ContentTexts(string? Tuning = null, string? Restricted = null, string? Enchantments = null,
    string? Weapons = null, string? Dungeons = null, string? Party = null);

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
    /// when there was no file, the tuning fitted to the weapons, dungeons and
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
            notices.Add(Missing(TuningJson.FileName, "numbers"));
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
