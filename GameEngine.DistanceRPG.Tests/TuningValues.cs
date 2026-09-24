using System.Globalization;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// A tuning as the numbers it holds — every scalar, and every table entry as the
/// game reads it — keyed as <c>tuning.json</c> names them (<c>MovementBudget</c>,
/// <c>PerStack.Block</c>). Two tunings are "the same numbers" exactly when these
/// are equal, whatever instances their tables are: what the tuning tests compare,
/// rather than record equality, which compares a table by reference.
/// </summary>
internal static class TuningValues
{
    public static IReadOnlyDictionary<string, string> Of(Tuning tuning)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in TuningSchema.Keys)
        {
            switch (key)
            {
                case IntKey k:
                    values[k.Name] = k.Get(tuning).ToString(CultureInfo.InvariantCulture);
                    break;
                case BoolKey k:
                    values[k.Name] = k.Get(tuning) ? "true" : "false";
                    break;
                case TableKey k:
                    foreach (var (member, value) in k.Get(tuning))
                        values[$"{k.Name}.{member}"] = value.ToString(CultureInfo.InvariantCulture);
                    break;
            }
        }
        return values;
    }

    /// <summary>Every number <paramref name="actual"/> holds is <paramref name="expected"/>'s.</summary>
    public static void Same(Tuning expected, Tuning actual) => Assert.Equal(Of(expected), Of(actual));

    /// <summary>The names whose numbers differ between the two, in ordinal order — a name only one of them holds included.</summary>
    public static string[] Moved(Tuning from, Tuning to)
    {
        var before = Of(from);
        var after = Of(to);
        return before.Keys.Union(after.Keys)
            .Where(name => before.GetValueOrDefault(name) != after.GetValueOrDefault(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
