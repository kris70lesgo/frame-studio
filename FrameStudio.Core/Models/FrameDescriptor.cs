namespace FrameStudio.Core.Models;

/// <summary>Neutral metadata for one frame in a recording or imported animation.</summary>
public sealed record FrameDescriptor(
    int Index,
    int DurationMilliseconds,
    PixelRect Bounds,
    string? SourcePath = null,
    long DataLength = 0);
