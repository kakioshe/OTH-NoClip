using System;
using System.Globalization;

internal static class VehicleChecks
{
    internal static void Run(Action<string, bool> check)
    {
        var owned = new VehicleModifierValue(100);
        check("vehicle first scale", owned.TryApply(100, 2, out float value) && value == 200);
        check("vehicle scale does not compound", owned.TryApply(value, 3, out value) && value == 300);
        check("vehicle repeated scale stable", owned.TryApply(value, 3, out value) && value == 300);
        check("vehicle stock restores legitimate baseline", owned.TryRestore(value, out value) && value == 100);
        check("vehicle cleanup idempotent", !owned.TryRestore(value, out value) && value == 100);
        // Model release -> legitimate upgrade/damage writer -> capture new baseline.
        owned = new VehicleModifierValue(80);
        check("vehicle new baseline after game writer", owned.TryApply(80, 2, out value) && value == 160);
        check("vehicle cleanup preserves updated baseline", owned.TryRestore(value, out value) && value == 80);

        owned = new VehicleModifierValue(100);
        owned.TryApply(100, 2, out _);
        check("vehicle unknown writer is preserved", !owned.TryApply(75, 2, out value) && value == 75 && owned.LostOwnership && !owned.Applied);
        check("vehicle cleanup never overwrites unknown writer", !owned.TryRestore(75, out value) && value == 75);
        check("vehicle ownership loss is sticky", !owned.TryApply(200, 2, out value) && value == 200);
        owned = new VehicleModifierValue(100);
        check("vehicle writer before activation is preserved", !owned.TryApply(90, 2, out value) && value == 90);
        owned = new VehicleModifierValue(100);
        owned.TryApply(100, 2, out _);
        check("vehicle writer just before disable is preserved", !owned.TryRestore(70, out value) && value == 70 && owned.LostOwnership);

        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
        {
            owned = new VehicleModifierValue(100);
            check("vehicle rejects invalid multiplier " + invalid, !owned.TryApply(100, invalid, out value) && value == 100 && !owned.Applied);
        }
        owned = new VehicleModifierValue(float.MaxValue);
        check("vehicle rejects overflow", !owned.TryApply(float.MaxValue, 2, out value) && value == float.MaxValue);
        owned = new VehicleModifierValue(0);
        check("vehicle zero baseline preserved", owned.TryApply(0, 3, out value) && value == 0);
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f })
        {
            bool rejected = false;
            try { new VehicleModifierValue(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
            check("vehicle rejects invalid baseline " + invalid, rejected);
        }

        var preferences = new VehiclePreferences();
        foreach (VehicleSetting setting in Enum.GetValues(typeof(VehicleSetting)))
        {
            check("vehicle default " + setting, preferences.Get(setting) == 1);
            check("vehicle lower range " + setting, preferences.Set(setting, 0.5f));
            check("vehicle rejects nonfinite " + setting, !preferences.Set(setting, float.NaN) && !preferences.Set(setting, float.PositiveInfinity));
            check("vehicle rejects out of range " + setting, !preferences.Set(setting, 0.49f) && !preferences.Set(setting, VehiclePreferences.Maximum(setting) + 0.01f));
        }
        check("steering upper range", preferences.Set(VehicleSetting.Steering, 1.5f) && !preferences.Set(VehicleSetting.Steering, 1.51f));
        check("grip upper range", preferences.Set(VehicleSetting.Grip, 2) && !preferences.Set(VehicleSetting.Grip, 2.01f));
        check("brakes upper range", preferences.Set(VehicleSetting.Brakes, 2) && !preferences.Set(VehicleSetting.Brakes, 2.01f));
        check("power upper range", preferences.Set(VehicleSetting.Power, 3));
        check("speed upper range", preferences.Set(VehicleSetting.SpeedLimit, 3));
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            preferences.Set(VehicleSetting.Power, 1.75f);
            var copy = VehiclePreferences.Parse(preferences.Serialize(), out int rejected);
            check("vehicle preferences round trip across cultures", rejected == 0 && copy.Serialize() == preferences.Serialize());
        }
        finally { CultureInfo.CurrentCulture = previous; }
        var parsed = VehiclePreferences.Parse("Power=2\nPower=3\nGrip=NaN\nSteering=2\nBrakes=1.5\nSpeedLimit=Infinity\n0=2\nGodMode=true\nbroken", out int errors);
        check("vehicle rejects malformed duplicate and activation settings", errors == 7);
        check("vehicle parse preserves valid values", parsed.Get(VehicleSetting.Power) == 2 && parsed.Get(VehicleSetting.Brakes) == 1.5f && parsed.Get(VehicleSetting.Grip) == 1);
        check("vehicle null config is stock", VehiclePreferences.Parse(null, out errors).Get(VehicleSetting.Power) == 1 && errors == 0);
        parsed.Reset();
        check("vehicle reset restores all preferences", parsed.Serialize() == new VehiclePreferences().Serialize());
        bool invalidSetting = false;
        try { preferences.Set((VehicleSetting)99, 1); } catch (ArgumentOutOfRangeException) { invalidSetting = true; }
        check("vehicle rejects unknown setting index", invalidSetting);
    }
}
