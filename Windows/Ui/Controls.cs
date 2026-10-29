using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Guarana.Windows.Ui;

internal interface IThemed
{
    void ApplyTheme(Palette p);
}

/// <summary>Empilha os filhos verticalmente com espaçamento, ocupando a largura e rolando quando preciso.</summary>
internal sealed class StackPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Gap { get; set; } = 12;

    public StackPanel()
    {
        AutoScroll = true;
        DoubleBuffered = true;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int gap = LogicalToDeviceUnits(Gap);
        int width = Math.Max(0, ClientSize.Width - Padding.Horizontal);
        int y = Padding.Top + AutoScrollPosition.Y;
        foreach (Control child in Controls)
        {
            if (!child.Visible) continue;
            int height = child.GetPreferredSize(new Size(width, 0)).Height;
            child.SetBounds(Padding.Left, y, width, height);
            y += height + gap;
        }
        int contentHeight = y - AutoScrollPosition.Y - gap + Padding.Bottom;
        var min = new Size(0, Math.Max(0, contentHeight));
        if (AutoScrollMinSize != min) AutoScrollMinSize = min;
    }
}

/// <summary>Cartão com cantos arredondados; o conteúdo vai em <see cref="Content"/>.</summary>
internal class Card : Panel, IThemed
{
    public TableLayoutPanel Content { get; } = new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        BackColor = Color.Transparent,
        Margin = Padding.Empty,
        Padding = Padding.Empty,
        GrowStyle = TableLayoutPanelGrowStyle.AddRows
    };

    public Card()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Padding = new Padding(20, 18, 20, 18);
        Content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(Content);
        BackColor = Theme.Current.Surface;
    }

    public void AddRow(Control control, int topSpacing = 0)
    {
        control.Margin = new Padding(0, topSpacing, 0, 0);
        if (control.Dock == DockStyle.None && control.Anchor == (AnchorStyles.Top | AnchorStyles.Left))
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        Content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Content.Controls.Add(control, 0, Content.RowCount++);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int inner = Math.Max(0, proposedSize.Width - Padding.Horizontal);
        int height = Content.GetPreferredSize(new Size(inner, 0)).Height;
        return new Size(proposedSize.Width, height + Padding.Vertical);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.BackdropOf(this));
        Theme.Prepare(g);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundedRect(rect, LogicalToDeviceUnits(12));
        using var fill = new SolidBrush(BackColor);
        g.FillPath(fill, path);
        using var pen = new Pen(Theme.Current.Border);
        g.DrawPath(pen, path);
    }

    public virtual void ApplyTheme(Palette p)
    {
        BackColor = p.Surface;
        Invalidate(true);
    }
}

internal sealed class ThemedLabel : Label, IThemed
{
    public bool Muted { get; }

    public ThemedLabel(string text, Font font, bool muted = false)
    {
        Text = text;
        Font = font;
        Muted = muted;
        AutoSize = true;
        BackColor = Color.Transparent;
        UseMnemonic = false;
        Margin = Padding.Empty;
        ForeColor = muted ? Theme.Current.TextMuted : Theme.Current.Text;
    }

    public void ApplyTheme(Palette p) => ForeColor = Muted ? p.TextMuted : p.Text;
}

/// <summary>Interruptor no estilo do Windows 11, com animação curta e suporte a teclado.</summary>
internal sealed class ToggleSwitch : Control, IThemed
{
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private bool _checked;
    private float _position;
    private bool _hover;

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        Size = new Size(44, 24);
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = Color.Transparent;
        Margin = new Padding(12, 0, 0, 0);
        AccessibleRole = AccessibleRole.CheckButton;
        _animation.Tick += (_, _) =>
        {
            float target = _checked ? 1f : 0f;
            _position += (target - _position) * 0.35f;
            if (Math.Abs(target - _position) < 0.02f)
            {
                _position = target;
                _animation.Stop();
            }
            Invalidate();
        };
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            if (IsHandleCreated && Visible) _animation.Start();
            else _position = value ? 1 : 0;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>Define o estado inicial sem animar nem disparar <see cref="CheckedChanged"/>.</summary>
    public void SetSilently(bool value)
    {
        _checked = value;
        _position = value ? 1 : 0;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        Focus();
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Theme.BackdropOf(this));
        Theme.Prepare(g);

        float pad = LogicalToDeviceUnits(2);
        var track = new RectangleF(pad, pad, Width - pad * 2 - 1, Height - pad * 2 - 1);
        var off = _hover ? Theme.Blend(p.Track, p.Text, 0.12f) : p.Track;
        var on = _hover ? p.AccentHover : p.Accent;
        if (!Enabled) { off = Theme.Blend(off, p.Surface, 0.5f); on = Theme.Blend(on, p.Surface, 0.5f); }
        using (var path = Theme.RoundedRect(track, track.Height / 2))
        using (var brush = new SolidBrush(Theme.Blend(off, on, _position)))
            g.FillPath(brush, path);

        float knobInset = LogicalToDeviceUnits(3);
        float knob = track.Height - knobInset * 2;
        float x = track.X + knobInset + (track.Width - knob - knobInset * 2) * _position;
        var knobRect = new RectangleF(x, track.Y + knobInset, knob, knob);
        using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            g.FillEllipse(shadow, knobRect.X, knobRect.Y + 1, knob, knob);
        using (var white = new SolidBrush(Color.White))
            g.FillEllipse(white, knobRect);

        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(p.Accent, LogicalToDeviceUnits(1));
            using var ring = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), Height / 2f);
            g.DrawPath(pen, ring);
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ToggleAccessible(this);

    private sealed class ToggleAccessible(ToggleSwitch owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleStates State =>
            base.State | (owner.Checked ? AccessibleStates.Checked : AccessibleStates.None);
        public override string DefaultAction => owner.Checked ? "Desativar" : "Ativar";
        public override void DoDefaultAction() => owner.Checked = !owner.Checked;
    }

    public void ApplyTheme(Palette p) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animation.Dispose();
        base.Dispose(disposing);
    }
}

internal enum PillKind { Primary, Secondary, Ghost, Chip }

/// <summary>Botão desenhado sob medida: primário, secundário, discreto ou chip selecionável.</summary>
internal sealed class PillButton : Button, IThemed
{
    private bool _hover;
    private bool _down;
    private bool _selected;

    public PillKind Kind { get; }

    /// <summary>Raio em unidades lógicas; negativo = formato de pílula.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = -1;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    public PillButton(string text, PillKind kind)
    {
        Text = text;
        Kind = kind;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = kind == PillKind.Primary ? Theme.BodyStrong : Theme.Body;
        Padding = kind == PillKind.Chip ? new Padding(14, 0, 14, 0) : new Padding(18, 0, 18, 0);
        Height = kind == PillKind.Chip ? 32 : 36;
        Margin = new Padding(0, 0, 8, 0);
        AutoSize = false;
        Size = GetPreferredSize(Size.Empty);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        return new Size(text.Width + Padding.Horizontal, Height);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Width = GetPreferredSize(Size.Empty).Width;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var p = Theme.Current;
        var g = pevent.Graphics;
        var backdrop = Theme.BackdropOf(this);
        g.Clear(backdrop);
        Theme.Prepare(g);

        Color fill, text;
        Color? border = null;
        switch (Kind)
        {
            case PillKind.Primary:
                fill = _down ? p.AccentPressed : _hover ? p.AccentHover : p.Accent;
                text = p.OnAccent;
                break;
            case PillKind.Secondary:
                fill = _down ? Theme.Blend(p.SurfaceAlt, p.Text, 0.08f) : _hover ? p.SurfaceAlt : p.Surface;
                text = p.Text;
                border = p.Border;
                break;
            case PillKind.Chip:
                if (_selected)
                {
                    fill = _down ? p.AccentPressed : _hover ? p.AccentHover : p.Accent;
                    text = p.OnAccent;
                }
                else
                {
                    fill = _down ? Theme.Blend(p.SurfaceAlt, p.Text, 0.12f)
                         : _hover ? Theme.Blend(p.SurfaceAlt, p.Text, 0.06f) : p.SurfaceAlt;
                    text = p.Text;
                }
                break;
            default:
                fill = _down ? Theme.Blend(backdrop, p.Text, 0.12f) : _hover ? Theme.Blend(backdrop, p.Text, 0.06f) : backdrop;
                text = p.TextMuted;
                if (_hover) text = p.Text;
                break;
        }
        if (!Enabled)
        {
            fill = Theme.Blend(fill, backdrop, 0.55f);
            text = Theme.Blend(text, backdrop, 0.55f);
        }

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = Radius < 0 ? rect.Height / 2 : LogicalToDeviceUnits(Radius);
        using (var path = Theme.RoundedRect(rect, radius))
        {
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            if (border is Color b)
            {
                using var pen = new Pen(b);
                g.DrawPath(pen, path);
            }
        }

        if (Focused && ShowFocusCues)
        {
            float inset = LogicalToDeviceUnits(2);
            using var ring = Theme.RoundedRect(RectangleF.Inflate(rect, -inset, -inset), Math.Max(0, radius - inset));
            using var pen = new Pen(Kind == PillKind.Primary || _selected ? p.OnAccent : p.Accent, LogicalToDeviceUnits(1));
            g.DrawPath(pen, ring);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }

    public void ApplyTheme(Palette p) => Invalidate();
}

/// <summary>Campo de texto com moldura arredondada, foco em destaque e estado de erro.</summary>
internal sealed class TextField : Panel, IThemed
{
    private bool _invalid;

    public TextBox Box { get; } = new()
    {
        BorderStyle = BorderStyle.None,
        TextAlign = HorizontalAlignment.Center
    };

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Suffix { get; init; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Invalid
    {
        get => _invalid;
        set { if (_invalid != value) { _invalid = value; Invalidate(); } }
    }

    public TextField(int width)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Size = new Size(width, 32);
        Margin = Padding.Empty;
        Padding = new Padding(8, 0, 8, 0);
        Cursor = Cursors.IBeam;
        Box.Font = Theme.Body;
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Click += (_, _) => Box.Focus();
        ApplyTheme(Theme.Current);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int suffix = Suffix is null ? 0 : TextRenderer.MeasureText(Suffix, Theme.Small).Width;
        Box.SetBounds(Padding.Left, (Height - Box.PreferredHeight) / 2,
            Math.Max(10, Width - Padding.Horizontal - suffix), Box.PreferredHeight);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Theme.BackdropOf(this));
        Theme.Prepare(g);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundedRect(rect, LogicalToDeviceUnits(8));
        using (var fill = new SolidBrush(p.SurfaceAlt)) g.FillPath(fill, path);
        var color = _invalid ? p.Danger : Box.Focused ? p.Accent : p.Border;
        using (var pen = new Pen(color, Box.Focused || _invalid ? LogicalToDeviceUnits(2) * 0.75f : 1f))
            g.DrawPath(pen, path);
        if (Suffix is not null)
        {
            var area = new Rectangle(0, 0, Width - Padding.Right, Height);
            TextRenderer.DrawText(g, Suffix, Theme.Small, area, p.TextMuted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    protected override void OnPaint(PaintEventArgs e) { }

    public void ApplyTheme(Palette p)
    {
        Box.BackColor = p.SurfaceAlt;
        Box.ForeColor = p.Text;
        Invalidate();
    }
}

/// <summary>Linha de configuração: título e descrição à esquerda, controle à direita.</summary>
internal sealed class SettingRow : TableLayoutPanel
{
    public SettingRow(string title, string description, Control control, Font? titleFont = null)
    {
        ColumnCount = 2;
        RowCount = 2;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Transparent;
        Margin = Padding.Empty;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new ThemedLabel(title, titleFont ?? Theme.BodyStrong) { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        var descLabel = new ThemedLabel(description, Theme.Small, muted: true)
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 2, 0, 0)
        };
        Controls.Add(titleLabel, 0, 0);
        Controls.Add(descLabel, 0, 1);
        control.Anchor = AnchorStyles.Right;
        Controls.Add(control, 1, 0);
        SetRowSpan(control, 2);
        if (control is ToggleSwitch toggle)
        {
            toggle.AccessibleName = title;
            titleLabel.Cursor = Cursors.Hand;
            titleLabel.Click += (_, _) => toggle.Checked = !toggle.Checked;
        }
    }
}

/// <summary>Divisória fina entre linhas de um cartão.</summary>
internal sealed class Divider : Control, IThemed
{
    public Divider()
    {
        Height = 1;
        Margin = new Padding(0, 14, 0, 14);
        BackColor = Theme.Current.Border;
    }

    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width, 1);

    public void ApplyTheme(Palette p) => BackColor = p.Border;
}

/// <summary>Rodapé fixo: confirma que tudo foi salvo e convida a fechar a janela sem receio.</summary>
internal sealed class FooterBar : Panel, IThemed
{
    private readonly Label _status = new()
    {
        Font = Theme.BodyStrong,
        AutoSize = true,
        BackColor = Color.Transparent,
        UseMnemonic = false,
        Margin = Padding.Empty
    };
    private readonly ThemedLabel _hint = new("Ao fechar, o Guaraná continua rodando na bandeja.", Theme.Small, muted: true);
    private bool _error;

    public PillButton Done { get; } = new("Concluir", PillKind.Primary);

    public FooterBar()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Dock = DockStyle.Bottom;
        Height = 68;
        Padding = new Padding(24, 0, 24, 0);
        var text = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left
        };
        _hint.Margin = new Padding(0, 2, 0, 0);
        text.Controls.Add(_status, 0, 0);
        text.Controls.Add(_hint, 0, 1);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Done.Anchor = AnchorStyles.Right;
        Done.Margin = Padding.Empty;
        layout.Controls.Add(text, 0, 0);
        layout.Controls.Add(Done, 1, 0);
        Controls.Add(layout);
    }

    public string Status => _status.Text;
    public bool IsError => _error;

    public void SetStatus(string text, bool error)
    {
        _error = error;
        _status.Text = text;
        _status.ForeColor = error ? Theme.Current.Danger : Theme.Current.Text;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Current.Border);
        e.Graphics.DrawLine(pen, 0, 0, Width, 0);
    }

    public void ApplyTheme(Palette p)
    {
        BackColor = p.Surface;
        SetStatus(_status.Text, _error);
        Invalidate(true);
    }
}
