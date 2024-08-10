using DualDrill.Engine;
using DualDrill.Engine.Headless;
using DualDrill.Graphics;
using DualDrill.Server.WebApi;
using DualDrill.WebView;
using Serilog;
using Serilog.Extensions.Logging;

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

        builder.Services.AddControllersWithViews();

        builder.Services.AddSingleton<DualDrill.Engine.Renderer.SimpleColorRenderer>();
        builder.Services.AddSingleton<DualDrill.Engine.Renderer.RotateCubeRenderer>();
        builder.Services.AddSingleton<HeadlessSurface>();
        builder.Services.AddSingleton<IGPUSurface>(sp => sp.GetRequiredService<HeadlessSurface>());

        //builder.Services.AddSingleton<WGPUProviderService>();
        //builder.Services.AddSingleton<GPUDevice>(sp => sp.GetRequiredService<WGPUProviderService>().Device);

        builder.Services.AddSingleton<IWebViewService, WebViewService>();
        //builder.Services.AddHostedService<VideoPushHostedService>();
        //builder.Services.AddHostedService<RenderResultReaderTestService>();
        //builder.Services.AddHostedService<WebGPUNativeWindowService>();
        //builder.Services.AddHostedService<VulkanWindowService>();

        //builder.Services.AddHostedService<DistributeXRApplicationService>();
        builder.Services.AddDualDrillServerServices();
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
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.MapHub<DrillHub>("/hub/user-input");
        app.MapHub<DualDrillBrowserClientHub>("/hub/browser-client");

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapControllers();
        app.MapDualDrillApi();
        app.MapRenderControls();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

        //app.MapRazorComponents<DualDrill.Server.Components.App>()
        //    .AddInteractiveServerRenderMode()
        //    .AddInteractiveWebAssemblyRenderMode()
        //    .AddAdditionalAssemblies(typeof(DualDrill.Client._Imports).Assembly);

        app.MapGet("/webroot", () => app.Environment.WebRootPath);


        await app.RunAsync();
    }
}
