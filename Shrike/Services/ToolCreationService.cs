using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shrike.Models;

namespace Shrike.Services;

public sealed class ToolCreationService
{
    private const string DefaultToolIconResource =
        "Shrike.Resources.DefaultToolIcon.png";

    private const string DefaultMacroIconResource =
        "Shrike.Resources.DefaultMacroIcon.png";

    private readonly ToolHistoryService _historyService = new();

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true
        };

    public ToolCreationResult CreateTool(
        CreateToolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ToolCreationResult.Fail(
                "Tool name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return ToolCreationResult.Fail(
                "Category is required.");
        }

        var type =
            NormalizeType(request.Type);

        if (type is null)
        {
            return ToolCreationResult.Fail(
                $"Unsupported tool type: {request.Type}");
        }

        if (type == "python3")
        {
            if (string.IsNullOrWhiteSpace(
                    request.SourcePythonPath)
                || !File.Exists(
                    request.SourcePythonPath))
            {
                return ToolCreationResult.Fail(
                    "Select a valid .py file.");
            }

            if (!string.Equals(
                    Path.GetExtension(
                        request.SourcePythonPath),
                    ".py",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ToolCreationResult.Fail(
                    "The source file must be a .py file.");
            }
        }
        else if (string.IsNullOrWhiteSpace(
                     request.Macro))
        {
            return ToolCreationResult.Fail(
                "Enter a Rhino macro.");
        }

        var categoryFolder =
            MakeSafeFolderName(request.Category);

        var toolFolder =
            MakeSafeFolderName(request.Name);

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
            && Directory
                .EnumerateFileSystemEntries(destination)
                .Any())
        {
            return ToolCreationResult.Fail(
                "A tool folder with this name already exists:\n\n" +
                destination);
        }

        try
        {
            Directory.CreateDirectory(destination);

            var iconFileName =
                CreateToolIcon(
                    request.SourceIconPath,
                    destination,
                    type);

            var manifest =
                new Dictionary<string, object?>
                {
                    ["id"] = MakeToolId(request.Name),
                    ["name"] = request.Name.Trim(),
                    ["category"] = request.Category.Trim(),
                    ["type"] = type,
                    ["icon"] = iconFileName,
                    ["tooltip"] = request.Tooltip.Trim()
                };

            if (type == "python3")
            {
                var mainPyPath =
                    Path.Combine(
                        destination,
                        "main.py");

                CopyPython3Script(
                    request.SourcePythonPath,
                    mainPyPath);

                manifest["entry"] = "main.py";

                // The first entry in the version history.
                _historyService.Snapshot(
                    destination,
                    mainPyPath,
                    ToolHistoryReason.Created);
            }
            else
            {
                manifest["command"] =
                    request.Macro.Trim();
            }

            var manifestPath =
                Path.Combine(
                    destination,
                    "tool.json");

            var manifestJson =
                JsonSerializer.Serialize(
                    manifest,
                    _jsonOptions);

            File.WriteAllText(
                manifestPath,
                manifestJson,
                new UTF8Encoding(false));

            return ToolCreationResult.Ok(
                $"Created '{request.Name.Trim()}'.",
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
                "Shrike could not create the tool.\n\n" +
                ex.Message);
        }
    }

    private static string? NormalizeType(
        string value)
    {
        if (string.Equals(
                value,
                "python3",
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                value,
                "python",
                StringComparison.OrdinalIgnoreCase))
        {
            return "python3";
        }

        if (string.Equals(
                value,
                "macro",
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                value,
                "rhino-macro",
                StringComparison.OrdinalIgnoreCase))
        {
            return "macro";
        }

        return null;
    }

    private static void CopyPython3Script(
        string sourcePath,
        string destinationPath)
    {
        var source =
            File.ReadAllText(sourcePath);

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

        File.WriteAllText(
            destinationPath,
            source,
            new UTF8Encoding(false));
    }

    private static string CreateToolIcon(
    string sourceIconPath,
    string destination,
    string toolType)
    {
        var target =
            Path.Combine(
                destination,
                "icon.png");

        // A user-selected icon always wins.
        if (!string.IsNullOrWhiteSpace(sourceIconPath))
        {
            if (!File.Exists(sourceIconPath))
            {
                throw new FileNotFoundException(
                    "Selected icon file was not found.",
                    sourceIconPath);
            }

            if (!string.Equals(
                    Path.GetExtension(sourceIconPath),
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Custom tool icons must be PNG files.");
            }

            File.Copy(
                sourceIconPath,
                target,
                overwrite: true);

            return "icon.png";
        }

        // Without a selected icon, use the default icon for the tool type.
        var resourceName =
            string.Equals(
                toolType,
                "macro",
                StringComparison.OrdinalIgnoreCase)
                ? DefaultMacroIconResource
                : DefaultToolIconResource;

        var assembly =
            Assembly.GetExecutingAssembly();

        using var stream =
            assembly.GetManifestResourceStream(
                resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                "Shrike could not find the embedded default icon:\n\n" +
                resourceName);
        }

        using var output =
            File.Create(target);

        stream.CopyTo(output);

        return "icon.png";
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

        return filtered.Trim('.', ' ');
    }

    private static string MakeToolId(
        string value)
    {
        var id =
            value.Trim()
                .ToLowerInvariant();

        id =
            Regex.Replace(
                id,
                @"[^a-z0-9]+",
                "-");

        id = id.Trim('-');

        if (string.IsNullOrWhiteSpace(id))
        {
            id =
                "tool-" +
                Guid.NewGuid()
                    .ToString("N")[..8];
        }

        return id;
    }
}
