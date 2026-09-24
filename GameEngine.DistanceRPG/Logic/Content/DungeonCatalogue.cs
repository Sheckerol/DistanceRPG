namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The loaded dungeons (§5.8), keyed by stable string id and kept in file
/// order, and the two tables a theme <em>computes</em> rather than authors
/// (§4.3, ROADMAP: "a dungeon's drop table is computed rather than authored"):
/// <list type="bullet">
/// <item><see cref="ThemeClasses"/> — the boss's drop table: every class all
/// of whose variants can take the theme as a forged graft, which is
/// <see cref="ModifierRules.CanGraft"/> run over the eight classes. "Where a
/// class cannot legally carry the theme, the boss simply does not drop that
/// class."</item>
/// <item><see cref="WieldClasses"/> — what the boss can hold: that table
/// narrowed to the classes an enemy can field (<see cref="EnemyPlacer.PlacedClasses"/>),
/// since a staff in an enemy's hands flees and a wand has no enemy AI.</item>
/// </list>
/// <para>
/// <strong>Built from the instances it is handed, never from
/// <see cref="GameContent.Current"/>.</strong> <see cref="GameContent.Load"/>
/// runs inside <c>Current</c>'s own static initialiser, where <c>Current</c>
/// is not yet assigned, and under a test's content swap it is the content
/// being replaced: a table computed off it would fail at startup or, worse,
/// describe the previous relations. So the rules and the weapon catalogue
/// come in by constructor, and a <c>restricted.json</c> edit moves a table
/// with no dungeon edit anywhere — which is the point of computing it.
/// </para>
/// <para>
/// A class's variants are read as <see cref="WeaponDef"/> spreads — a martial
/// class's one per role through <see cref="WeaponCatalogue.Variant"/>, a
/// caster's through <see cref="LootTable.CasterVariants"/> — and never
/// instantiated, so loading content still builds no <see cref="Weapon"/>.
/// Every class is asked about every variant, because the variant is rolled
/// after the class and a boss must never drop an ungrafted weapon.
/// </para>
/// Like <see cref="EnchantmentCatalogue"/>, the constructor validates what it
/// builds (<see cref="ContentValidator.ValidateDungeons"/>) against the tables
/// it has just computed, so no dungeon is ever served that its own rules
/// refuse.
/// </summary>
public sealed class DungeonCatalogue
{
    private static readonly VariantRole[] Roles = Enum.GetValues<VariantRole>();

    private readonly Dictionary<string, DungeonDef> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<ModifierType, WeaponClass[]> _themeClasses = new();
    private readonly Dictionary<ModifierType, WeaponClass[]> _wieldClasses = new();
    private readonly Tuning _tuning;

    /// <param name="data">The dungeon rows.</param>
    /// <param name="modifiers">The relations the drop tables are computed from: the in-flight rules, never the current content's.</param>
    /// <param name="weapons">The weapon catalogue whose variants the tables are computed over, and whose uniques the themes hunt.</param>
    /// <param name="tuning">
    /// The scalars an unpinned boss floor range reads (<see cref="Tuning.BossFloorMin"/>,
    /// <see cref="Tuning.BossFloorMax"/>): <see cref="GameContent.Load"/> passes the
    /// tuning it is loading, the one <paramref name="modifiers"/> was built on.
    /// Null reads the compiled fallback, §5.3's rule for a missing key — never
    /// the current content.
    /// </param>
    /// <exception cref="ContentException">A dungeon breaks one of the §5.8 rules (<see cref="ContentValidator.ValidateDungeons"/>).</exception>
    public DungeonCatalogue(DungeonsData data, ModifierRules modifiers, WeaponCatalogue weapons, Tuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(weapons);
        _tuning = tuning ?? ContentDefaults.Tuning;

        // Every modifier's table, not only the themes in the file: a table is
        // a fact about the relations and the weapons, asked of any modifier.
        foreach (var theme in Enum.GetValues<ModifierType>())
        {
            var table = Graftable(theme, modifiers, weapons);
            _themeClasses[theme] = table;
            _wieldClasses[theme] = table.Where(EnemyPlacer.PlacedClasses.Contains).ToArray();
        }

        All = data.Dungeons.ToArray();
        ContentValidator.ValidateDungeons(data, this, weapons);   // never serve a row its own tables refuse

        foreach (var def in All)
            _byId[def.Id] = def;   // unique by then
        Tutorial = All.Single(d => d.RetiresOnClear);   // exactly one by then
    }

    /// <summary>Every dungeon, in file order.</summary>
    public IReadOnlyList<DungeonDef> All { get; }

    /// <summary>
    /// The one dungeon that retires on its clear (§4.3) — the tutorial, the
    /// dungeon the hub opens behind. Exactly one exists by validation
    /// (<see cref="ContentValidator.RuleOneRetiringDungeon"/>).
    /// </summary>
    public DungeonDef Tutorial { get; }

    public DungeonDef this[string id]
        => TryGet(id, out var def) ? def : throw new KeyNotFoundException($"No dungeon '{id}' in the catalogue.");

    public bool TryGet(string id, out DungeonDef def) => _byId.TryGetValue(id, out def!);

    /// <summary>
    /// A boss themed on <paramref name="theme"/>'s drop table (§4.3): the classes
    /// every one of whose variants <see cref="ModifierRules.CanGraft"/> allows
    /// the theme onto as a forged graft, in <see cref="WeaponClass"/> order.
    /// <c>Brace</c> reaches dagger, sword and spear; <c>Block</c> all eight;
    /// <c>Charges</c>, forged-only, the throwing class alone. Breadth varies by
    /// theme and nothing normalises it. Empty for a modifier no class can take.
    /// </summary>
    public IReadOnlyList<WeaponClass> ThemeClasses(ModifierType theme)
        => _themeClasses.TryGetValue(theme, out var table) ? table : [];

    /// <summary>
    /// What <paramref name="def"/>'s boss can hold: its theme's drop table
    /// narrowed to the classes an enemy fields
    /// (<see cref="EnemyPlacer.PlacedClasses"/>), in <see cref="WeaponClass"/>
    /// order. The boss wields a weapon "like a dummy does" (§5.8), so the
    /// placer's bound on what an enemy can field bounds its hands — and only its
    /// hands: its drop is the whole of <see cref="ThemeClasses"/>. The day the
    /// placer fields more classes this widens with no edit here.
    /// </summary>
    public IReadOnlyList<WeaponClass> WieldClasses(DungeonDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        return _wieldClasses.TryGetValue(def.Theme, out var table) ? table : [];
    }

    /// <summary>
    /// The floors <paramref name="def"/>'s boss is rolled between, 1-based floor
    /// numbers: its own pinned range where content pins one (the tutorial's
    /// 2–2), else the tuning's (<see cref="Tuning.BossFloorMin"/>–<see cref="Tuning.BossFloorMax"/>,
    /// 5–10) under the tuning this catalogue was loaded with.
    /// </summary>
    public (int Min, int Max) BossFloorRange(DungeonDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        return (def.BossFloorMin ?? _tuning.BossFloorMin, def.BossFloorMax ?? _tuning.BossFloorMax);
    }

    /// <summary>The classes whose every drop variant takes <paramref name="theme"/> as a forged graft, in <see cref="WeaponClass"/> order.</summary>
    private static WeaponClass[] Graftable(ModifierType theme, ModifierRules modifiers, WeaponCatalogue weapons)
    {
        var table = new List<WeaponClass>();
        foreach (var cls in Enum.GetValues<WeaponClass>())
        {
            var variants = DropVariants(cls, weapons);
            if (variants.Count > 0 && variants.All(v => TakesAsForged(theme, v, modifiers)))
                table.Add(cls);
        }
        return table.ToArray();
    }

    /// <summary>
    /// The variants a drop of <paramref name="cls"/> rolls between, as the loot
    /// table decodes its draw: a martial class's one per role, a caster's the
    /// code-fixed <see cref="LootTable.CasterVariants"/> list. None for a class
    /// the weapon file leaves out, which no boss can drop.
    /// </summary>
    private static IReadOnlyList<WeaponDef> DropVariants(WeaponClass cls, WeaponCatalogue weapons)
    {
        if (weapons.ByClass(cls).Count == 0)
            return [];
        if (Weapon.KindOf(cls) != WeaponKind.Caster)
            return Roles.Select(role => weapons.Variant(cls, role)).ToArray();
        return LootTable.CasterVariants.TryGetValue(cls, out var ids)
            ? ids.Select(id => weapons[id]).ToArray()
            : [];
    }

    /// <summary>
    /// Whether <paramref name="variant"/> as it drops can take <paramref name="theme"/>
    /// forged: the relations beside its forged spread, and the forge limit
    /// (<see cref="ModifierRules.CanGraft"/>). A drop is forged and nothing else
    /// when the graft lands, so its spread is both what is present and what is forged.
    /// </summary>
    private static bool TakesAsForged(ModifierType theme, WeaponDef variant, ModifierRules modifiers)
    {
        var spread = ModifierSet.Of(variant.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());
        return modifiers.CanGraft(theme, Weapon.KindOf(variant.Class), spread, spread, asForged: true);
    }
}
