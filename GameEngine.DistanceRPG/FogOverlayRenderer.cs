using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Renders the fog of war as floor-to-ceiling boxes filling each fogged tile:
/// never-seen tiles are opaque black (the world simply isn't there yet),
/// explored-but-out-of-view tiles are dimmed with a translucent shroud.
/// Only boundary faces are emitted — no interior walls between adjacent
/// fogged tiles — so the camera can never peek under the fog and the
/// translucent shroud blends exactly once per boundary crossing.
///
/// Reveals and re-fogs ripple like the Phaser prototype: each tile's fog
/// cube drops into the floor (or the shroud rises back out) over 250ms,
/// staggered 18ms per Manhattan step from the triggering character, via
/// <see cref="AnimateReveal"/> and <see cref="AnimateRefog"/>. The static
/// tile mesh is rebuilt only when <see cref="MarkDirty"/> is called;
/// animating tiles are drawn from a small per-frame dynamic buffer. Attach
/// to a GameObject added to the scene <b>last</b> so the translucent passes
/// blend over everything already drawn.
/// </summary>
public class FogOverlayRenderer : Component, IRenderableComponent, IShaderAwareComponent
{
    /// <summary>Fog grids to visualize. Set once after construction.</summary>
    public FogState? Fog { get; set; }

    /// <summary>Top of the fog volume, just above the wall tops. Boxes span the floor to here.</summary>
    public float Height { get; set; } = 1.25f;

    private const float DimAlpha = 0.65f;        // the prototype's explored-fog opacity
    private const float AnimDuration = 0.25f;    // 250ms per tile
    private const float StaggerPerTile = 0.018f; // 18ms per Manhattan step of the ripple

    private sealed class TileAnim
    {
        public int R, C;
        public float Delay;
        public float Age;

        /// <summary>Reveal only: true when previously-seen (dim) fog is clearing, not opaque fog.</summary>
        public bool Dim;
    }

    private readonly List<TileAnim> _revealAnims = new();
    private readonly List<TileAnim> _fillAnims = new();
    private readonly HashSet<(int R, int C)> _fillAnimTiles = new();

    private ShaderManager? _shaderManager;
    private bool _dirty = true;
    private bool _buffersReady;
    private int _vao, _vbo, _ebo;
    private int _unseenIndexCount;
    private int _dimIndexCount;

    // Per-frame buffer for animating tiles (small: one ripple's worth of quads).
    private int _animVao, _animVbo, _animEbo;
    private int _animOpaqueIndexCount;
    private int _animDimIndexCount;

    public void MarkDirty() => _dirty = true;

    /// <summary>
    /// Start shrink-away animations for tiles that just became visible,
    /// rippling outward from the revealing character's tile. Previously-seen
    /// tiles shed translucent fog; unexplored ones shed opaque fog.
    /// </summary>
    public void AnimateReveal(IReadOnlyList<(int R, int C, bool WasSeen)> tiles, (int R, int C) origin)
    {
        foreach (var (r, c, wasSeen) in tiles)
        {
            int dist = Math.Abs(r - origin.R) + Math.Abs(c - origin.C);
            _revealAnims.Add(new TileAnim { R = r, C = c, Delay = dist * StaggerPerTile, Dim = wasSeen });
        }
        MarkDirty();
    }

    /// <summary>
    /// Start grow-back animations for explored tiles that just left view
    /// (turn-end fog reset). The ripple runs inward: the farthest tile fills
    /// first, closing toward the active character like the prototype.
    /// </summary>
    public void AnimateRefog(IReadOnlyList<(int R, int C)> tiles, (int R, int C) origin)
    {
        if (tiles.Count == 0) return;

        int maxDist = 0;
        foreach (var (r, c) in tiles)
            maxDist = Math.Max(maxDist, Math.Abs(r - origin.R) + Math.Abs(c - origin.C));

        foreach (var (r, c) in tiles)
        {
            int dist = Math.Abs(r - origin.R) + Math.Abs(c - origin.C);
            _fillAnims.Add(new TileAnim { R = r, C = c, Delay = (maxDist - dist) * StaggerPerTile });
            _fillAnimTiles.Add((r, c));
        }
        MarkDirty(); // static mesh must exclude these until their animation completes
    }

    public override void Update(float deltaTime)
    {
        for (int i = _revealAnims.Count - 1; i >= 0; i--)
        {
            _revealAnims[i].Age += deltaTime;
            if (_revealAnims[i].Age - _revealAnims[i].Delay >= AnimDuration)
                _revealAnims.RemoveAt(i);
        }

        bool fillCompleted = false;
        for (int i = _fillAnims.Count - 1; i >= 0; i--)
        {
            _fillAnims[i].Age += deltaTime;
            if (_fillAnims[i].Age - _fillAnims[i].Delay >= AnimDuration)
            {
                _fillAnimTiles.Remove((_fillAnims[i].R, _fillAnims[i].C));
                _fillAnims.RemoveAt(i);
                fillCompleted = true;
            }
        }
        if (fillCompleted)
            MarkDirty(); // fold finished tiles into the static dim mesh
    }

    public void SetShaderManager(ShaderManager shaderManager) => _shaderManager = shaderManager;

    public void Render(Camera camera, Matrix4 transform)
    {
        if (Fog == null || _shaderManager == null) return;

        int program = _shaderManager.GetShader("lit");
        if (program <= 0) return;

        if (!_buffersReady)
        {
            (_vao, _vbo, _ebo) = CreateQuadBuffers();
            (_animVao, _animVbo, _animEbo) = CreateQuadBuffers();
            _buffersReady = true;
        }

        if (_dirty)
        {
            RebuildStaticMesh();
            _dirty = false;
        }

        bool hasAnims = _revealAnims.Count > 0 || _fillAnims.Count > 0;
        if (hasAnims)
            RebuildAnimMesh();

        if (_unseenIndexCount == 0 && _dimIndexCount == 0 && !hasAnims) return;

        GL.UseProgram(program);
        var view = camera.ViewMatrix;
        var projection = camera.ProjectionMatrix;
        var model = transform;
        GL.UniformMatrix4(GL.GetUniformLocation(program, "view"), false, ref view);
        GL.UniformMatrix4(GL.GetUniformLocation(program, "projection"), false, ref projection);
        GL.UniformMatrix4(GL.GetUniformLocation(program, "model"), false, ref model);
        int colorLoc = GL.GetUniformLocation(program, "uColor");
        GL.Uniform3(GL.GetUniformLocation(program, "uLightDir"), new Vector3(0.3f, 1.0f, 0.5f));

        // ── Static fog ──────────────────────────────────────────────────────
        GL.BindVertexArray(_vao);

        if (_unseenIndexCount > 0)
        {
            GL.Uniform4(colorLoc, new Vector4(0f, 0f, 0f, 1f));
            GL.DrawElements(PrimitiveType.Triangles, _unseenIndexCount, DrawElementsType.UnsignedInt, 0);
        }

        if (_dimIndexCount > 0)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false);

            GL.Uniform4(colorLoc, new Vector4(0f, 0f, 0f, DimAlpha));
            GL.DrawElements(PrimitiveType.Triangles, _dimIndexCount, DrawElementsType.UnsignedInt,
                _unseenIndexCount * sizeof(uint));

            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
        }

        // ── Animating tiles (shrinking reveals, growing refills) ────────────
        if (hasAnims && (_animOpaqueIndexCount > 0 || _animDimIndexCount > 0))
        {
            GL.BindVertexArray(_animVao);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false);

            if (_animOpaqueIndexCount > 0)
            {
                GL.Uniform4(colorLoc, new Vector4(0f, 0f, 0f, 1f));
                GL.DrawElements(PrimitiveType.Triangles, _animOpaqueIndexCount, DrawElementsType.UnsignedInt, 0);
            }
            if (_animDimIndexCount > 0)
            {
                GL.Uniform4(colorLoc, new Vector4(0f, 0f, 0f, DimAlpha));
                GL.DrawElements(PrimitiveType.Triangles, _animDimIndexCount, DrawElementsType.UnsignedInt,
                    _animOpaqueIndexCount * sizeof(uint));
            }

            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
        }

        GL.BindVertexArray(0);
    }

    // ── Mesh building ────────────────────────────────────────────────────────

    private static (int Vao, int Vbo, int Ebo) CreateQuadBuffers()
    {
        int vao = GL.GenVertexArray();
        int vbo = GL.GenBuffer();
        int ebo = GL.GenBuffer();
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.BindVertexArray(0);
        return (vao, vbo, ebo);
    }

    private enum FogCat { Clear, Dim, Unseen }

    /// <summary>Fog category of a tile; out-of-bounds and grow-back-animating tiles count as clear.</summary>
    private FogCat CategoryAt(int r, int c)
    {
        var fog = Fog!;
        if (r < 0 || c < 0 || r >= fog.Rows || c >= fog.Cols) return FogCat.Clear;
        if (!fog.Seen[r, c]) return FogCat.Unseen;
        if (fog.Visible[r, c]) return FogCat.Clear;
        return _fillAnimTiles.Contains((r, c)) ? FogCat.Clear : FogCat.Dim;
    }

    private void RebuildStaticMesh()
    {
        var fog = Fog!;
        var vertices = new List<float>();
        var unseenIndices = new List<uint>();
        var dimIndices = new List<uint>();

        for (int r = 0; r < fog.Rows; r++)
        {
            for (int c = 0; c < fog.Cols; c++)
            {
                var cat = CategoryAt(r, c);
                if (cat == FogCat.Clear) continue;

                // A side face exists only at a fog boundary: opaque fog shows
                // its flank to anything not opaque (it is visible through the
                // dim shroud), the dim shroud only to fully clear tiles —
                // never against opaque fog, where the coplanar face would
                // z-fight the black wall behind it.
                bool FaceTo(int nr, int nc) => cat == FogCat.Unseen
                    ? CategoryAt(nr, nc) != FogCat.Unseen
                    : CategoryAt(nr, nc) == FogCat.Clear;

                AddTileBox(vertices, cat == FogCat.Dim ? dimIndices : unseenIndices, r, c,
                    xzScale: 1f, yScale: 1f,
                    north: FaceTo(r - 1, c), south: FaceTo(r + 1, c),
                    west: FaceTo(r, c - 1), east: FaceTo(r, c + 1));
            }
        }

        _unseenIndexCount = unseenIndices.Count;
        _dimIndexCount = dimIndices.Count;
        UploadQuadMesh(_vao, _vbo, _ebo, vertices, unseenIndices, dimIndices);
    }

    private void RebuildAnimMesh()
    {
        var vertices = new List<float>();
        var opaqueIndices = new List<uint>();
        var dimIndices = new List<uint>();

        // Full-footprint cubes, inset a hair so their walls never sit coplanar
        // with (and z-fight) the boundary faces of still-fogged neighbors.
        const float animFootprint = 0.985f;

        foreach (var anim in _revealAnims)
        {
            // Before the ripple reaches this tile it is still fully fogged;
            // then the cube drops into the floor — ease-in, so it starts slow
            // and accelerates down like a collapse — while the scene spawns
            // smoke puffs in its volume.
            float t = Math.Clamp((anim.Age - anim.Delay) / AnimDuration, 0f, 1f);
            float height = 1f - t * t;
            if (height <= 0f) continue;
            AddTileBox(vertices, anim.Dim ? dimIndices : opaqueIndices, anim.R, anim.C,
                xzScale: animFootprint, yScale: height, north: true, south: true, west: true, east: true);
        }

        foreach (var anim in _fillAnims)
        {
            // Rises back out of the floor to a full dim cube — ease-out, the
            // mirror of the reveal drop.
            float t = Math.Clamp((anim.Age - anim.Delay) / AnimDuration, 0f, 1f);
            float height = 1f - (1f - t) * (1f - t);
            if (height <= 0f) continue;
            AddTileBox(vertices, dimIndices, anim.R, anim.C,
                xzScale: animFootprint, yScale: height, north: true, south: true, west: true, east: true);
        }

        _animOpaqueIndexCount = opaqueIndices.Count;
        _animDimIndexCount = dimIndices.Count;
        UploadQuadMesh(_animVao, _animVbo, _animEbo, vertices, opaqueIndices, dimIndices);
    }

    /// <summary>
    /// Emit a fog box for a tile: top face plus the requested side faces
    /// (north = toward row-1, west = toward col-1). The box stands on the
    /// floor; scales shrink it toward its floor center for animations.
    /// No bottom face — the camera never sees fog from below.
    /// </summary>
    private void AddTileBox(List<float> vertices, List<uint> indices, int r, int c,
        float xzScale, float yScale, bool north, bool south, bool west, bool east)
    {
        float inset = (1f - xzScale) * WorldSpace.UnitsPerTile / 2f;
        float x0 = c * WorldSpace.UnitsPerTile + inset;
        float x1 = (c + 1) * WorldSpace.UnitsPerTile - inset;
        float z0 = r * WorldSpace.UnitsPerTile + inset;
        float z1 = (r + 1) * WorldSpace.UnitsPerTile - inset;
        float y1 = Height * yScale;

        AddFace(vertices, indices,
            new Vector3(x0, y1, z0), new Vector3(x1, y1, z0),
            new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.UnitY);

        if (north)
            AddFace(vertices, indices,
                new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0),
                new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), -Vector3.UnitZ);
        if (south)
            AddFace(vertices, indices,
                new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1),
                new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), Vector3.UnitZ);
        if (west)
            AddFace(vertices, indices,
                new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0),
                new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), -Vector3.UnitX);
        if (east)
            AddFace(vertices, indices,
                new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1),
                new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.UnitX);
    }

    /// <summary>Quad a→b→c→d wound so its front faces along <paramref name="n"/>.</summary>
    private static void AddFace(List<float> vertices, List<uint> indices,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
    {
        uint baseIdx = (uint)(vertices.Count / 6);
        Span<Vector3> corners = stackalloc Vector3[] { a, b, c, d };
        foreach (var v in corners)
            vertices.AddRange(new[] { v.X, v.Y, v.Z, n.X, n.Y, n.Z });

        indices.Add(baseIdx);
        indices.Add(baseIdx + 2);
        indices.Add(baseIdx + 1);
        indices.Add(baseIdx);
        indices.Add(baseIdx + 3);
        indices.Add(baseIdx + 2);
    }

    private static void UploadQuadMesh(int vao, int vbo, int ebo,
        List<float> vertices, List<uint> firstRange, List<uint> secondRange)
    {
        var indices = new uint[firstRange.Count + secondRange.Count];
        firstRange.CopyTo(indices, 0);
        secondRange.CopyTo(indices, firstRange.Count);
        var vertexArray = vertices.ToArray();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertexArray.Length * sizeof(float), vertexArray, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.DynamicDraw);
        GL.BindVertexArray(0);
    }

    public override void Dispose()
    {
        if (_buffersReady)
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteVertexArray(_animVao);
            GL.DeleteBuffer(_animVbo);
            GL.DeleteBuffer(_animEbo);
        }
        base.Dispose();
    }
}
