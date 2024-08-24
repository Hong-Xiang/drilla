using DualDrill.Engine.Connection;
using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;
using DualDrill.Engine.Services;
using DualDrill.Graphics;
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

    private static void AddGraphicsServices(IServiceCollection services)
    {
        services.AddSingleton<GPUInstance>();
        services.AddSingleton(sp => sp.GetRequiredService<GPUInstance>().RequestAdapter(null));
        services.AddSingleton(sp => sp.GetRequiredService<GPUAdapter>().RequestDevice());
        services.AddSingleton(sp => sp.GetRequiredService<GPUDevice>().GetQueue());
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
        services.AddSingleton<DualDrill.Engine.Renderer.WebGPULogoRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.RotateCubeRenderer>();
        services.AddSingleton<DualDrill.Engine.Renderer.ClearColorRenderer>();
    }

    static void AddSingletonHostedService<T>(this IServiceCollection services)
        where T : class, IHostableBackgroundService
    {
        services.AddSingleton<T>();
        services.AddHostedService<SingletonHostedService<T>>();
    }


    public static void AddDualDrillServerServices(this IServiceCollection services)
    {
        AddGraphicsServices(services);
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
