using System;

internal static class RepairChecks
{
    internal static void Run(Action<string, bool> check)
    {
        var car = new RepairCar();
        IVehicleRepairTarget current = car;
        int reports = 0;
        var repair = new VehicleRepair(() => current, _ => reports++);
        repair.Update();
        check("no unsolicited repair", car.Heals == 0 && reports == 0);
        repair.Request();
        repair.Request();
        check("click queues without healing", repair.Pending && car.Heals == 0);
        repair.Update();
        repair.Update();
        check("duplicate requests heal once", car.Heals == 1 && car.Amount == 100 && !repair.Pending);
        check("observed zero reports repaired", repair.Status == "Car repaired. Damage is zero.");
        repair.Request();
        repair.Update();
        check("undamaged car skips healing", car.Heals == 1);

        car.Value = 10;
        repair.Request();
        car.Eligible = false;
        current = new RepairCar();
        repair.Update();
        check("car change cancels rather than redirecting", car.Heals == 1 && ((RepairCar)current).Heals == 0);
        repair.Request();
        repair.Cancel();
        repair.Update();
        check("closing menu cancels queued repair", !repair.Pending && ((RepairCar)current).Heals == 0);

        current = null;
        repair.Request();
        check("missing car rejected", !repair.Pending);
        current = car;
        repair.Request();
        check("ineligible car rejected", !repair.Pending);
        car.Eligible = true;
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f, 101f })
        {
            car.Value = invalid;
            repair.Request();
            repair.Update();
            check("invalid damage rejected " + invalid, car.Heals == 1);
        }
        car.Value = 10;
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, 0f, -1f })
        {
            car.MaximumDamage = invalid;
            repair.Request();
            repair.Update();
            check("invalid maximum rejected " + invalid, car.Heals == 1);
        }
        car.MaximumDamage = 100;
        car.ThrowOnHeal = true;
        repair.Request();
        repair.Update();
        check("failed heal reported without retry", !repair.Pending && repair.Status.StartsWith("Repair failed:"));
        repair.Update();
        check("failure not automatically retried", car.Heals == 2);
        car.ThrowOnHeal = false;
        car.ApplyHeal = false;
        repair.Request();
        repair.Update();
        check("unobserved heal does not claim success", repair.Status.StartsWith("Repair requested;") && car.Value == 10);
        var broken = new VehicleRepair(() => throw new InvalidOperationException("lookup failed"), _ => { });
        broken.Request();
        check("lookup failure contained", !broken.Pending && broken.Status.StartsWith("Repair unavailable:"));
    }

    private sealed class RepairCar : IVehicleRepairTarget
    {
        public bool Eligible { get; set; } = true;
        public float Value = 10;
        public float Damage => Value;
        public float MaximumDamage { get; set; } = 100;
        public int Heals;
        public float Amount;
        public bool ThrowOnHeal;
        public bool ApplyHeal = true;
        public void Heal(float amount)
        {
            Heals++;
            if (ThrowOnHeal) throw new InvalidOperationException("heal failed");
            Amount = amount;
            if (ApplyHeal) Value = 0;
        }
    }
}
