using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using DualDrill.Engine;
using DualDrill.Server.WebApi;
using DualDrill.Graphics;
using DualDrill.Graphics.Headless;
using DualDrill.Server.WebView;

namespace DualDrill.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            WebRootPath = "../DualDrill.JS/dist"
        });

        builder.Services.Configure<HeadlessSurface.Option>(options =>
        {
        });

        builder.Services.AddSingleton<PeerClientConnectionService>();

        //builder.Services.AddSingleton<WebGPUHeadlessService>();
        //builder.Services.AddSingleton<VulkanHeadlessService>();
        builder.Services.AddSingleton<DualDrill.Engine.Renderer.TriangleRenderer>();
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
        builder.Services.AddSingleton<IFrameService, FrameService>();
        builder.Services.AddHostedService<DevicePollHostedService>();
        builder.Services.AddHostedService<HeadlessRealtimeFrameHostedService>();

        builder.Services.AddSingleton<WebViewService>();
        builder.Services.AddHostedService<WebViewWindowHostedService>();
        //builder.Services.AddHostedService<RenderResultReaderTestService>();
        //builder.Services.AddHostedService<WebGPUNativeWindowService>();
        //builder.Services.AddHostedService<VulkanWindowService>();

        //builder.Services.AddHostedService<DistributeXRApplicationService>();
        builder.Services.AddClients();
        builder.Services.AddControllers();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        //builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        //builder.Host.ConfigureContainer<ContainerBuilder>((container) =>
        //     {
        //         container.Register(async (c) =>
        //         {
        //             return await JSClientModule.CreateAsync(c.Resolve<IJSRuntime>());
        //         }).OnRelease(async (client) => { await (await client).DisposeAsync(); });

        //         container.RegisterType<InitializedClientContext>().InstancePerMatchingLifetimeScope(InitializedClientContext.ScopeName);
        //     });


        var app = builder.Build();

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

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapControllers();
        app.MapClients();
        app.MapRenderControls();

        app.MapRazorComponents<DualDrill.Server.Components.App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(DualDrill.Client.SignalRService).Assembly);

        app.MapGet("/webroot", () => app.Environment.WebRootPath);


        await app.RunAsync();
    }
}
