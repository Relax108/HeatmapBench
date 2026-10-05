using System.Windows;

namespace HeatmapBench.Core;

/// <summary>
/// Persistent grid plus the bounding box of the cells changed since the last render.
/// </summary>
public sealed class HeatmapBuffer
{
    public readonly int Width;
    public readonly int Height;
    public readonly double[,] Data;

    // Held only long enough to swap four ints, never while rendering,
    // so the producer cannot be made to wait for a frame.
    private readonly object _gate = new();
    private bool _dirty;
    private int _minX, _minY, _maxX, _maxY;

    public HeatmapBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new double[height, width];
    }

    public Int32Rect FullRegion => new(0, 0, Width, Height);

    public void MarkDirty() => MarkDirty(0, 0, Width - 1, Height - 1);

    /// <summary>Bounds are inclusive cell coordinates.</summary>
    public void MarkDirty(int minX, int minY, int maxX, int maxY)
    {
        lock (_gate)
        {
            if (!_dirty)
            {
                _minX = minX;
                _minY = minY;
                _maxX = maxX;
                _maxY = maxY;
                _dirty = true;
                return;
            }

            if (minX < _minX) _minX = minX;
            if (minY < _minY) _minY = minY;
            if (maxX > _maxX) _maxX = maxX;
            if (maxY > _maxY) _maxY = maxY;
        }
    }

    public bool ConsumeDirty(out Int32Rect region)
    {
        lock (_gate)
        {
            if (!_dirty)
            {
                region = default;
                return false;
            }

            region = new Int32Rect(_minX, _minY, _maxX - _minX + 1, _maxY - _minY + 1);
            _dirty = false;
            return true;
        }
    }
}
