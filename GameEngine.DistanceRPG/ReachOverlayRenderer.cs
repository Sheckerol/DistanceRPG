using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Draws <see cref="ReachArea"/>s on the dungeon floor: how far the active
/// member can walk, what it can reach from where it stands, the ring under each
/// enemy it can hit right now, and a hovered enemy's own walk and reach. Each
/// layer is a translucent fill plus a stronger band along its edge, laid just
/// above the floor in world units through <see cref="WorldSpace"/> — the one
/// place the logic lattice is turned into the world. Points on tiles the party
/// has never seen are left out, so the overlay never draws the shape of
/// unexplored floor. The scene hands in a fresh set with <see cref="Show"/>
/// only when something it reads changed, and the mesh is rebuilt then and only
/// then. Add it before the fog, so the shroud blends over it.
/// </summary>
public sealed class ReachOverlayRenderer : Component, IRenderableComponent, IShaderAwareComponent
{
    /// <summary>One area to draw: its fill, its edge band, and how high above the floor it lies (to keep layers apart).</summary>
    public readonly record struct Layer(ReachArea Area, Vector4 Fill, Vector4 Edge, float Lift);

    /// <summary>A ring on the floor around a logic point (an enemy's feet), radii in logic units.</summary>
    public readonly record struct Ring(float X, float Y, float Inner, float Outer, Vector4 Color);

    private const float Floor = 0.012f;          // just above the floor tiles
    private const int RingSegments = 40;

    /// <summary>Which tiles may show the overlay (the fog's Seen grid); null shows every tile.</summary>
    public Func<int, int, bool>? TileShown { get; set; }

    private ShaderManager? _shaderManager;
    private bool _buffersReady, _dirty;
    private int _vao, _vbo, _ebo;
    private readonly List<(Vector4 Color, int Start, int Count)> _draws = new();
    private IReadOnlyList<Layer> _layers = Array.Empty<Layer>();
    private IReadOnlyList<Ring> _rings = Array.Empty<Ring>();

    /// <summary>Replace what is drawn. An empty pair hides the overlay.</summary>
    public void Show(IReadOnlyList<Layer> layers, IReadOnlyList<Ring> rings)
    {
        _layers = layers;
        _rings = rings;
        _dirty = true;
    }

    /// <summary>Draw nothing.</summary>
    public void Clear() => Show(Array.Empty<Layer>(), Array.Empty<Ring>());

    /// <summary>Re-check which tiles are shown (the party has seen more floor).</summary>
    public void MarkDirty() => _dirty = true;

    public void SetShaderManager(ShaderManager shaderManager) => _shaderManager = shaderManager;

    public void Render(Camera camera, Matrix4 transform)
    {
        if (_shaderManager == null) return;
        int program = _shaderManager.GetShader("lit");
        if (program <= 0) return;

        if (!_buffersReady)
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.BindVertexArray(0);
            _buffersReady = true;
        }
        if (_dirty)
        {
            Rebuild();
            _dirty = false;
        }
        if (_draws.Count == 0) return;

        GL.UseProgram(program);
        var view = camera.ViewMatrix;
        var projection = camera.ProjectionMatrix;
        var model = transform;
        GL.UniformMatrix4(GL.GetUniformLocation(program, "view"), false, ref view);
        GL.UniformMatrix4(GL.GetUniformLocation(program, "projection"), false, ref projection);
        GL.UniformMatrix4(GL.GetUniformLocation(program, "model"), false, ref model);
        GL.Uniform3(GL.GetUniformLocation(program, "uLightDir"), Vector3.UnitY);   // flat-lit: the colours read as given
        int colorLoc = GL.GetUniformLocation(program, "uColor");

        GL.BindVertexArray(_vao);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.DepthMask(false);
        foreach (var (color, start, count) in _draws)
        {
            GL.Uniform4(colorLoc, color);
            GL.DrawElements(PrimitiveType.Triangles, count, DrawElementsType.UnsignedInt, start * sizeof(uint));
        }
        GL.DepthMask(true);
        GL.Disable(EnableCap.Blend);
        GL.BindVertexArray(0);
    }

    // ── Mesh building ────────────────────────────────────────────────────────

    private void Rebuild()
    {
        _draws.Clear();
        var vertices = new List<float>();
        var indices = new List<uint>();

        void Batch(Vector4 color, Action emit)
        {
            if (color.W <= 0f) return;
            int start = indices.Count;
            emit();
            if (indices.Count > start) _draws.Add((color, start, indices.Count - start));
        }

        foreach (var layer in _layers)
        {
            float y = Floor + layer.Lift;
            Batch(layer.Fill, () => EmitFill(vertices, indices, layer.Area, y));
            Batch(layer.Edge, () => EmitEdge(vertices, indices, layer.Area, y + 0.001f));
        }
        foreach (var ring in _rings)
            Batch(ring.Color, () => EmitRing(vertices, indices, ring, Floor + 0.01f));

        if (indices.Count == 0) return;
        var v = vertices.ToArray();
        var i = indices.ToArray();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, v.Length * sizeof(float), v, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, i.Length * sizeof(uint), i, BufferUsageHint.DynamicDraw);
        GL.BindVertexArray(0);
    }

    private bool Shown(ReachArea area, int row, int col)
    {
        if (!area.Contains(row, col)) return false;
        if (TileShown == null) return true;
        var (x, y) = ReachArea.NodePoint(row, col);
        return TileShown((int)MathF.Floor(y / GameConstants.Tile), (int)MathF.Floor(x / GameConstants.Tile));
    }

    // Each node owns the square half a step round it; a row's run of nodes is one quad.
    private void EmitFill(List<float> vertices, List<uint> indices, ReachArea area, float y)
    {
        float half = ReachArea.Step / 2f;
        for (int row = area.Row0; row < area.Row0 + area.Rows; row++)
        {
            int col = area.Col0, end = area.Col0 + area.Cols;
            while (col < end)
            {
                if (!Shown(area, row, col)) { col++; continue; }
                int first = col;
                while (col < end && Shown(area, row, col)) col++;
                var (x0, z0) = ReachArea.NodePoint(row, first);
                var (x1, _) = ReachArea.NodePoint(row, col - 1);
                AddQuad(vertices, indices, x0 - half, z0 - half, x1 + half, z0 + half, y);
            }
        }
    }

    // The edge band: every shown node with a four-way neighbour outside the area.
    private void EmitEdge(List<float> vertices, List<uint> indices, ReachArea area, float y)
    {
        float half = ReachArea.Step / 2f;
        for (int row = area.Row0; row < area.Row0 + area.Rows; row++)
        for (int col = area.Col0; col < area.Col0 + area.Cols; col++)
        {
            if (!Shown(area, row, col)) continue;
            if (area.Contains(row - 1, col) && area.Contains(row + 1, col)
                && area.Contains(row, col - 1) && area.Contains(row, col + 1)) continue;
            var (x, z) = ReachArea.NodePoint(row, col);
            AddQuad(vertices, indices, x - half, z - half, x + half, z + half, y);
        }
    }

    private static void EmitRing(List<float> vertices, List<uint> indices, Ring ring, float y)
    {
        for (int s = 0; s < RingSegments; s++)
        {
            float a0 = MathF.Tau * s / RingSegments, a1 = MathF.Tau * (s + 1) / RingSegments;
            var p0 = WorldSpace.FromLogic(ring.X + MathF.Cos(a0) * ring.Inner, ring.Y + MathF.Sin(a0) * ring.Inner, y);
            var p1 = WorldSpace.FromLogic(ring.X + MathF.Cos(a0) * ring.Outer, ring.Y + MathF.Sin(a0) * ring.Outer, y);
            var p2 = WorldSpace.FromLogic(ring.X + MathF.Cos(a1) * ring.Outer, ring.Y + MathF.Sin(a1) * ring.Outer, y);
            var p3 = WorldSpace.FromLogic(ring.X + MathF.Cos(a1) * ring.Inner, ring.Y + MathF.Sin(a1) * ring.Inner, y);
            AddFace(vertices, indices, p0, p1, p2, p3);
        }
    }

    // A floor quad over logic rectangle (x0, z0)–(x1, z1), converted to world units.
    private static void AddQuad(List<float> vertices, List<uint> indices, float x0, float z0, float x1, float z1, float y)
        => AddFace(vertices, indices,
            WorldSpace.FromLogic(x0, z0, y), WorldSpace.FromLogic(x1, z0, y),
            WorldSpace.FromLogic(x1, z1, y), WorldSpace.FromLogic(x0, z1, y));

    private static void AddFace(List<float> vertices, List<uint> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        uint baseIdx = (uint)(vertices.Count / 6);
        foreach (var p in new[] { a, b, c, d })
            vertices.AddRange([p.X, p.Y, p.Z, 0f, 1f, 0f]);
        // The fog's top-face winding: one face, seen from above (two would double the alpha).
        indices.AddRange([baseIdx, baseIdx + 2, baseIdx + 1, baseIdx, baseIdx + 3, baseIdx + 2]);
    }

    public override void Dispose()
    {
        if (_buffersReady)
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
        }
        base.Dispose();
    }
}
