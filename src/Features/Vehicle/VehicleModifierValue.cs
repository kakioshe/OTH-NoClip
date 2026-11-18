using System;

// One scalar on one settings object. The adapter must discard this owner when
// the native object changes, and release it before a known game writer runs.
internal sealed class VehicleModifierValue
{
    private readonly float baseline;
    private float lastWritten;
    internal bool Applied { get; private set; }
    internal bool LostOwnership { get; private set; }

    internal VehicleModifierValue(float baseline)
    {
        if (!Valid(baseline)) throw new ArgumentOutOfRangeException(nameof(baseline));
        this.baseline = baseline;
    }

    // A mismatch is an unknown writer, not evidence of a new legitimate baseline.
    // Stop owning it instead of multiplying or restoring an ambiguous value.
    internal bool TryApply(float current, float multiplier, out float next)
    {
        next = current;
        if (!Valid(multiplier) || multiplier == 0) return false;
        float candidate = baseline * multiplier;
        if (!Valid(candidate) || !Owns(current)) return false;
        lastWritten = candidate;
        Applied = true;
        next = candidate;
        return true;
    }

    internal bool TryRestore(float current, out float next)
    {
        next = current;
        if (!Applied || !Owns(current)) return false;
        next = baseline;
        Applied = false;
        return true;
    }

    private bool Owns(float current)
    {
        if (LostOwnership) return false;
        if (Valid(current) && current == (Applied ? lastWritten : baseline)) return true;
        LostOwnership = true;
        Applied = false;
        return false;
    }

    private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
}
