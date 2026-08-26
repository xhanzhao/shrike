using Rhino;
using Shrike.Models;

namespace Shrike.Services;

public interface IToolRunner
{
    bool CanRun(ToolDefinition tool);

    bool Run(ToolDefinition tool, RhinoDoc? doc);
}
