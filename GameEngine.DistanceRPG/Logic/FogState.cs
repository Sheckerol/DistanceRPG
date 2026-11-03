namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Runtime fog-of-war state: which tiles are currently visible and which have
/// ever been seen. Visibility is box-based — standing inside any fog box
/// reveals the whole box plus a one-tile border (the surrounding walls).
/// </summary>
public sealed class FogState
{
    /// <summary>Tiles currently in view. Reset each turn, indexed [row, col].</summary>
    public bool[,] Visible { get; }

    /// <summary>Tiles ever seen (persistent "explored" memory). Indexed [row, col].</summary>
    public bool[,] Seen { get; }

    public int Rows { get; }
    public int Cols { get; }

    public FogState(int rows, int cols)
    {
        Rows = rows;
        Cols = cols;
        Visible = new bool[rows, cols];
        Seen = new bool[rows, cols];
    }

    /// <summary>
    /// Reveal every fog box containing the given tile. Newly visible tiles are
    /// also marked seen. Returns the tiles that just became visible so the
    /// presentation layer can animate them.
    /// </summary>
    public List<(int R, int C, bool WasSeen)> RevealAt(int tileR, int tileC, IReadOnlyList<MapRect> fogBoxes)
    {
        var newlyVisible = new List<(int R, int C, bool WasSeen)>();

        foreach (var box in fogBoxes)
        {
            if (!box.Contains(tileR, tileC)) continue;

            // Reveal the box plus a one-tile border (the surrounding walls).
            int r0 = Math.Max(0, box.Y - 1);
            int r1 = Math.Min(Rows - 1, box.Y + box.H);
            int c0 = Math.Max(0, box.X - 1);
            int c1 = Math.Min(Cols - 1, box.X + box.W);
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    if (Visible[r, c]) continue;
                    Visible[r, c] = true;
                    newlyVisible.Add((r, c, Seen[r, c]));
                    Seen[r, c] = true;
                }
            }
        }

        return newlyVisible;
    }

    /// <summary>Clear current visibility (start of a new turn); Seen persists.</summary>
    public void ResetVisibility()
    {
        Array.Clear(Visible, 0, Visible.Length);
    }

    /// <summary>The character a save writes for a tile that is set.</summary>
    internal const char SetTile = '1';

    /// <summary>The character a save writes for a tile that is clear.</summary>
    internal const char ClearTile = '0';

    /// <summary>
    /// A fog grid as a save writes it (§5.1: "the explored fog grid"): one string
    /// per map row, <see cref="SetTile"/> for a tile that is set and
    /// <see cref="ClearTile"/> for one that is not — a floor is a few thousand
    /// tiles, and a row a line a person can read. <see cref="Decode"/> reads it back.
    /// </summary>
    internal static string[] Encode(bool[,] grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        int rows = grid.GetLength(0), cols = grid.GetLength(1);
        var lines = new string[rows];
        var line = new char[cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
                line[c] = grid[r, c] ? SetTile : ClearTile;
            lines[r] = new string(line);
        }
        return lines;
    }

    /// <summary>
    /// Fill <paramref name="grid"/> from <paramref name="lines"/> in
    /// <see cref="Encode"/>'s encoding: exactly one line per row of the grid, each
    /// exactly as long as a row, and nothing but the two characters.
    /// </summary>
    /// <exception cref="ArgumentException">The lines do not fit the grid, or hold another character; the message names the line.</exception>
    internal static void Decode(IReadOnlyList<string> lines, bool[,] grid, string paramName)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(grid);
        int rows = grid.GetLength(0), cols = grid.GetLength(1);
        if (lines.Count != rows)
            throw new ArgumentException($"{lines.Count} fog rows: the map has {rows}.", paramName);
        for (int r = 0; r < rows; r++)
        {
            string line = lines[r] ?? throw new ArgumentException($"Fog row {r} is null.", paramName);
            if (line.Length != cols)
                throw new ArgumentException($"Fog row {r} is {line.Length} tiles long: a map row is {cols}.", paramName);
            for (int c = 0; c < cols; c++)
            {
                grid[r, c] = line[c] switch
                {
                    SetTile => true,
                    ClearTile => false,
                    var other => throw new ArgumentException($"Fog row {r} holds '{other}' at column {c}: a tile is '{SetTile}' or '{ClearTile}'.", paramName),
                };
            }
        }
    }
}
