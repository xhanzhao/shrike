using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// One tool in the panel list: a flat row with no border.
/// It shows a light gray fill on hover and a darker one while
/// pressed. A tool color, when set, fills the row at rest.
/// The icon is only drawn when the tool has its own.
/// </summary>
public sealed class ToolRow : Drawable
{
    public const int RowHeight = 28;

    private const int IconSize = 16;
    private const float TextLeft = 10f;
    private const float IconGap = 8f;

    private bool _hover;
    private bool _mouseDown;

    public event EventHandler? Click;

    public string Text { get; set; } = string.Empty;

    public Image? Image { get; set; }

    /// <summary>
    /// The tool's highlight color, or null for none.
    /// </summary>
    public Color? FillColor { get; set; }

    public ToolRow()
    {
        Height = RowHeight;
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
        _mouseDown = false;
        Invalidate();
    }

    protected override void OnMouseDown(
        MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if ((e.Buttons & MouseButtons.Primary) == 0)
            return;

        _mouseDown = true;
        Invalidate();
    }

    protected override void OnMouseUp(
        MouseEventArgs e)
    {
        base.OnMouseUp(e);

        var wasDown = _mouseDown;
        _mouseDown = false;
        Invalidate();

        if (wasDown
            && (e.Buttons & MouseButtons.Primary) != 0
            && new RectangleF(Size).Contains(e.Location))
        {
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.AntiAlias = true;

        var baseFill =
            FillColor ?? Ui.Surface;

        var tint =
            Ui.IsDark(baseFill)
                ? Colors.White
                : Ui.Ink;

        Color? fill =
            FillColor;

        if (_hover)
            fill = Color.Blend(baseFill, tint, 0.07f);

        if (_mouseDown)
            fill = Color.Blend(baseFill, tint, 0.14f);

        if (fill.HasValue)
        {
            Draw.Frame(
                g,
                new RectangleF(0, 0, Width, Height),
                4f,
                fill.Value,
                fill.Value);
        }

        var x = TextLeft;

        if (Image is not null)
        {
            g.DrawImage(
                Image,
                x,
                (Height - IconSize) / 2f,
                IconSize,
                IconSize);

            x += IconSize + IconGap;
        }

        var font = Ui.BodyFont;

        var textSize =
            font.MeasureString(Text);

        using var brush =
            new SolidBrush(
                Ui.IsDark(baseFill)
                    ? Colors.White
                    : Ui.Ink);

        g.DrawText(
            font,
            brush,
            new RectangleF(
                x,
                (Height - textSize.Height) / 2f,
                Math.Max(0f, Width - x - TextLeft),
                textSize.Height + 2f),
            Text,
            FormattedTextWrapMode.None,
            FormattedTextAlignment.Left,
            FormattedTextTrimming.CharacterEllipsis);
    }
}
