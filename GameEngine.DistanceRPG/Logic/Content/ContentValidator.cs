namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The well-formedness checks content must pass before anything reads it
/// (§5.4–§5.9). The enchantment catalogue is checked first, on its own — it is
/// the game's code, reference data by the user's ruling of 2026-09-25, and
/// needs nothing else; then the relations, because they are the predicates
/// everything after them is checked against, and the catalogue against them;
/// then the weapons — their class rows first — which name enchantments; then the
/// dungeons, whose themes name the classes' signatures and whose drop tables are
/// computed over the weapons; the starting party last, because it names
/// specific weapons. Every failure throws
/// a <see cref="ContentException"/> naming the entry and the rule; the first
/// failure aborts, in a fixed check order, so the report is deterministic.
/// (§5.7's "every effect reference resolves to an EffectKind" holds by
/// construction: a catalogue row's effect is typed.) And every number of a
/// content file lies in its range (<see cref="RuleNumberInRange"/>, the rows of
/// <see cref="ContentBounds"/>), asked after whatever rule bounded it before.
/// </summary>
public static class ContentValidator
{
    /// <summary>
    /// A content file's own shape (1h): JSON, the documented members and no
    /// others, each value of the documented kind. Checked as each file is read
    /// (<see cref="JsonShape"/>) and reported at that file's stage of the load,
    /// before anything reads the record — the one new check a file brings, and no
    /// new invariant: the record it reads to is checked by every rule below.
    /// </summary>
    public const string RuleFileShape = "a content file is well-formed JSON of its documented shape";

    /// <summary>
    /// The game carries every data file it plays built in (the content rework,
    /// 1f): the shipped copies in <c>GameEngine.DistanceRPG/Data</c>, the master
    /// copies, embedded when the game is built. Refused when a copy the load needs
    /// — the tuning always, a content file with none beside the executable — is not
    /// in the build or could not be read out of it: a build defect, stamped
    /// <c>(built-in)</c>, which the suite catches first. The-build-carries-it, not a
    /// new invariant.
    /// </summary>
    public const string RuleBuiltInCopy = "the game carries every data file it plays built in";

    /// <summary>
    /// A tuning table keyed by an enum names every member of it
    /// (<see cref="Tuning.PerStack"/>, <see cref="Tuning.Offset"/>,
    /// <see cref="Tuning.MaxForged"/>, <see cref="Tuning.EffectPerLevel"/>): there
    /// is no number in code under a member it leaves out. A file cannot break it —
    /// the built-in copy is refused for a gap as it is read, and a copy beside it
    /// merges over it — so this is the refusal for a <see cref="Tuning"/> built in
    /// code. A no-number-under-it check, not a new invariant.
    /// </summary>
    public const string RuleTableNamesEveryMember = "a tuning table names every member of its kind";

    /// <summary>
    /// A number the enchantment catalogue leaves to <c>tuning.json</c>
    /// (<see cref="EnchantmentDefinitions.SetByTuning"/>: the elements' and
    /// Serrated's <c>ApplyPercent</c>, Arcane's potency) is named there: the row
    /// states none of its own, so without the dial there is no number at all.
    /// Only the built-in copy can leave one out — a copy beside the game merges
    /// over it and cannot take one away — so a failure is stamped
    /// <c>tuning.json (built-in)</c>. A code-names-it-so-the-JSON-must-name-it
    /// check, not a new invariant.
    /// </summary>
    public const string RuleTuningOwnsTheNumber = "a number the enchantment catalogue leaves to tuning.json is named there";

    /// <summary>
    /// Every number of a content file lies in the range the game plays it in
    /// (the user's ruling of 2026-09-25, R3: "since we are storing in raw json we
    /// should probably not trust the fields"): each has a row of
    /// <see cref="ContentBounds"/>, with its reason, and one outside it stops the
    /// game naming the file, the entry, the field, the number and the range
    /// (<see cref="RequireInRange"/>) — never clamped, since content is validated
    /// (§5.4). A rule that bounded a field before the ruling keeps the end it owns,
    /// its detail now in this rule's form, and this is asked after it: of a class
    /// row once the row's own checks pass, of a weapon once its row's do, of a
    /// pinned boss floor once <see cref="RuleBossFloorRange"/> has kept the 1 of
    /// each end, and before that rule asks whether the range inverts or anything
    /// else reads the floor, so an inversion is never how a floor out of range is
    /// named; and of a boss after its own rules. What a variant
    /// adds is asked by the reader, as it is read and before it is summed
    /// (<see cref="WeaponsJson"/>). A number is always its own file's to fix, so a
    /// built-in copy out of range is the master's (<c>StartupErrorScene.ClosingLine</c>).
    /// A new invariant, the ruling's.
    /// </summary>
    public const string RuleNumberInRange = "a number in a content file lies in the range the game plays it in";

    public const string RuleUnknownId = "id does not resolve to a modifier or an enchantment";
    public const string RuleModifierRelationIds = "a modifier relation may only name modifier ids";
    public const string RuleKindValue = "kind must be melee, ranged or caster";
    public const string RuleSelfExclusion = "an id may not exclude itself";
    public const string RuleExcludesSymmetric = "excludes must close symmetrically";
    public const string RuleRequiresAcyclic = "requires must be acyclic";
    public const string RuleRequiresAndExcludes = "an id may not both require and exclude the same id";
    public const string RuleRequiresConflict = "an id may not require two ids that exclude each other";

    public const string RuleDuplicateId = "an id is declared more than once";
    public const string RuleZeroTrigger = "a trigger cost is never zero";
    public const string RuleTriggerXorApplies = "an entry quotes a flat trigger cost or applies a status, never both";
    public const string RuleApplierNamesStatus = "a status applier names the status it applies";
    public const string RuleElementNamesType = "an elemental entry names the damage type it carries";
    public const string RuleOpposition = "damage-type opposition must be symmetric and total";
    public const string RuleLingerNamesElement = "a lingering element names an element that exists and the status it leaves";
    public const string RuleMagnitudeNamesAPotency = "an enchantment whose effect is its potency names one of at least one";

    /// <summary>
    /// <c>neverRolled</c> names enchantments of the catalogue alone (§5.5): only an
    /// enchantment is rolled or copied, and nothing reads the list for a
    /// modifier, so a modifier there resolves — the id check passes it — and then
    /// silently does not apply, the mirror of what <c>kind</c> and
    /// <c>forgedOnly</c> refuse (<see cref="RuleModifierRelationIds"/>). Which
    /// enchantments it names is <c>restricted.json</c>'s alone (the user's ruling
    /// of 2026-09-25): the catalogue's <c>unique</c> flag pins a soul's tier and
    /// is not asked here. It was, until the rework's audit — the load held
    /// <c>neverRolled</c> to exactly the unique souls, a second copy of the
    /// relation in code, so the file could only restate it.
    /// </summary>
    public const string RuleNeverRolledNamesEnchantments = "neverRolled names enchantments of the catalogue alone";

    public const string RuleDropPoolResolves = "every id the drop pool names resolves in the catalogue";

    public const string RuleEnchantmentsExcluded = "a weapon's enchantments never include two that exclude each other";
    public const string RuleEnchantmentDependency = "an enchantment with a dependency appears only after its prerequisites on the same weapon";
    public const string RuleOverhealBesideHealing = "an enchantment that converts surplus healing appears only beside a source of it";
    public const string RuleDependencyOnCurrency = "an enchantment that depends on another appears only on a weapon forged with a currency modifier";
    public const string RuleUniqueSoulOnUnique = "a unique enchantment arrives only on a unique weapon";
    public const string RuleArrivingEnchantments = "no weapon arrives carrying more than three enchantments";

    public const string RuleUniqueDerivedFrom = "a unique names the variant it derives from, and nothing else does";
    public const string RuleUniqueStatline = "a unique keeps its variant's class, statline and shape";
    public const string RuleUniqueDerivation = "a unique is its variant with exactly one modifier it already carries raised to x3 and nothing else changed";
    public const string RuleUniqueRaisedByRole = "a unique raises what its variant's role allows: a Purity variant the class signature, a Control or Support variant the signature or its own added modifier, never the second baseline modifier";
    public const string RuleLightUniqueShape = "a Light-forged unique carries its signature at x2 or x3 and nothing else changed, with 5 - n enchantments, one of them unique or at tier 3";
    public const string RuleMartialUniqueOneSoul = "a martial unique that raises one modifier to x3 carries exactly one enchantment, and it is a unique one";
    public const string RuleCasterUniqueEnchantments = "a caster unique keeps its variant's spread and differs from it only in its enchantments";
    public const string RuleCasterUniqueShape = "a caster unique is one shape on its innate: beside one unique enchantment, alone at tier 3, or beside one more catalogue enchantment, both at tier 1";

    public const string RulePartySize = "a roster carries exactly one member per party slot";
    public const string RulePartyStatPermutation = "every innate stat spread is a permutation of 1-4";
    public const string RulePartyWeaponExists = "a party member's starting and bag weapons resolve in the weapon catalogue";
    public const string RulePartyBagFits = "a party member's starting weapon and bag fit the inventory";
    public const string RulePartyHasACaster = "a roster always includes a caster";

    public const string RulePoolBarIsPositive = "a pool's bar is at least one";
    public const string RuleDivisorIsPositive = "a ratio's divisor is at least one";
    public const string RuleChanceIsAPercentage = "a chance is a percentage, 0 to 100";
    public const string RuleRevivalTakesATurn = "a revival takes at least one turn";
    public const string RuleCurveMidpointIsACount = "the unique curve's midpoint is a DefeatCount, never negative";
    public const string RuleKillCountsForSomething = "a kill always counts for at least one";
    public const string RuleEnchantmentLockIsPositive = "an enchantment reserves at least one point of mana";
    public const string RuleBossFloorRange = "a boss floor range starts at floor 1 and is never inverted";
    public const string RuleBossPaysLessThanTheFarm = "a boss never pays as many stacks as a well-farmed two-modifier weapon banks";
    public const string RuleCountIsNeverNegative = "a count is never negative";
    public const string RuleFloorNumberStartsAtOne = "a floor number starts at 1";
    public const string RuleShareIsAPercentage = "a share is a percentage, 0 to 100";
    public const string RuleCarryFillsTheParty = "the party's carry divides evenly among its members, at least one slot each";
    public const string RuleEverySlotHasAKey = "a member carries no more slots than the number keys can equip";
    public const string RuleGatherTakesATile = "the stairs gather the party from at least a tile away";

    public const string RuleThemeIsClassSignature = "a dungeon's theme is a class signature";
    public const string RuleThemeHasAUnique = "a theme's own class has a Purity variant whose unique raises the theme to its forge limit";
    public const string RuleThemeReachesAClass = "at least one class can carry a dungeon's theme";
    public const string RuleThemeReachesItsOwnClass = "a theme's own class can carry it, so its unique can drop";
    public const string RuleBossWieldsAClass = "a dungeon's boss can wield at least one class that carries its theme";
    public const string RuleRetiringDungeonPinsItsFloor = "a dungeon that retires on clear pins its boss floor";
    public const string RuleOneRetiringDungeon = "exactly one dungeon retires on clear";
    public const string RuleBossCarriesTheTheme = "a dungeon's boss carries its theme innately";
    public const string RuleBossStatline = "a boss has at least one hit point and one unit of movement";
    public const string RuleBossInnateStackable = "a boss's innate modifiers are stackable members, at least one stack each";
    public const string RuleAttunementIsAnElement = "an attunement is a damage type or none";

    /// <summary>The entry id a roster-wide failure is reported against: the file, since no one member broke it.</summary>
    private const string RosterId = "party";

    /// <summary>The entry id a file-wide dungeon failure is reported against: the file, since no one dungeon broke it.</summary>
    private const string DungeonsId = "dungeons";

    /// <summary>The top of a percentage; the bottom is 0, since a chance of nothing is a legible way to switch a roll off.</summary>
    private const int FullPercent = 100;

    /// <summary>
    /// The fewest entries a farm banks acquired stacks on (§3.2): a martial
    /// weapon is forged with two modifiers at least (<see cref="RuleTwoForgedAxes"/>),
    /// and a caster with <c>Resonant</c> beside the enchantment it carries. Each
    /// banks up to <see cref="Tuning.AcquiredHeadroom"/> above its forging, so a
    /// well-farmed weapon holds at least <see cref="FarmBanks"/> — §4.3's "ten",
    /// the number no boss is allowed to match.
    /// </summary>
    internal const int FewestFarmedEntries = 2;

    /// <summary>
    /// What a well-farmed two-modifier weapon banks above its forging under a
    /// headroom of <paramref name="acquiredHeadroom"/> — ten at the shipped five.
    /// A boss's guaranteed stacks stay short of it (PHASE4-045): "the farm stays
    /// the only route to a finished weapon" (§4.3).
    /// </summary>
    internal static int FarmBanks(int acquiredHeadroom) => FewestFarmedEntries * acquiredHeadroom;

    /// <summary>What a unique raises its one modifier to: the forge limit, and why nothing is forged past it (§1.5).</summary>
    private const int UniqueRaise = 3;

    /// <summary>
    /// A Light-forged unique trades forged depth for souls: <c>signature xn</c> buys
    /// <c>5 - n</c> enchantments (§1.5). A rule of the unique table, which decides
    /// what a unique arrives with — never <see cref="Tuning.EnchantmentSlots"/>, the
    /// most the enchanter lets a weapon hold: both are the docs' five, and they mean
    /// different things, so moving the key moves the enchanter's ceiling and never
    /// the unique table's shape (settled.md, the user's ruling of 2026-09-25).
    /// </summary>
    private const int LightShapeSlots = 5;

    /// <summary>The floor of the Light shape: at x1 the weapon is the variant it derives from.</summary>
    private const int LightShapeFloor = 2;

    /// <summary>
    /// The most enchantments any weapon arrives carrying (§1.5): the Light
    /// shape at its floor, which spends the last stack there is to spend. The
    /// enchanter builds on from there, to <see cref="Tuning.EnchantmentSlots"/>
    /// (§6.2); nothing drops with more. It is the floor of that key's clamp, so no
    /// <c>tuning.json</c> can hold a weapon to fewer slots than it arrives filling.
    /// </summary>
    internal const int MostArrivingEnchantments = LightShapeSlots - LightShapeFloor;

    /// <summary>The tier that makes a catalogue enchantment an artifact's: the only place a drop starts above tier 1, and the deepest any weapon arrives at (<see cref="ContentBounds.Tier"/>).</summary>
    internal const int ArtifactTier = 3;

    /// <summary>The tier an ordinary drop's enchantment arrives at (§3.3): tier is earned by casting, from here — the shallowest any weapon arrives at (<see cref="ContentBounds.Tier"/>).</summary>
    internal const int DropTier = 1;

    /// <summary>
    /// The currency group (§1.1): the two discounts, forge-limited to x1 and
    /// excluding each other. The combo platform of §1.5 — the only weapons an
    /// enchantment may depend on another existing on, because they are the
    /// only ones that can pay for two triggers (Light earns the mana, Resonant
    /// spends less of it). Compiled: what a group of modifiers is for is code;
    /// which weapon carries them is data.
    /// </summary>
    private static readonly ModifierType[] CurrencyGroup = [ModifierType.Light, ModifierType.Resonant];

    public const string RuleForgedNotAllowed = "every forged spread must be allowed";
    public const string RuleForgedStackCount = "a forged stack count is at least one";
    public const string RuleMaxForged = "nothing is forged past MaxForged";
    public const string RuleTwoForgedAxes = "no weapon is forged with fewer than two modifiers";

    /// <summary>
    /// A caster class names no signature and forges one modifier at least, and
    /// every staff and wand is forged with its class's baseline and nothing else
    /// (§1.3, §1.4). The signature: a caster has no role and so no Purity variant
    /// to deepen one, and a dungeon's theme that found a caster class by it would
    /// look there for the Purity variant its unique is made from. The one
    /// modifier: a caster's two forged axes are a modifier
    /// and its innate (§5.6: "no weapon starts with fewer than two forged axes, and
    /// a caster's second is its enchantment"), which is what
    /// <see cref="FewestFarmedEntries"/> counts. <em>Which</em> modifier is the
    /// class row's alone — the shipped rows' <c>Resonant x1</c> is their design,
    /// pinned as a design doc's value by the tests
    /// (<c>WeaponClassTests.AClassRowIsWhatTheClassCarries</c>), never by this
    /// rule, which held both rows to exactly <c>Resonant x1</c> until the rework's
    /// audit found it a copy of their value. What else a baseline may hold is the
    /// forge's to say, asked as it is of every baseline.
    /// </summary>
    public const string RuleCasterForged = "a caster class names no signature and forges one modifier at least, and every caster weapon is forged with its class's baseline alone";
    public const string RuleCasterInnate = "a caster carries exactly one innate enchantment";
    public const string RuleWandShape = "a wand carries an area shape and nothing else does";
    public const string RuleWandNoStatusApplier = "a wand carries no status-applying enchantment: an area cast names no target to land it on";
    public const string RuleRoleOnMartialOnly = "only a martial variant carries a role";
    public const string RuleVariantRoles = "a martial class has four variants, one per role";
    public const string RuleClassBaseline = "a class baseline is its signature plus a second modifier";
    public const string RuleVariantDelta = "a variant is its class baseline plus exactly one added modifier type";
    public const string RuleEfficiencyAddsLight = "an Efficiency variant adds Light x1";
    public const string RulePurityDeepensBaseline = "a Purity variant deepens the class signature";
    public const string RuleForgedOnlyUnused = "every forgedOnly id is forged on at least one weapon";

    /// <summary>
    /// A caster class's row orders the variants its drop rolls between
    /// (<see cref="WeaponClassDef.DropOrder"/>): a caster class that fields a
    /// variant names a drop order, every id in it a variant of that class and
    /// none twice; and a martial class's row names none, since its drop draws a
    /// role. The draw indexes the order, so an id that is no variant of the class
    /// would land a drop on a row that does not exist — refused at load, the way
    /// the drop pool's own ids are, rather than on one drop in four deep inside a
    /// collection — and one named twice would be drawn twice as often. A variant
    /// the order leaves out never drops, which is not a fault: it is how a new
    /// staff waits to be added to the drops. The order is <c>weapons.json</c>'s
    /// since the rework's audit; it was a list in code, a copy of eight of the
    /// file's ids.
    /// </summary>
    public const string RuleCasterVariantPool = "a caster class orders the variants its drop rolls between - its own variants, each once - and a martial class orders none";
    public const string RuleClassCarriesAUnique = "a class a drop can roll carries at least one unique";
    public const string RuleMartialVariantNoEnchantment = "a martial variant arrives carrying no enchantment of its own";

    /// <summary>
    /// Each <see cref="WeaponClass"/> member has one row in <c>weapons.json</c>'s
    /// <c>classes</c> (the content rework, 1d): the code names every class — the
    /// placer draws five, a staff and a wand are cast with, the save keys weapon XP
    /// by class — so every class's kind, stats, signature, baseline and statline
    /// must resolve. The reader refuses a file that leaves one out, as the file's
    /// parse failure; <see cref="ValidateWeapons"/> refuses records built in code
    /// that do. A code-names-it-so-it-must-resolve check, the kind
    /// <see cref="RuleCodeNamedWeaponsExist"/> is, not a new invariant.
    /// </summary>
    public const string RuleEveryClassHasARow = "the classes section of weapons.json gives each of the eight classes one row";

    /// <summary>
    /// A class row's kind is <c>caster</c> exactly for the Staff and the Wand: the
    /// code keys what a caster does on those two members — a staff's innate is its
    /// cast, a wand's element and shape are supplied at instantiation — while
    /// melee or ranged stays free for the six martial classes. A
    /// code-names-it-so-the-row-must-fit check, not a new invariant.
    /// </summary>
    public const string RuleCasterClass = "a class casts exactly when the code casts with it: the Staff and the Wand";

    /// <summary>
    /// A class row levels on one stat or more, none named twice and never CON
    /// (§2.2; <see cref="Progression.Governing"/>: CON governs no weapon — it is the
    /// body's pool, not a craft). The reader refuses a name that is no
    /// <see cref="Stat"/>; this refuses what a <see cref="Stat"/> list can still get
    /// wrong, for a file and for records built in code alike.
    /// </summary>
    public const string RuleClassStats = "a class levels on one or more of STR, DEX and INT, each named once";

    /// <summary>
    /// No two class rows share a signature: a dungeon's theme finds its own class
    /// by signature (<see cref="WeaponCatalogue.ClassSignedBy"/>, asked in enum
    /// order), so a second class signed the same would never be found — a Ranged
    /// row signed CritWindow over a CritWindow and Longshot baseline passes
    /// <see cref="RuleClassBaseline"/> and would take a CritWindow theme from the
    /// Dagger in silence.
    /// </summary>
    public const string RuleClassSignatureDistinct = "no two classes share a signature";

    /// <summary>
    /// The weapons the code names resolve (1a): what a dummy holds unless told
    /// otherwise (<see cref="EnemyState.DefaultWeaponId"/>, instantiated by every
    /// <c>new EnemyState</c>), the staff a crowded room's healer is converted to
    /// (<see cref="EnemyPlacer.HealerWeaponId"/>), and a variant of every class the
    /// placer draws rank and file from (<see cref="EnemyPlacer.PlacedClasses"/>),
    /// which a floor's placement decodes its draw into. A compiled reference a
    /// <c>weapons.json</c> edit could otherwise orphan into a crash one construction
    /// deep, or one floor deep — the same kind of check as the drop pool's
    /// (<see cref="RuleDropPoolResolves"/>), not a new invariant.
    /// </summary>
    public const string RuleCodeNamedWeaponsExist = "every weapon the code names resolves: the default a dummy holds, the healer staff, and a variant of each class the placer draws";

    /// <summary>The weapons the code names by id, each with what it is named as: <see cref="RuleCodeNamedWeaponsExist"/>'s list, in the order it is asked.</summary>
    private static readonly (string Id, string NamedAs)[] CodeNamedWeapons =
    [
        (EnemyState.DefaultWeaponId, "what a dummy holds unless told otherwise (EnemyState.DefaultWeaponId)"),
        (EnemyPlacer.HealerWeaponId, "the staff a crowded room's healer carries (EnemyPlacer.HealerWeaponId)"),
    ];

    /// <summary>
    /// The §5.3 checks on <c>tuning.json</c>, run first because tuning loads
    /// first: a pool's bar is at least one. <see cref="Progression.XpToNext"/> is
    /// the doc's line and carries no guard of its own, so a bar of 0 would make
    /// every step free and the replay loop endless. Refusing it here turns a
    /// mistyped number into a named startup failure instead of a hang.
    /// <para>
    /// The §3.2 knobs are checked for the same reason and nothing more: a chance
    /// is a percentage, a revival takes a turn — a
    /// <see cref="Tuning.ResurrectTurnsFloor"/> of 0 lets
    /// <see cref="FarmLadder.ResurrectTurns"/> return 0 and a dummy revive the
    /// turn it died — a kill counts for at least one, since
    /// <see cref="FarmLadder.DefeatAdvance"/> hands back
    /// <see cref="Tuning.CleanKillBonus"/> verbatim and a 0 there would make the
    /// blow that clears a dummy's whole constitution advance nothing at all,
    /// inverting the rule the key exists to state — and the unique curve's
    /// midpoint is a <c>DefeatCount</c>, which is a count. Nothing here has an
    /// opinion about whether a number is well <em>tuned</em>; that is what
    /// playtesting is for, and it is why a <see cref="Tuning.ResurrectTurnsBase"/>
    /// under the floor is left alone: every revival then simply lands on the
    /// floor, which is a choice rather than a knob that cannot mean anything.
    /// </para>
    /// <para>
    /// The §4.3/§4.4 knobs are held to the same standard. The boss floor range
    /// starts at floor 1 and is never inverted, or the roll's span is negative;
    /// the stack offset and the cooldown per floor are counts; the deepest boss
    /// pays fewer guaranteed stacks than a well-farmed two-modifier weapon banks
    /// (<see cref="FarmBanks"/>: six against ten at the shipped numbers), since
    /// §4.3 makes that one absolute — "never raise the boss cap to 10" — and a
    /// boss that paid as much would leave the farm no longer the only route to a
    /// finished weapon, inverting the rule the offset exists to state; the mercy
    /// floor is a floor number, which starts at 1; the death clawback is a share
    /// of a gain; the carry divides evenly among the members with a slot each, or
    /// somebody has no hand to hold a weapon in and the party's 24 is not what
    /// the members carry, and shares out no deeper than the number keys equip
    /// (<see cref="GameConstants.MaxInventorySlots"/>, nine a member), or loot
    /// lands in a slot no key can reach; and the stairs gather from at least a
    /// tile away. So is §3.4's: a drink's cost (<see cref="Tuning.PotionUseCost"/>)
    /// is a count, movement spent or none, never movement handed back; and §6.6's:
    /// the farm's yield a clear (<see cref="Tuning.FarmYieldPerClear"/>) is a count
    /// of units, some or none, never units taken away.
    /// How far short of the farm a boss should stop, and every other judgement
    /// between these numbers, is a test on the shipped values, never a refusal
    /// here.
    /// </para>
    /// <para>
    /// And §5.3's ratios are integer divisors, so each one the game divides by
    /// is at least 1 (<see cref="RuleDivisorIsPositive"/>): the mana a banked
    /// unit of movement buys (<see cref="Tuning.MovementUnitsPerMana"/>), the
    /// party's budget an enemy's costs are scaled by
    /// (<see cref="Tuning.MovementBudget"/>), the surplus healing a Ward level
    /// takes (<see cref="Tuning.OverhealPerWard"/>) and the damage a point of
    /// trigger mana buys (<see cref="Tuning.DotDamagePerMana"/>, which divides
    /// with no guard of its own), and a potion's share a unit of quality
    /// (<see cref="Tuning.HealthPotionPercent"/>, <see cref="Tuning.ManaPotionPercent"/>),
    /// which the quality that restores a whole pool divides by
    /// (<see cref="Alchemist.QualityForAll"/>). A <c>tuning.json</c> never reaches this with
    /// one below 1 — the file clamps (<see cref="TuningJson.Read"/>) — so this
    /// is the refusal for a <see cref="Tuning"/> built in code.
    /// </para>
    /// <para>
    /// And first, before any number is judged, every table keyed by an enum names
    /// every member of it (<see cref="RuleTableNamesEveryMember"/>): the game
    /// reads each member directly, with nothing under a gap. A file cannot leave
    /// one — the built-in copy is refused for it as it is read, and a copy beside
    /// it merges over it — so this too is the refusal for a tuning built in code.
    /// </para>
    /// </summary>
    public static void ValidateTuning(Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        RequireEveryMember(tuning.EffectPerLevel, nameof(Tuning.EffectPerLevel), "status");
        RequireEveryMember(tuning.PerStack, nameof(Tuning.PerStack), "modifier");
        RequireEveryMember(tuning.Offset, nameof(Tuning.Offset), "modifier");
        RequireEveryMember(tuning.MaxForged, nameof(Tuning.MaxForged), "modifier");

        if (tuning.StartingPool < 1)
            throw new ContentException(nameof(Tuning.StartingPool), RulePoolBarIsPositive, $"{tuning.StartingPool}");
        if (tuning.WeaponXpPerLevel < 1)
            throw new ContentException(nameof(Tuning.WeaponXpPerLevel), RulePoolBarIsPositive, $"{tuning.WeaponXpPerLevel}");

        RequireDivisor(tuning.MovementUnitsPerMana, nameof(Tuning.MovementUnitsPerMana));
        RequireDivisor(tuning.MovementBudget, nameof(Tuning.MovementBudget));
        RequireDivisor(tuning.OverhealPerWard, nameof(Tuning.OverhealPerWard));
        RequireDivisor(tuning.DotDamagePerMana, nameof(Tuning.DotDamagePerMana));
        RequireDivisor(tuning.HealthPotionPercent, nameof(Tuning.HealthPotionPercent));
        RequireDivisor(tuning.ManaPotionPercent, nameof(Tuning.ManaPotionPercent));

        RequirePercentage(tuning.DefeatStackChance, nameof(Tuning.DefeatStackChance));
        RequirePercentage(tuning.DropEnchantChancePercent, nameof(Tuning.DropEnchantChancePercent));
        RequirePercentage(tuning.UniqueChanceCeilingPercent, nameof(Tuning.UniqueChanceCeilingPercent));

        if (tuning.ResurrectTurnsFloor < 1)
            throw new ContentException(nameof(Tuning.ResurrectTurnsFloor), RuleRevivalTakesATurn, $"{tuning.ResurrectTurnsFloor}");
        if (tuning.CleanKillBonus < 1)
            throw new ContentException(nameof(Tuning.CleanKillBonus), RuleKillCountsForSomething, $"{tuning.CleanKillBonus}");
        if (tuning.UniqueChanceMidpoint < 0)
            throw new ContentException(nameof(Tuning.UniqueChanceMidpoint), RuleCurveMidpointIsACount, $"{tuning.UniqueChanceMidpoint}");

        if (tuning.BossFloorMin < 1)
            throw new ContentException(nameof(Tuning.BossFloorMin), RuleBossFloorRange, $"{tuning.BossFloorMin}");
        if (tuning.BossFloorMax < tuning.BossFloorMin)
            throw new ContentException(nameof(Tuning.BossFloorMax), RuleBossFloorRange, $"{tuning.BossFloorMax}, shallower than {nameof(Tuning.BossFloorMin)} {tuning.BossFloorMin}");
        RequireCount(tuning.BossStackOffset, nameof(Tuning.BossStackOffset));
        RequireBossPaysLessThanTheFarm(tuning.BossFloorMax, DungeonState.PromisedStacksFor(tuning.BossFloorMax, tuning),
            tuning.AcquiredHeadroom, nameof(Tuning.BossStackOffset));
        RequireCount(tuning.CooldownRunsPerFloor, nameof(Tuning.CooldownRunsPerFloor));
        if (tuning.MercyFloor < 1)
            throw new ContentException(nameof(Tuning.MercyFloor), RuleFloorNumberStartsAtOne, $"{tuning.MercyFloor}");
        if (tuning.DeathXpLossPercent < 0 || tuning.DeathXpLossPercent > FullPercent)
            throw new ContentException(nameof(Tuning.DeathXpLossPercent), RuleShareIsAPercentage, $"{tuning.DeathXpLossPercent}");
        if (tuning.PartyCarrySlots < GameConstants.PartySize || tuning.PartyCarrySlots % GameConstants.PartySize != 0)
            throw new ContentException(nameof(Tuning.PartyCarrySlots), RuleCarryFillsTheParty,
                $"{tuning.PartyCarrySlots} slots among {GameConstants.PartySize} members");
        if (PartyMemberState.SlotsFor(tuning) > GameConstants.MaxInventorySlots)
            throw new ContentException(nameof(Tuning.PartyCarrySlots), RuleEverySlotHasAKey,
                $"{tuning.PartyCarrySlots} slots among {GameConstants.PartySize} members is {PartyMemberState.SlotsFor(tuning)} each, past {GameConstants.MaxInventorySlots}");
        if (tuning.StairGatherTiles < 1)
            throw new ContentException(nameof(Tuning.StairGatherTiles), RuleGatherTakesATile, $"{tuning.StairGatherTiles}");
        RequireCount(tuning.PotionUseCost, nameof(Tuning.PotionUseCost));
        RequireCount(tuning.FarmYieldPerClear, nameof(Tuning.FarmYieldPerClear));
    }

    /// <summary>
    /// <paramref name="table"/>, the tuning's table <paramref name="name"/>, names
    /// every member of <typeparamref name="TEnum"/> (<see cref="RuleTableNamesEveryMember"/>):
    /// the first it leaves out, in the enum's order, is refused, naming it. The
    /// modifier rules ask it of their three tables as well, so they are never
    /// built over a gap.
    /// </summary>
    internal static void RequireEveryMember<TEnum>(IReadOnlyDictionary<TEnum, int> table, string name, string noun) where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(table);
        foreach (var member in Enum.GetValues<TEnum>())
            if (!table.ContainsKey(member))
                throw new ContentException($"{name}.{member}", RuleTableNamesEveryMember,
                    $"{name} names {table.Count} of the {Enum.GetValues<TEnum>().Length} of its kind, and no {member}: every {noun} has a number in {TuningJson.FileName}, and none is kept in code");
    }

    /// <summary>
    /// The numbers the catalogue leaves to <c>tuning.json</c> are there
    /// (<see cref="RuleTuningOwnsTheNumber"/>): every row whose
    /// <c>ApplyPercent</c> is <see cref="EnchantmentDefinitions.SetByTuning"/> is
    /// named in <see cref="Tuning.ApplyPercent"/>, and a row whose potency is is
    /// the one <see cref="Tuning.ArcanePotency"/> sets (<see cref="Tuning.ArcaneId"/>),
    /// the only potency dial there is. In the catalogue's order, so the one
    /// reported is the same every run. The staged load asks this at its first
    /// stage and stamps a failure <c>tuning.json (built-in)</c>; the catalogue
    /// asks it again as it is built, so it is never built around a number no one
    /// sets.
    /// </summary>
    public static void ValidateDials(EnchantmentsData data, Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(tuning);
        foreach (var e in data.Enchantments)
        {
            if (e.ApplyPercent == EnchantmentDefinitions.SetByTuning && !tuning.ApplyPercent.ContainsKey(e.Id))
                throw new ContentException(e.Id, RuleTuningOwnsTheNumber,
                    $"the catalogue leaves the applyPercent of {e.Id} to {TuningJson.FileName} {nameof(Tuning.ApplyPercent)}.{e.Id}, which is not there");
            if (e.Potency == EnchantmentDefinitions.SetByTuning && e.Id != Tuning.ArcaneId)
                throw new ContentException(e.Id, RuleTuningOwnsTheNumber,
                    $"the catalogue leaves the potency of {e.Id} to {TuningJson.FileName}, whose one potency dial, {nameof(Tuning.ArcanePotency)}, sets {Tuning.ArcaneId} alone");
        }
    }

    /// <summary>A ratio the game divides by is at least 1: zero would divide by nothing, and a negative would turn a cost into a refund.</summary>
    private static void RequireDivisor(int value, string key)
    {
        if (value < 1)
            throw new ContentException(key, RuleDivisorIsPositive, $"{value}");
    }

    /// <summary>A knob that counts something is never negative; zero is a choice (nothing locks, nothing is offset).</summary>
    private static void RequireCount(int value, string key)
    {
        if (value < 0)
            throw new ContentException(key, RuleCountIsNeverNegative, $"{value}");
    }

    /// <summary>
    /// The boss on <paramref name="floor"/>, the deepest a range reaches, pays
    /// <paramref name="pays"/> guaranteed stacks; the farm stays ahead of it
    /// (PHASE4-045). Reported against <paramref name="entry"/>: the tuning's
    /// offset, the key that prices a floor, or the dungeon that pins its own range.
    /// </summary>
    private static void RequireBossPaysLessThanTheFarm(int floor, int pays, int acquiredHeadroom, string entry)
    {
        int banks = FarmBanks(acquiredHeadroom);
        if (pays >= banks)
            throw new ContentException(entry, RuleBossPaysLessThanTheFarm,
                $"a floor-{floor} boss pays {pays} stacks, and a well-farmed two-modifier weapon banks {banks}");
    }

    /// <summary>A knob rolled against a draw is a percentage, or the draw means nothing.</summary>
    private static void RequirePercentage(int value, string key)
    {
        if (value < 0 || value > FullPercent)
            throw new ContentException(key, RuleChanceIsAPercentage, $"{value}");
    }

    /// <summary>
    /// <paramref name="value"/>, written in <paramref name="entry"/> at
    /// <paramref name="field"/>, lies in <paramref name="bound"/>
    /// (<see cref="RuleNumberInRange"/>); refused otherwise, the detail in the one
    /// form every range fault takes (<see cref="ContentBounds.Detail"/>):
    /// <c>cost 0 is outside 1 to 10000 - …</c>. The entry is the class, the
    /// weapon or the dungeon, and the stage that asks stamps the file.
    /// </summary>
    internal static void RequireInRange(string entry, NumberBound bound, int value, string field)
    {
        ArgumentNullException.ThrowIfNull(bound);
        if (value < bound.Min || value > bound.Max)
            throw new ContentException(entry, RuleNumberInRange, ContentBounds.Detail(bound, field, value));
    }

    /// <summary>
    /// <see cref="ValidateParty(PartyData, WeaponCatalogue, Tuning)"/> against the
    /// current content's carry: a direct check of a roster outside a load. The
    /// load itself names the tuning it is loading, and never reads the current
    /// content.
    /// </summary>
    public static void ValidateParty(PartyData data, WeaponCatalogue weapons)
        => ValidateParty(data, weapons, GameContent.Current.Tuning);

    /// <summary>
    /// The §5.9 checks on the starting party — code (<see cref="StartingParty"/>),
    /// by the user's ruling of 2026-09-25 — run last because a member names
    /// weapons, at a stage the load stamps <c>weapons.json</c>, the one file an
    /// edit to could break it: the roster is exactly
    /// <see cref="GameConstants.PartySize"/> long, no id is declared twice, every
    /// spread is a permutation of 1–4 (§2.1, and §5.7's list — "free to check and
    /// silently wrong if it drifts"), a member's weapons fit their inventory and
    /// resolve in the catalogue, and somebody in the party is a caster.
    /// <para>
    /// The size check is the one that would otherwise fail silently: the scene
    /// spawns over the tiles it found for its own colour capacity, so a fifth
    /// member would simply never appear. It lives here, where the presentation
    /// cannot forget it.
    /// </para>
    /// Ids are resolved against catalogue membership and never instantiated, so
    /// <see cref="GameContent"/>'s own invariant — nothing here builds a
    /// <see cref="Weapon"/> — survives. Party-wide stat properties ("each stat
    /// is somebody's 4 exactly once") are deliberately <em>not</em> rules: the
    /// doc fixes the permutation and says the assignment is a first guess, so
    /// only the per-member rule is enforced and the roster's own arrangement is
    /// pinned by test.
    /// </summary>
    /// <param name="tuning">
    /// The tuning whose carry the bags are measured against
    /// (<see cref="PartyMemberState.SlotsFor"/>): <see cref="GameContent.Load"/>
    /// passes the one it is loading, whose content is not current yet — the load
    /// never reads the current content.
    /// </param>
    public static void ValidateParty(PartyData data, WeaponCatalogue weapons, Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(tuning);
        int slots = PartyMemberState.SlotsFor(tuning);

        if (data.Members.Count != GameConstants.PartySize)
            throw new ContentException(RosterId, RulePartySize, $"{data.Members.Count} members, {GameConstants.PartySize} slots");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in data.Members)
        {
            if (!seen.Add(member.Id))
                throw new ContentException(member.Id, RuleDuplicateId);

            if (!member.Stats.IsPermutation)
                throw new ContentException(member.Id, RulePartyStatPermutation, member.Stats.ToString());

            int carried = 1 + member.BagWeaponIds.Count;
            if (carried > slots)
                throw new ContentException(member.Id, RulePartyBagFits, $"{carried} weapons, {slots} slots");

            // Equipped first, then the bag in slot order: the first failure
            // reported is the same one every run.
            foreach (var id in member.BagWeaponIds.Prepend(member.StartingWeaponId))
                if (!weapons.TryGet(id, out _))
                    throw new ContentException(member.Id, RulePartyWeaponExists, id);
        }

        // Whoever opens the game holding a staff or a wand is the party's
        // caster — a caster by its class row, which casts exactly for those two
        // (RuleCasterClass) — and a bag staff would make the check vacuous, since
        // every member carries one.
        if (!data.Members.Any(m => weapons.KindOf(weapons[m.StartingWeaponId].Class) == WeaponKind.Caster))
            throw new ContentException(RosterId, RulePartyHasACaster, "no member starts with a staff or a wand");
    }

    /// <summary>
    /// The §5.8 checks on <c>dungeons.json</c> (P5:429-438), run after the weapons
    /// and against the tables <paramref name="tables"/> computed from them —
    /// <see cref="DungeonCatalogue"/> calls this from its constructor, the way
    /// <see cref="EnchantmentCatalogue"/> validates what it builds. In a fixed
    /// order, so the first failure reported is the same one every run: ids are
    /// unique; then, per dungeon in file order, the theme, the floors, the boss
    /// and the attunement; then the file-wide count of retiring dungeons.
    /// <list type="bullet">
    /// <item><strong>The theme is a class signature</strong> (§4.3's list, as
    /// the class rows of <c>weapons.json</c> name them —
    /// <see cref="WeaponCatalogue.ClassSignedBy"/>), and its own class's
    /// Purity variant has a unique raising it to its forge limit — the
    /// dungeon's one unique, the Purity coincidence §5.8 exists to protect. A
    /// theme that is not a signature "still works — it just quietly means that
    /// dungeon can never produce its unique, and nothing else in the game
    /// announces the difference". <c>Pin</c> and <c>Light</c> are refused here,
    /// and so are <c>CritMultiplier</c> — a second baseline modifier no Purity
    /// variant doubles — and <c>Resonant</c>, forge-limited to x1; <c>Cleave</c>
    /// is a signature with no unique on its Purity variant until one is
    /// authored. The limit checked is the tuned one —
    /// <paramref name="modifiers"/>' <see cref="ModifierRules.MaxForged"/> —
    /// rather than the compiled x3 a unique is raised to: a boss's drop becomes
    /// the unique by arithmetic only when its graft takes the theme to
    /// <c>MaxForged(theme)</c> (§4.3), so a tuning that raised a theme's limit
    /// past its unique would pass every weapon check — nothing is forged past
    /// the limit, the graft stays legal — and quietly make the unique
    /// unobtainable. In effect, a theme's forge limit stays its unique's x3.</item>
    /// <item><strong>The drop table is not empty</strong>, or "the boss drops
    /// nothing at all", <strong>and holds the theme's own class</strong>: a
    /// relations edit that pushed the theme's own class out of its table would
    /// pass every other check here and quietly make the unique unobtainable —
    /// the failure §5.8 says "will get broken by accident". The weapon checks
    /// cannot see it, since the Purity variant's own forged spread may still be
    /// legal while the graft on top of it is not.</item>
    /// <item><strong>The boss can wield something</strong>: its drop table has a
    /// class an enemy fields.</item>
    /// <item><strong>The boss floor range</strong>, pinned or the tuning's,
    /// starts at floor 1 at either end; <strong>a pinned floor is no deeper
    /// than any tuning can name</strong> (<see cref="RuleNumberInRange"/>,
    /// <see cref="ContentBounds.BossFloorMin"/>), asked next; and the range is
    /// not inverted, asked last of the three, so an inversion is only ever of
    /// two floors in range — all before anything else reads the floor; a
    /// retiring dungeon pins it —
    /// the tutorial's two floors are the enchanter's mana budget, not a roll —
    /// and <strong>its deepest floor pays less than the farm banks</strong>
    /// (<see cref="RuleBossPaysLessThanTheFarm"/>): the tuning's own range is
    /// held to that before any dungeon loads, so only a dungeon that pins a
    /// deeper range of its own can break it here.</item>
    /// <item><strong>The boss has a statline</strong> — a hit point and a unit
    /// of movement — <strong>its innate modifiers are stackable</strong> (no
    /// member <see cref="ModifierRules.NeverStacked"/>, at least one stack
    /// each: innate stacks are content, bounded by nothing a weapon's forge
    /// bounds) <strong>and include the theme</strong>, which it carries
    /// "regardless of what it wields"; and then each of those numbers lies in
    /// its range (<see cref="RuleNumberInRange"/>): no bigger than the bar a pool
    /// opens on, no farther than the tuning measures, no deeper than a stack can
    /// grow.</item>
    /// <item><strong>An attunement is a damage type or none.</strong></item>
    /// <item><strong>Exactly one dungeon retires on clear</strong>: two
    /// un-repeatable dungeons "is a content decision nobody has made", and none
    /// would leave the hub nothing to open behind.</item>
    /// </list>
    /// </summary>
    /// <param name="data">The dungeon rows, in file order.</param>
    /// <param name="tables">The drop and wield tables computed from <paramref name="modifiers"/> and <paramref name="weapons"/>.</param>
    /// <param name="modifiers">The in-flight rules the tables were computed from, whose forge limit a theme's unique must reach; never the current content's.</param>
    /// <param name="weapons">The weapon catalogue whose class rows name the signatures a theme is drawn from, and whose uniques the themes hunt.</param>
    public static void ValidateDungeons(DungeonsData data, DungeonCatalogue tables, ModifierRules modifiers, WeaponCatalogue weapons)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(weapons);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var def in data.Dungeons)
            if (!seen.Add(def.Id))
                throw new ContentException(def.Id, RuleDuplicateId);

        foreach (var def in data.Dungeons)
        {
            var theme = def.Theme;
            if (weapons.ClassSignedBy(theme) is not { } own)
                throw new ContentException(def.Id, RuleThemeIsClassSignature, $"{theme} is no class's signature");

            // The Purity variant forges its class's signature at x2; the unique
            // derived from it is the one the boss's graft reaches by arithmetic,
            // and the graft makes a drop that unique only when it takes the theme
            // to its forge limit (§4.3). So the check reads the limit the rules
            // were tuned to, not a compiled x3: a limit raised past the unique
            // leaves the graft legal and the unique unreachable.
            var purity = weapons.ByClass(own).Count > 0 ? weapons.Variant(own, VariantRole.Purity) : null;
            var unique = purity is null ? null : weapons.UniqueDerivedFrom(purity.Id);
            int limit = modifiers.MaxForged(theme);
            if (unique is null || unique.Forged.GetValueOrDefault(theme) != limit)
                throw new ContentException(def.Id, RuleThemeHasAUnique, purity is null
                    ? $"the weapons carry no {own} Purity variant"
                    : unique is null
                        ? $"'{purity.Id}' has no unique derived from it"
                        : $"'{unique.Id}' carries {theme} x{unique.Forged.GetValueOrDefault(theme)}, and {theme}'s forge limit is x{limit}");

            var table = tables.ThemeClasses(theme);
            if (table.Count == 0)
                throw new ContentException(def.Id, RuleThemeReachesAClass, $"no class takes {theme} as a forged graft");
            if (!table.Contains(own))
                throw new ContentException(def.Id, RuleThemeReachesItsOwnClass,
                    $"{theme}'s own class {own} is not among {string.Join(", ", table)}");
            if (tables.WieldClasses(def).Count == 0)
                throw new ContentException(def.Id, RuleBossWieldsAClass,
                    $"{string.Join(", ", table)} carry {theme}, and no enemy fields any of them");

            // Each end of the range starts at floor 1 — the 1 RuleBossFloorRange
            // keeps, its detail the range rule's form; then a floor the dungeon
            // pins is no deeper than any tuning names; and only then is the range
            // asked whether it inverts, so an inversion is only ever of two floors
            // in range, and a floor out of range is named as one, with the whole
            // range, whatever the tuning or the other end. All of it before the
            // retiring rule or the farm's reads the floor.
            var (min, max) = tables.BossFloorRange(def);
            string minEnd = FloorEnd(def.BossFloorMin, ContentBounds.BossFloorMin, nameof(Tuning.BossFloorMin));
            string maxEnd = FloorEnd(def.BossFloorMax, ContentBounds.BossFloorMax, nameof(Tuning.BossFloorMax));
            if (min < 1)
                throw new ContentException(def.Id, RuleBossFloorRange, ContentBounds.Detail(ContentBounds.BossFloorMin, minEnd, min));
            if (max < 1)
                throw new ContentException(def.Id, RuleBossFloorRange, ContentBounds.Detail(ContentBounds.BossFloorMax, maxEnd, max));
            if (def.BossFloorMin is { } pinnedMin)
                RequireInRange(def.Id, ContentBounds.BossFloorMin, pinnedMin, ContentBounds.BossFloorMin.Field);
            if (def.BossFloorMax is { } pinnedMax)
                RequireInRange(def.Id, ContentBounds.BossFloorMax, pinnedMax, ContentBounds.BossFloorMax.Field);
            if (max < min)
                throw new ContentException(def.Id, RuleBossFloorRange, $"{maxEnd} {max} is below {minEnd} {min}");
            if (def.RetiresOnClear && (def.BossFloorMin is null || def.BossFloorMin != def.BossFloorMax))
                throw new ContentException(def.Id, RuleRetiringDungeonPinsItsFloor,
                    $"floors {def.BossFloorMin?.ToString() ?? "unpinned"} to {def.BossFloorMax?.ToString() ?? "unpinned"}");
            RequireBossPaysLessThanTheFarm(max, tables.MostStacksPaid(def), modifiers.AcquiredHeadroom, def.Id);

            ValidateBoss(def);

            if (def.Attunement is { } element && (element == DamageType.None || !Enum.IsDefined(element)))
                throw new ContentException(def.Id, RuleAttunementIsAnElement, $"{element}");
        }

        var retiring = data.Dungeons.Where(d => d.RetiresOnClear).ToList();
        if (retiring.Count != 1)
            throw new ContentException(retiring.Count == 0 ? DungeonsId : retiring[1].Id, RuleOneRetiringDungeon,
                $"{retiring.Count} dungeons retire on clear");
    }

    /// <summary>
    /// What one end of a boss floor range is called in a refusal: the dungeon's
    /// field where it pins that end (<c>bossFloorMin</c>), else the
    /// <c>tuning.json</c> key it reads (<c>tuning.json BossFloorMax</c>).
    /// </summary>
    private static string FloorEnd(int? pinned, NumberBound row, string key)
        => pinned is null ? $"{TuningJson.FileName} {key}" : row.Field;

    /// <summary>
    /// A dungeon's boss: a statline (a hit point, a unit of movement), innate
    /// modifiers that each stack — walked in <see cref="ModifierType"/> order so
    /// the one reported is the same every run — and the theme among them; then
    /// each of those numbers in its range (<see cref="RuleNumberInRange"/>), the
    /// rules before it keeping the 1 they own, their details in its form
    /// (<c>boss.maxHp 0 is outside 1 to 10000 - …</c>). A member that never
    /// stacks breaks no range, and keeps its detail. Reported against the
    /// dungeon, the entry that names the boss.
    /// </summary>
    private static void ValidateBoss(DungeonDef def)
    {
        var boss = def.Boss;
        if (boss is null)
            throw new ContentException(def.Id, RuleBossStatline, "names no boss");
        if (boss.MaxHp < 1)
            throw new ContentException(def.Id, RuleBossStatline, ContentBounds.Detail(ContentBounds.BossMaxHp, ContentBounds.BossMaxHp.Field, boss.MaxHp));
        if (boss.MovementBudget < 1)
            throw new ContentException(def.Id, RuleBossStatline,
                ContentBounds.Detail(ContentBounds.BossMovementBudget, ContentBounds.BossMovementBudget.Field, boss.MovementBudget));

        var innate = boss.Innate ?? new Dictionary<ModifierType, int>();
        foreach (var (t, n) in innate.OrderBy(kv => kv.Key))
        {
            if (ModifierRules.NeverStacked.Contains(t) || !Enum.IsDefined(t))
                throw new ContentException(def.Id, RuleBossInnateStackable, $"'{boss.Id}': {t} x{n}");
            if (n < 1)
                throw new ContentException(def.Id, RuleBossInnateStackable, ContentBounds.Detail(ContentBounds.BossInnate, InnateField(t), n));
        }

        if (innate.GetValueOrDefault(def.Theme) < 1)
            throw new ContentException(def.Id, RuleBossCarriesTheTheme, $"'{boss.Id}' carries no {def.Theme}");

        // And then the range rule, the rules above having kept each floor.
        RequireInRange(def.Id, ContentBounds.BossMaxHp, boss.MaxHp, ContentBounds.BossMaxHp.Field);
        RequireInRange(def.Id, ContentBounds.BossMovementBudget, boss.MovementBudget, ContentBounds.BossMovementBudget.Field);
        foreach (var (t, n) in innate.OrderBy(kv => kv.Key))
            RequireInRange(def.Id, ContentBounds.BossInnate, n, InnateField(t));
    }

    /// <summary>A boss's innate stack of <paramref name="t"/>, as a refusal names it: <c>boss.innate.Block</c>.</summary>
    private static string InnateField(ModifierType t) => $"boss.innate.{t}";

    /// <summary>
    /// The §5.5 checks on <c>restricted.json</c>: every id resolves, a modifier
    /// relation names only modifiers (<c>kind</c> and <c>forgedOnly</c>, which
    /// only modifiers have, among them), no id excludes itself, excludes closes
    /// symmetrically, requires is acyclic, and nothing both requires and excludes
    /// the same id. <paramref name="knownIds"/> is every modifier and enchantment
    /// id content may name. (The "every forgedOnly id is forged on at least one
    /// weapon" check needs the weapon list and lives with the weapon checks.)
    /// An id that resolves to nothing is checked against the enchantment ids of
    /// the enchantment catalogue (<see cref="EnchantmentDefinitions"/>), so its
    /// detail says so, and names the id it is nearest when it is a typo of one.
    /// </summary>
    public static void ValidateRestricted(RestrictedData data, IReadOnlySet<string> knownIds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(knownIds);

        // Every id resolves to a real modifier or enchantment: a typo in a
        // relation is a rule that silently does not apply.
        // Dictionaries are walked in ordinal key order so the first failure
        // reported is the same one every run — a report, not a race.
        foreach (var group in data.Excludes)
            RequireKnown(group, knownIds);
        foreach (var (id, needs) in data.Requires.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            RequireKnown([id], knownIds);
            RequireKnown(needs, knownIds);
        }
        foreach (var (id, kind) in data.Kind.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            RequireKnown([id], knownIds);
            if (!RestrictedData.TryParseKind(kind, out _))
                throw new ContentException(id, RuleKindValue);
        }
        RequireKnown(data.ForgedOnly, knownIds);
        RequireKnown(data.NeverRolled, knownIds);

        // A relation keyed by a modifier names only modifiers; see the method.
        ValidateModifierRelations(data);

        // And the two relations only modifiers have name nothing else; see the method.
        ValidateModifierOnlyRelations(data);

        // No id excludes itself: a group naming an id twice would.
        foreach (var group in data.Excludes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in group)
                if (!seen.Add(id))
                    throw new ContentException(id, RuleSelfExclusion);
        }

        // Excludes closes symmetrically. Expanding groups guarantees it; the check
        // stays so the invariant outlives any future loader that takes pairs directly.
        var excludes = Closure(data.Excludes);
        foreach (var (a, others) in excludes)
            foreach (var b in others)
                if (!excludes.TryGetValue(b, out var back) || !back.Contains(a))
                    throw new ContentException(a, RuleExcludesSymmetric);

        // Requires is acyclic, and nothing both requires and excludes the same id
        // — directly or through a prerequisite's own prerequisites — because
        // that id could never legally exist.
        foreach (var id in data.Requires.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var prerequisites = Prerequisites(id, data.Requires);
            if (excludes.TryGetValue(id, out var excludedById))
                foreach (var p in prerequisites)
                    if (excludedById.Contains(p))
                        throw new ContentException(id, RuleRequiresAndExcludes);
            foreach (var p in prerequisites)
                if (excludes.TryGetValue(p, out var excludedByP) && prerequisites.Any(excludedByP.Contains))
                    throw new ContentException(id, RuleRequiresConflict);
        }
    }

    /// <summary>
    /// A relation keyed by a modifier names only modifiers: a <c>requires</c> row
    /// keyed by one, and an exclusion group containing one. <see cref="ModifierRules.Allowed"/>
    /// sees a weapon's modifiers and nothing else, so a modifier requiring or
    /// excluding an enchantment id could only be dropped on the way in — the
    /// rule that silently does not apply, which the id check exists to prevent.
    /// Relations keyed by an enchantment are free to name enchantments; they are
    /// read beside the enchantment catalogue. The entry named is the modifier.
    /// <see cref="ModifierRules"/> runs this itself as well, so the rules can
    /// never be built around a dropped relation. (<c>kind</c> and
    /// <c>forgedOnly</c> are modifier relations whole, and the load refuses an
    /// enchantment in either: <see cref="ValidateModifierOnlyRelations"/>.)
    /// </summary>
    public static void ValidateModifierRelations(RestrictedData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var isModifier = (string id) => ModifierRules.ModifierIds.Contains(id);

        foreach (var group in data.Excludes)
        {
            var modifier = group.FirstOrDefault(isModifier);
            if (modifier != null && !group.All(isModifier))
                throw new ContentException(modifier, RuleModifierRelationIds);
        }

        foreach (var (id, needs) in data.Requires.OrderBy(r => r.Key, StringComparer.Ordinal))
            if (isModifier(id) && !needs.All(isModifier))
                throw new ContentException(id, RuleModifierRelationIds);
    }

    /// <summary>
    /// <c>kind</c> and <c>forgedOnly</c> name modifiers alone (PHASE5-092, -108,
    /// -109): §1.1's weapon-kind restriction and its safety rule — §5.5's
    /// <c>Brace</c> melee and <c>Charges</c> — are modifier relations whole.
    /// <see cref="ModifierRules"/> keeps their modifier rows, and nothing reads
    /// either for an enchantment: unlike an exclusion group or a <c>requires</c>
    /// row, neither has an enchantment reading anywhere. So an enchantment named
    /// in one resolves — the id check passes it — and then silently does not
    /// apply, the failure §5.5's id check exists to prevent. Refused under
    /// <see cref="RuleModifierRelationIds"/>, the entry the enchantment, the
    /// detail naming the file and the row; <c>kind</c> in ordinal key order, then
    /// <c>forgedOnly</c> in its own order, so the one reported is the same every
    /// run. This is the relations' own check, at <c>restricted.json</c>'s stage
    /// of the load, before any rules are built. It is not in
    /// <see cref="ValidateModifierRelations"/>, which the rules also run: they
    /// skip a row keyed by an enchantment by contract, and the load refuses one.
    /// </summary>
    private static void ValidateModifierOnlyRelations(RestrictedData data)
    {
        foreach (var (id, kind) in data.Kind.OrderBy(k => k.Key, StringComparer.Ordinal))
            if (!ModifierRules.ModifierIds.Contains(id))
                throw new ContentException(id, RuleModifierRelationIds,
                    $"'{id}' is an enchantment, and kind limits modifiers alone - {RestrictedJson.FileName} kind {id}: {kind}");

        foreach (var id in data.ForgedOnly)
            if (!ModifierRules.ModifierIds.Contains(id))
                throw new ContentException(id, RuleModifierRelationIds,
                    $"'{id}' is an enchantment, and forgedOnly lists modifiers alone - {RestrictedJson.FileName} forgedOnly {id}");
    }

    /// <summary>
    /// The §5.7 checks that need the entries alone: ids are unique, no trigger
    /// cost is zero (no enchantment fires free, ever), an entry quotes a flat
    /// trigger cost or applies a status and never both (a status applier prices
    /// off its level count, so a flat cost would decay into free), a status
    /// applier — a staff's effect, Serrated's wound, a lingering burn — names
    /// its status, an elemental entry names its type, and a lingering element
    /// names the element it lingers and the status it leaves. Which kinds
    /// land a status and which linger an element is read off the behaviour
    /// tables (<see cref="EnchantmentBehaviours.AppliesStatus"/>,
    /// <see cref="EnchantmentBehaviours.LingersAnElement"/>), so a new kind
    /// is a row there and never an edit here. With the first rule, the
    /// second makes every unique magnitude quote no flat trigger (P5:331): it
    /// applies a status, so its price is the ladder over what it lands.
    /// <see cref="EnchantmentCatalogue"/> runs this itself as well.
    /// <para>
    /// The lock joins them here because §3.3 makes it load-bearing twice: it
    /// reserves mana while equipped <em>and</em> it is the bar the tier ladder
    /// divides (<see cref="Enchantment.XpToNextTier"/>). A row with
    /// <c>Lock: 0</c> would price every tier at nothing for every stat, so it is
    /// refused at load exactly the way a <see cref="Tuning.StartingPool"/> of 0
    /// already is, rather than guarded inside the replay.
    /// </para>
    /// <para>
    /// The potency rule is the same idea one column over, for the kinds whose
    /// whole magnitude <em>is</em> their potency
    /// (<see cref="EnchantmentBehaviours.SizedByPotency"/>: Arcane's damage,
    /// Shattering's depth, Aegis's absorption). At zero such a row fires for
    /// nothing while still reserving its lock and the slot it sits in — the one
    /// shape §3.3 refuses everywhere else. Every other kind reads its magnitude
    /// from somewhere the row does not carry — an element's typing, a rule that
    /// fires once, a percentage of the blow — so none of them is asked. It is
    /// the row that is checked, not what a §5.3 dial lays over it: a row that
    /// states a magnitude has to state one worth firing for. A row that leaves
    /// its magnitude to <c>tuning.json</c> (<see cref="EnchantmentDefinitions.SetByTuning"/>:
    /// Arcane's, whose dial is <see cref="Tuning.ArcanePotency"/>) states none,
    /// so it is not asked here; its dial must be there
    /// (<see cref="ValidateDials"/>), and the file clamps it to 0–1000, where 0
    /// is the legal way to switch it off. The dial being keyed by id is what
    /// keeps this rule meaning something: every row it checks that no dial
    /// names is a row the game reads the magnitude from.
    /// </para>
    /// <para>
    /// The game's catalogue is code (<see cref="EnchantmentDefinitions"/>), so the
    /// load asks this first and on its own, stamping no file: no data file can
    /// break a rule here, only a code edit, which the suite catches first.
    /// </para>
    /// </summary>
    public static void ValidateEnchantments(EnchantmentsData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in data.Enchantments)
        {
            if (!seen.Add(e.Id))
                throw new ContentException(e.Id, RuleDuplicateId);
            if (e.Lock < 1)
                throw new ContentException(e.Id, RuleEnchantmentLockIsPositive, $"lock {e.Lock}");
            if (e.Trigger is <= 0)
                throw new ContentException(e.Id, RuleZeroTrigger, $"trigger {e.Trigger}");
            if ((e.Trigger != null) == (e.Applies != null))
                throw new ContentException(e.Id, RuleTriggerXorApplies,
                    e.Trigger != null ? "quotes a trigger and applies a status" : "quotes no trigger and applies nothing");
            if (EnchantmentBehaviours.LingersAnElement(e.Effect) && (e.DamageType is null or DamageType.None || e.Applies == null))
                throw new ContentException(e.Id, RuleLingerNamesElement,
                    e.Applies == null ? "leaves no status" : "names no element");
            if (EnchantmentBehaviours.AppliesStatus(e.Effect) && e.Applies == null)
                throw new ContentException(e.Id, RuleApplierNamesStatus);
            if (e.Effect == EffectKind.ElementalDamage && e.DamageType is null or DamageType.None)
                throw new ContentException(e.Id, RuleElementNamesType);
            if (EnchantmentBehaviours.SizedByPotency(e.Effect) && e.Potency != EnchantmentDefinitions.SetByTuning && e.Potency < 1)
                throw new ContentException(e.Id, RuleMagnitudeNamesAPotency, $"potency {e.Potency}");
        }

        // The drop pool is an ordered id list held in code (§3.1), so that
        // appending a catalogue entry cannot silently re-index what an
        // already-saved dummy drops. The cost of holding it there is that it can
        // name something the catalogue does not: refused here, the way every
        // other dangling reference is, rather than throwing at the first drop rolled.
        foreach (var id in LootTable.DropPool)
            if (!seen.Contains(id))
                throw new ContentException(id, RuleDropPoolResolves);
    }

    /// <summary>
    /// <c>neverRolled</c> against the catalogue (§5.5; <see cref="RuleNeverRolledNamesEnchantments"/>):
    /// every id it names is an enchantment of the catalogue, in the file's order,
    /// so the one reported is the same every run. <em>Which</em> enchantments no
    /// roll grants is <c>restricted.json</c>'s alone — the relations live there
    /// and nowhere else (the user's ruling of 2026-09-25: "if it lives in the
    /// code then changing the json loses all purpose") — so the catalogue's
    /// <c>unique</c> flag, which pins a soul's tier (<see cref="Enchantment.UniqueTier"/>),
    /// is not asked. It was until the rework's audit, which found the load
    /// holding <c>neverRolled</c> to exactly the unique souls: any edit to the
    /// list alone stopped the game, so the file could only restate the code. (A
    /// soul the file leaves out is rollable, and still drops nowhere: the drop
    /// pool, <see cref="LootTable.DropPool"/>, names none.)
    /// <see cref="EnchantmentCatalogue"/> runs this as it is built, at
    /// <c>restricted.json</c>'s stage of the load, after the id check has passed
    /// every id as a modifier or an enchantment: what is left to refuse is a
    /// modifier, which nothing reads the list for.
    /// </summary>
    public static void ValidateNeverRolled(EnchantmentsData data, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(restricted);

        var ids = data.Enchantments.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in restricted.NeverRolled)
            if (!ids.Contains(id))
                throw new ContentException(id, RuleNeverRolledNamesEnchantments, ModifierRules.ModifierIds.Contains(id)
                    ? $"'{id}' is a modifier, and neverRolled lists enchantments alone - {RestrictedJson.FileName} neverRolled {id}"
                    : $"'{id}' is no enchantment of the catalogue - {RestrictedJson.FileName} neverRolled {id}");
    }

    /// <summary>
    /// §1.4's chart as content (P5:392): four types, two pairs, every type opposed
    /// by exactly one. Each <see cref="DamageType"/> other than None is carried by
    /// exactly one elemental entry, and the exclusion groups of
    /// <paramref name="restricted"/> oppose every element to exactly one other,
    /// both ways. Returns the chart it validated — each type to the one that
    /// opposes it — for the catalogue to answer the type-chart step with. Run at
    /// <c>restricted.json</c>'s stage of the load — the elements are the code's,
    /// the pairs the file's — so a pair the groups get wrong is stamped with that
    /// file and names its <c>excludes</c> in the detail.
    /// </summary>
    public static IReadOnlyDictionary<DamageType, DamageType> ValidateOpposition(EnchantmentsData data, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(restricted);

        var elements = data.Enchantments.Where(e => e.Effect == EffectKind.ElementalDamage).ToList();
        var byType = new Dictionary<DamageType, EnchantmentDef>();
        foreach (var e in elements)
        {
            var type = e.DamageType ?? DamageType.None;
            if (type == DamageType.None)
                throw new ContentException(e.Id, RuleElementNamesType);
            if (!byType.TryAdd(type, e))
                throw new ContentException(e.Id, RuleOpposition, $"{type} is carried by '{byType[type].Id}' as well");
        }
        foreach (var type in Enum.GetValues<DamageType>())
            if (type != DamageType.None && !byType.ContainsKey(type))
                throw new ContentException(type.ToString(), RuleOpposition, "no innate enchantment carries it");

        // A lingering element lingers an element somebody carries: Searing is nothing without its Flaming.
        foreach (var e in data.Enchantments)
            if (EnchantmentBehaviours.LingersAnElement(e.Effect) && (e.DamageType is not { } lingered || !byType.ContainsKey(lingered)))
                throw new ContentException(e.Id, RuleLingerNamesElement, "no innate enchantment carries its element");

        var excludes = Closure(restricted.Excludes);
        var byId = elements.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var chart = new Dictionary<DamageType, DamageType>();
        foreach (var e in elements)
        {
            var opposed = excludes.TryGetValue(e.Id, out var set) ? set.Where(byId.ContainsKey).ToList() : new List<string>();
            if (opposed.Count != 1)
                throw new ContentException(e.Id, RuleOpposition, $"opposed by {opposed.Count} elements, not one, in {RestrictedJson.FileName} excludes");
            if (!excludes.TryGetValue(opposed[0], out var back) || !back.Contains(e.Id))
                throw new ContentException(e.Id, RuleOpposition, $"'{opposed[0]}' does not oppose it back in {RestrictedJson.FileName} excludes");
            chart[e.DamageType!.Value] = byId[opposed[0]].DamageType!.Value;
        }
        return chart;
    }

    /// <summary>
    /// The §5.6 checks on <c>weapons.json</c> (P5:317-332). The class rows first,
    /// in <see cref="WeaponClass"/> order, each failure's entry the class
    /// (<see cref="ValidateClasses"/>): one row a class, a kind that casts exactly
    /// for the Staff and the Wand, the stats it levels on, a signature no other
    /// class has, a baseline the forge allows (asked what a weapon's spread is
    /// asked, below), and a baseline of that signature and one more — or, for a
    /// caster, no signature, a baseline of one modifier at least, and a drop order
    /// only a caster names. Then the weapon rows, each read as the
    /// kind its class row gives it: ids are unique; every
    /// enchantment a weapon names resolves; every forged stack count is at least
    /// one; every forged modifier is allowed
    /// beside the rest of its spread — the state every later roll deepens
    /// from, asked of <see cref="ModifierRules.Allowed"/> with the whole spread
    /// present, because a from-zero build-up would refuse the forge's own
    /// <c>Charges</c>, and the forge is exactly what <c>forgedOnly</c> reserves
    /// the first stack for, and which refuses on every weapon a member outside
    /// the §1.1 table (<see cref="ModifierRules.NeverStacked"/>: Momentum,
    /// enchantment-only, and the reserved OnHitPoison), since a stack of one
    /// would change nothing; nothing is forged past <see cref="ModifierRules.MaxForged"/>;
    /// a martial weapon is forged on at least two modifiers and a caster on its
    /// class's baseline alone beside its one innate enchantment (a staff's
    /// fixed status effect; a wand's element, supplied at instantiation); only
    /// wands carry a shape, no wand carries a status-applying enchantment (an
    /// area cast is aimed at a point and names no target to land one on), and
    /// only martial variants carry a role; each martial class
    /// fields four variants, one per role, on its row's baseline — none carrying
    /// less of it — every variant adding exactly one modifier type to it
    /// (Efficiency <c>Light x1</c>, Purity the class signature again, Control and
    /// Support a new one); no
    /// weapon arrives with two enchantments that exclude each other — opposed
    /// types cannot share a weapon (§1.4), Flaming and Cold excluding each
    /// other exactly as Push and Drag do, off the same groups, while
    /// non-opposing types stack freely; and every <c>forgedOnly</c> id is
    /// forged somewhere, or it could never exist. An enchantment's dependency
    /// is met on the weapon itself (§1.5): a relation-keyed prerequisite ahead
    /// of its dependent, a surplus-healing converter beside a source of
    /// healing, and either only on a weapon forged with a currency modifier —
    /// the combo platform — while a unique enchantment arrives only on a
    /// unique, and nothing arrives carrying more than three enchantments.
    /// Unique derivation is checked with the uniques.
    /// <para>
    /// <strong>Every number in its range</strong> (<see cref="RuleNumberInRange"/>,
    /// <see cref="ContentBounds"/>), each asked after the checks of the row it is
    /// written in: a class row's statline once its row passes; a weapon's
    /// statline, its shape's measured dimensions and the tiers of what it arrives
    /// with once its own row does — each between the tier a drop arrives at and an
    /// artifact's, a unique soul's too, which plays at its pin. A
    /// forged count keeps the forge's own two rules, its detail in the range
    /// rule's form: a class row's baseline (<c>baseline.CritMultiplier 0 is
    /// outside 1 to 3 - …</c>) and a weapon's spread as the reader composed it
    /// (<c>forged.CritWindow 4 is outside 1 to 3 - …</c>), the top the tuned
    /// <see cref="ModifierRules.MaxForged"/> of the modifier.
    /// </para>
    /// <para>
    /// And three the drop leans on (§3.1, §3.2), each refusing at load what
    /// would otherwise surface as a wrong weapon or an exception one collection
    /// deep: a class with variants carries at least one unique, for a won roll
    /// to land on; a caster class that fields variants orders the ones its drop
    /// rolls between on its row, each its own and none twice
    /// (<see cref="RuleCasterVariantPool"/>), since the drop's draw indexes that
    /// order rather than the file's; and a martial variant carries
    /// no enchantment of its own, so the one entry a drop attaches is the one it
    /// rolled. So do the two weapons the code names by id and the classes the
    /// placer draws, each of which must field a variant
    /// (<see cref="RuleCodeNamedWeaponsExist"/>), asked once every row has passed
    /// its own checks and before the checks over whole classes — only "at least
    /// one", so a class short of a role is still <see cref="RuleVariantRoles"/>'s.
    /// </para>
    /// <para>
    /// <strong>A relations edit that makes a weapon illegal names both</strong>
    /// (§5.5, P5:294-298): the weapon is the entry (or its class, where the
    /// relation refuses the class's baseline: the class row alone writes it and
    /// every weapon of the class is forged with it, so the entry named is the one
    /// of the two to change), and the detail ends with the
    /// <c>restricted.json</c> relation that refused it — for a forged modifier the
    /// clause <see cref="ModifierRules.Allowed"/> refused on
    /// (<see cref="WhyNotAllowed"/>), for two enchantments the group that
    /// excludes them, for a dependency the <c>requires</c> row — read off
    /// <paramref name="restricted"/>, the relations in hand. So is a
    /// <c>forgedOnly</c> id no weapon is forged with: the check reads
    /// <c>restricted.json</c> at the weapons' stage, and says so.
    /// </para>
    /// </summary>
    public static void ValidateWeapons(WeaponsData data, ModifierRules rules, RestrictedData restricted, EnchantmentCatalogue enchantments)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(restricted);
        ArgumentNullException.ThrowIfNull(enchantments);

        // The class rows first: every weapon after them is read as the kind its
        // row gives it, and every variant against its row's baseline.
        ValidateClasses(data, rules, restricted);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var def in data.Weapons)
            if (!seen.Add(def.Id))
                throw new ContentException(def.Id, RuleDuplicateId);

        foreach (var def in data.Weapons)
        {
            var of = OfAWeaponIn(def, data);
            var spread = Spread(def.Id, def.Forged, of, rules);
            var row = data.ClassOf(def.Class)
                ?? throw new ContentException(def.Id, RuleEveryClassHasARow, $"its class {def.Class} has no row");
            var kind = row.Kind;

            foreach (var reference in def.Enchantments)
                if (!enchantments.TryGet(reference.Id, out _))
                    throw new ContentException(def.Id, RuleUnknownId, reference.Id);

            // Three souls is the most any weapon arrives carrying (§1.5).
            if (def.Enchantments.Count > MostArrivingEnchantments)
                throw new ContentException(def.Id, RuleArrivingEnchantments, $"{def.Enchantments.Count} enchantments");

            // One hit cannot be two contradictory things: an entry's enchantments
            // never include a pair the relations exclude, in attachment order so
            // the pair reported is the same one every run — naming the
            // restricted.json group that excludes them (P5:294-298: "naming both").
            // The pair is the catalogue's question, the one the enchanter asks of
            // what it would attach beside the weapon's entries (Phase 6a.3b).
            for (int i = 0; i < def.Enchantments.Count; i++)
                for (int j = i + 1; j < def.Enchantments.Count; j++)
                {
                    string a = def.Enchantments[i].Id, b = def.Enchantments[j].Id;
                    if (enchantments.Excludes(a, b))
                        throw new ContentException(def.Id, RuleEnchantmentsExcluded, WithRelation($"'{a}' and '{b}'", ExcludingGroup(restricted, a, b)));
                }

            // A dependency (§5.5 requires, keyed by an enchantment) is met on the
            // weapon itself, ahead of the dependent in attachment order: the loop
            // fires in that order (§1.7), and a lingering element reads the
            // element that fired before it, so behind it the burn could never
            // fire. Content that leaves the element off is refused here, naming
            // the restricted.json row it breaks; a transfer that takes it off
            // later (§6.5) leaves the burn quiet. The question is the catalogue's,
            // the one the enchanter asks of what it would attach (Phase 6a.3b).
            for (int i = 0; i < def.Enchantments.Count; i++)
            {
                string id = def.Enchantments[i].Id;
                if (enchantments.LacksPrerequisiteAhead(id, def.Enchantments.Take(i).Select(r => r.Id), out var need))
                    throw new ContentException(def.Id, RuleEnchantmentDependency, WithRelation($"'{id}' needs '{need}' ahead of it", RequiresRow(restricted, id)));
            }

            // Overheal alone is inert (§1.5): an entry whose behaviour fires on
            // healing above full converts a surplus something else must make,
            // so it sits only beside something that heals the wielder — a
            // status the table says restores HP, or a kind whose behaviour drinks.
            foreach (var reference in def.Enchantments)
                if (ConvertsSurplusHealing(enchantments[reference.Id])
                    && !def.Enchantments.Any(r => IsHealingSource(enchantments[r.Id])))
                    throw new ContentException(def.Id, RuleOverhealBesideHealing, $"'{reference.Id}' has no healing source beside it");

            // And a dependency needs two souls paid for, which only the currency
            // group can afford (§1.5): "the only place in the design where an
            // enchantment is allowed to depend on another one existing". A
            // Light artifact earns the second trigger's mana, a Resonant one
            // spends less on both; anything else carrying a pair that feeds
            // itself is a combo the design never priced.
            foreach (var reference in def.Enchantments)
                if (DependsOnAnother(enchantments[reference.Id], enchantments) && !CurrencyGroup.Any(t => spread.Stacks(t) > 0))
                    throw new ContentException(def.Id, RuleDependencyOnCurrency, $"'{reference.Id}' on a weapon forged without {string.Join(" or ", CurrencyGroup)}");

            // A unique enchantment is a thing that exists, not a catalogue entry
            // (§1.5, §3.3): content hands one only to the uniques that carry
            // it, never to a variant a drop could roll.
            if (!def.Unique)
                foreach (var reference in def.Enchantments)
                    if (enchantments[reference.Id].Unique)
                        throw new ContentException(def.Id, RuleUniqueSoulOnUnique, $"'{reference.Id}' on a variant");

            // Allowed is the question, asked exactly as every graft and roll asks it
            // (PHASE5-133: "no new validation code exists"), and then the forge
            // limit; see the method.
            RequireForgeable(def.Id, of, spread, kind, rules, restricted);

            if (kind == WeaponKind.Caster)
            {
                // A caster varies by what it casts and the shape it covers, never
                // by its spread: every staff and wand is forged with its class's
                // baseline, which the row alone writes (ValidateClasses has asked
                // it), and nothing else.
                var baseline = ModifierSet.Of(row.Baseline.Select(b => (b.Key, b.Value)).ToArray());
                if (!spread.Equals(baseline))
                    throw new ContentException(def.Id, RuleCasterForged, $"{spread}, and the {def.Class} baseline is {baseline}");
                if (def.Role != null)
                    throw new ContentException(def.Id, RuleRoleOnMartialOnly, def.Role.ToString());
                if ((def.Class == WeaponClass.Wand) != (def.Shape != null))
                    throw new ContentException(def.Id, RuleWandShape);
                ValidateCasterInnate(def, enchantments);
            }
            else
            {
                if (spread.Entries.Count() < 2)
                    throw new ContentException(def.Id, RuleTwoForgedAxes, spread.ToString());
                if (def.Shape != null)
                    throw new ContentException(def.Id, RuleWandShape);
                if (!def.Unique && def.Role == null)
                    throw new ContentException(def.Id, RuleRoleOnMartialOnly, "a martial variant carries no role");
                if (def.Unique && def.Role != null)
                    throw new ContentException(def.Id, RuleRoleOnMartialOnly, $"a unique carries the role {def.Role}");

                // A martial drop carries the entry it rolled and nothing else
                // (§3.1's "always exactly one"), and the farm banks its tiers
                // through the first one attached — which is the rolled entry
                // only because the variant lists none, since Instantiate appends
                // what the drop rolled after whatever the def carries. A variant
                // authored with an entry of its own would make a rolled drop
                // carry two and land the farm's tiers on the wrong one, both
                // silently. A unique is hand-placed rather than rolled and
                // carries its souls; a caster's single innate is its identity
                // and is RuleCasterInnate's.
                if (!def.Unique && def.Enchantments.Count > 0)
                    throw new ContentException(def.Id, RuleMartialVariantNoEnchantment,
                        $"'{def.Enchantments[0].Id}' on a {def.Class} variant");
            }

            // And every number of the row in its range (RuleNumberInRange), once
            // the row's own checks have passed: the statline as composed — a
            // variant's own number or its class's, a unique's its variant's, so a
            // class row's fault is named on the class first — the dimensions its
            // shape's kind measures, and the tier of each enchantment it arrives
            // with, in attachment order, between a drop's tier and an artifact's.
            RequireInRange(def.Id, ContentBounds.VariantRange, def.Range, "range");
            RequireInRange(def.Id, ContentBounds.VariantDamage, def.Damage, "damage");
            RequireInRange(def.Id, ContentBounds.VariantCost, def.Cost, "cost");
            RequireInRange(def.Id, ContentBounds.VariantManaCost, def.ManaCost, "manaCost");
            if (def.Shape is { } area && WeaponsJson.Dimensions.TryGetValue(area.Kind, out var measured))
                foreach (string dimension in measured)
                    RequireInRange(def.Id, ContentBounds.Dimension(dimension), DimensionOf(area, dimension), $"shape.{dimension}");
            for (int j = 0; j < def.Enchantments.Count; j++)
                RequireInRange(def.Id, ContentBounds.Tier, def.Enchantments[j].Tier, $"enchantments[{j}].tier");
        }

        // The weapons the code names by id are here (RuleCodeNamedWeaponsExist):
        // after every row has passed its own checks, and ahead of the checks over
        // whole classes, so a row taken out is named for what the code needs of
        // it rather than for the class it leaves short.
        foreach (var (id, namedAs) in CodeNamedWeapons)
            if (!seen.Contains(id))
                throw new ContentException(id, RuleCodeNamedWeaponsExist,
                    $"the code names it as {namedAs}, and {WeaponsJson.FileName} has no such weapon");

        // And a variant of every class the placer draws rank and file from: a
        // floor's placement decodes its draw into one of them, so a class left
        // empty would throw at the first floor rather than stop here. Only "at
        // least one" — a class short of a role is RuleVariantRoles', below.
        foreach (var cls in EnemyPlacer.PlacedClasses)
            if (!data.Weapons.Any(w => w.Class == cls && !w.Unique))
                throw new ContentException(cls.ToString(), RuleCodeNamedWeaponsExist,
                    $"the placer draws dummies of the {cls} class (EnemyPlacer.PlacedClasses), and {WeaponsJson.FileName} has no {cls} variant");

        foreach (var cls in Enum.GetValues<WeaponClass>())
        {
            var variants = data.Weapons.Where(w => w.Class == cls && !w.Unique).ToList();
            var row = data.ClassOf(cls)!;   // every class has its one row: ValidateClasses
            if (variants.Count == 0)
            {
                // A class the file gives no weapon is not malformed, only absent,
                // unless the placer draws it (above) — but a drop order there
                // names weapons that are not.
                if (row.DropOrder is { Count: > 0 } dangling)
                    throw new ContentException(dangling[0], RuleCasterVariantPool, $"named by the {cls} drop order, and no {cls} variant has that id");
                continue;
            }

            // A class with variants is a class a drop can roll (§3.1), and §3.2's
            // unique roll is a ruling: a won draw always yields a unique of the
            // dummy's class, re-steering over the class's own where the variant
            // has none. A class carrying no unique at all would make that draw
            // win and hand over nothing — reported as a lost roll rather than as
            // broken content — so it is refused here instead.
            if (!data.Weapons.Any(w => w.Unique && w.Class == cls))
                throw new ContentException(cls.ToString(), RuleClassCarriesAUnique, $"{variants.Count} variants, no unique");

            if (row.Kind == WeaponKind.Caster)
            {
                // Casters vary by effect and shape, not by role, so a drop's draw
                // indexes the order the class row names (weapons.json's) rather
                // than the file's order of its variants. Every id it names has to
                // be a variant of the class, or the draw lands on a row that does
                // not exist: refused at load, the way the drop pool's ids already
                // are, rather than throwing on one drop in four deep inside a
                // collection.
                if (row.DropOrder is not { Count: > 0 } order)
                    throw new ContentException(cls.ToString(), RuleCasterVariantPool,
                        $"names no dropOrder, the order its drop rolls its variants in - a copy of {WeaponsJson.FileName} older than the order has none: delete it and the built-in weapons play, or run --write-data <exe folder>");
                var ordered = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in order)
                {
                    if (!variants.Any(v => v.Id == id))
                        throw new ContentException(id, RuleCasterVariantPool, $"named by the {cls} drop order, and no {cls} variant has that id");
                    if (!ordered.Add(id))
                        throw new ContentException(id, RuleCasterVariantPool, $"named twice by the {cls} drop order");
                }
                continue;
            }

            ValidateVariants(row, variants);
        }

        ValidateUniques(data, enchantments);

        // A check that reads restricted.json at the weapons' stage names it (1h).
        foreach (var id in restricted.ForgedOnly)
            if (ModifierRules.TryParseId(id, out var t) && !data.Weapons.Any(w => w.Forged.GetValueOrDefault(t) > 0))
                throw new ContentException(id, RuleForgedOnlyUnused,
                    $"{RestrictedJson.FileName} forgedOnly {id}, and no weapon in {WeaponsJson.FileName} is forged with it");
    }

    /// <summary>
    /// Why <see cref="ModifierRules.Allowed"/> refuses <paramref name="t"/> on a
    /// weapon of <paramref name="kind"/> carrying <paramref name="present"/> — the
    /// relation that refused it, as <c>restricted.json</c> writes it — or null when
    /// it is allowed (P5:294-298, PHASE5-122: a relations edit that makes a weapon
    /// illegal names "the weapon that is now illegal <em>and</em> the rule that
    /// made it so").
    /// <para>
    /// An explanation, never a check (PHASE5-133: "no new validation code
    /// exists"): the validator asks <see cref="ModifierRules.Allowed"/> and calls
    /// this only once it has refused. It asks Allowed's own clauses in Allowed's
    /// order — a member outside the modifier table, the weapon kind, forged-only,
    /// the exclusion groups, the dependencies — so when several refuse, the first
    /// is the one named; and it reads them off <paramref name="restricted"/>, the
    /// relations the rules in hand were built from, the way
    /// <see cref="ModifierRules"/> reads them: a group or a row keyed by an
    /// enchantment is no modifier's. So it is non-null exactly when Allowed is
    /// false. The wording is the forge's — <paramref name="present"/> is the
    /// spread a row is forged with — and a member outside the table names no
    /// relation at all, since none refused it.
    /// </para>
    /// </summary>
    internal static string? WhyNotAllowed(ModifierType t, WeaponKind kind, ModifierSet present, RestrictedData restricted)
    {
        ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(restricted);
        string name = t.ToString();

        if (ModifierRules.NeverStacked.Contains(t))
            return $"{name} is not in the modifier table";

        if (restricted.Kind.TryGetValue(name, out var only) && RestrictedData.TryParseKind(only, out var required) && !required.HasFlag(kind))
            return $"{RestrictedJson.FileName} kind {name}: {only}";

        if (restricted.ForgedOnly.Contains(name, StringComparer.Ordinal) && present.Stacks(t) == 0)
            return $"{RestrictedJson.FileName} forgedOnly {name}";

        foreach (var group in restricted.Excludes)
        {
            if (!group.Contains(name, StringComparer.Ordinal))
                continue;
            foreach (var other in group)
                if (other != name && ModifierRules.TryParseId(other, out var excluded) && present.Stacks(excluded) > 0)
                    return $"{RestrictedJson.FileName} excludes [{string.Join(", ", group)}]: {other} is already forged here";
        }

        if (restricted.Requires.TryGetValue(name, out var needs))
        {
            var missing = needs.Where(need => ModifierRules.TryParseId(need, out var needed) && present.Stacks(needed) == 0).Distinct(StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
                return $"{RestrictedJson.FileName} requires {name}: [{string.Join(", ", needs)}], and this weapon has no {string.Join(" or ", missing)}";
        }
        return null;
    }

    /// <summary><paramref name="detail"/>, then the restricted.json relation that refused it after a dash — or the detail alone when no relation is named.</summary>
    private static string WithRelation(string detail, string? relation) => relation is null ? detail : $"{detail} - {relation}";

    /// <summary>The first exclusion group, in file order, holding both <paramref name="a"/> and <paramref name="b"/>, as <c>restricted.json</c> writes it; null when none does.</summary>
    private static string? ExcludingGroup(RestrictedData restricted, string a, string b)
        => restricted.Excludes.FirstOrDefault(group => group.Contains(a, StringComparer.Ordinal) && group.Contains(b, StringComparer.Ordinal)) is { } found
            ? $"{RestrictedJson.FileName} excludes [{string.Join(", ", found)}]"
            : null;

    /// <summary><paramref name="id"/>'s <c>requires</c> row as <c>restricted.json</c> writes it; null when it has none.</summary>
    private static string? RequiresRow(RestrictedData restricted, string id)
        => restricted.Requires.TryGetValue(id, out var needs) ? $"{RestrictedJson.FileName} requires {id}: [{string.Join(", ", needs)}]" : null;

    private static void ValidateCasterInnate(WeaponDef def, EnchantmentCatalogue enchantments)
    {
        var attached = def.Enchantments.Select(r => enchantments[r.Id]).ToList();
        if (def.Class == WeaponClass.Staff)
        {
            // Fixed by variant: the first enchantment is the effect it casts.
            if (attached.Count == 0 || attached[0].Effect != EffectKind.ApplyStatus || attached[0].Applies == null)
                throw new ContentException(def.Id, RuleCasterInnate, "a staff's first enchantment is the status it casts");
            if (attached[0].Targets == TargetSide.Any)
                throw new ContentException(def.Id, RuleCasterInnate, "a staff's effect targets allies or enemies, not either");
            if (!def.Unique && attached.Count != 1)
                throw new ContentException(def.Id, RuleCasterInnate, $"{attached.Count} enchantments on a variant");
        }
        else
        {
            // A wand's innate is a damage type: rolled with the drop, so a variant
            // lists none and the caller supplies it; a unique may fix its element.
            if (!def.Unique && attached.Count != 0)
                throw new ContentException(def.Id, RuleCasterInnate, "a wand variant's element is supplied at instantiation");
            if (attached.Count > 0 && attached[0].Effect != EffectKind.ElementalDamage)
                throw new ContentException(def.Id, RuleCasterInnate, "a wand's first enchantment is its element");
            // An area cast is aimed at a point and names the caster as its own
            // target (the Cast payload's ruling), so a status-applying cast
            // entry — a staff's kind — would land its status on the caster.
            // Refused, wherever it sits in the list, until Phase 3 decides what
            // one means on a wand: every caught actor, or nothing. Hit-side
            // kinds (a lingering element) fire on the hits and are unaffected.
            var applier = attached.FirstOrDefault(NoWandCarries);
            if (applier != null)
                throw new ContentException(def.Id, RuleWandNoStatusApplier, $"'{applier.Id}' applies {applier.Applies}");
        }
    }

    /// <summary>
    /// Whether <paramref name="entry"/> is one a wand may not carry
    /// (<see cref="RuleWandNoStatusApplier"/>): a status-applying cast entry, a
    /// staff's kind, which a wand's area cast would land on the caster. The one
    /// question content asks of a wand's list and the enchanter asks of an entry
    /// it would attach to a wand (<see cref="Enchanter.Offers"/>).
    /// </summary>
    internal static bool NoWandCarries(EnchantmentDef entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Effect == EffectKind.ApplyStatus;
    }

    /// <summary>
    /// The class rows (the content rework, 1d), in <see cref="WeaponClass"/> order,
    /// each failure's entry the class, each check one the code needs of a row
    /// rather than a new invariant:
    /// <list type="number">
    /// <item>one row a class (<see cref="RuleEveryClassHasARow"/>) — the reader's
    /// refusal for a file, this one for records built in code, which bypass it;</item>
    /// <item>a kind that is <c>caster</c> exactly for the Staff and the Wand, which
    /// the code casts with (<see cref="RuleCasterClass"/>);</item>
    /// <item>one stat or more to level on, none twice, never CON
    /// (<see cref="RuleClassStats"/>);</item>
    /// <item>a signature no class before it in the enum has
    /// (<see cref="RuleClassSignatureDistinct"/>), since a theme finds its class
    /// by signature;</item>
    /// <item>a baseline the forge allows, asked what a weapon's spread is asked
    /// and in the same order: every stack count at least one
    /// (<see cref="RuleForgedStackCount"/>), each modifier allowed beside the rest
    /// on the row's kind (<see cref="RuleForgedNotAllowed"/>, the detail naming
    /// the <c>restricted.json</c> relation that refused it), none past its forge
    /// limit (<see cref="RuleMaxForged"/>). The reader sums the baseline into
    /// every variant of the class, so a fault in it would otherwise surface on
    /// the first variant, a row with none of it written in; asked here, it names
    /// the class, where the baseline is written — and a class that fields no
    /// variant has its baseline asked all the same;</item>
    /// <item>a martial row's signature and baseline: a signature, and a baseline
    /// of exactly it and one other modifier (<see cref="RuleClassBaseline"/>); a
    /// caster row's: no signature, and a baseline of one modifier at least
    /// (<see cref="RuleCasterForged"/>) — which modifiers is the row's, the forge
    /// having allowed them above;</item>
    /// <item>a drop order on a caster's row alone
    /// (<see cref="RuleCasterVariantPool"/>): a martial drop draws one variant per
    /// role, so an order on a martial row is a list nothing reads. What a caster's
    /// order names is asked with the weapons, whose ids it names;</item>
    /// <item>and then its statline in its range (<see cref="RuleNumberInRange"/>):
    /// a reach and a cost of a unit at least, a damage and a mana cost of 0 at
    /// least, none past the farthest the tuning measures or the most a magnitude
    /// is (<see cref="ContentBounds"/>) — asked here, on the class, so a fault in
    /// the row is named where it is written and not on the first variant that
    /// plays it. A baseline's counts keep the forge's rules, their details in the
    /// range rule's form (<c>baseline.CritMultiplier 4 is outside 1 to 3 - …</c>).</item>
    /// </list>
    /// </summary>
    /// <param name="data">The class rows and the weapons.</param>
    /// <param name="rules">The in-flight rules — the relations and the fitted forge limits — a baseline is asked of, as a weapon's spread is.</param>
    /// <param name="restricted">The relations <paramref name="rules"/> were built from, which a refusal names.</param>
    private static void ValidateClasses(WeaponsData data, ModifierRules rules, RestrictedData restricted)
    {
        var signedBy = new Dictionary<ModifierType, WeaponClass>();
        foreach (var cls in Enum.GetValues<WeaponClass>())
        {
            string entry = cls.ToString();
            var rows = data.Classes.Where(row => row.Class == cls).ToList();
            if (rows.Count != 1)
                throw new ContentException(entry, RuleEveryClassHasARow,
                    rows.Count == 0 ? $"{WeaponsJson.FileName} has no {cls} row" : $"{rows.Count} {cls} rows");
            var row = rows[0];

            bool castsWith = cls is WeaponClass.Staff or WeaponClass.Wand;
            if ((row.Kind == WeaponKind.Caster) != castsWith)
                throw new ContentException(entry, RuleCasterClass, castsWith
                    ? $"kind {KindWord(row.Kind)}, and the code casts with the {cls}"
                    : $"kind {KindWord(row.Kind)}, and the code casts with the Staff and the Wand alone");

            if (row.Stats.Count == 0)
                throw new ContentException(entry, RuleClassStats, "levels on no stat");
            var levelsOn = new HashSet<Stat>();
            foreach (var stat in row.Stats)
                if (!levelsOn.Add(stat))
                    throw new ContentException(entry, RuleClassStats, $"names {stat} twice");
            if (levelsOn.Contains(Stat.CON))
                throw new ContentException(entry, RuleClassStats, "names CON, which governs no weapon: it is the pool of the body, not a craft");

            if (row.Signature is { } signature)
            {
                if (signedBy.TryGetValue(signature, out var first))
                    throw new ContentException(entry, RuleClassSignatureDistinct, $"{signature} is the signature of the {first} as well");
                signedBy[signature] = cls;
            }

            // Every weapon of the class is forged with the baseline, so it is the
            // forge's to allow before the class's shape is asked of it: a stack of
            // 0, a modifier restricted.json refuses on this kind, or one past its
            // limit is the row's fault, named here rather than on a variant.
            RequireForgeable(entry, OfABaseline, Spread(entry, row.Baseline, OfABaseline, rules), row.Kind, rules, restricted);

            string spread = row.Baseline.Count == 0
                ? "nothing"
                : string.Join(", ", row.Baseline.OrderBy(b => b.Key).Select(b => $"{b.Key} x{b.Value}"));
            if (castsWith)
            {
                if (row.Signature is not null || row.Baseline.Count == 0)
                    throw new ContentException(entry, RuleCasterForged, $"signature {row.Signature?.ToString() ?? "none"}, baseline {spread}");
            }
            else if (row.Signature is not { } named || row.Baseline.Count != 2 || !row.Baseline.ContainsKey(named))
            {
                throw new ContentException(entry, RuleClassBaseline, $"baseline is {spread}, signature {row.Signature?.ToString() ?? "none"}");
            }

            if (!castsWith && row.DropOrder is not null)
                throw new ContentException(entry, RuleCasterVariantPool,
                    $"names a dropOrder, and a {cls} drop draws one variant per role - only the Staff and the Wand order theirs");

            // The statline every weapon of the class shares, in its range, once
            // the row's own checks pass: named on the class, where it is written.
            RequireInRange(entry, ContentBounds.ClassRange, row.Range, "range");
            RequireInRange(entry, ContentBounds.ClassDamage, row.Damage, "damage");
            RequireInRange(entry, ContentBounds.ClassCost, row.Cost, "cost");
            RequireInRange(entry, ContentBounds.ClassManaCost, row.ManaCost, "manaCost");
        }
    }

    /// <summary>A kind as <c>weapons.json</c> and <c>restricted.json</c> write it: melee, ranged or caster.</summary>
    private static string KindWord(WeaponKind kind) => kind switch
    {
        WeaponKind.Melee => RestrictedData.MeleeKind,
        WeaponKind.Ranged => RestrictedData.RangedKind,
        WeaponKind.Caster => RestrictedData.CasterKind,
        _ => kind.ToString(),
    };

    /// <summary>
    /// The per-class variant checks, against the class's row: four variants, one
    /// per role; none carrying less of a baseline modifier than the row forges,
    /// and each adding exactly one modifier type to the baseline in the shape its
    /// role says.
    /// </summary>
    private static void ValidateVariants(WeaponClassDef row, List<WeaponDef> variants)
    {
        var cls = row.Class;
        foreach (var role in Enum.GetValues<VariantRole>())
        {
            int count = variants.Count(v => v.Role == role);
            if (count != 1)
                throw new ContentException(cls.ToString(), RuleVariantRoles, $"{count} {role} variants");
        }

        // The baseline is the row's (ValidateClasses: the signature and one
        // other). Every variant carries all of it, and one addition on top.
        var baseline = row.Baseline;
        var signature = row.Signature!.Value;
        foreach (var v in variants)
        {
            foreach (var (under, n) in baseline.OrderBy(b => b.Key))
                if (v.Forged.GetValueOrDefault(under) < n)
                    throw new ContentException(v.Id, RuleVariantDelta, $"carries {under} x{v.Forged.GetValueOrDefault(under)}, below the x{n} of the {cls} baseline");

            var added = AddedTo(v, baseline);
            if (added.Count != 1)
                throw new ContentException(v.Id, RuleVariantDelta, $"adds {added.Count} modifier types to the {cls} baseline");

            var t = added[0];
            switch (v.Role)
            {
                case VariantRole.Efficiency:
                    if (t != ModifierType.Light || v.Forged[t] != 1)
                        throw new ContentException(v.Id, RuleEfficiencyAddsLight, $"adds {t} x{v.Forged[t]}");
                    break;
                case VariantRole.Purity:
                    if (t != signature)
                        throw new ContentException(v.Id, RulePurityDeepensBaseline, $"adds {t}, not the {cls} signature {signature}");
                    break;
                default:
                    if (baseline.ContainsKey(t))
                        throw new ContentException(v.Id, RuleVariantDelta, $"deepens {t} instead of adding a modifier");
                    break;
            }
        }
    }

    /// <summary>
    /// The modifier types <paramref name="variant"/> carries beyond its class
    /// baseline, in enum order: its role's one addition once the variant
    /// checks have passed — Light for Efficiency, the signature again for
    /// Purity, the rider for Control and Support.
    /// </summary>
    private static List<ModifierType> AddedTo(WeaponDef variant, IReadOnlyDictionary<ModifierType, int> baseline)
        => variant.Forged
            .Where(f => f.Value > baseline.GetValueOrDefault(f.Key))
            .Select(f => f.Key)
            .OrderBy(t => t)
            .ToList();

    /// <summary>
    /// The derivation rule (§1.5, P5:328-330), checked over the whole unique
    /// table (K:179-182): a unique names the variant it derives from, and
    /// nothing else does; it keeps that variant's class, statline and shape —
    /// "everything else is unchanged". A martial unique is the variant with
    /// exactly one modifier it already carries raised to x3 and nothing else
    /// changed — never a second x3, never a modifier the variant lacks, never
    /// a freely-authored spread — and the modifier raised is one its
    /// variant's role may raise (§1.5's four shapes, W:1137-1142): a Purity
    /// variant's the class signature, a Control or Support variant's the
    /// signature or its own rider, so no unique ever raises the class's
    /// second baseline modifier; carrying exactly one enchantment, and one
    /// that exists nowhere else: "one modifier at x3, plus its unique
    /// enchantment". Unless the variant is Light-forged, in which case the
    /// currency cannot be raised and the artifact's identity lives in its
    /// souls: the signature at x2 or x3 and nothing else changed, with 5 - n
    /// enchantments, one of them unique or at tier 3. A caster unique raises
    /// nothing — Resonant is forge-limited to x1 — and differs from its
    /// variant only in its enchantment list, which is the whole artifact and
    /// takes one of three shapes (<see cref="ValidateCasterUniqueShape"/>). The
    /// class's kind, signature and baseline are its row's. (The reader composes
    /// a unique over the variant it names, so a file cannot break the first
    /// three; records built in code can.)
    /// </summary>
    private static void ValidateUniques(WeaponsData data, EnchantmentCatalogue enchantments)
    {
        var byId = data.Weapons.ToDictionary(w => w.Id, StringComparer.Ordinal);
        foreach (var def in data.Weapons)
        {
            if (!def.Unique)
            {
                if (def.DerivedFrom != null)
                    throw new ContentException(def.Id, RuleUniqueDerivedFrom, $"a variant names '{def.DerivedFrom}'");
                continue;
            }

            if (def.DerivedFrom == null)
                throw new ContentException(def.Id, RuleUniqueDerivedFrom, "names no variant");
            if (!byId.TryGetValue(def.DerivedFrom, out var variant) || variant.Unique)
                throw new ContentException(def.Id, RuleUniqueDerivedFrom, $"'{def.DerivedFrom}' is not a variant");
            if (variant.Class != def.Class || variant.Range != def.Range || variant.Damage != def.Damage
                || variant.Cost != def.Cost || variant.ManaCost != def.ManaCost || !Equals(variant.Shape, def.Shape))
                throw new ContentException(def.Id, RuleUniqueStatline, $"differs from '{variant.Id}' beyond its spread and enchantments");

            var changed = Enum.GetValues<ModifierType>()
                .Where(t => def.Forged.GetValueOrDefault(t) != variant.Forged.GetValueOrDefault(t))
                .ToList();

            var row = data.ClassOf(def.Class)!;   // the variant's class, which has its one row: ValidateClasses
            if (row.Kind == WeaponKind.Caster)
            {
                if (changed.Count != 0)
                    throw new ContentException(def.Id, RuleCasterUniqueEnchantments, $"changes {string.Join(", ", changed)}");
                if (def.Enchantments.SequenceEqual(variant.Enchantments))
                    throw new ContentException(def.Id, RuleCasterUniqueEnchantments, "its enchantments are the variant's own");
                ValidateCasterUniqueShape(def, variant, enchantments);
                continue;
            }

            var signature = row.Signature!.Value;   // a martial row names one: ValidateClasses
            if (variant.Forged.GetValueOrDefault(ModifierType.Light) > 0)
            {
                // The Light shape: depth traded for souls, one per stack given up, floor x2.
                if (changed.Count != 1 || changed[0] != signature)
                    throw new ContentException(def.Id, RuleLightUniqueShape, changed.Count == 0
                        ? "raises nothing"
                        : $"changes {string.Join(", ", changed)}, not the {def.Class} signature {signature} alone");
                int n = def.Forged.GetValueOrDefault(signature);
                if (n < LightShapeFloor || n > UniqueRaise)
                    throw new ContentException(def.Id, RuleLightUniqueShape, $"{signature} x{n}: the floor is x{LightShapeFloor} and the forge limit x{UniqueRaise}");
                int souls = LightShapeSlots - n;
                if (def.Enchantments.Count != souls)
                    throw new ContentException(def.Id, RuleLightUniqueShape, $"{def.Enchantments.Count} enchantments at {signature} x{n}, not {souls}");
                if (!def.Enchantments.Any(r => enchantments[r.Id].Unique || r.Tier >= ArtifactTier))
                    throw new ContentException(def.Id, RuleLightUniqueShape, $"none of its enchantments is unique or at tier {ArtifactTier}");
                continue;
            }

            if (changed.Count != 1)
                throw new ContentException(def.Id, RuleUniqueDerivation, changed.Count == 0
                    ? $"is '{variant.Id}' itself"
                    : $"changes {string.Join(", ", changed)}: exactly one modifier is raised");
            var raised = changed[0];
            int was = variant.Forged.GetValueOrDefault(raised), now = def.Forged.GetValueOrDefault(raised);
            if (was == 0)
                throw new ContentException(def.Id, RuleUniqueDerivation, $"adds {raised}, which '{variant.Id}' lacks");
            if (now != UniqueRaise)
                throw new ContentException(def.Id, RuleUniqueDerivation, now < was ? $"drops {raised}" : $"{raised} x{now}, not x{UniqueRaise}");

            // And it is a modifier the variant's role may raise (§1.5's four
            // shapes): the signature, or what the variant itself added — for
            // a Purity variant the signature again, for Control and Support
            // their rider. What neither names is the class's second baseline
            // modifier, which no role's unique raises. (The variant checks
            // above have passed, so the class has a baseline and the variant
            // one addition.)
            var added = AddedTo(variant, row.Baseline).Single();
            if (raised != signature && raised != added)
                throw new ContentException(def.Id, RuleUniqueRaisedByRole, variant.Role == VariantRole.Purity
                    ? $"raises {raised}: a unique of the Purity '{variant.Id}' deepens the {def.Class} signature {signature} alone"
                    : $"raises {raised}, the {def.Class} baseline's second modifier: a unique of the {variant.Role} '{variant.Id}' raises the signature {signature} or its own {added}");

            // And its soul: the one enchantment, which exists nowhere else — a
            // catalogue entry is a good drop, not an artifact, and a second
            // soul is what only the Light shape buys.
            if (def.Enchantments.Count != 1)
                throw new ContentException(def.Id, RuleMartialUniqueOneSoul, $"{def.Enchantments.Count} enchantments");
            if (!enchantments[def.Enchantments[0].Id].Unique)
                throw new ContentException(def.Id, RuleMartialUniqueOneSoul, $"'{def.Enchantments[0].Id}' is a catalogue entry, not a unique enchantment");
        }
    }

    /// <summary>
    /// The caster half of the unique rule (§1.5): a caster raises nothing, so
    /// its artifact is one of three shapes, each the one thing no ordinary
    /// drop can do — a unique enchantment beside its innate (hold one at all),
    /// its innate alone arriving at tier 3 (drop above tier 1), or two
    /// non-opposing catalogue entries at tier 1 (drop with more than one; the
    /// opposition is the relations' check, made on every weapon). Nothing else:
    /// not an innate deepened short of tier 3, not a soul beside a deepened
    /// innate, which would be two shapes at once, not a third entry. A staff's
    /// innate is fixed by its variant (§1.3), so a staff unique starts from its
    /// variant's; a wand's is rolled, so a wand unique fixes whichever element
    /// it names first. A unique enchantment's tier is pinned whatever the
    /// entry asks, so only a catalogue entry's tier is read.
    /// </summary>
    private static void ValidateCasterUniqueShape(WeaponDef def, WeaponDef variant, EnchantmentCatalogue enchantments)
    {
        var refs = def.Enchantments;
        if (variant.Enchantments.Count > 0 && (refs.Count == 0 || refs[0].Id != variant.Enchantments[0].Id))
            throw new ContentException(def.Id, RuleCasterUniqueShape, $"a {def.Class} unique starts from its variant's innate, '{variant.Enchantments[0].Id}'");

        bool IsUnique(EnchantmentRef r) => enchantments[r.Id].Unique;
        int uniques = refs.Count(IsUnique);
        bool ordinaryTiers = refs.All(r => IsUnique(r) || r.Tier == DropTier);
        bool uniqueSoul = refs.Count == 2 && uniques == 1 && ordinaryTiers;
        bool highTier = refs.Count == 1 && uniques == 0 && refs[0].Tier >= ArtifactTier;
        bool twoEntries = refs.Count == 2 && uniques == 0 && ordinaryTiers;
        if (!(uniqueSoul || highTier || twoEntries))
            throw new ContentException(def.Id, RuleCasterUniqueShape,
                $"carries {string.Join(", ", refs.Select(r => IsUnique(r) ? $"'{r.Id}' (unique)" : $"'{r.Id}' at tier {r.Tier}"))}");
    }

    /// <summary>Whether an entry heals its wielder: a status applier whose status the table says restores HP (a Regeneration innate), or a kind whose behaviour drinks (<see cref="EnchantmentBehaviours.HealsTheWielder"/>).</summary>
    private static bool IsHealingSource(EnchantmentDef def)
        => (def.Applies is { } status && StatusRules.Of(status).RestoresHp) || EnchantmentBehaviours.HealsTheWielder(def.Effect);

    /// <summary>Whether an entry's behaviour fires on healing above full — converts a surplus rather than making one (Overheal) — read off the behaviour tables, never off the kind's name.</summary>
    private static bool ConvertsSurplusHealing(EnchantmentDef def)
        => EnchantmentBehaviours.FiresOn(GameEvent.HealingAboveFull, def.Effect);

    /// <summary>
    /// Whether an entry cannot work alone: it names prerequisites in the
    /// relations' enchantment-keyed <c>requires</c> (a lingering element's
    /// element), or it converts a surplus of healing some other entry must
    /// make. The two dependencies §1.5 places, and the reason each sits only
    /// on the combo platform.
    /// </summary>
    private static bool DependsOnAnother(EnchantmentDef def, EnchantmentCatalogue enchantments)
        => enchantments.Prerequisites(def.Id).Count > 0 || ConvertsSurplusHealing(def);

    /// <summary>
    /// A forged spread as a set, unclamped — a weapon's, or a class row's
    /// baseline, which every weapon of the class is forged with: the forge is
    /// bounded by MaxForged above, never by the acquisition cap. A zero or
    /// negative count is refused here, naming <paramref name="entry"/>, before
    /// <see cref="ModifierSet.Of"/> would refuse it as an argument: invalid
    /// content aborts as content (§5.4). The shipped weapons carry none; a file
    /// edited can. Walked in enum order so the count reported is the same one
    /// every run; <paramref name="of"/> says what the counts are of, and the
    /// detail is the range rule's form, its top the tuned forge limit of the
    /// modifier, read off <paramref name="rules"/>
    /// (<c>baseline.CritMultiplier 0 is outside 1 to 3 - …</c>).
    /// </summary>
    private static ModifierSet Spread(string entry, IReadOnlyDictionary<ModifierType, int> forged, SpreadOf of, ModifierRules rules)
    {
        foreach (var (t, n) in forged.OrderBy(kv => kv.Key))
            if (n <= 0)
                throw new ContentException(entry, RuleForgedStackCount, of.Outside(t, n, rules));
        return ModifierSet.Of(forged.Select(kv => (kv.Key, kv.Value)).ToArray());
    }

    /// <summary>
    /// What a forged spread's counts are of, for the details about them: the
    /// words a relation's refusal puts before a count (<c>baseline Block x1 on a
    /// Melee weapon …</c>), the field a count's range fault names
    /// (<c>baseline.CritMultiplier</c>, <c>forged.Light</c>) and the row of
    /// <see cref="ContentBounds"/> it is held to.
    /// </summary>
    private sealed record SpreadOf(string Words, string Field, NumberBound Bound, IReadOnlySet<ModifierType>? Raised = null)
    {
        /// <summary>
        /// <paramref name="n"/> stacks of <paramref name="t"/> as a range fault,
        /// the kept rules' (<see cref="RuleForgedStackCount"/>, <see cref="RuleMaxForged"/>):
        /// the whole range, its top the tuned <see cref="ModifierRules.MaxForged"/> of the modifier.
        /// </summary>
        public string Outside(ModifierType t, int n, ModifierRules rules)
            => ContentBounds.Detail(Bound with { Max = rules.MaxForged(t) }, Raised?.Contains(t) == true ? $"raises.{t}" : $"{Field}{t}", n);
    }

    /// <summary>
    /// A class row's baseline (<see cref="ContentBounds.Baseline"/>): the entry is
    /// the class, and its row holds more than a spread, so a detail says the
    /// counts are the baseline's.
    /// </summary>
    private static readonly SpreadOf OfABaseline = new("baseline ", "baseline.", ContentBounds.Baseline);

    /// <summary>A weapon's forged spread as the reader composed it (<see cref="ContentBounds.Forged"/>): the entry is the weapon.</summary>
    private static readonly SpreadOf OfAWeapon = new("", "forged.", ContentBounds.Forged);

    /// <summary>
    /// <paramref name="def"/>'s spread as <see cref="OfAWeapon"/>, but a unique's
    /// count its <c>raises</c> set — one its variant does not hold at that count —
    /// named as the file writes it, <c>raises.Block</c>, the number the file
    /// wrote: <c>raises</c> is set over the variant, not summed onto it
    /// (<see cref="ContentBounds.Forged"/>).
    /// </summary>
    private static SpreadOf OfAWeaponIn(WeaponDef def, WeaponsData data)
    {
        if (!def.Unique || def.DerivedFrom is not { } from
            || data.Weapons.FirstOrDefault(w => !w.Unique && w.Id == from) is not { } variant)
            return OfAWeapon;
        var raised = def.Forged
            .Where(kv => !variant.Forged.TryGetValue(kv.Key, out int kept) || kept != kv.Value)
            .Select(kv => kv.Key)
            .ToHashSet();
        return OfAWeapon with { Raised = raised };
    }

    /// <summary>
    /// The forge's questions of <paramref name="spread"/>, forged on a weapon of
    /// <paramref name="kind"/>, reported against <paramref name="entry"/> in
    /// <see cref="ModifierType"/> order, so the one reported is the same every
    /// run: each modifier <see cref="ModifierRules.Allowed"/> beside the whole
    /// spread — the state every later roll deepens from, so the forge's own
    /// <c>Charges</c>, whose first stack <c>forgedOnly</c> reserves, is present
    /// when it is asked — and none past its <see cref="ModifierRules.MaxForged"/>.
    /// When Allowed refuses, the detail names the <c>restricted.json</c> relation
    /// that refused, read off <paramref name="restricted"/>, the relations in hand
    /// (<see cref="WhyNotAllowed"/>; PHASE5-122: "and the rule that made it so");
    /// a member outside the modifier table is no relation's doing, and says so.
    /// Past the limit, the detail is the range rule's form, naming the field and
    /// the whole range (<c>forged.CritWindow 4 is outside 1 to 3 - …</c>). Asked of
    /// every weapon's spread, and of every class row's baseline, which each weapon
    /// of the class is forged with: <paramref name="of"/> says what the counts are
    /// of — <see cref="OfAWeapon"/> or <see cref="OfABaseline"/>.
    /// </summary>
    private static void RequireForgeable(string entry, SpreadOf of, ModifierSet spread, WeaponKind kind, ModifierRules rules, RestrictedData restricted)
    {
        foreach (var (t, n) in spread.Entries)
        {
            if (!rules.Allowed(t, kind, spread))
                throw new ContentException(entry, RuleForgedNotAllowed, ModifierRules.NeverStacked.Contains(t)
                    ? $"{of.Words}{t} x{n}: not in the modifier table, so never a stack on any weapon"
                    : WithRelation($"{of.Words}{t} x{n} on a {kind} weapon beside {spread}", WhyNotAllowed(t, kind, spread, restricted)));
            if (n > rules.MaxForged(t))
                throw new ContentException(entry, RuleMaxForged, of.Outside(t, n, rules));
        }
    }

    /// <summary>A shape's dimension by the name the file writes it under (<see cref="WeaponsJson"/>).</summary>
    private static int DimensionOf(AreaShape shape, string name) => name switch
    {
        "radius" => shape.Radius,
        "length" => shape.Length,
        "width" => shape.Width,
        "angleDegrees" => shape.AngleDegrees,
        "targetReach" => shape.TargetReach,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No dimension of a shape is called that."),
    };

    /// <summary>
    /// Every id in <paramref name="ids"/> is a modifier or an enchantment. The
    /// detail says where the enchantment ids come from — the enchantment
    /// catalogue, which is code — and names the known id a typo is nearest,
    /// walked in ordinal order so the one suggested is the same every run. It is
    /// worded without an apostrophe: the screen that shows it draws the HUD's
    /// font, which has none and would leave a gap.
    /// </summary>
    private static void RequireKnown(IEnumerable<string> ids, IReadOnlySet<string> knownIds)
    {
        foreach (var id in ids)
            if (!knownIds.Contains(id))
            {
                string? near = TuningSchema.Nearest(id, knownIds.Order(StringComparer.Ordinal));
                throw new ContentException(id, RuleUnknownId,
                    "not a modifier, nor an enchantment in the enchantment catalogue" + (near is null ? "" : $" - did you mean {near}?"));
            }
    }

    /// <summary>Directed pairs from the groups, over string ids, for the checks above.</summary>
    private static Dictionary<string, HashSet<string>> Closure(IReadOnlyList<string[]> groups)
    {
        var closure = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var group in groups)
            foreach (var a in group)
                foreach (var b in group)
                {
                    if (a == b)
                        continue;
                    if (!closure.TryGetValue(a, out var excluded))
                        closure[a] = excluded = new HashSet<string>(StringComparer.Ordinal);
                    excluded.Add(b);
                }
        return closure;
    }

    /// <summary>Everything <paramref name="root"/> transitively requires; throws on a cycle, naming the id that closes it.</summary>
    private static HashSet<string> Prerequisites(string root, IReadOnlyDictionary<string, string[]> requires)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var onPath = new HashSet<string>(StringComparer.Ordinal) { root };
        Visit(root);
        return result;

        void Visit(string id)
        {
            if (!requires.TryGetValue(id, out var needs))
                return;
            foreach (var need in needs)
            {
                if (!onPath.Add(need))
                    throw new ContentException(need, RuleRequiresAcyclic);
                result.Add(need);
                Visit(need);
                onPath.Remove(need);
            }
        }
    }
}
