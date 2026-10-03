using FrameStudio.Core.Projects;

namespace FrameStudio.Tests;

public sealed class RecentProjectCatalogTests
{
    [Fact]
    public async Task Catalog_PersistsUniqueRecentProjectsNewestFirstWithinLimit()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-recents-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var catalog = new RecentProjectCatalog(Path.Combine(directory, "settings", "recent-projects.json"));
            var paths = Enumerable.Range(0, RecentProjectCatalog.MaximumEntries + 2)
                .Select(index => Path.Combine(directory, $"capture-{index}.fsp"))
                .ToArray();
            foreach (var path in paths)
                await File.WriteAllTextAsync(path, "project placeholder");

            foreach (var path in paths)
                await catalog.AddOrUpdateAsync(path);

            var entries = await catalog.LoadAsync();
            Assert.Equal(RecentProjectCatalog.MaximumEntries, entries.Count);
            Assert.Equal(Path.GetFullPath(paths[^1]), entries[0].Path);
            Assert.Equal(Path.GetFullPath(paths[2]), entries[^1].Path);

            var updated = await catalog.AddOrUpdateAsync(paths[3]);
            Assert.Equal(Path.GetFullPath(paths[3]), updated[0].Path);
            Assert.Equal(1, updated.Count(entry => entry.Path == Path.GetFullPath(paths[3])));

            var reopenedCatalog = new RecentProjectCatalog(Path.Combine(directory, "settings", "recent-projects.json"));
            Assert.Equal(updated.Select(entry => entry.Path), (await reopenedCatalog.LoadAsync()).Select(entry => entry.Path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Catalog_DropsMissingFilesAndRejectsNonProjectPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-recents-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var catalog = new RecentProjectCatalog(Path.Combine(directory, "recent-projects.json"));
            var projectPath = Path.Combine(directory, "capture.fsp");
            var imagePath = Path.Combine(directory, "capture.png");
            await File.WriteAllTextAsync(projectPath, "project placeholder");
            await File.WriteAllTextAsync(imagePath, "not a project");

            await Assert.ThrowsAsync<ArgumentException>(() => catalog.AddOrUpdateAsync(imagePath));
            await catalog.AddOrUpdateAsync(projectPath);
            File.Delete(projectPath);

            Assert.Empty(await catalog.LoadAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
