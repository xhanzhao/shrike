using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Dialogs;

/// <summary>
/// Shared building blocks for Shrike's form dialogs,
/// so every dialog lines up the same way.
/// </summary>
internal static class DialogLayout
{
    public const int LabelWidth = 96;

    public const int ActionButtonWidth = 80;

    public static TableRow FormRow(
        string label,
        Control control)
    {
        return new TableRow(
            new TableCell(
                new Label
                {
                    Text = label,
                    Width = LabelWidth,
                    VerticalAlignment =
                        VerticalAlignment.Center
                }),

            new TableCell(
                control,
                scaleWidth: true));
    }

    /// <summary>
    /// A text box with a button on its right, e.g. a path and "Browse...".
    /// </summary>
    public static Control WithButton(
        Control input,
        Button button)
    {
        return new TableLayout
        {
            Spacing =
                new Size(8, 0),

            Rows =
            {
                new TableRow(
                    new TableCell(
                        input,
                        scaleWidth: true),

                    new TableCell(
                        button))
            }
        };
    }

    /// <summary>
    /// Editable drop-down listing the categories that already exist,
    /// so a category is picked instead of retyped.
    /// A new name can still be typed.
    /// </summary>
    public static ComboBox CategoryComboBox(
        IEnumerable<string> existingCategories,
        string initialCategory)
    {
        var categories =
            existingCategories
                .Where(
                    category =>
                        !string.IsNullOrWhiteSpace(
                            category))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    category =>
                        category,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        return new ComboBox
        {
            DataStore = categories,
            AutoComplete = true,
            ReadOnly = false,
            Text = initialCategory
        };
    }

    /// <summary>
    /// Returns the typed category, matched to the casing of an
    /// existing category when one differs only by case.
    /// </summary>
    public static string ReadCategory(
        ComboBox comboBox)
    {
        var text =
            (comboBox.Text
             ?? string.Empty)
            .Trim();

        var existing =
            comboBox.DataStore?
                .OfType<string>()
                .FirstOrDefault(
                    category =>
                        string.Equals(
                            category,
                            text,
                            StringComparison.OrdinalIgnoreCase));

        return existing ?? text;
    }

    public static Control ButtonRow(
        params Control[] buttons)
    {
        var row =
            new StackLayout
            {
                Orientation =
                    Orientation.Horizontal,

                Spacing = 8
            };

        foreach (var button in buttons)
            row.Items.Add(button);

        return row;
    }

    public static Label HintLabel(
        string text)
    {
        return new Label
        {
            Text = text,
            Wrap = WrapMode.Word,
            TextColor =
                SystemColors.DisabledText
        };
    }

    /// <summary>
    /// Resizes a dialog to fit its content height,
    /// e.g. after an Expander opens or closes.
    /// </summary>
    public static void FitHeight(
        Window window)
    {
        if (window.Content is null)
            return;

        var preferred =
            window.Content.GetPreferredSize(
                new SizeF(
                    window.ClientSize.Width,
                    float.PositiveInfinity));

        window.ClientSize =
            new Size(
                window.ClientSize.Width,
                (int)Math.Ceiling(
                    preferred.Height));
    }
}
