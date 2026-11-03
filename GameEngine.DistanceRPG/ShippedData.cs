using System.Runtime.CompilerServices;
using System.Text;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG;

/// <summary>
/// The data files as the build carries them (the content rework, 1f, 1g): the
/// four shipped files in <c>GameEngine.DistanceRPG/Data</c> — the master copy of
/// every number, relation, weapon and dungeon — are embedded in the game when it
/// is built, and this is the one place that reads them back out. It is the
/// edge, like <see cref="DataFiles"/>: <c>Logic/</c> never reads what the build
/// carries, and is handed the texts instead.
/// <para>
/// <strong>What the game plays</strong> is <see cref="Load"/>: the files beside
/// the executable over these built-in copies, each copy the fallback for its
/// file (<see cref="ContentFiles.Load"/>). A built-in copy that is broken, or
/// one the build lacks, is a failure the load reports like any other, so the
/// game says so on its screen rather than crashing.
/// </para>
/// <para>
/// <strong>No eager static state.</strong> The type initializer only constructs
/// the two lazy holders; nothing is read until something asks. So a broken
/// built-in copy can never surface as a failure to initialise this type, and
/// nothing here runs before the game's log and crash handler are up. The texts
/// are read once, on first use (<see cref="Texts"/>), and never throw: a
/// resource the build lacks or cannot read is a null text, with why in
/// <see cref="ContentTexts.Unreadable"/>. The built-in content — the copies
/// loaded alone — is loaded only when <see cref="GameContent.Current"/> is asked
/// for with nothing made current, which the game never does (its startup makes
/// the loaded content current first), and by the suite's door
/// (<see cref="Content"/>).
/// </para>
/// </summary>
internal static class ShippedData
{
    /// <summary>What each built-in copy's resource name starts with: <c>DistanceRPG.Data.tuning.json</c> (the game project's <c>EmbeddedResource</c> item names them so).</summary>
    internal const string ResourcePrefix = "DistanceRPG.Data.";

    /// <summary>The four built-in texts, read on first use and once.</summary>
    private static readonly Lazy<ContentTexts> BuiltInTexts = new(ReadBuiltInTexts);

    /// <summary>The built-in copies' content, loaded on first use and once: what <see cref="GameContent.Current"/> opens on.</summary>
    private static readonly Lazy<GameContent> BuiltInContent = new(LoadBuiltIn);

    /// <summary>
    /// The four built-in texts — <c>tuning.json</c>, <c>restricted.json</c>,
    /// <c>weapons.json</c>, <c>dungeons.json</c> — exactly as the build carries
    /// them. Never throws: a copy the build lacks, or that could not be read out
    /// of it, is a null text with the reason in <see cref="ContentTexts.Unreadable"/>.
    /// </summary>
    public static ContentTexts Texts => BuiltInTexts.Value;

    /// <summary>
    /// What the game plays: <paramref name="files"/>, the texts found beside the
    /// executable, loaded over the built-in texts (<see cref="ContentFiles.Load"/>).
    /// Reads <see cref="Texts"/>, never <see cref="Content"/>.
    /// </summary>
    public static ContentLoad Load(ContentTexts files) => ContentFiles.Load(files, Texts);

    /// <summary>
    /// The built-in copies' content, loaded on their own — the content
    /// <see cref="GameContent.Current"/> opens on, one instance. The suite's door
    /// to the shipped content reads it here; the game never does.
    /// </summary>
    internal static GameContent Content => BuiltInContent.Value;

    /// <summary>
    /// Hand <see cref="GameContent"/> the loader of the content it opens on, when
    /// the game's assembly is first touched — by the game or by a test host.
    /// Touches neither holder and reads nothing: the loader runs only if
    /// <see cref="GameContent.Current"/> is asked for with nothing made current.
    /// </summary>
    [ModuleInitializer]
    internal static void Provide() => GameContent.OpenOn(ContentOfTheBuild);

    /// <summary>The built-in content, from its holder: the loader <see cref="Provide"/> hands in.</summary>
    private static GameContent ContentOfTheBuild() => BuiltInContent.Value;

    /// <summary>Each built-in copy, read out of the build in its own try: the text, or null and why.</summary>
    private static ContentTexts ReadBuiltInTexts()
    {
        var assembly = typeof(ShippedData).Assembly;
        var unreadable = new Dictionary<string, string>(StringComparer.Ordinal);

        string? Read(string name)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name);
                if (stream is null)
                {
                    unreadable[name] = "the game was built without it";
                    return null;
                }
                using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }
            catch (Exception error)
            {
                unreadable[name] = $"it could not be read out of the game ({error.GetType().Name}: {error.Message.Replace("'", "", StringComparison.Ordinal)})";
                return null;
            }
        }

        string? tuning = Read(TuningJson.FileName);
        string? restricted = Read(RestrictedJson.FileName);
        string? weapons = Read(WeaponsJson.FileName);
        string? dungeons = Read(DungeonsJson.FileName);
        return new ContentTexts(Tuning: tuning, Restricted: restricted, Weapons: weapons, Dungeons: dungeons,
            Unreadable: unreadable.Count == 0 ? null : unreadable);
    }

    /// <summary>
    /// The built-in texts loaded on their own, as though they sat beside the
    /// game: the content <see cref="GameContent.Current"/> opens on.
    /// </summary>
    /// <exception cref="InvalidOperationException">The built-in copies do not load: the failure is named. The game never gets here; its own load says so on screen.</exception>
    private static GameContent LoadBuiltIn()
    {
        var load = ContentFiles.Load(Texts, Texts);
        return load.Content
            ?? throw new InvalidOperationException($"The data files the game carries built in do not load: {load.Error?.Message ?? "no reason given"}");
    }
}
