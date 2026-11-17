using System;
using System.Runtime.InteropServices;

internal sealed class TrainerInput
{
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out ControllerState state);
    [StructLayout(LayoutKind.Sequential)] private struct Gamepad { public ushort buttons; public byte leftTrigger, rightTrigger; public short leftX, leftY, rightX, rightY; }
    [StructLayout(LayoutKind.Sequential)] private struct ControllerState { public uint packet; public Gamepad gamepad; }
    private readonly bool[] keys = new bool[256];
    private readonly bool[] previous = new bool[256];
    private readonly float[] pad = new float[Enum.GetValues(typeof(PadControl)).Length];
    private readonly float[] previousPad = new float[Enum.GetValues(typeof(PadControl)).Length];
    private bool wasFocused;
    internal bool Focused { get; private set; }
    internal bool Down(int key) => key > 0 && keys[key];
    internal bool Pressed(int key) => Down(key) && !previous[key];
    internal bool Released => !AnyHeld();
    internal static bool ReadKeyboard(bool[] state)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        bool focused = pid == (uint)Environment.ProcessId;
        for (int key = 1; key < state.Length; key++) state[key] = focused && (GetAsyncKeyState(key) & 0x8000) != 0;
        return focused;
    }
    internal void Poll()
    {
        Array.Copy(keys, previous, keys.Length); Array.Copy(pad, previousPad, pad.Length);
        Array.Clear(keys, 0, keys.Length); Array.Clear(pad, 0, pad.Length);
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        Focused = pid == (uint)Environment.ProcessId;
        if (Focused)
        {
            for (int key = 1; key < keys.Length; key++) keys[key] = (GetAsyncKeyState(key) & 0x8000) != 0;
            for (uint i = 0; i < 4; i++)
                if (XInputGetState(i, out var state) == 0) { ReadPad(state.gamepad); break; }
        }
        // Held keys when focus returns must not trigger a toggle.
        if (Focused && !wasFocused) { Array.Copy(keys, previous, keys.Length); Array.Copy(pad, previousPad, pad.Length); }
        wasFocused = Focused;
    }
    private void ReadPad(Gamepad value)
    {
        for (int i = 1; i < pad.Length; i++)
            pad[i] = ControllerInput.Control((PadControl)i, value.buttons, value.leftTrigger, value.rightTrigger, value.leftX, value.leftY, value.rightX, value.rightY);
    }
    internal float Value(TrainerBindings bindings, TrainerAction action) => Math.Max(bindings.KeyboardHeld(action, keys) ? 1f : 0f, pad[(int)bindings.Pad(action)]);
    internal bool Pressed(TrainerBindings bindings, TrainerAction action)
    {
        int control = (int)bindings.Pad(action);
        return (bindings.KeyboardHeld(action, keys) || pad[control] > 0.5f) &&
            !(bindings.KeyboardHeld(action, previous) || previousPad[control] > 0.5f);
    }
    private bool AnyHeld()
    {
        for (int i = 1; i < keys.Length; i++) if (keys[i]) return true;
        for (int i = 1; i < pad.Length; i++) if (pad[i] > 0.2f) return true;
        return false;
    }
    internal int CaptureKey()
    {
        for (int key = 8; key < keys.Length; key++)
            if (TrainerBindings.ValidKey(key) && !TrainerBindings.IsModifier(key) && Pressed(key)) return key;
        // Modifier taps/releases keep capture waiting for a main key.
        return 0;
    }
    internal KeyModifiers CaptureModifiers(int key) => TrainerBindings.IsModifier(key) ? KeyModifiers.None :
        (Down(0x11) ? KeyModifiers.Ctrl : 0) | (Down(0x10) ? KeyModifiers.Shift : 0) | (Down(0x12) ? KeyModifiers.Alt : 0);
    internal PadControl CapturePad()
    {
        for (int i = 1; i < pad.Length; i++) if (pad[i] > 0.65f && previousPad[i] <= 0.65f) return (PadControl)i;
        return PadControl.None;
    }
}
