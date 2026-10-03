using FrameStudio.Core.Codification.Gif.Encoder;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;

namespace FrameStudio.Core.Export;

public sealed class GifExportService
{
    public async ValueTask ExportAsync(string projectPath, string destinationPath, GifExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        options ??= new GifExportOptions();
        if (options.MaximumColors is < 2 or > 256)
            throw new ArgumentOutOfRangeException(nameof(options), "GIF palette size must be between 2 and 256 colors.");
        if (options.RepeatCount is < -1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(options), "Repeat count must be -1 (no loop), 0 (forever), or at most 65,535.");

        var project = await FrameProjectArchiveReader.ReadProjectAsync(projectPath, cancellationToken).ConfigureAwait(false);
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
                    for (var index = 0; index < project.Frames.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var descriptor = project.Frames[index];
                        var pixels = await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, project, index, cancellationToken).ConfigureAwait(false);
                        var boundedDelay = Math.Clamp(descriptor.DurationMilliseconds, 10, 655_350);
                        encoder.AddFrame(pixels, new PixelRect(0, 0, project.CanvasSize.Width, project.CanvasSize.Height),
                            boundedDelay, index == project.Frames.Count - 1);
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
