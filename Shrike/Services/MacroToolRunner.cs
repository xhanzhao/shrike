using Rhino;
using Shrike.Models;

namespace Shrike.Services;

public sealed class MacroToolRunner : IToolRunner
{
    public bool CanRun(
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

    public bool Run(
        ToolDefinition tool,
        RhinoDoc? doc)
    {
        var macro =
            (tool.Command ?? string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(macro))
        {
            RhinoApp.WriteLine(
                $"Shrike: macro is empty for '{tool.Name}'.");

            return false;
        }

        RhinoApp.WriteLine(
            $"Shrike: running macro '{tool.Name}'");

        try
        {
            return RhinoApp.RunScript(
                macro,
                echo: false);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine(
                $"Shrike: '{tool.Name}' macro failed.");

            RhinoApp.WriteLine(
                ex.ToString());

            return false;
        }
    }
}
