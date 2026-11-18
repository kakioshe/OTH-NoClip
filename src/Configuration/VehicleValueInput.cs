using System.Globalization;

internal static class VehicleValueInput
{
    internal static bool TryApply(VehiclePreferences preferences, VehicleSetting setting, string text, float minimum = 0.5f, float maximum = float.PositiveInfinity) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
        value >= minimum && value <= maximum &&
        preferences.Set(setting, value);
}
