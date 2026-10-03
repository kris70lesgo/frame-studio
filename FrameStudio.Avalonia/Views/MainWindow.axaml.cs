using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FrameStudio.Avalonia.ViewModels;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(string? projectPath)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(projectPath))
            Loaded += async (_, _) => await OpenEditorAsync(projectPath);
    }

    private async void ScreenCapture_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.ScreenCaptureService is not { } service)
            return;

        IRecordingSession? session = null;
        FrameProjectArchiveWriter? writer = null;
        try
        {
            var monitors = AddAvaloniaScreenScaling(await service.GetMonitorsAsync());
            var setup = await new CaptureSetupWindow(monitors).ShowDialog<CaptureSetupResult?>(this);
            if (setup is null)
                return;

            var projectFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save screen recording project",
                SuggestedFileName = $"Capture {DateTime.Now:yyyy-MM-dd HH-mm}.fsp",
                DefaultExtension = "fsp",
                ShowOverwritePrompt = true,
                FileTypeChoices = [new FilePickerFileType("Frame Studio project") { Patterns = ["*.fsp"] }]
            });
            var projectPath = projectFile?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(projectPath))
                return;

            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var canvasSize = new PixelSize(setup.Region.Width, setup.Region.Height);
            writer = await FrameProjectArchiveWriter.CreateAsync(projectPath, projectName, canvasSize);
            session = await service.StartAsync(new ScreenCaptureRequest(
                setup.Monitor.Id, setup.Region, setup.FramesPerSecond, setup.CaptureCursor));

            var recorderViewModel = new RecordingViewModel(session, writer, projectPath, setup.FramesPerSecond);
            session = null;
            writer = null;
            var completedPath = await new RecorderWindow(recorderViewModel).ShowDialog<string?>(this);
            if (!string.IsNullOrWhiteSpace(completedPath))
                await OpenEditorAsync(completedPath);
        }
        catch (Exception ex)
        {
            viewModel.ReportCaptureStatus($"Could not start or save the recording: {ex.Message}");
        }
        finally
        {
            if (session is not null)
                await session.DisposeAsync();
            if (writer is not null)
                await writer.DisposeAsync();
        }
    }

    private async void OpenProject_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        try
        {
            var projects = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Frame Studio project",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Frame Studio project") { Patterns = ["*.fsp"] }]
            });
            var projectPath = projects.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(projectPath))
                await OpenEditorAsync(projectPath);
        }
        catch (Exception ex)
        {
            viewModel.ReportCaptureStatus($"Could not open project: {ex.Message}");
        }
    }

    private async Task OpenEditorAsync(string projectPath)
    {
        var editor = await EditorWindow.CreateAsync(projectPath);
        editor.Show(this);
    }

    private IReadOnlyList<MonitorDescriptor> AddAvaloniaScreenScaling(IReadOnlyList<MonitorDescriptor> monitors)
    {
        // Avalonia screen bounds are device pixels; Scaling converts the selector's logical coordinates to pixels.
        var screens = Screens.All;
        var result = new List<MonitorDescriptor>(monitors.Count);
        foreach (var monitor in monitors)
        {
            var screen = screens.FirstOrDefault(candidate =>
                candidate.Bounds.X == monitor.Bounds.X &&
                candidate.Bounds.Y == monitor.Bounds.Y &&
                candidate.Bounds.Width == monitor.Bounds.Width &&
                candidate.Bounds.Height == monitor.Bounds.Height);
            if (screen is null)
                throw new InvalidOperationException($"Could not match Avalonia display information for {monitor.Name}.");

            result.Add(monitor with { ScaleFactor = screen.Scaling });
        }

        return result;
    }
}
