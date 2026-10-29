using Eto.Forms;

namespace Shrike.Services;

/// <summary>
/// Reads Python code from the system clipboard: either copied text
/// (for example an AI reply) or a .py file copied in File Explorer.
/// </summary>
public static class ClipboardSource
{
    public static ClipboardCodeResult ReadCode()
    {
        using var clipboard =
            new Clipboard();

        var pythonFile =
            clipboard.ContainsUris
                ? clipboard.Uris?
                    .Where(
                        uri =>
                            uri.IsFile)
                    .Select(
                        uri =>
                            uri.LocalPath)
                    .FirstOrDefault(
                        IsPythonFile)
                : null;

        if (pythonFile is not null)
            return ReadFile(pythonFile);

        return ClipboardCodeParser.Parse(
            clipboard.Text);
    }

    public static ClipboardCodeResult ReadFile(
        string path)
    {
        var result =
            ClipboardCodeParser.Parse(
                File.ReadAllText(path));

        result.SourceFilePath =
            path;

        result.CodeBlockCount = 0;

        if (string.IsNullOrWhiteSpace(
                result.SuggestedName))
        {
            result.SuggestedName =
                Path.GetFileNameWithoutExtension(path);
        }

        return result;
    }

    public static bool IsPythonFile(
        string path)
    {
        return string.Equals(
                   Path.GetExtension(path),
                   ".py",
                   StringComparison.OrdinalIgnoreCase)
               && File.Exists(path);
    }
}
