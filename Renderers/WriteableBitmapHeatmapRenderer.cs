using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HeatmapBench.Core;

namespace HeatmapBench.Renderers;

/// <summary>
/// One bitmap pixel per cell. Changed cells are converted to colour through a
/// lookup table straight into the bitmap's back buffer; WPF scales the bitmap.
/// Nothing is allocated per frame.
/// </summary>
public sealed unsafe class WriteableBitmapHeatmapRenderer : IHeatmapRenderer
{
    // Fine enough to keep the colormap's narrow first band (0 to 0.1 on a 0 to 100 range).
    public const int LookupSize = 4096;

    private readonly double[,] _data;
    private readonly int _width;
    private readonly int _height;
    private readonly double _min;
    private readonly double _scale;
    private readonly uint[] _lut;
    private readonly WriteableBitmap _bitmap;
    private readonly Image _image;

    private Int32Rect _pending;
    private bool _locked;

    public WriteableBitmapHeatmapRenderer(double[,] data, double min, double max, RangeColormap colormap)
    {
        _data = data;
        _height = data.GetLength(0);
        _width = data.GetLength(1);
        _min = min;
        _scale = (LookupSize - 1) / (max - min);
        _lut = colormap.BuildLookupTable(min, max, LookupSize);

        _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr32, null);
        _image = new Image { Source = _bitmap, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);

        Update(new Int32Rect(0, 0, _width, _height));
        Refresh();
    }

    public FrameworkElement View => _image;

    public void Update(Int32Rect dirty)
    {
        if (dirty.Width <= 0 || dirty.Height <= 0)
        {
            return;
        }

        if (!_locked)
        {
            _bitmap.Lock();
            _locked = true;
            _pending = dirty;
        }
        else
        {
            int left = Math.Min(_pending.X, dirty.X);
            int top = Math.Min(_pending.Y, dirty.Y);
            int right = Math.Max(_pending.X + _pending.Width, dirty.X + dirty.Width);
            int bottom = Math.Max(_pending.Y + _pending.Height, dirty.Y + dirty.Height);
            _pending = new Int32Rect(left, top, right - left, bottom - top);
        }

        fixed (double* cells = _data)
        fixed (uint* lut = _lut)
        {
            ColourConverter.Convert(
                cells, _width,
                (byte*)_bitmap.BackBuffer, _bitmap.BackBufferStride,
                dirty.X, dirty.Y, dirty.Width, dirty.Height,
                lut, LookupSize, _min, _scale);
        }
    }

    public void Refresh()
    {
        if (!_locked)
        {
            return;
        }

        _bitmap.AddDirtyRect(_pending);
        _bitmap.Unlock();
        _locked = false;
    }
}
