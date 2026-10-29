namespace Guarana.Core;

/// <param name="Active">Se o Guaraná deve manter a sessão ativa agora.</param>
/// <param name="Reason">Texto curto para a interface.</param>
/// <param name="NextChange">Próxima mudança de estado conhecida.</param>
/// <param name="Paused">Se o usuário pausou (determina Pausar/Retomar no menu).</param>
/// <param name="Temporary">Se a ativação atual é temporária.</param>
public sealed record ActivationState(bool Active, string Reason, DateTime? NextChange, bool Paused = false, bool Temporary = false);

public sealed class ActivationEngine
{
    private AppSettings _settings;

    public ActivationEngine(AppSettings settings) => _settings = settings.Copy();

    /// <summary>
    /// Aplica preferências editadas na janela, preservando o estado de execução
    /// (pausa e ativação temporária), que pertence ao motor e não ao formulário.
    /// </summary>
    public void UpdateSettings(AppSettings settings)
    {
        var updated = settings.Copy();
        updated.TemporaryUntil = _settings.TemporaryUntil;
        updated.SchedulePaused = _settings.SchedulePaused;
        updated.ManualEnabled = _settings.ManualEnabled;
        _settings = updated;
    }

    public void ActivateFor(TimeSpan duration, DateTime now)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        ActivateUntil(now.Add(duration), now);
    }

    public void ActivateUntil(DateTime until, DateTime now)
    {
        if (until <= now) throw new ArgumentOutOfRangeException(nameof(until));
        // Ao fim da sessão temporária, o modo manual fica pausado; o programado volta a valer.
        if (!_settings.ScheduleEnabled) _settings.ManualEnabled = false;
        _settings.TemporaryUntil = until;
        _settings.SchedulePaused = false;
    }

    public void Pause()
    {
        _settings.TemporaryUntil = null;
        if (_settings.ScheduleEnabled) _settings.SchedulePaused = true;
        else _settings.ManualEnabled = false;
    }

    public void Resume()
    {
        _settings.TemporaryUntil = null;
        if (_settings.ScheduleEnabled) _settings.SchedulePaused = false;
        else _settings.ManualEnabled = true;
    }

    /// <summary>Remove uma ativação temporária vencida. Retorna true se havia uma.</summary>
    public bool ExpireTemporary(DateTime now)
    {
        if (_settings.TemporaryUntil is not DateTime until || now < until) return false;
        _settings.TemporaryUntil = null;
        return true;
    }

    public AppSettings CurrentSettings => _settings.Copy();

    public ActivationState GetState(DateTime now)
    {
        if (_settings.TemporaryUntil is DateTime until && now < until)
            return new(true, $"Ativado até {until:HH:mm}", until, Temporary: true);

        if (!_settings.ScheduleEnabled)
            return _settings.ManualEnabled
                ? new(true, "Modo contínuo", null)
                : new(false, "Pausado", null, Paused: true);

        if (_settings.SchedulePaused) return new(false, "Programação pausada", null, Paused: true);

        var next = ScheduleEngine.NextTransition(now, _settings.Rules);
        return ScheduleEngine.IsActive(now, _settings.Rules)
            ? new(true, "Dentro do horário programado", next)
            : new(false, "Fora do horário programado", next);
    }
}
