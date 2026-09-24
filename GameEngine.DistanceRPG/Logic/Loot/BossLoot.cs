namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The boss's own drop (§4.3): the one weapon a run the dungeon's theme is
/// <em>forged</em> onto. Where every other drop in the dungeon merely rolls for
/// the theme (<see cref="LootTable"/>), the boss's is a variant of any class its
/// theme can be forged onto, carrying the theme as a forged graft on top of its
/// own class modifier, and one guaranteed stack per floor past four — and when
/// the variant rolled is the Purity variant of the theme's own class, the graft
/// takes it to ×3 and the drop <em>is</em> that class's unique, by arithmetic.
/// <para>
/// <strong>No mechanism of its own</strong> (PHASE4-036): the graft is the
/// enchanter's operation with the modifier chosen rather than rolled —
/// <see cref="ModifierRules.CanGraft"/>, asked as forged, which is also what the
/// drop table is computed from (<see cref="DungeonCatalogue.ThemeClasses"/>) — and
/// the variant, the entry it arrives with and where each stack lands are
/// <see cref="LootTable"/>'s own steps, taken rather than copied.
/// </para>
/// <para>
/// <strong><see cref="Roll"/> is a pure function of
/// <c>(boss.Theme, boss.PromisedStacks, floorSeed)</c></strong> and the loaded
/// content, and never reads what the boss is holding: the drop is drawn over its
/// dungeon's whole drop table, so a golem swinging a hatchet may drop a bow, a
/// staff or a wand, each forged with Block (§4.3: "the golem's table is all
/// eight classes"). It is called at collection like every drop, and re-derives
/// bit-identically from the row for as long as the visit lasts, and after a
/// load that restored it.
/// </para>
/// <para>
/// <strong>There is no unique roll</strong> (PHASE4-061): the repeat-kill ladder
/// is the only place a unique is rolled. The boss's <c>DefeatCount</c> invests
/// nothing either — nobody farmed it — so the drop is handed over at count 0,
/// and what depth it carries is the promise its floor made.
/// </para>
/// Engine-free and deterministic, on a stream of its own derived from the floor's
/// seed with a named salt, never a continuation of another and never
/// <c>Random.Shared</c>. <see cref="Mulberry32"/>, <c>MapGenerator</c> and
/// <c>Pathfinder</c> are untouched.
/// </summary>
public static class BossLoot
{
    /// <summary>
    /// The boss drop stream's splitter: odd, 32-bit, distinct from every other
    /// salt in the tree, and a compiled constant rather than a tuning key — like
    /// its siblings <see cref="LootTable.LootSalt"/> and
    /// <see cref="FarmLadder.ReviveSalt"/>, it decides what a seed yields.
    /// </summary>
    public const long BossDropSalt = 0xD35A2D97;

    /// <summary>
    /// The redraw bound on the drop's class draw: past this many draws on classes
    /// the theme cannot be forged onto, the first class it can is taken. It
    /// guarantees termination and nothing else — at three classes in eight, the
    /// narrowest shipped table but one, 64 misses running is about one drop in
    /// ten to the thirteenth — and on a one-class table (Charges) the fallback is
    /// the only class anyway. Compiled, like the salt.
    /// </summary>
    internal const int MaxClassDraws = 64;

    /// <summary>The classes the drop's class draw indexes, in <see cref="WeaponClass"/> order: a compiled list, so no draw's range depends on content.</summary>
    private static readonly WeaponClass[] Classes = Enum.GetValues<WeaponClass>();

    /// <summary>
    /// The stream the boss on the floor seeded <paramref name="floorSeed"/> rolls
    /// its drop on: <c>SplitMix32(floorSeed ^ BossDropSalt)</c>, the shape
    /// <see cref="LootTable.StreamFor"/> takes. A floor holds at most one boss, so
    /// the floor's seed alone picks the stream and no per-enemy term is mixed in;
    /// the finalizer is there because the stream's first draw — the class — is
    /// the load-bearing one, as a loot stream's first draw always is.
    /// </summary>
    /// <param name="floorSeed">The bottom floor's own seed (<see cref="Floor.Seed"/>): what its turn system rolls every drop and revival off.</param>
    public static Mulberry32 StreamFor(long floorSeed) => new(FarmLadder.SplitMix32(floorSeed ^ BossDropSalt));

    /// <summary>
    /// The boss's drop, in one pass (§4.3), each draw on <see cref="StreamFor"/>:
    /// <list type="number">
    /// <item>the <strong>class</strong> — <c>NextInt(0, 7)</c> over the compiled
    /// <see cref="WeaponClass"/> order, drawn again while the class is outside the
    /// theme's drop table (<see cref="DungeonCatalogue.ThemeClasses"/>), up to
    /// <see cref="MaxClassDraws"/> draws, then the first class in the table.
    /// Redrawn and never re-steered, so every class in the table is equally
    /// likely and the unique stays rare by construction rather than by decree
    /// (PHASE4-068); and content decides only whether a draw is kept, so a
    /// relations edit moves a drop only when a draw landed on a class whose
    /// legality it changed;</item>
    /// <item>the <strong>variant</strong>, rolled fresh — <see cref="LootTable"/>'s
    /// own step;</item>
    /// <item>the <strong>entry it arrives with</strong> — <see cref="LootTable.RollInnate"/>,
    /// again the table's own step;</item>
    /// <item>the <strong>forged graft</strong>: the variant instantiated with one
    /// forged stack of the theme (<see cref="WeaponCatalogue.Instantiate"/>), on
    /// top of whatever its own class modifier is — never an ungrafted weapon
    /// (PHASE4-056), since the table holds only classes every variant of which can
    /// take it;</item>
    /// <item>the <strong>unique by arithmetic</strong>: the drop is the variant's
    /// own unique — its souls and all — exactly when the graft took the theme to
    /// <see cref="ModifierRules.MaxForged"/> and that unique's forged spread is the
    /// grafted one (<see cref="UniqueByArithmetic"/>); nothing else marks a boss
    /// drop unique;</item>
    /// <item>the <strong>guaranteed stacks</strong>, <see cref="EnemyState.PromisedStacks"/>
    /// of them, each one uniform draw over the farm's own list
    /// (<see cref="LootTable.Eligible"/>: the forged modifiers — the theme among
    /// them now — then the enchantment entry, capped ones skipped, a unique's soul
    /// never) and landed as a farm's stack lands. Guaranteed rather than rolled
    /// for: the count is the floor's, only where they go is random.</item>
    /// </list>
    /// </summary>
    /// <param name="boss">The boss being collected: its <see cref="EnemyState.Theme"/> and its <see cref="EnemyState.PromisedStacks"/> (none read as 0), and nothing else — never what it holds.</param>
    /// <param name="floorSeed">The bottom floor's seed.</param>
    /// <returns>The drop, at <c>DefeatCount</c> 0 and never a unique roll.</returns>
    /// <exception cref="InvalidOperationException">
    /// The row carries no theme, or no class can be forged with it — refused at
    /// load for every dungeon (<see cref="ContentValidator.RuleThemeReachesAClass"/>),
    /// so only a row no dungeon made can get here.
    /// </exception>
    public static Drop Roll(EnemyState boss, long floorSeed)
    {
        ArgumentNullException.ThrowIfNull(boss);
        var theme = boss.Theme
            ?? throw new InvalidOperationException("A boss's drop is forged with its dungeon's theme, and this row carries none: it was not made by a dungeon.");
        int promised = Math.Max(0, boss.PromisedStacks ?? 0);

        var content = GameContent.Current;
        var catalogue = content.Weapons;
        var table = content.Dungeons.ThemeClasses(theme);
        if (table.Count == 0)
            throw new InvalidOperationException(
                $"No class can be forged with {theme}, so a boss themed on it has nothing to drop ({ContentValidator.RuleThemeReachesAClass}).");

        var rng = StreamFor(floorSeed);

        // (1) The class, over the whole table: whatever the boss was swinging.
        WeaponClass? drawn = null;
        for (int draw = 0; draw < MaxClassDraws && drawn is null; draw++)
        {
            var cls = Classes[rng.NextInt(0, Classes.Length - 1)];
            if (table.Contains(cls))
                drawn = cls;
        }
        var dropClass = drawn ?? Classes.First(table.Contains);

        // (2) and (3): the drop's own steps, exactly as any drop takes them.
        var variant = LootTable.VariantOf(catalogue, dropClass, rng.NextInt(0, LootTable.Roles - 1));
        var (element, rolled) = LootTable.RollInnate(dropClass, rng);

        // (4) The graft, forged at construction: chosen, not rolled.
        var weapon = catalogue.Instantiate(variant.Id, element, rolled, forgedGraft: theme);

        // (5) The unique, when the two rules meet — and only then.
        if (UniqueByArithmetic(catalogue, variant, weapon, theme) is { } unique)
            weapon = catalogue.Instantiate(unique.Id, LootTable.NeedsAnElement(unique) ? element : null);

        // (6) The floor's promise, landed as a farm's stacks land.
        var eligible = new List<ModifierType?>(LootTable.Roles);
        int bankedTiers = 0;
        for (int stack = 0; stack < promised; stack++)
        {
            LootTable.Eligible(weapon, bankedTiers, theme, eligible);
            if (eligible.Count == 0)
                continue;   // nothing left with room: out of reach at the six a floor-10 boss pays

            var entry = eligible[rng.NextInt(0, eligible.Count - 1)];
            LootTable.Land(weapon, entry);
            if (entry is null)
                bankedTiers++;
        }

        return new Drop(weapon, DefeatCount: 0, WasUniqueRoll: false);
    }

    /// <summary>
    /// The unique a grafted drop <em>is</em> (§1.5, §4.3), or null: the one
    /// authored from <paramref name="variant"/>, when the graft took
    /// <paramref name="theme"/> to <see cref="ModifierRules.MaxForged"/> — "a unique
    /// is a variant with one modifier at ×3" — and that unique's forged spread
    /// equals the grafted one. Tower Guard's <c>Block ×2, Push ×1</c> and the
    /// golem's Block are The Bulwark's <c>Block ×3, Push ×1</c>.
    /// <para>
    /// <strong>Both halves are load-bearing.</strong> Spread equality alone admits
    /// the Light shape: a Flensing Knife grafted with CritWindow reaches
    /// <c>CritWindow ×2, CritMultiplier ×1, Light ×1</c>, which is exactly the
    /// Nameless Knife's forged spread — and a dungeon holding two uniques would
    /// break "each themed dungeon can drop exactly one" (PHASE4-067). The ×3 is
    /// the doc's own condition, and it refuses that knife.
    /// </para>
    /// </summary>
    internal static WeaponDef? UniqueByArithmetic(WeaponCatalogue catalogue, WeaponDef variant, Weapon grafted, ModifierType theme)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(grafted);

        if (grafted.Forged.Stacks(theme) != GameContent.Current.Modifiers.MaxForged(theme))
            return null;
        if (catalogue.UniqueDerivedFrom(variant.Id) is not { } unique)
            return null;
        var spread = ModifierSet.Of(unique.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());
        return spread.Equals(grafted.Forged) ? unique : null;
    }
}
