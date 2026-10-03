using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private string _themeName = "Dark appearance";

    public IReadOnlyList<CaptureCapability> CaptureCapabilities { get; }

    public IScreenCaptureService? ScreenCaptureService { get; }
    public bool IsScreenCaptureAvailable => ScreenCaptureService is not null;

    [ObservableProperty]
    private string _captureStatus = string.Empty;

    public MainViewModel(IPlatformCapabilityProvider capabilityProvider, IScreenCaptureService? screenCaptureService = null)
    {
        ScreenCaptureService = screenCaptureService;
        CaptureCapabilities = capabilityProvider.GetCaptureCapabilities();
        _captureStatus = CaptureCapabilities.FirstOrDefault()?.Detail
            ?? "No capture sources are configured in this build.";
    }

    public void ReportCaptureStatus(string status) => CaptureStatus = status;

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ThemeName = IsDarkTheme ? "Dark appearance" : "Light appearance";

        if (Application.Current is { } application)
            application.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
