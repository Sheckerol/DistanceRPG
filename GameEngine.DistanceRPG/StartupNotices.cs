namespace GameEngine.DistanceRPG;

/// <summary>
/// What the game has to say on the next screen it shows. At startup that is the
/// data files' notices — a <c>tuning.json</c> value clamped or fitted, a file
/// missing or unreadable (<see cref="Hold"/>) — and the save's word on the
/// campaign, which leads them (<see cref="Lead"/>): that it resumed, that a
/// save was set aside and where, that the numbers changed since it was saved.
/// Later, a save that could not be written says so the same way. They are held
/// for the next scene the game opens, and handed to that scene alone
/// (<see cref="Take"/>). The screen between runs says them under its title; a
/// dungeon that a dev override boots straight into says them over its first
/// floor. A scene is handed only what arrived since the last one was, and the
/// log has them all. A holder of its own rather than a field of the game, so the
/// hand-over can be tested without opening a window.
/// </summary>
internal sealed class StartupNotices
{
    private IReadOnlyList<string> _held = [];

    /// <summary>Hold <paramref name="notices"/> for the first scene, in place of anything held before.</summary>
    public void Hold(IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        _held = notices;
    }

    /// <summary>
    /// Put <paramref name="notices"/> ahead of anything held. The save's word on
    /// the campaign comes first — what is being played, what became of a save —
    /// since the screen shows a few rows and the log has the rest.
    /// </summary>
    public void Lead(IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        if (notices.Count > 0)
            _held = [.. notices, .. _held];
    }

    /// <summary>The notices held, for the scene about to be shown. Only once: every call after the first returns none, until more are held.</summary>
    public IReadOnlyList<string> Take()
    {
        var taken = _held;
        _held = [];
        return taken;
    }
}
