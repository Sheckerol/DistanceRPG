namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Invalid content aborts startup, naming the entry and the rule it broke —
/// never clamped, never skipped (§5.4). A broken invariant is something every
/// downstream caller assumes holds, and limping past it turns a typo into a
/// crash somewhere unrelated.
/// <para>
/// The load stamps each failure with the file whose stage broke
/// (<see cref="In"/>, 1h), so the screen that stops the game
/// (<c>StartupErrorScene</c>) can name the file, the entry, the rule and the
/// detail from these properties alone, never by taking the message apart.
/// </para>
/// </summary>
public sealed class ContentException : Exception
{
    /// <summary>The id of the entry that broke the rule.</summary>
    public string EntryId { get; }

    /// <summary>The rule it broke, as a stable sentence tests can match on.</summary>
    public string Rule { get; }

    /// <summary>What exactly broke it — a modifier, a count, a path in a file — or null; <see cref="Rule"/> stays the stable sentence. A check that reads another file than the one its stage stamps names that file here.</summary>
    public string? Detail { get; }

    /// <summary>The file whose load stage broke (<c>restricted.json</c>), when the load knows it; null for a failure no stage has stamped.</summary>
    public string? File { get; }

    /// <summary>
    /// Whether that stage ran a record built in code rather than one read from
    /// the file: the compiled fallback, played because no file was found — shown
    /// as <c>(compiled)</c> beside <see cref="File"/>. The file beside it that did
    /// load is then what broke it, and the detail says which.
    /// </summary>
    public bool Compiled { get; }

    /// <param name="detail">What exactly broke it — a modifier, a count — beside the stable <see cref="Rule"/>.</param>
    public ContentException(string entryId, string rule, string? detail = null)
        : this(entryId, rule, detail, file: null, compiled: false, inner: null)
    {
    }

    private ContentException(string entryId, string rule, string? detail, string? file, bool compiled, Exception? inner)
        : base(Describe(entryId, rule, detail, file, compiled), inner)
    {
        EntryId = entryId;
        Rule = rule;
        Detail = detail;
        File = file;
        Compiled = compiled;
    }

    /// <summary>
    /// The same entry, rule and <see cref="Detail"/>, stamped with the file whose
    /// stage broke and whether that stage ran the compiled record; the failure
    /// stamped is kept as the inner exception. Stamping the same file again
    /// changes nothing.
    /// </summary>
    internal ContentException In(string file, bool compiled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        return File == file && Compiled == compiled
            ? this
            : new ContentException(EntryId, Rule, Detail, file, compiled, this);
    }

    /// <summary>The message: the entry, the rule and the detail, after the file and <c>(compiled)</c> when the failure is stamped.</summary>
    private static string Describe(string entryId, string rule, string? detail, string? file, bool compiled)
    {
        string what = detail == null
            ? $"Invalid content entry '{entryId}': {rule}"
            : $"Invalid content entry '{entryId}': {rule} ({detail})";
        return file == null ? what : $"{file}{(compiled ? " (compiled)" : "")}: {what}";
    }
}
