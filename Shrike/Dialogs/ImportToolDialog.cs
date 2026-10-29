using Eto.Forms;
using Shrike.Controls;
using Shrike.Models;

namespace Shrike.Dialogs;

public sealed class ImportToolDialog : ShrikeDialog
{
    private readonly TextBox _nameTextBox;

    private readonly CategoryField _categoryField;

    public string ToolName =>
        (_nameTextBox.Text ?? string.Empty)
        .Trim();

    public string Category =>
        _categoryField.Category;

    public ImportToolDialog(
        ToolPackageInfo packageInfo,
        IEnumerable<string> existingCategories)
        : base(
            "Import tool",
            "Add a shared .shriketool to your toolbox. " +
            "You can rename it or file it under another category.")
    {
        _nameTextBox =
            Ui.TextBox(
                packageInfo.Name);

        _categoryField =
            new CategoryField(
                existingCategories,
                packageInfo.Category);

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
            "Import",
            TryAccept,
            primary: true);

        InitialFocus =
            _nameTextBox;
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(ToolName))
        {
            ShowValidation(
                "Give the tool a name.");

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
