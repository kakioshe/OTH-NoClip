using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using VP = Il2CppVehiclePhysics;

internal sealed class VehicleAssistHooks : IDisposable
{
    private static VehicleAssistHooks current;
    private readonly HarmonyLib.Harmony hooks = new("local.oth.vehicle.assists");
    private readonly LocalVehicleProvider vehicles;
    private readonly Func<bool> flightActive, inputBlocked;
    private readonly Action<string> report;
    private readonly Action<string> log;
    private readonly bool ready;
    private VehicleAssistsTarget target;
    internal Action<string> Failed { get; set; }

    internal VehicleAssistHooks(LocalVehicleProvider vehicles, Func<bool> flightActive, Func<bool> inputBlocked, Action<string> report, Action<string> log)
    {
        this.vehicles = vehicles; this.flightActive = flightActive; this.inputBlocked = inputBlocked; this.report = report;
        this.log = log;
        current = this;
        try
        {
            Patch(typeof(EngineFloodingSystem), "FixedUpdateNetwork", nameof(FloodUpdate), false);
            Patch(typeof(EngineFloodingSystem), "SetFlooded", nameof(FloodTransition), false);
            Patch(typeof(VP.VehicleBase), "FixedUpdateNetwork", nameof(VehicleStep), true);
            Patch(typeof(VP.Wheel), "SetSubstepState", nameof(WheelSubstep), true);
            Patch(typeof(VP.VehicleBase), "OnDisable", nameof(VehicleDisabled), false);
            ready = true;
            log("Local Water immunity and Spider mode hooks installed.");
        }
        catch (Exception error) { hooks.UnpatchSelf(); report("Vehicle assists unavailable: " + error.Message); }
    }

    private void Patch(Type type, string name, string callback, bool postfix)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (method == null) throw new MissingMethodException(type.Name, name);
        var hook = new HarmonyMethod(typeof(VehicleAssistHooks), callback);
        hooks.Patch(method, prefix: postfix ? null : hook, postfix: postfix ? hook : null);
    }

    internal IVehicleAssistsTarget Resolve()
    {
        if (!ready) throw new InvalidOperationException("Required vehicle assist hooks are unavailable.");
        if (!vehicles.TryGet(out var local)) return null;
        target = new VehicleAssistsTarget(vehicles, local, flightActive, inputBlocked, log);
        return target;
    }

    private static void FloodUpdate(EngineFloodingSystem __instance)
    {
        var owner = current;
        try { if (owner?.Matches(__instance) == true) owner.target.BeforeFloodingUpdate(); }
        catch (Exception error) { owner?.Fail(error); }
    }

    private static bool FloodTransition(EngineFloodingSystem __instance, bool __0)
    {
        var owner = current;
        try { return owner?.Matches(__instance) != true || owner.target.AllowFloodTransition(__0); }
        catch (Exception error) { owner?.Fail(error); return true; }
    }

    private bool Matches(EngineFloodingSystem instance) => target != null && instance != null && target.Flooding != null && instance.Pointer == target.Flooding.Pointer;

    private static void VehicleStep(VP.VehicleBase __instance)
    {
        var owner = current;
        try { if (owner?.target != null && __instance != null && __instance.Pointer == owner.target.VehiclePointer) owner.target.StepSurfaceDrive(); }
        catch (Exception error) { owner?.Fail(error); }
    }

    private static void WheelSubstep(VP.Wheel __instance)
    {
        var owner = current;
        try { owner?.target?.SuppressWaterResistance(__instance); }
        catch (Exception error) { owner?.Fail(error); }
    }

    private static void VehicleDisabled(VP.VehicleBase __instance)
    {
        var owner = current;
        if (owner?.target != null && __instance != null && __instance.Pointer == owner.target.VehiclePointer) owner.target.Dispose();
    }

    private void Fail(Exception error)
    {
        target?.Dispose();
        string message = "Vehicle assists stopped: " + error.Message;
        if (Failed != null) Failed(message); else report(message);
    }

    public void Dispose()
    {
        try { target?.Dispose(); }
        finally { target = null; if (current == this) current = null; hooks.UnpatchSelf(); }
    }
}
