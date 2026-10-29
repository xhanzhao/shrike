using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// Rounded custom button used by Shrike.
///
/// Border rendering intentionally uses two filled rounded rectangles
/// instead of DrawPath + Pen. This avoids uneven 1px strokes under
/// Windows DPI scaling.
/// </summary>
public sealed class RoundedButton : Drawable
{
    private bool _hover;
    private bool _mouseDown;

    private string _text = string.Empty;
    private Image? _image;
    private Color? _customBackgroundColor;

    public event EventHandler? Click;

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;

            if (Loaded)
                Invalidate();
        }
    }

    public Image? Image
    {
        get => _image;
        set
        {
            _image = value;

            if (Loaded)
                Invalidate();
        }
    }

    public Font? TextFont { get; set; }

    /// <summary>
    /// Optional custom background color. Null uses the normal system color.
    /// Hover / pressed shades and readable text color are derived automatically.
    /// </summary>
    public Color? CustomBackgroundColor
    {
        get => _customBackgroundColor;
        set
        {
            _customBackgroundColor = value;

            if (Loaded)
                Invalidate();
        }
    }

    public float CornerRadius { get; set; } = 8f;

    public float BorderWidth { get; set; } = 1f;

    public int IconSize { get; set; } = 24;

    public int HorizontalPadding { get; set; } = 12;

    public int IconTextSpacing { get; set; } = 9;

    public bool CenterContent { get; set; }

    public RoundedButton()
    {
        Height = 40;
        CanFocus = true;
        Cursor = Cursors.Pointer;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _mouseDown = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!Enabled)
            return;

        if ((e.Buttons & MouseButtons.Primary) != 0)
        {
            _mouseDown = true;
            Focus();
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!Enabled)
            return;

        var wasMouseDown = _mouseDown;
        _mouseDown = false;
        Invalidate();

        if (!wasMouseDown)
            return;

        if ((e.Buttons & MouseButtons.Primary) == 0)
            return;

        var bounds = new Rectangle(Size);

        if (bounds.Contains((Point)e.Location))
            Click?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!Enabled)
            return;

        if (e.Key == Keys.Enter || e.Key == Keys.Space)
        {
            e.Handled = true;
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (Width <= 2 || Height <= 2)
            return;

        e.Graphics.AntiAlias = true;

        var baseBackground =
            CustomBackgroundColor
            ??
            SystemColors.ControlBackground;

        var fillColor =
            baseBackground;

        if (_hover)
        {
            fillColor =
                Color.Blend(
                    baseBackground,
                    Colors.Black,
                    0.06f);
        }

        if (_mouseDown)
        {
            fillColor =
                Color.Blend(
                    baseBackground,
                    Colors.Black,
                    0.12f);
        }

        var borderReference =
            CustomBackgroundColor.HasValue
                ? GetReadableTextColor(
                    baseBackground)
                : SystemColors.ControlText;

        var borderColor =
            Color.Blend(
                baseBackground,
                borderReference,
                0.20f);

        if (HasFocus)
        {
            borderColor =
                Color.Blend(
                    borderColor,
                    SystemColors.ControlText,
                    0.45f);
        }

        DrawUniformRoundedBorder(
            e.Graphics,
            borderColor,
            fillColor);

        DrawContent(
            e.Graphics);
    }

    private void DrawUniformRoundedBorder(
        Graphics graphics,
        Color borderColor,
        Color fillColor)
    {
        var border =
            Math.Max(
                1f,
                BorderWidth);

        // Outer shape is kept one logical pixel away from the
        // drawable edge, avoiding clipping by the control bounds.
        var outerInset = 1f;

        var outerRect =
            new RectangleF(
                outerInset,
                outerInset,
                Math.Max(
                    0f,
                    Width - outerInset * 2f),
                Math.Max(
                    0f,
                    Height - outerInset * 2f));

        using var outerPath =
            GraphicsPath.GetRoundRect(
                outerRect,
                CornerRadius);

        graphics.FillPath(
            borderColor,
            outerPath);

        // The border is produced by the difference between
        // the outer fill and the inner fill.
        // No Pen / DrawPath is used.
        var innerInset =
            outerInset + border;

        var innerRect =
            new RectangleF(
                innerInset,
                innerInset,
                Math.Max(
                    0f,
                    Width - innerInset * 2f),
                Math.Max(
                    0f,
                    Height - innerInset * 2f));

        var innerRadius =
            Math.Max(
                1f,
                CornerRadius - border);

        using var innerPath =
            GraphicsPath.GetRoundRect(
                innerRect,
                innerRadius);

        graphics.FillPath(
            fillColor,
            innerPath);
    }

    private static Color GetReadableTextColor(
        Color background)
    {
        var luminance =
            0.2126f * background.R
            +
            0.7152f * background.G
            +
            0.0722f * background.B;

        return luminance < 0.52f
            ? Colors.White
            : Colors.Black;
    }

    private void DrawContent(Graphics graphics)
    {
        var font =
            TextFont ??
            SystemFonts.Default();

        var textColor =
            Enabled
                ? CustomBackgroundColor.HasValue
                    ? GetReadableTextColor(
                        CustomBackgroundColor.Value)
                    : SystemColors.ControlText
                : SystemColors.DisabledText;

        var hasText =
            !string.IsNullOrWhiteSpace(
                Text);

        var hasImage =
            Image is not null;

        var textSize =
            hasText
                ? font.MeasureString(Text)
                : SizeF.Empty;

        float contentWidth =
            textSize.Width;

        if (hasImage)
        {
            contentWidth +=
                IconSize;

            if (hasText)
                contentWidth += IconTextSpacing;
        }

        float contentX;

        if (CenterContent)
        {
            contentX =
                Math.Max(
                    0f,
                    (Width - contentWidth) / 2f);
        }
        else
        {
            contentX =
                HorizontalPadding;
        }

        if (hasImage)
        {
            var iconY =
                (Height - IconSize) / 2f;

            graphics.DrawImage(
                Image!,
                contentX,
                iconY,
                IconSize,
                IconSize);

            contentX += IconSize;

            if (hasText)
                contentX += IconTextSpacing;
        }

        if (!hasText)
            return;

        var textY =
            (Height - textSize.Height) / 2f;

        var availableWidth =
            Math.Max(
                0f,
                Width -
                contentX -
                HorizontalPadding);

        using var textBrush =
            new SolidBrush(
                textColor);

        var textRectangle =
            new RectangleF(
                contentX,
                textY,
                availableWidth,
                textSize.Height + 3f);

        graphics.DrawText(
            font,
            textBrush,
            textRectangle,
            Text,
            FormattedTextWrapMode.None,
            FormattedTextAlignment.Left,
            FormattedTextTrimming.CharacterEllipsis);
    }
}
