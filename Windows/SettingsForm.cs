using System.Globalization;
using Guarana.Core;

namespace Guarana.Windows;

internal sealed class SettingsForm : Form
{
    private static readonly string[] Days =
        ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"];

    private readonly GuaranaContext _context;
    private readonly Label _status = new();
    private readonly Label _next = new();
    private readonly Label _error = new();
    private readonly CheckBox _schedule = new();
    private readonly CheckBox _startup = new();
    private readonly NumericUpDown _interval = new();
    private readonly NumericUpDown _duration = new();
    private readonly DataGridView _grid = new();
    private readonly Button _add = new();
    private readonly Button _delete = new();

    public SettingsForm(GuaranaContext context, AppSettings settings, bool startup)
    {
        _context = context;
        Text = "Guaraná · Configurações";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(650, 610);
        Size = new Size(700, 670);
        BackColor = Color.FromArgb(250, 248, 244);
        Font = new Font("Segoe UI", 10);

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 5
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        Controls.Add(main);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        header.Controls.Add(new Label
        {
            Text = "Guaraná",
            Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(120, 36, 31),
            AutoSize = true
        }, 0, 0);
        _status.AutoSize = true;
        _status.Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold);
        header.Controls.Add(_status, 0, 1);
        _next.AutoSize = true;
        _next.ForeColor = Color.DimGray;
        header.Controls.Add(_next, 0, 2);
        main.Controls.Add(header, 0, 0);

        var general = new GroupBox { Text = "Funcionamento", Dock = DockStyle.Fill };
        var generalFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 8, 5, 4),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        _schedule.Text = "Usar horários programados (fora deles, não enviar F15)";
        _schedule.AutoSize = true;
        _schedule.Checked = settings.ScheduleEnabled;
        _schedule.CheckedChanged += (_, _) => UpdateScheduleEnabled();
        generalFlow.Controls.Add(_schedule);

        var intervalFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        intervalFlow.Controls.Add(new Label { Text = "Enviar F15 a cada", AutoSize = true, Margin = new Padding(0, 6, 5, 0) });
        _interval.Minimum = 10;
        _interval.Maximum = 3600;
        _interval.Value = Math.Clamp(settings.IntervalSeconds, 10, 3600);
        _interval.Width = 75;
        intervalFlow.Controls.Add(_interval);
        intervalFlow.Controls.Add(new Label { Text = "segundos", AutoSize = true, Margin = new Padding(5, 6, 0, 0) });
        generalFlow.Controls.Add(intervalFlow);

        _startup.Text = "Iniciar com o Windows (somente este usuário)";
        _startup.AutoSize = true;
        _startup.Checked = startup;
        generalFlow.Controls.Add(_startup);
        generalFlow.Controls.Add(new Label
        {
            Text = "Ao habilitar, uma cópia do EXE é guardada em AppData para manter o caminho fixo.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.5f)
        });
        general.Controls.Add(generalFlow);
        main.Controls.Add(general, 0, 1);

        var scheduleGroup = new GroupBox { Text = "Horários · início incluído, fim excluído", Dock = DockStyle.Fill };
        var scheduleLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 3, ColumnCount = 1 };
        scheduleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scheduleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        scheduleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        var dayColumn = new DataGridViewComboBoxColumn { Name = "Dia", HeaderText = "Dia", FillWeight = 48 };
        dayColumn.Items.AddRange(Days);
        _grid.Columns.Add(dayColumn);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Inicio", HeaderText = "Início (HH:mm)", FillWeight = 26 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fim", HeaderText = "Fim (HH:mm)", FillWeight = 26 });
        foreach (var rule in settings.Rules)
            _grid.Rows.Add(Days[(int)rule.Day], rule.Start, rule.End);
        scheduleLayout.Controls.Add(_grid, 0, 0);

        var gridButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _add.Text = "Adicionar período";
        _add.AutoSize = true;
        _add.Click += (_, _) => _grid.Rows.Add("Segunda", "09:00", "18:00");
        _delete.Text = "Excluir selecionado";
        _delete.AutoSize = true;
        _delete.Click += (_, _) =>
        {
            if (_grid.CurrentRow is not null) _grid.Rows.Remove(_grid.CurrentRow);
        };
        gridButtons.Controls.Add(_add);
        gridButtons.Controls.Add(_delete);
        scheduleLayout.Controls.Add(gridButtons, 0, 1);
        scheduleLayout.Controls.Add(new Label
        {
            Text = "Se o horário de fim for igual ou anterior ao início, o período termina no dia seguinte.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.5f)
        }, 0, 2);
        scheduleGroup.Controls.Add(scheduleLayout);
        main.Controls.Add(scheduleGroup, 0, 2);

        var session = new GroupBox { Text = "Controles rápidos", Dock = DockStyle.Fill };
        var sessionFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 12, 5, 0), WrapContents = false };
        sessionFlow.Controls.Add(new Label { Text = "Ativar por", AutoSize = true, Margin = new Padding(0, 7, 5, 0) });
        _duration.Minimum = 1;
        _duration.Maximum = 1440;
        _duration.Value = 30;
        _duration.Width = 70;
        sessionFlow.Controls.Add(_duration);
        sessionFlow.Controls.Add(new Label { Text = "minutos", AutoSize = true, Margin = new Padding(5, 7, 8, 0) });
        var activate = new Button { Text = "Ativar", AutoSize = true };
        activate.Click += (_, _) => _context.ActivateFor((int)_duration.Value);
        sessionFlow.Controls.Add(activate);
        var pause = new Button { Text = "Pausar", AutoSize = true };
        pause.Click += (_, _) => _context.Pause();
        sessionFlow.Controls.Add(pause);
        var resume = new Button { Text = "Retomar", AutoSize = true };
        resume.Click += (_, _) => _context.Resume();
        sessionFlow.Controls.Add(resume);
        session.Controls.Add(sessionFlow);
        main.Controls.Add(session, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _error.AutoSize = true;
        _error.ForeColor = Color.Firebrick;
        footer.Controls.Add(_error, 0, 0);
        var save = new Button
        {
            Text = "Salvar configurações",
            AutoSize = true,
            BackColor = Color.FromArgb(150, 48, 41),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(6, 3, 6, 3)
        };
        save.Click += (_, _) => Save();
        footer.Controls.Add(save, 1, 0);
        main.Controls.Add(footer, 0, 4);

        UpdateScheduleEnabled();
        FormClosing += (_, e) =>
        {
            if (_context.IsExiting) return;
            e.Cancel = true;
            Hide();
        };
    }

    private void UpdateScheduleEnabled()
    {
        _grid.Enabled = _schedule.Checked;
        _add.Enabled = _schedule.Checked;
        _delete.Enabled = _schedule.Checked;
    }

    private void Save()
    {
        try
        {
            _grid.EndEdit();
            var settings = new AppSettings
            {
                IntervalSeconds = (int)_interval.Value,
                ScheduleEnabled = _schedule.Checked,
                SchedulePaused = _context.SchedulePaused,
                ManualEnabled = _context.ManualEnabled,
                Rules = []
            };
            foreach (DataGridViewRow row in _grid.Rows)
            {
                string dayText = Convert.ToString(row.Cells[0].Value, CultureInfo.InvariantCulture) ?? "";
                int dayIndex = Array.IndexOf(Days, dayText);
                if (dayIndex < 0) throw new ArgumentException("Selecione um dia válido em cada linha.");
                var rule = new ScheduleRule((DayOfWeek)dayIndex,
                    Convert.ToString(row.Cells[1].Value, CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(row.Cells[2].Value, CultureInfo.InvariantCulture) ?? "");
                ScheduleEngine.Validate(rule);
                settings.Rules.Add(rule);
            }
            _context.SaveSettings(settings, _startup.Checked);
            _error.ForeColor = Color.DarkGreen;
            _error.Text = "Configurações salvas.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _error.ForeColor = Color.Firebrick;
            _error.Text = ex.Message;
        }
    }

    public void SetStatus(ActivationState state, string nextText, string? error)
    {
        _status.Text = (state.Active ? "● Ativo" : "● Inativo") + " · " + state.Reason;
        _status.ForeColor = state.Active ? Color.FromArgb(89, 119, 53) : Color.FromArgb(130, 80, 80);
        _next.Text = nextText.Length > 0 ? nextText.TrimStart(' ', '·') : "Sem mudança programada";
        if (error is not null)
        {
            _error.ForeColor = Color.Firebrick;
            _error.Text = "Falha ao enviar F15: " + error;
        }
    }
}
