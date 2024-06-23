using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using DualDrill.Engine;
using DualDrill.Server.WebApi;
using DualDrill.Graphics;
using Microsoft.AspNetCore.ResponseCompression;
using DualDrill.Graphics.Headless;
using DualDrill.Server.WebView;

namespace DualDrill.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            WebRootPath = "../DualDrill.JS/dist"
        });


        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes =
  ResponseCompressionDefaults.MimeTypes.Concat(
      new[] { "application/x-frame-content" });
        });

        builder.Services.Configure<HeadlessSurface.Option>(options =>
        {
        });


        builder.Services.AddSingleton<DistributeXRConnectionService>();

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

        app.MapHub<DrillHub>("/hub/user-input");

        app.UseHttpsRedirection();
        app.UseResponseCompression();

        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapControllers();
        app.MapClients();
        app.MapRenderControls();

        app.MapRazorComponents<DualDrill.Server.Components.App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        app.MapGet("/webroot", () => app.Environment.WebRootPath);


        app.Run();
    }
}
