namespace HeatmapBench.Core;

/// <summary>
/// Colour stops placed at data values, blended smoothly between neighbours.
/// Values outside the first and last stop take the nearest stop's colour.
/// Colours are 0xRRGGBB.
/// </summary>
public sealed class RangeColormap
{
    private readonly (double Value, uint Rgb)[] _stops;

    public RangeColormap(params (double Value, uint Rgb)[] stops)
    {
        _stops = stops;
    }

    public static RangeColormap Amplitude { get; } = new(
        (0, 0x000000),    // Black
        (0.1, 0xFFFFFF),  // White
        (30, 0x6495ED),   // CornflowerBlue
        (40, 0x0000FF),   // Blue
        (48, 0x000080),   // Navy
        (55, 0xFFFF00),   // Yellow
        (65, 0xFFD700),   // Gold
        (75, 0xFFA500),   // Orange
        (82, 0xFF4500),   // OrangeRed
        (90, 0xFF0000),   // Red
        (96, 0x8B0000));  // DarkRed

    public uint Sample(double value)
    {
        if (!(value > _stops[0].Value))
        {
            return _stops[0].Rgb;
        }

        for (int i = 1; i < _stops.Length; i++)
        {
            if (value <= _stops[i].Value)
            {
                (double fromValue, uint from) = _stops[i - 1];
                (double toValue, uint to) = _stops[i];
                double t = (value - fromValue) / (toValue - fromValue);

                uint red = Blend((from >> 16) & 0xFF, (to >> 16) & 0xFF, t);
                uint green = Blend((from >> 8) & 0xFF, (to >> 8) & 0xFF, t);
                uint blue = Blend(from & 0xFF, to & 0xFF, t);
                return (red << 16) | (green << 8) | blue;
            }
        }

        return _stops[^1].Rgb;
    }

    /// <summary>Entry i holds the colour for min + i / (size - 1) * (max - min).</summary>
    public uint[] BuildLookupTable(double min, double max, int size)
    {
        uint[] table = new uint[size];
        for (int i = 0; i < size; i++)
        {
            table[i] = Sample(min + i / (double)(size - 1) * (max - min));
        }

        return table;
    }

    private static uint Blend(uint from, uint to, double t) => (uint)Math.Round(from + (to - from) * t);
}
