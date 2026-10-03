using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FrameStudio.Avalonia.ViewModels;

namespace FrameStudio.Avalonia.Views;

public partial class RecorderWindow : Window
{
    private readonly TaskCompletionSource<nint> _windowHandleReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RecordingViewModel? _viewModel;
    private bool _closeAfterSave;

    public RecorderWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
    }

    public Task<string?> ShowPreparingDialog(Window owner)
    {
        Opacity = 0;
        return ShowDialog<string?>(owner);
    }

    public Task<nint> WaitForWindowHandleAsync() => _windowHandleReady.Task;

    public bool HasViewModel => _viewModel is not null;

    public void AttachViewModel(RecordingViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.RecordingCompleted += OnRecordingCompleted;
    }

    public void ShowRecordingControls() => Opacity = 1;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("Avalonia did not provide a native recorder window handle.");
            _windowHandleReady.TrySetResult(handle);
        }
        catch (Exception ex)
        {
            _windowHandleReady.TrySetException(ex);
        }
    }

    private void OnRecordingCompleted(object? sender, string projectPath)
    {
        _closeAfterSave = true;
        Close(projectPath);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeAfterSave || _viewModel is null || _viewModel.HasFinished)
            return;

        e.Cancel = true;
        await _viewModel.StopRecordingCommand.ExecuteAsync(null);
    }
}
