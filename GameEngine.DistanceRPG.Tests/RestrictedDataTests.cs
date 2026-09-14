using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

public class RestrictedDataTests
{
    private static readonly IReadOnlySet<string> Known = ModifierRules.ModifierIds;

    private static RestrictedData Data(
        IReadOnlyList<string[]>? excludes = null,
        IReadOnlyDictionary<string, string[]>? requires = null,
        IReadOnlyDictionary<string, string>? kind = null,
        string[]? forgedOnly = null,
        string[]? neverRolled = null)
        => new(
            excludes ?? Array.Empty<string[]>(),
            requires ?? new Dictionary<string, string[]>(),
            kind ?? new Dictionary<string, string>(),
            forgedOnly ?? Array.Empty<string>(),
            neverRolled ?? Array.Empty<string>());

    private static HashSet<string> KnownPlus(params string[] enchantmentIds)
    {
        var ids = new HashSet<string>(Known, StringComparer.Ordinal);
        ids.UnionWith(enchantmentIds);
        return ids;
    }

    [Fact]
    public void DefaultRestricted_Validates()
    {
        var known = KnownPlus(ContentDefaults.EnchantmentIds.ToArray());
        ContentValidator.ValidateRestricted(ContentDefaults.Restricted, known);   // does not throw
        Assert.NotNull(GameContent.Load(ContentDefaults.Tuning, ContentDefaults.Restricted, ContentDefaults.EnchantmentIds));
        Assert.NotNull(GameContent.Current);   // the static init loaded the same defaults
    }

    [Fact]
    public void Default_DeclaresTheDocumentedRelations()
    {
        var r = ContentDefaults.Restricted;

        Assert.Equal(7, r.Excludes.Count);
        Assert.Equal(new[] { "Brace", "Opportunist", "Overwatch" }, r.Excludes[0]);
        Assert.Equal(new[] { "Push", "Drag", "Rout" }, r.Excludes[1]);
        Assert.Equal(new[] { "Riposte", "BlockWeaken" }, r.Excludes[2]);
        Assert.Equal(new[] { "CritWeaken", "CritSunder" }, r.Excludes[3]);
        Assert.Equal(new[] { "Light", "Resonant" }, r.Excludes[4]);
        Assert.Equal(new[] { "flaming", "cold" }, r.Excludes[5]);
        Assert.Equal(new[] { "shocking", "acidic" }, r.Excludes[6]);

        Assert.Equal(3, r.Requires.Count);
        Assert.Equal(new[] { "Block" }, r.Requires["Riposte"]);
        Assert.Equal(new[] { "Block" }, r.Requires["BlockWeaken"]);
        Assert.Equal(new[] { "Cleave" }, r.Requires["Rout"]);

        Assert.Equal(3, r.Kind.Count);
        Assert.Equal("melee", r.Kind["Brace"]);
        Assert.Equal("melee", r.Kind["Opportunist"]);
        Assert.Equal("ranged", r.Kind["Overwatch"]);

        Assert.Equal(new[] { "Charges" }, r.ForgedOnly);
        Assert.Empty(r.NeverRolled);
    }

    [Fact]
    public void Validator_RejectsSelfExclusion_UnknownId_RequiresCycle_RequiresAndExcludes()
    {
        var self = Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(excludes: [["Brace", "Brace"]]), Known));
        Assert.Equal("Brace", self.EntryId);
        Assert.Equal(ContentValidator.RuleSelfExclusion, self.Rule);

        var unknown = Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(excludes: [["Brace", "Flamign"]]), Known));
        Assert.Equal("Flamign", unknown.EntryId);
        Assert.Equal(ContentValidator.RuleUnknownId, unknown.Rule);

        var cycle = Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(
                Data(requires: new Dictionary<string, string[]> { ["Riposte"] = ["Block"], ["Block"] = ["Riposte"] }),
                Known));
        Assert.Contains(cycle.EntryId, new[] { "Block", "Riposte" });
        Assert.Equal(ContentValidator.RuleRequiresAcyclic, cycle.Rule);

        var selfRequire = Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(requires: new Dictionary<string, string[]> { ["Block"] = ["Block"] }), Known));
        Assert.Equal("Block", selfRequire.EntryId);
        Assert.Equal(ContentValidator.RuleRequiresAcyclic, selfRequire.Rule);

        var both = Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(
                Data(excludes: [["Riposte", "Block"]],
                     requires: new Dictionary<string, string[]> { ["Riposte"] = ["Block"] }),
                Known));
        Assert.Equal("Riposte", both.EntryId);
        Assert.Equal(ContentValidator.RuleRequiresAndExcludes, both.Rule);

        // Every abort names both the entry and the rule.
        foreach (var ex in new[] { self, unknown, cycle, selfRequire, both })
        {
            Assert.Contains(ex.EntryId, ex.Message);
            Assert.Contains(ex.Rule, ex.Message);
        }
    }

    [Fact]
    public void Validator_RejectsUnknownIds_InEverySection()
    {
        Assert.Equal("Typo", Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(requires: new Dictionary<string, string[]> { ["Typo"] = ["Block"] }), Known)).EntryId);
        Assert.Equal("Typo", Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(requires: new Dictionary<string, string[]> { ["Riposte"] = ["Typo"] }), Known)).EntryId);
        Assert.Equal("Typo", Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(kind: new Dictionary<string, string> { ["Typo"] = "melee" }), Known)).EntryId);
        Assert.Equal("Typo", Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(forgedOnly: ["Typo"]), Known)).EntryId);
        Assert.Equal("Typo", Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(neverRolled: ["Typo"]), Known)).EntryId);
    }

    [Fact]
    public void Validator_RejectsAnUnknownKindValue()
    {
        var ex = Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(Data(kind: new Dictionary<string, string> { ["Brace"] = "flying" }), Known));
        Assert.Equal("Brace", ex.EntryId);
        Assert.Equal(ContentValidator.RuleKindValue, ex.Rule);
    }

    [Fact]
    public void Validator_RejectsATransitivePrerequisiteThatIsExcluded()
    {
        var ex = Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(
                Data(excludes: [["Rout", "Block"]],
                     requires: new Dictionary<string, string[]> { ["Rout"] = ["Cleave"], ["Cleave"] = ["Block"] }),
                Known));
        Assert.Equal("Rout", ex.EntryId);
        Assert.Equal(ContentValidator.RuleRequiresAndExcludes, ex.Rule);
    }

    [Fact]
    public void Validator_RejectsRequiringTwoIdsThatExcludeEachOther()
    {
        var ex = Assert.Throws<ContentException>(() =>
            ContentValidator.ValidateRestricted(
                Data(excludes: [["Block", "Cleave"]],
                     requires: new Dictionary<string, string[]> { ["Rout"] = ["Block", "Cleave"] }),
                Known));
        Assert.Equal("Rout", ex.EntryId);
        Assert.Equal(ContentValidator.RuleRequiresConflict, ex.Rule);
    }

    [Fact]
    public void Validator_AcceptsRelationsOverEnchantmentIds_OnceTheyAreKnown()
    {
        var data = Data(
            excludes: [["flaming", "cold"]],
            requires: new Dictionary<string, string[]> { ["burning"] = ["flaming"] },
            neverRolled: ["burning"]);
        ContentValidator.ValidateRestricted(data, KnownPlus("flaming", "cold", "burning"));   // does not throw

        var ex = Assert.Throws<ContentException>(() => ContentValidator.ValidateRestricted(data, Known));
        Assert.Equal("flaming", ex.EntryId);
        Assert.Equal(ContentValidator.RuleUnknownId, ex.Rule);
    }

    [Fact]
    public void GameContent_Load_AbortsOnInvalidRelations_AndLeavesCurrentUntouched()
    {
        var before = GameContent.Current;
        var ex = Assert.Throws<ContentException>(() => GameContent.Load(new Tuning(), Data(excludes: [["Brace", "Brace"]])));
        Assert.Equal(ContentValidator.RuleSelfExclusion, ex.Rule);
        Assert.Same(before, GameContent.Current);
    }

    [Fact]
    public void GameContent_Load_BuildsRulesFromTheData_WithoutBecomingCurrent()
    {
        var content = GameContent.Load(new Tuning { AcquiredHeadroom = 1 }, Data(excludes: [["Block", "Cleave"]]));
        Assert.Equal(1, content.Modifiers.AcquiredHeadroom);
        Assert.Equal(new[] { Cleave }, content.Modifiers.Excludes[Block]);
        Assert.NotSame(content, GameContent.Current);
        Assert.Equal(5, GameContent.Current.Modifiers.AcquiredHeadroom);

        // Use makes content current; re-using the current content is the only safe swap in a parallel suite.
        GameContent.Use(GameContent.Current);
        Assert.Same(ContentDefaults.Tuning, GameContent.Current.Tuning);
    }

    [Theory]
    [InlineData("melee", WeaponKind.Melee, true)]
    [InlineData("ranged", WeaponKind.Ranged, true)]
    [InlineData("caster", WeaponKind.Caster, true)]
    [InlineData("Melee", (WeaponKind)0, false)]
    [InlineData("any", (WeaponKind)0, false)]
    public void TryParseKind_AcceptsExactlyTheThreeFileValues(string kind, WeaponKind expected, bool ok)
    {
        Assert.Equal(ok, RestrictedData.TryParseKind(kind, out var parsed));
        Assert.Equal(expected, parsed);
    }
}
