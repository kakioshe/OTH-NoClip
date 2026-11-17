using System;
using System.IO;

internal static class BindingChecks
{
    internal static void Run(Action<string, bool> check)
    {
        int acquired = 0, released = 0, warnings = 0;
        bool eligible = true, failAcquire = false, failRelease = false;
        var blocker = new GameInputBlocker(() => eligible, () => {
            acquired++;
            if (failAcquire) throw new InvalidOperationException("acquire failed");
            return () => { if (failRelease) throw new InvalidOperationException("release failed"); released++; };
        }, _ => warnings++);
        blocker.Update(false, true);
        check("idle input leaves game untouched", acquired == 0 && released == 0);
        blocker.Update(true, false); blocker.Update(true, true);
        check("input map acquired once", acquired == 1 && released == 0);
        blocker.Update(false, false);
        check("input map retained until controls release", released == 0);
        blocker.Update(false, true); blocker.Release();
        check("input map released once", released == 1);
        eligible = false; blocker.Update(true, true);
        check("no input block outside local single-player", acquired == 1);
        eligible = true; blocker.Update(true, true); eligible = false; blocker.Update(true, false);
        check("leaving session releases input immediately", acquired == 2 && released == 2);
        eligible = true; failAcquire = true;
        blocker.Update(true, true); blocker.Update(true, true);
        check("acquire errors do not spam or retry each frame", acquired == 3 && warnings == 1);
        blocker.Update(false, true); failAcquire = false; blocker.Update(true, true);
        check("new activation retries acquisition", acquired == 4);
        failRelease = true; blocker.Release(); blocker.Release();
        check("failed cleanup logs once and retains ownership", released == 2 && warnings == 2);
        failRelease = false; blocker.Release(); blocker.Release();
        check("cleanup retry releases owned map once", released == 3);
        var heading = FlightHeading.Horizontal(3, 4, 0, 1);
        check("camera heading normalized", Math.Abs(heading.x - 0.6f) < 0.00001f && Math.Abs(heading.z - 0.8f) < 0.00001f);
        heading = FlightHeading.Horizontal(0, 0, -1, 0);
        check("vertical camera retains heading", heading == (-1f, 0f));
        heading = FlightHeading.Horizontal(float.NaN, 1, 0, 1);
        check("invalid camera retains heading", heading == (0f, 1f));
        var fixedMenu = TrainerBindings.Parse("Menu.Key=70\nMenu.Modifiers=1\nMenu.Pad=Back\nForward.Key=45\n", out int fixedRejections);
        check("legacy menu becomes Insert only", fixedMenu.Key(TrainerAction.Menu) == 0x2D && fixedMenu.Modifiers(TrainerAction.Menu) == KeyModifiers.None && fixedMenu.Pad(TrainerAction.Menu) == PadControl.None);
        check("Insert conflict cleared on migration", fixedRejections == 1 && fixedMenu.Key(TrainerAction.Forward) == 0);
        check("menu cannot be rebound or cleared", !fixedMenu.SetKey(TrainerAction.Menu, 0, out _) && !fixedMenu.SetPad(TrainerAction.Menu, PadControl.Back, out _));
        check("F10 does not reserve game input", !InputConsumption.Keyboard(fixedMenu, 0x79, true, new bool[256]));
        check("right stick free for camera", !InputConsumption.Controller(fixedMenu, PadControl.RightXPositive, true) && !InputConsumption.Controller(fixedMenu, PadControl.RightXNegative, true));
        var bindings = new TrainerBindings();
        check("boost defaults to B", bindings.Key(TrainerAction.Boost) == 0x42);
        foreach (int modifier in new[] { 0x10, 0x11, 0x12 })
            check("standalone modifier rejected " + modifier, !bindings.SetKey(TrainerAction.Boost, modifier, out _) && bindings.Key(TrainerAction.Boost) == 0x42);
        var oldModifiers = TrainerBindings.Parse("Flight.Key=16\nTeleport.Key=17\nBoost.Key=18\nForward.Key=66\n", out int modifierRejections);
        check("saved modifier-only bindings cleared", modifierRejections == 3 && oldModifiers.Key(TrainerAction.Flight) == 0 &&
            oldModifiers.Key(TrainerAction.Teleport) == 0 && oldModifiers.Key(TrainerAction.Boost) == 0);
        check("clearing old modifiers preserves custom keys and controller", oldModifiers.Key(TrainerAction.Forward) == 0x42 && oldModifiers.Pad(TrainerAction.Boost) == PadControl.LeftShoulder);
        var modifierRoundTrip = TrainerBindings.Parse(oldModifiers.Serialize(), out modifierRejections);
        check("cleared modifier bindings round-trip", modifierRejections == 0 && modifierRoundTrip.Serialize() == oldModifiers.Serialize());
        var consumptionKeys = new bool[256];
        check("unbound map M remains available during flight", !InputConsumption.Keyboard(bindings, 0x4D, true, consumptionKeys));
        check("movement key available when flight inactive", !InputConsumption.Keyboard(bindings, 0x57, false, consumptionKeys));
        check("movement key reserved during flight", InputConsumption.Keyboard(bindings, 0x57, true, consumptionKeys));
        check("action shortcuts reserved with flight off", InputConsumption.Keyboard(bindings, 0x77, false, consumptionKeys));
        check("unavailable teleport shortcut free during flight", !InputConsumption.Keyboard(bindings, 0x78, true, consumptionKeys) && InputConsumption.Keyboard(bindings, 0x78, false, consumptionKeys));
        check("unbound controls never reserved", !InputConsumption.Keyboard(bindings, 0, true, consumptionKeys) && !InputConsumption.Controller(bindings, PadControl.None, true));
        check("controller map button available unless assigned", !InputConsumption.Controller(bindings, PadControl.Y, true));
        check("controller movement scoped to flight", !InputConsumption.Controller(bindings, PadControl.LeftYPositive, false) && InputConsumption.Controller(bindings, PadControl.LeftYPositive, true));
        check("controller menu shortcut free", !InputConsumption.Controller(bindings, PadControl.Back, false));
        var chordBindings = new TrainerBindings();
        chordBindings.SetKey(TrainerAction.Left, 0x4D, KeyModifiers.Ctrl, out _);
        check("chord primary free without modifier", !InputConsumption.Keyboard(chordBindings, 0x4D, true, consumptionKeys));
        consumptionKeys[0x11] = true;
        check("chord consumes primary during flight", InputConsumption.Keyboard(chordBindings, 0x4D, true, consumptionKeys));
        check("inactive movement chord leaves map free", !InputConsumption.Keyboard(chordBindings, 0x4D, false, consumptionKeys));
        consumptionKeys[0x10] = true;
        check("extra modifiers leave exact chord free", !InputConsumption.Keyboard(chordBindings, 0x4D, true, consumptionKeys));
        check("native key names map to VK", InputConsumption.VirtualKey("m") == 0x4D && InputConsumption.VirtualKey("leftCtrl") == 0x11 && InputConsumption.VirtualKey("f10") == 0x79 && InputConsumption.VirtualKey("numpad3") == 0x63);
        check("unknown controls pass through", InputConsumption.VirtualKey("unknown") == 0 && InputConsumption.Pad("unknown") == PadControl.None);
        check("native controller names map to bindings", InputConsumption.Pad("buttonNorth") == PadControl.Y && InputConsumption.Pad("leftStick/up") == PadControl.LeftYPositive);
        check("default flight remains F8", bindings.Key(TrainerAction.Flight) == 0x77);
        check("default teleport remains F9", bindings.Key(TrainerAction.Teleport) == 0x78);
        check("default menu Insert only", bindings.Key(TrainerAction.Menu) == 0x2D && bindings.Pad(TrainerAction.Menu) == PadControl.None);
        check("keyboard conflict rejected", !bindings.SetKey(TrainerAction.Flight, 0x78, out _) && bindings.Key(TrainerAction.Flight) == 0x77);
        check("pad conflict rejected", !bindings.SetPad(TrainerAction.Flight, PadControl.LeftShoulder, out _) && bindings.Pad(TrainerAction.Flight) == PadControl.None);
        foreach (int reserved in new[] { -1, 1, 0x1B, 0x79, 0x5B, 0xA0, 256 })
            check("reserved keyboard rejected " + reserved, !bindings.SetKey(TrainerAction.Menu, reserved, out _));
        check("unknown action rejected", !bindings.SetKey((TrainerAction)999, 0, out _));
        check("unknown pad rejected", !bindings.SetPad(TrainerAction.Menu, (PadControl)999, out _));
        check("clear keyboard", bindings.SetKey(TrainerAction.Flight, 0, out _) && bindings.SetKey(TrainerAction.Teleport, 0, out _));
        check("swap keyboard assignments", bindings.SetKey(TrainerAction.Flight, 0x78, out _) && bindings.SetKey(TrainerAction.Teleport, 0x77, out _));
        bindings.SetPad(TrainerAction.Boost, PadControl.None, out _);
        check("assign controller action", bindings.SetPad(TrainerAction.Flight, PadControl.LeftShoulder, out _));
        bindings.SetPad(TrainerAction.Boost, PadControl.A, out _);
        var restored = TrainerBindings.Parse(bindings.Serialize(), out int rejected);
        check("valid settings accepted", rejected == 0);
        foreach (var action in TrainerBindings.Actions)
            check("round-trip " + action, bindings.Key(action) == restored.Key(action) && bindings.Pad(action) == restored.Pad(action));
        var malformed = TrainerBindings.Parse("garbage\nFlight.Key=121\nMenu.Pad=999\n999.Key=65\nFlight.Key=abc\nMenu.Key=\nMenu.Pad=Back=Start\n", out rejected);
        check("malformed config counted", rejected == 7);
        check("malformed config retains defaults", malformed.Key(TrainerAction.Flight) == 0x77 && malformed.Pad(TrainerAction.Menu) == PadControl.None);
        var duplicate = TrainerBindings.Parse("Flight.Key=120\nFlight.Key=119\nTeleport.Pad=LeftShoulder\n", out rejected);
        check("duplicate lines and assignments diagnosed", rejected == 3);
        check("duplicate key resolves without double action", duplicate.Key(TrainerAction.Flight) == 120 && duplicate.Key(TrainerAction.Teleport) == 0);
        check("duplicate pad resolves without double action", duplicate.Pad(TrainerAction.Teleport) == PadControl.LeftShoulder && duplicate.Pad(TrainerAction.Boost) == PadControl.None);
        bindings.Reset();
        check("reset restores defaults", bindings.Key(TrainerAction.Flight) == 0x77 && bindings.Pad(TrainerAction.Boost) == PadControl.LeftShoulder);
        check("empty file uses defaults", TrainerBindings.Parse("# empty\r\n\n", out rejected).Serialize() == bindings.Serialize() && rejected == 0);
        check("arrow defaults avoid game radial keys", bindings.Key(TrainerAction.Rise) == 0x26 && bindings.Key(TrainerAction.Descend) == 0x28 &&
            bindings.Key(TrainerAction.TurnLeft) == 0x25 && bindings.Key(TrainerAction.TurnRight) == 0x27);
        var legacy = TrainerBindings.Parse("# Port's Trainer bindings v1; original defaults\nFlight.Key=70\nRise.Key=32\nDescend.Key=17\nTurnLeft.Key=81\nTurnRight.Key=69\n", out rejected);
        check("legacy defaults migrate and preserve custom flight", rejected == 0 && legacy.Key(TrainerAction.Flight) == 70 &&
            legacy.Key(TrainerAction.Rise) == 0x26 && legacy.Key(TrainerAction.Descend) == 0x28 && legacy.Key(TrainerAction.TurnLeft) == 0x25 && legacy.Key(TrainerAction.TurnRight) == 0x27);
        legacy = TrainerBindings.Parse("# Port's Trainer bindings v1; custom arrows\nFlight.Key=38\nRise.Key=32\nTurnLeft.Key=82\n", out rejected);
        check("migration preserves occupied arrows and custom controls", rejected == 0 && legacy.Key(TrainerAction.Flight) == 38 && legacy.Key(TrainerAction.Rise) == 32 && legacy.Key(TrainerAction.TurnLeft) == 82);
        check("Shift Q shortcut", bindings.SetKey(TrainerAction.Flight, 0x51, KeyModifiers.Shift, out _));
        check("Ctrl Q is a distinct shortcut", bindings.SetKey(TrainerAction.Teleport, 0x51, KeyModifiers.Ctrl, out _));
        check("duplicate chord rejected", !bindings.SetKey(TrainerAction.Menu, 0x51, KeyModifiers.Shift, out _));
        check("shortcut display", bindings.ShortcutName(TrainerAction.Flight) == "Shift + Q");
        restored = TrainerBindings.Parse(bindings.Serialize(), out rejected);
        check("combination round-trip", rejected == 0 && restored.Modifiers(TrainerAction.Flight) == KeyModifiers.Shift && restored.Modifiers(TrainerAction.Teleport) == KeyModifiers.Ctrl);
        foreach (var mods in new[] { (KeyModifiers)8, (KeyModifiers)(-1) })
            check("invalid modifier flags rejected " + mods, !bindings.SetKey(TrainerAction.Flight, 0x51, mods, out _));
        check("modifier as chord primary rejected", !bindings.SetKey(TrainerAction.Flight, 0x10, KeyModifiers.Ctrl, out _));
        check("unbound with modifiers rejected", !bindings.SetKey(TrainerAction.Flight, 0, KeyModifiers.Ctrl, out _));
        var state = new bool[256];
        state[0x51] = true;
        check("chord requires modifier", !bindings.KeyboardHeld(TrainerAction.Flight, state));
        state[0x10] = true;
        check("chord activates", bindings.KeyboardHeld(TrainerAction.Flight, state) && !bindings.KeyboardHeld(TrainerAction.Teleport, state));
        state[0x11] = true;
        check("extra modifier does not trigger different chord", !bindings.KeyboardHeld(TrainerAction.Flight, state) && !bindings.KeyboardHeld(TrainerAction.Teleport, state));
        check("multi-modifier chord", bindings.SetKey(TrainerAction.Left, 0x51, KeyModifiers.Shift | KeyModifiers.Ctrl, out _) && bindings.KeyboardHeld(TrainerAction.Left, state));
        state[0x12] = true;
        check("three-modifier chord", bindings.SetKey(TrainerAction.Left, 0x51, KeyModifiers.Shift | KeyModifiers.Ctrl | KeyModifiers.Alt, out _) && bindings.KeyboardHeld(TrainerAction.Left, state));
        bindings.SetKey(TrainerAction.Forward, 0x51, out _);
        check("chord suppresses plain continuous key", !bindings.KeyboardHeld(TrainerAction.Forward, state));
        Array.Clear(state);
        state[0x57] = state[0x10] = state[0x42] = true;
        bindings.Reset();
        check("boost B keeps movement usable with modifiers held", bindings.KeyboardHeld(TrainerAction.Forward, state) && bindings.KeyboardHeld(TrainerAction.Boost, state));
        state[0x42] = false;
        check("Shift alone no longer activates boost", !bindings.KeyboardHeld(TrainerAction.Boost, state));
        state[0x77] = true;
        check("plain toggles ignore extra modifiers", !bindings.KeyboardHeld(TrainerAction.Flight, state));
        state[0x10] = false;
        check("plain toggle works without modifiers", bindings.KeyboardHeld(TrainerAction.Flight, state));
        malformed = TrainerBindings.Parse("Flight.Modifiers=8\nMenu.Modifiers=-1\nBoost.Key=16\nBoost.Modifiers=1\nTeleport.Modifiers=2\nTeleport.Modifiers=1\n", out rejected);
        check("malformed modifiers diagnosed", rejected == 4 && malformed.Key(TrainerAction.Boost) == 0);

        string directory = Path.Combine(Path.GetTempPath(), "ports-trainer-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "bindings.cfg");
        try
        {
            var store = new BindingStore(path);
            var missing = store.Load(out rejected);
            check("missing settings load defaults without writing a file", missing.Key(TrainerAction.Flight) == 0x77 && rejected == 0 && !File.Exists(path));
            store.Save(bindings);
            check("save creates configuration directory", File.Exists(path));
            bindings.SetKey(TrainerAction.Flight, 0x24, out _);
            store.Save(bindings);
            check("save replaces existing configuration", store.Load(out rejected).Key(TrainerAction.Flight) == 0x24 && rejected == 0);
            check("save leaves no temporary file", !File.Exists(path + ".tmp"));
            File.WriteAllText(path, "invalid=value");
            check("store preserves parser diagnostics", store.Load(out rejected).Key(TrainerAction.Flight) == 0x77 && rejected == 1);
            bool rejectedNull = false;
            try { store.Save(null); }
            catch (ArgumentNullException) { rejectedNull = true; }
            check("store rejects missing bindings", rejectedNull);
            bool rejectedPath = false;
            try { new BindingStore(" "); }
            catch (ArgumentException) { rejectedPath = true; }
            check("store rejects empty path", rejectedPath);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }

        ushort[] buttons = { 0x1000, 0x2000, 0x4000, 0x8000, 0x0010, 0x0020, 0x0100, 0x0200, 0x0040, 0x0080, 1, 2, 4, 8 };
        for (int i = 0; i < buttons.Length; i++)
        {
            PadControl control = (PadControl)(i + 1);
            check("controller button " + control, ControllerInput.Control(control, buttons[i], 0, 0, 0, 0, 0, 0) == 1f);
            check("other controller button ignored " + control, ControllerInput.Control(control, buttons[(i + 1) % buttons.Length], 0, 0, 0, 0, 0, 0) == 0f);
        }
        foreach (PadControl control in Enum.GetValues<PadControl>())
            check("neutral controller " + control, ControllerInput.Control(control, 0, 0, 0, 0, 0, 0, 0) == 0f);
        for (int axis = 0; axis < 4; axis++)
        {
            var negative = (PadControl)((int)PadControl.LeftXNegative + axis * 2);
            var positive = (PadControl)((int)negative + 1);
            short[] values = new short[4]; values[axis] = short.MaxValue;
            check("positive axis " + positive, ControllerInput.Control(positive, 0, 0, 0, values[0], values[1], values[2], values[3]) == 1f);
            check("positive axis ignores negative " + negative, ControllerInput.Control(negative, 0, 0, 0, values[0], values[1], values[2], values[3]) == 0f);
            values[axis] = short.MinValue;
            check("negative axis " + negative, ControllerInput.Control(negative, 0, 0, 0, values[0], values[1], values[2], values[3]) == 1f);
            check("negative axis ignores positive " + positive, ControllerInput.Control(positive, 0, 0, 0, values[0], values[1], values[2], values[3]) == 0f);
        }
        check("controller left trigger", ControllerInput.Control(PadControl.LeftTrigger, 0, 255, 0, 0, 0, 0, 0) == 1f);
        check("controller right trigger", ControllerInput.Control(PadControl.RightTrigger, 0, 0, 255, 0, 0, 0, 0) == 1f);
        check("controller trigger proportional", Math.Abs(ControllerInput.Control(PadControl.LeftTrigger, 0, 120, 0, 0, 0, 0, 0) - 0.4f) < 0.00001f);
    }
}
