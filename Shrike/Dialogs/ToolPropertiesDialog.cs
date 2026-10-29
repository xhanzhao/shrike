using Eto.Drawing;
using Eto.Forms;
using Shrike.Controls;
using Shrike.Models;
using Shrike.Services;

namespace Shrike.Dialogs;

/// <summary>
/// One place to customize a tool: name, icon, category, tooltip,
/// color, script, and version history.
///
/// Field changes are only applied when the user clicks Save,
/// which makes the result true. A rollback from Version History
/// is applied right away and is reported by ScriptRolledBack.
/// </summary>
public sealed class ToolPropertiesDialog : ShrikeDialog
{
    private const string DefaultToolIconResource =
        "Shrike.Resources.DefaultToolIcon.png";

    private readonly ToolDefinition _tool;

    private readonly ToolManagementService _managementService;

    private readonly TextBox _nameTextBox;

    private readonly CategoryField _categoryField;

    private readonly TextArea _tooltipTextArea;

    private readonly IconTile _iconTile;

    private readonly SwatchPicker _swatchPicker;

    private readonly Label _scriptLabel;

    private readonly Label _scriptDetailLabel;

    private readonly string? _initialColorHex;

    public string NewName =>
        (_nameTextBox.Text
         ?? string.Empty)
        .Trim();

    public string NewCategory =>
        _categoryField.Category;

    public string NewTooltip =>
        (_tooltipTextArea.Text
         ?? string.Empty)
        .Trim();

    /// <summary>
    /// A .png picked to replace the icon, or null when unchanged.
    /// </summary>
    public string? NewIconPath
    {
        get;
        private set;
    }

    /// <summary>
    /// A .py picked to replace the script, or null when unchanged.
    /// </summary>
    public string? NewScriptPath
    {
        get;
        private set;
    }

    public string? NewColorHex =>
        _swatchPicker.SelectedHex;

    public bool ColorChanged =>
        !string.Equals(
            NewColorHex,
            _initialColorHex,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the script was rolled back from Version History.
    /// That change is applied right away, not on Save.
    /// </summary>
    public bool ScriptRolledBack
    {
        get;
        private set;
    }

    public ToolPropertiesDialog(
        ToolDefinition tool,
        IEnumerable<string> existingCategories,
        ToolManagementService managementService,
        string? currentIconPath,
        string? currentColorHex)
        : base(
            "Tool properties",
            managementService.CanModify(tool)
                ? "Changes are applied when you click Save."
                : "This tool comes with Shrike, so only its color " +
                  "can change. Duplicate it to edit a copy.")
    {
        _tool = tool;
        _managementService = managementService;
        _initialColorHex = currentColorHex;

        var canModify =
            managementService.CanModify(tool);

        _iconTile =
            new IconTile
            {
                Image =
                    LoadIcon(currentIconPath)
                    ?? EmbeddedImageLoader.LoadBitmap(
                        DefaultToolIconResource),

                Enabled = canModify
            };

        _iconTile.Click +=
            (_, _) =>
                BrowseIcon();

        _nameTextBox =
            Ui.TextBox(
                tool.Name);

        _nameTextBox.ReadOnly =
            !canModify;

        _categoryField =
            new CategoryField(
                existingCategories,
                tool.Category)
            {
                Enabled = canModify
            };

        _tooltipTextArea =
            Ui.TextArea(
                tool.Tooltip);

        _tooltipTextArea.ReadOnly =
            !canModify;

        _swatchPicker =
            new SwatchPicker(
                UserSettingsService.ColorPresets,
                currentColorHex);

        _scriptLabel =
            new Label
            {
                Font = Ui.StrongFont,
                TextColor = Ui.Ink
            };

        _scriptDetailLabel =
            new Label
            {
                TextColor = Ui.Muted
            };

        UpdateScriptLabels();

        var replaceButton =
            Ui.Button("Replace...");

        replaceButton.Enabled =
            canModify;

        replaceButton.Click +=
            (_, _) =>
                BrowseScript();

        var historyButton =
            Ui.Button("History...");

        historyButton.Enabled =
            canModify;

        historyButton.Click +=
            (_, _) =>
                ShowHistory();

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

        var scriptCard =
            new Card(
                new TableLayout
                {
                    Spacing =
                        new Size(8, 0),

                    Rows =
                    {
                        new TableRow(
                            new TableCell(
                                Ui.Stack(
                                    2,
                                    _scriptLabel,
                                    _scriptDetailLabel),
                                scaleWidth: true),

                            new TableCell(
                                replaceButton),

                            new TableCell(
                                historyButton))
                    }
                });

        SetBody(
            Ui.Stack(
                Ui.FieldGap,
                Ui.Field(
                    "Name",
                    nameRow),
                Ui.Field(
                    "Category",
                    _categoryField),
                Ui.Field(
                    "Tooltip",
                    new FieldFrame(
                        _tooltipTextArea,
                        multiline: true)),
                Ui.Field(
                    "Color",
                    _swatchPicker),
                Ui.Field(
                    "Script",
                    scriptCard)));

        AddCancelButton();

        AddButton(
            "Save",
            TryAccept,
            primary: true);

        InitialFocus =
            canModify
                ? _nameTextBox
                : _swatchPicker;
    }

    private static Image? LoadIcon(
        string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath)
            || !File.Exists(iconPath))
        {
            return null;
        }

        try
        {
            // Read the bytes first so the file is not kept locked.
            using var stream =
                new MemoryStream(
                    File.ReadAllBytes(
                        iconPath));

            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private void UpdateScriptLabels()
    {
        if (NewScriptPath is not null)
        {
            _scriptLabel.Text =
                Path.GetFileName(NewScriptPath);

            _scriptDetailLabel.Text =
                $"Replaces {_tool.Entry} when you click Save";

            return;
        }

        _scriptLabel.Text =
            _tool.Entry;

        var versionCount =
            _managementService.CanModify(_tool)
                ? _managementService.History
                    .GetEntries(_tool)
                    .Count
                : 0;

        _scriptDetailLabel.Text =
            versionCount switch
            {
                0 => "No earlier versions yet",
                1 => "1 earlier version saved",
                _ => $"{versionCount} earlier versions saved"
            };
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

        var image =
            LoadIcon(dialog.FileName);

        if (image is null)
        {
            ShowValidation(
                "That PNG could not be read.");

            return;
        }

        ClearValidation();

        NewIconPath =
            dialog.FileName;

        _iconTile.Image =
            image;
    }

    private void BrowseScript()
    {
        var dialog =
            new Eto.Forms.OpenFileDialog
            {
                Title =
                    $"Replace the script of {_tool.Name}",

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

        NewScriptPath =
            dialog.FileName;

        UpdateScriptLabels();
    }

    private void ShowHistory()
    {
        var dialog =
            new ToolHistoryDialog(
                _tool,
                _managementService.History,
                _managementService);

        if (dialog.ShowModal(this))
            ScriptRolledBack = true;

        UpdateScriptLabels();
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(
                NewName))
        {
            ShowValidation(
                "Give the tool a name.");

            _nameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(
                NewCategory))
        {
            ShowValidation(
                "Pick or type a category.");

            _categoryField.FocusInput();
            return;
        }

        Close(true);
    }
}
