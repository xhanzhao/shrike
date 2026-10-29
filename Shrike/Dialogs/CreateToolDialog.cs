using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;
using Shrike.Services;

namespace Shrike.Dialogs;

public sealed class CreateToolDialog : Dialog<bool>
{
    private const int PythonDialogHeight = 350;
    private const int MacroDialogHeight = 400;

    private const int CodePreviewHeight = 220;

    private const int PythonInputHeight = 28;
    private const int MacroInputHeight = 78;

    private const int InputActionButtonWidth = 80;

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

    private readonly Control
        _macroInputControl;

    private readonly TextArea
        _codePreviewTextArea;

    private readonly Label
        _codeWarningLabel;

    private readonly TableRow
        _codePreviewRow;

    // Python source from the clipboard or a dropped file.
    // When set it is used instead of the selected .py path.
    private string
        _sourceCode =
            string.Empty;

    public bool RecordMacroRequested
    {
        get;
        private set;
    }

    public CreateToolRequest? Request
    {
        get;
        private set;
    }

    private string SelectedToolType =>
        _typeDropDown.SelectedIndex == 1
            ? "macro"
            : "python3";

    public CreateToolRequest DraftRequest =>
        BuildRequest();

    public CreateToolDialog(
        CreateToolRequest? initialRequest = null,
        ClipboardCodeResult? initialCode = null)
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

        var recordMacroButton =
            new Button
            {
                Text = "Record",
                Width = InputActionButtonWidth
            };

        recordMacroButton.Click +=
            (_, _) =>
                RequestMacroRecording();

        var pastePythonButton =
            new Button
            {
                Text = "Paste",
                Width = InputActionButtonWidth,
                ToolTip =
                    "Use Python code or a .py file from the clipboard"
            };

        pastePythonButton.Click +=
            (_, _) =>
                PasteFromClipboard();

        _codePreviewTextArea =
            new TextArea
            {
                ReadOnly = true,
                Wrap = false,
                Height = CodePreviewHeight,
                Font = Fonts.Monospace(9)
            };

        _codeWarningLabel =
            new Label
            {
                Wrap = WrapMode.Word,
                Visible = false
            };

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
                            browsePythonButton),

                        new TableCell(
                            pastePythonButton))
                }
            };

        _macroInputControl =
            new TableLayout
            {
                Spacing =
                    new Size(8, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _macroTextArea,
                            scaleWidth: true),

                        new TableCell(
                            new StackLayout
                            {
                                Orientation =
                                    Orientation.Vertical,

                                VerticalContentAlignment =
                                    VerticalAlignment.Center,

                                Items =
                                {
                                    recordMacroButton
                                }
                            }))
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

        _codePreviewRow =
            new TableRow(
                new TableCell(
                    new Label
                    {
                        Text = "Code"
                    }),

                new TableCell(
                    new StackLayout
                    {
                        Orientation =
                            Orientation.Vertical,

                        Spacing = 6,

                        HorizontalContentAlignment =
                            HorizontalAlignment.Stretch,

                        Items =
                        {
                            _codeWarningLabel,
                            _codePreviewTextArea
                        }
                    },
                    scaleWidth: true));

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

                    _codePreviewRow,

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

        ApplyInitialRequest(
            initialRequest);

        if (initialCode is not null)
        {
            ApplyCode(
                initialCode,
                fillFields: true);
        }

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
                PythonDialogHeight
                + (HasSourceCode
                    ? CodePreviewHeight + 40
                    : 0);
        }
        else
        {
            _typeInputLabel.Text =
                "Macro";

            _typeInputHost.Height =
                MacroInputHeight;

            _typeInputHost.Content =
                _macroInputControl;

            Height =
                MacroDialogHeight;
        }

        SetCodePreviewVisible(
            isPython
            && HasSourceCode);

        _typeInputHost.Invalidate();
        Invalidate();
    }

    private void ApplyInitialRequest(
        CreateToolRequest? initialRequest)
    {
        if (initialRequest is null)
            return;

        _typeDropDown.SelectedIndex =
            string.Equals(
                initialRequest.Type,
                "macro",
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                initialRequest.Type,
                "rhino-macro",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

        _nameTextBox.Text =
            initialRequest.Name
            ?? string.Empty;

        _categoryTextBox.Text =
            string.IsNullOrWhiteSpace(
                initialRequest.Category)
                ? "Utility"
                : initialRequest.Category;

        _tooltipTextArea.Text =
            initialRequest.Tooltip
            ?? string.Empty;

        _pythonPathTextBox.Text =
            initialRequest.SourcePythonPath
            ?? string.Empty;

        _macroTextArea.Text =
            initialRequest.Macro
            ?? string.Empty;

        _iconPathTextBox.Text =
            initialRequest.SourceIconPath
            ?? string.Empty;

        // A draft can carry pasted code, for example when the user
        // switches to macro recording and comes back.
        if (!string.IsNullOrWhiteSpace(
                initialRequest.SourceCode))
        {
            ApplyCode(
                ClipboardCodeParser.Parse(
                    initialRequest.SourceCode),
                fillFields: false);
        }
    }

    private bool HasSourceCode =>
        !string.IsNullOrWhiteSpace(
            _sourceCode);

    private void PasteFromClipboard()
    {
        ClipboardCodeResult code;

        try
        {
            code =
                ClipboardSource.ReadCode();
        }
        catch (Exception ex)
        {
            ShowWarning(
                "Shrike could not read the clipboard.\n\n" +
                ex.Message);

            return;
        }

        if (!code.HasCode)
        {
            ShowWarning(
                "The clipboard does not contain any text " +
                "or a .py file.");

            return;
        }

        ApplyCode(
            code,
            fillFields: true);
    }

    private void ApplyCode(
        ClipboardCodeResult code,
        bool fillFields)
    {
        if (!code.HasCode)
            return;

        _typeDropDown.SelectedIndex = 0;

        _sourceCode =
            code.Code;

        _pythonPathTextBox.Text =
            code.Summary;

        _codePreviewTextArea.Text =
            code.Code;

        _codeWarningLabel.Text =
            string.Join(
                "\n",
                code.Warnings);

        _codeWarningLabel.Visible =
            code.Warnings.Count > 0;

        if (fillFields)
        {
            FillIfEmpty(
                _nameTextBox,
                code.SuggestedName);

            if (!string.IsNullOrWhiteSpace(
                    code.SuggestedCategory))
            {
                _categoryTextBox.Text =
                    code.SuggestedCategory;
            }

            if (string.IsNullOrWhiteSpace(
                    _tooltipTextArea.Text)
                &&
                !string.IsNullOrWhiteSpace(
                    code.SuggestedTooltip))
            {
                _tooltipTextArea.Text =
                    code.SuggestedTooltip;
            }
        }

        UpdateToolTypeUi();
    }

    private void ClearSourceCode()
    {
        _sourceCode =
            string.Empty;

        _codePreviewTextArea.Text =
            string.Empty;

        _codeWarningLabel.Visible =
            false;

        UpdateToolTypeUi();
    }

    private void SetCodePreviewVisible(
        bool visible)
    {
        foreach (var cell in
                 _codePreviewRow.Cells)
        {
            if (cell.Control is not null)
            {
                cell.Control.Visible =
                    visible;
            }
        }
    }

    private static void FillIfEmpty(
        TextBox textBox,
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                textBox.Text)
            &&
            !string.IsNullOrWhiteSpace(
                value))
        {
            textBox.Text =
                value;
        }
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
                SelectedToolType,

            SourcePythonPath =
                HasSourceCode
                    ? string.Empty
                    : (_pythonPathTextBox.Text
                       ?? string.Empty)
                    .Trim(),

            SourceCode =
                _sourceCode,

            Macro =
                (_macroTextArea.Text
                 ?? string.Empty)
                .Trim(),

            SourceIconPath =
                (_iconPathTextBox.Text
                 ?? string.Empty)
                .Trim()
        };
    }

    private void RequestMacroRecording()
    {
        if (!string.Equals(
                SelectedToolType,
                "macro",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RecordMacroRequested = true;

        // Close the modal dialog so Rhino becomes interactive again.
        // ToolboxPanel will start recording and reopen this dialog
        // with the recorded macro after Stop is clicked.
        Close(false);
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

        ClearSourceCode();

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
            if (!HasSourceCode
                &&
                (string.IsNullOrWhiteSpace(
                     pythonPath)
                 ||
                 !File.Exists(
                     pythonPath)))
            {
                ShowWarning(
                    "Select a .py file or paste Python code.");

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
