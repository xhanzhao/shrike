using Rhino;
using Rhino.Commands;
using Shrike.Panels;

namespace Shrike.Commands;

public sealed class OpenShrikeCommand : Command
{
    public static OpenShrikeCommand? Instance { get; private set; }

    public OpenShrikeCommand()
    {
        Instance = this;
    }

    public override string EnglishName => "Shrike";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        ShrikePlugin.ShowPanel();
        return Result.Success;
    }
}
