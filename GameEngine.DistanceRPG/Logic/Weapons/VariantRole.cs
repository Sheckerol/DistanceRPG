namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The four fixed roles a martial class's variants fill (§1.2), in the doc's
/// order: Efficiency adds <c>Light x1</c>, Purity another stack of the class's
/// signature, Control something that degrades the enemy, Support something
/// that helps the rest of the party. Casters vary by effect and shape instead
/// and carry no role.
/// </summary>
public enum VariantRole
{
    Efficiency,
    Purity,
    Control,
    Support,
}
