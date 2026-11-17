using System;

// Shared policy for reserving only controls available to the trainer in the current state.
internal static class InputConsumption
{
    internal static bool Active(TrainerAction action, bool flight) => action switch {
        TrainerAction.Menu or TrainerAction.Flight => true,
        TrainerAction.Teleport => !flight,
        TrainerAction.TurnLeft or TrainerAction.TurnRight => false,
        _ => flight
    };

    internal static bool Keyboard(TrainerBindings bindings, int key, bool flight, bool[] state)
    {
        if (key == 0) return false;
        foreach (var action in TrainerBindings.Actions)
            if (Active(action, flight) && bindings.Key(action) == key && bindings.KeyboardModifiersMatch(action, state)) return true;
        return false;
    }

    internal static bool Controller(TrainerBindings bindings, PadControl control, bool flight)
    {
        if (control == PadControl.None) return false;
        foreach (var action in TrainerBindings.Actions)
            if (Active(action, flight) && bindings.Pad(action) == control) return true;
        return false;
    }

    internal static int VirtualKey(string name)
    {
        name = name.ToLowerInvariant();
        if (name.Length == 1 && char.IsLetterOrDigit(name[0])) return char.ToUpperInvariant(name[0]);
        if (name.StartsWith("f") && int.TryParse(name.Substring(1), out int f) && f >= 1 && f <= 24) return 0x6F + f;
        if (name.StartsWith("numpad") && name.Length == 7 && char.IsDigit(name[6])) return 0x60 + name[6] - '0';
        return name switch {
            "leftshift" or "rightshift" or "shift" => 0x10, "leftctrl" or "rightctrl" or "ctrl" => 0x11,
            "leftalt" or "rightalt" or "alt" => 0x12, "space" => 0x20, "tab" => 9, "enter" => 13,
            "backspace" => 8, "insert" => 0x2D, "delete" => 0x2E, "home" => 0x24, "end" => 0x23,
            "pageup" => 0x21, "pagedown" => 0x22, "leftarrow" => 0x25, "uparrow" => 0x26,
            "rightarrow" => 0x27, "downarrow" => 0x28, "capslock" => 0x14, "numlock" => 0x90,
            "scrolllock" => 0x91, "pause" => 0x13, "printscreen" => 0x2C,
            "semicolon" => 0xBA, "equals" => 0xBB, "comma" => 0xBC, "minus" => 0xBD,
            "period" => 0xBE, "slash" => 0xBF, "backquote" => 0xC0, "leftbracket" => 0xDB,
            "backslash" => 0xDC, "rightbracket" => 0xDD, "quote" => 0xDE,
            "numpadmultiply" => 0x6A, "numpadplus" => 0x6B, "numpadminus" => 0x6D,
            "numpadperiod" => 0x6E, "numpaddivide" => 0x6F, _ => 0
        };
    }

    internal static PadControl Pad(string path) => path.ToLowerInvariant() switch {
        "buttonsouth" => PadControl.A, "buttoneast" => PadControl.B, "buttonwest" => PadControl.X, "buttonnorth" => PadControl.Y,
        "start" => PadControl.Start, "select" => PadControl.Back, "leftshoulder" => PadControl.LeftShoulder,
        "rightshoulder" => PadControl.RightShoulder, "leftstickpress" => PadControl.LeftStick, "rightstickpress" => PadControl.RightStick,
        "lefttrigger" => PadControl.LeftTrigger, "righttrigger" => PadControl.RightTrigger,
        "dpad/up" => PadControl.DpadUp, "dpad/down" => PadControl.DpadDown, "dpad/left" => PadControl.DpadLeft, "dpad/right" => PadControl.DpadRight,
        "leftstick/up" => PadControl.LeftYPositive, "leftstick/down" => PadControl.LeftYNegative,
        "leftstick/left" => PadControl.LeftXNegative, "leftstick/right" => PadControl.LeftXPositive,
        "rightstick/up" => PadControl.RightYPositive, "rightstick/down" => PadControl.RightYNegative,
        "rightstick/left" => PadControl.RightXNegative, "rightstick/right" => PadControl.RightXPositive, _ => PadControl.None
    };
}
