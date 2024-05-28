using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine;

public interface IRenderService
{
    ValueTask Render(RenderScene scene);
}

public record struct Camera(
    float NearPlaneWidth,
    float NearPlaneHeight,
    float NearPlaneDistance,
    float FarPlaneDistance,
    Vector3 Position,
    Vector3 Target,
    Vector3 Up)
{
}

public record struct Cube(float Scale, Vector3 Rotation)
{
}

public record struct RenderScene(
    long Frame,
    Camera Camera,
    Cube Cube)
{
}

public interface IRenderStateService
{
    ValueTask UpdateScene(Func<RenderScene, ValueTask<RenderScene>> update);
}
