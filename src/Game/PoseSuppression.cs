using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Prevent Fusion from overwriting the active local vehicle's flight pose.
internal sealed class PoseSuppression : IDisposable
{
    private static PoseSuppression current;
    private readonly HarmonyLib.Harmony hook = new("local.oth.noclip.pose");
    private readonly TrainerLogger logger;
    private IntPtr bodyPointer;
    private Func<bool> eligible;

    internal PoseSuppression(TrainerLogger logger)
    {
        this.logger = logger;
        current = this;
        try {
            var target = typeof(Il2CppFusion.Addons.Physics.NetworkRigidbody3D).GetMethod(
                "ApplyRBPositionRotation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null, new[] { typeof(Rigidbody), typeof(Vector3), typeof(Quaternion) }, null);
            if (target == null) throw new MissingMethodException("NetworkRigidbody3D.ApplyRBPositionRotation");
            var prefix = typeof(PoseSuppression).GetMethod(nameof(BodyPrefix), BindingFlags.Static | BindingFlags.NonPublic);
            hook.Patch(target, new HarmonyMethod(prefix));
            logger.LogInfo("Vehicle pose suppression patch installed.");
        } catch (Exception e) { logger.LogError("Vehicle pose suppression patch failed: " + e); }
    }

    internal void Begin(Rigidbody body, Func<bool> isEligible)
    {
        bodyPointer = body.Pointer;
        eligible = isEligible;
    }

    internal void End()
    {
        eligible = null;
        bodyPointer = IntPtr.Zero;
    }

    private static bool BodyPrefix(Rigidbody __0)
        => current == null || current.ShouldApplyPose(__0);

    private bool ShouldApplyPose(Rigidbody body)
    {
        try {
            return bodyPointer == IntPtr.Zero || body == null || body.Pointer != bodyPointer || eligible == null || !eligible();
        } catch (Exception e) {
            logger.LogWarning("Vehicle pose eligibility check failed: " + e.Message);
            return true;
        }
    }

    public void Dispose()
    {
        End();
        if (current == this) current = null;
        hook.UnpatchSelf();
    }
}
