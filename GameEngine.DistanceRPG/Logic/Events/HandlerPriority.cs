namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Where a handler runs in its event's chain — order is data (§1.7), and it
/// comes from one of two places rather than being invented per handler. A
/// combat or status step takes <see cref="Step"/> from the §1.6 damage
/// pipeline (1 roll, 2 Weakened, 3 Sundered, 4 enchantments, 5 Block, 6 Ward,
/// 7 crit riders, 8 displacement) with <see cref="Index"/> ordering handlers
/// inside a step; an enchantment takes <see cref="Index"/> from its position
/// in the weapon's list — attachment order. Sorted by step, then index; two
/// handlers on one event never share a priority.
/// </summary>
public readonly record struct HandlerPriority(int Step, int Index) : IComparable<HandlerPriority>
{
    public int CompareTo(HandlerPriority other)
    {
        int byStep = Step.CompareTo(other.Step);
        return byStep != 0 ? byStep : Index.CompareTo(other.Index);
    }

    public static bool operator <(HandlerPriority left, HandlerPriority right) => left.CompareTo(right) < 0;
    public static bool operator >(HandlerPriority left, HandlerPriority right) => left.CompareTo(right) > 0;
    public static bool operator <=(HandlerPriority left, HandlerPriority right) => left.CompareTo(right) <= 0;
    public static bool operator >=(HandlerPriority left, HandlerPriority right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"({Step},{Index})";
}

/// <summary>
/// One line of a printable chain: "what happens when I am hit" is a sorted
/// list you can print (§1.7), and this is its row.
/// </summary>
public sealed record HandlerInfo(GameEvent Event, HandlerPriority Priority, string Name)
{
    public override string ToString() => $"{Event} {Priority} {Name}";
}
