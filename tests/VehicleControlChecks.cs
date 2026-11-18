using System;
using System.IO;

internal static class VehicleControlChecks
{
    internal static void Run(Action<string, bool> check)
    {
        var preferences = new VehiclePreferences();
        preferences.Set(VehicleSetting.Power, 2);
        preferences.Set(VehicleSetting.Brakes, 1.5f);
        float torque = 200, brake = 4000;
        var group = new VehicleValueGroup();
        group.Add(VehicleSetting.Power, () => torque, value => torque = value);
        group.Add(VehicleSetting.Brakes, () => brake, value => brake = value);
        group.Apply(preferences);
        group.Apply(preferences);
        check("group modifiers do not compound", torque == 400 && brake == 6000);
        preferences.Set(VehicleSetting.Power, 3);
        group.Apply(preferences);
        check("live slider edits use original baseline", torque == 600);
        group.Restore();
        group.Restore();
        check("group disable restores once", torque == 200 && brake == 4000);

        group.Apply(preferences);
        torque = 515;
        bool failed = false;
        try { group.Apply(preferences); } catch (InvalidOperationException) { failed = true; }
        check("unknown writer stops group and preserves its value", failed && torque == 515);
        check("unknown writer rolls back other owned values", brake == 4000);

        torque = 200;
        group = new VehicleValueGroup();
        group.Add(VehicleSetting.Power, () => torque, value => torque = value);
        group.Add(VehicleSetting.Brakes, () => brake, value => throw new InvalidOperationException("setter failed"));
        failed = false;
        try { group.Apply(preferences); } catch (InvalidOperationException) { failed = true; }
        check("partial write failure restores earlier settings", failed && torque == 200 && brake == 4000);

        bool wheelAlive = true;
        float grip = 1;
        group = new VehicleValueGroup();
        group.Add(VehicleSetting.Grip, () => wheelAlive ? grip : throw new InvalidOperationException("destroyed"), value => grip = value);
        group.Add(VehicleSetting.Power, () => torque, value => torque = value);
        preferences.Set(VehicleSetting.Grip, 1.5f);
        group.Apply(preferences);
        wheelAlive = false;
        failed = false;
        try { group.Restore(); } catch (InvalidOperationException) { failed = true; }
        check("destroyed wheel does not prevent remaining cleanup", failed && torque == 200 && grip == 1.5f);

        float damageMultiplier = 0.7f;
        int writes = 0;
        var protection = new VehicleProtection(() => damageMultiplier, value => { damageMultiplier = value; writes++; });
        protection.Enable();
        protection.Enable();
        check("protection sets zero once", protection.Active && damageMultiplier == 0 && writes == 1);
        protection.Disable();
        protection.Disable();
        check("protection restores legitimate nonunit baseline", !protection.Active && damageMultiplier == 0.7f && writes == 2);
        protection.Enable();
        damageMultiplier = 0.4f;
        failed = false;
        try { protection.Enable(); } catch (InvalidOperationException) { failed = true; }
        protection.Disable();
        check("protection relinquishes unknown external write", failed && damageMultiplier == 0.4f);
        damageMultiplier = 0;
        protection = new VehicleProtection(() => damageMultiplier, value => damageMultiplier = value);
        protection.Enable();
        protection.Disable();
        check("protection preserves existing zero baseline", damageMultiplier == 0);
        failed = false;
        try { new VehicleProtection(() => float.NaN, _ => { }); } catch (InvalidOperationException) { failed = true; }
        check("invalid protection baseline rejected", failed);

        damageMultiplier = 0.7f;
        protection = new VehicleProtection(() => damageMultiplier, value => { damageMultiplier = value; if (value == 0) throw new InvalidOperationException("after write"); });
        try { protection.Enable(); } catch (InvalidOperationException) { }
        protection.Disable();
        check("partially successful protection setter can be restored", damageMultiplier == 0.7f);

        var car = new ControlCar();
        int resolutions = 0;
        var controls = new VehicleControls(preferences, () => { resolutions++; return car; }, _ => { });
        controls.Update(false);
        check("fresh session controls start disabled", !controls.Modifiers && !controls.GodMode && resolutions == 0);
        controls.Modifiers = true;
        controls.GodMode = true;
        controls.Update(false);
        check("both controls can run together", car.Applies == 1 && car.Protected);
        controls.Modifiers = false;
        controls.Update(false);
        check("performance off preserves protection and preferences", car.Releases == 1 && car.Protected && preferences.Get(VehicleSetting.Power) == 3);
        controls.Modifiers = true;
        controls.Update(true);
        check("flight pauses only performance", car.Applies == 1 && car.Releases == 2 && car.Protected);
        controls.Update(false);
        check("leaving flight resumes performance", car.Applies == 2);
        controls.GodMode = false;
        controls.Update(false);
        check("protection off keeps performance active", car.Applies == 3 && !car.Protected);
        controls.GodMode = true;
        controls.RestoreStock();
        controls.Update(false);
        check("restore stock preserves protection", !controls.Modifiers && car.Protected && preferences.Get(VehicleSetting.Power) == 1);
        car.Eligible = false;
        controls.Update(false);
        check("car loss resets both switches and disposes original", !controls.Modifiers && !controls.GodMode && car.Disposals == 1);
        controls.Update(false);
        check("car loss does not autoenable replacement", resolutions == 1);
        car = new ControlCar { ThrowApply = true };
        controls.Modifiers = true;
        controls.Update(false);
        check("apply failure stops and cleans up", !controls.Modifiers && !controls.GodMode && car.Disposals == 1 && controls.Status.StartsWith("Vehicle controls stopped:"));
        controls = new VehicleControls(preferences, () => null, _ => { });
        controls.GodMode = true;
        controls.Update(false);
        check("unavailable car disables requested protection", !controls.GodMode && controls.Status.Contains("unavailable"));
        car = new ControlCar();
        controls = new VehicleControls(preferences, () => car, _ => { });
        controls.Modifiers = controls.GodMode = true;
        controls.Update(false);
        controls.Dispose();
        controls.Dispose();
        check("unload cleanup idempotent", car.Disposals == 1 && !controls.Modifiers && !controls.GodMode);

        string directory = Path.Combine(Path.GetTempPath(), "trainer-vehicle-" + Guid.NewGuid());
        try
        {
            var store = new VehiclePreferenceStore(Path.Combine(directory, "nested", "vehicle.cfg"));
            var loaded = store.Load(out int rejected);
            check("missing preference file uses stock", rejected == 0 && loaded.Get(VehicleSetting.Grip) == 1);
            loaded.Set(VehicleSetting.Power, 2.5f);
            store.Save(loaded);
            loaded = store.Load(out rejected);
            check("vehicle preferences roundtrip", rejected == 0 && loaded.Get(VehicleSetting.Power) == 2.5f);
            string text = File.ReadAllText(Path.Combine(directory, "nested", "vehicle.cfg"));
            check("activation not persisted", !text.Contains("GodMode") && !text.Contains("Modifiers"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class ControlCar : IVehicleControlsTarget
    {
        public bool Eligible { get; set; } = true;
        public bool Protected;
        public int Applies, Releases, Disposals;
        public bool ThrowApply;
        public void Apply(VehiclePreferences preferences) { if (ThrowApply) throw new InvalidOperationException("apply failed"); Applies++; }
        public void ReleasePerformance() { Releases++; }
        public void Protect(bool enabled) { Protected = enabled; }
        public void Dispose() { Disposals++; Protected = false; }
    }
}
