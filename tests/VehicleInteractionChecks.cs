using System;

internal static class VehicleInteractionChecks
{
    internal static void Run(Action<string, bool> check)
    {
        var preferences = new VehiclePreferences();
        check("manual value retains precision", VehicleValueInput.TryApply(preferences, VehicleSetting.Power, "1.237") && preferences.Get(VehicleSetting.Power) == 1.237f);
        foreach (string invalid in new[] { "", "-", "NaN", "Infinity", "0.49", "10.01", "1,5", "1x" })
            check("invalid manual entry preserves value " + invalid, !VehicleValueInput.TryApply(preferences, VehicleSetting.Power, invalid) && preferences.Get(VehicleSetting.Power) == 1.237f);
        check("manual steering respects own limit", !VehicleValueInput.TryApply(preferences, VehicleSetting.Steering, "1.6"));
        check("manual lower bound accepted", VehicleValueInput.TryApply(preferences, VehicleSetting.Grip, "0.5"));
        check("manual upper bound accepted", VehicleValueInput.TryApply(preferences, VehicleSetting.Power, "3"));

        var car = new FlipCar();
        double now = 0;
        IVehicleFlipTarget current = car;
        var flip = new VehicleFlip(() => current, _ => { }, () => now);
        flip.Update();
        check("flip never unsolicited", car.Calls == 0);
        flip.Request(); flip.Request();
        check("flip queues without mutation", flip.Pending && car.Calls == 0);
        flip.Update(); flip.Update();
        check("flip executes once and observes", flip.Pending && car.Calls == 1);
        flip.Request();
        check("flip blocks duplicates during readback", car.Calls == 1);
        now = 0.15; flip.Update();
        check("flip first sample keeps observing", flip.Pending);
        now = 0.75; flip.Update();
        check("flip delayed success clears visible status", !flip.Pending && flip.Status == "");
        flip.Request(); flip.Cancel(); flip.Update();
        check("flip cancels when menu closes", car.Calls == 1);
        flip.Request(); car.Eligible = false; current = new FlipCar(); flip.Update();
        check("flip does not redirect to replacement car", car.Calls == 1 && ((FlipCar)current).Calls == 0);
        current = car; flip.Request();
        check("flip rejects ineligible car", !flip.Pending);
        current = null; flip.Request();
        check("flip rejects absent car", !flip.Pending);
        car.Eligible = true; car.Fail = true; current = car;
        flip.Request(); flip.Update(); flip.Update();
        check("flip failure reported without retry", car.Calls == 2 && !flip.Pending && flip.Status.StartsWith("Flip failed:"));
        flip = new VehicleFlip(() => throw new InvalidOperationException("lookup failed"), _ => { });
        flip.Request();
        check("flip lookup failure contained", !flip.Pending && flip.Status.StartsWith("Flip unavailable:"));

        car = new FlipCar { Upright = false };
        flip = new VehicleFlip(() => car, _ => { }, () => now);
        flip.Request(); flip.Update();
        now += 0.8; flip.Update(); flip.Update();
        check("flip reports failed delayed pose", !flip.Pending && flip.Status.StartsWith("Car did not stay upright."));

        car.Upright = true;
        flip.Request(); flip.Update(); flip.Cancel();
        check("closing menu retains readback", flip.Pending);
        car.Eligible = false; flip.Update();
        check("observation stops on authority car or flight change", !flip.Pending && flip.Status.StartsWith("Flip observation stopped:"));

        car.Eligible = true;
        flip.Request(); flip.Update(); flip.Stop();
        now += 1; flip.Update();
        check("unload clears readback and request", !flip.Pending && car.Calls == 3);

        flip.Request(); flip.Update(); car.FailRead = true;
        now += 1; flip.Update();
        check("readback failure contained without retry", !flip.Pending && car.Calls == 4 && flip.Status.StartsWith("Flip readback failed:"));
    }

    private sealed class FlipCar : IVehicleFlipTarget
    {
        public bool Eligible { get; set; } = true;
        public bool Upright { get; set; } = true;
        public string Pose => FailRead ? throw new InvalidOperationException("read failed") : "test pose";
        internal int Calls;
        internal bool Fail;
        internal bool FailRead;
        public void Flip() { Calls++; if (Fail) throw new InvalidOperationException("reposition failed"); }
    }
}
