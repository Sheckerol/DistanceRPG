using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The thirty-two weapons and the innate enchantments as content: the
/// validator's checks, the statlines, the resolved costs, and the catalogue's
/// lookups by stable id. In the content collection because one test swaps the
/// current content.
/// </summary>
[Collection(TestContent.Collection)]
public class WeaponContentTests
{
    private static WeaponCatalogue Catalogue => GameContent.Current.Weapons;
    private static ModifierRules Rules => GameContent.Current.Modifiers;

    private static readonly WeaponClass[] MartialClasses =
        [WeaponClass.Dagger, WeaponClass.Sword, WeaponClass.Spear, WeaponClass.Axe, WeaponClass.Ranged, WeaponClass.Throwing];

    private static readonly DamageType[] Elements = [DamageType.Flaming, DamageType.Cold, DamageType.Shocking, DamageType.Acidic];

    /// <summary>An instance of any catalogue entry: a wand variant gets an element so it can be built at all; a wand unique fixes its own.</summary>
    private static Weapon Any(WeaponDef def)
        => TestWeapons.Get(def.Id, def.Class == WeaponClass.Wand && def.Enchantments.Count == 0 ? DamageType.Flaming : null);

    [Fact]
    public void DefaultContent_LoadsThirtyTwoWeapons_AndEightInnates()
    {
        // The uniques and the souls load beside these (§1.5); UniqueContentTests counts them.
        var innates = GameContent.Current.Enchantments.All
            .Where(e => e.Effect is EffectKind.ApplyStatus or EffectKind.ElementalDamage).ToList();
        Assert.Equal(32, Catalogue.All.Count(w => !w.Unique));
        Assert.Equal(8, innates.Count);
        Assert.Equal(
            new[] { "regeneration", "ward", "poison", "mire", "flaming", "cold", "shocking", "acidic" },
            innates.Select(e => e.Id));
        Assert.Equal(GameContent.Current.Enchantments.Ids, ContentDefaults.EnchantmentIds);
        Assert.Equal(Catalogue.All.Count, Catalogue.All.Select(w => w.Id).Distinct().Count());
    }

    [Fact]
    public void AllThirtyTwo_ForgedSpreadsAreAllowed_AndUnderMaxForged()
    {
        foreach (var def in Catalogue.All)
        {
            var weapon = Any(def);
            var spread = weapon.Forged;
            Assert.Equal(spread, weapon.Modifiers);   // nothing acquired at instantiation
            Assert.NotEmpty(spread.Entries);
            foreach (var (t, n) in spread.Entries)
            {
                Assert.True(Rules.Allowed(t, weapon.Kind, spread), $"{def.Id}: {t} x{n} is not allowed beside {spread}");
                Assert.True(n <= Rules.MaxForged(t), $"{def.Id}: {t} x{n} is forged past {Rules.MaxForged(t)}");
            }
        }

        // The third assertion the code doc asks for beside these: Excludes is symmetric after construction.
        foreach (var (a, others) in Rules.Excludes)
            foreach (var b in others)
                Assert.Contains(a, Rules.Excludes[b]);
    }

    [Fact]
    public void EveryWeapon_HasAtLeastTwoForgedAxes()
    {
        foreach (var def in Catalogue.All)
        {
            int types = def.Forged.Count(f => f.Value > 0);
            if (Weapon.KindOf(def.Class) == WeaponKind.Caster)
            {
                // A caster's second axis is its innate enchantment, not a stack: Resonant x1 and nothing else forged.
                Assert.Equal(1, types);
                Assert.Equal(1, def.Forged[Resonant]);
                if (!def.Unique)
                    Assert.Equal(def.Class == WeaponClass.Staff ? 1 : 0, def.Enchantments.Count);   // a unique's enchantments are its artifact (§1.5)
            }
            else
            {
                Assert.True(types >= 2, $"{def.Id} is forged on {types} modifier type(s)");
                if (!def.Unique)
                    Assert.Empty(def.Enchantments);   // a unique carries its soul
            }
        }
    }

    [Fact]
    public void EveryClass_HasFourVariants_OnePerRole()
    {
        Assert.Equal(8, Enum.GetValues<WeaponClass>().Length);
        foreach (var cls in MartialClasses)
        {
            var variants = Catalogue.ByClass(cls);
            Assert.Equal(4, variants.Count);
            Assert.Equal(Enum.GetValues<VariantRole>(), variants.Select(v => v.Role!.Value));   // one per role, in role order
            Assert.All(variants, v => Assert.False(v.Unique));
            foreach (var role in Enum.GetValues<VariantRole>())
                Assert.Equal(role, Catalogue.Variant(cls, role).Role);
        }

        // Casters vary by effect and by shape rather than by role: four effects, four shapes.
        var staves = Catalogue.ByClass(WeaponClass.Staff);
        var wands = Catalogue.ByClass(WeaponClass.Wand);
        Assert.Equal(4, staves.Count);
        Assert.Equal(4, wands.Count);
        Assert.All(staves.Concat(wands), v => Assert.Null(v.Role));
        Assert.Equal(4, staves.Select(s => s.Enchantments.Single().Id).Distinct().Count());
        Assert.Equal(Enum.GetValues<AreaShapeKind>(), wands.Select(w => w.Shape!.Kind));
    }

    [Fact]
    public void Variant_IsBaselinePlusExactlyOneModifierType()
    {
        var baselines = new Dictionary<WeaponClass, (ModifierType Signature, ModifierType Second)>
        {
            [WeaponClass.Dagger] = (CritWindow, CritMultiplier),
            [WeaponClass.Sword] = (Block, Push),
            [WeaponClass.Spear] = (Brace, Longshot),
            [WeaponClass.Axe] = (Cleave, Opportunist),
            [WeaponClass.Ranged] = (Longshot, CritWindow),
            [WeaponClass.Throwing] = (Charges, CritMultiplier),
        };

        foreach (var (cls, (signature, second)) in baselines)
        {
            foreach (var def in Catalogue.ByClass(cls))
            {
                var spread = TestWeapons.Get(def.Id).Forged;
                Assert.True(spread.Stacks(signature) >= 1, $"{def.Id} lacks its signature {signature}");
                Assert.True(spread.Stacks(second) >= 1, $"{def.Id} lacks its second {second}");

                // Whatever sits above the baseline is the variant's own addition: exactly one modifier type.
                var extra = spread.Entries
                    .Select(e => (e.Type, Extra: e.Stacks - (e.Type == signature || e.Type == second ? 1 : 0)))
                    .Where(e => e.Extra > 0)
                    .ToList();
                var (added, stacks) = Assert.Single(extra);
                switch (def.Role)
                {
                    case VariantRole.Efficiency:
                        Assert.Equal((Light, 1), (added, stacks));
                        break;
                    case VariantRole.Purity:
                        Assert.Equal((signature, 1), (added, stacks));   // the signature again
                        break;
                    default:
                        Assert.NotEqual(signature, added);
                        Assert.NotEqual(second, added);
                        break;
                }
            }
        }

        // The one spread with a x2 addition: the Reaver forges Splitting x2 (settled).
        Assert.Equal(2, TestWeapons.Get("reaver").Stacks(Splitting));
    }

    [Fact]
    public void Casters_CarryExactlyOneInnate_StaffFixed_WandElementFromCaller()
    {
        foreach (var def in Catalogue.ByClass(WeaponClass.Staff))
        {
            var staff = TestWeapons.Get(def.Id);
            var innate = Assert.Single(staff.Enchantments);
            Assert.Same(innate, staff.Innate);
            Assert.Equal(EffectKind.ApplyStatus, innate.Def.Effect);
            Assert.NotNull(innate.Def.Applies);
            Assert.Null(innate.Def.Trigger);   // a status applier prices its trigger off its levels
            Assert.Equal(1, innate.Tier);
            Assert.Throws<ArgumentException>(() => TestWeapons.Get(def.Id, DamageType.Flaming));   // a staff takes no element
        }
        Assert.Equal(("regeneration", StatusEffectType.Regeneration, TargetSide.Ally, 1), StaffInnate("staff_of_renewal"));
        Assert.Equal(("ward", StatusEffectType.Ward, TargetSide.Ally, 5), StaffInnate("staff_of_warding"));
        Assert.Equal(("poison", StatusEffectType.Poison, TargetSide.Enemy, 3), StaffInnate("staff_of_blight"));
        Assert.Equal(("mire", StatusEffectType.Mire, TargetSide.Enemy, 2), StaffInnate("staff_of_mire"));

        foreach (var def in Catalogue.ByClass(WeaponClass.Wand))
        {
            Assert.Empty(def.Enchantments);   // the element is rolled with the drop, so the entry lists none
            Assert.Throws<ArgumentException>(() => TestWeapons.Get(def.Id));
            Assert.Throws<ArgumentException>(() => TestWeapons.Get(def.Id, DamageType.None));
            foreach (var element in Elements)
            {
                var wand = TestWeapons.Get(def.Id, element);
                var innate = Assert.Single(wand.Enchantments);
                Assert.Same(innate, wand.Innate);
                Assert.Equal(EffectKind.ElementalDamage, innate.Def.Effect);
                Assert.Equal(element, innate.Def.DamageType);
                Assert.Equal(element.ToString().ToLowerInvariant(), innate.Id);
                Assert.Null(innate.Def.Applies);
                Assert.Equal(5, innate.Def.Trigger);
                Assert.Equal(TargetSide.Enemy, innate.Def.Targets);
            }
        }

        // A martial weapon carries no innate and takes no element.
        Assert.Null(TestWeapons.Get("great_axe").Innate);
        Assert.Throws<ArgumentException>(() => TestWeapons.Get("great_axe", DamageType.Cold));

        static (string Id, StatusEffectType Applies, TargetSide Targets, int Levels) StaffInnate(string id)
        {
            var innate = TestWeapons.Get(id).Innate!;
            return (innate.Id, innate.Def.Applies!.Value, innate.Def.Targets, innate.LevelsFor(innate.Def.Potency));
        }
    }

    [Theory]
    [InlineData("flensing_knife", "Flensing Knife", WeaponClass.Dagger, 40, 15, 30, 0, "CritWindow x1, CritMultiplier x1, Light x1")]
    [InlineData("assassins_fang", "Assassin's Fang", WeaponClass.Dagger, 40, 15, 30, 0, "CritWindow x2, CritMultiplier x1")]
    [InlineData("disarming_kris", "Disarming Kris", WeaponClass.Dagger, 40, 15, 30, 0, "CritWindow x1, CritMultiplier x1, CritWeaken x1")]
    [InlineData("weakspot_stiletto", "Weakspot Stiletto", WeaponClass.Dagger, 40, 15, 30, 0, "CritWindow x1, CritMultiplier x1, CritSunder x1")]
    [InlineData("arming_sword", "Arming Sword", WeaponClass.Sword, 80, 10, 50, 0, "Block x1, Light x1, Push x1")]
    [InlineData("tower_guard", "Tower Guard", WeaponClass.Sword, 80, 10, 50, 0, "Block x2, Push x1")]
    [InlineData("riposte_blade", "Riposte Blade", WeaponClass.Sword, 80, 10, 50, 0, "Block x1, Riposte x1, Push x1")]
    [InlineData("wardens_shield", "Warden's Shield", WeaponClass.Sword, 80, 10, 50, 0, "Block x1, Push x1, BlockWeaken x1")]
    [InlineData("skirmishers_pike", "Skirmisher's Pike", WeaponClass.Spear, 128, 7, 55, 0, "Brace x1, Longshot x1, Light x1")]
    [InlineData("phalanx_spear", "Phalanx Spear", WeaponClass.Spear, 128, 7, 55, 0, "Brace x2, Longshot x1")]
    [InlineData("halberd", "Halberd", WeaponClass.Spear, 128, 7, 55, 0, "Brace x1, Longshot x1, Push x1")]
    [InlineData("pinning_lance", "Pinning Lance", WeaponClass.Spear, 128, 7, 55, 0, "Brace x1, Longshot x1, Pin x1")]
    [InlineData("hatchet", "Hatchet", WeaponClass.Axe, 60, 18, 60, 0, "Cleave x1, Light x1, Opportunist x1")]
    [InlineData("great_axe", "Great Axe", WeaponClass.Axe, 60, 18, 60, 0, "Cleave x2, Opportunist x1")]
    [InlineData("reaver", "Reaver", WeaponClass.Axe, 60, 18, 60, 0, "Cleave x1, Splitting x2, Opportunist x1")]
    [InlineData("routing_axe", "Routing Axe", WeaponClass.Axe, 60, 18, 60, 0, "Cleave x1, Opportunist x1, Rout x1")]
    [InlineData("hunting_bow", "Hunting Bow", WeaponClass.Ranged, 320, 5, 30, 0, "CritWindow x1, Longshot x1, Light x1")]
    [InlineData("longbow", "Longbow", WeaponClass.Ranged, 320, 5, 30, 0, "CritWindow x1, Longshot x2")]
    [InlineData("pinning_bow", "Pinning Bow", WeaponClass.Ranged, 320, 5, 30, 0, "CritWindow x1, Longshot x1, Pin x1")]
    [InlineData("crossbow", "Crossbow", WeaponClass.Ranged, 320, 5, 30, 0, "CritWindow x1, Longshot x1, Overwatch x1")]
    [InlineData("darts", "Darts", WeaponClass.Throwing, 190, 9, 15, 0, "CritMultiplier x1, Charges x1, Light x1")]
    [InlineData("bandolier", "Bandolier", WeaponClass.Throwing, 190, 9, 15, 0, "CritMultiplier x1, Charges x2")]
    [InlineData("harpoon", "Harpoon", WeaponClass.Throwing, 190, 9, 15, 0, "CritMultiplier x1, Charges x1, Drag x1")]
    [InlineData("softening_javelins", "Softening Javelins", WeaponClass.Throwing, 190, 9, 15, 0, "CritMultiplier x1, Charges x1, Softening x1")]
    [InlineData("staff_of_renewal", "Staff of Renewal", WeaponClass.Staff, 100, 0, 40, 15, "Resonant x1")]
    [InlineData("staff_of_warding", "Staff of Warding", WeaponClass.Staff, 100, 0, 40, 20, "Resonant x1")]
    [InlineData("staff_of_blight", "Staff of Blight", WeaponClass.Staff, 100, 0, 40, 20, "Resonant x1")]
    [InlineData("staff_of_mire", "Staff of Mire", WeaponClass.Staff, 100, 0, 40, 25, "Resonant x1")]
    [InlineData("wand_of_the_blast", "Wand of the Blast", WeaponClass.Wand, 160, 8, 45, 20, "Resonant x1")]
    [InlineData("wand_of_the_cone", "Wand of the Cone", WeaponClass.Wand, 160, 8, 45, 20, "Resonant x1")]
    [InlineData("wand_of_the_beam", "Wand of the Beam", WeaponClass.Wand, 160, 8, 45, 20, "Resonant x1")]
    [InlineData("wand_of_the_nova", "Wand of the Nova", WeaponClass.Wand, 160, 8, 45, 20, "Resonant x1")]
    public void Statlines_MatchTheTables(string id, string name, WeaponClass cls, int range, int damage, int cost, int manaCost, string forged)
    {
        var def = Catalogue[id];
        Assert.Equal(name, def.Name);
        Assert.Equal(cls, def.Class);
        Assert.False(def.Unique);
        Assert.Null(def.DerivedFrom);

        var weapon = Any(def);
        Assert.Equal((id, name, cls), (weapon.Id, weapon.Name, weapon.Class));
        Assert.Equal((range, damage, cost, manaCost), (weapon.Range, weapon.Damage, weapon.Cost, weapon.ManaCost));
        Assert.Equal(forged, weapon.Forged.ToString());   // in ModifierType order
        Assert.Equal(Weapon.KindOf(cls), weapon.Kind);
        Assert.Equal(cls is WeaponClass.Staff or WeaponClass.Wand, weapon.IsCaster);
    }

    [Theory]
    [InlineData("wand_of_the_blast", AreaShapeKind.Blast, 48, 0, 0, 0, 160)]
    [InlineData("wand_of_the_cone", AreaShapeKind.Cone, 0, 128, 0, 90, 0)]
    [InlineData("wand_of_the_beam", AreaShapeKind.Beam, 0, 224, 32, 0, 0)]
    [InlineData("wand_of_the_nova", AreaShapeKind.Nova, 96, 0, 0, 0, 0)]
    public void WandShapes_MatchTheTable(string id, AreaShapeKind kind, int radius, int length, int width, int angle, int reach)
    {
        var shape = TestWeapons.Get(id, DamageType.Shocking).AreaShape;
        Assert.Equal(new AreaShape(kind, radius, length, width, angle, reach), shape);
        Assert.Null(TestWeapons.Get("staff_of_renewal").AreaShape);
        Assert.Null(TestWeapons.Get("longbow").AreaShape);
    }

    [Fact]
    public void ResolvedCost_TruncatesLightDiscount()
    {
        Assert.Equal(27, TestWeapons.Get("flensing_knife").ResolvedCost);    // 30 x 90 / 100
        Assert.Equal(49, TestWeapons.Get("skirmishers_pike").ResolvedCost);  // 55 x 90 / 100 = 49.5, truncated
        Assert.Equal(13, TestWeapons.Get("darts").ResolvedCost);             // 15 x 90 / 100 = 13.5
        Assert.Equal(45, TestWeapons.Get("arming_sword").ResolvedCost);
        Assert.Equal(30, TestWeapons.Get("weakspot_stiletto").ResolvedCost); // no Light: the base cost
        Assert.Equal(40, TestWeapons.Get("staff_of_renewal").ResolvedCost);

        // Forged x1 plus the whole acquired headroom is x6, a 60% discount: 30 -> 12.
        var knife = TestWeapons.Get("flensing_knife");
        knife.Acquire(Light, 5);
        Assert.Equal(6, knife.Stacks(Light));
        Assert.Equal(12, knife.ResolvedCost);
        Assert.Equal(1, knife.Forged.Stacks(Light));   // the forged spread never moves

        // The same x6 on the axe: 60 x 40 / 100 = 24, the modifiers doc's own example.
        var hatchet = TestWeapons.Get("hatchet");
        hatchet.Acquire(Light, 5);
        Assert.Equal(6, hatchet.Stacks(Light));
        Assert.Equal(24, hatchet.ResolvedCost);

        // A maxed Light x5 halves any weapon: dagger 30 -> 15, sword 50 -> 25, axe 60 -> 30.
        foreach (var (id, halved) in new[] { ("weakspot_stiletto", 15), ("tower_guard", 25), ("great_axe", 30) })
        {
            var weapon = TestWeapons.Get(id);
            weapon.Acquire(Light, 5);
            Assert.Equal(5, weapon.Stacks(Light));
            Assert.Equal(halved, weapon.ResolvedCost);
        }
    }

    [Fact]
    public void ResolvedManaCost_TruncatesResonant()
    {
        var mire = TestWeapons.Get("staff_of_mire");
        Assert.Equal(25, mire.ManaCost);
        Assert.Equal(22, mire.ResolvedManaCost);   // x1: 25 x 90 / 100 = 22.5
        mire.Acquire(Resonant, 4);
        Assert.Equal(12, mire.ResolvedManaCost);   // x5: half
        mire.Acquire(Resonant, 1);
        Assert.Equal(6, mire.Stacks(Resonant));
        Assert.Equal(10, mire.ResolvedManaCost);   // x6, the ceiling
        mire.Acquire(Resonant, 1);
        Assert.Equal(6, mire.Stacks(Resonant));    // forged x1 plus five acquired is all the headroom there is
        Assert.Equal(10, mire.ResolvedManaCost);

        Assert.Equal(13, TestWeapons.Get("staff_of_renewal").ResolvedManaCost);                      // 15 -> 13.5
        Assert.Equal(18, TestWeapons.Get("wand_of_the_nova", DamageType.Cold).ResolvedManaCost);    // 20 -> 18: 10% a stack, not "20% off"
        var base20 = TestWeapons.Make("Base staff", 100, 0, 40, manaCost: 20, WeaponClass.Staff, (Resonant, 1));
        Assert.Equal(18, base20.ResolvedManaCost);
        base20.Acquire(Resonant, 5);
        Assert.Equal(8, base20.ResolvedManaCost);   // x6
        Assert.Equal(0, TestWeapons.Get("great_axe").ResolvedManaCost);
    }

    [Fact]
    public void Acquire_ClampsAtForgedPlusFive_AndRefreshesResolvedCost()
    {
        var fang = TestWeapons.Get("assassins_fang");   // CritWindow x2 forged
        fang.Acquire(CritWindow, 10);
        Assert.Equal(7, fang.Stacks(CritWindow));        // 2 + 5
        Assert.Equal(2, fang.Forged.Stacks(CritWindow));
        Assert.Equal(13, CombatRules.CritThreshold(fang));
        fang.Acquire(Block, 9);
        Assert.Equal(5, fang.Stacks(Block));             // never forged: 5
        Assert.Equal(30, fang.ResolvedCost);             // nothing but Light moves it

        var knife = TestWeapons.Get("flensing_knife");
        Assert.Equal(27, knife.ResolvedCost);
        knife.Acquire(Light, 1);
        Assert.Equal(24, knife.ResolvedCost);            // refreshed on acquisition, never per swing
        Assert.Throws<ArgumentOutOfRangeException>(() => knife.Acquire(Light, -1));

        // Two instances never share stacks: the catalogue hands out one item per call.
        var a = TestWeapons.Get("hatchet");
        var b = TestWeapons.Get("hatchet");
        Assert.NotSame(a, b);
        a.Acquire(Cleave, 2);
        Assert.Equal(3, a.Stacks(Cleave));
        Assert.Equal(1, b.Stacks(Cleave));
    }

    [Fact]
    public void Acquire_RefusesWhatTheRelationsRefuse_WhateverTheSource()
    {
        // The relations bind every source alike (§1.1), and Acquire is the one path every acquired stack
        // takes, so it asks the predicate a roll gates its offers on: a refused stack throws and the weapon is
        // untouched. Nothing ever gives a bow its first Charges, a Brace, or a member outside the table.
        var bow = TestWeapons.Get("longbow");
        Assert.Throws<InvalidOperationException>(() => bow.Acquire(Charges, 1));       // forged only: deepened, never granted
        Assert.Throws<InvalidOperationException>(() => bow.Acquire(Brace, 1));         // melee only
        Assert.Throws<InvalidOperationException>(() => bow.Acquire(Momentum, 1));      // an enchantment, never a stack
        Assert.Throws<InvalidOperationException>(() => bow.Acquire(OnHitPoison, 1));   // reserved: no behaviour
        Assert.Equal(bow.Forged, bow.Modifiers);
        Assert.Equal(30, bow.ResolvedCost);

        // One displacement direction: a sword that pushes is never given a pull, nor a Rout (which wants a Cleave besides).
        var guard = TestWeapons.Get("tower_guard");
        Assert.Throws<InvalidOperationException>(() => guard.Acquire(Drag, 1));
        Assert.Throws<InvalidOperationException>(() => guard.Acquire(Rout, 1));
        Assert.Equal(guard.Forged, guard.Modifiers);

        // Deepening what a weapon holds passes — the Bandolier's forged Charges — and a prerequisite, once
        // grafted, opens what it gates: a dagger is never offered Riposte until it holds a Block.
        var bandolier = TestWeapons.Get("bandolier");
        bandolier.Acquire(Charges, 1);
        Assert.Equal(3, bandolier.Stacks(Charges));
        var fang = TestWeapons.Get("assassins_fang");
        Assert.Throws<InvalidOperationException>(() => fang.Acquire(Riposte, 1));
        fang.Acquire(Block, 1);
        fang.Acquire(Riposte, 1);
        Assert.Equal((1, 1), (fang.Stacks(Block), fang.Stacks(Riposte)));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 19)]
    [InlineData(2, 18)]
    [InlineData(3, 17)]
    [InlineData(6, 14)]
    [InlineData(8, 12)]
    public void CritWindowLadder(int stacks, int threshold)
    {
        var actor = TestPools.Char("A");
        actor.Innate = ModifierSet.Of((CritWindow, stacks));
        Assert.Equal(threshold, CombatRules.CritThreshold(actor));
    }

    [Fact]
    public void CritWindow_IsForgedOnDaggersAndBowsOnly_CritMultiplierOnDaggersAndThrowing()
    {
        Assert.Equal(19, CombatRules.CritThreshold(TestWeapons.Get("weakspot_stiletto")));
        Assert.Equal(18, CombatRules.CritThreshold(TestWeapons.Get("assassins_fang")));
        Assert.Equal(19, CombatRules.CritThreshold(TestWeapons.Get("hunting_bow")));
        Assert.Equal(19, CombatRules.CritThreshold(TestWeapons.Get("longbow")));   // Purity deepens Longshot, not the window: the variant table wins over the stale ladder row
        foreach (var def in Catalogue.All.Where(d => d.Class is not (WeaponClass.Dagger or WeaponClass.Ranged)))
            Assert.Equal(20, CombatRules.CritThreshold(Any(def)));

        foreach (var def in Catalogue.All)
        {
            int expected = def.Class is WeaponClass.Dagger or WeaponClass.Throwing ? 3 : 2;
            Assert.Equal(expected, Any(def).Modifiers.Value(CritMultiplier));
        }
    }

    [Fact]
    public void Catalogue_LooksUpByStableId_NeverByIndex()
    {
        Assert.Equal("Tower Guard", Catalogue["tower_guard"].Name);
        Assert.True(Catalogue.TryGet("harpoon", out var harpoon));
        Assert.Equal(VariantRole.Control, harpoon.Role);
        Assert.False(Catalogue.TryGet("nope", out _));
        Assert.Throws<KeyNotFoundException>(() => Catalogue["nope"]);
        Assert.Throws<KeyNotFoundException>(() => TestWeapons.Get("nope"));
        Assert.Equal("tower_guard", Catalogue.Variant(WeaponClass.Sword, VariantRole.Purity).Id);
        Assert.Equal(
            new[] { "flensing_knife", "assassins_fang", "disarming_kris", "weakspot_stiletto" },
            Catalogue.ByClass(WeaponClass.Dagger).Select(d => d.Id));
        Assert.Throws<KeyNotFoundException>(() => Catalogue.Variant(WeaponClass.Staff, VariantRole.Purity));

        Assert.Equal("Regeneration", GameContent.Current.Enchantments["regeneration"].Name);
        Assert.Equal("cold", GameContent.Current.Enchantments.InnateFor(DamageType.Cold).Id);
        Assert.Throws<KeyNotFoundException>(() => GameContent.Current.Enchantments.InnateFor(DamageType.None));
    }

    [Fact]
    public void Enchantment_LevelsAndTriggerCosts_FollowTheLadder()
    {
        var staff = TestWeapons.Get("staff_of_renewal");
        var regeneration = staff.Innate!;
        Assert.Equal(1, regeneration.LevelsFor(1));
        Assert.Equal(1, regeneration.TriggerCostFor(1));   // 1 x 2 / 2 / 3 = 0, floored at 1: no enchantment fires free
        Assert.Equal(5, regeneration.TriggerCostFor(5));   // 15 / 3
        Assert.Equal(1, regeneration.ResolvedTriggerCost(staff, 1));
        Assert.Equal(4, regeneration.ResolvedTriggerCost(staff, 5));   // 5 x 90 / 100 = 4.5

        var poison = GameContent.Current.Enchantments["poison"];
        Assert.Equal(3, new Enchantment(poison, 1).LevelsFor(poison.Potency));
        Assert.Equal(6, new Enchantment(poison, 2).LevelsFor(poison.Potency));           // tier scales the effect
        Assert.Equal(3, new Enchantment(poison with { Unique = true }, 2).LevelsFor(3));   // a unique reads the source alone

        var wand = TestWeapons.Get("wand_of_the_beam", DamageType.Flaming);
        var flaming = wand.Innate!;
        Assert.Equal(5, flaming.TriggerCostFor(0));          // a flat trigger, whatever the levels
        Assert.Equal(4, flaming.ResolvedTriggerCost(wand, 0));
        Assert.Equal(20, flaming.Def.ApplyPercent);          // the tuning table's percentage
        Assert.Equal(15, TestWeapons.Get("wand_of_the_beam", DamageType.Cold).Innate!.Def.ApplyPercent);
    }

    [Fact]
    public void Validator_RefusesIllegalWeapons_NamingEntryAndRule()
    {
        // Past MaxForged: Light x2, and Resonant x2 on a staff, caught before the caster check reads the spread.
        var ex = LoadWith("flensing_knife", w => w with { Forged = Spread((CritWindow, 1), (CritMultiplier, 1), (Light, 2)) });
        Assert.Equal(("flensing_knife", ContentValidator.RuleMaxForged), (ex.EntryId, ex.Rule));
        ex = LoadWith("staff_of_mire", w => w with { Forged = Spread((Resonant, 2)) });
        Assert.Equal(("staff_of_mire", ContentValidator.RuleMaxForged), (ex.EntryId, ex.Rule));

        // A zero or negative count is a content error naming the entry, never an argument error out of ModifierSet.Of.
        ex = LoadWith("flensing_knife", w => w with { Forged = Spread((CritWindow, 1), (CritMultiplier, 1), (Light, -1)) });
        Assert.Equal(("flensing_knife", ContentValidator.RuleForgedStackCount), (ex.EntryId, ex.Rule));
        Assert.Contains("Light x-1", ex.Message);
        ex = LoadWith("darts", w => w with { Forged = Spread((CritMultiplier, 1), (Charges, 1), (Light, 0)) });
        Assert.Equal(("darts", ContentValidator.RuleForgedStackCount), (ex.EntryId, ex.Rule));

        // The wrong kind: Brace on a bow.
        ex = LoadWith("crossbow", w => w with { Forged = Spread((Longshot, 1), (CritWindow, 1), (Brace, 1)) });
        Assert.Equal(("crossbow", ContentValidator.RuleForgedNotAllowed), (ex.EntryId, ex.Rule));

        // Two reactions: a class baseline quietly giving someone Brace beside Opportunist.
        ex = LoadWith("routing_axe", w => w with { Forged = Spread((Cleave, 1), (Opportunist, 1), (Brace, 1)) });
        Assert.Equal(("routing_axe", ContentValidator.RuleForgedNotAllowed), (ex.EntryId, ex.Rule));

        // A member outside the modifier table, on any weapon: the Shieldbreaker carries Momentum as its soul and
        // never as a stack, and OnHitPoison is reserved. A stack of either would change nothing.
        ex = LoadWith("shieldbreaker", w => w with { Forged = Spread((Cleave, 1), (Opportunist, 1), (Splitting, 3), (Momentum, 1)) });
        Assert.Equal(("shieldbreaker", ContentValidator.RuleForgedNotAllowed), (ex.EntryId, ex.Rule));
        Assert.Contains("Momentum x1: not in the modifier table", ex.Message);
        ex = LoadWith("crossbow", w => w with { Forged = Spread((Longshot, 1), (CritWindow, 1), (Overwatch, 1), (OnHitPoison, 1)) });
        Assert.Equal(("crossbow", ContentValidator.RuleForgedNotAllowed), (ex.EntryId, ex.Rule));

        // One forged axis.
        ex = LoadWith("hatchet", w => w with { Forged = Spread((Cleave, 3)) });
        Assert.Equal(("hatchet", ContentValidator.RuleTwoForgedAxes), (ex.EntryId, ex.Rule));

        // A variant adding two modifier types, or the wrong one for its role.
        ex = LoadWith("hatchet", w => w with { Forged = Spread((Cleave, 1), (Opportunist, 1), (Light, 1), (Pin, 1)) });
        Assert.Equal(("hatchet", ContentValidator.RuleVariantDelta), (ex.EntryId, ex.Rule));
        ex = LoadWith("hatchet", w => w with { Forged = Spread((Cleave, 1), (Opportunist, 1), (Pin, 1)) });
        Assert.Equal(("hatchet", ContentValidator.RuleEfficiencyAddsLight), (ex.EntryId, ex.Rule));
        ex = LoadWith("great_axe", w => w with { Forged = Spread((Cleave, 1), (Opportunist, 1), (Splitting, 1)) });
        Assert.Equal(("great_axe", ContentValidator.RulePurityDeepensBaseline), (ex.EntryId, ex.Rule));
        ex = LoadWith("tower_guard", w => w with { Forged = Spread((Block, 1), (Push, 2)) });   // deepens the second axis, not the signature
        Assert.Equal(("tower_guard", ContentValidator.RulePurityDeepensBaseline), (ex.EntryId, ex.Rule));
        Assert.Contains("signature Block", ex.Message);

        // A class whose baseline lost its signature: every dagger forged Block where CritWindow was.
        var bludgeons = new WeaponsData(ContentDefaults.Weapons.Weapons
            .Select(w => w.Class == WeaponClass.Dagger
                ? w with { Forged = w.Forged.ToDictionary(f => f.Key == CritWindow ? Block : f.Key, f => f.Value) }
                : w)
            .ToList());
        ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments, bludgeons));
        Assert.Equal(("Dagger", ContentValidator.RuleClassBaseline), (ex.EntryId, ex.Rule));
        ex = LoadWith("routing_axe", w => w with { Role = VariantRole.Control });
        Assert.Equal(("Axe", ContentValidator.RuleVariantRoles), (ex.EntryId, ex.Rule));

        // A caster forged on anything but Resonant x1, or with the wrong innate.
        ex = LoadWith("staff_of_mire", w => w with { Forged = Spread((Resonant, 1), (Block, 1)) });
        Assert.Equal(("staff_of_mire", ContentValidator.RuleCasterForged), (ex.EntryId, ex.Rule));
        ex = LoadWith("staff_of_mire", w => w with { Enchantments = [new EnchantmentRef("mire"), new EnchantmentRef("poison")] });
        Assert.Equal(("staff_of_mire", ContentValidator.RuleCasterInnate), (ex.EntryId, ex.Rule));
        ex = LoadWith("staff_of_mire", w => w with { Enchantments = [new EnchantmentRef("flaming")] });
        Assert.Equal(("staff_of_mire", ContentValidator.RuleCasterInnate), (ex.EntryId, ex.Rule));
        ex = LoadWith("wand_of_the_nova", w => w with { Enchantments = [new EnchantmentRef("flaming")] });
        Assert.Equal(("wand_of_the_nova", ContentValidator.RuleCasterInnate), (ex.EntryId, ex.Rule));
        ex = LoadWith("wand_of_the_nova", w => w with { Shape = null });
        Assert.Equal(("wand_of_the_nova", ContentValidator.RuleWandShape), (ex.EntryId, ex.Rule));
        ex = LoadWith("staff_of_renewal", w => w with { Enchantments = [new EnchantmentRef("regen")] });
        Assert.Equal(("staff_of_renewal", ContentValidator.RuleUnknownId), (ex.EntryId, ex.Rule));

        // Ids are unique.
        var doubled = new WeaponsData(ContentDefaults.Weapons.Weapons.Append(ContentDefaults.Weapons.Weapons[0]).ToList());
        ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments, doubled));
        Assert.Equal(("flensing_knife", ContentValidator.RuleDuplicateId), (ex.EntryId, ex.Rule));

        // Every forgedOnly id is forged somewhere, or it could never exist.
        var restricted = ContentDefaults.Restricted with { ForgedOnly = [nameof(Charges), nameof(OnHitPoison)] };
        ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning, restricted, ContentDefaults.Enchantments, ContentDefaults.Weapons));
        Assert.Equal((nameof(OnHitPoison), ContentValidator.RuleForgedOnlyUnused), (ex.EntryId, ex.Rule));

        // Every abort names both the entry and the rule, and the defaults are untouched by any of it.
        Assert.Contains(ex.EntryId, ex.Message);
        Assert.Contains(ex.Rule, ex.Message);
        Assert.Equal(32 + 11, Catalogue.All.Count);
    }

    [Fact]
    public void Validator_RefusesIllegalEnchantments_AndABrokenChart()
    {
        // No enchantment fires free.
        var ex = LoadWithEnchantment("flaming", e => e with { Trigger = 0 });
        Assert.Equal(("flaming", ContentValidator.RuleZeroTrigger), (ex.EntryId, ex.Rule));

        // A flat trigger or a status, never both, never neither.
        ex = LoadWithEnchantment("regeneration", e => e with { Trigger = 5 });
        Assert.Equal(("regeneration", ContentValidator.RuleTriggerXorApplies), (ex.EntryId, ex.Rule));
        ex = LoadWithEnchantment("flaming", e => e with { Trigger = null });
        Assert.Equal(("flaming", ContentValidator.RuleTriggerXorApplies), (ex.EntryId, ex.Rule));

        // The chart: two elements carrying one type, or an element nobody opposes.
        ex = LoadWithEnchantment("cold", e => e with { DamageType = DamageType.Flaming });
        Assert.Equal(("cold", ContentValidator.RuleOpposition), (ex.EntryId, ex.Rule));
        var unopposed = ContentDefaults.Restricted with
        {
            Excludes = ContentDefaults.Restricted.Excludes.Where(g => g[0] != "flaming").ToList(),
        };
        ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning, unopposed, ContentDefaults.Enchantments, ContentDefaults.Weapons));
        Assert.Equal(("flaming", ContentValidator.RuleOpposition), (ex.EntryId, ex.Rule));

        // Duplicate ids.
        var doubled = new EnchantmentsData(ContentDefaults.Enchantments.Enchantments.Append(ContentDefaults.Enchantments.Enchantments[0]).ToList());
        ex = Assert.Throws<ContentException>(() => GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted, doubled, ContentDefaults.Weapons));
        Assert.Equal(("regeneration", ContentValidator.RuleDuplicateId), (ex.EntryId, ex.Rule));

        // The opposed pairs may be read off the relations: flaming-cold, shocking-acidic, and nothing across.
        var excludes = ContentDefaults.Restricted.Excludes;
        Assert.Contains(excludes, g => g.SequenceEqual(new[] { "flaming", "cold" }));
        Assert.Contains(excludes, g => g.SequenceEqual(new[] { "shocking", "acidic" }));
        Assert.DoesNotContain(excludes, g => g.Contains("flaming") && g.Contains("shocking"));
    }

    [Fact]
    public void TestContent_SwapsCurrentForOneTest_AndRestoresIt()
    {
        var before = GameContent.Current;
        var cheaper = new WeaponsData(ContentDefaults.Weapons.Weapons
            .Select(w => w.Id == "great_axe" ? w with { Cost = 10 } : w)
            .ToList());

        using (TestContent.Use(weapons: cheaper))
        {
            Assert.NotSame(before, GameContent.Current);
            Assert.Equal(10, TestWeapons.Get("great_axe").ResolvedCost);
            Assert.Equal(60, TestWeapons.Get("reaver").ResolvedCost);   // the rest of the defaults came along
        }

        Assert.Same(before, GameContent.Current);
        Assert.Equal(60, TestWeapons.Get("great_axe").ResolvedCost);
    }

    private static ContentException LoadWith(string id, Func<WeaponDef, WeaponDef> edit)
    {
        var weapons = new WeaponsData(ContentDefaults.Weapons.Weapons.Select(w => w.Id == id ? edit(w) : w).ToList());
        return Assert.Throws<ContentException>(() =>
            GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.Enchantments, weapons));
    }

    private static ContentException LoadWithEnchantment(string id, Func<EnchantmentDef, EnchantmentDef> edit)
    {
        var enchantments = new EnchantmentsData(ContentDefaults.Enchantments.Enchantments.Select(e => e.Id == id ? edit(e) : e).ToList());
        return Assert.Throws<ContentException>(() =>
            GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted, enchantments, ContentDefaults.Weapons));
    }

    private static IReadOnlyDictionary<ModifierType, int> Spread(params (ModifierType Type, int Stacks)[] forged)
        => forged.ToDictionary(f => f.Type, f => f.Stacks);
}
