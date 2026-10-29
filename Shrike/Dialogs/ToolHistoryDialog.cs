using System.Diagnostics;
using Eto.Drawing;
using Eto.Forms;
using Shrike.Controls;
using Shrike.Models;
using Shrike.Services;

namespace Shrike.Dialogs;

/// <summary>
/// Lists the saved versions of a tool's script, previews them,
/// and rolls the tool back to a selected version.
///
/// Result is true when the script was rolled back.
/// </summary>
public sealed class ToolHistoryDialog : ShrikeDialog
{
    private const int ListWidth = 280;

    private const int BodyHeight = 380;

    private readonly ToolDefinition _tool;

    private readonly ToolHistoryService _historyService;

    private readonly ToolManagementService _managementService;

    private readonly ListBox _versionList;

    private readonly TextArea _previewTextArea;

    private readonly TextBox _noteTextBox;

    private readonly RoundedButton _saveNoteButton;

    private readonly RoundedButton _rollbackButton;

    private IReadOnlyList<ToolHistoryEntry> _entries =
        Array.Empty<ToolHistoryEntry>();

    private bool _rolledBack;

    public ToolHistoryDialog(
        ToolDefinition tool,
        ToolHistoryService historyService,
        ToolManagementService managementService)
        : base(
            "Version history",
            $"Earlier scripts of \"{tool.Name}\". Shrike saves one " +
            "every time the script is replaced or rolled back.",
            bodyWidth: 780)
    {
        _tool = tool;
        _historyService = historyService;
        _managementService = managementService;

        _versionList =
            new ListBox
            {
                Width = ListWidth
            };

        _versionList.SelectedIndexChanged +=
            (_, _) =>
                ShowSelectedVersion();

        _previewTextArea =
            new TextArea
            {
                ReadOnly = true,
                Wrap = false,
                Border = BorderType.None,
                Font = Fonts.Monospace(9),
                BackgroundColor = Ui.Surface,
                TextColor = Ui.Ink
            };

        _noteTextBox =
            Ui.TextBox(
                placeholder:
                    "Add a note to this version, e.g. \"added fillet radius\"");

        _saveNoteButton =
            Ui.Button("Save note");

        _saveNoteButton.Click +=
            (_, _) =>
                SaveNote();

        var noteRow =
            new TableLayout
            {
                Spacing =
                    new Size(8, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            new FieldFrame(
                                _noteTextBox),
                            scaleWidth: true),

                        new TableCell(
                            _saveNoteButton))
                }
            };

        var previewColumn =
            new TableLayout
            {
                Spacing =
                    new Size(0, Ui.Gap),

                Rows =
                {
                    new TableRow(
                        Ui.FieldLabel(
                            "Code")),

                    new TableRow(
                        new TableCell(
                            new FieldFrame(
                                _previewTextArea,
                                multiline: true),
                            scaleWidth: true))
                    {
                        ScaleHeight = true
                    },

                    new TableRow(
                        new Panel
                        {
                            Height = Ui.Gap
                        }),

                    new TableRow(
                        noteRow)
                }
            };

        var listColumn =
            new TableLayout
            {
                Spacing =
                    new Size(0, Ui.Gap),

                Rows =
                {
                    new TableRow(
                        Ui.FieldLabel(
                            "Versions")),

                    new TableRow(
                        _versionList)
                    {
                        ScaleHeight = true
                    }
                }
            };

        SetBody(
            new TableLayout
            {
                Height = BodyHeight,

                Spacing =
                    new Size(Ui.FieldGap, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            listColumn),

                        new TableCell(
                            previewColumn,
                            scaleWidth: true))
                }
            });

        AddFooterAction(
            "Copy code",
            CopySelectedCode);

        AddFooterAction(
            "Open folder",
            OpenHistoryFolder);

        AddButton(
            "Close",
            Cancel);

        _rollbackButton =
            AddButton(
                "Roll back to this version",
                RollbackToSelected);

        _rollbackButton.CustomBackgroundColor =
            Ui.Ink;

        _rollbackButton.TextFont =
            Ui.StrongFont;

        InitialFocus =
            _versionList;

        LoadVersions();
    }

    protected override void Cancel()
    {
        Close(_rolledBack);
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
            ShowValidation(
                "The note could not be saved: " +
                ex.Message);

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
                "Roll back",
                MessageBoxButtons.YesNo,
                MessageBoxType.Question);

        if (confirmation != DialogResult.Yes)
            return;

        if (!_managementService.RollbackScript(
                _tool,
                entry,
                out var error))
        {
            ShowValidation(
                error);

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
            ShowValidation(
                "The history folder could not be opened: " +
                ex.Message);
        }
    }
}
