using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameStudio.Core.Export;
using FrameStudio.Core.Models;
using FrameStudio.Core.Projects;

namespace FrameStudio.Avalonia.ViewModels;

public partial class TimelineFrameViewModel : ObservableObject
{
    [ObservableProperty]
    private int _frameNumber;

    [ObservableProperty]
    private int _durationMilliseconds;

    [ObservableProperty]
    private bool _isSelected;

    public int SourceFrameIndex { get; set; }
    public string FrameLabel => $"{FrameNumber + 1:00}";
    public string DurationLabel => $"{DurationMilliseconds} ms";

    partial void OnFrameNumberChanged(int value) => OnPropertyChanged(nameof(FrameLabel));
    partial void OnDurationMillisecondsChanged(int value) => OnPropertyChanged(nameof(DurationLabel));
}

public partial class EditorViewModel : ObservableObject
{
    private FrameProject _sourceProject;
    private readonly string _projectPath;

    public ObservableCollection<TimelineFrameViewModel> Frames { get; } = [];
    public string ProjectName => _sourceProject.Name;
    public string CanvasLabel => $"{_sourceProject.CanvasSize.Width:N0} × {_sourceProject.CanvasSize.Height:N0}";
    public PixelSize CanvasSize => _sourceProject.CanvasSize;
    public bool CanSave => IsDirty && !IsBusy;
    public bool CanEdit => !IsBusy;
    public bool CanMoveSelectedEarlier => SelectedFrame is not null && Frames.IndexOf(SelectedFrame) > 0 && !IsBusy;
    public bool CanMoveSelectedLater => SelectedFrame is not null && Frames.IndexOf(SelectedFrame) >= 0 && Frames.IndexOf(SelectedFrame) < Frames.Count - 1 && !IsBusy;

    [ObservableProperty]
    private TimelineFrameViewModel? _selectedFrame;

    [ObservableProperty]
    private string _durationText = "";

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isBusy;

    public EditorViewModel(string projectPath, FrameProject project)
    {
        _projectPath = projectPath;
        _sourceProject = project;
        foreach (var frame in project.Frames)
            Frames.Add(new TimelineFrameViewModel { FrameNumber = frame.Index, SourceFrameIndex = frame.Index, DurationMilliseconds = frame.DurationMilliseconds });
        SelectedFrame = Frames.FirstOrDefault();
    }

    public async ValueTask<byte[]> ReadSelectedFrameAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedFrame is null)
            throw new InvalidOperationException("No frame is selected.");

        return await FrameProjectArchiveReader.ReadFrameRgbaAsync(_projectPath, _sourceProject,
            SelectedFrame.SourceFrameIndex, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<byte[]> ReadFrameAsync(int sourceFrameIndex, CancellationToken cancellationToken = default)
    {
        if ((uint)sourceFrameIndex >= (uint)_sourceProject.Frames.Count)
            throw new ArgumentOutOfRangeException(nameof(sourceFrameIndex));

        return await FrameProjectArchiveReader.ReadFrameRgbaAsync(_projectPath, _sourceProject,
            sourceFrameIndex, cancellationToken).ConfigureAwait(false);
    }

    public void ReportStatus(string status) => Status = status;

    [RelayCommand]
    private void DeleteSelectedFrame()
    {
        if (SelectedFrame is null || Frames.Count <= 1 || IsBusy)
            return;

        var index = Frames.IndexOf(SelectedFrame);
        Frames.RemoveAt(index);
        RenumberFrames();
        SelectedFrame = Frames[Math.Min(index, Frames.Count - 1)];
        IsDirty = true;
        Status = $"Removed frame {index + 1}. Save to keep the change.";
        RaiseFrameSelectionState();
    }

    [RelayCommand]
    private void DuplicateSelectedFrame()
    {
        if (SelectedFrame is null || IsBusy || Frames.Count >= 100_000)
            return;

        var index = Frames.IndexOf(SelectedFrame);
        var copy = new TimelineFrameViewModel
        {
            FrameNumber = index + 1,
            SourceFrameIndex = SelectedFrame.SourceFrameIndex,
            DurationMilliseconds = SelectedFrame.DurationMilliseconds
        };
        Frames.Insert(index + 1, copy);
        RenumberFrames();
        SelectedFrame = copy;
        IsDirty = true;
        Status = $"Duplicated frame {index + 1}. Save to keep the change.";
        RaiseFrameSelectionState();
    }

    [RelayCommand]
    private void MoveSelectedFrameEarlier()
    {
        if (SelectedFrame is null || IsBusy)
            return;

        var index = Frames.IndexOf(SelectedFrame);
        if (index <= 0)
            return;

        Frames.Move(index, index - 1);
        RenumberFrames();
        IsDirty = true;
        Status = $"Moved frame {index + 1} earlier. Save to keep the change.";
        RaiseFrameSelectionState();
    }

    [RelayCommand]
    private void MoveSelectedFrameLater()
    {
        if (SelectedFrame is null || IsBusy)
            return;

        var index = Frames.IndexOf(SelectedFrame);
        if (index < 0 || index >= Frames.Count - 1)
            return;

        Frames.Move(index, index + 1);
        RenumberFrames();
        IsDirty = true;
        Status = $"Moved frame {index + 1} later. Save to keep the change.";
        RaiseFrameSelectionState();
    }

    [RelayCommand]
    private void ApplyDuration()
    {
        if (SelectedFrame is null || !int.TryParse(DurationText, out var duration) || duration is < 10 or > 655_350)
        {
            Status = "Frame duration must be between 10 and 655,350 ms.";
            return;
        }

        SelectedFrame.DurationMilliseconds = duration;
        IsDirty = true;
        Status = "Duration changed. Save to keep the change.";
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (!IsDirty || IsBusy)
            return;

        IsBusy = true;
        Status = "Saving project…";
        try
        {
            var references = Frames.Select(frame => new ProjectFrameReference(frame.SourceFrameIndex, frame.DurationMilliseconds)).ToArray();
            _sourceProject = await FrameProjectArchiveEditor.RewriteAsync(_projectPath, _projectPath, _sourceProject, references);
            foreach (var frame in Frames)
                frame.SourceFrameIndex = frame.FrameNumber;
            IsDirty = false;
            Status = "Project saved";
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExportGifAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Status = "Exporting GIF…";
        try
        {
            var references = Frames.Select(frame => new ProjectFrameReference(frame.SourceFrameIndex, frame.DurationMilliseconds)).ToArray();
            await new GifExportService().ExportSelectionAsync(_projectPath, destinationPath, references,
                new GifExportOptions(RepeatCount: 0), cancellationToken);
            Status = "GIF exported";
        }
        catch (Exception ex)
        {
            Status = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedFrameChanging(TimelineFrameViewModel? oldValue, TimelineFrameViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsSelected = false;
        if (newValue is not null)
            newValue.IsSelected = true;
    }

    partial void OnSelectedFrameChanged(TimelineFrameViewModel? value)
    {
        DurationText = value?.DurationMilliseconds.ToString() ?? "";
        OnPropertyChanged(nameof(SelectedFrameNumberLabel));
        RaiseFrameSelectionState();
    }

    partial void OnIsBusyChanged(bool value) => RaiseFrameSelectionState();

    public string SelectedFrameNumberLabel => SelectedFrame is null ? "No frame selected" : $"Frame {SelectedFrame.FrameNumber + 1} of {Frames.Count}";

    private void RenumberFrames()
    {
        for (var index = 0; index < Frames.Count; index++)
            Frames[index].FrameNumber = index;
        OnPropertyChanged(nameof(SelectedFrameNumberLabel));
    }

    private void RaiseFrameSelectionState()
    {
        OnPropertyChanged(nameof(CanMoveSelectedEarlier));
        OnPropertyChanged(nameof(CanMoveSelectedLater));
        OnPropertyChanged(nameof(SelectedFrameNumberLabel));
    }
}
