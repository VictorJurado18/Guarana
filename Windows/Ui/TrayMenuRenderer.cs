namespace Guarana.Windows.Ui;

/// <summary>Menu da bandeja com cantos suaves, espaçamento generoso e cores do tema.</summary>
internal sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
{
    public TrayMenuRenderer() : base(new Colors()) => RoundedEdges = false;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Theme.Current.Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using var pen = new Pen(Theme.Current.Border);
        e.Graphics.DrawRectangle(pen, rect);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var p = Theme.Current;
        var g = e.Graphics;
        Theme.Prepare(g);
        var rect = new RectangleF(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        using var path = Theme.RoundedRect(rect, 5);
        using var brush = new SolidBrush(p.SurfaceAlt);
        g.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var p = Theme.Current;
        e.TextColor = e.Item.Tag as string == "status" ? p.TextMuted
                    : e.Item.Tag as string == "danger" ? p.Danger
                    : e.Item.Enabled ? p.Text : p.TextMuted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Current.TextMuted;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(Theme.Current.Border);
        e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        Theme.Prepare(g);
        using var pen = new Pen(Theme.Current.Accent, 2);
        var r = e.ImageRectangle;
        g.DrawLines(pen, [
            new Point(r.Left + 3, r.Top + r.Height / 2),
            new Point(r.Left + r.Width / 2 - 1, r.Bottom - 4),
            new Point(r.Right - 3, r.Top + 4)
        ]);
    }

    private sealed class Colors : ProfessionalColorTable
    {
        public override Color MenuBorder => Theme.Current.Border;
        public override Color ToolStripDropDownBackground => Theme.Current.Surface;
        public override Color ImageMarginGradientBegin => Theme.Current.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Current.Surface;
        public override Color ImageMarginGradientEnd => Theme.Current.Surface;
        public override Color MenuItemSelected => Theme.Current.SurfaceAlt;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color SeparatorDark => Theme.Current.Border;
        public override Color SeparatorLight => Theme.Current.Border;
    }
}
