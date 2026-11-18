using System;
using System.Diagnostics;

internal interface IVehicleFlipTarget
{
    bool Eligible { get; }
    bool Upright { get; }
    string Pose { get; }
    void Flip();
}

// Capture the clicked car, then execute once outside IMGUI events.
internal sealed class VehicleFlip
{
    private readonly Func<IVehicleFlipTarget> resolve;
    private readonly Action<string> report;
    private IVehicleFlipTarget requested;
    private IVehicleFlipTarget observing;
    private readonly Func<double> time;
    private double observeAt;
    private int observation;
    internal bool Pending => requested != null || observing != null;
    internal string Status { get; private set; } = "";

    internal VehicleFlip(Func<IVehicleFlipTarget> resolve, Action<string> report, Func<double> time = null)
    { this.resolve = resolve; this.report = report; this.time = time ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency); }

    internal void Request()
    {
        if (Pending) return;
        try
        {
            var target = resolve();
            if (target == null || !target.Eligible) { SetStatus("Flip unavailable: a local authoritative car with flight off is required."); return; }
            requested = target;
            SetStatus("Flip queued.");
        }
        catch (Exception error) { SetStatus("Flip unavailable: " + error.Message); }
    }

    internal void Update()
    {
        Observe();
        var target = requested;
        requested = null;
        if (target == null) return;
        try
        {
            if (!target.Eligible) { SetStatus("Flip cancelled: car, authority or flight state changed."); return; }
            report("Flip before: " + target.Pose);
            target.Flip();
            report("Flip immediate: " + target.Pose);
            observing = target;
            observation = 0;
            observeAt = time() + 0.15;
            SetStatus("Physics teleport requested; checking upright pose.");
        }
        catch (Exception error) { SetStatus("Flip failed: " + error.Message); }
    }

    internal void Cancel() { requested = null; }
    internal void Stop() { requested = null; observing = null; }

    private void Observe()
    {
        if (observing == null) return;
        try
        {
            if (!observing.Eligible)
            { observing = null; SetStatus("Flip observation stopped: car, authority or flight state changed."); return; }
            if (time() < observeAt) return;
            report("Flip delayed " + (observation == 0 ? "0.15s" : "0.75s") + ": " + observing.Pose);
            if (observation++ == 0) { observeAt += 0.60; return; }
            bool upright = observing.Upright;
            observing = null;
            if (upright)
            {
                Status = "";
                report("Flip readback: car is upright.");
            }
            else SetStatus("Car did not stay upright. Try open, level ground; pose details logged.");
        }
        catch (Exception error)
        { observing = null; SetStatus("Flip readback failed: " + error.Message); }
    }
    private void SetStatus(string text) { Status = text; report(text); }
}
