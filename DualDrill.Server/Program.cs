using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using DualDrill.Engine;
using DualDrill.Server.WebApi;
using DualDrill.Graphics;
using DualDrill.Graphics.Headless;
using DualDrill.Server.Services;
using Serilog.Extensions.Logging;
using Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.FileProviders;

namespace DualDrill.Server;

public class Program
{
    public static async Task Main(string[] args)
    {

        var seriLogger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
              .WriteTo.Console()
              .CreateLogger();
        var factory = new SerilogLoggerFactory(seriLogger);
        SIPSorcery.LogFactory.Set(factory);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
        });

        builder.Services.Configure<HeadlessSurface.Option>(options =>
        {
        });

        builder.Services.AddMessagePipe();

        builder.Services.AddSingleton<PeerClientConnectionService>();

        builder.Services.AddSingleton<DualDrill.Engine.Renderer.SimpleColorRenderer>();
        builder.Services.AddSingleton<DualDrill.Engine.Renderer.RotateCubeRenderer>();
        builder.Services.AddSingleton<HeadlessSurface>(sp =>
        {
            var option = builder.Configuration.Get<HeadlessSurface.Option>();
            var wgpu = sp.GetRequiredService<WGPUProviderService>();
            return new HeadlessSurface(wgpu.Device, option);
        });
        builder.Services.AddSingleton<IGPUSurface>(sp => sp.GetRequiredService<HeadlessSurface>());

        builder.Services.AddSingleton<WGPUProviderService>();
        builder.Services.AddSingleton<GPUDevice>(sp => sp.GetRequiredService<WGPUProviderService>().Device);

        builder.Services.AddSingleton<FrameInputService>();
        builder.Services.AddSingleton<FrameSimulationService>();
        builder.Services.AddSingleton<RTCDemoVideoSource>();
        builder.Services.AddSingleton<IFrameService, FrameService>();
        builder.Services.AddHostedService<DevicePollHostedService>();
        builder.Services.AddHostedService<HeadlessRealtimeFrameHostedService>();

        builder.Services.AddHostedService<VideoPushHostedService>();
        //builder.Services.AddHostedService<RenderResultReaderTestService>();
        //builder.Services.AddHostedService<WebGPUNativeWindowService>();
        //builder.Services.AddHostedService<VulkanWindowService>();

        //builder.Services.AddHostedService<DistributeXRApplicationService>();
        builder.Services.AddClients();
        builder.Services.AddControllers();
        builder.Services.AddHealthChecks();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        var app = builder.Build();
        app.MapHealthChecks("health");

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.MapGet("/api/hello", () => "Hello");

        app.MapHub<DrillHub>("/hub/user-input");
        app.MapHub<DualDrillBrowserClientHub>("/hub/browser-client");

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapControllers();
        app.MapClients();
        app.MapRenderControls();


        app.MapRazorComponents<DualDrill.Server.Components.App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(DualDrill.Client._Imports).Assembly);

        app.MapGet("/webroot", () => app.Environment.WebRootPath);


        await app.RunAsync();
    }
}
