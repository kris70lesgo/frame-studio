using System.Diagnostics;
using System.Threading.Channels;
using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Platform.Windows;

internal sealed class WindowsRecordingSession : IRecordingSession
{
    private readonly Win32FrameGrabber _grabber;
    private readonly int _framesPerSecond;
    private readonly Channel<CapturedFrame> _frames = Channel.CreateBounded<CapturedFrame>(new BoundedChannelOptions(3)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly CancellationTokenSource _stop = new();
    private readonly object _stateLock = new();
    private TaskCompletionSource _resumeSignal = CompletedSignal();
    private readonly Task _captureTask;
    private bool _isPaused;
    private bool _isStopped;
    private int _resetTiming;
    private int _disposed;

    public WindowsRecordingSession(Win32FrameGrabber grabber, int framesPerSecond)
    {
        _grabber = grabber;
        _framesPerSecond = framesPerSecond;
        _captureTask = Task.Run(CaptureLoopAsync);
    }

    public IAsyncEnumerable<CapturedFrame> ReadFramesAsync(CancellationToken cancellationToken = default) =>
        _frames.Reader.ReadAllAsync(cancellationToken);

    public ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_stateLock)
        {
            if (_isStopped || _isPaused)
                return ValueTask.CompletedTask;

            _isPaused = true;
            _resumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask ResumeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource? signal = null;
        lock (_stateLock)
        {
            if (_isStopped || !_isPaused)
                return ValueTask.CompletedTask;

            _isPaused = false;
            Interlocked.Exchange(ref _resetTiming, 1);
            signal = _resumeSignal;
            _resumeSignal = CompletedSignal();
        }
        signal.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource? signal = null;
        lock (_stateLock)
        {
            if (!_isStopped)
            {
                _isStopped = true;
                signal = _resumeSignal;
            }
        }

        _stop.Cancel();
        signal?.TrySetResult();
        await _captureTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        await StopAsync().ConfigureAwait(false);
        _stop.Dispose();
    }

    private async Task CaptureLoopAsync()
    {
        Exception? failure = null;
        long previousCapture = 0;
        var intervalTicks = Stopwatch.Frequency / (double)_framesPerSecond;

        try
        {
            while (true)
            {
                await WaitUntilResumedAsync(_stop.Token).ConfigureAwait(false);
                if (Interlocked.Exchange(ref _resetTiming, 0) != 0)
                    previousCapture = 0;

                _stop.Token.ThrowIfCancellationRequested();
                if (previousCapture != 0)
                    await DelayUntilAsync(previousCapture + intervalTicks, _stop.Token).ConfigureAwait(false);

                await WaitUntilResumedAsync(_stop.Token).ConfigureAwait(false);
                if (Interlocked.Exchange(ref _resetTiming, 0) != 0)
                    previousCapture = 0;

                var captureTicks = Stopwatch.GetTimestamp();
                var capturedAt = DateTimeOffset.UtcNow;
                var rgba = _grabber.CaptureRgba();
                var duration = previousCapture == 0
                    ? Math.Max(1, (int)Math.Round(1000d / _framesPerSecond))
                    : Math.Clamp((int)Math.Round((captureTicks - previousCapture) * 1000d / Stopwatch.Frequency), 1, int.MaxValue);
                previousCapture = captureTicks;

                await _frames.Writer.WriteAsync(new CapturedFrame(
                    new PixelSize(_grabber.Width, _grabber.Height), rgba, duration, capturedAt), _stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) { failure = ex; }
        finally
        {
            _grabber.Dispose();
            _frames.Writer.TryComplete(failure);
        }
    }

    private async Task WaitUntilResumedAsync(CancellationToken cancellationToken)
    {
        Task wait;
        lock (_stateLock)
            wait = _isPaused ? _resumeSignal.Task : Task.CompletedTask;
        await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DelayUntilAsync(double targetTicks, CancellationToken cancellationToken)
    {
        while (true)
        {
            var remainingTicks = targetTicks - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
                return;

            var remainingMilliseconds = remainingTicks * 1000d / Stopwatch.Frequency;
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, remainingMilliseconds)), cancellationToken).ConfigureAwait(false);
        }
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }
}
