using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FrameStudio.Avalonia.ViewModels;
using CorePixelRect = FrameStudio.Core.Models.PixelRect;
using CorePixelSize = FrameStudio.Core.Models.PixelSize;
using System.Runtime.InteropServices;

namespace FrameStudio.Avalonia.Views;

public partial class EditorWindow : Window
{
    private EditorViewModel _viewModel = null!;
    private readonly Image _previewImage;
    private readonly TextBlock _previewEmptyText;
    private readonly ListBox _timelineList;
    private readonly Button _playButton;
    private readonly Button _cropToolButton;
    private readonly Button _applyCropButton;
    private readonly StackPanel _cropControls;
    private readonly Canvas _cropCanvas;
    private readonly Border _cropSelectionVisual;
    private readonly DispatcherTimer _playTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly SemaphoreSlim _frameReadGate = new(2, 2);
    private CancellationTokenSource? _previewCancellation;
    private WriteableBitmap? _previewBitmap;
    private bool _isLoadingPreview;
    private bool _isPlaying;
    private bool _isDraggingCrop;
    private bool _allowClose;
    private bool _isConfirmingClose;
    private Point _cropStart;
    private Rect? _cropSelection;
    private CorePixelRect? _pendingCrop;

    public EditorWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, EditorWindow_OnKeyDown, RoutingStrategies.Tunnel);
        _previewImage = this.FindControl<Image>("PreviewImage")!;
        _previewEmptyText = this.FindControl<TextBlock>("PreviewEmptyText")!;
        _timelineList = this.FindControl<ListBox>("TimelineList")!;
        _playButton = this.FindControl<Button>("PlayButton")!;
        _cropToolButton = this.FindControl<Button>("CropToolButton")!;
        _applyCropButton = this.FindControl<Button>("ApplyCropButton")!;
        _cropControls = this.FindControl<StackPanel>("CropControls")!;
        _cropCanvas = this.FindControl<Canvas>("CropCanvas")!;
        _cropSelectionVisual = this.FindControl<Border>("CropSelectionVisual")!;
        _playTimer.Tick += PlayTimer_OnTick;
        Closing += EditorWindow_OnClosing;
        Closed += (_, _) =>
        {
            _playTimer.Stop();
            _previewCancellation?.Cancel();
            _previewBitmap?.Dispose();
            _viewModel?.DiscardEdits();
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

    private async void EditorWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose || _viewModel is null || !_viewModel.IsDirty)
            return;

        e.Cancel = true;
        if (_isConfirmingClose)
            return;

        _isConfirmingClose = true;
        try
        {
            var choice = await new UnsavedChangesWindow().ShowDialog<UnsavedChangesChoice?>(this);
            if (choice is null or UnsavedChangesChoice.Cancel)
                return;

            if (choice == UnsavedChangesChoice.Save)
            {
                await _viewModel.SaveProjectCommand.ExecuteAsync(null);
                if (_viewModel.IsDirty)
                    return;
            }
            else
            {
                _viewModel.DiscardEdits();
            }

            _allowClose = true;
            Close();
        }
        finally
        {
            _isConfirmingClose = false;
        }
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

    private void CropTool_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_cropCanvas.IsVisible)
        {
            EndCropMode();
            return;
        }

        _playTimer.Stop();
        _isPlaying = false;
        _playButton.Content = "▶ Play";
        _pendingCrop = null;
        _cropSelection = null;
        _cropSelectionVisual.IsVisible = false;
        _applyCropButton.IsEnabled = false;
        _cropCanvas.IsVisible = true;
        _cropControls.IsVisible = true;
        if (!_cropToolButton.Classes.Contains("selected"))
            _cropToolButton.Classes.Add("selected");
    }

    private void CropCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_cropCanvas).Properties.IsLeftButtonPressed || !TryGetImageBounds(out var imageBounds))
            return;

        var point = e.GetPosition(_cropCanvas);
        if (!imageBounds.Contains(point))
            return;

        _cropStart = point;
        _cropSelection = new Rect(point, point);
        _isDraggingCrop = true;
        _cropSelectionVisual.IsVisible = true;
        e.Pointer.Capture(_cropCanvas);
        UpdateCropSelectionVisual();
        e.Handled = true;
    }

    private void CropCanvas_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingCrop || !TryGetImageBounds(out var imageBounds))
            return;

        var point = ClampToBounds(e.GetPosition(_cropCanvas), imageBounds);
        _cropSelection = new Rect(
            Math.Min(_cropStart.X, point.X), Math.Min(_cropStart.Y, point.Y),
            Math.Abs(point.X - _cropStart.X), Math.Abs(point.Y - _cropStart.Y));
        UpdateCropSelectionVisual();
        e.Handled = true;
    }

    private void CropCanvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingCrop || !TryGetImageBounds(out var imageBounds))
            return;

        _isDraggingCrop = false;
        e.Pointer.Capture(null);
        var selection = _cropSelection ?? default;
        var canvasSize = _viewModel.CanvasSize;
        var left = Math.Clamp((int)Math.Floor((selection.X - imageBounds.X) * canvasSize.Width / imageBounds.Width), 0, canvasSize.Width);
        var top = Math.Clamp((int)Math.Floor((selection.Y - imageBounds.Y) * canvasSize.Height / imageBounds.Height), 0, canvasSize.Height);
        var right = Math.Clamp((int)Math.Ceiling((selection.Right - imageBounds.X) * canvasSize.Width / imageBounds.Width), 0, canvasSize.Width);
        var bottom = Math.Clamp((int)Math.Ceiling((selection.Bottom - imageBounds.Y) * canvasSize.Height / imageBounds.Height), 0, canvasSize.Height);
        if (right > left && bottom > top)
        {
            _pendingCrop = new CorePixelRect(left, top, right - left, bottom - top);
            _applyCropButton.IsEnabled = true;
        }

        e.Handled = true;
    }

    private async void ApplyCrop_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_pendingCrop is not { } crop)
            return;

        _applyCropButton.IsEnabled = false;
        if (await _viewModel.CropAsync(crop))
            EndCropMode();
        else
            _applyCropButton.IsEnabled = true;
    }

    private void CancelCrop_OnClick(object? sender, RoutedEventArgs e) => EndCropMode();

    private void EndCropMode()
    {
        _cropCanvas.IsVisible = false;
        _cropControls.IsVisible = false;
        _cropSelectionVisual.IsVisible = false;
        _cropSelection = null;
        _pendingCrop = null;
        _isDraggingCrop = false;
        _applyCropButton.IsEnabled = false;
        _cropToolButton.Classes.Remove("selected");
    }

    private void UpdateCropSelectionVisual()
    {
        if (_cropSelection is not { } selection)
            return;

        Canvas.SetLeft(_cropSelectionVisual, selection.X);
        Canvas.SetTop(_cropSelectionVisual, selection.Y);
        _cropSelectionVisual.Width = selection.Width;
        _cropSelectionVisual.Height = selection.Height;
    }

    private bool TryGetImageBounds(out Rect bounds)
    {
        bounds = _previewImage.Bounds;
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static Point ClampToBounds(Point point, Rect bounds) => new(
        Math.Clamp(point.X, bounds.X, bounds.Right), Math.Clamp(point.Y, bounds.Y, bounds.Bottom));

    private async void ResizeTool_OnClick(object? sender, RoutedEventArgs e)
    {
        var targetSize = await new ResizeWindow(_viewModel.CanvasSize).ShowDialog<CorePixelSize?>(this);
        if (targetSize is { } size && size != _viewModel.CanvasSize)
            await _viewModel.ResizeAsync(size);
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

    private async void EditorWindow_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && HasShortcutModifier(e))
        {
            await _viewModel.SaveProjectCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        if (e.Source is TextBox)
            return;

        if (e.Key == Key.D && HasShortcutModifier(e))
        {
            _viewModel.DuplicateSelectedFrameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Left && HasShortcutModifier(e))
        {
            _viewModel.MoveSelectedFrameEarlierCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Right && HasShortcutModifier(e))
        {
            _viewModel.MoveSelectedFrameLaterCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None && e.Source is not Button)
        {
            _viewModel.DeleteSelectedFrameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None && e.Source is not Button)
        {
            Play_OnClick(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _cropCanvas.IsVisible)
        {
            EndCropMode();
            e.Handled = true;
        }
    }

    private static bool HasShortcutModifier(KeyEventArgs e) =>
        e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta;

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
            {
                await _viewModel.ExportGifAsync(path);
                if (string.Equals(_viewModel.Status, "GIF exported", StringComparison.Ordinal))
                    await new ExportCompleteWindow(path).ShowDialog(this);
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"Could not complete GIF export: {ex.Message}");
        }
    }
}
