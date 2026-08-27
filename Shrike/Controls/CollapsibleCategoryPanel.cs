using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// Compact collapsible category section.
///
/// Outer margins are intentionally NOT defined here.
/// ToolboxPanel owns the global spacing so all categories use
/// one consistent spacing system.
/// </summary>
public sealed class CollapsibleCategoryPanel : Panel
{
    private readonly string _title;

    private readonly RoundedButton
        _headerButton;

    private readonly Panel
        _contentHost;

    public event EventHandler?
        ExpandedChanged;

    public bool Expanded
    {
        get;
        private set;
    }


    /// <summary>
    /// Right-click menu for the category header.
    /// </summary>
    public ContextMenu? HeaderContextMenu
    {
        get =>
            _headerButton.ContextMenu;

        set =>
            _headerButton.ContextMenu = value;
    }

    /// <summary>
    /// Optional background color for the category header.
    /// </summary>
    public Color? HeaderBackgroundColor
    {
        get =>
            _headerButton.CustomBackgroundColor;

        set =>
            _headerButton.CustomBackgroundColor = value;
    }

    public CollapsibleCategoryPanel(
        string title,
        Control categoryContent,
        bool expanded = true)
    {
        _title = title;
        Expanded = expanded;

        _headerButton =
            new RoundedButton
            {
                Height = 28,

                CornerRadius = 6f,

                BorderWidth = 1f,

                HorizontalPadding = 10,

                CenterContent = false,

                TextFont =
                    SystemFonts.Bold(),

                ToolTip =
                    "Expand / collapse category"
            };

        _headerButton.Click +=
            (_, _) => Toggle();

        _contentHost =
            new Panel
            {
                // Tool buttons are slightly inset relative
                // to their category header.
                Padding =
                    new Padding(
                        left: 10,
                        top: 6,
                        right: 10,
                        bottom: 0),

                Content =
                    categoryContent
            };

        Content =
            new StackLayout
            {
                Orientation =
                    Orientation.Vertical,

                Padding = 0,

                Spacing = 0,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch,

                Items =
                {
                    _headerButton,
                    _contentHost
                }
            };

        UpdateVisualState();
    }

    private void Toggle()
    {
        Expanded =
            !Expanded;

        UpdateVisualState();

        ExpandedChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void UpdateVisualState()
    {
        _headerButton.Text =
            Expanded
                ? $"▼  {_title}"
                : $"▶  {_title}";

        _contentHost.Visible =
            Expanded;

        Invalidate();
    }
}
