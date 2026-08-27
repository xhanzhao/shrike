using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Dialogs;

public sealed class EditMacroDialog : Dialog<bool>
{
    private readonly TextArea _macroTextArea;
    private readonly Action<string> _testRunAction;

    public string Macro =>
        (_macroTextArea.Text ?? string.Empty).Trim();

    public EditMacroDialog(
        string toolName,
        string currentMacro,
        Action<string> testRunAction)
    {
        Title = $"Edit Macro - {toolName}";
        Width = 560;
        Height = 300;
        Resizable = true;

        _testRunAction = testRunAction;

        _macroTextArea =
            new TextArea
            {
                Text = currentMacro ?? string.Empty
            };

        var testRunButton =
            new Button
            {
                Text = "Test Run"
            };

        testRunButton.Click +=
            (_, _) => TestRun();

        var saveButton =
            new Button
            {
                Text = "Save"
            };

        saveButton.Click +=
            (_, _) => TrySave();

        var cancelButton =
            new Button
            {
                Text = "Cancel"
            };

        cancelButton.Click +=
            (_, _) => Close(false);

        DefaultButton = saveButton;
        AbortButton = cancelButton;

        var layout =
            new DynamicLayout
            {
                Padding = 12,
                Spacing = new Size(8, 8)
            };

        layout.AddRow(
            new Label
            {
                Text = "Macro"
            });

        layout.Add(
            _macroTextArea,
            yscale: true);

        layout.AddSeparateRow(
            testRunButton,
            null,
            saveButton,
            cancelButton);

        Content = layout;
    }

    private void TestRun()
    {
        var macro = Macro;

        if (string.IsNullOrWhiteSpace(macro))
        {
            ShowWarning("Macro cannot be empty.");
            return;
        }

        _testRunAction(macro);
    }

    private void TrySave()
    {
        if (string.IsNullOrWhiteSpace(Macro))
        {
            ShowWarning("Macro cannot be empty.");
            return;
        }

        Close(true);
    }

    private static void ShowWarning(
        string message)
    {
        MessageBox.Show(
            message,
            "Shrike",
            MessageBoxButtons.OK,
            MessageBoxType.Warning);
    }
}
