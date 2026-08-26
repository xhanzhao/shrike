using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;

namespace Shrike.Dialogs;

public sealed class ImportToolDialog : Dialog<bool>
{
    private readonly TextBox
        _nameTextBox;

    private readonly TextBox
        _categoryTextBox;

    public string ToolName =>
        (_nameTextBox.Text
         ?? string.Empty)
        .Trim();

    public string Category =>
        (_categoryTextBox.Text
         ?? string.Empty)
        .Trim();

    public ImportToolDialog(
        ToolPackageInfo packageInfo)
    {
        Title = "Import Shrike Tool";
        Width = 440;
        Resizable = false;

        _nameTextBox =
            new TextBox
            {
                Text =
                    packageInfo.Name
            };

        _categoryTextBox =
            new TextBox
            {
                Text =
                    packageInfo.Category
            };

        var importButton =
            new Button
            {
                Text = "Import"
            };

        importButton.Click +=
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
            importButton;

        AbortButton =
            cancelButton;

        var infoLabel =
            new Label
            {
                Text =
                    $"Type: {packageInfo.Type}    Entry: {packageInfo.Entry}"
            };

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
                Text = "Tool Name"
            },
            _nameTextBox);

        layout.AddRow(
            new Label
            {
                Text = "Category"
            },
            _categoryTextBox);

        layout.AddRow(
            null,
            infoLabel);

        layout.AddSeparateRow(
            null,
            importButton,
            cancelButton);

        Content =
            layout;
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(
                ToolName))
        {
            MessageBox.Show(
                "Tool name cannot be empty.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);

            return;
        }

        if (string.IsNullOrWhiteSpace(
                Category))
        {
            MessageBox.Show(
                "Category cannot be empty.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);

            return;
        }

        Close(true);
    }
}
