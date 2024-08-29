using System.Numerics;

namespace DualDrill.Engine.Event;

public sealed record class CameraEvent(
    Vector3 Position,
    Vector3 Forward,
    Vector3 Up
)
{
}
