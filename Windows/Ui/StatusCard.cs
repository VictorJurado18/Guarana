using System.Drawing.Drawing2D;
using Guarana.Core;

namespace Guarana.Windows.Ui;

/// <summary>Cartão de destaque com o estado atual, o ícone da fruta e a ação principal.</summary>
internal sealed class StatusCard : Control, IThemed
{
    private readonly Image _awake;
    private readonly Image _asleep;
    private ActivationState? _state;
    private string _title = "";
    private string _subtitle = "";

    public PillButton Action { get; } = new("Pausar", PillKind.Secondary);

    public StatusCard(Image awake, Image asleep)
    {
        _awake = awake;
        _asleep = asleep;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Controls.Add(Action);
        AccessibleRole = AccessibleRole.StaticText;
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        new(proposedSize.Width, LogicalToDeviceUnits(112));

    public void SetState(ActivationState state, DateTime now)
    {
        string title, subtitle;
        if (state.Temporary && state.NextChange is DateTime until)
        {
            title = "Mantendo o PC acordado";
            subtitle = $"Ativação temporária · {Remaining(until - now)} · termina às {until:HH:mm}";
        }
        else if (state.Active)
        {
            title = "Mantendo o PC acordado";
            subtitle = state.NextChange is DateTime end ? $"{state.Reason} · até {Friendly(end, now)}" : state.Reason;
        }
        else if (state.Paused)
        {
            title = "Pausado";
            subtitle = "Nenhum sinal é enviado até você retomar.";
        }
        else
        {
            title = "Aguardando o próximo horário";
            subtitle = state.NextChange is DateTime start ? $"Começa {Friendly(start, now)}" : "Nenhum período programado.";
        }

        Action.Text = state.Temporary ? "Encerrar" : state.Paused ? "Retomar" : "Pausar";
        bool layout = Action.Width != Action.GetPreferredSize(Size.Empty).Width;
        if (state == _state && title == _title && subtitle == _subtitle && !layout) return;
        _state = state;
        _title = title;
        _subtitle = subtitle;
        AccessibleName = title + ". " + subtitle;
        BackColor = state.Active ? Theme.Current.SuccessSoft : Theme.Current.Surface;
        PerformLayout();
        Invalidate(true);
    }

    private static string Remaining(TimeSpan left)
    {
        if (left.TotalMinutes < 1) return "menos de 1 min";
        if (left.TotalHours < 1) return $"faltam {Math.Ceiling(left.TotalMinutes):0} min";
        int minutes = (int)Math.Ceiling(left.TotalMinutes);
        return $"faltam {minutes / 60} h {minutes % 60:00} min";
    }

    private static string Friendly(DateTime when, DateTime now)
    {
        if (when.Date == now.Date) return $"hoje às {when:HH:mm}";
        if (when.Date == now.Date.AddDays(1)) return $"amanhã às {when:HH:mm}";
        string day = when.ToString("dddd", new System.Globalization.CultureInfo("pt-BR"));
        return $"{day} às {when:HH:mm}";
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int pad = LogicalToDeviceUnits(20);
        Action.Width = Action.GetPreferredSize(Size.Empty).Width;
        Action.Location = new Point(Width - pad - Action.Width, (Height - Action.Height) / 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Theme.BackdropOf(this));
        Theme.Prepare(g);

        bool active = _state?.Active == true;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Theme.RoundedRect(rect, LogicalToDeviceUnits(14)))
        {
            using (var brush = new SolidBrush(BackColor)) g.FillPath(brush, path);
            using var pen = new Pen(active ? Theme.Blend(p.Border, p.Success, 0.35f) : p.Border);
            g.DrawPath(pen, path);
        }

        int pad = LogicalToDeviceUnits(20);
        int icon = LogicalToDeviceUnits(60);
        var iconRect = new Rectangle(pad, (Height - icon) / 2, icon, icon);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(active ? _awake : _asleep, iconRect);

        int textX = iconRect.Right + LogicalToDeviceUnits(16);
        int textW = Math.Max(0, Action.Left - textX - LogicalToDeviceUnits(12));

        // Indicador + rótulo curto acima do título.
        string badge = _state is null ? "" : _state.Active ? "ATIVO" : _state.Paused ? "PAUSADO" : "INATIVO";
        var badgeColor = _state?.Active == true ? p.Success : p.TextMuted;
        int titleH = TextRenderer.MeasureText("Ag", Theme.Hero).Height;
        int smallH = TextRenderer.MeasureText("Ag", Theme.Small).Height;
        int blockH = smallH + titleH + smallH + LogicalToDeviceUnits(2);
        int y = (Height - blockH) / 2;

        int dot = LogicalToDeviceUnits(8);
        using (var brush = new SolidBrush(badgeColor))
            g.FillEllipse(brush, textX, y + (smallH - dot) / 2f, dot, dot);
        TextRenderer.DrawText(g, badge, Theme.Small, new Point(textX + dot + LogicalToDeviceUnits(6), y), badgeColor,
            TextFormatFlags.NoPrefix);
        y += smallH;

        TextRenderer.DrawText(g, _title, Theme.Hero, new Rectangle(textX, y, textW, titleH), p.Text,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        y += titleH + LogicalToDeviceUnits(2);
        TextRenderer.DrawText(g, _subtitle, Theme.Small, new Rectangle(textX, y, textW, smallH), p.TextMuted,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    public void ApplyTheme(Palette p)
    {
        BackColor = _state?.Active == true ? p.SuccessSoft : p.Surface;
        Invalidate(true);
    }
}
