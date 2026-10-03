using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;
using PixelRect = FrameStudio.Core.Models.PixelRect;

namespace FrameStudio.Avalonia.Views;

public sealed record CaptureSetupResult(MonitorDescriptor Monitor, PixelRect Region, int FramesPerSecond, bool CaptureCursor);

public partial class CaptureSetupWindow : Window
{
    private readonly ComboBox _monitorPicker;
    private readonly TextBlock _regionLabel;
    private readonly Slider _frameRateSlider;
    private readonly TextBlock _frameRateLabel;
    private readonly CheckBox _captureCursorCheckBox;
    private PixelRect? _selectedRegion;

    public CaptureSetupWindow()
    {
        InitializeComponent();
        _monitorPicker = this.FindControl<ComboBox>("MonitorPicker")!;
        _regionLabel = this.FindControl<TextBlock>("RegionLabel")!;
        _frameRateSlider = this.FindControl<Slider>("FrameRateSlider")!;
        _frameRateLabel = this.FindControl<TextBlock>("FrameRateLabel")!;
        _captureCursorCheckBox = this.FindControl<CheckBox>("CaptureCursorCheckBox")!;
    }

    public CaptureSetupWindow(IReadOnlyList<MonitorDescriptor> monitors) : this()
    {
        if (monitors.Count == 0)
            throw new InvalidOperationException("Windows did not report any available displays.");

        _monitorPicker.ItemsSource = monitors;
        _monitorPicker.SelectedItem = monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors[0];
        UpdateRegionToFullDisplay();
        UpdateFrameRateLabel();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void MonitorPicker_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateRegionToFullDisplay();
    }

    private async void ChooseArea_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_monitorPicker.SelectedItem is not MonitorDescriptor monitor)
            return;

        var picker = new RegionSelectorWindow(monitor);
        var region = await picker.ShowDialog<PixelRect?>(this);
        if (region is { } selected)
        {
            _selectedRegion = selected;
            _regionLabel.Text = $"{selected.Width:N0} × {selected.Height:N0} pixels";
        }
    }

    private void FrameRateSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => UpdateFrameRateLabel();

    private void UpdateFrameRateLabel() => _frameRateLabel.Text = $"{(int)Math.Round(_frameRateSlider.Value)} FPS";

    private void UpdateRegionToFullDisplay()
    {
        if (_monitorPicker.SelectedItem is not MonitorDescriptor monitor)
            return;

        _selectedRegion = new PixelRect(0, 0, monitor.Bounds.Width, monitor.Bounds.Height);
        _regionLabel.Text = $"Full display · {monitor.Bounds.Width:N0} × {monitor.Bounds.Height:N0}";
    }

    private void Start_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_monitorPicker.SelectedItem is not MonitorDescriptor monitor || _selectedRegion is not { } region)
            return;

        Close(new CaptureSetupResult(monitor, region, (int)Math.Round(_frameRateSlider.Value), _captureCursorCheckBox.IsChecked == true));
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}

public partial class RegionSelectorWindow : Window
{
    private readonly MonitorDescriptor _monitor;
    private readonly Canvas _selectionCanvas;
    private readonly Border _selectionVisual;
    private readonly TextBlock _sizeLabel;
    private Point _startPoint;
    private Rect _selection;
    private bool _isDragging;

    public RegionSelectorWindow() : this(new MonitorDescriptor("design", "Display", new PixelRect(0, 0, 1, 1), 1, true))
    {
    }

    public RegionSelectorWindow(MonitorDescriptor monitor)
    {
        _monitor = monitor;
        InitializeComponent();
        _selectionCanvas = this.FindControl<Canvas>("SelectionCanvas")!;
        _selectionVisual = this.FindControl<Border>("SelectionVisual")!;
        _sizeLabel = this.FindControl<TextBlock>("SizeLabel")!;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);
        Width = monitor.Bounds.Width / Math.Max(1d, monitor.ScaleFactor);
        Height = monitor.Bounds.Height / Math.Max(1d, monitor.ScaleFactor);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void SelectionCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_selectionCanvas).Properties.IsLeftButtonPressed)
            return;

        _startPoint = ClampToCanvas(e.GetPosition(_selectionCanvas));
        _selection = new Rect(_startPoint, _startPoint);
        _isDragging = true;
        _selectionVisual.IsVisible = true;
        e.Pointer.Capture(_selectionCanvas);
        UpdateSelectionVisual();
        e.Handled = true;
    }

    private void SelectionCanvas_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
            return;

        var current = ClampToCanvas(e.GetPosition(_selectionCanvas));
        _selection = new Rect(
            Math.Min(_startPoint.X, current.X),
            Math.Min(_startPoint.Y, current.Y),
            Math.Abs(current.X - _startPoint.X),
            Math.Abs(current.Y - _startPoint.Y));
        UpdateSelectionVisual();
    }

    private void SelectionCanvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void UpdateSelectionVisual()
    {
        Canvas.SetLeft(_selectionVisual, _selection.X);
        Canvas.SetTop(_selectionVisual, _selection.Y);
        _selectionVisual.Width = _selection.Width;
        _selectionVisual.Height = _selection.Height;
        var scale = Math.Max(1d, _monitor.ScaleFactor);
        var width = (int)Math.Round(_selection.Width * scale);
        var height = (int)Math.Round(_selection.Height * scale);
        _sizeLabel.Text = width > 0 && height > 0 ? $"{width:N0} × {height:N0} px" : "Drag to select an area";
    }

    private Point ClampToCanvas(Point point) => new(
        Math.Clamp(point.X, 0, _selectionCanvas.Bounds.Width),
        Math.Clamp(point.Y, 0, _selectionCanvas.Bounds.Height));

    private void Confirm_OnClick(object? sender, RoutedEventArgs e)
    {
        var scale = Math.Max(1d, _monitor.ScaleFactor);
        var left = Math.Clamp((int)Math.Round(_selection.X * scale), 0, _monitor.Bounds.Width);
        var top = Math.Clamp((int)Math.Round(_selection.Y * scale), 0, _monitor.Bounds.Height);
        var right = Math.Clamp((int)Math.Round(_selection.Right * scale), 0, _monitor.Bounds.Width);
        var bottom = Math.Clamp((int)Math.Round(_selection.Bottom * scale), 0, _monitor.Bounds.Height);
        if (right - left < 16 || bottom - top < 16)
        {
            _sizeLabel.Text = "Select an area at least 16 × 16 pixels";
            return;
        }

        Close(new PixelRect(left, top, right - left, bottom - top));
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void RegionSelector_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close(null);
        else if (e.Key == Key.Enter)
            Confirm_OnClick(sender, new RoutedEventArgs());
    }
}
