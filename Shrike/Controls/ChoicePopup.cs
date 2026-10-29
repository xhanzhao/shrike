using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// A borderless list that drops down under a field, drawn in
/// Shrike's black-and-white style instead of the system menu.
///
/// It closes when a choice is picked, on Escape, or when focus
/// moves elsewhere.
/// </summary>
public sealed class ChoicePopup : Form
{
    private const int RowHeight = 28;
    private const int MaxVisibleRows = 8;
    private const int Inset = 4;

    private readonly ChoiceList _list;

    private bool _closed;

    private Window? _owner;

    public event EventHandler<string>? Picked;

    /// <summary>
    /// Raised when the action row under the list is clicked.
    /// </summary>
    public event EventHandler? ActionPicked;

    public ChoicePopup(
        IReadOnlyList<string> choices,
        string? current,
        int width,
        string? actionText = null)
    {
        WindowStyle = WindowStyle.None;
        Resizable = false;
        Minimizable = false;
        Maximizable = false;
        ShowInTaskbar = false;
        Topmost = true;
        BackgroundColor = Ui.Surface;

        _list =
            new ChoiceList(
                choices,
                current)
            {
                Width = width - Inset * 2 - 2
            };

        _list.Picked +=
            (_, choice) =>
            {
                Picked?.Invoke(this, choice);
                CloseOnce();
            };

        var visibleRows =
            Math.Min(
                MaxVisibleRows,
                choices.Count);

        var listHeight =
            visibleRows * RowHeight;

        Control listHost =
            choices.Count > MaxVisibleRows
                ? new Scrollable
                {
                    Border = BorderType.None,
                    ExpandContentWidth = true,
                    Height = listHeight,
                    BackgroundColor = Ui.Surface,
                    Content = _list
                }
                : _list;

        var body =
            new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 0,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch
            };

        if (choices.Count > 0)
            body.Items.Add(listHost);

        var actionHeight = 0;

        if (!string.IsNullOrWhiteSpace(actionText))
        {
            var action =
                new ActionRow(actionText);

            action.Click +=
                (_, _) =>
                {
                    ActionPicked?.Invoke(this, EventArgs.Empty);
                    CloseOnce();
                };

            if (choices.Count > 0)
            {
                body.Items.Add(
                    new Drawable
                    {
                        Height = 1,
                        BackgroundColor = Ui.Rule
                    });

                actionHeight += 1;
            }

            body.Items.Add(action);

            actionHeight += RowHeight;
        }

        Content =
            new PopupFrame
            {
                Padding = new Padding(Inset + 1),
                Content = body
            };

        ClientSize =
            new Size(
                width,
                listHeight + actionHeight + Inset * 2 + 2);

        LostFocus +=
            (_, _) =>
                CloseOnce();
    }

    /// <summary>
    /// Shows the list under the anchor, aligned to its left edge.
    /// </summary>
    public void ShowBelow(
        Control anchor)
    {
        var window =
            anchor.ParentWindow;

        if (window is not null)
        {
            Owner = window;
            _owner = window;

            // Clicking back into the dialog, or moving it,
            // dismisses the list.
            window.GotFocus += OnOwnerChanged;
            window.LocationChanged += OnOwnerChanged;
        }

        var origin =
            anchor.PointToScreen(
                new PointF(
                    0,
                    anchor.Height + 4));

        Location =
            new Point(
                (int)Math.Round(origin.X),
                (int)Math.Round(origin.Y));

        Show();
        _list.Focus();
    }

    protected override void OnKeyDown(
        KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
            return;

        switch (e.Key)
        {
            case Keys.Escape:
                e.Handled = true;
                CloseOnce();
                break;

            case Keys.Down:
                e.Handled = true;
                _list.MoveHighlight(1);
                break;

            case Keys.Up:
                e.Handled = true;
                _list.MoveHighlight(-1);
                break;

            case Keys.Enter:
                e.Handled = true;
                _list.PickHighlighted();
                break;
        }
    }

    private void OnOwnerChanged(
        object? sender,
        EventArgs e)
    {
        CloseOnce();
    }

    private void CloseOnce()
    {
        if (_closed)
            return;

        _closed = true;

        if (_owner is not null)
        {
            _owner.GotFocus -= OnOwnerChanged;
            _owner.LocationChanged -= OnOwnerChanged;
        }

        Close();
    }

    /// <summary>
    /// The popup's 1px outline.
    /// </summary>
    private sealed class PopupFrame : Drawable
    {
        public PopupFrame()
        {
            BackgroundColor = Ui.Surface;
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            Draw.Frame(
                e.Graphics,
                new RectangleF(0, 0, Width, Height),
                1f,
                Ui.RuleStrong,
                Ui.Surface);
        }
    }

    /// <summary>
    /// A row under the list for an action rather than a choice,
    /// e.g. "New category...".
    /// </summary>
    private sealed class ActionRow : Drawable
    {
        private readonly string _text;

        private bool _hover;

        public event EventHandler? Click;

        public ActionRow(
            string text)
        {
            _text = text;
            Height = RowHeight;
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

            if ((e.Buttons & MouseButtons.Primary) != 0)
                Click?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.AntiAlias = true;

            var row =
                new RectangleF(0, 0, Width, Height);

            if (_hover)
            {
                Draw.Frame(
                    g,
                    RectangleF.Inflate(row, 0, -2),
                    4f,
                    Ui.Hover,
                    Ui.Hover);
            }

            // A plus sign where the list draws its check mark.
            using var pen =
                new Pen(
                    Ui.Ink,
                    1.4f);

            var center =
                new PointF(14f, Height / 2f);

            g.DrawLine(pen, center.X - 4f, center.Y, center.X + 4f, center.Y);
            g.DrawLine(pen, center.X, center.Y - 4f, center.X, center.Y + 4f);

            var font = Ui.BodyFont;

            var size =
                font.MeasureString(_text);

            g.DrawText(
                font,
                Ui.Ink,
                28f,
                (Height - size.Height) / 2f,
                _text);
        }
    }

    private sealed class ChoiceList : Drawable
    {
        private readonly IReadOnlyList<string> _choices;

        private readonly int _currentIndex;

        private int _highlightIndex;

        public event EventHandler<string>? Picked;

        public ChoiceList(
            IReadOnlyList<string> choices,
            string? current)
        {
            _choices = choices;
            CanFocus = true;
            Cursor = Cursors.Pointer;
            BackgroundColor = Ui.Surface;
            Height = Math.Max(1, choices.Count) * RowHeight;

            _currentIndex = -1;

            for (var i = 0; i < choices.Count; i++)
            {
                if (string.Equals(
                        choices[i],
                        current,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _currentIndex = i;
                }
            }

            _highlightIndex =
                Math.Max(0, _currentIndex);
        }

        public void MoveHighlight(
            int step)
        {
            if (_choices.Count == 0)
                return;

            _highlightIndex =
                Math.Clamp(
                    _highlightIndex + step,
                    0,
                    _choices.Count - 1);

            Invalidate();
        }

        public void PickHighlighted()
        {
            if (_highlightIndex >= 0
                && _highlightIndex < _choices.Count)
            {
                Picked?.Invoke(
                    this,
                    _choices[_highlightIndex]);
            }
        }

        private int IndexAt(
            PointF location)
        {
            var index =
                (int)(location.Y / RowHeight);

            return index >= 0 && index < _choices.Count
                ? index
                : -1;
        }

        protected override void OnMouseMove(
            MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var index =
                IndexAt(e.Location);

            if (index < 0
                || index == _highlightIndex)
            {
                return;
            }

            _highlightIndex = index;
            Invalidate();
        }

        protected override void OnMouseUp(
            MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if ((e.Buttons & MouseButtons.Primary) == 0)
                return;

            var index =
                IndexAt(e.Location);

            if (index < 0)
                return;

            _highlightIndex = index;
            PickHighlighted();
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.AntiAlias = true;

            var font = Ui.BodyFont;

            for (var i = 0; i < _choices.Count; i++)
            {
                var row =
                    new RectangleF(
                        0,
                        i * RowHeight,
                        Width,
                        RowHeight);

                if (i == _highlightIndex)
                {
                    Draw.Frame(
                        g,
                        row,
                        4f,
                        Ui.Hover,
                        Ui.Hover);
                }

                if (i == _currentIndex)
                {
                    DrawCheck(
                        g,
                        new PointF(
                            row.X + 14f,
                            row.Y + RowHeight / 2f));
                }

                var textSize =
                    font.MeasureString(
                        _choices[i]);

                g.DrawText(
                    i == _currentIndex
                        ? Ui.StrongFont
                        : font,
                    Ui.Ink,
                    row.X + 28f,
                    row.Y + (RowHeight - textSize.Height) / 2f,
                    _choices[i]);
            }
        }

        private static void DrawCheck(
            Graphics g,
            PointF center,
            Color color)
        {
            using var pen =
                new Pen(
                    color,
                    1.6f);

            g.DrawLine(
                pen,
                center.X - 4f,
                center.Y,
                center.X - 1f,
                center.Y + 3f);

            g.DrawLine(
                pen,
                center.X - 1f,
                center.Y + 3f,
                center.X + 4f,
                center.Y - 3f);
        }

        private static void DrawCheck(
            Graphics g,
            PointF center)
        {
            DrawCheck(
                g,
                center,
                Ui.Ink);
        }
    }
}
