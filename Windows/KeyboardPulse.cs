using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Guarana.Windows;

internal enum PulseResult { Sent, ModifierHeld, Failed }

internal static class KeyboardPulse
{
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const ushort F15 = 0x7E;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    // MouseInput is the largest member; it is declared so the union has the size SendInput expects.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    /// <summary>Tempo desde a última entrada de teclado ou mouse (inclusive a do próprio F15).</summary>
    public static TimeSpan IdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.MaxValue;
        // Both counters wrap every ~49.7 days; unsigned subtraction handles the wrap.
        uint elapsed = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    public static PulseResult TrySend(out string? error)
    {
        error = null;
        // An F15 event while the user holds Shift, Ctrl, Alt or Win may trigger a shortcut.
        if (IsDown(0x10) || IsDown(0x11) || IsDown(0x12) || IsDown(0x5B) || IsDown(0x5C))
            return PulseResult.ModifierHeld;

        var inputs = new[]
        {
            new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    Keyboard = new KeyboardInput { VirtualKey = F15, Flags = KeyUp }
                }
            }
        };
        if (SendInput(1, inputs, Marshal.SizeOf<Input>()) == 1) return PulseResult.Sent;
        error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        return PulseResult.Failed;
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
