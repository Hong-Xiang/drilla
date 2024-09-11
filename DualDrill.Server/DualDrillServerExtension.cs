using DualDrill.ApiGen;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;
using DualDrill.Engine.Services;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using DualDrill.Server.Services;
using DualDrill.WebView;

namespace DualDrill.Server;

public static class DualDrillServerExtension
{
    private static void AddConnectionServices(IServiceCollection services)
    {
        services.AddSingleton<ClientsManager>();
        //services.AddSingleton<ISignalConnectionProviderService, SignalConnectionOverSignalRProvider>();
        services.AddSingleton<ISignalConnectionProviderService, SignalConnectionOverWebViewWithWebSocketService>();
        services.AddScoped<IClient>(sp => new ClientIdentity(Guid.NewGuid()));
        services.AddWebViewConnectionServices();
    }

    private static async ValueTask AddGraphicsServices(this IServiceCollection services, CancellationToken cancellation)
    {
        var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        services.AddSingleton<IGPUInstance, GPUInstance<WebGPUNETBackend>>(sp => instance);

        var instanceLegacy = WGPUBackend.Instance.CreateGPUInstance();
        services.AddSingleton(instanceLegacy);

        var adapter = await instance.RequestAdapterAsync(new GPURequestAdapterOptions()
        {
            PowerPreference = GPUPowerPreference.HighPerformance
        }, cancellation);

        services.AddSingleton(adapter ?? throw new GraphicsApiException<WebGPUNETBackend>("Failed to get adapter"));
        var device = await adapter.RequestDeviceAsync(new GPUDeviceDescriptor(), cancellation);


        services.AddSingleton(device);


        //var adapterLegacy = await instanceLegacy.RequestAdapterAsync(new GPURequestAdapterOptions()
        //{
        //    PowerPreference = GPUPowerPreference.HighPerformance
        //}, cancellation);
        //var deviceLegacy = await (adapterLegacy as GPUAdapter<WGPUBackend>).RequestDeviceAsyncLegacy(new GPUDeviceDescriptor(), cancellation);
        //services.AddSingleton(deviceLegacy);
        //services.AddSingleton(sp => sp.GetRequiredService<GPUDevice>().GetQueue());
    }

    private static void AddRealtimeSimulationServices(IServiceCollection services)
    {
        services.AddSingleton<FrameInputService>();
        services.AddSingleton<FrameSimulationService>();
        services.AddSingleton<IFrameRenderService, FrameRenderService>();

        services.AddSingletonHostedService<DevicePollHostedService>();
        services.AddSingletonHostedService<RealtimeFrameHostableBackgroundService>();
    }

    private static void AddHeadlessServices(IServiceCollection services)
    {
        services.AddSingleton<HeadlessSurface>();
        services.AddSingleton<HeadlessSurfaceCaptureVideoSource>();
        services.AddSingleton<IGPUSurface>(sp => sp.GetRequiredService<HeadlessSurface>());
    }

    private static void AddRenderService(IServiceCollection services)
    {
        services.AddSingleton<MeshService>();
        services.AddSingleton<TextureService>();
        services.AddSingleton<DualDrill.Engine.Renderer.WebGPULogoRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.RotateCubeRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.ClearColorRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.VolumeRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.StaticTriangleRenderer>();
        services.AddSingleton<ILSLDevelopShaderModuleService>();
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
        AddConnectionServices(services);
        AddRealtimeSimulationServices(services);
        AddRenderService(services);
        AddHeadlessServices(services);

        //services.AddScoped<InitializedClientContext>();

        //services.AddScoped(async sp =>
        //{
        //    var disposables = sp.GetRequiredService<ContextAsyncDisposableCollection>();
        //    var client = await JSClientModule.CreateAsync(sp.GetRequiredService<IJSRuntime>());
        //    disposables.Add(client);
        //    return client;
        //});

        //services.AddKeyedScoped(typeof(InitializedClientContext), (sp, level) =>
        //{
        //    var context = sp.GetRequiredService<InitializedClientContext>();
        //    return context.ClientModule ?? throw new NullReferenceException(nameof(context.ClientModule));
        //});

        //services.AddScoped<MediaDevices>();
        //services.AddScoped<JSClientModule>();
        //services.AddScoped<BrowserClient>();
        //services.AddScoped<IClient>(sp => sp.GetRequiredService<BrowserClient>());
        //services.AddScoped<CircuitService>();
        //services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<CircuitService>());
    }
}
