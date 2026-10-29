using Eto.Drawing;
using Eto.Forms;

namespace Shrike.Controls;

/// <summary>
/// A category in the panel: a rounded frame holding a bold header
/// (thin chevron, title, number of tools) and, when expanded, its
/// tool rows. The frame grows and shrinks with the section.
///
/// Outer margins are intentionally NOT defined here.
/// ToolboxPanel owns the global spacing so all categories use
/// one consistent spacing system.
/// </summary>
public sealed class CollapsibleCategoryPanel : Drawable
{
    private readonly CategoryHeader _header;

    private readonly Panel _contentHost;

    public event EventHandler?
        ExpandedChanged;

    public bool Expanded
    {
        get;
        private set;
    }

    /// <summary>
    /// Raised on right-click of the header. The sender is the header
    /// control and the point is in its coordinates.
    /// </summary>
    public event EventHandler<PointF>? HeaderMenuRequested
    {
        add => _header.MenuRequested += value;
        remove => _header.MenuRequested -= value;
    }

    /// <summary>
    /// Optional background color for the category header.
    /// </summary>
    public Color? HeaderBackgroundColor
    {
        get =>
            _header.FillColor;

        set
        {
            _header.FillColor = value;
            _header.Invalidate();
        }
    }

    public CollapsibleCategoryPanel(
        string title,
        Control categoryContent,
        bool expanded = true,
        int toolCount = 0)
    {
        Expanded = expanded;

        // Room for the 1px frame, so the header's hover fill and the
        // tool rows sit inside it.
        Padding = new Padding(3);
        BackgroundColor = Ui.Surface;

        _header =
            new CategoryHeader(
                title,
                toolCount)
            {
                Expanded = expanded
            };

        _header.Click +=
            (_, _) => Toggle();

        _contentHost =
            new Panel
            {
                // Tool names line up with the category title.
                Padding =
                    new Padding(
                        left: CategoryHeader.TitleLeft - 10,
                        top: 0,
                        right: 0,
                        bottom: 3),

                Content =
                    categoryContent,

                Visible =
                    expanded
            };

        Content =
            new StackLayout
            {
                Orientation =
                    Orientation.Vertical,

                Spacing = 0,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch,

                Items =
                {
                    _header,
                    _contentHost
                }
            };
    }

    private void Toggle()
    {
        Expanded =
            !Expanded;

        _header.Expanded =
            Expanded;

        _header.Invalidate();

        _contentHost.Visible =
            Expanded;

        Invalidate();

        ExpandedChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        base.OnPaint(e);

        Draw.Frame(
            e.Graphics,
            new RectangleF(0, 0, Width, Height),
            Ui.FieldRadius + 2f,
            Ui.Rule,
            Ui.Surface);
    }

    protected override void OnSizeChanged(
        EventArgs e)
    {
        base.OnSizeChanged(e);
        Invalidate();
    }

    private sealed class CategoryHeader : Drawable
    {
        public const int TitleLeft = 24;

        private readonly string _title;

        private readonly int _count;

        private bool _hover;

        public event EventHandler? Click;

        public event EventHandler<PointF>? MenuRequested;

        public bool Expanded { get; set; }

        public Color? FillColor { get; set; }

        public CategoryHeader(
            string title,
            int count)
        {
            _title = title;
            _count = count;
            Height = 28;
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
            else if ((e.Buttons & MouseButtons.Alternate) != 0)
                MenuRequested?.Invoke(this, e.Location);
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.AntiAlias = true;

            var baseFill =
                FillColor ?? Ui.Surface;

            var ink =
                Ui.IsDark(baseFill)
                    ? Colors.White
                    : Ui.Ink;

            Color? fill =
                FillColor;

            if (_hover)
                fill = Color.Blend(baseFill, ink, 0.07f);

            if (fill.HasValue)
            {
                Draw.Frame(
                    g,
                    new RectangleF(0, 0, Width, Height),
                    4f,
                    fill.Value,
                    fill.Value);
            }

            DrawChevron(
                g,
                new PointF(12f, Height / 2f),
                ink);

            var titleFont = Ui.StrongFont;

            var titleSize =
                titleFont.MeasureString(_title);

            var y =
                (Height - titleSize.Height) / 2f;

            g.DrawText(
                titleFont,
                ink,
                TitleLeft,
                y,
                _title);

            if (_count <= 0)
                return;

            g.DrawText(
                Ui.BodyFont,
                Color.Blend(baseFill, ink, 0.55f),
                TitleLeft + titleSize.Width + 8f,
                y,
                _count.ToString());
        }

        /// <summary>
        /// Points down when expanded and right when collapsed.
        /// </summary>
        private void DrawChevron(
            Graphics g,
            PointF c,
            Color color)
        {
            using var pen =
                new Pen(
                    color,
                    1.4f);

            if (Expanded)
            {
                g.DrawLine(pen, c.X - 4f, c.Y - 2f, c.X, c.Y + 2f);
                g.DrawLine(pen, c.X, c.Y + 2f, c.X + 4f, c.Y - 2f);
            }
            else
            {
                g.DrawLine(pen, c.X - 2f, c.Y - 4f, c.X + 2f, c.Y);
                g.DrawLine(pen, c.X + 2f, c.Y, c.X - 2f, c.Y + 4f);
            }
        }
    }
}
