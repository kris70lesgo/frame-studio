using System.ComponentModel;
using System.Runtime.InteropServices;
using FrameStudio.Core.Models;

namespace FrameStudio.Platform.Windows;

/// <summary>Owns the GDI resources for one desktop region and returns tightly-packed RGBA frames.</summary>
internal sealed class Win32FrameGrabber : IDisposable
{
    private readonly PixelRect _region;
    private readonly bool _captureCursor;
    private IntPtr _desktopDc;
    private IntPtr _memoryDc;
    private IntPtr _bitmap;
    private IntPtr _previousBitmap;
    private bool _disposed;

    public int Width => _region.Width;
    public int Height => _region.Height;

    public Win32FrameGrabber(PixelRect region, bool captureCursor)
    {
        _region = region;
        _captureCursor = captureCursor;

        _desktopDc = Win32Native.GetDC(IntPtr.Zero);
        if (_desktopDc == IntPtr.Zero)
            throw LastError("Could not open the desktop drawing context.");

        _memoryDc = Win32Native.CreateCompatibleDC(_desktopDc);
        if (_memoryDc == IntPtr.Zero)
        {
            Dispose();
            throw LastError("Could not create an in-memory drawing context.");
        }

        _bitmap = Win32Native.CreateCompatibleBitmap(_desktopDc, Width, Height);
        if (_bitmap == IntPtr.Zero)
        {
            Dispose();
            throw LastError("Could not allocate a screen capture bitmap.");
        }

        _previousBitmap = Win32Native.SelectObject(_memoryDc, _bitmap);
        if (_previousBitmap == IntPtr.Zero || _previousBitmap == new IntPtr(-1))
        {
            _previousBitmap = IntPtr.Zero;
            Dispose();
            throw LastError("Could not select the screen capture bitmap.");
        }
    }

    public byte[] CaptureRgba()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var copied = Win32Native.BitBlt(_memoryDc, 0, 0, Width, Height, _desktopDc,
            _region.X, _region.Y, Win32Native.SourceCopy | Win32Native.CaptureBlt);
        if (!copied)
            throw LastError("Could not copy pixels from the selected screen region.");

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
        _ = Win32Native.SelectObject(_memoryDc, _previousBitmap);
        int lines;
        try
        {
            // GetDIBits requires the bitmap to be deselected from every memory DC.
            lines = Win32Native.GetDIBits(_desktopDc, _bitmap, 0, (uint)Height, bgra, ref header, Win32Native.DibRgbColors);
        }
        finally
        {
            _ = Win32Native.SelectObject(_memoryDc, _bitmap);
        }
        if (lines != Height)
            throw LastError("Could not read pixels from the screen capture bitmap.");

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
        if (_desktopDc != IntPtr.Zero)
            _ = Win32Native.ReleaseDC(IntPtr.Zero, _desktopDc);

        _previousBitmap = IntPtr.Zero;
        _bitmap = IntPtr.Zero;
        _memoryDc = IntPtr.Zero;
        _desktopDc = IntPtr.Zero;
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
            var x = cursorInfo.Position.X - _region.X - (int)iconInfo.HotspotX;
            var y = cursorInfo.Position.Y - _region.Y - (int)iconInfo.HotspotY;
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

    private static Win32Exception LastError(string message) => new(Marshal.GetLastWin32Error(), message);
}
