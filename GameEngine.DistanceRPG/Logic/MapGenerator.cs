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
/// <para>
/// The corridors' width is the caller's (after play, 2026-10: "widen the halls
/// ... to 3 tiles"): <see cref="PrototypeCorridorWidth"/> by default, the
/// prototype's dungeon to the tile (the golden tests), and the game's floors
/// <see cref="CorridorWidth"/>. The carving draws nothing, so every width
/// places the same rooms off the same draws; a wider run keeps the prototype's
/// two lanes and adds more beyond them — below a horizontal run, right of a
/// vertical one, the L's corner moved to meet — so a wider map only ever adds
/// floor, and a party saved on a narrower one still stands on floor.
/// </para>
/// </summary>
public static class MapGenerator
{
    public const int Cols = 50;
    public const int Rows = 70;

    /// <summary>The prototype's corridors: two tiles wide.</summary>
    public const int PrototypeCorridorWidth = 2;

    /// <summary>
    /// The game's corridors, in tiles. Code, not tuning: a saved floor is
    /// regenerated from its seed (§5.1), so a number turned between a save and
    /// its load would move the walls under the saved party.
    /// </summary>
    public const int CorridorWidth = 3;

    private const int MinRoom = 5;
    private const int MaxRoom = 12;
    private const int Attempts = 300;

    public static MapData Generate(Mulberry32 rng, int corridorWidth = PrototypeCorridorWidth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(corridorWidth, PrototypeCorridorWidth);
        int rows = Rows, cols = Cols;
        int far = corridorWidth - 1;   // the lanes past a run's first: 1 in the prototype

        var grid = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                grid[r, c] = 1;

        var roomGrid = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                roomGrid[r, c] = -1;

        var corridors = new List<Corridor>();

        // Carve a corridorWidth-wide horizontal run, its lanes downward (stays inside border)
        void CarveH(int y, int x1, int x2)
        {
            var (xa, xb) = x1 <= x2 ? (x1, x2) : (x2, x1);
            for (int x = xa; x <= xb; x++)
                for (int lane = 0; lane < corridorWidth; lane++)
                    if (y + lane >= 1 && y + lane < rows - 1 && x >= 1 && x < cols - 1) grid[y + lane, x] = 0;
            corridors.Add(new Corridor(xa, y, xb - xa + 1, corridorWidth, horizontal: true));
        }

        // Carve a corridorWidth-wide vertical run, its lanes rightward (stays inside border)
        void CarveV(int x, int y1, int y2)
        {
            var (ya, yb) = y1 <= y2 ? (y1, y2) : (y2, y1);
            for (int y = ya; y <= yb; y++)
                for (int lane = 0; lane < corridorWidth; lane++)
                    if (y >= 1 && y < rows - 1 && x + lane >= 1 && x + lane < cols - 1) grid[y, x + lane] = 0;
            corridors.Add(new Corridor(x, ya, corridorWidth, yb - ya + 1, horizontal: false));
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
                int vCol = a.Cx < nearest.Cx ? nearest.Cx - far : nearest.Cx;
                int vRowEnd = nearest.Cy < a.Cy ? a.Cy + far : a.Cy;
                CarveH(a.Cy, a.Cx, nearest.Cx);
                CarveV(vCol, vRowEnd, nearest.Cy);
            }
            else
            {
                // V first, H second
                int vCol = nearest.Cx < a.Cx ? a.Cx - far : a.Cx;
                int vRowEnd = nearest.Cy > a.Cy ? nearest.Cy + far : nearest.Cy;
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
    /// Expand each corridor segment outward along its axis as far as every lane
    /// remains open floor — two, as generated; as many as
    /// <see cref="CorridorWidener"/> left it. Mutates corridor objects in place.
    /// </summary>
    public static void ExpandCorridors(List<Corridor> corridors, int[,] grid)
    {
        foreach (var seg in corridors)
        {
            if (seg.Horizontal)
            {
                bool Open(int x) => Enumerable.Range(seg.Y, seg.H).All(y => grid[y, x] == 0);
                while (seg.X > 0 && Open(seg.X - 1)) { seg.X--; seg.W++; }
                int xEnd = seg.X + seg.W;
                while (xEnd < Cols && Open(xEnd)) { seg.W++; xEnd++; }
            }
            else
            {
                bool Open(int y) => Enumerable.Range(seg.X, seg.W).All(x => grid[y, x] == 0);
                while (seg.Y > 0 && Open(seg.Y - 1)) { seg.Y--; seg.H++; }
                int yEnd = seg.Y + seg.H;
                while (yEnd < Rows && Open(yEnd)) { seg.H++; yEnd++; }
            }
        }
    }
}
