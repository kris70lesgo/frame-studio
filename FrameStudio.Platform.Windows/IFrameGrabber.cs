namespace FrameStudio.Platform.Windows;

internal interface IFrameGrabber : IDisposable
{
    int Width { get; }
    int Height { get; }
    byte[] CaptureRgba();
}
