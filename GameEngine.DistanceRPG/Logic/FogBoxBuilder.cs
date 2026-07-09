namespace GameEngine.DistanceRPG.Logic;

/// <summary>The fog reveal boxes derived from a generated map.</summary>
public sealed class FogBoxSet
{
    /// <summary>Final pruned reveal boxes: rooms + expanded corridors + unions.</summary>
    public required List<MapRect> FogBoxes { get; init; }

    /// <summary>Corridors after expansion (clones; <see cref="MapData.Corridors"/> is mutated in place).</summary>
    public required List<Corridor> ExpandedCorridors { get; init; }

    /// <summary>Just the union boxes, used by the prototype as extra pseudo-rooms.</summary>
    public required List<MapRect> UnionBoxes { get; init; }
}

/// <summary>
/// Assembles the fog reveal boxes from a generated map, mirroring the Phaser
/// scene's setup order: expand corridors in place, compute union boxes from
/// the pristine room rects and expanded corridors, then prune boxes fully
/// contained in others.
/// </summary>
public static class FogBoxBuilder
{
    public static FogBoxSet Build(MapData map)
    {
        MapGenerator.ExpandCorridors(map.Corridors, map.Grid);
        var expanded = map.Corridors.Select(c => c.Clone()).ToList();

        var unions = FogGeometry.ComputeUnionFogBoxes(map.DebugRooms, expanded);

        var fogBoxes = new List<MapRect>();
        fogBoxes.AddRange(map.Rooms.Select(r => new MapRect(r.X, r.Y, r.W, r.H)));
        fogBoxes.AddRange(map.Corridors);
        fogBoxes.AddRange(unions);

        return new FogBoxSet
        {
            FogBoxes = FogGeometry.PruneFullyOverlappedBoxes(fogBoxes),
            ExpandedCorridors = expanded,
            UnionBoxes = unions,
        };
    }
}
