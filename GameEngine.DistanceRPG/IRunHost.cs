namespace GameEngine.DistanceRPG;

/// <summary>
/// What a dungeon scene reaches the game through when its run is over: one
/// method, so the scene never holds the concrete game that hosts it (§6.7 — the
/// campaign outlives every scene, and the scenes are views of it).
/// <para>
/// The run itself ends in the campaign (<see cref="Logic.CampaignState.EndRun"/>),
/// by the way out or by a wipe, and the scene shows its summary; the scene calls
/// this once, when the player has read it and the screen has faded to black.
/// What comes next is the host's to decide: today the next run, in the
/// campaign's next dungeon (<see cref="Logic.CampaignState.DefaultNext"/>) on a
/// new seed, with the party rested; the between-runs screen, once it exists,
/// through this same call.
/// </para>
/// </summary>
public interface IRunHost
{
    /// <summary>The run the calling scene was playing is over and its summary read: begin whatever follows. Called once per scene, never while its own fade is still running.</summary>
    void RunOver();
}
