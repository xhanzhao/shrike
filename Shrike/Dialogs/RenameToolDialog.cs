using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Dialogs;

public sealed class RenameToolDialog : Dialog<bool>
{
    private readonly TextBox _nameTextBox;

    public string NewName =>
        (_nameTextBox.Text ?? string.Empty).Trim();

    public RenameToolDialog(string currentName)
    {
        Title = "Rename Tool";
        Width = 420;
        Resizable = false;
        Padding = 12;

        _nameTextBox = new TextBox
        {
            Text = currentName ?? string.Empty
        };

        var saveButton = new Button
        {
            Text = "Save"
        };

        saveButton.Click +=
            (_, _) => TryAccept();

        var cancelButton = new Button
        {
            Text = "Cancel"
        };

        cancelButton.Click +=
            (_, _) => Close(false);

        DefaultButton = saveButton;
        AbortButton = cancelButton;

        var layout = new DynamicLayout
        {
            Spacing = new Size(8, 8)
        };

        layout.AddRow(
            new Label
            {
                Text = "Tool name"
            });

        layout.AddRow(_nameTextBox);

        layout.AddSeparateRow(
            null,
            saveButton,
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

        Close(true);
    }
}
