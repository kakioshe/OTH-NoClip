using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

internal static class CallbackTrace
{
    private static readonly Dictionary<string, int> Counts = new();
    private static IntPtr vehiclePointer, bodyPointer;
    private static Rigidbody body;
    private static float deadline;
    private static int samples;
    private const int Limit = 500;

    internal static void Install()
    {
        var harmony = new Harmony("local.oth.noclip.trace");
        var vehicle = typeof(VehiclePhysics.VehicleBase);
        Patch(harmony, vehicle, "FixedUpdateNetwork", Type.EmptyTypes, false);
        Patch(harmony, vehicle, "AfterClientPredictionReset", Type.EmptyTypes, false);
        Patch(harmony, vehicle, "RestoreSnapshot", Type.EmptyTypes, false);
        Patch(harmony, vehicle, "Reposition", new[] { typeof(Vector3) }, false);
        Patch(harmony, vehicle, "Reposition", new[] { typeof(Vector3), typeof(Quaternion) }, false);
        Patch(harmony, typeof(Fusion.Addons.Physics.NetworkRigidbody3D), "ApplyRBPositionRotation", new[] { typeof(Rigidbody), typeof(Vector3), typeof(Quaternion) }, true);
    }

    private static void Patch(Harmony harmony, Type type, string name, Type[] parameters, bool rigidbodyMethod)
    {
        string label = type.FullName + "." + name + "/" + parameters.Length;
        try {
            var target = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, parameters, null);
            if (target == null) { Plugin.Logger.LogWarning("Trace patch target missing: " + label); return; }
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            harmony.Patch(target, new HarmonyMethod(typeof(CallbackTrace).GetMethod(rigidbodyMethod ? nameof(BodyPrefix) : nameof(VehiclePrefix), flags)), new HarmonyMethod(typeof(CallbackTrace).GetMethod(rigidbodyMethod ? nameof(BodyPostfix) : nameof(VehiclePostfix), flags)));
            Plugin.Logger.LogInfo("Trace patch installed (native invocation unverified): " + label);
        } catch (Exception e) { Plugin.Logger.LogWarning("Trace patch failed: " + label + ": " + e); }
    }

    internal static void Begin(VehiclePhysics.VehicleBase vehicle, Rigidbody rigidbody)
    {
        Counts.Clear(); samples = 0;
        vehiclePointer = vehicle.Pointer; bodyPointer = rigidbody.Pointer; body = rigidbody;
        deadline = Time.unscaledTime + 3f;
        Plugin.Logger.LogInfo("Callback trace started: duration=3s, sampleLimit=500, authority=not captured; patch installation does not prove native interception.");
    }

    internal static void End()
    {
        try {
            foreach (var entry in Counts) Plugin.Logger.LogInfo($"Callback trace count: {entry.Key}={entry.Value}");
            Plugin.Logger.LogInfo($"Callback trace ended: observed={samples}, sampleLimit={Limit}; absent callbacks do not prove absence of native calls.");
        } catch (Exception e) { Plugin.Logger.LogWarning("Callback trace summary failed: " + e.Message); }
        vehiclePointer = IntPtr.Zero; bodyPointer = IntPtr.Zero; body = null;
    }

    private static void VehiclePrefix(Il2CppObjectBase __instance, MethodBase __originalMethod) => RecordVehicle(__instance, __originalMethod, "before");
    private static void VehiclePostfix(Il2CppObjectBase __instance, MethodBase __originalMethod) => RecordVehicle(__instance, __originalMethod, "after");
    private static void RecordVehicle(Il2CppObjectBase instance, MethodBase method, string phase)
    {
        try { if (vehiclePointer != IntPtr.Zero && instance != null && instance.Pointer == vehiclePointer) Record(method, phase, ""); }
        catch (Exception e) { Plugin.Logger.LogWarning("Vehicle trace failed: " + e.Message); }
    }
    private static void BodyPrefix(Rigidbody __0, Vector3 __1, Quaternion __2, MethodBase __originalMethod) => RecordBody(__0, __1, __2, __originalMethod, "before");
    private static void BodyPostfix(Rigidbody __0, Vector3 __1, Quaternion __2, MethodBase __originalMethod) => RecordBody(__0, __1, __2, __originalMethod, "after");
    private static void RecordBody(Rigidbody rb, Vector3 position, Quaternion rotation, MethodBase method, string phase)
    {
        try { if (bodyPointer != IntPtr.Zero && rb != null && rb.Pointer == bodyPointer) Record(method, phase, $", requestedPosition={position}, requestedRotation={rotation.eulerAngles}"); }
        catch (Exception e) { Plugin.Logger.LogWarning("Rigidbody trace failed: " + e.Message); }
    }
    private static void Record(MethodBase method, string phase, string requested)
    {
        if (Time.unscaledTime >= deadline || body == null) return;
        string key = method.DeclaringType.Name + "." + method.Name + "/" + method.GetParameters().Length + ":" + phase;
        Counts.TryGetValue(key, out int count); Counts[key] = count + 1;
        if (samples++ >= Limit) return;
        Plugin.Logger.LogInfo($"Callback trace {key}: frame={Time.frameCount}, time={Time.unscaledTime:F3}, fixedTime={Time.fixedTime:F3}, bodyPosition={body.position}, bodyRotation={body.rotation.eulerAngles}, transformPosition={body.transform.position}, transformRotation={body.transform.rotation.eulerAngles}{requested}");
    }
}

