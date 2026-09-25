namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One content file as the load holds it (1h): the record its text read to —
/// the copy beside the executable's, or the built-in copy's when there was none
/// — or the failure that stopped it reading, which waits to be reported at the
/// file's own stage of the load rather than thrown when it was met.
/// </summary>
/// <param name="File">The file's name, as a failure at its stage is stamped with it: <c>restricted.json</c>.</param>
/// <param name="Data">The record; null exactly when <paramref name="ParseFailure"/> is set.</param>
/// <param name="ParseFailure">Why the file's text is not its documented shape, or null.</param>
/// <param name="BuiltIn">The text was the copy the game carries built in: no copy beside the executable was there to read.</param>
internal sealed record ContentSource<T>(string File, T? Data, ContentException? ParseFailure, bool BuiltIn) where T : class
{
    /// <summary>A record handed to the load as it stands — a test's, standing where the file's would be — which is no built-in copy.</summary>
    public static ContentSource<T> Given(string file, T data) => new(file, data, null, BuiltIn: false);
}

/// <summary>
/// The loaded content singletons, built in the load order — the game's own
/// enchantment catalogue, checked on its own; then the tuning; then the
/// relations everything after them is checked against, and the catalogue
/// against them; then the weapons; then the dungeons whose themes name
/// modifiers and whose drop tables are computed over the weapons; then the
/// starting party, which names weapons — and validated on the way in: invalid
/// content throws <see cref="ContentException"/>, stamped with the file whose
/// stage broke, and the game does not start (it says why on screen). The
/// enchantment catalogue (<see cref="EnchantmentDefinitions"/>) and the starting
/// party (<see cref="StartingParty"/>) are code — reference data, by the user's
/// ruling of 2026-09-25 — handed to the load as data and checked like any
/// content; the tuning, the relations, the weapons and the dungeons are read
/// from their files, whose shipped copies are the master copy the game carries
/// built in. <see cref="Current"/> opens on the built-in copies' content, loaded
/// on first use by a loader the game's edge hands in (<see cref="OpenOn"/>); the
/// game's startup reads the data files once (<see cref="ContentFiles.Load"/>)
/// and makes what they load current before anything is built from it. Nothing
/// here instantiates a <see cref="Weapon"/> or reads <see cref="Current"/> — the
/// roster's weapon ids are checked against catalogue membership, never by
/// building one, and the dungeons' tables read weapon rows, never items — so a
/// load can never re-enter the content it is building.
/// </summary>
public sealed class GameContent
{
    /// <summary>The content made current (<see cref="Use"/>): the data files' content, which the game's startup makes current once, or a test's.</summary>
    private static GameContent? _current;

    /// <summary>The built-in copies' content, loaded on first use by the loader the edge handed in (<see cref="OpenOn"/>); null until one is handed in.</summary>
    private static Lazy<GameContent>? _builtIn;

    /// <summary>
    /// The content the game is running under: the data files' content, which the
    /// game's startup makes current, once, through <see cref="Use"/> before
    /// anything reads this — read once, never hot-reloaded (§5.3) — and otherwise
    /// swapped only by tests. Until something is made current it is the content of
    /// the data files the game carries built in, loaded the first time it is asked
    /// for (<see cref="OpenOn"/>): what a test reads when it swaps nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing is current, and no loader of the built-in content was handed in.</exception>
    public static GameContent Current
        => _current ?? _builtIn?.Value
            ?? throw new InvalidOperationException("No content is current and none is built in: the game's edge hands the built-in content in when its assembly loads (ShippedData.Provide).");

    /// <summary>
    /// Hand in the loader of the content <see cref="Current"/> opens on — the
    /// built-in data files' content — once: the game's edge does it as its
    /// assembly loads (<c>ShippedData.Provide</c>), since reading what the build
    /// carries is the edge's and <c>Logic/</c> never does. Nothing is loaded
    /// here: <paramref name="builtIn"/> runs the first time <see cref="Current"/>
    /// is asked for with nothing made current — never in the game, whose startup
    /// makes the data files' content current first.
    /// </summary>
    /// <exception cref="InvalidOperationException">A loader was handed in already; the one there is left as it was.</exception>
    internal static void OpenOn(Func<GameContent> builtIn)
    {
        ArgumentNullException.ThrowIfNull(builtIn);
        var opened = new Lazy<GameContent>(builtIn, LazyThreadSafetyMode.ExecutionAndPublication);
        if (Interlocked.CompareExchange(ref _builtIn, opened, null) is not null)
            throw new InvalidOperationException("The content the game opens on is handed in once, as the game's assembly loads (ShippedData.Provide).");
    }

    /// <summary>The §5.3 scalars this content was loaded under.</summary>
    public Tuning Tuning { get; }

    /// <summary>The §1.1 table and relations.</summary>
    public ModifierRules Modifiers { get; }

    /// <summary>The §5.7 catalogue: the code's rows (<see cref="EnchantmentDefinitions"/>), with the tuning's dials laid over them.</summary>
    public EnchantmentCatalogue Enchantments { get; }

    /// <summary>The §5.6 catalogue.</summary>
    public WeaponCatalogue Weapons { get; }

    /// <summary>The §5.8 dungeons, and the drop tables their themes compute.</summary>
    public DungeonCatalogue Dungeons { get; }

    /// <summary>The §5.9 starting roster: the code's (<see cref="StartingParty"/>).</summary>
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
    /// Validate and build in load order, without making the result current, from
    /// records handed in as they stand — the suite's door to content built from
    /// parts (<c>TestContent.Load</c>): the enchantment catalogue on its own, the
    /// scalars, then the relations against every modifier and every enchantment
    /// id the catalogue has, and the catalogue against the relations (opposition,
    /// the unique souls), the weapons against all three, the dungeons against the
    /// relations and the weapons their tables are computed from, and the roster
    /// last, against the weapon catalogue it names and the carry its bags must
    /// fit. The records are handed to the staged load as given — no built-in copy
    /// — which checks them in exactly this order. Every part is required: no
    /// content of the game's stands in code to fill one in.
    /// <para>
    /// Everything after the tuning is built from this call's own locals and
    /// never from <see cref="Current"/>: under a test's swap it is the content
    /// being replaced, and before one it is the built-in content, whose own load
    /// this may be part of.
    /// </para>
    /// </summary>
    /// <param name="enchantments">
    /// The enchantment catalogue: the game's own is code
    /// (<see cref="EnchantmentDefinitions.Data"/>); a test hands another to
    /// simulate a code edit — an appended row, a reversed order, a broken one.
    /// </param>
    /// <param name="party">The §5.9 roster: the game's own is code (<see cref="StartingParty.Data"/>).</param>
    /// <param name="dungeons">The §5.8 dungeons.</param>
    /// <exception cref="ContentException">Something is not well formed; the entry and the rule are named, and the file whose stage broke. A catalogue broken on its own names no file.</exception>
    internal static GameContent Load(Tuning tuning, RestrictedData restricted, EnchantmentsData enchantments, WeaponsData weapons,
        PartyData party, DungeonsData dungeons)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(restricted);
        ArgumentNullException.ThrowIfNull(enchantments);
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(dungeons);
        return Load(tuning, enchantments,
            ContentSource<RestrictedData>.Given(RestrictedJson.FileName, restricted),
            ContentSource<WeaponsData>.Given(ContentFiles.WeaponsFileName, weapons),
            ContentSource<DungeonsData>.Given(ContentFiles.DungeonsFileName, dungeons),
            party);
    }

    /// <summary>
    /// The staged load (1h): the code's enchantment catalogue checked on its
    /// own, then each file's record — or the failure that stopped it parsing —
    /// validated and built in the load order, the first stage to break being
    /// the failure reported, stamped with its file
    /// (<see cref="ContentException.In"/>). The catalogue comes first and names
    /// no file: it is the game's own code, which no data file edits, so a
    /// failure there is a code edit's, and the suite catches it before a
    /// playtester can. Then the tuning — refused if it was built in code with a
    /// table short of a member — and the catalogue's dials: a number the catalogue
    /// leaves to <c>tuning.json</c> (<see cref="EnchantmentDefinitions.SetByTuning"/>)
    /// that the tuning does not name stops the game stamped <c>tuning.json</c>,
    /// built-in, since a copy beside the game merges over the built-in one and
    /// cannot take a dial away. Then five stages, each reporting its own file's
    /// parse failure before anything reads the record:
    /// <list type="number">
    /// <item><c>restricted.json</c>'s shape;</item>
    /// <item><c>restricted.json</c>'s semantics — its own checks, its ids against
    /// every modifier and every enchantment the catalogue has, and the modifier
    /// rules — and then the catalogue against the relations: the opposition
    /// chart its elements form, and its unique souls against
    /// <c>neverRolled</c>. The catalogue is code, so <c>restricted.json</c> is
    /// the file to fix, and the failure is stamped with it;</item>
    /// <item><c>weapons.json</c>'s shape, then its semantics;</item>
    /// <item><c>dungeons.json</c>'s;</item>
    /// <item>the starting party, which is code, against the weapons it names:
    /// stamped <c>weapons.json</c>, with that file's built-in flag, since the one
    /// party failure a data file can cause is a <c>weapons.json</c> that renames
    /// a weapon only the roster names.</item>
    /// </list>
    /// A syntax error in a later file therefore never pre-empts a semantic error
    /// in an earlier one, and a check that reads another file than its stage's
    /// names that file in its detail. With every record in hand (the records
    /// overload) stage 1 passes, and the checks run in exactly this order.
    /// </summary>
    /// <exception cref="ContentException">The first stage to break, stamped with its file; the catalogue's own, unstamped.</exception>
    internal static GameContent Load(Tuning tuning, EnchantmentsData enchantments, ContentSource<RestrictedData> restricted,
        ContentSource<WeaponsData> weapons, ContentSource<DungeonsData> dungeons, PartyData party)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(enchantments);
        ArgumentNullException.ThrowIfNull(restricted);
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(dungeons);
        ArgumentNullException.ThrowIfNull(party);

        // (0) The code catalogue on its own: the game's own rows, which no file
        // edits, so the failure names no file.
        ContentValidator.ValidateEnchantments(enchantments);

        // A file's tuning never fails here — it is clamped as it is read and
        // fitted to the content — so this is the refusal for a tuning built in
        // code, and no file's stage.
        ContentValidator.ValidateTuning(tuning);

        // The numbers the catalogue leaves to tuning.json are there. Only the
        // built-in copy can lack one — a copy beside it merges over it — so a
        // failure is that copy's.
        try
        {
            ContentValidator.ValidateDials(enchantments, tuning);
        }
        catch (ContentException failure)
        {
            throw failure.In(TuningJson.FileName, builtIn: true);
        }

        var restrictedData = Shape(restricted);         // (1)
        var (modifiers, enchantmentCatalogue) = Stage(restricted, () =>   // (2)
        {
            var knownIds = new HashSet<string>(ModifierRules.ModifierIds, StringComparer.Ordinal);
            knownIds.UnionWith(enchantments.Enchantments.Select(e => e.Id));
            ContentValidator.ValidateRestricted(restrictedData, knownIds);
            var rules = new ModifierRules(tuning, restrictedData);

            // Then the catalogue against the relations: it runs ValidateEnchantments
            // again, then ValidateOpposition for its chart and ValidateNeverRolled
            // for its souls, both answered by restricted.json.
            return (rules, new EnchantmentCatalogue(enchantments, tuning, restrictedData));
        });

        var weaponsData = Shape(weapons);               // (3)
        var weaponCatalogue = Stage(weapons, () =>
        {
            ContentValidator.ValidateWeapons(weaponsData, modifiers, restrictedData, enchantmentCatalogue);
            return new WeaponCatalogue(weaponsData, enchantmentCatalogue);
        });

        var dungeonsData = Shape(dungeons);             // (4): computes the tables from these locals, then runs ValidateDungeons against them
        var dungeonCatalogue = Stage(dungeons, () => new DungeonCatalogue(dungeonsData, modifiers, weaponCatalogue, tuning));

        var roster = Stage(weapons, () =>               // (5): last, since a member names weapons — stamped weapons.json
        {
            ContentValidator.ValidateParty(party, weaponCatalogue, tuning);
            return new PartyRoster(party);
        });

        return new GameContent(tuning, modifiers, enchantmentCatalogue, weaponCatalogue, dungeonCatalogue, roster);
    }

    /// <summary>A file's shape stage: its parse failure, stamped with the file, or the record it read to.</summary>
    private static T Shape<T>(ContentSource<T> source) where T : class
    {
        if (source.ParseFailure is { } failure)
            throw failure.In(source.File, source.BuiltIn);
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
            throw failure.In(source.File, source.BuiltIn);
        }
    }

    /// <summary>Make <paramref name="content"/> current. Tests swap content; production calls this once, at startup, with what the data files loaded, and never again.</summary>
    public static void Use(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _current = content;
    }
}
