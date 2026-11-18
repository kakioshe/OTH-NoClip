using System;
using System.Collections.Generic;

internal sealed class VehicleValueGroup
{
    private sealed class Value
    {
        internal VehicleSetting Setting;
        internal Func<float> Read;
        internal Action<float> Write;
        internal VehicleModifierValue Owner;
    }

    private readonly List<Value> values = new();

    internal void Add(VehicleSetting setting, Func<float> read, Action<float> write)
    {
        values.Add(new Value { Setting = setting, Read = read, Write = write, Owner = new VehicleModifierValue(read()) });
    }

    internal void Apply(VehiclePreferences preferences)
    {
        try
        {
            foreach (var value in values)
            {
                float before = value.Read();
                if (!value.Owner.TryApply(before, preferences.Get(value.Setting), out float next))
                    throw new InvalidOperationException("Vehicle settings changed outside the trainer; modifiers stopped.");
                if (before != next) value.Write(next);
                if (value.Read() != next) throw new InvalidOperationException("Vehicle modifier readback failed.");
            }
        }
        catch
        {
            Restore();
            throw;
        }
    }

    // Try every original value even if one original object has already gone away.
    internal string Restore()
    {
        Exception failure = null;
        int restored = 0, external = 0, failed = 0;
        foreach (var value in values)
        {
            try
            {
                if (!value.Owner.Applied) { if (value.Owner.LostOwnership) external++; continue; }
                if (!value.Owner.TryRestore(value.Read(), out float baseline)) { external++; continue; }
                value.Write(baseline);
                if (value.Read() != baseline) throw new InvalidOperationException("Vehicle restoration readback failed.");
                restored++;
            }
            catch (Exception error) { failed++; failure ??= error; }
        }
        string result = $"Restored {restored} values; external changes {external}; unavailable/failed {failed}.";
        if (failure != null) throw new InvalidOperationException(result, failure);
        return result;
    }
}
