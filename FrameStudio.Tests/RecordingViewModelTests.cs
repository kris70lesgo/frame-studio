using System.Runtime.CompilerServices;
using FrameStudio.Avalonia.ViewModels;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Tests;

public sealed class RecordingViewModelTests
{
    [Fact]
    public async Task RecordingViewModel_PauseResumeAndStopWritesCapturedFramesToProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-recording-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "recording.fsp");
        byte[] firstPixels = [24, 96, 180, 255];
        byte[] secondPixels = [240, 180, 36, 255];
        var session = new FakeRecordingSession(
            new CapturedFrame(new PixelSize(1, 1), firstPixels, 100, DateTimeOffset.UtcNow),
            new CapturedFrame(new PixelSize(1, 1), secondPixels, 200, DateTimeOffset.UtcNow.AddMilliseconds(100)));

        try
        {
            var writer = await FrameProjectArchiveWriter.CreateAsync(projectPath, "Recorded sample", new PixelSize(1, 1));
            var viewModel = new RecordingViewModel(session, writer, projectPath, framesPerSecond: 10);
            string? completedPath = null;
            viewModel.RecordingCompleted += (_, path) => completedPath = path;

            try
            {
                await viewModel.TogglePauseCommand.ExecuteAsync(null);
                Assert.True(viewModel.IsPaused);
                Assert.Equal(1, session.PauseCount);

                await viewModel.TogglePauseCommand.ExecuteAsync(null);
                Assert.False(viewModel.IsPaused);
                Assert.Equal(1, session.ResumeCount);

                Assert.False(File.Exists(projectPath));
                await viewModel.StopRecordingCommand.ExecuteAsync(null);

                Assert.True(viewModel.HasFinished);
                Assert.Equal("Recording saved", viewModel.Status);
                Assert.Equal(projectPath, completedPath);
                Assert.Equal(1, session.StopCount);
                Assert.True(session.IsDisposed);

                var project = await FrameProjectArchiveReader.ReadProjectAsync(projectPath);
                Assert.Equal("Recorded sample", project.Name);
                Assert.Equal(new PixelSize(1, 1), project.CanvasSize);
                Assert.Equal(new[] { 100, 200 }, project.Frames.Select(frame => frame.DurationMilliseconds));
                Assert.Equal(firstPixels, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, project, 0));
                Assert.Equal(secondPixels, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, project, 1));
            }
            finally
            {
                if (!viewModel.HasFinished)
                    await viewModel.StopRecordingCommand.ExecuteAsync(null);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FakeRecordingSession(params CapturedFrame[] frames) : IRecordingSession
    {
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int StopCount { get; private set; }
        public bool IsDisposed { get; private set; }

        public async IAsyncEnumerable<CapturedFrame> ReadFramesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return frame;
            }
        }

        public ValueTask PauseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PauseCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask ResumeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResumeCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
