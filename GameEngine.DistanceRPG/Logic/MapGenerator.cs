namespace GameEngine.DistanceRPG.Logic;

/// <summary>Output of <see cref="MapGenerator.Generate"/>.</summary>
public sealed class MapData
{
    /// <summary>Tile grid indexed [row, col]; 0 = floor, 1 = wall.</summary>
    public required int[,] Grid { get; init; }

    /// <summary>Room index per tile, -1 outside rooms. Indexed [row, col].</summary>
    public required int[,] RoomGrid { get; init; }

    public required List<Room> Rooms { get; init; }
    public required List<Corridor> Corridors { get; init; }

    public required (int Row, int Col) PlayerStart { get; init; }
    public required (int Row, int Col) EnemyStart { get; init; }

    /// <summary>Pristine copies taken before any later mutation (fog math, debug draw).</summary>
    public required List<Room> DebugRooms { get; init; }
    public required List<Corridor> DebugCorridors { get; init; }
}

/// <summary>
/// Procedural dungeon generator — a faithful port of the Phaser prototype's
/// mapGen.js so identical seeds produce identical dungeons. Places up to 300
/// candidate rooms with a 2-tile separation, then connects each room to its
/// nearest already-connected neighbour with L-shaped 2-tile-wide corridors
/// (greedy MST). RNG call order matches the JS exactly; do not reorder.
/// </summary>
public static class MapGenerator
{
    public const int Cols = 50;
    public const int Rows = 70;

    private const int MinRoom = 5;
    private const int MaxRoom = 12;
    private const int Attempts = 300;

    public static MapData Generate(Mulberry32 rng)
    {
        int rows = Rows, cols = Cols;

        var grid = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                grid[r, c] = 1;

        var roomGrid = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                roomGrid[r, c] = -1;

        var corridors = new List<Corridor>();

        // Carve a 2-tile-wide horizontal run (stays inside border)
        void CarveH(int y, int x1, int x2)
        {
            var (xa, xb) = x1 <= x2 ? (x1, x2) : (x2, x1);
            for (int x = xa; x <= xb; x++)
            {
                if (y >= 1 && y < rows - 1 && x >= 1 && x < cols - 1) grid[y, x] = 0;
                if (y + 1 >= 1 && y + 1 < rows - 1 && x >= 1 && x < cols - 1) grid[y + 1, x] = 0;
            }
            corridors.Add(new Corridor(xa, y, xb - xa + 1, 2, horizontal: true));
        }

        // Carve a 2-tile-wide vertical run (stays inside border)
        void CarveV(int x, int y1, int y2)
        {
            var (ya, yb) = y1 <= y2 ? (y1, y2) : (y2, y1);
            for (int y = ya; y <= yb; y++)
            {
                if (y >= 1 && y < rows - 1 && x >= 1 && x < cols - 1) grid[y, x] = 0;
                if (y >= 1 && y < rows - 1 && x + 1 >= 1 && x + 1 < cols - 1) grid[y, x + 1] = 0;
            }
            corridors.Add(new Corridor(x, ya, 2, yb - ya + 1, horizontal: false));
        }

        // Place rooms
        var rooms = new List<Room>();
        for (int i = 0; i < Attempts; i++)
        {
            int w = rng.NextInt(MinRoom, MaxRoom);
            int h = rng.NextInt(MinRoom, MaxRoom);
            int x = rng.NextInt(1, cols - w - 2);
            int y = rng.NextInt(1, rows - h - 2);

            bool overlaps = rooms.Any(r =>
                x < r.X + r.W + 2 && x + w + 2 > r.X &&
                y < r.Y + r.H + 2 && y + h + 2 > r.Y);
            if (overlaps) continue;

            int roomIdx = rooms.Count;
            for (int ry = y; ry < y + h; ry++)
                for (int rx = x; rx < x + w; rx++)
                {
                    grid[ry, rx] = 0;
                    roomGrid[ry, rx] = roomIdx;
                }

            rooms.Add(new Room(x, y, w, h,
                cx: (int)Math.Floor(x + w / 2.0),
                cy: (int)Math.Floor(y + h / 2.0)));
        }

        // Connect each room to the nearest already-connected room (greedy MST)
        for (int i = 1; i < rooms.Count; i++)
        {
            var a = rooms[i];
            var nearest = rooms[0];
            int minD = int.MaxValue;
            for (int j = 0; j < i; j++)
            {
                int d = Math.Abs(a.Cx - rooms[j].Cx) + Math.Abs(a.Cy - rooms[j].Cy);
                if (d < minD)
                {
                    minD = d;
                    nearest = rooms[j];
                }
            }

            if (rng.NextDouble() < 0.5)
            {
                // H first, V second
                int vCol = a.Cx < nearest.Cx ? nearest.Cx - 1 : nearest.Cx;
                int vRowEnd = nearest.Cy < a.Cy ? a.Cy + 1 : a.Cy;
                CarveH(a.Cy, a.Cx, nearest.Cx);
                CarveV(vCol, vRowEnd, nearest.Cy);
            }
            else
            {
                // V first, H second
                int vCol = nearest.Cx < a.Cx ? a.Cx - 1 : a.Cx;
                int vRowEnd = nearest.Cy > a.Cy ? nearest.Cy + 1 : nearest.Cy;
                CarveV(vCol, a.Cy, vRowEnd);
                CarveH(nearest.Cy, a.Cx, nearest.Cx);
            }
        }

        var playerStart = (rooms[0].Cy, rooms[0].Cx);
        var enemyStart = (rooms[^1].Cy, rooms[^1].Cx);
        int maxDist = -1;
        foreach (var r in rooms)
        {
            int d = Math.Abs(r.Cy - rooms[0].Cy) + Math.Abs(r.Cx - rooms[0].Cx);
            if (d > maxDist)
            {
                maxDist = d;
                enemyStart = (r.Cy, r.Cx);
            }
        }

        return new MapData
        {
            Grid = grid,
            RoomGrid = roomGrid,
            Rooms = rooms,
            Corridors = corridors,
            PlayerStart = playerStart,
            EnemyStart = enemyStart,
            DebugRooms = rooms.Select(r => r.Clone()).ToList(),
            DebugCorridors = corridors.Select(c => c.Clone()).ToList(),
        };
    }

    /// <summary>
    /// Expand each corridor segment outward along its axis as far as both lanes
    /// remain open floor. Mutates corridor objects in place.
    /// </summary>
    public static void ExpandCorridors(List<Corridor> corridors, int[,] grid)
    {
        foreach (var seg in corridors)
        {
            if (seg.Horizontal)
            {
                while (seg.X > 0
                       && grid[seg.Y, seg.X - 1] == 0
                       && grid[seg.Y + 1, seg.X - 1] == 0) { seg.X--; seg.W++; }
                int xEnd = seg.X + seg.W;
                while (xEnd < Cols
                       && grid[seg.Y, xEnd] == 0
                       && grid[seg.Y + 1, xEnd] == 0) { seg.W++; xEnd++; }
            }
            else
            {
                while (seg.Y > 0
                       && grid[seg.Y - 1, seg.X] == 0
                       && grid[seg.Y - 1, seg.X + 1] == 0) { seg.Y--; seg.H++; }
                int yEnd = seg.Y + seg.H;
                while (yEnd < Rows
                       && grid[yEnd, seg.X] == 0
                       && grid[yEnd, seg.X + 1] == 0) { seg.H++; yEnd++; }
            }
        }
    }
}
