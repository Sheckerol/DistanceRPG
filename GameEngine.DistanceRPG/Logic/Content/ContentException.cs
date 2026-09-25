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
    /// Whether that stage ran the copy of the file the game carries built in —
    /// played because no copy beside the executable was there to read, or the
    /// built-in <c>tuning.json</c> every read stands on — shown as
    /// <c>(built-in)</c> beside <see cref="File"/>. Either the built-in copy is
    /// itself broken (a master copy edited and never tested: the fix is in
    /// <c>GameEngine.DistanceRPG/Data</c>), or a file beside it that did load
    /// broke it, and the detail says which.
    /// </summary>
    public bool BuiltIn { get; }

    /// <param name="detail">What exactly broke it — a modifier, a count — beside the stable <see cref="Rule"/>.</param>
    public ContentException(string entryId, string rule, string? detail = null)
        : this(entryId, rule, detail, file: null, builtIn: false, inner: null)
    {
    }

    private ContentException(string entryId, string rule, string? detail, string? file, bool builtIn, Exception? inner)
        : base(Describe(entryId, rule, detail, file, builtIn), inner)
    {
        EntryId = entryId;
        Rule = rule;
        Detail = detail;
        File = file;
        BuiltIn = builtIn;
    }

    /// <summary>
    /// The same entry, rule and <see cref="Detail"/>, stamped with the file whose
    /// stage broke and whether that stage ran the built-in copy; the failure
    /// stamped is kept as the inner exception. Stamping the same file again
    /// changes nothing.
    /// </summary>
    internal ContentException In(string file, bool builtIn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        return File == file && BuiltIn == builtIn
            ? this
            : new ContentException(EntryId, Rule, Detail, file, builtIn, this);
    }

    /// <summary>The message: the entry, the rule and the detail, after the file and <c>(built-in)</c> when the failure is stamped.</summary>
    private static string Describe(string entryId, string rule, string? detail, string? file, bool builtIn)
    {
        string what = detail == null
            ? $"Invalid content entry '{entryId}': {rule}"
            : $"Invalid content entry '{entryId}': {rule} ({detail})";
        return file == null ? what : $"{file}{(builtIn ? " (built-in)" : "")}: {what}";
    }
}
