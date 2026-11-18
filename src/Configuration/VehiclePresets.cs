using System;

internal enum VehiclePreset { Stock, Trail, Climb, Fast }

// Presets change saved tuning only. They never enable modifiers or other assists.
internal static class VehiclePresets
{
    internal static void Apply(VehiclePreferences preferences, VehiclePreset preset)
    {
        float[] values = preset switch
        {
            VehiclePreset.Stock => new[] { 1f, 1f, 1f, 1f, 1f },
            VehiclePreset.Trail => new[] { 2f, 1.5f, 1.5f, 1.1f, 1.5f },
            VehiclePreset.Climb => new[] { 3f, 1f, 2f, 1f, 2f },
            VehiclePreset.Fast => new[] { 5f, 5f, 1.5f, 0.75f, 2f },
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };
        for (int i = 0; i < values.Length; i++) preferences.Set((VehicleSetting)i, values[i]);
    }
}
