using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// 3D stand-in for a training-dummy enemy, mirroring its logic-space position.
/// Defeated dummies ease down to a flat floor remnant — they never blocked
/// movement, but a full-size corpse read as if it did — and grow back on
/// resurrection. A shove or a drag slides it to where it landed rather than
/// teleporting it there, while it is in sight; hidden in the fog, it is simply
/// where the logic put it.
/// </summary>
public class EnemyObject : PrimitiveBoxObject
{
    private const float Width = 0.75f;
    private const float Height = 0.95f;
    private const float ScaleEaseRate = 8f;

    /// <summary>How long a displacement takes to play out on screen: the logic moved the dummy in one step, the stand-in follows.</summary>
    private const float SlideSeconds = 0.18f;

    /// <summary>Squashed remnant while defeated: clearly walkable, still marks the spot.</summary>
    private static readonly Vector3 DefeatedScale = new(0.55f, 0.16f, 0.55f);

    public EnemyState State { get; }

    private Vector3 _targetScale = Vector3.One;
    private Vector3 _slideFrom;
    private float _slideLeft;

    public EnemyObject(EnemyState state, Vector4 color)
        : base(Width, Height, Width, color)
    {
        State = state;
        SyncTransform();
    }

    /// <summary>Shrink to the floor remnant on defeat; grow back on resurrection.</summary>
    public void SetDefeatedVisual(bool defeated)
        => _targetScale = defeated ? DefeatedScale : Vector3.One;

    /// <summary>
    /// Sync on displacement: the turn system shoved or dragged the dummy tile
    /// by tile in one go, so ease from where it was last drawn to where it now
    /// stands, and the blow reads as a shove. Only the drawing lags: reach,
    /// sight, occupancy and fog read the logic position, which is already final.
    /// A dummy hidden in the fog plays no slide — the engine does not update an
    /// inactive object, so the slide could never run — and lands on its tile
    /// at once.
    /// </summary>
    public void SlideToState()
    {
        _slideFrom = Position;
        _slideLeft = IsActive ? SlideSeconds : 0f;
        SyncTransform();
    }

    /// <summary>
    /// Show or hide the stand-in with the fog. Hiding ends a slide on the spot:
    /// the engine stops updating an inactive object, so a slide left running
    /// would stall at its start — every sync easing from where the shove found
    /// the dummy, however far it then walks unseen — and play out from that
    /// stale spot on the next reveal. It lands where the logic put it instead.
    /// </summary>
    public void SetVisible(bool visible)
    {
        IsActive = visible;
        if (!visible && _slideLeft > 0f)
        {
            _slideLeft = 0f;
            SyncTransform();
        }
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        bool moved = false;
        if (_slideLeft > 0f)
        {
            _slideLeft = MathF.Max(0f, _slideLeft - deltaTime);
            moved = true;
        }
        if (Scale != _targetScale)
        {
            Scale = Vector3.Lerp(Scale, _targetScale, Math.Min(1f, ScaleEaseRate * deltaTime));
            if ((Scale - _targetScale).LengthSquared < 1e-6f)
                Scale = _targetScale;
            moved = true; // keep the scaling box seated on the floor
        }
        if (moved)
            SyncTransform();
    }

    /// <summary>
    /// Mirror the logic position into the 3D transform, the box seated on the
    /// floor at its current scale — mid-slide, the eased point on the way from
    /// where the shove found it.
    /// </summary>
    public void SyncTransform()
    {
        var target = WorldSpace.FromLogic(State.X, State.Y, Height * Scale.Y / 2f);
        if (_slideLeft > 0f)
        {
            float t = 1f - _slideLeft / SlideSeconds;
            float eased = 1f - (1f - t) * (1f - t); // ease out: quick off the blow, settling on the tile
            target = Vector3.Lerp(new Vector3(_slideFrom.X, target.Y, _slideFrom.Z), target, eased);
        }
        Position = target;
    }
}
