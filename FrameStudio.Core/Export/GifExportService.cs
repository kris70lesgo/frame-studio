using FrameStudio.Core.Codification.Gif.Encoder;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;

namespace FrameStudio.Core.Export;

public sealed class GifExportService
{
    public async ValueTask ExportAsync(string projectPath, string destinationPath, GifExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await ExportCoreAsync(projectPath, destinationPath, null, options, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ExportSelectionAsync(string projectPath, string destinationPath,
        IReadOnlyList<ProjectFrameReference> frames, GifExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        await ExportCoreAsync(projectPath, destinationPath, frames, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ExportCoreAsync(string projectPath, string destinationPath,
        IReadOnlyList<ProjectFrameReference>? frameSelection, GifExportOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        options = (options ?? new GifExportOptions()).Validate();

        await using var reader = await FrameProjectArchiveReader.OpenAsync(projectPath, cancellationToken).ConfigureAwait(false);
        var project = reader.Project;
        var outputSize = options.OutputSize ?? ScaleSize(project.CanvasSize, options.ScalePercent);
        var frames = frameSelection ?? project.Frames
            .Select(frame => new ProjectFrameReference(frame.Index, frame.DurationMilliseconds)).ToArray();
        if (frames.Count is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(frameSelection), "GIF export requires between 1 and 100,000 frames.");
        foreach (var frame in frames)
        {
            if ((uint)frame.SourceFrameIndex >= (uint)project.Frames.Count || frame.DurationMilliseconds <= 0)
                throw new ArgumentException("The GIF frame selection contains an invalid source index or duration.", nameof(frameSelection));
        }
        if (options.FramesPerSecond > 0)
            frames = ResampleFrames(frames, options.FramesPerSecond);
        if (frames.Count > 100_000)
            throw new ArgumentOutOfRangeException(nameof(frameSelection), "The selected GIF frame rate would produce more than 100,000 output frames.");

        var fullDestination = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullDestination)!;
        Directory.CreateDirectory(directory);
        var partialPath = Path.Combine(directory, $".{Path.GetFileName(fullDestination)}.{Guid.NewGuid():N}.partial");

        try
        {
            await using (var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024, useAsync: true))
            {
                var encoder = new GifFile(output)
                {
                    RepeatCount = options.RepeatCount,
                    MaximumNumberColor = options.MaximumColors,
                    QuantizationType = options.Quantization,
                    UseDithering = options.Dither
                };

                try
                {
                    for (var index = 0; index < frames.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var frame = frames[index];
                        var pixels = await reader.ReadFrameRgbaAsync(frame.SourceFrameIndex, cancellationToken).ConfigureAwait(false);
                        if (outputSize != project.CanvasSize)
                            pixels = RgbaFrameTransform.ResizeNearestNeighbor(pixels, project.CanvasSize, outputSize);
                        var boundedDelay = Math.Clamp(frame.DurationMilliseconds, 10, 655_350);
                        encoder.AddFrame(pixels, new PixelRect(0, 0, outputSize.Width, outputSize.Height),
                            boundedDelay, index == frames.Count - 1);
                    }
                }
                finally
                {
                    encoder.Dispose();
                }
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialPath, fullDestination, overwrite: true);
        }
        catch
        {
            try { File.Delete(partialPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private static PixelSize ScaleSize(PixelSize source, int scalePercent)
    {
        if (scalePercent == 100)
            return source;

        var width = Math.Max(1, (int)Math.Round(source.Width * scalePercent / 100d, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(source.Height * scalePercent / 100d, MidpointRounding.AwayFromZero));
        var output = new PixelSize(width, height);
        FrameProjectArchiveWriter.ValidateCanvasSize(output);
        return output;
    }

    private static IReadOnlyList<ProjectFrameReference> ResampleFrames(
        IReadOnlyList<ProjectFrameReference> frames, int framesPerSecond)
    {
        var totalDuration = frames.Sum(frame => (long)frame.DurationMilliseconds);
        var outputFrameCount = Math.Max(1L,
            (long)Math.Round(totalDuration * framesPerSecond / 1000d, MidpointRounding.AwayFromZero));
        if (outputFrameCount > 100_000)
            throw new ArgumentOutOfRangeException(nameof(frames), "The selected GIF frame rate would produce more than 100,000 output frames.");

        var cumulativeDurations = new long[frames.Count];
        long cumulative = 0;
        for (var index = 0; index < frames.Count; index++)
        {
            cumulative += frames[index].DurationMilliseconds;
            cumulativeDurations[index] = cumulative;
        }

        var output = new ProjectFrameReference[(int)outputFrameCount];
        var previousCentisecondBoundary = 0L;
        var sourceFrameIndex = 0;
        for (var index = 0; index < output.Length; index++)
        {
            var centerTime = totalDuration * ((index + 0.5d) / output.Length);
            while (sourceFrameIndex < cumulativeDurations.Length - 1 &&
                   centerTime >= cumulativeDurations[sourceFrameIndex])
                sourceFrameIndex++;

            var nextCentisecondBoundary = (long)Math.Round(
                (index + 1d) * totalDuration / output.Length / 10d,
                MidpointRounding.AwayFromZero);
            var durationMilliseconds = Math.Max(10, (int)((nextCentisecondBoundary - previousCentisecondBoundary) * 10));
            previousCentisecondBoundary = nextCentisecondBoundary;
            output[index] = new ProjectFrameReference(frames[sourceFrameIndex].SourceFrameIndex, durationMilliseconds);
        }

        return output;
    }
}
