using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;
using Shrike.Services;

namespace Shrike.Dialogs;

public sealed class CreateToolDialog : Dialog<bool>
{
    private const string DefaultCategory = "Utility";

    private readonly TextBox
        _pythonPathTextBox;

    private readonly TextBox
        _nameTextBox;

    private readonly ComboBox
        _categoryComboBox;

    private readonly TextArea
        _tooltipTextArea;

    private readonly TextBox
        _iconPathTextBox;

    private readonly Expander
        _moreOptions;

    // The name and tooltip filled in from the last script picked.
    // They are only replaced when the user has not edited them.
    private string _suggestedName =
        string.Empty;

    private string _suggestedTooltip =
        string.Empty;

    public CreateToolRequest? Request
    {
        get;
        private set;
    }

    public CreateToolDialog(
        IEnumerable<string> existingCategories,
        string? initialCategory = null)
    {
        Title = "Create New Tool";
        Width = 600;
        Resizable = false;

        _pythonPathTextBox =
            new TextBox
            {
                ReadOnly = true,
                PlaceholderText =
                    "Select a tested Rhino 8 Python file..."
            };

        _nameTextBox =
            new TextBox
            {
                PlaceholderText =
                    "Filled in from the file name"
            };

        var categories =
            existingCategories.ToList();

        _categoryComboBox =
            DialogLayout.CategoryComboBox(
                categories,
                !string.IsNullOrWhiteSpace(initialCategory)
                    ? initialCategory
                    : categories.Count > 0
                        ? categories[0]
                        : DefaultCategory);

        _tooltipTextArea =
            new TextArea
            {
                Height = 60
            };

        _iconPathTextBox =
            new TextBox
            {
                ReadOnly = true,
                PlaceholderText =
                    "Default icon"
            };

        var browsePythonButton =
            new Button
            {
                Text = "Browse...",
                Width = DialogLayout.ActionButtonWidth
            };

        browsePythonButton.Click +=
            (_, _) =>
                BrowsePython();

        var browseIconButton =
            new Button
            {
                Text = "Browse...",
                Width = DialogLayout.ActionButtonWidth
            };

        browseIconButton.Click +=
            (_, _) =>
                BrowseIcon();

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

        _moreOptions =
            new Expander
            {
                Header =
                    new Label
                    {
                        Text = "More options (tooltip, icon)"
                    },

                Expanded = false,

                Content =
                    new TableLayout
                    {
                        Padding =
                            new Padding(0, 8, 0, 0),

                        Spacing =
                            new Size(12, 10),

                        Rows =
                        {
                            DialogLayout.FormRow(
                                "Tooltip",
                                _tooltipTextArea),

                            DialogLayout.FormRow(
                                "Icon (.png)",
                                DialogLayout.WithButton(
                                    _iconPathTextBox,
                                    browseIconButton))
                        }
                    }
            };

        _moreOptions.ExpandedChanged +=
            (_, _) =>
                DialogLayout.FitHeight(this);

        Content =
            new TableLayout
            {
                Padding = 14,

                Spacing =
                    new Size(12, 10),

                Rows =
                {
                    DialogLayout.FormRow(
                        "Python (.py)",
                        DialogLayout.WithButton(
                            _pythonPathTextBox,
                            browsePythonButton)),

                    DialogLayout.FormRow(
                        "Tool name",
                        _nameTextBox),

                    DialogLayout.FormRow(
                        "Category",
                        _categoryComboBox),

                    new TableRow(
                        new TableCell(
                            _moreOptions)),

                    new TableRow(
                        null,

                        new TableCell(
                            DialogLayout.ButtonRow(
                                createButton,
                                cancelButton)))
                }
            };
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
                DialogLayout.ReadCategory(
                    _categoryComboBox),

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

        ApplySuggestions(
            dialog.FileName);
    }

    /// <summary>
    /// Fills the name and tooltip from the script, unless the user
    /// has already typed their own.
    /// </summary>
    private void ApplySuggestions(
        string scriptPath)
    {
        var currentName =
            _nameTextBox.Text
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentName)
            || currentName == _suggestedName)
        {
            _suggestedName =
                ScriptInfoReader.SuggestName(
                    scriptPath);

            _nameTextBox.Text =
                _suggestedName;
        }

        var currentTooltip =
            _tooltipTextArea.Text
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentTooltip)
            || currentTooltip == _suggestedTooltip)
        {
            _suggestedTooltip =
                ScriptInfoReader.SuggestTooltip(
                    scriptPath);

            _tooltipTextArea.Text =
                _suggestedTooltip;
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

        if (string.IsNullOrWhiteSpace(
                _nameTextBox.Text))
        {
            ShowWarning(
                "Enter a tool name.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                DialogLayout.ReadCategory(
                    _categoryComboBox)))
        {
            ShowWarning(
                "Enter a category.");

            return;
        }

        Request =
            BuildRequest();

        Close(true);
    }

    private void ShowWarning(
        string message)
    {
        MessageBox.Show(
            this,
            message,
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Warning);
    }
}
