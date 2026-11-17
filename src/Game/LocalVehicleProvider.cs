using Il2Cpp;
using UnityEngine;
using VehiclePhysics = Il2CppVehiclePhysics;

internal sealed class LocalVehicle
{
    internal VehiclePhysics.VehicleBase Vehicle { get; }
    internal Rigidbody Body { get; }

    internal LocalVehicle(VehiclePhysics.VehicleBase vehicle, Rigidbody body)
    {
        Vehicle = vehicle;
        Body = body;
    }
}

// One eligibility and identity boundary for every local-vehicle feature.
internal sealed class LocalVehicleProvider
{
    internal bool SessionReady
    {
        get
        {
            var runner = OverlandNetworkRunner.Instance;
            return runner != null && runner.IsSinglePlayer && OverlandNetworkRunner.IsLocalPlayerSpawned;
        }
    }

    internal bool TryGet(out LocalVehicle local)
    {
        local = null;
        if (!TryResolve(out var vehicle, out var body)) return false;
        local = new LocalVehicle(vehicle, body);
        return true;
    }

    private bool TryResolve(out VehiclePhysics.VehicleBase vehicle, out Rigidbody body)
    {
        vehicle = null;
        body = null;
        if (!SessionReady) return false;
        var playerObject = OverlandNetworkRunner.Instance.LocalPlayerNetworkObject;
        var player = playerObject != null ? playerObject.GetComponent<FusionPlayer>() : null;
        vehicle = player != null ? player.vehicleBase : null;
        body = vehicle != null ? vehicle.cachedRigidbody : null;
        return body != null;
    }

    internal bool IsCurrent(LocalVehicle expected)
    {
        return expected != null && expected.Vehicle != null && expected.Body != null &&
            TryResolve(out var vehicle, out var body) && vehicle.Pointer == expected.Vehicle.Pointer &&
            body.Pointer == expected.Body.Pointer;
    }
}
