using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FrameStudio.Avalonia.Services;
using FrameStudio.Avalonia.ViewModels;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;
using FrameStudio.Platform.Abstractions;
using AvaloniaPixelSize = global::Avalonia.PixelSize;
using CorePixelSize = FrameStudio.Core.Models.PixelSize;

namespace FrameStudio.Avalonia.Views;

public partial class SketchboardWindow : Window
{
    private readonly string _projectPath;
    private readonly ComboBox _resolutionPicker;
    private readonly Slider _frameRateSlider;
    private readonly TextBlock _frameRateLabel;
    private readonly TextBlock _canvasSizeLabel;
    private readonly TextBlock _brushSizeLabel;
    private readonly TextBlock _statusText;
    private readonly ComboBox _brushColorPicker;
    private readonly Slider _brushSizeSlider;
    private readonly Button _startButton;
    private readonly StackPanel _recordingButtons;
    private readonly Border _canvasSurface;
    private readonly Canvas _drawingCanvas;
    private readonly DispatcherTimer _captureTimer;
    private CorePixelSize _canvasSize = new(960, 540);
    private Color _brushColor = Color.Parse("#292733");
    private Polyline? _currentLine;
    private RecordingViewModel? _recordingViewModel;
    private SketchboardRecordingSession? _recordingSession;
    private DateTimeOffset? _lastFrameCapturedAt;
    private int _capturedFrameCount;
    private bool _isDrawing;
    private bool _isInitialized;
    private bool _closeAfterSave;

    public SketchboardWindow()
        : this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FrameStudio-sketchboard-design.fsp"), 30)
    {
    }

    public SketchboardWindow(string projectPath, int defaultFramesPerSecond)
    {
        _projectPath = System.IO.Path.GetFullPath(projectPath);
        InitializeComponent();
        _resolutionPicker = this.FindControl<ComboBox>("ResolutionPicker")!;
        _frameRateSlider = this.FindControl<Slider>("FrameRateSlider")!;
        _frameRateLabel = this.FindControl<TextBlock>("FrameRateLabel")!;
        _canvasSizeLabel = this.FindControl<TextBlock>("CanvasSizeLabel")!;
        _brushSizeLabel = this.FindControl<TextBlock>("BrushSizeLabel")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _brushColorPicker = this.FindControl<ComboBox>("BrushColorPicker")!;
        _brushSizeSlider = this.FindControl<Slider>("BrushSizeSlider")!;
        _startButton = this.FindControl<Button>("StartButton")!;
        _recordingButtons = this.FindControl<StackPanel>("RecordingButtons")!;
        _canvasSurface = this.FindControl<Border>("CanvasSurface")!;
        _drawingCanvas = this.FindControl<Canvas>("DrawingCanvas")!;
        _frameRateSlider.Value = Math.Clamp(defaultFramesPerSecond, 10, 60);
        _captureTimer = new DispatcherTimer();

        _isInitialized = true;
        ApplySelectedResolution(clearCanvas: false);
        UpdateFrameRateLabel();
        UpdateBrushSizeLabel();
        Closing += OnClosing;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ResolutionPicker_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitialized && _recordingViewModel is null)
            ApplySelectedResolution(clearCanvas: true);
    }

    private void FrameRateSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitialized)
            UpdateFrameRateLabel();
    }

    private void BrushColorPicker_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_brushColorPicker.SelectedItem is ComboBoxItem { Tag: string color } && Color.TryParse(color, out var parsed))
            _brushColor = parsed;
    }

    private void BrushSizeSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitialized)
            UpdateBrushSizeLabel();
    }

    private void ApplySelectedResolution(bool clearCanvas)
    {
        if (_resolutionPicker.SelectedItem is not ComboBoxItem { Tag: string dimensions })
            return;

        var parts = dimensions.Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height))
            return;

        _canvasSize = new CorePixelSize(width, height);
        _canvasSurface.Width = width;
        _canvasSurface.Height = height;
        _drawingCanvas.Width = width;
        _drawingCanvas.Height = height;
        _canvasSizeLabel.Text = $"{width:N0} × {height:N0} px";
        if (clearCanvas)
            _drawingCanvas.Children.Clear();
    }

    private void UpdateFrameRateLabel() => _frameRateLabel.Text = $"{(int)Math.Round(_frameRateSlider.Value)} FPS";

    private void UpdateBrushSizeLabel() => _brushSizeLabel.Text = $"{(int)Math.Round(_brushSizeSlider.Value)} px";

    private void DrawingCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_recordingViewModel is { IsStopping: true } || !e.GetCurrentPoint(_drawingCanvas).Properties.IsLeftButtonPressed)
            return;

        var position = ClampToCanvas(e.GetPosition(_drawingCanvas));
        _currentLine = new Polyline
        {
            Stroke = new SolidColorBrush(_brushColor),
            StrokeThickness = _brushSizeSlider.Value,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round
        };
        _currentLine.Points.Add(position);
        _drawingCanvas.Children.Add(_currentLine);
        _isDrawing = true;
        e.Pointer.Capture(_drawingCanvas);
        e.Handled = true;
    }

    private void DrawingCanvas_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDrawing || _currentLine is null || !e.GetCurrentPoint(_drawingCanvas).Properties.IsLeftButtonPressed)
            return;

        _currentLine.Points.Add(ClampToCanvas(e.GetPosition(_drawingCanvas)));
        e.Handled = true;
    }

    private void DrawingCanvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDrawing = false;
        _currentLine = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private Point ClampToCanvas(Point point) => new(
        Math.Clamp(point.X, 0, _canvasSize.Width),
        Math.Clamp(point.Y, 0, _canvasSize.Height));

    private void Undo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_drawingCanvas.Children.Count > 0)
            _drawingCanvas.Children.RemoveAt(_drawingCanvas.Children.Count - 1);
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs e) => _drawingCanvas.Children.Clear();

    private async void StartRecording_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_recordingViewModel is not null)
            return;

        _startButton.IsEnabled = false;
        var framesPerSecond = (int)Math.Round(_frameRateSlider.Value);
        FrameProjectArchiveWriter? writer = null;
        try
        {
            writer = await FrameProjectArchiveWriter.CreateAsync(
                _projectPath, System.IO.Path.GetFileNameWithoutExtension(_projectPath), _canvasSize);
            var session = new SketchboardRecordingSession(_canvasSize);
            var viewModel = new RecordingViewModel(session, writer, _projectPath, framesPerSecond, "sketchboard");
            _recordingSession = session;
            _recordingViewModel = viewModel;
            _recordingViewModel.PropertyChanged += RecordingViewModel_OnPropertyChanged;
            _recordingViewModel.RecordingCompleted += RecordingViewModel_OnRecordingCompleted;
            DataContext = viewModel;
            writer = null;

            _resolutionPicker.IsEnabled = false;
            _frameRateSlider.IsEnabled = false;
            _startButton.IsVisible = false;
            _recordingButtons.IsVisible = true;
            _statusText.Text = "Recording your sketch · draw on the canvas";
            _captureTimer.Interval = TimeSpan.FromSeconds(1d / framesPerSecond);
            _captureTimer.Tick += CaptureTimer_OnTick;

            CaptureFrame();
            _captureTimer.Start();
        }
        catch (Exception ex)
        {
            if (_recordingViewModel is not null)
            {
                _captureTimer.Stop();
                _recordingSession?.ReportFailure(ex);
                _statusText.Text = $"Sketchboard capture stopped: {ex.Message} · stop to save captured frames";
            }
            else if (writer is not null)
                await writer.DisposeAsync();

            if (_recordingViewModel is null)
            {
                _startButton.IsEnabled = true;
                _statusText.Text = $"Could not start sketchboard recording: {ex.Message}";
            }
        }
    }

    private void CaptureTimer_OnTick(object? sender, EventArgs e)
    {
        try
        {
            CaptureFrame();
        }
        catch (Exception ex)
        {
            _captureTimer.Stop();
            _recordingSession?.ReportFailure(ex);
        }
    }

    private void CaptureFrame()
    {
        if (_recordingViewModel is null || _recordingSession is null || _recordingSession.IsPaused ||
            _recordingSession.IsStopped)
            return;

        var capturedAt = DateTimeOffset.UtcNow;
        var interval = _lastFrameCapturedAt is { } lastFrame
            ? (int)Math.Round((capturedAt - lastFrame).TotalMilliseconds)
            : (int)Math.Round(1000d / _frameRateSlider.Value);
        var pixels = RenderCanvasAsRgba();
        var duration = Math.Clamp(interval, 1, 60_000);
        if (_recordingSession.PublishFrame(new CapturedFrame(_canvasSize, pixels, duration, capturedAt)))
        {
            _lastFrameCapturedAt = capturedAt;
            _capturedFrameCount++;
        }
    }

    private byte[] RenderCanvasAsRgba()
    {
        var pixelSize = new AvaloniaPixelSize(_canvasSize.Width, _canvasSize.Height);
        using var rendered = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        rendered.Render(_canvasSurface);

        using var rgba = new WriteableBitmap(pixelSize, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var framebuffer = rgba.Lock();
        rendered.CopyPixels(framebuffer);

        var pixels = new byte[checked(_canvasSize.Width * _canvasSize.Height * 4)];
        var rowLength = checked(_canvasSize.Width * 4);
        for (var row = 0; row < _canvasSize.Height; row++)
            Marshal.Copy(IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes), pixels, row * rowLength, rowLength);
        return pixels;
    }

    private void RecordingViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecordingViewModel.IsPaused) && _recordingViewModel?.IsPaused == false)
            _lastFrameCapturedAt = null;
        else if (e.PropertyName == nameof(RecordingViewModel.Status) && _recordingViewModel is { HasFinished: false })
            _statusText.Text = _recordingViewModel.Status;
    }

    private void RecordingViewModel_OnRecordingCompleted(object? sender, string projectPath)
    {
        _captureTimer.Stop();
        _closeAfterSave = true;
        Close(projectPath);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _captureTimer.Stop();
        if (_closeAfterSave || _recordingViewModel is null || _recordingViewModel.HasFinished)
            return;

        e.Cancel = true;
        await _recordingViewModel.StopRecordingCommand.ExecuteAsync(null);
        if (_recordingViewModel.HasFinished && _capturedFrameCount == 0)
        {
            _closeAfterSave = true;
            Close(null);
        }
    }
}
