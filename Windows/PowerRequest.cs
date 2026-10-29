using System.Runtime.InteropServices;

namespace Guarana.Windows;

/// <summary>
/// Impede suspensão e desligamento da tela pela API oficial do Windows
/// (a mesma usada por players de vídeo). Complementa o F15, que cuida do
/// status "Ausente" de apps como Teams e Slack.
/// </summary>
internal static class PowerRequest
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    private static bool? _applied;

    /// <summary>Deve ser chamado sempre na mesma thread (a thread de interface).</summary>
    public static void Set(bool keepAwake)
    {
        if (_applied == keepAwake) return;
        uint flags = keepAwake ? EsContinuous | EsSystemRequired | EsDisplayRequired : EsContinuous;
        if (SetThreadExecutionState(flags) != 0) _applied = keepAwake;
    }
}
