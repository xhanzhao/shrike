using System.Reflection;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.PlugIns;
using Rhino.UI;
using Shrike.Panels;

namespace Shrike;

[Guid("B360391B-0A03-4F18-BFA3-B59DE913D437")]
public sealed class ShrikePlugin : PlugIn
{
    public static ShrikePlugin? Instance { get; private set; }

    public ShrikePlugin()
    {
        Instance = this;
    }

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        // Rhino 8-specific RegisterPanel overload.
        // It uses an embedded-resource icon instead of System.Drawing.Icon.
        Rhino.UI.Panels.RegisterPanel(
            this,
            typeof(ToolboxPanel),
            "Shrike",
            Assembly.GetExecutingAssembly(),
            "Shrike.Resources.Shrike.ico",
            PanelType.System);

        RhinoApp.WriteLine("Shrike loaded.");

        return LoadReturnCode.Success;
    }
}
