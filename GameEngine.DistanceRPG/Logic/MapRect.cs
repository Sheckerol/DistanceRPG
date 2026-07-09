namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Axis-aligned rectangle in tile coordinates (X = column, Y = row).
/// Mutable because corridor expansion and fog-union passes grow rects in place,
/// mirroring the original JS objects.
/// </summary>
public class MapRect
{
    public int X;
    public int Y;
    public int W;
    public int H;

    public MapRect() { }

    public MapRect(int x, int y, int w, int h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public MapRect Clone() => new(X, Y, W, H);

    public bool Contains(int row, int col)
        => row >= Y && row < Y + H && col >= X && col < X + W;
}

/// <summary>A 2-tile-wide corridor run; Horizontal is the carve direction.</summary>
public sealed class Corridor : MapRect
{
    public bool Horizontal;

    public Corridor() { }

    public Corridor(int x, int y, int w, int h, bool horizontal) : base(x, y, w, h)
    {
        Horizontal = horizontal;
    }

    public new Corridor Clone() => new(X, Y, W, H, Horizontal);
}

/// <summary>A carved room with its centre tile precomputed.</summary>
public sealed class Room : MapRect
{
    public int Cx;
    public int Cy;

    public Room() { }

    public Room(int x, int y, int w, int h, int cx, int cy) : base(x, y, w, h)
    {
        Cx = cx;
        Cy = cy;
    }

    public new Room Clone() => new(X, Y, W, H, Cx, Cy);
}
