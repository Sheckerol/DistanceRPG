namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The geometry of the four wand shapes (§1.4), in logic units (32 per tile),
/// and the one question the turn system asks of it: which actors a cast of a
/// shape from where the caster stands, aimed at a point, catches. A shape hits
/// every actor inside it except the caster, and every candidate must be in
/// the caster's line of sight — "all shapes stop at walls": the segment-vs-wall
/// test extended to a per-target check inside the shape. Who the candidates
/// are — the far side always, the caster's own side only while friendly fire
/// is on — is the caller's business, which knows sides; the geometry knows
/// positions. An actor is inside a shape when its centre is: a body is where
/// it stands, so a Beam one tile wide catches the row it runs along and not
/// the rows beside it.
/// </summary>
public static class AreaShapes
{
    /// <summary>Half-angle tolerance on the cone's edge, so a body exactly on it is inside rather than lost to rounding.</summary>
    private const float CosineTolerance = 1e-5f;

    /// <summary>
    /// A shape set down on the map once the aim is resolved: where it sits
    /// (<see cref="OriginX"/>, <see cref="OriginY"/> — a Blast's point, or the
    /// caster for the rest) and, for a Cone or Beam, the unit direction it
    /// points along. <see cref="Contains"/> answers whether a point is inside it.
    /// </summary>
    public readonly record struct Placement(AreaShape Shape, float OriginX, float OriginY, float DirX, float DirY)
    {
        /// <summary>
        /// Whether the point is inside the placed shape: within a Blast's or a
        /// Nova's radius of its origin; within a Cone's length and half its
        /// angle either side of its direction (the apex counts); within a Beam's
        /// length along its direction and half its width across it.
        /// </summary>
        public bool Contains(float x, float y)
        {
            float dx = x - OriginX;
            float dy = y - OriginY;
            float d2 = dx * dx + dy * dy;
            switch (Shape.Kind)
            {
                case AreaShapeKind.Blast:
                case AreaShapeKind.Nova:
                    return d2 <= (float)Shape.Radius * Shape.Radius;

                case AreaShapeKind.Cone:
                {
                    if (d2 > (float)Shape.Length * Shape.Length) return false;
                    if (d2 == 0f) return true;   // the apex
                    float cosine = (dx * DirX + dy * DirY) / MathF.Sqrt(d2);
                    float cosineHalf = MathF.Cos(Shape.AngleDegrees * 0.5f * MathF.PI / 180f);
                    return cosine >= cosineHalf - CosineTolerance;
                }

                case AreaShapeKind.Beam:
                {
                    float along = dx * DirX + dy * DirY;
                    float across = MathF.Abs(dx * DirY - dy * DirX);
                    return along >= 0f && along <= Shape.Length && across <= Shape.Width * 0.5f;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(Shape), Shape.Kind, "Not an area shape kind.");
            }
        }
    }

    /// <summary>
    /// Resolve <paramref name="aim"/> into where <paramref name="shape"/> sits
    /// for <paramref name="caster"/>: a Blast centres on the aimed point,
    /// pulled back along the line from the caster to within its
    /// <see cref="AreaShape.TargetReach"/> when aimed further; a Cone or Beam
    /// points from the caster toward the aim, and has no placement at all
    /// when the aim is the caster's own position (null: a direction cannot be
    /// read off a point); a Nova sits on the caster and ignores the aim.
    /// </summary>
    public static Placement? Place(AreaShape shape, ActorState caster, (float X, float Y) aim)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(caster);

        float dx = aim.X - caster.X;
        float dy = aim.Y - caster.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        switch (shape.Kind)
        {
            case AreaShapeKind.Blast:
            {
                if (distance <= shape.TargetReach || distance == 0f)
                    return new Placement(shape, aim.X, aim.Y, 0f, 0f);
                float scale = shape.TargetReach / distance;
                return new Placement(shape, caster.X + dx * scale, caster.Y + dy * scale, 0f, 0f);
            }

            case AreaShapeKind.Cone:
            case AreaShapeKind.Beam:
                if (distance == 0f) return null;
                return new Placement(shape, caster.X, caster.Y, dx / distance, dy / distance);

            case AreaShapeKind.Nova:
                return new Placement(shape, caster.X, caster.Y, 0f, 0f);

            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape.Kind, "Not an area shape kind.");
        }
    }

    /// <summary>
    /// The living actors among <paramref name="actors"/>, the caster excluded,
    /// whose centres lie inside <paramref name="shape"/> as placed by
    /// <paramref name="aim"/> and whom the caster can see through
    /// <paramref name="grid"/>'s walls — nearest the caster first, ties in the
    /// given order (a stable sort), so the order the hits land in is data and
    /// never a roll. Empty for a Cone or Beam aimed at the caster itself.
    /// </summary>
    public static IReadOnlyList<ActorState> Targets(AreaShape shape, ActorState caster, (float X, float Y) aim, IEnumerable<ActorState> actors, int[,] grid)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(grid);

        if (Place(shape, caster, aim) is not { } placed)
            return Array.Empty<ActorState>();

        return actors
            .Where(a => a != caster && a.Alive && placed.Contains(a.X, a.Y)
                        && LineOfSight.HasLineOfSight(grid, caster.X, caster.Y, a.X, a.Y))
            .OrderBy(a => (a.X - caster.X) * (a.X - caster.X) + (a.Y - caster.Y) * (a.Y - caster.Y))
            .ToList();
    }
}
