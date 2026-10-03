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

    [Fact]
    public async Task RecordingViewModel_RecordingCanBeEditedSavedAndExportedAsGif()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-record-edit-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "recording.fsp");
        var gifPath = Path.Combine(directory, "recording.gif");
        var size = new PixelSize(1, 1);
        byte[] red = [255, 0, 0, 255];
        byte[] green = [0, 255, 0, 255];
        byte[] blue = [0, 0, 255, 255];
        var session = new FakeRecordingSession(
            new CapturedFrame(size, red, 40, DateTimeOffset.UtcNow),
            new CapturedFrame(size, green, 50, DateTimeOffset.UtcNow.AddMilliseconds(40)),
            new CapturedFrame(size, blue, 60, DateTimeOffset.UtcNow.AddMilliseconds(90)));

        try
        {
            var writer = await FrameProjectArchiveWriter.CreateAsync(projectPath, "Record edit export", size);
            var recording = new RecordingViewModel(session, writer, projectPath, framesPerSecond: 20);
            try
            {
                string? completedPath = null;
                recording.RecordingCompleted += (_, path) => completedPath = path;
                await recording.StopRecordingCommand.ExecuteAsync(null);

                Assert.True(recording.HasFinished);
                Assert.Equal("Recording saved", recording.Status);
                Assert.Equal(projectPath, completedPath);
                Assert.Equal(3, (await FrameProjectArchiveReader.ReadProjectAsync(projectPath)).Frames.Count);

                var recordedProject = await FrameProjectArchiveReader.ReadProjectAsync(projectPath);
                var editor = new EditorViewModel(projectPath, recordedProject);
                editor.MoveSelectedFrameLaterCommand.Execute(null);
                editor.SelectedFrame = editor.Frames[0];
                editor.DurationText = "70";
                editor.ApplyDurationCommand.Execute(null);
                Assert.Equal(new[] { 1, 0, 2 }, editor.Frames.Select(frame => frame.SourceFrameIndex));
                Assert.Equal(new[] { 70, 40, 60 }, editor.Frames.Select(frame => frame.DurationMilliseconds));

                await editor.SaveProjectCommand.ExecuteAsync(null);
                Assert.Equal("Project saved", editor.Status);
                var editedProject = await FrameProjectArchiveReader.ReadProjectAsync(projectPath);
                Assert.Equal(new[] { 70, 40, 60 }, editedProject.Frames.Select(frame => frame.DurationMilliseconds));
                Assert.Equal(green, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, editedProject, 0));
                Assert.Equal(red, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, editedProject, 1));
                Assert.Equal(blue, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, editedProject, 2));

                await editor.ExportGifAsync(gifPath);
                Assert.Equal("GIF exported", editor.Status);
                var gif = await File.ReadAllBytesAsync(gifPath);
                Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
                var metadata = FrameProjectTests.ReadGifMetadata(gif);
                Assert.Equal(size, metadata.CanvasSize);
                Assert.Equal(new[] { 70, 40, 60 }, metadata.FrameDurationsMilliseconds);
                Assert.Equal(0, metadata.RepeatCount);
                var decodedFrames = GifLzwRoundTripTests.DecodeFrameRgba(gif);
                Assert.Equal(3, decodedFrames.Count);
                Assert.Equal(green, decodedFrames[0]);
                Assert.Equal(red, decodedFrames[1]);
                Assert.Equal(blue, decodedFrames[2]);
            }
            finally
            {
                if (!recording.HasFinished)
                    await recording.StopRecordingCommand.ExecuteAsync(null);
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
