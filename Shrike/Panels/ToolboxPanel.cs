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

    private readonly TextBox _searchBox;
    private readonly StackLayout _toolList;

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
            new CreateToolDialog();

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

        ReloadTools();

        MessageBox.Show(
            result.Message +
            "\n\nCreated in:\n" +
            result.ToolDirectory,
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Information);
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

        var confirmation =
            MessageBox.Show(
                $"Replace the current script for \"{tool.Name}\"?\n\n" +
                "The existing script will be overwritten.",
                "Replace Script",
                MessageBoxButtons.YesNo,
                MessageBoxType.Warning);

        if (confirmation !=
            DialogResult.Yes)
        {
            return;
        }

        if (!_managementService.ReplaceScript(
                tool,
                dialog.FileName,
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

        if (!_managementService.RenameCategory(
                categoryTools,
                newCategory,
                out var error))
        {
            ShowError(error);
            return;
        }

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

        var menu =
            new ContextMenu();

        var renameItem =
            new ButtonMenuItem
            {
                Text =
                    "Rename Category",

                Enabled =
                    _managementService.CanRenameCategory(
                        categoryTools)
            };

        renameItem.Click +=
            (_, _) =>
                RenameCategory(
                    categoryName);

        menu.Items.Add(
            renameItem);

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

    private void RebuildToolList()
    {
        _toolList.Items.Clear();

        var query =
            (_searchBox.Text
             ?? string.Empty)
            .Trim();

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
            filtered);

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
                category.Key);
        }
    }

    private void AddFavoritesSection(
        IReadOnlyList<ToolDefinition> filteredTools)
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
            FavoritesSectionKey);
    }

    private void AddCategorySection(
        string displayName,
        IEnumerable<ToolDefinition> tools,
        string stateKey)
    {
        var content =
            CreateCategoryContent(
                tools);

        var expanded =
            !_categoryExpandedStates
                .TryGetValue(
                    stateKey,
                    out var savedState)
            ||
            savedState;

        var section =
            new CollapsibleCategoryPanel(
                displayName,
                content,
                expanded);

        section.ExpandedChanged +=
            (_, _) =>
            {
                _categoryExpandedStates[
                    stateKey] =
                    section.Expanded;
            };

        if (!string.Equals(
                stateKey,
                FavoritesSectionKey,
                StringComparison.Ordinal))
        {
            section.HeaderContextMenu =
                CreateCategoryContextMenu(
                    displayName);
        }

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
