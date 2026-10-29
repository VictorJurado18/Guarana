using Microsoft.Win32;
using Guarana.Core;

namespace Guarana.Windows;

internal static class StartupManager
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Guarana";

    public static string InstalledExePath => Path.Combine(SettingsStore.DirectoryPath, "Guarana.exe");

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        string? value = key?.GetValue(ValueName) as string;
        return string.Equals(value, $"\"{InstalledExePath}\"", StringComparison.OrdinalIgnoreCase)
            && File.Exists(InstalledExePath);
    }

    public static void Enable()
    {
        string source = Environment.ProcessPath ?? throw new InvalidOperationException("Executável não encontrado.");
        string target = InstalledExePath;
        Directory.CreateDirectory(SettingsStore.DirectoryPath);
        if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            File.Copy(source, target, true);
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, true)
            ?? throw new InvalidOperationException("Não foi possível configurar a inicialização.");
        key.SetValue(ValueName, $"\"{target}\"", RegistryValueKind.String);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
        key?.DeleteValue(ValueName, false);
    }
}
