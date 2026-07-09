using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// 3D stand-in for a party member: a colour-coded box whose world transform
/// mirrors the member's logic-space position.
/// </summary>
public class CharacterObject : PrimitiveBoxObject
{
    private const float Width = 0.55f;
    private const float Height = 1.1f;

    public PartyMemberState State { get; }

    public CharacterObject(PartyMemberState state, Vector4 color)
        : base(Width, Height, Width, color)
    {
        State = state;
        SyncTransform();
    }

    /// <summary>Mirror the logic-space position into the 3D transform.</summary>
    public void SyncTransform()
    {
        Position = WorldSpace.FromLogic(State.X, State.Y, Height / 2f);
    }
}
