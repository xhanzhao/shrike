using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// Draws Shrike's own border around a borderless input, so fields
/// are black and white in every state instead of using the
/// system's blue focus border.
///
/// Border: Rule at rest, RuleStrong on hover, Ink while focused.
/// </summary>
public sealed class FieldFrame : Drawable
{
    private bool _hover;
    private bool _focused;

    public FieldFrame(
        Control input,
        bool multiline = false)
    {
        Padding =
            multiline
                ? new Padding(8, 6)
                // Keep content 2px inside so it never paints
                // over the 1px border.
                : new Padding(8, 2);

        if (!multiline)
            Height = Ui.FieldHeight;

        BackgroundColor = Ui.Surface;

        Content =
            multiline
                ? input
                : new TableLayout
                {
                    Rows =
                    {
                        new TableRow { ScaleHeight = true },
                        new TableRow(input),
                        new TableRow { ScaleHeight = true }
                    }
                };

        Track(input);

        MouseDown +=
            (_, _) =>
                input.Focus();
    }

    /// <summary>
    /// Shows the focus border while the given control has focus.
    /// </summary>
    public void Track(
        Control control)
    {
        control.GotFocus +=
            (_, _) =>
            {
                _focused = true;
                Invalidate();
            };

        control.LostFocus +=
            (_, _) =>
            {
                _focused = false;
                Invalidate();
            };

        control.MouseEnter +=
            (_, _) =>
                SetHover(true);

        control.MouseLeave +=
            (_, _) =>
                SetHover(false);
    }

    protected override void OnMouseEnter(
        MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        SetHover(true);
    }

    protected override void OnMouseLeave(
        MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(false);
    }

    private void SetHover(
        bool hover)
    {
        if (_hover == hover)
            return;

        _hover = hover;
        Invalidate();
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        base.OnPaint(e);

        var border =
            _focused
                ? Ui.Ink
                : _hover
                    ? Ui.RuleStrong
                    : Ui.Rule;

        var width =
            _focused ? 1.5f : 1f;

        Draw.Frame(
            e.Graphics,
            new RectangleF(
                0,
                0,
                Width,
                Height),
            Ui.FieldRadius,
            border,
            Ui.Surface,
            width);
    }
}

/// <summary>
/// Small drawing helpers shared by Shrike's custom controls.
/// </summary>
internal static class Draw
{
    /// <summary>
    /// A rounded rectangle with an even border, drawn as two fills
    /// (no pen), which stays crisp under Windows DPI scaling.
    /// </summary>
    public static void Frame(
        Graphics graphics,
        RectangleF bounds,
        float radius,
        Color border,
        Color fill,
        float borderWidth = 1f)
    {
        graphics.AntiAlias = true;

        var outer =
            RectangleF.Inflate(
                bounds,
                -0.5f,
                -0.5f);

        if (outer.Width <= 0 || outer.Height <= 0)
            return;

        using (var outerPath =
                   GraphicsPath.GetRoundRect(
                       outer,
                       radius))
        {
            graphics.FillPath(
                border,
                outerPath);
        }

        var inner =
            RectangleF.Inflate(
                outer,
                -borderWidth,
                -borderWidth);

        if (inner.Width <= 0 || inner.Height <= 0)
            return;

        using var innerPath =
            GraphicsPath.GetRoundRect(
                inner,
                Math.Max(
                    1f,
                    radius - borderWidth));

        graphics.FillPath(
            fill,
            innerPath);
    }

    public static void Chevron(
        Graphics graphics,
        PointF center,
        Color color)
    {
        graphics.AntiAlias = true;

        using var pen =
            new Pen(
                color,
                1.4f);

        graphics.DrawLine(
            pen,
            center.X - 4f,
            center.Y - 2f,
            center.X,
            center.Y + 2f);

        graphics.DrawLine(
            pen,
            center.X,
            center.Y + 2f,
            center.X + 4f,
            center.Y - 2f);
    }
}

/// <summary>
/// A quiet bordered container for a group of read-only details
/// with their actions, such as a tool's script and its versions.
/// </summary>
public sealed class Card : Drawable
{
    public Card(
        Control content)
    {
        Padding = new Padding(12, 10);
        BackgroundColor = Ui.Surface;
        Content = content;
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        base.OnPaint(e);

        Draw.Frame(
            e.Graphics,
            new RectangleF(0, 0, Width, Height),
            Ui.FieldRadius + 1f,
            Ui.Rule,
            Ui.Surface);
    }
}
