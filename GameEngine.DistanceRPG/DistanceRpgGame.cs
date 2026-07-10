using GameEngine.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Turn-based party RPG ported from the original Phaser prototype.
/// Registers DungeonScene in OnLoad (where _camera is guaranteed non-null).
/// </summary>
public class DistanceRpgGame : Game
{
    private DungeonScene? _dungeon;

    public DistanceRpgGame()
    {
        // Esc quits only if the scene doesn't consume it (e.g. to close the inventory).
        _inputEventManager.SubscribeToKeyPressed(_ =>
        {
            if (_dungeon?.HandleEscape() != true) Close();
        }, Keys.Escape);
    }

    protected override void OnLoad()
    {
        base.OnLoad(); // _camera is created here

        // Black background so the void beyond the map reads as unexplored fog.
        GL.ClearColor(0f, 0f, 0f, 1f);

        _dungeon = new DungeonScene(_camera!, this);
        SceneManager.Instance.RegisterScene("DungeonScene", _dungeon);
        SceneManager.Instance.TransitionToScene("DungeonScene");
    }
}
