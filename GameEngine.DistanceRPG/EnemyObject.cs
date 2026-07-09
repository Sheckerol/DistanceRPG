using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// 3D stand-in for the training-dummy enemy, mirroring its logic-space position.
/// </summary>
public class EnemyObject : PrimitiveBoxObject
{
    private const float Width = 0.75f;
    private const float Height = 0.95f;

    public EnemyState State { get; }

    public EnemyObject(EnemyState state, Vector4 color)
        : base(Width, Height, Width, color)
    {
        State = state;
        SyncTransform();
    }

    public void SyncTransform()
    {
        Position = WorldSpace.FromLogic(State.X, State.Y, Height / 2f);
    }
}
