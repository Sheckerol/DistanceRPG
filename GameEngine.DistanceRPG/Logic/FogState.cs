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
    public List<(int R, int C)> RevealAt(int tileR, int tileC, IReadOnlyList<MapRect> fogBoxes)
    {
        var newlyVisible = new List<(int R, int C)>();

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
                    Seen[r, c] = true;
                    newlyVisible.Add((r, c));
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
}
