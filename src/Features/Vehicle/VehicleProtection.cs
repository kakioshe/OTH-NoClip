using System;

// Own only the damage multiplier. Existing damage is never changed here.
internal sealed class VehicleProtection
{
    private readonly Func<float> read;
    private readonly Action<float> write;
    private readonly float baseline;
    internal bool Active { get; private set; }

    internal VehicleProtection(Func<float> read, Action<float> write)
    {
        this.read = read;
        this.write = write;
        baseline = read();
        if (!float.IsFinite(baseline) || baseline < 0) throw new InvalidOperationException("Invalid damage multiplier.");
    }

    internal void Enable()
    {
        if (read() != (Active ? 0 : baseline)) throw new InvalidOperationException("Damage multiplier changed outside the trainer.");
        if (Active) return;
        // Mark ownership before the call so a partially successful write can be restored.
        Active = true;
        write(0);
        if (read() != 0) throw new InvalidOperationException("Damage protection readback failed.");
    }

    internal void Disable()
    {
        if (!Active) return;
        Active = false;
        if (read() != 0) return; // Relinquish an unknown external write.
        write(baseline);
        if (read() != baseline) throw new InvalidOperationException("Damage protection restoration failed.");
    }
}
