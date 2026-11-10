using GameEngine.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Draws a base under every body on the floor, as a miniature stands on one: a
/// dark disc with a rim in the body's colour, at the radius every reach and
/// collision is measured from (<see cref="Logic.ActorState.Radius"/>) — wider
/// than the box drawn on it, which is only a figure. So what the eye judges is
/// what the rules measure: an attack lands when the gap between two bases is
/// within the weapon's range, and an enemy is in the reach overlay's zone when
/// its base touches it. The scene says which bases stand where
/// (<see cref="Bases"/>), asked every frame, and the mesh is rebuilt only when
/// the answer changes. Add it before the fog, so the shroud blends over it.
/// </summary>
public sealed class MiniBaseRenderer : Component, IRenderableComponent, IShaderAwareComponent
{
    /// <summary>One base: its centre and radius in world units (on the XZ plane), and its rim's colour.</summary>
    public readonly record struct MiniBase(float X, float Z, float Radius, Vector4 Rim);

    private const float Height = 0.03f;          // above the reach overlay's layers, under every figure
    private const int Segments = 32;
    private const float RimWidth = 2.5f * WorldSpace.LogicToWorld;
    private static readonly Vector4 DiscColor = new(0.07f, 0.07f, 0.09f, 0.80f);

    /// <summary>The bases to draw now; null draws none.</summary>
    public Func<IReadOnlyList<MiniBase>>? Bases { get; set; }

    private ShaderManager? _shaderManager;
    private bool _buffersReady;
    private int _vao, _vbo, _ebo;
    private int _discCount;
    private readonly List<(Vector4 Color, int Start, int Count)> _rims = new();
    private MiniBase[] _built = Array.Empty<MiniBase>();

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

        var bases = Bases?.Invoke() ?? Array.Empty<MiniBase>();
        if (!bases.SequenceEqual(_built))
        {
            _built = bases.ToArray();
            Rebuild();
        }
        if (_built.Length == 0) return;

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
        GL.Uniform4(colorLoc, DiscColor);
        GL.DrawElements(PrimitiveType.Triangles, _discCount, DrawElementsType.UnsignedInt, 0);
        foreach (var (color, start, count) in _rims)
        {
            GL.Uniform4(colorLoc, color);
            GL.DrawElements(PrimitiveType.Triangles, count, DrawElementsType.UnsignedInt, start * sizeof(uint));
        }
        GL.DepthMask(true);
        GL.Disable(EnableCap.Blend);
        GL.BindVertexArray(0);
    }

    // Every disc first (one draw, one colour), then each rim in its own colour, a hair above.
    private void Rebuild()
    {
        _rims.Clear();
        var vertices = new List<float>();
        var indices = new List<uint>();

        foreach (var b in _built)
            for (int s = 0; s < Segments; s++)
            {
                var (a0, a1) = Angles(s);
                uint i = (uint)(vertices.Count / 6);
                Vertex(vertices, b.X, b.Z, Height);
                Vertex(vertices, b.X + MathF.Cos(a0) * b.Radius, b.Z + MathF.Sin(a0) * b.Radius, Height);
                Vertex(vertices, b.X + MathF.Cos(a1) * b.Radius, b.Z + MathF.Sin(a1) * b.Radius, Height);
                indices.AddRange([i, i + 2, i + 1]);   // the fog's top-face winding, seen from above
            }
        _discCount = indices.Count;

        foreach (var b in _built)
        {
            int start = indices.Count;
            float inner = MathF.Max(0f, b.Radius - RimWidth), outer = b.Radius;
            for (int s = 0; s < Segments; s++)
            {
                var (a0, a1) = Angles(s);
                uint i = (uint)(vertices.Count / 6);
                Vertex(vertices, b.X + MathF.Cos(a0) * inner, b.Z + MathF.Sin(a0) * inner, Height + 0.001f);
                Vertex(vertices, b.X + MathF.Cos(a0) * outer, b.Z + MathF.Sin(a0) * outer, Height + 0.001f);
                Vertex(vertices, b.X + MathF.Cos(a1) * outer, b.Z + MathF.Sin(a1) * outer, Height + 0.001f);
                Vertex(vertices, b.X + MathF.Cos(a1) * inner, b.Z + MathF.Sin(a1) * inner, Height + 0.001f);
                indices.AddRange([i, i + 2, i + 1, i, i + 3, i + 2]);
            }
            _rims.Add((b.Rim, start, indices.Count - start));
        }

        if (indices.Count == 0) return;
        var v = vertices.ToArray();
        var idx = indices.ToArray();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, v.Length * sizeof(float), v, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, idx.Length * sizeof(uint), idx, BufferUsageHint.DynamicDraw);
        GL.BindVertexArray(0);
    }

    private static (float, float) Angles(int s) => (MathF.Tau * s / Segments, MathF.Tau * (s + 1) / Segments);

    private static void Vertex(List<float> vertices, float x, float z, float y) => vertices.AddRange([x, y, z, 0f, 1f, 0f]);

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
