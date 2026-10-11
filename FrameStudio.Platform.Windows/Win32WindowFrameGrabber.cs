using System.ComponentModel;
using System.Runtime.InteropServices;
using FrameStudio.Core.Models;

namespace FrameStudio.Platform.Windows;

/// <summary>Captures one window's own rendered content instead of copying the desktop beneath it.</summary>
internal sealed class Win32WindowFrameGrabber : IFrameGrabber
{
    private readonly nint _window;
    private readonly bool _captureCursor;
    private readonly PixelSize _initialSize;
    private Win32Native.Rect _lastBounds;
    private IntPtr _memoryDc;
    private IntPtr _bitmap;
    private IntPtr _previousBitmap;
    private bool _disposed;

    public int Width => _initialSize.Width;
    public int Height => _initialSize.Height;

    public Win32WindowFrameGrabber(nint window, bool captureCursor)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Window capture is available only on Windows.");
        if (window == IntPtr.Zero || !Win32Native.IsWindow(window))
            throw new ArgumentException("Choose an available application window.", nameof(window));
        if (Win32Native.IsIconic(window))
            throw new InvalidOperationException("Restore the selected window before recording it.");
        if (!Win32Native.GetWindowRect(window, out _lastBounds))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the selected window's bounds.");

        var width = _lastBounds.Right - _lastBounds.Left;
        var height = _lastBounds.Bottom - _lastBounds.Top;
        ValidateDimensions(width, height);
        _initialSize = new PixelSize(width, height);
        _window = window;
        _captureCursor = captureCursor;

        var desktopDc = Win32Native.GetDC(IntPtr.Zero);
        if (desktopDc == IntPtr.Zero)
            throw LastError("Could not open the desktop drawing context.");

        try
        {
            _memoryDc = Win32Native.CreateCompatibleDC(desktopDc);
            if (_memoryDc == IntPtr.Zero)
                throw LastError("Could not create an in-memory drawing context.");

            _bitmap = Win32Native.CreateCompatibleBitmap(desktopDc, Width, Height);
            if (_bitmap == IntPtr.Zero)
                throw LastError("Could not allocate a window capture bitmap.");

            _previousBitmap = Win32Native.SelectObject(_memoryDc, _bitmap);
            if (_previousBitmap == IntPtr.Zero || _previousBitmap == new IntPtr(-1))
            {
                _previousBitmap = IntPtr.Zero;
                throw LastError("Could not select the window capture bitmap.");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            _ = Win32Native.ReleaseDC(IntPtr.Zero, desktopDc);
        }
    }

    public byte[] CaptureRgba()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UpdateWindowBounds();

        var rendered = Win32Native.PrintWindow(_window, _memoryDc, Win32Native.PrintWindowRenderFullContent) ||
                       Win32Native.PrintWindow(_window, _memoryDc, 0);
        if (!rendered)
            throw new InvalidOperationException("Windows could not render this application window. Try another window or use screen-region recording.");

        if (_captureCursor)
            DrawCursor();

        var header = new Win32Native.BitmapInfo
        {
            Header = new Win32Native.BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<Win32Native.BitmapInfoHeader>(),
                Width = Width,
                Height = -Height,
                Planes = 1,
                BitCount = 32,
                Compression = Win32Native.BiRgb,
                SizeImage = checked((uint)(Width * Height * 4))
            }
        };
        var bgra = new byte[checked(Width * Height * 4)];
        var desktopDc = Win32Native.GetDC(IntPtr.Zero);
        if (desktopDc == IntPtr.Zero)
            throw LastError("Could not open the desktop drawing context.");

        try
        {
            // GetDIBits requires the bitmap to be deselected from every memory DC.
            _ = Win32Native.SelectObject(_memoryDc, _previousBitmap);
            try
            {
                var lines = Win32Native.GetDIBits(desktopDc, _bitmap, 0, (uint)Height, bgra, ref header, Win32Native.DibRgbColors);
                if (lines != Height)
                    throw LastError("Could not read pixels from the window capture bitmap.");
            }
            finally
            {
                _ = Win32Native.SelectObject(_memoryDc, _bitmap);
            }
        }
        finally
        {
            // The common DC must be released on the thread that acquired it.
            _ = Win32Native.ReleaseDC(IntPtr.Zero, desktopDc);
        }

        for (var index = 0; index < bgra.Length; index += 4)
        {
            (bgra[index], bgra[index + 2]) = (bgra[index + 2], bgra[index]);
            bgra[index + 3] = byte.MaxValue;
        }

        return bgra;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_memoryDc != IntPtr.Zero && _previousBitmap != IntPtr.Zero)
            _ = Win32Native.SelectObject(_memoryDc, _previousBitmap);
        if (_bitmap != IntPtr.Zero)
            _ = Win32Native.DeleteObject(_bitmap);
        if (_memoryDc != IntPtr.Zero)
            _ = Win32Native.DeleteDC(_memoryDc);
        _previousBitmap = IntPtr.Zero;
        _bitmap = IntPtr.Zero;
        _memoryDc = IntPtr.Zero;
    }

    private void UpdateWindowBounds()
    {
        if (!Win32Native.IsWindow(_window))
            throw new InvalidOperationException("The selected window was closed during recording.");
        if (Win32Native.IsIconic(_window))
            throw new InvalidOperationException("The selected window was minimized during recording.");
        if (!Win32Native.GetWindowRect(_window, out var bounds))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the selected window's bounds.");

        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        if (width != Width || height != Height)
            throw new InvalidOperationException("The selected window changed size during recording. Stop and start a new recording to use its new size.");

        _lastBounds = bounds;
    }

    private void DrawCursor()
    {
        var cursorInfo = new Win32Native.CursorInfo { Size = Marshal.SizeOf<Win32Native.CursorInfo>() };
        if (!Win32Native.GetCursorInfo(out cursorInfo) || cursorInfo.Flags != Win32Native.CursorShowing || cursorInfo.Cursor == IntPtr.Zero)
            return;

        if (!Win32Native.GetIconInfo(cursorInfo.Cursor, out var iconInfo))
            return;

        try
        {
            var x = cursorInfo.Position.X - _lastBounds.Left - (int)iconInfo.HotspotX;
            var y = cursorInfo.Position.Y - _lastBounds.Top - (int)iconInfo.HotspotY;
            _ = Win32Native.DrawIconEx(_memoryDc, x, y, cursorInfo.Cursor, 0, 0, 0, IntPtr.Zero, Win32Native.DiNormal);
        }
        finally
        {
            if (iconInfo.Mask != IntPtr.Zero)
                _ = Win32Native.DeleteObject(iconInfo.Mask);
            if (iconInfo.Color != IntPtr.Zero)
                _ = Win32Native.DeleteObject(iconInfo.Color);
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > 16_384 || height > 16_384 || (long)width * height > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(width), "Window capture bounds must fit within 16,384 pixels per side and 16 megapixels total.");
    }

    private static Win32Exception LastError(string message) => new(Marshal.GetLastWin32Error(), message);
}
