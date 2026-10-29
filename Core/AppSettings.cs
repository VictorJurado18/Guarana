using System.Text.Json;

namespace Guarana.Core;

public sealed class AppSettings
{
    public int IntervalSeconds { get; set; } = 59;
    public bool ScheduleEnabled { get; set; }
    public bool SchedulePaused { get; set; }
    public bool ManualEnabled { get; set; } = true;
    public List<ScheduleRule> Rules { get; set; } = DefaultRules();

    public static List<ScheduleRule> DefaultRules() =>
        [
            new(DayOfWeek.Monday, "09:00", "18:00"),
            new(DayOfWeek.Tuesday, "09:00", "18:00"),
            new(DayOfWeek.Wednesday, "09:00", "18:00"),
            new(DayOfWeek.Thursday, "09:00", "18:00"),
            new(DayOfWeek.Friday, "09:00", "18:00")
        ];

    public AppSettings Copy() => new()
    {
        IntervalSeconds = IntervalSeconds,
        ScheduleEnabled = ScheduleEnabled,
        SchedulePaused = SchedulePaused,
        ManualEnabled = ManualEnabled,
        Rules = Rules.Select(r => r with { }).ToList()
    };
}

public sealed record ScheduleRule(DayOfWeek Day, string Start, string End);

public static class SettingsStore
{
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guarana");

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
            if (settings is null || settings.IntervalSeconds is < 10 or > 3600 || settings.Rules is null)
                throw new InvalidDataException("Configurações inválidas.");
            foreach (var rule in settings.Rules)
                ScheduleEngine.Validate(rule);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            // Preserve an unreadable file for diagnosis rather than overwriting it.
            try
            {
                if (File.Exists(FilePath))
                    File.Move(FilePath, FilePath + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            }
            catch { /* The default settings remain usable. */ }
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, FilePath, true);
    }
}
