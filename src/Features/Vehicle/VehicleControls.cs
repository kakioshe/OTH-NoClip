using System;

internal interface IVehicleControlsTarget : IDisposable
{
    bool Eligible { get; }
    void Apply(VehiclePreferences preferences);
    void ReleasePerformance();
    void Protect(bool enabled);
}

internal sealed class VehicleControls : IDisposable
{
    private readonly Func<IVehicleControlsTarget> resolve;
    private readonly Action<string> report;
    private IVehicleControlsTarget target;
    internal VehiclePreferences Preferences { get; }
    internal bool Modifiers { get; set; }
    internal bool GodMode { get; set; }
    internal bool PerformanceActive { get; private set; }
    internal string Status { get; private set; } = "";

    internal VehicleControls(VehiclePreferences preferences, Func<IVehicleControlsTarget> resolve, Action<string> report)
    {
        Preferences = preferences;
        this.resolve = resolve;
        this.report = report;
    }

    internal void Update(bool flightActive, VehiclePreferences effectivePreferences = null)
    {
        try
        {
            if (target != null && !target.Eligible)
            {
                Stop("Vehicle changed or became unavailable; controls switched off.");
                return;
            }
            if (!Modifiers && !GodMode) { Release(); return; }
            target ??= resolve();
            if (target == null || !target.Eligible) { Stop("Vehicle controls unavailable: an authoritative local single-player car is required."); return; }
            if (Modifiers && !flightActive) { target.Apply(effectivePreferences ?? Preferences); PerformanceActive = true; }
            else { target.ReleasePerformance(); PerformanceActive = false; }
            target.Protect(GodMode);
            Status = Modifiers && flightActive ? "Performance modifiers paused during flight." : "";
        }
        catch (Exception error) { Stop("Vehicle controls stopped: " + error.Message); }
    }

    internal void RestoreStock()
    {
        Modifiers = false;
        Preferences.Reset();
    }

    private void Stop(string text)
    {
        Modifiers = GodMode = false;
        try { Release(); }
        catch (Exception error) { text += " Cleanup: " + error.Message; }
        Status = text;
        report(text);
    }

    private void Release()
    {
        PerformanceActive = false;
        var previous = target;
        target = null;
        previous?.Dispose();
    }

    public void Dispose() { Modifiers = GodMode = false; Release(); }
}
