using System;
using System.Collections.Generic;
using System.IO;

internal enum TrainerAction { Menu, Flight, Teleport, Forward, Back, Left, Right, Rise, Descend, TurnLeft, TurnRight, Boost }
[Flags]
internal enum KeyModifiers { None = 0, Ctrl = 1, Shift = 2, Alt = 4 }
internal enum PadControl
{
    None, A, B, X, Y, Start, Back, LeftShoulder, RightShoulder, LeftStick, RightStick,
    DpadUp, DpadDown, DpadLeft, DpadRight, LeftXNegative, LeftXPositive, LeftYNegative,
    LeftYPositive, RightXNegative, RightXPositive, RightYNegative, RightYPositive, LeftTrigger, RightTrigger
}

// No Unity or loader dependency: validation and persistence can run in CI.
internal sealed class TrainerBindings
{
    // Retain old turn settings for compatibility, but never reserve their controls.
    internal static bool Retired(TrainerAction action) => action == TrainerAction.TurnLeft || action == TrainerAction.TurnRight;
    internal static readonly TrainerAction[] Actions = (TrainerAction[])Enum.GetValues(typeof(TrainerAction));
    private readonly Dictionary<TrainerAction, int> keys = new();
    private readonly Dictionary<TrainerAction, KeyModifiers> modifiers = new();
    private readonly Dictionary<TrainerAction, PadControl> pads = new();
    internal TrainerBindings() { Reset(); }
    internal int Key(TrainerAction action) => keys[action];
    internal KeyModifiers Modifiers(TrainerAction action) => modifiers[action];
    internal PadControl Pad(TrainerAction action) => pads[action];
    internal void Reset()
    {
        int[] defaults = { 0x2D, 0x77, 0x78, 0x57, 0x53, 0x41, 0x44, 0x26, 0x28, 0x25, 0x27, 0x42 };
        PadControl[] controller = { PadControl.None, PadControl.None, PadControl.None,
            PadControl.LeftYPositive, PadControl.LeftYNegative, PadControl.LeftXNegative, PadControl.LeftXPositive,
            PadControl.RightTrigger, PadControl.LeftTrigger, PadControl.RightXNegative, PadControl.RightXPositive, PadControl.LeftShoulder };
        for (int i = 0; i < Actions.Length; i++) { keys[Actions[i]] = defaults[i]; modifiers[Actions[i]] = KeyModifiers.None; pads[Actions[i]] = controller[i]; }
    }
    internal static bool ValidKey(int key) => key == 0 ||
        (key >= 0x08 && key <= 0xFE && key != 0x1B && key != 0x79 && key != 0x5B && key != 0x5C && key != 0x5D &&
         key != 0xA0 && key != 0xA1 && key != 0xA2 && key != 0xA3 && key != 0xA4 && key != 0xA5);
    internal bool SetKey(TrainerAction action, int key, out string error)
        => SetKey(action, key, KeyModifiers.None, out error);
    internal static bool IsModifier(int key) => key == 0x10 || key == 0x11 || key == 0x12;
    internal static bool ValidCombination(int key, KeyModifiers mods) => ValidKey(key) && !IsModifier(key) && ((int)mods & ~7) == 0 &&
        (mods == KeyModifiers.None || key != 0);
    internal bool SetKey(TrainerAction action, int key, KeyModifiers mods, out string error)
    {
        if (!Enum.IsDefined(typeof(TrainerAction), action) || !ValidCombination(key, mods)) { error = "That key combination is reserved or unsupported."; return false; }
        if (action == TrainerAction.Menu && (key != 0x2D || mods != KeyModifiers.None)) { error = "Menu uses Insert."; return false; }
        foreach (var other in Actions)
            if (key != 0 && !Retired(other) && other != action && keys[other] == key && modifiers[other] == mods) { error = "Already assigned to " + Label(other) + ". Clear it first."; return false; }
        keys[action] = key; modifiers[action] = mods; error = null; return true;
    }
    internal bool SetPad(TrainerAction action, PadControl control, out string error)
    {
        if (!Enum.IsDefined(typeof(TrainerAction), action) || !Enum.IsDefined(typeof(PadControl), control)) { error = "Unsupported controller control."; return false; }
        if (action == TrainerAction.Menu && control != PadControl.None) { error = "Menu uses Insert."; return false; }
        foreach (var other in Actions)
            if (control != PadControl.None && !Retired(other) && other != action && pads[other] == control) { error = "Already assigned to " + Label(other) + ". Clear it first."; return false; }
        pads[action] = control; error = null; return true;
    }
    internal string Serialize()
    {
        var lines = new List<string> { "# Port's Trainer bindings v2; 0 / None means unbound. Insert opens the menu." };
        foreach (var action in Actions) { lines.Add(action + ".Key=" + Key(action)); lines.Add(action + ".Modifiers=" + (int)Modifiers(action)); lines.Add(action + ".Pad=" + Pad(action)); }
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
    internal static TrainerBindings Parse(string text, out int rejected)
    {
        var result = new TrainerBindings();
        var keyValues = new Dictionary<TrainerAction, int>();
        var modifierValues = new Dictionary<TrainerAction, KeyModifiers>();
        var padValues = new Dictionary<TrainerAction, PadControl>();
        rejected = 0;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] pair = line.Split('=');
            string[] name = pair[0].Split('.');
            if (pair.Length != 2 || name.Length != 2 || !Enum.TryParse(name[0], out TrainerAction action) || !Enum.IsDefined(typeof(TrainerAction), action)) { rejected++; continue; }
            if (name[1] == "Key" && int.TryParse(pair[1], out int key) && ValidKey(key) && !keyValues.ContainsKey(action)) keyValues.Add(action, key);
            else if (name[1] == "Modifiers" && int.TryParse(pair[1], out int mods) && mods >= 0 && mods <= 7 && !modifierValues.ContainsKey(action)) modifierValues.Add(action, (KeyModifiers)mods);
            else if (name[1] == "Pad" && Enum.TryParse(pair[1], out PadControl pad) && Enum.IsDefined(typeof(PadControl), pad) && !padValues.ContainsKey(action)) padValues.Add(action, pad);
            else rejected++;
        }
        // Apply the complete set before checking conflicts so swapped defaults round-trip.
        foreach (var item in keyValues) result.keys[item.Key] = item.Value;
        foreach (var item in modifierValues) result.modifiers[item.Key] = item.Value;
        // Upgrade only the old shipped defaults; retain custom shortcuts and all controller settings.
        if (text.StartsWith("# Port's Trainer bindings v1;", StringComparison.Ordinal))
        {
            var legacy = new[] { (TrainerAction.Rise, 0x20, 0x26), (TrainerAction.Descend, 0x11, 0x28),
                (TrainerAction.TurnLeft, 0x51, 0x25), (TrainerAction.TurnRight, 0x45, 0x27) };
            foreach (var (action, oldKey, newKey) in legacy)
            {
                if (result.Key(action) != oldKey || result.Modifiers(action) != KeyModifiers.None) continue;
                bool occupied = false;
                foreach (var other in Actions) if (other != action && result.Key(other) == newKey && result.Modifiers(other) == KeyModifiers.None) occupied = true;
                if (!occupied) result.keys[action] = newKey;
            }
        }
        foreach (var item in padValues) result.pads[item.Key] = item.Value;
        // The menu is fixed to Insert even when loading older custom settings.
        result.keys[TrainerAction.Menu] = 0x2D;
        result.modifiers[TrainerAction.Menu] = KeyModifiers.None;
        result.pads[TrainerAction.Menu] = PadControl.None;
        var seenKeys = new HashSet<(int, KeyModifiers)>();
        var seenPads = new HashSet<PadControl>();
        foreach (var action in Actions)
        {
            if (Retired(action)) continue;
            int key = result.Key(action);
            if (!ValidCombination(key, result.Modifiers(action)) || (key != 0 && !seenKeys.Add((key, result.Modifiers(action)))))
            { result.keys[action] = 0; result.modifiers[action] = KeyModifiers.None; rejected++; }
            PadControl pad = result.Pad(action);
            if (pad != PadControl.None && !seenPads.Add(pad)) { result.pads[action] = PadControl.None; rejected++; }
        }
        return result;
    }
    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, Serialize());
        File.Move(temporary, path, true);
    }
    internal static string Label(TrainerAction action) => action switch
    {
        TrainerAction.Menu => "Open / close menu", TrainerAction.Flight => "Toggle flight", TrainerAction.Teleport => "Teleport to map pin",
        TrainerAction.Rise => "Ascend", TrainerAction.TurnLeft => "Turn left", TrainerAction.TurnRight => "Turn right", _ => action.ToString()
    };
    internal string ShortcutName(TrainerAction action)
    {
        KeyModifiers mods = Modifiers(action);
        return ((mods & KeyModifiers.Ctrl) != 0 ? "Ctrl + " : "") +
            ((mods & KeyModifiers.Shift) != 0 ? "Shift + " : "") +
            ((mods & KeyModifiers.Alt) != 0 ? "Alt + " : "") + KeyName(Key(action));
    }

    // Plain movement keys still work with boost held; a configured chord takes precedence over its plain key.
    internal bool KeyboardHeld(TrainerAction action, bool[] state)
    {
        int key = Key(action);
        if (key == 0 || !state[key]) return false;
        return KeyboardModifiersMatch(action, state);
    }
    internal bool KeyboardModifiersMatch(TrainerAction action, bool[] state)
    {
        int key = Key(action);
        KeyModifiers held = (state[0x11] ? KeyModifiers.Ctrl : 0) | (state[0x10] ? KeyModifiers.Shift : 0) | (state[0x12] ? KeyModifiers.Alt : 0);
        KeyModifiers required = Modifiers(action);
        if (required != KeyModifiers.None) return held == required;
        foreach (var other in Actions)
            if (!Retired(other) && Key(other) == key && Modifiers(other) != KeyModifiers.None && Modifiers(other) == held) return false;
        // Toggle shortcuts require exact modifiers; continuous controls allow boost and other held modifiers.
        return action > TrainerAction.Teleport || held == KeyModifiers.None;
    }
    internal static string PadName(PadControl control) => control switch
    {
        PadControl.None => "Unbound", PadControl.LeftShoulder => "Left bumper", PadControl.RightShoulder => "Right bumper",
        PadControl.LeftXNegative => "Left stick left", PadControl.LeftXPositive => "Left stick right",
        PadControl.LeftYNegative => "Left stick down", PadControl.LeftYPositive => "Left stick up",
        PadControl.RightXNegative => "Right stick left", PadControl.RightXPositive => "Right stick right",
        PadControl.RightYNegative => "Right stick down", PadControl.RightYPositive => "Right stick up",
        PadControl.LeftTrigger => "Left trigger", PadControl.RightTrigger => "Right trigger",
        PadControl.LeftStick => "Left stick click", PadControl.RightStick => "Right stick click", _ => control.ToString()
    };
    internal static string KeyName(int key) => key switch
    {
        0 => "Unbound", 0x2D => "Insert", 0x20 => "Space", 0x10 => "Shift", 0x11 => "Ctrl", 0x12 => "Alt",
        0x08 => "Backspace", 0x09 => "Tab", 0x0D => "Enter", 0x2E => "Delete", 0x24 => "Home", 0x23 => "End",
        0x21 => "Page Up", 0x22 => "Page Down", 0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
        >= 0x70 and <= 0x87 => "F" + (key - 0x6F), >= 0x30 and <= 0x5A => ((char)key).ToString(),
        >= 0x60 and <= 0x69 => "Numpad " + (key - 0x60), _ => "VK " + key.ToString("X2")
    };
}
