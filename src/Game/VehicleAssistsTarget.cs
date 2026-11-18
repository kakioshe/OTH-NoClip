using System;
using Il2Cpp;
using UnityEngine;
using Vector = System.Numerics.Vector3;

internal sealed class VehicleAssistsTarget : IVehicleAssistsTarget
{
    private readonly LocalVehicleProvider vehicles;
    private readonly LocalVehicle local;
    private readonly Func<bool> flightActive, inputBlocked;
    private readonly FusionPlayer player;
    private readonly NetworkedVehicleStatus status;
    private readonly Action<string> log;
    internal readonly EngineFloodingSystem Flooding;
    private bool disposed, floodEnabled, climbEnabled;
    private int blockedFloods, climbSamples, immersionSamples, waterSamples, resistanceSamples;
    private float nextWaterSample;
    private bool attached;
    internal IntPtr VehiclePointer => local.Vehicle.Pointer;

    internal VehicleAssistsTarget(LocalVehicleProvider vehicles, LocalVehicle local, Func<bool> flightActive, Func<bool> inputBlocked, Action<string> log)
    {
        this.vehicles = vehicles;
        this.local = local;
        this.flightActive = flightActive;
        this.inputBlocked = inputBlocked;
        this.log = log;
        player = local.Vehicle.GetComponentInParent<FusionPlayer>();
        status = local.Vehicle.GetComponentInChildren<NetworkedVehicleStatus>(true);
        Flooding = local.Vehicle.GetComponentInChildren<EngineFloodingSystem>(true);
        if (Flooding == null) Flooding = local.Vehicle.GetComponentInParent<EngineFloodingSystem>();
        // A parent lookup must still resolve a component belonging to this car.
        if (Flooding != null && Flooding.GetComponentInParent<Il2CppVehiclePhysics.VehicleBase>()?.Pointer != VehiclePointer)
            Flooding = null;
    }

    public bool Eligible => !disposed && vehicles.IsCurrent(local) && local.Vehicle.initialized && local.Vehicle.HasStateAuthority;

    public void SetEnabled(bool flooding, bool climb)
    {
        if (!Eligible) throw new InvalidOperationException("Local vehicle or authority changed.");
        if (flooding && (Flooding == null || !Flooding.HasStateAuthority))
            throw new InvalidOperationException("Local engine flooding system unavailable.");
        if (floodEnabled != flooding || climbEnabled != climb)
            log("Vehicle assists: flooding=" + flooding + ", climb=" + climb + ".");
        floodEnabled = flooding;
        climbEnabled = climb;
    }

    internal bool PreventFlooding => floodEnabled && Eligible && Flooding != null && Flooding.HasStateAuthority && !Flooding.IsFlooded;

    internal void BeforeFloodingUpdate()
    {
        // Prevent accumulated immersion; leave water detection, drying and other updates running.
        // Already-flooded engines remain flooded until the game dries them normally.
        if (PreventFlooding)
        {
            float immersed = Flooding.TimeImmersed;
            Flooding.TimeImmersed = 0;
            if (immersed > 0 && ++immersionSamples <= 4)
                log("Flooding immunity reset local immersion: before=" + immersed + ", after=" + Flooding.TimeImmersed + ".");
        }
    }

    internal bool AllowFloodTransition(bool flooded)
    {
        if (!flooded || !PreventFlooding) return true;
        if (++blockedFloods <= 4) log("Flooding immunity blocked local SetFlooded(true), event " + blockedFloods + ".");
        return false;
    }

    internal void StepSurfaceDrive()
    {
        TraceWaterResistance();
        if (!climbEnabled || !Eligible || flightActive() || local.Body.isKinematic || local.Vehicle.paused)
        { attached = false; return; }
        if (player == null || player.vehicleBase == null || player.vehicleBase.Pointer != VehiclePointer) return;
        var input = player.CurrentFrameInput;
        bool automaticReverse = (input.InputFlags & NetworkInputData.InputFlagsEnum.ThrottleAndBrakeBehaviour_AutoForwardAndReverse) != 0;
        float drive = automaticReverse ? input.Throttle - input.Brake :
            input.Throttle * (input.SelectedManualGear < 0 ? -1 : 1);
        if (!automaticReverse && input.Brake > 0.1f) drive = 0;
        float steer = input.Steering;
        if (status != null && status.HandbrakeSet) drive = steer = 0;
        if (inputBlocked()) drive = steer = 0;
        if (!TrySurface(drive, out var normal))
        {
            if (attached) log("Spider mode released: no nearby usable surface.");
            attached = false;
            return;
        }
        var body = local.Body;
        if (!SurfaceDrive.Solve(ToVector(normal), ToVector(body.rotation * Vector3.up),
            ToVector(body.rotation * Vector3.forward), ToVector(Physics.gravity), ToVector(body.velocity),
            ToVector(body.angularVelocity), drive, steer, Time.fixedDeltaTime,
            out var acceleration, out var angular)) return;
        body.AddForce(new Vector3(acceleration.X, acceleration.Y, acceleration.Z), ForceMode.Acceleration);
        body.AddTorque(new Vector3(angular.X, angular.Y, angular.Z), ForceMode.Acceleration);
        if (!attached || ++climbSamples % 250 == 0)
            log("Spider mode applied: normal=" + normal + ", drive=" + drive + ", steer=" + steer +
                ", speed=" + body.velocity.magnitude + ", acceleration=" + acceleration + ".");
        attached = true;
    }

    private bool TrySurface(float drive, out Vector3 normal)
    {
        normal = Vector3.zero;
        var body = local.Body;
        var up = body.rotation * Vector3.up;
        var forward = body.rotation * Vector3.forward;
        // Fresh probes supplement wheel contacts at crests and while pitching onto a wall.
        // No retained normal can apply forces after the surface is lost.
        bool below = CastSurface(body.worldCenterOfMass, -up, 2.5f, out var beneath);
        if (Math.Abs(drive) > 0.05f && CastSurface(body.worldCenterOfMass, forward * Math.Sign(drive), 3f, out var approach) &&
            Vector3.Dot(approach.normal, up) >= -0.1f)
        {
            normal = approach.normal;
            return true;
        }
        var states = local.Vehicle.wheelState;
        int count = 0;
        if (states != null && states.Length <= 32)
            for (int i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (state == null || !state.enabled || !state.grounded) continue;
                var n = state.wheelContact.normal;
                var direction = new Vector3(n.x, n.y, n.z);
                if (!Finite(direction) || direction.sqrMagnitude < 0.5f) continue;
                direction.Normalize();
                if (Vector3.Dot(direction, up) < 0.25f ||
                    (count > 0 && Vector3.Dot(normal.normalized, direction) < 0.5f)) continue;
                normal += direction;
                count++;
            }
        if (count > 0) { normal.Normalize(); return true; }
        if (!below || Vector3.Dot(beneath.normal, up) < 0.25f) return false;
        normal = beneath.normal;
        return true;
    }

    private bool CastSurface(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
    {
        nearest = default;
        float closest = distance + 1;
        // Ignore water/trigger volumes and all colliders belonging to this vehicle.
        var hits = Physics.RaycastAll(origin, direction, distance, ~(1 << 4), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.distance >= closest || !Finite(hit.normal)) continue;
            var car = hit.collider.GetComponentInParent<Il2CppVehiclePhysics.VehicleBase>();
            if (car != null) continue;
            if (hit.rigidbody != null && hit.rigidbody.Pointer == local.Body.Pointer) continue;
            closest = hit.distance;
            nearest = hit;
        }
        return closest <= distance;
    }

    private void TraceWaterResistance()
    {
        if (!floodEnabled || !Eligible || Flooding == null || waterSamples >= 12 || Time.unscaledTime < nextWaterSample) return;
        var detector = Flooding.waterDetector;
        if (detector == null || !detector.CarIsTouchingWater) return;
        nextWaterSample = Time.unscaledTime + 5f;
        waterSamples++;
        log("Water resistance sample: flooded=" + Flooding.IsFlooded + ", speed=" + local.Body.velocity.magnitude + ".");
        var states = local.Vehicle.wheelState;
        if (states == null || states.Length > 32) return;
        for (int i = 0; i < states.Length; i++)
        {
            var state = states[i];
            if (state == null) continue;
            var grip = state.dynamicGrip;
            var config = state.groundMaterial?.Config;
            log("Water wheel " + i + ": grounded=" + state.grounded + ", material=" + config?.Label +
                ", resistance=" + state.groundResistance + ", drag=" + grip.Drag + ", decel=" + grip.Decel +
                ", targetDrag=" + grip.TargetDrag + ", targetDecel=" + grip.TargetDecel + ".");
        }
    }

    internal void SuppressWaterResistance(Il2CppVehiclePhysics.Wheel wheel)
    {
        if (!floodEnabled || !Eligible || flightActive() || wheel == null || Flooding == null ||
            Flooding.waterDetector == null || !Flooding.waterDetector.CarIsTouchingWater) return;
        var wheels = local.Vehicle.m_wheels;
        if (wheels == null || wheels.Length > 32) return;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null || wheels[i].Pointer != wheel.Pointer) continue;
            // The game has just interpolated its transient wheel inputs for this substep.
            // Fg is terrain drag/deceleration; tire force, grip, brakes and torque stay intact.
            // The next substep recomputes it, so no setting/asset ownership is introduced.
            var state = wheel.m_int;
            var before = state.Fg;
            state.Fg = Vector2.zero;
            wheel.m_int = state;
            if (before.sqrMagnitude > 0.0001f && ++resistanceSamples <= 4)
                log("Water immunity suppressed local wheel resistance: wheel=" + i + ", before=" + before +
                    ", after=" + wheel.m_int.Fg + ".");
            return;
        }
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    private static Vector ToVector(Vector3 v) => new Vector(v.x, v.y, v.z);
    public void Dispose() { disposed = true; floodEnabled = climbEnabled = false; }
}
