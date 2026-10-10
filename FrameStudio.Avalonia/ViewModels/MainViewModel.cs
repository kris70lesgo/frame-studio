using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public IReadOnlyList<CaptureCapability> CaptureCapabilities { get; }

    public IScreenCaptureService? ScreenCaptureService { get; }
    public ICaptureWindowExclusionService? CaptureWindowExclusionService { get; }
    public bool IsScreenCaptureAvailable => ScreenCaptureService is not null && CaptureCapabilities.Any(capability =>
        capability.Source == CaptureSource.Screen && capability.Readiness == FeatureReadiness.Ready);
    public ObservableCollection<RecentProjectViewModel> RecentProjects { get; } = [];
    public bool IsRecentProjectsEmpty => RecentProjects.Count == 0;
    public string RecentProjectsCountLabel => RecentProjects.Count == 1 ? "1 PROJECT" : $"{RecentProjects.Count} PROJECTS";

    private readonly RecentProjectCatalog _recentProjectCatalog;

    [ObservableProperty]
    private string _captureStatus = string.Empty;

    public MainViewModel(IPlatformCapabilityProvider capabilityProvider, IScreenCaptureService? screenCaptureService = null,
        ICaptureWindowExclusionService? captureWindowExclusionService = null)
    {
        ScreenCaptureService = screenCaptureService;
        CaptureWindowExclusionService = captureWindowExclusionService;
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        _recentProjectCatalog = new RecentProjectCatalog(Path.Combine(localData, "FrameStudio", "recent-projects.json"));

        CaptureCapabilities = capabilityProvider.GetCaptureCapabilities();
        _captureStatus = CaptureCapabilities.FirstOrDefault()?.Detail
            ?? "No capture sources are configured in this build.";

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
        IsDarkTheme = !IsDarkTheme;
        ThemeName = IsDarkTheme ? "Dark appearance" : "Light appearance";

        if (Application.Current is { } application)
            application.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
