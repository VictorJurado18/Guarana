using Guarana.Core;

namespace Guarana.Windows;

internal sealed class GuaranaContext : ApplicationContext
{
    private readonly ActivationEngine _engine;
    private readonly NotifyIcon _tray;
    private readonly Icon _activeIcon;
    private readonly Icon _pausedIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseItem;
    private SettingsForm? _form;
    private DateTimeOffset _nextPulse;
    private bool _wasActive;
    private bool _exiting;
    private string? _lastError;

    public GuaranaContext()
    {
        bool firstRun = !File.Exists(SettingsStore.FilePath);
        _engine = new ActivationEngine(SettingsStore.Load());
        _activeIcon = LoadIcon("guarana.ico");
        _pausedIcon = LoadIcon("guarana-paused.ico");
        _statusItem = new ToolStripMenuItem { Enabled = false };
        _pauseItem = new ToolStripMenuItem();
        _pauseItem.Click += (_, _) => TogglePause();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add("Ativar por 30 minutos", null, (_, _) => ActivateFor(30));
        menu.Items.Add("Configurações...", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Exit());

        _tray = new NotifyIcon
        {
            Icon = _activeIcon,
            Text = "Guaraná",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => OpenSettings();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
        if (firstRun) OpenSettings();
    }

    private static Icon LoadIcon(string name)
    {
        using var stream = typeof(GuaranaContext).Assembly.GetManifestResourceStream("Guarana.Assets." + name)
            ?? throw new InvalidOperationException("Ícone não encontrado: " + name);
        return new Icon(stream);
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var state = _engine.GetState(now);
        if (state.Active && !_wasActive) _nextPulse = DateTimeOffset.Now;
        _wasActive = state.Active;

        if (state.Active && DateTimeOffset.Now >= _nextPulse)
        {
            _lastError = KeyboardPulse.TrySend(out string? error) ? null : error;
            _nextPulse = DateTimeOffset.Now.AddSeconds(_engine.CurrentSettings.IntervalSeconds);
        }

        string next = state.NextChange is DateTime change
            ? $" · Próxima mudança: {change:ddd dd/MM HH:mm}"
            : "";
        string label = (state.Active ? "Ativo" : "Pausado") + " · " + state.Reason;
        _tray.Icon = state.Active ? _activeIcon : _pausedIcon;
        _tray.Text = (label.Length > 63 ? label[..63] : label);
        _statusItem.Text = label;
        _pauseItem.Text = state.Active ? "Pausar" : "Retomar";
        _pauseItem.Enabled = state.Active || state.Reason.Contains("Pausado", StringComparison.Ordinal);
        _form?.SetStatus(state, next, _lastError);
    }

    private void TogglePause()
    {
        if (_engine.GetState(DateTime.Now).Active) _engine.Pause();
        else _engine.Resume();
        SaveRuntimeState();
    }

    public void Pause()
    {
        _engine.Pause();
        SaveRuntimeState();
    }

    public void Resume()
    {
        _engine.Resume();
        SaveRuntimeState();
    }

    public void ActivateFor(int minutes)
    {
        _engine.ActivateFor(TimeSpan.FromMinutes(minutes), DateTime.Now);
        SaveRuntimeState();
    }

    private void SaveRuntimeState()
    {
        SettingsStore.Save(_engine.CurrentSettings);
        Tick();
    }

    public void SaveSettings(AppSettings settings, bool startup)
    {
        if (startup) StartupManager.Enable();
        else StartupManager.Disable();
        SettingsStore.Save(settings);
        _engine.UpdateSettings(settings);
        Tick();
    }

    private void OpenSettings()
    {
        if (_form is null || _form.IsDisposed)
            _form = new SettingsForm(this, _engine.CurrentSettings, StartupManager.IsEnabled());
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.BringToFront();
        _form.Activate();
        Tick();
    }

    private void Exit()
    {
        _exiting = true;
        _timer.Stop();
        _tray.Visible = false;
        _form?.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Dispose();
            _activeIcon.Dispose();
            _pausedIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    public bool IsExiting => _exiting;
    public bool ManualEnabled => _engine.CurrentSettings.ManualEnabled;
    public bool SchedulePaused => _engine.CurrentSettings.SchedulePaused;
}
