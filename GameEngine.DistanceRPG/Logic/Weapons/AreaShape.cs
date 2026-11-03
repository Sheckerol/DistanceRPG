namespace GameEngine.DistanceRPG.Logic;

/// <summary>The four wand geometries (§1.4): only the element varies between two wands of one shape.</summary>
public enum AreaShapeKind
{
    Blast,
    Cone,
    Beam,
    Nova,
}

/// <summary>
/// A wand's geometry, in logic units (32 per tile): a Blast is a circle of
/// <see cref="Radius"/> centred on a point within <see cref="TargetReach"/> of
/// the caster; a Cone spans <see cref="AngleDegrees"/> for <see cref="Length"/>
/// from the caster; a Beam is <see cref="Width"/> wide and <see cref="Length"/>
/// long from the caster; a Nova is a circle of <see cref="Radius"/> centred on
/// the caster. Data only here; the geometry functions arrive with the wands.
/// A dimension a kind does not use is 0.
/// </summary>
public sealed record AreaShape(AreaShapeKind Kind, int Radius, int Length, int Width, int AngleDegrees, int TargetReach)
{
    public static AreaShape Blast(int radius, int targetReach) => new(AreaShapeKind.Blast, radius, 0, 0, 0, targetReach);
    public static AreaShape Cone(int angleDegrees, int length) => new(AreaShapeKind.Cone, 0, length, 0, angleDegrees, 0);
    public static AreaShape Beam(int width, int length) => new(AreaShapeKind.Beam, 0, length, width, 0, 0);
    public static AreaShape Nova(int radius) => new(AreaShapeKind.Nova, radius, 0, 0, 0, 0);
}
