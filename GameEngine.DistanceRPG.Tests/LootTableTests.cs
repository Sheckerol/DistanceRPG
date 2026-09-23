using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The drop itself (§3.1, §3.2): what class it is, which variant, what it
/// arrives enchanted with, what the farm bought it, and the unique roll at the
/// end. Nothing here collects anything — <see cref="LootTable.Roll"/> is a pure
/// function of a dummy's saved state and a map seed, so every assertion is a
/// claim about that function rather than about anything a party did.
/// <para>
/// <strong>Three classes cannot yet be placed.</strong>
/// <see cref="EnemyPlacer.PlacedClasses"/> is five — no bow, no wand, and a
/// staff only through the crowded-room healer conversion, always Renewal — so
/// the fixtures below hand-build an <see cref="EnemyState"/> for those. That is
/// deliberate: widening placement waits on the kiting AI and the wand placement
/// scorer (PHASE3-003, Phase 4), and the roll has to be right before the placer
/// can reach it.
/// </para>
/// In the content collection because several of these swap content through
/// <see cref="TestContent.Use"/>.
/// </summary>
[Collection(TestContent.Collection)]
public class LootTableTests
{
    /// <summary>The prototype's own map seed; it overflows int32, which is the interesting case for a stream mix.</summary>
    private const long Seed = 2762136374;

    /// <summary>
    /// A dummy carrying <paramref name="weaponId"/>. Only three things are ever
    /// read off it — the weapon's class, the defeat count and the spawn index —
    /// which is exactly what §5.1 stores.
    /// </summary>
    private static EnemyState Dummy(string weaponId, int defeatCount = 0, int spawnIndex = 0, DamageType? element = null)
        => new() { Weapon = TestWeapons.Get(weaponId, element), DefeatCount = defeatCount, SpawnIndex = spawnIndex };

    /// <summary>Acquired stacks: the live total less the identity spread, which is what the farm bought and what §6.4's budget loses.</summary>
    private static int Acquired(Weapon w) => w.Modifiers.Entries.Sum(e => e.Stacks) - w.Forged.Entries.Sum(e => e.Stacks);

    /// <summary>Tiers the farm banked through the innate: it arrives at 1, so everything above that is the farm's.</summary>
    private static int Banked(Weapon w) => w.Enchantments.Count == 0 ? 0 : w.Enchantments[0].Tier - 1;

    /// <summary>One drop per spawn index at a fixed seed: the sweep §3.5's determinism is about, since banding is a within-floor property.</summary>
    private static List<Drop> Sweep(string weaponId, int defeatCount, int spawns, long seed = Seed, DamageType? element = null)
        => Enumerable.Range(0, spawns).Select(i => LootTable.Roll(Dummy(weaponId, defeatCount, i, element), seed)).ToList();

    // ── The class and the variant ────────────────────────────────────────────

    [Theory]
    [InlineData(WeaponClass.Dagger, "assassins_fang", null)]
    [InlineData(WeaponClass.Sword, "tower_guard", null)]
    [InlineData(WeaponClass.Spear, "phalanx_spear", null)]
    [InlineData(WeaponClass.Axe, "great_axe", null)]
    [InlineData(WeaponClass.Ranged, "longbow", null)]                       // hand-built: the placer fields no bow yet
    [InlineData(WeaponClass.Throwing, "bandolier", null)]
    [InlineData(WeaponClass.Staff, "staff_of_blight", null)]                // hand-built: the placer only ever converts a Renewal healer
    [InlineData(WeaponClass.Wand, "wand_of_the_blast", DamageType.Cold)]    // hand-built: the placer fields no wand yet
    public void ADropIsItsKillersClass_WithTheVariantRolledFresh(WeaponClass cls, string weaponId, DamageType? element)
    {
        // The class is read back off what the dummy was holding and never
        // re-rolled; the variant is rolled fresh on the drop's own stream, so
        // what it fought with says the class and nothing more.
        var drops = Sweep(weaponId, defeatCount: 0, spawns: 64, element: element);

        Assert.All(drops, d => Assert.Equal(cls, d.Weapon.Class));

        // All four variants turn up across one floor's spawn indices. A unique
        // keeps its variant's class, so it is counted as the class's and not as
        // one of the four.
        var variants = drops.Where(d => !d.WasUniqueRoll).Select(d => d.Weapon.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(4, variants.Count);
        Assert.All(variants, id => Assert.False(GameContent.Current.Weapons[id].Unique));
    }

    [Fact]
    public void AppendingAWeaponLeavesAnExistingSeedsDropUnchanged()
    {
        // The variant draw is off Enum.GetValues<VariantRole>().Length and never
        // off ByClass(...).Count, which is file order and a content-driven
        // length: the rule EnemyPlacer already states ("adding a weapon re-rolls
        // nobody"). A fifth staff is the case that would break a count read off
        // the catalogue, since the caster classes are the ones whose variants
        // carry no role.
        var before = Sweep("staff_of_blight", defeatCount: 6, spawns: 16).Select(Describe).ToList();
        var martialBefore = Sweep("assassins_fang", defeatCount: 6, spawns: 16).Select(Describe).ToList();

        var fifth = new WeaponDef(
            Id: "staff_of_embers", Name: "Staff of Embers", Class: WeaponClass.Staff, Role: null,
            Range: 100, Damage: 0, Cost: 40, ManaCost: 20,
            Forged: new Dictionary<ModifierType, int> { [ModifierType.Resonant] = 1 },
            Enchantments: [new EnchantmentRef("poison")], Shape: null, Unique: false, DerivedFrom: null);

        using var _ = TestContent.Use(weapons: new WeaponsData([.. ContentDefaults.Weapons.Weapons, fifth]));

        Assert.Equal(5, GameContent.Current.Weapons.ByClass(WeaponClass.Staff).Count);
        Assert.Equal(before, Sweep("staff_of_blight", defeatCount: 6, spawns: 16).Select(Describe).ToList());
        Assert.Equal(martialBefore, Sweep("assassins_fang", defeatCount: 6, spawns: 16).Select(Describe).ToList());
    }

    // ── The enchantment it arrives with ──────────────────────────────────────

    [Fact]
    public void AppendingACatalogueEntryLeavesAnExistingSeedsDropUnchanged()
    {
        // The enchantment draw is off LootTable.DropOrder, an ordered id list
        // held in code, and never off EnchantmentCatalogue.Rollable, which is
        // file order: appending an entry there would re-map every index and
        // silently re-roll every drop already recorded against a seed. §3.3's
        // own catalogue gains four more entries later in this phase.
        var before = Sweep("assassins_fang", defeatCount: 4, spawns: 64).Select(Describe).ToList();
        var dagger = LootTable.DropOrder(WeaponClass.Dagger).ToList();

        var appended = new EnchantmentDef(
            Id: "test_glimmer", Name: "Glimmer", Effect: EffectKind.Vampiric, Targets: TargetSide.Ally,
            Lock: 20, Trigger: 3, Potency: 1, ApplyPercent: 100, Applies: null, DamageType: null, Unique: false);

        using var _ = TestContent.Use(enchantments: new EnchantmentsData([.. ContentDefaults.Enchantments.Enchantments, appended]));

        Assert.Equal(10, GameContent.Current.Enchantments.Rollable.Count);   // the content-ordered list did grow
        Assert.Equal(dagger, LootTable.DropOrder(WeaponClass.Dagger));       // the code-ordered one did not
        Assert.Equal(before, Sweep("assassins_fang", defeatCount: 4, spawns: 64).Select(Describe).ToList());
    }

    [Fact]
    public void AStaffDropsItsOwnEffect_EveryTime()
    {
        // A staff's identity is the effect it casts, so it is fixed by the
        // variant and the drop rolls nothing for it: a Staff of Blight carries
        // Poison on every seed there is.
        //
        // §3.3 says class "only sets the odds of arriving with one, never which
        // one", and that Ward on a Staff of Mire is an ordinary drop. §3.1's
        // staff rule wins over both, because it is the more specific statement
        // about the same case: a Staff of Mire drops carrying Mire, and Ward
        // reaches it through the enchanter (§6.4) or a transfer (§6.5), which is
        // the situation §3.3 is describing rather than a second drop rule.
        var drops = Sweep("staff_of_blight", defeatCount: 0, spawns: 64).Where(d => !d.WasUniqueRoll).ToList();

        Assert.All(drops, d =>
        {
            var listed = GameContent.Current.Weapons[d.Weapon.Id].Enchantments;
            Assert.Equal(listed.Single().Id, Assert.Single(d.Weapon.Enchantments).Id);
        });

        var blight = drops.Where(d => d.Weapon.Id == "staff_of_blight").ToList();
        Assert.NotEmpty(blight);
        Assert.All(blight, d => Assert.Equal("poison", d.Weapon.Enchantments[0].Id));
    }

    [Fact]
    public void AWandDropsAnElementRolledUniformly()
    {
        // A wand's identity is its shape, which leaves the damage free to roll:
        // one element per drop, and all four turn up over a floor. Hand-built,
        // because the placer cannot field a wand until Phase 4.
        var drops = Sweep("wand_of_the_blast", defeatCount: 0, spawns: 64, element: DamageType.Flaming)
            .Where(d => !d.WasUniqueRoll).ToList();

        var elements = new List<DamageType>();
        foreach (var drop in drops)
        {
            var entry = Assert.Single(drop.Weapon.Enchantments);
            Assert.Equal(EffectKind.ElementalDamage, entry.Def.Effect);
            elements.Add(entry.Def.DamageType!.Value);
        }

        Assert.Equal(4, elements.Distinct().Count());
        foreach (var element in Enum.GetValues<DamageType>().Where(t => t != DamageType.None))
            Assert.InRange(elements.Count(e => e == element), 8, 24);   // 17 / 14 / 20 / 13 as it stands
    }

    [Fact]
    public void AMartialDropIsUsuallyPlain_AndRarelyEnchanted()
    {
        // The rare roll and nothing else: no number appears anywhere in §3.1, so
        // the rate is a knob, and what is pinned here is that the knob is the
        // only thing deciding.
        var plain = Sweep("assassins_fang", defeatCount: 0, spawns: 400).Where(d => !d.WasUniqueRoll).ToList();
        int enchanted = plain.Count(d => d.Weapon.Enchantments.Count == 1);
        Assert.InRange(enchanted / (double)plain.Count, 0.05, 0.18);   // 10% compiled

        using (TestContent.Use(tuning: ContentDefaults.Tuning with { DropEnchantChancePercent = 0 }))
            Assert.All(Sweep("assassins_fang", defeatCount: 0, spawns: 64).Where(d => !d.WasUniqueRoll),
                d => Assert.Empty(d.Weapon.Enchantments));

        using (TestContent.Use(tuning: ContentDefaults.Tuning with { DropEnchantChancePercent = 100 }))
            Assert.All(Sweep("assassins_fang", defeatCount: 0, spawns: 64).Where(d => !d.WasUniqueRoll),
                d => Assert.Single(d.Weapon.Enchantments));
    }

    [Fact]
    public void AMartialDropNeverCarriesAStatusApplier()
    {
        // The pool is filtered on EnchantmentBehaviours.FiresOn over the events
        // the class actually raises, so an entry a martial weapon wins can always
        // fire on something it does. The four staff effects are the case that
        // matters: ApplyStatus is in the cast table and no other, so a dagger
        // winning one would reserve its lock for a behaviour that can never run.
        using var _ = TestContent.Use(tuning: ContentDefaults.Tuning with { DropEnchantChancePercent = 100 });

        var entries = Sweep("assassins_fang", defeatCount: 0, spawns: 400)
            .Where(d => !d.WasUniqueRoll)
            .Select(d => Assert.Single(d.Weapon.Enchantments).Def)
            .ToList();

        Assert.All(entries, def => Assert.NotEqual(EffectKind.ApplyStatus, def.Effect));
        Assert.All(entries, def => Assert.False(GameContent.Current.Enchantments.NeverRolled.Contains(def.Id)));
        Assert.Equal(["flaming", "cold", "shocking", "acidic", "vampiric"], LootTable.DropOrder(WeaponClass.Dagger));
    }

    [Fact]
    public void ADropCarriesExactlyOneEnchantment_AtTierOne()
    {
        // Always exactly one, always tier 1: breadth is the enchanter's product
        // and depth is a project, so what the dungeon hands over is a seed.
        // Every class, including the three the placer cannot yet field.
        foreach (var (weaponId, element) in EveryClass())
            foreach (var drop in Sweep(weaponId, defeatCount: 0, spawns: 32, element: element).Where(d => !d.WasUniqueRoll))
            {
                Assert.InRange(drop.Weapon.Enchantments.Count, 0, 1);
                Assert.All(drop.Weapon.Enchantments, e => Assert.Equal(1, e.Tier));
                if (drop.Weapon.IsCaster)
                    Assert.Single(drop.Weapon.Enchantments);
            }
    }

    [Fact]
    public void TheInnateIsForged()
    {
        // Forged in the §1.1 sense — part of what the weapon is — which is what
        // makes it farmable (§3.2) and what counts toward service time (§6.2).
        using var _ = TestContent.Use(tuning: ContentDefaults.Tuning with { DropEnchantChancePercent = 100 });

        foreach (var (weaponId, element) in EveryClass())
            foreach (var drop in Sweep(weaponId, defeatCount: 0, spawns: 16, element: element).Where(d => !d.WasUniqueRoll))
            {
                Assert.Single(drop.Weapon.Enchantments);
                Assert.Equal(1, drop.Weapon.ForgedEnchantmentCount);
                Assert.True(drop.Weapon.IsForged(0));
            }
    }

    [Fact]
    public void FarmingNeverAttachesASecondEnchantment()
    {
        // Farming buys depth in what the weapon already is: it can raise the
        // tier of an innate and can never attach a second.
        foreach (var (weaponId, element) in EveryClass())
            foreach (var drop in Sweep(weaponId, defeatCount: 40, spawns: 16, element: element).Where(d => !d.WasUniqueRoll))
                Assert.InRange(drop.Weapon.Enchantments.Count, 0, 1);
    }

    // ── The farm's investment ────────────────────────────────────────────────

    [Fact]
    public void StacksLandOnlyOnForgedModifiers_AndSkipTheCapped()
    {
        // Depth, never breadth: the roll only picks a modifier the weapon is
        // already forged with, and the stacks inherit the forged ceiling — 7 and
        // 6 here, rather than the bare 5 an off-class graft would hit. Deep
        // enough that every seed has finished: where a farm lands is the claim,
        // and how many cycles it took to land there is the test below.
        var guards = Sweep("tower_guard", defeatCount: 100, spawns: 200)
            .Where(d => !d.WasUniqueRoll && d.Weapon.Id == "tower_guard").ToList();
        Assert.NotEmpty(guards);

        Assert.All(guards, d =>
        {
            Assert.Equal(7, d.Weapon.Stacks(ModifierType.Block));
            Assert.Equal(6, d.Weapon.Stacks(ModifierType.Push));
            Assert.Equal(
                d.Weapon.Forged.Entries.Select(e => e.Type).ToList(),
                d.Weapon.Modifiers.Entries.Select(e => e.Type).ToList());
        });

        // The allowance is never wasted, because no weapon is forged with fewer
        // than two axes (§1.2) and the casters carry an innate enchantment
        // instead, which does the same job: every drop of every class has at
        // least two homes for a stack to land in, so none of them is the weapon
        // you could not fully invest in.
        foreach (var (weaponId, element) in EveryClass())
            foreach (var drop in Sweep(weaponId, defeatCount: 0, spawns: 8, element: element).Where(d => !d.WasUniqueRoll))
                Assert.True(drop.Weapon.Forged.Entries.Count() + drop.Weapon.Enchantments.Count >= 2, drop.Weapon.Id);
    }

    [Fact]
    public void AssassinsFang_AndDisarmingKris_FillTheirCeilings()
    {
        // Both dials of the crit build maxed on the two-modifier weapon; all
        // three on the three-modifier one, given roughly 1.5x the cycles, since
        // the same 50% roll has three homes to land in instead of two.
        //
        // This is also where §3.2's "past ten the drop cannot improve at all" is
        // pinned as stale: that line survives from the removed hard-top draft,
        // and the T2 table it sits beside — with settled.md behind it — has a
        // three-modifier weapon still improving to fifteen. Every weapon can be
        // finished; a three-modifier weapon just takes longer.
        var fangs = Sweep("assassins_fang", defeatCount: 100, spawns: 200)
            .Where(d => !d.WasUniqueRoll && d.Weapon.Id == "assassins_fang").ToList();
        var krises = Sweep("disarming_kris", defeatCount: 100, spawns: 200)
            .Where(d => !d.WasUniqueRoll && d.Weapon.Id == "disarming_kris").ToList();
        Assert.NotEmpty(fangs);
        Assert.NotEmpty(krises);

        Assert.All(fangs, d =>
        {
            Assert.Equal(7, d.Weapon.Stacks(ModifierType.CritWindow));
            Assert.Equal(6, d.Weapon.Stacks(ModifierType.CritMultiplier));
            Assert.Equal(10, Acquired(d.Weapon));
        });
        Assert.All(krises, d =>
        {
            Assert.Equal(6, d.Weapon.Stacks(ModifierType.CritWindow));
            Assert.Equal(6, d.Weapon.Stacks(ModifierType.CritMultiplier));
            Assert.Equal(6, d.Weapon.Stacks(ModifierType.CritWeaken));
            Assert.Equal(15, Acquired(d.Weapon));
        });

        // And the 1.5x is the cycles, not just the room: the Fang fills around
        // DefeatCount 20 and the Kris around 30.
        double fang = CyclesToFill("assassins_fang", room: 10);
        double kris = CyclesToFill("disarming_kris", room: 15);
        Assert.InRange(fang, 18, 25);          // 21.3 as it stands
        Assert.InRange(kris, 27, 37);          // 31.7
        Assert.InRange(kris / fang, 1.3, 1.7); // 1.49
    }

    [Fact]
    public void AStaffOfBlightBanksFiveTiers_AndNoMore()
    {
        // The only farm that buys potency. Resonant x6 and five banked tiers:
        // the enchantment's own tier has no ceiling, only the farm's
        // contribution to it does, and the entry leaves the draw exactly as a
        // capped modifier does. Past that the cycles land on Resonant alone.
        foreach (int defeatCount in new[] { 40, 200 })
        {
            var staves = Sweep("staff_of_blight", defeatCount, spawns: 200)
                .Where(d => !d.WasUniqueRoll && d.Weapon.Id == "staff_of_blight").ToList();
            Assert.NotEmpty(staves);

            Assert.All(staves, d =>
            {
                Assert.Equal(6, d.Weapon.Stacks(ModifierType.Resonant));
                Assert.Equal(6, d.Weapon.Enchantments[0].Tier);   // arrives at 1, banks five
                Assert.Equal(5, Banked(d.Weapon));
                Assert.Equal(0, d.Weapon.Enchantments[0].Xp);     // a tier's worth exactly: no floating remainder
            });
        }
    }

    [Fact]
    public void FarmedStacksAreAcquired_AndSpendTheEnchantersBudget()
    {
        // Farming spends the weapon's future: the stacks are acquired, out of
        // the same per-modifier budget §6.4 would otherwise fill. The identity
        // spread never moves, so the ceiling never does either.
        var variant = GameContent.Current.Weapons["assassins_fang"];
        var forged = ModifierSet.Of(variant.Forged.Select(kv => (kv.Key, kv.Value)).ToArray());
        var rules = GameContent.Current.Modifiers;

        var tenth = Sweep("assassins_fang", defeatCount: 10, spawns: 200)
            .Where(d => !d.WasUniqueRoll && d.Weapon.Id == "assassins_fang").ToList();
        Assert.NotEmpty(tenth);
        Assert.All(tenth, d => Assert.Equal(forged, d.Weapon.Forged));
        Assert.InRange(tenth.Average(d => Acquired(d.Weapon)), 4.2, 5.8);   // "has spent five of them somewhere"

        // What the enchanter can still reach is exactly the cap less the live
        // total, so a farmed weapon hands over less of it and a finished one
        // hands over none: "the enchanter can never deepen it again".
        Assert.All(tenth, d => Assert.Equal(
            10 - Acquired(d.Weapon),
            d.Weapon.Forged.Entries.Sum(e => rules.Cap(e.Type, e.Stacks) - d.Weapon.Stacks(e.Type))));

        var finished = Sweep("assassins_fang", defeatCount: 100, spawns: 200)
            .Where(d => !d.WasUniqueRoll && d.Weapon.Id == "assassins_fang").ToList();
        Assert.All(finished, d => Assert.Equal(
            0, d.Weapon.Forged.Entries.Sum(e => rules.Cap(e.Type, e.Stacks) - d.Weapon.Stacks(e.Type))));

        // And all of that happened to a copy: a drop is an instance, never a row
        // mutated in the shared table, so the next one off the same id arrives
        // plain. Nothing the farm did is visible from the catalogue.
        var fresh = TestWeapons.Get("assassins_fang");
        Assert.Equal(forged, fresh.Modifiers);
        Assert.NotSame(finished[0].Weapon, fresh);
        Assert.Empty(fresh.Enchantments);
    }

    [Fact]
    public void ExpectedStacksTrackTheDocsTable()
    {
        // The T2 table, on a three-modifier fixture deliberately: it prints 10 at
        // DefeatCount 20 as the uncapped expectation while calling it a
        // two-modifier weapon's ceilings, and with the per-modifier forged + 5
        // cap a two-modifier weapon's mean is 9.12 — nearly a full point outside
        // the band. The claim the mean actually supports is the raw 50% roll, so
        // it runs on a weapon with room to spare, and the exact two-modifier
        // claim is its own test below.
        var expected = new (int DefeatCount, double Stacks)[] { (5, 2.5), (10, 5), (15, 7.5), (20, 10) };
        foreach (var (defeatCount, stacks) in expected)
        {
            var drops = Sweep("disarming_kris", defeatCount, spawns: 500)
                .Where(d => !d.WasUniqueRoll && d.Weapon.Id == "disarming_kris").ToList();
            Assert.NotEmpty(drops);

            // Everything the 50% roll bought: a stack, or the tier a landing on
            // the enchantment entry banks instead.
            double mean = drops.Average(d => Acquired(d.Weapon) + Banked(d.Weapon));
            Assert.InRange(mean, stacks - 0.4, stacks + 0.4);
        }
    }

    [Fact]
    public void ATwoModifierWeaponCapsAtTenAndNeverExceedsIt()
    {
        // The exact claim the two-modifier case does support: no farm-wide total
        // cap, only §1.1's per-modifier forged + 5, which on two modifiers is ten
        // and never eleven however long the farm runs. Ten is a ceiling at every
        // depth and a certainty only past the point the 50% roll has certainly
        // paid for it — a drop that also rolled an enchantment has three homes to
        // fill rather than two, so DefeatCount 20 is where the farm usually ends
        // rather than where it always has.
        foreach (int defeatCount in new[] { 20, 40, 200 })
        {
            var guards = Sweep("tower_guard", defeatCount, spawns: 200)
                .Where(d => !d.WasUniqueRoll && d.Weapon.Id == "tower_guard").ToList();
            Assert.NotEmpty(guards);
            Assert.All(guards, d => Assert.InRange(Acquired(d.Weapon), 0, 10));
            if (defeatCount == 200)
                Assert.All(guards, d => Assert.Equal(10, Acquired(d.Weapon)));
            if (defeatCount == 40)
                Assert.InRange(guards.Count(d => Acquired(d.Weapon) == 10) / (double)guards.Count, 0.9, 1.0);
        }
    }

    [Fact]
    public void TheEligibleListPutsTheEnchantmentEntryLast()
    {
        // The draw is a uniform index into the eligible list, so the list's order
        // is gameplay: the forged modifiers first, in ModifierType order, and the
        // enchantment entry appended last. Two seeds, each pinned to a known
        // index, because reversing the list would simply swap what they produce
        // and nothing else in the suite would notice.
        var onTheModifier = LootTable.Roll(Dummy("staff_of_blight", defeatCount: 1), 4);
        Assert.Equal(2, onTheModifier.Weapon.Stacks(ModifierType.Resonant));   // index 0: the one forged modifier
        Assert.Equal(1, onTheModifier.Weapon.Enchantments[0].Tier);

        var onTheEnchantment = LootTable.Roll(Dummy("staff_of_blight", defeatCount: 1), 3);
        Assert.Equal(1, onTheEnchantment.Weapon.Stacks(ModifierType.Resonant));   // index 1: the entry, appended last
        Assert.Equal(2, onTheEnchantment.Weapon.Enchantments[0].Tier);
    }

    // ── The unique ───────────────────────────────────────────────────────────

    [Fact]
    public void TheUniqueRollDependsOnTheCountAlone()
    {
        // The roll never depends on stacks — only on DefeatCount — so it keeps
        // climbing after a weapon has stopped improving, which is what makes the
        // deep farm a gamble rather than a grind. Partitioned by how wide the
        // rolled variant's spread is, since that is what decides how much room
        // the farm had and where its stacks went.
        double expected = FarmLadder.UniqueChancePermille(20) / 1000.0;
        var catalogue = GameContent.Current.Weapons;

        var byWidth = Enumerable.Range(0, 1000)
            .Select(spawn =>
            {
                // The variant is the first draw off the stream, so it is
                // predictable without asking the drop: the draw order this phase
                // fixes, read back.
                var variant = catalogue.Variant(WeaponClass.Dagger, (VariantRole)LootTable.StreamFor(Seed, spawn).NextInt(0, 3));
                return (Width: variant.Forged.Count, Won: LootTable.Roll(Dummy("flensing_knife", 20, spawn), Seed).WasUniqueRoll);
            })
            .GroupBy(x => x.Width)
            .ToList();

        Assert.Equal(2, byWidth.Count);   // the Fang carries two modifier types; the other three carry three
        foreach (var group in byWidth)
            Assert.InRange(group.Count(x => x.Won) / (double)group.Count(), expected - 0.08, expected + 0.08);
    }

    [Fact]
    public void EveryClassCanWinTheUniqueRollAtFourty()
    {
        // Eleven uniques cover thirty-two variants, so preferring the variant's
        // own and stopping there would leave two thirds of every class's farms at
        // an effective 0% however deep they ran. The re-steer keeps the logistic
        // true for every class, which is the claim the curve makes.
        double expected = FarmLadder.UniqueChancePermille(40) / 1000.0;

        foreach (var (weaponId, element) in EveryClass())
        {
            var drops = Sweep(weaponId, defeatCount: 40, spawns: 200, element: element).ToList();
            var wins = drops.Where(d => d.WasUniqueRoll).ToList();

            Assert.NotEmpty(wins);
            Assert.All(wins, d => Assert.True(d.Weapon.Unique, $"{weaponId} won and handed over {d.Weapon.Id}"));
            Assert.InRange(wins.Count / (double)drops.Count, expected - 0.15, expected + 0.15);
        }
    }

    [Fact]
    public void AWonUniqueIsTheVariantsOwn_WithTheAcquiredBudgetUntouched()
    {
        // A unique's spread is forged, so winning lands on a deeper base with the
        // acquired budget untouched: not a better version of the same weapon, the
        // opposite kind of object. The farm's stacks and banked tiers go with it.
        var drop = LootTable.Roll(Dummy("assassins_fang", defeatCount: 40), 11);

        Assert.True(drop.WasUniqueRoll);
        Assert.Equal("widowmaker", drop.Weapon.Id);
        Assert.Equal(3, drop.Weapon.Stacks(ModifierType.CritWindow));
        Assert.Equal(0, Acquired(drop.Weapon));
        Assert.Equal(8, GameContent.Current.Modifiers.Cap(ModifierType.CritWindow, 3));   // ceiling 8, five still to spend
        Assert.Equal(Enchantment.UniqueTier, Assert.Single(drop.Weapon.Enchantments).Tier);
    }

    [Fact]
    public void AVariantWithNoUniqueReSteersWithinItsClass()
    {
        // Twenty-one of the thirty-two variants have no unique of their own. A
        // won roll re-steers over the class's uniques rather than handing back an
        // ordinary weapon, drawn uniformly where a class has more than one — and
        // the chance draw is taken either way, so losing leaves the stream
        // exactly where winning would have.
        Assert.Null(GameContent.Current.Weapons.UniqueDerivedFrom("weakspot_stiletto"));

        var steered = new List<string>();
        for (long seed = 1; seed <= 2000; seed++)
        {
            var drop = LootTable.Roll(Dummy("weakspot_stiletto", defeatCount: 40), seed);
            if (!drop.WasUniqueRoll) continue;
            if (LootTable.Roll(Dummy("weakspot_stiletto"), seed).Weapon.Id != "weakspot_stiletto") continue;   // the variant this seed rolled
            steered.Add(drop.Weapon.Id);
        }

        Assert.NotEmpty(steered);
        Assert.All(steered, id => Assert.Equal(WeaponClass.Dagger, GameContent.Current.Weapons[id].Class));
        Assert.Equal(["flensing_knife_unique", "widowmaker"], steered.Distinct().Order(StringComparer.Ordinal));
        Assert.InRange(steered.Count(id => id == "widowmaker") / (double)steered.Count, 0.4, 0.6);
    }

    [Fact]
    public void BurstAndGrindArriveAtTheSameDrop()
    {
        // A clean kill advances two rows of the table at once, so the columns are
        // cycles rather than swings: a burst party reaches DefeatCount 10 in five
        // fights and a grind party in ten, and both arrive at the same drop —
        // which is true because the roll reads the count and never the route.
        int burst = Enumerable.Range(0, 5).Sum(_ => FarmLadder.DefeatAdvance(dealt: 50, maxHp: 50));
        int grind = Enumerable.Range(0, 10).Sum(_ => FarmLadder.DefeatAdvance(dealt: 25, maxHp: 50));
        Assert.Equal(10, burst);
        Assert.Equal(10, grind);

        for (int spawn = 0; spawn < 16; spawn++)
            Assert.Equal(
                Describe(LootTable.Roll(Dummy("tower_guard", burst, spawn), Seed)),
                Describe(LootTable.Roll(Dummy("tower_guard", grind, spawn), Seed)));
    }

    // ── The pool as content ──────────────────────────────────────────────────

    [Fact]
    public void TheDropPoolResolvesInTheCatalogue()
    {
        // Holding the pool in code is what stops a content append re-indexing it;
        // the cost is that it can name something the file does not, which is
        // refused at load the way every other dangling reference is.
        Assert.All(LootTable.DropPool, id => Assert.True(GameContent.Current.Enchantments.TryGet(id, out _)));

        var before = GameContent.Current;
        var missing = new EnchantmentsData(ContentDefaults.Enchantments.Enchantments.Where(e => e.Id != "vampiric").ToList());
        var ex = Assert.Throws<ContentException>(() => TestContent.Use(enchantments: missing));

        Assert.Equal(("vampiric", ContentValidator.RuleDropPoolResolves), (ex.EntryId, ex.Rule));
        Assert.Same(before, GameContent.Current);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>One enemy weapon per class; the last three are hand-built for the reason the class doc gives.</summary>
    private static (string WeaponId, DamageType? Element)[] EveryClass() =>
    [
        ("assassins_fang", null),
        ("tower_guard", null),
        ("phalanx_spear", null),
        ("great_axe", null),
        ("longbow", null),
        ("bandolier", null),
        ("staff_of_blight", null),
        ("wand_of_the_blast", DamageType.Cold),
    ];

    /// <summary>Everything about a drop that a seed is supposed to fix: the item, its live spread, its entries and the unique outcome.</summary>
    private static string Describe(Drop drop)
        => $"{drop.Weapon.Id} [{drop.Weapon.Modifiers}] " +
           string.Join(", ", drop.Weapon.Enchantments.Select(e => $"{e.Id}@{e.Tier}+{e.Xp}")) +
           $" unique={drop.WasUniqueRoll} count={drop.DefeatCount}";

    /// <summary>The mean DefeatCount at which <paramref name="variantId"/> first has its whole acquired allowance spent.</summary>
    private static double CyclesToFill(string variantId, int room)
    {
        var firsts = new List<int>();
        for (int spawn = 0; spawn < 100; spawn++)
            for (int n = 1; n <= 200; n++)
            {
                var drop = LootTable.Roll(Dummy(variantId, n, spawn), Seed);
                if (drop.WasUniqueRoll || drop.Weapon.Id != variantId) continue;
                if (Acquired(drop.Weapon) < room) continue;
                firsts.Add(n);
                break;
            }

        Assert.NotEmpty(firsts);
        return firsts.Average();
    }
}
