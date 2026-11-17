using System;
using System.Collections.Generic;
using UnityEngine;

internal readonly struct FlightControls
{
    internal readonly float Forward, Right, Rise;
    internal readonly bool Boost, Hold;

    internal FlightControls(float forward, float right, float rise, bool boost, bool hold)
    {
        Forward = forward;
        Right = right;
        Rise = rise;
        Boost = boost;
        Hold = hold;
    }
}

internal sealed class Flight
{
    private const float FlightSpeed = 45f;
    private const float BoostSpeed = 135f;
    private const float MinimumDirectionSquared = 0.01f;
    private readonly LocalVehicleProvider vehicles;
    private readonly PoseSuppression pose;
    private readonly TrainerLogger logger;
    private LocalVehicle local;

    internal Flight(LocalVehicleProvider vehicles, PoseSuppression pose, TrainerLogger logger)
    {
        this.vehicles = vehicles;
        this.pose = pose;
        this.logger = logger;
    }

    internal bool Active => active;
    private bool active, gravity, kinematic;
    private Rigidbody body;
    private Vector3 flightPosition;
    private Quaternion flightRotation;
    private readonly List<(Collider collider, bool enabled)> colliders = new();
    internal void Update()
    {
        try { if (active && !IsCurrentLocalVehicle()) Stop(); }
        catch (Exception error) { logger.LogError("Flight eligibility failed: " + error); Stop(); }
    }

    internal void Toggle()
    {
        try { if (active) Stop(); else StartFlight(); }
        catch (Exception error) { logger.LogError("Flight toggle failed: " + error); Stop(); }
    }

    internal void LateUpdate(FlightControls controls)
    {
        if (!active || body == null) return;
        try
        {
            // Keep position and heading still while editing or unfocused.
            if (!controls.Hold) UpdateCameraHeading();
            Move(controls);
        }
        catch (Exception error) { logger.LogError("Flight failed: " + error); Stop(); }
    }

    private void UpdateCameraHeading()
    {
        var camera = Camera.main;
        if (camera == null || !camera.isActiveAndEnabled) return;
        var view = camera.transform.forward;
        var previous = flightRotation * Vector3.forward;
        var heading = FlightHeading.Horizontal(view.x, view.z, previous.x, previous.z);
        flightRotation = Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.z), Vector3.up);
    }

    private void Move(FlightControls controls)
    {
        var forward = Vector3.ProjectOnPlane(flightRotation * Vector3.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < MinimumDirectionSquared) forward = Vector3.forward;
        var right = Vector3.Cross(Vector3.up, forward);
        var direction = forward * controls.Forward + right * controls.Right + Vector3.up * controls.Rise;
        float speed = controls.Boost ? BoostSpeed : FlightSpeed;
        flightPosition += Vector3.ClampMagnitude(direction, 1f) * speed * Time.deltaTime;
        body.rotation = flightRotation;
        body.position = flightPosition;
    }
    private void StartFlight()
    {
        if (!vehicles.TryGet(out local))
        {
            logger.LogWarning("Flight unavailable: local single-player vehicle not found.");
            return;
        }
        body = local.Body;
        gravity = body.useGravity; kinematic = body.isKinematic;
        colliders.Clear();
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) { if (c != null) colliders.Add((c, c.enabled)); }
        active = true;
        pose.Begin(body, IsCurrentLocalVehicle);
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
        logger.LogInfo($"Flight enabled: vehicle={local.Vehicle.name}, position={body.position}, colliders={colliders.Count}, originalKinematic={kinematic}, originalGravity={gravity}.");
    }
    private bool IsCurrentLocalVehicle() => active && vehicles.IsCurrent(local);

    internal void Stop() {
        if (!active) return;
        active = false;
        pose.End();
        foreach (var item in colliders) {
            try { if (item.collider != null) item.collider.enabled = item.enabled; }
            catch (Exception e) { logger.LogWarning("Collider restoration failed: " + e.Message); }
        }
        colliders.Clear();
        try { if (body != null) body.useGravity = gravity; }
        catch (Exception e) { logger.LogWarning("Gravity restoration failed: " + e.Message); }
        try { if (body != null) body.isKinematic = kinematic; }
        catch (Exception e) { logger.LogWarning("Kinematic restoration failed: " + e.Message); }
        body = null; local = null;
        logger.LogInfo("Flight disabled.");
    }
}
