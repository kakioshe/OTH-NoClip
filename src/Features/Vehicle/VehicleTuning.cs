using System;

// Simple is a separate set of user preferences. Detailed values are never
// overwritten by an automatic calculation or a mode change.
internal sealed class VehicleTuning
{
    private readonly VehicleControls controls;
    private readonly VehicleOverdrive overdrive;
    private VehiclePreferences applied;
    private bool ownsGearing;
    private float selectedGearing = 1;
    private bool paused;
    private VehicleTuneSnapshot customDetailed;
    internal VehiclePreset? FactoryPreset { get; private set; }
    internal bool Pending { get; private set; }
    internal float DetailedGearing { get; private set; } = 1;
    internal bool Simple { get; private set; } = true;
    internal VehiclePreferences SimplePreferences { get; }
    internal VehiclePreferences Preferences => Simple ? SimplePreferences : controls.Preferences;
    internal string Status { get; private set; } = "";
    internal string State => !controls.Modifiers ? "OFF" : paused ? "PAUSED" : controls.PerformanceActive ? "ON" : "WAITING";
    internal string ActivationText => !controls.Modifiers ? "Stock performance. Your tuning is not applied." : paused ? "Tuning paused during flight." :
        Pending ? controls.PerformanceActive ? "Tuning is on. Stop to apply the pending change." : "Stop the car to apply your tuning." :
        controls.PerformanceActive ? "Your tuning is applied." : "Waiting for your car.";

    internal VehicleTuning(VehicleControls controls, VehicleOverdrive overdrive, VehiclePreferences simplePreferences)
    {
        this.controls = controls; this.overdrive = overdrive; SimplePreferences = simplePreferences;
        foreach (VehicleSetting setting in Enum.GetValues(typeof(VehicleSetting)))
            if (!SimpleVehicleTuning.Valid(setting, simplePreferences.Get(setting))) simplePreferences.Set(setting, 1);
    }

    internal float Minimum(VehicleSetting setting) => Simple && setting == VehicleSetting.SpeedLimit ? 1 : 0.5f;
    internal float Maximum(VehicleSetting setting) => Simple ? SimpleVehicleTuning.Maximum(setting) : VehiclePreferences.Maximum(setting);
    internal bool Set(VehicleSetting setting, float value)
    {
        float previous = Preferences.Get(setting);
        if (Simple && !SimpleVehicleTuning.Valid(setting, value) || !Preferences.Set(setting, value)) return false;
        if (value != previous) Edited();
        return true;
    }
    private void Edited() { if (!Simple) FactoryPreset = null; controls.Modifiers = true; }
    internal bool SetText(VehicleSetting setting, string text)
    {
        float previous = Preferences.Get(setting);
        if (!VehicleValueInput.TryApply(Preferences, setting, text, Minimum(setting), Maximum(setting))) return false;
        if (Preferences.Get(setting) != previous) Edited();
        return true;
    }
    internal bool SetGearing(float value)
    {
        if (!VehicleOverdrive.ValidMultiplier(value)) return false;
        if (value != DetailedGearing) { DetailedGearing = value; Edited(); }
        return true;
    }

    internal VehicleTuneSnapshot Capture() => new(Simple, Preferences, Simple ? 1 : DetailedGearing);

    internal void Recall(VehicleTuneSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Simple != Simple) throw new ArgumentException("Preset mode does not match.", nameof(snapshot));
        var values = snapshot.CopyPreferences();
        foreach (VehicleSetting setting in Enum.GetValues(typeof(VehicleSetting))) Preferences.Set(setting, values.Get(setting));
        if (!Simple) DetailedGearing = snapshot.Gearing;
        Edited();
    }

    internal void ApplyFactoryPreset(VehiclePreset preset)
    {
        if (Simple) throw new InvalidOperationException("Factory presets use Detailed mode.");
        if (FactoryPreset == null) customDetailed = Capture();
        VehiclePresets.Apply(Preferences, preset);
        FactoryPreset = preset;
        controls.Modifiers = true;
    }

    internal bool RecallCustom()
    {
        if (FactoryPreset == null) { controls.Modifiers = true; return true; }
        if (customDetailed == null) return false;
        Recall(customDetailed);
        return true;
    }

    internal void SetMode(bool simple)
    {
        if (Simple == simple) return;
        // Switching views must not silently apply a different tune at speed.
        controls.Modifiers = false;
        controls.Update(false);
        ReleaseGearing();
        Simple = simple;
        Status = "Settings kept. Change a value or click Tuning OFF to apply them.";
    }

    internal void RestoreStock()
    {
        controls.RestoreStock();
        SimplePreferences.Reset();
        DetailedGearing = 1;
        FactoryPreset = null;
        ReleaseGearing();
    }

    internal void Update(bool flightActive)
    {
        overdrive.Update(flightActive);
        Pending = false; paused = false;
        if (ownsGearing && selectedGearing != overdrive.Multiplier)
        { Stop("Automatic speed adjustment unavailable. See the game log."); return; }
        if (!controls.Modifiers)
        {
            if (ownsGearing || applied != null) ReleaseGearing();
            controls.Update(flightActive);
            return;
        }
        if (flightActive)
        {
            controls.Update(true, applied);
            paused = true;
            Status = "Vehicle tuning paused during flight.";
            return;
        }

        float speed = Simple ? SimplePreferences.Get(VehicleSetting.SpeedLimit) : DetailedGearing;
        if (overdrive.Multiplier != speed)
        {
            if (!overdrive.Select(speed))
            {
                if (!overdrive.WaitingForStop) { Stop("Automatic speed adjustment unavailable. See the game log."); return; }
                ownsGearing = true;
                selectedGearing = overdrive.Multiplier;
                Pending = true;
                Status = Simple ? "Stop the car to apply the new top speed." : "Stop the car to apply the new high-gear speed boost.";
                // Retain the entire previous tune until its gearing can change.
                // A first activation waits with stock performance and God mode independent.
                controls.Update(applied == null, applied);
                if (!controls.Modifiers) ReleaseGearing();
                return;
            }
            ownsGearing = true;
            selectedGearing = speed;
        }
        applied = Simple ? SimpleVehicleTuning.Calculate(SimplePreferences) : controls.Preferences.Copy();
        controls.Update(false, applied);
        if (!controls.Modifiers) { ReleaseGearing(); Status = controls.Status; return; }
        Status = "";
    }

    private void Stop(string reason)
    {
        controls.Modifiers = false;
        controls.Update(false);
        ReleaseGearing();
        Status = "Vehicle tuning stopped: " + reason;
    }

    private void ReleaseGearing()
    {
        applied = null; ownsGearing = false; selectedGearing = 1;
        Pending = false; paused = false;
        try { overdrive.Reset(); Status = ""; }
        catch (Exception error) { Status = "Vehicle tuning cleanup: " + error.Message; }
    }
}

internal static class SimpleVehicleTuning
{
    internal static float Maximum(VehicleSetting setting) => setting == VehicleSetting.Power || setting == VehicleSetting.SpeedLimit ? 3 : VehiclePreferences.Maximum(setting);
    internal static bool Valid(VehicleSetting setting, float value) => float.IsFinite(value) &&
        value >= (setting == VehicleSetting.SpeedLimit ? 1 : 0.5f) && value <= Maximum(setting);

    internal static VehiclePreferences Calculate(VehiclePreferences preferences)
    {
        foreach (VehicleSetting setting in Enum.GetValues(typeof(VehicleSetting)))
            if (!Valid(setting, preferences.Get(setting))) throw new ArgumentOutOfRangeException(nameof(preferences));
        var result = new VehiclePreferences();
        float speed = preferences.Get(VehicleSetting.SpeedLimit);
        // Taller gearing reduces wheel torque by this factor. Compensate it so
        // Acceleration remains a separate request, instead of weakening launch.
        result.Set(VehicleSetting.Power, preferences.Get(VehicleSetting.Power) * speed);
        result.Set(VehicleSetting.SpeedLimit, speed);
        result.Set(VehicleSetting.Grip, preferences.Get(VehicleSetting.Grip));
        result.Set(VehicleSetting.Steering, preferences.Get(VehicleSetting.Steering));
        result.Set(VehicleSetting.Brakes, preferences.Get(VehicleSetting.Brakes));
        return result;
    }
}
