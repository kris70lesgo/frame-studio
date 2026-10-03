using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;
using System.Globalization;

namespace FrameStudio.Avalonia.Views;

public partial class TextOverlayWindow : Window
{
    private readonly PixelSize _canvasSize;
    private readonly TextBox _textInput;
    private readonly TextBox _fontSizeInput;
    private readonly TextBox _xInput;
    private readonly TextBox _yInput;
    private readonly TextBox _colorInput;
    private readonly TextBlock _errorLabel;

    public TextOverlayWindow() : this(new PixelSize(640, 360))
    {
    }

    public TextOverlayWindow(PixelSize canvasSize)
    {
        AvaloniaXamlLoader.Load(this);
        _canvasSize = canvasSize;
        _textInput = this.FindControl<TextBox>("AnnotationInput")!;
        _fontSizeInput = this.FindControl<TextBox>("FontSizeInput")!;
        _xInput = this.FindControl<TextBox>("XInput")!;
        _yInput = this.FindControl<TextBox>("YInput")!;
        _colorInput = this.FindControl<TextBox>("ColorInput")!;
        _errorLabel = this.FindControl<TextBlock>("ErrorLabel")!;

        _textInput.Text = "Add a note";
        _fontSizeInput.Text = Math.Clamp(Math.Min(canvasSize.Width, canvasSize.Height) / 12, 6, 48).ToString(CultureInfo.InvariantCulture);
        _xInput.Text = Math.Min(24, Math.Max(0, canvasSize.Width / 8)).ToString(CultureInfo.InvariantCulture);
        _yInput.Text = Math.Min(24, Math.Max(0, canvasSize.Height / 8)).ToString(CultureInfo.InvariantCulture);
        _colorInput.Text = "#FFFFFF";
    }

    private void Apply_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(_fontSizeInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var fontSize) ||
            !int.TryParse(_xInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(_yInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var y))
        {
            ShowError("Enter whole numbers for size and position.");
            return;
        }

        if (!TryParseColor(_colorInput.Text, out var color))
        {
            ShowError("Enter a color in #RRGGBB format.");
            return;
        }

        var options = new TextOverlayOptions(_textInput.Text ?? string.Empty, x, y, fontSize, color);
        try
        {
            RgbaFrameTransform.ValidateTextOverlay(_canvasSize, options);
            Close(options);
        }
        catch (ArgumentException ex)
        {
            ShowError(ex.Message);
        }
    }

    private static bool TryParseColor(string? value, out Rgba32 color)
    {
        color = Rgba32.FromRgb(255, 255, 255);
        if (value is not { Length: 7 } || value[0] != '#')
            return false;

        if (!byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) ||
            !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) ||
            !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            return false;

        color = Rgba32.FromRgb(red, green, blue);
        return true;
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Foreground = Brushes.OrangeRed;
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}
