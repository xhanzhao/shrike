using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// Shrike's black-and-white design tokens.
///
/// Every color is derived from Rhino's system colors, so dialogs
/// follow Rhino's light and dark themes without a separate palette.
/// </summary>
internal static class Ui
{
    // Spacing scale, in logical pixels.
    public const int Gap = 6;
    public const int FieldGap = 14;
    public const int DialogPadding = 20;

    public const int FieldHeight = 30;
    public const int ButtonHeight = 30;
    public const float FieldRadius = 5f;

    /// <summary>Dialog body and input fields.</summary>
    public static Color Surface =>
        SystemColors.WindowBackground;

    /// <summary>Footer bar behind the dialog buttons.</summary>
    public static Color Footer =>
        Color.Blend(
            Surface,
            Ink,
            0.04f);

    /// <summary>Primary text, focus rings and the primary button.</summary>
    public static Color Ink =>
        SystemColors.ControlText;

    /// <summary>Field labels and secondary text.</summary>
    public static Color Muted =>
        Color.Blend(
            Surface,
            Ink,
            0.58f);

    /// <summary>Field borders and dividers.</summary>
    public static Color Rule =>
        Color.Blend(
            Surface,
            Ink,
            0.18f);

    /// <summary>Highlighted row in a list.</summary>
    public static Color Hover =>
        Color.Blend(
            Surface,
            Ink,
            0.07f);

    /// <summary>Hover state of borders.</summary>
    public static Color RuleStrong =>
        Color.Blend(
            Surface,
            Ink,
            0.40f);

    public static Font BodyFont =>
        SystemFonts.Default();

    public static Font TitleFont =>
        new(
            SystemFonts.Default().Family,
            SystemFonts.Default().Size + 4f,
            FontStyle.Bold);

    public static Font StrongFont =>
        new(
            SystemFonts.Default().Family,
            SystemFonts.Default().Size,
            FontStyle.Bold);

    public static Font SmallFont =>
        new(
            SystemFonts.Default().Family,
            Math.Max(
                7f,
                SystemFonts.Default().Size - 1f));

    public static Label Title(
        string text)
    {
        return new Label
        {
            Text = text,
            Font = TitleFont,
            TextColor = Ink
        };
    }

    public static Label Description(
        string text)
    {
        return new Label
        {
            Text = text,
            Wrap = WrapMode.Word,
            TextColor = Muted
        };
    }

    public static Label FieldLabel(
        string text)
    {
        return new Label
        {
            Text = text,
            TextColor = Muted
        };
    }

    /// <summary>
    /// A label stacked above its field, the layout used by every
    /// Shrike form.
    /// </summary>
    public static Control Field(
        string label,
        Control input)
    {
        return new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = Gap,

            HorizontalContentAlignment =
                HorizontalAlignment.Stretch,

            Items =
            {
                FieldLabel(label),
                input
            }
        };
    }

    public static StackLayout Stack(
        int spacing,
        params Control?[] items)
    {
        var stack =
            new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = spacing,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch
            };

        foreach (var item in items)
        {
            if (item is not null)
                stack.Items.Add(item);
        }

        return stack;
    }

    /// <summary>
    /// A flat button. The primary button is solid ink with
    /// inverted text; the rest are outlined.
    /// </summary>
    public static RoundedButton Button(
        string text,
        bool primary = false,
        int? width = null)
    {
        var textWidth =
            (int)Math.Ceiling(
                BodyFont.MeasureString(text).Width);

        return new RoundedButton
        {
            Text = text,
            Height = ButtonHeight,
            Width = width ?? Math.Max(84, textWidth + 32),
            CornerRadius = FieldRadius,
            BorderWidth = 1f,
            CenterContent = true,
            HorizontalPadding = 12,
            TextFont = primary ? StrongFont : BodyFont,
            CustomBackgroundColor = primary ? Ink : null
        };
    }

    public static TextBox TextBox(
        string? text = null,
        string? placeholder = null)
    {
        return new TextBox
        {
            Text = text ?? string.Empty,
            PlaceholderText = placeholder ?? string.Empty,
            ShowBorder = false,
            BackgroundColor = Surface,
            TextColor = Ink
        };
    }

    public static TextArea TextArea(
        string? text = null,
        int height = 52)
    {
        return new TextArea
        {
            Text = text ?? string.Empty,
            Border = BorderType.None,
            Wrap = true,
            AcceptsTab = false,
            // Enter runs the dialog's primary action.
            AcceptsReturn = false,
            Height = height,
            BackgroundColor = Surface,
            TextColor = Ink
        };
    }

    public static bool IsDark(
        Color color)
    {
        var luminance =
            0.2126f * color.R
            + 0.7152f * color.G
            + 0.0722f * color.B;

        return luminance < 0.52f;
    }
}
