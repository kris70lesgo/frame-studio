using FrameStudio.Core.Codification.Gif.Encoder;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;

namespace FrameStudio.Core.Export;

public sealed record GifExportOptions(
    int RepeatCount = 0,
    int MaximumColors = 256,
    ColorQuantizationTypes Quantization = ColorQuantizationTypes.Octree,
    int ScalePercent = 100,
    int FramesPerSecond = 0,
    bool Dither = false,
    PixelSize? OutputSize = null)
{
    public GifExportOptions Validate()
    {
        if (RepeatCount is < -1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(RepeatCount), "Repeat count must be -1 (no loop), 0 (forever), or at most 65,535.");
        if (MaximumColors is < 2 or > 256)
            throw new ArgumentOutOfRangeException(nameof(MaximumColors), "GIF palette size must be between 2 and 256 colors.");
        if (!Enum.IsDefined(Quantization))
            throw new ArgumentOutOfRangeException(nameof(Quantization));
        if (ScalePercent is < 1 or > 400)
            throw new ArgumentOutOfRangeException(nameof(ScalePercent), "GIF output scale must be between 1% and 400%.");
        if (FramesPerSecond is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(FramesPerSecond), "GIF output frame rate must be 0 (source timing) or between 1 and 100 FPS.");
        if (OutputSize is { } outputSize)
            FrameProjectArchiveWriter.ValidateCanvasSize(outputSize);
        return this;
    }
}
