using System.ComponentModel;
using Guarana.Core;

namespace Guarana.Windows.Ui;

/// <summary>
/// Editor de horários: cada período tem um intervalo e os dias em que vale.
/// Internamente vira uma regra por dia, o mesmo formato salvo em settings.json.
/// </summary>
internal sealed class ScheduleEditor : TableLayoutPanel, IThemed
{
    private readonly TableLayoutPanel _rows;
    private readonly PillButton _add;
    private readonly ThemedLabel _empty;

    public event EventHandler? Changed;

    public ScheduleEditor()
    {
        ColumnCount = 1;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Transparent;
        Margin = Padding.Empty;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _rows = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _empty = new ThemedLabel("Nenhum período. Adicione um para começar.", Theme.Small, muted: true)
        {
            Margin = new Padding(0, 4, 0, 4)
        };

        _add = new PillButton("+  Adicionar período", PillKind.Ghost) { Margin = new Padding(0, 6, 0, 0), Radius = 8 };
        _add.Click += (_, _) =>
        {
            var row = AddRow([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                "09:00", "18:00");
            row.FocusStart();
            RaiseChanged();
        };

        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(_rows, 0, 0);
        Controls.Add(_empty, 0, 1);
        Controls.Add(_add, 0, 2);
    }

    public void Load(IEnumerable<ScheduleRule> rules)
    {
        SuspendLayout();
        foreach (Control c in _rows.Controls.Cast<Control>().ToList()) c.Dispose();
        _rows.Controls.Clear();
        _rows.RowStyles.Clear();
        _rows.RowCount = 0;
        // Agrupa dias com o mesmo intervalo, na ordem em que aparecem.
        foreach (var group in rules.GroupBy(r => (r.Start, r.End)))
            AddRow(group.Select(r => r.Day), group.Key.Start, group.Key.End);
        UpdateEmpty();
        ResumeLayout(true);
    }

    /// <summary>Converte a interface em regras. Retorna null e uma mensagem se algo estiver inválido.</summary>
    public List<ScheduleRule>? TryGetRules(out string? error)
    {
        error = null;
        var rules = new List<ScheduleRule>();
        foreach (var row in _rows.Controls.OfType<PeriodRow>())
        {
            if (!row.TryRead(out var days, out var start, out var end, out error)) return null;
            rules.AddRange(days.Select(d => new ScheduleRule(d, start, end)));
        }
        return rules;
    }

    private PeriodRow AddRow(IEnumerable<DayOfWeek> days, string start, string end)
    {
        var row = new PeriodRow(days, start, end) { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
        row.Changed += (_, _) => RaiseChanged();
        row.RemoveRequested += (_, _) =>
        {
            _rows.Controls.Remove(row);
            row.Dispose();
            UpdateEmpty();
            RaiseChanged();
        };
        _rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rows.Controls.Add(row, 0, _rows.RowCount++);
        UpdateEmpty();
        return row;
    }

    private void UpdateEmpty() => _empty.Visible = _rows.Controls.Count == 0;

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool Enabled
    {
        get => base.Enabled;
        set
        {
            base.Enabled = value;
            foreach (Control c in Controls) c.Enabled = value;
        }
    }

    public void ApplyTheme(Palette p) => Invalidate(true);
}

/// <summary>Uma linha: chips de dia + início + fim + remover.</summary>
internal sealed class PeriodRow : Panel
{
    private static readonly (DayOfWeek Day, string Short, string Long)[] Week =
    [
        (DayOfWeek.Monday, "Seg", "segunda"),
        (DayOfWeek.Tuesday, "Ter", "terça"),
        (DayOfWeek.Wednesday, "Qua", "quarta"),
        (DayOfWeek.Thursday, "Qui", "quinta"),
        (DayOfWeek.Friday, "Sex", "sexta"),
        (DayOfWeek.Saturday, "Sáb", "sábado"),
        (DayOfWeek.Sunday, "Dom", "domingo")
    ];

    private readonly List<(DayOfWeek Day, PillButton Chip)> _chips = [];
    private readonly TextField _start = new(64);
    private readonly TextField _end = new(64);
    private readonly ThemedLabel _dash = new("até", Theme.Small, muted: true);
    private readonly PillButton _remove = new("✕", PillKind.Ghost) { Radius = 8 };

    public event EventHandler? Changed;
    public event EventHandler? RemoveRequested;

    public PeriodRow(IEnumerable<DayOfWeek> days, string start, string end)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 8);
        var selected = days.ToHashSet();
        foreach (var (day, shortName, longName) in Week)
        {
            var chip = new PillButton(shortName, PillKind.Chip)
            {
                Selected = selected.Contains(day),
                Radius = 8,
                Padding = new Padding(0),
                AccessibleName = longName
            };
            chip.Click += (_, _) => { chip.Selected = !chip.Selected; Changed?.Invoke(this, EventArgs.Empty); };
            _chips.Add((day, chip));
            Controls.Add(chip);
        }

        _start.Box.Text = start;
        _end.Box.Text = end;
        _start.Box.AccessibleName = "Início";
        _end.Box.AccessibleName = "Fim";
        foreach (var field in new[] { _start, _end })
        {
            var f = field;
            f.Box.MaxLength = 5;
            f.Box.Leave += (_, _) =>
            {
                var normalized = ScheduleEngine.Normalize(f.Box.Text);
                f.Invalid = normalized is null;
                if (normalized is not null && normalized != f.Box.Text) f.Box.Text = normalized;
                Changed?.Invoke(this, EventArgs.Empty);
            };
            f.Box.TextChanged += (_, _) => f.Invalid = false;
            Controls.Add(f);
        }

        _remove.AccessibleName = "Remover período";
        _remove.Padding = new Padding(10, 0, 10, 0);
        _remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_dash);
        Controls.Add(_remove);
    }

    public void FocusStart() => _start.Box.Focus();

    public bool TryRead(out List<DayOfWeek> days, out string start, out string end, out string? error)
    {
        days = _chips.Where(c => c.Chip.Selected).Select(c => c.Day).ToList();
        start = ScheduleEngine.Normalize(_start.Box.Text) ?? "";
        end = ScheduleEngine.Normalize(_end.Box.Text) ?? "";
        _start.Invalid = start.Length == 0;
        _end.Invalid = end.Length == 0;
        error = null;
        if (days.Count == 0) error = "Selecione ao menos um dia em cada período.";
        else if (start.Length == 0 || end.Length == 0) error = "Use horários no formato HH:mm, por exemplo 09:00.";
        return error is null;
    }

    // Layout manual numa única linha: os chips de dia encolhem para caber; horários e botão ficam à direita.
    private int ChipWidth(int width)
    {
        int gap = LogicalToDeviceUnits(4);
        int right = _start.Width + _end.Width + _remove.Width + TextRenderer.MeasureText("até", Theme.Small).Width
                    + LogicalToDeviceUnits(8) * 4;
        int available = width - right - LogicalToDeviceUnits(12);
        return Math.Clamp((available - gap * 6) / 7, LogicalToDeviceUnits(30), LogicalToDeviceUnits(48));
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        new(Math.Max(proposedSize.Width, 0), LogicalToDeviceUnits(32));

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int chipW = ChipWidth(Width);
        int gap = LogicalToDeviceUnits(4);
        int h = LogicalToDeviceUnits(32);
        int x = 0;
        foreach (var (_, chip) in _chips)
        {
            chip.SetBounds(x, 0, chipW, h);
            x += chipW + gap;
        }
        int y = 0;
        int spacing = LogicalToDeviceUnits(8);
        int right = Width;
        _remove.SetBounds(right - _remove.Width, y, _remove.Width, h);
        right -= _remove.Width + spacing;
        _end.SetBounds(right - _end.Width, y, _end.Width, h);
        right -= _end.Width + spacing;
        int dashW = TextRenderer.MeasureText("até", Theme.Small).Width;
        _dash.SetBounds(right - dashW, y + (h - _dash.PreferredHeight) / 2, dashW, _dash.PreferredHeight);
        right -= dashW + spacing;
        _start.SetBounds(right - _start.Width, y, _start.Width, h);
    }
}
