using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.ViewModels;

public partial class RecordingViewModel : ObservableObject
{
    private readonly IRecordingSession _session;
    private readonly FrameProjectArchiveWriter _writer;
    private readonly string _destinationPath;
    private readonly Task _consumerTask;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly DispatcherTimer _elapsedTimer;
    private int _capturedFrameCount;
    private bool _hasFinished;
    private Exception? _captureFailure;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isStopping;

    [ObservableProperty]
    private bool _canControlRecording = true;

    [ObservableProperty]
    private bool _canPauseRecording = true;

    [ObservableProperty]
    private int _frameCount;

    [ObservableProperty]
    private string _elapsedLabel = "00:00";

    [ObservableProperty]
    private string _status = "Recording screen region";

    [ObservableProperty]
    private string _pauseResumeLabel = "Pause";

    public event EventHandler<string>? RecordingCompleted;

    public RecordingViewModel(IRecordingSession session, FrameProjectArchiveWriter writer, string destinationPath, int framesPerSecond)
    {
        _session = session;
        _writer = writer;
        _destinationPath = destinationPath;
        FrameRateLabel = $"{framesPerSecond} FPS";
        _consumerTask = Task.Run(ConsumeFramesAsync);

        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _elapsedTimer.Tick += (_, _) => ElapsedLabel = _elapsed.Elapsed.ToString(@"mm\:ss");
        _elapsedTimer.Start();
    }

    public string FrameRateLabel { get; }

    [RelayCommand]
    private async Task TogglePauseAsync()
    {
        if (IsStopping || _hasFinished || !CanPauseRecording)
            return;

        if (IsPaused)
        {
            await _session.ResumeAsync();
            _elapsed.Start();
            IsPaused = false;
            PauseResumeLabel = "Pause";
            Status = "Recording screen region";
        }
        else
        {
            await _session.PauseAsync();
            _elapsed.Stop();
            IsPaused = true;
            PauseResumeLabel = "Resume";
            Status = "Recording paused";
        }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        if (IsStopping || _hasFinished)
            return;

        IsStopping = true;
        CanControlRecording = false;
        CanPauseRecording = false;
        Status = "Finishing captured frames…";
        _elapsedTimer.Stop();
        _elapsed.Stop();
        try
        {
            await _session.StopAsync();
            await _consumerTask;

            if (_captureFailure is { } captureFailure)
            {
                if (Volatile.Read(ref _capturedFrameCount) == 0)
                {
                    _hasFinished = true;
                    Status = $"Capture failed before a frame was saved: {captureFailure.Message}";
                    IsStopping = false;
                    return;
                }

                await _writer.CompleteAsync();
                _hasFinished = true;
                var savedFrameCount = Volatile.Read(ref _capturedFrameCount);
                var frameNoun = savedFrameCount == 1 ? "frame" : "frames";
                Status = $"Capture stopped: {captureFailure.Message}. Saved {savedFrameCount} {frameNoun}.";
                RecordingCompleted?.Invoke(this, _destinationPath);
                return;
            }

            await _writer.CompleteAsync();
            _hasFinished = true;
            Status = "Recording saved";
            RecordingCompleted?.Invoke(this, _destinationPath);
        }
        catch (Exception ex)
        {
            Status = $"Could not save recording: {ex.Message}";
            _hasFinished = true;
            IsStopping = false;
        }
        finally
        {
            await _session.DisposeAsync();
            await _writer.DisposeAsync();
        }
    }

    public bool HasFinished => _hasFinished;

    private async Task ConsumeFramesAsync()
    {
        var frames = _session.ReadFramesAsync().GetAsyncEnumerator();
        try
        {
            while (true)
            {
                bool hasFrame;
                try
                {
                    hasFrame = await frames.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _captureFailure = ex;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_hasFinished || IsStopping)
                            return;

                        _elapsed.Stop();
                        _elapsedTimer.Stop();
                        CanPauseRecording = false;
                        Status = "Capture stopped unexpectedly. Stop to save the frames captured so far.";
                    });
                    return;
                }

                if (!hasFrame)
                    return;

                var frame = frames.Current;
                await _writer.WriteFrameAsync(frame.Size, frame.RgbaPixels, frame.DurationMilliseconds).ConfigureAwait(false);
                var frameCount = Interlocked.Increment(ref _capturedFrameCount);
                Dispatcher.UIThread.Post(() => FrameCount = frameCount);
            }
        }
        finally
        {
            await frames.DisposeAsync().ConfigureAwait(false);
        }
    }
}
