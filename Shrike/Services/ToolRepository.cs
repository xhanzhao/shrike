using System.Text.Json;
using Rhino;
using Shrike.Models;

namespace Shrike.Services;

public sealed class ToolRepository
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public string BundledToolsRoot => ToolPathService.GetBundledToolsRoot();
    public string UserToolsRoot => ToolPathService.GetUserToolsRoot();

    public IReadOnlyList<ToolDefinition> LoadTools()
    {
        var toolsById = new Dictionary<string, ToolDefinition>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var root in ToolPathService.GetToolSearchRoots())
            ScanRoot(root, toolsById);

        var ordered = toolsById.Values
            .OrderBy(tool => tool.Category)
            .ThenBy(tool => tool.Name)
            .ToList();

        RhinoApp.WriteLine($"Shrike: loaded {ordered.Count} tool(s).");
        RhinoApp.WriteLine($"Shrike bundled tools: {BundledToolsRoot}");
        RhinoApp.WriteLine($"Shrike user tools: {UserToolsRoot}");

        return ordered;
    }

    private void ScanRoot(
        string root,
        IDictionary<string, ToolDefinition> toolsById)
    {
        if (!Directory.Exists(root))
            return;

        string[] manifests;

        try
        {
            manifests = Directory.GetFiles(
                root,
                "tool.json",
                SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"Shrike: failed to scan '{root}'.");
            RhinoApp.WriteLine(ex.Message);
            return;
        }

        foreach (var manifestPath in manifests)
        {
            try
            {
                var json = File.ReadAllText(manifestPath);

                var tool = JsonSerializer.Deserialize<ToolDefinition>(
                    json,
                    _jsonOptions);

                if (tool is null)
                    continue;

                tool.ManifestPath = manifestPath;
                tool.ToolDirectory =
                    Path.GetDirectoryName(manifestPath) ?? string.Empty;

                Normalize(tool);

                if (!Validate(tool, out var error))
                {
                    RhinoApp.WriteLine(
                        $"Shrike: skipped '{manifestPath}': {error}");
                    continue;
                }

                // Bundled root is scanned first; user root second.
                // A user tool with the same id overrides the bundled one.
                toolsById[tool.Id] = tool;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine(
                    $"Shrike: failed to read '{manifestPath}'.");
                RhinoApp.WriteLine(ex.Message);
            }
        }
    }

    private static void Normalize(ToolDefinition tool)
    {
        if (string.IsNullOrWhiteSpace(tool.Id))
            tool.Id = new DirectoryInfo(tool.ToolDirectory).Name;

        if (string.IsNullOrWhiteSpace(tool.Name))
            tool.Name = tool.Id;

        if (string.IsNullOrWhiteSpace(tool.Category))
            tool.Category = "Uncategorized";

        if (string.IsNullOrWhiteSpace(tool.Type))
            tool.Type = "python3";

        if (string.IsNullOrWhiteSpace(tool.Entry))
            tool.Entry = "main.py";
    }

    private static bool Validate(
        ToolDefinition tool,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(tool.Id))
        {
            error = "Tool has no id.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            error = "Tool has no name.";
            return false;
        }

        var entryPath = Path.Combine(
            tool.ToolDirectory,
            tool.Entry);

        if (!File.Exists(entryPath))
        {
            error = $"Entry file was not found: {entryPath}";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
