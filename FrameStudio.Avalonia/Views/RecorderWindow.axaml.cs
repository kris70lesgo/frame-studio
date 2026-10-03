using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FrameStudio.Avalonia.ViewModels;

namespace FrameStudio.Avalonia.Views;

public partial class RecorderWindow : Window
{
    private RecordingViewModel _viewModel = null!;
    private bool _closeAfterSave;

    public RecorderWindow()
    {
        InitializeComponent();
    }

    public RecorderWindow(RecordingViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.RecordingCompleted += OnRecordingCompleted;
        Closing += OnClosing;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnRecordingCompleted(object? sender, string projectPath)
    {
        _closeAfterSave = true;
        Close(projectPath);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeAfterSave || _viewModel.HasFinished)
            return;

        e.Cancel = true;
        await _viewModel.StopRecordingCommand.ExecuteAsync(null);
    }
}
