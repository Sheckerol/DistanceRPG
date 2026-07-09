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
    public DistanceRpgGame()
    {
        _inputEventManager.SubscribeToKeyPressed(_ => Close(), Keys.Escape);
    }

    protected override void OnLoad()
    {
        base.OnLoad(); // _camera is created here

        // Black background so the void beyond the map reads as unexplored fog.
        GL.ClearColor(0f, 0f, 0f, 1f);

        SceneManager.Instance.RegisterScene("DungeonScene",
            new DungeonScene(_camera!, this));
        SceneManager.Instance.TransitionToScene("DungeonScene");
    }
}
