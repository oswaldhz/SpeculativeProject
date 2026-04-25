using System.Text.Json;

namespace TextSpeculator.App;

internal sealed class CorpusHistoryStore
{
    private const int MaxEntries = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _historyPath;

    public CorpusHistoryStore(string? historyPath = null)
    {
        _historyPath = historyPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TextSpeculator",
            "corpus-history.json");
    }

    public IReadOnlyList<CorpusHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_historyPath))
                return Array.Empty<CorpusHistoryEntry>();

            var json = File.ReadAllText(_historyPath);
            var entries = JsonSerializer.Deserialize<List<CorpusHistoryEntry>>(json, JsonOptions) ?? new List<CorpusHistoryEntry>();

            return entries
                .Select(NormalizeEntry)
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .OrderByDescending(entry => entry.LastUsedUtc)
                .Take(MaxEntries)
                .ToList();
        }
        catch
        {
            return Array.Empty<CorpusHistoryEntry>();
        }
    }

    public void Save(IEnumerable<CorpusHistoryEntry> entries)
    {
        try
        {
            var normalizedEntries = entries
                .Select(NormalizeEntry)
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .OrderByDescending(entry => entry.LastUsedUtc)
                .Take(MaxEntries)
                .ToList();

            var directory = Path.GetDirectoryName(_historyPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(normalizedEntries, JsonOptions);
            File.WriteAllText(_historyPath, json);
        }
        catch
        {
            // Keep history persistence best-effort so the editor never fails because
            // a local history file could not be written.
        }
    }

    public List<CorpusHistoryEntry> Upsert(IReadOnlyList<CorpusHistoryEntry> existingEntries, IReadOnlyList<string> paths)
    {
        var normalizedPaths = NormalizePaths(paths);
        if (normalizedPaths.Count == 0)
            return existingEntries.ToList();

        var updatedEntries = existingEntries
            .Where(entry => !PathsMatch(entry.Paths, normalizedPaths))
            .ToList();

        updatedEntries.Insert(0, CreateEntry(normalizedPaths, DateTimeOffset.UtcNow));

        return updatedEntries
            .OrderByDescending(entry => entry.LastUsedUtc)
            .Take(MaxEntries)
            .ToList();
    }

    public static List<string> NormalizePaths(IEnumerable<string> paths)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path.Trim());
            }
            catch
            {
                continue;
            }

            if (seen.Add(fullPath))
                normalized.Add(fullPath);
        }

        return normalized;
    }

    public static bool PathsMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return string.Equals(BuildKey(left), BuildKey(right), StringComparison.OrdinalIgnoreCase);
    }

    private static CorpusHistoryEntry CreateEntry(IReadOnlyList<string> normalizedPaths, DateTimeOffset lastUsedUtc)
    {
        var displayName = BuildDisplayName(normalizedPaths);
        return new CorpusHistoryEntry(displayName, normalizedPaths.ToArray(), lastUsedUtc, normalizedPaths.Count);
    }

    private static CorpusHistoryEntry? NormalizeEntry(CorpusHistoryEntry entry)
    {
        var normalizedPaths = NormalizePaths(entry.Paths);
        if (normalizedPaths.Count == 0)
            return null;

        return CreateEntry(normalizedPaths, entry.LastUsedUtc);
    }

    private static string BuildDisplayName(IReadOnlyList<string> paths)
    {
        var primaryName = Path.GetFileName(paths[0]);
        return paths.Count == 1
            ? primaryName
            : $"{primaryName} +{paths.Count - 1}";
    }

    private static string BuildKey(IEnumerable<string> paths)
    {
        return string.Join(
            "|",
            NormalizePaths(paths)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
    }
}

internal sealed record CorpusHistoryEntry(
    string DisplayName,
    string[] Paths,
    DateTimeOffset LastUsedUtc,
    int DocumentCount);
