using System;
using System.IO;
using System.Text.Json;

internal static class VehicleUserPresetChecks
{
    internal static void Run(Action<string, bool> check)
    {
        var controls = new VehicleControls(new VehiclePreferences(), () => null, _ => { });
        var gearing = new VehicleOverdrive(() => null, _ => { });
        var tuning = new VehicleTuning(controls, gearing, new VehiclePreferences());
        check("preset startup never enables tuning", !controls.Modifiers);
        check("invalid edit never enables tuning", !tuning.Set(VehicleSetting.Power, 4) && !controls.Modifiers);
        check("unchanged edit leaves tuning off", tuning.Set(VehicleSetting.Power, 1) && !controls.Modifiers);
        check("valid changed slider enables tuning", tuning.Set(VehicleSetting.Power, 2) && controls.Modifiers);
        controls.Modifiers = false;
        check("invalid numeric draft leaves tuning off", !tuning.SetText(VehicleSetting.SpeedLimit, "invalid") && !controls.Modifiers);
        check("committing numeric draft enables tuning", tuning.SetText(VehicleSetting.SpeedLimit, "2.25") && controls.Modifiers);
        tuning.SetMode(false);
        check("mode change retains committed old-mode draft", !controls.Modifiers && tuning.SimplePreferences.Get(VehicleSetting.SpeedLimit) == 2.25f);
        tuning.Set(VehicleSetting.Power, 7); tuning.SetGearing(2.5f);
        var custom = tuning.Capture();
        controls.Modifiers = false;
        tuning.ApplyFactoryPreset(VehiclePreset.Trail);
        check("factory preset enables tuning", controls.Modifiers && tuning.FactoryPreset == VehiclePreset.Trail);
        tuning.SetText(VehicleSetting.Power, "2"); // Focus/blur with unchanged value must not destroy Custom.
        tuning.ApplyFactoryPreset(VehiclePreset.Fast);
        tuning.SetMode(true); tuning.SetMode(false);
        controls.Modifiers = false;
        check("Custom survives multiple presets and both mode switches", tuning.RecallCustom() && tuning.Preferences.Get(VehicleSetting.Power) == 7 && tuning.DetailedGearing == 2.5f && controls.Modifiers);
        tuning.ApplyFactoryPreset(VehiclePreset.Climb);
        tuning.SetText(VehicleSetting.Power, "6.5");
        tuning.ApplyFactoryPreset(VehiclePreset.Stock);
        tuning.RecallCustom();
        check("editing a factory preset creates a new recoverable Custom", tuning.Preferences.Get(VehicleSetting.Power) == 6.5f && tuning.FactoryPreset == null);
        controls.Modifiers = false;
        tuning.SetGearing(2.75f);
        check("gear-speed edit also enables tuning", controls.Modifiers);
        controls.Modifiers = false;
        tuning.Recall(custom);
        check("loading complete snapshot enables tuning and restores gearing", controls.Modifiers && tuning.Preferences.Get(VehicleSetting.Power) == 7 && tuning.DetailedGearing == 2.5f);
        custom.CopyPreferences().Set(VehicleSetting.Power, 9);
        check("snapshot read cannot mutate saved values", custom.CopyPreferences().Get(VehicleSetting.Power) == 7);
        tuning.SetMode(true);
        bool wrongMode = false;
        try { tuning.Recall(custom); } catch (ArgumentException) { wrongMode = true; }
        check("wrong-mode snapshot is refused without enabling", wrongMode && !controls.Modifiers);
        var simple = tuning.Capture();
        tuning.Set(VehicleSetting.Power, 3);
        check("snapshot capture does not alias live settings", simple.CopyPreferences().Get(VehicleSetting.Power) == 2);

        string root = Path.Combine(Path.GetTempPath(), "oth-user-presets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "presets.json");
            var store = new VehicleUserPresets(path);
            store.Load(out int rejected);
            check("missing preset file is empty", rejected == 0 && !store.TryGet(true, 0, out _));
            for (int slot = 0; slot < VehicleUserPresets.SlotCount; slot++)
            { store.Save(true, slot, simple); store.Save(false, slot, custom); }
            var loaded = new VehicleUserPresets(path);
            loaded.Load(out rejected);
            check("saved presets survive a new store instance", rejected == 0);
            for (int slot = 0; slot < VehicleUserPresets.SlotCount; slot++)
            {
                check("simple saved slot isolated " + slot, loaded.TryGet(true, slot, out var value) && value.Simple && value.CopyPreferences().Get(VehicleSetting.SpeedLimit) == 2.25f);
                check("detailed saved slot includes high-gear speed " + slot, loaded.TryGet(false, slot, out value) && !value.Simple && value.Gearing == 2.5f && value.CopyPreferences().Get(VehicleSetting.Power) == 7);
            }
            loaded.Save(false, 1, new VehicleTuneSnapshot(false, new VehiclePreferences(), 1.5f));
            loaded.Load(out rejected);
            check("overwriting one slot preserves its neighbours", loaded.TryGet(false, 1, out var replaced) && replaced.Gearing == 1.5f && loaded.TryGet(false, 0, out var neighbour) && neighbour.Gearing == 2.5f);
            check("preset update preserves previous file backup", File.Exists(path + ".bak"));
            string text = File.ReadAllText(path);
            check("preset files do not contain activation flags", !text.Contains("Modifiers") && !text.Contains("GodMode") && !text.Contains("Active"));
            tuning.RestoreStock();
            check("Restore stock never erases saved presets", loaded.TryGet(true, 0, out _) && loaded.TryGet(false, 0, out _));

            var document = new VehicleUserPresets.PresetFile { Version = 1 };
            document.Entries.Add(new() { Simple = false, Slot = 1, Preferences = custom.CopyPreferences().Serialize(), HighGearSpeedBoost = 2 });
            document.Entries.Add(new() { Simple = false, Slot = 1, Preferences = custom.CopyPreferences().Serialize() }); // duplicate
            document.Entries.Add(new() { Simple = true, Slot = 2, Preferences = "Power=2\n" }); // partial
            document.Entries.Add(new() { Simple = true, Slot = 4, Preferences = simple.CopyPreferences().Serialize() });
            document.Entries.Add(new() { Simple = false, Slot = 2, Preferences = custom.CopyPreferences().Serialize(), HighGearSpeedBoost = 4 });
            document.Entries.Add(new() { Simple = true, Slot = 3, Preferences = custom.CopyPreferences().Serialize() }); // simple bounds
            File.WriteAllText(path, JsonSerializer.Serialize(document));
            loaded.Load(out rejected);
            check("corrupt entries are rejected independently", rejected == 5 && loaded.TryGet(false, 0, out _) && !loaded.TryGet(true, 0, out _));
            File.WriteAllText(path, "{bad json");
            bool malformed = false;
            try { loaded.Load(out _); } catch (JsonException) { malformed = true; }
            check("malformed file never partially replaces memory or disk", malformed && loaded.TryGet(false, 0, out _) && File.ReadAllText(path) == "{bad json");
            File.WriteAllText(path, "{\"Version\":2,\"Entries\":[]}");
            bool unsupported = false;
            try { loaded.Load(out _); } catch (InvalidDataException) { unsupported = true; }
            check("unsupported preset format is refused", unsupported);
            bool badSlot = false, badMode = false;
            try { loaded.TryGet(true, 3, out _); } catch (ArgumentOutOfRangeException) { badSlot = true; }
            try { loaded.Save(true, 0, custom); } catch (ArgumentException) { badMode = true; }
            check("slot and mode inputs are validated", badSlot && badMode);

            string blocker = Path.Combine(root, "blocker");
            File.WriteAllText(blocker, "file, not a directory");
            var failing = new VehicleUserPresets(Path.Combine(blocker, "presets.json"));
            bool failed = false;
            try { failing.Save(true, 0, simple); } catch (IOException) { failed = true; }
            check("failed disk save never claims a saved slot", failed && !failing.TryGet(true, 0, out _));
        }
        finally { Directory.Delete(root, true); }
    }
}
