using DualDrill.Graphics;
using DualDrill.Server.Controllers;
using System.Text.Json;

namespace DualDrill.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddHttpClient();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.MaxDepth = 128;
            var converters = options.SerializerOptions.Converters;
            converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<GPUVertexFormat>(JsonNamingPolicy.SnakeCaseLower));
            converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<GPUVertexStepMode>(JsonNamingPolicy.SnakeCaseLower));
            converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<GPUIndexFormat>(JsonNamingPolicy.SnakeCaseLower));
        });

        builder.Services.AddControllersWithViews(options =>
        {
            options.InputFormatters.Add(new PlainTextFormatter());
        }).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.MaxDepth = 128;
            var converters = options.JsonSerializerOptions.Converters;
            converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<GPUVertexFormat>(JsonNamingPolicy.SnakeCaseLower));
            converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<GPUVertexStepMode>(JsonNamingPolicy.SnakeCaseLower));
            converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<GPUIndexFormat>(JsonNamingPolicy.SnakeCaseLower));
        });
        builder.Services.AddHealthChecks();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<DuckDBConnectionService>();

        await builder.Services.AddDualDrillServerServices(CancellationToken.None);


        var app = builder.Build();
        app.MapHealthChecks("health");
        app.MapGet("/webroot", () => app.Environment.WebRootPath);

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.MapControllers();

        app.UseSwagger();
        app.UseSwaggerUI();

        await app.RunAsync();
    }
}
