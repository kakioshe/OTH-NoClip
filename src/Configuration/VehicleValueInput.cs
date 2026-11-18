using System.Globalization;

internal static class VehicleValueInput
{
    internal static bool TryApply(VehiclePreferences preferences, VehicleSetting setting, string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
        preferences.Set(setting, value);
}
