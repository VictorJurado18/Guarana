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
        CopySelf();
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, true)
            ?? throw new InvalidOperationException("Não foi possível configurar a inicialização.");
        key.SetValue(ValueName, $"\"{InstalledExePath}\"", RegistryValueKind.String);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
        key?.DeleteValue(ValueName, false);
    }

    /// <summary>
    /// Se a inicialização automática está ligada e o usuário abriu outra versão do EXE,
    /// atualiza a cópia em AppData para que o próximo login use a versão nova.
    /// </summary>
    public static void RefreshInstalledCopy()
    {
        try
        {
            if (!IsEnabled() || Environment.ProcessPath is not string source || IsInstalledPath(source)) return;
            var current = new FileInfo(source);
            var installed = new FileInfo(InstalledExePath);
            if (current.Length != installed.Length || current.LastWriteTimeUtc != installed.LastWriteTimeUtc)
                CopySelf();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Mantém a cópia anterior; não é motivo para impedir a abertura.
        }
    }

    private static void CopySelf()
    {
        string source = Environment.ProcessPath ?? throw new InvalidOperationException("Executável não encontrado.");
        Directory.CreateDirectory(SettingsStore.DirectoryPath);
        if (IsInstalledPath(source)) return;
        File.Copy(source, InstalledExePath, true);
        File.SetLastWriteTimeUtc(InstalledExePath, File.GetLastWriteTimeUtc(source));
    }

    private static bool IsInstalledPath(string path) =>
        string.Equals(Path.GetFullPath(path), Path.GetFullPath(InstalledExePath), StringComparison.OrdinalIgnoreCase);
}
