namespace GameEngine.DistanceRPG.Logic;

/// <summary>Why an actor's movement changed: what the ledger's <see cref="MovementLedger.Changed"/> carries.</summary>
public enum MoveReason
{
    /// <summary>Steps actually walked (a frame's, or an enemy's planned path).</summary>
    Walk,
    /// <summary>A swing or throw.</summary>
    Attack,
    /// <summary>A staff's or wand's cast.</summary>
    Cast,
    /// <summary>A shot held for Overwatch.</summary>
    Overwatch,
    /// <summary>A weapon swap, as the event table settled it.</summary>
    Swap,
    /// <summary>A potion drunk, as the event table settled it.</summary>
    Potion,
    /// <summary>Any other spend the event table settled.</summary>
    Other,
    /// <summary>A turn's (or an enemy action's) refill.</summary>
    TurnStart,
    /// <summary>The rest before a dungeon.</summary>
    Rest,
    /// <summary>Read back from a save (or set up by a test).</summary>
    Restore,
}

/// <summary>One change to an actor's movement: before, after, and why.</summary>
public readonly record struct MovementChange(float Before, float After, MoveReason Reason);

/// <summary>
/// An actor's movement left this turn, in logic units — and the only door it
/// changes through, so whatever shows it (the reach overlay, the MOVE readout)
/// hears every change (<see cref="Changed"/>) and why. Two ways to spend:
/// <list type="bullet">
/// <item><see cref="TrySpend"/> for an action, all or nothing: there is enough
/// and it is paid, or there is not and nothing changes.</item>
/// <item><see cref="SpendUpTo"/> for walking (and a spend the event table has
/// already settled): whatever is left, up to the amount, and says how much it
/// took — never below zero.</item>
/// </list>
/// The balance is a float — a walk spends fractions every frame — while action
/// costs are whole numbers for the party and the scaled fractions an enemy pays.
/// </summary>
public sealed class MovementLedger
{
    public MovementLedger(float left = 0f) => Left = Checked(left);

    /// <summary>Movement left, in logic units.</summary>
    public float Left { get; private set; }

    /// <summary>Raised after every change that moved the balance, with what it was, what it is, and why.</summary>
    public event Action<MovementChange>? Changed;

    /// <summary>Is there movement enough for <paramref name="cost"/>?</summary>
    public bool CanAfford(float cost) => Left >= cost;

    /// <summary>
    /// Pay <paramref name="cost"/> whole, or nothing: true when it was paid,
    /// false (and the balance untouched) when there is not enough.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cost"/> is negative or not a number.</exception>
    public bool TrySpend(float cost, MoveReason reason)
    {
        if (!(cost >= 0f)) throw new ArgumentOutOfRangeException(nameof(cost), cost, "A cost is never negative.");
        if (Left < cost) return false;
        Move(Left - cost, reason);
        return true;
    }

    /// <summary>
    /// Pay as much of <paramref name="wanted"/> as is left, and return how much
    /// that was: all of it, or the rest of the balance. Nothing for nothing wanted.
    /// </summary>
    public float SpendUpTo(float wanted, MoveReason reason)
    {
        if (!(wanted > 0f)) return 0f;
        float taken = MathF.Min(wanted, Left);
        Move(Left - taken, reason);
        return taken;
    }

    /// <summary>Set the balance outright: a turn's refill, a rest, a save read back.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="left"/> is negative or not a number.</exception>
    public void Reset(float left, MoveReason reason) => Move(Checked(left), reason);

    private void Move(float after, MoveReason reason)
    {
        float before = Left;
        Left = after;
        if (before != after) Changed?.Invoke(new MovementChange(before, after, reason));
    }

    private static float Checked(float left)
        => left >= 0f ? left : throw new ArgumentOutOfRangeException(nameof(left), left, "Movement left is never negative.");
}
