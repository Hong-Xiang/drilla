using Drilla.Server;
using Microsoft.AspNetCore.Mvc;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Encoders;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddSingleton<RTCClientsStore>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

var fService = new WeatherForecastService();

app.MapGet("/api/weatherforecast", () =>
{
    return fService.Forecast();
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapPost("/api/render/push", async (HttpRequest request, CancellationToken cancel, [FromServices] RTCClientsStore clients) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var sdp = await reader.ReadToEndAsync(cancel).ConfigureAwait(false);

    var pc = new RTCPeerConnection(null);
    pc.setRemoteDescription(
        new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = sdp
        });

    var ans = pc.createAnswer();
    await pc.setLocalDescription(ans).ConfigureAwait(false);
    var _ = new RTCPushClientConnection(pc, (channel) =>
    {
        if (clients.PushChannel is null)
        {
            clients.PushChannel = channel;
            clients.TryLinkClients();
        }
    });
    return ans.sdp;
});

app.MapPost("/api/render/pull", async (HttpRequest request, CancellationToken cancel, [FromServices] RTCClientsStore clients) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var sdp = await reader.ReadToEndAsync(cancel).ConfigureAwait(false);

    var pc = new RTCPeerConnection(null);
    pc.setRemoteDescription(
        new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = sdp
        });

    var ans = pc.createAnswer();
    await pc.setLocalDescription(ans).ConfigureAwait(false);
    var _ = new RTCPushClientConnection(pc, (channel) =>
    {
        if (clients.PullChannel is null)
        {
            clients.PullChannel = channel;
            clients.TryLinkClients();
        }
    });
    return ans.sdp;
});


app.MapHub<WebRTCPeerHub>("/hub/rtc");

app.UseHealthChecks("/health");
app.MapFallbackToFile("/index.html");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

sealed class WeatherForecastService
{
    public WeatherForecast[] Forecast()
    {// "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" 
        var summaries = new[] { "A", "B" };
        return Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
                .ToArray();
    }
}