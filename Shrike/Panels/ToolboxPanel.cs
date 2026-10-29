using System.Diagnostics;
using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Shrike.Controls;
using Shrike.Dialogs;
using Shrike.Models;
using Shrike.Services;

namespace Shrike.Panels;

[Guid("89C2BD00-F9C5-4D03-8229-A24E6A54026B")]
public sealed class ToolboxPanel : Panel
{
    private const int UiGap = 5;

    private const int ToolButtonHeight = 30;
    private const float ToolCornerRadius = 9f;
    private const int ToolIconDisplaySize = 18;

    private const int HeaderIconDisplaySize = 18;

    private const string NewToolIconResource =
        "Shrike.Resources.NewToolIcon.png";

    private const string PasteToolIconResource =
        "Shrike.Resources.PasteToolIcon.png";

    private const string ImportToolIconResource =
        "Shrike.Resources.ImportToolIcon.png";

    private const string PanelOptionsIconResource =
        "Shrike.Resources.PanelOptionsIcon.png";

    private const string RefreshIconResource =
        "Shrike.Resources.RefreshIcon.png";

    private const string FavoritesSectionKey =
        "__Shrike_favorites__";

    private readonly ToolRepository _repository;
    private readonly ToolExecutionService _executionService;
    private readonly ToolCreationService _creationService;
    private readonly ToolManagementService _managementService;
    private readonly ToolPackageService _packageService;
    private readonly UserSettingsService _settingsService;
    private readonly ToolWatchService _watchService;
    private readonly MacroRecorderService _macroRecorderService;

    private readonly TextBox _searchBox;
    private readonly StackLayout _toolList;
    private readonly Panel _recordingBar;

    private CreateToolRequest?
        _macroRecordingDraft;

    private readonly Dictionary<string, bool>
        _categoryExpandedStates =
            new(
                StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<ToolDefinition>
        _tools =
            Array.Empty<ToolDefinition>();

    // Tool buttons currently on screen, used to find which tool
    // a .py file was dropped on.
    private readonly List<(RoundedButton Button, ToolDefinition Tool)>
        _toolButtons =
            new();

    public ToolboxPanel()
    {
        _repository =
            new ToolRepository();

        _executionService =
            new ToolExecutionService();

        _creationService =
            new ToolCreationService();

        _managementService =
            new ToolManagementService();

        _packageService =
            new ToolPackageService();

        _settingsService =
            new UserSettingsService();

        _watchService =
            new ToolWatchService();

        _watchService.ToolsChanged +=
            OnWatchedToolsChanged;

        _macroRecorderService =
            new MacroRecorderService();

        _searchBox =
            new TextBox
            {
                PlaceholderText =
                    "Search tools..."
            };

        _searchBox.TextChanged +=
            (_, _) =>
                RebuildToolList();

        var searchPreferredHeight =
            (int)Math.Ceiling(
                _searchBox
                    .GetPreferredSize()
                    .Height);

        var headerControlHeight =
            searchPreferredHeight > 0
                ? searchPreferredHeight
                : 30;

        var managementButton =
            CreateHeaderIconButton(
                PanelOptionsIconResource,
                "Panel Options",
                headerControlHeight);

        managementButton.Click +=
            (_, _) =>
                ShowPanelManagementMenu(
                    managementButton);

        var createButton =
            CreateHeaderIconButton(
                NewToolIconResource,
                "New Tool",
                headerControlHeight);

        createButton.Click +=
            (_, _) =>
                CreateNewTool();

        var pasteButton =
            CreateHeaderIconButton(
                PasteToolIconResource,
                "New Tool from Clipboard",
                headerControlHeight);

        pasteButton.Click +=
            (_, _) =>
                CreateToolFromClipboard();

        var importButton =
            CreateHeaderIconButton(
                ImportToolIconResource,
                "Import Tool",
                headerControlHeight);

        importButton.Click +=
            (_, _) =>
                ImportTool();

        var refreshButton =
            CreateHeaderIconButton(
                RefreshIconResource,
                "Refresh",
                headerControlHeight);

        refreshButton.Click +=
            (_, _) =>
                ReloadTools();

        var header =
            new StackLayout
            {
                Orientation =
                    Orientation.Horizontal,

                Spacing = 8,

                VerticalContentAlignment =
                    VerticalAlignment.Center,

                Items =
                {
                    new StackLayoutItem(
                        _searchBox,
                        expand: true),

                    managementButton,
                    createButton,
                    pasteButton,
                    importButton,
                    refreshButton
                }
            };

        var stopRecordingButton =
            new Button
            {
                Text = "Stop",
                Width = 62
            };

        stopRecordingButton.Click +=
            (_, _) =>
                StopMacroRecording();

        _recordingBar =
            new Panel
            {
                Visible = false,

                Padding =
                    new Padding(
                        left: 8,
                        top: 4,
                        right: 4,
                        bottom: 4),

                Content =
                    new StackLayout
                    {
                        Orientation =
                            Orientation.Horizontal,

                        Spacing = 8,

                        VerticalContentAlignment =
                            VerticalAlignment.Center,

                        Items =
                        {
                            new StackLayoutItem(
                                new Label
                                {
                                    Text =
                                        "● Recording Macro..."
                                },
                                expand: true),

                            stopRecordingButton
                        }
                    }
            };

        _toolList =
            new StackLayout
            {
                Orientation =
                    Orientation.Vertical,

                Spacing = UiGap,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch
            };

        var toolListHost =
            new Panel
            {
                Padding =
                    new Padding(UiGap),

                Content =
                    _toolList
            };

        var scrollable =
            new Scrollable
            {
                ExpandContentWidth = true,
                Content = toolListHost,
                AllowDrop = true
            };

        // Drop a .py file on empty space to create a tool,
        // or on a tool to replace its script.
        scrollable.DragEnter +=
            (_, e) =>
                e.Effects =
                    GetDropEffect(e);

        scrollable.DragOver +=
            (_, e) =>
                e.Effects =
                    GetDropEffect(e);

        scrollable.DragDrop +=
            (_, e) =>
                OnPythonFileDropped(
                    scrollable,
                    e);

        Content =
            new StackLayout
            {
                Padding =
                    new Padding(
                        left: 12,
                        top: 10,
                        right: 12,
                        bottom: 10),

                Spacing = UiGap,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch,

                Items =
                {
                    header,

                    _recordingBar,

                    new StackLayoutItem(
                        scrollable,
                        expand: true)
                }
            };

        ReloadTools();

        _watchService.Start();
    }

    private RoundedButton CreateHeaderIconButton(
        string resourceName,
        string tooltip,
        int height)
    {
        var button =
            new RoundedButton
            {
                Width = height,
                Height = height,

                CornerRadius =
                    Math.Max(
                        6f,
                        height * 0.22f),

                BorderWidth = 1f,

                IconSize =
                    Math.Min(
                        HeaderIconDisplaySize,
                        Math.Max(
                            14,
                            height - 10)),

                CenterContent = true,

                ToolTip = tooltip
            };

        button.Image =
            EmbeddedImageLoader.LoadBitmap(
                resourceName);

        return button;
    }

    private void ShowPanelManagementMenu(
        Control anchor)
    {
        var isSearching =
            !string.IsNullOrWhiteSpace(
                _searchBox.Text);

        var menu =
            new ContextMenu();

        var expandAllItem =
            new ButtonMenuItem
            {
                Text = "Expand All",
                Enabled = !isSearching
            };

        expandAllItem.Click +=
            (_, _) =>
                SetAllCategoriesExpanded(
                    true);

        menu.Items.Add(
            expandAllItem);

        var collapseAllItem =
            new ButtonMenuItem
            {
                Text = "Collapse All",
                Enabled = !isSearching
            };

        collapseAllItem.Click +=
            (_, _) =>
                SetAllCategoriesExpanded(
                    false);

        menu.Items.Add(
            collapseAllItem);

        menu.Items.Add(
            new SeparatorMenuItem());

        var openUserToolsFolderItem =
            new ButtonMenuItem
            {
                Text = "Open User Tools Folder"
            };

        openUserToolsFolderItem.Click +=
            (_, _) =>
                OpenUserToolsFolder();

        menu.Items.Add(
            openUserToolsFolderItem);

        menu.Show(
            anchor,
            new PointF(
                0,
                anchor.Height));
    }

    private void SetAllCategoriesExpanded(
        bool expanded)
    {
        if (!string.IsNullOrWhiteSpace(
                _searchBox.Text))
        {
            return;
        }

        foreach (var category in
                 _tools
                     .Select(
                         tool =>
                             tool.Category)
                     .Where(
                         category =>
                             !string.IsNullOrWhiteSpace(
                                 category))
                     .Distinct(
                         StringComparer.OrdinalIgnoreCase))
        {
            _categoryExpandedStates[
                category] =
                expanded;
        }

        _categoryExpandedStates[
            FavoritesSectionKey] =
            expanded;

        RebuildToolList();
    }

    private void OpenUserToolsFolder()
    {
        try
        {
            var folder =
                _repository.UserToolsRoot;

            Directory.CreateDirectory(
                folder);

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            ShowError(
                "Shrike could not open the User Tools folder.\n\n" +
                ex.Message);
        }
    }

    private void OnWatchedToolsChanged(
        object? sender,
        EventArgs e)
    {
        var application =
            Application.Instance;

        if (application is null)
            return;

        application.AsyncInvoke(
            () => ReloadTools());
    }

    private void ReloadTools()
    {
        _tools =
            _repository.LoadTools();

        RebuildToolList();
    }

    private void CreateNewTool(
        CreateToolRequest? initialRequest = null,
        ClipboardCodeResult? initialCode = null)
    {
        if (_macroRecorderService.IsRecording)
        {
            ShowError(
                "Shrike is currently recording a Macro.\n\n" +
                "Stop the recording before creating another tool.");

            return;
        }

        var dialog =
            new CreateToolDialog(
                initialRequest,
                initialCode);

        var accepted =
            ShowDialog(dialog);

        if (dialog.RecordMacroRequested)
        {
            StartMacroRecording(
                dialog.DraftRequest);

            return;
        }

        if (!accepted
            ||
            dialog.Request is null)
        {
            return;
        }

        var result =
            _creationService.CreateTool(
                dialog.Request);

        if (!result.Success)
        {
            ShowError(
                result.Message);

            return;
        }

        ReloadTools();

        MessageBox.Show(
            result.Message +
            "\n\nCreated in:\n" +
            result.ToolDirectory,
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
    }

    private void CreateToolFromClipboard()
    {
        var code =
            ReadClipboardCode();

        if (code is null)
            return;

        CreateNewTool(
            null,
            code);
    }

    private ClipboardCodeResult? ReadClipboardCode()
    {
        ClipboardCodeResult code;

        try
        {
            code =
                ClipboardSource.ReadCode();
        }
        catch (Exception ex)
        {
            ShowError(
                "Shrike could not read the clipboard.\n\n" +
                ex.Message);

            return null;
        }

        if (!code.HasCode)
        {
            MessageBox.Show(
                "The clipboard is empty.\n\n" +
                "Copy Python code from your AI chat, " +
                "or copy a .py file in File Explorer, then try again.",
                "Shrike",
                MessageBoxButtons.OK,
                MessageBoxType.Information);

            return null;
        }

        return code;
    }

    private static IReadOnlyList<string> GetDroppedPythonFiles(
        DataObject? data)
    {
        if (data is null
            || !data.ContainsUris
            || data.Uris is null)
        {
            return Array.Empty<string>();
        }

        return data.Uris
            .Where(
                uri =>
                    uri.IsFile)
            .Select(
                uri =>
                    uri.LocalPath)
            .Where(
                ClipboardSource.IsPythonFile)
            .ToList();
    }

    private static DragEffects GetDropEffect(
        DragEventArgs e)
    {
        return GetDroppedPythonFiles(e.Data).Count > 0
            ? DragEffects.Copy
            : DragEffects.None;
    }

    private void OnPythonFileDropped(
        Control dropTarget,
        DragEventArgs e)
    {
        var files =
            GetDroppedPythonFiles(
                e.Data);

        if (files.Count == 0)
            return;

        var screenPoint =
            dropTarget.PointToScreen(
                e.Location);

        var targetTool =
            _toolButtons
                .Where(
                    item =>
                        item.Button.Visible
                        &&
                        new RectangleF(
                                item.Button.Size)
                            .Contains(
                                item.Button.PointFromScreen(
                                    screenPoint)))
                .Select(
                    item =>
                        item.Tool)
                .FirstOrDefault();

        var path =
            files[0];

        var skipped =
            files.Count - 1;

        // Leave the drag-and-drop operation before showing dialogs,
        // so File Explorer is not blocked while a dialog is open.
        Application.Instance?.AsyncInvoke(
            () =>
            {
                if (targetTool is null)
                {
                    CreateNewTool(
                        null,
                        ClipboardSource.ReadFile(
                            path));
                }
                else
                {
                    ReplaceToolScriptWithFile(
                        targetTool,
                        path);
                }

                if (skipped > 0)
                {
                    RhinoApp.WriteLine(
                        $"Shrike: only the first dropped file was used; " +
                        $"{skipped} other file(s) were ignored.");
                }
            });
    }

    private void StartMacroRecording(
        CreateToolRequest draft)
    {
        if (_macroRecorderService.IsRecording)
            return;

        draft.Type =
            "macro";

        _macroRecordingDraft =
            draft;

        _macroRecorderService.Start();

        _recordingBar.Visible =
            true;

        RhinoApp.WriteLine(
            "Shrike Macro Recorder is recording. " +
            "Run Rhino commands, then click Stop in the Shrike panel.");

        RhinoApp.SetFocusToMainWindow();
    }

    private void StopMacroRecording()
    {
        if (!_macroRecorderService.IsRecording)
            return;

        var recordedMacro =
            _macroRecorderService.Stop();

        _recordingBar.Visible =
            false;

        var draft =
            _macroRecordingDraft
            ??
            new CreateToolRequest
            {
                Type = "macro",
                Category = "Utility"
            };

        _macroRecordingDraft =
            null;

        if (!string.IsNullOrWhiteSpace(
                recordedMacro))
        {
            draft.Macro =
                AppendRecordedMacro(
                    draft.Macro,
                    recordedMacro);
        }

        if (string.IsNullOrWhiteSpace(
                recordedMacro))
        {
            RhinoApp.WriteLine(
                "Shrike Macro Recorder: no completed commands were recorded.");
        }

        // Reopen Create New Tool with all previous values preserved
        // and the recorded commands filled into the Macro field.
        CreateNewTool(
            draft);
    }

    private static string AppendRecordedMacro(
        string existingMacro,
        string recordedMacro)
    {
        var existing =
            (existingMacro
             ?? string.Empty)
            .Trim();

        var recorded =
            (recordedMacro
             ?? string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(
                existing))
        {
            return recorded;
        }

        if (string.IsNullOrWhiteSpace(
                recorded))
        {
            return existing;
        }

        return
            existing +
            Environment.NewLine +
            recorded;
    }

    private void ImportTool()
    {
        var fileDialog =
            new Eto.Forms.OpenFileDialog
            {
                Title = "Import Shrike Tool",
                CheckFileExists = true,
                MultiSelect = false,

                Filters =
                {
                    new FileFilter(
                        "Shrike Tool Package",
                        "*.shriketool")
                }
            };

        var fileResult =
            fileDialog.ShowDialog(this);

        if (fileResult ==
            Eto.Forms.DialogResult.Cancel)
        {
            return;
        }

        if (!_packageService.TryReadPackageInfo(
                fileDialog.FileName,
                out var packageInfo,
                out var previewError))
        {
            ShowError(previewError);
            return;
        }

        var importDialog =
            new ImportToolDialog(
                packageInfo!);

        var accepted =
            ShowDialog(importDialog);

        if (!accepted)
            return;

        var result =
            _packageService.ImportTool(
                fileDialog.FileName,
                importDialog.ToolName,
                importDialog.Category);

        if (!result.Success)
        {
            ShowError(
                result.Message);

            return;
        }

        ReloadTools();

        MessageBox.Show(
            result.Message +
            "\n\nImported to:\n" +
            result.ToolDirectory,
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
    }

    private void ExportTool(
        ToolDefinition tool)
    {
        var defaultName =
            ToolPackageService.MakeSuggestedPackageFileName(
                tool.Name);

        var dialog =
            new Eto.Forms.SaveFileDialog
            {
                Title =
                    $"Export Tool - {tool.Name}",

                FileName =
                    defaultName,

                Filters =
                {
                    new FileFilter(
                        "Shrike Tool Package",
                        "*.shriketool")
                }
            };

        var result =
            dialog.ShowDialog(this);

        if (result ==
            Eto.Forms.DialogResult.Cancel)
        {
            return;
        }

        var destination =
            dialog.FileName;

        if (!destination.EndsWith(
                ".shriketool",
                StringComparison.OrdinalIgnoreCase))
        {
            destination +=
                ".shriketool";
        }

        if (!_packageService.ExportTool(
                tool,
                destination,
                out var error))
        {
            ShowError(error);
            return;
        }

        MessageBox.Show(
            $"Exported \"{tool.Name}\" to:\n\n{destination}",
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
    }

    private void EditToolScript(
        ToolDefinition tool)
    {
        if (!IsPythonTool(tool))
            return;

        if (!_managementService.EditScript(
                tool,
                out var error))
        {
            ShowError(error);
        }
    }

    private void EditToolMacro(
        ToolDefinition tool)
    {
        if (!IsMacroTool(tool))
            return;

        if (!_managementService.CanModify(tool))
        {
            ShowReadOnlyMessage();
            return;
        }

        var dialog =
            new EditMacroDialog(
                tool.Name,
                tool.Command,
                TestRunMacro);

        var accepted =
            ShowDialog(dialog);

        if (!accepted)
            return;

        if (!_managementService.UpdateMacro(
                tool,
                dialog.Macro,
                out var error))
        {
            ShowError(error);
            return;
        }

        ReloadTools();
    }

    private void TestRunMacro(
        string macro)
    {
        if (string.IsNullOrWhiteSpace(macro))
            return;

        try
        {
            var success =
                RhinoApp.RunScript(
                    macro,
                    echo: false);

            if (!success)
            {
                ShowError(
                    "Rhino could not complete the Macro test run.");
            }
        }
        catch (Exception ex)
        {
            ShowError(
                "Shrike could not test the Macro.\n\n" +
                ex.Message);
        }
    }

    private void ReplaceToolScript(
        ToolDefinition tool)
    {
        if (!IsPythonTool(tool))
            return;

        if (!_managementService.CanModify(tool))
        {
            ShowReadOnlyMessage();
            return;
        }

        var dialog =
            new Eto.Forms.OpenFileDialog
            {
                Title =
                    $"Replace Script - {tool.Name}",

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
            Eto.Forms.DialogResult.Cancel)
        {
            return;
        }

        ReplaceToolScriptWithFile(
            tool,
            dialog.FileName);
    }

    private void ReplaceToolScriptWithFile(
        ToolDefinition tool,
        string path)
    {
        if (!IsPythonTool(tool))
            return;

        if (!_managementService.CanModify(tool))
        {
            ShowReadOnlyMessage();
            return;
        }

        if (!ConfirmReplaceScript(
                tool,
                Path.GetFileName(path),
                Array.Empty<string>()))
        {
            return;
        }

        if (!_managementService.ReplaceScript(
                tool,
                path,
                out var error))
        {
            ShowError(error);
            return;
        }

        MessageBox.Show(
            $"Replaced script for \"{tool.Name}\".",
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
    }

    private void ReplaceToolScriptFromClipboard(
        ToolDefinition tool)
    {
        if (!IsPythonTool(tool))
            return;

        if (!_managementService.CanModify(tool))
        {
            ShowReadOnlyMessage();
            return;
        }

        var code =
            ReadClipboardCode();

        if (code is null)
            return;

        if (!ConfirmReplaceScript(
                tool,
                code.Summary,
                code.Warnings))
        {
            return;
        }

        if (!_managementService.ReplaceScriptContent(
                tool,
                code.Code,
                ToolHistoryReason.BeforeReplaceFromClipboard,
                out var error))
        {
            ShowError(error);
            return;
        }

        MessageBox.Show(
            $"Replaced script for \"{tool.Name}\".",
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
    }

    private bool ConfirmReplaceScript(
        ToolDefinition tool,
        string sourceDescription,
        IReadOnlyCollection<string> warnings)
    {
        var message =
            $"Replace the script of \"{tool.Name}\" with:\n" +
            $"{sourceDescription}\n\n" +
            "The current script is saved to Version History, " +
            "so you can roll back.";

        if (warnings.Count > 0)
        {
            message +=
                "\n\n" +
                string.Join(
                    "\n",
                    warnings);
        }

        return MessageBox.Show(
                   message,
                   "Replace Script",
                   MessageBoxButtons.YesNo,
                   MessageBoxType.Question)
               == DialogResult.Yes;
    }

    private void ShowVersionHistory(
        ToolDefinition tool)
    {
        if (!IsPythonTool(tool))
            return;

        var dialog =
            new ToolHistoryDialog(
                tool,
                _managementService.History,
                _managementService);

        ShowDialog(dialog);
    }

    private void DuplicateTool(
        ToolDefinition tool)
    {
        var dialog =
            new DuplicateToolDialog(
                tool);

        var accepted =
            ShowDialog(dialog);

        if (!accepted)
            return;

        var result =
            _managementService.DuplicateTool(
                tool,
                dialog.NewName,
                dialog.Category);

        if (!result.Success)
        {
            ShowError(
                result.Message);

            return;
        }

        ReloadTools();

        MessageBox.Show(
            result.Message +
            "\n\nCreated in:\n" +
            result.ToolDirectory,
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
    }


    private void MoveToolToCategory(
        ToolDefinition tool,
        string category)
    {
        if (!_managementService.CanModify(
                tool))
        {
            ShowReadOnlyMessage();
            return;
        }

        if (string.Equals(
                tool.Category,
                category,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!_managementService.MoveToolToCategory(
                tool,
                category,
                out var error))
        {
            ShowError(error);
            return;
        }

        ReloadTools();
    }

    private void MoveToolToNewCategory(
        ToolDefinition tool)
    {
        if (!_managementService.CanModify(
                tool))
        {
            ShowReadOnlyMessage();
            return;
        }

        var dialog =
            new CategoryNameDialog(
                "New Category",
                string.Empty,
                "Move");

        var accepted =
            ShowDialog(dialog);

        if (!accepted)
            return;

        MoveToolToCategory(
            tool,
            dialog.CategoryName);
    }

    private void RenameCategory(
        string categoryName)
    {
        var categoryTools =
            _tools
                .Where(
                    tool =>
                        string.Equals(
                            tool.Category,
                            categoryName,
                            StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (!_managementService.CanRenameCategory(
                categoryTools))
        {
            ShowCategoryReadOnlyMessage();
            return;
        }

        var dialog =
            new CategoryNameDialog(
                "Rename Category",
                categoryName,
                "Rename");

        var accepted =
            ShowDialog(dialog);

        if (!accepted)
            return;

        var newCategory =
            dialog.CategoryName;

        if (string.Equals(
                categoryName,
                newCategory,
                StringComparison.Ordinal))
        {
            return;
        }

        var targetCategoryAlreadyExisted =
            _tools.Any(
                tool =>
                    !string.Equals(
                        tool.Category,
                        categoryName,
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    string.Equals(
                        tool.Category,
                        newCategory,
                        StringComparison.OrdinalIgnoreCase));

        if (!_managementService.RenameCategory(
                categoryTools,
                newCategory,
                out var error))
        {
            ShowError(error);
            return;
        }

        _settingsService.RenameCategoryColor(
            categoryName,
            newCategory,
            targetCategoryAlreadyExisted);

        if (_categoryExpandedStates.TryGetValue(
                categoryName,
                out var expanded))
        {
            _categoryExpandedStates.Remove(
                categoryName);

            _categoryExpandedStates[
                newCategory] =
                expanded;
        }

        ReloadTools();
    }

    private ButtonMenuItem CreateMoveToCategoryMenu(
        ToolDefinition tool)
    {
        var canModify =
            _managementService.CanModify(
                tool);

        var moveMenu =
            new ButtonMenuItem
            {
                Text = "Move to Category",
                Enabled = canModify
            };

        if (!canModify)
            return moveMenu;

        var existingCategories =
            _tools
                .Select(
                    item =>
                        item.Category)
                .Where(
                    category =>
                        !string.IsNullOrWhiteSpace(
                            category))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Where(
                    category =>
                        !string.Equals(
                            category,
                            tool.Category,
                            StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    category =>
                        category)
                .ToList();

        foreach (var category in
                 existingCategories)
        {
            var targetCategory =
                category;

            var categoryItem =
                new ButtonMenuItem
                {
                    Text =
                        targetCategory
                };

            categoryItem.Click +=
                (_, _) =>
                    MoveToolToCategory(
                        tool,
                        targetCategory);

            moveMenu.Items.Add(
                categoryItem);
        }

        if (existingCategories.Count > 0)
        {
            moveMenu.Items.Add(
                new SeparatorMenuItem());
        }

        var newCategoryItem =
            new ButtonMenuItem
            {
                Text = "New Category..."
            };

        newCategoryItem.Click +=
            (_, _) =>
                MoveToolToNewCategory(
                    tool);

        moveMenu.Items.Add(
            newCategoryItem);

        return moveMenu;
    }

    private ContextMenu CreateCategoryContextMenu(
        string displayName,
        string categoryKey)
    {
        var menu =
            new ContextMenu();

        var isFavorites =
            string.Equals(
                categoryKey,
                FavoritesSectionKey,
                StringComparison.Ordinal);

        if (!isFavorites)
        {
            var categoryTools =
                _tools
                    .Where(
                        tool =>
                            string.Equals(
                                tool.Category,
                                displayName,
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var renameItem =
                new ButtonMenuItem
                {
                    Text = "Rename Category",

                    Enabled =
                        _managementService.CanRenameCategory(
                            categoryTools)
                };

            renameItem.Click +=
                (_, _) =>
                    RenameCategory(
                        displayName);

            menu.Items.Add(
                renameItem);

            menu.Items.Add(
                new SeparatorMenuItem());
        }

        menu.Items.Add(
            CreateCategoryColorMenu(
                categoryKey));

        return menu;
    }

    private void ToggleFavorite(
        ToolDefinition tool)
    {
        _settingsService.ToggleFavorite(
            tool.Id);

        RebuildToolList();
    }

    private void RenameTool(
        ToolDefinition tool)
    {
        if (!_managementService.CanModify(tool))
        {
            ShowReadOnlyMessage();
            return;
        }

        var dialog =
            new RenameToolDialog(
                tool.Name);

        var accepted =
            ShowDialog(dialog);

        if (!accepted)
            return;

        if (!_managementService.RenameTool(
                tool,
                dialog.NewName,
                out var error))
        {
            ShowError(error);
            return;
        }

        ReloadTools();
    }

    private void ChangeToolIcon(
        ToolDefinition tool)
    {
        if (!_managementService.CanModify(tool))
        {
            ShowReadOnlyMessage();
            return;
        }

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
            Eto.Forms.DialogResult.Cancel)
        {
            return;
        }

        if (!_managementService.ChangeIcon(
                tool,
                dialog.FileName,
                out var error))
        {
            ShowError(error);
            return;
        }

        ReloadTools();
    }

    private void OpenToolFolder(
        ToolDefinition tool)
    {
        if (!_managementService.OpenToolFolder(
                tool,
                out var error))
        {
            ShowError(error);
        }
    }

    private void DeleteTool(
        ToolDefinition tool)
    {
        if (!_managementService.CanModify(tool))
        {
            ShowReadOnlyMessage();
            return;
        }

        var confirmation =
            MessageBox.Show(
                $"Delete \"{tool.Name}\"?\n\n" +
                "This permanently deletes the tool folder, " +
                "including its Python script and icon.",
                "Delete Tool",
                MessageBoxButtons.YesNo,
                MessageBoxType.Warning);

        if (confirmation !=
            DialogResult.Yes)
        {
            return;
        }

        if (!_managementService.DeleteTool(
                tool,
                out var error))
        {
            ShowError(error);
            return;
        }

        _settingsService.SetFavorite(
            tool.Id,
            false);

        _settingsService.SetToolColor(
            tool.Id,
            null);

        ReloadTools();
    }

    private ContextMenu CreateToolContextMenu(
        ToolDefinition tool)
    {
        var menu =
            new ContextMenu();

        var runItem =
            new ButtonMenuItem
            {
                Text = "Run"
            };

        runItem.Click +=
            (_, _) =>
                _executionService.Run(
                    tool);

        menu.Items.Add(
            runItem);

        if (IsPythonTool(tool))
        {
            var editItem =
                new ButtonMenuItem
                {
                    Text = "Edit Script"
                };

            editItem.Click +=
                (_, _) =>
                    EditToolScript(tool);

            menu.Items.Add(editItem);

            var replaceScriptItem =
                new ButtonMenuItem
                {
                    Text = "Replace Script",
                    Enabled =
                        _managementService.CanModify(tool)
                };

            replaceScriptItem.Click +=
                (_, _) =>
                    ReplaceToolScript(tool);

            menu.Items.Add(replaceScriptItem);

            var replaceFromClipboardItem =
                new ButtonMenuItem
                {
                    Text = "Replace Script from Clipboard",
                    Enabled =
                        _managementService.CanModify(tool)
                };

            replaceFromClipboardItem.Click +=
                (_, _) =>
                    ReplaceToolScriptFromClipboard(tool);

            menu.Items.Add(replaceFromClipboardItem);

            var historyItem =
                new ButtonMenuItem
                {
                    Text = "Version History...",
                    Enabled =
                        _managementService.CanModify(tool)
                };

            historyItem.Click +=
                (_, _) =>
                    ShowVersionHistory(tool);

            menu.Items.Add(historyItem);
        }

        if (IsMacroTool(tool))
        {
            var editMacroItem =
                new ButtonMenuItem
                {
                    Text = "Edit Macro",
                    Enabled =
                        _managementService.CanModify(tool)
                };

            editMacroItem.Click +=
                (_, _) =>
                    EditToolMacro(tool);

            menu.Items.Add(editMacroItem);
        }

        var openFolderItem =
            new ButtonMenuItem
            {
                Text = "Open Folder"
            };

        openFolderItem.Click +=
            (_, _) =>
                OpenToolFolder(
                    tool);

        menu.Items.Add(
            openFolderItem);

        menu.Items.Add(
            new SeparatorMenuItem());

        var isFavorite =
            _settingsService.IsFavorite(
                tool.Id);

        var favoriteItem =
            new ButtonMenuItem
            {
                Text =
                    isFavorite
                        ? "Unpin from Favorites"
                        : "Pin to Favorites"
            };

        favoriteItem.Click +=
            (_, _) =>
                ToggleFavorite(
                    tool);

        menu.Items.Add(
            favoriteItem);

        var duplicateItem =
            new ButtonMenuItem
            {
                Text = "Duplicate"
            };

        duplicateItem.Click +=
            (_, _) =>
                DuplicateTool(
                    tool);

        menu.Items.Add(
            duplicateItem);

        var exportItem =
            new ButtonMenuItem
            {
                Text = "Export Tool..."
            };

        exportItem.Click +=
            (_, _) =>
                ExportTool(
                    tool);

        menu.Items.Add(
            exportItem);

        var moveToCategoryItem =
            CreateMoveToCategoryMenu(
                tool);

        menu.Items.Add(
            moveToCategoryItem);

        menu.Items.Add(
            new SeparatorMenuItem());

        var canModify =
            _managementService.CanModify(
                tool);

        var renameItem =
            new ButtonMenuItem
            {
                Text = "Rename",
                Enabled = canModify
            };

        renameItem.Click +=
            (_, _) =>
                RenameTool(
                    tool);

        menu.Items.Add(
            renameItem);

        var changeIconItem =
            new ButtonMenuItem
            {
                Text = "Change Icon",
                Enabled = canModify
            };

        changeIconItem.Click +=
            (_, _) =>
                ChangeToolIcon(
                    tool);

        menu.Items.Add(
            changeIconItem);

        menu.Items.Add(
            CreateToolColorMenu(
                tool));

        var deleteItem =
            new ButtonMenuItem
            {
                Text = "Delete",
                Enabled = canModify
            };

        deleteItem.Click +=
            (_, _) =>
                DeleteTool(
                    tool);

        menu.Items.Add(
            deleteItem);

        return menu;
    }

    private ButtonMenuItem CreateToolColorMenu(
        ToolDefinition tool)
    {
        var item =
            new ButtonMenuItem
            {
                Text = "Change Color"
            };

        PopulateColorMenu(
            item,
            _settingsService.GetToolColor(
                tool.Id),
            colorHex =>
            {
                _settingsService.SetToolColor(
                    tool.Id,
                    colorHex);

                RebuildToolList();
            });

        return item;
    }

    private ButtonMenuItem CreateCategoryColorMenu(
        string categoryKey)
    {
        var item =
            new ButtonMenuItem
            {
                Text = "Change Category Color"
            };

        PopulateColorMenu(
            item,
            _settingsService.GetCategoryColor(
                categoryKey),
            colorHex =>
            {
                _settingsService.SetCategoryColor(
                    categoryKey,
                    colorHex);

                RebuildToolList();
            });

        return item;
    }

    private void PopulateColorMenu(
        ButtonMenuItem parent,
        string? currentColorHex,
        Action<string?> applyColor)
    {
        var defaultItem =
            new ButtonMenuItem
            {
                Text = "Default"
            };

        defaultItem.Click +=
            (_, _) =>
                applyColor(null);

        parent.Items.Add(
            defaultItem);

        parent.Items.Add(
            new SeparatorMenuItem());

        foreach (var preset in
                 GetColorPresets())
        {
            var colorHex =
                preset.Hex;

            var presetItem =
                new ButtonMenuItem
                {
                    Text = preset.Name
                };

            presetItem.Click +=
                (_, _) =>
                    applyColor(
                        colorHex);

            parent.Items.Add(
                presetItem);
        }

    }

    private static IReadOnlyList<(string Name, string Hex)>
        GetColorPresets()
    {
        // The panel is black and white, so tool and category
        // highlights are shades of gray only.
        return new (string Name, string Hex)[]
        {
            ("Light Gray", "#F0F0F0"),
            ("Gray", "#D9D9D9"),
            ("Mid Gray", "#A6A6A6"),
            ("Dark Gray", "#595959"),
            ("Black", "#1F1F1F")
        };
    }

    private static Color ToGray(
        Color color)
    {
        var luminance =
            0.299f * color.R
            + 0.587f * color.G
            + 0.114f * color.B;

        return new Color(
            luminance,
            luminance,
            luminance,
            color.A);
    }

    private static bool TryParseUiColor(
        string? colorHex,
        out Color color)
    {
        if (!string.IsNullOrWhiteSpace(
                colorHex)
            &&
            Color.TryParse(
                colorHex,
                out color))
        {
            // Colors saved by older versions are shown as gray.
            color =
                ToGray(color);

            return true;
        }

        color =
            Colors.Transparent;

        return false;
    }


    private void RebuildToolList()
    {
        _toolList.Items.Clear();

        _toolButtons.Clear();

        var query =
            (_searchBox.Text
             ?? string.Empty)
            .Trim();

        var isSearching =
            !string.IsNullOrWhiteSpace(
                query);

        var filtered =
            _tools
                .Where(
                    tool =>
                        Matches(
                            tool,
                            query))
                .ToList();

        if (filtered.Count == 0)
        {
            ShowEmptyState(
                query);

            return;
        }

        AddFavoritesSection(
            filtered,
            isSearching);

        foreach (var category in
                 filtered
                     .GroupBy(
                         tool =>
                             tool.Category)
                     .OrderBy(
                         group =>
                             group.Key))
        {
            AddCategorySection(
                category.Key,
                category,
                category.Key,
                forceExpanded: isSearching);
        }
    }

    private void AddFavoritesSection(
        IReadOnlyList<ToolDefinition> filteredTools,
        bool forceExpanded)
    {
        var favorites =
            filteredTools
                .Where(
                    tool =>
                        _settingsService.IsFavorite(
                            tool.Id))
                .OrderBy(
                    tool =>
                        tool.Name)
                .ToList();

        if (favorites.Count == 0)
            return;

        AddCategorySection(
            "★ Favorites",
            favorites,
            FavoritesSectionKey,
            forceExpanded);
    }

    private void AddCategorySection(
        string displayName,
        IEnumerable<ToolDefinition> tools,
        string stateKey,
        bool forceExpanded = false)
    {
        var content =
            CreateCategoryContent(
                tools);

        var savedExpanded =
            !_categoryExpandedStates
                .TryGetValue(
                    stateKey,
                    out var savedState)
            ||
            savedState;

        // Search is temporary UI state:
        // categories containing matches are always expanded,
        // but their saved collapsed/expanded state is not changed.
        var expanded =
            forceExpanded
                ? true
                : savedExpanded;

        var section =
            new CollapsibleCategoryPanel(
                displayName,
                content,
                expanded);

        if (TryParseUiColor(
                _settingsService.GetCategoryColor(
                    stateKey),
                out var categoryColor))
        {
            section.HeaderBackgroundColor =
                categoryColor;
        }

        section.ExpandedChanged +=
            (_, _) =>
            {
                if (forceExpanded)
                    return;

                _categoryExpandedStates[
                    stateKey] =
                    section.Expanded;
            };

        section.HeaderContextMenu =
            CreateCategoryContextMenu(
                displayName,
                stateKey);

        _toolList.Items.Add(
            section);
    }

    private Control CreateCategoryContent(
        IEnumerable<ToolDefinition> tools)
    {
        var stack =
            new StackLayout
            {
                Orientation =
                    Orientation.Vertical,

                Spacing = 7,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch
            };

        foreach (var tool in
                 tools.OrderBy(
                     item =>
                         item.Name))
        {
            stack.Items.Add(
                CreateToolButton(
                    tool));
        }

        return stack;
    }

    private static bool IsPythonTool(
        ToolDefinition tool)
    {
        return string.Equals(
                   tool.Type,
                   "python3",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   tool.Type,
                   "python",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMacroTool(
        ToolDefinition tool)
    {
        return string.Equals(
                   tool.Type,
                   "macro",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   tool.Type,
                   "rhino-macro",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(
        ToolDefinition tool,
        string query)
    {
        if (string.IsNullOrWhiteSpace(
                query))
        {
            return true;
        }

        return Contains(
                   tool.Name,
                   query)
               ||
               Contains(
                   tool.Category,
                   query)
               ||
               Contains(
                   tool.Tooltip,
                   query);
    }

    private static bool Contains(
        string? source,
        string query)
    {
        return !string.IsNullOrEmpty(
                   source)
               &&
               source.Contains(
                   query,
                   StringComparison
                       .OrdinalIgnoreCase);
    }

    private RoundedButton CreateToolButton(
        ToolDefinition tool)
    {
        var button =
            new RoundedButton
            {
                Text =
                    tool.Name,

                ToolTip =
                    tool.Tooltip,

                Height =
                    ToolButtonHeight,

                CornerRadius =
                    ToolCornerRadius,

                BorderWidth = 1f,

                IconSize =
                    ToolIconDisplaySize,

                HorizontalPadding = 13,

                IconTextSpacing = 10,

                CenterContent = false
            };

        if (TryParseUiColor(
                _settingsService.GetToolColor(
                    tool.Id),
                out var toolColor))
        {
            button.CustomBackgroundColor =
                toolColor;
        }

        var iconPath =
            GetIconPath(
                tool);

        if (iconPath is not null)
        {
            try
            {
                button.Image =
                    LoadBitmapFresh(
                        iconPath);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine(
                    $"Shrike: could not load icon " +
                    $"'{iconPath}'.");

                RhinoApp.WriteLine(
                    ex.Message);
            }
        }

        button.Click +=
            (_, _) =>
                _executionService.Run(
                    tool);

        button.ContextMenu =
            CreateToolContextMenu(
                tool);

        _toolButtons.Add(
            (button, tool));

        return button;
    }

    private void ShowEmptyState(
        string query)
    {
        string message;

        if (_tools.Count == 0)
        {
            message =
                "No tools were found.\n\n" +
                "Use the New Tool button to import a " +
                "Rhino 8 Python script.";
        }
        else
        {
            message =
                $"No tools match \"{query}\".";
        }

        _toolList.Items.Add(
            new Label
            {
                Text = message,
                Wrap = WrapMode.Word
            });
    }

    private static Bitmap LoadBitmapFresh(
        string path)
    {
        var bytes =
            File.ReadAllBytes(
                path);

        using var stream =
            new MemoryStream(
                bytes);

        return new Bitmap(
            stream);
    }

    private static string? GetIconPath(
        ToolDefinition tool)
    {
        if (string.IsNullOrWhiteSpace(
                tool.Icon))
        {
            return null;
        }

        var path =
            Path.Combine(
                tool.ToolDirectory,
                tool.Icon);

        return File.Exists(
                   path)
            ? path
            : null;
    }

    private static bool ShowDialog(
        Dialog<bool> dialog)
    {
        var doc =
            RhinoDoc.ActiveDoc;

        if (doc is null)
            return dialog.ShowModal();

        var parent =
            Rhino.UI.RhinoEtoApp
                .MainWindowForDocument(
                    doc);

        return dialog.ShowModal(
            parent);
    }

    private static void ShowReadOnlyMessage()
    {
        MessageBox.Show(
            "This is a bundled Shrike tool.\n\n" +
            "Bundled tools are read-only. " +
            "Only tools created in Documents\\Shrike\\Tools " +
            "can be renamed, moved, re-iconed, replaced, or deleted.",
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
    }

    private static void ShowCategoryReadOnlyMessage()
    {
        MessageBox.Show(
            "This category contains one or more bundled Shrike tools.\n\n" +
            "A category can only be renamed when all of its tools are " +
            "user-created tools in Documents\\Shrike\\Tools.",
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
    }

    private static void ShowError(
        string error)
    {
        MessageBox.Show(
            error,
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Error);
    }
}
