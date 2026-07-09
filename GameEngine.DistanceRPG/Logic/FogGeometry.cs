namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Computes the fog-of-war reveal boxes for a generated map, ported from the
/// Phaser prototype. Standing inside any box reveals that whole box (plus a
/// one-tile border), so overlapping/adjacent rooms and corridors are merged
/// into extra "union" boxes: without them, standing in the shared area of two
/// overlapping spaces would reveal only one of them.
/// </summary>
public sealed class FogGeometry
{
    private readonly IReadOnlyList<MapRect> _roomBoxes;
    private readonly IReadOnlyList<Corridor> _corridors;

    private FogGeometry(IReadOnlyList<MapRect> roomBoxes, IReadOnlyList<Corridor> corridors)
    {
        _roomBoxes = roomBoxes;
        _corridors = corridors;
    }

    /// <summary>
    /// Compute the union boxes for room/corridor overlaps.
    /// Four cases, then an expansion pass that treats each union box as a room
    /// and re-checks it against every corridor until no new boxes appear.
    /// </summary>
    /// <param name="debugRooms">Pristine room rects (before union rooms are appended).</param>
    /// <param name="expandedCorridors">Corridors after <see cref="MapGenerator.ExpandCorridors"/>.</param>
    public static List<MapRect> ComputeUnionFogBoxes(
        IReadOnlyList<MapRect> debugRooms, IReadOnlyList<Corridor> expandedCorridors)
    {
        var self = new FogGeometry(debugRooms, expandedCorridors);

        var boxes = new List<MapRect>();
        var keys = new HashSet<string>();

        void Add(MapRect? rect)
        {
            if (rect == null || rect.W <= 0 || rect.H <= 0) return;
            if (!keys.Add($"{rect.X},{rect.Y},{rect.W},{rect.H}")) return;
            boxes.Add(rect);
        }

        // Case 1: room-corridor overlap
        foreach (var room in debugRooms)
            foreach (var corridor in expandedCorridors)
                if (RectIntersection(room, corridor) != null)
                    foreach (var r in self.BuildUnionRectsFromOverlap(room, corridor))
                        Add(r);

        // Case 2: parallel corridor pairs with bounding-box overlap
        foreach (var r in self.ComputeCorridorUnionFogBoxes())
            Add(r);

        // Case 3: corridor adjacent to room with no wall between them
        foreach (var r in self.ComputeAdjacentRoomCorridorBoxes())
            Add(r);

        // Case 4: stepped parallel corridors (touching boundary, no bounding-box overlap)
        foreach (var r in self.ComputeSteppedCorridorUnionBoxes())
            Add(r);

        // Expansion pass: treat each union box as a room and check it against every
        // corridor it overlaps or touches. New union boxes may themselves overlap
        // further corridors, so repeat until no new boxes are added.
        int prevCount;
        do
        {
            prevCount = boxes.Count;
            foreach (var ubox in boxes.ToList())
            {
                foreach (var corr in expandedCorridors)
                {
                    if (RectIntersection(ubox, corr) != null)
                    {
                        foreach (var r in self.BuildUnionRectsFromOverlap(ubox, corr))
                            Add(r);
                    }
                    else
                    {
                        Add(BuildAdjacentRect(ubox, corr));
                    }
                }
            }
        } while (boxes.Count > prevCount);

        return boxes;
    }

    /// <summary>
    /// Drop duplicate boxes and any box fully contained in another —
    /// the outer box already reveals everything the inner one would.
    /// </summary>
    public static List<MapRect> PruneFullyOverlappedBoxes(IEnumerable<MapRect> boxes)
    {
        var normalized = new List<MapRect>();
        var seen = new HashSet<string>();

        foreach (var box in boxes)
        {
            if (box == null || box.W <= 0 || box.H <= 0) continue;
            if (!seen.Add($"{box.X},{box.Y},{box.W},{box.H}")) continue;
            normalized.Add(box);
        }

        var keep = new List<MapRect>();
        for (int i = 0; i < normalized.Count; i++)
        {
            var a = normalized[i];
            bool covered = false;
            for (int j = 0; j < normalized.Count; j++)
            {
                if (i == j) continue;
                if (RectContainsRect(a, normalized[j]))
                {
                    covered = true;
                    break;
                }
            }
            if (!covered) keep.Add(a);
        }

        return keep;
    }

    public static bool RectContainsRect(MapRect inner, MapRect outer)
        => inner.X >= outer.X &&
           inner.Y >= outer.Y &&
           inner.X + inner.W <= outer.X + outer.W &&
           inner.Y + inner.H <= outer.Y + outer.H;

    public static MapRect? RectIntersection(MapRect a, MapRect b)
    {
        int x0 = Math.Max(a.X, b.X);
        int y0 = Math.Max(a.Y, b.Y);
        int x1 = Math.Min(a.X + a.W, b.X + b.W);
        int y1 = Math.Min(a.Y + a.H, b.Y + b.H);
        if (x1 <= x0 || y1 <= y0) return null;
        return new MapRect(x0, y0, x1 - x0, y1 - y0);
    }

    private IEnumerable<MapRect> BuildUnionRectsFromOverlap(MapRect room, Corridor corridor)
    {
        var overlap = RectIntersection(room, corridor);
        var parallel = BuildParallelUnionRect(room, corridor);
        var perpendicular = BuildPerpendicularUnionRect(room, corridor, overlap);
        return perpendicular != null ? new[] { parallel, perpendicular } : new[] { parallel };
    }

    private MapRect BuildParallelUnionRect(MapRect room, Corridor corridor)
    {
        if (corridor.Horizontal)
        {
            int y0 = Math.Max(room.Y, corridor.Y);
            int y1 = Math.Min(room.Y + room.H, corridor.Y + corridor.H);
            int bandY0 = y1 > y0 ? y0 : corridor.Y;
            int bandY1 = y1 > y0 ? y1 : corridor.Y + corridor.H;
            int x0 = Math.Min(room.X, corridor.X);
            int x1 = Math.Max(room.X + room.W, corridor.X + corridor.W);

            foreach (var otherRoom in _roomBoxes)
            {
                bool overlapsCorridorX = otherRoom.X < corridor.X + corridor.W && otherRoom.X + otherRoom.W > corridor.X;
                // Must *fully cover* the band height — overlapping is not enough, it would include
                // tiles where the other room doesn't reach and those tiles may be walls.
                bool coversBandY = otherRoom.Y <= bandY0 && otherRoom.Y + otherRoom.H >= bandY1;
                if (!overlapsCorridorX || !coversBandY) continue;
                x0 = Math.Min(x0, otherRoom.X);
                x1 = Math.Max(x1, otherRoom.X + otherRoom.W);
            }
            return new MapRect(x0, bandY0, x1 - x0, bandY1 - bandY0);
        }
        else
        {
            int x0 = Math.Max(room.X, corridor.X);
            int x1 = Math.Min(room.X + room.W, corridor.X + corridor.W);
            int bandX0 = x1 > x0 ? x0 : corridor.X;
            int bandX1 = x1 > x0 ? x1 : corridor.X + corridor.W;
            int y0 = Math.Min(room.Y, corridor.Y);
            int y1 = Math.Max(room.Y + room.H, corridor.Y + corridor.H);

            foreach (var otherRoom in _roomBoxes)
            {
                // Must fully cover the band width
                bool coversBandX = otherRoom.X <= bandX0 && otherRoom.X + otherRoom.W >= bandX1;
                bool overlapsCorridorY = otherRoom.Y < corridor.Y + corridor.H && otherRoom.Y + otherRoom.H > corridor.Y;
                if (!coversBandX || !overlapsCorridorY) continue;
                y0 = Math.Min(y0, otherRoom.Y);
                y1 = Math.Max(y1, otherRoom.Y + otherRoom.H);
            }
            return new MapRect(bandX0, y0, bandX1 - bandX0, y1 - y0);
        }
    }

    private MapRect? BuildPerpendicularUnionRect(MapRect room, Corridor corridor, MapRect? overlap)
    {
        if (overlap == null) return null;

        if (corridor.Horizontal)
        {
            int bandX0 = overlap.X;
            int bandX1 = overlap.X + overlap.W;
            int y0 = Math.Min(room.Y, corridor.Y);
            int y1 = Math.Max(room.Y + room.H, corridor.Y + corridor.H);

            foreach (var otherRoom in _roomBoxes)
            {
                // Must fully cover the band width so no wall tiles are included
                bool coversBandX = otherRoom.X <= bandX0 && otherRoom.X + otherRoom.W >= bandX1;
                bool overlapsCorridorY = otherRoom.Y < corridor.Y + corridor.H && otherRoom.Y + otherRoom.H > corridor.Y;
                if (!coversBandX || !overlapsCorridorY) continue;
                y0 = Math.Min(y0, otherRoom.Y);
                y1 = Math.Max(y1, otherRoom.Y + otherRoom.H);
            }
            return new MapRect(bandX0, y0, bandX1 - bandX0, y1 - y0);
        }
        else
        {
            int bandY0 = overlap.Y;
            int bandY1 = overlap.Y + overlap.H;
            int x0 = Math.Min(room.X, corridor.X);
            int x1 = Math.Max(room.X + room.W, corridor.X + corridor.W);

            foreach (var otherRoom in _roomBoxes)
            {
                // Must fully cover the band height
                bool coversBandY = otherRoom.Y <= bandY0 && otherRoom.Y + otherRoom.H >= bandY1;
                bool overlapsCorridorX = otherRoom.X < corridor.X + corridor.W && otherRoom.X + otherRoom.W > corridor.X;
                if (!coversBandY || !overlapsCorridorX) continue;
                x0 = Math.Min(x0, otherRoom.X);
                x1 = Math.Max(x1, otherRoom.X + otherRoom.W);
            }
            return new MapRect(x0, bandY0, x1 - x0, bandY1 - bandY0);
        }
    }

    private List<MapRect> ComputeCorridorUnionFogBoxes()
    {
        var boxes = new List<MapRect>();
        var keys = new HashSet<string>();

        for (int i = 0; i < _corridors.Count; i++)
        {
            for (int j = i + 1; j < _corridors.Count; j++)
            {
                var a = _corridors[i];
                var b = _corridors[j];
                if (a.Horizontal != b.Horizontal) continue; // only parallel corridors
                var overlap = RectIntersection(a, b);
                if (overlap == null) continue;

                foreach (var rect in BuildCorridorUnionRectsFromOverlap(a, b, overlap))
                {
                    if (rect == null || rect.W <= 0 || rect.H <= 0) continue;
                    if (!keys.Add($"{rect.X},{rect.Y},{rect.W},{rect.H}")) continue;
                    boxes.Add(rect);
                }
            }
        }

        return boxes;
    }

    private static IEnumerable<MapRect?> BuildCorridorUnionRectsFromOverlap(Corridor a, Corridor b, MapRect overlap)
    {
        var parallel = BuildParallelCorridorUnionRect(a, b, overlap);
        var perpendicular = BuildPerpendicularCorridorUnionRect(a, b, overlap);
        return perpendicular != null ? new[] { parallel, perpendicular } : new[] { parallel };
    }

    private static MapRect? BuildParallelCorridorUnionRect(Corridor a, Corridor b, MapRect overlap)
    {
        if (a.Horizontal)
        {
            int y0 = overlap.Y;
            int y1 = overlap.Y + overlap.H;
            int x0 = Math.Min(a.X, b.X);
            int x1 = Math.Max(a.X + a.W, b.X + b.W);
            return new MapRect(x0, y0, x1 - x0, y1 - y0);
        }
        else
        {
            int x0 = overlap.X;
            int x1 = overlap.X + overlap.W;
            int y0 = Math.Min(a.Y, b.Y);
            int y1 = Math.Max(a.Y + a.H, b.Y + b.H);
            return new MapRect(x0, y0, x1 - x0, y1 - y0);
        }
    }

    private static MapRect? BuildPerpendicularCorridorUnionRect(Corridor a, Corridor b, MapRect overlap)
    {
        if (a.Horizontal)
        {
            int x0 = overlap.X;
            int x1 = overlap.X + overlap.W;
            int y0 = Math.Min(a.Y, b.Y);
            int y1 = Math.Max(a.Y + a.H, b.Y + b.H);
            return new MapRect(x0, y0, x1 - x0, y1 - y0);
        }
        else
        {
            int y0 = overlap.Y;
            int y1 = overlap.Y + overlap.H;
            int x0 = Math.Min(a.X, b.X);
            int x1 = Math.Max(a.X + a.W, b.X + b.W);
            return new MapRect(x0, y0, x1 - x0, y1 - y0);
        }
    }

    private static MapRect? CorridorOverlapOrTouchBand(Corridor a, Corridor b)
    {
        var overlap = RectIntersection(a, b);
        if (overlap != null) return overlap;
        if (a.Horizontal != b.Horizontal) return null;

        if (a.Horizontal)
        {
            int x0 = Math.Max(a.X, b.X);
            int x1 = Math.Min(a.X + a.W, b.X + b.W);
            if (x1 <= x0) return null;
            if (a.Y + a.H == b.Y) return new MapRect(x0, b.Y, x1 - x0, 1);
            if (b.Y + b.H == a.Y) return new MapRect(x0, a.Y, x1 - x0, 1);
            return null;
        }
        else
        {
            int y0 = Math.Max(a.Y, b.Y);
            int y1 = Math.Min(a.Y + a.H, b.Y + b.H);
            if (y1 <= y0) return null;
            if (a.X + a.W == b.X) return new MapRect(b.X, y0, 1, y1 - y0);
            if (b.X + b.W == a.X) return new MapRect(a.X, y0, 1, y1 - y0);
            return null;
        }
    }

    // Case 3: corridor directly adjacent to a room with no wall tile between them.
    // RectIntersection returns null (no overlap), so Case 1 misses this.
    private List<MapRect> ComputeAdjacentRoomCorridorBoxes()
    {
        var boxes = new List<MapRect>();

        foreach (var room in _roomBoxes)
        {
            foreach (var corridor in _corridors)
            {
                if (RectIntersection(room, corridor) != null) continue; // handled by Case 1

                if (corridor.Horizontal)
                {
                    int xOverlap0 = Math.Max(room.X, corridor.X);
                    int xOverlap1 = Math.Min(room.X + room.W, corridor.X + corridor.W);
                    if (xOverlap1 <= xOverlap0) continue;

                    bool adjacentBelow = corridor.Y == room.Y + room.H;
                    bool adjacentAbove = room.Y == corridor.Y + corridor.H;
                    if (!adjacentBelow && !adjacentAbove) continue;

                    boxes.Add(new MapRect(
                        xOverlap0,
                        Math.Min(room.Y, corridor.Y),
                        xOverlap1 - xOverlap0,
                        room.H + corridor.H));
                }
                else
                {
                    int yOverlap0 = Math.Max(room.Y, corridor.Y);
                    int yOverlap1 = Math.Min(room.Y + room.H, corridor.Y + corridor.H);
                    if (yOverlap1 <= yOverlap0) continue;

                    bool adjacentRight = corridor.X == room.X + room.W;
                    bool adjacentLeft = room.X == corridor.X + corridor.W;
                    if (!adjacentRight && !adjacentLeft) continue;

                    boxes.Add(new MapRect(
                        Math.Min(room.X, corridor.X),
                        yOverlap0,
                        room.W + corridor.W,
                        yOverlap1 - yOverlap0));
                }
            }
        }

        return boxes;
    }

    // Returns a combined rect if box and corr are directly adjacent (no wall between),
    // or null if they don't share an edge with overlapping cross-axis ranges.
    private static MapRect? BuildAdjacentRect(MapRect box, Corridor corr)
    {
        if (corr.Horizontal)
        {
            int xOverlap0 = Math.Max(box.X, corr.X);
            int xOverlap1 = Math.Min(box.X + box.W, corr.X + corr.W);
            if (xOverlap1 <= xOverlap0) return null;
            bool below = corr.Y == box.Y + box.H;
            bool above = box.Y == corr.Y + corr.H;
            if (!below && !above) return null;
            return new MapRect(xOverlap0, Math.Min(box.Y, corr.Y), xOverlap1 - xOverlap0, box.H + corr.H);
        }
        else
        {
            int yOverlap0 = Math.Max(box.Y, corr.Y);
            int yOverlap1 = Math.Min(box.Y + box.H, corr.Y + corr.H);
            if (yOverlap1 <= yOverlap0) return null;
            bool right = corr.X == box.X + box.W;
            bool left = box.X == corr.X + corr.W;
            if (!right && !left) return null;
            return new MapRect(Math.Min(box.X, corr.X), yOverlap0, box.W + corr.W, yOverlap1 - yOverlap0);
        }
    }

    // Case 4: two same-direction corridors that are stepped — they touch at a
    // boundary edge with overlapping cross-axis ranges but no bounding-box
    // intersection. CorridorOverlapOrTouchBand detects the touch; we turn it
    // into a union zone spanning the full extent of both corridors in the band.
    private List<MapRect> ComputeSteppedCorridorUnionBoxes()
    {
        var boxes = new List<MapRect>();

        for (int i = 0; i < _corridors.Count; i++)
        {
            for (int j = i + 1; j < _corridors.Count; j++)
            {
                var a = _corridors[i];
                var b = _corridors[j];
                if (a.Horizontal != b.Horizontal) continue;
                if (RectIntersection(a, b) != null) continue; // handled by Case 2

                var band = CorridorOverlapOrTouchBand(a, b);
                if (band == null) continue;

                if (a.Horizontal)
                {
                    boxes.Add(new MapRect(
                        band.X,
                        Math.Min(a.Y, b.Y),
                        band.W,
                        Math.Max(a.Y + a.H, b.Y + b.H) - Math.Min(a.Y, b.Y)));
                }
                else
                {
                    boxes.Add(new MapRect(
                        Math.Min(a.X, b.X),
                        band.Y,
                        Math.Max(a.X + a.W, b.X + b.W) - Math.Min(a.X, b.X),
                        band.H));
                }
            }
        }

        return boxes;
    }
}
