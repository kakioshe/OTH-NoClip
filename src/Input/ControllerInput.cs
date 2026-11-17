using System;

// Kept independent of Unity so controller scaling can be checked without the game.
internal static class ControllerInput
{
    private const float StickDeadZone = 0.2f;
    private const byte TriggerDeadZone = 30;
    private static readonly ushort[] ButtonMasks = { 0x1000, 0x2000, 0x4000, 0x8000, 0x0010, 0x0020, 0x0100, 0x0200, 0x0040, 0x0080, 1, 2, 4, 8 };

    internal static float Control(PadControl control, ushort buttons, byte leftTrigger, byte rightTrigger, short leftX, short leftY, short rightX, short rightY)
    {
        int index = (int)control - 1;
        if (index >= 0 && index < ButtonMasks.Length) return (buttons & ButtonMasks[index]) != 0 ? 1f : 0f;
        return control switch
        {
            PadControl.LeftXNegative => Math.Max(0, -Axis(leftX)), PadControl.LeftXPositive => Math.Max(0, Axis(leftX)),
            PadControl.LeftYNegative => Math.Max(0, -Axis(leftY)), PadControl.LeftYPositive => Math.Max(0, Axis(leftY)),
            PadControl.RightXNegative => Math.Max(0, -Axis(rightX)), PadControl.RightXPositive => Math.Max(0, Axis(rightX)),
            PadControl.RightYNegative => Math.Max(0, -Axis(rightY)), PadControl.RightYPositive => Math.Max(0, Axis(rightY)),
            PadControl.LeftTrigger => Trigger(leftTrigger), PadControl.RightTrigger => Trigger(rightTrigger), _ => 0f
        };
    }

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
