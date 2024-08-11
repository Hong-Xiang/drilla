using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
