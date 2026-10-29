using System.Text.Json;
using Guarana.Core;

var weekdays = new[] { new ScheduleRule(DayOfWeek.Monday, "09:00", "18:00") };
var monday10 = new DateTime(2026, 11, 2, 10, 0, 0);

var tests = new (string Name, Action Run)[]
{
    // ---- ScheduleEngine ----
    ("Início incluído e fim excluído", () =>
    {
        Check(!ScheduleEngine.IsActive(new DateTime(2026, 11, 2, 8, 59, 59), weekdays));
        Check(ScheduleEngine.IsActive(new DateTime(2026, 11, 2, 9, 0, 0), weekdays));
        Check(!ScheduleEngine.IsActive(new DateTime(2026, 11, 2, 18, 0, 0), weekdays));
    }),
    ("Período atravessando meia-noite", () =>
    {
        var rules = new[] { new ScheduleRule(DayOfWeek.Monday, "22:00", "02:00") };
        Check(ScheduleEngine.IsActive(new DateTime(2026, 11, 2, 23, 0, 0), rules));
        Check(ScheduleEngine.IsActive(new DateTime(2026, 11, 3, 1, 59, 59), rules));
        Check(!ScheduleEngine.IsActive(new DateTime(2026, 11, 3, 2, 0, 0), rules));
        Check(!ScheduleEngine.IsActive(new DateTime(2026, 11, 4, 1, 0, 0), rules));
    }),
    ("Próxima transição ignora períodos sobrepostos", () =>
    {
        var rules = new[]
        {
            new ScheduleRule(DayOfWeek.Monday, "09:00", "12:00"),
            new ScheduleRule(DayOfWeek.Monday, "11:00", "15:00")
        };
        Check(ScheduleEngine.NextTransition(monday10, rules) == new DateTime(2026, 11, 2, 15, 0, 0));
    }),
    ("Dias da semana e próximo início", () =>
    {
        var rules = new[] { new ScheduleRule(DayOfWeek.Friday, "09:00", "18:00") };
        Check(!ScheduleEngine.IsActive(monday10, rules));
        Check(ScheduleEngine.NextTransition(monday10, rules) == new DateTime(2026, 11, 6, 9, 0, 0));
    }),
    ("Normalização de horários digitados", () =>
    {
        Check(ScheduleEngine.Normalize("9") == "09:00");
        Check(ScheduleEngine.Normalize("9:5") == "09:05");
        Check(ScheduleEngine.Normalize("0930") == "09:30");
        Check(ScheduleEngine.Normalize(" 18:00 ") == "18:00");
        Check(ScheduleEngine.Normalize("24:00") is null);
        Check(ScheduleEngine.Normalize("12:60") is null);
        Check(ScheduleEngine.Normalize("abc") is null);
        Check(ScheduleEngine.Normalize("") is null);
    }),

    // ---- ActivationEngine ----
    ("Ativação temporária termina em modo manual", () =>
    {
        var engine = new ActivationEngine(new AppSettings());
        engine.ActivateFor(TimeSpan.FromMinutes(30), monday10);
        var during = engine.GetState(monday10.AddMinutes(29));
        Check(during.Active && during.Temporary);
        Check(!engine.GetState(monday10.AddMinutes(30)).Active);
    }),
    ("Ativação temporária retorna ao horário programado", () =>
    {
        var engine = new ActivationEngine(new AppSettings { ScheduleEnabled = true, Rules = [.. weekdays] });
        var morning = new DateTime(2026, 11, 2, 8, 0, 0);
        engine.ActivateFor(TimeSpan.FromMinutes(30), morning);
        Check(engine.GetState(morning.AddMinutes(20)).Active);
        Check(!engine.GetState(morning.AddMinutes(30)).Active);
        Check(engine.GetState(morning.AddHours(1)).Active);
    }),
    ("Ativação temporária sobrevive a reinício", () =>
    {
        var engine = new ActivationEngine(new AppSettings());
        engine.ActivateFor(TimeSpan.FromMinutes(30), monday10);
        var json = JsonSerializer.Serialize(engine.CurrentSettings);
        var restarted = new ActivationEngine(JsonSerializer.Deserialize<AppSettings>(json)!);
        Check(restarted.GetState(monday10.AddMinutes(10)).Temporary);
        Check(!restarted.GetState(monday10.AddMinutes(31)).Active);
    }),
    ("Expirar limpa a ativação temporária uma única vez", () =>
    {
        var engine = new ActivationEngine(new AppSettings());
        engine.ActivateFor(TimeSpan.FromMinutes(5), monday10);
        Check(!engine.ExpireTemporary(monday10.AddMinutes(4)));
        Check(engine.ExpireTemporary(monday10.AddMinutes(5)));
        Check(!engine.ExpireTemporary(monday10.AddMinutes(6)));
        Check(engine.CurrentSettings.TemporaryUntil is null);
    }),
    ("Pausa e retomada no modo contínuo", () =>
    {
        var engine = new ActivationEngine(new AppSettings());
        engine.Pause();
        var paused = engine.GetState(monday10);
        Check(!paused.Active && paused.Paused);
        engine.Resume();
        Check(engine.GetState(monday10).Active);
    }),
    ("Pausa programada persiste ao reiniciar", () =>
    {
        var engine = new ActivationEngine(new AppSettings { ScheduleEnabled = true, Rules = [.. weekdays] });
        engine.Pause();
        var restarted = new ActivationEngine(engine.CurrentSettings);
        Check(!restarted.GetState(monday10).Active);
        restarted.Resume();
        Check(restarted.GetState(monday10).Active);
    }),
    ("Pausar fora do horário impede a ativação seguinte", () =>
    {
        var engine = new ActivationEngine(new AppSettings { ScheduleEnabled = true, Rules = [.. weekdays] });
        var early = new DateTime(2026, 11, 2, 7, 0, 0);
        var waiting = engine.GetState(early);
        Check(!waiting.Active && !waiting.Paused);
        engine.Pause();
        Check(engine.GetState(early).Paused);
        Check(!engine.GetState(monday10).Active);
    }),
    ("Salvar preferências preserva pausa e ativação temporária", () =>
    {
        var engine = new ActivationEngine(new AppSettings());
        engine.ActivateFor(TimeSpan.FromMinutes(30), monday10);
        // A janela envia preferências sem conhecer o estado de execução.
        engine.UpdateSettings(new AppSettings { IntervalSeconds = 120, ManualEnabled = true });
        var state = engine.GetState(monday10.AddMinutes(1));
        Check(state.Temporary);
        Check(engine.CurrentSettings.IntervalSeconds == 120);
        Check(!engine.GetState(monday10.AddMinutes(31)).Active);
    }),
    ("Configurações antigas sem campos novos carregam com padrões", () =>
    {
        var json = """{"IntervalSeconds":59,"ScheduleEnabled":false,"SchedulePaused":false,"ManualEnabled":true,"Rules":[]}""";
        var settings = JsonSerializer.Deserialize<AppSettings>(json)!;
        Check(settings.OnlyWhenIdle && settings.PreventSleep && settings.TemporaryUntil is null);
    })
};

int failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine("OK      " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FALHOU  " + name + ": " + ex.Message); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} testes passaram.");
return failed == 0 ? 0 : 1;

static void Check(bool condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? expression = null)
{
    if (!condition) throw new Exception("Condição não atendida: " + expression);
}
