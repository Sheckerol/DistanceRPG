using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// 3D stand-in for an enemy, mirroring its logic-space position: a dummy's box,
/// or a larger one for a boss. Defeated enemies ease down to a flat floor
/// remnant — they never blocked movement, but a full-size corpse read as if it
/// did — and grow back on resurrection; one already dead when its floor is built
/// starts as the remnant. A shove or a drag slides it to where it landed rather
/// than teleporting it there, while it is in sight; hidden in the fog, it is
/// simply where the logic put it.
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

    /// <summary>
    /// How much larger than a dummy's box this stand-in is drawn: 1 for rank and
    /// file, more for a boss, which should read as the one thing on the floor that
    /// is not a dummy. Only the drawing grows — its plate and its click target
    /// with it — never its reach or its footprint, which are the logic's.
    /// </summary>
    public float Size { get; }

    private readonly float _height;
    private Vector3 _targetScale = Vector3.One;
    private Vector3 _slideFrom;
    private float _slideLeft;

    // The farm's gains (§3.2) this stand-in last announced: what a revival's
    // beat is measured from (TakeRevivalGains).
    private int _shownDamage;
    private int _shownMaxHp;

    /// <param name="state">The enemy row it stands in for.</param>
    /// <param name="color">Its colour while it stands.</param>
    /// <param name="size">How much larger than a dummy it is drawn: 1 for rank and file.</param>
    public EnemyObject(EnemyState state, Vector4 color, float size = 1f)
        : base(Width * size, Height * size, Width * size, color)
    {
        State = state;
        Size = size;
        _height = Height * size;
        (_shownDamage, _shownMaxHp) = (state.RevivalDamage, state.RevivalMaxHp);
        SyncTransform();
    }

    /// <summary>
    /// What the enemy's revivals have added since this stand-in last said so
    /// (§3.2) — the damage and the maximum HP its row has gained — which a
    /// revival's beat announces, and which are then marked as said. Taken from the
    /// row when the stand-in is built, so a floor the party returns to announces
    /// only what is gained while the party is there to see it.
    /// </summary>
    public (int Damage, int MaxHp) TakeRevivalGains()
    {
        var gained = (State.RevivalDamage - _shownDamage, State.RevivalMaxHp - _shownMaxHp);
        (_shownDamage, _shownMaxHp) = (State.RevivalDamage, State.RevivalMaxHp);
        return gained;
    }

    /// <summary>
    /// Shrink to the floor remnant on defeat; grow back on resurrection. Eased,
    /// so a kill is seen to land — unless <paramref name="snap"/>, which sets
    /// the scale outright: a corpse on a floor the party returns to was lying
    /// there before anyone arrived, and is built lying down rather than seen to
    /// fall a second time.
    /// </summary>
    public void SetDefeatedVisual(bool defeated, bool snap = false)
    {
        _targetScale = defeated ? DefeatedScale : Vector3.One;
        if (!snap) return;
        Scale = _targetScale;
        SyncTransform();   // seated on the floor at its new height
    }

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
        var target = WorldSpace.FromLogic(State.X, State.Y, _height * Scale.Y / 2f);
        if (_slideLeft > 0f)
        {
            float t = 1f - _slideLeft / SlideSeconds;
            float eased = 1f - (1f - t) * (1f - t); // ease out: quick off the blow, settling on the tile
            target = Vector3.Lerp(new Vector3(_slideFrom.X, target.Y, _slideFrom.Z), target, eased);
        }
        Position = target;
    }
}
