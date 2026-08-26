using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Dialogs;

/// <summary>
/// Reusable dialog for renaming a category or creating a category
/// while moving a tool.
/// </summary>
public sealed class CategoryNameDialog : Dialog<bool>
{
    private readonly TextBox
        _categoryTextBox;

    public string CategoryName =>
        (_categoryTextBox.Text
         ?? string.Empty)
        .Trim();

    public CategoryNameDialog(
        string title,
        string initialName,
        string actionText)
    {
        Title = title;
        Width = 420;
        Resizable = false;

        _categoryTextBox =
            new TextBox
            {
                Text =
                    initialName
                    ?? string.Empty
            };

        var actionButton =
            new Button
            {
                Text =
                    actionText
            };

        actionButton.Click +=
            (_, _) =>
                TryAccept();

        var cancelButton =
            new Button
            {
                Text = "Cancel"
            };

        cancelButton.Click +=
            (_, _) =>
                Close(false);

        DefaultButton =
            actionButton;

        AbortButton =
            cancelButton;

        var layout =
            new DynamicLayout
            {
                Padding = 12,
                Spacing =
                    new Size(8, 8)
            };

        layout.AddRow(
            new Label
            {
                Text = "Category name"
            },
            _categoryTextBox);

        layout.AddSeparateRow(
            null,
            actionButton,
            cancelButton);

        Content =
            layout;
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(
                CategoryName))
        {
            MessageBox.Show(
                "Category name cannot be empty.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);

            return;
        }

        Close(true);
    }
}
