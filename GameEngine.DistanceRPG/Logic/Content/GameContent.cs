namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded content singletons, built in the §5.4 load order — tuning, then
/// the relations everything after them is checked against, then the
/// enchantments the weapons name, then the weapons, then the dungeons whose
/// themes name modifiers and whose drop tables are computed over the weapons,
/// then the roster that names weapons — and validated on the way in: invalid
/// content throws <see cref="ContentException"/> and the game does not start.
/// The compiled defaults are what <see cref="Current"/> opens on; the game's
/// startup reads the data files once (<see cref="ContentFiles.Load"/>) and makes
/// what they load current before anything is built from it. Nothing here
/// instantiates a <see cref="Weapon"/>, so the static initialiser cannot cycle
/// through a weapon reading its own modifiers — the roster's weapon ids are
/// checked against catalogue membership, never by building one, and the
/// dungeons' tables read weapon rows, never items.
/// </summary>
public sealed class GameContent
{
    /// <summary>
    /// The content the game is running under: the compiled defaults until the
    /// game's startup makes the data files' content current, once, through
    /// <see cref="Use"/> — read once, never hot-reloaded (§5.3) — and otherwise
    /// swapped only by tests.
    /// </summary>
    public static GameContent Current { get; private set; } =
        Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments, ContentDefaults.Weapons,
            ContentDefaults.Party, ContentDefaults.Dungeons);

    /// <summary>The §5.3 scalars this content was loaded under.</summary>
    public Tuning Tuning { get; }

    /// <summary>The §1.1 table and relations.</summary>
    public ModifierRules Modifiers { get; }

    /// <summary>The §5.7 catalogue.</summary>
    public EnchantmentCatalogue Enchantments { get; }

    /// <summary>The §5.6 catalogue.</summary>
    public WeaponCatalogue Weapons { get; }

    /// <summary>The §5.8 dungeons, and the drop tables their themes compute.</summary>
    public DungeonCatalogue Dungeons { get; }

    /// <summary>The §5.9 starting roster.</summary>
    public PartyRoster Party { get; }

    private GameContent(Tuning tuning, ModifierRules modifiers, EnchantmentCatalogue enchantments, WeaponCatalogue weapons,
        DungeonCatalogue dungeons, PartyRoster party)
    {
        Tuning = tuning;
        Modifiers = modifiers;
        Enchantments = enchantments;
        Weapons = weapons;
        Dungeons = dungeons;
        Party = party;
    }

    /// <summary>
    /// Validate and build in load order, without making the result current: the
    /// scalars first, then the relations against every modifier and enchantment
    /// id, the enchantments on their own and against the relations (opposition),
    /// the weapons against all three, the dungeons against the relations and the
    /// weapons their tables are computed from, and the roster last, against the
    /// weapon catalogue it names and the carry its bags must fit.
    /// <para>
    /// Everything after the tuning is built from this call's own locals and
    /// never from <see cref="Current"/>: this runs inside <c>Current</c>'s own
    /// static initialiser, where <c>Current</c> is not yet assigned, and under a
    /// test's swap it is the content being replaced.
    /// </para>
    /// </summary>
    /// <param name="party">
    /// The §5.9 roster; the compiled default when a caller has no opinion, which
    /// is every caller that is not exercising the roster itself.
    /// </param>
    /// <param name="dungeons">The §5.8 dungeons; the compiled default when a caller has no opinion.</param>
    /// <exception cref="ContentException">Something is not well formed; the entry and the rule are named.</exception>
    public static GameContent Load(Tuning tuning, RestrictedData restricted, EnchantmentsData enchantments, WeaponsData weapons,
        PartyData? party = null, DungeonsData? dungeons = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);
        ArgumentNullException.ThrowIfNull(enchantments);
        ArgumentNullException.ThrowIfNull(weapons);
        party ??= ContentDefaults.Party;
        dungeons ??= ContentDefaults.Dungeons;

        ContentValidator.ValidateTuning(tuning);

        var knownIds = new HashSet<string>(ModifierRules.ModifierIds, StringComparer.Ordinal);
        knownIds.UnionWith(enchantments.Enchantments.Select(e => e.Id));
        ContentValidator.ValidateRestricted(restricted, knownIds);
        var modifiers = new ModifierRules(tuning, restricted);

        var enchantmentCatalogue = new EnchantmentCatalogue(enchantments, tuning, restricted);   // runs ValidateEnchantments, then ValidateOpposition for its chart

        ContentValidator.ValidateWeapons(weapons, modifiers, restricted, enchantmentCatalogue);
        var weaponCatalogue = new WeaponCatalogue(weapons, enchantmentCatalogue);

        var dungeonCatalogue = new DungeonCatalogue(dungeons, modifiers, weaponCatalogue, tuning);   // computes the tables from these locals, then runs ValidateDungeons against them

        ContentValidator.ValidateParty(party, weaponCatalogue, tuning);   // last: a member names weapons, so the catalogue has to exist first
        var roster = new PartyRoster(party);

        return new GameContent(tuning, modifiers, enchantmentCatalogue, weaponCatalogue, dungeonCatalogue, roster);
    }

    /// <summary>Make <paramref name="content"/> current. Tests swap content; production calls this once, at startup, with what the data files loaded, and never again.</summary>
    public static void Use(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Current = content;
    }
}
