namespace GameEngine.DistanceRPG;

/// <summary>
/// What a dungeon scene reaches the game through when its run is over: one
/// method, so the scene never holds the concrete game that hosts it (§6.7 — the
/// campaign outlives every scene, and the scenes are views of it).
/// <para>
/// The scene calls it once, when the party has left the dungeon and the screen
/// has faded to black; what comes next is the host's to decide. Today that is
/// a fresh visit to the same dungeon — every floor regenerated on a new seed,
/// the party rested (§4.1: leaving discards the whole visit). The run's end
/// with its three outcomes will hand over to the next run, and the between-runs
/// screen after that, through this same call.
/// </para>
/// </summary>
public interface IRunHost
{
    /// <summary>The run the calling scene was playing is over: begin whatever follows. Called once per scene, never while its own fade is still running.</summary>
    void RunOver();
}
