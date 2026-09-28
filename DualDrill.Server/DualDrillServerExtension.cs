using DualDrill.Engine.Services;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using DualDrill.CLSL;
using DualDrill.Server.Services;

namespace DualDrill.Server;

public static class DualDrillServerExtension
{
    private static async ValueTask AddGraphicsServices(this IServiceCollection services, CancellationToken cancellation)
    {
        var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        services.AddSingleton<IGPUInstance, GPUInstance<WebGPUNETBackend>>(sp => instance);

        var adapter = await instance.RequestAdapterAsync(new GPURequestAdapterOptions()
        {
            PowerPreference = GPUPowerPreference.HighPerformance
        }, cancellation);

        services.AddSingleton(adapter ?? throw new GraphicsApiException<WebGPUNETBackend>("Failed to get adapter"));
        var device = await adapter.RequestDeviceAsync(new GPUDeviceDescriptor(), cancellation);


        services.AddSingleton(device);
    }

    private static void AddFrameRenderServices(IServiceCollection services)
    {
        services.AddSingleton<IFrameRenderService, FrameRenderService>();
        services.AddSingletonHostedService<DevicePollHostedService>();
    }

    private static void AddRenderService(IServiceCollection services)
    {
        services.AddSingleton<MeshService>();
        services.AddSingleton<TextureService>();
        services.AddSingleton<DualDrill.Engine.Renderer.WebGPULogoRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.RotateCubeRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.ClearColorRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.StaticTriangleRenderer>();
    }

    private static void AddCLSLCompilerService(IServiceCollection services)
    {
        services.AddSingleton<ILSLDevelopShaderModuleService>();
        services.AddSingleton<SlangService>();
    }

    static void AddSingletonHostedService<T>(this IServiceCollection services)
        where T : class, IHostableBackgroundService
    {
        services.AddSingleton<T>();
        services.AddHostedService<SingletonHostedService<T>>();
    }


    public static async ValueTask AddDualDrillServerServices(this IServiceCollection services, CancellationToken cancellation)
    {
        await AddGraphicsServices(services, cancellation);
        AddFrameRenderServices(services);
        AddRenderService(services);
        AddCLSLCompilerService(services);
    }
}
