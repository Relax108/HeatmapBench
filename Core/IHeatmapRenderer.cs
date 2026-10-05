using System.Windows;

namespace HeatmapBench.Core;

public interface IHeatmapRenderer
{
    /// <summary>The element to place in the window.</summary>
    FrameworkElement View { get; }

    /// <summary>Bring the renderer's image up to date for the changed cells.</summary>
    void Update(Int32Rect dirty);

    /// <summary>Ask for the image to be shown.</summary>
    void Refresh();
}
