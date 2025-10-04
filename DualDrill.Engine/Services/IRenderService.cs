using DualDrill.Engine.Scene;

namespace DualDrill.Engine.Services;

public interface IRenderService
{
    ValueTask Render(RenderScene scene);
}

public interface IRenderStateService
{
    ValueTask UpdateScene(Func<RenderScene, ValueTask<RenderScene>> update);
}
