using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using Il2Cpp;
using VehiclePhysics = Il2CppVehiclePhysics;
using CommandTerminal = Il2CppCommandTerminal;
using UnityEngine;

[assembly: MelonInfo(typeof(Plugin), "Port's Trainer", "0.7.0", "Port")]
[assembly: MelonGame("Funselektor", "over the hill")]

public class Plugin : MelonMod
{
    internal static TrainerLogger Logger;
    private Flight flight;
    private TrainerInput input;
    private TrainerMenu menu;
    private GameInputBlocker gameInput;
    private GameBindingSuppression gameBindings;
    public override void OnInitializeMelon()
    {
        Logger = new TrainerLogger(LoggerInstance);
        PoseSuppression.Install();
        input = new TrainerInput();
        menu = new TrainerMenu(input, Path.Combine(MelonEnvironment.UserDataDirectory, "PortsTrainer.bindings.cfg"));
        flight = new Flight(input, menu);
        gameInput = new GameInputBlocker(() => {
            var runner = OverlandNetworkRunner.Instance;
            return runner != null && runner.IsSinglePlayer && OverlandNetworkRunner.IsLocalPlayerSpawned;
        }, AcquireGameInputBlock, Logger.LogWarning);
        gameBindings = new GameBindingSuppression(IsInputEligible, () => flight.Active, () => menu.Bindings);
        Logger.LogInfo("Port's Trainer: Insert opens the controls menu. Camera-relative flight. Default F8 flight / F9 map-pin teleport. v0.7.0.");
    }
    public override void OnUpdate()
    {
        input?.Poll(); menu?.Update(); flight?.Update();
        if (input != null && menu != null && flight != null)
            gameInput.Update(menu.Visible, input.Released);
        gameBindings?.Update();
    }
    public override void OnLateUpdate() { flight?.LateUpdate(); menu?.MaintainCursor(); }
    public override void OnGUI() { menu?.Draw(); }
    public override void OnDeinitializeMelon() { menu?.Close(); flight?.Stop(); gameInput?.Release(); gameBindings?.Dispose(); }

    private static bool IsInputEligible()
    {
        var runner = OverlandNetworkRunner.Instance;
        return GameInput.IsReady && runner != null && runner.IsSinglePlayer && OverlandNetworkRunner.IsLocalPlayerSpawned;
    }

    private static Action AcquireGameInputBlock()
    {
        const int trainerMapPriority = 10000;
        var token = GameInput.PushMap(GameInput.Map.GameHasNoFocus, trainerMapPriority);
        Logger.LogInfo("Trainer acquired gameplay input block.");
        return () => { GameInput.PopMap(token); Logger.LogInfo("Trainer released gameplay input block."); };
    }

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
    const float FlightSpeed = 45f, BoostSpeed = 135f;
    const float MinimumDirectionSquared = 0.01f;
    private readonly TrainerInput input;
    private readonly TrainerMenu menu;
    internal Flight(TrainerInput input, TrainerMenu menu) { this.input = input; this.menu = menu; }
    internal bool Active => active;
    private float Value(TrainerAction action) => menu.BlocksActions ? 0f : input.Value(menu.Bindings, action);
    bool active, gravity, kinematic;
    Rigidbody body;
    VehiclePhysics.VehicleBase vehicle;
    Vector3 flightPosition;
    Quaternion flightRotation;
    readonly List<(Collider collider, bool enabled)> colliders = new();
    internal void Update()
    {
        try {
            var runner = OverlandNetworkRunner.Instance;
            if (active && !IsCurrentLocalVehicle()) Stop();
            if (!menu.BlocksActions && input.Pressed(menu.Bindings, TrainerAction.Flight)) { if (active) Stop(); else StartFlight(runner); }
            if (!menu.BlocksActions && input.Pressed(menu.Bindings, TrainerAction.Teleport)) TeleportToPin(runner);
        } catch (Exception e) { Debug.LogError("Port's Trainer: " + e); Stop(); }
    }
    internal void LateUpdate()
    {
        try {
            if (active && body != null) {
                // Keep position and heading still while editing or unfocused.
                if (!menu.BlocksActions)
                {
                    var camera = Camera.main;
                    if (camera != null && camera.isActiveAndEnabled)
                    {
                        var view = camera.transform.forward;
                        var previous = flightRotation * Vector3.forward;
                        var heading = FlightHeading.Horizontal(view.x, view.z, previous.x, previous.z);
                        flightRotation = Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.z), Vector3.up);
                    }
                }
                var direction = Vector3.zero;
                var forward = Vector3.ProjectOnPlane(flightRotation * Vector3.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < MinimumDirectionSquared) forward = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, forward);
                direction += forward * (Value(TrainerAction.Forward) - Value(TrainerAction.Back));
                direction += right * (Value(TrainerAction.Right) - Value(TrainerAction.Left));
                direction += Vector3.up * (Value(TrainerAction.Rise) - Value(TrainerAction.Descend));
                bool boost = Value(TrainerAction.Boost) > 0.5f;
                float speed = boost ? BoostSpeed : FlightSpeed;
                var requested = flightPosition + Vector3.ClampMagnitude(direction, 1f) * speed * Time.deltaTime;
                flightPosition = requested;
                body.rotation = flightRotation;
                body.position = requested;
            }
        } catch (Exception e) { Debug.LogError("Port's Trainer: " + e); Stop(); }
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
        if (runner == null || !runner.IsSinglePlayer || !OverlandNetworkRunner.IsLocalPlayerSpawned || runner.LocalPlayerNetworkObject == null) { Debug.LogWarning("Port's Trainer: single-player vehicle unavailable."); return; }
        var player = runner.LocalPlayerNetworkObject.GetComponent<FusionPlayer>();
        vehicle = player != null ? player.vehicleBase : null;
        body = vehicle != null ? vehicle.cachedRigidbody : null;
        if (body == null) { Debug.LogWarning("Port's Trainer: local vehicle not found."); return; }
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



