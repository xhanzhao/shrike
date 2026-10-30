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

    private const int HeaderIconDisplaySize = 18;

    private const int BrandIconSize = 32;

    private const string BrandIconResource =
        "Shrike.Resources.Shrike.ico";

    private const string BrandDescription =
        "Turns tested, AI-written Python scripts into Rhino commands.";

    private const string NewToolIconResource =
        "Shrike.Resources.NewToolIcon.png";

    private const string PanelOptionsIconResource =
        "Shrike.Resources.PanelOptionsIcon.png";

    private const string DefaultToolIconResource =
        "Shrike.Resources.DefaultToolIcon.png";

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
            Ui.TextBox(
                placeholder:
                    "Search tools");

        _searchBox.TextChanged +=
            (_, _) =>
                RebuildToolList();

        var addButton =
            CreateHeaderIconButton(
                NewToolIconResource,
                "New or import a tool");

        addButton.Click +=
            (_, _) =>
                ShowAddMenu(
                    addButton);

        var managementButton =
            CreateHeaderIconButton(
                PanelOptionsIconResource,
                "Panel options");

        managementButton.Click +=
            (_, _) =>
                ShowPanelManagementMenu(
                    managementButton);

        var toolbar =
            new TableLayout
            {
                Spacing =
                    new Size(Ui.Gap, 0),

                Rows =
                {
                    new TableRow(
                        new TableCell(
                            new FieldFrame(
                                _searchBox),
                            scaleWidth: true),

                        new TableCell(
                            addButton),

                        new TableCell(
                            managementButton))
                }
            };

        _toolList =
            new StackLayout
            {
                Orientation =
                    Orientation.Vertical,

                Spacing = 8,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch
            };

        // No frame around the list: the categories and rows
        // sit directly on the panel.
        var scrollable =
            new Scrollable
            {
                Border = BorderType.None,
                ExpandContentWidth = true,
                BackgroundColor = Ui.Surface,
                Content = _toolList
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

        BackgroundColor =
            Ui.Surface;

        Content =
            new StackLayout
            {
                Padding =
                    new Padding(
                        left: 10,
                        top: 10,
                        right: 10,
                        bottom: 10),

                Spacing = 10,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch,

                Items =
                {
                    CreateBrandHeader(),

                    toolbar,

                    new StackLayoutItem(
                        scrollable,
                        expand: true),

                    _statusLabel
                }
            };

        ReloadTools();

        _watchService.Start();
    }

    /// <summary>
    /// Shrike's name, mark and a one-line summary of what the
    /// panel is for, shown above the search bar.
    /// </summary>
    private static Control CreateBrandHeader()
    {
        var icon =
            new ImageView
            {
                Image =
                    EmbeddedImageLoader.LoadTintedBitmap(
                        BrandIconResource,
                        Ui.Ink),

                Size =
                    new Size(
                        BrandIconSize,
                        BrandIconSize)
            };

        var text =
            new StackLayout
            {
                Orientation =
                    Orientation.Vertical,

                Spacing = 2,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch,

                Items =
                {
                    Ui.Title(
                        "Shrike"),

                    new Label
                    {
                        Text = BrandDescription,
                        Wrap = WrapMode.Word,
                        Font = Ui.SmallFont,
                        TextColor = Ui.Muted
                    }
                }
            };

        return new TableLayout
        {
            Spacing =
                new Size(10, 0),

            Rows =
            {
                new TableRow(
                    new TableCell(
                        icon),

                    new TableCell(
                        text,
                        scaleWidth: true))
            }
        };
    }

    private static RoundedButton CreateHeaderIconButton(
        string resourceName,
        string tooltip)
    {
        var button =
            new RoundedButton
            {
                Width = Ui.FieldHeight,
                Height = Ui.FieldHeight,
                CornerRadius = Ui.FieldRadius,
                BorderWidth = 1f,
                IconSize = HeaderIconDisplaySize,
                CenterContent = true,
                CustomBackgroundColor = Ui.Surface,
                ToolTip = tooltip
            };

        button.Image =
            EmbeddedImageLoader.LoadBitmap(
                resourceName);

        return button;
    }

    private void ShowAddMenu(
        Control anchor)
    {
        var menu =
            new MenuPopup();

        menu.Add(
            "New Tool...",
            CreateNewTool);

        menu.Add(
            "Import Tool...",
            ImportTool);

        menu.ShowBelow(
            anchor);
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
        // Expanding and collapsing do nothing while searching:
        // every category with a match is shown open.
        var isSearching =
            !string.IsNullOrWhiteSpace(
                _searchBox.Text);

        var menu =
            new MenuPopup();

        menu.Add(
            "Expand All",
            () =>
                SetAllCategoriesExpanded(
                    true),
            enabled: !isSearching);

        menu.Add(
            "Collapse All",
            () =>
                SetAllCategoriesExpanded(
                    false),
            enabled: !isSearching);

        menu.AddSeparator();

        menu.Add(
            "Refresh",
            ReloadTools);

        menu.Add(
            "Open User Tools Folder",
            OpenUserToolsFolder);

        menu.ShowBelow(
            anchor);
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

        _watchService.IgnorePendingChanges();

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

    private MenuPopup BuildCategoryMenu(
        string displayName,
        string categoryKey)
    {
        var menu =
            new MenuPopup();

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

            menu.Add(
                "Rename Category...",
                () =>
                    RenameCategory(
                        displayName),
                _managementService.CanRenameCategory(
                    categoryTools));

            menu.AddSeparator();
        }

        // The header colors, flat in the menu instead of a submenu.
        // The current one is checked and each shows its gray.
        var currentColor =
            _settingsService.GetCategoryColor(
                categoryKey);

        void ApplyColor(
            string? colorHex)
        {
            _settingsService.SetCategoryColor(
                categoryKey,
                colorHex);

            RebuildToolList();
        }

        menu.AddHeading(
            "Header color");

        menu.Add(
            "No color",
            () =>
                ApplyColor(null),
            isChecked:
                string.IsNullOrWhiteSpace(
                    currentColor));

        foreach (var preset in
                 UserSettingsService.ColorPresets)
        {
            var colorHex =
                preset.Hex;

            menu.Add(
                preset.Name,
                () =>
                    ApplyColor(
                        colorHex),
                isChecked:
                    string.Equals(
                        colorHex,
                        currentColor,
                        StringComparison.OrdinalIgnoreCase),
                swatch:
                    Color.Parse(
                        colorHex));
        }

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

    private MenuPopup BuildToolMenu(
        ToolDefinition tool)
    {
        var menu =
            new MenuPopup();

        var canModify =
            _managementService.CanModify(
                tool);

        menu.Add(
            "Run",
            () =>
                _executionService.Run(
                    tool));

        if (IsPythonTool(tool))
        {
            menu.Add(
                "Edit in Script Editor",
                () =>
                    EditToolScript(
                        tool));

            menu.Add(
                "Replace Script...",
                () =>
                    ReplaceToolScript(
                        tool),
                canModify);
        }

        menu.Add(
            "Properties...",
            () =>
                ShowToolProperties(
                    tool));

        menu.AddSeparator();

        menu.Add(
            _settingsService.IsFavorite(
                tool.Id)
                ? "Unpin from Favorites"
                : "Pin to Favorites",
            () =>
                ToggleFavorite(
                    tool));

        menu.Add(
            "Duplicate...",
            () =>
                DuplicateTool(
                    tool));

        menu.Add(
            "Export Tool...",
            () =>
                ExportTool(
                    tool));

        menu.Add(
            "Open Folder",
            () =>
                OpenToolFolder(
                    tool));

        menu.AddSeparator();

        menu.Add(
            "Delete",
            () =>
                DeleteTool(
                    tool),
            canModify);

        return menu;
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
            "Favorites",
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
        var toolList =
            tools.ToList();

        var content =
            CreateCategoryContent(
                toolList);

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
                expanded,
                toolList.Count);

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

        section.HeaderMenuRequested +=
            (sender, location) =>
            {
                if (sender is Control header)
                {
                    BuildCategoryMenu(
                        displayName,
                        stateKey)
                        .ShowAt(
                            header,
                            location);
                }
            };

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

                Spacing = 0,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch
            };

        foreach (var tool in
                 tools.OrderBy(
                     item =>
                         ToolDisplayName.For(item.Name),
                     StringComparer.OrdinalIgnoreCase))
        {
            stack.Items.Add(
                CreateToolRow(
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
                   ToolDisplayName.For(tool.Name),
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

    private ToolRow CreateToolRow(
        ToolDefinition tool)
    {
        var row =
            new ToolRow
            {
                Text =
                    ToolDisplayName.For(
                        tool.Name),

                ToolTip =
                    string.IsNullOrWhiteSpace(tool.Tooltip)
                        ? tool.Name
                        : tool.Tooltip
            };

        if (TryParseUiColor(
                _settingsService.GetToolColor(
                    tool.Id),
                out var toolColor))
        {
            row.FillColor =
                toolColor;
        }

        var iconPath =
            GetIconPath(
                tool);

        // The default icon is the same for every tool,
        // so it is left out and the name lines up instead.
        if (iconPath is not null
            && !IsDefaultIcon(iconPath))
        {
            try
            {
                row.Image =
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

        row.Click +=
            (_, _) =>
                _executionService.Run(
                    tool);

        row.MenuRequested +=
            (_, location) =>
                BuildToolMenu(
                    tool)
                    .ShowAt(
                        row,
                        location);

        return row;
    }

    private static byte[]? _defaultIconBytes;

    private static bool IsDefaultIcon(
        string iconPath)
    {
        try
        {
            if (_defaultIconBytes is null)
            {
                using var resource =
                    typeof(ToolboxPanel).Assembly
                        .GetManifestResourceStream(
                            DefaultToolIconResource);

                if (resource is null)
                    return false;

                using var buffer =
                    new MemoryStream();

                resource.CopyTo(buffer);

                _defaultIconBytes =
                    buffer.ToArray();
            }

            var info =
                new FileInfo(iconPath);

            return info.Length == _defaultIconBytes.Length
                   && File.ReadAllBytes(iconPath)
                       .AsSpan()
                       .SequenceEqual(_defaultIconBytes);
        }
        catch
        {
            return false;
        }
    }

    private void ShowEmptyState(
        string query)
    {
        string message;

        if (_tools.Count == 0)
        {
            message =
                "No tools yet.\n\n" +
                "Click + to add a tested Rhino 8 Python script.";
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
                Wrap = WrapMode.Word,
                TextColor = Ui.Muted
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
