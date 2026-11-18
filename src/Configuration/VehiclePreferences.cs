using System;
using System.Globalization;
using System.Text;

internal enum VehicleSetting { Power, SpeedLimit, Grip, Steering, Brakes }

// Preferences only: activation and damage protection must start off each session.
internal sealed class VehiclePreferences
{
    private readonly float[] values = { 1, 1, 1, 1, 1 };
    internal float Get(VehicleSetting setting) => values[Index(setting)];

    internal static float Maximum(VehicleSetting setting) => setting == VehicleSetting.Power || setting == VehicleSetting.SpeedLimit ? 10f :
        setting == VehicleSetting.Steering ? 1.5f : 2f;

    internal bool Set(VehicleSetting setting, float value)
    {
        int index = Index(setting);
        float maximum = Maximum(setting);
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.5f || value > maximum) return false;
        values[index] = value;
        return true;
    }

    internal void Reset()
    {
        for (int i = 0; i < values.Length; i++) values[i] = 1;
    }

    internal VehiclePreferences Copy()
    {
        var result = new VehiclePreferences();
        Array.Copy(values, result.values, values.Length);
        return result;
    }

    internal string Serialize()
    {
        var text = new StringBuilder();
        foreach (VehicleSetting setting in Enum.GetValues(typeof(VehicleSetting)))
            text.Append(setting).Append('=').Append(Get(setting).ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        return text.ToString();
    }

    internal static VehiclePreferences Parse(string text, out int rejected)
    {
        var result = new VehiclePreferences();
        var seen = new bool[5];
        rejected = 0;
        foreach (string line in (text ?? "").Split('\n'))
        {
            string entry = line.Trim();
            if (entry.Length == 0 || entry.StartsWith("#", StringComparison.Ordinal)) continue;
            int separator = entry.IndexOf('=');
            if (separator <= 0) { rejected++; continue; }
            string name = entry.Substring(0, separator).Trim();
            // Do not accept numeric enum spellings or future activation flags.
            if (!Enum.TryParse(name, out VehicleSetting setting) || !Enum.IsDefined(typeof(VehicleSetting), setting) || setting.ToString() != name)
            { rejected++; continue; }
            int index = Index(setting);
            if (seen[index] || !float.TryParse(entry.Substring(separator + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !result.Set(setting, value))
            { rejected++; continue; }
            seen[index] = true;
        }
        return result;
    }

    private static int Index(VehicleSetting setting)
    {
        int index = (int)setting;
        if (index < 0 || index >= 5) throw new ArgumentOutOfRangeException(nameof(setting));
        return index;
    }
}
