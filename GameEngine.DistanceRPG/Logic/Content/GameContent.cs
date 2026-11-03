namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded content singletons, built in the §5.4 load order — tuning, then
/// the relations everything after them is checked against, then the
/// enchantments the weapons name, then the weapons, then the roster that names
/// weapons — and validated on the way in: invalid content throws
/// <see cref="ContentException"/> and the game does not start. Phase 1 loads the
/// compiled defaults; Phase 5 adds the files. Nothing here instantiates a
/// <see cref="Weapon"/>, so the static initialiser cannot cycle through a weapon
/// reading its own modifiers — the roster's weapon ids are checked against
/// catalogue membership, never by building one.
/// </summary>
public sealed class GameContent
{
    /// <summary>The content the game is running under. Swapped only by tests, through <see cref="Use"/>.</summary>
    public static GameContent Current { get; private set; } =
        Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments, ContentDefaults.Weapons, ContentDefaults.Party);

    /// <summary>The §5.3 scalars this content was loaded under.</summary>
    public Tuning Tuning { get; }

    /// <summary>The §1.1 table and relations.</summary>
    public ModifierRules Modifiers { get; }

    /// <summary>The §5.7 catalogue.</summary>
    public EnchantmentCatalogue Enchantments { get; }

    /// <summary>The §5.6 catalogue.</summary>
    public WeaponCatalogue Weapons { get; }

    /// <summary>The §5.9 starting roster.</summary>
    public PartyRoster Party { get; }

    private GameContent(Tuning tuning, ModifierRules modifiers, EnchantmentCatalogue enchantments, WeaponCatalogue weapons, PartyRoster party)
    {
        Tuning = tuning;
        Modifiers = modifiers;
        Enchantments = enchantments;
        Weapons = weapons;
        Party = party;
    }

    /// <summary>
    /// Validate and build in load order, without making the result current: the
    /// scalars first, then the relations against every modifier and enchantment
    /// id, the enchantments on their own and against the relations (opposition),
    /// the weapons against all three, and the roster last, against the weapon
    /// catalogue it names.
    /// </summary>
    /// <param name="party">
    /// The §5.9 roster; the compiled default when a caller has no opinion, which
    /// is every caller that is not exercising the roster itself.
    /// </param>
    /// <exception cref="ContentException">Something is not well formed; the entry and the rule are named.</exception>
    public static GameContent Load(Tuning tuning, RestrictedData restricted, EnchantmentsData enchantments, WeaponsData weapons,
        PartyData? party = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);
        ArgumentNullException.ThrowIfNull(enchantments);
        ArgumentNullException.ThrowIfNull(weapons);
        party ??= ContentDefaults.Party;

        ContentValidator.ValidateTuning(tuning);

        var knownIds = new HashSet<string>(ModifierRules.ModifierIds, StringComparer.Ordinal);
        knownIds.UnionWith(enchantments.Enchantments.Select(e => e.Id));
        ContentValidator.ValidateRestricted(restricted, knownIds);
        var modifiers = new ModifierRules(tuning, restricted);

        var enchantmentCatalogue = new EnchantmentCatalogue(enchantments, tuning, restricted);   // runs ValidateEnchantments, then ValidateOpposition for its chart

        ContentValidator.ValidateWeapons(weapons, modifiers, restricted, enchantmentCatalogue);
        var weaponCatalogue = new WeaponCatalogue(weapons, enchantmentCatalogue);

        ContentValidator.ValidateParty(party, weaponCatalogue);   // last: a member names weapons, so the catalogue has to exist first
        var roster = new PartyRoster(party);

        return new GameContent(tuning, modifiers, enchantmentCatalogue, weaponCatalogue, roster);
    }

    /// <summary>Make <paramref name="content"/> current. Tests swap content; production loads once and never calls this twice.</summary>
    public static void Use(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Current = content;
    }
}
