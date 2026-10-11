using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameStudio.Avalonia.Services;
using FrameStudio.Core.Projects;
using FrameStudio.Platform.Abstractions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FrameStudio.Avalonia.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private string _themeName = "Light appearance";

    [ObservableProperty]
    private int _defaultFramesPerSecond = FrameStudioPreferences.Default.DefaultFramesPerSecond;

    [ObservableProperty]
    private bool _captureCursor = FrameStudioPreferences.Default.CaptureCursor;

    public IReadOnlyList<CaptureCapability> CaptureCapabilities { get; }

    public IScreenCaptureService? ScreenCaptureService { get; }
    public IWindowCaptureService? WindowCaptureService { get; }
    public ICaptureWindowExclusionService? CaptureWindowExclusionService { get; }
    public bool IsScreenCaptureAvailable => ScreenCaptureService is not null && CaptureCapabilities.Any(capability =>
        capability.Source == CaptureSource.Screen && capability.Readiness == FeatureReadiness.Ready);
    public bool IsWindowCaptureAvailable => WindowCaptureService is not null && CaptureCapabilities.Any(capability =>
        capability.Source == CaptureSource.Window && capability.Readiness == FeatureReadiness.Ready);
    public ObservableCollection<RecentProjectViewModel> RecentProjects { get; } = [];
    public bool IsRecentProjectsEmpty => RecentProjects.Count == 0;
    public string RecentProjectsCountLabel => RecentProjects.Count == 1 ? "1 PROJECT" : $"{RecentProjects.Count} PROJECTS";

    private readonly RecentProjectCatalog _recentProjectCatalog;
    private readonly FrameStudioPreferencesStore _preferencesStore;
    private FrameStudioPreferences _preferences = FrameStudioPreferences.Default;

    public FrameStudioPreferences Preferences => _preferences with
    {
        IsDarkTheme = IsDarkTheme,
        DefaultFramesPerSecond = DefaultFramesPerSecond,
        CaptureCursor = CaptureCursor
    };

    [ObservableProperty]
    private string _captureStatus = string.Empty;

    public MainViewModel(IPlatformCapabilityProvider capabilityProvider, IScreenCaptureService? screenCaptureService = null,
        ICaptureWindowExclusionService? captureWindowExclusionService = null,
        FrameStudioPreferencesStore? preferencesStore = null,
        IWindowCaptureService? windowCaptureService = null)
    {
        ScreenCaptureService = screenCaptureService;
        WindowCaptureService = windowCaptureService;
        CaptureWindowExclusionService = captureWindowExclusionService;
        _preferencesStore = preferencesStore ?? FrameStudioPreferencesStore.ForCurrentUser();
        _recentProjectCatalog = new RecentProjectCatalog(
            Path.Combine(FrameStudioPreferencesStore.UserDataDirectoryPath, "recent-projects.json"));

        CaptureCapabilities = capabilityProvider.GetCaptureCapabilities();
        _captureStatus = CaptureCapabilities.FirstOrDefault()?.Detail
            ?? "No capture sources are configured in this build.";

        var preferences = _preferencesStore.Load();
        _preferences = preferences;
        DefaultFramesPerSecond = preferences.DefaultFramesPerSecond;
        CaptureCursor = preferences.CaptureCursor;
        ApplyTheme(preferences.IsDarkTheme);

        RecentProjects.CollectionChanged += RecentProjects_OnCollectionChanged;
    }

    public void ReportCaptureStatus(string status) => CaptureStatus = status;

    public async Task LoadRecentProjectsAsync(CancellationToken cancellationToken = default)
    {
        RecentProjects.Clear();
        IReadOnlyList<RecentProjectEntry> entries;
        try
        {
            entries = await _recentProjectCatalog.LoadAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ReportCaptureStatus($"Could not read recent projects: {ex.Message}");
            return;
        }

        foreach (var entry in entries)
        {
            try
            {
                var project = await FrameProjectArchiveReader.ReadProjectAsync(entry.Path, cancellationToken);
                RecentProjects.Add(RecentProjectViewModel.From(entry.Path, project, entry.LastOpenedUtc));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                                       ArgumentException or System.Text.Json.JsonException)
            {
                // A stale or damaged recent entry should not keep other projects from appearing.
            }
        }
    }

    public async Task AddRecentProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var project = await FrameProjectArchiveReader.ReadProjectAsync(projectPath, cancellationToken);
        var entries = await _recentProjectCatalog.AddOrUpdateAsync(projectPath, cancellationToken);
        RecentProjects.Clear();
        foreach (var entry in entries)
        {
            if (PathComparerForCurrentPlatform.Equals(entry.Path, Path.GetFullPath(projectPath)))
                RecentProjects.Add(RecentProjectViewModel.From(entry.Path, project, entry.LastOpenedUtc));
            else
            {
                try
                {
                    var recentProject = await FrameProjectArchiveReader.ReadProjectAsync(entry.Path, cancellationToken);
                    RecentProjects.Add(RecentProjectViewModel.From(entry.Path, recentProject, entry.LastOpenedUtc));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                                           ArgumentException or System.Text.Json.JsonException)
                {
                    // Keep the opened project usable even when an older recent file has since become invalid.
                }
            }
        }
    }

    private static StringComparer PathComparerForCurrentPlatform => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private void RecentProjects_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsRecentProjectsEmpty));
        OnPropertyChanged(nameof(RecentProjectsCountLabel));
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        ApplyPreferences(Preferences with { IsDarkTheme = !IsDarkTheme });
    }

    public void ApplyPreferences(FrameStudioPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var normalized = preferences.Normalize();
        _preferences = normalized;
        DefaultFramesPerSecond = normalized.DefaultFramesPerSecond;
        CaptureCursor = normalized.CaptureCursor;
        ApplyTheme(normalized.IsDarkTheme);

        try
        {
            _preferencesStore.Save(normalized);
            CaptureStatus = "Preferences saved on this device.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CaptureStatus = $"Preferences changed for this session but could not be saved: {exception.Message}";
        }
    }

    private void ApplyTheme(bool isDarkTheme)
    {
        IsDarkTheme = isDarkTheme;
        ThemeName = IsDarkTheme ? "Dark appearance" : "Light appearance";
        if (Application.Current is { } application)
            application.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
