namespace FrameStudio.Core.Models;

/// <summary>Neutral summary of an editable frame sequence.</summary>
public sealed record FrameProject(
    string Name,
    PixelSize CanvasSize,
    IReadOnlyList<FrameDescriptor> Frames,
    DateTimeOffset ModifiedAt)
{
    public TimeSpan Duration => TimeSpan.FromMilliseconds(
        Frames.Sum(frame => Math.Max(0, frame.DurationMilliseconds)));
}
