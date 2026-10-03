using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FrameStudio.Avalonia.ViewModels;
using System.Runtime.InteropServices;

namespace FrameStudio.Avalonia.Views;

public partial class EditorWindow : Window
{
    private EditorViewModel _viewModel = null!;
    private readonly Image _previewImage;
    private readonly TextBlock _previewEmptyText;
    private readonly ListBox _timelineList;
    private readonly Button _playButton;
    private readonly DispatcherTimer _playTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly SemaphoreSlim _frameReadGate = new(2, 2);
    private CancellationTokenSource? _previewCancellation;
    private WriteableBitmap? _previewBitmap;
    private bool _isLoadingPreview;
    private bool _isPlaying;

    public EditorWindow()
    {
        InitializeComponent();
        _previewImage = this.FindControl<Image>("PreviewImage")!;
        _previewEmptyText = this.FindControl<TextBlock>("PreviewEmptyText")!;
        _timelineList = this.FindControl<ListBox>("TimelineList")!;
        _playButton = this.FindControl<Button>("PlayButton")!;
        _playTimer.Tick += PlayTimer_OnTick;
        Closed += (_, _) =>
        {
            _playTimer.Stop();
            _previewCancellation?.Cancel();
            _previewBitmap?.Dispose();
        };
    }

    private EditorWindow(EditorViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public static async Task<EditorWindow> CreateAsync(string projectPath)
    {
        var project = await FrameStudio.Core.Projects.FrameProjectArchiveReader.ReadProjectAsync(projectPath);
        return new EditorWindow(new EditorViewModel(projectPath, project));
    }

    private async void Editor_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedFrame is not null)
        {
            _timelineList.SelectedItem = _viewModel.SelectedFrame;
            await LoadPreviewAsync();
        }
    }

    private async void Timeline_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_timelineList.SelectedItem is TimelineFrameViewModel selected)
        {
            _viewModel.SelectedFrame = selected;
            await LoadPreviewAsync();
            UpdatePlaybackInterval();
        }
    }

    private async Task LoadPreviewAsync()
    {
        if (_viewModel.SelectedFrame is not { } selectedFrame)
            return;

        _previewCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        _isLoadingPreview = true;
        var acquiredReadSlot = false;
        try
        {
            await _frameReadGate.WaitAsync(cancellation.Token);
            acquiredReadSlot = true;
            var pixels = await _viewModel.ReadFrameAsync(selectedFrame.SourceFrameIndex, cancellation.Token);
            if (cancellation.IsCancellationRequested || !ReferenceEquals(_viewModel.SelectedFrame, selectedFrame))
                return;

            var size = _viewModel.CanvasSize;
            var bitmap = new WriteableBitmap(new PixelSize(size.Width, size.Height), new Vector(96, 96),
                PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (var framebuffer = bitmap.Lock())
            {
                var rowLength = size.Width * 4;
                for (var row = 0; row < size.Height; row++)
                    Marshal.Copy(pixels, row * rowLength, IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes), rowLength);
            }

            if (cancellation.IsCancellationRequested || !ReferenceEquals(_viewModel.SelectedFrame, selectedFrame))
            {
                bitmap.Dispose();
                return;
            }

            _previewBitmap?.Dispose();
            _previewBitmap = bitmap;
            _previewImage.Source = bitmap;
            _previewEmptyText.IsVisible = false;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"Could not preview frame: {ex.Message}");
        }
        finally
        {
            if (acquiredReadSlot)
                _frameReadGate.Release();
            if (ReferenceEquals(_previewCancellation, cancellation))
            {
                _previewCancellation = null;
                _isLoadingPreview = false;
            }
            cancellation.Dispose();
        }
    }

    private async void TimelineThumbnail_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Image image || image.DataContext is not TimelineFrameViewModel frame || image.Tag is not null)
            return;

        var state = new ThumbnailLoadState();
        var cancellationToken = state.Cancellation.Token;
        image.Tag = state;
        var acquiredReadSlot = false;
        try
        {
            await _frameReadGate.WaitAsync(cancellationToken);
            acquiredReadSlot = true;
            var pixels = await _viewModel.ReadFrameAsync(frame.SourceFrameIndex, cancellationToken);
            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(image.Tag, state))
                return;

            var canvasSize = _viewModel.CanvasSize;
            var scale = Math.Min(92d / canvasSize.Width, 58d / canvasSize.Height);
            var width = Math.Max(1, (int)Math.Round(canvasSize.Width * scale));
            var height = Math.Max(1, (int)Math.Round(canvasSize.Height * scale));
            var thumbnailPixels = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                var sourceY = y * canvasSize.Height / height;
                for (var x = 0; x < width; x++)
                {
                    var sourceX = x * canvasSize.Width / width;
                    var sourceOffset = (sourceY * canvasSize.Width + sourceX) * 4;
                    var destinationOffset = (y * width + x) * 4;
                    Buffer.BlockCopy(pixels, sourceOffset, thumbnailPixels, destinationOffset, 4);
                }
            }

            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
                PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (var framebuffer = bitmap.Lock())
            {
                var rowLength = width * 4;
                for (var row = 0; row < height; row++)
                    Marshal.Copy(thumbnailPixels, row * rowLength, IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes), rowLength);
            }

            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(image.Tag, state))
            {
                bitmap.Dispose();
                return;
            }

            state.Bitmap = bitmap;
            image.Source = bitmap;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(image.Tag, state))
                _viewModel.ReportStatus($"Could not load frame thumbnail: {ex.Message}");
        }
        finally
        {
            if (acquiredReadSlot)
                _frameReadGate.Release();
        }
    }

    private static void TimelineThumbnail_OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Image image)
            return;

        if (image.Tag is ThumbnailLoadState state)
            state.Dispose();
        image.Tag = null;
        image.Source = null;
    }

    private void Play_OnClick(object? sender, RoutedEventArgs e)
    {
        _isPlaying = !_isPlaying;
        _playButton.Content = _isPlaying ? "Ⅱ Pause" : "▶ Play";
        if (_isPlaying)
        {
            UpdatePlaybackInterval();
            _playTimer.Start();
        }
        else
            _playTimer.Stop();
    }

    private async void PlayTimer_OnTick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _isLoadingPreview || _viewModel.Frames.Count == 0)
            return;

        var currentIndex = _viewModel.SelectedFrame is null ? 0 : _viewModel.Frames.IndexOf(_viewModel.SelectedFrame);
        var nextIndex = (currentIndex + 1) % _viewModel.Frames.Count;
        _timelineList.SelectedItem = _viewModel.Frames[nextIndex];
        await LoadPreviewAsync();
        UpdatePlaybackInterval();
    }

    private void UpdatePlaybackInterval()
    {
        if (_viewModel.SelectedFrame is { } frame)
            _playTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(frame.DurationMilliseconds, 10, 655_350));
    }

    private sealed class ThumbnailLoadState : IDisposable
    {
        private bool _disposed;

        public CancellationTokenSource Cancellation { get; } = new();
        public WriteableBitmap? Bitmap { get; set; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Cancellation.Cancel();
            Bitmap?.Dispose();
            Cancellation.Dispose();
        }
    }

    private async void ExportGif_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export animation as GIF",
                SuggestedFileName = $"{_viewModel.ProjectName}.gif",
                DefaultExtension = "gif",
                ShowOverwritePrompt = true,
                FileTypeChoices = [new FilePickerFileType("GIF animation") { Patterns = ["*.gif"] }]
            });
            var path = file?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
                await _viewModel.ExportGifAsync(path);
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"Could not open the GIF export dialog: {ex.Message}");
        }
    }
}
