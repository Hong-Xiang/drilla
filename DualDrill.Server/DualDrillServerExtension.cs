using DualDrill.Engine.Connection;
using DualDrill.Engine.Media;
using DualDrill.Engine.Services;
using DualDrill.Graphics;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server;

public static class DualDrillServerExtension
{
    private static Ok<string[]> GetConnectedClients([FromServices] ClientStore clients)
    {
        return TypedResults.Ok(clients.ClientUris.Select(static s => s.ToString()).ToArray());
    }

    private static IResult CreatePeerConnectionToServerAsync(
        [FromRoute] Guid clientId,
        [FromServices] ISignalConnectionProviderService signalConnectionService,
        [FromServices] RTCPeerConnectionProviderService peerConnectionService
        )
    {
        signalConnectionService.CreateConnection(ClientStore.ServerId, clientId, SignalConnectionProvider.SignalR);
        peerConnectionService.CreatePeerConnection(clientId);
        return Results.Ok(null);
    }

    private static void AddConnectionServices(IServiceCollection services)
    {
        services.AddSingleton<ClientStore>();
        services.AddSingleton<ISignalConnectionProviderService, MessagePipeSignalConnectionProvider>();
        services.AddSingleton<ISignalConnectionOverSignalRService, SignalRConnectionMessagePushProviderService>();
        services.AddSingleton<RTCPeerConnectionProviderService>();
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
        services.AddSingleton<HeadlessSurfaceCaptureVideoSource>();
        services.AddSingleton<IFrameRenderService, FrameRenderService>();
        services.AddHostedService<DevicePollHostedService>();
        services.AddHostedService<RealtimeFrameHostedService>();
    }

    public static void AddDualDrillServerServices(this IServiceCollection services)
    {
        AddGraphicsServices(services);
        AddConnectionServices(services);
        AddRealtimeSimulationServices(services);

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

    public static void MapDualDrillApi(this WebApplication app)
    {
        app.MapGet("/api/clients", GetConnectedClients);
        app.MapPost("/api/peer-connection/server/{clientId}", CreatePeerConnectionToServerAsync);
    }
}
