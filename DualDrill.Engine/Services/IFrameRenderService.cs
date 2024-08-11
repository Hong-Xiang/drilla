﻿using DualDrill.Engine.Scene;
using DualDrill.Graphics;

namespace DualDrill.Engine.Services;

public interface IFrameRenderService
{
    ValueTask RenderAsync(long frame, RenderScene scene, GPUTexture renderTarget, CancellationToken cancellation);
}

