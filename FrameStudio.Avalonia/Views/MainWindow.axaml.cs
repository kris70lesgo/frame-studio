using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FrameStudio.Avalonia.Services;
using FrameStudio.Avalonia.ViewModels;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType FrameProjectFileType = new("Frame Studio project")
    {
        Patterns = ["*.fsp"],
        AppleUniformTypeIdentifiers = ["public.data"]
    };

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(string? projectPath)
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
                await viewModel.LoadRecentProjectsAsync();
            if (!string.IsNullOrWhiteSpace(projectPath))
                await OpenEditorAsync(projectPath);
        };
    }

    private async void ScreenCapture_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.ScreenCaptureService is not { } service)
            return;

        try
        {
            var monitors = AddAvaloniaScreenScaling(await service.GetMonitorsAsync());
            var setup = await new CaptureSetupWindow(monitors, viewModel.DefaultFramesPerSecond, viewModel.CaptureCursor)
                .ShowDialog<CaptureSetupResult?>(this);
            if (setup is null)
                return;

            var projectFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save screen recording project",
                SuggestedFileName = $"Capture {DateTime.Now:yyyy-MM-dd HH-mm}.fsp",
                DefaultExtension = "fsp",
                ShowOverwritePrompt = true,
                FileTypeChoices = [FrameProjectFileType]
            });
            var projectPath = projectFile?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(projectPath))
                return;

            await StartRecordingAsync(viewModel, projectPath,
                new PixelSize(setup.Region.Width, setup.Region.Height), setup.FramesPerSecond,
                () => service.StartAsync(new ScreenCaptureRequest(
                    setup.Monitor.Id, setup.Region, setup.FramesPerSecond, setup.CaptureCursor)),
                excludeApplicationWindows: true);
        }
        catch (Exception ex)
        {
            viewModel.ReportCaptureStatus($"Could not start or save the recording: {ex.Message}");
        }
    }

    private async void WindowCapture_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.WindowCaptureService is not { } service)
            return;

        try
        {
            var windows = await service.GetWindowsAsync();
            if (windows.Count == 0)
            {
                viewModel.ReportCaptureStatus("No visible application windows are available to record.");
                return;
            }

            var setup = await new WindowCaptureSetupWindow(windows, viewModel.DefaultFramesPerSecond, viewModel.CaptureCursor)
                .ShowDialog<WindowCaptureSetupResult?>(this);
            if (setup is null)
                return;

            var projectFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save window recording project",
                SuggestedFileName = $"{SanitizeFileName(setup.Window.Title)} {DateTime.Now:yyyy-MM-dd HH-mm}.fsp",
                DefaultExtension = "fsp",
                ShowOverwritePrompt = true,
                FileTypeChoices = [FrameProjectFileType]
            });
            var projectPath = projectFile?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(projectPath))
                return;

            await StartRecordingAsync(viewModel, projectPath,
                new PixelSize(setup.Window.Bounds.Width, setup.Window.Bounds.Height), setup.FramesPerSecond,
                () => service.StartAsync(new WindowCaptureRequest(
                    setup.Window.Handle, setup.FramesPerSecond, setup.CaptureCursor)),
                excludeApplicationWindows: false);
        }
        catch (Exception ex)
        {
            viewModel.ReportCaptureStatus($"Could not start or save the window recording: {ex.Message}");
        }
    }

    private async Task StartRecordingAsync(MainViewModel viewModel, string projectPath, PixelSize canvasSize,
        int framesPerSecond, Func<ValueTask<IRecordingSession>> startSession, bool excludeApplicationWindows)
    {
        IRecordingSession? session = null;
        FrameProjectArchiveWriter? writer = null;
        IDisposable? captureWindowExclusion = null;
        RecorderWindow? recorderWindow = null;
        try
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            writer = await FrameProjectArchiveWriter.CreateAsync(projectPath, projectName, canvasSize);
            recorderWindow = new RecorderWindow();
            var recorderDialog = recorderWindow.ShowPreparingDialog(this);
            var recorderHandle = await recorderWindow.WaitForWindowHandleAsync();

            if (excludeApplicationWindows)
            {
                var exclusionService = viewModel.CaptureWindowExclusionService
                    ?? throw new InvalidOperationException("This platform cannot exclude app windows from screen capture.");
                var mainHandle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (mainHandle == IntPtr.Zero)
                    throw new InvalidOperationException("Avalonia did not provide the main window handle.");
                captureWindowExclusion = exclusionService.ExcludeFromCapture([mainHandle, recorderHandle]);
            }

            session = await startSession();
            var recorderViewModel = new RecordingViewModel(session, writer, projectPath, framesPerSecond);
            session = null;
            writer = null;
            recorderWindow.AttachViewModel(recorderViewModel);
            recorderWindow.ShowRecordingControls();
            var completedPath = await recorderDialog;

            captureWindowExclusion?.Dispose();
            captureWindowExclusion = null;
            if (!string.IsNullOrWhiteSpace(completedPath))
                await OpenEditorAsync(completedPath);
        }
        catch
        {
            if (recorderWindow is { IsVisible: true, HasViewModel: false })
                recorderWindow.Close(null);
            throw;
        }
        finally
        {
            if (captureWindowExclusion is not null)
            {
                try { captureWindowExclusion.Dispose(); }
                catch (Exception ex) { viewModel.ReportCaptureStatus($"Could not restore the app's capture visibility: {ex.Message}"); }
            }
            if (session is not null)
                await session.DisposeAsync();
            if (writer is not null)
                await writer.DisposeAsync();
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalidCharacters.Contains(character) ? ' ' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Window capture" : sanitized;
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
                FileTypeFilter = [FrameProjectFileType]
            });
            var projectPath = projects.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                if (!string.Equals(Path.GetExtension(projectPath), ".fsp", StringComparison.OrdinalIgnoreCase))
                {
                    viewModel.ReportCaptureStatus("Choose a Frame Studio project with the .fsp extension.");
                    return;
                }

                await OpenEditorAsync(projectPath);
            }
        }
        catch (Exception ex)
        {
            viewModel.ReportCaptureStatus($"Could not open project: {ex.Message}");
        }
    }

    private async void Preferences_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var preferences = await new PreferencesWindow(viewModel.Preferences).ShowDialog<FrameStudioPreferences?>(this);
        if (preferences is not null)
            viewModel.ApplyPreferences(preferences);
    }

    private async void OpenRecentProject_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: RecentProjectViewModel recentProject })
            return;

        try
        {
            await OpenEditorAsync(recentProject.Path);
        }
        catch (Exception ex)
        {
            (DataContext as MainViewModel)?.ReportCaptureStatus($"Could not open project: {ex.Message}");
        }
    }

    private async Task OpenEditorAsync(string projectPath)
    {
        var preferences = (DataContext as MainViewModel)?.Preferences ?? FrameStudioPreferences.Default;
        var editor = await EditorWindow.CreateAsync(projectPath, preferences);
        if (DataContext is MainViewModel viewModel)
        {
            try
            {
                await viewModel.AddRecentProjectAsync(projectPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                viewModel.ReportCaptureStatus($"Project opened, but recent history could not be saved: {ex.Message}");
            }
        }
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
