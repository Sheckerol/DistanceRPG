using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The content the game ships, as the suite reads it: its one door (the content
/// rework, 1g). Every test that needs the shipped tuning, relations,
/// enchantments, weapons, dungeons or starting party — or the movement budget —
/// asks here and nowhere else, so the rework can move where each comes from
/// without touching a test again: first the compiled content, then the code
/// catalogues for the enchantments and the party, then the data files the game
/// carries built in, which are the master copy.
/// <para>
/// Named <c>ShippedContent</c> rather than <c>Shipped</c>: eight suites already
/// have a member called <c>Shipped</c>, and inside them the name would bind to
/// that member instead of this class.
/// </para>
/// </summary>
internal static class ShippedContent
{
    /// <summary>The tuning the game ships.</summary>
    public static Tuning Tuning => ContentDefaults.Tuning;

    /// <summary>The relations the game ships (<c>restricted.json</c>).</summary>
    public static RestrictedData Restricted => ContentDefaults.Restricted;

    /// <summary>The enchantment catalogue and its unique souls.</summary>
    public static EnchantmentsData Enchantments => ContentDefaults.Enchantments;

    /// <summary>The weapons the game ships (<c>weapons.json</c>).</summary>
    public static WeaponsData Weapons => ContentDefaults.Weapons;

    /// <summary>The dungeons the game ships (<c>dungeons.json</c>).</summary>
    public static DungeonsData Dungeons => ContentDefaults.Dungeons;

    /// <summary>The starting party.</summary>
    public static PartyData Party => ContentDefaults.Party;

    /// <summary>The movement budget a member starts a turn with, in logic units: the shipped tuning's <c>MovementBudget</c>.</summary>
    public static float Budget => GameConstants.MaxDistance;
}
