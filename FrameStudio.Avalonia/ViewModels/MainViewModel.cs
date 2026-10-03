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

    public string CaptureStatus { get; }

    public MainViewModel(IPlatformCapabilityProvider capabilityProvider)
    {
        CaptureCapabilities = capabilityProvider.GetCaptureCapabilities();
        CaptureStatus = CaptureCapabilities.FirstOrDefault()?.Detail
            ?? "No capture sources are configured in this build.";
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
