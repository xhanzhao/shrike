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

            var entryFileName =
                MakeScriptFileName(
                    request.Name);

            var iconFileName =
                CreateToolIcon(
                    request.SourceIconPath,
                    destination);

            var manifest =
                new Dictionary<string, object?>
                {
                    ["id"] = MakeToolId(request.Name),
                    ["name"] = request.Name.Trim(),
                    ["category"] = request.Category.Trim(),
                    ["type"] = type,
                    ["icon"] = iconFileName,
                    ["tooltip"] = request.Tooltip.Trim(),
                    ["entry"] = entryFileName
                };

            var entryPath =
                Path.Combine(
                    destination,
                    entryFileName);

            CopyPython3Script(
                request.SourcePythonPath,
                entryPath);

            // The first entry in the version history.
            _historyService.Snapshot(
                destination,
                entryPath,
                ToolHistoryReason.Created);

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
    string destination)
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

        // Without a selected icon, use the default tool icon.
        var resourceName =
            DefaultToolIconResource;

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

    // Module names a tool script must not take: a script called
    // random.py in the tool folder would shadow "import random".
    private static readonly HashSet<string> ReservedModuleNames =
        new(StringComparer.Ordinal)
        {
            "abc", "array", "ast", "base64", "bisect", "calendar",
            "cmath", "code", "collections", "copy", "csv", "datetime",
            "decimal", "enum", "fractions", "functools", "glob", "hashlib",
            "heapq", "inspect", "io", "itertools", "json", "logging",
            "main", "math", "operator", "os", "pathlib", "pickle",
            "platform", "queue", "random", "re", "select", "shutil",
            "signal", "socket", "statistics", "string", "struct",
            "subprocess", "sys", "tempfile", "test", "threading", "time",
            "timeit", "token", "tokenize", "types", "typing", "uuid",
            "warnings", "weakref", "zipfile",
            "clr", "ghpythonlib", "rhinoscript", "rhinoscriptsyntax",
            "scriptcontext", "rhino", "system", "eto", "grasshopper"
        };

    /// <summary>
    /// Names the script after the tool, so open tabs in Rhino's
    /// Script Editor are told apart: "Random Select" becomes
    /// random_select.py. A name that would shadow a Python module
    /// gets a "_tool" suffix.
    /// </summary>
    internal static string MakeScriptFileName(
        string toolName)
    {
        var name =
            Regex.Replace(
                toolName.Trim(),
                "([a-z])([A-Z])",
                "$1_$2");

        name =
            Regex.Replace(
                name.ToLowerInvariant(),
                "[^a-z0-9]+",
                "_")
            .Trim('_');

        if (name.Length == 0)
            return "main.py";

        if (char.IsDigit(name[0]))
            name = "tool_" + name;

        if (ReservedModuleNames.Contains(name))
            name += "_tool";

        return name + ".py";
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
