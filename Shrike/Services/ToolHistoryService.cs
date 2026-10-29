using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shrike.Models;

namespace Shrike.Services;

/// <summary>
/// Keeps snapshots of a tool's entry script inside the tool folder:
///
///     ToolFolder\.shrike\history\index.json
///     ToolFolder\.shrike\history\20261029-010637123-a1b2c3.py
///
/// Storing history next to the tool means it follows the tool when
/// it is renamed or moved, and is deleted together with the tool.
/// </summary>
public sealed class ToolHistoryService
{
    public const string InternalFolderName = ".shrike";

    private const string HistoryFolderName = "history";

    private const string IndexFileName = "index.json";

    private const int MaxEntries = 20;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    /// <summary>
    /// True when the path is inside a tool's private .shrike folder.
    /// Export, duplicate and the file watcher use this to skip it.
    /// </summary>
    public static bool IsInternalPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path
            .Split(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            .Any(
                segment =>
                    string.Equals(
                        segment,
                        InternalFolderName,
                        StringComparison.OrdinalIgnoreCase));
    }

    public static string GetHistoryDirectory(
        string toolDirectory)
    {
        return Path.Combine(
            toolDirectory,
            InternalFolderName,
            HistoryFolderName);
    }

    /// <summary>
    /// Returns snapshots, newest first.
    /// </summary>
    public IReadOnlyList<ToolHistoryEntry> GetEntries(
        ToolDefinition tool)
    {
        return LoadIndex(
                GetHistoryDirectory(
                    tool.ToolDirectory))
            .Where(
                entry =>
                    File.Exists(
                        GetEntryPath(
                            tool.ToolDirectory,
                            entry)))
            .OrderByDescending(
                entry =>
                    entry.CreatedUtc)
            .ToList();
    }

    public string ReadEntry(
        ToolDefinition tool,
        ToolHistoryEntry entry)
    {
        return File.ReadAllText(
            GetEntryPath(
                tool.ToolDirectory,
                entry));
    }

    /// <summary>
    /// Saves the current content of the entry script.
    ///
    /// Nothing is written when the script is missing, or when its
    /// content is identical to the newest snapshot.
    /// </summary>
    public ToolHistoryEntry? Snapshot(
        string toolDirectory,
        string entryScriptPath,
        ToolHistoryReason reason)
    {
        if (!File.Exists(entryScriptPath))
            return null;

        var content =
            File.ReadAllText(
                entryScriptPath);

        var hash =
            ComputeHash(content);

        var historyDirectory =
            GetHistoryDirectory(
                toolDirectory);

        var entries =
            LoadIndex(
                historyDirectory);

        var newest =
            entries
                .OrderByDescending(
                    entry =>
                        entry.CreatedUtc)
                .FirstOrDefault();

        if (newest is not null
            && string.Equals(
                newest.Hash,
                hash,
                StringComparison.OrdinalIgnoreCase)
            && File.Exists(
                GetEntryPath(
                    toolDirectory,
                    newest)))
        {
            return newest;
        }

        Directory.CreateDirectory(
            historyDirectory);

        var now =
            DateTime.UtcNow;

        var fileName =
            $"{now:yyyyMMdd-HHmmssfff}-{hash[..6]}.py";

        File.WriteAllText(
            Path.Combine(
                historyDirectory,
                fileName),
            content,
            new UTF8Encoding(false));

        var created =
            new ToolHistoryEntry
            {
                FileName = fileName,
                CreatedUtc = now,
                Reason = reason,
                LineCount = CountLines(content),
                Hash = hash
            };

        entries.Add(created);

        PruneAndSave(
            historyDirectory,
            entries);

        return created;
    }

    public ToolHistoryEntry? Snapshot(
        ToolDefinition tool,
        ToolHistoryReason reason)
    {
        return Snapshot(
            tool.ToolDirectory,
            GetEntryScriptPath(tool),
            reason);
    }

    public void SetNote(
        ToolDefinition tool,
        ToolHistoryEntry entry,
        string note)
    {
        var historyDirectory =
            GetHistoryDirectory(
                tool.ToolDirectory);

        var entries =
            LoadIndex(
                historyDirectory);

        var stored =
            entries.FirstOrDefault(
                candidate =>
                    string.Equals(
                        candidate.FileName,
                        entry.FileName,
                        StringComparison.OrdinalIgnoreCase));

        if (stored is null)
            return;

        stored.Note =
            (note ?? string.Empty).Trim();

        entry.Note =
            stored.Note;

        SaveIndex(
            historyDirectory,
            entries);
    }

    public static string GetEntryScriptPath(
        ToolDefinition tool)
    {
        return Path.GetFullPath(
            Path.Combine(
                tool.ToolDirectory,
                string.IsNullOrWhiteSpace(tool.Entry)
                    ? "main.py"
                    : tool.Entry));
    }

    public static string ComputeHash(
        string content)
    {
        var bytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    NormalizeNewLines(content)));

        return Convert
            .ToHexString(bytes)
            .ToLowerInvariant();
    }

    public static int CountLines(
        string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        return NormalizeNewLines(content)
            .TrimEnd('\n')
            .Split('\n')
            .Length;
    }

    private static string NormalizeNewLines(
        string content)
    {
        return content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
    }

    private static string GetEntryPath(
        string toolDirectory,
        ToolHistoryEntry entry)
    {
        return Path.Combine(
            GetHistoryDirectory(
                toolDirectory),
            Path.GetFileName(
                entry.FileName));
    }

    private void PruneAndSave(
        string historyDirectory,
        List<ToolHistoryEntry> entries)
    {
        var ordered =
            entries
                .OrderByDescending(
                    entry =>
                        entry.CreatedUtc)
                .ToList();

        foreach (var expired in
                 ordered.Skip(MaxEntries))
        {
            try
            {
                File.Delete(
                    Path.Combine(
                        historyDirectory,
                        Path.GetFileName(
                            expired.FileName)));
            }
            catch
            {
            }
        }

        SaveIndex(
            historyDirectory,
            ordered
                .Take(MaxEntries)
                .ToList());
    }

    private List<ToolHistoryEntry> LoadIndex(
        string historyDirectory)
    {
        var indexPath =
            Path.Combine(
                historyDirectory,
                IndexFileName);

        if (!File.Exists(indexPath))
            return new List<ToolHistoryEntry>();

        try
        {
            return JsonSerializer.Deserialize<List<ToolHistoryEntry>>(
                       File.ReadAllText(indexPath),
                       _jsonOptions)
                   ??
                   new List<ToolHistoryEntry>();
        }
        catch
        {
            // A damaged index should never block replacing a script.
            return new List<ToolHistoryEntry>();
        }
    }

    private void SaveIndex(
        string historyDirectory,
        List<ToolHistoryEntry> entries)
    {
        Directory.CreateDirectory(
            historyDirectory);

        File.WriteAllText(
            Path.Combine(
                historyDirectory,
                IndexFileName),
            JsonSerializer.Serialize(
                entries,
                _jsonOptions),
            new UTF8Encoding(false));
    }
}
