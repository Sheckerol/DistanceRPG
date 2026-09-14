namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded content singletons, built in the §5.4 load order (tuning, then
/// the relations everything after them is checked against) and validated on
/// the way in: invalid content throws <see cref="ContentException"/> and the
/// game does not start. Phase 1 loads the compiled defaults; Phase 5 adds the
/// files. Nothing here depends on <see cref="Weapon"/>, so the static
/// initialiser cannot cycle through a weapon reading its own modifiers.
/// </summary>
public sealed class GameContent
{
    /// <summary>The content the game is running under. Swapped only by tests, through <see cref="Use"/>.</summary>
    public static GameContent Current { get; private set; } =
        Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.EnchantmentIds);

    /// <summary>The §5.3 scalars this content was loaded under.</summary>
    public Tuning Tuning { get; }

    /// <summary>The §1.1 table and relations.</summary>
    public ModifierRules Modifiers { get; }

    private GameContent(Tuning tuning, ModifierRules modifiers)
    {
        Tuning = tuning;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Validate and build, without making the result current.
    /// <paramref name="enchantmentIds"/> are the enchantment ids the relations may
    /// name alongside the modifiers; the enchantment catalogue supplies them once
    /// it exists.
    /// </summary>
    /// <exception cref="ContentException">The relations are not well formed.</exception>
    public static GameContent Load(Tuning tuning, RestrictedData restricted, IEnumerable<string>? enchantmentIds = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);

        var knownIds = new HashSet<string>(ModifierRules.ModifierIds, StringComparer.Ordinal);
        if (enchantmentIds != null)
            knownIds.UnionWith(enchantmentIds);
        ContentValidator.ValidateRestricted(restricted, knownIds);

        return new GameContent(tuning, new ModifierRules(tuning, restricted));
    }

    /// <summary>Make <paramref name="content"/> current. Tests swap content; production loads once and never calls this twice.</summary>
    public static void Use(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Current = content;
    }
}
