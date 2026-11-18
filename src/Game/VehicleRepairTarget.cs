using System;
using Il2Cpp;

internal sealed class VehicleRepairTarget : IVehicleRepairTarget
{
    private readonly LocalVehicleProvider vehicles;
    private readonly LocalVehicle local;
    private readonly CarDamageSystem damage;

    private VehicleRepairTarget(LocalVehicleProvider vehicles, LocalVehicle local, CarDamageSystem damage)
    {
        this.vehicles = vehicles;
        this.local = local;
        this.damage = damage;
    }

    internal static IVehicleRepairTarget Resolve(LocalVehicleProvider vehicles)
    {
        if (!vehicles.TryGet(out var local)) return null;
        var damage = local.Vehicle.GetComponentInChildren<CarDamageSystem>(true);
        if (damage == null) damage = local.Vehicle.GetComponentInParent<CarDamageSystem>();
        return damage == null ? null : new VehicleRepairTarget(vehicles, local, damage);
    }

    public bool Eligible => vehicles.IsCurrent(local) && local.Vehicle.initialized &&
        local.Vehicle.HasStateAuthority && damage != null && damage.HasStateAuthority &&
        damage.vehicle != null && damage.vehicle.Pointer == local.Vehicle.Pointer;

    private void RequireEligible()
    {
        if (!Eligible) throw new InvalidOperationException("The original local car is no longer available with state authority.");
    }

    public float Damage { get { RequireEligible(); return damage.DamageAmount; } }
    public float MaximumDamage { get { RequireEligible(); return damage.MaxDamageAmount; } }

    public void Heal(float amount)
    {
        RequireEligible();
        var player = OverlandNetworkRunner.LocalPlayerRef;
        // Use the game's healing operation, including its visual/event path. No inventory operation.
        damage.HealPlayer(player, player, amount, CarDamageSystem.HealthChangingReason.HealedBySelf);
    }
}
