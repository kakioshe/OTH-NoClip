using System;
using System.Reflection;
using HarmonyLib;
using VP = Il2CppVehiclePhysics;

internal sealed class VehicleOverdriveHooks : IDisposable
{
    private static VehicleOverdriveHooks current;
    private readonly HarmonyLib.Harmony hooks = new("local.oth.vehicle.overdrive");
    private readonly LocalVehicleProvider vehicles;
    private readonly Func<bool> flightActive;
    private readonly TrainerLogger logger;
    private readonly bool ready;
    private VehicleOverdriveTarget target;
    internal Action<string> Failed { get; set; }

    internal VehicleOverdriveHooks(LocalVehicleProvider vehicles, Func<bool> flightActive, TrainerLogger logger)
    {
        this.vehicles = vehicles; this.flightActive = flightActive; this.logger = logger;
        current = this;
        try
        {
            var method = typeof(VP.FunselektorFusionVPStandardInput).GetMethod("ApplyVehicleInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (method == null) throw new MissingMethodException("ApplyVehicleInput");
            hooks.Patch(method, prefix: Hook(nameof(InputPrefix)), postfix: Hook(nameof(InputPostfix)), finalizer: Hook(nameof(InputFinalizer)));
            var disable = typeof(VP.VehicleBase).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (disable == null) throw new MissingMethodException("VehicleBase.OnDisable");
            hooks.Patch(disable, prefix: Hook(nameof(DisablePrefix)));
            ready = true;
            logger.LogInfo("High-range overdrive input/teardown hooks installed; interception requires runtime validation.");
        }
        catch (Exception error) { hooks.UnpatchSelf(); logger.LogWarning("High-range overdrive unavailable: " + error.Message); }
    }

    private static HarmonyMethod Hook(string name) => new(typeof(VehicleOverdriveHooks), name);

    internal IVehicleOverdriveTarget Resolve()
    {
        if (!ready) throw new InvalidOperationException("Required overdrive hooks are unavailable.");
        if (!vehicles.TryGet(out var local)) return null;
        target = new VehicleOverdriveTarget(vehicles, local, logger.LogInfo);
        return target;
    }

    private static void InputPrefix(VP.FunselektorFusionVPStandardInput __instance, out VehicleOverdriveTarget __state)
    {
        __state = null;
        var owner = current;
        try
        {
            var active = owner?.target;
            if (active == null || active.Stopped || __instance == null || active.Input.Pointer != __instance.Pointer) return;
            active.BeforeInput();
            __state = active;
        }
        catch (Exception error) { owner?.Fail(error); }
    }

    private static void InputPostfix(VehicleOverdriveTarget __state)
    {
        var owner = current;
        try { if (__state != null && __state == owner?.target) __state.AfterInput(owner.flightActive()); }
        catch (Exception error) { owner?.Fail(error); }
    }

    private static Exception InputFinalizer(Exception __exception, VehicleOverdriveTarget __state)
    {
        if (__exception != null && __state != null) current?.Fail(__exception);
        return __exception;
    }

    private static void DisablePrefix(VP.VehicleBase __instance)
    {
        var owner = current;
        if (owner?.target == null || owner.target.Stopped || __instance == null || owner.target.Controller.Pointer != __instance.Pointer) return;
        owner.Fail(new InvalidOperationException("Original vehicle is disabling."));
    }

    private void Fail(Exception error)
    {
        var previous = target;
        target = null;
        try { previous?.Dispose(); }
        catch (Exception cleanup) { logger.LogWarning("Overdrive cleanup: " + cleanup.Message); }
        Failed?.Invoke(error.Message);
    }

    public void Dispose()
    {
        try { target?.Dispose(); }
        finally { target = null; if (current == this) current = null; hooks.UnpatchSelf(); }
    }
}
