using System.Text.Json;

namespace FrameStudio.Core.Projects;

public sealed record RecentProjectEntry(string Path, DateTimeOffset LastOpenedUtc);

/// <summary>Persists a small, validated list of recently opened Frame Studio projects.</summary>
public sealed class RecentProjectCatalog
{
    public const int MaximumEntries = 8;

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _catalogPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RecentProjectCatalog(string catalogPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        _catalogPath = Path.GetFullPath(catalogPath);
    }

    public async Task<IReadOnlyList<RecentProjectEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = await ReadStoredEntriesAsync(cancellationToken).ConfigureAwait(false);
            return Normalize(stored);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<RecentProjectEntry>> AddOrUpdateAsync(string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullPath = Path.GetFullPath(projectPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".fsp", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Recent projects must use the .fsp extension.", nameof(projectPath));
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The Frame Studio project could not be found.", fullPath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = Normalize(await ReadStoredEntriesAsync(cancellationToken).ConfigureAwait(false))
                .Where(entry => !PathComparer.Equals(entry.Path, fullPath))
                .ToList();
            entries.Insert(0, new RecentProjectEntry(fullPath, DateTimeOffset.UtcNow));
            var normalized = Normalize(entries).Take(MaximumEntries).ToArray();
            await WriteEntriesAsync(normalized, cancellationToken).ConfigureAwait(false);
            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<RecentProjectEntry>> ReadStoredEntriesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_catalogPath))
            return [];

        try
        {
            await using var stream = new FileStream(_catalogPath, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 4096, useAsync: true);
            return await JsonSerializer.DeserializeAsync<RecentProjectEntry[]>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? [];
        }
        catch (JsonException)
        {
            // A damaged preference file must not prevent the application from opening.
            return [];
        }
    }

    private static IReadOnlyList<RecentProjectEntry> Normalize(IEnumerable<RecentProjectEntry> entries)
    {
        var result = new List<RecentProjectEntry>();
        var seen = new HashSet<string>(PathComparer);
        foreach (var entry in entries.OrderByDescending(entry => entry.LastOpenedUtc))
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Path))
                continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(entry.Path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (!string.Equals(Path.GetExtension(fullPath), ".fsp", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(fullPath) || !seen.Add(fullPath))
                continue;

            result.Add(entry with { Path = fullPath });
            if (result.Count == MaximumEntries)
                break;
        }

        return result;
    }

    private async Task WriteEntriesAsync(IReadOnlyList<RecentProjectEntry> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_catalogPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_catalogPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _catalogPath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
