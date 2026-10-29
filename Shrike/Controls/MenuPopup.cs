using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// A drop-down menu in Shrike's black-and-white style, used instead
/// of the system menu for the panel's + and options buttons.
///
/// Add items with Add and AddSeparator, then ShowBelow a button.
/// It closes when an item runs, on Escape, or when focus moves away.
/// </summary>
public sealed class MenuPopup : Form
{
    private const int RowHeight = 28;
    private const int SeparatorHeight = 9;
    private const int Inset = 4;
    private const int MinWidth = 180;

    private readonly List<MenuEntry> _entries = new();

    private MenuList? _list;

    private bool _closed;

    private Window? _owner;

    public MenuPopup()
    {
        WindowStyle = WindowStyle.None;
        Resizable = false;
        Minimizable = false;
        Maximizable = false;
        ShowInTaskbar = false;
        Topmost = true;
        BackgroundColor = Ui.Surface;

        LostFocus +=
            (_, _) =>
                CloseOnce();
    }

    public void Add(
        string text,
        Action action,
        bool enabled = true)
    {
        _entries.Add(
            new MenuEntry(
                text,
                action,
                enabled));
    }

    public void AddSeparator()
    {
        _entries.Add(
            MenuEntry.Separator);
    }

    /// <summary>
    /// Shows the menu under the anchor, with its right edge lined up
    /// with the anchor's right edge, since the panel's buttons sit at
    /// the right of the header.
    /// </summary>
    public void ShowBelow(
        Control anchor)
    {
        var font = Ui.BodyFont;

        var textWidth =
            _entries
                .Where(entry => !entry.IsSeparator)
                .Select(entry => font.MeasureString(entry.Text).Width)
                .DefaultIfEmpty(0f)
                .Max();

        var width =
            Math.Max(
                MinWidth,
                (int)Math.Ceiling(textWidth) + 28 + 24 + Inset * 2);

        _list =
            new MenuList(_entries)
            {
                Width = width - Inset * 2 - 2
            };

        _list.Picked +=
            (_, entry) =>
            {
                CloseOnce();
                entry.Action();
            };

        Content =
            new PopupFrame
            {
                Padding = new Padding(Inset + 1),
                Content = _list
            };

        ClientSize =
            new Size(
                width,
                _list.Height + Inset * 2 + 2);

        var window =
            anchor.ParentWindow;

        if (window is not null)
        {
            Owner = window;
            _owner = window;

            window.GotFocus += OnOwnerChanged;
            window.LocationChanged += OnOwnerChanged;
        }

        var origin =
            anchor.PointToScreen(
                new PointF(
                    anchor.Width - width,
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

        if (e.Handled || _list is null)
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

    private sealed record MenuEntry(
        string Text,
        Action Action,
        bool Enabled)
    {
        public static readonly MenuEntry Separator =
            new(string.Empty, () => { }, false);

        public bool IsSeparator =>
            ReferenceEquals(this, Separator);
    }

    private sealed class MenuList : Drawable
    {
        private readonly IReadOnlyList<MenuEntry> _entries;

        private readonly float[] _tops;

        private int _highlightIndex = -1;

        public event EventHandler<MenuEntry>? Picked;

        public MenuList(
            IReadOnlyList<MenuEntry> entries)
        {
            _entries = entries;
            CanFocus = true;
            BackgroundColor = Ui.Surface;

            _tops = new float[entries.Count];

            var y = 0f;

            for (var i = 0; i < entries.Count; i++)
            {
                _tops[i] = y;
                y += RowHeightOf(i);
            }

            Height = (int)Math.Ceiling(y);
        }

        private float RowHeightOf(
            int index)
        {
            return _entries[index].IsSeparator
                ? SeparatorHeight
                : RowHeight;
        }

        private bool IsPickable(
            int index)
        {
            return index >= 0
                   && index < _entries.Count
                   && !_entries[index].IsSeparator
                   && _entries[index].Enabled;
        }

        public void MoveHighlight(
            int step)
        {
            var index = _highlightIndex;

            for (var tries = 0; tries < _entries.Count; tries++)
            {
                index += step;

                if (index < 0 || index >= _entries.Count)
                    return;

                if (IsPickable(index))
                {
                    _highlightIndex = index;
                    Invalidate();
                    return;
                }
            }
        }

        public void PickHighlighted()
        {
            if (IsPickable(_highlightIndex))
                Picked?.Invoke(this, _entries[_highlightIndex]);
        }

        private int IndexAt(
            PointF location)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (location.Y >= _tops[i]
                    && location.Y < _tops[i] + RowHeightOf(i))
                {
                    return i;
                }
            }

            return -1;
        }

        protected override void OnMouseMove(
            MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var index =
                IndexAt(e.Location);

            var next =
                IsPickable(index) ? index : -1;

            Cursor =
                next >= 0
                    ? Cursors.Pointer
                    : Cursors.Default;

            if (next == _highlightIndex)
                return;

            _highlightIndex = next;
            Invalidate();
        }

        protected override void OnMouseLeave(
            MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _highlightIndex = -1;
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

            if (!IsPickable(index))
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

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                if (entry.IsSeparator)
                {
                    var y =
                        _tops[i] + SeparatorHeight / 2f;

                    g.FillRectangle(
                        Ui.Rule,
                        8f,
                        y,
                        Width - 16f,
                        1f);

                    continue;
                }

                var row =
                    new RectangleF(
                        0,
                        _tops[i],
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

                var size =
                    font.MeasureString(entry.Text);

                g.DrawText(
                    font,
                    entry.Enabled
                        ? Ui.Ink
                        : Ui.Muted,
                    row.X + 12f,
                    row.Y + (RowHeight - size.Height) / 2f,
                    entry.Text);
            }
        }
    }
}
