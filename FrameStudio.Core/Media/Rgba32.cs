namespace FrameStudio.Core.Models;

/// <summary>A framework-neutral, byte-ordered RGBA color.</summary>
public readonly record struct Rgba32(byte R, byte G, byte B, byte A)
{
    public static Rgba32 FromArgb(byte alpha, byte red, byte green, byte blue) => new(red, green, blue, alpha);
    public static Rgba32 FromRgb(byte red, byte green, byte blue) => new(red, green, blue, byte.MaxValue);
}

public static class Rgba32Colors
{
    public static Rgba32 Transparent => new(0, 0, 0, 0);
}
