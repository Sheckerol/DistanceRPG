namespace GameEngine.DistanceRPG;

/// <summary>
/// What a dungeon scene reaches the game through: the run is over, or the
/// campaign should be saved. The scene never holds the concrete game that hosts
/// it (§6.7 — the campaign outlives every scene, and the scenes are views of it).
/// <para>
/// The run itself ends in the campaign (<see cref="Logic.CampaignState.EndRun"/>),
/// by the way out or by a wipe, and the scene shows its summary; the scene calls
/// <see cref="RunOver"/> once, when the player has read it and the screen has
/// faded to black. What comes next is the host's to decide: the screen between
/// runs (<see cref="BetweenRunsScene"/>), where the last run's summary sits above
/// the dungeon list and the next dungeon is chosen.
/// </para>
/// <para>
/// And the scene asks for the autosaves a run in progress takes (1f, Phase 5.8):
/// at every player turn's start and after every floor arrival
/// (<see cref="RequestSave"/>). It only asks. The host writes the save at the end
/// of the frame the request arrived in — never inside the turn system's raise
/// that asked — and only at a safe point
/// (<see cref="Logic.SaveGame.IsSafePoint"/>).
/// </para>
/// </summary>
public interface IRunHost
{
    /// <summary>The run the calling scene was playing is over and its summary read: show whatever follows. Called once per scene, never while its own fade is still running.</summary>
    void RunOver();

    /// <summary>
    /// Save the campaign — the run in progress in it — once the frame is over: a
    /// player turn has started, or the party has arrived on a floor. Asking twice
    /// in a frame is one save; a request that finds the enemies moving waits for
    /// the next safe point.
    /// </summary>
    void RequestSave();
}
