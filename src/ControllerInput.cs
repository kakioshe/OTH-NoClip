using System;

// Kept independent of Unity so controller scaling can be checked without the game.
internal static class ControllerInput
{
    private const float StickDeadZone = 0.2f;
    private const byte TriggerDeadZone = 30;

    internal static float Axis(short value)
    {
        float normalized = Math.Max(-1f, value / (float)short.MaxValue);
        float magnitude = Math.Abs(normalized);
        return magnitude <= StickDeadZone ? 0f : Math.Sign(normalized) * (magnitude - StickDeadZone) / (1f - StickDeadZone);
    }

    internal static float Trigger(byte value)
    {
        return value <= TriggerDeadZone ? 0f : (value - TriggerDeadZone) / (float)(byte.MaxValue - TriggerDeadZone);
    }
}
