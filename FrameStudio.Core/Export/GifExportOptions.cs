using FrameStudio.Core.Codification.Gif.Encoder;

namespace FrameStudio.Core.Export;

public sealed record GifExportOptions(
    int RepeatCount = 0,
    int MaximumColors = 256,
    ColorQuantizationTypes Quantization = ColorQuantizationTypes.Octree);
