using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// 4.3's boss drop: the one weapon a run its dungeon's theme is forged onto. A
/// variant of any class the theme can be forged onto - whatever the boss was
/// holding - carrying the theme as a forged graft on top of its own class
/// modifier, one guaranteed stack per floor past four, and, when the variant is
/// the Purity variant of the theme's own class, that class's unique by
/// arithmetic alone. There is no unique roll, and nothing else in the dungeon is
/// forged with the theme.
/// <para>
/// Every named seed is pinned by an assertion before a test leans on it. In the
/// content collection, because two tests swap the relations or the tuning.
/// </para>
/// </summary>
[Collection(TestContent.Collection)]
public class BossLootTests
{
    /// <summary>The shipped seed: its tutorial golem holds a Pinning Lance and drops an Assassin's Fang forged with Block.</summary>
    private const long GoldenSeed = 2762136374;

    /// <summary>A tutorial seed whose golem (holding a Harpoon) drops a Tower Guard: The Bulwark. What <c>DISTANCERPG_SEED=7</c> boots.</summary>
    private const long BulwarkSeed = 7;

    /// <summary>A tutorial seed whose golem drops a Riposte Blade, carrying no entry.</summary>
    private const long RiposteSeed = 19;

    /// <summary>A tutorial seed whose golem drops a Halberd, carrying no entry.</summary>
    private const long HalberdSeed = 134;

    /// <summary>A tutorial seed whose golem drops a Hunting Bow, carrying no entry: Block on a bow.</summary>
    private const long BowSeed = 18;

    /// <summary>A Knife Warrens seed five floors deep whose Knife Mother drops a Flensing Knife.</summary>
    private const long FlensingSeed = 49;

    private static DungeonCatalogue Dungeons => GameContent.Current.Dungeons;

    private static DungeonDef Tutorial => Dungeons.Tutorial;

    private static WeaponCatalogue Weapons => GameContent.Current.Weapons;

    private static ModifierRules Rules => GameContent.Current.Modifiers;

    private static readonly WeaponClass[] AllEight = Enum.GetValues<WeaponClass>();

    private static readonly string[] Ids = ["A", "B", "C", "D"];

    private static PartyMemberState[] Party()
        => Ids.Select((id, i) => TestPools.Holding(id, TestWeapons.Get("weakspot_stiletto"), colorIndex: i)).ToArray();

    /// <summary>A weapon row's forged spread as a set.</summary>
    private static ModifierSet Spread(WeaponDef def) => ModifierSet.Of(def.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());

    /// <summary><paramref name="def"/>'s spread with one more forged stack of <paramref name="theme"/>: what a graft makes of it.</summary>
    private static ModifierSet Grafted(WeaponDef def, ModifierType theme) => ModifierSet.Of([.. Spread(def).Entries, (theme, 1)]);

    /// <summary><paramref name="def"/>'s boss, built as generation builds it, holding <paramref name="holding"/> and promised <paramref name="promised"/> stacks.</summary>
    private static EnemyState Boss(DungeonDef def, int promised = 0, string holding = "hatchet")
        => DungeonState.BossRow(def, TestWeapons.Get(holding), promised);

    /// <summary>A spread of floor seeds across the 32-bit range.</summary>
    private static long[] Seeds(int count) => Enumerable.Range(0, count).Select(i => (long)(uint)(i * 2654435761u)).ToArray();

    /// <summary>The golem's drop on the tutorial entered on <paramref name="mapSeed"/>: its bottom floor is floor 2, index 1.</summary>
    private static Drop GolemDrop(long mapSeed) => BossLoot.Roll(Boss(Tutorial), DungeonState.FloorSeed(mapSeed, 1));

    /// <summary>The row a drop was rolled as: the variant itself, or the one a unique is derived from.</summary>
    private static WeaponDef VariantOf(Drop drop)
    {
        var def = Weapons[drop.Weapon.Id];
        return def.Unique ? Weapons[def.DerivedFrom!] : def;
    }

    /// <summary>Acquired stacks: the live total less the identity spread.</summary>
    private static int Acquired(Weapon w) => w.Modifiers.Entries.Sum(e => e.Stacks) - w.Forged.Entries.Sum(e => e.Stacks);

    /// <summary>Tiers banked through the entry: it arrives at 1, and a soul is pinned there.</summary>
    private static int Banked(Weapon w) => w.Enchantments.Count == 0 ? 0 : w.Enchantments[0].Tier - 1;

    /// <summary>Everything about a drop a seed fixes: the item, its forged and live spreads, its entries, and the facts beside it.</summary>
    private static string Describe(Drop drop)
        => $"{drop.Weapon.Id} F[{drop.Weapon.Forged}] M[{drop.Weapon.Modifiers}] " +
           string.Join(",", drop.Weapon.Enchantments.Select(e => $"{e.Id}@{e.Tier}+{e.Xp}")) +
           $" x{drop.DefeatCount} {drop.WasUniqueRoll}";

    /// <summary>The first two outputs of a stream as one number: equal seeds give equal fingerprints.</summary>
    private static ulong Fingerprint(Mulberry32 rng)
        => ((ulong)(uint)(rng.NextDouble() * 4294967296.0) << 32) | (uint)(rng.NextDouble() * 4294967296.0);

    /// <summary>Stand <paramref name="actor"/> on a free floor tile beside <paramref name="target"/>: left, right, above, below.</summary>
    private static void StandBeside(Floor floor, ActorState actor, EnemyState target)
    {
        var (r, c) = Displacer.TileOf(target);
        var taken = floor.Enemies.Where(e => e.Alive).Select(Displacer.TileOf).ToHashSet();
        foreach (var (tr, tc) in new[] { (r, c - 1), (r, c + 1), (r - 1, c), (r + 1, c) })
        {
            if (floor.Map.Grid[tr, tc] != 0 || taken.Contains((tr, tc))) continue;
            (actor.X, actor.Y) = Displacer.CentreOf(tr, tc);
            return;
        }
        Assert.Fail($"No free floor tile beside the enemy at {(r, c)}.");
    }

    // ── Forged with the theme ────────────────────────────────────────────────

    [Fact]
    public void TheBossDropIsForgedWithTheTheme_OnTopOfItsClassModifier()
    {
        // PHASE4-034, -062: the boss drop is the rolled variant plus one forged
        // stack of the theme - on top of whatever the weapon's own class
        // modifier is, which stays exactly where the variant had it. On the
        // shipped tutorial the golem holds a Pinning Lance and drops an
        // Assassin's Fang: its CritWindow x2 and CritMultiplier x1 untouched, and
        // Block forged beside them.
        var golden = DungeonState.Enter(Tutorial, GoldenSeed, Party()).Floors[^1];
        var golem = golden.Boss!;
        Assert.Equal(("pinning_lance", (ModifierType?)Block, (int?)0), (golem.Weapon.Id, golem.Theme, golem.PromisedStacks));
        var fang = BossLoot.Roll(golem, golden.Seed);
        Assert.Equal("assassins_fang", fang.Weapon.Id);
        Assert.Equal(ModifierSet.Of((CritWindow, 2), (CritMultiplier, 1)), Spread(Weapons["assassins_fang"]));
        Assert.Equal(ModifierSet.Of((Block, 1), (CritWindow, 2), (CritMultiplier, 1)), fang.Weapon.Forged);
        Assert.Equal(fang.Weapon.Forged, fang.Weapon.Modifiers);   // the tutorial promises no stacks: everything on it is forged
        Assert.Equal((0, false), (fang.DefeatCount, fang.WasUniqueRoll));

        // Every shipped dungeon, over a spread of floors: the variant's spread
        // plus the theme, forged - or, where the graft made the unique, the
        // unique's spread, which is the same arithmetic.
        foreach (var def in Dungeons.All)
            foreach (long seed in Seeds(200))
            {
                var drop = BossLoot.Roll(Boss(def), seed);
                Assert.Equal(Grafted(VariantOf(drop), def.Theme), drop.Weapon.Forged);
                if (Weapons.Class(drop.Weapon.Class).Signature is { } signature)
                    Assert.True(drop.Weapon.Forged.Stacks(signature) >= 1, $"{drop.Weapon.Id} lost its class's {signature}");
            }
    }

    [Fact]
    public void ABossNeverDropsAnUngraftedWeapon()
    {
        // PHASE4-056: dropping a plain weapon of a class the theme will not take
        // was rejected - the drop table bends to the guarantee instead. Five
        // hundred floors of every shipped dungeon, at the deepest promise: never
        // a class outside the table, never a weapon without its forged graft.
        foreach (var def in Dungeons.All)
        {
            var table = Dungeons.ThemeClasses(def.Theme);
            var boss = Boss(def, promised: DungeonState.PromisedStacksFor(GameContent.Current.Tuning.BossFloorMax));
            foreach (long seed in Seeds(500))
            {
                var drop = BossLoot.Roll(boss, seed);
                Assert.Contains(drop.Weapon.Class, table);
                Assert.Equal(VariantOf(drop).Forged.GetValueOrDefault(def.Theme) + 1, drop.Weapon.Forged.Stacks(def.Theme));
            }
        }
    }

    [Fact]
    public void ABraceBossDropsDaggersSwordsAndSpearsOnly()
    {
        // PHASE4-054, the boss's half: Brace is melee-only and shares the
        // threat-zone group with the axe's Opportunist, so the Spear Warden's drop
        // is a dagger, a sword or a spear, a third each - redrawn over the eight,
        // never re-steered to the next class in order.
        var hall = Dungeons["spear_hall"];
        Assert.Equal(new[] { Dagger, Sword, Spear }, Dungeons.ThemeClasses(hall.Theme));

        var counts = new Dictionary<WeaponClass, int>();
        const int Floors = 3000;
        foreach (long seed in Seeds(Floors))
        {
            var cls = BossLoot.Roll(Boss(hall), seed).Weapon.Class;
            counts[cls] = counts.GetValueOrDefault(cls) + 1;
        }
        Assert.Equal(new[] { Dagger, Sword, Spear }, counts.Keys.Order());
        Assert.All(counts.Values, n => Assert.InRange(n / (double)Floors, 1 / 3.0 - 0.03, 1 / 3.0 + 0.03));
    }

    [Fact]
    public void TheGolemDropsFromAllEightClasses_WhateverItHolds()
    {
        // F:263-264, L539-545, PHASE4-057: Block grafts onto anything, so the
        // golem's table is all eight classes - the bow, the staff and the wand no
        // enemy fields among them - each an eighth of its drops. And the drop never
        // reads what the golem is holding: a golem holding any of the five classes
        // an enemy fields drops the identical weapon on the identical floor.
        Assert.Equal(AllEight, Dungeons.ThemeClasses(Tutorial.Theme));
        var holders = EnemyPlacer.PlacedClasses.Select(cls => Weapons.Variant(cls, VariantRole.Efficiency).Id).ToArray();
        Assert.Equal(5, holders.Length);

        var counts = new Dictionary<WeaponClass, int>();
        const int Floors = 4000;
        foreach (long seed in Seeds(Floors))
        {
            var drops = holders.Select(held => Describe(BossLoot.Roll(Boss(Tutorial, holding: held), seed))).Distinct().ToArray();
            Assert.Single(drops);
            var cls = BossLoot.Roll(Boss(Tutorial), seed).Weapon.Class;
            counts[cls] = counts.GetValueOrDefault(cls) + 1;
        }
        Assert.Equal(AllEight, counts.Keys.Order());
        Assert.All(counts.Values, n => Assert.InRange(n / (double)Floors, 1 / 8.0 - 0.03, 1 / 8.0 + 0.03));
    }

    [Fact]
    public void ARelationsEditThatKeepsTheDrawnClassKeepsTheDrop()
    {
        // No draw's range depends on content: the class draw indexes the
        // compiled eight, and the relations decide only whether a draw is kept.
        // With Brace out of the threat-zone group - which admits the axe to the
        // Brace table - a floor whose class draws never touched the axe drops the
        // identical weapon, and only a floor with an axe draw ahead of the kept
        // one moves: to the axe, the first draw that now stands.
        var braceFreed = ShippedContent.Restricted with
        {
            Excludes = ShippedContent.Restricted.Excludes
                .Select(g => g.Contains(nameof(Brace)) ? g.Where(id => id != nameof(Brace)).ToArray() : g)
                .ToList(),
        };
        var seeds = Seeds(600);
        var hall = Dungeons["spear_hall"];
        var boss = Boss(hall, promised: 2);
        var before = seeds.Select(s => Describe(BossLoot.Roll(boss, s))).ToArray();
        var beforeClasses = seeds.Select(s => BossLoot.Roll(boss, s).Weapon.Class).ToArray();

        string[] after;
        WeaponClass[] afterClasses;
        using (TestContent.Use(restricted: braceFreed))
        {
            Assert.Equal(new[] { Dagger, Sword, Spear, Axe }, GameContent.Current.Dungeons.ThemeClasses(Brace));
            after = seeds.Select(s => Describe(BossLoot.Roll(boss, s))).ToArray();
            afterClasses = seeds.Select(s => BossLoot.Roll(boss, s).Weapon.Class).ToArray();
        }

        // The class draws each floor made before the edit, up to the one kept.
        WeaponClass[] ClassDraws(long seed)
        {
            var rng = BossLoot.StreamFor(seed);
            var draws = new List<WeaponClass>();
            do draws.Add(AllEight[rng.NextInt(0, 7)]);
            while (draws[^1] is not (Dagger or Sword or Spear));
            return draws.ToArray();
        }

        int kept = 0, moved = 0, firstDrawKept = 0;
        for (int i = 0; i < seeds.Length; i++)
        {
            var draws = ClassDraws(seeds[i]);
            Assert.Equal(draws[^1], beforeClasses[i]);
            if (draws.Contains(Axe))
            {
                Assert.Equal(Axe, afterClasses[i]);
                moved++;
            }
            else
            {
                Assert.Equal(before[i], after[i]);
                kept++;
                if (draws.Length == 1) firstDrawKept++;
            }
        }
        Assert.True(kept > 0 && moved > 0 && firstDrawKept > 0, $"kept {kept}, moved {moved}, first draw kept {firstDrawKept}");
    }

    // ── The unique, by arithmetic and nothing else ───────────────────────────

    [Fact]
    public void TowerGuardAndTheGolemsBlockAreTheBulwark()
    {
        // PHASE4-063, -064: "Tower Guard (Block x2) + golem's Block graft ->
        // Block x3 = The Bulwark." Nothing special-cased it: the graft took the
        // Purity variant of Block's own class to x3, and a variant with its
        // signature at x3 is that class's unique. It arrives as The Bulwark in
        // full - Sturdy attached - and it was never a unique roll.
        var visit = DungeonState.Enter(Tutorial, BulwarkSeed, Party());
        var bottom = visit.Floors[^1];
        var golem = bottom.Boss!;
        Assert.Equal("harpoon", golem.Weapon.Id);   // whatever it holds

        // The seed's draws: a sword, then the Purity variant - the Tower Guard.
        var rng = BossLoot.StreamFor(bottom.Seed);
        Assert.Equal(Sword, AllEight[rng.NextInt(0, 7)]);
        Assert.Equal(VariantRole.Purity, (VariantRole)rng.NextInt(0, 3));
        Assert.Equal("tower_guard", Weapons.Variant(Sword, VariantRole.Purity).Id);

        var drop = BossLoot.Roll(golem, bottom.Seed);
        Assert.Equal(("the_bulwark", "The Bulwark"), (drop.Weapon.Id, drop.Weapon.Name));
        Assert.True(drop.Weapon.Unique);
        Assert.False(drop.WasUniqueRoll);
        Assert.Equal("sturdy", Assert.Single(drop.Weapon.Enchantments).Id);
        Assert.Equal(ModifierSet.Of((Block, 3), (Push, 1)), drop.Weapon.Forged);

        // The arithmetic, in the open: the Tower Guard's spread and the golem's
        // graft are the unique's spread, and the graft took Block to its limit.
        Assert.Equal(ModifierSet.Of((Block, 2), (Push, 1)), TestWeapons.Get("tower_guard").Forged);
        var grafted = Weapons.Instantiate("tower_guard", forgedGraft: Block);
        Assert.Equal(Spread(Weapons["the_bulwark"]), grafted.Forged);
        Assert.Equal(Rules.MaxForged(Block), grafted.Forged.Stacks(Block));
        Assert.False(grafted.Unique);   // the graft alone is a Tower Guard; the drop is the unique because the two rules met
        Assert.Same(Weapons["the_bulwark"], BossLoot.UniqueByArithmetic(Weapons, Weapons["tower_guard"], grafted, Block));
    }

    [Fact]
    public void ARiposteBladeAndASpearComeOutGraftedButOrdinary()
    {
        // PHASE4-065: any other roll is an ordinary themed drop. A Riposte Blade
        // becomes Block x2, Push x1, Riposte x1 - excellent, and not a unique.
        // The doc's spear, "Brace x1, Block x1", predates the spear's second
        // baseline modifier (settled: every class baseline carries one): a
        // Halberd comes out Brace x1, Longshot x1, Push x1, Block x1 - an
        // off-class hybrid and still not a unique.
        var blade = GolemDrop(RiposteSeed);
        Assert.Equal("riposte_blade", blade.Weapon.Id);
        Assert.Equal(ModifierSet.Of((Block, 2), (Push, 1), (Riposte, 1)), blade.Weapon.Forged);
        Assert.False(blade.Weapon.Unique);
        Assert.Null(Weapons.UniqueDerivedFrom("riposte_blade"));

        var halberd = GolemDrop(HalberdSeed);
        Assert.Equal("halberd", halberd.Weapon.Id);
        Assert.Equal(ModifierSet.Of((Brace, 1), (Longshot, 1), (Push, 1), (Block, 1)), halberd.Weapon.Forged);
        Assert.False(halberd.Weapon.Unique);
        Assert.Null(Weapons.UniqueDerivedFrom("halberd"));

        Assert.All(new[] { blade, halberd }, d => Assert.False(d.WasUniqueRoll));
    }

    [Fact]
    public void TheLightShapeIsNotReachedByAGraft()
    {
        // PHASE4-066, -067: a Flensing Knife is its DaggerBaseline (CritWindow x1,
        // CritMultiplier x1) and Light x1; the Knife Mother's CritWindow takes it to
        // CritWindow x2, CritMultiplier x1, Light x1 - which IS the Nameless
        // Knife's forged spread (the Light shape: its signature at x2, paid for in
        // souls). Spread equality alone would hand it over, and the Knife Warrens
        // would hold two uniques. The x3 condition refuses it: the graft took
        // CritWindow to 2, not to its forge limit.
        var warrens = Dungeons["knife_warrens"];
        var visit = DungeonState.Enter(warrens, FlensingSeed, Party());
        Assert.Equal((5, 1), (visit.BossFloor, visit.PromisedStacks));
        var bottom = visit.Floors[^1];
        var drop = BossLoot.Roll(bottom.Boss!, bottom.Seed);

        Assert.Equal("flensing_knife", drop.Weapon.Id);
        var nameless = Weapons["flensing_knife_unique"];
        Assert.Equal(("Nameless Knife", "flensing_knife"), (nameless.Name, nameless.DerivedFrom));
        Assert.Same(nameless, Weapons.UniqueDerivedFrom("flensing_knife"));
        Assert.Equal(ModifierSet.Of((CritWindow, 2), (CritMultiplier, 1), (Light, 1)), drop.Weapon.Forged);
        Assert.Equal(Spread(nameless), drop.Weapon.Forged);   // equal - and still not the unique
        Assert.False(drop.Weapon.Unique);

        Assert.Equal(2, drop.Weapon.Forged.Stacks(CritWindow));
        Assert.Equal(3, Rules.MaxForged(CritWindow));
        var grafted = Weapons.Instantiate("flensing_knife", forgedGraft: CritWindow);
        Assert.Null(BossLoot.UniqueByArithmetic(Weapons, Weapons["flensing_knife"], grafted, CritWindow));
    }

    [Fact]
    public void NoBossRollsAUnique_EachDungeonHasExactlyOne()
    {
        // PHASE4-061, -067, -068: there is no unique roll on a boss kill, and each
        // themed dungeon can drop exactly one unique, which everyone can name: its
        // theme's own class's Purity unique - Stormcrow included, off Long
        // Gallery bows no enemy there wields. Rare by construction, never by
        // decree: at the rate the class and variant draws alone give - an eighth
        // times a quarter on an eight-class table, a third times a quarter on the
        // Brace one, a quarter on the Charges one.
        var hunted = new Dictionary<string, string>
        {
            ["tutorial"] = "the_bulwark",
            ["spear_hall"] = "hoplites_wall",
            ["long_gallery"] = "stormcrow",
            ["knife_warrens"] = "widowmaker",
            ["quiver_pits"] = "feathered_death",
        };
        Assert.Equal(hunted.Keys.Order(StringComparer.Ordinal), Dungeons.All.Select(d => d.Id).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(Ranged, Dungeons.WieldClasses(Dungeons["long_gallery"]));

        const int Floors = 2000;
        foreach (var def in Dungeons.All)
        {
            var uniques = new List<string>();
            foreach (long seed in Seeds(Floors))
            {
                var drop = BossLoot.Roll(Boss(def), seed);
                Assert.False(drop.WasUniqueRoll);
                if (drop.Weapon.Unique)
                    uniques.Add(drop.Weapon.Id);
            }

            Assert.Equal(new[] { hunted[def.Id] }, uniques.Distinct());
            var own = Weapons[hunted[def.Id]];
            Assert.Equal((Weapons.ClassSignedBy(def.Theme), 3), ((WeaponClass?)own.Class, own.Forged[def.Theme]));
            double expected = 1.0 / Dungeons.ThemeClasses(def.Theme).Count / LootTable.Roles;
            Assert.InRange(uniques.Count / (double)Floors, expected - 0.02, expected + 0.02);
        }
    }

    // ── A deeper boss pays more ──────────────────────────────────────────────

    [Fact]
    public void ADeeperBossPaysOneGuaranteedStackPerFloorPastFour()
    {
        // PHASE4-042, -044: floor 5 -> 1, 6 -> 2, ... 10 -> 6, and the tutorial's
        // floor 2 none. Guaranteed rather than rolled for: every drop at a promise
        // carries exactly that many acquired stacks - a tier banked through its
        // entry counting as one, as a farm's does - and only where they land is
        // random.
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, Enumerable.Range(5, 6).Select(DungeonState.PromisedStacksFor));
        Assert.Equal(0, DungeonState.PromisedStacksFor(2));

        var tutorial = DungeonState.Enter(Tutorial, GoldenSeed, Party());
        Assert.Equal((2, 0, (int?)0), (tutorial.BossFloor, tutorial.PromisedStacks, tutorial.Floors[^1].Boss!.PromisedStacks));

        // The Spear Hall at every depth, off seeds pinned to each floor.
        var hall = Dungeons["spear_hall"];
        foreach (var (seed, floor) in new (long, int)[] { (4, 5), (1, 6), (5, 7), (0, 8), (3, 9), (6, 10) })
        {
            var visit = DungeonState.Enter(hall, seed, Party());
            Assert.Equal(floor, visit.BossFloor);
            Assert.Equal(floor - 4, visit.PromisedStacks);
            var bottom = visit.Floors[^1];
            Assert.Equal<int?>(visit.PromisedStacks, bottom.Boss!.PromisedStacks);
            var drop = BossLoot.Roll(bottom.Boss, bottom.Seed);
            Assert.Equal(visit.PromisedStacks, Acquired(drop.Weapon) + Banked(drop.Weapon));
        }

        // Exactly, on every floor of every table, not on average.
        foreach (var def in Dungeons.All)
            for (int promised = 0; promised <= 6; promised++)
                foreach (long seed in Seeds(60))
                {
                    var drop = BossLoot.Roll(Boss(def, promised), seed);
                    Assert.Equal(promised, Acquired(drop.Weapon) + Banked(drop.Weapon));
                }
    }

    [Fact]
    public void TheStacksLandAsAFarmsDo()
    {
        // PHASE4-043: distributed exactly as a farm's are - rolled among the
        // modifiers the weapon is already forged with, the theme graft included,
        // the enchantment entry banking a tier, skipping anything at its ceiling -
        // and a unique's soul, pinned at tier 1, never takes one. Over the Long
        // Gallery's whole table at the deepest promise.
        var gallery = Dungeons["long_gallery"];
        var boss = Boss(gallery, promised: 6);
        bool themeDeepened = false, tierBanked = false, uniqueSeen = false;
        foreach (long seed in Seeds(400))
        {
            var w = BossLoot.Roll(boss, seed).Weapon;
            // Depth, never breadth: nothing it was not forged with.
            Assert.Equal(w.Forged.Entries.Select(e => e.Type), w.Modifiers.Entries.Select(e => e.Type));
            Assert.True(w.Forged.Stacks(Longshot) >= 1);
            themeDeepened |= w.Modifiers.Stacks(Longshot) > w.Forged.Stacks(Longshot);
            tierBanked |= Banked(w) > 0;
            if (w.Unique)
            {
                uniqueSeen = true;
                var soul = Assert.Single(w.Enchantments);
                Assert.Equal((Enchantment.UniqueTier, 0), (soul.Tier, soul.Xp));
                Assert.Equal(6, Acquired(w));   // every stack on a modifier
            }
        }
        Assert.True(themeDeepened && tierBanked && uniqueSeen, $"theme {themeDeepened}, tier {tierBanked}, unique {uniqueSeen}");

        // The list itself: the farm's, in the farm's order, capped entries out.
        var list = new List<ModifierType?>();
        var guard = Weapons.Instantiate("tower_guard", forgedGraft: Longshot);
        LootTable.Eligible(guard, 0, Longshot, list);
        Assert.Equal(new ModifierType?[] { Block, Longshot, Push }, list);   // the theme among the forged, never a second time at the end
        guard.Acquire(Block, 20);
        Assert.Equal(Rules.Cap(Block, 2), guard.Stacks(Block));
        LootTable.Eligible(guard, 0, Longshot, list);
        Assert.Equal(new ModifierType?[] { Longshot, Push }, list);   // Block at its ceiling is skipped

        var staff = Weapons.Instantiate("staff_of_blight", forgedGraft: Longshot);
        LootTable.Eligible(staff, 0, Longshot, list);
        Assert.Equal(new ModifierType?[] { Longshot, Resonant, null }, list);   // the entry last, banking a tier
        LootTable.Eligible(staff, GameContent.Current.Tuning.AcquiredHeadroom, Longshot, list);
        Assert.Equal(new ModifierType?[] { Longshot, Resonant }, list);         // five banked: the entry is capped too

        var stormcrow = Weapons.Instantiate("stormcrow");
        LootTable.Eligible(stormcrow, 0, Longshot, list);
        Assert.Equal(new ModifierType?[] { CritWindow, Longshot }, list);   // a soul takes no stack
    }

    [Fact]
    public void ABossNeverPaysAsMuchAsTheFarm()
    {
        // PHASE4-045: the most a boss pays is six, deliberately short of the ten
        // a well-farmed two-modifier weapon banks: entering a dungeon is a choice
        // and farming is not, so certainty does not also get to be the largest
        // reward. Six on the shipped values; a tuning or a dungeon that would
        // bring it level with the farm is refused at load
        // (DungeonContentTests.ABossThatWouldPayAsMuchAsTheFarmIsRefused).
        var tuning = GameContent.Current.Tuning;
        Assert.Equal(6, tuning.BossFloorMax - tuning.BossStackOffset);
        Assert.Equal(6, DungeonState.PromisedStacksFor(tuning.BossFloorMax));
        Assert.Equal(10, 2 * tuning.AcquiredHeadroom);
        Assert.Equal(2 * tuning.AcquiredHeadroom, ContentValidator.FarmBanks(tuning.AcquiredHeadroom));
        Assert.True(tuning.BossFloorMax - tuning.BossStackOffset < 2 * tuning.AcquiredHeadroom);
    }

    [Fact]
    public void TheGolemsSpearWorksToBlockSix_ADummysToFive()
    {
        // PHASE4-037, -038, -041: the ceiling rule does the rest. The golem's
        // Halberd carries Block forged, so it works up to Block x6 (forged + 5); a
        // spear that rolled Block off a dummy in the same dungeon carries it
        // acquired and tops out at x5 - and so will the enchanter's graft (6a),
        // which arrives acquired too.
        var golems = GolemDrop(HalberdSeed).Weapon;
        Assert.Equal(("halberd", 1), (golems.Id, golems.Forged.Stacks(Block)));
        Assert.Equal(6, Rules.Cap(Block, golems.Forged.Stacks(Block)));
        golems.Acquire(Block, 20);
        Assert.Equal(6, golems.Stacks(Block));

        // A tutorial spear dummy, farmed: its first drop that grew Block.
        Drop? rolled = null;
        for (int spawn = 0; spawn < 200 && rolled is null; spawn++)
        {
            var dummy = new EnemyState { Weapon = TestWeapons.Get("phalanx_spear"), DefeatCount = 20, SpawnIndex = spawn, Theme = Block };
            var drop = LootTable.Roll(dummy, GoldenSeed);
            if (drop.Weapon.Class == Spear && drop.Weapon.Stacks(Block) > 0)
                rolled = drop;
        }
        Assert.NotNull(rolled);
        var dummys = rolled.Weapon;
        Assert.Equal(0, dummys.Forged.Stacks(Block));
        Assert.Equal(5, Rules.Cap(Block, 0));
        dummys.Acquire(Block, 20);
        Assert.Equal(5, dummys.Stacks(Block));

        // In service later: an acquired graft asks the relations alone, and caps the same.
        var served = TestWeapons.Get("halberd");
        Assert.True(Rules.CanGraft(Block, served.Kind, served.Modifiers, served.Forged, asForged: false));
        served.Acquire(Block, 20);
        Assert.Equal(5, served.Stacks(Block));
    }

    [Fact]
    public void TheThemeIsForgedOntoOneWeaponARun()
    {
        // PHASE4-039: the theme is one weapon a run - the boss's drop - and a
        // coat of paint on nothing else. Across every floor of a ten-floor Spear
        // Hall only the boss row carries a promise; every row carries the theme to
        // roll for, and no ordinary drop comes away forged with it, however deep
        // its farm ran.
        var hall = Dungeons["spear_hall"];
        var visit = DungeonState.Enter(hall, 6, Party());
        Assert.Equal(10, visit.BossFloor);

        var rows = visit.Floors.SelectMany(f => f.Enemies.Select(e => (Floor: f, Row: e))).ToList();
        var promised = Assert.Single(rows, r => r.Row.PromisedStacks is not null);
        Assert.Same(visit.Floors[^1].Boss, promised.Row);
        Assert.All(rows, r => Assert.Equal<ModifierType?>(Brace, r.Row.Theme));

        foreach (var (floor, row) in rows.Where(r => r.Row != promised.Row))
        {
            var farmed = new EnemyState { Weapon = row.Weapon, SpawnIndex = row.SpawnIndex, DefeatCount = 30, Theme = row.Theme };
            foreach (var drop in new[] { LootTable.Roll(row, floor.Seed), LootTable.Roll(farmed, floor.Seed) })
                Assert.Equal(Spread(Weapons[drop.Weapon.Id]), drop.Weapon.Forged);   // as its row forged it, and nothing more
        }

        var bossDrop = BossLoot.Roll(promised.Row, promised.Floor.Seed);
        Assert.Equal(Grafted(VariantOf(bossDrop), Brace), bossDrop.Weapon.Forged);
    }

    [Fact]
    public void BossFloorSurvivesIntoTheDrop()
    {
        // PHASE4-134, plan 1n: the boss floor sets the stack count, so it has to
        // survive into the drop resolution. The promise is fixed at entry and
        // written on the boss row: a row reloaded with what a save keeps - its
        // weapon, its place, the Run group's promise, the theme its dungeon
        // re-derives - drops the identical weapon, and a tuning.json edit after
        // entry moves neither the visit's promise nor the drop.
        var hall = Dungeons["spear_hall"];
        var visit = DungeonState.Enter(hall, 5, Party());
        Assert.Equal((7, 3), (visit.BossFloor, visit.PromisedStacks));
        var bottom = visit.Floors[^1];
        var boss = bottom.Boss!;
        string dropped = Describe(BossLoot.Roll(boss, bottom.Seed));

        var reloaded = new EnemyState
        {
            X = boss.X,
            Y = boss.Y,
            Weapon = TestWeapons.Get(boss.Weapon.Id),
            SpawnIndex = boss.SpawnIndex,
            DefeatCount = boss.DefeatCount,
            Theme = hall.Theme,
            PromisedStacks = visit.PromisedStacks,
        };
        Assert.Equal(dropped, Describe(BossLoot.Roll(reloaded, bottom.Seed)));

        using (TestContent.Use(tuning: ShippedContent.Tuning with { BossStackOffset = 2 }))
        {
            Assert.Equal(5, DungeonState.PromisedStacksFor(7));             // the tuning now prices floor 7 at five...
            Assert.Equal(3, visit.PromisedStacks);                          // ...and the visit still owes three
            Assert.Equal<int?>(3, boss.PromisedStacks);
            Assert.Equal(dropped, Describe(BossLoot.Roll(boss, bottom.Seed)));
            Assert.Equal(5, DungeonState.Enter(GameContent.Current.Dungeons["spear_hall"], 5, Party()).PromisedStacks);   // read at the next entry
        }
    }

    // ── Its own stream ───────────────────────────────────────────────────────

    [Fact]
    public void TheBossDropComesOffItsOwnStream_AndReDerivesAfterAReload()
    {
        // A new stream, derived from the floor's seed with a named salt of its
        // own - odd, 32-bit, and none already in the tree - finalized as a loot
        // stream is. Never the stream a dummy at the boss's spawn index would
        // roll on, and none of the floor's drop or revival streams across every
        // floor a boss can sit on.
        Assert.Equal(0xD35A2D97, BossLoot.BossDropSalt);
        long[] salts = [EnemyPlacer.SeedSalt, LootTable.LootSalt, FarmLadder.ReviveSalt, DungeonState.FloorSalt, StairPlacer.StairSalt, DungeonState.BossSalt, BossLoot.BossDropSalt];
        Assert.Equal(salts.Length, salts.Distinct().Count());
        Assert.InRange(BossLoot.BossDropSalt, 1L, uint.MaxValue);
        Assert.Equal(1, BossLoot.BossDropSalt & 1);

        foreach (long seed in new[] { GoldenSeed, 0L })
        {
            var finalized = new Dictionary<ulong, string>();
            void Add(Mulberry32 stream, string name)
            {
                if (!finalized.TryAdd(Fingerprint(stream), name))
                    Assert.Fail($"{name} is the same stream as {finalized[Fingerprint(stream)]}");
            }
            int floors = GameContent.Current.Tuning.BossFloorMax;
            for (int n = 0; n < floors; n++)
            {
                long floorSeed = DungeonState.FloorSeed(seed, n);
                for (int spawn = 0; spawn <= 128; spawn++)
                    Add(LootTable.StreamFor(floorSeed, spawn), $"drop: floor {n}, spawn {spawn}");
                for (int spawn = 0; spawn <= 64; spawn++)
                    for (int cycle = 0; cycle <= 64; cycle++)
                        Add(FarmLadder.ReviveStream(floorSeed, spawn, cycle), $"revival: floor {n}, spawn {spawn}, cycle {cycle}");
            }
            for (int n = 0; n < floors; n++)
            {
                long floorSeed = DungeonState.FloorSeed(seed, n);
                Add(BossLoot.StreamFor(floorSeed), $"boss drop: floor {n}");
                Assert.Equal(Fingerprint(new Mulberry32(FarmLadder.SplitMix32(floorSeed ^ 0xD35A2D97))), Fingerprint(BossLoot.StreamFor(floorSeed)));
            }
            Assert.Equal(floors * (129 + 65 * 65 + 1), finalized.Count);
        }

        // In play: the golem falls on the shipped tutorial and hands over its drop,
        // which the floor then re-derives - before and after a reload of its row,
        // holding something else entirely - identically.
        var killer = TestPools.Holding("A", TestWeapons.Make("Sledge", range: 60, damage: 200, cost: 30));
        var party = new[] { killer };
        var visit = DungeonState.Enter(Tutorial, GoldenSeed, party);
        var bottom = visit.Floors[^1];
        var golem = bottom.Boss!;
        var turns = new TurnSystem(bottom.Map.Grid, party, bottom.Enemies, () => 10, bottom.Seed, boss: golem);
        var handed = new List<Drop>();
        turns.EnemyDropped += (_, drop) => handed.Add(drop);
        StandBeside(bottom, killer, golem);
        Assert.True(turns.TryAttack(killer, golem));
        Assert.False(golem.Alive);

        string fell = Describe(Assert.Single(handed));
        Assert.Equal(Describe(BossLoot.Roll(golem, bottom.Seed)), fell);
        Assert.NotEqual(Describe(LootTable.Roll(golem, bottom.Seed)), fell);   // never the stream, or the table, a dummy at its index rolls
        Assert.Equal(fell, Describe(turns.GroundItemOf(golem)!));

        var reloaded = new EnemyState
        {
            X = golem.X,
            Y = golem.Y,
            Weapon = TestWeapons.Get("darts"),   // what it holds is never read
            SpawnIndex = golem.SpawnIndex,
            DefeatCount = golem.DefeatCount,
            DefeatedAtTurn = golem.DefeatedAtTurn,
            Alive = false,
            Theme = Tutorial.Theme,
            PromisedStacks = visit.PromisedStacks,
        };
        var again = new TurnSystem(bottom.Map.Grid, party, [.. bottom.Enemies.SkipLast(1), reloaded], () => 10, bottom.Seed, boss: reloaded)
        {
            ResurrectionActive = false,
        };
        Assert.Equal(fell, Describe(again.GroundItemOf(reloaded)!));
    }

    // ── One question ─────────────────────────────────────────────────────────

    [Fact]
    public void TheGraftIsTheOneQuestionEveryGraftAsks()
    {
        // PHASE4-036: no mechanism of its own. The boss's graft is the
        // enchanter's operation with the modifier chosen: ModifierRules.CanGraft,
        // asked as forged - the relations beside the weapon's spread, and the forge
        // limit. An illegal forged graft throws at Instantiate rather than forging
        // silently; and the drop table and the drop ask the same question, so a
        // class is in a theme's table exactly when every one of its variants
        // takes the graft, and taking it never throws.
        var ex = Assert.Throws<ArgumentException>(() => Weapons.Instantiate("hatchet", forgedGraft: Brace));   // Opportunist excludes it
        Assert.Equal("forgedGraft", ex.ParamName);
        Assert.Throws<ArgumentException>(() => Weapons.Instantiate("hunting_bow", forgedGraft: Brace));        // melee only
        Assert.Throws<ArgumentException>(() => Weapons.Instantiate("flensing_knife", forgedGraft: Light));     // forged Light x1 is the limit
        Assert.Throws<ArgumentException>(() => Weapons.Instantiate("tower_guard", forgedGraft: Charges));      // forged-only, from nothing
        Assert.Equal(ModifierSet.Of((Block, 3), (Push, 1)), Weapons.Instantiate("tower_guard", forgedGraft: Block).Forged);

        foreach (var theme in Dungeons.All.Select(d => d.Theme).Distinct())
        {
            var table = Dungeons.ThemeClasses(theme);
            foreach (var cls in AllEight)
            {
                var variants = Weapons.KindOf(cls) == WeaponKind.Caster
                    ? LootTable.CasterVariants[cls].Select(id => Weapons[id]).ToArray()
                    : Enum.GetValues<VariantRole>().Select(role => Weapons.Variant(cls, role)).ToArray();
                bool allTake = true;
                foreach (var variant in variants)
                {
                    var spread = Spread(variant);
                    bool can = Rules.CanGraft(theme, Weapons.KindOf(cls), spread, spread, asForged: true);
                    allTake &= can;
                    if (can)
                        Assert.Equal(Grafted(variant, theme), Weapons.Instantiate(variant.Id, cls == Wand ? DamageType.Cold : null, forgedGraft: theme).Forged);
                    else
                        Assert.Throws<ArgumentException>(() => Weapons.Instantiate(variant.Id, cls == Wand ? DamageType.Cold : null, forgedGraft: theme));
                }
                Assert.Equal(allTake, table.Contains(cls));
            }
        }
    }
}
