namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One number's range in a content file (the user's ruling of 2026-09-25, R3:
/// "since we are storing in raw json we should probably not trust the fields"):
/// the least and the most the game plays it at, and why. A number outside it
/// stops the game (<see cref="ContentValidator.RuleNumberInRange"/>), naming the
/// file, the entry, the field, the number and the range — never clamped, as
/// <c>tuning.json</c>'s numbers are, since content is validated (§5.4).
/// </summary>
/// <param name="File">The file the number is written in: <c>weapons.json</c> or <c>dungeons.json</c> (<c>restricted.json</c> holds no number).</param>
/// <param name="Field">
/// Where in the file, as a path with each place a row or a table member takes
/// written as a letter — X a class, i a row, j an entry, M a modifier:
/// <c>classes.X.range</c>, <c>variants[i].shape.radius</c>, <c>boss.innate.M</c>.
/// The record field it lands in is the path's last name, camel-cased, but for
/// <c>forged.M</c>, a weapon's spread as the reader composes it. A refusal
/// names the field as it stands in the entry instead (<c>shape.radius</c>).
/// </param>
/// <param name="Min">The least the game plays.</param>
/// <param name="Max">The most the game plays: a <c>tuning.json</c> clamp's ceiling or a rule, never a number of a master copy.</param>
/// <param name="Why">
/// Why the range is what it is, in the words a refusal shows after the range —
/// on the screen that stops the game, in the HUD's font: no apostrophe, quote,
/// brace or semicolon.
/// </param>
/// <param name="KeptBy">
/// The rule that bounded the field before the ruling, and keeps the end it owns:
/// it is asked first, and the range rule after it, so it refuses what it always
/// refused, its detail now in the range rule's form (<see cref="ContentBounds.Detail"/>).
/// It owns the floor of every row it is set on; a forged count's top is its
/// forge limit, <see cref="ContentValidator.RuleMaxForged"/>'s at the tuned
/// <c>MaxForged</c> of the modifier, so that row's range rule is never asked.
/// Null where the range rule owns both ends.
/// </param>
internal sealed record NumberBound(string File, string Field, int Min, int Max, string Why, string? KeptBy = null);

/// <summary>
/// Every number of a content file, and its range (R3(a), settled.md): the rows
/// <see cref="ContentValidator.RequireInRange"/> asks, each with its reason, and
/// the rows an older rule keeps (<see cref="NumberBound.KeptBy"/>). A number
/// added to a content record gets its row here in the same change, or the suite
/// fails (<c>ContentBoundsTests</c>).
/// <para>
/// <strong>No bound is a master copy's value.</strong> Every ceiling is read
/// once, off a <see cref="TuningSchema"/> clamp or a rule of the game: the
/// deepest any forge goes (<see cref="DeepestForge"/>), the deepest a stack
/// grows (<see cref="DeepestStack"/>), the deepest floor a tuning names
/// (<see cref="DeepestBossFloor"/>), the most a magnitude is
/// (<see cref="Magnitude"/>), the farthest a distance runs
/// (<see cref="Distance"/>) and the largest bar a pool opens on
/// (<see cref="Bar"/>); and the tiers a weapon arrives at
/// (<see cref="ContentValidator.DropTier"/>, <see cref="ContentValidator.ArtifactTier"/>),
/// the least movement a cost is charged (<see cref="Progression.MinimumMovementCost"/>)
/// and a full turn's degrees. So a shipped
/// number moves without touching a bound, and a clamp moved in the schema moves
/// the bound with it. The save reads its ceiling on a boss floor here too
/// (<see cref="DeepestBossFloor"/>, <c>SaveMapper</c>), so the content and the
/// save never disagree about how deep a boss goes.
/// </para>
/// <para>
/// <strong>What is not here, and why.</strong> <c>restricted.json</c> holds no
/// number: its relations are ids and groups of ids. A caster's <c>dropOrder</c>
/// is ids, bound in length and membership by
/// <see cref="ContentValidator.RuleCasterVariantPool"/>. A dimension a shape's
/// kind does not measure is the reader's: 0, or refused as the file's shape
/// (<see cref="ContentValidator.RuleFileShape"/>). And the enchantment catalogue
/// is code, reviewed and tested like every rule, with its own load checks and
/// its dialled numbers <c>tuning.json</c>'s, clamped.
/// </para>
/// </summary>
internal static class ContentBounds
{
    // ── The ceilings, each read once off the schema or a rule ────────────────

    /// <summary>The deepest any forge goes: <c>MaxForged</c>'s clamp ceiling. No weapon is forged past its modifier's limit, and no limit past this.</summary>
    internal static int DeepestForge { get; } = Table(nameof(Tuning.MaxForged)).Max;

    /// <summary>The deepest a stack can grow: the deepest forge plus <c>AcquiredHeadroom</c>'s clamp ceiling, the most acquisition adds above a forging.</summary>
    internal static int DeepestStack { get; } = DeepestForge + Int(nameof(Tuning.AcquiredHeadroom)).Max;

    /// <summary>The deepest floor a tuning names: <c>BossFloorMax</c>'s clamp ceiling — and the deepest a dungeon may pin its boss on.</summary>
    internal static int DeepestBossFloor { get; } = Int(nameof(Tuning.BossFloorMax)).Max;

    /// <summary>The most a magnitude is: the ceiling <c>tuning.json</c>'s magnitudes clamp at, <c>PerStack</c>'s.</summary>
    internal static int Magnitude { get; } = Table(nameof(Tuning.PerStack)).Max;

    /// <summary>The farthest a distance runs, in logic units: <c>MovementBudget</c>'s clamp ceiling, the farthest the tuning measures — past any map.</summary>
    internal static int Distance { get; } = Int(nameof(Tuning.MovementBudget)).Max;

    /// <summary>The largest bar a pool opens on: <c>StartingPool</c>'s clamp ceiling.</summary>
    internal static int Bar { get; } = Int(nameof(Tuning.StartingPool)).Max;

    /// <summary>The degrees of a full turn: a cone no wider than all around.</summary>
    internal const int FullTurn = 360;

    // ── weapons.json: the class rows ─────────────────────────────────────────

    private const string ReachWhy = "a reach of 0 hits nothing, and none is longer than the farthest tuning.json measures (MovementBudget at its most)";
    private const string DamageWhy = "a staff hits for 0, its cast being its innate, and no hit is bigger than a tuning.json magnitude can be (PerStack at its most)";
    private const string CostWhy = "movement is charged a unit at least, so a smaller cost is never charged, and none is more than the farthest tuning.json measures (MovementBudget at its most)";
    private const string ManaCostWhy = "a martial class casts for 0, below 0 a cast would open on an empty pool, and none costs more than a tuning.json magnitude can be (PerStack at its most)";
    private const string ForgedWhy = "a forged stack is one at least, and none is deeper than the forge limit of its modifier (MaxForged)";

    /// <summary>A class row's reach (§1.2–§1.4).</summary>
    internal static NumberBound ClassRange { get; } = new(WeaponsJson.FileName, "classes.X.range", 1, Distance, ReachWhy);

    /// <summary>A class row's damage: a staff's is 0, its cast being its innate (§1.3).</summary>
    internal static NumberBound ClassDamage { get; } = new(WeaponsJson.FileName, "classes.X.damage", 0, Magnitude, DamageWhy);

    /// <summary>A class row's cost: movement is charged <see cref="Progression.MinimumMovementCost"/> at least (<see cref="Progression.MovementCost"/> floors a smaller one), so the file would name a cost never charged, which the HUD shows.</summary>
    internal static NumberBound ClassCost { get; } = new(WeaponsJson.FileName, "classes.X.cost", Progression.MinimumMovementCost, Distance, CostWhy);

    /// <summary>A class row's mana a cast: a martial class's is 0, and below 0 the cast gate would open on an empty pool.</summary>
    internal static NumberBound ClassManaCost { get; } = new(WeaponsJson.FileName, "classes.X.manaCost", 0, Magnitude, ManaCostWhy);

    /// <summary>
    /// A class row's baseline stack: kept by the forge's own two rules, asked of
    /// it as of a weapon's spread — <see cref="ContentValidator.RuleForgedStackCount"/>
    /// the floor, <see cref="ContentValidator.RuleMaxForged"/> the top at the tuned
    /// limit of the modifier, which is never above <see cref="DeepestForge"/>.
    /// </summary>
    internal static NumberBound Baseline { get; } = new(WeaponsJson.FileName, "classes.X.baseline.M", 1, DeepestForge, ForgedWhy,
        KeptBy: ContentValidator.RuleForgedStackCount);

    // ── weapons.json: the weapons ────────────────────────────────────────────

    /// <summary>A variant's own reach (REWORK DECISION 1), played as its class's is: asked of the composed row, so a class row's own fault is named first, on the class.</summary>
    internal static NumberBound VariantRange { get; } = ClassRange with { Field = "variants[i].range" };

    /// <summary>A variant's own damage.</summary>
    internal static NumberBound VariantDamage { get; } = ClassDamage with { Field = "variants[i].damage" };

    /// <summary>A variant's own cost.</summary>
    internal static NumberBound VariantCost { get; } = ClassCost with { Field = "variants[i].cost" };

    /// <summary>A variant's own mana a cast: the Staff of Renewal's and the Staff of Mire's.</summary>
    internal static NumberBound VariantManaCost { get; } = ClassManaCost with { Field = "variants[i].manaCost" };

    /// <summary>
    /// What a variant adds to its class's baseline, asked by the reader as it is
    /// read, before it is summed (<see cref="WeaponsJson"/>): summed, a raw -1
    /// would read as a composed stack one short, a 0 on a baseline modifier would
    /// add nothing in silence, and the largest whole number would wrap. What the
    /// sum composes to stays the forge's rules' (<see cref="Forged"/>).
    /// </summary>
    internal static NumberBound Adds { get; } = new(WeaponsJson.FileName, "variants[i].adds.M", 1, DeepestForge,
        "an add adds a stack at least, and none is deeper than any forge goes (MaxForged at its most)");

    /// <summary>
    /// A weapon's forged stack, as the reader composes it — a variant's class
    /// baseline plus its adds, a unique's <c>raises</c> set over its variant — kept
    /// as a baseline's is (<see cref="Baseline"/>). A unique's <c>raises</c> is set,
    /// not summed, so its raw number is the composed one, refused here.
    /// </summary>
    internal static NumberBound Forged { get; } = new(WeaponsJson.FileName, "forged.M", 1, DeepestForge, ForgedWhy,
        KeptBy: ContentValidator.RuleForgedStackCount);

    /// <summary>
    /// The tier an enchantment arrives at: a drop's is
    /// <see cref="ContentValidator.DropTier"/>, and only an artifact's is higher,
    /// <see cref="ContentValidator.ArtifactTier"/> (§1.5, §3.3). The file names what
    /// a weapon arrives with; tier grows in play from there, uncapped (§3.3: "the
    /// pool is the cap"), and a saved weapon's tier is the save's to bound. Below 1
    /// plays as 1 (<see cref="WeaponCatalogue.Instantiate"/>), a tier the game never
    /// plays. A unique soul's is held to the same range, and plays at its pin
    /// (<see cref="Enchantment.UniqueTier"/>) wherever it is read: the suite loads a
    /// soul written at 3 and holds the pin on it
    /// (<c>UniqueContentTests.UniqueSouls_AreNeverRolled_AndTierPinned</c>), so no
    /// narrower range is asked of a soul.
    /// </summary>
    internal static NumberBound Tier { get; } = new(WeaponsJson.FileName, "enchantments[j].tier", ContentValidator.DropTier, ContentValidator.ArtifactTier,
        $"a weapon arrives at tier {ContentValidator.DropTier}, and an artifact at {ContentValidator.ArtifactTier} at most - tier grows in play from there, never in the file");

    private const string DimensionWhy = "a measured dimension of 0 covers nothing, and none is longer than the farthest tuning.json measures (MovementBudget at its most)";

    /// <summary>A Blast's or a Nova's radius, where its kind measures one (§1.4).</summary>
    internal static NumberBound Radius { get; } = new(WeaponsJson.FileName, "variants[i].shape.radius", 1, Distance, DimensionWhy);

    /// <summary>A Cone's or a Beam's length.</summary>
    internal static NumberBound Length { get; } = new(WeaponsJson.FileName, "variants[i].shape.length", 1, Distance, DimensionWhy);

    /// <summary>A Beam's width.</summary>
    internal static NumberBound Width { get; } = new(WeaponsJson.FileName, "variants[i].shape.width", 1, Distance, DimensionWhy);

    /// <summary>A Cone's angle, in degrees: a cone of nothing, or one wider than all around, is no cone.</summary>
    internal static NumberBound AngleDegrees { get; } = new(WeaponsJson.FileName, "variants[i].shape.angleDegrees", 1, FullTurn,
        "a cone of 0 degrees covers nothing, and none is wider than a full turn");

    /// <summary>How far a Blast's point may be aimed from its caster.</summary>
    internal static NumberBound TargetReach { get; } = new(WeaponsJson.FileName, "variants[i].shape.targetReach", 1, Distance, DimensionWhy);

    // ── dungeons.json ────────────────────────────────────────────────────────

    private const string FloorWhy = "a floor number starts at 1, and no boss is deeper than the deepest floor tuning.json can name (BossFloorMax at its most)";

    /// <summary>
    /// A pinned boss floor, the shallower end: <see cref="ContentValidator.RuleBossFloorRange"/>
    /// keeps the 1. The top is the deepest floor a tuning names — and the deepest
    /// <see cref="TuningFit"/> can always serve: at <c>AcquiredHeadroom</c>'s clamp
    /// floor a farm banks 2, so floor 99 needs an offset of 98, inside
    /// <c>BossStackOffset</c>'s clamp (the suite asserts it off the schema).
    /// Asked once the rule that keeps the 1 has asked it of both ends, and before
    /// that rule asks whether the range inverts or anything else reads the floor,
    /// so a floor out of range is named as one, with the whole range, whatever the
    /// tuning or the other end — never as a range that inverts.
    /// </summary>
    internal static NumberBound BossFloorMin { get; } = new(DungeonsJson.FileName, "bossFloorMin", 1, DeepestBossFloor, FloorWhy,
        KeptBy: ContentValidator.RuleBossFloorRange);

    /// <summary>A pinned boss floor, the deeper end: kept as the shallower one is, its 1 the boss floor range's rule's and its top the range rule's, both asked before the range is asked whether it inverts.</summary>
    internal static NumberBound BossFloorMax { get; } = BossFloorMin with { Field = "bossFloorMax" };

    /// <summary>A boss's hit points: <see cref="ContentValidator.RuleBossStatline"/> keeps the 1, and no pool opens on a bigger bar.</summary>
    internal static NumberBound BossMaxHp { get; } = new(DungeonsJson.FileName, "boss.maxHp", 1, Bar,
        "a boss has a hit point at least, and none more than the largest bar a pool opens on (StartingPool at its most)",
        KeptBy: ContentValidator.RuleBossStatline);

    /// <summary>A boss's movement a turn, in logic units: <see cref="ContentValidator.RuleBossStatline"/> keeps the 1.</summary>
    internal static NumberBound BossMovementBudget { get; } = new(DungeonsJson.FileName, "boss.movementBudget", 1, Distance,
        "a boss moves a unit at least, and no farther than tuning.json measures (MovementBudget at its most)",
        KeptBy: ContentValidator.RuleBossStatline);

    /// <summary>
    /// A boss's innate stack: <see cref="ContentValidator.RuleBossInnateStackable"/>
    /// keeps the 1. Innate stacks are no forge's — "a golem deeper than any forge
    /// is a golem" (A21) — but none is deeper than a stack can ever grow
    /// (<see cref="DeepestStack"/>).
    /// </summary>
    internal static NumberBound BossInnate { get; } = new(DungeonsJson.FileName, "boss.innate.M", 1, DeepestStack,
        "an innate stack is one at least, and none is deeper than a stack can grow (MaxForged and AcquiredHeadroom at their most)",
        KeptBy: ContentValidator.RuleBossInnateStackable);

    // ── All of them ──────────────────────────────────────────────────────────

    /// <summary>Every row, in the order the files hold their numbers: <c>weapons.json</c>'s classes, variants, enchantments and shapes, then <c>dungeons.json</c>'s floors and boss.</summary>
    internal static IReadOnlyList<NumberBound> All { get; } =
    [
        ClassRange, ClassDamage, ClassCost, ClassManaCost, Baseline,
        VariantRange, VariantDamage, VariantCost, VariantManaCost, Adds, Forged,
        Tier,
        Radius, Length, Width, AngleDegrees, TargetReach,
        BossFloorMin, BossFloorMax, BossMaxHp, BossMovementBudget, BossInnate,
    ];

    /// <summary>The row a shape's dimension is held to, by the name the file writes it under (<c>radius</c>, <c>angleDegrees</c>).</summary>
    internal static NumberBound Dimension(string name) => name switch
    {
        "radius" => Radius,
        "length" => Length,
        "width" => Width,
        "angleDegrees" => AngleDegrees,
        "targetReach" => TargetReach,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No dimension of a shape is called that."),
    };

    /// <summary>
    /// The one form every range fault's detail takes, the kept rules' included:
    /// <c>{field} {value} is outside {min} to {max} - {why}</c> —
    /// <c>cost 0 is outside 1 to 10000 - movement is charged a unit at least, …</c>.
    /// </summary>
    internal static string Detail(NumberBound bound, string field, int value)
    {
        ArgumentNullException.ThrowIfNull(bound);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return $"{field} {Outside(bound, value)}";
    }

    /// <summary>The same with no field before it — <c>0 is outside 1 to 3 - …</c> — where the reader names the path itself (<see cref="JsonShape.Fail"/>).</summary>
    internal static string Outside(NumberBound bound, int value)
    {
        ArgumentNullException.ThrowIfNull(bound);
        return $"{value} is outside {bound.Min} to {bound.Max} - {bound.Why}";
    }

    private static IntKey Int(string name)
        => TuningSchema.Find(name) as IntKey ?? throw new InvalidOperationException($"The tuning schema has no whole-number key {name}.");

    private static TableKey Table(string name)
        => TuningSchema.Find(name) as TableKey ?? throw new InvalidOperationException($"The tuning schema has no table key {name}.");
}
