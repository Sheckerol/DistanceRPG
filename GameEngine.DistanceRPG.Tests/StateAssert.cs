using System.Collections;
using System.Reflection;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// "Field for field" (A15), as an assertion: two object graphs are the same
/// state when every public instance property, walked recursively, reads the
/// same on both — a campaign and the campaign a save brought back, member by
/// member, weapon by weapon, entry by entry. The first difference fails the
/// test and names its path (<c>campaign.Party[2].Inventory[1].Modifiers</c>).
/// <list type="bullet">
/// <item>Numbers, strings, booleans and enums by value, and floats and doubles
/// <strong>bitwise</strong>: a position that came back one ulp away is not the
/// position that was saved.</item>
/// <item>A <see cref="ModifierSet"/> by its own <c>Equals</c>, its counts.</item>
/// <item>A content row — <see cref="EnchantmentDef"/>, <see cref="WeaponDef"/>,
/// <see cref="DungeonDef"/>, <see cref="PartyMemberDef"/> — <strong>by
/// reference</strong>: a restored entry must be the loaded catalogue's own row,
/// never a copy that happens to read alike. Two states under two contents are
/// therefore never equivalent by this; compare their captures instead.</item>
/// <item>A sequence element by element, a value tuple by its fields, anything
/// else by its public properties, in name order, with a guard against walking a
/// cycle — or the same pair — twice. Indexers and delegates are skipped.</item>
/// </list>
/// Public state only: what a save keeps below it (the raw pools) is compared
/// by the two captures' texts, which every round trip also asserts.
/// </summary>
internal static class StateAssert
{
    /// <summary>Fail, naming the path, at the first place <paramref name="actual"/> reads differently from <paramref name="expected"/>.</summary>
    public static void Equivalent(object? expected, object? actual, string root = "state")
    {
        if (Difference(expected, actual, root, new HashSet<(object, object)>(PairComparer.Instance)) is { } difference)
            Assert.Fail(difference);
    }

    private static readonly HashSet<Type> ContentRows = [typeof(EnchantmentDef), typeof(WeaponDef), typeof(DungeonDef), typeof(PartyMemberDef)];

    private static string? Difference(object? expected, object? actual, string path, HashSet<(object, object)> walked)
    {
        if (expected is null || actual is null)
            return expected is null && actual is null ? null : $"{path}: expected {Show(expected)}, was {Show(actual)}";

        // A sequence is its items, whatever collection holds them: a list the
        // live state grew and the array a load built read alike.
        if (expected is IEnumerable sequence and not string && actual is IEnumerable other and not string)
        {
            var left = sequence.Cast<object?>().ToList();
            var right = other.Cast<object?>().ToList();
            for (int i = 0; i < Math.Min(left.Count, right.Count); i++)
                if (Difference(left[i], right[i], $"{path}[{i}]", walked) is { } inner)
                    return inner;
            return left.Count == right.Count ? null : $"{path}: expected {left.Count} items, was {right.Count}";
        }

        var type = expected.GetType();
        if (type != actual.GetType())
            return $"{path}: expected a {type.Name}, was a {actual.GetType().Name}";

        switch (expected)
        {
            case float f:
                return BitConverter.SingleToInt32Bits(f) == BitConverter.SingleToInt32Bits((float)actual)
                    ? null : $"{path}: expected {f:R}, was {(float)actual:R}";
            case double d:
                return BitConverter.DoubleToInt64Bits(d) == BitConverter.DoubleToInt64Bits((double)actual)
                    ? null : $"{path}: expected {d:R}, was {(double)actual:R}";
            case string or decimal or Enum:
                return Equals(expected, actual) ? null : $"{path}: expected {Show(expected)}, was {Show(actual)}";
            case ModifierSet set:
                return set.Equals(actual) ? null : $"{path}: expected {set}, was {actual}";
            case Delegate:
                return null;
        }
        if (type.IsPrimitive)
            return Equals(expected, actual) ? null : $"{path}: expected {Show(expected)}, was {Show(actual)}";
        if (ContentRows.Contains(type))
            return ReferenceEquals(expected, actual) ? null : $"{path}: expected the content's own {type.Name} '{Show(expected)}', was another instance";

        if (!type.IsValueType && !walked.Add((expected, actual)))
            return null;   // this pair is being, or has been, walked

        if (type.IsGenericType && type.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (Difference(field.GetValue(expected), field.GetValue(actual), $"{path}.{field.Name}", walked) is { } inner)
                    return inner;
            return null;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is { IsPublic: true })
            .OrderBy(p => p.Name, StringComparer.Ordinal);
        foreach (var property in properties)
        {
            var (left, leftThrew) = Read(property, expected);
            var (right, rightThrew) = Read(property, actual);
            if (leftThrew != rightThrew)
                return $"{path}.{property.Name}: expected {(leftThrew ?? Show(left))}, was {(rightThrew ?? Show(right))}";
            if (leftThrew is not null)
                continue;   // both refuse the read, alike
            if (Difference(left, right, $"{path}.{property.Name}", walked) is { } inner)
                return inner;
        }
        return null;
    }

    /// <summary>A property's value, or the name of what reading it threw.</summary>
    private static (object? Value, string? Threw) Read(PropertyInfo property, object target)
    {
        try
        {
            return (property.GetValue(target), null);
        }
        catch (TargetInvocationException e)
        {
            return (null, $"a {e.InnerException?.GetType().Name} on read");
        }
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        _ => value.ToString() ?? value.GetType().Name,
    };

    /// <summary>Pairs by reference: a walked pair is the same two objects, never two that read alike.</summary>
    private sealed class PairComparer : IEqualityComparer<(object, object)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((object, object) x, (object, object) y) => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);

        public int GetHashCode((object, object) pair)
            => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Item1),
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Item2));
    }
}
