using System;
using UnityEngine;
using Il2CppFusion.Addons.Physics;

internal sealed class VehicleFlipTarget : IVehicleFlipTarget
{
    private const float RecoveryLift = 0.75f;
    private readonly LocalVehicleProvider vehicles;
    private readonly LocalVehicle local;
    private readonly Func<bool> flightActive;
    private readonly NetworkRigidbody3D physics;

    private VehicleFlipTarget(LocalVehicleProvider vehicles, LocalVehicle local, Func<bool> flightActive, NetworkRigidbody3D physics)
    { this.vehicles = vehicles; this.local = local; this.flightActive = flightActive; this.physics = physics; }

    internal static IVehicleFlipTarget Resolve(LocalVehicleProvider vehicles, Func<bool> flightActive)
    {
        if (!vehicles.TryGet(out var local)) return null;
        var physics = local.Vehicle.GetComponent<NetworkRigidbody3D>();
        return physics == null ? null : new VehicleFlipTarget(vehicles, local, flightActive, physics);
    }

    public bool Eligible => !flightActive() && vehicles.IsCurrent(local) && local.Vehicle.initialized &&
        local.Vehicle.HasStateAuthority && !local.Body.isKinematic && physics != null &&
        physics.HasStateAuthority && physics.Rigidbody != null && physics.Rigidbody.Pointer == local.Body.Pointer;

    public bool Upright => Eligible && Vector3.Dot(local.Body.rotation * Vector3.up, Vector3.up) > 0.7f;
    public string Pose => "body=" + local.Body.position + ", up=" + (local.Body.rotation * Vector3.up) +
        ", transform=" + local.Body.transform.position + ", transformUp=" + local.Body.transform.up +
        ", physics=" + physics.RBPosition + ", physicsUp=" + (physics.RBRotation * Vector3.up) +
        ", speed=" + local.Body.velocity.magnitude;

    public void Flip()
    {
        if (!Eligible) throw new InvalidOperationException("The original car is no longer eligible.");
        var body = local.Body;
        var forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.Cross(body.rotation * Vector3.right, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        var position = body.position + Vector3.up * RecoveryLift;
        var rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        // VehicleBase updates transform/wheel bookkeeping, while Fusion owns physics and interpolation state.
        local.Vehicle.Reposition(position, rotation);
        physics.Teleport(new Il2CppSystem.Nullable<Vector3>(position), new Il2CppSystem.Nullable<Quaternion>(rotation));
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }
}
