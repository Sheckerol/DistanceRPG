using GameEngine.Core;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Renders the fog of war as a horizontal quad layer above the dungeon:
/// never-seen tiles are opaque black (the world simply isn't there yet),
/// explored-but-out-of-view tiles are dimmed with a translucent shroud.
/// The tile mesh is rebuilt only when <see cref="MarkDirty"/> is called
/// (fog changes at most on tile crossings). Attach to a GameObject that is
/// added to the scene <b>last</b> so the translucent pass blends over
/// everything already drawn.
/// </summary>
public class FogOverlayRenderer : Component, IRenderableComponent, IShaderAwareComponent
{
    /// <summary>Fog grids to visualize. Set once after construction.</summary>
    public FogState? Fog { get; set; }

    /// <summary>Height of the fog layer, just above the wall tops.</summary>
    public float Height { get; set; } = 1.25f;

    private const float DimAlpha = 0.65f; // the prototype's explored-fog opacity

    private ShaderManager? _shaderManager;
    private bool _dirty = true;
    private bool _buffersReady;
    private int _vao, _vbo, _ebo;
    private int _unseenIndexCount;
    private int _dimIndexCount;

    public void MarkDirty() => _dirty = true;

    public void SetShaderManager(ShaderManager shaderManager) => _shaderManager = shaderManager;

    public void Render(Camera camera, Matrix4 transform)
    {
        if (Fog == null || _shaderManager == null) return;

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
            RebuildMesh();
            _dirty = false;
        }

        if (_unseenIndexCount == 0 && _dimIndexCount == 0) return;

        GL.UseProgram(program);
        var view = camera.ViewMatrix;
        var projection = camera.ProjectionMatrix;
        var model = transform;
        GL.UniformMatrix4(GL.GetUniformLocation(program, "view"), false, ref view);
        GL.UniformMatrix4(GL.GetUniformLocation(program, "projection"), false, ref projection);
        GL.UniformMatrix4(GL.GetUniformLocation(program, "model"), false, ref model);
        int colorLoc = GL.GetUniformLocation(program, "uColor");
        GL.Uniform3(GL.GetUniformLocation(program, "uLightDir"), new Vector3(0.3f, 1.0f, 0.5f));

        GL.BindVertexArray(_vao);

        // Opaque pass: tiles never seen.
        if (_unseenIndexCount > 0)
        {
            GL.Uniform4(colorLoc, new Vector4(0f, 0f, 0f, 1f));
            GL.DrawElements(PrimitiveType.Triangles, _unseenIndexCount, DrawElementsType.UnsignedInt, 0);
        }

        // Translucent pass: explored but currently out of view. Depth writes
        // off so the shroud never occludes later translucent draws.
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

        GL.BindVertexArray(0);
    }

    private void RebuildMesh()
    {
        var fog = Fog!;
        var vertices = new List<float>();
        var unseenIndices = new List<uint>();
        var dimIndices = new List<uint>();

        for (int r = 0; r < fog.Rows; r++)
        {
            for (int c = 0; c < fog.Cols; c++)
            {
                bool seen = fog.Seen[r, c];
                bool visible = fog.Visible[r, c];
                if (seen && visible) continue;

                var target = seen ? dimIndices : unseenIndices;
                uint baseIdx = (uint)(vertices.Count / 6);

                float x0 = c * WorldSpace.UnitsPerTile;
                float x1 = x0 + WorldSpace.UnitsPerTile;
                float z0 = r * WorldSpace.UnitsPerTile;
                float z1 = z0 + WorldSpace.UnitsPerTile;

                // 4 corners, normal up
                vertices.AddRange(new[] { x0, Height, z0, 0f, 1f, 0f });
                vertices.AddRange(new[] { x1, Height, z0, 0f, 1f, 0f });
                vertices.AddRange(new[] { x1, Height, z1, 0f, 1f, 0f });
                vertices.AddRange(new[] { x0, Height, z1, 0f, 1f, 0f });

                target.Add(baseIdx);
                target.Add(baseIdx + 2);
                target.Add(baseIdx + 1);
                target.Add(baseIdx);
                target.Add(baseIdx + 3);
                target.Add(baseIdx + 2);
            }
        }

        _unseenIndexCount = unseenIndices.Count;
        _dimIndexCount = dimIndices.Count;

        var indices = new uint[_unseenIndexCount + _dimIndexCount];
        unseenIndices.CopyTo(indices, 0);
        dimIndices.CopyTo(indices, _unseenIndexCount);
        var vertexArray = vertices.ToArray();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertexArray.Length * sizeof(float), vertexArray, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
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
        }
        base.Dispose();
    }
}
