using FrameStudio.Avalonia.ViewModels;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;

namespace FrameStudio.Tests;

public sealed class EditorViewModelTests
{
    [Fact]
    public async Task EditorViewModel_EditsFramesSavesProjectAndExportsGif()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "editor-flow.fsp");
        var gifPath = Path.Combine(directory, "editor-flow.gif");
        byte[] red = [255, 0, 0, 255];
        byte[] green = [0, 255, 0, 255];
        byte[] blue = [0, 0, 255, 255];

        try
        {
            await using (var writer = await FrameProjectArchiveWriter.CreateAsync(projectPath, "Editor flow", new PixelSize(1, 1)))
            {
                await writer.WriteFrameAsync(new PixelSize(1, 1), red, 40);
                await writer.WriteFrameAsync(new PixelSize(1, 1), green, 50);
                await writer.WriteFrameAsync(new PixelSize(1, 1), blue, 60);
                await writer.CompleteAsync();
            }

            var sourceProject = await FrameProjectArchiveReader.ReadProjectAsync(projectPath);
            var editor = new EditorViewModel(projectPath, sourceProject);

            editor.SelectedFrame = editor.Frames[1];
            editor.MoveSelectedFrameLaterCommand.Execute(null);
            Assert.Equal(new[] { 0, 2, 1 }, editor.Frames.Select(frame => frame.SourceFrameIndex));

            editor.DuplicateSelectedFrameCommand.Execute(null);
            editor.DurationText = "90";
            editor.ApplyDurationCommand.Execute(null);
            editor.SelectedFrame = editor.Frames[1];
            editor.DeleteSelectedFrameCommand.Execute(null);

            Assert.Equal(new[] { 0, 1, 1 }, editor.Frames.Select(frame => frame.SourceFrameIndex));
            Assert.Equal(new[] { 40, 50, 90 }, editor.Frames.Select(frame => frame.DurationMilliseconds));
            Assert.Equal(green, await editor.ReadSelectedFrameAsync());

            await editor.SaveProjectCommand.ExecuteAsync(null);
            Assert.False(editor.IsDirty);
            Assert.Equal("Project saved", editor.Status);

            var savedProject = await FrameProjectArchiveReader.ReadProjectAsync(projectPath);
            Assert.Equal(new[] { 40, 50, 90 }, savedProject.Frames.Select(frame => frame.DurationMilliseconds));
            Assert.Equal(red, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, savedProject, 0));
            Assert.Equal(green, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, savedProject, 1));
            Assert.Equal(green, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, savedProject, 2));

            await editor.ExportGifAsync(gifPath);
            Assert.Equal("GIF exported", editor.Status);
            var gif = await File.ReadAllBytesAsync(gifPath);
            Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
            Assert.Equal(0x3b, gif[^1]);
            var metadata = FrameProjectTests.ReadGifMetadata(gif);
            Assert.Equal(new PixelSize(1, 1), metadata.CanvasSize);
            Assert.Equal(new[] { 40, 50, 90 }, metadata.FrameDurationsMilliseconds);
            Assert.Equal(0, metadata.RepeatCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
