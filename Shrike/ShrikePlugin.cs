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
    // Set once Shrike has put its panel in Rhino's side bar,
    // so later starts respect wherever the user moved or closed it.
    private const string PanelPlacedSetting = "PanelPlacedInSideBar";

    public static ShrikePlugin? Instance { get; private set; }

    public ShrikePlugin()
    {
        Instance = this;
    }

    // Load with Rhino, so the panel can be docked on first start
    // instead of waiting for the Shrike command.
    public override PlugInLoadTime LoadTime =>
        PlugInLoadTime.AtStartup;

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

        // The main window is not ready during OnLoad.
        RhinoApp.Idle += DockPanelOnFirstStart;

        RhinoApp.WriteLine("Shrike loaded.");

        return LoadReturnCode.Success;
    }

    private void DockPanelOnFirstStart(
        object? sender,
        EventArgs e)
    {
        RhinoApp.Idle -= DockPanelOnFirstStart;

        if (Settings.GetBool(PanelPlacedSetting, false))
            return;

        ShowPanel();
    }

    /// <summary>
    /// Opens the Shrike panel. The first time, it is added as a tab
    /// next to Rhino's Layers panel in the side bar; after that,
    /// Rhino reopens it wherever the user last left it.
    /// </summary>
    public static void ShowPanel()
    {
        var panelId =
            typeof(ToolboxPanel).GUID;

        var placed =
            Instance?.Settings.GetBool(
                PanelPlacedSetting,
                false)
            ?? false;

        // Also moves a panel that an earlier version left floating.
        if (!placed)
        {
            Rhino.UI.Panels.OpenPanelAsSibling(
                panelId,
                PanelIds.Layers,
                true);
        }
        else
        {
            Rhino.UI.Panels.OpenPanel(
                panelId,
                true);
        }

        Instance?.Settings.SetBool(
            PanelPlacedSetting,
            true);
    }
}
