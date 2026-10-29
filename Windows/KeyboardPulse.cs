using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Guarana.Windows;

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public static bool TrySend(out string? error)
    {
        error = null;
        // An F15 event while the user holds a modifier may trigger an application shortcut.
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0 ||
            (GetAsyncKeyState(0x11) & 0x8000) != 0 ||
            (GetAsyncKeyState(0x12) & 0x8000) != 0)
            return true;

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
        if (SendInput(1, inputs, Marshal.SizeOf<Input>()) == 1) return true;
        error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        return false;
    }
}
