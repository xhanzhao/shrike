using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Shrike.Models;

namespace Shrike.Services;

public sealed class ToolPackageService
{
    public bool TryReadPackageInfo(
        string packagePath,
        out ToolPackageInfo? info,
        out string error)
    {
        info = null;
        error = string.Empty;

        if (!ValidatePackagePath(
                packagePath,
                out error))
        {
            return false;
        }

        try
        {
            using var archive =
                ZipFile.OpenRead(
                    packagePath);

            var manifestEntry =
                archive.Entries
                    .FirstOrDefault(
                        entry =>
                            string.Equals(
                                NormalizeArchivePath(
                                    entry.FullName),
                                "tool.json",
                                StringComparison.OrdinalIgnoreCase));

            if (manifestEntry is null)
            {
                error =
                    "This package does not contain a root tool.json file.";

                return false;
            }

            using var stream =
                manifestEntry.Open();

            using var reader =
                new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true);

            var json =
                reader.ReadToEnd();

            var manifest =
                JsonNode.Parse(json)
                    ?.AsObject();

            if (manifest is null)
            {
                error =
                    "The package tool.json is invalid.";

                return false;
            }

            var name =
                ReadString(
                    manifest,
                    "name");

            var category =
                ReadString(
                    manifest,
                    "category");

            var type =
                ReadString(
                    manifest,
                    "type");

            var entry =
                ReadString(
                    manifest,
                    "entry");

            var command =
                ReadString(
                    manifest,
                    "command");

            if (string.IsNullOrWhiteSpace(name)
                ||
                string.IsNullOrWhiteSpace(category))
            {
                error =
                    "The package tool.json must contain a name and category.";

                return false;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                type = "python3";
            }

            if (!IsPythonType(type))
            {
                error =
                    $"This package is a '{type}' tool.\n\n" +
                    "Shrike only supports Python 3 tools.";

                return false;
            }

            if (string.IsNullOrWhiteSpace(entry))
            {
                entry = "main.py";
            }

            info =
                new ToolPackageInfo
                {
                    Name = name,
                    Category = category,
                    Type = type,
                    Entry = entry,
                    Command = command
                };

            return true;
        }
        catch (InvalidDataException ex)
        {
            error =
                "This .shriketool file is not a valid Shrike package.\n\n" +
                ex.Message;

            return false;
        }
        catch (Exception ex)
        {
            error =
                "Shrike could not read the tool package.\n\n" +
                ex.Message;

            return false;
        }
    }

    public ToolCreationResult ImportTool(
        string packagePath,
        string newName,
        string newCategory)
    {
        newName =
            (newName ?? string.Empty)
            .Trim();

        newCategory =
            (newCategory ?? string.Empty)
            .Trim();

        if (!ValidatePackagePath(
                packagePath,
                out var validationError))
        {
            return ToolCreationResult.Fail(
                validationError);
        }

        if (string.IsNullOrWhiteSpace(
                newName))
        {
            return ToolCreationResult.Fail(
                "Tool name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(
                newCategory))
        {
            return ToolCreationResult.Fail(
                "Category cannot be empty.");
        }

        var categoryFolder =
            MakeSafeFolderName(
                newCategory);

        var toolFolder =
            MakeSafeFolderName(
                newName);

        if (string.IsNullOrWhiteSpace(
                categoryFolder)
            ||
            string.IsNullOrWhiteSpace(
                toolFolder))
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

        if (Directory.Exists(
                destination)
            &&
            Directory.EnumerateFileSystemEntries(
                    destination)
                .Any())
        {
            return ToolCreationResult.Fail(
                "A tool folder with this name already exists:\n\n" +
                destination);
        }

        var tempRoot =
            Path.Combine(
                Path.GetTempPath(),
                "Shrike",
                "Import",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                tempRoot);

            ExtractPackageSafely(
                packagePath,
                tempRoot);

            var manifestPath =
                Path.Combine(
                    tempRoot,
                    "tool.json");

            if (!File.Exists(
                    manifestPath))
            {
                return ToolCreationResult.Fail(
                    "This package does not contain a root tool.json file.");
            }

            var json =
                File.ReadAllText(
                    manifestPath);

            var manifest =
                JsonNode.Parse(json)
                    ?.AsObject();

            if (manifest is null)
            {
                return ToolCreationResult.Fail(
                    "The package tool.json is invalid.");
            }

            var entry =
                ReadString(
                    manifest,
                    "entry");

            var type =
                ReadString(
                    manifest,
                    "type");

            if (string.IsNullOrWhiteSpace(type))
            {
                type = "python3";
            }

            string normalizedType;

            if (IsPythonType(type))
            {
                normalizedType = "python3";

                if (string.IsNullOrWhiteSpace(entry))
                {
                    entry = "main.py";
                }

                var safeEntryPath =
                    GetSafeContainedPath(
                        tempRoot,
                        entry);

                if (!File.Exists(safeEntryPath))
                {
                    return ToolCreationResult.Fail(
                        $"The package entry script does not exist:\n\n{entry}");
                }
            }
            else if (IsMacroType(type))
            {
                return ToolCreationResult.Fail(
                    "This package is a Rhino Macro tool.\n\n" +
                    "Shrike only supports Python 3 tools.");
            }
            else
            {
                return ToolCreationResult.Fail(
                    $"Unsupported tool type: {type}\n\n" +
                    "Shrike only supports Python 3 tools.");
            }

            SetCanonicalString(
                manifest,
                "id",
                MakeUniqueToolId(
                    newName));

            SetCanonicalString(
                manifest,
                "name",
                newName);

            SetCanonicalString(
                manifest,
                "category",
                newCategory);

            SetCanonicalString(
                manifest,
                "type",
                normalizedType);

            SetCanonicalString(
                manifest,
                "entry",
                entry);

            RemoveCanonicalProperty(
                manifest,
                "command");

            // Preserve icon / tooltip if present, but normalize their
            // property casing as well.
            var icon =
                ReadString(
                    manifest,
                    "icon");

            if (!string.IsNullOrWhiteSpace(
                    icon))
            {
                SetCanonicalString(
                    manifest,
                    "icon",
                    icon);
            }

            var tooltip =
                ReadString(
                    manifest,
                    "tooltip");

            if (!string.IsNullOrWhiteSpace(
                    tooltip))
            {
                SetCanonicalString(
                    manifest,
                    "tooltip",
                    tooltip);
            }

            File.WriteAllText(
                manifestPath,
                manifest.ToJsonString(
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }),
                new UTF8Encoding(false));

            CopyDirectory(
                tempRoot,
                destination);

            return ToolCreationResult.Ok(
                $"Imported '{newName}'.",
                destination);
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(
                        destination))
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
                "Shrike could not import the tool package.\n\n" +
                ex.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(
                        tempRoot))
                {
                    Directory.Delete(
                        tempRoot,
                        recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    public bool ExportTool(
        ToolDefinition tool,
        string destinationPath,
        out string error)
    {
        error = string.Empty;

        if (tool is null
            ||
            string.IsNullOrWhiteSpace(
                tool.ToolDirectory)
            ||
            !Directory.Exists(
                tool.ToolDirectory))
        {
            error =
                "Tool folder does not exist.";

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                destinationPath))
        {
            error =
                "Choose a valid export location.";

            return false;
        }

        try
        {
            var fullDestination =
                Path.GetFullPath(
                    destinationPath);

            var destinationDirectory =
                Path.GetDirectoryName(
                    fullDestination);

            if (!string.IsNullOrWhiteSpace(
                    destinationDirectory))
            {
                Directory.CreateDirectory(
                    destinationDirectory);
            }

            if (File.Exists(
                    fullDestination))
            {
                File.Delete(
                    fullDestination);
            }

            using var archive =
                ZipFile.Open(
                    fullDestination,
                    ZipArchiveMode.Create);

            var root =
                Path.GetFullPath(
                    tool.ToolDirectory);

            foreach (var file in
                     Directory.EnumerateFiles(
                         root,
                         "*",
                         SearchOption.AllDirectories))
            {
                // Version history and run data are private to this
                // computer and are never exported.
                if (ToolHistoryService.IsInternalPath(
                        Path.GetRelativePath(
                            root,
                            file)))
                {
                    continue;
                }

                var relativePath =
                    Path.GetRelativePath(
                        root,
                        file)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '/');

                archive.CreateEntryFromFile(
                    file,
                    relativePath,
                    CompressionLevel.Optimal);
            }

            return true;
        }
        catch (Exception ex)
        {
            error =
                "Shrike could not export the tool.\n\n" +
                ex.Message;

            return false;
        }
    }

    public static string MakeSuggestedPackageFileName(
        string toolName)
    {
        var safe =
            MakeSafeFileName(
                toolName);

        if (string.IsNullOrWhiteSpace(
                safe))
        {
            safe =
                "ShrikeTool";
        }

        return
            safe +
            ".shriketool";
    }

    private static bool ValidatePackagePath(
        string packagePath,
        out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(
                packagePath)
            ||
            !File.Exists(
                packagePath))
        {
            error =
                "Select a valid .shriketool file.";

            return false;
        }

        if (!string.Equals(
                Path.GetExtension(
                    packagePath),
                ".shriketool",
                StringComparison.OrdinalIgnoreCase))
        {
            error =
                "The selected file must have the .shriketool extension.";

            return false;
        }

        return true;
    }

    private static void ExtractPackageSafely(
        string packagePath,
        string destinationRoot)
    {
        var root =
            EnsureTrailingSeparator(
                Path.GetFullPath(
                    destinationRoot));

        using var archive =
            ZipFile.OpenRead(
                packagePath);

        foreach (var entry in
                 archive.Entries)
        {
            if (string.IsNullOrEmpty(
                    entry.Name))
            {
                continue;
            }

            var relative =
                NormalizeArchivePath(
                    entry.FullName);

            var destination =
                Path.GetFullPath(
                    Path.Combine(
                        destinationRoot,
                        relative));

            if (!destination.StartsWith(
                    root,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The package contains an unsafe file path.");
            }

            var parent =
                Path.GetDirectoryName(
                    destination);

            if (!string.IsNullOrWhiteSpace(
                    parent))
            {
                Directory.CreateDirectory(
                    parent);
            }

            entry.ExtractToFile(
                destination,
                overwrite: true);
        }
    }

    private static string GetSafeContainedPath(
        string root,
        string relativePath)
    {
        var fullRoot =
            EnsureTrailingSeparator(
                Path.GetFullPath(
                    root));

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    relativePath));

        if (!fullPath.StartsWith(
                fullRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The package entry path is outside the tool folder.");
        }

        return fullPath;
    }

    private static string NormalizeArchivePath(
        string value)
    {
        return value
            .Replace('\\', '/')
            .TrimStart('/');
    }

    private static bool IsPythonType(
        string type)
    {
        return string.Equals(
                   type,
                   "python3",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   type,
                   "python",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMacroType(
        string type)
    {
        return string.Equals(
                   type,
                   "macro",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   type,
                   "rhino-macro",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveCanonicalProperty(
        JsonObject manifest,
        string propertyName)
    {
        var matchingKeys =
            manifest
                .Select(
                    property => property.Key)
                .Where(
                    key =>
                        string.Equals(
                            key,
                            propertyName,
                            StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var key in matchingKeys)
        {
            manifest.Remove(key);
        }
    }

    private static string ReadString(
        JsonObject manifest,
        string propertyName)
    {
        try
        {
            foreach (var property in manifest)
            {
                if (!string.Equals(
                        property.Key,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.Value
                           ?.GetValue<string>()
                           ?.Trim()
                       ??
                       string.Empty;
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void SetCanonicalString(
        JsonObject manifest,
        string propertyName,
        string value)
    {
        var matchingKeys =
            manifest
                .Select(
                    property =>
                        property.Key)
                .Where(
                    key =>
                        string.Equals(
                            key,
                            propertyName,
                            StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var key in matchingKeys)
        {
            manifest.Remove(
                key);
        }

        manifest[propertyName] =
            value;
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        Directory.CreateDirectory(
            destination);

        foreach (var file in
                 Directory.GetFiles(
                     source))
        {
            File.Copy(
                file,
                Path.Combine(
                    destination,
                    Path.GetFileName(
                        file)),
                overwrite: false);
        }

        foreach (var directory in
                 Directory.GetDirectories(
                     source))
        {
            if (ToolHistoryService.IsInternalPath(
                    directory))
            {
                continue;
            }

            CopyDirectory(
                directory,
                Path.Combine(
                    destination,
                    Path.GetFileName(
                        directory)));
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
                            !invalid.Contains(
                                character))
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

    private static string MakeSafeFileName(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var filtered =
            new string(
                (value ?? string.Empty)
                    .Trim()
                    .Where(
                        character =>
                            !invalid.Contains(
                                character))
                    .ToArray());

        return filtered.Trim(
            '.',
            ' ');
    }

    private static string MakeUniqueToolId(
        string value)
    {
        var slug =
            (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

        slug =
            Regex.Replace(
                slug,
                @"[^a-z0-9]+",
                "-")
            .Trim('-');

        if (string.IsNullOrWhiteSpace(
                slug))
        {
            slug =
                "tool";
        }

        return
            $"{slug}-" +
            Guid.NewGuid()
                .ToString("N")[..8];
    }

    private static string EnsureTrailingSeparator(
        string path)
    {
        if (path.EndsWith(
                Path.DirectorySeparatorChar
                    .ToString(),
                StringComparison.Ordinal))
        {
            return path;
        }

        return
            path +
            Path.DirectorySeparatorChar;
    }
}
