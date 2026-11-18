using System;

internal interface IVehicleAssistsTarget : IDisposable
{
    bool Eligible { get; }
    void SetEnabled(bool flooding, bool climb);
}

internal sealed class VehicleAssists : IDisposable
{
    private readonly Func<IVehicleAssistsTarget> resolve;
    private readonly Action<string> report;
    private IVehicleAssistsTarget target;
    internal bool Flooding { get; set; }
    internal bool Climb { get; set; }
    internal string Status { get; private set; } = "";

    internal VehicleAssists(Func<IVehicleAssistsTarget> resolve, Action<string> report)
    { this.resolve = resolve; this.report = report; }

    internal void Update(bool flightActive)
    {
        try
        {
            if (target != null && !target.Eligible) { Stop("Vehicle changed; assists switched off."); return; }
            if (!Flooding && !Climb) { Release(); Status = ""; return; }
            target ??= resolve();
            if (target == null || !target.Eligible) { Stop("Vehicle assists require an authoritative local single-player car."); return; }
            target.SetEnabled(Flooding, Climb && !flightActive);
            Status = Climb && flightActive ? "Spider mode paused during flight." : "";
        }
        catch (Exception error) { Stop("Vehicle assists stopped: " + error.Message); }
    }

    internal void Fail(string message) => Stop(message);

    private void Stop(string message)
    {
        Flooding = Climb = false;
        try { Release(); } catch (Exception error) { message += " Cleanup: " + error.Message; }
        Status = message;
        report(message);
    }

    private void Release() { var previous = target; target = null; previous?.Dispose(); }
    public void Dispose() { Flooding = Climb = false; Release(); }
}
