using Eto.Forms;
using Shrike.Controls;

namespace Shrike.Dialogs;

/// <summary>
/// Asks for a category name, e.g. when renaming a category.
/// </summary>
public sealed class CategoryNameDialog : ShrikeDialog
{
    private readonly TextBox _categoryTextBox;

    public string CategoryName =>
        (_categoryTextBox.Text
         ?? string.Empty)
        .Trim();

    public CategoryNameDialog(
        string heading,
        string description,
        string initialName,
        string actionText)
        : base(
            heading,
            description,
            bodyWidth: 360)
    {
        _categoryTextBox =
            Ui.TextBox(
                initialName);

        SetBody(
            Ui.Field(
                "Category name",
                new FieldFrame(
                    _categoryTextBox)));

        AddCancelButton();

        AddButton(
            actionText,
            TryAccept,
            primary: true);

        InitialFocus =
            _categoryTextBox;
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(
                CategoryName))
        {
            ShowValidation(
                "Type a category name.");

            return;
        }

        Close(true);
    }
}
