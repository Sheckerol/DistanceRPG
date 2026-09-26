namespace GameEngine.DistanceRPG;

/// <summary>
/// What both screens with a campaign in play reach the game through for its
/// saves and for leaving play (the user's ruling of 2026-09-25 on the save model,
/// R2(d), R2(e)): the menu ESC opens — in the dungeon the pause menu, between runs
/// the same menu over the hub (<see cref="PauseMenu"/>) — saves into a manual slot
/// at a safe point and quits to the title. The screens never hold the concrete
/// game that hosts them (§6.7).
/// <para>
/// <strong>A save by hand is only asked for</strong>, as the autosaves are
/// (<see cref="IRunHost.RequestSave"/>): the host writes it once the frame is
/// over, re-asking the safe point there (<c>CampaignBoot.SaveTo</c>), and tells the
/// screen how it went — the words the menu draws under itself. The campaign's own
/// slot, which every save of it goes into already (the user's ruling of 2026-09-25
/// on the save slots, R5), is shown on the list and never offered
/// (<see cref="OwnSlot"/>). <strong>Leaving play</strong> is the host's too: the
/// campaign's slot written through the window close's own door, then the title. A
/// dev-override session has no title (<see cref="HasTitle"/>): it saves only its
/// scratch slot, and its menu quits the game instead.
/// </para>
/// </summary>
public interface ISaveHost
{
    /// <summary>The save folder's slots as they stand now, read afresh (the listing never moves a file): what the save list offers. A dev-override session has none to offer.</summary>
    SaveListing Slots();

    /// <summary>The slot the campaign in play saves into, by number — the one it was loaded from or begun in — which the save list shows as saving itself and never offers; 0 for the autosave, and for a dev override's scratch slot, which has no list.</summary>
    int OwnSlot { get; }

    /// <summary>
    /// Save the campaign into manual slot <paramref name="slot"/>, chosen on the save
    /// list — once the frame is over, and only at a safe point — then tell the screen
    /// how it went. Asked once per choice; a dev-override session refuses it.
    /// </summary>
    void SaveToSlot(int slot);

    /// <summary>
    /// Leave play for the title: the campaign's slot written as the window's close
    /// writes it — a quit during the enemies' turn played out to the next player
    /// turn first — the campaign dropped, and the title shown. Never asked in a
    /// dev-override session, whose menu closes the game instead.
    /// </summary>
    void QuitToTitle();

    /// <summary>Whether this session has a title to quit to and slots to save into: false for a dev-override launch, which plays a scratch campaign apart from both.</summary>
    bool HasTitle { get; }
}

/// <summary>
/// What a dungeon scene reaches the game through: the run is over, or the
/// campaign should be saved — by itself, or by hand into a slot, and play left
/// for the title (<see cref="ISaveHost"/>). The scene never holds the concrete
/// game that hosts it (§6.7 — the campaign outlives every scene, and the scenes
/// are views of it).
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
public interface IRunHost : ISaveHost
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
