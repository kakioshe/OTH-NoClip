using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using VP = Il2CppVehiclePhysics;

// Hooks only act on the currently owned original car. No global tuning/damage changes.
internal sealed class VehicleControlHooks : IDisposable
{
    private static VehicleControlHooks current;
    private readonly HarmonyLib.Harmony hooks = new("local.oth.vehicle.controls");
    private readonly LocalVehicleProvider vehicles;
    private readonly TrainerLogger logger;
    private VehicleControlsTarget target;
    private readonly bool ready;

    internal VehicleControlHooks(LocalVehicleProvider vehicles, TrainerLogger logger)
    {
        this.vehicles = vehicles;
        this.logger = logger;
        current = this;
        try
        {
            foreach (string writer in new[] { "GetBaseValuesFromVehicleConfig", "EngineTierChanged", "ApplyDriveabilityDebuffs", "ResetDriveabilityDebuffs", "SetDamageModifier" })
                Patch(typeof(CarDamageSystem), writer, nameof(DamageWriter));
            Patch(typeof(CarDamageSystem), "ApplyDamage", nameof(DamagePrefix));
            Patch(typeof(VP.VehicleBase), "SetWheelTireFrictionMultiplier", nameof(WheelWriter));
            Patch(typeof(VP.VehicleBase), "OnDisable", nameof(DisablePrefix));
            ready = true;
            logger.LogInfo("Vehicle controls writer/teardown hooks installed.");
        }
        catch (Exception error)
        {
            hooks.UnpatchSelf();
            logger.LogWarning("Vehicle modifiers/protection unavailable: " + error.Message);
        }
    }

    private void Patch(Type type, string name, string prefix)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (method == null) throw new MissingMethodException(type.Name, name);
        hooks.Patch(method, new HarmonyMethod(typeof(VehicleControlHooks), prefix));
    }

    internal IVehicleControlsTarget Resolve()
    {
        if (!ready) throw new InvalidOperationException("Required vehicle lifecycle hooks are unavailable.");
        if (!vehicles.TryGet(out var local)) return null;
        target = new VehicleControlsTarget(vehicles, local, logger.LogInfo);
        return target;
    }

    private static void DamageWriter(CarDamageSystem __instance)
    {
        var owner = current;
        if (owner?.target == null || owner.target.Writing || __instance == null || owner.target.Damage == null ||
            __instance.Pointer != owner.target.Damage.Pointer) return;
        owner.Cleanup(false);
    }

    private static void WheelWriter(VP.VehicleBase __instance)
    {
        var owner = current;
        if (owner?.target == null || owner.target.Writing || __instance == null ||
            __instance.Pointer != owner.target.Controller.Pointer) return;
        owner.Cleanup(false);
    }

    private static void DisablePrefix(VP.VehicleBase __instance)
    {
        var owner = current;
        if (owner?.target == null || __instance == null || __instance.Pointer != owner.target.Controller.Pointer) return;
        owner.Cleanup(true);
    }

    private static bool DamagePrefix(CarDamageSystem __instance)
    {
        var owner = current;
        try
        {
            if (owner?.target == null || __instance == null || owner.target.Damage == null ||
                __instance.Pointer != owner.target.Damage.Pointer || !owner.target.Protected || !owner.target.Eligible) return true;
            owner.target.RecordDamageBlocked();
            return false;
        }
        catch (Exception error) { owner?.logger.LogWarning("Damage protection eligibility failed: " + error.Message); return true; }
    }

    private void Cleanup(bool disabling)
    {
        try
        {
            if (disabling) target.BeforeDisable();
            else target.BeforeGameWriter();
        }
        catch (Exception error)
        {
            logger.LogWarning("Vehicle writer cleanup failed: " + error.Message);
            try { target.Dispose(); }
            catch (Exception cleanupError) { logger.LogWarning("Vehicle cleanup unavailable: " + cleanupError.Message); }
        }
    }

    public void Dispose()
    {
        try { target?.Dispose(); }
        finally { target = null; if (current == this) current = null; hooks.UnpatchSelf(); }
    }
}
