using Eto.Drawing;
using Eto.Forms;
using Shrike.Controls;
using Shrike.Models;
using Shrike.Services;

namespace Shrike.Dialogs;

public sealed class CreateToolDialog : ShrikeDialog
{
    private const string DefaultCategory = "Utility";

    private const string DefaultToolIconResource =
        "Shrike.Resources.DefaultToolIcon.png";

    private readonly ScriptPicker _scriptPicker;

    private readonly IconTile _iconTile;

    private readonly TextBox _nameTextBox;

    private readonly CategoryField _categoryField;

    private readonly TextArea _tooltipTextArea;

    private string? _iconPath;

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
        : base(
            "Create tool",
            "Add a tested Python script to your toolbox.")
    {
        var categories =
            existingCategories.ToList();

        _scriptPicker =
            new ScriptPicker();

        _scriptPicker.Click +=
            (_, _) =>
                BrowsePython();

        _iconTile =
            new IconTile
            {
                Image =
                    EmbeddedImageLoader.LoadBitmap(
                        DefaultToolIconResource)
            };

        _iconTile.Click +=
            (_, _) =>
                BrowseIcon();

        _nameTextBox =
            Ui.TextBox(
                placeholder:
                    "Filled in from the file name");

        _categoryField =
            new CategoryField(
                categories,
                !string.IsNullOrWhiteSpace(initialCategory)
                    ? initialCategory
                    : categories.Count > 0
                        ? categories[0]
                        : DefaultCategory);

        _tooltipTextArea =
            Ui.TextArea();

        var nameRow =
            new TableLayout
            {
                Spacing =
                    new Size(Ui.Gap, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _iconTile),

                        new TableCell(
                            new FieldFrame(
                                _nameTextBox),
                            scaleWidth: true))
                }
            };

        SetBody(
            Ui.Stack(
                Ui.FieldGap,
                _scriptPicker,
                Ui.Field(
                    "Name",
                    nameRow),
                Ui.Field(
                    "Category",
                    _categoryField),
                Ui.Field(
                    "Tooltip (optional)",
                    new FieldFrame(
                        _tooltipTextArea,
                        multiline: true))));

        AddCancelButton();

        AddButton(
            "Create tool",
            TryAccept,
            primary: true);

        InitialFocus =
            _scriptPicker;
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
                _categoryField.Category,

            Tooltip =
                (_tooltipTextArea.Text
                 ?? string.Empty)
                .Trim(),

            Type =
                "python3",

            SourcePythonPath =
                _scriptPicker.Path
                ?? string.Empty,

            SourceIconPath =
                _iconPath
                ?? string.Empty
        };
    }

    private void BrowsePython()
    {
        var dialog =
            new Eto.Forms.OpenFileDialog
            {
                Title =
                    "Choose a Python script",

                CheckFileExists = true,

                MultiSelect = false,

                Filters =
                {
                    new FileFilter(
                        "Python Script",
                        "*.py")
                }
            };

        if (dialog.ShowDialog(this) ==
            DialogResult.Cancel)
        {
            return;
        }

        _scriptPicker.Path =
            dialog.FileName;

        ClearValidation();

        ApplySuggestions(
            dialog.FileName);

        _nameTextBox.Focus();
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
                    "Choose a tool icon",

                CheckFileExists = true,

                MultiSelect = false,

                Filters =
                {
                    new FileFilter(
                        "PNG Image",
                        "*.png")
                }
            };

        if (dialog.ShowDialog(this) ==
            DialogResult.Cancel)
        {
            return;
        }

        try
        {
            using var stream =
                new MemoryStream(
                    File.ReadAllBytes(
                        dialog.FileName));

            _iconTile.Image =
                new Bitmap(stream);

            _iconPath =
                dialog.FileName;

            ClearValidation();
        }
        catch
        {
            ShowValidation(
                "That PNG could not be read.");
        }
    }

    private void TryAccept()
    {
        var pythonPath =
            _scriptPicker.Path;

        if (string.IsNullOrWhiteSpace(
                pythonPath)
            ||
            !File.Exists(
                pythonPath))
        {
            ShowValidation(
                "Choose a .py script first.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _nameTextBox.Text))
        {
            ShowValidation(
                "Give the tool a name.");

            _nameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(
                _categoryField.Category))
        {
            ShowValidation(
                "Pick or type a category.");

            _categoryField.FocusInput();
            return;
        }

        Request =
            BuildRequest();

        Close(true);
    }
}
