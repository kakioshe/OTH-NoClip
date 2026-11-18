using System;

internal static class VehicleTuningChecks
{
    internal static void Run(Action<string, bool> check)
    {
        var simple = new VehiclePreferences();
        simple.Set(VehicleSetting.SpeedLimit, 2);
        simple.Set(VehicleSetting.Power, 1.5f);
        simple.Set(VehicleSetting.Grip, 1.7f);
        simple.Set(VehicleSetting.Steering, 0.8f);
        simple.Set(VehicleSetting.Brakes, 1.8f);
        var calculated = SimpleVehicleTuning.Calculate(simple);
        check("simple calculates limiter and power compensation together", calculated.Get(VehicleSetting.SpeedLimit) == 2 && calculated.Get(VehicleSetting.Power) == 3);
        check("simple handling values are retained independently", calculated.Get(VehicleSetting.Grip) == 1.7f && calculated.Get(VehicleSetting.Steering) == 0.8f && calculated.Get(VehicleSetting.Brakes) == 1.8f);
        check("simple calculation preserves source acceleration preference", simple.Get(VehicleSetting.Power) == 1.5f);
        simple.Set(VehicleSetting.SpeedLimit, 3); simple.Set(VehicleSetting.Power, 3);
        check("simple maximum stays within power bounds", SimpleVehicleTuning.Calculate(simple).Get(VehicleSetting.Power) == 9);
        foreach (VehicleSetting setting in Enum.GetValues(typeof(VehicleSetting)))
        {
            check("simple rejects nonfinite " + setting, !SimpleVehicleTuning.Valid(setting, float.NaN) && !SimpleVehicleTuning.Valid(setting, float.PositiveInfinity));
            check("simple rejects upper overflow " + setting, !SimpleVehicleTuning.Valid(setting, SimpleVehicleTuning.Maximum(setting) + 0.01f));
        }
        check("simple speed rejects below stock", !SimpleVehicleTuning.Valid(VehicleSetting.SpeedLimit, 0.5f));
        check("simple numeric entry enforces its own speed minimum", !VehicleValueInput.TryApply(simple, VehicleSetting.SpeedLimit, "0.5", 1, 3));
        check("simple numeric entry enforces its own power maximum", !VehicleValueInput.TryApply(simple, VehicleSetting.Power, "4", 0.5f, 3));
        simple.Set(VehicleSetting.Power, 4);
        bool invalid = false;
        try { SimpleVehicleTuning.Calculate(simple); } catch (ArgumentOutOfRangeException) { invalid = true; }
        check("simple invalid stored inputs cannot create an unchecked tune", invalid);

        var car = new ControlsTarget();
        var gears = new OverdriveTarget();
        var detailed = new VehiclePreferences();
        detailed.Set(VehicleSetting.Power, 5); detailed.Set(VehicleSetting.SpeedLimit, 7);
        var controls = new VehicleControls(detailed, () => car, _ => { });
        var overdrive = new VehicleOverdrive(() => gears, _ => { });
        var tuning = new VehicleTuning(controls, overdrive, new VehiclePreferences());
        check("simple is default and starts inactive", tuning.Simple && !controls.Modifiers && overdrive.Multiplier == 1);
        check("inactive tuning visibly reports OFF", tuning.State == "OFF" && tuning.ActivationText.Contains("not applied"));
        check("simple and detailed use independent preference objects", tuning.Preferences != detailed && detailed.Get(VehicleSetting.Power) == 5);
        check("simple rejects out of bounds entry without changing value", !tuning.Set(VehicleSetting.SpeedLimit, 0.5f) && tuning.Preferences.Get(VehicleSetting.SpeedLimit) == 1);
        controls.Modifiers = true;
        tuning.Update(false);
        check("simple stock tune ignores saved detailed power and speed", car.Power == 1 && car.Speed == 1 && gears.Selects == 0);
        check("applied tuning visibly reports ON", tuning.State == "ON" && controls.PerformanceActive);
        tuning.Set(VehicleSetting.SpeedLimit, 2); tuning.Set(VehicleSetting.Power, 1.5f);
        tuning.Update(false);
        check("simple activation coordinates power limiter and gearing", controls.Modifiers && car.Power == 3 && car.Speed == 2 && gears.Multiplier == 2);
        int selected = gears.Selects;
        tuning.Update(false); tuning.Update(false);
        check("simple stable tune never reselects gearing", gears.Selects == selected);
        check("simple calculation never rewrites detailed values", detailed.Get(VehicleSetting.Power) == 5 && detailed.Get(VehicleSetting.SpeedLimit) == 7);
        gears.Speed = 2;
        tuning.Set(VehicleSetting.Power, 2);
        tuning.Update(false);
        check("simple acceleration edit applies while moving without changing gearing", car.Power == 4 && gears.Selects == selected && gears.Multiplier == 2);
        tuning.Set(VehicleSetting.SpeedLimit, 3);
        tuning.Set(VehicleSetting.Grip, 2);
        tuning.Update(false);
        check("simple pending speed edit keeps complete previous tune", car.Power == 4 && car.Speed == 2 && car.Grip == 1 && gears.Multiplier == 2 && tuning.Status.Contains("Stop"));
        check("pending edit distinguishes existing active tuning", tuning.State == "ON" && tuning.Pending && tuning.ActivationText.Contains("pending"));
        gears.Speed = 0;
        tuning.Update(false);
        check("simple applies complete pending tune after stop", car.Power == 6 && car.Speed == 3 && car.Grip == 2 && gears.Multiplier == 3 && tuning.Status == "");
        tuning.Update(true);
        check("simple flight pauses performance and gearing", gears.Paused && car.Releases > 0 && tuning.Status.Contains("flight") && controls.Modifiers);
        check("flight visibly reports PAUSED instead of ON", tuning.State == "PAUSED" && !controls.PerformanceActive);
        tuning.Update(false);
        check("simple flight resume retains computed tune", !gears.Paused && car.Speed == 3 && car.Power == 6 && tuning.Status == "");
        controls.Modifiers = false; tuning.Update(false);
        check("simple inactive releases automatic gearing", overdrive.Multiplier == 1 && gears.Disposals == 1);

        // A first activation while moving must not apply only the power/limiter half.
        car = new ControlsTarget(); gears = new OverdriveTarget { Speed = 4 };
        controls = new VehicleControls(new VehiclePreferences(), () => car, _ => { });
        overdrive = new VehicleOverdrive(() => gears, _ => { });
        tuning = new VehicleTuning(controls, overdrive, new VehiclePreferences());
        tuning.Set(VehicleSetting.SpeedLimit, 2); controls.Modifiers = controls.GodMode = true;
        tuning.Update(false);
        check("simple first moving activation waits with stock performance", car.Applies == 0 && overdrive.Multiplier == 1 && tuning.Status.Contains("Stop"));
        check("unapplied moving activation visibly reports WAITING", tuning.State == "WAITING" && !controls.PerformanceActive && tuning.Pending);
        check("simple pending activation leaves protection independent", controls.GodMode && car.Protected);
        controls.Modifiers = false; tuning.Update(false);
        check("simple cancel pending request releases captured target", gears.Disposals == 1 && overdrive.Multiplier == 1);

        gears = new OverdriveTarget(); controls.Modifiers = true; tuning.Update(false);
        gears.Eligible = false;
        tuning.Update(false);
        check("simple gearing target loss stops the whole tune", !controls.Modifiers && overdrive.Multiplier == 1 && tuning.Status.Contains("unavailable"));
        check("simple gearing loss preserves God mode on an eligible car", controls.GodMode && car.Protected);
        gears = new OverdriveTarget { ThrowSelect = true };
        controls.Modifiers = true; tuning.Update(false);
        check("simple gearing selection failure cannot leave power-only activation", !controls.Modifiers && overdrive.Multiplier == 1);
        gears = new OverdriveTarget(); car.ThrowApply = true;
        controls.Modifiers = true; tuning.Update(false);
        check("simple performance failure releases calculated gearing", !controls.Modifiers && overdrive.Multiplier == 1 && gears.Disposals == 1);
        car.ThrowApply = false; gears = new OverdriveTarget();
        controls.Modifiers = true; tuning.Update(false);
        tuning.SetMode(false);
        check("mode switch disables activation and releases automatic gearing", !tuning.Simple && !controls.Modifiers && overdrive.Multiplier == 1);
        tuning.Set(VehicleSetting.Power, 8); tuning.Set(VehicleSetting.SpeedLimit, 9);
        tuning.SetGearing(1.5f); controls.Modifiers = true;
        tuning.Update(false);
        check("detailed mode retains independently adjustable original values", car.Power == 8 && car.Speed == 9 && gears.Multiplier == 1.5f && tuning.State == "ON");
        controls.Modifiers = false; tuning.Update(false);
        check("detailed OFF releases gearing and keeps its slider preference", overdrive.Multiplier == 1 && tuning.DetailedGearing == 1.5f && tuning.State == "OFF");
        check("detailed gearing slider rejects invalid values", !tuning.SetGearing(3.1f) && !tuning.SetGearing(float.NaN) && tuning.DetailedGearing == 1.5f);
        tuning.SetMode(true);
        check("switching to simple preserves previous simple preference values", tuning.Preferences.Get(VehicleSetting.SpeedLimit) == 2);
        check("switching to simple preserves detailed preference values", controls.Preferences.Get(VehicleSetting.Power) == 8 && controls.Preferences.Get(VehicleSetting.SpeedLimit) == 9);
        controls.GodMode = true;
        tuning.RestoreStock();
        check("Restore stock resets both profiles and leaves protection independent", tuning.SimplePreferences.Serialize() == new VehiclePreferences().Serialize() && controls.Preferences.Serialize() == new VehiclePreferences().Serialize() && controls.GodMode && !controls.Modifiers);
        var stored = new VehiclePreferences(); stored.Set(VehicleSetting.Power, 9); stored.Set(VehicleSetting.SpeedLimit, 7);
        tuning = new VehicleTuning(controls, overdrive, stored);
        check("simple load normalizes unsupported stored tuning", stored.Get(VehicleSetting.Power) == 1 && stored.Get(VehicleSetting.SpeedLimit) == 1);
        var saved = tuning.SimplePreferences.Serialize();
        check("simple preferences round trip without activation", VehiclePreferences.Parse(saved, out int rejected).Serialize() == saved && rejected == 0 && !controls.Modifiers);
    }

    private sealed class ControlsTarget : IVehicleControlsTarget
    {
        public bool Eligible => true;
        internal float Power, Speed, Grip;
        internal int Applies, Releases;
        internal bool Protected, ThrowApply;
        public void Apply(VehiclePreferences preferences)
        {
            if (ThrowApply) throw new Exception("performance failed");
            Applies++; Power = preferences.Get(VehicleSetting.Power); Speed = preferences.Get(VehicleSetting.SpeedLimit); Grip = preferences.Get(VehicleSetting.Grip);
        }
        public void ReleasePerformance() => Releases++;
        public void Protect(bool enabled) => Protected = enabled;
        public void Dispose() { Releases++; }
    }

    private sealed class OverdriveTarget : IVehicleOverdriveTarget
    {
        public bool Eligible { get; set; } = true;
        public float Speed { get; set; }
        internal float Multiplier = 1;
        internal bool Paused, ThrowSelect;
        internal int Selects, Disposals;
        public void SetMultiplier(float value)
        { if (ThrowSelect) throw new Exception("gearing unavailable"); Selects++; Multiplier = value; }
        public void Pause(bool paused) => Paused = paused;
        public void Dispose() { Disposals++; }
    }
}
