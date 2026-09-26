namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The §5.3 <c>tuning.json</c> scalars and tables: the record's shape — every
/// key's name, which is a property's name here, verbatim, and what each one
/// means — and never a value. The values are <c>tuning.json</c>'s: the file is
/// the master copy (the content rework, 1f), which the game carries built in
/// and reads first, strictly (<see cref="TuningJson.ReadBuiltIn"/>), so every
/// key has a number; the copy beside the executable is then read over it key by
/// key and per table entry (<see cref="TuningJson.Read"/>), so a partial file is
/// valid and the game runs with no file at all. Read once at startup, never
/// hot-reloaded. <see cref="TuningSchema"/> lists every key with its group and
/// its clamp range. Ratios are integer divisors whose names say what they
/// convert. Nothing the golden tests pin belongs here.
/// <para>
/// There is no tuning of nothing: the constructor is private, and the one
/// instance built here is <see cref="Blank"/> — every scalar 0 or false, every
/// table empty — which the built-in read lays the file over and which is never
/// played (<see cref="ContentValidator.ValidateTuning"/> refuses it). Any other
/// tuning is a read file, or one of those with keys changed (<c>with</c>). The
/// four tables keyed by an enum are complete by rule
/// (<see cref="ContentValidator.RuleTableNamesEveryMember"/>) and read directly.
/// </para>
/// </summary>
public sealed record Tuning
{
    private Tuning()
    {
    }

    /// <summary>
    /// Every scalar 0 or false and every table empty: what the built-in
    /// <c>tuning.json</c> is read over, so every number the game plays is the
    /// file's. Never played — <see cref="ContentValidator.ValidateTuning"/>
    /// refuses it — and never a fallback.
    /// </summary>
    internal static readonly Tuning Blank = new();

    // ---- Modifiers (§1.1) ----

    /// <summary>Acquired stacks a modifier may gain above its forged count: <c>cap(t) = forged(t) + AcquiredHeadroom</c>.</summary>
    public int AcquiredHeadroom { get; init; }

    /// <summary>
    /// What one stack is worth, per modifier — the §1.1 table. Complete by rule
    /// (<see cref="ContentValidator.RuleTableNamesEveryMember"/>): every member
    /// has a row, so no stack is ever worth nothing by omission. The values are
    /// <c>tuning.json</c>'s, each commented there beside its member.
    /// </summary>
    public IReadOnlyDictionary<ModifierType, int> PerStack { get; init; } = new Dictionary<ModifierType, int>();

    /// <summary>
    /// What a modifier grants before its first stack — CritMultiplier's base x2,
    /// Charges' first throw. Complete by rule
    /// (<see cref="ContentValidator.RuleTableNamesEveryMember"/>); the values are
    /// <c>tuning.json</c>'s.
    /// </summary>
    public IReadOnlyDictionary<ModifierType, int> Offset { get; init; } = new Dictionary<ModifierType, int>();

    /// <summary>
    /// The deepest a weapon may be forged in each modifier: a unique raises one
    /// modifier to x3 and nothing is forged past it, and the currency pair shares
    /// a lower limit because they share a shape and exclude each other. Complete
    /// by rule (<see cref="ContentValidator.RuleTableNamesEveryMember"/>); the
    /// values are <c>tuning.json</c>'s, raised to the loaded weapons' forgings
    /// where they fall short (<see cref="TuningFit"/>).
    /// </summary>
    public IReadOnlyDictionary<ModifierType, int> MaxForged { get; init; } = new Dictionary<ModifierType, int>();

    // ---- Statuses (§1.5) ----

    /// <summary>
    /// The constant effect of one level of each status — the §1.5 table's
    /// third column, the only dial a status has. Complete by rule
    /// (<see cref="ContentValidator.RuleTableNamesEveryMember"/>); the values are
    /// <c>tuning.json</c>'s, each commented there beside its status. No decay
    /// constants: one level a round, universally.
    /// </summary>
    public IReadOnlyDictionary<StatusEffectType, int> EffectPerLevel { get; init; } = new Dictionary<StatusEffectType, int>();

    /// <summary>Surplus healing points that convert into one Ward level.</summary>
    public int OverhealPerWard { get; init; }

    /// <summary>
    /// Expected damage-over-time points bought per point of trigger mana. This is
    /// the docs' <c>DotManaPerDamage = 1/3</c> (§1.5; settled.md fixes the value)
    /// stored as its integer reciprocal, because §5.3 makes every ratio an integer
    /// divisor named by its units: <c>triggerCost = max(1, expectedTotal / DotDamagePerMana)</c>.
    /// Loader note: the <c>tuning.json</c> key is <c>DotDamagePerMana</c>, this
    /// property's name; there is no <c>DotManaPerDamage</c> key, since 1/3 cannot be
    /// written as an integer under that name.
    /// </summary>
    public int DotDamagePerMana { get; init; }

    // ---- Appliers (§1.5) ----

    /// <summary>
    /// Per-enchantment percentage of the source number that becomes status
    /// levels, keyed by enchantment id: an id named here sets that entry's
    /// percentage, laid over its catalogue row. The four elements and Serrated
    /// (the docs' <c>BleedPercent</c>: the share of the weapon's own damage a
    /// hit leaves as Bleeding levels) have no percentage of their own at all —
    /// the catalogue leaves theirs to this table
    /// (<see cref="EnchantmentDefinitions.SetByTuning"/>), so the built-in
    /// <c>tuning.json</c> names each or the game does not start
    /// (<see cref="ContentValidator.RuleTuningOwnsTheNumber"/>). An id the
    /// catalogue does not have is dropped at load, with a notice. The values and
    /// what each is for are <c>tuning.json</c>'s.
    /// </summary>
    public IReadOnlyDictionary<string, int> ApplyPercent { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// The odds a non-caster drop arrives carrying an enchantment (§3.1): a
    /// staff's is fixed by its variant and a wand's is its element, so this is
    /// the martial roll and nothing else. Rolled as
    /// <c>NextInt(0, 999) &lt; DropEnchantChancePercent × 10</c>, an integer draw
    /// for the reason <see cref="FarmLadder.UniqueChancePermille"/> gives.
    /// <para>
    /// <strong>Provisional.</strong> §3.1 says "an uncommon roll on the loot
    /// stream" and names no number anywhere, so the number <c>tuning.json</c>
    /// ships is picked to stop the drop path blocking on it and is expected to
    /// move after play — one of the two figures in Phase 3 that is a guess
    /// rather than a doc's.
    /// </para>
    /// </summary>
    public int DropEnchantChancePercent { get; init; }

    /// <summary>
    /// The one entry <see cref="ArcanePotency"/> is the dial for. The overlay is
    /// keyed by this <em>id</em> and never by <see cref="EffectKind.BonusDamage"/>,
    /// which is the rule <see cref="ApplyPercent"/> already follows: a dial names
    /// an entry, while which entries carry a kind is content's to say. A second
    /// bonus-damage row — a greater Arcane, a soul that adds untyped damage —
    /// therefore keeps the magnitude its catalogue row states, exactly as an
    /// entry this file's percentage table does not name keeps its own.
    /// </summary>
    public const string ArcaneId = "arcane";

    /// <summary>
    /// Arcane's base potency: the untyped damage it adds to a hit, before its
    /// tier multiplies it through <see cref="Enchantment.LevelsFor"/> (§3.3). It
    /// is the potency of the row of <see cref="ArcaneId"/> and no other: that row
    /// states none of its own (<see cref="EnchantmentDefinitions.SetByTuning"/>),
    /// so this key is where Arcane's magnitude lives.
    /// <para>
    /// <strong>Provisional.</strong> §3.3's starting-set table prices Arcane's
    /// lock and trigger and gives no potency column at all, so this is the second
    /// of Phase 3's two guessed figures. It sits here rather than in the
    /// catalogue row so a playtest can turn it without a rebuild.
    /// </para>
    /// </summary>
    public int ArcanePotency { get; init; }

    // ---- Farming (§3.2) ----
    // The repeat-kill ladder's knobs. They are read through FarmLadder, which is
    // the only place the arithmetic over them lives.

    /// <summary>
    /// Turns a dummy at <c>DefeatCount</c> 0 takes to resurrect:
    /// <c>resurrectTurns(n) = max(ResurrectTurnsFloor, ResurrectTurnsBase − n)</c>
    /// (§3.2). Replaces the constant <c>DummyResurrectTurns</c>, which was a
    /// single number with no ladder under it. settled.md keeps the front-loaded slope
    /// as designed and records <c>10 − n/2</c> as the fallback if the front half
    /// of a farm ever feels too samey.
    /// </summary>
    public int ResurrectTurnsBase { get; init; }

    /// <summary>
    /// The shortest a revival ever gets (§3.2). A floor rather than a curve
    /// because "a two-turn revival is not a fight, it is a treadmill": at three
    /// there is still room to reposition, swing at something else, or leave.
    /// Coupled to <see cref="ResurrectTurnsBase"/> — lowering either makes deep
    /// farming untenable long before the statline does.
    /// <see cref="ContentValidator.ValidateTuning"/> refuses a value below 1, or
    /// a dummy could revive the turn it died.
    /// </summary>
    public int ResurrectTurnsFloor { get; init; }

    /// <summary>
    /// The chance, as a percentage, that one farm cycle buys the drop a stack
    /// (§3.2). Flat rather than decaying, which settled.md accepts as designed:
    /// what ends a farm is the per-modifier <c>forged + 5</c> ceiling, so a
    /// falling rate would only make the same ending slower to reach.
    /// </summary>
    public int DefeatStackChance { get; init; }

    /// <summary>
    /// What a clean kill — a killing blow dealing at least the target's max HP —
    /// advances <c>DefeatCount</c> by (settled.md). It needs no counterweight
    /// because <c>DefeatCount</c> is already both reward and threat: it buys two
    /// cycles of drop quality <em>and</em> hands the dummy two cycles of statline.
    /// <see cref="ContentValidator.ValidateTuning"/> refuses a value below 1:
    /// <see cref="FarmLadder.DefeatAdvance"/> returns this figure verbatim, so a 0
    /// would leave a clean kill advancing nothing — strictly worse than the
    /// ordinary kill it is supposed to be worth double.
    /// </summary>
    public int CleanKillBonus { get; init; }

    /// <summary>
    /// The percentage of its own current damage or max HP a dummy gains per
    /// revival (§3.2; settled.md overrides the flat +5). Proportional, so a
    /// dagger dummy and an axe dummy grow at the same relative rate, and
    /// compounding, so the curve accelerates the longer a farm runs.
    /// <para>
    /// <strong>Provisional by the doc's own admission</strong> — "still a
    /// placeholder pending play"; what carries over from the flat version is the
    /// shape, not the number. At 15 an 18-damage axe dummy reaches 37 damage in
    /// five Damage rolls: 18, 21, 24, 28, 32, 37.
    /// </para>
    /// </summary>
    public int ReviveStepPercent { get; init; }

    // ---- Uniques (§3.2) ----
    // The logistic's three constants, all knobs: the ceiling sets how much
    // grinding can ever be worth, the midpoint moves the hot zone, and K
    // controls how sharply it arrives.

    /// <summary>
    /// The asymptote the unique chance climbs toward, as a percentage (§3.2). It
    /// is what keeps the deep farm a gamble rather than a long safe purchase:
    /// "no amount of grinding *guarantees* a unique". The asymptote does the work
    /// a hard cap used to.
    /// </summary>
    public int UniqueChanceCeilingPercent { get; init; }

    /// <summary>
    /// The <c>DefeatCount</c> the curve is steepest at, where it passes half the
    /// ceiling (§3.2). settled.md keeps it at 20 pending real numbers from
    /// <see cref="ReviveStepPercent"/> play, and fixes the direction of the
    /// coupling: if a twenty-times-revived dummy proves unsurvivable or trivial
    /// the midpoint moves to match, never the other way round.
    /// </summary>
    public int UniqueChanceMidpoint { get; init; }

    /// <summary>
    /// How sharply the curve arrives, as a percentage: the doc's
    /// <c>K = 0.26</c>, "tuned so DefeatCount 5 lands on ~1%". Percent-named
    /// because §5.3 makes every rate an integer with its units in its name rather
    /// than a bare float.
    /// </summary>
    public int UniqueChanceKPercent { get; init; }

    // ---- Economy (§1.2, §1.3) ----

    /// <summary>
    /// Banked movement (logic units, 32 per tile) that buys one mana at end of
    /// turn: <c>manaRegained = unspentMovement / MovementUnitsPerMana</c>, an
    /// integer divisor rather than a fractional rate, so the file reads correctly
    /// and nothing drifts ("1 per point" would be 32 mana a tile). Derived from
    /// one calibration target rather than picked (§1.3), so it re-derives when
    /// anything it depends on moves: a caster at Resonant x6 carrying a tier-1
    /// enchantment sustains one cast a round while standing still:
    /// <c>(movementBudget - castCost) / (castMana + triggerMana, at x6)
    /// = (160 - 40) / (8 + 3) = 120 / 11 ~ 10.9 -> 10</c>.
    /// A fully banked turn is 16 mana; a turn spent on one cast banks 120 and
    /// pays 12, against the 11 spent: a hair above break-even, forever.
    /// Nothing below x6 sustains, and that cliff is deliberate.
    /// </summary>
    public int MovementUnitsPerMana { get; init; }

    /// <summary>
    /// How far a party member moves in a turn, in logic units (32 per tile):
    /// the 160 every movement cost is priced against (§1.2, §1.3) — a swing, a
    /// cast, a swap — the whole a turn start refills before Mire cuts it
    /// (<see cref="PartyMemberState.StartTurn"/>), and the half of it a turn may
    /// bank. It is also what converts the party's costs into an enemy's: an
    /// enemy pays <c>EnemyMove / MovementBudget</c> of a weapon's cost, so a
    /// longer party budget makes every enemy swing cheaper as well. settled.md's
    /// Phase 2 pass names it: the caster's 160 "is <c>MovementBudget</c>" beside
    /// <see cref="StartingPool"/>. Its number is <c>tuning.json</c>'s alone: no
    /// constant in the code holds it.
    /// <para>
    /// <see cref="MovementUnitsPerMana"/> is derived from it and does
    /// <strong>not</strong> follow it: a budget turned in <c>tuning.json</c>
    /// leaves the divisor where it was, so a longer budget also banks more mana a
    /// turn, and whoever turns one re-derives the other by hand (settled.md: the
    /// divisor "re-derives when the movement budget or cast costs move").
    /// </para>
    /// </summary>
    public int MovementBudget { get; init; }

    /// <summary>Movement a weapon swap costs once a live enemy has been seen this turn; free otherwise.</summary>
    public int WeaponSwapCost { get; init; }

    /// <summary>Tiles a Longshot shot travels before it starts paying out.</summary>
    public int LongshotFreeTiles { get; init; }

    /// <summary>Whether area casts hit allies. Off until the kiting AI and the wand placement scorer exist.</summary>
    public bool FriendlyFireEnabled { get; init; }

    /// <summary>Percentage of an area cast's damage an ally takes when friendly fire is on.</summary>
    public int FriendlyFireAllyPercent { get; init; }

    // ---- Progression (§2.1, §2.2) ----
    // StartingPool is §5.3's own key, in its Economy group. WeaponXpPerLevel is
    // named here, because §5.3 names the weapon ladder's bar nowhere at all, and
    // tuning.json keeps it under a Progression heading of its own. They are the
    // two numbers a playtest would actually turn; the shape of a level's effect
    // (Progression.DamagePerLevels and the rest) stays code.

    /// <summary>
    /// The bar every point-growing pool opens on (§2.1): max HP, max mana and —
    /// from Phase 6a — a weapon's wear capacity alike, so the 30 is never a bare
    /// literal in three places. A point costs <c>currentMax / governingStat</c>
    /// from here, which is why a CON 1 wizard and a CON 4 fighter start level and
    /// diverge rather than starting apart. Below 1 there is no step to take, and
    /// <see cref="ContentValidator.ValidateTuning"/> refuses it at load.
    /// <para>
    /// <strong>A caster's pool opens here too, on 30 and not on 160.</strong>
    /// settled.md had both figures live; the 160 in §1.3's derivations is the
    /// <em>movement</em> budget — <c>(160 - 40) / (8 + 3)</c> divides a staff's
    /// 40-unit movement cost out of <see cref="MovementBudget"/>, which is what
    /// <see cref="MovementUnitsPerMana"/> above is derived from —
    /// so there is no second starting-mana key and this one number is every
    /// pool's bar. Recorded under settled.md's Phase 2 implementation pass, with
    /// the consequence it accepts: a fresh caster gets one cast from a full
    /// pool. The pool an actor with no progression carries is
    /// <see cref="GameConstants.MaxMana"/>, and is a different number.
    /// </para>
    /// </summary>
    public int StartingPool { get; init; }

    /// <summary>
    /// The weapon ladder's bar per level (§2.2): the doc's
    /// <c>xpToNext(L) = 100 * L / governingStat</c> is
    /// <c>XpToNext(WeaponXpPerLevel * L, stat)</c>, so the ladder and the pools
    /// run on one function. Marked "tune later" where it is written and left
    /// unnamed there; this is the name.
    /// </summary>
    public int WeaponXpPerLevel { get; init; }

    // ---- Dungeon (§4.3, §4.4) ----
    // §5.3's Dungeon group, plus the stairs' gathering radius, which §5.3 does
    // not list and which lands in the same group. Floors here are floor
    // NUMBERS, 1-based, the way prose and the HUD count them; a floor's seed
    // index is its number less one. None of these decides dungeon layout —
    // the stream salts that do are compiled constants, the golden set's
    // neighbours, and never keys here.

    /// <summary>
    /// The shallowest floor a dungeon's boss is rolled onto (§4.3: "between 5
    /// and 10 on entry"). A dungeon that pins its own range in content
    /// (<see cref="DungeonDef.BossFloorMin"/>) ignores this, as the tutorial's
    /// floor 2 does; every other dungeon reads it at entry.
    /// </summary>
    public int BossFloorMin { get; init; }

    /// <summary>The deepest floor a dungeon's boss is rolled onto; the roll's one draw spans <c>BossFloorMax − BossFloorMin</c>.</summary>
    public int BossFloorMax { get; init; }

    /// <summary>
    /// The floors a boss's drop pays nothing for: its guaranteed stacks are
    /// <c>max(0, BossFloor − BossStackOffset)</c> — 1 at floor 5, 6 at floor 10
    /// (§4.3's table). The tutorial's floor 2 pays 0 of these; the one stack the
    /// doc prices it at is the forged graft itself. The deepest boss must stay
    /// short of what a well-farmed two-modifier weapon banks (twice
    /// <see cref="AcquiredHeadroom"/>, ten): a load refuses an offset, a depth or
    /// a headroom that would bring it level
    /// (<see cref="ContentValidator.RuleBossPaysLessThanTheFarm"/>).
    /// </summary>
    public int BossStackOffset { get; init; }

    /// <summary>Runs a clear locks its dungeon for, per floor its boss was beaten on: the lock is <c>BossFloor × CooldownRunsPerFloor</c> (§4.3), counted on the campaign's run counter, and multiplied out in one place, <see cref="DungeonState.LockRuns"/>.</summary>
    public int CooldownRunsPerFloor { get; init; }

    /// <summary>
    /// The floor NUMBER a failed run must have reached to earn the mercy tick
    /// (§6.3: "failing at floor 3 or deeper"). A number, not an index: the
    /// tutorial's two floors never reach it, with no exception written.
    /// </summary>
    public int MercyFloor { get; init; }

    /// <summary>
    /// The percentage of this run's gain in each XP pool a wipe claws back
    /// (§4.4). 100 erases the run's progress, 0 makes death a pure loot
    /// forfeiture; it ships as data and never as a hardcoded 100%.
    /// <para>
    /// <strong>Provisional.</strong> settled.md fixes the mechanism and leaves the
    /// number open ("nobody can pick this number on paper"), so 50 is picked
    /// here to stop the run's end blocking on it and is expected to move after
    /// play.
    /// </para>
    /// </summary>
    public int DeathXpLossPercent { get; init; }

    /// <summary>
    /// How near the stairs, in whole tiles, every living member must stand for
    /// the party to use them. Tiles, as the name says: whoever measures it in
    /// logic units multiplies by <see cref="GameConstants.LogicUnitsPerTile"/>.
    /// Three admits a marching line of three ranks behind whoever is on the stair.
    /// </summary>
    public int StairGatherTiles { get; init; }

    // ---- Enchanter (Phase 6a; the user's ruling of 2026-09-25) ----
    // What a deposit costs (§6.1, §6.2): how a weapon's wear capacity grows, where
    // its enchantment slots open, and how much wear attaching each enchantment
    // needs. The user ruled the numbers and keeps each in tuning.json, so they are
    // keys ahead of their reader: no rule of play reads one until Phase 6a builds
    // the enchanter, and until then a save's hash leaves them out
    // (TuningKey.Played). A weapon's capacity opens on StartingPool, the one bar
    // every pool opens on, which stays one key.

    /// <summary>
    /// The capacity a full bar's worth of cashed wear adds to a weapon's
    /// <c>WearCapacity</c> (§6.1): the bar is the capacity at the time, and the
    /// progress carries — a deposit short of a full bar still counts toward the
    /// next point, and what one cashes past a bar starts the next. The growth is
    /// the same whichever service the deposit bought (§6.4). 0 freezes capacity.
    /// No rule reads it yet: Phase 6a's enchanter will.
    /// </summary>
    public int WearCapacityPerBar { get; init; }

    /// <summary>
    /// The capacity a weapon's first enchantment slot opens at (§6.2): slot n opens
    /// at <c>FirstSlotCapacity + SlotCapacityStep × (n − 1)</c>, up to
    /// <see cref="EnchantmentSlots"/>. What a weapon arrives with fills its slots
    /// from the first. No rule reads it yet: Phase 6a's enchanter will.
    /// </summary>
    public int FirstSlotCapacity { get; init; }

    /// <summary>The capacity between one enchantment slot and the next (<see cref="FirstSlotCapacity"/>); 0 opens every slot at once. No rule reads it yet: Phase 6a's enchanter will.</summary>
    public int SlotCapacityStep { get; init; }

    /// <summary>
    /// The most enchantments the enchanter lets a weapon hold: the docs' five
    /// (§6.2), the last slot. Never fewer than a weapon arrives carrying — the
    /// key's clamp starts at <see cref="ContentValidator.MostArrivingEnchantments"/>,
    /// so no <c>tuning.json</c> can hold a weapon to fewer slots than its own
    /// souls fill. Not the five of §1.5's Light shape, which decides what a unique
    /// arrives with and stays a rule constant: the two agree by the docs and mean
    /// different things. No rule reads it yet: Phase 6a's enchanter will.
    /// </summary>
    public int EnchantmentSlots { get; init; }

    /// <summary>
    /// The least wear a deposit must cash to attach a weapon's first enchantment
    /// (§6.2): attaching enchantment #n needs a free slot and a deposit of at least
    /// <c>FirstEnchantmentWear + EnchantmentWearStep × (n − 1)</c>. A deposit still
    /// spends the whole pool (§6.1), and capacity above the price is breathing
    /// room. No rule reads it yet: Phase 6a's enchanter will.
    /// </summary>
    public int FirstEnchantmentWear { get; init; }

    /// <summary>How much more wear each later enchantment needs than the one before (<see cref="FirstEnchantmentWear"/>); 0 prices every one as the first. No rule reads it yet: Phase 6a's enchanter will.</summary>
    public int EnchantmentWearStep { get; init; }

    // ---- Inventory (§4.4) ----

    /// <summary>
    /// The party's whole carry, in weapon slots: 6 a member, 24 party-wide
    /// (§4.4, settled.md), shared evenly by <see cref="GameConstants.PartySize"/>
    /// members into <see cref="PartyMemberState.InventorySlots"/>. Loot and
    /// weapons share it — there is no haul bag — and slot 0 of each member's
    /// share is the weapon in hand. "Exact number is a tuning target; the
    /// structure is the decision."
    /// <para>
    /// A multiple of the party's size, at most 36:
    /// <see cref="ContentValidator.ValidateTuning"/> refuses a carry that does not
    /// divide evenly, and one deeper than
    /// <see cref="GameConstants.MaxInventorySlots"/> (nine) a member, because a
    /// slot is equipped by the number key printed beside it and the number row
    /// stops at 9.
    /// </para>
    /// </summary>
    public int PartyCarrySlots { get; init; }

    // ---- Saves (the user's ruling of 2026-09-25 on the save model) ----

    /// <summary>
    /// How many save slots there are, each holding one campaign that saves only
    /// there: the save folder's <c>slot-1</c> to <c>slot-N</c>. The user ruled the
    /// count a key — "the number of saves should live on the tuning.json so that we
    /// don't need to fix the code every time I change my mind". At least one, so a
    /// campaign always has a slot to begin in, and at most nine, a digit picking
    /// each. A count lowered below a slot that holds a save deletes nothing: the
    /// slot is kept, listed, and goes on saving the campaign played from it; NEW
    /// CAMPAIGN never offers it. The launcher reads it, never a rule of play, so a
    /// save's hash leaves it out (<see cref="TuningKey.Played"/>).
    /// </summary>
    public int SaveSlots { get; init; }
}
