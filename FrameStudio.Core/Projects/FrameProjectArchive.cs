using System.IO.Compression;
using System.Text.Json;
using FrameStudio.Core.Models;

namespace FrameStudio.Core.Projects;

/// <summary>Stores a recording as a portable archive with one independently compressed RGBA entry per frame.</summary>
public sealed class FrameProjectArchiveWriter : IAsyncDisposable
{
    private const string ManifestName = "project.json";
    private readonly string _destinationPath;
    private readonly string _temporaryPath;
    private readonly PixelSize _canvasSize;
    private readonly string _name;
    private readonly FileStream _fileStream;
    private readonly ZipArchive _archive;
    private readonly List<FrameDescriptor> _frames = [];
    private bool _completed;
    private bool _disposed;

    private FrameProjectArchiveWriter(string destinationPath, string temporaryPath, string name, PixelSize canvasSize,
        FileStream fileStream, ZipArchive archive)
    {
        _destinationPath = destinationPath;
        _temporaryPath = temporaryPath;
        _name = name;
        _canvasSize = canvasSize;
        _fileStream = fileStream;
        _archive = archive;
    }

    public static ValueTask<FrameProjectArchiveWriter> CreateAsync(string destinationPath, string name, PixelSize canvasSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateCanvasSize(canvasSize);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.partial");
        var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024, useAsync: true);
        var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        return ValueTask.FromResult(new FrameProjectArchiveWriter(fullPath, temporaryPath, name, canvasSize, stream, archive));
    }

    public async ValueTask WriteFrameAsync(PixelSize frameSize, ReadOnlyMemory<byte> rgbaPixels,
        int durationMilliseconds, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed)
            throw new InvalidOperationException("The project has already been completed.");

        var expectedLength = checked(_canvasSize.Width * _canvasSize.Height * 4);
        if (frameSize != _canvasSize || rgbaPixels.Length != expectedLength)
            throw new ArgumentException("Captured frame size and RGBA buffer must match the project canvas.", nameof(rgbaPixels));
        if (durationMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMilliseconds), "Frame duration must be positive.");
        if (_frames.Count >= 100_000)
            throw new InvalidOperationException("This project exceeds the 100,000 frame limit.");

        var index = _frames.Count;
        var entryName = GetFrameEntryName(index);
        var entry = _archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using (var output = entry.Open())
            await output.WriteAsync(rgbaPixels, cancellationToken).ConfigureAwait(false);

        _frames.Add(new FrameDescriptor(index, durationMilliseconds,
            new PixelRect(0, 0, _canvasSize.Width, _canvasSize.Height), entryName, expectedLength));
    }

    public async ValueTask<FrameProject> CompleteAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed)
            throw new InvalidOperationException("The project has already been completed.");
        if (_frames.Count == 0)
            throw new InvalidOperationException("A project needs at least one captured frame.");

        cancellationToken.ThrowIfCancellationRequested();
        var project = new FrameProject(_name, _canvasSize, _frames.ToArray(), DateTimeOffset.UtcNow);
        var manifest = _archive.CreateEntry(ManifestName, CompressionLevel.Fastest);
        await using (var output = manifest.Open())
            await JsonSerializer.SerializeAsync(output, project, cancellationToken: cancellationToken).ConfigureAwait(false);

        _archive.Dispose();
        await _fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await _fileStream.DisposeAsync().ConfigureAwait(false);
        File.Move(_temporaryPath, _destinationPath, overwrite: true);
        _completed = true;
        return project;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (!_completed)
        {
            _archive.Dispose();
            await _fileStream.DisposeAsync().ConfigureAwait(false);
            try { File.Delete(_temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static string GetFrameEntryName(int index) => $"frames/{index:D8}.rgba";

    internal static void ValidateCanvasSize(PixelSize size)
    {
        if (size.Width <= 0 || size.Height <= 0 || size.Width > 16_384 || size.Height > 16_384 || (long)size.Width * size.Height > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(size), "Canvas must fit within 16,384 pixels per side and 16 megapixels total.");
    }
}

public static class FrameProjectArchiveReader
{
    private const string ManifestName = "project.json";

    public static async ValueTask<FrameProject> ReadProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        await using var session = await OpenAsync(projectPath, cancellationToken).ConfigureAwait(false);
        return session.Project;
    }

    public static async ValueTask<FrameProjectArchiveReadSession> OpenAsync(string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        cancellationToken.ThrowIfCancellationRequested();
        var stream = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 64 * 1024, useAsync: true);
        ZipArchive? archive = null;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var project = await ReadProjectManifestAsync(archive, cancellationToken).ConfigureAwait(false);
            ValidateProject(project);
            return new FrameProjectArchiveReadSession(stream, archive, project);
        }
        catch
        {
            try
            {
                archive?.Dispose();
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    internal static async ValueTask<FrameProjectArchiveReadSession> OpenAsync(string projectPath, FrameProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentNullException.ThrowIfNull(project);
        ValidateProject(project);
        cancellationToken.ThrowIfCancellationRequested();
        var stream = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 64 * 1024, useAsync: true);
        ZipArchive? archive = null;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            return new FrameProjectArchiveReadSession(stream, archive, project);
        }
        catch
        {
            try
            {
                archive?.Dispose();
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    public static async ValueTask<byte[]> ReadFrameRgbaAsync(string projectPath, FrameProject project, int index,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        await using var session = await OpenAsync(projectPath, project, cancellationToken).ConfigureAwait(false);
        return await session.ReadFrameRgbaAsync(index, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<FrameProject> ReadProjectManifestAsync(ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(ManifestName) ?? throw new InvalidDataException("Project manifest is missing.");
        if (entry.Length > 32 * 1024 * 1024)
            throw new InvalidDataException("Project manifest exceeds the supported size.");

        await using var manifestStream = entry.Open();
        var project = await JsonSerializer.DeserializeAsync<FrameProject>(manifestStream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Project manifest is invalid.");

        return project;
    }

    private static void ValidateProject(FrameProject project)
    {
        FrameProjectArchiveWriter.ValidateCanvasSize(project.CanvasSize);
        if (string.IsNullOrWhiteSpace(project.Name) || project.Frames.Count is < 1 or > 100_000)
            throw new InvalidDataException("Project metadata is outside the supported limits.");

        for (var index = 0; index < project.Frames.Count; index++)
        {
            var frame = project.Frames[index];
            if (frame.Index != index || frame.DurationMilliseconds <= 0 || frame.DataLength != (long)project.CanvasSize.Width * project.CanvasSize.Height * 4)
                throw new InvalidDataException($"Frame {index} has invalid metadata.");
        }

    }
}

/// <summary>Reads frames serially from one open .fsp archive without reopening its ZIP directory for each frame.</summary>
public sealed class FrameProjectArchiveReadSession : IAsyncDisposable
{
    private readonly FileStream _stream;
    private readonly ZipArchive _archive;
    private int _disposed;

    internal FrameProjectArchiveReadSession(FileStream stream, ZipArchive archive, FrameProject project)
    {
        _stream = stream;
        _archive = archive;
        Project = project;
    }

    public FrameProject Project { get; }

    public async ValueTask<byte[]> ReadFrameRgbaAsync(int index, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if ((uint)index >= (uint)Project.Frames.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var frame = Project.Frames[index];
        var expectedLength = checked(Project.CanvasSize.Width * Project.CanvasSize.Height * 4);
        var entry = _archive.GetEntry(FrameProjectArchiveWriter.GetFrameEntryName(index))
            ?? throw new InvalidDataException($"Frame data for frame {index} is missing.");
        if (entry.Length != expectedLength || frame.DataLength != expectedLength)
            throw new InvalidDataException($"Frame data for frame {index} has an invalid size.");

        var pixels = new byte[expectedLength];
        await using var input = entry.Open();
        await input.ReadExactlyAsync(pixels, cancellationToken).ConfigureAwait(false);
        return pixels;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _archive.Dispose();
        }
        finally
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public static class FrameProjectArchiveEditor
{
    public static async ValueTask<FrameProject> RewriteAsync(string sourcePath, string destinationPath, FrameProject project,
        IReadOnlyList<ProjectFrameReference> frames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(frames), "An edited project must contain between 1 and 100,000 frames.");

        await using var reader = await FrameProjectArchiveReader.OpenAsync(sourcePath, project, cancellationToken).ConfigureAwait(false);
        await using var writer = await FrameProjectArchiveWriter.CreateAsync(destinationPath, project.Name, project.CanvasSize, cancellationToken)
            .ConfigureAwait(false);
        foreach (var frame in frames)
        {
            if ((uint)frame.SourceFrameIndex >= (uint)project.Frames.Count)
                throw new ArgumentOutOfRangeException(nameof(frames), "An edited frame refers to a missing source frame.");
            if (frame.DurationMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(frames), "Edited frame durations must be positive.");

            var pixels = await reader.ReadFrameRgbaAsync(frame.SourceFrameIndex, cancellationToken).ConfigureAwait(false);
            await writer.WriteFrameAsync(project.CanvasSize, pixels, frame.DurationMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        return await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public static ValueTask<FrameProject> CropAsync(string sourcePath, string destinationPath, FrameProject project,
        IReadOnlyList<ProjectFrameReference> frames, PixelRect crop, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (crop.Width <= 0 || crop.Height <= 0 || crop.X < 0 || crop.Y < 0 ||
            (long)crop.X + crop.Width > project.CanvasSize.Width || (long)crop.Y + crop.Height > project.CanvasSize.Height)
            throw new ArgumentOutOfRangeException(nameof(crop), "Crop bounds must fit inside the project canvas.");

        var outputSize = new PixelSize(crop.Width, crop.Height);
        return TransformAsync(sourcePath, destinationPath, project, frames, outputSize,
            pixels => RgbaFrameTransform.Crop(pixels, project.CanvasSize, crop), cancellationToken);
    }

    public static ValueTask<FrameProject> ResizeAsync(string sourcePath, string destinationPath, FrameProject project,
        IReadOnlyList<ProjectFrameReference> frames, PixelSize targetSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        FrameProjectArchiveWriter.ValidateCanvasSize(targetSize);
        return TransformAsync(sourcePath, destinationPath, project, frames, targetSize,
            pixels => RgbaFrameTransform.ResizeNearestNeighbor(pixels, project.CanvasSize, targetSize), cancellationToken);
    }

    public static ValueTask<FrameProject> AddTextOverlayAsync(string sourcePath, string destinationPath, FrameProject project,
        IReadOnlyList<ProjectFrameReference> frames, TextOverlayOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        RgbaFrameTransform.ValidateTextOverlay(project.CanvasSize, options);
        return TransformAsync(sourcePath, destinationPath, project, frames, project.CanvasSize,
            pixels => RgbaFrameTransform.DrawText(pixels, project.CanvasSize, options), cancellationToken);
    }

    public static ValueTask<FrameProject> AddStrokeOverlayAsync(string sourcePath, string destinationPath, FrameProject project,
        IReadOnlyList<ProjectFrameReference> frames, StrokeOverlayOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        RgbaFrameTransform.ValidateStrokeOverlay(project.CanvasSize, options);
        return TransformAsync(sourcePath, destinationPath, project, frames, project.CanvasSize,
            pixels => RgbaFrameTransform.DrawStroke(pixels, project.CanvasSize, options), cancellationToken);
    }

    private static async ValueTask<FrameProject> TransformAsync(string sourcePath, string destinationPath, FrameProject project,
        IReadOnlyList<ProjectFrameReference> frames, PixelSize outputSize, Func<byte[], byte[]> transform,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(frames), "An edited project must contain between 1 and 100,000 frames.");

        await using var reader = await FrameProjectArchiveReader.OpenAsync(sourcePath, project, cancellationToken).ConfigureAwait(false);
        await using var writer = await FrameProjectArchiveWriter.CreateAsync(destinationPath, project.Name, outputSize, cancellationToken)
            .ConfigureAwait(false);
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((uint)frame.SourceFrameIndex >= (uint)project.Frames.Count)
                throw new ArgumentOutOfRangeException(nameof(frames), "An edited frame refers to a missing source frame.");
            if (frame.DurationMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(frames), "Edited frame durations must be positive.");

            var pixels = await reader.ReadFrameRgbaAsync(frame.SourceFrameIndex, cancellationToken).ConfigureAwait(false);
            var transformed = transform(pixels);
            await writer.WriteFrameAsync(outputSize, transformed, frame.DurationMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        return await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
