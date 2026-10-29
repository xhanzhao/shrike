using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;

namespace Shrike.Dialogs;

public sealed class CreateToolDialog : Dialog<bool>
{
    private const int DialogHeight = 320;

    private const int InputActionButtonWidth = 80;

    private readonly TextBox
        _nameTextBox;

    private readonly TextBox
        _categoryTextBox;

    private readonly TextArea
        _tooltipTextArea;

    private readonly TextBox
        _pythonPathTextBox;

    private readonly TextBox
        _iconPathTextBox;

    public CreateToolRequest? Request
    {
        get;
        private set;
    }

    public CreateToolDialog()
    {
        Title = "Create New Tool";
        Width = 600;
        Height = DialogHeight;
        Resizable = false;

        _nameTextBox =
            new TextBox
            {
                PlaceholderText =
                    "e.g. Select By Layer"
            };

        _categoryTextBox =
            new TextBox
            {
                Text = "Utility"
            };

        _tooltipTextArea =
            new TextArea
            {
                Height = 70
            };

        _pythonPathTextBox =
            new TextBox
            {
                ReadOnly = true,
                PlaceholderText =
                    "Select a Rhino 8 Python file..."
            };

        _iconPathTextBox =
            new TextBox
            {
                ReadOnly = true,
                PlaceholderText =
                    "Optional — default icon will be used"
            };

        var browsePythonButton =
            new Button
            {
                Text = "Browse...",
                Width = InputActionButtonWidth
            };

        browsePythonButton.Click +=
            (_, _) =>
                BrowsePython();

        var browseIconButton =
            new Button
            {
                Text = "Browse...",
                Width = InputActionButtonWidth
            };

        browseIconButton.Click +=
            (_, _) =>
                BrowseIcon();

        var pythonInput =
            new TableLayout
            {
                Spacing =
                    new Size(8, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _pythonPathTextBox,
                            scaleWidth: true),

                        new TableCell(
                            browsePythonButton))
                }
            };

        var iconInput =
            new TableLayout
            {
                Spacing =
                    new Size(8, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _iconPathTextBox,
                            scaleWidth: true),

                        new TableCell(
                            browseIconButton))
                }
            };

        var createButton =
            new Button
            {
                Text = "Create Tool"
            };

        createButton.Click +=
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
            createButton;

        AbortButton =
            cancelButton;

        var form =
            new TableLayout
            {
                Padding = 14,

                Spacing =
                    new Size(12, 10),

                Rows =
                {
                    CreateFormRow(
                        "Tool name",
                        _nameTextBox),

                    CreateFormRow(
                        "Category",
                        _categoryTextBox),

                    CreateFormRow(
                        "Python (.py)",
                        pythonInput),

                    CreateFormRow(
                        "Icon (.png)",
                        iconInput),

                    CreateFormRow(
                        "Tooltip",
                        _tooltipTextArea),

                    new TableRow(
                        null,

                        new TableCell(
                            new StackLayout
                            {
                                Orientation =
                                    Orientation.Horizontal,

                                Spacing = 8,

                                Items =
                                {
                                    createButton,
                                    cancelButton
                                }
                            }))
                }
            };

        Content =
            form;

    }

    private static TableRow CreateFormRow(
        string label,
        Control control)
    {
        return new TableRow(
            new TableCell(
                new Label
                {
                    Text = label,
                    Width = 96
                }),

            new TableCell(
                control,
                scaleWidth: true));
    }

    private CreateToolRequest BuildRequest()
    {
        return new CreateToolRequest
        {
            Name =
                (_nameTextBox.Text
                 ?? string.Empty)
                .Trim(),

            Category =
                (_categoryTextBox.Text
                 ?? string.Empty)
                .Trim(),

            Tooltip =
                _tooltipTextArea.Text
                ?? string.Empty,

            Type =
                "python3",

            SourcePythonPath =
                (_pythonPathTextBox.Text
                 ?? string.Empty)
                .Trim(),

            SourceIconPath =
                (_iconPathTextBox.Text
                 ?? string.Empty)
                .Trim()
        };
    }

    private void BrowsePython()
    {
        var dialog =
            new Eto.Forms.OpenFileDialog
            {
                Title =
                    "Select Rhino 8 Python Script",

                CheckFileExists = true,

                MultiSelect = false,

                Filters =
                {
                    new FileFilter(
                        "Python Script",
                        "*.py")
                }
            };

        var result =
            dialog.ShowDialog(this);

        if (result ==
            DialogResult.Cancel)
        {
            return;
        }

        _pythonPathTextBox.Text =
            dialog.FileName;

        if (string.IsNullOrWhiteSpace(
                _nameTextBox.Text))
        {
            _nameTextBox.Text =
                Path.GetFileNameWithoutExtension(
                    dialog.FileName);
        }
    }

    private void BrowseIcon()
    {
        var dialog =
            new Eto.Forms.OpenFileDialog
            {
                Title =
                    "Select Tool Icon",

                CheckFileExists = true,

                MultiSelect = false,

                Filters =
                {
                    new FileFilter(
                        "PNG Image",
                        "*.png")
                }
            };

        var result =
            dialog.ShowDialog(this);

        if (result ==
            DialogResult.Cancel)
        {
            return;
        }

        _iconPathTextBox.Text =
            dialog.FileName;
    }

    private void TryAccept()
    {
        var name =
            (_nameTextBox.Text
             ?? string.Empty)
            .Trim();

        var category =
            (_categoryTextBox.Text
             ?? string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(
                name))
        {
            ShowWarning(
                "Enter a tool name.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                category))
        {
            ShowWarning(
                "Enter a category.");

            return;
        }

        var pythonPath =
            (_pythonPathTextBox.Text
             ?? string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(
                pythonPath)
            ||
            !File.Exists(
                pythonPath))
        {
            ShowWarning(
                "Select a valid .py file.");

            return;
        }

        Request =
            BuildRequest();

        Close(true);
    }

    private static void ShowWarning(
        string message)
    {
        MessageBox.Show(
            message,
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Warning);
    }
}
