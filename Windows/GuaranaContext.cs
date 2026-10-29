using Guarana.Core;
using Guarana.Windows.Ui;
using Microsoft.Win32;

namespace Guarana.Windows;

internal sealed class GuaranaContext : ApplicationContext
{
    private static readonly (string Label, int Minutes)[] TrayDurations =
        [("15 minutos", 15), ("30 minutos", 30), ("1 hora", 60), ("2 horas", 120), ("4 horas", 240), ("8 horas", 480)];

    private readonly ActivationEngine _engine;
    private readonly NotifyIcon _tray;
    private readonly Icon _activeIcon;
    private readonly Icon _pausedIcon;
    private readonly Icon _appIcon;
    private readonly Image _awakeImage;
    private readonly Image _asleepImage;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _endTemporaryItem;
    private readonly SynchronizationContext _ui;
    private SettingsForm? _form;
    private AppSettings _settings;
    private ActivationState? _lastState;
    private DateTime _nextPulseUtc;
    private bool _wasActive;
    private bool _locked;
    private bool _exiting;
    private string? _lastError;

    public GuaranaContext(EventWaitHandle showSignal)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        bool firstRun = !File.Exists(SettingsStore.FilePath);
        _settings = SettingsStore.Load();
        _engine = new ActivationEngine(_settings);
        _activeIcon = LoadIcon("guarana.ico");
        _pausedIcon = LoadIcon("guarana-paused.ico");
        using (var full = Resource("guarana.ico")) _appIcon = new Icon(full);
        _awakeImage = LoadImage("guarana.png");
        _asleepImage = LoadImage("guarana-paused.png");

        _statusItem = new ToolStripMenuItem { Enabled = false, Tag = "status" };
        _pauseItem = new ToolStripMenuItem("Pausar");
        _pauseItem.Click += (_, _) => TogglePause();
        _endTemporaryItem = new ToolStripMenuItem("Encerrar ativação temporária");
        _endTemporaryItem.Click += (_, _) => EndTemporary();

        var activateFor = new ToolStripMenuItem("Ativar por");
        foreach (var (label, minutes) in TrayDurations)
            activateFor.DropDownItems.Add(label, null, (_, _) => ActivateFor(minutes));

        var open = new ToolStripMenuItem("Abrir Guaraná") { Font = new Font(Theme.Body, FontStyle.Bold) };
        open.Click += (_, _) => OpenSettings();

        _menu = new ContextMenuStrip
        {
            Renderer = new TrayMenuRenderer(),
            ShowImageMargin = false,
            Padding = new Padding(2, 6, 2, 6),
            Font = Theme.Body
        };
        _menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            new ToolStripSeparator(),
            open,
            _pauseItem,
            activateFor,
            _endTemporaryItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Sair", null, (_, _) => Exit())
        });
        foreach (ToolStripItem item in _menu.Items)
            if (item is ToolStripMenuItem) item.Padding = new Padding(8, 5, 16, 5);
        if (activateFor.DropDown is ToolStripDropDownMenu sub)
        {
            sub.Renderer = _menu.Renderer;
            sub.ShowImageMargin = false;
            sub.Padding = new Padding(2, 6, 2, 6);
            foreach (ToolStripItem item in sub.Items) item.Padding = new Padding(8, 5, 16, 5);
        }

        _tray = new NotifyIcon
        {
            Icon = _activeIcon,
            Text = "Guaraná",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenSettings(); };

        SystemEvents.SessionSwitch += OnSessionSwitch;
        Theme.Changed += OnThemeChanged;
        ThreadPool.RegisterWaitForSingleObject(showSignal,
            (_, _) => _ui.Post(_ => OpenSettings(), null), null, Timeout.Infinite, executeOnlyOnce: false);

        StartupManager.RefreshInstalledCopy();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
        if (firstRun) OpenSettings();
    }

    // ---- Estado exposto à janela -------------------------------------------------------

    public AppSettings Settings => _settings.Copy();
    public ActivationState State => _engine.GetState(DateTime.Now);
    public bool StartupEnabled => StartupManager.IsEnabled();
    public bool IsExiting => _exiting;
    public Icon AppIcon => _appIcon;

    // ---- Ciclo principal ------------------------------------------------------------------

    private void Tick()
    {
        var now = DateTime.Now;
        if (_engine.ExpireTemporary(now))
        {
            Persist();
            _tray.ShowBalloonTip(4000, "Guaraná", "A ativação temporária terminou.", ToolTipIcon.None);
        }

        var state = _engine.GetState(now);
        PowerRequest.Set(state.Active && _settings.PreventSleep);
        Pulse(state.Active);
        UpdateUi(state, now);
    }

    private void Pulse(bool active)
    {
        var utcNow = DateTime.UtcNow;
        if (active && !_wasActive) _nextPulseUtc = utcNow;
        _wasActive = active;
        if (!active || _locked || utcNow < _nextPulseUtc) return;

        var interval = TimeSpan.FromSeconds(_settings.IntervalSeconds);
        if (_settings.OnlyWhenIdle)
        {
            var idle = KeyboardPulse.IdleTime();
            if (idle < interval)
            {
                // O próprio usuário já está mantendo a sessão ativa; tenta de novo quando o intervalo vencer.
                _nextPulseUtc = utcNow + (interval - idle);
                return;
            }
        }

        switch (KeyboardPulse.TrySend(out string? error))
        {
            case PulseResult.Sent:
                _lastError = null;
                _nextPulseUtc = utcNow + interval;
                break;
            case PulseResult.ModifierHeld:
                _nextPulseUtc = utcNow.AddSeconds(2);
                break;
            default:
                _lastError = error;
                _nextPulseUtc = utcNow + interval;
                break;
        }
    }

    private void UpdateUi(ActivationState state, DateTime now)
    {
        if (state != _lastState)
        {
            _lastState = state;
            _tray.Icon = state.Active ? _activeIcon : _pausedIcon;
            string tip = "Guaraná · " + (state.Active ? state.Reason : state.Paused ? "Pausado" : state.Reason);
            _tray.Text = tip.Length > 63 ? tip[..63] : tip;
            _statusItem.Text = (state.Active ? "●  Ativo · " : "○  ") + state.Reason;
            _pauseItem.Text = state.Paused ? "Retomar" : state.ScheduleOnly() ? "Pausar programação" : "Pausar";
            _endTemporaryItem.Visible = state.Temporary;
        }
        if (_form is { Visible: true }) _form.SetState(state, now, _lastError);
    }

    // ---- Comandos ----------------------------------------------------------------------------

    private void TogglePause()
    {
        if (_engine.GetState(DateTime.Now).Paused) Resume();
        else Pause();
    }

    public void Pause() { _engine.Pause(); Persist(); }

    public void Resume() { _engine.Resume(); Persist(); }

    public void EndTemporary()
    {
        // Encerrar volta ao modo normal: no programado, a agenda; no contínuo, pausado.
        _engine.ExpireTemporary(DateTime.MaxValue);
        Persist();
    }

    public void ActivateFor(int minutes)
    {
        _engine.ActivateFor(TimeSpan.FromMinutes(minutes), DateTime.Now);
        Persist();
    }

    public void ApplyPreferences(AppSettings settings)
    {
        _engine.UpdateSettings(settings);
        _settings = _engine.CurrentSettings;
        SettingsStore.Save(_settings);
        _lastState = null;
        Tick();
    }

    public void SetStartup(bool enabled)
    {
        if (enabled) StartupManager.Enable();
        else StartupManager.Disable();
    }

    /// <summary>Na primeira vez que a janela é fechada, avisa que o app continua na bandeja.</summary>
    public void WindowHidden()
    {
        if (_settings.TrayHintShown) return;
        var settings = _engine.CurrentSettings;
        settings.TrayHintShown = true;
        _engine.UpdateSettings(settings);
        Persist();
        _tray.ShowBalloonTip(5000, "O Guaraná continua aqui",
            "Suas configurações foram salvas. Clique no ícone da bandeja para abrir de novo.", ToolTipIcon.None);
    }

    private void Persist()
    {
        _settings = _engine.CurrentSettings;
        try { SettingsStore.Save(_settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _lastError = ex.Message; }
        _lastState = null;
        Tick();
    }

    private void OpenSettings()
    {
        if (_exiting) return;
        if (_form is null || _form.IsDisposed)
            _form = new SettingsForm(this, _awakeImage, _asleepImage);
        _lastState = null;
        Tick();
        _form.Show();
        if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
        _form.BringToFront();
        _form.Activate();
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
            _locked = true;
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
        {
            _locked = false;
            _nextPulseUtc = DateTime.UtcNow;
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => _ui.Post(_ => _menu.Invalidate(), null);

    private void Exit()
    {
        _exiting = true;
        _timer.Stop();
        PowerRequest.Set(false);
        _tray.Visible = false;
        _form?.Close();
        ExitThread();
    }

    // ---- Recursos ------------------------------------------------------------------------------

    private static Stream Resource(string name) =>
        typeof(GuaranaContext).Assembly.GetManifestResourceStream("Guarana.Assets." + name)
        ?? throw new InvalidOperationException("Recurso não encontrado: " + name);

    private static Icon LoadIcon(string name)
    {
        using var stream = Resource(name);
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    private static Image LoadImage(string name)
    {
        using var stream = Resource(name);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            Theme.Changed -= OnThemeChanged;
            _timer.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _form?.Dispose();
            _activeIcon.Dispose();
            _pausedIcon.Dispose();
            _appIcon.Dispose();
            _awakeImage.Dispose();
            _asleepImage.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal static class ActivationStateExtensions
{
    /// <summary>Estado inativo por estar fora do horário (não pausado manualmente).</summary>
    public static bool ScheduleOnly(this ActivationState state) => !state.Active && !state.Paused;
}
