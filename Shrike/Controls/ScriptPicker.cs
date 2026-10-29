using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// The script slot at the top of Create Tool.
///
/// Empty: a dashed outline asking for a tested script.
/// Filled: a solid ".py" badge with the file name and its folder.
/// Clicking it raises Click so the dialog can open a file picker.
/// </summary>
public sealed class ScriptPicker : Drawable
{
    private const int BadgeSize = 34;

    private string? _path;
    private bool _hover;

    public event EventHandler? Click;

    public string? Path
    {
        get => _path;
        set
        {
            _path = value;
            Invalidate();
        }
    }

    public string EmptyTitle { get; set; } =
        "Choose a Python script";

    public string EmptyHint { get; set; } =
        "Use the version you tested in Rhino's Script Editor.";

    public ScriptPicker()
    {
        Height = 62;
        CanFocus = true;
        Cursor = Cursors.Pointer;
        BackgroundColor = Ui.Surface;
    }

    protected override void OnMouseEnter(
        MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(
        MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnMouseUp(
        MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (Enabled
            && (e.Buttons & MouseButtons.Primary) != 0)
        {
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnKeyDown(
        KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Enabled
            && e.Key == Keys.Space)
        {
            e.Handled = true;
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnGotFocus(
        EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(
        EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.AntiAlias = true;

        var hasFile =
            !string.IsNullOrWhiteSpace(_path);

        var bounds =
            new RectangleF(
                0.5f,
                0.5f,
                Width - 1f,
                Height - 1f);

        var emphasis =
            _hover || HasFocus;

        if (hasFile)
        {
            Draw.Frame(
                g,
                new RectangleF(0, 0, Width, Height),
                Ui.FieldRadius + 1f,
                emphasis ? Ui.Ink : Ui.Rule,
                Ui.Surface);
        }
        else
        {
            using var path =
                GraphicsPath.GetRoundRect(
                    bounds,
                    Ui.FieldRadius + 1f);

            using var pen =
                new Pen(
                    emphasis ? Ui.Ink : Ui.RuleStrong,
                    1f)
                {
                    DashStyle = DashStyles.Dash
                };

            g.DrawPath(
                pen,
                path);
        }

        var badge =
            new RectangleF(
                14,
                (Height - BadgeSize) / 2f,
                BadgeSize,
                BadgeSize);

        // Filled badge once a script is chosen,
        // outlined badge while the slot is empty.
        Draw.Frame(
            g,
            badge,
            4f,
            Ui.Ink,
            hasFile ? Ui.Ink : Ui.Surface,
            1.2f);

        var badgeFont =
            Ui.StrongFont;

        var badgeText = ".py";

        var badgeTextSize =
            badgeFont.MeasureString(
                badgeText);

        g.DrawText(
            badgeFont,
            hasFile ? Ui.Surface : Ui.Ink,
            badge.X + (badge.Width - badgeTextSize.Width) / 2f,
            badge.Y + (badge.Height - badgeTextSize.Height) / 2f,
            badgeText);

        var title =
            hasFile
                ? System.IO.Path.GetFileName(_path!)
                : EmptyTitle;

        var subtitle =
            hasFile
                ? System.IO.Path.GetDirectoryName(_path!) ?? string.Empty
                : EmptyHint;

        var textX =
            badge.Right + 12f;

        var textWidth =
            Math.Max(
                0f,
                Width - textX - 14f);

        var titleFont =
            Ui.StrongFont;

        var subtitleFont =
            Ui.SmallFont;

        var titleHeight =
            titleFont.MeasureString(title).Height;

        var subtitleHeight =
            subtitleFont.MeasureString(subtitle).Height;

        var top =
            (Height - titleHeight - subtitleHeight - 2f) / 2f;

        using var titleBrush =
            new SolidBrush(Ui.Ink);

        using var subtitleBrush =
            new SolidBrush(Ui.Muted);

        g.DrawText(
            titleFont,
            titleBrush,
            new RectangleF(
                textX,
                top,
                textWidth,
                titleHeight + 2f),
            title,
            FormattedTextWrapMode.None,
            FormattedTextAlignment.Left,
            FormattedTextTrimming.CharacterEllipsis);

        g.DrawText(
            subtitleFont,
            subtitleBrush,
            new RectangleF(
                textX,
                top + titleHeight + 2f,
                textWidth,
                subtitleHeight + 2f),
            subtitle,
            FormattedTextWrapMode.None,
            FormattedTextAlignment.Left,
            FormattedTextTrimming.CharacterEllipsis);
    }
}
