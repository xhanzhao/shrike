using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// Category input: type a new category, or pick an existing one
/// from the menu behind the chevron. Picking keeps names consistent,
/// so a typo does not create a second, near-identical category.
/// </summary>
public sealed class CategoryField : Panel
{
    private readonly TextBox _textBox;

    private readonly IReadOnlyList<string> _categories;

    /// <summary>
    /// The typed category, matched to the casing of an existing
    /// category when one differs only by case.
    /// </summary>
    public string Category
    {
        get
        {
            var text =
                (_textBox.Text
                 ?? string.Empty)
                .Trim();

            return _categories.FirstOrDefault(
                       category =>
                           string.Equals(
                               category,
                               text,
                               StringComparison.OrdinalIgnoreCase))
                   ?? text;
        }
    }

    public CategoryField(
        IEnumerable<string> existingCategories,
        string initialCategory)
    {
        _categories =
            existingCategories
                .Where(
                    category =>
                        !string.IsNullOrWhiteSpace(
                            category))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    category =>
                        category,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        _textBox =
            Ui.TextBox(
                initialCategory,
                "Type a new category or pick one");

        var chevron =
            new ChevronButton
            {
                Width = 22,
                Height = Ui.FieldHeight - 6,
                ToolTip = "Existing categories"
            };

        var frame =
            new FieldFrame(
                new TableLayout
                {
                    Spacing =
                        new Size(4, 0),

                    Rows =
                    {
                        new TableRow(
                            new TableCell(
                                _textBox,
                                scaleWidth: true),

                            new TableCell(
                                chevron))
                    }
                });

        frame.Track(
            _textBox);

        chevron.Click +=
            (_, _) =>
                ShowMenu(frame);

        Content = frame;
    }

    public void FocusInput()
    {
        _textBox.Focus();
    }

    private void ShowMenu(
        Control anchor)
    {
        var popup =
            new ChoicePopup(
                _categories,
                Category,
                anchor.Width,
                "New category...");

        popup.ActionPicked +=
            (_, _) =>
            {
                _textBox.Text = string.Empty;
                _textBox.Focus();
            };

        popup.Picked +=
            (_, picked) =>
            {
                _textBox.Text = picked;
                _textBox.Focus();
            };

        popup.ShowBelow(
            anchor);
    }

    private sealed class ChevronButton : Drawable
    {
        private bool _hover;

        public event EventHandler? Click;

        public ChevronButton()
        {
            Cursor = Cursors.Pointer;
            BackgroundColor = Ui.Surface;
        }

        protected override void OnMouseEnter(
            MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(
            MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnMouseUp(
            MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (Enabled
                && (e.Buttons & MouseButtons.Primary) != 0)
            {
                Click?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            Draw.Chevron(
                e.Graphics,
                new PointF(
                    Width / 2f,
                    Height / 2f),
                !Enabled
                    ? Ui.Rule
                    : _hover
                        ? Ui.Ink
                        : Ui.Muted);
        }
    }
}
