using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

[BepInPlugin("local.oth.noclip", "OTH NoClip", "0.4.0")]
public class Plugin : BasePlugin
{
    internal static BepInEx.Logging.ManualLogSource Logger;
    public override void Load() { Logger = Log; CallbackTrace.Install(); AddComponent<Flight>(); Log.LogInfo("No-clip loaded. F8 toggles flight in single-player."); }
}
public class Flight : MonoBehaviour
{
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint index, out ControllerState state);
    [StructLayout(LayoutKind.Sequential)] struct Gamepad { public ushort buttons; public byte leftTrigger, rightTrigger; public short leftX, leftY, rightX, rightY; }
    [StructLayout(LayoutKind.Sequential)] struct ControllerState { public uint packet; public Gamepad gamepad; }
    static bool Focused() { GetWindowThreadProcessId(GetForegroundWindow(), out uint pid); return pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id; }
    static bool Down(int key) { return Focused() && (GetAsyncKeyState(key) & 0x8000) != 0; }
    static float Axis(short value) { float v = Math.Max(-1f, value / 32767f); return Math.Abs(v) <= 0.2f ? 0f : Math.Sign(v) * (Math.Abs(v) - 0.2f) / 0.8f; }
    static float Trigger(byte value) { return value <= 30 ? 0f : (value - 30) / 225f; }
    static bool ReadController(out Gamepad pad) {
        pad = default;
        if (!Focused()) return false;
        for (uint i = 0; i < 4; i++) if (XInputGetState(i, out var state) == 0) { pad = state.gamepad; return true; }
        return false;
    }
    bool lastToggle, active, gravity, kinematic;
    Rigidbody body;
    VehiclePhysics.VehicleBase vehicle;
    float nextDiagnostic;
    Vector3 lastRequestedPosition;
    Vector3 flightPosition;
    Quaternion flightRotation;
    bool hasRequestedPosition;
    float diagnosticBurstEnd, nextUpdateDiagnostic, nextFixedDiagnostic;
    readonly List<(Collider collider, bool enabled)> colliders = new();
    void Update()
    {
        bool toggle = Down(0x77);
        try {
            var runner = OverlandNetworkRunner.Instance;
            if (active && (runner == null || !runner.IsSinglePlayer || !OverlandNetworkRunner.IsLocalPlayerSpawned)) Stop();
            if (toggle && !lastToggle) { if (active) Stop(); else StartFlight(runner); }
            if (active && body != null && ShouldLogStage(ref nextUpdateDiagnostic)) LogStage("Update");
        } catch (Exception e) { Debug.LogError("OTH NoClip: " + e); Stop(); }
        lastToggle = toggle;
    }
    void FixedUpdate()
    {
        try { if (active && body != null && ShouldLogStage(ref nextFixedDiagnostic)) LogStage("FixedUpdate"); }
        catch (Exception e) { Debug.LogError("OTH NoClip: " + e); Stop(); }
    }
    bool ShouldLogStage(ref float nextSample)
    {
        if (Time.unscaledTime < diagnosticBurstEnd) return true;
        if (Time.unscaledTime < nextSample) return false;
        nextSample = Time.unscaledTime + 1f;
        return true;
    }
    void LogStage(string stage)
    {
        Plugin.Logger.LogInfo($"Pose stage {stage}: frame={Time.frameCount}, time={Time.unscaledTime:F3}, fixedTime={Time.fixedTime:F3}, position={body.position}, rotation={body.rotation.eulerAngles}, transformPosition={body.transform.position}, previousRequested={(hasRequestedPosition ? lastRequestedPosition.ToString() : "none")}, velocity={body.velocity}, kinematic={body.isKinematic}, interpolation={body.interpolation}");
    }
    void LateUpdate()
    {
        try {
            if (active && body != null) {
                var direction = Vector3.zero;
                var forward = Vector3.ProjectOnPlane(flightRotation * Vector3.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, forward);
                if (Down(0x57)) direction += forward;
                if (Down(0x53)) direction -= forward;
                if (Down(0x44)) direction += right;
                if (Down(0x41)) direction -= right;
                if (Down(0x20)) direction += Vector3.up;
                if (Down(0x11)) direction -= Vector3.up;
                bool boost = Down(0x10);
                if (ReadController(out var pad)) {
                    direction += forward * Axis(pad.leftY) + right * Axis(pad.leftX);
                    direction += Vector3.up * (Trigger(pad.rightTrigger) - Trigger(pad.leftTrigger));
                    boost |= (pad.buttons & 0x0100) != 0; // Left bumper
                    flightRotation = Quaternion.AngleAxis(Axis(pad.rightX) * 90f * Time.deltaTime, Vector3.up) * flightRotation;
                }
                float speed = boost ? 135f : 45f;
                var before = body.position;
                var requested = flightPosition + Vector3.ClampMagnitude(direction, 1f) * speed * Time.deltaTime;
                flightPosition = requested;
                body.rotation = flightRotation;
                body.position = requested;
                if (Time.unscaledTime < diagnosticBurstEnd || Time.unscaledTime >= nextDiagnostic) {
                    Plugin.Logger.LogInfo($"Flight diagnostic: frame={Time.frameCount}, time={Time.unscaledTime:F3}, fixedTime={Time.fixedTime:F3}, input={direction}, position={before}, rotation={body.rotation.eulerAngles}, previousRequested={(hasRequestedPosition ? lastRequestedPosition.ToString() : "none")}, requested={requested}, actualAfterWrite={body.position}, kinematic={body.isKinematic}, focused={Focused()}, dt={Time.deltaTime:F4}");
                    nextDiagnostic = Time.unscaledTime + 1f;
                }
                lastRequestedPosition = requested;
                hasRequestedPosition = true;
            }
        } catch (Exception e) { Debug.LogError("OTH NoClip: " + e); Stop(); }
    }
    void StartFlight(OverlandNetworkRunner runner)
    {
        if (runner == null || !runner.IsSinglePlayer || !OverlandNetworkRunner.IsLocalPlayerSpawned || runner.LocalPlayerNetworkObject == null) { Debug.LogWarning("OTH NoClip: single-player vehicle unavailable."); return; }
        var player = runner.LocalPlayerNetworkObject.GetComponent<FusionPlayer>();
        vehicle = player != null ? player.vehicleBase : null;
        body = vehicle != null ? vehicle.cachedRigidbody : null;
        if (body == null) { Debug.LogWarning("OTH NoClip: local vehicle not found."); return; }
        gravity = body.useGravity; kinematic = body.isKinematic;
        colliders.Clear();
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) { if (c != null) colliders.Add((c, c.enabled)); }
        active = true;
        CallbackTrace.Begin(vehicle, body);
        hasRequestedPosition = false;
        nextDiagnostic = 0f;
        diagnosticBurstEnd = Time.unscaledTime + 3f;
        nextUpdateDiagnostic = diagnosticBurstEnd;
        nextFixedDiagnostic = diagnosticBurstEnd;
        foreach (var component in body.GetComponentsInParent<Component>(true)) {
            if (component == null) continue;
            try { Plugin.Logger.LogInfo($"Vehicle component: object={component.gameObject.name}, type={component.GetIl2CppType().FullName}, managedType={component.GetType().FullName}"); }
            catch (Exception e) { Plugin.Logger.LogWarning($"Vehicle component identification failed: {e.Message}"); }
        }
        foreach (var item in colliders) item.collider.enabled = false;
        if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        body.useGravity = false; body.isKinematic = true;
        var forward = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.Cross(Vector3.ProjectOnPlane(body.transform.right, Vector3.up), Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        flightPosition = body.position;
        flightRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        body.rotation = flightRotation;
        Plugin.Logger.LogInfo($"Flight enabled: vehicle={vehicle.name}, position={body.position}, colliders={colliders.Count}, originalKinematic={kinematic}, originalGravity={gravity}.");
    }
    void Stop() {
        if (!active) return;
        foreach (var item in colliders) if (item.collider != null) item.collider.enabled = item.enabled;
        colliders.Clear();
        if (body != null) { body.useGravity = gravity; body.isKinematic = kinematic; }
        CallbackTrace.End();
        active = false; body = null; vehicle = null;
        Plugin.Logger.LogInfo("Flight disabled.");
    }
    void OnDestroy() { Stop(); }
}



