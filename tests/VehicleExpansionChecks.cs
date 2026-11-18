using System;
using System.Numerics;

internal static class VehicleExpansionChecks
{
    internal static void Run(Action<string, bool> check)
    {
        var preferences = new VehiclePreferences();
        foreach (var setting in new[] { VehicleSetting.Power, VehicleSetting.SpeedLimit })
        {
            check("10x accepted for " + setting, preferences.Set(setting, 10));
            check("above 10x rejected for " + setting, !preferences.Set(setting, 10.01f));
        }
        var loaded = VehiclePreferences.Parse(preferences.Serialize(), out int rejected);
        check("10x tuning survives reload", rejected == 0 && loaded.Get(VehicleSetting.Power) == 10 && loaded.Get(VehicleSetting.SpeedLimit) == 10);
        foreach (VehiclePreset preset in Enum.GetValues(typeof(VehiclePreset)))
        {
            preferences.Set(VehicleSetting.Power, 9);
            VehiclePresets.Apply(preferences, preset);
            loaded = VehiclePreferences.Parse(preferences.Serialize(), out rejected);
            check("preset roundtrip " + preset, rejected == 0 && loaded.Serialize() == preferences.Serialize());
            float[][] expected = {
                new[] { 1f, 1f, 1f, 1f, 1f }, new[] { 2f, 1.5f, 1.5f, 1.1f, 1.5f },
                new[] { 3f, 1f, 2f, 1f, 2f }, new[] { 5f, 5f, 1.5f, 0.75f, 2f }
            };
            for (int index = 0; index < 5; index++)
                check("preset replaces " + preset + " " + (VehicleSetting)index, preferences.Get((VehicleSetting)index) == expected[(int)preset][index]);
        }
        VehiclePresets.Apply(preferences, VehiclePreset.Fast);
        check("fast preset uses increased bounds and reduced steering", preferences.Get(VehicleSetting.Power) == 5 && preferences.Get(VehicleSetting.SpeedLimit) == 5 && preferences.Get(VehicleSetting.Steering) == 0.75f);
        string before = preferences.Serialize();
        bool invalid = false;
        try { VehiclePresets.Apply(preferences, (VehiclePreset)99); } catch (ArgumentOutOfRangeException) { invalid = true; }
        check("unknown preset leaves preferences intact", invalid && before == preferences.Serialize());

        var car = new AssistCar();
        int resolutions = 0;
        var assists = new VehicleAssists(() => { resolutions++; return car; }, _ => { });
        assists.Update(false);
        check("assists start off without resolving a car", !assists.Flooding && !assists.Climb && resolutions == 0);
        assists.Flooding = true; assists.Update(false);
        check("flooding is independent of climb", car.Flood && !car.Climb);
        assists.Climb = true; assists.Update(false);
        check("assists run together", car.Flood && car.Climb);
        assists.Update(true);
        check("flight pauses climb and retains flooding", car.Flood && !car.Climb && assists.Climb);
        assists.Update(false);
        check("landing resumes requested climb", car.Climb);
        assists.Flooding = false; assists.Update(false);
        check("climb remains independent of flooding", !car.Flood && car.Climb);
        car.Eligible = false; assists.Update(false);
        check("car loss clears both toggles and disposes original", !assists.Flooding && !assists.Climb && car.Disposed);
        car = new AssistCar { Throw = true };
        assists.Climb = true; assists.Update(false);
        check("assist write failure disables and releases", !assists.Climb && car.Disposed && assists.Status.Contains("stopped"));
        car = new AssistCar();
        assists.Flooding = true; assists.Update(false);
        assists.Fail("hook failed");
        check("callback failure disarms immediately", !assists.Flooding && car.Disposed && assists.Status == "hook failed");
        assists = new VehicleAssists(() => null, _ => { });
        assists.Climb = true; assists.Update(false);
        check("unavailable car refuses activation", !assists.Climb);
        car = new AssistCar();
        assists = new VehicleAssists(() => car, _ => { }) { Climb = true };
        assists.Update(false); assists.Dispose(); assists.Dispose();
        check("unload disarms without persistent physics changes", !assists.Climb && car.Disposals == 1);

        var gravity = new Vector3(0, -9.81f, 0);
        (bool valid, Vector3 acceleration, Vector3 angular) Solve(Vector3 normal, Vector3 up, Vector3 forward,
            float drive = 0, float steer = 0, Vector3? velocity = null, Vector3? spin = null, float dt = 0.02f)
        {
            bool valid = SurfaceDrive.Solve(normal, up, forward, gravity, velocity ?? Vector3.Zero,
                spin ?? Vector3.Zero, drive, steer, dt, out var force, out var angular);
            return (valid, force, angular);
        }
        foreach (var n in new[] { Vector3.UnitY, Vector3.Normalize(new Vector3(0, 1, 1)), Vector3.UnitX, -Vector3.UnitY })
        {
            var forward = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, n));
            var idle = Solve(n, n, forward);
            check("surface hold cancels gravity on " + n, idle.valid &&
                (idle.acceleration + gravity + n * 25).Length() < 0.001f);
            var powered = Solve(n, n, forward, drive: 1);
            check("surface drive supplies tangent propulsion on " + n,
                powered.valid && Math.Abs(Vector3.Dot(powered.acceleration - idle.acceleration, forward) - 18) < 0.001f);
            var cruise = Solve(n, n, forward, drive: 1, velocity: forward * SurfaceDrive.DriveSpeed);
            check("surface motor stops accelerating at target on " + n,
                (cruise.acceleration - idle.acceleration).Length() < 0.001f);
            var reverse = Solve(n, n, forward, drive: -1);
            check("reverse propulsion works on " + n, Vector3.Dot(reverse.acceleration - idle.acceleration, forward) < -14.28f);
            var holding = Solve(n, n, forward, velocity: forward * 4);
            check("released throttle holds surface instead of sliding on " + n,
                Vector3.Dot(holding.acceleration - idle.acceleration, forward) < -11f);
            check("right steering turns around surface normal on " + n,
                Vector3.Dot(Solve(n, n, forward, 1, 1).angular, n) > 0);
            check("reverse steering reverses yaw on " + n,
                Vector3.Dot(Solve(n, n, forward, -1, 1).angular, n) < 0);
        }
        var wall = Solve(-Vector3.UnitZ, Vector3.UnitY, Vector3.UnitZ, drive: 1);
        check("head-on wall approach climbs upward without wheel torque", wall.valid &&
            Vector3.Dot(wall.acceleration + gravity, Vector3.UnitY) > 17.99f && wall.angular.X < 0);
        var rearWall = Solve(Vector3.UnitZ, Vector3.UnitY, Vector3.UnitZ, drive: -1);
        check("reversing onto wall also climbs upward", rearWall.valid &&
            Vector3.Dot(rearWall.acceleration + gravity, Vector3.UnitY) > 14.28f);
        check("overturned roof does not latch", !Solve(Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ).valid);
        check("missing surface disarms", !Solve(Vector3.Zero, Vector3.UnitY, Vector3.UnitZ).valid);
        check("bad surface disarms", !Solve(new Vector3(float.NaN, 1, 0), Vector3.UnitY, Vector3.UnitZ).valid);
        check("bad velocity disarms", !Solve(Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ,
            velocity: new Vector3(float.PositiveInfinity, 0, 0)).valid);
        foreach (float dt in new[] { 0f, -1f, 0.11f, float.NaN, float.PositiveInfinity })
            check("invalid surface timestep disarms " + dt, !Solve(Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ, dt: dt).valid);
        var extreme = Solve(Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ, drive: 1, steer: 1,
            velocity: new Vector3(1000, 1000, 1000), spin: new Vector3(100, 100, 100));
        check("surface linear and angular acceleration bounded", extreme.valid &&
            extreme.acceleration.Length() <= SurfaceDrive.MaximumAcceleration + 0.001f &&
            extreme.angular.Length() <= SurfaceDrive.MaximumAngularAcceleration + 0.001f);
        check("surface inputs clamp", Solve(Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ, 10, 10) ==
            Solve(Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ, 1, 1));
        check("surface alignment damps tumbling", Solve(Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ, spin: Vector3.UnitX).angular.X < 0);
    }

    private sealed class AssistCar : IVehicleAssistsTarget
    {
        public bool Eligible { get; set; } = true;
        internal bool Flood, Climb, Disposed, Throw;
        internal int Disposals;
        public void SetEnabled(bool flooding, bool climb)
        {
            if (Throw) throw new InvalidOperationException("setter failed");
            Flood = flooding; Climb = climb;
        }
        public void Dispose() { Disposed = true; Disposals++; Flood = Climb = false; }
    }
}
