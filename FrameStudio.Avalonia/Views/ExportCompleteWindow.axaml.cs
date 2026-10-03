using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace FrameStudio.Avalonia.Views;

public partial class ExportCompleteWindow : Window
{
    private string _gifPath = string.Empty;
    private readonly TextBlock _fileDetails;
    private readonly TextBlock _pathLabel;
    private readonly TextBlock _statusLabel;

    public ExportCompleteWindow()
    {
        InitializeComponent();

        _fileDetails = this.FindControl<TextBlock>("FileDetails")!;
        _pathLabel = this.FindControl<TextBlock>("PathLabel")!;
        _statusLabel = this.FindControl<TextBlock>("StatusLabel")!;
    }

    public ExportCompleteWindow(string gifPath) : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gifPath);
        _gifPath = Path.GetFullPath(gifPath);
        _pathLabel.Text = _gifPath;
        _fileDetails.Text = $"{Path.GetFileName(_gifPath)} · {FormatFileSize(new FileInfo(_gifPath).Length)}";
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OpenFile_OnClick(object? sender, RoutedEventArgs e) =>
        await RunActionAsync(() => OpenPathAsync(_gifPath), "GIF opened", "Could not open GIF");

    private async void OpenFolder_OnClick(object? sender, RoutedEventArgs e) =>
        await RunActionAsync(() => OpenPathAsync(Path.GetDirectoryName(_gifPath)!), "Folder opened", "Could not open folder");

    private async void CopyPath_OnClick(object? sender, RoutedEventArgs e) =>
        await RunActionAsync(CopyPathAsync, "Path copied to clipboard", "Could not copy path");

    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();

    private async Task CopyPathAsync()
    {
        var clipboard = Clipboard;
        if (clipboard is null)
            throw new InvalidOperationException("Clipboard access is unavailable in this session.");

        await clipboard.SetTextAsync(_gifPath);
    }

    private static Task OpenPathAsync(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException("The exported file or its folder could not be found.", path);

        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo(path) { UseShellExecute = true }
            : new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
        if (!OperatingSystem.IsWindows())
            startInfo.ArgumentList.Add(path);

        using var process = Process.Start(startInfo);
        return process is null
            ? Task.FromException(new InvalidOperationException("The operating system did not open the requested location."))
            : Task.CompletedTask;
    }

    private async Task RunActionAsync(Func<Task> action, string successMessage, string failurePrefix)
    {
        try
        {
            await action();
            _statusLabel.Text = successMessage;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"{failurePrefix}: {ex.Message}";
        }
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / (1024d * 1024d):0.##} MB"
    };
}
