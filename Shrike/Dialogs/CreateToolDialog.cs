using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;

namespace Shrike.Dialogs;

public sealed class CreateToolDialog : Dialog<bool>
{
    private const int PythonDialogHeight = 350;
    private const int MacroDialogHeight = 400;

    private const int PythonInputHeight = 28;
    private const int MacroInputHeight = 78;

    private readonly DropDown
        _typeDropDown;

    private readonly TextBox
        _nameTextBox;

    private readonly TextBox
        _categoryTextBox;

    private readonly TextArea
        _tooltipTextArea;

    private readonly TextBox
        _pythonPathTextBox;

    private readonly TextArea
        _macroTextArea;

    private readonly TextBox
        _iconPathTextBox;

    private readonly Label
        _typeInputLabel;

    private readonly Panel
        _typeInputHost;

    private readonly Control
        _pythonInputControl;

    public CreateToolRequest? Request
    {
        get;
        private set;
    }

    private string SelectedToolType =>
        _typeDropDown.SelectedIndex == 1
            ? "macro"
            : "python3";

    public CreateToolDialog()
    {
        Title = "Create New Tool";
        Width = 600;
        Height = PythonDialogHeight;
        Resizable = false;

        _typeDropDown =
            new DropDown();

        _typeDropDown.Items.Add(
            new ListItem
            {
                Text = "Python 3"
            });

        _typeDropDown.Items.Add(
            new ListItem
            {
                Text = "Rhino Macro"
            });

        _typeDropDown.SelectedIndex = 0;

        _nameTextBox =
            new TextBox
            {
                PlaceholderText =
                    "e.g. Select By Layer"
            };

        _categoryTextBox =
            new TextBox
            {
                Text = "Utilities"
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

        _macroTextArea =
            new TextArea
            {
                Height = 70
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
                Text = "Browse..."
            };

        browsePythonButton.Click +=
            (_, _) =>
                BrowsePython();

        var browseIconButton =
            new Button
            {
                Text = "Browse..."
            };

        browseIconButton.Click +=
            (_, _) =>
                BrowseIcon();

        _pythonInputControl =
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

        _typeInputLabel =
            new Label
            {
                Text = "Python (.py)"
            };

        _typeInputHost =
            new Panel
            {
                Height =
                    PythonInputHeight,

                Content =
                    _pythonInputControl
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
                        "Tool Type",
                        _typeDropDown),

                    CreateFormRow(
                        "Tool name",
                        _nameTextBox),

                    CreateFormRow(
                        "Category",
                        _categoryTextBox),

                    new TableRow(
                        new TableCell(
                            _typeInputLabel),

                        new TableCell(
                            _typeInputHost,
                            scaleWidth: true)),

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

        _typeDropDown.SelectedIndexChanged +=
            (_, _) =>
                UpdateToolTypeUi();

        UpdateToolTypeUi();
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

    private void UpdateToolTypeUi()
    {
        var isPython =
            string.Equals(
                SelectedToolType,
                "python3",
                StringComparison.OrdinalIgnoreCase);

        if (isPython)
        {
            _typeInputLabel.Text =
                "Python (.py)";

            _typeInputHost.Height =
                PythonInputHeight;

            _typeInputHost.Content =
                _pythonInputControl;

            Height =
                PythonDialogHeight;
        }
        else
        {
            _typeInputLabel.Text =
                "Macro";

            _typeInputHost.Height =
                MacroInputHeight;

            _typeInputHost.Content =
                _macroTextArea;

            Height =
                MacroDialogHeight;
        }

        _typeInputHost.Invalidate();
        Invalidate();
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

        var type =
            SelectedToolType;

        var pythonPath =
            (_pythonPathTextBox.Text
             ?? string.Empty)
            .Trim();

        var macro =
            (_macroTextArea.Text
             ?? string.Empty)
            .Trim();

        if (string.Equals(
                type,
                "python3",
                StringComparison.OrdinalIgnoreCase))
        {
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
        }
        else
        {
            if (string.IsNullOrWhiteSpace(
                    macro))
            {
                ShowWarning(
                    "Enter a Rhino macro.");

                return;
            }
        }

        Request =
            new CreateToolRequest
            {
                Name =
                    name,

                Category =
                    category,

                Tooltip =
                    _tooltipTextArea.Text
                    ?? string.Empty,

                Type =
                    type,

                SourcePythonPath =
                    pythonPath,

                Macro =
                    macro,

                SourceIconPath =
                    (_iconPathTextBox.Text
                     ?? string.Empty)
                    .Trim()
            };

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
