using Rhino;
using Shrike.Models;

namespace Shrike.Services;

/// <summary>
/// Rhino 8 Python 3 runner.
///
/// Rhino 8's new scripting infrastructure is invoked through:
///
///     _-ScriptEditor _R "C:\path\to\script.py"
///
/// This intentionally does NOT use Rhino.Runtime.PythonScript.Create(),
/// which is the legacy IronPython/Python 2 engine.
/// </summary>
public sealed class Python3ToolRunner : IToolRunner
{
    public bool CanRun(ToolDefinition tool)
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

    public bool Run(ToolDefinition tool, RhinoDoc? doc)
    {
        var scriptPath =
            Path.GetFullPath(
                Path.Combine(
                    tool.ToolDirectory,
                    tool.Entry));

        if (!File.Exists(scriptPath))
        {
            RhinoApp.WriteLine(
                $"Shrike: Python file not found: {scriptPath}");

            return false;
        }

        // A quoted path is required when folders contain spaces.
        // Double embedded quotes defensively.
        var safePath =
            scriptPath.Replace("\"", "\"\"");

        var macro =
            $"_-ScriptEditor _R \"{safePath}\"";

        RhinoApp.WriteLine(
            $"Shrike: running '{tool.Name}'");

        try
        {
            return RhinoApp.RunScript(
                macro,
                echo: false);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine(
                $"Shrike: '{tool.Name}' failed.");
            RhinoApp.WriteLine(ex.ToString());

            return false;
        }
    }
}
