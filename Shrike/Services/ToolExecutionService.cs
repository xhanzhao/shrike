using Rhino;
using Shrike.Models;

namespace Shrike.Services;

public sealed class ToolExecutionService
{
    private readonly IReadOnlyList<IToolRunner>
        _runners;

    public ToolExecutionService()
    {
        _runners =
            new IToolRunner[]
            {
                new Python3ToolRunner()
            };
    }

    public bool Run(
        ToolDefinition tool)
    {
        var runner =
            _runners.FirstOrDefault(
                candidate =>
                    candidate.CanRun(tool));

        if (runner is null)
        {
            RhinoApp.WriteLine(
                $"Shrike: unsupported tool type '{tool.Type}'.");

            return false;
        }

        return runner.Run(
            tool,
            RhinoDoc.ActiveDoc);
    }
}
