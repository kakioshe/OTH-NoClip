using System;
using System.Globalization;
using Il2Cpp;
using UnityEngine;
using VP = Il2CppVehiclePhysics;

internal sealed class VehicleOverdriveTarget : IVehicleOverdriveTarget
{
    private readonly LocalVehicleProvider vehicles;
    private readonly LocalVehicle local;
    internal readonly VP.VPVehicleController Controller;
    internal readonly VP.FunselektorFusionVPStandardInput Input;
    private readonly VP.Differential.Settings differential;
    private readonly VP.Driveline driveline;
    private readonly VP.Gearbox gearbox;
    private readonly OverdriveRatio ratio;
    private readonly Action<string> log;
    private float multiplier = 1, nextSample;
    private int samples;
    private bool stopped, paused;
    internal bool Stopped => stopped;

    internal VehicleOverdriveTarget(LocalVehicleProvider vehicles, LocalVehicle local, Action<string> log)
    {
        this.vehicles = vehicles; this.local = local; this.log = log;
        Controller = local.Vehicle.TryCast<VP.VPVehicleController>();
        if (Controller == null) throw new InvalidOperationException("Unsupported vehicle controller.");
        Input = Controller.GetComponentInChildren<VP.FunselektorFusionVPStandardInput>(true);
        if (Input == null) Input = Controller.GetComponentInParent<VP.FunselektorFusionVPStandardInput>();
        differential = Controller.differential;
        driveline = Controller.m_driveline;
        gearbox = Controller.m_gearbox;
        ValidateIdentity();
        ValidateIsolation();
        ratio = new OverdriveRatio(() => { ValidateIdentity(); return differential.gearRatio; },
            value => { ValidateIdentity(); differential.gearRatio = value; });
        log("Overdrive captured car=" + Controller.Pointer + " input=" + Input.Pointer +
            " differential=" + differential.Pointer + " driveline=" + driveline.Pointer + " gearbox=" + gearbox.Pointer + ".");
    }

    public bool Eligible => !stopped && vehicles.IsCurrent(local) && Controller != null && Controller.initialized &&
        Controller.HasStateAuthority && Input != null && Input.HasStateAuthority;
    public float Speed => local.Body.linearVelocity.magnitude;

    private void ValidateIdentity()
    {
        // Cleanup deliberately checks the captured objects, not a newly resolved car.
        if (Controller == null || !Controller.initialized || Input == null || Input.vpVehicleController == null ||
            Input.vpVehicleController.Pointer != Controller.Pointer || differential == null || driveline == null || gearbox == null ||
            Controller.differential == null || Controller.differential.Pointer != differential.Pointer ||
            Controller.m_driveline == null || Controller.m_driveline.Pointer != driveline.Pointer ||
            driveline.axleDifferential == null || driveline.axleDifferential.Pointer != differential.Pointer ||
            Controller.m_gearbox == null || Controller.m_gearbox.Pointer != gearbox.Pointer || Controller.highLowGearsDifferential == null)
            throw new InvalidOperationException("Original drivetrain identity is unavailable or changed.");
        ValidateAxles();
    }

    private void ValidateIsolation()
    {
        var customization = Controller.GetComponentInChildren<VehicleCustomizationController>(true);
        if (customization == null) customization = Controller.GetComponentInParent<VehicleCustomizationController>();
        var car = customization != null ? customization.CurrentCarInfo : null;
        var setup = car != null ? car.carCategorySetup : null;
        if (setup == null) throw new InvalidOperationException("Cannot verify drivetrain settings isolation.");
        void Reject(VP.Differential.Settings other)
        {
            if (other != null && other.Pointer == differential.Pointer)
                throw new InvalidOperationException("Shared differential settings cannot be modified.");
        }
        Reject(setup.centerDifferential); Reject(setup.interAxleDifferential);
        Reject(Controller.centerDifferential); Reject(Controller.interAxleDifferential);
        foreach (var other in UnityEngine.Object.FindObjectsOfType<VP.VPVehicleController>(true))
        {
            if (other == null || other.Pointer == Controller.Pointer) continue;
            Reject(other.differential); Reject(other.centerDifferential); Reject(other.interAxleDifferential);
        }
    }

    private void ValidateAxles()
    {
        int matched = 0;
        void CheckAxles(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<VP.Differential> axles)
        {
            if (axles == null) return;
            foreach (var axle in axles)
            {
                if (axle == null || axle.settings == null || axle.settings.Pointer != differential.Pointer)
                    throw new InvalidOperationException("Active wheel differential settings do not match.");
                matched++;
            }
        }
        CheckAxles(driveline.m_frontDifferentials); CheckAxles(driveline.m_rearDifferentials);
        if (matched == 0) throw new InvalidOperationException("No active wheel differential could be verified.");
    }

    public void SetMultiplier(float value)
    {
        if (!VehicleOverdrive.ValidMultiplier(value)) throw new ArgumentOutOfRangeException(nameof(value));
        ratio.Release();
        ValidateIdentity(); ValidateIsolation();
        multiplier = value;
        samples = 0; nextSample = 0;
        log("High-range overdrive selected " + value.ToString(CultureInfo.InvariantCulture) + "x; bounded input telemetry restarted (90 samples maximum).");
    }

    public void Pause(bool value)
    {
        ValidateIdentity();
        paused = value;
        if (paused) ratio.Release();
    }

    internal void BeforeInput() => ratio.BeforeWriter();

    internal void AfterInput(bool flightActive)
    {
        if (!Eligible) { Dispose(); return; }
        ValidateIdentity();
        bool low = Input.GearboxIsLowTransferCase;
        bool enabled = Input.inputIsEnabled;
        if (Controller.data == null) throw new InvalidOperationException("Vehicle input bus is unavailable.");
        int requested = Controller.data.Get(VP.Channel.Input, VP.InputData.ManualGear);
        float baseline = differential.gearRatio;
        float authored = low ? Controller.highLowGearsDifferential.lowGearsGearRatio : Controller.highLowGearsDifferential.highGearsGearRatio;
        if (enabled && baseline != authored)
            throw new InvalidOperationException("Input writer reduction did not match the selected range.");
        ratio.AfterWriter(paused || flightActive ? 1 : multiplier, enabled, low,
            requested, gearbox.sensorEngagedGear, gearbox.sensorSwitchingGears);
        if (samples >= 90 || Time.realtimeSinceStartup < nextSample) return;
        nextSample = Time.realtimeSinceStartup + 1;
        samples++;
        var engine = Controller.m_engine;
        string wheels = "";
        var states = Controller.wheelState;
        if (states != null)
            for (int index = 0; index < states.Length; index++)
                if (states[index] != null) wheels += FormattableString.Invariant($" w{index}={states[index].angularVelocity:F2}/slip={states[index].slipRatio:F3}");
        log(FormattableString.Invariant($"Overdrive input #{samples}: authority={Controller.HasStateAuthority} enabled={enabled} low={low} selected={Input.SelectedManualGear} requested={requested} engaged={gearbox.sensorEngagedGear} shifting={gearbox.sensorSwitchingGears} flight={flightActive || paused} multiplier={multiplier:F2} baseline={baseline:F3} effective={differential.gearRatio:F3} rpm={(engine != null ? engine.sensorRpm : -1):F0} limiter={(engine != null && engine.sensorRpmLimiter)} speedKmh={Speed * 3.6f:F2} speedLimit={(Controller.speedControl != null ? Controller.speedControl.speedLimit : -1):F2} wheelAngularVelocityRadSec:{wheels}"));
    }

    public void Dispose()
    {
        if (stopped) return;
        stopped = true;
        ratio.Release();
        log("High-range overdrive released original owned reduction.");
    }
}
