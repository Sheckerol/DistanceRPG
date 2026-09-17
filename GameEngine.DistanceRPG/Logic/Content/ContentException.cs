namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Invalid content aborts startup, naming the entry and the rule it broke —
/// never clamped, never skipped (§5.4). A broken invariant is something every
/// downstream caller assumes holds, and limping past it turns a typo into a
/// crash somewhere unrelated.
/// </summary>
public sealed class ContentException : Exception
{
    /// <summary>The id of the entry that broke the rule.</summary>
    public string EntryId { get; }

    /// <summary>The rule it broke, as a stable sentence tests can match on.</summary>
    public string Rule { get; }

    /// <param name="detail">What exactly broke it — a modifier, a count — for the message alone; <see cref="Rule"/> stays the stable sentence.</param>
    public ContentException(string entryId, string rule, string? detail = null)
        : base(detail == null
            ? $"Invalid content entry '{entryId}': {rule}"
            : $"Invalid content entry '{entryId}': {rule} ({detail})")
    {
        EntryId = entryId;
        Rule = rule;
    }
}
