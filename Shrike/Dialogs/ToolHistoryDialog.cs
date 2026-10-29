using System.Diagnostics;
using Eto.Drawing;
using Eto.Forms;
using Shrike.Models;
using Shrike.Services;

namespace Shrike.Dialogs;

/// <summary>
/// Lists the saved versions of a tool's script, previews them,
/// and rolls the tool back to a selected version.
///
/// Result is true when the script was rolled back.
/// </summary>
public sealed class ToolHistoryDialog : Dialog<bool>
{
    private readonly ToolDefinition _tool;

    private readonly ToolHistoryService _historyService;

    private readonly ToolManagementService _managementService;

    private readonly ListBox _versionList;

    private readonly TextArea _previewTextArea;

    private readonly TextBox _noteTextBox;

    private readonly Button _saveNoteButton;

    private readonly Button _rollbackButton;

    private IReadOnlyList<ToolHistoryEntry> _entries =
        Array.Empty<ToolHistoryEntry>();

    private bool _rolledBack;

    public ToolHistoryDialog(
        ToolDefinition tool,
        ToolHistoryService historyService,
        ToolManagementService managementService)
    {
        _tool = tool;
        _historyService = historyService;
        _managementService = managementService;

        Title = $"Version History - {tool.Name}";
        Width = 820;
        Height = 520;
        Resizable = true;

        _versionList =
            new ListBox
            {
                Width = 300
            };

        _versionList.SelectedIndexChanged +=
            (_, _) =>
                ShowSelectedVersion();

        _previewTextArea =
            new TextArea
            {
                ReadOnly = true,
                Wrap = false,
                Font = Fonts.Monospace(9)
            };

        _noteTextBox =
            new TextBox
            {
                PlaceholderText =
                    "Optional note, e.g. \"added fillet radius\""
            };

        _saveNoteButton =
            new Button
            {
                Text = "Save Note"
            };

        _saveNoteButton.Click +=
            (_, _) =>
                SaveNote();

        _rollbackButton =
            new Button
            {
                Text = "Roll Back to This Version"
            };

        _rollbackButton.Click +=
            (_, _) =>
                RollbackToSelected();

        var copyButton =
            new Button
            {
                Text = "Copy Code"
            };

        copyButton.Click +=
            (_, _) =>
                CopySelectedCode();

        var openFolderButton =
            new Button
            {
                Text = "Open History Folder"
            };

        openFolderButton.Click +=
            (_, _) =>
                OpenHistoryFolder();

        var closeButton =
            new Button
            {
                Text = "Close"
            };

        closeButton.Click +=
            (_, _) =>
                Close(_rolledBack);

        AbortButton =
            closeButton;

        var noteRow =
            new TableLayout
            {
                Spacing =
                    new Size(8, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _noteTextBox,
                            scaleWidth: true),

                        new TableCell(
                            _saveNoteButton))
                }
            };

        var previewColumn =
            new TableLayout
            {
                Spacing =
                    new Size(0, 8),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _previewTextArea,
                            scaleWidth: true))
                    {
                        ScaleHeight = true
                    },

                    new TableRow(
                        noteRow)
                }
            };

        var buttonRow =
            new StackLayout
            {
                Orientation =
                    Orientation.Horizontal,

                Spacing = 8,

                Items =
                {
                    _rollbackButton,
                    copyButton,
                    openFolderButton,

                    new StackLayoutItem(
                        null,
                        expand: true),

                    closeButton
                }
            };

        Content =
            new TableLayout
            {
                Padding = 14,

                Spacing =
                    new Size(12, 10),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            _versionList),

                        new TableCell(
                            previewColumn,
                            scaleWidth: true))
                    {
                        ScaleHeight = true
                    },

                    new TableRow(
                        null,

                        new TableCell(
                            buttonRow))
                }
            };

        LoadVersions();
    }

    private bool IsCurrentSelected =>
        _versionList.SelectedIndex <= 0;

    private ToolHistoryEntry? SelectedEntry =>
        IsCurrentSelected
            ? null
            : _entries[_versionList.SelectedIndex - 1];

    private void LoadVersions(
        int selectedIndex = 0)
    {
        _entries =
            _historyService.GetEntries(_tool);

        _versionList.Items.Clear();

        _versionList.Items.Add(
            new ListItem
            {
                Text = "Current version"
            });

        foreach (var entry in _entries)
        {
            _versionList.Items.Add(
                new ListItem
                {
                    Text = FormatEntry(entry)
                });
        }

        _versionList.SelectedIndex =
            Math.Clamp(
                selectedIndex,
                0,
                _entries.Count);

        ShowSelectedVersion();
    }

    private static string FormatEntry(
        ToolHistoryEntry entry)
    {
        var text =
            $"{entry.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}  " +
            $"{entry.ReasonText}  ({entry.LineCount} lines)";

        if (!string.IsNullOrWhiteSpace(entry.Note))
        {
            text +=
                $"  - {entry.Note}";
        }

        return text;
    }

    private void ShowSelectedVersion()
    {
        var entry =
            SelectedEntry;

        _rollbackButton.Enabled =
            entry is not null
            && _managementService.CanModify(_tool);

        _noteTextBox.Enabled =
            entry is not null;

        _saveNoteButton.Enabled =
            entry is not null;

        _noteTextBox.Text =
            entry?.Note ?? string.Empty;

        _previewTextArea.Text =
            ReadSelectedCode()
            ?? string.Empty;
    }

    private string? ReadSelectedCode()
    {
        try
        {
            var entry =
                SelectedEntry;

            return entry is null
                ? File.ReadAllText(
                    ToolHistoryService.GetEntryScriptPath(
                        _tool))
                : _historyService.ReadEntry(
                    _tool,
                    entry);
        }
        catch (Exception ex)
        {
            return
                "Shrike could not read this version.\n\n" +
                ex.Message;
        }
    }

    private void SaveNote()
    {
        var entry =
            SelectedEntry;

        if (entry is null)
            return;

        var selectedIndex =
            _versionList.SelectedIndex;

        try
        {
            _historyService.SetNote(
                _tool,
                entry,
                _noteTextBox.Text ?? string.Empty);
        }
        catch (Exception ex)
        {
            ShowMessage(
                "Shrike could not save the note.\n\n" +
                ex.Message,
                MessageBoxType.Error);

            return;
        }

        LoadVersions(
            selectedIndex);
    }

    private void RollbackToSelected()
    {
        var entry =
            SelectedEntry;

        if (entry is null)
            return;

        var confirmation =
            MessageBox.Show(
                this,
                $"Roll \"{_tool.Name}\" back to the version from " +
                $"{entry.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}?\n\n" +
                "The current script is saved to the history first, " +
                "so this can be undone.",
                "Roll Back",
                MessageBoxButtons.YesNo,
                MessageBoxType.Question);

        if (confirmation != DialogResult.Yes)
            return;

        if (!_managementService.RollbackScript(
                _tool,
                entry,
                out var error))
        {
            ShowMessage(
                error,
                MessageBoxType.Error);

            return;
        }

        _rolledBack = true;

        LoadVersions();
    }

    private void CopySelectedCode()
    {
        var code =
            ReadSelectedCode();

        if (string.IsNullOrEmpty(code))
            return;

        using var clipboard =
            new Clipboard();

        clipboard.Text =
            code;
    }

    private void OpenHistoryFolder()
    {
        try
        {
            var folder =
                ToolHistoryService.GetHistoryDirectory(
                    _tool.ToolDirectory);

            Directory.CreateDirectory(folder);

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            ShowMessage(
                "Shrike could not open the history folder.\n\n" +
                ex.Message,
                MessageBoxType.Error);
        }
    }

    private void ShowMessage(
        string message,
        MessageBoxType type)
    {
        MessageBox.Show(
            this,
            message,
            "Shrike",
            MessageBoxButtons.OK,
            type);
    }
}
