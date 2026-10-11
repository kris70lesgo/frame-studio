using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;
using FrameStudio.Avalonia.ViewModels;
using System.Runtime.InteropServices;
using AvaloniaPixelSize = Avalonia.PixelSize;
using CorePixelSize = FrameStudio.Core.Models.PixelSize;

namespace FrameStudio.Avalonia.Views;

public partial class RecorderWindow : Window
{
    private readonly TaskCompletionSource<nint> _windowHandleReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TextBlock _recordingSourceLabel;
    private readonly Border _webcamPreviewBorder;
    private readonly Image _webcamPreview;
    private RecordingViewModel? _viewModel;
    private IWebcamRecordingSession? _webcamSession;
    private WriteableBitmap? _webcamPreviewBitmap;
    private int _webcamPreviewUpdatePending;
    private bool _closeAfterSave;

    public RecorderWindow()
    {
        InitializeComponent();
        _recordingSourceLabel = this.FindControl<TextBlock>("RecordingSourceLabel")!;
        _webcamPreviewBorder = this.FindControl<Border>("WebcamPreviewBorder")!;
        _webcamPreview = this.FindControl<Image>("WebcamPreview")!;
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += OnClosed;
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

    public void AttachWebcamPreview(IWebcamRecordingSession session, string deviceName)
    {
        _webcamSession = session;
        _recordingSourceLabel.Text = $"Webcam · {deviceName}";
        _webcamPreviewBorder.IsVisible = true;
        Height = 374;
        MinHeight = 374;
        _webcamSession.FrameCaptured += WebcamSession_OnFrameCaptured;
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

    private void WebcamSession_OnFrameCaptured(CapturedFrame frame)
    {
        if (Interlocked.Exchange(ref _webcamPreviewUpdatePending, 1) != 0)
            return;

        try
        {
            var pixels = frame.RgbaPixels.ToArray();
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (!IsVisible)
                        return;

                    var expectedSize = new CorePixelSize(frame.Size.Width, frame.Size.Height);
                    if (_webcamPreviewBitmap is null || _webcamPreviewBitmap.PixelSize.Width != expectedSize.Width ||
                        _webcamPreviewBitmap.PixelSize.Height != expectedSize.Height)
                    {
                        _webcamPreviewBitmap?.Dispose();
                        _webcamPreviewBitmap = new WriteableBitmap(
                            new AvaloniaPixelSize(expectedSize.Width, expectedSize.Height), new Vector(96, 96),
                            PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                        _webcamPreview.Source = _webcamPreviewBitmap;
                    }

                    using var framebuffer = _webcamPreviewBitmap.Lock();
                    var rowLength = checked(expectedSize.Width * 4);
                    for (var row = 0; row < expectedSize.Height; row++)
                        Marshal.Copy(pixels, row * rowLength, IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes), rowLength);
                }
                finally
                {
                    Interlocked.Exchange(ref _webcamPreviewUpdatePending, 0);
                }
            });
        }
        catch
        {
            Interlocked.Exchange(ref _webcamPreviewUpdatePending, 0);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_webcamSession is not null)
            _webcamSession.FrameCaptured -= WebcamSession_OnFrameCaptured;
        _webcamPreviewBitmap?.Dispose();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeAfterSave || _viewModel is null || _viewModel.HasFinished)
            return;

        e.Cancel = true;
        await _viewModel.StopRecordingCommand.ExecuteAsync(null);
    }
}
