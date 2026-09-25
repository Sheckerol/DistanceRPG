using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The modifier rules as the game loads them: the relations, and the numbers the
/// rules read out of the tuning. No number expected here is a copy of the
/// master's. Where a test leans on one, it reads it from the tuning the game
/// reads (<see cref="ShippedContent.Tuning"/>); where the numbers are the test's
/// subject — that the rules read them from the tuning, and how they combine them —
/// the test moves them off the shipped ones, or states its own, over that tuning,
/// so no test compares a shipped table with rules built from that same table,
/// which a table kept in code would pass for as long as the two agreed. A table
/// edited in <c>tuning.json</c> fails nothing here: what is pinned is how the
/// rules read and combine the numbers, never what the numbers are.
/// </summary>
public class ModifierRulesTests
{
    private static ModifierRules Rules => GameContent.Current.Modifiers;
    private static readonly ModifierType[] All = Enum.GetValues<ModifierType>();

    /// <summary>The tuning the game reads: the numbers the rules are expected to resolve.</summary>
    private static Tuning Shipped => ShippedContent.Tuning;

    /// <summary>The rules over the shipped tuning with <paramref name="moves"/> made (<see cref="TuningValues.With"/>) and the shipped relations.</summary>
    private static ModifierRules Over(params (string Name, int Value)[] moves)
        => new(TuningValues.With(Shipped, moves), ShippedContent.Restricted);

    /// <summary>A step off <paramref name="value"/> within [<paramref name="min"/>, <paramref name="max"/>]: one up, or one down at the ceiling — never the value itself.</summary>
    private static int Step(int value, int min, int max) => value + 1 <= max ? value + 1 : Math.Max(min, value - 1);

    /// <summary>A step off <paramref name="value"/> within the range the tuning's table <paramref name="table"/> clamps to.</summary>
    private static int Step(string table, int value)
    {
        var key = (TableKey)TuningSchema.Find(table)!;
        return Step(value, key.Min, key.Max);
    }

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
    // The modifier, the offset and the per-stack price the test lays over the shipped tuning for it — its
    // own numbers, none of them the master's to say — the stacks, and what they resolve to, worked by hand.
    [InlineData(CritMultiplier, 2, 3, 0, 2)]    // no stacks: the offset alone, whatever a stack is priced at
    [InlineData(CritMultiplier, 2, 3, 1, 5)]    // the first stack on top of the offset
    [InlineData(CritMultiplier, 2, 3, 8, 26)]   // only a unique that raised CritMultiplier gets this deep
    [InlineData(Charges, 1, 2, 1, 3)]           // a thrower forged x1
    [InlineData(Charges, 1, 2, 8, 17)]          // a thrower worked to the top
    [InlineData(CritMultiplier, 2, 3, 6, 20)]   // forged x1 and fully worked
    [InlineData(Block, 1, 4, 3, 13)]            // The Bulwark's depth
    [InlineData(Light, 0, 7, 5, 35)]            // no offset: the price times the stacks — a maxed graft
    [InlineData(Light, 0, 7, 6, 42)]            // the Light ceiling's depth
    [InlineData(Resonant, 5, 0, 6, 5)]          // a stack priced at nothing adds nothing to the offset
    [InlineData(CritWindow, 0, 2, 8, 16)]
    [InlineData(Brace, 0, 0, 0, 0)]             // nothing at all
    public void Resolve_IsOffsetPlusPerStackTimesStacks(ModifierType t, int offset, int perStack, int stacks, int expected)
    {
        // What Resolve sums is known without reading the master, so the sum is checked, not restated: an
        // offset, a price and a depth the test states, and the total worked by hand.
        Assert.Equal(expected, Over(($"Offset.{t}", offset), ($"PerStack.{t}", perStack)).Resolve(t, stacks));
    }

    [Fact]
    public void TheRulesReadEveryTableFromTheTuning_MemberByMember()
    {
        // The rules keep no number of their own: what a stack of each modifier is worth, what it grants
        // before its first stack, how deep it may be forged, and the headroom every acquired source shares
        // are the tuning's (tuning.json's PerStack, Offset, MaxForged and AcquiredHeadroom). Checked as the
        // tuning schema's tables are (TuningSchemaTests.EveryTableReadsAsTheGameReadsIt): a tuning built
        // from the shipped one with one member of each table moved — a step within its range, so a number
        // the shipped table does not give it — is read at the moved numbers for that member and at the
        // built-in numbers for every other. Rules compared with the very tables they were built from would
        // agree with a table kept in code for as long as the two matched; a moved member cannot be matched.
        foreach (var t in All)
        {
            var moved = (PerStack: Step(nameof(Tuning.PerStack), Shipped.PerStack[t]),
                Offset: Step(nameof(Tuning.Offset), Shipped.Offset[t]),
                MaxForged: Step(nameof(Tuning.MaxForged), Shipped.MaxForged[t]));
            Assert.NotEqual((Shipped.PerStack[t], Shipped.Offset[t], Shipped.MaxForged[t]), moved);
            var rules = Over(($"PerStack.{t}", moved.PerStack), ($"Offset.{t}", moved.Offset), ($"MaxForged.{t}", moved.MaxForged));
            foreach (var other in All)
                Assert.Equal(other == t ? moved : (Shipped.PerStack[other], Shipped.Offset[other], Shipped.MaxForged[other]),
                    (rules.PerStack(other), rules.Offset(other), rules.MaxForged(other)));
            Assert.Equal(Shipped.AcquiredHeadroom, rules.AcquiredHeadroom);   // a table's move is no headroom's
        }

        // The headroom, moved, is read as moved — and the tables are left as they were.
        var headroomKey = (IntKey)TuningSchema.Find(nameof(Tuning.AcquiredHeadroom))!;
        int headroom = Step(Shipped.AcquiredHeadroom, headroomKey.Min, headroomKey.Max);
        Assert.NotEqual(Shipped.AcquiredHeadroom, headroom);
        var roomier = Over((nameof(Tuning.AcquiredHeadroom), headroom));
        Assert.Equal(headroom, roomier.AcquiredHeadroom);
        Assert.All(All, t => Assert.Equal((Shipped.PerStack[t], Shipped.Offset[t], Shipped.MaxForged[t]),
            (roomier.PerStack(t), roomier.Offset(t), roomier.MaxForged(t))));
    }

    [Fact]
    public void Cap_IsForgedPlusHeadroom_NoFlatTerm()
    {
        // A cap is the forged count plus the acquired headroom and nothing else. The headroom is the test's
        // own — 4, laid over the shipped tuning; the game's own is tuning.json's to say — so every ceiling
        // below is worked by hand, for every modifier alike.
        var rules = Over((nameof(Tuning.AcquiredHeadroom), 4));
        foreach (var t in All)
        {
            Assert.Equal(7, rules.Cap(t, 3));       // Widowmaker's CritWindow x3
            Assert.Equal(6, rules.Cap(t, 2));       // Assassin's Fang, the Purity dagger: one higher than its siblings
            Assert.Equal(5, rules.Cap(t, 1));       // Flensing Knife: CritWindow and Light alike
            Assert.Equal(4, rules.Cap(t, 0));       // never forged: the headroom alone
            Assert.Equal(104, rules.Cap(t, 100));   // nothing is capped
        }
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
    public void NeverStacked_MomentumAndOnHitPoison_AreRefusedEverywhere()
    {
        // Momentum is a refund and so an enchantment (the Shieldbreaker's soul); OnHitPoison is reserved with no
        // behaviour. Neither is in the §1.1 table, so a stack of either would change nothing — the dead
        // stack the table forbids — and the one predicate refuses both on every kind, beside anything, even
        // beside a stack of itself: no forge, roll or graft reaches one.
        Assert.Equal(new[] { OnHitPoison, Momentum }, ModifierRules.NeverStacked);
        foreach (var t in ModifierRules.NeverStacked)
            foreach (var kind in new[] { WeaponKind.Melee, WeaponKind.Ranged, WeaponKind.Caster })
            {
                Assert.False(Rules.Allowed(t, kind, ModifierSet.Empty));
                Assert.False(Rules.Allowed(t, kind, ModifierSet.Of((t, 1))));
            }

        // Every other member is in the table: some kind of weapon, holding what it needs, may carry it.
        foreach (var t in All.Except(ModifierRules.NeverStacked))
        {
            var present = ModifierSet.Of([(t, 1), .. Rules.Requires.GetValueOrDefault(t, []).Select(p => (p, 1))]);
            Assert.True(new[] { WeaponKind.Melee, WeaponKind.Ranged, WeaponKind.Caster }.Any(kind => Rules.Allowed(t, kind, present)),
                $"{t} is allowed on no weapon");
        }
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
        var tuning = TuningValues.With(ShippedContent.Tuning, (nameof(Tuning.AcquiredHeadroom), 2), ("PerStack.Block", 4), ("MaxForged.Block", 1));

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
        Assert.Equal(Shipped.Offset[Block] + 4 * 2, rules.Resolve(Block, 2));   // the moved per-stack, over the offset left alone
        Assert.Equal(Shipped.Offset[CritMultiplier] + Shipped.PerStack[CritMultiplier], rules.Resolve(CritMultiplier, 1));   // the offset and per-stack the tuning leaves alone: the shipped ones
        Assert.Equal(1, rules.MaxForged(Block));                          // moved
        Assert.Equal(Shipped.MaxForged[Light], rules.MaxForged(Light));   // left alone: the shipped limit
        Assert.Equal(Shipped.MaxForged[Brace], rules.MaxForged(Brace));   // left alone: the shipped limit
    }

    [Fact]
    public void APartialFileMergesPerEntry_ATableBuiltInCodeNamesEveryMember()
    {
        // A file that re-prices one modifier names that entry alone, and the read
        // merges it over the built-in table: the rules still read every other key
        // at its shipped value.
        var partial = TuningJson.Read("""{ "PerStack": { "Block": 4 }, "Offset": { "Charges": 0 }, "MaxForged": { "Block": 1 } }""", ShippedContent.Tuning).Tuning;
        var rules = new ModifierRules(partial, ShippedContent.Restricted);

        foreach (var t in All)
        {
            Assert.Equal(t == Block ? 4 : Rules.PerStack(t), rules.PerStack(t));
            Assert.Equal(t == Charges ? 0 : Rules.Offset(t), rules.Offset(t));
            Assert.Equal(t == Block ? 1 : Rules.MaxForged(t), rules.MaxForged(t));
        }

        // No dead stacks from a partial file: every modifier the shipped table prices is still worth something.
        foreach (var t in All.Where(t => Shipped.PerStack[t] > 0))
            Assert.True(rules.PerStack(t) > 0, $"{t}: a partial table zeroed its stacks");

        // An explicit zero is an override, not an absence: three stacks are worth the offset alone.
        var zeroed = new ModifierRules(TuningJson.Read("""{ "PerStack": { "Block": 0 } }""", ShippedContent.Tuning).Tuning, ShippedContent.Restricted);
        Assert.Equal(0, zeroed.PerStack(Block));
        Assert.Equal(Shipped.Offset[Block], zeroed.Resolve(Block, 3));

        // A table built in code that names one entry alone — no file, so no merge —
        // has no number under the other twenty-two: refused, naming the first it
        // leaves out, rather than read as worth nothing.
        var refused = Assert.Throws<ContentException>(() =>
            new ModifierRules(ShippedContent.Tuning with { PerStack = new Dictionary<ModifierType, int> { [Block] = 4 } }, ShippedContent.Restricted));
        Assert.Equal(("PerStack.Brace", ContentValidator.RuleTableNamesEveryMember), (refused.EntryId, refused.Rule));
    }

    [Fact]
    public void Rules_RefuseAModifierRelationNamingAnEnchantment()
    {
        // Allowed sees a weapon's modifiers and nothing else, so the member could only be dropped.
        var restricted = new RestrictedData([["flaming", nameof(Light)]], new Dictionary<string, string[]>(),
            new Dictionary<string, string>(), [], []);
        var ex = Assert.Throws<ContentException>(() => new ModifierRules(ShippedContent.Tuning, restricted));
        Assert.Equal(nameof(Light), ex.EntryId);
        Assert.Equal(ContentValidator.RuleModifierRelationIds, ex.Rule);
    }

    [Fact]
    public void Rules_RejectAnUnknownKindValue()
    {
        var restricted = new RestrictedData([], new Dictionary<string, string[]>(),
            new Dictionary<string, string> { [nameof(Brace)] = "flying" }, [], []);
        var ex = Assert.Throws<ContentException>(() => new ModifierRules(ShippedContent.Tuning, restricted));
        Assert.Equal(nameof(Brace), ex.EntryId);
        Assert.Equal(ContentValidator.RuleKindValue, ex.Rule);
    }

    [Fact]
    public void Tuning_IsTheShippedTuning_PricingEveryModifier()
    {
        // What each key is set to is tuning.json's to say: its doc-fixed numbers
        // are pinned where their docs are (ShippedDataTests lists them), and no
        // other is pinned anywhere. What is the code's is that the game plays the
        // shipped tuning, and that its per-stack table prices every modifier.
        var tuning = GameContent.Current.Tuning;
        Assert.Same(ShippedContent.Tuning, tuning);
        Assert.Equal(23, tuning.PerStack.Count);
    }
}
