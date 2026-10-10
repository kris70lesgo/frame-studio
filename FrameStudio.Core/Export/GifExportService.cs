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
        options ??= new GifExportOptions();
        if (options.MaximumColors is < 2 or > 256)
            throw new ArgumentOutOfRangeException(nameof(options), "GIF palette size must be between 2 and 256 colors.");
        if (options.RepeatCount is < -1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(options), "Repeat count must be -1 (no loop), 0 (forever), or at most 65,535.");

        await using var reader = await FrameProjectArchiveReader.OpenAsync(projectPath, cancellationToken).ConfigureAwait(false);
        var project = reader.Project;
        var frames = frameSelection ?? project.Frames
            .Select(frame => new ProjectFrameReference(frame.Index, frame.DurationMilliseconds)).ToArray();
        if (frames.Count is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(frameSelection), "GIF export requires between 1 and 100,000 frames.");
        foreach (var frame in frames)
        {
            if ((uint)frame.SourceFrameIndex >= (uint)project.Frames.Count || frame.DurationMilliseconds <= 0)
                throw new ArgumentException("The GIF frame selection contains an invalid source index or duration.", nameof(frameSelection));
        }

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
                    QuantizationType = options.Quantization
                };

                try
                {
                    for (var index = 0; index < frames.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var frame = frames[index];
                        var pixels = await reader.ReadFrameRgbaAsync(frame.SourceFrameIndex, cancellationToken).ConfigureAwait(false);
                        var boundedDelay = Math.Clamp(frame.DurationMilliseconds, 10, 655_350);
                        encoder.AddFrame(pixels, new PixelRect(0, 0, project.CanvasSize.Width, project.CanvasSize.Height),
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
}
