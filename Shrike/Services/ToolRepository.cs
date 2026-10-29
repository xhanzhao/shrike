using System.Text.Json;
using Rhino;
using Shrike.Models;

namespace Shrike.Services;

public sealed class ToolRepository
{
    private readonly JsonSerializerOptions
        _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling =
                    JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

    public string BundledToolsRoot =>
        ToolPathService.GetBundledToolsRoot();

    public string UserToolsRoot =>
        ToolPathService.GetUserToolsRoot();

    public IReadOnlyList<ToolDefinition>
        LoadTools()
    {
        var toolsById =
            new Dictionary<string, ToolDefinition>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var root in
                 ToolPathService.GetToolSearchRoots())
        {
            ScanRoot(
                root,
                toolsById);
        }

        var ordered =
            toolsById.Values
                .OrderBy(
                    tool =>
                        tool.Category)
                .ThenBy(
                    tool =>
                        tool.Name)
                .ToList();

        RhinoApp.WriteLine(
            $"Shrike: loaded {ordered.Count} tool(s).");

        RhinoApp.WriteLine(
            $"Shrike bundled tools: {BundledToolsRoot}");

        RhinoApp.WriteLine(
            $"Shrike user tools: {UserToolsRoot}");

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
            manifests =
                Directory.GetFiles(
                    root,
                    "tool.json",
                    SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine(
                $"Shrike: failed to scan '{root}'.");

            RhinoApp.WriteLine(
                ex.Message);

            return;
        }

        foreach (var manifestPath in
                 manifests)
        {
            try
            {
                var json =
                    File.ReadAllText(
                        manifestPath);

                var tool =
                    JsonSerializer.Deserialize<ToolDefinition>(
                        json,
                        _jsonOptions);

                if (tool is null)
                    continue;

                tool.ManifestPath =
                    manifestPath;

                tool.ToolDirectory =
                    Path.GetDirectoryName(
                        manifestPath)
                    ??
                    string.Empty;

                Normalize(
                    tool);

                if (!Validate(
                        tool,
                        out var error))
                {
                    RhinoApp.WriteLine(
                        $"Shrike: skipped '{manifestPath}': {error}");

                    continue;
                }

                // Bundled root is scanned first and user root second.
                // A user tool with the same id overrides the bundled tool.
                toolsById[
                    tool.Id] =
                    tool;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine(
                    $"Shrike: failed to read '{manifestPath}'.");

                RhinoApp.WriteLine(
                    ex.Message);
            }
        }
    }

    private static void Normalize(
        ToolDefinition tool)
    {
        if (string.IsNullOrWhiteSpace(
                tool.Id))
        {
            tool.Id =
                new DirectoryInfo(
                    tool.ToolDirectory)
                .Name;
        }

        if (string.IsNullOrWhiteSpace(
                tool.Name))
        {
            tool.Name =
                tool.Id;
        }

        if (string.IsNullOrWhiteSpace(
                tool.Category))
        {
            tool.Category =
                "Uncategorized";
        }

        if (string.IsNullOrWhiteSpace(
                tool.Type))
        {
            tool.Type =
                "python3";
        }

        if (IsPythonTool(
                tool)
            &&
            string.IsNullOrWhiteSpace(
                tool.Entry))
        {
            tool.Entry =
                "main.py";
        }
    }

    private static bool Validate(
        ToolDefinition tool,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(
                tool.Id))
        {
            error =
                "Tool has no id.";

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                tool.Name))
        {
            error =
                "Tool has no name.";

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                tool.ToolDirectory))
        {
            error =
                "Tool directory is invalid.";

            return false;
        }

        if (IsPythonTool(
                tool))
        {
            var entry =
                string.IsNullOrWhiteSpace(
                    tool.Entry)
                    ? "main.py"
                    : tool.Entry;

            var entryPath =
                Path.Combine(
                    tool.ToolDirectory,
                    entry);

            if (!File.Exists(
                    entryPath))
            {
                error =
                    $"Python entry file was not found: {entryPath}";

                return false;
            }

            error =
                string.Empty;

            return true;
        }

        if (IsMacroTool(
                tool))
        {
            error =
                "Rhino Macro tools are no longer supported.";

            return false;
        }

        error =
            $"Unsupported tool type: {tool.Type}";

        return false;
    }

    private static bool IsPythonTool(
        ToolDefinition tool)
    {
        return string.Equals(
                   tool.Type,
                   "python3",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   tool.Type,
                   "python",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMacroTool(
        ToolDefinition tool)
    {
        return string.Equals(
                   tool.Type,
                   "macro",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   tool.Type,
                   "rhino-macro",
                   StringComparison.OrdinalIgnoreCase);
    }
}
