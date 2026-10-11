using System.Threading.Channels;
using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.Services;

/// <summary>Transfers rendered sketchboard frames to the normal project writer.</summary>
public sealed class SketchboardRecordingSession : IRecordingSession
{
    private readonly Channel<CapturedFrame> _frames = Channel.CreateUnbounded<CapturedFrame>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly PixelSize _canvasSize;
    private int _state;

    public SketchboardRecordingSession(PixelSize canvasSize)
    {
        if (canvasSize.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(canvasSize), "Sketchboard canvas dimensions must be positive.");

        _canvasSize = canvasSize;
    }

    public bool IsPaused => Volatile.Read(ref _state) == 1;

    public bool IsStopped => Volatile.Read(ref _state) == 2;

    public bool PublishFrame(CapturedFrame frame)
    {
        if (Volatile.Read(ref _state) != 0)
            return false;

        var expectedLength = checked(_canvasSize.Width * _canvasSize.Height * 4);
        if (frame.Size != _canvasSize || frame.RgbaPixels.Length != expectedLength)
            throw new ArgumentException("Sketchboard frame size and RGBA buffer must match the configured canvas.", nameof(frame));
        if (frame.DurationMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(frame), "Frame duration must be positive.");

        // A caller may reuse its render buffer after publishing; the session owns each queued frame.
        return _frames.Writer.TryWrite(frame with { RgbaPixels = frame.RgbaPixels.ToArray() });
    }

    public void ReportFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Interlocked.Exchange(ref _state, 2);
        _frames.Writer.TryComplete(exception);
    }

    public async IAsyncEnumerable<CapturedFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _frames.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return frame;
    }

    public ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.CompareExchange(ref _state, 1, 0);
        return ValueTask.CompletedTask;
    }

    public ValueTask ResumeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.CompareExchange(ref _state, 0, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _state, 2) != 2)
            _frames.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
