namespace Guarana.Core;

public sealed record ActivationState(bool Active, string Reason, DateTime? NextChange);

public sealed class ActivationEngine
{
    private AppSettings _settings;
    private DateTime? _temporaryUntil;

    public ActivationEngine(AppSettings settings) => _settings = settings.Copy();

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings.Copy();
    }

    public void ActivateFor(TimeSpan duration, DateTime now)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (!_settings.ScheduleEnabled) _settings.ManualEnabled = false;
        _temporaryUntil = now.Add(duration);
        _settings.SchedulePaused = false;
    }

    public void Pause()
    {
        _temporaryUntil = null;
        if (_settings.ScheduleEnabled) _settings.SchedulePaused = true;
        else _settings.ManualEnabled = false;
    }

    public void Resume()
    {
        _temporaryUntil = null;
        if (_settings.ScheduleEnabled) _settings.SchedulePaused = false;
        else _settings.ManualEnabled = true;
    }

    public AppSettings CurrentSettings => _settings.Copy();

    public ActivationState GetState(DateTime now)
    {
        if (_temporaryUntil is DateTime until)
        {
            if (now < until) return new(true, "Ativado temporariamente", until);
            _temporaryUntil = null;
        }

        if (!_settings.ScheduleEnabled)
            return _settings.ManualEnabled
                ? new(true, "Modo contínuo", null)
                : new(false, "Pausado", null);

        var next = ScheduleEngine.NextTransition(now, _settings.Rules);
        if (_settings.SchedulePaused) return new(false, "Pausado manualmente", null);
        return ScheduleEngine.IsActive(now, _settings.Rules)
            ? new(true, "Dentro do horário programado", next)
            : new(false, "Fora do horário programado", next);
    }
}
