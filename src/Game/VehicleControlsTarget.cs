using System;
using Il2Cpp;
using VP = Il2CppVehiclePhysics;

internal sealed class VehicleControlsTarget : IVehicleControlsTarget
{
    private readonly LocalVehicleProvider vehicles;
    private readonly LocalVehicle local;
    internal readonly VP.VPVehicleController Controller;
    internal readonly CarDamageSystem Damage;
    private readonly Action<string> report;
    private VehicleValueGroup performance;
    private VehicleProtection protection;
    private bool stopped;
    private int blockedDamage;
    internal bool Writing { get; private set; }
    internal bool Protected => protection != null && protection.Active;

    internal VehicleControlsTarget(LocalVehicleProvider vehicles, LocalVehicle local, Action<string> report)
    {
        this.vehicles = vehicles;
        this.local = local;
        this.report = report;
        Controller = local.Vehicle.TryCast<VP.VPVehicleController>();
        if (Controller == null) throw new InvalidOperationException("Unsupported vehicle controller.");
        Damage = Controller.GetComponentInChildren<CarDamageSystem>(true);
        if (Damage == null) Damage = Controller.GetComponentInParent<CarDamageSystem>();
    }

    public bool Eligible => !stopped && vehicles.IsCurrent(local) && Controller != null &&
        Controller.initialized && Controller.HasStateAuthority && Damage != null && Damage.HasStateAuthority &&
        Damage.vehicle != null && Damage.vehicle.Pointer == Controller.Pointer;

    private void RequireEligible()
    {
        if (!Eligible) throw new InvalidOperationException("Local vehicle or authority changed.");
    }

    public void Apply(VehiclePreferences preferences)
    {
        RequireEligible();
        Writing = true;
        try
        {
            performance ??= CapturePerformance();
            performance.Apply(preferences);
        }
        finally { Writing = false; }
    }

    public void ReleasePerformance()
    {
        var owned = performance;
        performance = null;
        if (owned == null) return;
        bool previous = Writing;
        Writing = true;
        try { report("Performance cleanup: " + owned.Restore()); }
        finally { Writing = previous; }
    }

    public void Protect(bool enabled)
    {
        if (enabled) RequireEligible();
        bool previous = Writing;
        Writing = true;
        try
        {
            if (!enabled)
            {
                var owned = protection;
                protection = null;
                if (owned != null)
                {
                    owned.Disable();
                    report("God mode released; current damage multiplier " + ReadDamageModifier() + ".");
                }
                return;
            }
            bool first = protection == null;
            protection ??= new VehicleProtection(ReadDamageModifier, WriteDamageModifier);
            protection.Enable();
            if (first) report("God mode enabled; local damage multiplier read back as zero. Existing damage unchanged.");
        }
        finally { Writing = previous; }
    }

    private float ReadDamageModifier()
    {
        if (Damage == null || Controller == null || Damage.vehicle == null || Damage.vehicle.Pointer != Controller.Pointer)
            throw new InvalidOperationException("Original damage component is unavailable.");
        return Damage.damageModifier;
    }
    private void WriteDamageModifier(float value) { ReadDamageModifier(); Damage.SetDamageModifier(value); }

    // Called before known game writers, while original values/identities still exist.
    internal void BeforeGameWriter()
    {
        if (Writing) return;
        try { ReleasePerformance(); }
        finally { Protect(false); }
    }

    internal void BeforeDisable()
    {
        stopped = true;
        report("Vehicle OnDisable intercepted; initialized=" + (Controller != null && Controller.initialized) +
            ", wheels=" + (Controller != null ? Controller.wheelCount : 0) + "; attempting original-value cleanup.");
        BeforeGameWriter();
    }

    internal void RecordDamageBlocked()
    {
        if (++blockedDamage <= 8) report("God mode intercepted local ApplyDamage; blocked event " + blockedDamage + ".");
    }

    public void Dispose()
    {
        stopped = true;
        BeforeGameWriter();
    }

    private VehicleValueGroup CapturePerformance()
    {
        var engine = Controller.engine;
        var speed = Controller.speedControl;
        var steering = Controller.steering;
        var brakes = Controller.brakes;
        if (engine == null || speed == null || steering == null || brakes == null)
            throw new InvalidOperationException("Vehicle settings unavailable.");
        var customization = Controller.GetComponentInChildren<VehicleCustomizationController>(true);
        if (customization == null) customization = Controller.GetComponentInParent<VehicleCustomizationController>();
        var car = customization != null ? customization.CurrentCarInfo : null;
        var setup = car != null ? car.carCategorySetup : null;
        if (setup == null || setup.engineTiers == null || setup.engineTiers.Length == 0 ||
            setup.steering == null || setup.brakes == null)
            throw new InvalidOperationException("Cannot verify isolation from shared vehicle settings.");
        if (setup.steering.Pointer == steering.Pointer || setup.brakes.Pointer == brakes.Pointer)
            throw new InvalidOperationException("Shared steering/brake settings cannot be modified.");
        foreach (var tier in setup.engineTiers)
        {
            if (tier == null || tier.engine == null || tier.speedControl == null)
                throw new InvalidOperationException("Engine tier isolation is unknown.");
            if (tier.engine.Pointer == engine.Pointer || tier.speedControl.Pointer == speed.Pointer)
                throw new InvalidOperationException("Shared engine/speed settings cannot be modified.");
        }
        if (Controller.m_engine == null || Controller.m_engine.settings == null || Controller.m_engine.settings.Pointer != engine.Pointer ||
            Controller.m_steering == null || Controller.m_steering.settings == null || Controller.m_steering.settings.Pointer != steering.Pointer ||
            Controller.m_brakes == null || Controller.m_brakes.settings == null || Controller.m_brakes.settings.Pointer != brakes.Pointer)
            throw new InvalidOperationException("Active physics settings do not match the controller.");

        void CheckController()
        {
            if (Controller == null || !Controller.initialized)
                throw new InvalidOperationException("Original controller is unavailable; stale access skipped.");
        }
        var group = new VehicleValueGroup();
        void Add(VehicleSetting setting, Func<bool> sameObject, Func<float> read, Action<float> write)
        {
            void Validate()
            {
                CheckController();
                if (!sameObject()) throw new InvalidOperationException("Original settings identity changed; stale access skipped.");
            }
            group.Add(setting, () => { Validate(); return read(); }, value => { Validate(); write(value); });
        }
        bool SameEngine() => Controller.engine != null && Controller.engine.Pointer == engine.Pointer;
        Add(VehicleSetting.Power, SameEngine, () => engine.idleRpmTorque, value => engine.idleRpmTorque = value);
        Add(VehicleSetting.Power, SameEngine, () => engine.peakRpmTorque, value => engine.peakRpmTorque = value);
        Add(VehicleSetting.Power, SameEngine, () => engine.torqueCapLimit, value => engine.torqueCapLimit = value);
        Add(VehicleSetting.Power, SameEngine, () => engine.powerCapLimit, value => engine.powerCapLimit = value);
        Add(VehicleSetting.SpeedLimit, () => Controller.speedControl != null && Controller.speedControl.Pointer == speed.Pointer, () => speed.speedLimit, value => speed.speedLimit = value);
        Add(VehicleSetting.Steering, () => Controller.steering != null && Controller.steering.Pointer == steering.Pointer, () => steering.maxSteerAngle, value => steering.maxSteerAngle = value);
        Add(VehicleSetting.Brakes, () => Controller.brakes != null && Controller.brakes.Pointer == brakes.Pointer, () => brakes.maxBrakeTorque, value => brakes.maxBrakeTorque = value);
        var states = Controller.wheelState;
        int count = Controller.wheelCount;
        if (states == null || count < 1 || count > 32 || states.Length != count)
            throw new InvalidOperationException("Wheel inventory unavailable.");
        for (int index = 0; index < count; index++)
        {
            int wheel = index;
            var state = states[wheel];
            if (state == null) throw new InvalidOperationException("Wheel state unavailable.");
            void CheckWheel()
            {
                CheckController();
                var current = Controller.wheelState;
                if (current == null || current.Length != count || Controller.wheelCount != count ||
                    current[wheel] == null || current[wheel].Pointer != state.Pointer)
                    throw new InvalidOperationException("Original wheel identity changed; stale access skipped.");
            }
            group.Add(VehicleSetting.Grip, () => { CheckWheel(); return state.tireForceMult; },
                value => { CheckWheel(); Controller.SetWheelTireFrictionMultiplier(wheel, value); });
        }
        report("Captured isolated performance settings and " + count + " original wheel baselines.");
        return group;
    }
}
