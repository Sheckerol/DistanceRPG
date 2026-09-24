namespace GameEngine.DistanceRPG;

/// <summary>
/// What the game has to say on the next screen it shows. At startup that is the
/// data files' notices — a <c>tuning.json</c> value clamped or fitted, a file
/// missing or unreadable (<see cref="Hold"/>) — and the save's word on the
/// campaign, which leads them (<see cref="Lead"/>): that it resumed, that the
/// numbers changed since it was saved, that a save was set aside and where.
/// Later, a save that could not be written says so the same way. They are held
/// for the next scene the game opens, and handed to that scene alone
/// (<see cref="Take"/>). The screen between runs says them under its title,
/// the save's word whole (<see cref="Leading"/>, <see cref="NoticeText.Rows"/>);
/// a dungeon that a dev override boots straight into says them over its first
/// floor. A scene is handed only what arrived since the last one was, and the
/// log has them all. A holder of its own rather than a field of the game, so the
/// hand-over can be tested without opening a window.
/// </summary>
internal sealed class StartupNotices
{
    // The save's word on the campaign, first, and what the data files raised
    // after it: kept apart so a screen can show the one whole and cap the other.
    private IReadOnlyList<string> _lead = [];
    private IReadOnlyList<string> _held = [];

    /// <summary>Hold <paramref name="notices"/> — the data files' — for the first scene, in place of any held before; the save's word, led, stays ahead of them.</summary>
    public void Hold(IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        _held = notices;
    }

    /// <summary>
    /// Put <paramref name="notices"/> ahead of anything held. The save's word on
    /// the campaign comes first — what is being played, what became of a save —
    /// and the screen between runs shows it whole, ahead of the data files'
    /// notices, which it caps (<see cref="Leading"/>).
    /// </summary>
    public void Lead(IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        if (notices.Count > 0)
            _lead = [.. notices, .. _lead];
    }

    /// <summary>
    /// How many of the notices <see cref="Take"/> hands over next lead them — the
    /// save's word, which the screen between runs shows whole. Read it before the
    /// take, which empties the holder.
    /// </summary>
    public int Leading => _lead.Count;

    /// <summary>The notices held, the led ones first, for the scene about to be shown. Only once: every call after the first returns none, until more are held.</summary>
    public IReadOnlyList<string> Take()
    {
        IReadOnlyList<string> taken = [.. _lead, .. _held];
        _lead = [];
        _held = [];
        return taken;
    }
}
