using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Conversions between logic space (the prototype's pixels: 32 per tile, +y is
/// "down" the map) and 3D world space (1 world unit per tile on the XZ plane:
/// world X = column axis, world Z = row axis, +Y is up).
/// </summary>
public static class WorldSpace
{
    /// <summary>World units per map tile.</summary>
    public const float UnitsPerTile = 1f;

    /// <summary>Multiply a logic-space length (pixels) to get world units.</summary>
    public const float LogicToWorld = UnitsPerTile / Logic.GameConstants.Tile;

    /// <summary>World position of a tile's centre at a given height.</summary>
    public static Vector3 TileCenter(int row, int col, float y = 0f)
        => new((col + 0.5f) * UnitsPerTile, y, (row + 0.5f) * UnitsPerTile);

    /// <summary>Convert a logic-space point (x right, y down) to world space.</summary>
    public static Vector3 FromLogic(float logicX, float logicY, float y = 0f)
        => new(logicX * LogicToWorld, y, logicY * LogicToWorld);

    /// <summary>Convert a world position back to logic space (x right, y down).</summary>
    public static (float X, float Y) ToLogic(Vector3 world)
        => (world.X / LogicToWorld, world.Z / LogicToWorld);
}
