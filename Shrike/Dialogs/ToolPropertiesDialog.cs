using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;
using Shrike.Services;

namespace Shrike.Dialogs;

/// <summary>
/// One place to customize a tool: name, category, tooltip, icon,
/// color, script, and version history.
///
/// Field changes are only applied when the user clicks Save,
/// which makes the result true. A rollback from Version History
/// is applied right away and is reported by ScriptRolledBack.
/// </summary>
public sealed class ToolPropertiesDialog : Dialog<bool>
{
    private const int IconPreviewSize = 32;

    private readonly ToolDefinition _tool;

    private readonly ToolManagementService _managementService;

    private readonly TextBox _nameTextBox;

    private readonly ComboBox _categoryComboBox;

    private readonly TextArea _tooltipTextArea;

    private readonly ImageView _iconPreview;

    private readonly Label _iconLabel;

    private readonly DropDown _colorDropDown;

    private readonly Label _scriptLabel;

    private readonly string? _initialColorHex;

    public string NewName =>
        (_nameTextBox.Text
         ?? string.Empty)
        .Trim();

    public string NewCategory =>
        DialogLayout.ReadCategory(
            _categoryComboBox);

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
        _colorDropDown.SelectedIndex <= 0
            ? null
            : UserSettingsService.ColorPresets[
                _colorDropDown.SelectedIndex - 1].Hex;

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
    {
        _tool = tool;
        _managementService = managementService;
        _initialColorHex = currentColorHex;

        var canModify =
            managementService.CanModify(tool);

        Title = $"Tool Properties - {tool.Name}";
        Width = 560;
        Resizable = false;

        _nameTextBox =
            new TextBox
            {
                Text = tool.Name,
                Enabled = canModify
            };

        _categoryComboBox =
            DialogLayout.CategoryComboBox(
                existingCategories,
                tool.Category);

        _categoryComboBox.Enabled =
            canModify;

        _tooltipTextArea =
            new TextArea
            {
                Text = tool.Tooltip,
                Height = 60,
                Enabled = canModify
            };

        _iconPreview =
            new ImageView
            {
                Size =
                    new Size(
                        IconPreviewSize,
                        IconPreviewSize)
            };

        _iconLabel =
            new Label
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        ShowIcon(
            currentIconPath,
            string.IsNullOrWhiteSpace(currentIconPath)
                ? "Default icon"
                : Path.GetFileName(currentIconPath));

        var changeIconButton =
            new Button
            {
                Text = "Change...",
                Width = DialogLayout.ActionButtonWidth,
                Enabled = canModify
            };

        changeIconButton.Click +=
            (_, _) =>
                BrowseIcon();

        _colorDropDown =
            CreateColorDropDown(
                currentColorHex);

        _scriptLabel =
            new Label
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        UpdateScriptLabel();

        var replaceScriptButton =
            new Button
            {
                Text = "Replace...",
                Width = DialogLayout.ActionButtonWidth,
                Enabled = canModify
            };

        replaceScriptButton.Click +=
            (_, _) =>
                BrowseScript();

        var historyButton =
            new Button
            {
                Text = "History...",
                Width = DialogLayout.ActionButtonWidth,
                Enabled = canModify
            };

        historyButton.Click +=
            (_, _) =>
                ShowHistory();

        var saveButton =
            new Button
            {
                Text = "Save"
            };

        saveButton.Click +=
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
            saveButton;

        AbortButton =
            cancelButton;

        var iconRow =
            new TableLayout
            {
                Spacing =
                    new Size(8, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _iconPreview),

                        new TableCell(
                            _iconLabel,
                            scaleWidth: true),

                        new TableCell(
                            changeIconButton))
                }
            };

        var scriptRow =
            new TableLayout
            {
                Spacing =
                    new Size(8, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _scriptLabel,
                            scaleWidth: true),

                        new TableCell(
                            replaceScriptButton),

                        new TableCell(
                            historyButton))
                }
            };

        var form =
            new TableLayout
            {
                Padding = 14,

                Spacing =
                    new Size(12, 10)
            };

        if (!canModify)
        {
            form.Rows.Add(
                new TableRow(
                    null,

                    new TableCell(
                        DialogLayout.HintLabel(
                            "Bundled tool: only its color can be changed. " +
                            "Duplicate it to customize a copy."))));
        }

        form.Rows.Add(
            DialogLayout.FormRow(
                "Name",
                _nameTextBox));

        form.Rows.Add(
            DialogLayout.FormRow(
                "Category",
                _categoryComboBox));

        form.Rows.Add(
            DialogLayout.FormRow(
                "Tooltip",
                _tooltipTextArea));

        form.Rows.Add(
            DialogLayout.FormRow(
                "Icon",
                iconRow));

        form.Rows.Add(
            DialogLayout.FormRow(
                "Color",
                _colorDropDown));

        form.Rows.Add(
            DialogLayout.FormRow(
                "Script",
                scriptRow));

        form.Rows.Add(
            new TableRow(
                null,

                new TableCell(
                    DialogLayout.ButtonRow(
                        saveButton,
                        cancelButton))));

        Content =
            form;
    }

    private static DropDown CreateColorDropDown(
        string? currentColorHex)
    {
        var dropDown =
            new DropDown();

        dropDown.Items.Add(
            "Default");

        var selectedIndex = 0;

        for (var i = 0;
             i < UserSettingsService.ColorPresets.Count;
             i++)
        {
            var preset =
                UserSettingsService.ColorPresets[i];

            dropDown.Items.Add(
                preset.Name);

            if (string.Equals(
                    preset.Hex,
                    currentColorHex,
                    StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = i + 1;
            }
        }

        dropDown.SelectedIndex =
            selectedIndex;

        return dropDown;
    }

    private void ShowIcon(
        string? iconPath,
        string caption)
    {
        _iconLabel.Text =
            caption;

        if (string.IsNullOrWhiteSpace(iconPath)
            || !File.Exists(iconPath))
        {
            _iconPreview.Image = null;
            return;
        }

        try
        {
            // Read the bytes first so the file is not kept locked.
            using var stream =
                new MemoryStream(
                    File.ReadAllBytes(
                        iconPath));

            _iconPreview.Image =
                new Bitmap(stream);
        }
        catch
        {
            _iconPreview.Image = null;
        }
    }

    private void UpdateScriptLabel()
    {
        if (NewScriptPath is not null)
        {
            _scriptLabel.Text =
                $"Replace with {Path.GetFileName(NewScriptPath)} on Save";

            return;
        }

        var versionCount =
            _managementService.CanModify(_tool)
                ? _managementService.History
                    .GetEntries(_tool)
                    .Count
                : 0;

        _scriptLabel.Text =
            versionCount switch
            {
                0 => _tool.Entry,
                1 => $"{_tool.Entry} · 1 saved version",
                _ => $"{_tool.Entry} · {versionCount} saved versions"
            };
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

        if (dialog.ShowDialog(this) ==
            DialogResult.Cancel)
        {
            return;
        }

        NewIconPath =
            dialog.FileName;

        ShowIcon(
            NewIconPath,
            Path.GetFileName(NewIconPath));
    }

    private void BrowseScript()
    {
        var dialog =
            new Eto.Forms.OpenFileDialog
            {
                Title =
                    $"Replace Script - {_tool.Name}",

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

        UpdateScriptLabel();
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

        UpdateScriptLabel();
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(
                NewName))
        {
            MessageBox.Show(
                this,
                "Tool name cannot be empty.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Warning);

            return;
        }

        if (string.IsNullOrWhiteSpace(
                NewCategory))
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
