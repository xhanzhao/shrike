using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;

namespace Shrike.Dialogs;

public sealed class CreateToolDialog : Dialog<bool>
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _categoryTextBox;
    private readonly TextArea _tooltipTextArea;
    private readonly TextBox _pythonPathTextBox;
    private readonly TextBox _iconPathTextBox;

    public CreateToolRequest? Request { get; private set; }

    public CreateToolDialog()
    {
        Title = "Create New Tool";
        Width = 600;
        Resizable = false;
        Padding = 12;

        _nameTextBox = new TextBox
        {
            PlaceholderText = "e.g. Select By Layer"
        };

        _categoryTextBox = new TextBox
        {
            Text = "Utility"
        };

        _tooltipTextArea = new TextArea
        {
            Height = 60
        };

        _pythonPathTextBox = new TextBox
        {
            ReadOnly = true,
            PlaceholderText =
                "Select a Rhino 8 Python file..."
        };

        _iconPathTextBox = new TextBox
        {
            ReadOnly = true,
            PlaceholderText =
                "Optional — default icon will be used"
        };

        var browsePythonButton = new Button
        {
            Text = "Browse..."
        };
        browsePythonButton.Click += (_, _) => BrowsePython();

        var browseIconButton = new Button
        {
            Text = "Browse..."
        };
        browseIconButton.Click += (_, _) => BrowseIcon();

        var createButton = new Button
        {
            Text = "Create Tool"
        };
        createButton.Click += (_, _) => TryAccept();

        var cancelButton = new Button
        {
            Text = "Cancel"
        };
        cancelButton.Click += (_, _) => Close(false);

        DefaultButton = createButton;
        AbortButton = cancelButton;

        var pythonRow = new TableLayout
        {
            Spacing = new Size(6, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(
                        _pythonPathTextBox,
                        scaleWidth: true),
                    browsePythonButton)
            }
        };

        var iconRow = new TableLayout
        {
            Spacing = new Size(6, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(
                        _iconPathTextBox,
                        scaleWidth: true),
                    browseIconButton)
            }
        };

        var layout = new DynamicLayout
        {
            Spacing = new Size(8, 8)
        };

        layout.AddRow(
            new Label { Text = "Tool name" },
            _nameTextBox);

        layout.AddRow(
            new Label { Text = "Category" },
            _categoryTextBox);

        layout.AddRow(
            new Label { Text = "Python (.py)" },
            pythonRow);

        layout.AddRow(
            new Label { Text = "Icon (.png)" },
            iconRow);

        layout.AddRow(
            new Label { Text = "Tooltip" },
            _tooltipTextArea);

        layout.AddSeparateRow(
            null,
            createButton,
            cancelButton);

        Content = layout;
    }

    private void BrowsePython()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Rhino 8 Python Script",
            CheckFileExists = true,
            MultiSelect = false,
            Filters =
            {
                new FileFilter(
                    "Python Script",
                    "*.py")
            }
        };

        var result = dialog.ShowDialog(this);

        if (result == DialogResult.Cancel)
            return;

        _pythonPathTextBox.Text = dialog.FileName;

        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            _nameTextBox.Text =
                Path.GetFileNameWithoutExtension(
                    dialog.FileName);
        }
    }

    private void BrowseIcon()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Tool Icon",
            CheckFileExists = true,
            MultiSelect = false,
            Filters =
            {
                new FileFilter(
                    "PNG Image",
                    "*.png")
            }
        };

        var result = dialog.ShowDialog(this);

        if (result == DialogResult.Cancel)
            return;

        _iconPathTextBox.Text = dialog.FileName;
    }

    private void TryAccept()
    {
        var name =
            (_nameTextBox.Text ?? string.Empty).Trim();

        var category =
            (_categoryTextBox.Text ?? string.Empty).Trim();

        var pythonPath =
            (_pythonPathTextBox.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(
                this,
                "Enter a tool name.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            MessageBox.Show(
                this,
                "Enter a category.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(pythonPath)
            || !File.Exists(pythonPath))
        {
            MessageBox.Show(
                this,
                "Select a valid .py file.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);
            return;
        }

        Request = new CreateToolRequest
        {
            Name = name,
            Category = category,
            Tooltip =
                _tooltipTextArea.Text ?? string.Empty,
            SourcePythonPath = pythonPath,
            SourceIconPath =
                (_iconPathTextBox.Text ?? string.Empty).Trim()
        };

        Close(true);
    }
}
