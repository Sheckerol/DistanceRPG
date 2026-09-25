using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;
using static GameEngine.DistanceRPG.Logic.WeaponClass;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// 4.3 and 5.8's dungeons as content: the tutorial and the four provisional
/// dungeons, the rules a dungeon row is held to at load, the drop table each
/// theme computes from the relations (never authored), what a boss can hold,
/// the one question every graft asks, and the Phase 4 tuning keys. In the
/// content collection because several tests load or swap content.
/// </summary>
[Collection(TestContent.Collection)]
public class DungeonContentTests
{
    private static DungeonCatalogue Dungeons => GameContent.Current.Dungeons;

    private static ModifierRules Rules => GameContent.Current.Modifiers;

    private static WeaponCatalogue Weapons => GameContent.Current.Weapons;

    private static readonly WeaponClass[] AllEight = Enum.GetValues<WeaponClass>();

    /// <summary>A weapon row's forged spread as a set: what a drop carries the moment it is made.</summary>
    private static ModifierSet Spread(WeaponDef def) => ModifierSet.Of(def.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());

    /// <summary>The variants a drop of <paramref name="cls"/> rolls between: one per role for a martial class, the code-fixed list for a caster.</summary>
    private static IEnumerable<WeaponDef> DropVariants(WeaponClass cls)
        => Weapon.KindOf(cls) == WeaponKind.Caster
            ? LootTable.CasterVariants[cls].Select(id => Weapons[id])
            : Enum.GetValues<VariantRole>().Select(role => Weapons.Variant(cls, role));

    /// <summary>Whether <paramref name="variant"/> as it drops takes <paramref name="theme"/> as a forged graft.</summary>
    private static bool TakesForged(ModifierType theme, WeaponDef variant)
    {
        var spread = Spread(variant);
        return Rules.CanGraft(theme, Weapon.KindOf(variant.Class), spread, spread, asForged: true);
    }

    /// <summary>The compiled dungeons with <paramref name="edit"/> applied to the one named <paramref name="id"/>.</summary>
    private static DungeonsData With(string id, Func<DungeonDef, DungeonDef> edit)
        => new(ShippedContent.Dungeons.Dungeons.Select(d => d.Id == id ? edit(d) : d).ToList());

    /// <summary>The compiled dungeons with <paramref name="extra"/> appended.</summary>
    private static DungeonsData Plus(DungeonDef extra) => new([.. ShippedContent.Dungeons.Dungeons, extra]);

    /// <summary>An unpinned, unattuned dungeon themed on <paramref name="theme"/>, its boss carrying the theme.</summary>
    private static DungeonDef Themed(string id, ModifierType theme)
        => new(id, id, theme, Attunement: null, BossFloorMin: null, BossFloorMax: null,
            new BossDef(id + "_boss", "Boss", MaxHp: 200, MovementBudget: 70, new Dictionary<ModifierType, int> { [theme] = 3 }),
            RetiresOnClear: false);

    private static DungeonDef WithInnate(DungeonDef def, params (ModifierType Type, int Stacks)[] innate)
        => def with { Boss = def.Boss with { Innate = innate.ToDictionary(e => e.Type, e => e.Stacks) } };

    /// <summary>The compiled content with <paramref name="dungeons"/>, loaded (and never made current).</summary>
    private static GameContent Load(DungeonsData dungeons)
        => TestContent.Load(ShippedContent.Tuning, ShippedContent.Restricted, ShippedContent.Enchantments, ShippedContent.Weapons,
            dungeons: dungeons);

    /// <summary>The compiled content under <paramref name="tuning"/>, loaded (and never made current).</summary>
    private static GameContent LoadTuned(Tuning tuning)
        => TestContent.Load(tuning, ShippedContent.Restricted, ShippedContent.Enchantments, ShippedContent.Weapons);

    /// <summary>The compiled tuning with <paramref name="t"/>'s forge limit set to <paramref name="limit"/>, every other limit as compiled.</summary>
    private static Tuning WithMaxForged(ModifierType t, int limit)
        => ShippedContent.Tuning with { MaxForged = new Dictionary<ModifierType, int>(ShippedContent.Tuning.MaxForged) { [t] = limit } };

    private static ContentException Rejected(DungeonsData dungeons) => Assert.Throws<ContentException>(() => Load(dungeons));

    private static void Refused(DungeonsData dungeons, string entry, string rule)
    {
        var ex = Rejected(dungeons);
        Assert.Equal((entry, rule), (ex.EntryId, ex.Rule));
        Assert.Contains(ex.Rule, ex.Message);
    }

    [Fact]
    public void TheTutorialIsTheShippedDungeon()
    {
        // The tutorial is the dungeon shipped today: themed on Block, its boss a
        // stone golem (PHASE4-072), 80 HP on a movement budget of 40 with Block x3
        // innate whatever it holds (S:302), two floors deep and no deeper, gone
        // for good once beaten.
        var tutorial = Dungeons.Tutorial;
        Assert.Equal("tutorial", tutorial.Id);
        Assert.Equal("The Tutorial", tutorial.Name);
        Assert.Same(tutorial, Dungeons["tutorial"]);
        Assert.Equal(Block, tutorial.Theme);
        Assert.Equal(2, tutorial.BossFloorMin);
        Assert.Equal(2, tutorial.BossFloorMax);
        Assert.Equal((2, 2), Dungeons.BossFloorRange(tutorial));
        Assert.True(tutorial.RetiresOnClear);
        Assert.Null(tutorial.Attunement);   // it teaches Block, not the type chart

        var golem = tutorial.Boss;
        Assert.Equal("stone_golem", golem.Id);
        Assert.Equal("Stone Golem", golem.Name);
        Assert.Equal(80, golem.MaxHp);
        Assert.Equal(40, golem.MovementBudget);
        Assert.Equal(new[] { (Block, 3) }, golem.Innate.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)));

        // Beside it the four provisional dungeons, each a theme with a unique to
        // hunt, each rolling its depth from the tuning's 5-10.
        Assert.Equal(new[] { "tutorial", "spear_hall", "long_gallery", "knife_warrens", "quiver_pits" }, Dungeons.All.Select(d => d.Id));
        Assert.Equal(new[] { Block, Brace, Longshot, CritWindow, Charges }, Dungeons.All.Select(d => d.Theme));
        foreach (var def in Dungeons.All.Where(d => d != tutorial))
        {
            Assert.Null(def.BossFloorMin);
            Assert.Null(def.BossFloorMax);
            Assert.Equal((5, 10), Dungeons.BossFloorRange(def));
            Assert.False(def.RetiresOnClear);
        }

        // The catalogue's own lookups: by id, and a miss that names itself.
        Assert.True(Dungeons.TryGet("spear_hall", out var hall));
        Assert.Same(Dungeons["spear_hall"], hall);
        Assert.False(Dungeons.TryGet("the_tutorial", out _));
        Assert.Throws<KeyNotFoundException>(() => Dungeons["the_tutorial"]);
    }

    [Fact]
    public void ABraceBossReachesDaggersSwordsAndSpearsOnly()
    {
        // F:245-256, verbatim: "that dungeon drops daggers, swords and spears, and
        // nothing else" (PHASE4-053/-055).
        Assert.Equal(new[] { Dagger, Sword, Spear }, Dungeons.ThemeClasses(Brace));
        Assert.Equal(new[] { Dagger, Sword, Spear }, Dungeons.ThemeClasses(Dungeons["spear_hall"].Theme));

        // And each class it leaves out fails for the doc's own reason.
        // Axe: forged Opportunist, which shares Brace's reaction group. It is melee
        // and has forge room to spare, so the exclusion is the whole of it: the
        // same axe without its Opportunist would take the graft.
        Assert.Contains(Opportunist, Rules.Excludes[Brace]);
        foreach (var axe in DropVariants(Axe))
        {
            var spread = Spread(axe);
            Assert.True(spread.Stacks(Opportunist) > 0, axe.Id);
            Assert.False(Rules.CanGraft(Brace, WeaponKind.Melee, spread, spread, asForged: true), axe.Id);

            var withoutOpportunist = ModifierSet.Of(axe.Forged.Where(kv => kv.Key != Opportunist).Select(kv => (kv.Key, kv.Value)).ToArray());
            Assert.True(Rules.CanGraft(Brace, WeaponKind.Melee, withoutOpportunist, withoutOpportunist, asForged: true), axe.Id);
        }

        // Ranged and Throwing: Brace is melee-only and both are ranged weapons.
        // Staff and Wand: also not melee. The kind alone refuses it -- a weapon of
        // that kind carrying nothing at all still cannot take it -- where a melee
        // weapon carrying nothing can.
        Assert.Equal(WeaponKind.Melee, Rules.RequiresKind[Brace]);
        Assert.True(Rules.CanGraft(Brace, WeaponKind.Melee, ModifierSet.Empty, ModifierSet.Empty, asForged: true));
        Assert.Equal(WeaponKind.Ranged, Weapon.KindOf(Ranged));
        Assert.Equal(WeaponKind.Ranged, Weapon.KindOf(Throwing));
        Assert.Equal(WeaponKind.Caster, Weapon.KindOf(Staff));
        Assert.Equal(WeaponKind.Caster, Weapon.KindOf(Wand));
        foreach (var cls in new[] { Ranged, Throwing, Staff, Wand })
        {
            Assert.False(Rules.CanGraft(Brace, Weapon.KindOf(cls), ModifierSet.Empty, ModifierSet.Empty, asForged: true), cls.ToString());
            Assert.All(DropVariants(cls), v => Assert.False(TakesForged(Brace, v), v.Id));
        }

        // Every variant of the three it keeps can take it -- the Phalanx Spear too,
        // its own class, whose graft deepens it to the forge limit.
        foreach (var cls in new[] { Dagger, Sword, Spear })
            Assert.All(DropVariants(cls), v => Assert.True(TakesForged(Brace, v), v.Id));
        Assert.Equal(Rules.MaxForged(Brace), Spread(Weapons["phalanx_spear"]).Stacks(Brace) + 1);
    }

    [Fact]
    public void BlockReachesAllEightClasses()
    {
        // "Block and Cleave graft onto anything, so the golem's table is all eight
        // classes" (F:263-264, PHASE4-057), and nothing normalises breadth.
        Assert.Equal(AllEight, Dungeons.ThemeClasses(Block));
        Assert.Equal(AllEight, Dungeons.ThemeClasses(Dungeons.Tutorial.Theme));
        Assert.Equal(AllEight, Dungeons.ThemeClasses(Cleave));

        // The one variant that could have refused it is Tower Guard, forged Block x2:
        // the golem's graft lands it on exactly MaxForged, and not past it.
        var towerGuard = Spread(Weapons["tower_guard"]);
        Assert.Equal(2, towerGuard.Stacks(Block));
        Assert.Equal(3, Rules.MaxForged(Block));
        Assert.Equal(Rules.MaxForged(Block), towerGuard.Stacks(Block) + 1);
        Assert.True(Rules.CanGraft(Block, WeaponKind.Melee, towerGuard, towerGuard, asForged: true));

        // One stack deeper is refused: The Bulwark is already at the limit.
        var bulwark = Spread(Weapons["the_bulwark"]);
        Assert.Equal(3, bulwark.Stacks(Block));
        Assert.False(Rules.CanGraft(Block, WeaponKind.Melee, bulwark, bulwark, asForged: true));
    }

    [Fact]
    public void TheDropTableIsTheLegalOne_TheBossWieldsWhatAnEnemyCanField()
    {
        foreach (var def in Dungeons.All)
        {
            // Every dungeon is themed on exactly one modifier (PHASE4-032), and its
            // table is CanGraft run over the eight classes, every variant asked.
            var legal = AllEight.Where(cls => DropVariants(cls).All(v => TakesForged(def.Theme, v))).ToArray();
            Assert.Equal(legal, Dungeons.ThemeClasses(def.Theme));

            // The boss's hands are the part an enemy can field, in class order.
            Assert.Equal(legal.Where(EnemyPlacer.PlacedClasses.Contains), Dungeons.WieldClasses(def));
            Assert.All(Dungeons.WieldClasses(def), cls => Assert.Contains(cls, Dungeons.ThemeClasses(def.Theme)));
        }

        // Block, Longshot and CritWindow reach all eight; Charges, forged-only, only
        // the throwing weapons that already carry it.
        Assert.Equal(AllEight, Dungeons.ThemeClasses(Longshot));
        Assert.Equal(AllEight, Dungeons.ThemeClasses(CritWindow));
        Assert.Equal(new[] { Throwing }, Dungeons.ThemeClasses(Charges));

        // The five the placer fields, where the theme allows them.
        var five = new[] { Dagger, Sword, Spear, Axe, Throwing };
        Assert.Equal(five, EnemyPlacer.PlacedClasses);
        Assert.Equal(five, Dungeons.WieldClasses(Dungeons["tutorial"]));
        Assert.Equal(new[] { Dagger, Sword, Spear }, Dungeons.WieldClasses(Dungeons["spear_hall"]));
        Assert.Equal(five, Dungeons.WieldClasses(Dungeons["long_gallery"]));
        Assert.Equal(five, Dungeons.WieldClasses(Dungeons["knife_warrens"]));
        Assert.Equal(new[] { Throwing }, Dungeons.WieldClasses(Dungeons["quiver_pits"]));

        // So the Long Gallery drops bows no enemy holds: the drop is the whole table.
        Assert.Contains(Ranged, Dungeons.ThemeClasses(Longshot));
        Assert.DoesNotContain(Ranged, Dungeons.WieldClasses(Dungeons["long_gallery"]));
    }

    [Fact]
    public void TheTableIsComputed_ARelationsEditMovesIt()
    {
        // The relations with Brace taken out of the threat-zone group: an axe's
        // Opportunist no longer excludes it. No dungeon row changes (PHASE4-059,
        // S:311-313: "with no other edit anywhere").
        var braceFreed = ShippedContent.Restricted with
        {
            Excludes = ShippedContent.Restricted.Excludes
                .Select(g => g.Contains(nameof(Brace)) ? g.Where(id => id != nameof(Brace)).ToArray() : g)
                .ToList(),
        };

        // Read off what Load returns while the shipped content is still current:
        // a table computed off GameContent.Current would still say three classes.
        var edited = TestContent.Load(ShippedContent.Tuning, braceFreed, ShippedContent.Enchantments, ShippedContent.Weapons);
        Assert.NotSame(edited, GameContent.Current);
        Assert.Equal(new[] { Dagger, Sword, Spear, Axe }, edited.Dungeons.ThemeClasses(Brace));
        Assert.Equal(new[] { Dagger, Sword, Spear }, Dungeons.ThemeClasses(Brace));   // the current content's, untouched

        // The Spear Hall is the same row, and its boss may now hold an axe too.
        Assert.Same(ShippedContent.Dungeons.Dungeons[1], edited.Dungeons["spear_hall"]);
        Assert.Equal(new[] { Dagger, Sword, Spear, Axe }, edited.Dungeons.WieldClasses(edited.Dungeons["spear_hall"]));

        // And through the game's own content swap, the game reads the moved table.
        using (TestContent.Use(restricted: braceFreed))
            Assert.Contains(Axe, GameContent.Current.Dungeons.ThemeClasses(Brace));
        Assert.DoesNotContain(Axe, GameContent.Current.Dungeons.ThemeClasses(Brace));
    }

    [Fact]
    public void AThemeIsAClassSignature_PinLightAndCritMultiplierAreRefused()
    {
        // Themes are drawn from class signatures -- the modifiers a Purity variant
        // doubles (PHASE4-069). The compiled table holds six.
        var signatures = AllEight.Where(ContentValidator.Signature.ContainsKey).Select(cls => ContentValidator.Signature[cls]).ToArray();
        Assert.Equal(new[] { CritWindow, Block, Brace, Cleave, Longshot, Charges }, signatures);
        foreach (var cls in AllEight.Where(ContentValidator.Signature.ContainsKey))
        {
            Assert.Equal(2, Weapons.Variant(cls, VariantRole.Purity).Forged[ContentValidator.Signature[cls]]);
            Assert.Equal(cls, ContentValidator.ClassSignedBy(ContentValidator.Signature[cls]));
        }

        // Pin, a Control and Support modifier: no variant is forged x2 in it, so it
        // could never reach x3 (PHASE4-070).
        Assert.All(Weapons.All.Where(d => !d.Unique), d => Assert.True(d.Forged.GetValueOrDefault(Pin) < 2, d.Id));
        Refused(With("knife_warrens", d => d with { Theme = Pin }), "knife_warrens", ContentValidator.RuleThemeIsClassSignature);

        // Light, forge-limited to x1 (PHASE4-071): a Light graft can never be the x3 of a unique.
        Assert.Equal(1, Rules.MaxForged(Light));
        Refused(With("knife_warrens", d => d with { Theme = Light }), "knife_warrens", ContentValidator.RuleThemeIsClassSignature);

        // CritMultiplier is in F:313-315's list but is a second baseline modifier
        // now, and no Purity variant doubles it; Resonant is forge-limited to x1 on
        // the casters that carry it. Neither yields the Purity-coincidence unique
        // under the shipped content, so both are refused -- a recorded departure.
        Assert.All(Weapons.All.Where(d => !d.Unique), d => Assert.True(d.Forged.GetValueOrDefault(CritMultiplier) < 2, d.Id));
        Refused(With("knife_warrens", d => d with { Theme = CritMultiplier }), "knife_warrens", ContentValidator.RuleThemeIsClassSignature);
        Assert.Equal(1, Rules.MaxForged(Resonant));
        Refused(With("knife_warrens", d => d with { Theme = Resonant }), "knife_warrens", ContentValidator.RuleThemeIsClassSignature);

        // Nothing outside the enum passes either.
        Refused(With("knife_warrens", d => d with { Theme = (ModifierType)99 }), "knife_warrens", ContentValidator.RuleThemeIsClassSignature);
        Assert.Null(ContentValidator.ClassSignedBy(Pin));
    }

    [Fact]
    public void EveryThemeHuntsAUniqueFromItsOwnClass()
    {
        // Each shipped theme's own class is in its drop table, and its Purity
        // variant's unique raises the theme to x3 -- the one unique the dungeon
        // gates, whatever the table's breadth (PHASE4-058, F:266-268).
        var hunted = new List<(string Dungeon, string Unique)>();
        foreach (var def in Dungeons.All)
        {
            var own = Assert.IsType<WeaponClass>(ContentValidator.ClassSignedBy(def.Theme));
            Assert.Contains(own, Dungeons.ThemeClasses(def.Theme));

            var purity = Weapons.Variant(own, VariantRole.Purity);
            Assert.Equal(2, purity.Forged[def.Theme]);
            var unique = Weapons.UniqueDerivedFrom(purity.Id);
            Assert.NotNull(unique);
            Assert.Equal(3, unique.Forged[def.Theme]);
            hunted.Add((def.Id, unique.Id));
        }
        Assert.Equal(new[]
        {
            ("tutorial", "the_bulwark"),
            ("spear_hall", "hoplites_wall"),
            ("long_gallery", "stormcrow"),
            ("knife_warrens", "widowmaker"),
            ("quiver_pits", "feathered_death"),
        }, hunted);

        // The Long Gallery's own class is one no enemy wields: Stormcrow is dropped, never held.
        Assert.DoesNotContain(Ranged, EnemyPlacer.PlacedClasses);
        Assert.Contains(Ranged, Dungeons.ThemeClasses(Longshot));

        // Cleave is a signature, but the Great Axe has no unique: refused until one is authored.
        Assert.Null(Weapons.UniqueDerivedFrom(Weapons.Variant(Axe, VariantRole.Purity).Id));
        Refused(Plus(Themed("axe_pit", Cleave)), "axe_pit", ContentValidator.RuleThemeHasAUnique);

        // A relations edit that makes Longshot melee-only pushes the bows out of
        // their own theme's table: the Long Gallery could never drop Stormcrow,
        // and every other dungeon check still passes. Refused -- asked of a
        // catalogue built straight over those relations, because through Load the
        // bows themselves are refused first; this rule guards what the weapon
        // rules cannot see (S:322-324).
        var meleeLongshot = ShippedContent.Restricted with
        {
            Kind = new Dictionary<string, string>(ShippedContent.Restricted.Kind, StringComparer.Ordinal) { [nameof(Longshot)] = RestrictedData.MeleeKind },
        };
        var ex = Assert.Throws<ContentException>(() =>
            new DungeonCatalogue(ShippedContent.Dungeons, new ModifierRules(ShippedContent.Tuning, meleeLongshot), Weapons, ShippedContent.Tuning));
        Assert.Equal(("long_gallery", ContentValidator.RuleThemeReachesItsOwnClass), (ex.EntryId, ex.Rule));

        var viaLoad = Assert.Throws<ContentException>(() =>
            TestContent.Load(ShippedContent.Tuning, meleeLongshot, ShippedContent.Enchantments, ShippedContent.Weapons));
        Assert.Equal(("hunting_bow", ContentValidator.RuleForgedNotAllowed), (viaLoad.EntryId, viaLoad.Rule));
    }

    [Fact]
    public void AThemesUniqueSitsAtItsForgeLimit_ALimitTunedPastItIsRefused()
    {
        // A boss's drop becomes its theme's unique by arithmetic alone: the graft
        // takes the theme to MaxForged(theme) and the grafted spread is the
        // unique's (S6). The Purity variant is forged x2 and its unique x3, so the
        // two meet only while the theme's forge limit is x3 -- and for every
        // shipped theme it is (PHASE4-058).
        foreach (var def in Dungeons.All)
        {
            var own = Assert.IsType<WeaponClass>(ContentValidator.ClassSignedBy(def.Theme));
            var purity = Weapons.Variant(own, VariantRole.Purity);
            var unique = Weapons.UniqueDerivedFrom(purity.Id);
            Assert.NotNull(unique);
            Assert.Equal(Rules.MaxForged(def.Theme), purity.Forged[def.Theme] + 1);   // the graft lands on the limit
            Assert.Equal(Rules.MaxForged(def.Theme), unique.Forged[def.Theme]);       // and the unique sits there
        }

        // Block's limit tuned to x4. The graft is still legal -- a Tower Guard takes
        // it, so the golem's table is still all eight -- but it stops at x3, a stack
        // short of the limit, and no weapon check notices: nothing is forged past
        // x4. No golem drop could ever become The Bulwark, so the tutorial, the
        // dungeon that hunts it, is refused.
        var blockToFour = WithMaxForged(Block, 4);
        var towerGuard = Spread(Weapons["tower_guard"]);
        Assert.True(new ModifierRules(blockToFour, ShippedContent.Restricted).CanGraft(Block, WeaponKind.Melee, towerGuard, towerGuard, asForged: true));
        Assert.Equal(3, towerGuard.Stacks(Block) + 1);
        var ex = Assert.Throws<ContentException>(() => LoadTuned(blockToFour));
        Assert.Equal(("tutorial", ContentValidator.RuleThemeHasAUnique), (ex.EntryId, ex.Rule));
        Assert.Contains("'the_bulwark' carries Block x3, and Block's forge limit is x4", ex.Message);

        // One signature's limit is the business of the dungeon themed on it: Brace's names the Spear Hall.
        ex = Assert.Throws<ContentException>(() => LoadTuned(WithMaxForged(Brace, 4)));
        Assert.Equal(("spear_hall", ContentValidator.RuleThemeHasAUnique), (ex.EntryId, ex.Rule));

        // And a signature no dungeon is themed on is none of theirs: Cleave at x4 loads.
        var cleaveToFour = LoadTuned(WithMaxForged(Cleave, 4));
        Assert.Equal(4, cleaveToFour.Modifiers.MaxForged(Cleave));
        Assert.Equal(3, cleaveToFour.Modifiers.MaxForged(Block));
    }

    [Fact]
    public void ABossWithNothingToWieldIsRefused()
    {
        // Longshot kept to the bows alone. Ranged-only is not enough on its own:
        // a throwing weapon is a ranged weapon too, so a thrown Longshot stays
        // legal and the boss could still hold one.
        var rangedOnly = ShippedContent.Restricted with
        {
            Kind = new Dictionary<string, string>(ShippedContent.Restricted.Kind, StringComparer.Ordinal) { [nameof(Longshot)] = RestrictedData.RangedKind },
        };
        var loose = new DungeonCatalogue(ShippedContent.Dungeons, new ModifierRules(ShippedContent.Tuning, rangedOnly), Weapons, ShippedContent.Tuning);
        Assert.Equal(new[] { Ranged, Throwing }, loose.ThemeClasses(Longshot));
        Assert.Equal(new[] { Throwing }, loose.WieldClasses(loose["long_gallery"]));

        // Excluded beside Charges as well, it reaches the bows and nothing else: a
        // table no enemy fields, so the Long Gallery's boss would have nothing to hold.
        var bowsOnly = rangedOnly with { Excludes = [.. ShippedContent.Restricted.Excludes, [nameof(Longshot), nameof(Charges)]] };
        var rules = new ModifierRules(ShippedContent.Tuning, bowsOnly);
        var ex = Assert.Throws<ContentException>(() => new DungeonCatalogue(ShippedContent.Dungeons, rules, Weapons, ShippedContent.Tuning));
        Assert.Equal(("long_gallery", ContentValidator.RuleBossWieldsAClass), (ex.EntryId, ex.Rule));

        // The same relations without the gallery: the table is Ranged alone.
        var withoutGallery = new DungeonsData(ShippedContent.Dungeons.Dungeons.Where(d => d.Id != "long_gallery").ToList());
        var tables = new DungeonCatalogue(withoutGallery, rules, Weapons, ShippedContent.Tuning);
        Assert.Equal(new[] { Ranged }, tables.ThemeClasses(Longshot));
        Assert.Empty(tables.WieldClasses(ShippedContent.Dungeons.Dungeons.Single(d => d.Id == "long_gallery")));
    }

    [Fact]
    public void OnlyTheTutorialRetires_AndItPinsItsFloor()
    {
        Assert.Equal(new[] { "tutorial" }, Dungeons.All.Where(d => d.RetiresOnClear).Select(d => d.Id));
        Assert.Same(Dungeons["tutorial"], Dungeons.Tutorial);

        // 5.8's two checks. Only one dungeon retires on clear: a second is "a
        // content decision nobody has made", and none leaves the hub nothing to
        // open behind.
        Refused(With("spear_hall", d => d with { RetiresOnClear = true, BossFloorMin = 5, BossFloorMax = 5 }),
            "spear_hall", ContentValidator.RuleOneRetiringDungeon);
        Refused(With("tutorial", d => d with { RetiresOnClear = false }), "dungeons", ContentValidator.RuleOneRetiringDungeon);

        // And the one that does pins its boss floor: the tutorial's two floors are
        // the enchanter's mana budget, not a roll.
        Refused(With("tutorial", d => d with { BossFloorMax = 3 }), "tutorial", ContentValidator.RuleRetiringDungeonPinsItsFloor);
        Refused(With("tutorial", d => d with { BossFloorMin = null, BossFloorMax = null }), "tutorial", ContentValidator.RuleRetiringDungeonPinsItsFloor);
    }

    [Fact]
    public void ABossFloorRangeStartsAtFloorOne_AndIsNeverInverted()
    {
        // A pinned range is its own; an unpinned one is the tuning's.
        Assert.Equal((5, 10), Dungeons.BossFloorRange(Dungeons["spear_hall"]));
        using (TestContent.Use(tuning: ShippedContent.Tuning with { BossFloorMin = 3, BossFloorMax = 4 }))
        {
            var swapped = GameContent.Current.Dungeons;
            Assert.Equal((3, 4), swapped.BossFloorRange(swapped["spear_hall"]));
            Assert.Equal((2, 2), swapped.BossFloorRange(swapped.Tutorial));
        }

        Refused(With("tutorial", d => d with { BossFloorMin = 0, BossFloorMax = 0 }), "tutorial", ContentValidator.RuleBossFloorRange);
        Refused(With("spear_hall", d => d with { BossFloorMin = 7, BossFloorMax = 6 }), "spear_hall", ContentValidator.RuleBossFloorRange);

        // One end pinned reads the other off the tuning: a floor-12 minimum against its 10 is inverted.
        Refused(With("spear_hall", d => d with { BossFloorMin = 12 }), "spear_hall", ContentValidator.RuleBossFloorRange);
        var shallow = Load(With("spear_hall", d => d with { BossFloorMax = 6 }));
        Assert.Equal((5, 6), shallow.Dungeons.BossFloorRange(shallow.Dungeons["spear_hall"]));

        // And an id is declared once.
        Refused(Plus(ShippedContent.Dungeons.Dungeons[1]), "spear_hall", ContentValidator.RuleDuplicateId);
    }

    [Fact]
    public void ABossCarriesItsThemeInnately()
    {
        // Innate, whatever it wields (PHASE4-033): the golem's Block x3, and the
        // same depth on the four provisional bosses.
        foreach (var def in Dungeons.All)
            Assert.Equal(3, def.Boss.Innate[def.Theme]);

        Refused(With("spear_hall", d => WithInnate(d, (Block, 3))), "spear_hall", ContentValidator.RuleBossCarriesTheTheme);
    }

    [Fact]
    public void ABossHasAStatline_AndOnlyStackableInnates()
    {
        Refused(With("tutorial", d => d with { Boss = d.Boss with { MaxHp = 0 } }), "tutorial", ContentValidator.RuleBossStatline);
        Refused(With("tutorial", d => d with { Boss = d.Boss with { MovementBudget = 0 } }), "tutorial", ContentValidator.RuleBossStatline);
        Refused(With("tutorial", d => d with { Boss = null! }), "tutorial", ContentValidator.RuleBossStatline);

        // A stack of a member with no behaviour would do nothing, and a count below 1 is no stack.
        Refused(With("tutorial", d => WithInnate(d, (Block, 3), (Momentum, 1))), "tutorial", ContentValidator.RuleBossInnateStackable);
        Refused(With("tutorial", d => WithInnate(d, (Block, 3), (Brace, 0))), "tutorial", ContentValidator.RuleBossInnateStackable);

        // Innate stacks are content, bounded by nothing a weapon's forge bounds (A21):
        // a golem deeper than any forge is a golem, not an error.
        var granite = Load(With("tutorial", d => WithInnate(d, (Block, 7), (Brace, 2))));
        Assert.Equal(7, granite.Dungeons.Tutorial.Boss.Innate[Block]);
        Assert.True(7 > Rules.MaxForged(Block));
    }

    [Fact]
    public void AnAttunementIsAnElement()
    {
        // None is legal and every shipped dungeon has none: attunement is its own
        // field, independent of the modifier theme.
        Assert.All(Dungeons.All, d => Assert.Null(d.Attunement));

        var cold = Load(With("spear_hall", d => d with { Attunement = DamageType.Cold }));
        Assert.Equal(DamageType.Cold, cold.Dungeons["spear_hall"].Attunement);

        Refused(With("spear_hall", d => d with { Attunement = DamageType.None }), "spear_hall", ContentValidator.RuleAttunementIsAnElement);
        Refused(With("spear_hall", d => d with { Attunement = (DamageType)99 }), "spear_hall", ContentValidator.RuleAttunementIsAnElement);
    }

    [Fact]
    public void BlockStaysObtainableAfterTheTutorialCloses()
    {
        // A rushed tutorial costs a head start, never a closed build (PHASE4-085):
        // dungeons that stay open drop swords, and every sword is forged with Block.
        var elsewhere = Dungeons.All
            .Where(d => d != Dungeons.Tutorial && Dungeons.ThemeClasses(d.Theme).Contains(Sword))
            .Select(d => d.Id)
            .ToArray();
        Assert.Equal(new[] { "spear_hall", "long_gallery", "knife_warrens" }, elsewhere);
        Assert.All(DropVariants(Sword), sword => Assert.True(sword.Forged.GetValueOrDefault(Block) >= 1, sword.Id));
    }

    [Fact]
    public void TheNewKnobsAreTheDocsNumbers_AndAreRefusedWhenTheyCannotMeanAnything()
    {
        var tuning = GameContent.Current.Tuning;
        Assert.Equal(5, tuning.BossFloorMin);
        Assert.Equal(10, tuning.BossFloorMax);
        Assert.Equal(4, tuning.BossStackOffset);
        Assert.Equal(1, tuning.CooldownRunsPerFloor);
        Assert.Equal(3, tuning.MercyFloor);
        Assert.Equal(50, tuning.DeathXpLossPercent);
        Assert.Equal(24, tuning.PartyCarrySlots);
        Assert.Equal(3, tuning.StairGatherTiles);

        // What they add up to: a floor-5 boss pays one stack and a floor-10 six,
        // deliberately short of the farm's ten (F:139-165).
        Assert.Equal(1, tuning.BossFloorMin - tuning.BossStackOffset);
        Assert.Equal(6, tuning.BossFloorMax - tuning.BossStackOffset);
        Assert.True(tuning.BossFloorMax - tuning.BossStackOffset < 2 * tuning.AcquiredHeadroom);
        Assert.Equal(6, tuning.PartyCarrySlots / GameConstants.PartySize);

        // A range that starts below floor 1 or runs backwards has no roll to make.
        Rejected(t => t with { BossFloorMin = 0 }, nameof(Tuning.BossFloorMin), ContentValidator.RuleBossFloorRange);
        Rejected(t => t with { BossFloorMin = 8, BossFloorMax = 7 }, nameof(Tuning.BossFloorMax), ContentValidator.RuleBossFloorRange);

        // A stack offset and a lock length are counts.
        Rejected(t => t with { BossStackOffset = -1 }, nameof(Tuning.BossStackOffset), ContentValidator.RuleCountIsNeverNegative);
        Rejected(t => t with { CooldownRunsPerFloor = -1 }, nameof(Tuning.CooldownRunsPerFloor), ContentValidator.RuleCountIsNeverNegative);

        // The mercy floor is a floor number, and numbers start at 1.
        Rejected(t => t with { MercyFloor = 0 }, nameof(Tuning.MercyFloor), ContentValidator.RuleFloorNumberStartsAtOne);

        // The clawback is a share of a gain.
        Rejected(t => t with { DeathXpLossPercent = 101 }, nameof(Tuning.DeathXpLossPercent), ContentValidator.RuleShareIsAPercentage);
        Rejected(t => t with { DeathXpLossPercent = -1 }, nameof(Tuning.DeathXpLossPercent), ContentValidator.RuleShareIsAPercentage);

        // The carry divides among the members, a slot each at least, and no member
        // carries more slots than the number keys can equip.
        Rejected(t => t with { PartyCarrySlots = 25 }, nameof(Tuning.PartyCarrySlots), ContentValidator.RuleCarryFillsTheParty);
        Rejected(t => t with { PartyCarrySlots = 0 }, nameof(Tuning.PartyCarrySlots), ContentValidator.RuleCarryFillsTheParty);
        Rejected(t => t with { PartyCarrySlots = 40 }, nameof(Tuning.PartyCarrySlots), ContentValidator.RuleEverySlotHasAKey);

        // The stairs gather the party from a tile away at least.
        Rejected(t => t with { StairGatherTiles = 0 }, nameof(Tuning.StairGatherTiles), ContentValidator.RuleGatherTakesATile);

        // The ends of every range are choices, not errors: a one-floor range, no
        // offset, no lock, no clawback or all of it, one slot a member or nine, one tile.
        ContentValidator.ValidateTuning(ShippedContent.Tuning with
        {
            BossFloorMin = 1, BossFloorMax = 1, BossStackOffset = 0, CooldownRunsPerFloor = 0,
            MercyFloor = 1, DeathXpLossPercent = 0, PartyCarrySlots = GameConstants.PartySize, StairGatherTiles = 1,
        });
        ContentValidator.ValidateTuning(ShippedContent.Tuning with
        {
            DeathXpLossPercent = 100, PartyCarrySlots = GameConstants.PartySize * GameConstants.MaxInventorySlots,
        });
    }

    [Fact]
    public void ABossThatWouldPayAsMuchAsTheFarmIsRefused()
    {
        // PHASE4-045: the most a boss pays is six, deliberately short of the ten a
        // well-farmed two-modifier weapon banks - "never raise the boss cap to 10".
        // A boss that paid as much would leave the farm no longer the only route to
        // a finished weapon, so a load holds every tuning and every dungeon to it.
        int farm = ContentValidator.FarmBanks(GameContent.Current.Tuning.AcquiredHeadroom);
        Assert.Equal(10, farm);
        Assert.Equal(6, Dungeons.MostStacksPaid(Dungeons["spear_hall"]));
        Assert.Equal(0, Dungeons.MostStacksPaid(Dungeons.Tutorial));
        Assert.All(Dungeons.All, d => Assert.True(Dungeons.MostStacksPaid(d) < farm, d.Id));

        // Deeper floors, a smaller offset or a shallower farm: each brings the
        // deepest boss level with the farm, and each is refused at the key that
        // prices a floor.
        Rejected(t => t with { BossFloorMax = 14 }, nameof(Tuning.BossStackOffset), ContentValidator.RuleBossPaysLessThanTheFarm);
        Rejected(t => t with { BossFloorMax = 16 }, nameof(Tuning.BossStackOffset), ContentValidator.RuleBossPaysLessThanTheFarm);
        Rejected(t => t with { BossStackOffset = 0 }, nameof(Tuning.BossStackOffset), ContentValidator.RuleBossPaysLessThanTheFarm);
        Rejected(t => t with { AcquiredHeadroom = 3 }, nameof(Tuning.BossStackOffset), ContentValidator.RuleBossPaysLessThanTheFarm);

        // One short of the farm is a choice, not an error: nine stacks against ten,
        // and a deeper dungeon that moves its offset with it.
        ContentValidator.ValidateTuning(ShippedContent.Tuning with { BossFloorMax = 13 });
        ContentValidator.ValidateTuning(ShippedContent.Tuning with { BossStackOffset = 1 });
        ContentValidator.ValidateTuning(ShippedContent.Tuning with { BossFloorMax = 16, BossStackOffset = 7 });

        // A dungeon that pins a deeper range of its own is held to it the same way.
        Refused(With("spear_hall", d => d with { BossFloorMax = 14 }), "spear_hall", ContentValidator.RuleBossPaysLessThanTheFarm);
        var deep = Load(With("spear_hall", d => d with { BossFloorMax = 13 }));
        Assert.Equal(9, deep.Dungeons.MostStacksPaid(deep.Dungeons["spear_hall"]));
    }

    [Fact]
    public void CanGraftIsTheRelationsPlusTheForgeLimit()
    {
        // A forged Light graft past x1 is refused; the same graft acquired is not
        // (PHASE4-036: one question, the source's choice the only difference).
        var knife = Spread(Weapons["flensing_knife"]);
        Assert.Equal(1, knife.Stacks(Light));
        Assert.Equal(1, Rules.MaxForged(Light));
        Assert.False(Rules.CanGraft(Light, WeaponKind.Melee, knife, knife, asForged: true));
        Assert.True(Rules.CanGraft(Light, WeaponKind.Melee, knife, knife, asForged: false));

        // The forge limit counts the forged spread, never what was acquired on top:
        // a Tower Guard farmed to Block x5 still takes one forged stack.
        var farmed = ModifierSet.Of((Block, 5), (Push, 1));
        var forged = ModifierSet.Of((Block, 2), (Push, 1));
        Assert.True(Rules.CanGraft(Block, WeaponKind.Melee, farmed, forged, asForged: true));

        // And the relations bind a forged graft as they bind every source: Light
        // beside a caster's Resonant is refused either way.
        var staff = Spread(Weapons["staff_of_renewal"]);
        Assert.False(Rules.CanGraft(Light, WeaponKind.Caster, staff, staff, asForged: true));
        Assert.False(Rules.CanGraft(Light, WeaponKind.Caster, staff, staff, asForged: false));

        // Over every variant and every modifier: asked acquired it is Allowed and
        // nothing else; asked forged it is Allowed plus the forge limit.
        foreach (var def in Weapons.All.Where(d => !d.Unique))
        {
            var spread = Spread(def);
            var kind = Weapon.KindOf(def.Class);
            foreach (var t in Enum.GetValues<ModifierType>())
            {
                Assert.Equal(Rules.Allowed(t, kind, spread), Rules.CanGraft(t, kind, spread, spread, asForged: false));
                Assert.Equal(Rules.Allowed(t, kind, spread) && spread.Stacks(t) < Rules.MaxForged(t),
                    Rules.CanGraft(t, kind, spread, spread, asForged: true));
            }
        }
    }

    private static void Rejected(Func<Tuning, Tuning> edit, string key, string rule)
    {
        var ex = Assert.Throws<ContentException>(() => ContentValidator.ValidateTuning(edit(ShippedContent.Tuning)));
        Assert.Equal((key, rule), (ex.EntryId, ex.Rule));
        Assert.Contains(ex.Rule, ex.Message);

        // And the load refuses it the same way, leaving the running content alone.
        var before = GameContent.Current;
        var loaded = Assert.Throws<ContentException>(() => TestContent.Use(tuning: edit(ShippedContent.Tuning)));
        Assert.Equal((key, rule), (loaded.EntryId, loaded.Rule));
        Assert.Same(before, GameContent.Current);
    }
}
