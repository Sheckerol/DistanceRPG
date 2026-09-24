namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One content file as the load holds it (1h): the record it read to — or the
/// compiled record, when no file was found — or the failure that stopped it
/// reading, which waits to be reported at the file's own stage of the load
/// rather than thrown when it was met.
/// </summary>
/// <param name="File">The file's name, as a failure at its stage is stamped with it: <c>restricted.json</c>.</param>
/// <param name="Data">The record; null exactly when <paramref name="ParseFailure"/> is set.</param>
/// <param name="ParseFailure">Why the file's text is not its documented shape, or null.</param>
/// <param name="Compiled">The record is the compiled one, built in code, and no file was read for it.</param>
internal sealed record ContentSource<T>(string File, T? Data, ContentException? ParseFailure, bool Compiled) where T : class
{
    /// <summary>A record built in code, standing where the file would be.</summary>
    public static ContentSource<T> Built(string file, T data) => new(file, data, null, Compiled: true);
}

/// <summary>
/// The loaded content singletons, built in the §5.4 load order — tuning, then
/// the relations everything after them is checked against, then the
/// enchantments the weapons name, then the weapons, then the dungeons whose
/// themes name modifiers and whose drop tables are computed over the weapons,
/// then the roster that names weapons — and validated on the way in: invalid
/// content throws <see cref="ContentException"/>, stamped with the file whose
/// stage broke, and the game does not start (it says why on screen).
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
    /// weapon catalogue it names and the carry its bags must fit. The records
    /// are wrapped as built in code and handed to the staged load, which checks
    /// them in exactly this order.
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
    /// <exception cref="ContentException">Something is not well formed; the entry and the rule are named, and the file whose stage broke (every record here is one built in code, so each is marked <see cref="ContentException.Compiled"/>).</exception>
    public static GameContent Load(Tuning tuning, RestrictedData restricted, EnchantmentsData enchantments, WeaponsData weapons,
        PartyData? party = null, DungeonsData? dungeons = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);
        ArgumentNullException.ThrowIfNull(enchantments);
        ArgumentNullException.ThrowIfNull(weapons);
        return Load(tuning,
            ContentSource<RestrictedData>.Built(RestrictedJson.FileName, restricted),
            ContentSource<EnchantmentsData>.Built(EnchantmentsJson.FileName, enchantments),
            ContentSource<WeaponsData>.Built(ContentFiles.WeaponsFileName, weapons),
            ContentSource<DungeonsData>.Built(ContentFiles.DungeonsFileName, dungeons ?? ContentDefaults.Dungeons),
            ContentSource<PartyData>.Built(ContentFiles.PartyFileName, party ?? ContentDefaults.Party));
    }

    /// <summary>
    /// The staged load (1h): each file's record — or the failure that stopped it
    /// parsing — validated and built in §5.4's order, the first stage to break
    /// being the failure reported, stamped with its file
    /// (<see cref="ContentException.In"/>). The tuning is checked first; then
    /// seven stages, each reporting its own file's parse failure before anything
    /// reads the record:
    /// <list type="number">
    /// <item><c>restricted.json</c>'s shape;</item>
    /// <item><c>enchantments.json</c>'s shape;</item>
    /// <item><c>restricted.json</c>'s semantics — its own checks and the
    /// modifier rules — which come after the enchantments have parsed, because
    /// the ids it checks include theirs. So a shape failure in
    /// <c>enchantments.json</c> is reported before a semantic one in
    /// <c>restricted.json</c>: the stated rule, not an accident;</item>
    /// <item><c>enchantments.json</c>'s semantics: the catalogue, against the relations;</item>
    /// <item><c>weapons.json</c>'s shape, then its semantics;</item>
    /// <item><c>dungeons.json</c>'s;</item>
    /// <item><c>party.json</c>'s.</item>
    /// </list>
    /// A syntax error in a later file therefore never pre-empts a semantic error
    /// in an earlier one, and a check that reads another file than its stage's
    /// names that file in its detail. With every record in hand (the public
    /// overload) stages 1 and 2 pass, and the checks run in exactly the order
    /// they always have.
    /// </summary>
    /// <exception cref="ContentException">The first stage to break, stamped with its file.</exception>
    internal static GameContent Load(Tuning tuning, ContentSource<RestrictedData> restricted, ContentSource<EnchantmentsData> enchantments,
        ContentSource<WeaponsData> weapons, ContentSource<DungeonsData> dungeons, ContentSource<PartyData> party)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);
        ArgumentNullException.ThrowIfNull(enchantments);
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(dungeons);
        ArgumentNullException.ThrowIfNull(party);

        // A file's tuning never fails here — it is clamped as it is read and
        // fitted to the content — so this is the refusal for a tuning built in
        // code, and no file's stage.
        ContentValidator.ValidateTuning(tuning);

        var restrictedData = Shape(restricted);         // (1)
        var enchantmentsData = Shape(enchantments);     // (2)

        var modifiers = Stage(restricted, () =>         // (3)
        {
            var knownIds = new HashSet<string>(ModifierRules.ModifierIds, StringComparer.Ordinal);
            knownIds.UnionWith(enchantmentsData.Enchantments.Select(e => e.Id));
            ContentValidator.ValidateRestricted(restrictedData, knownIds);
            return new ModifierRules(tuning, restrictedData);
        });

        var enchantmentCatalogue = Stage(enchantments, () =>   // (4): runs ValidateEnchantments, then ValidateOpposition for its chart
            new EnchantmentCatalogue(enchantmentsData, tuning, restrictedData));

        var weaponsData = Shape(weapons);               // (5)
        var weaponCatalogue = Stage(weapons, () =>
        {
            ContentValidator.ValidateWeapons(weaponsData, modifiers, restrictedData, enchantmentCatalogue);
            return new WeaponCatalogue(weaponsData, enchantmentCatalogue);
        });

        var dungeonsData = Shape(dungeons);             // (6): computes the tables from these locals, then runs ValidateDungeons against them
        var dungeonCatalogue = Stage(dungeons, () => new DungeonCatalogue(dungeonsData, modifiers, weaponCatalogue, tuning));

        var partyData = Shape(party);                   // (7): last, since a member names weapons
        var roster = Stage(party, () =>
        {
            ContentValidator.ValidateParty(partyData, weaponCatalogue, tuning);
            return new PartyRoster(partyData);
        });

        return new GameContent(tuning, modifiers, enchantmentCatalogue, weaponCatalogue, dungeonCatalogue, roster);
    }

    /// <summary>A file's shape stage: its parse failure, stamped with the file, or the record it read to.</summary>
    private static T Shape<T>(ContentSource<T> source) where T : class
    {
        if (source.ParseFailure is { } failure)
            throw failure.In(source.File, source.Compiled);
        return source.Data ?? throw new ArgumentException($"{source.File} holds neither a record nor why it has none.", nameof(source));
    }

    /// <summary>A file's semantic stage: <paramref name="run"/>, with any content failure it raises stamped with the file.</summary>
    private static TResult Stage<T, TResult>(ContentSource<T> source, Func<TResult> run) where T : class
    {
        try
        {
            return run();
        }
        catch (ContentException failure)
        {
            throw failure.In(source.File, source.Compiled);
        }
    }

    /// <summary>Make <paramref name="content"/> current. Tests swap content; production calls this once, at startup, with what the data files loaded, and never again.</summary>
    public static void Use(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Current = content;
    }
}
