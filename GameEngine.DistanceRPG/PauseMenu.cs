namespace GameEngine.DistanceRPG;

/// <summary>What a number key on the menu does (<see cref="PauseMenu.Press"/>).</summary>
internal enum PauseAction
{
    /// <summary>Nothing: a key the menu does not answer.</summary>
    None,

    /// <summary>1: the menu closes.</summary>
    Close,

    /// <summary>2 at a safe point: the save list opens in the options' place.</summary>
    OpenSaves,

    /// <summary>2 where nothing can be saved by hand: the menu says why (<see cref="PauseMenu.Press"/>'s <c>Said</c>), saving nothing.</summary>
    Refuse,

    /// <summary>3: the campaign's slot written, and play left for the title.</summary>
    QuitToTitle,

    /// <summary>3 in a dev-override session, which has no title: the window closes, saving the scratch slot as ever.</summary>
    QuitGame,
}

/// <summary>
/// The one menu ESC opens while a campaign is in play — the dungeon's pause menu,
/// and the same menu over the hub between runs (the user's ruling of 2026-09-25 on
/// the save model, R2(d), R2(e)): <c>1  CLOSE</c>, <c>2  SAVE GAME</c>,
/// <c>3  QUIT TO TITLE</c>. Pure — a digit in, an action out — so every key is
/// tested (<c>SaveMenuTests</c>), and the screens only draw and act on it.
/// <list type="bullet">
/// <item><strong>SAVE GAME only at a safe point</strong>, the autosave's
/// (<see cref="Logic.SaveGame.IsSafePoint"/>): in the dungeon the party's own turn
/// with nothing in flight, never while the enemies move; between runs always. Off
/// one, 2 answers <see cref="SaveOnYourTurn"/> and saves nothing; on one it opens
/// the save list (<see cref="SlotMenu"/>, save mode).</item>
/// <item><strong>QUIT TO TITLE</strong> writes the campaign's slot through the window
/// close's own door and shows the title (<see cref="ISaveHost.QuitToTitle"/>). There
/// is no QUIT GAME in a session with a title: the title's QUIT, or the window's
/// close button, ends the game.</item>
/// <item><strong>A dev-override session has no title and saves apart</strong>: 3
/// reads <see cref="QuitGameLine"/> and closes the window, saving its scratch slot
/// as ever, and 2 says <see cref="NoSaveInOverride"/>.</item>
/// </list>
/// Every word is in the HUD's font: capitals, digits, spaces and dashes.
/// </summary>
internal static class PauseMenu
{
    /// <summary>The first option: the menu closes.</summary>
    internal const string CloseLine = "1  CLOSE";

    /// <summary>The second option: the save list, at a safe point.</summary>
    internal const string SaveLine = "2  SAVE GAME";

    /// <summary>The third option, in a session with a title: the campaign's slot written, then the title.</summary>
    internal const string QuitToTitleLine = "3  QUIT TO TITLE";

    /// <summary>The third option in a dev-override session, which has no title: the window closes.</summary>
    internal const string QuitGameLine = "3  QUIT GAME";

    /// <summary>What 2 says off a safe point — the enemies taking their turn, its banner up — saving nothing.</summary>
    internal const string SaveOnYourTurn = "SAVE ON YOUR OWN TURN";

    /// <summary>What 2 says in a dev-override session: its saves go to its scratch slot alone, never a manual slot.</summary>
    internal const string NoSaveInOverride = "A DEV OVERRIDE LAUNCH SAVES ONLY ITS SCRATCH SLOT";

    /// <summary>The options, top to bottom, each led by the number key that takes it: QUIT TO TITLE last, or QUIT GAME in a session with no title.</summary>
    /// <param name="hasTitle">Whether the session has a title to quit to (<see cref="ISaveHost.HasTitle"/>).</param>
    internal static IReadOnlyList<string> Options(bool hasTitle) => [CloseLine, SaveLine, hasTitle ? QuitToTitleLine : QuitGameLine];

    /// <summary>
    /// What <paramref name="digit"/> does on the menu: 1 closes it; 2 opens the save
    /// list at a safe point, and otherwise says why not; 3 quits to the title, or,
    /// with no title, the game. Every other digit does nothing.
    /// </summary>
    /// <param name="digit">The number key pressed.</param>
    /// <param name="canSave">Whether the campaign stands at a safe point now (<c>DungeonScene.CanSaveNow</c>; always between runs).</param>
    /// <param name="hasTitle">Whether the session has a title and slots (<see cref="ISaveHost.HasTitle"/>).</param>
    /// <returns>The action, and what the menu says under itself for a refusal — null for any other action.</returns>
    internal static (PauseAction Action, string? Said) Press(int digit, bool canSave, bool hasTitle)
        => digit switch
        {
            1 => (PauseAction.Close, null),
            2 when !hasTitle => (PauseAction.Refuse, NoSaveInOverride),
            2 when !canSave => (PauseAction.Refuse, SaveOnYourTurn),
            2 => (PauseAction.OpenSaves, null),
            3 => (hasTitle ? PauseAction.QuitToTitle : PauseAction.QuitGame, null),
            _ => (PauseAction.None, null),
        };
}
