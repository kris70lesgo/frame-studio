namespace FrameStudio.Core.Export;

public sealed record FfmpegMp4ExportOptions(int Crf = 23, int ScalePercent = 100)
{
    public FfmpegMp4ExportOptions Validate()
    {
        if (Crf is < 0 or > 51)
            throw new ArgumentOutOfRangeException(nameof(Crf), "FFmpeg CRF must be between 0 and 51.");
        if (ScalePercent is < 1 or > 400)
            throw new ArgumentOutOfRangeException(nameof(ScalePercent), "Output scale must be between 1% and 400%.");
        return this;
    }
}
