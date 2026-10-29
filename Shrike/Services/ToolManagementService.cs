using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Rhino;
using Shrike.Models;

namespace Shrike.Services;

public sealed class ToolManagementService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ToolHistoryService _historyService = new();

    public ToolHistoryService History =>
        _historyService;

    public bool CanModify(ToolDefinition tool)
    {
        if (tool is null
            || string.IsNullOrWhiteSpace(tool.ToolDirectory))
        {
            return false;
        }

        var userRoot =
            EnsureTrailingSeparator(
                Path.GetFullPath(
                    ToolPathService.GetUserToolsRoot()));

        var toolPath =
            EnsureTrailingSeparator(
                Path.GetFullPath(
                    tool.ToolDirectory));

        return toolPath.StartsWith(
            userRoot,
            StringComparison.OrdinalIgnoreCase);
    }


    public bool CanRenameCategory(
        IEnumerable<ToolDefinition> tools)
    {
        var categoryTools =
            tools?.ToList()
            ??
            new List<ToolDefinition>();

        return categoryTools.Count > 0
               &&
               categoryTools.All(
                   CanModify);
    }

    public bool MoveToolToCategory(
        ToolDefinition tool,
        string newCategory,
        out string error)
    {
        error = string.Empty;

        if (!CanModify(tool))
        {
            error =
                "Bundled tools cannot be moved to another category.";

            return false;
        }

        return MoveToolsToCategory(
            new[] { tool },
            newCategory,
            out error);
    }

    public bool RenameCategory(
        IEnumerable<ToolDefinition> tools,
        string newCategory,
        out string error)
    {
        error = string.Empty;

        var categoryTools =
            tools?.ToList()
            ??
            new List<ToolDefinition>();

        if (categoryTools.Count == 0)
        {
            error =
                "No tools were found in this category.";

            return false;
        }

        if (!CanRenameCategory(
                categoryTools))
        {
            error =
                "This category contains bundled tools and cannot be renamed.";

            return false;
        }

        var sourceCategories =
            categoryTools
                .Select(
                    tool =>
                        tool.Category)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (sourceCategories.Count != 1)
        {
            error =
                "The selected tools do not belong to one category.";

            return false;
        }

        return MoveToolsToCategory(
            categoryTools,
            newCategory,
            out error);
    }

    public bool EditScript(
        ToolDefinition tool,
        out string error)
    {
        error = string.Empty;

        var scriptPath =
            GetEntryPath(tool);

        if (!File.Exists(scriptPath))
        {
            error =
                $"Script file does not exist:\n\n{scriptPath}";

            return false;
        }

        try
        {
            var safePath =
                scriptPath.Replace(
                    "\"",
                    "\"\"");

            var macro =
                $"_-ScriptEditor _O \"{safePath}\"";

            var opened =
                RhinoApp.RunScript(
                    macro,
                    echo: false);

            if (!opened)
            {
                error =
                    "Rhino could not open the script in Script Editor.";

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error =
                "Shrike could not open the script in Rhino Script Editor.\n\n" +
                ex.Message;

            return false;
        }
    }

    /// <summary>
    /// Replaces the actual entry .py file of a user-created tool.
    ///
    /// Tool metadata is intentionally untouched:
    /// id / name / category / icon / tooltip / Favorites remain the same.
    /// </summary>
    public bool ReplaceScript(
        ToolDefinition tool,
        string sourcePythonPath,
        out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(sourcePythonPath)
            || !File.Exists(sourcePythonPath))
        {
            error = "Select a valid .py file.";
            return false;
        }

        if (!string.Equals(
                Path.GetExtension(sourcePythonPath),
                ".py",
                StringComparison.OrdinalIgnoreCase))
        {
            error = "The replacement file must be a .py file.";
            return false;
        }

        // Selecting the current script itself should simply succeed.
        if (string.Equals(
                Path.GetFullPath(sourcePythonPath),
                GetEntryPath(tool),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string source;

        try
        {
            source =
                File.ReadAllText(sourcePythonPath);
        }
        catch (Exception ex)
        {
            error =
                "Shrike could not read the Python script.\n\n" +
                ex.Message;

            return false;
        }

        return ReplaceScriptContent(
            tool,
            source,
            ToolHistoryReason.BeforeReplaceFromFile,
            out error);
    }

    /// <summary>
    /// Replaces the entry .py file with the given source code.
    ///
    /// The current script is saved to the tool's version history
    /// first, so every replacement can be rolled back.
    /// </summary>
    public bool ReplaceScriptContent(
        ToolDefinition tool,
        string source,
        ToolHistoryReason reason,
        out string error)
    {
        error = string.Empty;

        if (!CanModify(tool))
        {
            error =
                "Bundled tools are read-only. " +
                "Only user-created tools can replace their script.";

            return false;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "The replacement script is empty.";
            return false;
        }

        try
        {
            var processed =
                PreparePython3Script(source);

            _historyService.Snapshot(
                tool,
                reason);

            WriteEntryScript(
                tool,
                processed);

            return true;
        }
        catch (Exception ex)
        {
            error =
                "Shrike could not replace the Python script.\n\n" +
                ex.Message;

            return false;
        }
    }

    /// <summary>
    /// Restores a version from the tool's history.
    /// The current script is saved first, so a rollback can be undone.
    /// </summary>
    public bool RollbackScript(
        ToolDefinition tool,
        ToolHistoryEntry entry,
        out string error)
    {
        error = string.Empty;

        string source;

        try
        {
            source =
                _historyService.ReadEntry(
                    tool,
                    entry);
        }
        catch (Exception ex)
        {
            error =
                "Shrike could not read this version.\n\n" +
                ex.Message;

            return false;
        }

        return ReplaceScriptContent(
            tool,
            source,
            ToolHistoryReason.BeforeRollback,
            out error);
    }

    private static void WriteEntryScript(
        ToolDefinition tool,
        string content)
    {
        var destinationFullPath =
            GetEntryPath(tool);

        var destinationDirectory =
            Path.GetDirectoryName(destinationFullPath);

        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException(
                "Shrike could not determine the script destination folder.");
        }

        Directory.CreateDirectory(
            destinationDirectory);

        // Write to a temporary file first so the existing main.py
        // is not damaged if writing the replacement fails.
        var tempPath =
            Path.Combine(
                destinationDirectory,
                $".Shrike_replace_{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(
                tempPath,
                content,
                new UTF8Encoding(false));

            File.Copy(
                tempPath,
                destinationFullPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }
    }

    public ToolCreationResult DuplicateTool(
        ToolDefinition sourceTool,
        string newName,
        string category)
    {
        newName =
            (newName ?? string.Empty).Trim();

        category =
            (category ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            return ToolCreationResult.Fail(
                "Tool name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return ToolCreationResult.Fail(
                "Category cannot be empty.");
        }

        if (sourceTool is null
            || string.IsNullOrWhiteSpace(
                sourceTool.ToolDirectory)
            || !Directory.Exists(
                sourceTool.ToolDirectory))
        {
            return ToolCreationResult.Fail(
                "Source tool folder does not exist.");
        }

        var categoryFolder =
            MakeSafeFolderName(category);

        var toolFolder =
            MakeSafeFolderName(newName);

        if (string.IsNullOrWhiteSpace(categoryFolder)
            || string.IsNullOrWhiteSpace(toolFolder))
        {
            return ToolCreationResult.Fail(
                "The tool name or category cannot be converted " +
                "to a valid folder name.");
        }

        var destination =
            Path.Combine(
                ToolPathService.GetUserToolsRoot(),
                categoryFolder,
                toolFolder);

        if (Directory.Exists(destination)
            && Directory.EnumerateFileSystemEntries(
                    destination)
                .Any())
        {
            return ToolCreationResult.Fail(
                "A tool folder with this name already exists:\n\n" +
                destination);
        }

        try
        {
            CopyDirectory(
                sourceTool.ToolDirectory,
                destination);

            var copiedManifestPath =
                Path.Combine(
                    destination,
                    "tool.json");

            UpdateDuplicatedManifest(
                copiedManifestPath,
                sourceTool,
                newName,
                category);

            return ToolCreationResult.Ok(
                $"Duplicated '{sourceTool.Name}' as '{newName}'.",
                destination);
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(destination))
                {
                    Directory.Delete(
                        destination,
                        recursive: true);
                }
            }
            catch
            {
            }

            return ToolCreationResult.Fail(
                "Shrike could not duplicate the tool.\n\n" +
                ex.Message);
        }
    }

    public bool RenameTool(
        ToolDefinition tool,
        string newName,
        out string error)
    {
        error = string.Empty;

        if (!CanModify(tool))
        {
            error = "Bundled tools cannot be renamed.";
            return false;
        }

        newName = (newName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            error = "Tool name cannot be empty.";
            return false;
        }

        try
        {
            tool.Name = newName;
            SaveManifest(tool);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool ChangeIcon(
        ToolDefinition tool,
        string sourceIconPath,
        out string error)
    {
        error = string.Empty;

        if (!CanModify(tool))
        {
            error = "Bundled tools cannot be modified.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourceIconPath)
            || !File.Exists(sourceIconPath))
        {
            error = "Select a valid PNG file.";
            return false;
        }

        if (!string.Equals(
                Path.GetExtension(sourceIconPath),
                ".png",
                StringComparison.OrdinalIgnoreCase))
        {
            error = "Tool icons must be PNG files.";
            return false;
        }

        try
        {
            var destination =
                Path.Combine(
                    tool.ToolDirectory,
                    "icon.png");

            var sourceFullPath =
                Path.GetFullPath(
                    sourceIconPath);

            var destinationFullPath =
                Path.GetFullPath(
                    destination);

            if (!string.Equals(
                    sourceFullPath,
                    destinationFullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(
                    sourceFullPath,
                    destinationFullPath,
                    overwrite: true);
            }

            tool.Icon = "icon.png";
            SaveManifest(tool);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool DeleteTool(
        ToolDefinition tool,
        out string error)
    {
        error = string.Empty;

        if (!CanModify(tool))
        {
            error = "Bundled tools cannot be deleted.";
            return false;
        }

        try
        {
            var userRoot =
                EnsureTrailingSeparator(
                    Path.GetFullPath(
                        ToolPathService.GetUserToolsRoot()));

            var toolPath =
                EnsureTrailingSeparator(
                    Path.GetFullPath(
                        tool.ToolDirectory));

            if (string.Equals(
                    userRoot,
                    toolPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "Shrike will not delete the user Tools root.";

                return false;
            }

            if (!Directory.Exists(
                    tool.ToolDirectory))
            {
                error = "Tool folder does not exist.";
                return false;
            }

            Directory.Delete(
                tool.ToolDirectory,
                recursive: true);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool OpenToolFolder(
        ToolDefinition tool,
        out string error)
    {
        error = string.Empty;

        if (tool is null
            || string.IsNullOrWhiteSpace(
                tool.ToolDirectory)
            || !Directory.Exists(
                tool.ToolDirectory))
        {
            error = "Tool folder does not exist.";
            return false;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        tool.ToolDirectory,

                    UseShellExecute = true
                });

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }


    private bool MoveToolsToCategory(
        IReadOnlyList<ToolDefinition> tools,
        string newCategory,
        out string error)
    {
        error = string.Empty;

        newCategory =
            (newCategory ?? string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(
                newCategory))
        {
            error =
                "Category name cannot be empty.";

            return false;
        }

        var safeCategoryFolder =
            MakeSafeFolderName(
                newCategory);

        if (string.IsNullOrWhiteSpace(
                safeCategoryFolder))
        {
            error =
                "The category name cannot be converted " +
                "to a valid folder name.";

            return false;
        }

        if (tools.Count == 0)
        {
            error =
                "No tools were selected.";

            return false;
        }

        if (tools.Any(
                tool =>
                    !CanModify(tool)))
        {
            error =
                "Bundled tools cannot be moved or have their category renamed.";

            return false;
        }

        var userRoot =
            Path.GetFullPath(
                ToolPathService.GetUserToolsRoot());

        var destinationCategoryFolder =
            Path.Combine(
                userRoot,
                safeCategoryFolder);

        var plans =
            new List<ToolMovePlan>();

        try
        {
            foreach (var tool in tools)
            {
                if (string.IsNullOrWhiteSpace(
                        tool.ToolDirectory)
                    ||
                    !Directory.Exists(
                        tool.ToolDirectory))
                {
                    error =
                        $"Tool folder does not exist for \"{tool.Name}\".";

                    return false;
                }

                var sourceDirectory =
                    Path.GetFullPath(
                        tool.ToolDirectory);

                var sourceFolderName =
                    new DirectoryInfo(
                        sourceDirectory)
                    .Name;

                var destinationDirectory =
                    Path.GetFullPath(
                        Path.Combine(
                            destinationCategoryFolder,
                            sourceFolderName));

                var oldManifestPath =
                    string.IsNullOrWhiteSpace(
                        tool.ManifestPath)
                        ? Path.Combine(
                            sourceDirectory,
                            "tool.json")
                        : Path.GetFullPath(
                            tool.ManifestPath);

                plans.Add(
                    new ToolMovePlan
                    {
                        Tool = tool,
                        OldCategory =
                            tool.Category,
                        OldToolDirectory =
                            sourceDirectory,
                        OldManifestPath =
                            oldManifestPath,
                        NewToolDirectory =
                            destinationDirectory
                    });
            }

            var duplicateDestinations =
                plans
                    .GroupBy(
                        plan =>
                            plan.NewToolDirectory,
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(
                        group =>
                            group.Count() > 1);

            if (duplicateDestinations is not null)
            {
                error =
                    "Two tools would be moved to the same destination folder.";

                return false;
            }

            foreach (var plan in plans)
            {
                if (PathsEqual(
                        plan.OldToolDirectory,
                        plan.NewToolDirectory))
                {
                    continue;
                }

                if (Directory.Exists(
                        plan.NewToolDirectory)
                    ||
                    File.Exists(
                        plan.NewToolDirectory))
                {
                    error =
                        "A tool folder with the same name already exists " +
                        "in the destination category:\n\n" +
                        plan.NewToolDirectory;

                    return false;
                }
            }

            Directory.CreateDirectory(
                destinationCategoryFolder);

            foreach (var plan in plans)
            {
                if (PathsEqual(
                        plan.OldToolDirectory,
                        plan.NewToolDirectory))
                {
                    continue;
                }

                Directory.Move(
                    plan.OldToolDirectory,
                    plan.NewToolDirectory);

                plan.WasMoved = true;
            }

            foreach (var plan in plans)
            {
                plan.Tool.Category =
                    newCategory;

                plan.Tool.ToolDirectory =
                    plan.NewToolDirectory;

                plan.Tool.ManifestPath =
                    Path.Combine(
                        plan.NewToolDirectory,
                        "tool.json");

                SaveManifest(
                    plan.Tool);
            }

            foreach (var oldParent in
                     plans
                         .Select(
                             plan =>
                                 Path.GetDirectoryName(
                                     plan.OldToolDirectory))
                         .Where(
                             path =>
                                 !string.IsNullOrWhiteSpace(path))
                         .Distinct(
                             StringComparer.OrdinalIgnoreCase))
            {
                TryDeleteDirectoryIfEmpty(
                    oldParent!,
                    userRoot);
            }

            return true;
        }
        catch (Exception ex)
        {
            // Best-effort rollback.
            foreach (var plan in
                     plans.AsEnumerable()
                         .Reverse())
            {
                try
                {
                    if (plan.WasMoved
                        &&
                        Directory.Exists(
                            plan.NewToolDirectory)
                        &&
                        !Directory.Exists(
                            plan.OldToolDirectory))
                    {
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(
                                plan.OldToolDirectory)!);

                        Directory.Move(
                            plan.NewToolDirectory,
                            plan.OldToolDirectory);
                    }

                    plan.Tool.Category =
                        plan.OldCategory;

                    plan.Tool.ToolDirectory =
                        plan.OldToolDirectory;

                    plan.Tool.ManifestPath =
                        plan.OldManifestPath;

                    if (Directory.Exists(
                            plan.OldToolDirectory))
                    {
                        SaveManifest(
                            plan.Tool);
                    }
                }
                catch
                {
                }
            }

            error =
                "Shrike could not update the category.\n\n" +
                ex.Message;

            return false;
        }
    }

    private static bool PathsEqual(
        string first,
        string second)
    {
        var firstFull =
            Path.GetFullPath(first)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        var secondFull =
            Path.GetFullPath(second)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        return string.Equals(
            firstFull,
            secondFull,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteDirectoryIfEmpty(
        string directory,
        string userRoot)
    {
        try
        {
            var fullDirectory =
                Path.GetFullPath(directory);

            var fullUserRoot =
                Path.GetFullPath(userRoot);

            if (PathsEqual(
                    fullDirectory,
                    fullUserRoot))
            {
                return;
            }

            if (!Directory.Exists(
                    fullDirectory))
            {
                return;
            }

            if (Directory.EnumerateFileSystemEntries(
                    fullDirectory)
                .Any())
            {
                return;
            }

            Directory.Delete(
                fullDirectory);
        }
        catch
        {
        }
    }

    private sealed class ToolMovePlan
    {
        public required ToolDefinition Tool
        {
            get;
            init;
        }

        public required string OldCategory
        {
            get;
            init;
        }

        public required string OldToolDirectory
        {
            get;
            init;
        }

        public required string OldManifestPath
        {
            get;
            init;
        }

        public required string NewToolDirectory
        {
            get;
            init;
        }

        public bool WasMoved
        {
            get;
            set;
        }
    }

    private static string PreparePython3Script(
        string source)
    {
        var trimmedStart =
            source.TrimStart();

        if (trimmedStart.StartsWith(
                "#! python 2",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "This script explicitly requests Python 2. " +
                "Shrike tools currently use Rhino 8 Python 3.");
        }

        if (!trimmedStart.StartsWith(
                "#! python 3",
                StringComparison.OrdinalIgnoreCase))
        {
            source =
                "#! python 3" +
                Environment.NewLine +
                Environment.NewLine +
                source;
        }

        return source;
    }

    private void UpdateDuplicatedManifest(
        string copiedManifestPath,
        ToolDefinition sourceTool,
        string newName,
        string category)
    {
        JsonObject manifest;

        if (File.Exists(copiedManifestPath))
        {
            var json =
                File.ReadAllText(
                    copiedManifestPath);

            manifest =
                JsonNode.Parse(json)
                    ?.AsObject()
                ??
                new JsonObject();
        }
        else
        {
            manifest =
                new JsonObject();
        }

        manifest["id"] =
            MakeUniqueToolId(newName);

        manifest["name"] =
            newName;

        manifest["category"] =
            category;

        manifest["type"] =
            string.IsNullOrWhiteSpace(sourceTool.Type)
                ? "python3"
                : sourceTool.Type;

        manifest["entry"] =
            string.IsNullOrWhiteSpace(sourceTool.Entry)
                ? "main.py"
                : sourceTool.Entry;

        if (!string.IsNullOrWhiteSpace(
                sourceTool.Command))
        {
            manifest["command"] =
                sourceTool.Command;
        }

        manifest["icon"] =
            sourceTool.Icon ?? string.Empty;

        manifest["tooltip"] =
            sourceTool.Tooltip ?? string.Empty;

        var jsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true
            };

        File.WriteAllText(
            copiedManifestPath,
            manifest.ToJsonString(
                jsonOptions),
            new UTF8Encoding(false));
    }

    private void SaveManifest(
        ToolDefinition tool)
    {
        var manifestPath =
            tool.ManifestPath;

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            manifestPath =
                Path.Combine(
                    tool.ToolDirectory,
                    "tool.json");
        }

        var json =
            JsonSerializer.Serialize(
                tool,
                _jsonOptions);

        File.WriteAllText(
            manifestPath,
            json,
            new UTF8Encoding(false));

        tool.ManifestPath =
            manifestPath;
    }

    private static string GetEntryPath(
        ToolDefinition tool)
    {
        return Path.GetFullPath(
            Path.Combine(
                tool.ToolDirectory,
                string.IsNullOrWhiteSpace(tool.Entry)
                    ? "main.py"
                    : tool.Entry));
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        Directory.CreateDirectory(
            destination);

        foreach (var file in
                 Directory.GetFiles(source))
        {
            var target =
                Path.Combine(
                    destination,
                    Path.GetFileName(file));

            File.Copy(
                file,
                target,
                overwrite: false);
        }

        foreach (var directory in
                 Directory.GetDirectories(source))
        {
            // Version history stays with the original tool.
            if (ToolHistoryService.IsInternalPath(directory))
                continue;

            var target =
                Path.Combine(
                    destination,
                    Path.GetFileName(directory));

            CopyDirectory(
                directory,
                target);
        }
    }

    private static string MakeSafeFolderName(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var filtered =
            new string(
                value.Trim()
                    .Where(
                        character =>
                            !invalid.Contains(character))
                    .ToArray());

        filtered =
            Regex.Replace(
                filtered,
                @"\s+",
                string.Empty);

        return filtered.Trim(
            '.',
            ' ');
    }

    private static string MakeUniqueToolId(
        string value)
    {
        var slug =
            value.Trim()
                .ToLowerInvariant();

        slug =
            Regex.Replace(
                slug,
                @"[^a-z0-9]+",
                "-");

        slug =
            slug.Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
            slug = "tool";

        return
            $"{slug}-" +
            Guid.NewGuid()
                .ToString("N")[..8];
    }

    private static string QuoteArgument(
        string value)
    {
        return
            "\"" +
            value.Replace("\"", "\\\"") +
            "\"";
    }

    private static string EnsureTrailingSeparator(
        string path)
    {
        if (path.EndsWith(
                Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal))
        {
            return path;
        }

        return
            path +
            Path.DirectorySeparatorChar;
    }
}
