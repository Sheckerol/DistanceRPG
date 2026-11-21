using System.Runtime.CompilerServices;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The content the suite runs under: a frozen copy of the game's configuration,
/// and the suite's one door to it (the user's ask of 2026-11-20 — "have a mock
/// configuration so that if I change them for gameplay reasons we don't have
/// them break"). Every test that needs a tuning, relations, weapons, dungeons, a
/// starting party or the movement budget asks here, and what it gets never moves
/// when the game's own data does:
/// <list type="bullet">
/// <item>The four data files are the copies in <c>TestData/content</c> —
/// <c>tuning.json</c>, <c>restricted.json</c>, <c>weapons.json</c> and
/// <c>dungeons.json</c> as the game shipped them on 2026-11-20 — never the
/// masters in <c>GameEngine.DistanceRPG/Data</c>, which are free to change for
/// play. They are a fixture, not a record: edit one when a test needs a row or a
/// number it lacks.</item>
/// <item>The starting party is <see cref="Party"/>, written out here, never
/// <see cref="StartingParty"/>.</item>
/// <item>The enchantment catalogue is the game's own
/// (<see cref="EnchantmentDefinitions"/>): behaviour and reference data in code,
/// which a test of that code is about.</item>
/// </list>
/// It is also what <see cref="GameContent.Current"/> is while a test swaps
/// nothing: <see cref="MakeCurrent"/> makes it current as the suite's assembly
/// loads, ahead of the built-in content the game would open on.
/// <para>
/// The copies are read strictly, as a build's own are, so nothing here ever reads
/// a number of the master's: a <c>tuning.json</c> key the code gains must be added
/// to the frozen copy with it, and the suite stops, saying so, until it is. The
/// tests that are about the game's real files and roster read them
/// through <see cref="ShippedData"/> and <see cref="StartingParty"/>, and assert
/// that they load and can be played, never what they say
/// (<c>ShippedFilesTests</c>).
/// </para>
/// <para>
/// The numbers the older worked examples stand on are a second layer over this
/// one (<see cref="Premise"/>).
/// </para>
/// </summary>
internal static class FixtureContent
{
    /// <summary>The folder the frozen copies are in, beside the suite's own build.</summary>
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "TestData", "content");

    private static readonly Lazy<ContentTexts> TextsHolder = new(() => new ContentTexts(
        Tuning: File.ReadAllText(Path.Combine(Folder, TuningJson.FileName)),
        Restricted: File.ReadAllText(Path.Combine(Folder, RestrictedJson.FileName)),
        Weapons: File.ReadAllText(Path.Combine(Folder, WeaponsJson.FileName)),
        Dungeons: File.ReadAllText(Path.Combine(Folder, DungeonsJson.FileName))));

    private static readonly Lazy<RestrictedData> RestrictedHolder = new(() => RestrictedJson.Read(Texts.Restricted!));

    private static readonly Lazy<WeaponsData> WeaponsHolder = new(() => WeaponsJson.Read(Texts.Weapons!));

    private static readonly Lazy<DungeonsData> DungeonsHolder = new(() => DungeonsJson.Read(Texts.Dungeons!));

    private static readonly Lazy<PartyData> PartyHolder = new(() => new PartyData(
    [
        //   id   name                       STR DEX CON INT   in hand              bag
        new("A", "Shield", new InnateStats(3, 1, 4, 2), "tower_guard", ["darts"]),
        new("B", "Axe", new InnateStats(4, 2, 3, 1), "great_axe", ["skirmishers_pike"]),
        new("C", "Caster", new InnateStats(2, 3, 1, 4), "staff_of_renewal", ["wand_of_the_blast"], DamageType.Flaming),
        new("D", "Dagger", new InnateStats(1, 4, 2, 3), "weakspot_stiletto", ["hunting_bow"]),
    ]));

    /// <summary>
    /// The frozen files loaded as a build that carried them would load them: by the
    /// game's own loader, the copies its built-in ones and nothing beside the game,
    /// under the fixture's party. A copy that does not load stops the suite, naming
    /// why — as a <c>tuning.json</c> key the code has gained and the copy lacks does.
    /// </summary>
    private static readonly Lazy<GameContent> ContentHolder = new(() =>
    {
        var load = Load(Texts);
        return load.Content ?? throw new InvalidOperationException(
            $"The suite's frozen configuration (GameEngine.DistanceRPG.Tests/TestData/content) does not load: {load.Error?.Message ?? "no reason given"}. "
            + "A tuning.json key the code has gained must be added to TestData/content/tuning.json too.");
    });

    /// <summary>The fixture's tuning: the frozen <c>tuning.json</c> as a game would play it, fitted — the one instance <see cref="GameContent.Current"/> opens on in the suite.</summary>
    public static Tuning Tuning => Content.Tuning;

    /// <summary>The fixture's relations: the frozen <c>restricted.json</c>, read once.</summary>
    public static RestrictedData Restricted => RestrictedHolder.Value;

    /// <summary>The enchantment catalogue and its unique souls: the game's own code (<see cref="EnchantmentDefinitions"/>).</summary>
    public static EnchantmentsData Enchantments => EnchantmentDefinitions.Data;

    /// <summary>The fixture's weapons: the frozen <c>weapons.json</c>, read once.</summary>
    public static WeaponsData Weapons => WeaponsHolder.Value;

    /// <summary>The fixture's dungeons: the frozen <c>dungeons.json</c>, read once.</summary>
    public static DungeonsData Dungeons => DungeonsHolder.Value;

    /// <summary>
    /// The fixture's starting party: the roster as the game shipped it on
    /// 2026-11-20 — A a Scutum and Gladius and Darts, B a Dane Axe and a Hasta, C a
    /// Staff of Renewal and a Flaming Wand of the Blast, D a Rondel and a Shortbow —
    /// written out here, so the game's own (<see cref="StartingParty"/>) may move.
    /// </summary>
    public static PartyData Party => PartyHolder.Value;

    /// <summary>The movement budget a member starts a turn with, in logic units: the fixture tuning's <c>MovementBudget</c>.</summary>
    public static float Budget => Tuning.MovementBudget;

    /// <summary>The four frozen data files' texts, as a build would carry them.</summary>
    public static ContentTexts Texts => TextsHolder.Value;

    /// <summary>The fixture's content, loaded once: what <see cref="GameContent.Current"/> is in the suite while a test swaps nothing.</summary>
    public static GameContent Content => ContentHolder.Value;

    /// <summary>
    /// What a game built with the frozen files would play with <paramref name="files"/>
    /// beside it: the game's own loader (<see cref="ContentFiles.Load(ContentTexts, ContentTexts, PartyData)"/>),
    /// the frozen copies its built-in ones, under the fixture's party. The suite's
    /// door to the loader, in <see cref="ShippedData.Load"/>'s place, which loads
    /// over the game's real files.
    /// </summary>
    public static ContentLoad Load(ContentTexts files) => Load(files, Texts);

    /// <summary><see cref="Load(ContentTexts)"/> over <paramref name="builtIn"/> in the frozen copies' place: a build whose built-in copies a test broke.</summary>
    public static ContentLoad Load(ContentTexts files, ContentTexts builtIn) => ContentFiles.Load(files, builtIn, Party);

    /// <summary>
    /// Make the fixture current as the suite's assembly loads, so a test that swaps
    /// nothing reads the frozen configuration and never the game's built-in one —
    /// which <see cref="GameContent.Current"/> would otherwise open on
    /// (<see cref="ShippedData.Provide"/>). A fixture that does not load stops the
    /// suite here, naming why.
    /// </summary>
    [ModuleInitializer]
    internal static void MakeCurrent() => GameContent.Use(Content);
}
