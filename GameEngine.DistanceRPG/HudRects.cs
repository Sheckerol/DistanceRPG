using GameEngine.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Flat rectangles for the HUD — the party selector's HP and mana bars — in
/// window pixels, y down, as the text is placed. Drawn on the engine's
/// <c>overlay</c> program (a vec2 clip-space position and a <c>uColor</c>), the
/// one <see cref="GameEngine.Core.UI.FullscreenFade"/> uses, so no shader is
/// added: one quad, rewritten each fill. Each fill draws at once, so a rect
/// drawn before the frame's text flush sits under the text.
/// </summary>
internal sealed class HudRects : IDisposable
{
    private int _vao;
    private int _vbo;
    private bool _initialized;
    private bool _disposed;
    private readonly float[] _verts = new float[12];

    /// <summary>Allocate the quad. GL thread, a context current.</summary>
    public void Initialize()
    {
        if (_initialized) return;
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _verts.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        _initialized = true;
    }

    /// <summary>Fill the rectangle at (<paramref name="x"/>, <paramref name="y"/>), <paramref name="width"/> by <paramref name="height"/> pixels, in a window <paramref name="windowWidth"/> by <paramref name="windowHeight"/>. Nothing for an empty one.</summary>
    public void Fill(ShaderManager shaders, int windowWidth, int windowHeight, float x, float y, float width, float height, Vector4 color)
    {
        if (!_initialized || _disposed || width <= 0f || height <= 0f || windowWidth <= 0 || windowHeight <= 0) return;
        var program = shaders.GetShader("overlay");
        if (program == 0) return;

        float left = x / windowWidth * 2f - 1f;
        float right = (x + width) / windowWidth * 2f - 1f;
        float top = 1f - y / windowHeight * 2f;
        float bottom = 1f - (y + height) / windowHeight * 2f;
        float[] quad = [left, bottom, right, bottom, left, top, left, top, right, bottom, right, top];
        quad.CopyTo(_verts, 0);

        bool wasBlend = GL.IsEnabled(EnableCap.Blend);
        bool wasDepth = GL.IsEnabled(EnableCap.DepthTest);
        GL.UseProgram(program);
        var loc = GL.GetUniformLocation(program, "uColor");
        if (loc >= 0) GL.Uniform4(loc, color.X, color.Y, color.Z, color.W);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _verts.Length * sizeof(float), _verts);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);

        if (!wasBlend) GL.Disable(EnableCap.Blend);
        if (wasDepth) GL.Enable(EnableCap.DepthTest);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_initialized)
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            _initialized = false;
        }
    }
}
