using System;
using System.Numerics;

// Surface-relative motor. The adapter supplies fresh nearby contact; no airborne latch.
internal static class SurfaceDrive
{
    internal const float DriveSpeed = 10f;
    internal const float ReverseSpeed = 5f;
    internal const float MaximumAcceleration = 85f;
    internal const float MaximumAngularAcceleration = 30f;

    internal static bool Solve(Vector3 normal, Vector3 up, Vector3 forward, Vector3 gravity,
        Vector3 velocity, Vector3 angularVelocity, float drive, float steer, float deltaTime,
        out Vector3 acceleration, out Vector3 angularAcceleration)
    {
        acceleration = angularAcceleration = Vector3.Zero;
        if (!Finite(normal) || !Finite(up) || !Finite(forward) || !Finite(gravity) ||
            !Finite(velocity) || !Finite(angularVelocity) || !float.IsFinite(drive) || !float.IsFinite(steer) ||
            !float.IsFinite(deltaTime) || deltaTime <= 0 || deltaTime > 0.1f ||
            normal.LengthSquared() < 0.5f || up.LengthSquared() < 0.5f) return false;
        normal = Vector3.Normalize(normal);
        up = Vector3.Normalize(up);
        // Refuse roof contact on an overturned car; perpendicular wall approaches are allowed.
        if (Vector3.Dot(normal, up) < -0.1f) return false;
        var tangent = forward - normal * Vector3.Dot(forward, normal);
        // At a head-on wall, current up supplies the direction to climb onto it.
        if (tangent.LengthSquared() < 0.05f) tangent = (drive < 0 ? -1 : 1) * (up - normal * Vector3.Dot(up, normal));
        if (tangent.LengthSquared() < 0.05f) return false;
        tangent = Vector3.Normalize(tangent);
        drive = Math.Clamp(drive, -1f, 1f);
        steer = Math.Clamp(steer, -1f, 1f);
        var surfaceVelocity = velocity - normal * Vector3.Dot(velocity, normal);
        var desired = tangent * drive * (drive < 0 ? ReverseSpeed : DriveSpeed);
        var traction = Limit((desired - surfaceVelocity) / 0.35f, 18f);
        float separationSpeed = Math.Max(0, Vector3.Dot(velocity, normal));
        acceleration = Limit(-gravity - normal * Math.Min(55f, 25f + separationSpeed * 8f) + traction,
            MaximumAcceleration);
        var tiltVelocity = angularVelocity - normal * Vector3.Dot(angularVelocity, normal);
        var align = Vector3.Cross(up, normal) * 24f - tiltVelocity * 8f;
        float yaw = steer * 1.5f * Math.Sign(drive);
        angularAcceleration = Limit(align + normal * ((yaw - Vector3.Dot(angularVelocity, normal)) * 8f),
            MaximumAngularAcceleration);
        return true;
    }

    private static Vector3 Limit(Vector3 value, float maximum)
    {
        float length = value.Length();
        return length > maximum ? value * (maximum / length) : value;
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
