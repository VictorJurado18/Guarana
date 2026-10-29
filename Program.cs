using Guarana.Windows;

namespace Guarana;

internal static class Program
{
    private const string MutexName = @"Local\GuaranaDesktopApp";
    private const string ShowSignalName = @"Local\GuaranaDesktopApp.Show";

    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(true, MutexName, out bool created);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        if (!created)
        {
            // Já está rodando: pede à instância existente para abrir a janela e sai.
            showSignal.Set();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new GuaranaContext(showSignal));
    }
}
