using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MelonLoader;
using Il2Cpp;
using VehiclePhysics = Il2CppVehiclePhysics;
using CommandTerminal = Il2CppCommandTerminal;
using UnityEngine;

[assembly: MelonInfo(typeof(Plugin), "oth-trainer", "0.6.0", "oth-trainer")]
[assembly: MelonGame("Funselektor", "over the hill")]

public class Plugin : MelonMod
{
    internal static TrainerLogger Logger;
    private Flight flight;
    public override void OnInitializeMelon() { Logger = new TrainerLogger(LoggerInstance); PoseSuppression.Install(); flight = new Flight(); Logger.LogInfo("F8 toggles vehicle flight; F9 teleports to the map pin in single-player."); }
    public override void OnUpdate() { flight?.Update(); }
    public override void OnLateUpdate() { flight?.LateUpdate(); }
    public override void OnDeinitializeMelon() { flight?.Stop(); }
}
internal sealed class TrainerLogger
{
    private readonly MelonLogger.Instance logger;
    internal TrainerLogger(MelonLogger.Instance logger) { this.logger = logger; }
    internal void LogInfo(string message) { logger.Msg(message); }
    internal void LogWarning(string message) { logger.Warning(message); }
    internal void LogError(string message) { logger.Error(message); }
}
public class Flight
{
    const int ToggleKey = 0x77, TeleportKey = 0x78; // F8, F9
    const int ForwardKey = 0x57, BackKey = 0x53, RightKey = 0x44, LeftKey = 0x41;
    const int UpKey = 0x20, DownKey = 0x11, BoostKey = 0x10;
    const ushort LeftBumper = 0x0100;
    const float FlightSpeed = 45f, BoostSpeed = 135f, TurnDegreesPerSecond = 90f;
    const float MinimumDirectionSquared = 0.01f;
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint index, out ControllerState state);
    [StructLayout(LayoutKind.Sequential)] struct Gamepad { public ushort buttons; public byte leftTrigger, rightTrigger; public short leftX, leftY, rightX, rightY; }
    [StructLayout(LayoutKind.Sequential)] struct ControllerState { public uint packet; public Gamepad gamepad; }
    static bool Focused() { GetWindowThreadProcessId(GetForegroundWindow(), out uint pid); return pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id; }
    static bool Down(int key) { return Focused() && (GetAsyncKeyState(key) & 0x8000) != 0; }
    static bool ReadController(out Gamepad pad) {
        pad = default;
        if (!Focused()) return false;
        for (uint i = 0; i < 4; i++) if (XInputGetState(i, out var state) == 0) { pad = state.gamepad; return true; }
        return false;
    }
    bool lastToggle, lastTeleport, active, gravity, kinematic;
    Rigidbody body;
    VehiclePhysics.VehicleBase vehicle;
    Vector3 flightPosition;
    Quaternion flightRotation;
    readonly List<(Collider collider, bool enabled)> colliders = new();
    internal void Update()
    {
        bool toggle = Down(ToggleKey);
        bool teleport = Down(TeleportKey);
        try {
            var runner = OverlandNetworkRunner.Instance;
            if (active && !IsCurrentLocalVehicle()) Stop();
            if (toggle && !lastToggle) { if (active) Stop(); else StartFlight(runner); }
            if (teleport && !lastTeleport) TeleportToPin(runner);
        } catch (Exception e) { Debug.LogError("oth-trainer: " + e); Stop(); }
        lastToggle = toggle;
        lastTeleport = teleport;
    }
    internal void LateUpdate()
    {
        try {
            if (active && body != null) {
                var direction = Vector3.zero;
                var forward = Vector3.ProjectOnPlane(flightRotation * Vector3.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < MinimumDirectionSquared) forward = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, forward);
                if (Down(ForwardKey)) direction += forward;
                if (Down(BackKey)) direction -= forward;
                if (Down(RightKey)) direction += right;
                if (Down(LeftKey)) direction -= right;
                if (Down(UpKey)) direction += Vector3.up;
                if (Down(DownKey)) direction -= Vector3.up;
                bool boost = Down(BoostKey);
                if (ReadController(out var pad)) {
                    direction += forward * ControllerInput.Axis(pad.leftY) + right * ControllerInput.Axis(pad.leftX);
                    direction += Vector3.up * (ControllerInput.Trigger(pad.rightTrigger) - ControllerInput.Trigger(pad.leftTrigger));
                    boost |= (pad.buttons & LeftBumper) != 0;
                    flightRotation = Quaternion.AngleAxis(ControllerInput.Axis(pad.rightX) * TurnDegreesPerSecond * Time.deltaTime, Vector3.up) * flightRotation;
                }
                float speed = boost ? BoostSpeed : FlightSpeed;
                var requested = flightPosition + Vector3.ClampMagnitude(direction, 1f) * speed * Time.deltaTime;
                flightPosition = requested;
                body.rotation = flightRotation;
                body.position = requested;
            }
        } catch (Exception e) { Debug.LogError("oth-trainer: " + e); Stop(); }
    }
    void TeleportToPin(OverlandNetworkRunner runner)
    {
        if (active) { Plugin.Logger.LogWarning("Disable flight before teleporting to a map pin."); return; }
        if (runner == null || !runner.IsSinglePlayer || !OverlandNetworkRunner.IsLocalPlayerSpawned || runner.LocalPlayerNetworkObject == null) {
            Plugin.Logger.LogWarning("Teleport unavailable: single-player vehicle unavailable."); return;
        }
        var player = runner.LocalPlayerNetworkObject.GetComponent<FusionPlayer>();
        if (player == null || player.vehicleBase == null || player.vehicleBase.cachedRigidbody == null) {
            Plugin.Logger.LogWarning("Teleport unavailable: local vehicle not found."); return;
        }
        var map = MapNetworkManager.Instance;
        if (map == null || !map.PlayerRefToPinpointPosition.TryGet(OverlandNetworkRunner.LocalPlayerRef, out _)) {
            Plugin.Logger.LogWarning("Teleport unavailable: place a map pin first."); return;
        }
        CheatsController.TeleportAtPin(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<CommandTerminal.CommandArg>(0));
        Plugin.Logger.LogInfo("Requested teleport to the local player's map pin.");
    }
    void StartFlight(OverlandNetworkRunner runner)
    {
        if (runner == null || !runner.IsSinglePlayer || !OverlandNetworkRunner.IsLocalPlayerSpawned || runner.LocalPlayerNetworkObject == null) { Debug.LogWarning("oth-trainer: single-player vehicle unavailable."); return; }
        var player = runner.LocalPlayerNetworkObject.GetComponent<FusionPlayer>();
        vehicle = player != null ? player.vehicleBase : null;
        body = vehicle != null ? vehicle.cachedRigidbody : null;
        if (body == null) { Debug.LogWarning("oth-trainer: local vehicle not found."); return; }
        gravity = body.useGravity; kinematic = body.isKinematic;
        colliders.Clear();
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) { if (c != null) colliders.Add((c, c.enabled)); }
        active = true;
        PoseSuppression.Begin(body, IsCurrentLocalVehicle);
        foreach (var item in colliders) item.collider.enabled = false;
        if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        body.useGravity = false; body.isKinematic = true;
        var forward = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < MinimumDirectionSquared)
            forward = Vector3.Cross(Vector3.ProjectOnPlane(body.transform.right, Vector3.up), Vector3.up);
        if (forward.sqrMagnitude < MinimumDirectionSquared) forward = Vector3.forward;
        flightPosition = body.position;
        flightRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        body.rotation = flightRotation;
        Plugin.Logger.LogInfo($"Flight enabled: vehicle={vehicle.name}, position={body.position}, colliders={colliders.Count}, originalKinematic={kinematic}, originalGravity={gravity}.");
    }
    bool IsCurrentLocalVehicle()
    {
        var runner = OverlandNetworkRunner.Instance;
        if (!active || body == null || vehicle == null || runner == null || !runner.IsSinglePlayer || !OverlandNetworkRunner.IsLocalPlayerSpawned || runner.LocalPlayerNetworkObject == null) return false;
        var player = runner.LocalPlayerNetworkObject.GetComponent<FusionPlayer>();
        return player != null && player.vehicleBase != null && player.vehicleBase.Pointer == vehicle.Pointer && vehicle.cachedRigidbody != null && vehicle.cachedRigidbody.Pointer == body.Pointer;
    }
    internal void Stop() {
        if (!active) return;
        active = false;
        PoseSuppression.End();
        foreach (var item in colliders) {
            try { if (item.collider != null) item.collider.enabled = item.enabled; }
            catch (Exception e) { Plugin.Logger.LogWarning("Collider restoration failed: " + e.Message); }
        }
        colliders.Clear();
        try { if (body != null) body.useGravity = gravity; }
        catch (Exception e) { Plugin.Logger.LogWarning("Gravity restoration failed: " + e.Message); }
        try { if (body != null) body.isKinematic = kinematic; }
        catch (Exception e) { Plugin.Logger.LogWarning("Kinematic restoration failed: " + e.Message); }
        body = null; vehicle = null;
        Plugin.Logger.LogInfo("Flight disabled.");
    }
}



