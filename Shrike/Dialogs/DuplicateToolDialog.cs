using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;

namespace Shrike.Dialogs;

public sealed class DuplicateToolDialog : Dialog<bool>
{
    private readonly TextBox _nameTextBox;
    private readonly ComboBox _categoryComboBox;

    public string NewName =>
        (_nameTextBox.Text ?? string.Empty).Trim();

    public string Category =>
        DialogLayout.ReadCategory(_categoryComboBox);

    public DuplicateToolDialog(
        ToolDefinition sourceTool,
        IEnumerable<string> existingCategories)
    {
        Title = "Duplicate Tool";
        Width = 440;
        Resizable = false;
        Padding = 12;

        _nameTextBox = new TextBox
        {
            Text = $"{sourceTool.Name} Copy"
        };

        _categoryComboBox =
            DialogLayout.CategoryComboBox(
                existingCategories,
                sourceTool.Category);

        var duplicateButton = new Button
        {
            Text = "Duplicate"
        };

        duplicateButton.Click +=
            (_, _) => TryAccept();

        var cancelButton = new Button
        {
            Text = "Cancel"
        };

        cancelButton.Click +=
            (_, _) => Close(false);

        DefaultButton = duplicateButton;
        AbortButton = cancelButton;

        var layout = new DynamicLayout
        {
            Spacing = new Size(8, 8)
        };

        layout.AddRow(
            new Label { Text = "New tool name" },
            _nameTextBox);

        layout.AddRow(
            new Label { Text = "Category" },
            _categoryComboBox);

        layout.AddSeparateRow(
            null,
            duplicateButton,
            cancelButton);

        Content = layout;
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            MessageBox.Show(
                this,
                "Tool name cannot be empty.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);

            return;
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            MessageBox.Show(
                this,
                "Category cannot be empty.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);

            return;
        }

        Close(true);
    }
}
