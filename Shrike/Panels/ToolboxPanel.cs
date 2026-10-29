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

    private const string ImportToolIconResource =
        "Shrike.Resources.ImportToolIcon.png";

    private const string PanelOptionsIconResource =
        "Shrike.Resources.PanelOptionsIcon.png";

    private const string RefreshIconResource =
        "Shrike.Resources.RefreshIcon.png";

    private const string FavoritesSectionKey =
        "__Shrike_favorites__";

    private static readonly TimeSpan StatusDisplayTime =
        TimeSpan.FromSeconds(6);

    private readonly ToolRepository _repository;
    private readonly ToolExecutionService _executionService;
    private readonly ToolCreationService _creationService;
    private readonly ToolManagementService _managementService;
    private readonly ToolPackageService _packageService;
    private readonly UserSettingsService _settingsService;
    private readonly ToolWatchService _watchService;

    private readonly TextBox _searchBox;
    private readonly StackLayout _toolList;
    private readonly Label _statusLabel;
    private readonly UITimer _statusTimer;

    private readonly Dictionary<string, bool>
        _categoryExpandedStates =
            new(
                StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<ToolDefinition>
        _tools =
            Array.Empty<ToolDefinition>();

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
                    importButton,
                    refreshButton
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
                Content = toolListHost
            };

        // Results of quick actions are shown here instead of
        // in a message box, so the panel never blocks for an "OK".
        _statusLabel =
            new Label
            {
                Wrap = WrapMode.Word,
                TextColor =
                    SystemColors.DisabledText,
                Visible = false
            };

        _statusTimer =
            new UITimer
            {
                Interval =
                    StatusDisplayTime.TotalSeconds
            };

        _statusTimer.Elapsed +=
            (_, _) =>
                ClearStatus();

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

                    new StackLayoutItem(
                        scrollable,
                        expand: true),

                    _statusLabel
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

    private void ShowStatus(
        string message)
    {
        _statusTimer.Stop();

        _statusLabel.Text =
            message;

        _statusLabel.Visible = true;

        _statusTimer.Start();
    }

    private void ClearStatus()
    {
        _statusTimer.Stop();

        _statusLabel.Text =
            string.Empty;

        _statusLabel.Visible = false;
    }

    private IEnumerable<string> GetCategories()
    {
        return _tools
            .Select(
                tool =>
                    tool.Category)
            .Where(
                category =>
                    !string.IsNullOrWhiteSpace(
                        category))
            .Distinct(
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Makes sure a tool that was just added or moved is visible.
    /// </summary>
    private void RevealCategory(
        string category)
    {
        if (!string.IsNullOrWhiteSpace(
                category))
        {
            _categoryExpandedStates[
                category] =
                true;
        }
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

    private void CreateNewTool()
    {
        var dialog =
            new CreateToolDialog(
                GetCategories());

        var accepted =
            ShowDialog(dialog);

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

        RevealCategory(
            dialog.Request.Category);

        ReloadTools();

        ShowStatus(
            $"Created \"{dialog.Request.Name}\" in {dialog.Request.Category}.");
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
                packageInfo!,
                GetCategories());

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

        RevealCategory(
            importDialog.Category);

        ReloadTools();

        ShowStatus(
            $"Imported \"{importDialog.ToolName}\" into {importDialog.Category}.");
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

        ShowStatus(
            $"Exported \"{tool.Name}\" to {destination}");
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

        // No confirmation: the current script is saved to
        // Version History first, so a replace can be rolled back.
        if (!_managementService.ReplaceScript(
                tool,
                dialog.FileName,
                out var error))
        {
            ShowError(error);
            return;
        }

        ShowStatus(
            $"Replaced the script of \"{tool.Name}\". " +
            "The previous version is in Version History.");
    }

    private void ShowToolProperties(
        ToolDefinition tool)
    {
        var dialog =
            new ToolPropertiesDialog(
                tool,
                GetCategories(),
                _managementService,
                GetIconPath(
                    tool),
                _settingsService.GetToolColor(
                    tool.Id));

        var saved =
            ShowDialog(dialog);

        if (!saved)
        {
            if (dialog.ScriptRolledBack)
            {
                ShowStatus(
                    $"Rolled back the script of \"{tool.Name}\".");
            }

            return;
        }

        if (dialog.ColorChanged)
        {
            _settingsService.SetToolColor(
                tool.Id,
                dialog.NewColorHex);
        }

        if (!_managementService.CanModify(
                tool))
        {
            RebuildToolList();
            return;
        }

        if (!ApplyToolProperties(
                tool,
                dialog,
                out var error))
        {
            ShowError(error);
            ReloadTools();
            return;
        }

        RevealCategory(
            tool.Category);

        ReloadTools();

        ShowStatus(
            $"Saved \"{tool.Name}\".");
    }

    private bool ApplyToolProperties(
        ToolDefinition tool,
        ToolPropertiesDialog dialog,
        out string error)
    {
        error = string.Empty;

        if (dialog.NewName != tool.Name
            && !_managementService.RenameTool(
                tool,
                dialog.NewName,
                out error))
        {
            return false;
        }

        if (dialog.NewTooltip != (tool.Tooltip ?? string.Empty).Trim()
            && !_managementService.ChangeTooltip(
                tool,
                dialog.NewTooltip,
                out error))
        {
            return false;
        }

        if (dialog.NewIconPath is not null
            && !_managementService.ChangeIcon(
                tool,
                dialog.NewIconPath,
                out error))
        {
            return false;
        }

        if (dialog.NewScriptPath is not null
            && !_managementService.ReplaceScript(
                tool,
                dialog.NewScriptPath,
                out error))
        {
            return false;
        }

        // Moving changes the tool folder, so it goes last.
        if (!string.Equals(
                dialog.NewCategory,
                tool.Category,
                StringComparison.Ordinal)
            && !_managementService.MoveToolToCategory(
                tool,
                dialog.NewCategory,
                out error))
        {
            return false;
        }

        return true;
    }

    private void DuplicateTool(
        ToolDefinition tool)
    {
        var dialog =
            new DuplicateToolDialog(
                tool,
                GetCategories());

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

        RevealCategory(
            dialog.Category);

        ReloadTools();

        ShowStatus(
            $"Created \"{dialog.NewName}\" in {dialog.Category}.");
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
                "Rename category",
                "Every tool in this category moves to the new name.",
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

        var canModify =
            _managementService.CanModify(
                tool);

        AddMenuItem(
            menu,
            "Run",
            () =>
                _executionService.Run(
                    tool));

        if (IsPythonTool(tool))
        {
            AddMenuItem(
                menu,
                "Edit in Script Editor",
                () =>
                    EditToolScript(
                        tool));

            AddMenuItem(
                menu,
                "Replace Script...",
                () =>
                    ReplaceToolScript(
                        tool),
                canModify);
        }

        AddMenuItem(
            menu,
            "Properties...",
            () =>
                ShowToolProperties(
                    tool));

        menu.Items.Add(
            new SeparatorMenuItem());

        AddMenuItem(
            menu,
            _settingsService.IsFavorite(
                tool.Id)
                ? "Unpin from Favorites"
                : "Pin to Favorites",
            () =>
                ToggleFavorite(
                    tool));

        AddMenuItem(
            menu,
            "Duplicate...",
            () =>
                DuplicateTool(
                    tool));

        AddMenuItem(
            menu,
            "Export Tool...",
            () =>
                ExportTool(
                    tool));

        AddMenuItem(
            menu,
            "Open Folder",
            () =>
                OpenToolFolder(
                    tool));

        menu.Items.Add(
            new SeparatorMenuItem());

        AddMenuItem(
            menu,
            "Delete",
            () =>
                DeleteTool(
                    tool),
            canModify);

        return menu;
    }

    private static void AddMenuItem(
        ContextMenu menu,
        string text,
        Action onClick,
        bool enabled = true)
    {
        var item =
            new ButtonMenuItem
            {
                Text = text,
                Enabled = enabled
            };

        item.Click +=
            (_, _) =>
                onClick();

        menu.Items.Add(
            item);
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
                 UserSettingsService.ColorPresets)
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

    private void ShowReadOnlyMessage()
    {
        ShowStatus(
            "Bundled tools are read-only. " +
            "Duplicate the tool to customize a copy.");
    }

    private void ShowCategoryReadOnlyMessage()
    {
        ShowStatus(
            "This category contains bundled tools, " +
            "so it cannot be renamed.");
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
