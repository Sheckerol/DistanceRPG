namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What a dummy was carrying, as the extraction hands it over (§3.1, §3.5): the
/// weapon itself, the quality it was collected at, and whether the unique roll
/// came in. The weapon is the whole of the drop — a fresh
/// <see cref="Weapon"/> instance, never a row shared out of the catalogue, with
/// its own <see cref="Weapon.Modifiers"/> and its own entries — so the two facts
/// beside it are presentation's and Phase 4's, and nothing reads them to decide
/// what the item is.
/// </summary>
/// <param name="Weapon">The item, built through <see cref="WeaponCatalogue.Instantiate"/> like every other.</param>
/// <param name="DefeatCount">
/// The farm's depth at the moment it was collected: what bought the stacks and
/// set the odds. <strong>The extraction kill is itself one of those cycles</strong>
/// — the Killed applier advances <see cref="EnemyState.DefeatCount"/> before the
/// drop is rolled off it — so a dummy put down for the fortieth time yields a drop
/// at 40 whose unique roll resolved at 40, one higher than the plate the player
/// last read over it while it was still standing.
/// </param>
/// <param name="WasUniqueRoll">Whether the unique roll won — the extraction beat Phase 4 pays off.</param>
public sealed record Drop(Weapon Weapon, int DefeatCount, bool WasUniqueRoll);

/// <summary>
/// The drop, rolled in one pass from a dummy's class, its <c>DefeatCount</c> and
/// the floor's seed (§3.1, §3.2, §3.5). Four questions in a fixed order —
/// variant, innate enchantment, farm investment, the unique — drawn on a stream
/// of this table's own, seeded per enemy.
/// <para>
/// <strong><see cref="Roll"/> is a pure function of
/// <c>(enemy.Weapon.Class, enemy.DefeatCount, enemy.SpawnIndex, mapSeed)</c></strong>
/// and the loaded content. Nothing else: no collection order, no turn number, no
/// party state. That is what makes a drop replayable from a save that stores
/// only §5.1's fields, and what lets a floor re-derive a ground item nobody
/// picked up rather than storing it — a re-derived drop is bit-identical because
/// its inputs are.
/// </para>
/// <para>
/// <strong>It is called at collection, never at defeat</strong> (§3.2: "nothing
/// is handed over at the time"). A dummy that dies records its
/// <c>DefeatCount</c> and nothing else; the weapon is computed when the kill is
/// permanent (§4.4).
/// </para>
/// Engine-free and deterministic. <see cref="Mulberry32"/>, <c>MapGenerator</c>
/// and <c>Pathfinder</c> are untouched, and <see cref="EnemyPlacer"/>'s stream is
/// neither read nor advanced.
/// </summary>
public static class LootTable
{
    /// <summary>
    /// The drop stream's splitter: §3.5 asks for <c>mapSeed ^ LootSalt</c>, in
    /// the pattern <see cref="EnemyPlacer.SeedSalt"/> already sets, and distinct
    /// from both it and <see cref="FarmLadder.ReviveSalt"/> — new randomness gets
    /// its own stream and never a continuation of an existing one.
    /// </summary>
    public const long LootSalt = 0x5BF03635;

    /// <summary>
    /// The per-enemy term mixed into the drop seed. Collection order is the
    /// player's: a map-wide stream would make the same farm yield different
    /// weapons depending on which corpse the party walked to first, and a save
    /// would have to remember where that stream stood.
    /// </summary>
    private const long SpawnMix = 0x2545F491;

    /// <summary>Percentages are integers out of this.</summary>
    private const int Percent = 100;

    /// <summary>The odds this table rolls are quantised to integers out of this, and drawn with <c>NextInt</c> for the reason <see cref="FarmLadder.UniqueChancePermille"/> gives.</summary>
    private const int Permille = 1000;

    private const int PermillePerPercent = Permille / Percent;

    /// <summary>
    /// A drop carries at most one enchantment and it is always the first thing
    /// attached (§3.1: "always exactly one"), so the farm's enchantment entry is
    /// this index whenever the weapon has one at all.
    /// <para>
    /// The index is an assumption about content, so content is held to it:
    /// <see cref="WeaponCatalogue.Instantiate"/> appends the rolled entry
    /// <em>after</em> whatever the def lists, and
    /// <see cref="ContentValidator.RuleMartialVariantNoEnchantment"/> refuses a
    /// martial variant that lists one — without that rule a dagger variant
    /// authored with, say, <c>vampiric</c> would drop carrying two entries and
    /// the farm would bank its tiers onto the listed one. A caster's single
    /// innate is its identity and is <see cref="ContentValidator.RuleCasterInnate"/>'s.
    /// </para>
    /// </summary>
    private const int InnateIndex = 0;

    /// <summary>
    /// The neutral divisor the farm's XP grant is priced at: the dummy that has
    /// been casting with the weapon has no INT of its own, and
    /// <c>InnateStats.None</c> is the house answer for a thing with no nature.
    /// Granting at exactly one tier's bar here leaves no floating remainder for a
    /// future wielder's INT to re-price.
    /// </summary>
    private const int NoNature = InnateStats.Low;

    /// <summary>The variants a drop rolls between, counted off the code-fixed enum and never off the catalogue's length (see <see cref="StreamFor"/>).</summary>
    private static readonly int Roles = Enum.GetValues<VariantRole>().Length;

    /// <summary>The four elements a wand's drop rolls between, in <see cref="DamageType"/> order: a code-fixed count, for the same reason.</summary>
    private static readonly DamageType[] Elements =
        Enum.GetValues<DamageType>().Where(t => t != DamageType.None).ToArray();

    /// <summary>
    /// The kinds that are declared and do nothing yet — the entries §3.3 names
    /// without fixing what they are worth. They are kept out of
    /// <see cref="DropOrder"/> because a drop's one enchantment is guaranteed to
    /// be its only one: winning the rare roll and getting an inert entry would
    /// spend the roll <em>and</em> reserve the entry's lock for nothing. Code
    /// rather than content, because whether a kind has a behaviour is code; a
    /// kind leaves this set in the change that gives it one.
    /// </summary>
    private static readonly IReadOnlySet<EffectKind> InertKinds = new HashSet<EffectKind>
    {
        EffectKind.Weightless,
        EffectKind.Momentum,
        EffectKind.Echoing,   // the first of them that is not a unique soul: without this filter a martial drop could win it
    };

    /// <summary>
    /// The drop pool: every enchantment id a drop may ever roll, in a fixed
    /// order held here in code (<see cref="DropOrder"/> filters it per class).
    /// <para>
    /// <strong>Deliberately not <see cref="EnchantmentCatalogue.Rollable"/>,
    /// which is file order.</strong> Appending an entry to
    /// <c>enchantments.json</c> would re-map every index of a content-ordered
    /// list, so an already-saved, not-yet-collected dummy would silently start
    /// dropping a different weapon — the hazard <see cref="EnemyPlacer"/> already
    /// refuses for its own draw ("adding a weapon re-rolls nobody"). Here a new
    /// entry is a deliberate one-line edit at the end of this list, and until it
    /// is made the entry simply never drops.
    /// </para>
    /// <para>
    /// §3.3's four remaining entries were appended here when they shipped, which
    /// is that edit being made: Arcane and Shattering join the martial pool, and
    /// the last two are named and then filtered out by <see cref="DropOrder"/> —
    /// Aegis because its behaviour is a compiled step on the defender rather than
    /// a row in any loop, Echoing because it is declared and inert. Naming them
    /// and letting the filters answer is the point: a later change that gives
    /// either a live loop behaviour makes it rollable without a second edit here.
    /// </para>
    /// <see cref="ContentValidator.ValidateEnchantments"/> refuses content that
    /// does not carry every id named here.
    /// </summary>
    public static readonly IReadOnlyList<string> DropPool =
    [
        "flaming", "cold", "shocking", "acidic",     // the four elements, in DamageType order
        "vampiric",                                  // the one ordinary catalogue entry Phase 1 shipped
        "regeneration", "ward", "poison", "mire",    // the staff effects: a cast and nothing else, so no martial drop keeps them
        "arcane", "shattering",                      // §3.3's damage entry and its crit entry: both fire at step 4 of any hit
        "aegis", "echoing",                          // filtered out below — a compiled defender step, and a declared-inert kind
    ];

    /// <summary>
    /// The variants a caster class's drop rolls between, in a fixed order held
    /// here in code — the caster half of the rule <see cref="DropPool"/> follows
    /// for the enchantment draw.
    /// <para>
    /// <strong>A caster's variants carry no role</strong> — they vary by effect
    /// and shape (§3.1), which is what
    /// <see cref="ContentValidator.RuleRoleOnMartialOnly"/> says — so the draw
    /// cannot decode to a <see cref="VariantRole"/> the way a martial one does,
    /// and the only list left to index would be
    /// <see cref="WeaponCatalogue.ByClass"/>, which is <em>file</em> order.
    /// Swapping two rows of <c>weapons.json</c> would then silently re-roll
    /// every saved, not-yet-collected caster dummy's drop — half of exactly what
    /// §3.5's rule forbids, the other half being the append
    /// <see cref="Roles"/> already covers. Naming them here makes the draw's
    /// meaning code's, so a content reorder moves nothing and an append re-rolls
    /// nobody.
    /// </para>
    /// <see cref="ContentValidator.ValidateWeapons"/> refuses a caster class
    /// that does not hold every variant named here, which is also what keeps the
    /// draw in range: a file with three staves is refused at load, the way every
    /// other dangling reference is, rather than throwing on roughly one drop in
    /// four deep inside a collection.
    /// </summary>
    public static readonly IReadOnlyDictionary<WeaponClass, IReadOnlyList<string>> CasterVariants =
        new Dictionary<WeaponClass, IReadOnlyList<string>>
        {
            [WeaponClass.Staff] = ["staff_of_renewal", "staff_of_warding", "staff_of_blight", "staff_of_mire"],
            [WeaponClass.Wand] = ["wand_of_the_blast", "wand_of_the_cone", "wand_of_the_beam", "wand_of_the_nova"],
        };

    /// <summary>
    /// The stream the dummy at <paramref name="spawnIndex"/> rolls its drop on:
    /// <c>mapSeed ^ LootSalt</c> plus a per-enemy term, run through
    /// <see cref="FarmLadder.SplitMix32"/> before it is handed over.
    /// <para>
    /// The finalizer is there because the composition is a thin linear function
    /// of the index and mulberry32's <em>first</em> output is a comparatively
    /// weak function of its state — and the first draw off this stream is the
    /// variant, one of the two draws this phase makes most load-bearing. Without
    /// it neighbouring spawn indices on one floor would band.
    /// </para>
    /// <para>
    /// <paramref name="mapSeed"/> is the floor's own generation seed and the mix
    /// carries no floor term, which is safe only while every floor has a distinct
    /// seed: Phase 4 owes this phase either a per-floor seed or a floor term in
    /// both of the phase's mixes.
    /// </para>
    /// </summary>
    public static Mulberry32 StreamFor(long mapSeed, int spawnIndex)
        => new(FarmLadder.SplitMix32(mapSeed ^ LootSalt ^ (spawnIndex * SpawnMix)));

    /// <summary>
    /// The entries a drop of <paramref name="cls"/> may roll, in the fixed order
    /// of <see cref="DropPool"/>: the rare martial roll draws a uniform index
    /// into this.
    /// <para>
    /// Three filters, and each buys the same thing — that the entry a drop wins
    /// is one the weapon can actually use. An entry in <c>neverRolled</c> is a
    /// unique soul and exists only on the unique that carries it; an inert kind
    /// has no behaviour yet (<see cref="InertKinds"/>); and an entry whose only
    /// behaviour sits on an event this class never raises would be a lock
    /// reserved for something that can never fire — the four staff effects are
    /// exactly that on a dagger, since <see cref="EffectKind.ApplyStatus"/>
    /// appears in the cast table and no other. §3.3's "any enchantment goes on
    /// any weapon" is about what may be <em>attached</em>; what may be
    /// <em>rolled</em> is the one place class enters at all (§3.3's own
    /// class-agnostic section), and it sets the odds rather than picking a
    /// favourite.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> DropOrder(WeaponClass cls)
    {
        var catalogue = GameContent.Current.Enchantments;
        var raised = RaisedBy(cls);
        var order = new List<string>(DropPool.Count);
        foreach (var id in DropPool)
        {
            var def = catalogue[id];   // validated at load: every pool id resolves
            if (catalogue.NeverRolled.Contains(id) || InertKinds.Contains(def.Effect))
                continue;
            if (!raised.Any(e => EnchantmentBehaviours.FiresOn(e, def.Effect)))
                continue;
            order.Add(id);
        }
        return order;
    }

    /// <summary>
    /// The whole drop, in one pass (§3.1, §3.2). Four questions, always in this
    /// order and each drawn on <see cref="StreamFor"/>:
    /// <list type="number">
    /// <item>the <strong>variant</strong>, uniform over the code-fixed
    /// <see cref="VariantRole"/> count — the class is the dummy's own and is read
    /// back rather than rolled (§3.1);</item>
    /// <item>the <strong>innate enchantment</strong> — a staff's is fixed by its
    /// variant and draws nothing, a wand draws one of the four elements, and
    /// everything else rolls <see cref="Tuning.DropEnchantChancePercent"/> and,
    /// on a hit, one entry of <see cref="DropOrder"/>;</item>
    /// <item>the <strong>farm investment</strong>: <c>DefeatCount</c> draws at
    /// <see cref="Tuning.DefeatStackChance"/>, each success landing uniformly on
    /// one of the entries the weapon still has room in;</item>
    /// <item>the <strong>unique roll</strong>, one draw against
    /// <see cref="FarmLadder.UniqueChancePermille"/>, which a win resolves to a
    /// unique of the dummy's class and discards the farm's investment for.</item>
    /// </list>
    /// Every branch draws or does not draw by a rule that depends on saved state
    /// alone, so the pass is deterministic and the same enemy row rolls the same
    /// weapon on every run and on every machine.
    /// </summary>
    /// <param name="enemy">The dummy being collected: its weapon's class, its <c>DefeatCount</c> and its <c>SpawnIndex</c>, and nothing else.</param>
    /// <param name="mapSeed">The floor's generation seed.</param>
    public static Drop Roll(EnemyState enemy, long mapSeed)
    {
        ArgumentNullException.ThrowIfNull(enemy);

        var catalogue = GameContent.Current.Weapons;
        var cls = enemy.Weapon.Class;
        int defeatCount = enemy.DefeatCount;
        var rng = StreamFor(mapSeed, enemy.SpawnIndex);

        // (1) The variant, rolled fresh: the dummy fought with one of its class's
        // four and drops another, so what it was holding says the class and
        // nothing more (§3.1).
        var variant = VariantOf(catalogue, cls, rng.NextInt(0, Roles - 1));

        // (2) The entry it arrives with. A staff's is already on the def.
        DamageType? element = null;
        EnchantmentDef? rolled = null;
        if (cls == WeaponClass.Wand)
        {
            element = Elements[rng.NextInt(0, Elements.Length - 1)];
        }
        else if (cls != WeaponClass.Staff)
        {
            int chance = GameContent.Current.Tuning.DropEnchantChancePercent * PermillePerPercent;
            if (rng.NextInt(0, Permille - 1) < chance)
            {
                var pool = DropOrder(cls);
                if (pool.Count > 0)
                    rolled = GameContent.Current.Enchantments[pool[rng.NextInt(0, pool.Count - 1)]];
            }
        }

        var weapon = catalogue.Instantiate(variant.Id, element, rolled);

        // (3) The farm: one draw per cycle at DefeatStackChance, then a uniform
        // draw over what still has room. Depth, never breadth — the list is what
        // the weapon already carries — and the entries run out while the odds
        // below never do.
        int bankedTiers = 0;
        var eligible = new List<ModifierType?>(Roles);
        for (int cycle = 0; cycle < defeatCount; cycle++)
        {
            if (!FarmLadder.RollsAStack(rng))
                continue;

            Eligible(weapon, bankedTiers, eligible);
            if (eligible.Count == 0)
                continue;   // everything capped: the cycle is spent, which is what ends a farm

            var entry = eligible[rng.NextInt(0, eligible.Count - 1)];
            if (entry is { } modifier)
            {
                weapon.Acquire(modifier, 1);
            }
            else
            {
                // A tier's worth of XP rather than a tier outright (§3.2): the
                // same credit a trigger the dummy paid for would have made, so
                // the farm has no path of its own into the ladder.
                var innate = weapon.Enchantments[InnateIndex];
                weapon.CreditEnchantment(InnateIndex, innate.XpToNextTier(NoNature), NoNature);
                bankedTiers++;
            }
        }

        // (4) The unique. The draw is taken either way, so the stream stands in
        // the same place whichever branch runs.
        bool won = rng.NextInt(0, Permille - 1) < FarmLadder.UniqueChancePermille(defeatCount);
        if (won)
        {
            // A won draw always yields a unique of the dummy's class (§3.2): it
            // is never reported as a lost roll, so there is no branch here that
            // hands back the ordinary drop.
            //
            // A unique's spread is forged, so winning lands on a deeper base with
            // the acquired budget untouched (§3.2) — which means the farm's
            // stacks and banked tiers are discarded rather than carried over.
            var unique = UniqueFor(catalogue, cls, variant.Id, rng);
            weapon = catalogue.Instantiate(unique.Id, NeedsAnElement(unique) ? element : null);
            return new Drop(weapon, defeatCount, WasUniqueRoll: true);
        }

        return new Drop(weapon, defeatCount, WasUniqueRoll: false);
    }

    /// <summary>
    /// The variant a draw of <paramref name="index"/> resolves to. A martial
    /// class decodes it as a <see cref="VariantRole"/>, which is how
    /// <see cref="EnemyPlacer"/> already reads its own draw; a caster's variants
    /// carry no role — they vary by effect and shape (§3.1) — so the same
    /// code-fixed draw indexes <see cref="CasterVariants"/>, an ordered id list
    /// held in code. Either way both the <em>count</em> and the <em>order</em>
    /// are fixed here rather than by the catalogue's, so appending a weapon
    /// re-rolls nobody and reordering the file moves nobody.
    /// </summary>
    private static WeaponDef VariantOf(WeaponCatalogue catalogue, WeaponClass cls, int index)
    {
        if (Weapon.KindOf(cls) != WeaponKind.Caster)
            return catalogue.Variant(cls, (VariantRole)index);

        var order = CasterVariants.TryGetValue(cls, out var named)
            ? named
            : throw new KeyNotFoundException($"{cls} is a caster class that {nameof(CasterVariants)} does not name; a drop rolls one of {Roles} variants.");
        return order.Count == Roles
            ? catalogue[order[index]]   // resolvable at load: ContentValidator.RuleCasterVariantPool
            : throw new InvalidOperationException($"{cls}'s drop order names {order.Count} variants; a drop rolls one of {Roles}.");
    }

    /// <summary>
    /// What a farm cycle may land on, rebuilt into <paramref name="into"/> each
    /// time because what is capped changes as the farm runs.
    /// <para>
    /// <strong>The order is stated rather than incidental</strong>, because the
    /// draw is a uniform index into it: the weapon's forged modifier types first,
    /// in <see cref="ModifierType"/> order (which
    /// <see cref="ModifierSet.Entries"/> already yields), then the enchantment
    /// entry last. Putting the entry first instead would change which entry every
    /// hit on every farmed weapon landed on, with nothing in the suite noticing.
    /// </para>
    /// <para>
    /// A modifier is eligible while it is below <see cref="ModifierRules.Cap"/>
    /// over its <em>forged</em> count — 6, 7 or 8 rather than the bare 5 an
    /// off-class graft would hit — and while the relations still allow it, which
    /// is the gate <see cref="Weapon.Acquire"/> demands of every source rather
    /// than throwing at it. The enchantment entry is eligible until
    /// <see cref="Tuning.AcquiredHeadroom"/> tiers have been banked through it:
    /// the entry's own tier has no ceiling (§3.3), only the farm's contribution
    /// to it does, and it leaves the draw exactly as a capped modifier does.
    /// </para>
    /// </summary>
    private static void Eligible(Weapon weapon, int bankedTiers, List<ModifierType?> into)
    {
        var rules = GameContent.Current.Modifiers;
        into.Clear();

        foreach (var (type, forged) in weapon.Forged.Entries)
            if (weapon.Modifiers.Stacks(type) < rules.Cap(type, forged) && rules.Allowed(type, weapon.Kind, weapon.Modifiers))
                into.Add(type);

        if (weapon.Enchantments.Count > InnateIndex && bankedTiers < rules.AcquiredHeadroom)
            into.Add(null);   // the enchantment entry, always last
    }

    /// <summary>
    /// The unique a won roll hands over: the one authored from
    /// <paramref name="variantId"/>, or — where nobody wrote one, which is true
    /// of two thirds of the variants — one of the class's own, drawn uniformly
    /// in catalogue order.
    /// <para>
    /// It never hands back nothing. "A won draw yields a unique of the dummy's
    /// class" is §3.2's ruling against the draft where a variant with no
    /// authored unique yielded the ordinary drop, and a class with no unique at
    /// all would reopen it one content edit later — silently, since the drop
    /// would report <c>WasUniqueRoll: false</c> for a draw that in fact won.
    /// <see cref="ContentValidator.RuleClassCarriesAUnique"/> refuses that at
    /// load; the throw below is what makes the unreachable case loud rather than
    /// quiet.
    /// </para>
    /// </summary>
    private static WeaponDef UniqueFor(WeaponCatalogue catalogue, WeaponClass cls, string variantId, Mulberry32 rng)
    {
        if (catalogue.UniqueDerivedFrom(variantId) is { } own)
            return own;

        var uniques = catalogue.UniquesOf(cls);
        return uniques.Count > 0
            ? uniques[rng.NextInt(0, uniques.Count - 1)]
            : throw new InvalidOperationException($"{cls} carries no unique for a won roll to land on; {nameof(ContentValidator)}.{nameof(ContentValidator.RuleClassCarriesAUnique)} refuses that at load.");
    }

    /// <summary>
    /// Whether <paramref name="def"/> is a wand whose element is still open, and
    /// so takes the one the drop already rolled. A wand unique that names its own
    /// element takes none — handing it a second would be refused — and everything
    /// else takes none either.
    /// </summary>
    private static bool NeedsAnElement(WeaponDef def)
    {
        if (def.Class != WeaponClass.Wand)
            return false;
        var catalogue = GameContent.Current.Enchantments;
        return !def.Enchantments.Any(r => catalogue[r.Id].Effect == EffectKind.ElementalDamage);
    }

    /// <summary>
    /// The events a weapon of <paramref name="cls"/> actually raises: everything
    /// strikes, lands a hit, kills and can be healed past full; a caster casts as
    /// well. What an entry needs is one live behaviour among them — the test
    /// <see cref="DropOrder"/> applies.
    /// </summary>
    private static GameEvent[] RaisedBy(WeaponClass cls) => Weapon.KindOf(cls) == WeaponKind.Caster
        ? [GameEvent.Cast, GameEvent.DamageTaken, GameEvent.DamageDealt, GameEvent.Killed, GameEvent.HealingAboveFull]
        : [GameEvent.DamageTaken, GameEvent.DamageDealt, GameEvent.Killed, GameEvent.HealingAboveFull];
}
