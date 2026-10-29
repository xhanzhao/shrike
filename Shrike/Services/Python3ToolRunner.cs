using Rhino;
using Shrike.Models;

namespace Shrike.Services;

/// <summary>
/// Rhino 8 Python runner.
///
/// Rhino 8's new scripting infrastructure is invoked through:
///
///     _-ScriptEditor _R "C:\path\to\script.py"
///
/// Script Editor picks CPython 3 or IronPython 2 from the script's
/// "#! python 3" / "#! python 2" directive, the same way it does
/// when the script is run from the editor itself.
///
/// This intentionally does NOT use Rhino.Runtime.PythonScript.Create(),
/// which always runs the legacy IronPython/Python 2 engine.
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

        RepairInjectedDirective(scriptPath);

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

    // Tools imported by earlier Shrike versions may carry an added
    // "#! python 3" above their own "#! python2" line.
    private static void RepairInjectedDirective(
        string scriptPath)
    {
        try
        {
            var source =
                File.ReadAllText(scriptPath);

            if (!PythonScriptHeader.TryRemoveInjectedDirective(
                    source,
                    out var repaired))
            {
                return;
            }

            File.WriteAllText(
                scriptPath,
                repaired,
                new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine(
                $"Shrike: could not update the script header: {ex.Message}");
        }
    }
}
