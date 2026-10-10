using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;
using SkiaSharp;

namespace FrameStudio.Core.Export;

/// <summary>Exports a frame project to H.264 MP4 using an FFmpeg executable available on the local machine.</summary>
public sealed class FfmpegMp4ExportService
{
    private readonly string? _ffmpegExecutablePath;

    public FfmpegMp4ExportService(string? ffmpegExecutablePath = null) => _ffmpegExecutablePath = ffmpegExecutablePath;

    public ValueTask ExportAsync(string projectPath, string destinationPath, CancellationToken cancellationToken = default) =>
        ExportCoreAsync(projectPath, destinationPath, null, cancellationToken);

    public ValueTask ExportSelectionAsync(string projectPath, string destinationPath,
        IReadOnlyList<ProjectFrameReference> frames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        return ExportCoreAsync(projectPath, destinationPath, frames, cancellationToken);
    }

    public static string? FfmpegExecutablePath => FindExecutable("ffmpeg");
    public static string? FfprobeExecutablePath => FindExecutable("ffprobe");
    public static bool IsFfmpegAvailable => FfmpegExecutablePath is not null;
    public static bool IsFfprobeAvailable => FfprobeExecutablePath is not null;

    private async ValueTask ExportCoreAsync(string projectPath, string destinationPath,
        IReadOnlyList<ProjectFrameReference>? frameSelection, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var ffmpegPath = ResolveFfmpegPath();
        await using var reader = await FrameProjectArchiveReader.OpenAsync(projectPath, cancellationToken).ConfigureAwait(false);
        var project = reader.Project;
        var frames = frameSelection ?? project.Frames
            .Select(frame => new ProjectFrameReference(frame.Index, frame.DurationMilliseconds)).ToArray();
        if (frames.Count is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(frameSelection), "MP4 export requires between 1 and 100,000 frames.");
        foreach (var frame in frames)
        {
            if ((uint)frame.SourceFrameIndex >= (uint)project.Frames.Count || frame.DurationMilliseconds <= 0)
                throw new ArgumentException("The MP4 frame selection contains an invalid source index or duration.", nameof(frameSelection));
        }

        var fullDestination = Path.GetFullPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(fullDestination)!;
        Directory.CreateDirectory(destinationDirectory);
        var partialPath = Path.Combine(destinationDirectory,
            $".{Path.GetFileNameWithoutExtension(fullDestination)}.{Guid.NewGuid():N}.partial.mp4");
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"frame-studio-mp4-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var manifestPath = Path.Combine(workingDirectory, "frames.ffconcat");
            await WriteFramesAndManifestAsync(reader, frames, workingDirectory, manifestPath, cancellationToken)
                .ConfigureAwait(false);

            await RunFfmpegAsync(ffmpegPath, workingDirectory, Path.GetFileName(manifestPath), partialPath, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialPath, fullDestination, overwrite: true);
        }
        catch
        {
            TryDeleteFile(partialPath);
            throw;
        }
        finally
        {
            TryDeleteDirectory(workingDirectory);
        }
    }

    private static async Task WriteFramesAndManifestAsync(FrameProjectArchiveReadSession reader,
        IReadOnlyList<ProjectFrameReference> frames, string workingDirectory, string manifestPath,
        CancellationToken cancellationToken)
    {
        var project = reader.Project;
        await using var manifest = new StreamWriter(manifestPath, append: false, Encoding.ASCII);
        await manifest.WriteLineAsync("ffconcat version 1.0".AsMemory(), cancellationToken).ConfigureAwait(false);
        var terminalSampleNeeded = frames[^1].DurationMilliseconds > 1;

        for (var index = 0; index < frames.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var imageName = $"frame-{index:D8}.png";
            var pixels = await reader.ReadFrameRgbaAsync(frames[index].SourceFrameIndex, cancellationToken).ConfigureAwait(false);
            await WritePngAsync(Path.Combine(workingDirectory, imageName), project.CanvasSize, pixels, cancellationToken)
                .ConfigureAwait(false);

            await manifest.WriteLineAsync($"file {imageName}".AsMemory(), cancellationToken).ConfigureAwait(false);
            await manifest.WriteLineAsync("option framerate 1000".AsMemory(), cancellationToken).ConfigureAwait(false);
            var durationMilliseconds = frames[index].DurationMilliseconds;
            if (index == frames.Count - 1 && terminalSampleNeeded)
                durationMilliseconds--;
            var durationSeconds = (durationMilliseconds / 1000d).ToString("0.000", CultureInfo.InvariantCulture);
            await manifest.WriteLineAsync($"duration {durationSeconds}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        // A 1 ms repeated sample supplies the last timestamp; the same pixels keep the visible duration exact.
        if (terminalSampleNeeded)
        {
            await manifest.WriteLineAsync($"file frame-{frames.Count - 1:D8}.png".AsMemory(), cancellationToken).ConfigureAwait(false);
            await manifest.WriteLineAsync("option framerate 1000".AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        await manifest.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WritePngAsync(string path, PixelSize size, byte[] rgbaPixels, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var imageInfo = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(imageInfo);
        Marshal.Copy(rgbaPixels, 0, bitmap.GetPixels(), rgbaPixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Could not encode an MP4 input frame as PNG.");
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
        encoded.SaveTo(output);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunFfmpegAsync(string ffmpegPath, string workingDirectory, string manifestName,
        string outputPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-f", "concat", "-safe", "0", "-i", manifestName,
            "-fps_mode", "vfr",
            "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2:0:0:black",
            "-c:v", "libx264", "-preset", "fast", "-crf", "23", "-pix_fmt", "yuv420p",
            "-movflags", "+faststart", "-f", "mp4", outputPath
        })
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("FFmpeg could not be started.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException("Could not start FFmpeg. Install FFmpeg with H.264 (libx264) support and make it available on PATH.", ex);
        }

        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(error) ? "FFmpeg returned no error details." : error.Trim();
            if (detail.Length > 1200)
                detail = detail[^1200..];
            throw new InvalidOperationException($"FFmpeg could not create the MP4. Confirm that this FFmpeg build includes the libx264 encoder. {detail}");
        }

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            throw new InvalidDataException("FFmpeg reported success but did not produce an MP4 file.");
    }

    private string ResolveFfmpegPath()
    {
        if (!string.IsNullOrWhiteSpace(_ffmpegExecutablePath))
        {
            var explicitPath = Path.GetFullPath(_ffmpegExecutablePath);
            if (File.Exists(explicitPath))
                return explicitPath;
            throw new FileNotFoundException("The configured FFmpeg executable was not found.", explicitPath);
        }

        return FfmpegExecutablePath ?? throw new FileNotFoundException(
            "FFmpeg was not found on PATH. Install FFmpeg with H.264 (libx264) support to export MP4.");
    }

    private static string? FindExecutable(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        var directories = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                directories.Add(directory);
        }

        // GUI apps on macOS may not inherit a shell PATH; include both standard Homebrew prefixes.
        if (OperatingSystem.IsMacOS())
        {
            directories.Add("/opt/homebrew/bin");
            directories.Add("/usr/local/bin");
        }

        if (directories.Count == 0)
            return null;

        var pathExtensions = Environment.GetEnvironmentVariable("PATHEXT");
        var extensions = OperatingSystem.IsWindows()
            ? (string.IsNullOrWhiteSpace(pathExtensions) ? ".EXE;.CMD;.BAT;.COM" : pathExtensions)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];
        foreach (var directory in directories)
        foreach (var extension in extensions)
        {
            var candidate = Path.Combine(directory, Path.HasExtension(executableName) ? executableName : executableName + extension);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
