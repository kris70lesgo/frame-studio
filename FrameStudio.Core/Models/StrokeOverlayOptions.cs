namespace FrameStudio.Core.Models;

/// <summary>A freehand stroke that is rasterized into every frame of a project.</summary>
public sealed record StrokeOverlayOptions(
    IReadOnlyList<PixelCoordinate> Points,
    int Thickness,
    Rgba32 Color);
