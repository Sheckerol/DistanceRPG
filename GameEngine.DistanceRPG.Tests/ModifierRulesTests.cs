using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

public class ModifierRulesTests
{
    private static ModifierRules Rules => GameContent.Current.Modifiers;
    private static readonly ModifierType[] All = Enum.GetValues<ModifierType>();

    [Fact]
    public void ModifierType_HasTheTwentyThreeMembers_InDocOrder()
    {
        ModifierType[] expected =
        [
            Brace, Block, CritWindow, CritMultiplier, Cleave, Charges, Longshot, Light, Riposte, Push, Drag,
            Splitting, Overwatch, Opportunist, Rout, Pin, Softening, CritWeaken, CritSunder, BlockWeaken,
            OnHitPoison, Resonant, Momentum,
        ];
        Assert.Equal(expected, All);
        Assert.Equal(23, All.Length);
    }

    [Fact]
    public void WeaponKind_FlagsAreExact()
    {
        Assert.Equal(1, (int)WeaponKind.Melee);
        Assert.Equal(2, (int)WeaponKind.Ranged);
        Assert.Equal(4, (int)WeaponKind.Caster);
        Assert.Equal(7, (int)WeaponKind.Any);
        Assert.True(WeaponKind.Any.HasFlag(WeaponKind.Caster));
        Assert.False(WeaponKind.Melee.HasFlag(WeaponKind.Ranged));
    }

    [Theory]
    [InlineData(CritMultiplier, 0, 2)]   // base x2 with no stacks
    [InlineData(CritMultiplier, 1, 3)]
    [InlineData(CritMultiplier, 8, 10)]  // only a unique that raised CritMultiplier reaches x10
    [InlineData(Charges, 1, 2)]          // the shipped baseline: 2 throws at x1
    [InlineData(Charges, 8, 9)]          // 9 throws for 135, just inside the budget
    [InlineData(CritMultiplier, 6, 8)]   // forged x1 and fully worked: the x8 multiplier ceiling
    [InlineData(Block, 3, 9)]            // The Bulwark
    [InlineData(Light, 5, 50)]           // a maxed graft halves any weapon
    [InlineData(Light, 6, 60)]           // the Light ceiling: a 60% discount
    [InlineData(Resonant, 6, 60)]        // the same ceiling for the same shape
    [InlineData(CritWindow, 8, 8)]       // crit on 12+
    [InlineData(Brace, 0, 0)]
    public void Resolve_IsOffsetPlusPerStackTimesStacks(ModifierType t, int stacks, int expected)
        => Assert.Equal(expected, Rules.Resolve(t, stacks));

    [Theory]
    [InlineData(Brace, 1)]
    [InlineData(Block, 3)]
    [InlineData(CritWindow, 1)]
    [InlineData(CritMultiplier, 1)]
    [InlineData(Cleave, 1)]
    [InlineData(Charges, 1)]
    [InlineData(Longshot, 1)]
    [InlineData(Light, 10)]
    [InlineData(Riposte, 1)]
    [InlineData(Push, 1)]
    [InlineData(Drag, 1)]
    [InlineData(Splitting, 3)]
    [InlineData(Overwatch, 1)]
    [InlineData(Opportunist, 1)]
    [InlineData(Rout, 1)]
    [InlineData(Pin, 1)]
    [InlineData(Softening, 3)]
    [InlineData(CritWeaken, 1)]
    [InlineData(CritSunder, 1)]
    [InlineData(BlockWeaken, 1)]
    [InlineData(OnHitPoison, 0)]
    [InlineData(Resonant, 10)]
    [InlineData(Momentum, 0)]
    public void PerStack_MatchesTheModifierTable(ModifierType t, int perStack)
        => Assert.Equal(perStack, Rules.PerStack(t));

    [Fact]
    public void Offset_IsCritMultiplierTwo_ChargesOne_ElseZero()
    {
        foreach (var t in All)
            Assert.Equal(t switch { CritMultiplier => 2, Charges => 1, _ => 0 }, Rules.Offset(t));
    }

    [Fact]
    public void Cap_IsForgedPlusHeadroom_NoFlatTerm()
    {
        Assert.Equal(5, Rules.AcquiredHeadroom);
        foreach (var t in All)
        {
            Assert.Equal(8, Rules.Cap(t, 3));     // Widowmaker's CritWindow x3: a ceiling of 8
            Assert.Equal(7, Rules.Cap(t, 2));     // Assassin's Fang, the Purity dagger: one higher than its siblings
            Assert.Equal(6, Rules.Cap(t, 1));     // Flensing Knife: CritWindow and Light both ceiling at 6
            Assert.Equal(5, Rules.Cap(t, 0));     // never forged: caps at 5
            Assert.Equal(105, Rules.Cap(t, 100)); // nothing is capped
        }
    }

    [Fact]
    public void MaxForged_IsOneForCurrencyPair_ElseThree()
    {
        foreach (var t in All)
            Assert.Equal(t is Light or Resonant ? 1 : 3, Rules.MaxForged(t));
    }

    [Fact]
    public void Excludes_IsSymmetricAfterConstruction()
    {
        var excludes = Rules.Excludes;
        foreach (var (a, others) in excludes)
            foreach (var b in others)
            {
                Assert.NotEqual(a, b);
                Assert.Contains(a, excludes[b]);
            }

        // The five modifier groups expand to 6 + 6 + 2 + 2 + 2 directed pairs.
        Assert.Equal(18, excludes.Values.Sum(v => v.Length));
        Assert.Equal(new[] { Overwatch, Opportunist }, excludes[Brace]);
        Assert.Equal(new[] { Brace, Overwatch }, excludes[Opportunist]);
        Assert.Equal(new[] { Brace, Opportunist }, excludes[Overwatch]);
        Assert.Equal(new[] { Drag, Rout }, excludes[Push]);
        Assert.Equal(new[] { Push, Rout }, excludes[Drag]);
        Assert.Equal(new[] { Push, Drag }, excludes[Rout]);
        Assert.Equal(new[] { BlockWeaken }, excludes[Riposte]);
        Assert.Equal(new[] { Riposte }, excludes[BlockWeaken]);
        Assert.Equal(new[] { CritSunder }, excludes[CritWeaken]);
        Assert.Equal(new[] { CritWeaken }, excludes[CritSunder]);
        Assert.Equal(new[] { Resonant }, excludes[Light]);
        Assert.Equal(new[] { Light }, excludes[Resonant]);

        // Modifiers in no group have no entry at all.
        foreach (var t in new[] { Block, CritWindow, CritMultiplier, Cleave, Charges, Longshot, Splitting, Softening, Pin })
            Assert.DoesNotContain(t, excludes);
    }

    [Theory]
    [InlineData(Brace, WeaponKind.Ranged, new ModifierType[0], false)]        // melee only
    [InlineData(Brace, WeaponKind.Caster, new ModifierType[0], false)]
    [InlineData(Brace, WeaponKind.Melee, new ModifierType[0], true)]
    [InlineData(Opportunist, WeaponKind.Ranged, new ModifierType[0], false)]  // melee only
    [InlineData(Overwatch, WeaponKind.Melee, new ModifierType[0], false)]     // ranged only
    [InlineData(Overwatch, WeaponKind.Ranged, new ModifierType[0], true)]
    [InlineData(Riposte, WeaponKind.Melee, new ModifierType[0], false)]       // nothing to counter off
    [InlineData(Riposte, WeaponKind.Melee, new[] { Block }, true)]
    [InlineData(BlockWeaken, WeaponKind.Melee, new ModifierType[0], false)]   // nothing to succeed at
    [InlineData(BlockWeaken, WeaponKind.Melee, new[] { Block }, true)]
    [InlineData(Rout, WeaponKind.Melee, new ModifierType[0], false)]          // without a cleave, a worse Push
    [InlineData(Rout, WeaponKind.Melee, new[] { Cleave }, true)]
    [InlineData(Charges, WeaponKind.Ranged, new ModifierType[0], false)]      // never granted from zero
    [InlineData(Charges, WeaponKind.Ranged, new[] { Charges }, true)]         // deepenable once present
    [InlineData(Push, WeaponKind.Melee, new[] { Drag }, false)]               // one displacement direction
    [InlineData(Push, WeaponKind.Melee, new[] { Block }, true)]
    [InlineData(Light, WeaponKind.Melee, new[] { Resonant }, false)]          // one currency per weapon
    [InlineData(Resonant, WeaponKind.Melee, new ModifierType[0], true)]       // not caster-only: a graft
    [InlineData(Resonant, WeaponKind.Caster, new[] { Light }, false)]
    [InlineData(Brace, WeaponKind.Melee, new[] { Opportunist }, false)]       // one threat zone
    [InlineData(Opportunist, WeaponKind.Melee, new[] { Brace }, false)]
    [InlineData(CritSunder, WeaponKind.Melee, new[] { CritWeaken }, false)]   // one rider per crit
    [InlineData(Block, WeaponKind.Ranged, new ModifierType[0], true)]         // no relations at all
    [InlineData(Riposte, WeaponKind.Melee, new[] { Block, BlockWeaken }, false)] // prerequisite met, but excluded
    public void Allowed_FoldsKindForgedOnlyExcludesAndRequires(ModifierType t, WeaponKind kind, ModifierType[] present, bool expected)
    {
        var set = ModifierSet.Of(present.Select(p => (p, 1)).ToArray());
        Assert.Equal(expected, Rules.Allowed(t, kind, set));
    }

    [Fact]
    public void Requires_IsOneWay()
    {
        Assert.DoesNotContain(Block, Rules.Requires);   // Block needs nothing
        Assert.DoesNotContain(Cleave, Rules.Requires);
        Assert.Equal(new[] { Block }, Rules.Requires[Riposte]);
        Assert.Equal(new[] { Block }, Rules.Requires[BlockWeaken]);
        Assert.Equal(new[] { Cleave }, Rules.Requires[Rout]);
        Assert.Equal(3, Rules.Requires.Count);
        Assert.True(Rules.Allowed(Block, WeaponKind.Melee, ModifierSet.Empty));
        Assert.True(Rules.Allowed(Cleave, WeaponKind.Melee, ModifierSet.Of((Rout, 1))));
    }

    [Fact]
    public void RequiresKind_RestrictsExactlyTheThreeThreatZoneModifiers()
    {
        Assert.Equal(WeaponKind.Melee, Rules.RequiresKind[Brace]);
        Assert.Equal(WeaponKind.Melee, Rules.RequiresKind[Opportunist]);
        Assert.Equal(WeaponKind.Ranged, Rules.RequiresKind[Overwatch]);
        Assert.Equal(3, Rules.RequiresKind.Count);
        Assert.DoesNotContain(Resonant, Rules.RequiresKind);
    }

    [Fact]
    public void ForgedOnly_IsExactlyCharges()
        => Assert.Equal(new[] { Charges }, Rules.ForgedOnly);

    [Fact]
    public void UnrelatedModifiers_HaveNoRelationAtAll()
    {
        // In no exclusion group, with no prerequisite and no kind restriction.
        foreach (var t in new[] { Block, CritWindow, CritMultiplier, Cleave, Longshot, Splitting, Softening, Pin })
        {
            Assert.DoesNotContain(t, Rules.Excludes);
            Assert.DoesNotContain(t, Rules.Requires);
            Assert.DoesNotContain(t, Rules.RequiresKind);
            Assert.DoesNotContain(t, Rules.ForgedOnly);
            foreach (var kind in new[] { WeaponKind.Melee, WeaponKind.Ranged, WeaponKind.Caster })
                Assert.True(Rules.Allowed(t, kind, ModifierSet.Empty));
        }

        // Charges is ForgedOnly and nothing else.
        Assert.DoesNotContain(Charges, Rules.Excludes);
        Assert.DoesNotContain(Charges, Rules.Requires);
        Assert.DoesNotContain(Charges, Rules.RequiresKind);
    }

    [Fact]
    public void FirstStack_NeverDecreasesAnyValue_ExceptCharges()
    {
        foreach (var t in All.Where(t => t != Charges))
            Assert.True(Rules.Resolve(t, 1) >= Rules.Resolve(t, 0), $"{t}: first stack decreases the value");

        // Charges is the one negative graft, and it is restricted rather than re-priced.
        Assert.Contains(Charges, Rules.ForgedOnly);
    }

    [Fact]
    public void Symmetric_ExpandsAGroupToItsDirectedPairs()
    {
        var closure = ModifierRules.Symmetric([[Brace, Opportunist, Overwatch], [Light, Resonant]]);
        Assert.Equal(new[] { Overwatch, Opportunist }, closure[Brace]);
        Assert.Equal(new[] { Brace, Overwatch }, closure[Opportunist]);
        Assert.Equal(new[] { Brace, Opportunist }, closure[Overwatch]);
        Assert.Equal(new[] { Resonant }, closure[Light]);
        Assert.Equal(new[] { Light }, closure[Resonant]);
        Assert.Equal(5, closure.Count);
    }

    [Fact]
    public void Rules_IgnoreEnchantmentIds_AndReadTheirNumbersFromTuning()
    {
        var restricted = new RestrictedData(
            Excludes: [["flaming", "cold"], [nameof(Push), nameof(Drag)]],
            Requires: new Dictionary<string, string[]>
            {
                ["burning"] = ["flaming"],
                [nameof(Riposte)] = [nameof(Block)],
            },
            Kind: new Dictionary<string, string> { ["flaming"] = "caster", [nameof(Overwatch)] = "ranged" },
            ForgedOnly: ["burning", nameof(Charges)],
            NeverRolled: ["burning"]);
        var tuning = new Tuning
        {
            AcquiredHeadroom = 2,
            PerStack = new Dictionary<ModifierType, int> { [Block] = 4 },
            Offset = new Dictionary<ModifierType, int>(),
            MaxForged = new Dictionary<ModifierType, int> { [Block] = 1 },
        };

        var rules = new ModifierRules(tuning, restricted);

        Assert.Equal(new[] { Drag }, rules.Excludes[Push]);
        Assert.Equal(new[] { Push }, rules.Excludes[Drag]);
        Assert.Equal(2, rules.Excludes.Count);
        Assert.Equal(new[] { Block }, rules.Requires[Riposte]);
        Assert.Single(rules.Requires);
        Assert.Equal(WeaponKind.Ranged, rules.RequiresKind[Overwatch]);
        Assert.Single(rules.RequiresKind);
        Assert.Equal(new[] { Charges }, rules.ForgedOnly);

        Assert.Equal(2, rules.AcquiredHeadroom);
        Assert.Equal(3, rules.Cap(Brace, 1));
        Assert.Equal(8, rules.Resolve(Block, 2));
        Assert.Equal(3, rules.Resolve(CritMultiplier, 1));   // absent offset and per-stack fall back per key to the compiled 2 + 1
        Assert.Equal(1, rules.MaxForged(Block));              // override
        Assert.Equal(1, rules.MaxForged(Light));              // absent: the compiled override
        Assert.Equal(3, rules.MaxForged(Brace));              // in neither table: 3
    }

    [Fact]
    public void PartialTuningTables_FallBackPerKey_ToTheCompiledDefaults()
    {
        // A file that re-prices one modifier replaces the whole table on the
        // record; the rules still read every other key at its compiled value.
        var partial = new Tuning
        {
            PerStack = new Dictionary<ModifierType, int> { [Block] = 4 },
            Offset = new Dictionary<ModifierType, int> { [Charges] = 0 },
            MaxForged = new Dictionary<ModifierType, int> { [Block] = 1 },
        };
        var rules = new ModifierRules(partial, ContentDefaults.Restricted);

        foreach (var t in All)
        {
            Assert.Equal(t == Block ? 4 : Rules.PerStack(t), rules.PerStack(t));
            Assert.Equal(t == Charges ? 0 : Rules.Offset(t), rules.Offset(t));
            Assert.Equal(t == Block ? 1 : Rules.MaxForged(t), rules.MaxForged(t));
        }

        // No dead stacks from a partial file: every priced modifier is still worth something.
        foreach (var t in All.Where(t => t is not (OnHitPoison or Momentum)))
            Assert.True(rules.PerStack(t) > 0, $"{t}: a partial table zeroed its stacks");

        // An explicit zero is an override, not an absence.
        var zeroed = new ModifierRules(new Tuning { PerStack = new Dictionary<ModifierType, int> { [Block] = 0 } }, ContentDefaults.Restricted);
        Assert.Equal(0, zeroed.PerStack(Block));
        Assert.Equal(0, zeroed.Resolve(Block, 3));
    }

    [Fact]
    public void Rules_RefuseAModifierRelationNamingAnEnchantment()
    {
        // Allowed sees a weapon's modifiers and nothing else, so the member could only be dropped.
        var restricted = new RestrictedData([["flaming", nameof(Light)]], new Dictionary<string, string[]>(),
            new Dictionary<string, string>(), [], []);
        var ex = Assert.Throws<ContentException>(() => new ModifierRules(new Tuning(), restricted));
        Assert.Equal(nameof(Light), ex.EntryId);
        Assert.Equal(ContentValidator.RuleModifierRelationIds, ex.Rule);
    }

    [Fact]
    public void Rules_RejectAnUnknownKindValue()
    {
        var restricted = new RestrictedData([], new Dictionary<string, string[]>(),
            new Dictionary<string, string> { [nameof(Brace)] = "flying" }, [], []);
        var ex = Assert.Throws<ContentException>(() => new ModifierRules(new Tuning(), restricted));
        Assert.Equal(nameof(Brace), ex.EntryId);
        Assert.Equal(ContentValidator.RuleKindValue, ex.Rule);
    }

    [Fact]
    public void Tuning_CompiledDefaults_MatchTheDocs()
    {
        var tuning = GameContent.Current.Tuning;
        Assert.Same(ContentDefaults.Tuning, tuning);
        Assert.Equal(5, tuning.AcquiredHeadroom);
        Assert.Equal(23, tuning.PerStack.Count);
        Assert.Equal(1, tuning.EffectPerLevel[StatusEffectType.Regeneration]);
        Assert.Equal(5, tuning.OverhealPerWard);
        Assert.Equal(3, tuning.DotDamagePerMana);
        Assert.Equal(20, tuning.ApplyPercent["flaming"]);
        Assert.Equal(15, tuning.ApplyPercent["cold"]);
        Assert.Equal(25, tuning.ApplyPercent["shocking"]);
        Assert.Equal(15, tuning.ApplyPercent["acidic"]);
        Assert.Equal(10, tuning.MovementUnitsPerMana);
        Assert.Equal(16, (int)GameConstants.MaxDistance / tuning.MovementUnitsPerMana);   // a fully banked turn
        Assert.Equal(20, tuning.WeaponSwapCost);
        Assert.Equal(3, tuning.LongshotFreeTiles);
        Assert.False(tuning.FriendlyFireEnabled);
        Assert.Equal(50, tuning.FriendlyFireAllyPercent);
    }
}
