using DualDrill.Client.Pages;
using DualDrill.Server.Components;
using DualDrill.Server.BrowserClient;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Collections.Concurrent;
using DualDrill.Engine.Connection;

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

        builder.Services.AddSingleton<ClientHub>();

        builder.Services.AddScoped<BrowserClient.BrowserClient>();
        builder.Services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<BrowserClient.BrowserClient>());
        builder.Services.AddBrowserClients();


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

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapBrowserClients();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        app.MapGet("/webroot", () => app.Environment.WebRootPath);

        app.Run();
    }
}
