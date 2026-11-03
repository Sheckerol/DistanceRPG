using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The content the game ships, as the suite reads it: its one door (the content
/// rework, 1g). Every test that needs the shipped tuning, relations,
/// enchantments, weapons, dungeons or starting party — or the movement budget —
/// asks here and nowhere else. The tuning, the relations, the weapons and the
/// dungeons are the data files the game carries built in, which are the master
/// copy (<see cref="ShippedData"/>): each read once, from the build's own texts,
/// so every test reads the one instance. The enchantment catalogue and the
/// starting party are code (<see cref="EnchantmentDefinitions"/>,
/// <see cref="StartingParty"/>).
/// <para>
/// Named <c>ShippedContent</c> rather than <c>Shipped</c>: eight suites already
/// have a member called <c>Shipped</c>, and inside them the name would bind to
/// that member instead of this class.
/// </para>
/// </summary>
internal static class ShippedContent
{
    private static readonly Lazy<RestrictedData> RestrictedHolder = new(() => RestrictedJson.Read(ShippedData.Texts.Restricted!));

    private static readonly Lazy<WeaponsData> WeaponsHolder = new(() => WeaponsJson.Read(ShippedData.Texts.Weapons!));

    private static readonly Lazy<DungeonsData> DungeonsHolder = new(() => DungeonsJson.Read(ShippedData.Texts.Dungeons!));

    /// <summary>The tuning the game ships: the built-in <c>tuning.json</c> as the game plays it, fitted — the one instance <see cref="GameContent.Current"/> opens on.</summary>
    public static Tuning Tuning => ShippedData.Content.Tuning;

    /// <summary>The relations the game ships: the built-in <c>restricted.json</c>, read once.</summary>
    public static RestrictedData Restricted => RestrictedHolder.Value;

    /// <summary>The enchantment catalogue and its unique souls: code, reference data (<see cref="EnchantmentDefinitions"/>).</summary>
    public static EnchantmentsData Enchantments => EnchantmentDefinitions.Data;

    /// <summary>The weapons the game ships: the built-in <c>weapons.json</c>, read once.</summary>
    public static WeaponsData Weapons => WeaponsHolder.Value;

    /// <summary>The dungeons the game ships: the built-in <c>dungeons.json</c>, read once.</summary>
    public static DungeonsData Dungeons => DungeonsHolder.Value;

    /// <summary>The starting party: code, reference data (<see cref="StartingParty"/>).</summary>
    public static PartyData Party => StartingParty.Data;

    /// <summary>The movement budget a member starts a turn with, in logic units: the shipped tuning's <c>MovementBudget</c>.</summary>
    public static float Budget => Tuning.MovementBudget;

    /// <summary>The four data files' texts as the build carries them: the master copies.</summary>
    public static ContentTexts Texts => ShippedData.Texts;

    /// <summary>The built-in copies' content, loaded on their own: what <see cref="GameContent.Current"/> opens on.</summary>
    public static GameContent Content => ShippedData.Content;
}
