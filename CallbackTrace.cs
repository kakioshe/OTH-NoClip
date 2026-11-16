using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

internal static class CallbackTrace
{
    private static readonly Dictionary<string, int> Counts = new();
    private static readonly string RunId = Guid.NewGuid().ToString("N").Substring(0, 8);
    private static int activation;
    private static long suppressedCalls;
    private static bool suppress;
    private static Func<bool> eligible;
    private static IntPtr vehiclePointer, bodyPointer;
    private static Rigidbody body;
    private static float deadline;
    private static int samples;
    private const int Limit = 40; // Per callback/phase, so a busy method cannot starve other probes.

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

    internal static void Begin(VehiclePhysics.VehicleBase vehicle, Rigidbody rigidbody, Func<bool> isEligible)
    {
        Counts.Clear(); samples = 0; suppressedCalls = 0; activation++;
        suppress = true; eligible = isEligible;
        vehiclePointer = vehicle.Pointer; bodyPointer = rigidbody.Pointer; body = rigidbody;
        deadline = Time.unscaledTime + 3f;
        Plugin.Logger.LogInfo($"T01-E01 trace started: run={RunId}, activation={activation}, mode=suppress, vehiclePointer={vehiclePointer}, bodyPointer={bodyPointer}, duration=3s, perCallbackPhaseLimit={Limit}; suppression lasts until flight stops, not just the trace window.");
        Plugin.Logger.LogInfo($"T01-E01 initial state: position={rigidbody.position}, rotation={rigidbody.rotation.eulerAngles}, velocity={rigidbody.velocity}, angularVelocity={rigidbody.angularVelocity}, kinematic={rigidbody.isKinematic}, gravity={rigidbody.useGravity}");
        try { Plugin.Logger.LogInfo($"T01-E01 authority: state={vehicle.HasStateAuthority}, input={vehicle.HasInputAuthority}, parent={(rigidbody.transform.parent != null ? rigidbody.transform.parent.name : "none")}"); }
        catch (Exception e) { Plugin.Logger.LogWarning("T01-E01 authority/parent unknown: " + e.Message); }
    }

    internal static void End()
    {
        // Release the intervention before logging or restoring physics, even if either fails.
        eligible = null; suppress = false;
        vehiclePointer = IntPtr.Zero; bodyPointer = IntPtr.Zero; body = null;
        try {
            foreach (var entry in Counts) Plugin.Logger.LogInfo($"Callback trace count: {entry.Key}={entry.Value}, omitted={Math.Max(0, entry.Value - Limit)}");
            Plugin.Logger.LogInfo($"T01-E01 trace ended: run={RunId}, activation={activation}, detailedSamples={samples}, suppressedCalls={suppressedCalls}; counts cover first 3s; suppression count covers full activation. Exit pose was NOT reconciled; check snapback and normal driving. Absent callbacks do not prove absence of native calls.");
        } catch (Exception e) { Plugin.Logger.LogWarning("Callback trace summary failed: " + e.Message); }
    }

    private static void VehiclePrefix(Il2CppObjectBase __instance, MethodBase __originalMethod) => RecordVehicle(__instance, __originalMethod, "before");
    private static void VehiclePostfix(Il2CppObjectBase __instance, MethodBase __originalMethod) => RecordVehicle(__instance, __originalMethod, "after");
    private static void RecordVehicle(Il2CppObjectBase instance, MethodBase method, string phase)
    {
        try { if (vehiclePointer != IntPtr.Zero && instance != null && instance.Pointer == vehiclePointer) Record(method, phase, ""); }
        catch (Exception e) { Plugin.Logger.LogWarning("Vehicle trace failed: " + e.Message); }
    }
    private static bool BodyPrefix(Rigidbody __0, Vector3 __1, Quaternion __2, MethodBase __originalMethod, out bool __state)
    {
        __state = false;
        RecordBody(__0, __1, __2, __originalMethod, "before");
        try {
            if (suppress && bodyPointer != IntPtr.Zero && __0 != null && __0.Pointer == bodyPointer && eligible != null && eligible()) {
                __state = true;
                suppressedCalls++;
                if (suppressedCalls == 1) Plugin.Logger.LogInfo($"T01-E01 suppression intercepted: run={RunId}, activation={activation}, frame={Time.frameCount}, bodyPointer={bodyPointer}");
            }
        } catch (Exception e) { Plugin.Logger.LogWarning("T01-E01 eligibility failed; allowing original callback: " + e.Message); __state = false; }
        return !__state;
    }
    private static void BodyPostfix(Rigidbody __0, Vector3 __1, Quaternion __2, MethodBase __originalMethod, bool __state) => RecordBody(__0, __1, __2, __originalMethod, __state ? "after-skipped" : "after");
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
        if (count >= Limit) return;
        samples++;
        Plugin.Logger.LogInfo($"Callback trace {key}: run={RunId}, activation={activation}, frame={Time.frameCount}, time={Time.unscaledTime:F3}, fixedTime={Time.fixedTime:F3}, bodyPosition={body.position}, bodyRotation={body.rotation.eulerAngles}, transformPosition={body.transform.position}, transformRotation={body.transform.rotation.eulerAngles}{requested}");
    }
}
