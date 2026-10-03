namespace FrameStudio.Core.Models;

/// <summary>Neutral metadata for one frame in a recording or imported animation.</summary>
public sealed record FrameDescriptor(
    int Index,
    int DurationMilliseconds,
    PixelRect Bounds,
    string? SourcePath = null,
    long DataLength = 0);

/// <summary>One editable timeline item linked to the source frame pixels in an archive.</summary>
public sealed record ProjectFrameReference(int SourceFrameIndex, int DurationMilliseconds);
