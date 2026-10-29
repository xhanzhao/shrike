using Eto.Drawing;
using Eto.Forms;
using Shrike.Controls;

namespace Shrike.Dialogs;

/// <summary>
/// The frame shared by every Shrike dialog:
///
///   Heading
///   One line on what the dialog does.
///
///   [body: labels stacked above fields]
///   ─────────────────────────────────────────
///   validation message          [Cancel] [Primary]
///
/// The window sizes itself to its content. Buttons are drawn by
/// Shrike, so there is no system-blue default button; Enter runs
/// the primary action and Escape cancels.
/// </summary>
public abstract class ShrikeDialog : Dialog<bool>
{
    private readonly Panel _bodyHost;

    private readonly Label _validationLabel;

    private readonly StackLayout _footerButtons;

    private readonly StackLayout _footerExtras;

    private Action? _primaryAction;

    /// <summary>
    /// The control that gets focus when the dialog opens.
    /// </summary>
    protected Control? InitialFocus { get; set; }

    protected ShrikeDialog(
        string heading,
        string? description,
        int bodyWidth = 420)
    {
        Title = heading;
        Resizable = false;
        Minimizable = false;
        Maximizable = false;
        ShowInTaskbar = false;
        AutoSize = true;
        BackgroundColor = Ui.Surface;

        _bodyHost =
            new Panel
            {
                Width = bodyWidth
            };

        _validationLabel =
            new Label
            {
                TextColor = Ui.Ink,
                Font = Ui.StrongFont,
                Wrap = WrapMode.Word,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false
            };

        _footerButtons =
            new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalContentAlignment = VerticalAlignment.Center
            };

        _footerExtras =
            new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalContentAlignment = VerticalAlignment.Center
            };

        var header =
            Ui.Stack(
                4,
                Ui.Title(heading),
                string.IsNullOrWhiteSpace(description)
                    ? null
                    : Ui.Description(description));

        var top =
            new Panel
            {
                BackgroundColor = Ui.Surface,

                Padding =
                    new Padding(
                        Ui.DialogPadding,
                        18,
                        Ui.DialogPadding,
                        Ui.DialogPadding),

                // The header gets the body's width, so a long
                // description wraps instead of widening the dialog.
                Content =
                    Ui.Stack(
                        18,
                        new Panel
                        {
                            Width = bodyWidth,
                            Content = header
                        },
                        _bodyHost)
            };

        var footer =
            new Panel
            {
                BackgroundColor = Ui.Footer,

                Padding =
                    new Padding(
                        Ui.DialogPadding,
                        12),

                Content =
                    new TableLayout
                    {
                        Spacing =
                            new Size(12, 0),

                        Rows =
                        {
                            new TableRow(
                                new TableCell(
                                    _footerExtras),

                                // The cell stays in the layout while the
                                // message is hidden, so the buttons keep
                                // to the right.
                                new TableCell(
                                    new Panel
                                    {
                                        Content = _validationLabel
                                    },
                                    scaleWidth: true),

                                new TableCell(
                                    _footerButtons))
                        }
                    }
            };

        Content =
            new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 0,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch,

                Items =
                {
                    top,
                    new Divider(),
                    footer
                }
            };
    }

    protected void SetBody(
        Control body)
    {
        _bodyHost.Content = body;
    }

    /// <summary>
    /// Adds a button on the right of the footer, in reading order.
    /// The primary button also runs on Enter.
    /// </summary>
    protected RoundedButton AddButton(
        string text,
        Action onClick,
        bool primary = false)
    {
        var button =
            Ui.Button(
                text,
                primary);

        button.Click +=
            (_, _) =>
                onClick();

        if (primary)
            _primaryAction = onClick;

        _footerButtons.Items.Add(button);

        return button;
    }

    /// <summary>
    /// Adds a secondary button on the left of the footer,
    /// for actions that do not close the dialog.
    /// </summary>
    protected RoundedButton AddFooterAction(
        string text,
        Action onClick)
    {
        var button =
            Ui.Button(text);

        button.Click +=
            (_, _) =>
                onClick();

        _footerExtras.Items.Add(button);

        return button;
    }

    protected void AddCancelButton(
        string text = "Cancel")
    {
        AddButton(
            text,
            Cancel);
    }

    /// <summary>
    /// Shows a short problem in the footer, next to the buttons,
    /// instead of opening a message box on top of the dialog.
    /// </summary>
    protected void ShowValidation(
        string message)
    {
        _validationLabel.Text = message;
        _validationLabel.Visible = true;
    }

    protected void ClearValidation()
    {
        _validationLabel.Text = string.Empty;
        _validationLabel.Visible = false;
    }

    /// <summary>
    /// Called for Escape and the Cancel button.
    /// </summary>
    protected virtual void Cancel()
    {
        Close(false);
    }

    protected override void OnShown(
        EventArgs e)
    {
        base.OnShown(e);
        InitialFocus?.Focus();
    }

    protected override void OnKeyDown(
        KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
            return;

        if (e.KeyData == Keys.Escape)
        {
            e.Handled = true;
            Cancel();
        }
        else if (e.KeyData == Keys.Enter
                 && _primaryAction is not null)
        {
            e.Handled = true;
            _primaryAction();
        }
    }

    /// <summary>
    /// The 1px line between the body and the footer.
    /// </summary>
    private sealed class Divider : Drawable
    {
        public Divider()
        {
            Height = 1;
            BackgroundColor = Ui.Rule;
        }
    }
}
