using System.Reflection;
using GameEngine.DistanceRPG.Logic;
using static GameEngine.DistanceRPG.Logic.ModifierType;

namespace GameEngine.DistanceRPG.Tests;

public class ModifierSetTests
{
    [Fact]
    public void With_AddsAndClampsAgainstForged()
    {
        var forged = ModifierSet.Of((CritWindow, 3));
        Assert.Equal(8, forged.With(CritWindow, 10, forged).Stacks(CritWindow));               // cap = 3 + 5
        Assert.Equal(8, ModifierSet.Empty.With(CritWindow, 10, forged).Stacks(CritWindow));
        Assert.Equal(5, ModifierSet.Empty.With(Block, 7, ModifierSet.Empty).Stacks(Block));    // never forged: caps at 5

        var source = ModifierSet.Of((Block, 1));
        var grown = source.With(Block, 1, source);
        Assert.NotSame(source, grown);
        Assert.Equal(1, source.Stacks(Block));   // source unchanged
        Assert.Equal(2, grown.Stacks(Block));
        Assert.Equal(0, grown.Stacks(Brace));    // absent is 0
    }

    [Fact]
    public void With_IsAdditive_AndANoOpAtTheCap()
    {
        var forged = ModifierSet.Of((Brace, 1));
        var set = forged;
        for (int i = 0; i < 5; i++)
            set = set.With(Brace, 1, forged);
        Assert.Equal(6, set.Stacks(Brace));

        // A stack landing on an already-capped modifier is a no-op, not a special case.
        Assert.Equal(6, set.With(Brace, 1, forged).Stacks(Brace));
        Assert.Equal(set, set.With(Brace, 3, forged));

        // The acquisition cap is per type: another type has its own budget.
        Assert.Equal(5, set.With(Block, 9, forged).Stacks(Block));
    }

    [Fact]
    public void With_RefusesANegativeDelta()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ModifierSet.Of((Block, 1)).With(Block, -1, ModifierSet.Empty));

    [Fact]
    public void Of_IsUnclamped_BecauseTheForgeIsCheckedByMaxForged()
    {
        var forged = ModifierSet.Of((CritWindow, 9), (CritWindow, 1));   // repeated types sum
        Assert.Equal(10, forged.Stacks(CritWindow));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModifierSet.Of((Block, -1)));
    }

    [Fact]
    public void Empty_HasNoStacksAndNoEntries()
    {
        foreach (var t in Enum.GetValues<ModifierType>())
            Assert.Equal(0, ModifierSet.Empty.Stacks(t));
        Assert.Empty(ModifierSet.Empty.Entries);
        Assert.Equal(ModifierSet.Empty, ModifierSet.Of());
    }

    [Fact]
    public void Entries_AreInEnumOrder_AndSkipAbsentTypes()
    {
        var set = ModifierSet.Of((Resonant, 1), (Brace, 2), (Light, 1));
        Assert.Equal(new[] { (Brace, 2), (Light, 1), (Resonant, 1) }, set.Entries);
        Assert.Equal("Brace x2, Light x1, Resonant x1", set.ToString());
    }

    [Fact]
    public void Value_ResolvesThroughTheLoadedRules()
    {
        Assert.Equal(2, ModifierSet.Empty.Value(CritMultiplier));                 // the offset alone
        Assert.Equal(3, ModifierSet.Of((CritMultiplier, 1)).Value(CritMultiplier));
        Assert.Equal(6, ModifierSet.Of((Block, 2)).Value(Block));
        Assert.Equal(0, ModifierSet.Empty.Value(Block));
    }

    [Fact]
    public void Equality_IsByStacks()
    {
        Assert.Equal(ModifierSet.Of((Block, 1), (Push, 1)), ModifierSet.Of((Push, 1), (Block, 1)));
        Assert.NotEqual(ModifierSet.Of((Block, 1)), ModifierSet.Of((Block, 2)));
        Assert.NotEqual(ModifierSet.Of((Block, 1)), ModifierSet.Of((Brace, 1)));
        Assert.Equal(ModifierSet.Of((Block, 1)).GetHashCode(), ModifierSet.Of((Block, 1)).GetHashCode());
    }

    [Fact]
    public void NoRemoveApi()
    {
        // Nothing is ever removed from a weapon, so a satisfied prerequisite stays satisfied.
        var members = typeof(ModifierSet).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var member in members)
            foreach (var forbidden in new[] { "Remove", "Without", "Decrement", "Clear", "Subtract" })
                Assert.DoesNotContain(forbidden, member.Name, StringComparison.OrdinalIgnoreCase);
    }
}
