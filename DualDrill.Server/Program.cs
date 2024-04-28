using DualDrill.Client.Pages;
using DualDrill.Server.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Collections.Concurrent;
using DualDrill.Engine.Connection;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;

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

        builder.Services.AddSingleton<DistributeXRConnectionService>();
        builder.Services.AddSingleton<DistributeXRApplication>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<DistributeXRApplication>());

        //builder.Services.AddHostedService<DistributeXRApplicationService>();
        builder.Services.AddClients();

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
        app.MapClients();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        app.MapGet("/webroot", () => app.Environment.WebRootPath);

        app.Run();
    }
}
