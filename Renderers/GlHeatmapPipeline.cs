using OpenTK.Graphics.OpenGL4;

namespace HeatmapBench.Renderers;

/// <summary>
/// The GPU side of one heatmap: the cell values live in a float texture, the
/// colormap in a second texture, and a fragment shader turns one into the
/// other while drawing a single full-screen triangle.
/// Uses no WPF types, so it runs in any OpenGL 3.3 context. Every call needs
/// that context to be current, and sets all the state it relies on, because
/// the context may be shared with other controls.
/// </summary>
public sealed unsafe class GlHeatmapPipeline
{
    // Row 0 of the grid is drawn at the top, as ScottPlot does.
    private const string VertexSource = """
        #version 330 core
        out vec2 uv;
        void main()
        {
            vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
            uv = vec2(p.x, 1.0 - p.y);
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    private const string FragmentSource = """
        #version 330 core
        in vec2 uv;
        out vec4 colour;
        uniform sampler2D grid;
        uniform sampler2D lut;
        uniform float minValue;
        uniform float scale;
        uniform int lastIndex;
        void main()
        {
            float value = texture(grid, uv).r;
            int index = clamp(int((value - minValue) * scale), 0, lastIndex);
            colour = vec4(texelFetch(lut, ivec2(index, 0), 0).rgb, 1.0);
        }
        """;

    private readonly int _width;
    private readonly int _program;
    private readonly int _vertexArray;
    private readonly int _gridTexture;
    private readonly int _lutTexture;

    /// <param name="lut">Colours as 0xRRGGBB; entry i is the colour for min + i / (length - 1) * (max - min).</param>
    public GlHeatmapPipeline(int width, int height, uint[] lut, double min, double max)
    {
        _width = width;
        _program = BuildProgram();
        _vertexArray = GL.GenVertexArray();

        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);

        _gridTexture = CreateTexture();
        GL.TexImage2D(
            TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, width, height, 0,
            PixelFormat.Red, PixelType.Float, IntPtr.Zero);

        _lutTexture = CreateTexture();
        fixed (uint* colours = lut)
        {
            GL.TexImage2D(
                TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, lut.Length, 1, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, (IntPtr)colours);
        }

        GL.UseProgram(_program);
        GL.Uniform1(GL.GetUniformLocation(_program, "grid"), 0);
        GL.Uniform1(GL.GetUniformLocation(_program, "lut"), 1);
        GL.Uniform1(GL.GetUniformLocation(_program, "minValue"), (float)min);
        GL.Uniform1(GL.GetUniformLocation(_program, "scale"), (float)((lut.Length - 1) / (max - min)));
        GL.Uniform1(GL.GetUniformLocation(_program, "lastIndex"), lut.Length - 1);
        GL.UseProgram(0);
    }

    /// <summary>
    /// Sends one rectangle of cells to the GPU.
    /// </summary>
    /// <param name="cells">The whole grid, row by row; only the rectangle is read.</param>
    public void Upload(float[] cells, int x, int y, int width, int height)
    {
        GL.BindTexture(TextureTarget.Texture2D, _gridTexture);
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        GL.PixelStore(PixelStoreParameter.UnpackRowLength, _width);

        fixed (float* first = &cells[y * _width + x])
        {
            GL.TexSubImage2D(
                TextureTarget.Texture2D, 0, x, y, width, height,
                PixelFormat.Red, PixelType.Float, (IntPtr)first);
        }

        GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
    }

    /// <summary>Draws the heatmap over the whole of the bound framebuffer's viewport.</summary>
    public void Draw()
    {
        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.ScissorTest);
        GL.Disable(EnableCap.CullFace);

        GL.UseProgram(_program);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _lutTexture);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _gridTexture);

        GL.BindVertexArray(_vertexArray);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
        GL.UseProgram(0);
    }

    private static int CreateTexture()
    {
        int texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        return texture;
    }

    private static int BuildProgram()
    {
        int vertex = Compile(ShaderType.VertexShader, VertexSource);
        int fragment = Compile(ShaderType.FragmentShader, FragmentSource);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertex);
        GL.AttachShader(program, fragment);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
        {
            throw new InvalidOperationException("Heatmap shader link failed: " + GL.GetProgramInfoLog(program));
        }

        GL.DetachShader(program, vertex);
        GL.DetachShader(program, fragment);
        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);
        return program;
    }

    private static int Compile(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            throw new InvalidOperationException($"Heatmap {type} compile failed: " + GL.GetShaderInfoLog(shader));
        }

        return shader;
    }
}
