using System;

// The adapter must revalidate the same local, authoritative car before reading or healing.
internal interface IVehicleRepairTarget
{
    bool Eligible { get; }
    float Damage { get; }
    float MaximumDamage { get; }
    void Heal(float amount);
}

internal sealed class VehicleRepair
{
    private readonly Func<IVehicleRepairTarget> resolve;
    private readonly Action<string> report;
    private IVehicleRepairTarget requested;
    internal string Status { get; private set; } = "";
    internal bool Pending => requested != null;

    internal VehicleRepair(Func<IVehicleRepairTarget> resolve, Action<string> report)
    {
        this.resolve = resolve;
        this.report = report;
    }

    // Capture identity on click; never carry a queued repair over to a different car.
    internal void Request()
    {
        if (Pending) return;
        try
        {
            var target = resolve();
            if (target == null || !target.Eligible) { SetStatus("Repair unavailable: a local authoritative single-player car is required."); return; }
            requested = target;
            Status = "Repair queued.";
        }
        catch (Exception error) { SetStatus("Repair unavailable: " + error.Message); }
    }

    // Execute once from Update rather than an IMGUI layout/repaint event.
    internal void Update()
    {
        var target = requested;
        requested = null;
        if (target == null) return;
        try
        {
            if (!target.Eligible) { SetStatus("Repair cancelled: the car or its authority changed."); return; }
            float damage = target.Damage;
            float maximum = target.MaximumDamage;
            if (!float.IsFinite(damage) || damage < 0 || !float.IsFinite(maximum) || maximum <= 0 || damage > maximum)
            {
                SetStatus("Repair unavailable: invalid vehicle damage state.");
                return;
            }
            if (damage == 0) { SetStatus("The car has no damage to repair."); return; }
            target.Heal(maximum);
            float remaining = target.Damage;
            SetStatus(remaining == 0 ? "Car repaired. Damage is zero." : "Repair requested; damage has not yet reached zero.");
        }
        catch (Exception error) { SetStatus("Repair failed: " + error.Message); }
    }

    internal void Cancel() { requested = null; }

    private void SetStatus(string text)
    {
        Status = text;
        report(text);
    }
}
