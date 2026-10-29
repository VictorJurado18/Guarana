using Guarana.Windows;

namespace Guarana;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var singleInstance = new Mutex(true, @"Local\GuaranaDesktopApp", out bool created);
        if (!created)
        {
            MessageBox.Show("O Guaraná já está em execução na bandeja do Windows.",
                "Guaraná", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.Run(new GuaranaContext());
    }
}
