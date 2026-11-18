using System;

int failures = 0;
int checks = 0;

void Check(string name, float actual, float expected, float tolerance = 0.00001f)
{
    checks++;
    if (float.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance) return;
    Console.Error.WriteLine($"FAIL {name}: expected {expected}, got {actual}");
    failures++;
}

Check("axis centered", ControllerInput.Axis(0), 0);
Check("axis positive dead zone edge", ControllerInput.Axis(6553), 0);
Check("axis negative dead zone edge", ControllerInput.Axis(-6553), 0);
Check("axis positive just outside dead zone", ControllerInput.Axis(6554), (6554f / 32767f - 0.2f) / 0.8f);
Check("axis negative just outside dead zone", ControllerInput.Axis(-6554), -(6554f / 32767f - 0.2f) / 0.8f);
Check("axis positive maximum", ControllerInput.Axis(short.MaxValue), 1);
Check("axis negative maximum clamps", ControllerInput.Axis(short.MinValue), -1);
Check("axis midpoint", ControllerInput.Axis(19660), 0.5f, 0.0001f);
Check("trigger released", ControllerInput.Trigger(0), 0);
Check("trigger dead zone edge", ControllerInput.Trigger(30), 0);
Check("trigger just outside dead zone", ControllerInput.Trigger(31), 1f / 225f);
Check("trigger midpoint", ControllerInput.Trigger(120), 0.4f);
Check("trigger maximum", ControllerInput.Trigger(byte.MaxValue), 1);

BindingChecks.Run((name, passed) =>
{
    checks++;
    if (passed) return;
    Console.Error.WriteLine("FAIL " + name);
    failures++;
});

LifetimeChecks.Run((name, passed) =>
{
    checks++;
    if (passed) return;
    Console.Error.WriteLine("FAIL " + name);
    failures++;
});

VehicleChecks.Run((name, passed) =>
{
    checks++;
    if (passed) return;
    Console.Error.WriteLine("FAIL " + name);
    failures++;
});

RepairChecks.Run((name, passed) =>
{
    checks++;
    if (passed) return;
    Console.Error.WriteLine("FAIL " + name);
    failures++;
});

VehicleControlChecks.Run((name, passed) =>
{
    checks++;
    if (passed) return;
    Console.Error.WriteLine("FAIL " + name);
    failures++;
});

VehicleInteractionChecks.Run((name, passed) =>
{
    checks++;
    if (passed) return;
    Console.Error.WriteLine("FAIL " + name);
    failures++;
});

VehicleExpansionChecks.Run((name, passed) =>
{
    checks++;
    if (passed) return;
    Console.Error.WriteLine("FAIL " + name);
    failures++;
});

Console.WriteLine($"{checks - failures}/{checks} trainer checks passed.");
return failures == 0 ? 0 : 1;
