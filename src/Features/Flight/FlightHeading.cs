using System;

internal static class FlightHeading
{
    // Ignore camera pitch. Looking straight down keeps the last usable heading.
    internal static (float x, float z) Horizontal(float x, float z, float fallbackX, float fallbackZ)
    {
        float lengthSquared = x * x + z * z;
        if (!float.IsFinite(lengthSquared) || lengthSquared < 0.01f) return (fallbackX, fallbackZ);
        float length = (float)Math.Sqrt(lengthSquared);
        return (x / length, z / length);
    }
}
