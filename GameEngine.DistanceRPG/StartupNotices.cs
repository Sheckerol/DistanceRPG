namespace GameEngine.DistanceRPG;

/// <summary>
/// What the game's startup had to say: the data files' notices, such as a
/// <c>tuning.json</c> value clamped or fitted, or a file missing or unreadable.
/// They are held for the first scene the game opens on, and handed to that
/// scene alone (<see cref="Take"/>). The screen between runs says them under
/// its title. A dungeon that a dev override boots straight into says them over
/// its first floor. Every later scene is handed none, and the log has them all.
/// A holder of its own rather than a field of the game, so the hand-over can be
/// tested without opening a window.
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

    /// <summary>The notices held, for the scene about to be shown. Only once: every call after the first returns none.</summary>
    public IReadOnlyList<string> Take()
    {
        var taken = _held;
        _held = [];
        return taken;
    }
}
