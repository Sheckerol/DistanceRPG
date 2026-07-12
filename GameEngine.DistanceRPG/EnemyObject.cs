using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// 3D stand-in for a training-dummy enemy, mirroring its logic-space position.
/// Defeated dummies ease down to a flat floor remnant — they never blocked
/// movement, but a full-size corpse read as if it did — and grow back on
/// resurrection.
/// </summary>
public class EnemyObject : PrimitiveBoxObject
{
    private const float Width = 0.75f;
    private const float Height = 0.95f;
    private const float ScaleEaseRate = 8f;

    /// <summary>Squashed remnant while defeated: clearly walkable, still marks the spot.</summary>
    private static readonly Vector3 DefeatedScale = new(0.55f, 0.16f, 0.55f);

    public EnemyState State { get; }

    private Vector3 _targetScale = Vector3.One;

    public EnemyObject(EnemyState state, Vector4 color)
        : base(Width, Height, Width, color)
    {
        State = state;
        SyncTransform();
    }

    /// <summary>Shrink to the floor remnant on defeat; grow back on resurrection.</summary>
    public void SetDefeatedVisual(bool defeated)
        => _targetScale = defeated ? DefeatedScale : Vector3.One;

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (Scale != _targetScale)
        {
            Scale = Vector3.Lerp(Scale, _targetScale, Math.Min(1f, ScaleEaseRate * deltaTime));
            if ((Scale - _targetScale).LengthSquared < 1e-6f)
                Scale = _targetScale;
            SyncTransform(); // keep the scaling box seated on the floor
        }
    }

    public void SyncTransform()
    {
        Position = WorldSpace.FromLogic(State.X, State.Y, Height * Scale.Y / 2f);
    }
}
