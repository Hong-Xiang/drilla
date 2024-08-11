using DualDrill.Engine.Renderer;
using System.Numerics;

namespace DualDrill.Engine.Scene;

public record struct RenderScene(
    Vector3 ClearColor,
    Camera Camera,
    WebGPULogoRenderer.State LogoState,
    Cube Cube)
{
    public static RenderScene TestScene(int width, int height)
    {
        var scale = 5000.0f;
        var scene = new RenderScene
        {
            Cube = new Cube(Vector3.Zero, Vector3.Zero),
            Camera = new Camera()
            {
                NearPlaneWidth = width / scale,
                NearPlaneHeight = height / scale,
            },
            LogoState = new()
            {
                Scale = new Vector2((float)height / width, 1.0f)
            },
            ClearColor = new Vector3(0.2f)
        };
        return scene;
    }
}
