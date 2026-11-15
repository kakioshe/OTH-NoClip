using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

[BepInPlugin("local.oth.noclip", "OTH NoClip", "0.4.0")]
public class Plugin : BasePlugin
{
    public override void Load() { AddComponent<Flight>(); Log.LogInfo("No-clip loaded. F8 toggles flight in single-player."); }
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
    readonly List<(Collider collider, bool enabled)> colliders = new();
    void Update()
    {
        bool toggle = Down(0x77);
        try {
            var runner = OverlandNetworkRunner.Instance;
            if (active && (runner == null || !runner.IsSinglePlayer || !OverlandNetworkRunner.IsLocalPlayerSpawned)) Stop();
            if (toggle && !lastToggle) { if (active) Stop(); else StartFlight(runner); }
        } catch (Exception e) { Debug.LogError("OTH NoClip: " + e); Stop(); }
        lastToggle = toggle;
    }
    void LateUpdate()
    {
        try {
            if (active && body != null) {
                var direction = Vector3.zero;
                var forward = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up).normalized;
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
                    body.rotation = Quaternion.AngleAxis(Axis(pad.rightX) * 90f * Time.deltaTime, Vector3.up) * body.rotation;
                }
                float speed = boost ? 135f : 45f;
                body.position += Vector3.ClampMagnitude(direction, 1f) * speed * Time.deltaTime;
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
        foreach (var item in colliders) item.collider.enabled = false;
        if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        body.useGravity = false; body.isKinematic = true;
        var forward = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.Cross(Vector3.ProjectOnPlane(body.transform.right, Vector3.up), Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        body.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        Debug.Log("OTH NoClip: enabled.");
    }
    void Stop() {
        if (!active) return;
        foreach (var item in colliders) if (item.collider != null) item.collider.enabled = item.enabled;
        colliders.Clear();
        if (body != null) { body.useGravity = gravity; body.isKinematic = kinematic; }
        active = false; body = null; vehicle = null;
        Debug.Log("OTH NoClip: disabled.");
    }
    void OnDestroy() { Stop(); }
}



