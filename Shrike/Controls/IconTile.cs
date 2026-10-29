using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// A square, clickable preview of a tool's icon. It sits to the
/// left of the name field, the way the icon sits beside the name
/// on the tool button in the panel.
/// </summary>
public sealed class IconTile : Drawable
{
    private Image? _image;
    private bool _hover;

    public event EventHandler? Click;

    public Image? Image
    {
        get => _image;
        set
        {
            _image = value;
            Invalidate();
        }
    }

    public IconTile()
    {
        Width = Ui.FieldHeight;
        Height = Ui.FieldHeight;
        Cursor = Cursors.Pointer;
        BackgroundColor = Ui.Surface;
        ToolTip = "Change icon (.png)";
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

    protected override void OnPaint(
        PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;

        Draw.Frame(
            g,
            new RectangleF(0, 0, Width, Height),
            Ui.FieldRadius,
            _hover && Enabled ? Ui.Ink : Ui.Rule,
            Ui.Surface);

        if (_image is null)
            return;

        const float iconSize = 18f;

        g.DrawImage(
            _image,
            (Width - iconSize) / 2f,
            (Height - iconSize) / 2f,
            iconSize,
            iconSize);
    }
}
