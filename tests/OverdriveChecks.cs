using System;

internal static class OverdriveChecks
{
    internal static void Run(Action<string, bool> check)
    {
        float value = 6;
        var ratio = new OverdriveRatio(() => value, next => value = next);
        void Apply(float multiplier = 2, bool enabled = true, bool low = false, int request = 1, int engaged = 1, bool shifting = false)
            => ratio.AfterWriter(multiplier, enabled, low, request, engaged, shifting);
        bool Throws(Action action)
        {
            try { action(); return false; }
            catch (Exception) { return true; }
        }

        Apply();
        check("overdrive inverse ratio doubles speed per engine revolution", value == 3);
        Apply();
        check("overdrive repeated postfix cannot compound", value == 3);
        ratio.BeforeWriter();
        check("overdrive releases before native writer", value == 6);
        value = 9; // A legitimate game range/setup writer.
        Apply(3);
        check("overdrive rebases after known writer", value == 3);
        ratio.Release();
        check("overdrive restores new legitimate baseline", value == 9);
        ratio.Release();
        check("overdrive release idempotent", value == 9);

        foreach (var state in new (bool enabled, bool low, int request, int engaged, bool shifting)[] {
            (false, false, 1, 1, false),
            (true, true, 1, 1, false), (true, false, -1, 1, false),
            (true, false, 1, -1, false), (true, false, 0, 1, false),
            (true, false, 1, 0, false), (true, false, 1, 1, true) })
        {
            Apply();
            Apply(2, state.enabled, state.low, state.request, state.engaged, state.shifting);
            check("overdrive restores for disabled/Low/reverse/neutral/shift " + state, value == 9);
        }
        Apply(); Apply(1);
        check("overdrive 1x releases stock reduction", value == 9);
        ratio.BeforeWriter(); // Input-disabled native path leaves value unchanged.
        Apply(2, enabled: false);
        check("overdrive skipped native range write does not compound", value == 9);
        Apply(1.5f);
        check("overdrive fractional gearing", value == 6);
        value = 7;
        check("overdrive unknown writer rejected", Throws(() => Apply()) && value == 7);
        check("overdrive cleanup preserves unknown writer", Throws(ratio.Release) && value == 7);
        ratio.Release();
        check("overdrive fault cleanup idempotent", value == 7);
        value = 9; Apply(); value = 8;
        check("overdrive prefix refuses external writer before game overwrite", Throws(ratio.BeforeWriter) && value == 8);
        foreach (float invalid in new[] { 0, -1, float.NaN, float.PositiveInfinity })
        {
            value = invalid;
            check("overdrive rejects invalid reduction " + invalid, Throws(() => Apply()) && (float.IsNaN(invalid) ? float.IsNaN(value) : value == invalid));
        }
        foreach (float invalid in new[] { 0.99f, 3.01f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            check("overdrive rejects multiplier " + invalid, Throws(() => Apply(invalid)));
        value = float.Epsilon;
        check("overdrive refuses reduction underflow", Throws(() => Apply(3)) && value == float.Epsilon);

        var target = new FakeTarget();
        var feature = new VehicleOverdrive(() => target, _ => { });
        check("overdrive starts stock", feature.Multiplier == 1);
        check("overdrive stopped selection", feature.Select(1.5f) && target.Multiplier == 1.5f && feature.Multiplier == 1.5f);
        target.Speed = 1;
        check("overdrive moving edit rejected", !feature.Select(2) && feature.Multiplier == 1.5f && target.Multiplier == 1.5f);
        target.Speed = float.NaN;
        check("overdrive unknown speed rejected", !feature.Select(2) && feature.Multiplier == 1.5f);
        target.Speed = -1;
        check("overdrive negative speed rejected", !feature.Select(2));
        target.Speed = VehicleOverdrive.StoppedSpeed;
        check("overdrive stopped boundary accepted", feature.Select(2));
        feature.Update(true);
        check("overdrive pauses for flight without losing tuning", target.Paused && feature.Multiplier == 2);
        feature.Update(false);
        check("overdrive resumes after flight", !target.Paused && feature.Status == "");
        target.Eligible = false;
        feature.Update(false);
        check("overdrive target loss resets activation", feature.Multiplier == 1 && target.Disposals == 1 && feature.Status.Contains("unavailable"));
        feature.Update(false);
        check("overdrive never resolves a replacement automatically", target.Disposals == 1);
        target = new FakeTarget();
        feature.Select(3); feature.Reset(); feature.Reset();
        check("overdrive Restore stock cleanup immediate and idempotent", feature.Multiplier == 1 && target.Disposals == 1);
        target = new FakeTarget { ThrowSet = true };
        check("overdrive partial activation failure cleans up", !feature.Select(2) && feature.Multiplier == 1 && target.Disposals == 1);
        target = new FakeTarget(); feature.Select(2); target.ThrowPause = true;
        feature.Update(true);
        check("overdrive pause failure resets and releases", feature.Multiplier == 1 && target.Disposals == 1);
        target = new FakeTarget(); feature.Select(2); target.ThrowDispose = true;
        feature.Fail("callback failed");
        check("overdrive callback cleanup failure reported", feature.Multiplier == 1 && feature.Status.Contains("Cleanup") && target.Disposals == 1);
        feature.Dispose();
        check("overdrive disposal after fault is safe", target.Disposals == 1);
        check("overdrive invalid selection does not resolve target", !feature.Select(float.NaN));
        var missing = new VehicleOverdrive(() => null, _ => { });
        check("overdrive unavailable target reported", !missing.Select(2) && missing.Multiplier == 1 && missing.Status.Contains("required"));
        var broken = new VehicleOverdrive(() => throw new Exception("resolve failure"), _ => { });
        check("overdrive resolver failure reported", !broken.Select(2) && broken.Status.Contains("resolve failure"));
    }

    private sealed class FakeTarget : IVehicleOverdriveTarget
    {
        public bool Eligible { get; set; } = true;
        public float Speed { get; set; }
        internal float Multiplier = 1;
        internal bool Paused, ThrowSet, ThrowPause, ThrowDispose;
        internal int Disposals;
        public void SetMultiplier(float value) { if (ThrowSet) throw new Exception("set failed"); Multiplier = value; }
        public void Pause(bool value) { if (ThrowPause) throw new Exception("pause failed"); Paused = value; }
        public void Dispose() { Disposals++; if (ThrowDispose) throw new Exception("dispose failed"); }
    }
}
