using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// A row of gray swatches for a tool's highlight color.
/// The first swatch, drawn with a slash, means "no color".
/// The selected swatch gets an ink ring and its name is shown
/// to the right of the row.
/// </summary>
public sealed class SwatchPicker : Drawable
{
    private const float Diameter = 20f;
    private const float Spacing = 10f;
    private const float RingGap = 3f;

    private readonly IReadOnlyList<(string Name, string Hex)> _presets;

    private int _selectedIndex;
    private int _hoverIndex = -1;

    /// <summary>
    /// The selected color as hex, or null for no color.
    /// </summary>
    public string? SelectedHex =>
        _selectedIndex <= 0
            ? null
            : _presets[_selectedIndex - 1].Hex;

    public SwatchPicker(
        IReadOnlyList<(string Name, string Hex)> presets,
        string? selectedHex)
    {
        _presets = presets;

        Height = (int)(Diameter + RingGap * 2f + 4f);
        BackgroundColor = Ui.Surface;

        _selectedIndex = 0;

        for (var i = 0; i < presets.Count; i++)
        {
            if (string.Equals(
                    presets[i].Hex,
                    selectedHex,
                    StringComparison.OrdinalIgnoreCase))
            {
                _selectedIndex = i + 1;
            }
        }
    }

    private int Count =>
        _presets.Count + 1;

    private RectangleF SwatchBounds(
        int index)
    {
        var x =
            RingGap + 1f
            + index * (Diameter + Spacing);

        return new RectangleF(
            x,
            (Height - Diameter) / 2f,
            Diameter,
            Diameter);
    }

    private int HitTest(
        PointF location)
    {
        for (var i = 0; i < Count; i++)
        {
            var hit =
                RectangleF.Inflate(
                    SwatchBounds(i),
                    Spacing / 2f,
                    RingGap);

            if (hit.Contains(location))
                return i;
        }

        return -1;
    }

    private string NameOf(
        int index)
    {
        return index <= 0
            ? "No color"
            : _presets[index - 1].Name;
    }

    protected override void OnMouseMove(
        MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var index =
            HitTest(e.Location);

        if (index == _hoverIndex)
            return;

        _hoverIndex = index;

        Cursor =
            index >= 0
                ? Cursors.Pointer
                : Cursors.Default;

        Invalidate();
    }

    protected override void OnMouseLeave(
        MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverIndex = -1;
        Invalidate();
    }

    protected override void OnMouseUp(
        MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!Enabled
            || (e.Buttons & MouseButtons.Primary) == 0)
        {
            return;
        }

        var index =
            HitTest(e.Location);

        if (index < 0)
            return;

        _selectedIndex = index;
        Invalidate();
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.AntiAlias = true;

        for (var i = 0; i < Count; i++)
        {
            var bounds =
                SwatchBounds(i);

            if (i == _selectedIndex
                || i == _hoverIndex)
            {
                var ring =
                    RectangleF.Inflate(
                        bounds,
                        RingGap,
                        RingGap);

                g.FillEllipse(
                    i == _selectedIndex
                        ? Ui.Ink
                        : Ui.RuleStrong,
                    ring);

                g.FillEllipse(
                    Ui.Surface,
                    RectangleF.Inflate(
                        ring,
                        -1.5f,
                        -1.5f));
            }

            if (i == 0)
            {
                // "No color": an outlined circle with a slash.
                g.FillEllipse(
                    Ui.Rule,
                    bounds);

                g.FillEllipse(
                    Ui.Surface,
                    RectangleF.Inflate(
                        bounds,
                        -1f,
                        -1f));

                using var pen =
                    new Pen(
                        Ui.RuleStrong,
                        1.2f);

                g.DrawLine(
                    pen,
                    bounds.Left + 4f,
                    bounds.Bottom - 4f,
                    bounds.Right - 4f,
                    bounds.Top + 4f);

                continue;
            }

            var color =
                Color.Parse(
                    _presets[i - 1].Hex);

            // Hairline outline so the lightest gray is visible.
            g.FillEllipse(
                Ui.Rule,
                bounds);

            g.FillEllipse(
                color,
                RectangleF.Inflate(
                    bounds,
                    -1f,
                    -1f));
        }

        var labelIndex =
            _hoverIndex >= 0
                ? _hoverIndex
                : _selectedIndex;

        var label =
            NameOf(labelIndex);

        var font =
            Ui.BodyFont;

        var labelSize =
            font.MeasureString(label);

        g.DrawText(
            font,
            _hoverIndex >= 0 && _hoverIndex != _selectedIndex
                ? Ui.Muted
                : Ui.Ink,
            SwatchBounds(Count - 1).Right + RingGap + 12f,
            (Height - labelSize.Height) / 2f,
            label);
    }
}
