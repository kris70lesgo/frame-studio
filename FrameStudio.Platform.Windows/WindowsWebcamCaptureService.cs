#if WINDOWS
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using CoreCapturedFrame = FrameStudio.Platform.Abstractions.CapturedFrame;
using WinMediaStreamType = global::Windows.Media.Capture.MediaStreamType;

namespace FrameStudio.Platform.Windows;

/// <summary>Captures color frames from Windows camera sources through MediaCapture.</summary>
public sealed class WindowsWebcamCaptureService : IWebcamService
{
    private const int CameraFrameTimeoutSeconds = 12;

    public async ValueTask<IReadOnlyList<WebcamDeviceDescriptor>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();

        var groups = await MediaFrameSourceGroup.FindAllAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return groups
            .Where(HasColorVideoSource)
            .Select(group => new WebcamDeviceDescriptor(group.Id, group.DisplayName))
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async ValueTask<IWebcamRecordingSession> StartAsync(
        string deviceId,
        int framesPerSecond,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (framesPerSecond is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "Camera frame rate must be between 1 and 120 FPS.");
        cancellationToken.ThrowIfCancellationRequested();

        var groups = await MediaFrameSourceGroup.FindAllAsync();
        var selectedGroup = groups.FirstOrDefault(group =>
            string.Equals(group.Id, deviceId, StringComparison.Ordinal) && HasColorVideoSource(group));
        if (selectedGroup is null)
            throw new InvalidOperationException("The selected camera is no longer available. Refresh the device list and try again.");

        var capture = new MediaCapture();
        MediaFrameReader? reader = null;
        WindowsWebcamRecordingSession? session = null;
        try
        {
            var settings = new MediaCaptureInitializationSettings
            {
                SourceGroup = selectedGroup,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };

            try
            {
                await capture.InitializeAsync(settings);
            }
            catch (Exception ex) when (IsCameraAccessDenied(ex))
            {
                throw new InvalidOperationException(
                    "Windows blocked camera access. Enable Settings > Privacy & security > Camera > Let desktop apps access your camera, then try again.", ex);
            }

            var colorSource = capture.FrameSources.Values.FirstOrDefault(source =>
                source.Info.SourceKind == MediaFrameSourceKind.Color && IsVideoStream(source.Info.MediaStreamType));
            if (colorSource is null)
                throw new InvalidOperationException("The selected camera does not expose a color video stream.");

            reader = await capture.CreateFrameReaderAsync(colorSource);
            session = new WindowsWebcamRecordingSession(capture, reader, framesPerSecond);
            capture = null!;
            reader = null;
            await session.StartAndWaitForFrameAsync(cancellationToken);
            return session;
        }
        catch
        {
            if (session is not null)
                await session.DisposeAsync();
            else
            {
                if (reader is not null)
                    reader.Dispose();
                capture?.Dispose();
            }
            throw;
        }
    }

    private static bool HasColorVideoSource(MediaFrameSourceGroup group) =>
        group.SourceInfos.Any(info =>
            info.SourceKind == MediaFrameSourceKind.Color && IsVideoStream(info.MediaStreamType));

    private static bool IsVideoStream(WinMediaStreamType streamType) =>
        streamType is WinMediaStreamType.VideoPreview or WinMediaStreamType.VideoRecord;

    private static bool IsCameraAccessDenied(Exception exception) =>
        exception is UnauthorizedAccessException ||
        exception is COMException { HResult: unchecked((int)0x80070005) };

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Webcam capture is available only on Windows.");
    }

    private sealed class WindowsWebcamRecordingSession : IWebcamRecordingSession
    {
        private readonly MediaCapture _capture;
        private readonly MediaFrameReader _reader;
        private readonly int _framesPerSecond;
        private readonly long _frameIntervalTicks;
        private readonly Channel<CoreCapturedFrame> _frames = Channel.CreateBounded<CoreCapturedFrame>(
            new BoundedChannelOptions(3)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
        private readonly TaskCompletionSource<PixelSize> _firstFrame =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _timingLock = new();
        private int _state;
        private int _processingFrame;
        private int _stopStarted;
        private long _lastFrameTimestamp;
        private long _nextFrameTimestamp;
        private PixelSize _frameSize;

        public event Action<CoreCapturedFrame>? FrameCaptured;

        public WindowsWebcamRecordingSession(MediaCapture capture, MediaFrameReader reader, int framesPerSecond)
        {
            _capture = capture;
            _reader = reader;
            _framesPerSecond = framesPerSecond;
            _frameIntervalTicks = Math.Max(1, Stopwatch.Frequency / framesPerSecond);
            _reader.FrameArrived += Reader_OnFrameArrived;
        }

        public PixelSize FrameSize => _frameSize;

        public bool IsPaused => Volatile.Read(ref _state) == 1;

        public bool IsStopped => Volatile.Read(ref _state) == 2;

        public async Task StartAndWaitForFrameAsync(CancellationToken cancellationToken)
        {
            var startStatus = await _reader.StartAsync();
            if (startStatus != MediaFrameReaderStartStatus.Success)
                throw new InvalidOperationException($"Windows could not start the camera frame reader ({startStatus}).");

            await _firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(CameraFrameTimeoutSeconds), cancellationToken);
        }

        public async IAsyncEnumerable<CoreCapturedFrame> ReadFramesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var frame in _frames.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                yield return frame;
        }

        public ValueTask PauseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
            {
                lock (_timingLock)
                {
                    _lastFrameTimestamp = 0;
                    _nextFrameTimestamp = 0;
                }
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask ResumeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.CompareExchange(ref _state, 0, 1);
            return ValueTask.CompletedTask;
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Exchange(ref _stopStarted, 1) != 0)
                return;

            Interlocked.Exchange(ref _state, 2);

            _reader.FrameArrived -= Reader_OnFrameArrived;
            try
            {
                await _reader.StopAsync();
            }
            finally
            {
                _frames.Writer.TryComplete();
                _firstFrame.TrySetException(new OperationCanceledException("Camera recording stopped before a frame arrived."));
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync();
            }
            finally
            {
                _reader.Dispose();
                _capture.Dispose();
            }
        }

        private void Reader_OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            if (Volatile.Read(ref _state) != 0 || Interlocked.Exchange(ref _processingFrame, 1) != 0)
                return;

            try
            {
                var timestamp = Stopwatch.GetTimestamp();
                int durationMilliseconds;
                long previousPublishedTimestamp;
                lock (_timingLock)
                {
                    if (_state != 0 || (_nextFrameTimestamp != 0 && timestamp < _nextFrameTimestamp))
                        return;

                    _nextFrameTimestamp = timestamp + _frameIntervalTicks;
                    previousPublishedTimestamp = _lastFrameTimestamp;
                    durationMilliseconds = previousPublishedTimestamp == 0
                        ? Math.Max(1, (int)Math.Round(1000d / _framesPerSecond))
                        : Math.Clamp((int)Math.Round(Stopwatch.GetElapsedTime(previousPublishedTimestamp, timestamp).TotalMilliseconds), 1, 60_000);
                }

                using var frameReference = sender.TryAcquireLatestFrame();
                var sourceBitmap = frameReference?.VideoMediaFrame?.SoftwareBitmap;
                if (sourceBitmap is null)
                    return;

                using var bitmap = SoftwareBitmap.Convert(sourceBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
                var frameSize = new PixelSize(bitmap.PixelWidth, bitmap.PixelHeight);
                var rgbaPixels = CopyBitmapAsRgba(bitmap);
                _frameSize = frameSize;

                var capturedFrame = new CoreCapturedFrame(frameSize, rgbaPixels, durationMilliseconds, DateTimeOffset.UtcNow);
                if (_frames.Writer.TryWrite(capturedFrame))
                {
                    lock (_timingLock)
                        _lastFrameTimestamp = timestamp;
                    _firstFrame.TrySetResult(frameSize);
                    NotifyFrameCaptured(capturedFrame);
                }
            }
            catch (Exception ex)
            {
                _firstFrame.TrySetException(ex);
                Interlocked.Exchange(ref _state, 2);
                _frames.Writer.TryComplete(ex);
            }
            finally
            {
                Volatile.Write(ref _processingFrame, 0);
            }
        }

        private static byte[] CopyBitmapAsRgba(SoftwareBitmap bitmap)
        {
            using var pixelBuffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
            using var bufferReference = pixelBuffer.CreateReference();
            ((IMemoryBufferByteAccess)(object)bufferReference).GetBuffer(out var data, out var capacity);

            var plane = pixelBuffer.GetPlaneDescription(0);
            return WebcamFrameConverter.CopyBgraToRgba(data, capacity,
                new PixelSize(bitmap.PixelWidth, bitmap.PixelHeight), plane.Stride, checked((int)plane.StartIndex));
        }

        private void NotifyFrameCaptured(CoreCapturedFrame frame)
        {
            var handlers = FrameCaptured;
            if (handlers is null)
                return;

            foreach (Action<CoreCapturedFrame> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(frame);
                }
                catch
                {
                    // A preview subscriber must not stop the camera recording stream.
                }
            }
        }
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMemoryBufferByteAccess
    {
        void GetBuffer(out IntPtr buffer, out uint capacity);
    }
}
#else
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Platform.Windows;

/// <summary>Windows Runtime webcam capture is included only in the Windows-targeted application.</summary>
public sealed class WindowsWebcamCaptureService : IWebcamService
{
    public ValueTask<IReadOnlyList<WebcamDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<WebcamDeviceDescriptor>>([]);

    public ValueTask<IWebcamRecordingSession> StartAsync(
        string deviceId,
        int framesPerSecond,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<IWebcamRecordingSession>(
            new PlatformNotSupportedException("Windows Runtime webcam capture requires the Windows-targeted application build."));
}
#endif
