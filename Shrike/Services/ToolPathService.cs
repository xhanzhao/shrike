using System.Reflection;

namespace Shrike.Services;

public static class ToolPathService
{
    public static string GetPluginDirectory()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var directory = Path.GetDirectoryName(assemblyPath);

        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException(
                "Shrike could not determine the plugin directory.");

        return directory;
    }

    public static string GetBundledToolsRoot()
    {
        return Path.Combine(GetPluginDirectory(), "Tools");
    }

    public static string GetUserToolsRoot()
    {
        var documents = Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments);

        var root = Path.Combine(documents, "Shrike", "Tools");
        Directory.CreateDirectory(root);
        return root;
    }

    public static IReadOnlyList<string> GetToolSearchRoots()
    {
        return new[]
        {
            GetBundledToolsRoot(),
            GetUserToolsRoot()
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    }
}
