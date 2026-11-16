using System;

// Kept independent of Unity so controller scaling can be checked without the game.
internal static class ControllerInput
{
    internal static float Axis(short value)
    {
        float v = Math.Max(-1f, value / 32767f);
        return Math.Abs(v) <= 0.2f ? 0f : Math.Sign(v) * (Math.Abs(v) - 0.2f) / 0.8f;
    }

    internal static float Trigger(byte value)
    {
        return value <= 30 ? 0f : (value - 30) / 225f;
    }
}
