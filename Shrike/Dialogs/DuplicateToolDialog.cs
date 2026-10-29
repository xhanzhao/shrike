using Eto.Forms;
using Shrike.Controls;
using Shrike.Models;

namespace Shrike.Dialogs;

public sealed class DuplicateToolDialog : ShrikeDialog
{
    private readonly TextBox _nameTextBox;

    private readonly CategoryField _categoryField;

    public string NewName =>
        (_nameTextBox.Text ?? string.Empty)
        .Trim();

    public string Category =>
        _categoryField.Category;

    public DuplicateToolDialog(
        ToolDefinition sourceTool,
        IEnumerable<string> existingCategories)
        : base(
            "Duplicate tool",
            $"Make an editable copy of \"{sourceTool.Name}\". " +
            "The copy keeps the script and icon.")
    {
        _nameTextBox =
            Ui.TextBox(
                $"{sourceTool.Name} Copy");

        _categoryField =
            new CategoryField(
                existingCategories,
                sourceTool.Category);

        SetBody(
            Ui.Stack(
                Ui.FieldGap,
                Ui.Field(
                    "Name",
                    new FieldFrame(
                        _nameTextBox)),
                Ui.Field(
                    "Category",
                    _categoryField)));

        AddCancelButton();

        AddButton(
            "Duplicate",
            TryAccept,
            primary: true);

        InitialFocus =
            _nameTextBox;
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            ShowValidation(
                "Give the copy a name.");

            _nameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            ShowValidation(
                "Pick or type a category.");

            _categoryField.FocusInput();
            return;
        }

        Close(true);
    }
}
