using System.Diagnostics;
using Guarana.Core;
using Guarana.Windows.Ui;

namespace Guarana.Windows;

/// <summary>Janela principal. Todas as alterações são salvas automaticamente.</summary>
internal sealed class SettingsForm : Form
{
    private static readonly (string Label, int Minutes)[] QuickDurations =
        [("15 min", 15), ("30 min", 30), ("1 h", 60), ("2 h", 120), ("4 h", 240), ("8 h", 480)];

    private readonly GuaranaContext _context;
    private readonly StackPanel _stack = new() { Padding = new Padding(24, 16, 24, 20) };
    private readonly StatusCard _status;
    private readonly ToggleSwitch _schedule = new();
    private readonly ScheduleEditor _scheduleEditor = new();
    private readonly ToggleSwitch _onlyWhenIdle = new();
    private readonly ToggleSwitch _preventSleep = new();
    private readonly ToggleSwitch _startup = new();
    private readonly TextField _interval = new(84) { Suffix = "seg" };
    private readonly ThemedLabel _footerText = new("", Theme.Small, muted: true);
    private readonly Panel _footer = new() { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(24, 0, 24, 0) };
    private readonly System.Windows.Forms.Timer _saveDelay = new() { Interval = 450 };
    private string? _pulseError;
    private bool _loading;

    public SettingsForm(GuaranaContext context, Image awake, Image asleep)
    {
        _context = context;
        _status = new StatusCard(awake, asleep);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Guaraná";
        Icon = context.AppIcon;
        Font = Theme.Body;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(660, 760);
        MinimumSize = new Size(560, 520);
        MaximizeBox = false;
        KeyPreview = true;

        _stack.Dock = DockStyle.Fill;
        _stack.Controls.Add(BuildHeader(awake));
        _stack.Controls.Add(_status);
        _stack.Controls.Add(BuildQuickCard());
        _stack.Controls.Add(BuildScheduleCard());
        _stack.Controls.Add(BuildPreferencesCard());

        _footerText.Anchor = AnchorStyles.Left;
        _footerText.Dock = DockStyle.Fill;
        _footerText.AutoSize = false;
        _footerText.TextAlign = ContentAlignment.MiddleLeft;
        _footer.Controls.Add(_footerText);

        Controls.Add(_stack);
        Controls.Add(_footer);
        ResumeLayout(true);

        _status.Action.Click += (_, _) =>
        {
            var state = _context.State;
            if (state.Temporary) _context.EndTemporary();
            else if (state.Paused) _context.Resume();
            else _context.Pause();
        };
        _saveDelay.Tick += (_, _) => { _saveDelay.Stop(); SavePreferences(); };

        LoadFrom(_context.Settings, _context.StartupEnabled);
        ApplyTheme();
        Theme.Changed += OnThemeChanged;

        FormClosing += (_, e) =>
        {
            if (_context.IsExiting) return;
            if (_saveDelay.Enabled) { _saveDelay.Stop(); SavePreferences(); }
            e.Cancel = true;
            Hide();
        };
    }

    // ---- Construção ------------------------------------------------------------------

    private Control BuildHeader(Image logo)
    {
        var header = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Height = 64
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var picture = new PictureBox
        {
            Image = logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(44, 44),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 12, 0),
            Anchor = AnchorStyles.Left
        };
        var titles = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left
        };
        titles.Controls.Add(new ThemedLabel("Guaraná", Theme.Title), 0, 0);
        titles.Controls.Add(new ThemedLabel("Seu Windows acordado, só quando você quer.", Theme.Small, muted: true), 0, 1);

        var about = new PillButton("Sobre", PillKind.Ghost) { Anchor = AnchorStyles.Right, Margin = Padding.Empty, Radius = 8 };
        about.Click += (_, _) => ShowAbout();

        header.Controls.Add(picture, 0, 0);
        header.Controls.Add(titles, 1, 0);
        header.Controls.Add(about, 2, 0);
        return new FixedHeight(header, 64);
    }

    private Card BuildQuickCard()
    {
        var card = new Card();
        card.AddRow(new ThemedLabel("Ativação rápida", Theme.SectionTitle));
        card.AddRow(new ThemedLabel("Mantém o PC acordado por um tempo e depois volta ao modo normal.",
            Theme.Small, muted: true), 2);
        var chips = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        foreach (var (label, minutes) in QuickDurations)
        {
            var chip = new PillButton(label, PillKind.Chip) { Margin = new Padding(0, 0, 8, 8) };
            chip.Click += (_, _) => _context.ActivateFor(minutes);
            chips.Controls.Add(chip);
        }
        card.AddRow(chips, 14);
        return card;
    }

    private Card BuildScheduleCard()
    {
        var card = new Card();
        card.AddRow(new SettingRow("Horários programados",
            "Fora dos períodos, o Guaraná fica em repouso e não envia nenhum sinal.", _schedule));
        AddDivider(card);
        card.AddRow(_scheduleEditor);
        card.AddRow(new ThemedLabel(
            "Início incluído, fim excluído. Se o fim for igual ou anterior ao início, o período termina no dia seguinte.",
            Theme.Small, muted: true), 6);

        _schedule.CheckedChanged += (_, _) =>
        {
            _scheduleEditor.Enabled = _schedule.Checked;
            QueueSave();
        };
        _scheduleEditor.Changed += (_, _) => QueueSave();
        return card;
    }

    private Card BuildPreferencesCard()
    {
        var card = new Card();
        card.AddRow(new ThemedLabel("Preferências", Theme.SectionTitle));
        card.AddRow(new SettingRow("Só quando o computador estiver ocioso",
            "Não envia nada enquanto você digita ou mexe o mouse.", _onlyWhenIdle), 14);
        AddDivider(card);
        card.AddRow(new SettingRow("Impedir suspensão e tela apagada",
            "Usa o recurso do próprio Windows enquanto o Guaraná estiver ativo.", _preventSleep));
        AddDivider(card);
        card.AddRow(new SettingRow("Iniciar com o Windows",
            "Somente para este usuário. Não requer administrador.", _startup));
        AddDivider(card);
        card.AddRow(new SettingRow("Intervalo entre sinais",
            "Tempo entre os pulsos da tecla F15 (10 a 3600 segundos).", _interval));

        _onlyWhenIdle.CheckedChanged += (_, _) => QueueSave();
        _preventSleep.CheckedChanged += (_, _) => QueueSave();
        _startup.CheckedChanged += (_, _) => ApplyStartup();
        _interval.Box.MaxLength = 4;
        _interval.Box.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
        _interval.Box.TextChanged += (_, _) => { _interval.Invalid = false; QueueSave(); };
        _interval.Box.Leave += (_, _) => _interval.Invalid = ReadInterval() is null;
        return card;
    }

    private static void AddDivider(Card card)
    {
        var divider = new Divider();
        card.AddRow(divider);
        divider.Margin = new Padding(0, 14, 0, 14);
    }

    // ---- Dados ----------------------------------------------------------------------

    private void LoadFrom(AppSettings settings, bool startup)
    {
        _loading = true;
        _schedule.SetSilently(settings.ScheduleEnabled);
        _scheduleEditor.Load(settings.Rules);
        _scheduleEditor.Enabled = settings.ScheduleEnabled;
        _onlyWhenIdle.SetSilently(settings.OnlyWhenIdle);
        _preventSleep.SetSilently(settings.PreventSleep);
        _startup.SetSilently(startup);
        _interval.Box.Text = settings.IntervalSeconds.ToString();
        _loading = false;
        ShowFooter("As alterações são salvas automaticamente.", error: false);
    }

    private int? ReadInterval() =>
        int.TryParse(_interval.Box.Text, out int value) && value is >= 10 and <= 3600 ? value : null;

    private void QueueSave()
    {
        if (_loading) return;
        _saveDelay.Stop();
        _saveDelay.Start();
    }

    private void SavePreferences()
    {
        var rules = _scheduleEditor.TryGetRules(out string? error);
        int? interval = ReadInterval();
        if (rules is null)
        {
            ShowFooter(error ?? "Revise os horários.", error: true);
            return;
        }
        if (interval is null)
        {
            if (!_interval.Box.Focused) _interval.Invalid = true;
            ShowFooter("O intervalo deve ficar entre 10 e 3600 segundos.", error: true);
            return;
        }
        if (_schedule.Checked && rules.Count == 0)
        {
            ShowFooter("Adicione ao menos um período ou desative os horários programados.", error: true);
            return;
        }

        var settings = _context.Settings;
        settings.ScheduleEnabled = _schedule.Checked;
        settings.Rules = rules;
        settings.OnlyWhenIdle = _onlyWhenIdle.Checked;
        settings.PreventSleep = _preventSleep.Checked;
        settings.IntervalSeconds = interval.Value;
        try
        {
            _context.ApplyPreferences(settings);
            ShowFooter($"✓  Salvo às {DateTime.Now:HH:mm:ss}", error: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowFooter("Não foi possível salvar: " + ex.Message, error: true);
        }
    }

    private void ApplyStartup()
    {
        if (_loading) return;
        try
        {
            _context.SetStartup(_startup.Checked);
            ShowFooter(_startup.Checked ? "✓  Vai iniciar com o Windows." : "✓  Não vai mais iniciar com o Windows.", error: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or System.Security.SecurityException)
        {
            _startup.SetSilently(!_startup.Checked);
            ShowFooter("Não foi possível alterar a inicialização: " + ex.Message, error: true);
        }
    }

    private void ShowFooter(string text, bool error)
    {
        _footerText.Text = text;
        _footerText.ForeColor = error ? Theme.Current.Danger : Theme.Current.TextMuted;
        _footerText.Tag = error ? "error" : null;
    }

    /// <summary>Chamado pelo contexto a cada mudança de estado.</summary>
    public void SetState(ActivationState state, DateTime now, string? pulseError)
    {
        _status.SetState(state, now);
        if (pulseError != _pulseError)
        {
            _pulseError = pulseError;
            if (pulseError is not null) ShowFooter("Falha ao enviar F15: " + pulseError, error: true);
            else if (_footerText.Text.StartsWith("Falha ao enviar", StringComparison.Ordinal))
                ShowFooter("O envio voltou a funcionar.", error: false);
        }
    }

    // ---- Tema e diálogos --------------------------------------------------------------

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(ApplyTheme);
        else ApplyTheme();
    }

    private void ApplyTheme()
    {
        var p = Theme.Current;
        BackColor = p.Background;
        _stack.BackColor = p.Background;
        _footer.BackColor = p.Background;
        Walk(this, p);
        ShowFooter(_footerText.Text, _footerText.Tag as string == "error");
        Theme.ApplyChrome(this);
        Invalidate(true);
    }

    private static void Walk(Control root, Palette p)
    {
        foreach (Control c in root.Controls)
        {
            if (c is IThemed themed) themed.ApplyTheme(p);
            Walk(c, p);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyChrome(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ShowAbout()
    {
        var github = new TaskDialogCommandLinkButton("Abrir no GitHub", "Código-fonte, novidades e relato de problemas");
        var page = new TaskDialogPage
        {
            Caption = "Sobre o Guaraná",
            Heading = $"Guaraná {Application.ProductVersion.Split('+')[0]}",
            Text = "Mantém o Windows acordado com um pulso discreto da tecla F15, " +
                   "continuamente, por tempo determinado ou em horários programados.\n\n" +
                   "Sem rede, sem telemetria e sem atualização automática.",
            Footnote = new TaskDialogFootnote("Software livre sob a Licença MIT."),
            Icon = new TaskDialogIcon(_context.AppIcon),
            Buttons = { github, TaskDialogButton.Close },
            AllowCancel = true
        };
        if (TaskDialog.ShowDialog(this, page) == github)
            Process.Start(new ProcessStartInfo("https://github.com/VictorJurado18/Guarana") { UseShellExecute = true });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Theme.Changed -= OnThemeChanged;
            _saveDelay.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Envolve um controle com altura fixa (em unidades lógicas) para o <see cref="StackPanel"/>.</summary>
    private sealed class FixedHeight : Panel
    {
        private readonly int _height;

        public FixedHeight(Control child, int height)
        {
            _height = height;
            BackColor = Color.Transparent;
            child.Dock = DockStyle.Fill;
            Controls.Add(child);
        }

        public override Size GetPreferredSize(Size proposedSize) =>
            new(proposedSize.Width, LogicalToDeviceUnits(_height));
    }
}
