using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using UnityEngine.InputSystem;

// Temporary binding overrides leave the game's map stack and unrelated bindings intact.
internal sealed class GameBindingSuppression
{
    private sealed class OwnedBinding
    {
        internal InputAction Action;
        internal int Index;
        internal string Id;
        internal InputBinding Original;
    }
    private readonly TrainerLogger logger;
    private readonly List<OwnedBinding> owned = new();
    private readonly bool[] keys = new bool[256];
    private readonly Func<bool> eligible, flight;
    private readonly Func<TrainerBindings> bindings;
    private readonly Dictionary<string, List<InputControl>> controls = new();
    private IntPtr keyboard;
    private IntPtr assetPointer;
    private long gamepads;
    private bool warned;
    private readonly HarmonyLib.Harmony hook = new("local.oth.trainer.input");
    private static GameBindingSuppression current;

    internal GameBindingSuppression(Func<bool> eligible, Func<bool> flight, Func<TrainerBindings> bindings, TrainerLogger logger)
    {
        this.logger = logger;
        this.eligible = eligible; this.flight = flight; this.bindings = bindings;
        current = this;
        try
        {
            var manager = typeof(InputSystem).Assembly.GetType("UnityEngine.InputSystem.InputManager");
            var target = manager?.GetMethod("OnBeforeUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (target == null) throw new MissingMethodException("InputManager.OnBeforeUpdate");
            hook.Patch(target, new HarmonyMethod(typeof(GameBindingSuppression).GetMethod(nameof(BeforeInput), BindingFlags.Static | BindingFlags.NonPublic)));
            logger.LogInfo("Selective game binding suppression hook installed (runtime verification pending).");
        }
        catch (Exception error) { logger.LogWarning("Early input hook unavailable; first-frame conflicts may occur: " + error.Message); }
    }
    private static void BeforeInput() => current?.Update();

    internal void Update()
    {
        try
        {
            if (!eligible() || !TrainerInput.ReadKeyboard(keys)) { Release(); return; }
            var asset = GameInput.PlayerInputActions?.asset;
            if (asset == null) { Release(); return; }
            if (assetPointer != asset.Pointer) { Release(); controls.Clear(); assetPointer = asset.Pointer; }
            IntPtr kb = UnityEngine.InputSystem.Keyboard.current?.Pointer ?? IntPtr.Zero;
            long pads = 0;
            for (int i = 0; i < Gamepad.all.Count; i++) pads ^= Gamepad.all[i].Pointer.ToInt64();
            if (keyboard != kb || gamepads != pads) { controls.Clear(); keyboard = kb; gamepads = pads; }
            var configuration = bindings();
            bool flying = flight();
            // Restore only paths we still own. Other screens/rebinding tools retain their changes.
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                var item = owned[i];
                var current = item.Action.bindings[item.Index];
                if (!StillOwned(item, current)) { owned.RemoveAt(i); continue; }
                if (ShouldConsume(item.Original.effectivePath, configuration, flying)) continue;
                Restore(item); owned.RemoveAt(i);
            }
            var maps = asset.actionMaps;
            for (int m = 0; m < maps.Count; m++)
            {
                var actions = maps[m].actions;
                for (int a = 0; a < actions.Count; a++)
                {
                    var action = actions[a];
                    var actionBindings = action.bindings;
                    for (int b = 0; b < actionBindings.Count; b++)
                    {
                        var original = actionBindings[b];
                        if (original.isComposite || string.IsNullOrEmpty(original.effectivePath) ||
                            !ShouldConsume(original.effectivePath, configuration, flying)) continue;
                        var item = new OwnedBinding { Action = action, Index = b, Id = original.id.ToString(), Original = original };
                        InputActionRebindingExtensions.ApplyBindingOverride(action, b, "");
                        owned.Add(item);
                    }
                }
            }
            warned = false;
        }
        catch (Exception error)
        {
            if (!warned) logger.LogWarning("Selective game binding suppression unavailable: " + error.Message);
            warned = true;
            Release();
        }
    }

    private bool ShouldConsume(string path, TrainerBindings configuration, bool flying)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (!controls.TryGetValue(path, out var matching))
        {
            matching = new List<InputControl>();
            AddControls(matching, path, UnityEngine.InputSystem.Keyboard.current);
            for (int i = 0; i < Gamepad.all.Count; i++) AddControls(matching, path, Gamepad.all[i]);
            controls[path] = matching;
        }
        foreach (var control in matching)
        {
            if (control.device.Pointer == keyboard)
            {
                if (InputConsumption.Keyboard(configuration, InputConsumption.VirtualKey(control.name), flying, keys)) return true;
            }
            else
            {
                string relative = control.path.Substring(control.device.path.Length + 1);
                if (InputConsumption.Controller(configuration, InputConsumption.Pad(relative), flying)) return true;
                // A whole stick/axis binding includes both directions; reserve it if either is in use.
                foreach (string direction in new[] { "/up", "/down", "/left", "/right" })
                    if (InputConsumption.Controller(configuration, InputConsumption.Pad(relative + direction), flying)) return true;
                string axis = relative.EndsWith("/x") ? relative.Substring(0, relative.Length - 2) : null;
                if (axis != null && (InputConsumption.Controller(configuration, InputConsumption.Pad(axis + "/left"), flying) ||
                    InputConsumption.Controller(configuration, InputConsumption.Pad(axis + "/right"), flying))) return true;
                axis = relative.EndsWith("/y") ? relative.Substring(0, relative.Length - 2) : null;
                if (axis != null && (InputConsumption.Controller(configuration, InputConsumption.Pad(axis + "/up"), flying) ||
                    InputConsumption.Controller(configuration, InputConsumption.Pad(axis + "/down"), flying))) return true;
            }
        }
        return false;
    }
    private static void AddControls(List<InputControl> result, string path, InputDevice device)
    {
        if (device == null) return;
        var all = device.allControls;
        for (int i = 0; i < all.Count; i++) if (InputControlPath.Matches(path, all[i])) result.Add(all[i]);
    }
    private static void Restore(OwnedBinding item)
    {
        InputActionRebindingExtensions.RemoveBindingOverride(item.Action, item.Index);
        InputActionRebindingExtensions.ApplyBindingOverride(item.Action, item.Index, item.Original);
    }
    private static bool StillOwned(OwnedBinding item, InputBinding current)
        => current.id.ToString() == item.Id && current.overridePath == "" &&
            current.overrideProcessors == item.Original.overrideProcessors && current.overrideInteractions == item.Original.overrideInteractions;

    internal void Release()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            var item = owned[i];
            try
            {
                var current = item.Action.bindings[item.Index];
                if (StillOwned(item, current)) Restore(item);
                owned.RemoveAt(i);
            }
            catch (Exception error) { if (!warned) logger.LogWarning("Binding restoration will retry: " + error.Message); warned = true; }
        }
    }
    internal void Dispose() { current = null; try { hook.UnpatchSelf(); } finally { Release(); } }
}
