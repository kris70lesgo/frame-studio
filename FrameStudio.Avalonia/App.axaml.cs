using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FrameStudio.Avalonia.Services;
using FrameStudio.Avalonia.ViewModels;
using FrameStudio.Avalonia.Views;
using FrameStudio.Platform.Windows;

namespace FrameStudio.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var projectPath = desktop.Args?.FirstOrDefault(argument =>
                string.Equals(Path.GetExtension(argument), ".fsp", StringComparison.OrdinalIgnoreCase) && File.Exists(argument));
            desktop.MainWindow = new MainWindow(projectPath)
            {
                DataContext = new MainViewModel(new ShellCapabilityProvider(),
                    OperatingSystem.IsWindows() ? new WindowsPlatformServices() : null),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
