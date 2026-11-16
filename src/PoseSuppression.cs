using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Prevent Fusion from overwriting the active local vehicle's flight pose.
internal static class PoseSuppression
{
    private static IntPtr bodyPointer;
    private static Func<bool> eligible;

    internal static void Install()
    {
        try {
            var target = typeof(Il2CppFusion.Addons.Physics.NetworkRigidbody3D).GetMethod(
                "ApplyRBPositionRotation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null, new[] { typeof(Rigidbody), typeof(Vector3), typeof(Quaternion) }, null);
            if (target == null) throw new MissingMethodException("NetworkRigidbody3D.ApplyRBPositionRotation");
            var prefix = typeof(PoseSuppression).GetMethod(nameof(BodyPrefix), BindingFlags.Static | BindingFlags.NonPublic);
            new HarmonyLib.Harmony("local.oth.noclip.pose").Patch(target, new HarmonyMethod(prefix));
            Plugin.Logger.LogInfo("Vehicle pose suppression patch installed.");
        } catch (Exception e) { Plugin.Logger.LogError("Vehicle pose suppression patch failed: " + e); }
    }

    internal static void Begin(Rigidbody body, Func<bool> isEligible)
    {
        bodyPointer = body.Pointer;
        eligible = isEligible;
    }

    internal static void End()
    {
        eligible = null;
        bodyPointer = IntPtr.Zero;
    }

    private static bool BodyPrefix(Rigidbody __0)
    {
        try {
            return bodyPointer == IntPtr.Zero || __0 == null || __0.Pointer != bodyPointer || eligible == null || !eligible();
        } catch (Exception e) {
            Plugin.Logger.LogWarning("Vehicle pose eligibility check failed: " + e.Message);
            return true;
        }
    }
}
