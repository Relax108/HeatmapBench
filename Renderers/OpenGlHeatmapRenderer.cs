using System.Diagnostics;
using System.Windows;
using HeatmapBench.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;

namespace HeatmapBench.Renderers;

/// <summary>
/// Colour mapping and scaling on the GPU. The CPU's only per-frame work is
/// narrowing the changed cells from double to float (OpenGL has no double
/// textures) and handing that rectangle to the driver.
/// </summary>
public sealed unsafe class OpenGlHeatmapRenderer : IHeatmapRenderer
{
    private readonly double[,] _data;
    private readonly int _width;
    private readonly int _height;
    private readonly double _min;
    private readonly double _max;
    private readonly uint[] _lut;
    private readonly float[] _staging;
    private readonly FrameStats _stats;
    private readonly GLWpfControl _control;

    private GlHeatmapPipeline? _pipeline;
    private Int32Rect _pending;
    private bool _hasPending;

    public OpenGlHeatmapRenderer(double[,] data, double min, double max, RangeColormap colormap, FrameStats stats)
    {
        _data = data;
        _height = data.GetLength(0);
        _width = data.GetLength(1);
        _min = min;
        _max = max;
        _lut = colormap.BuildLookupTable(min, max, WriteableBitmapHeatmapRenderer.LookupSize);
        _staging = new float[_width * _height];
        _stats = stats;

        _control = new GLWpfControl();
        _control.Render += OnRender;
        _control.Start(new GLWpfControlSettings
        {
            MajorVersion = 3,
            MinorVersion = 3,
            RenderContinuously = false,
        });

        Update(new Int32Rect(0, 0, _width, _height));
    }

    public FrameworkElement View => _control;

    /// <summary>Copies the changed cells into the float staging array. No OpenGL calls: the context is only current while the control renders.</summary>
    public void Update(Int32Rect dirty)
    {
        if (dirty.Width <= 0 || dirty.Height <= 0)
        {
            return;
        }

        fixed (double* source = _data)
        fixed (float* target = _staging)
        {
            for (int y = dirty.Y; y < dirty.Y + dirty.Height; y++)
            {
                double* src = source + (long)y * _width + dirty.X;
                float* dst = target + (long)y * _width + dirty.X;

                for (int x = 0; x < dirty.Width; x++)
                {
                    dst[x] = (float)src[x];
                }
            }
        }

        if (!_hasPending)
        {
            _pending = dirty;
            _hasPending = true;
            return;
        }

        int left = Math.Min(_pending.X, dirty.X);
        int top = Math.Min(_pending.Y, dirty.Y);
        int right = Math.Max(_pending.X + _pending.Width, dirty.X + dirty.Width);
        int bottom = Math.Max(_pending.Y + _pending.Height, dirty.Y + dirty.Height);
        _pending = new Int32Rect(left, top, right - left, bottom - top);
    }

    public void Refresh()
    {
        _control.InvalidateVisual();
    }

    /// <summary>Called by the control during WPF's render pass, with its framebuffer bound.</summary>
    private void OnRender(TimeSpan elapsed)
    {
        long start = Stopwatch.GetTimestamp();

        _pipeline ??= new GlHeatmapPipeline(_width, _height, _lut, _min, _max);

        if (_hasPending)
        {
            _pipeline.Upload(_staging, _pending.X, _pending.Y, _pending.Width, _pending.Height);
            _hasPending = false;
        }

        GL.Viewport(0, 0, _control.FrameBufferWidth, _control.FrameBufferHeight);
        _pipeline.Draw();

        // Waits for the GPU, so the upload and the draw are inside the measured time.
        GL.Finish();

        _stats.Paint.Add(start, Stopwatch.GetTimestamp());
    }
}
