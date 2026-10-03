using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FrameStudio.Avalonia.Services;
using FrameStudio.Avalonia.ViewModels;
using FrameStudio.Avalonia.Views;

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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(new ShellCapabilityProvider()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
