using Guarana.Core;

var tests = new (string Name, Action Run)[]
{
    ("Início incluído e fim excluído", () =>
    {
        var rules = new[] { new ScheduleRule(DayOfWeek.Monday, "09:00", "18:00") };
        Check(!ScheduleEngine.IsActive(new DateTime(2026, 11, 2, 8, 59, 59), rules));
        Check(ScheduleEngine.IsActive(new DateTime(2026, 11, 2, 9, 0, 0), rules));
        Check(!ScheduleEngine.IsActive(new DateTime(2026, 11, 2, 18, 0, 0), rules));
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
        Check(ScheduleEngine.NextTransition(new DateTime(2026, 11, 2, 10, 0, 0), rules)
            == new DateTime(2026, 11, 2, 15, 0, 0));
    }),
    ("Dias da semana e próximo início", () =>
    {
        var rules = new[] { new ScheduleRule(DayOfWeek.Friday, "09:00", "18:00") };
        Check(!ScheduleEngine.IsActive(new DateTime(2026, 11, 2, 10, 0, 0), rules));
        Check(ScheduleEngine.NextTransition(new DateTime(2026, 11, 2, 10, 0, 0), rules)
            == new DateTime(2026, 11, 6, 9, 0, 0));
    }),
    ("Ativação temporária termina em modo manual", () =>
    {
        var now = new DateTime(2026, 11, 2, 10, 0, 0);
        var engine = new ActivationEngine(new AppSettings());
        engine.ActivateFor(TimeSpan.FromMinutes(30), now);
        Check(engine.GetState(now.AddMinutes(29)).Active);
        Check(!engine.GetState(now.AddMinutes(30)).Active);
    }),
    ("Ativação temporária retorna ao horário programado", () =>
    {
        var settings = new AppSettings
        {
            ScheduleEnabled = true,
            Rules = [new ScheduleRule(DayOfWeek.Monday, "09:00", "18:00")]
        };
        var engine = new ActivationEngine(settings);
        var monday = new DateTime(2026, 11, 2, 8, 0, 0);
        engine.ActivateFor(TimeSpan.FromMinutes(30), monday);
        Check(engine.GetState(monday.AddMinutes(20)).Active);
        Check(!engine.GetState(monday.AddMinutes(30)).Active);
        Check(engine.GetState(monday.AddHours(1)).Active);
    }),
    ("Pausa e retomada", () =>
    {
        var now = new DateTime(2026, 11, 2, 10, 0, 0);
        var engine = new ActivationEngine(new AppSettings());
        engine.Pause();
        Check(!engine.GetState(now).Active);
        engine.Resume();
        Check(engine.GetState(now).Active);
    }),
    ("Pausa programada persiste ao reiniciar", () =>
    {
        var settings = new AppSettings
        {
            ScheduleEnabled = true,
            Rules = [new ScheduleRule(DayOfWeek.Monday, "09:00", "18:00")]
        };
        var now = new DateTime(2026, 11, 2, 10, 0, 0);
        var engine = new ActivationEngine(settings);
        engine.Pause();
        var restarted = new ActivationEngine(engine.CurrentSettings);
        Check(!restarted.GetState(now).Active);
        restarted.Resume();
        Check(restarted.GetState(now).Active);
    })
};

int failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine("OK  " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FALHOU  " + name + ": " + ex.Message); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} testes passaram.");
return failed == 0 ? 0 : 1;

static void Check(bool condition)
{
    if (!condition) throw new Exception("Condição não atendida.");
}
