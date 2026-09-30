using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One step of the save format's growth (PHASE5-002): lifts a document written
/// at version <paramref name="From"/> to <c>From + 1</c>. Pure — the document in,
/// the document out, no content and no state read — and it reshapes only: the
/// codec stamps the new version on what it returns. The seam a later phase's
/// state comes into the save through, whatever shape that phase gives it: every
/// row of the document is an object of named members, so a step can add to one
/// without reshaping the rest.
/// </summary>
/// <param name="From">The version this step reads.</param>
/// <param name="Apply">The document at <paramref name="From"/>, reshaped to the next version; it may be the same object, changed.</param>
public sealed record SaveMigration(int From, Func<JsonObject, JsonObject> Apply);

/// <summary>
/// The save as text (§5.2): <c>System.Text.Json</c> with an explicit version
/// field, through the source-generated contract (<see cref="SaveJsonContext"/>).
/// Strings in and out — the file is read and written at the edge, and never
/// from <c>Logic/</c>.
/// <list type="bullet">
/// <item><strong>The version is an integer, and this build writes
/// <see cref="CurrentVersion"/>.</strong> A document without one, or with
/// anything but a whole number there, is unreadable.</item>
/// <item><strong>Newer than this build is refused</strong>, whatever else it
/// holds: an older build cannot know what a newer one meant, and reading it
/// anyway would write the next save without it.</item>
/// <item><strong>Older runs the migrations in order</strong>, one
/// <see cref="SaveMigration"/> per version, and is read at the version it
/// reaches. A version no step lifts is unreadable.</item>
/// <item><strong>Unknown members are ignored; a missing one is not.</strong> A
/// member a later build of the same version added never breaks this reader,
/// and a member this reader needs and does not find is an error, never a
/// default.</item>
/// </list>
/// Every refusal is a <see cref="SaveFormatException"/> naming where.
/// </summary>
public static class SaveCodec
{
    /// <summary>
    /// The version this build writes, and the newest it reads. 2 since Phase 6a
    /// (a weapon's wear, and the version its tuning hash is of); a version-1
    /// document is lifted to it (<see cref="LiftToVersion2"/>).
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>The member every version of the document carries its version in.</summary>
    internal const string VersionMember = "version";

    /// <summary>The root member naming the save version whose keys the tuning hash is of (<see cref="SaveFile.TuningHashVersion"/>): version 2's.</summary>
    internal const string TuningHashVersionMember = "tuningHashVersion";

    /// <summary>A weapon row's wear banked (<see cref="WeaponDto.Wear"/>): version 2's.</summary>
    internal const string WearMember = "wear";

    /// <summary>A weapon row's wear ever cashed (<see cref="WeaponDto.WearCashed"/>): version 2's.</summary>
    internal const string WearCashedMember = "wearCashed";

    /// <summary>The campaign's stable (<see cref="CampaignDto.Stable"/>): version 2's, from Phase 6a.2.</summary>
    internal const string StableMember = "stable";

    /// <summary>The root member a save's time is written in (<see cref="SaveFile.SavedAt"/>): the document's last.</summary>
    internal const string SavedAtMember = "savedAt";

    /// <summary>Where <see cref="Write"/> lays the time out: the root's last member, on a line of its own.</summary>
    private const string TimeLead = ",\n  \"" + SavedAtMember + "\": ";

    /// <summary>The root's time member as <see cref="Write"/> writes it — null, or one string on its line — and the root's close after it, to the end of the text.</summary>
    private static readonly Regex TimeLine = new("""\A,\n  "savedAt": (?:null|"(?:[^"\\\r\n]|\\[^\r\n])*")\n}\n?\z""", RegexOptions.CultureInvariant);

    /// <summary>
    /// The migrations this build ships, one per version it can lift: version 1,
    /// the first, to 2 (<see cref="LiftToVersion2"/>), Phase 6a's.
    /// </summary>
    public static IReadOnlyList<SaveMigration> Migrations { get; } = [new SaveMigration(From: 1, LiftToVersion2)];

    /// <summary>
    /// Version 1 to 2 (Phase 6a.1; the v2 hand-off, settled.md): a weapon wears
    /// from version 2, and the tuning hash records the version whose keys it is
    /// of. The step writes what a version-1 document means by both, and by the
    /// stable it never had, and nothing else — it reads no content and no tuning:
    /// <list type="bullet">
    /// <item><strong>Every weapon row</strong> gets <c>"wear": 0</c> and
    /// <c>"wearCashed": 0</c>: no weapon wore before version 2, and a capacity
    /// replayed from 0 cashed is <c>StartingPool</c>'s. The rows are found where
    /// <see cref="WeaponDto"/> sits — a member's bag, the last run's spoils kept
    /// and lost, a visited floor's enemy rows, the drop a corpse lies on, and the
    /// weapons put down on a floor — each walked by name, never by shape.</item>
    /// <item><strong>The root</strong> gets <c>"tuningHashVersion": 1</c>: a
    /// version-1 document's hash was taken on version 1's keys
    /// (<see cref="TuningKey.HashedFrom"/>, PHASE 6a DECISION 9).</item>
    /// <item><strong>The campaign's stable</strong>, a member version 1 never had,
    /// is removed if a hand put one there: the version-1 reader ignored it, so to
    /// version 1 it meant no stable, and read at version 2 it would be weapons the
    /// campaign never owned — or a refusal of a save version 1 loaded. Where there
    /// is none the step writes none, and the reader takes the member's default,
    /// an empty stable (Phase 6a.2's review).</item>
    /// </list>
    /// <strong>It never throws on a hand-edited document.</strong> It writes by
    /// the indexer, so a stray <c>wear</c> a version-1 row already carried (the
    /// reader ignored it) is overwritten with version 1's meaning, 0, and a stray
    /// root <c>tuningHashVersion</c> with 1; a stray <c>stable</c> is removed,
    /// whatever kind of thing it is. And a member that is missing, null or
    /// the wrong kind of thing — a party that is a number, a bag that is a table,
    /// a weapon row that is a string — is passed over, so the reader then refuses
    /// the document where it is wrong, by its path
    /// (<see cref="SaveFormatException.Where"/>), exactly as it would at version 2:
    /// a step runs outside the reader's own refusals (<see cref="Read"/>), where
    /// an exception would say nothing of where.
    /// </summary>
    internal static JsonObject LiftToVersion2(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);

        static void Unworn(JsonNode? row)
        {
            if (row is not JsonObject weapon)
                return;
            weapon[WearMember] = 0;
            weapon[WearCashedMember] = 0;
        }

        static IEnumerable<JsonNode?> Items(JsonNode? list) => list is JsonArray rows ? rows : Enumerable.Empty<JsonNode?>();

        document[TuningHashVersionMember] = 1;
        if (document["campaign"] is not JsonObject campaign)
            return document;
        campaign.Remove(StableMember);   // version 1 had none: one a hand put there meant nothing to it

        foreach (var member in Items(campaign["party"]))
            if (member is JsonObject row)
                foreach (var carried in Items(row["inventory"]))
                    Unworn(carried);

        if (campaign["lastResult"] is JsonObject last)
        {
            foreach (var spoil in Items(last["spoilsKept"]))
                Unworn(spoil);
            foreach (var spoil in Items(last["spoilsLost"]))
                Unworn(spoil);
        }

        if (campaign["visit"] is JsonObject visit)
        {
            foreach (var floor in Items(visit["floors"]))
            {
                if (floor is not JsonObject visited)
                    continue;
                foreach (var enemy in Items(visited["enemies"]))
                {
                    if (enemy is not JsonObject row)
                        continue;
                    Unworn(row["weapon"]);
                    if (row["groundItem"] is JsonObject lying)
                        Unworn(lying["weapon"]);
                }
                foreach (var item in Items(visited["looseItems"]))
                    if (item is JsonObject loose)
                        Unworn(loose["weapon"]);
            }
        }
        return document;
    }

    /// <summary>Comments and trailing commas skipped, as a hand-edited file may carry them; a member named twice refused.</summary>
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowDuplicateProperties = false,
    };

    /// <summary>
    /// The document as text: indented, <c>"\n"</c> line ends and one at the end,
    /// each record's members in declaration order, and every float as the
    /// shortest text that reads back to the same bits. Writes the version the
    /// file says, whatever it is; <see cref="SaveGame.Capture"/> says
    /// <see cref="CurrentVersion"/>.
    /// </summary>
    public static string Write(SaveFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return JsonSerializer.Serialize(file, SaveJsonContext.Default.SaveFile) + "\n";
    }

    /// <summary>
    /// <paramref name="text"/> with its time left out: the root's
    /// <see cref="SavedAtMember"/> member taken out as <see cref="Write"/> lays it
    /// out — the root's last member, on a line of its own — so two saves of one
    /// campaign taken at different times are one text, and so is a save written
    /// before the member existed and one written after it. What says whether a save
    /// would write anything: a slot already holding the campaign keeps its save, its
    /// backup and the time its state was first written — but for a save by hand,
    /// which rewrites the time alone and keeps the backup (the user's ruling of
    /// 2026-09-25 on the save slots, R5; <c>CampaignBoot.Save</c>). A text laid out
    /// any other way — a hand edit, a text that is no save — comes back as it is,
    /// so it compares as the bytes it holds: never the same as a save it is not.
    /// Pure text: nothing is parsed.
    /// </summary>
    internal static string Untimed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int at = text.LastIndexOf(TimeLead, StringComparison.Ordinal);
        if (at < 0 || !TimeLine.IsMatch(text.AsSpan(at)))
            return text;
        int end = text.IndexOf('\n', at + TimeLead.Length);   // the time's own line ends here, before the root's close
        return string.Concat(text.AsSpan(0, at), text.AsSpan(end));
    }

    /// <summary>
    /// Read a save's text: parse it, check its version, lift an older one
    /// through <paramref name="migrations"/> in order, and read the result.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="migrations">The steps to lift an older document with; <see cref="Migrations"/>, this build's own, when omitted.</param>
    /// <exception cref="SaveFormatException">The text is not a save this build can read, and where it says so.</exception>
    /// <exception cref="ArgumentException"><paramref name="migrations"/> lifts one version two ways, or holds a null step.</exception>
    public static SaveFile Read(string text, IReadOnlyList<SaveMigration>? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var steps = migrations ?? Migrations;
        if (steps.Any(step => step is null))
            throw new ArgumentException("The migration table holds a null step.", nameof(migrations));

        var document = Parse(text);
        int version = VersionOf(document);
        if (version > CurrentVersion)
            throw new SaveFormatException(VersionMember,
                $"{version} is newer than this build reads ({CurrentVersion}): a save a newer build wrote is never read by an older one, nor written over");

        while (version < CurrentVersion)
        {
            var found = steps.Where(step => step.From == version).ToArray();
            if (found.Length == 0)
                throw new SaveFormatException(VersionMember,
                    $"{version} is older than this build reads ({CurrentVersion}), and no migration lifts it to {version + 1}");
            if (found.Length > 1)
                throw new ArgumentException($"The migration table lifts version {version} {found.Length} ways: one step per version.", nameof(migrations));

            document = found[0].Apply(document)
                ?? throw new InvalidOperationException($"The migration from version {version} returned no document.");
            version++;
            document[VersionMember] = version;
        }

        try
        {
            return document.Deserialize(SaveJsonContext.Default.SaveFile)
                ?? throw new SaveFormatException("$", "the document reads as nothing");
        }
        catch (JsonException e)
        {
            throw Refusal(e);
        }
    }

    /// <summary>The text as one JSON object, or where it is not one.</summary>
    private static JsonObject Parse(string text)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text, documentOptions: DocumentOptions);
        }
        catch (JsonException e)
        {
            throw Refusal(e);
        }

        return node as JsonObject
            ?? throw new SaveFormatException("$", node is null
                ? "the document is null: a save is one JSON object"
                : $"the document is a JSON {node.GetValueKind().ToString().ToLowerInvariant()}: a save is one JSON object");
    }

    /// <summary>The version the document says it is.</summary>
    private static int VersionOf(JsonObject document)
    {
        if (!document.TryGetPropertyValue(VersionMember, out var node) || node is null)
            throw new SaveFormatException(VersionMember, "missing: a save says which version of the format it is written in");
        if (node is not JsonValue value || !value.TryGetValue(out int version))
            throw new SaveFormatException(VersionMember, $"{node.ToJsonString()} is not a whole number");
        return version;
    }

    /// <summary>
    /// A parser's or a reader's failure as a refusal: where — the JSON path the
    /// reader was at, or the line of text a syntax error is on — and the
    /// problem, without the position the reader appends to its message, on one
    /// short line (<see cref="JsonShape.OneLine"/>): for a word it does not know
    /// the reader quotes the rest of the file, and the notice a refused save gets
    /// is shown whole.
    /// </summary>
    private static SaveFormatException Refusal(JsonException e)
    {
        string where = e.Path is { Length: > 0 } path
            ? path == "$" ? path : path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : path[1..]
            : e.LineNumber is { } line ? $"line {line + 1}" : "$";

        string problem = e.Message;
        foreach (string suffix in new[] { " Path: ", " LineNumber: " })
        {
            int cut = problem.IndexOf(suffix, StringComparison.Ordinal);
            if (cut > 0)
                problem = problem[..cut];
        }
        return new SaveFormatException(where, JsonShape.OneLine(problem), e);
    }
}
