namespace FrameStudio.Core.Models;

/// <summary>A pixel size independent of any UI framework.</summary>
public readonly record struct PixelSize(int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>A point in device pixels, independent of any UI framework.</summary>
public readonly record struct PixelCoordinate(int X, int Y);

/// <summary>A rectangle in device pixels, independent of WPF and Avalonia geometry types.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public int Right => checked(X + Width);
    public int Bottom => checked(Y + Height);
}
