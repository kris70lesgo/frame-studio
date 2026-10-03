using FrameStudio.Core.Models;

namespace FrameStudio.Avalonia.ViewModels;

public sealed record RecentProjectViewModel(string Path, string Name, string Details, string OpenedLabel)
{
    public static RecentProjectViewModel From(string path, FrameProject project, DateTimeOffset lastOpenedUtc)
    {
        var openedLocal = lastOpenedUtc.ToLocalTime();
        var openedLabel = openedLocal.Date == DateTime.Today
            ? $"Opened today at {openedLocal:HH:mm}"
            : $"Opened {openedLocal:MMM d, yyyy}";

        return new RecentProjectViewModel(path, project.Name,
            $"{project.CanvasSize.Width:N0} × {project.CanvasSize.Height:N0} · {project.Frames.Count:N0} frames",
            openedLabel);
    }
}
