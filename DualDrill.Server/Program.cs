using DualDrill.Engine.Headless;
using DualDrill.Server.Connection;
using DualDrill.Server.WebApi;
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

        var builder = WebApplication.CreateBuilder();

        builder.Services.Configure<HeadlessSurface.Option>(options =>
        {
        });

        builder.Services.AddMessagePipe();

        builder.Services.AddControllersWithViews(options =>
        {
            options.InputFormatters.Add(new PlainTextFormatter());
        });
        //builder.Services.AddControllers();
        builder.Services.AddHealthChecks();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        //builder.Services.AddOpenApi();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddDualDrillServerServices();


        var app = builder.Build();
        app.MapHealthChecks("health");
        app.MapGet("/webroot", () => app.Environment.WebRootPath);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.MapHub<DrillHub>("/hub/signal-connection");

        app.UseHttpsRedirection();

        app.UseWebSockets();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapControllers();
        app.MapRenderControls();
        //app.MapControllerRoute(
        //    name: "default",
        //    pattern: "{controller=Home}/{action=Index}/{id?}")
        //    .WithStaticAssets();

        app.UseSwagger();
        app.UseSwaggerUI();

        //app.MapOpenApi();

        //app.MapRazorComponents<DualDrill.Server.Components.App>()
        //    .AddInteractiveServerRenderMode()
        //    .AddInteractiveWebAssemblyRenderMode()
        //    .AddAdditionalAssemblies(typeof(DualDrill.Client._Imports).Assembly);



        await app.RunAsync();
    }
}
