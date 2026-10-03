namespace FrameStudio.Core.Models;

/// <summary>Text to rasterize into every selected project frame. Position uses canvas pixels.</summary>
public sealed record TextOverlayOptions(
    string Text,
    int X,
    int Y,
    int FontSize,
    Rgba32 Color);
