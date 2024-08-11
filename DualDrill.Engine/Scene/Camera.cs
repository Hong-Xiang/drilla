using System.Numerics;

namespace DualDrill.Engine.Scene;

public sealed record class Camera
{
    public float NearPlaneWidth { get; init; } = 1f;
    public float NearPlaneHeight { get; init; } = 1f;
    public float NearPlaneDistance { get; init; } = 0.1f;
    public float FarPlaneDistance { get; init; } = 1000.0f;
    public Vector3 Position { get; init; } = -5.0f * Vector3.UnitZ;
    public Vector3 LookAt { get; init; } = Vector3.Zero;
    public Vector3 Up { get; init; } = Vector3.UnitY;

    public Matrix4x4 ViewProjectionMatrix =>
        Matrix4x4.CreateLookAt(
            Position,
            LookAt,
            Up) *
        Matrix4x4.CreatePerspective(
            NearPlaneWidth,
            NearPlaneHeight,
            NearPlaneDistance,
            FarPlaneDistance
        );
}

