using System;

internal interface IVehicleOverdriveTarget : IDisposable
{
    bool Eligible { get; }
    float Speed { get; }
    void SetMultiplier(float multiplier);
    void Pause(bool paused);
}

// Session-only gearing, deliberately independent of saved performance presets.
internal sealed class VehicleOverdrive : IDisposable
{
    internal const float StoppedSpeed = 0.1f;
    private readonly Func<IVehicleOverdriveTarget> resolve;
    private readonly Action<string> report;
    private IVehicleOverdriveTarget target;
    internal float Multiplier { get; private set; } = 1;
    internal string Status { get; private set; } = "";

    internal VehicleOverdrive(Func<IVehicleOverdriveTarget> resolve, Action<string> report)
    { this.resolve = resolve; this.report = report; }

    internal bool Select(float multiplier)
    {
        if (!ValidMultiplier(multiplier)) return false;
        try
        {
            target ??= resolve();
            if (target == null || !target.Eligible) throw new InvalidOperationException("An authoritative local single-player car is required.");
            float speed = target.Speed;
            if (!float.IsFinite(speed) || speed < 0 || speed > StoppedSpeed)
            { Status = "Stop the car before changing High-range overdrive."; return false; }
            target.SetMultiplier(multiplier);
            Multiplier = multiplier;
            Status = "";
            return true;
        }
        catch (Exception error) { Fail(error.Message); return false; }
    }

    internal void Update(bool flightActive)
    {
        try
        {
            if (target == null) return;
            if (!target.Eligible) { Fail("Vehicle changed or became unavailable."); return; }
            target.Pause(flightActive);
            if (flightActive && Multiplier > 1) Status = "High-range overdrive paused during flight.";
            else if (Status == "High-range overdrive paused during flight.") Status = "";
        }
        catch (Exception error) { Fail(error.Message); }
    }

    internal void Fail(string reason)
    {
        string message = "High-range overdrive stopped: " + reason;
        try { Reset(); }
        catch (Exception error) { message += " Cleanup: " + error.Message; }
        Status = message;
        report(message);
    }

    internal void Reset()
    {
        Multiplier = 1;
        var previous = target;
        target = null;
        Status = "";
        previous?.Dispose();
    }

    internal static bool ValidMultiplier(float value) => float.IsFinite(value) && value >= 1 && value <= 3;
    public void Dispose() => Reset();
}

// One effective reduction. A known writer is allowed to establish a new baseline
// only after the previous owned value has been released successfully.
internal sealed class OverdriveRatio
{
    private readonly Func<float> read;
    private readonly Action<float> write;
    private VehicleModifierValue owned;

    internal OverdriveRatio(Func<float> read, Action<float> write) { this.read = read; this.write = write; }

    internal void BeforeWriter() => Release();

    internal void AfterWriter(float multiplier, bool inputEnabled, bool lowRange, int requestedGear, int engagedGear, bool shifting)
    {
        if (!VehicleOverdrive.ValidMultiplier(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        // Always release first, including paths where the game skipped its range write.
        // The prefix normally already did so; this also makes repeated calls stable.
        if (multiplier == 1 || !inputEnabled || lowRange || requestedGear <= 0 || engagedGear <= 0 || shifting)
        { Release(); return; }
        float current = read();
        if (!float.IsFinite(current) || current <= 0) throw new InvalidOperationException("Invalid differential reduction.");
        if (current / multiplier <= 0) throw new InvalidOperationException("Differential reduction underflow.");
        owned ??= new VehicleModifierValue(current);
        if (!owned.TryApply(current, 1 / multiplier, out float next))
            throw new InvalidOperationException("Differential changed outside the known game writer.");
        write(next);
    }

    internal void Release()
    {
        if (owned == null) return;
        var previous = owned;
        owned = null;
        if (!previous.TryRestore(read(), out float baseline))
            throw new InvalidOperationException("Differential ownership lost; external value preserved.");
        write(baseline);
    }
}
